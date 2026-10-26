using CombatExtended;
using RimWorld;
using UnityEngine;
using Verse;

namespace ElevatedGunPlatform
{
    /// <summary>3D line helpers shared by the elevated line-of-fire, sight and suppression checks (DESIGN 3).
    /// A line is a segment from a to b in CE's coordinates: x/z on the map, y = height (walls are 2.0).</summary>
    public static class ElevatedLine
    {
        /// <summary>The elevated turret behind a shooter (the turret, or the pawn manning it). Use for seers, target
        /// searchers and projectile launchers; a turret gun's verb caster is always the turret itself.</summary>
        public static Thing ElevatedShooter(Thing shooter) => EGP.ResolveElevated(shooter);

        /// <summary>The target itself if it is an elevated turret. A pawn manning one is not elevated.</summary>
        public static Thing ElevatedTarget(Thing target) => EGP.IsElevated(target) ? target : null;

        /// <summary>A target in the air (a drop pod, shuttle or other skyfaller, or a round in flight), as CE's
        /// interception (CIWS) aims at. Lines and sight to it keep CE's own rules for every shooter (DESIGN 3.2, 3.3).</summary>
        public static bool Airborne(Thing target) => target is Skyfaller || target is ProjectileCE;

        /// <summary>Cells from CE's line walk carry the start height in y; map lookups and comparisons need y = 0.</summary>
        public static IntVec3 Flat(IntVec3 cell) => new IntVec3(cell.x, 0, cell.z);

        /// <summary>Min and max height of segment a-b over the square of the given cell. Returns false when the
        /// segment does not cross the square (CE's walk also yields corner neighbours); then both heights are the
        /// height at the segment's point closest to the cell centre.</summary>
        public static bool HeightRangeInCell(Vector3 a, Vector3 b, IntVec3 cell, out float minY, out float maxY)
        {
            if (CrossingInCell(a, b, cell, out float t0, out float t1))
            {
                float y0 = Mathf.Lerp(a.y, b.y, t0);
                float y1 = Mathf.Lerp(a.y, b.y, t1);
                minY = Mathf.Min(y0, y1);
                maxY = Mathf.Max(y0, y1);
                return true;
            }
            minY = maxY = HeightNear(a, b, cell.ToVector3Shifted());
            return false;
        }

        /// <summary>Parameters (0..1) at which segment a-b enters and leaves the square of the given cell on the map
        /// plane. Returns false when the segment does not cross the square.</summary>
        public static bool CrossingInCell(Vector3 a, Vector3 b, IntVec3 cell, out float t0, out float t1)
        {
            t0 = 0f;
            t1 = 1f;
            return Clip(a.x, b.x - a.x, cell.x, cell.x + 1f, ref t0, ref t1)
                && Clip(a.z, b.z - a.z, cell.z, cell.z + 1f, ref t0, ref t1);
        }

        /// <summary>Height of segment a-b at its point closest (on the map plane) to p.</summary>
        public static float HeightNear(Vector3 a, Vector3 b, Vector3 p)
        {
            return Mathf.Lerp(a.y, b.y, ClosestT(a, b, p));
        }

        /// <summary>Parameter (0..1) of the point of segment a-b closest to p on the map plane.</summary>
        public static float ClosestT(Vector3 a, Vector3 b, Vector3 p)
        {
            float dx = b.x - a.x, dz = b.z - a.z;
            float len2 = dx * dx + dz * dz;
            if (len2 < 1e-6f)
            {
                return 0f;
            }
            return Mathf.Clamp01(((p.x - a.x) * dx + (p.z - a.z) * dz) / len2);
        }

        /// <summary>Top of the roof slab over a cell (CE: built 2.1, thin rock 4.0, overhead mountain 8.0; every other
        /// roof is a built one, see RoofSlabs), or negative infinity when the cell has no roof.</summary>
        public static float RoofTop(IntVec3 cell, Map map)
        {
            cell = Flat(cell);
            if (map == null || !cell.InBounds(map))
            {
                return float.NegativeInfinity;
            }
            RoofDef roof = cell.GetRoof(map);
            return roof == null ? float.NegativeInfinity : CE_Utility.GetBoundsFor(cell, roof).max.y;
        }

        /// <summary>A roofed cell blocks the line when the line is inside or below the roof slab there (DESIGN 3.2):
        /// its min height over the cell is below the slab top. A line passing above a built roof is not blocked.</summary>
        public static bool RoofBlocks(Vector3 a, Vector3 b, IntVec3 cell, Map map)
        {
            float top = RoofTop(cell, map);
            if (float.IsNegativeInfinity(top))
            {
                return false;
            }
            HeightRangeInCell(a, b, Flat(cell), out float minY, out _);
            return minY < top;
        }

        /// <summary>Roofs over an elevated turret's own cells (DESIGN 3.2). CE's walk skips the first crossed cell when
        /// the line starts on a cell boundary (the centre of an even-sized turret), so own cells are judged here alone
        /// (see InElevatedRect): a roof blocks where the line really passes through the cell, and where the line's
        /// end at this turret (endpoint: its muzzle, or its top for a target) lies inside or under the slab of a cell
        /// touching that point. A cell the line only touches at a corner above its slab doesn't count. Gives the
        /// blocking cell and the line's height there.</summary>
        public static bool OwnRoofBlocks(Thing elevated, Vector3 endpoint, Vector3 a, Vector3 b, Map map,
            out IntVec3 blockCell, out float minY)
        {
            blockCell = IntVec3.Invalid;
            minY = 0f;
            if (elevated == null || map == null)
            {
                return false;
            }
            foreach (IntVec3 cell in elevated.OccupiedRect())
            {
                float roofTop = RoofTop(cell, map);
                if (EndpointUnderRoof(endpoint, cell, roofTop))
                {
                    blockCell = cell;
                    minY = endpoint.y;
                    return true;
                }
                if (CrossedRoofBlocks(a, b, cell, roofTop, out minY))
                {
                    blockCell = cell;
                    return true;
                }
            }
            return false;
        }

        /// <summary>The point lies in the cell's closed square, at or below its roof slab top: a round starting
        /// there starts inside the slab (slabs start at wall height, which every muzzle on a platform is above).</summary>
        public static bool EndpointUnderRoof(Vector3 point, IntVec3 cell, float roofTop) =>
            !float.IsNegativeInfinity(roofTop) && point.y < roofTop
            && point.x >= cell.x && point.x <= cell.x + 1f && point.z >= cell.z && point.z <= cell.z + 1f;

        /// <summary>OwnRoofBlocks for both ends of a line a-b (a at the source); flashes the blocking cell in the
        /// debug overlay.</summary>
        public static bool OwnRoofsBlock(Thing elevatedSource, Thing elevatedTarget, Vector3 a, Vector3 b, Map map)
        {
            if (OwnRoofBlocks(elevatedSource, a, a, b, map, out IntVec3 cell, out float minY)
                || OwnRoofBlocks(elevatedTarget, b, a, b, map, out cell, out minY))
            {
                Flash(map, cell, 0f, minY);
                return true;
            }
            return false;
        }

        /// <summary>A cell of an elevated shooter's, seer's or target's footprint: its roof is judged by OwnRoofBlocks,
        /// not by the walk.</summary>
        public static bool InElevatedRect(IntVec3 cell, Thing elevatedSource, Thing elevatedTarget) =>
            (elevatedSource != null && elevatedSource.OccupiedRect().Contains(cell))
            || (elevatedTarget != null && elevatedTarget.OccupiedRect().Contains(cell));

        /// <summary>RoofBlocks for a cell the segment really crosses, with the roof slab top already looked up
        /// (negative infinity: no roof).</summary>
        public static bool CrossedRoofBlocks(Vector3 a, Vector3 b, IntVec3 cell, float roofTop, out float minY)
        {
            minY = 0f;
            if (float.IsNegativeInfinity(roofTop) || !CrossingInCell(a, b, cell, out float t0, out float t1) || t1 - t0 <= 1e-4f)
            {
                return false;
            }
            HeightRangeInCell(a, b, cell, out minY, out _);
            return minY < roofTop;
        }

        /// <summary>Debug overlay (DESIGN 3.6), dev mode only: CE's colours (0 blocked, 0.7 clear, 0.4 path) and the
        /// line's height as text.</summary>
        public static void Flash(Map map, IntVec3 cell, float colorPct, float height)
        {
            if (map == null || ElevatedGunPlatformMod.Settings == null || !ElevatedGunPlatformMod.Settings.DebugOverlayActive)
            {
                return;
            }
            map.debugDrawer.FlashCell(Flat(cell), colorPct, height.ToString("0.00"), 50);
        }

        public static bool DebugActive => ElevatedGunPlatformMod.Settings != null && ElevatedGunPlatformMod.Settings.DebugOverlayActive;

        // Liang-Barsky clip of the parameter range against one axis slab.
        private static bool Clip(float p, float d, float lo, float hi, ref float t0, ref float t1)
        {
            if (Mathf.Abs(d) < 1e-6f)
            {
                return p >= lo && p <= hi;
            }
            float ta = (lo - p) / d;
            float tb = (hi - p) / d;
            if (ta > tb)
            {
                float swap = ta;
                ta = tb;
                tb = swap;
            }
            if (ta > t0)
            {
                t0 = ta;
            }
            if (tb < t1)
            {
                t1 = tb;
            }
            return t0 <= t1;
        }
    }
}
