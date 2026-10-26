using System;
using System.Collections.Generic;
using CombatExtended;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ElevatedGunPlatform
{
    /// <summary>DESIGN 3.3 spotting (S1-S3): when the seer or the target is an elevated turret, sight is the 3D line
    /// from the seer's muzzle to the target's top instead of vanilla's flat line of sight.</summary>
    [HarmonyPatch(typeof(AttackTargetFinder), nameof(AttackTargetFinder.CanSee),
        new[] { typeof(Thing), typeof(Thing), typeof(Func<IntVec3, bool>) })]
    public static class SpottingPatch_CanSee
    {
        public static void Postfix(Thing seer, Thing target, Func<IntVec3, bool> validator, ref bool __result)
        {
            Thing elevatedSeer = ElevatedLine.ElevatedShooter(seer);
            if (elevatedSeer == null && ElevatedLine.ElevatedTarget(target) == null)
            {
                return;
            }
            if (ElevatedLine.Airborne(target))
            {
                CiwsTrace.AskedAboutAirborne("sight", seer, target, "vanilla's own answer");
                return;
            }
            Map map = seer?.Map;
            if (map == null || target == null || target.Map != map)
            {
                return;
            }
            __result = ElevatedSight.CanSee(seer, elevatedSeer ?? seer, target, validator, map);
        }
    }

    /// <summary>The 3D sight test of DESIGN 3.3: gas (the validator), roofs (as in 3.2) and buildings/cover (by CE
    /// bounds) block; pawns never do; leaning is ignored.</summary>
    public static class ElevatedSight
    {
        private const float DebugBlocked = 0f;
        private const float DebugClear = 0.7f;
        private const float DebugPath = 0.4f;

        // Reused cell buffers; a nested call (a validator that checks sight itself) gets its own.
        [ThreadStatic]
        private static List<IntVec3> cellBuffer;
        [ThreadStatic]
        private static HashSet<IntVec3> seenBuffer;
        [ThreadStatic]
        private static bool buffersInUse;

        /// <summary>seer is the thing asking (the turret, or the pawn manning it); seerEff is the thing whose muzzle
        /// the line starts from (the elevated turret it mans, else the seer itself).</summary>
        public static bool CanSee(Thing seer, Thing seerEff, Thing target, Func<IntVec3, bool> validator, Map map)
        {
            Vector3 seerPos = seerEff.DrawPos;
            Vector3 targetPos = target.DrawPos;
            Vector3 from = new Vector3(seerPos.x, new CollisionVertical(seerEff).shotHeight, seerPos.z);
            Vector3 to = new Vector3(targetPos.x, new CollisionVertical(target).Max, targetPos.z);

            bool nested = buffersInUse;
            List<IntVec3> cells;
            HashSet<IntVec3> seen;
            if (nested)
            {
                cells = new List<IntVec3>();
                seen = new HashSet<IntVec3>();
            }
            else
            {
                cells = cellBuffer ?? (cellBuffer = new List<IntVec3>());
                seen = seenBuffer ?? (seenBuffer = new HashSet<IntVec3>());
                cells.Clear();
                seen.Clear();
                buffersInUse = true;
            }
            try
            {
                CollectCells(from, to, cells, seen);
                return Walk(seer, seerEff, target, validator, map, from, to, cells);
            }
            finally
            {
                if (!nested)
                {
                    buffersInUse = false;
                }
            }
        }

        /// <summary>CE's cell walk, flattened to y = 0 and de-duplicated, plus the end cell (the walk can stop short of
        /// it, and its roof must still be checked).</summary>
        private static void CollectCells(Vector3 from, Vector3 to, List<IntVec3> cells, HashSet<IntVec3> seen)
        {
            foreach (IntVec3 cell in GenSightCE.PointsOnLineOfSight(from, to))
            {
                IntVec3 flat = ElevatedLine.Flat(cell);
                if (seen.Add(flat))
                {
                    cells.Add(flat);
                }
            }
            IntVec3 end = ElevatedLine.Flat(to.ToIntVec3());
            if (seen.Add(end))
            {
                cells.Add(end);
            }
        }

        private static bool Walk(Thing seer, Thing seerEff, Thing target, Func<IntVec3, bool> validator, Map map,
            Vector3 from, Vector3 to, List<IntVec3> cells)
        {
            CellRect seerRect = seerEff.OccupiedRect();
            CellRect ownerRect = seer == seerEff ? seerRect : seer.OccupiedRect();
            CellRect targetRect = target.OccupiedRect();
            IntVec3 fromCell = ElevatedLine.Flat(from.ToIntVec3());
            IntVec3 toCell = ElevatedLine.Flat(to.ToIntVec3());

            Vector3 dir = to - from;
            float length = dir.magnitude;
            bool hasSegment = length > 1e-4f;
            Ray ray = hasSegment ? new Ray(from, dir) : default(Ray);
            bool debug = ElevatedLine.DebugActive;
            Thing lastClear = null;

            // Roofs over an elevated seer's or target's own cells: those the line crosses (CE's walk can skip one).
            Thing elevatedSeer = EGP.IsElevated(seerEff) ? seerEff : null;
            Thing elevatedTarget = ElevatedLine.ElevatedTarget(target);
            if (ElevatedLine.OwnRoofsBlock(elevatedSeer, elevatedTarget, from, to, map))
            {
                return false;
            }

            for (int i = 0; i < cells.Count; i++)
            {
                IntVec3 cell = cells[i];
                if (!cell.InBounds(map))
                {
                    continue;
                }
                float status = DebugPath;
                bool blocked = false;
                if (!ElevatedLine.InElevatedRect(cell, elevatedSeer, elevatedTarget) && ElevatedLine.RoofBlocks(from, to, cell, map))
                {
                    blocked = true;
                }
                else if (cell != fromCell && cell != toCell && !seerRect.Contains(cell) && !ownerRect.Contains(cell)
                    && !targetRect.Contains(cell))
                {
                    if (validator != null && !validator(cell))
                    {
                        blocked = true;
                    }
                    else if (hasSegment)
                    {
                        Thing edifice = cell.GetEdifice(map);
                        blocked = TestObstacle(edifice, seer, seerEff, target, seerRect, ownerRect, targetRect, from, ray,
                            length, ref lastClear, ref status);
                        if (!blocked)
                        {
                            Thing cover = cell.GetCover(map);
                            blocked = cover != edifice && TestObstacle(cover, seer, seerEff, target, seerRect, ownerRect,
                                targetRect, from, ray, length, ref lastClear, ref status);
                        }
                    }
                }
                if (debug)
                {
                    ElevatedLine.HeightRangeInCell(from, to, cell, out float minY, out _);
                    ElevatedLine.Flash(map, cell, blocked ? DebugBlocked : status, minY);
                }
                if (blocked)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>True when the thing is an obstacle whose CE bounds the segment crosses. Sets status to clear when an
        /// obstacle was tested and passed.</summary>
        private static bool TestObstacle(Thing thing, Thing seer, Thing seerEff, Thing target, CellRect seerRect,
            CellRect ownerRect, CellRect targetRect, Vector3 from, Ray ray, float length, ref Thing lastClear, ref float status)
        {
            if (thing == null || thing == lastClear || thing is Pawn || thing == seer || thing == seerEff || thing == target
                || thing is Plant || thing.def.category == ThingCategory.Plant)
            {
                return false;
            }
            IntVec3 pos = thing.Position;
            if (seerRect.Contains(pos) || ownerRect.Contains(pos) || targetRect.Contains(pos))
            {
                return false;
            }
            Bounds bounds = CE_Utility.GetBoundsFor(thing);
            if (bounds.Contains(from) || (bounds.IntersectRay(ray, out float dist) && dist <= length))
            {
                return true;
            }
            // Multi-cell obstacles show up in several cells of the walk; test each once.
            lastClear = thing;
            status = DebugClear;
            return false;
        }
    }
}
