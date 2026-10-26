using System;
using System.Collections.Generic;
using System.Reflection;
using CombatExtended;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace ElevatedGunPlatform
{
    /// <summary>DESIGN 3.2 (aim): CE aims at a pawn's centre mass, or above partial cover next to the target. A line
    /// from a raised muzzle can clear a wall partway along the shot by the target's top but not by its centre, so CE's
    /// aim would put every round into that wall. For an elevated shooter the aim is raised to the lowest height that
    /// clears every full-height obstacle and roof slab on the line, the way CE aims at the visible part of a target
    /// behind partial cover. Nothing changes when nothing is in the way or when the target is not visible at all.
    /// CE puts the returned height at its own aim point (after range estimate, visibility shift and lead) and fires
    /// from the caster's TrueCenter at ShotHeight towards it, so the rule works on that line: the line it clears is
    /// the line the round flies.</summary>
    [HarmonyPatch(typeof(Verb_LaunchProjectileCE), nameof(Verb_LaunchProjectileCE.GetTargetHeight))]
    public static class AimPatch_GetTargetHeight
    {
        private const float Margin = 0.05f;

        // protected Vector2 sourceLoc, newTargetLoc: the shot's start and aim point on the map plane. ShiftTarget, the
        // only caller of GetTargetHeight, sets both just before the call, then aims from sourceLoc at ShotHeight to
        // newTargetLoc at the returned height.
        private static readonly AccessTools.FieldRef<Verb_LaunchProjectileCE, Vector2> SourceLoc = ResolveLoc("sourceLoc");
        private static readonly AccessTools.FieldRef<Verb_LaunchProjectileCE, Vector2> NewTargetLoc = ResolveLoc("newTargetLoc");

        private static readonly HashSet<IntVec3> seen = new HashSet<IntVec3>();

        private static AccessTools.FieldRef<Verb_LaunchProjectileCE, Vector2> ResolveLoc(string name)
        {
            FieldInfo field = AccessTools.Field(typeof(Verb_LaunchProjectileCE), name);
            if (field == null || field.IsStatic || field.FieldType != typeof(Vector2))
            {
                return null;
            }
            try
            {
                return AccessTools.FieldRefAccess<Verb_LaunchProjectileCE, Vector2>(field);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static bool Prepare()
        {
            if (SourceLoc == null || NewTargetLoc == null)
            {
                MountingUtility.WarnOnce("GetTargetHeight",
                    "CombatExtended.Verb_LaunchProjectileCE.sourceLoc or newTargetLoc not found; elevated turrets keep CE's aim height over walls.");
                return false;
            }
            return true;
        }

        public static void Postfix(Verb_LaunchProjectileCE __instance, LocalTargetInfo target, ref float __result)
        {
            Thing caster = __instance.caster;
            if (!EGP.IsElevated(caster) || !target.HasThing)
            {
                return;
            }
            if (__instance.projectilePropsCE != null && __instance.projectilePropsCE.flyOverhead)
            {
                return;
            }
            Map map = caster.Map;
            Thing targetThing = target.Thing;
            if (map == null || targetThing.Map != map)
            {
                return;
            }
            // CE's shot: from source at height m, through its aim point (aimDistance along dir) at height __result.
            Vector2 source = SourceLoc(__instance);
            Vector2 chord = NewTargetLoc(__instance) - source;
            float aimDistance = chord.magnitude;
            if (aimDistance < 1f)
            {
                return;
            }
            Vector2 dir = chord / aimDistance;
            Vector3 targetPos = targetThing.DrawPos;
            // Where the shot reaches the target.
            float targetDistance = (targetPos.x - source.x) * dir.x + (targetPos.z - source.y) * dir.y;
            if (targetDistance < 1f)
            {
                return;
            }
            float m = __instance.ShotHeight;
            float top = new CollisionVertical(targetThing).Max;

            // Heights from here on are the shot's height where it reaches the target.
            float needed = LowestClearHeight(new Vector3(source.x, m, source.y), dir, targetDistance, map, caster, targetThing);
            float current = m + (__result - m) * targetDistance / aimDistance;
            if (needed <= current || needed >= top)
            {
                // Already clear, or the target is not visible over the obstacle (line of fire handles that).
                return;
            }
            // Clear by the margin, but never closer to the target's top than to the obstacle line.
            float wanted = Mathf.Min(needed + Margin, (needed + top) * 0.5f);
            // Back to a height at CE's aim point; never lower than CE's own aim.
            __result = Mathf.Max(__result, m + (wanted - m) * aimDistance / targetDistance);
        }

        /// <summary>Lowest height at the target (targetDistance along dir from the muzzle) of a line from the muzzle
        /// that stays above every full-height obstacle and roof slab the shot crosses before the target. Obstacles
        /// taller than the muzzle are ignored (line of fire already rejects those shots).</summary>
        private static float LowestClearHeight(Vector3 from, Vector2 dir, float targetDistance, Map map, Thing caster,
            Thing targetThing)
        {
            float m = from.y;
            Vector3 to = new Vector3(from.x + dir.x * targetDistance, m, from.z + dir.y * targetDistance);
            CellRect casterRect = caster.OccupiedRect();
            CellRect targetRect = targetThing.OccupiedRect();

            float needed = float.NegativeInfinity;
            seen.Clear();
            foreach (IntVec3 walked in GenSightCE.PointsOnLineOfSight(from, to))
            {
                IntVec3 cell = ElevatedLine.Flat(walked);
                if (!seen.Add(cell) || !cell.InBounds(map) || casterRect.Contains(cell) || targetRect.Contains(cell))
                {
                    continue;
                }
                // The obstacle's far edge on the line: where the shot leaves the cell. A cell the walk only passes by
                // (a corner neighbour) is not crossed.
                if (!ElevatedLine.CrossingInCell(from, to, cell, out _, out float leave))
                {
                    continue;
                }
                float farEdge = leave * targetDistance;
                if (farEdge <= 0f)
                {
                    continue;
                }

                float obstacleTop = ObstacleTop(cell, map, caster, targetThing);
                float roofTop = ElevatedLine.RoofTop(cell, map);
                float highest = Mathf.Max(obstacleTop, roofTop);
                if (highest <= 0f || highest >= m)
                {
                    continue;
                }
                needed = Mathf.Max(needed, NeededAt(m, highest, targetDistance, farEdge));
            }
            seen.Clear();
            // Roofs over the shooter's own cells, and an elevated target's, that the shot crosses: the line of fire
            // judges those on their own (ElevatedLine.OwnRoofBlocks), and CE's walk can skip one of them.
            needed = Mathf.Max(needed, OwnRoofsNeeded(from, to, targetDistance, map, caster));
            if (EGP.IsElevated(targetThing))
            {
                needed = Mathf.Max(needed, OwnRoofsNeeded(from, to, targetDistance, map, targetThing));
            }
            return needed;
        }

        /// <summary>Height at the target that a line from the muzzle (height m) must have to stay above top at
        /// farEdge: m + (h - m) * farEdge / targetDistance > top.</summary>
        private static float NeededAt(float m, float top, float targetDistance, float farEdge) =>
            m - (m - top) * targetDistance / farEdge;

        private static float OwnRoofsNeeded(Vector3 from, Vector3 to, float targetDistance, Map map, Thing elevated)
        {
            float m = from.y;
            float needed = float.NegativeInfinity;
            foreach (IntVec3 cell in elevated.OccupiedRect())
            {
                float roofTop = ElevatedLine.RoofTop(cell, map);
                if (float.IsNegativeInfinity(roofTop) || roofTop >= m
                    || !ElevatedLine.CrossingInCell(from, to, cell, out float enter, out float leave) || leave - enter <= 1e-4f)
                {
                    continue;
                }
                float farEdge = leave * targetDistance;
                if (farEdge > 0f)
                {
                    needed = Mathf.Max(needed, NeededAt(m, roofTop, targetDistance, farEdge));
                }
            }
            return needed;
        }

        /// <summary>Top of the full-height building in a cell (walls, closed doors, rock), or 0.</summary>
        private static float ObstacleTop(IntVec3 cell, Map map, Thing caster, Thing targetThing)
        {
            Building edifice = cell.GetEdifice(map);
            if (edifice == null || edifice == caster || edifice == targetThing || edifice.def.Fillage != FillCategory.Full)
            {
                return 0f;
            }
            return CE_Utility.GetBoundsFor(edifice).max.y;
        }
    }
}
