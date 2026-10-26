using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using CombatExtended;
using RimWorld;
using UnityEngine;
using Verse;

namespace ElevatedGunPlatform
{
    /// <summary>DESIGN 3.8: while the player has switched it on and an elevated turret is selected, outline where it
    /// can hit a standing human. For every candidate cell the answer is the line-of-fire check's (DESIGN 3.2,
    /// LineOfFire.cs) for a human standing there, minus other pawns: the line from the muzzle (CE's shot source and shot
    /// height) to the top of the human (CE: 1.0, raised by the low cover it stands in) at the cell centre, walked with
    /// CE's own GenSightCE.PointsOnLineOfSight to the cell, heights from ElevatedLine.HeightRangeInCell. A walked cell
    /// blocks when the line in it is below its roof slab top, or, unless it is the source or target cell, when the line
    /// crosses it at or below the CE height of its cover (walls, rock, closed doors, platforms and other elevated
    /// turrets at this mod's heights, sandbags and other low cover; plants never, low cover next to the turret never,
    /// as in CE); the turret's own cells are judged only by their roofs (ElevatedLine.OwnRoofBlocks). Because CE's walk
    /// is costly, a cheap grid walk first clears lines that stay well above every obstacle near them; only the others
    /// take CE's walk. Candidates are within range (the smaller of the order and firing ranges) and outside min range
    /// of the turret's position, walkable, not a doorway, unroofed, not fogged and not its own. The fire arc filters the finished
    /// result. The work is spread over frames within a shared time budget.</summary>
    public static class FiringZone
    {
        /// <summary>CE height of a standing human (BoundsInjector.ForPawn: humanlike 0.5 wide, 1.0 tall), 1.75 m.</summary>
        public const float HumanTop = 1f;

        /// <summary>A finished outline is recomputed after this long (real time, so also while paused).</summary>
        private const float RefreshSeconds = 1f;
        /// <summary>A computation whose turret hasn't been drawn for this long and for more than AbandonFrames frames
        /// (deselected) is dropped. Both, so one long frame (an autosave) doesn't throw the work away.</summary>
        private const float AbandonSeconds = 0.5f;
        private const int AbandonFrames = 2;
        /// <summary>After an error, the turret's outline is not tried again for this long.</summary>
        private const float FailureBackoffSeconds = 5f;
        /// <summary>While the arc changes (being edited), the drawn cells are re-filtered at most this often.</summary>
        private const float ArcFilterSeconds = 0.1f;
        /// <summary>Time all selected turrets' outlines may take together in one frame.</summary>
        private const double FrameBudgetMilliseconds = 2.5;
        /// <summary>Cheap cells handled between two looks at the clock; after each CE walk it is read at once.</summary>
        private const int CellsPerClockCheck = 16;
        private const int MaxPooledBuffers = 4;
        private static readonly Color EdgeColor = new Color(0.45f, 0.8f, 1f);

        private static readonly long FrameBudgetTicks = (long)(FrameBudgetMilliseconds * Stopwatch.Frequency / 1000.0);

        // Shared per-frame budget. Jobs share it equally (by last frame's count), and a job that overran its share
        // pays it back in later frames; jobs of turrets that show no outline yet go first: while any ran last frame,
        // refreshes wait.
        private static int budgetFrame = -1;
        private static float budgetTime = float.NaN;
        private static long budgetUsedTicks;
        private static int jobsThisFrame;
        private static int jobsLastFrame;
        private static int firstJobsThisFrame;
        private static int firstJobsLastFrame;
        private static bool warnedFailure;

        private static readonly Stack<Buffers> pool = new Stack<Buffers>();
        // Unfinished computations, so those of turrets no longer drawn give their buffers back.
        private static readonly List<Job> activeJobs = new List<Job>();

        // CE's private CompFireArc.functionEnabled (the arc is on): WithinFireArc is all-true while it is off.
        // Resolved on first use, with plain reflection, so a change in CE or Harmony can't break the type.
        private static bool arcEnabledResolved;
        private static Func<CompFireArc, bool> arcEnabled;

        /// <summary>What an outline depends on, besides the map's contents and the fire arc. Holds no Map.</summary>
        internal struct Key
        {
            public int mapId;
            public IntVec3 position;
            public Rot4 rotation;
            public float muzzle;
            public float range;
            public float minRange;

            public bool SamePlace(Key other) => mapId == other.mapId && position == other.position && rotation == other.rotation;

            public bool Same(Key other) =>
                SamePlace(other) && muzzle == other.muzzle && range == other.range && minRange == other.minRange;
        }

        /// <summary>One turret's outline and its computation in progress, kept on its record comp (transient) and
        /// dropped when the turret despawns.</summary>
        public sealed class Cache
        {
            // Finished result: cells the turret can hit, ignoring the fire arc.
            internal readonly List<IntVec3> reachable = new List<IntVec3>();
            internal bool hasResult;
            internal Key resultKey;
            internal float resultTime;
            internal int resultVersion;

            // What is drawn: the result within the fire arc.
            internal readonly List<IntVec3> cells = new List<IntVec3>();
            internal int cellsVersion = -1;
            internal float cellsTime;
            internal float cellsArcCenter;
            internal float cellsArcSpan;
            internal bool cellsArcOn;

            internal float failedUntil = float.NegativeInfinity;
            internal readonly Job job = new Job();
        }

        internal sealed class Job
        {
            public bool active;
            public float lastSeen;
            public int lastSeenFrame;
            /// <summary>Time this job spent beyond its shares, paid back by skipping later frames' shares.</summary>
            public long debtTicks;
            public Key key;
            public CellRect rect;
            public int width;
            public int count;
            /// <summary>Next step: [0, count) fills the grids, [count, 2 count) spreads the obstacle tops to
            /// neighbours, [2 count, 3 count) tests the lines.</summary>
            public int next;
            public Vector3 source;
            public IntVec3 sourceCell;
            public CellRect own;
            public Buffers buffers;
            public readonly List<IntVec3> result = new List<IntVec3>();
        }

        /// <summary>Per-cell grids of one computation, pooled between computations.</summary>
        internal sealed class Buffers
        {
            /// <summary>Roof slab top, or negative infinity.</summary>
            public float[] roofTop = new float[0];
            /// <summary>CE height of the cover that can block a line through the cell (not the turret, not a plant,
            /// not low cover next to the turret), or 0.</summary>
            public float[] coverTop = new float[0];
            /// <summary>Top of a human standing in the cell: HumanTop, raised by low cover there (as CE does).</summary>
            public float[] targetTop = new float[0];
            /// <summary>Highest obstacle (roof or cover, the turret's own cells left out) in the cell and its eight
            /// neighbours, or negative infinity.</summary>
            public float[] nearTop = new float[0];
            public bool[] candidate = new bool[0];

            public void Ensure(int count)
            {
                if (roofTop.Length < count)
                {
                    roofTop = new float[count];
                    coverTop = new float[count];
                    targetTop = new float[count];
                    nearTop = new float[count];
                    candidate = new bool[count];
                }
            }
        }

        public static void Draw(Building_TurretGunCE turret, ref Cache cache)
        {
            if (cache == null)
            {
                cache = new Cache();
            }
            float now = RealTime.LastRealTime;
            // The second test copes with the clock starting over.
            if (now < cache.failedUntil && now >= cache.failedUntil - FailureBackoffSeconds)
            {
                return;
            }
            try
            {
                Map map = turret.Map;
                if (!TryGetKey(turret, map, out Key key))
                {
                    return;
                }
                Advance(turret, map, cache, key, now);
                // A result for another place (the turret was moved) is never shown; a slightly older one is.
                if (!cache.hasResult || !cache.resultKey.SamePlace(key))
                {
                    return;
                }
                ApplyArc(turret.CompFireArc, cache, now);
                if (cache.cells.Count > 0)
                {
                    GenDraw.DrawFieldEdges(cache.cells, EdgeColor);
                }
            }
            catch (Exception e)
            {
                Abandon(cache.job);
                cache.failedUntil = now + FailureBackoffSeconds;
                if (!warnedFailure)
                {
                    warnedFailure = true;
                    Log.Warning("[ElevatedGunPlatform] Firing zone overlay failed; it is skipped for a few seconds: " + e);
                }
            }
        }

        /// <summary>Drops a turret's outline and returns its grids (the turret despawned).</summary>
        public static void Release(Cache cache)
        {
            if (cache != null)
            {
                Abandon(cache.job);
            }
        }

        private static bool TryGetKey(Building_TurretGunCE turret, Map map, out Key key)
        {
            key = default(Key);
            // The verb of CE's range ring and OrderAttack (AttackVerb can switch on multi-verb turrets).
            Verb verb = turret.GunCompEq?.PrimaryVerb;
            if (map == null || verb?.verbProps == null)
            {
                return false;
            }
            key.mapId = map.uniqueID;
            key.position = turret.Position;
            key.rotation = turret.Rotation;
            key.muzzle = new CollisionVertical(turret).shotHeight;
            // OrderAttack accepts up to verbProps.range; firing needs the verb's effective range (ammo, gun stats).
            key.range = Mathf.Min(verb.verbProps.range, verb.EffectiveRange);
            key.minRange = verb.verbProps.minRange;
            return true;
        }

        /// <summary>Starts a computation when the result is missing, outdated or for another key, and runs the
        /// current one within this frame's share of the budget.</summary>
        private static void Advance(Building_TurretGunCE turret, Map map, Cache cache, Key key, float now)
        {
            Job job = cache.job;
            if (job.active && (!job.key.Same(key) || now < job.lastSeen || Unseen(job, now)))
            {
                Abandon(job);
            }
            if (!job.active)
            {
                bool due = !cache.hasResult || !cache.resultKey.Same(key) || now - cache.resultTime >= RefreshSeconds
                    || now < cache.resultTime;
                if (!due)
                {
                    return;
                }
                Start(job, turret, key);
            }
            job.lastSeen = now;
            job.lastSeenFrame = RealTime.frameCount;
            bool first = !cache.hasResult || !cache.resultKey.SamePlace(key);
            if (Run(job, turret, map, first))
            {
                cache.reachable.Clear();
                cache.reachable.AddRange(job.result);
                cache.hasResult = true;
                cache.resultKey = key;
                cache.resultTime = now;
                cache.resultVersion++;
                Abandon(job);
            }
        }

        private static void Start(Job job, Building_TurretGunCE turret, Key key)
        {
            job.active = true;
            job.key = key;
            job.next = 0;
            job.result.Clear();
            Vector3 drawPos = turret.DrawPos;
            // CE's shot source for turrets (Verb_LaunchProjectileCE.ShotSource) and LineOfFire's source cell.
            job.source = new Vector3(drawPos.x, key.muzzle, drawPos.z);
            job.sourceCell = ElevatedLine.Flat(job.source.ToIntVec3());
            job.own = turret.OccupiedRect();
            // One cell beyond range: CE's walk can yield a cell next to the line.
            int radius = Mathf.CeilToInt(Mathf.Max(0f, key.range)) + 1;
            job.rect = CellRect.CenteredOn(key.position, radius).ClipInsideMap(turret.Map);
            job.width = job.rect.Width;
            job.count = job.width * job.rect.Height;
            job.debtTicks = 0;
            job.lastSeenFrame = RealTime.frameCount;
            activeJobs.Add(job);
        }

        private static bool Unseen(Job job, float now) =>
            RealTime.frameCount - job.lastSeenFrame > AbandonFrames && now - job.lastSeen > AbandonSeconds;

        private static void Abandon(Job job)
        {
            if (job.active)
            {
                activeJobs.Remove(job);
            }
            job.active = false;
            job.result.Clear();
            if (job.buffers != null)
            {
                if (pool.Count < MaxPooledBuffers)
                {
                    pool.Push(job.buffers);
                }
                job.buffers = null;
            }
        }

        /// <summary>Works on the job within its share of this frame's budget. True when done.</summary>
        private static bool Run(Job job, Building_TurretGunCE turret, Map map, bool first)
        {
            // A new frame: Verse's frame counter or its real-time clock moved on.
            int frame = RealTime.frameCount;
            float time = RealTime.LastRealTime;
            if (frame != budgetFrame || time != budgetTime)
            {
                budgetFrame = frame;
                budgetTime = time;
                budgetUsedTicks = 0;
                jobsLastFrame = jobsThisFrame;
                firstJobsLastFrame = firstJobsThisFrame;
                jobsThisFrame = 0;
                firstJobsThisFrame = 0;
                // Turrets no longer drawn (deselected) give their buffers back.
                for (int k = activeJobs.Count - 1; k >= 0; k--)
                {
                    if (activeJobs[k] != job && Unseen(activeJobs[k], time))
                    {
                        Abandon(activeJobs[k]);
                    }
                }
            }
            jobsThisFrame++;
            if (first)
            {
                firstJobsThisFrame++;
            }
            if (job.key.muzzle <= 0f || job.key.range <= 0f)
            {
                job.result.Clear();
                return true;
            }
            if (!first && firstJobsLastFrame > 0)
            {
                return false;
            }
            long share = FrameBudgetTicks / Math.Max(1, first ? firstJobsLastFrame : jobsLastFrame);
            if (job.debtTicks >= share)
            {
                job.debtTicks -= share;
                return false;
            }
            long allowance = Math.Min(share - job.debtTicks, FrameBudgetTicks - budgetUsedTicks);
            if (allowance <= 0)
            {
                return false;
            }
            if (job.buffers == null)
            {
                job.buffers = pool.Count > 0 ? pool.Pop() : new Buffers();
                job.buffers.Ensure(job.count);
            }
            long start = Stopwatch.GetTimestamp();
            long spent = 0;
            try
            {
                int total = job.count * 3;
                int sinceCheck = 0;
                while (job.next < total)
                {
                    int i = job.next % job.count;
                    bool costly = false;
                    if (job.next < job.count)
                    {
                        FillCell(job, turret, map, i);
                    }
                    else if (job.next < job.count * 2)
                    {
                        SpreadCell(job, i);
                    }
                    else
                    {
                        costly = TestCell(job, i);
                    }
                    job.next++;
                    if (costly || ++sinceCheck >= CellsPerClockCheck)
                    {
                        sinceCheck = 0;
                        if (Stopwatch.GetTimestamp() - start >= allowance)
                        {
                            break;
                        }
                    }
                }
                return job.next >= total;
            }
            finally
            {
                spent = Stopwatch.GetTimestamp() - start;
                budgetUsedTicks += spent;
                job.debtTicks = Math.Max(0, job.debtTicks + spent - share);
            }
        }

        private static IntVec3 CellAt(Job job, int i) => new IntVec3(job.rect.minX + i % job.width, 0, job.rect.minZ + i / job.width);

        private static void FillCell(Job job, Building_TurretGunCE turret, Map map, int i)
        {
            Buffers buf = job.buffers;
            IntVec3 cell = CellAt(job, i);
            float roofTop = ElevatedLine.RoofTop(cell, map);
            bool roofed = !float.IsNegativeInfinity(roofTop);
            // LineOfFire tests the cell's cover (GetCover; plants and the turret itself never block). A full-fillage
            // edifice counts too, in case the cover grid holds something else there. CE: 2.0 for walls and rock, 0 for
            // an open door, fillPercent for low cover; this mod's heights for platforms and E.
            float coverTop = 0f;
            float lowCover = 0f;
            Thing cover = cell.GetCover(map);
            if (cover != null && cover != turret && !cover.IsPlant())
            {
                float top = new CollisionVertical(cover).Max;
                if (cover.def.Fillage == FillCategory.Full)
                {
                    coverTop = top;
                }
                else
                {
                    lowCover = top;
                    // CE lets a shot pass low cover next to the shooter when the shot starts above it.
                    if (!job.own.ExpandedBy(1).Contains(cell) || top >= job.key.muzzle)
                    {
                        coverTop = top;
                    }
                }
            }
            Building edifice = cell.GetEdifice(map);
            if (edifice != null && edifice != turret && edifice != cover && edifice.def.Fillage == FillCategory.Full)
            {
                coverTop = Mathf.Max(coverTop, new CollisionVertical(edifice).Max);
            }
            buf.roofTop[i] = roofTop;
            buf.coverTop[i] = coverTop;
            // CE raises a pawn standing in a cover cell by that cover's height.
            buf.targetTop[i] = HumanTop + lowCover;
            float distSq = (cell - job.key.position).LengthHorizontalSquared;
            // Anywhere a human can walk: trees, chunks and sandbags included (vanilla's Standable leaves those out),
            // doorways not (a pawn only passes through them).
            buf.candidate[i] = !roofed && distSq <= job.key.range * job.key.range && distSq >= job.key.minRange * job.key.minRange
                && !job.own.Contains(cell) && cell.Walkable(map) && cell.GetDoor(map) == null && !cell.Fogged(map);
        }

        /// <summary>The highest obstacle among a cell and its neighbours, for the cheap first test.</summary>
        private static void SpreadCell(Job job, int i)
        {
            Buffers buf = job.buffers;
            int x = i % job.width;
            int z = i / job.width;
            int height = job.count / job.width;
            float highest = float.NegativeInfinity;
            for (int nz = Math.Max(0, z - 1); nz <= Math.Min(height - 1, z + 1); nz++)
            {
                for (int nx = Math.Max(0, x - 1); nx <= Math.Min(job.width - 1, x + 1); nx++)
                {
                    int j = nz * job.width + nx;
                    if (job.own.Contains(new IntVec3(job.rect.minX + nx, 0, job.rect.minZ + nz)))
                    {
                        continue;
                    }
                    highest = Mathf.Max(highest, buf.roofTop[j]);
                    if (buf.coverTop[j] > 0f)
                    {
                        highest = Mathf.Max(highest, buf.coverTop[j]);
                    }
                }
            }
            buf.nearTop[i] = highest;
        }

        /// <summary>Tests one candidate's line; true when that took CE's (costly) walk.</summary>
        private static bool TestCell(Job job, int i)
        {
            if (!job.buffers.candidate[i])
            {
                return false;
            }
            IntVec3 target = CellAt(job, i);
            Vector3 a = job.source;
            // LineOfFire walks to the target cell's centre on the ground and takes heights to the target's top there.
            Vector3 ground = target.ToVector3Shifted();
            Vector3 b = new Vector3(ground.x, job.buffers.targetTop[i], ground.z);
            if (OwnRoofsBlock(job, a, b))
            {
                return false;
            }
            if (SurelyClear(job, a, b, target))
            {
                job.result.Add(target);
                return false;
            }
            if (WalkClear(job, a, b, ground, target))
            {
                job.result.Add(target);
            }
            return true;
        }

        /// <summary>ElevatedLine.OwnRoofBlocks on the grid (the muzzle is the turret's end of the line).</summary>
        private static bool OwnRoofsBlock(Job job, Vector3 a, Vector3 b)
        {
            foreach (IntVec3 cell in job.own)
            {
                if (!InRect(job, cell, out int index))
                {
                    continue;
                }
                float roofTop = job.buffers.roofTop[index];
                if (ElevatedLine.EndpointUnderRoof(a, cell, roofTop) || ElevatedLine.CrossedRoofBlocks(a, b, cell, roofTop, out _))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Cheap, conservative test (Amanatides-Woo grid walk over the crossed cells): true only when the line
        /// stays above every obstacle next to it by a margin, so that CE's walk, whose cells all lie next to the
        /// crossed ones, can't find a block either. False means "don't know".</summary>
        private static bool SurelyClear(Job job, Vector3 a, Vector3 b, IntVec3 target)
        {
            float m = a.y;
            float drop = m - b.y;
            float dx = b.x - a.x;
            float dz = b.z - a.z;
            float length = Mathf.Sqrt(dx * dx + dz * dz);
            if (length < 1e-3f)
            {
                return false;
            }
            // How far the line drops over two cells: covers heights CE's walk takes next to the crossed cells.
            float margin = 2f * Mathf.Abs(drop) / length + 0.01f;
            int x = Mathf.FloorToInt(a.x);
            int z = Mathf.FloorToInt(a.z);
            int stepX = dx > 0f ? 1 : -1;
            int stepZ = dz > 0f ? 1 : -1;
            float tDeltaX = Mathf.Abs(dx) > 1e-6f ? 1f / Mathf.Abs(dx) : float.PositiveInfinity;
            float tDeltaZ = Mathf.Abs(dz) > 1e-6f ? 1f / Mathf.Abs(dz) : float.PositiveInfinity;
            float tMaxX = float.IsPositiveInfinity(tDeltaX) ? float.PositiveInfinity : (dx > 0f ? x + 1 - a.x : a.x - x) * tDeltaX;
            float tMaxZ = float.IsPositiveInfinity(tDeltaZ) ? float.PositiveInfinity : (dz > 0f ? z + 1 - a.z : a.z - z) * tDeltaZ;
            int maxSteps = Math.Abs(target.x - x) + Math.Abs(target.z - z) + 2;
            float[] nearTop = job.buffers.nearTop;
            for (int n = 0; n <= maxSteps; n++)
            {
                bool atTarget = x == target.x && z == target.z;
                float tExit = atTarget ? 1f : Mathf.Min(1f, Mathf.Min(tMaxX, tMaxZ));
                if (!InRect(job, new IntVec3(x, 0, z), out int index) || m - drop * tExit - margin <= nearTop[index])
                {
                    return false;
                }
                if (atTarget)
                {
                    return true;
                }
                if (tMaxX < tMaxZ)
                {
                    x += stepX;
                    tMaxX += tDeltaX;
                }
                else
                {
                    z += stepZ;
                    tMaxZ += tDeltaZ;
                }
            }
            return false;
        }

        /// <summary>LineOfFire.CanHitCellFromCell for a human at the target cell, with CE's own walk (to the cell's
        /// ground centre, as LineOfFire walks); heights on the line a-b to the human's top; other pawns are left out.</summary>
        private static bool WalkClear(Job job, Vector3 a, Vector3 b, Vector3 ground, IntVec3 target)
        {
            Buffers buf = job.buffers;
            foreach (IntVec3 walked in GenSightCE.PointsOnLineOfSight(a, ground))
            {
                IntVec3 cell = ElevatedLine.Flat(walked);
                // The turret's own cells: its body never blocks, their roofs were judged by OwnRoofsBlock.
                if (!InRect(job, cell, out int index) || job.own.Contains(cell))
                {
                    continue;
                }
                bool crossing = ElevatedLine.HeightRangeInCell(a, b, cell, out float minY, out _);
                // ElevatedLine.RoofBlocks, on every walked cell.
                if (minY < buf.roofTop[index])
                {
                    return false;
                }
                if (cell == job.sourceCell || cell == target)
                {
                    continue;
                }
                // CE's cover test: passed when the cover is below the line (HighestCoverAt < minY), else the shot
                // ray against the cover's bounds, assumed to count a ray grazing the top as a hit.
                float top = buf.coverTop[index];
                if (top > 0f && crossing && minY <= top)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool InRect(Job job, IntVec3 cell, out int index)
        {
            CellRect rect = job.rect;
            if (cell.x < rect.minX || cell.x > rect.maxX || cell.z < rect.minZ || cell.z > rect.maxZ)
            {
                index = -1;
                return false;
            }
            index = (cell.z - rect.minZ) * job.width + (cell.x - rect.minX);
            return true;
        }

        /// <summary>The drawn cells: the result within the fire arc (the arc being edited, while editing).</summary>
        private static void ApplyArc(CompFireArc arc, Cache cache, float now)
        {
            bool on = arc != null && ArcOn(arc);
            float center = on ? arc.CenterAngle : 0f;
            float span = on ? arc.Span : 0f;
            float age = now - cache.cellsTime;
            bool newResult = cache.cellsVersion != cache.resultVersion;
            bool arcChanged = on != cache.cellsArcOn || cache.cellsArcCenter != center || cache.cellsArcSpan != span;
            // Switching the arc on or off shows at once; dragging it re-filters at most every ArcFilterSeconds.
            if (!newResult && on == cache.cellsArcOn && !(arcChanged && age >= ArcFilterSeconds) && age >= 0f)
            {
                return;
            }
            cache.cellsVersion = cache.resultVersion;
            cache.cellsTime = now;
            cache.cellsArcOn = on;
            cache.cellsArcCenter = center;
            cache.cellsArcSpan = span;
            cache.cells.Clear();
            List<IntVec3> reachable = cache.reachable;
            if (!on)
            {
                cache.cells.AddRange(reachable);
                return;
            }
            for (int i = 0; i < reachable.Count; i++)
            {
                if (arc.WithinFireArc(new LocalTargetInfo(reachable[i])))
                {
                    cache.cells.Add(reachable[i]);
                }
            }
        }

        private static bool ArcOn(CompFireArc arc)
        {
            if (!arcEnabledResolved)
            {
                arcEnabledResolved = true;
                arcEnabled = MakeArcEnabled();
            }
            if (arcEnabled == null)
            {
                return true;
            }
            try
            {
                return arcEnabled(arc);
            }
            catch (Exception)
            {
                return true;
            }
        }

        private static Func<CompFireArc, bool> MakeArcEnabled()
        {
            try
            {
                MethodInfo getter = typeof(CompFireArc).GetProperty("functionEnabled",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetGetMethod(true);
                return getter != null && getter.ReturnType == typeof(bool) && getter.GetParameters().Length == 0
                    ? (Func<CompFireArc, bool>)Delegate.CreateDelegate(typeof(Func<CompFireArc, bool>), getter, false)
                    : null;
            }
            catch (Exception)
            {
                // Treated as always on: the arc filter then runs whenever the arc changes.
                return null;
            }
        }
    }
}
