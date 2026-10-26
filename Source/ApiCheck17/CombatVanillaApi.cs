using System;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ElevatedGunPlatform.ApiCheck
{
    /// <summary>Compiled only by the 1.7 API check build (never shipped, never called). Source/Combat/ is excluded
    /// from that build because CE's API carries Unity types the 1.7 reference assemblies don't have, so this file
    /// makes the same vanilla calls Combat/ makes, to catch any of them changing in 1.7.</summary>
    internal static class CombatVanillaApi
    {
        internal static void Check(Thing seer, Thing target, Pawn pawn, Map map, IntVec3 cell, Verb verb, Faction faction)
        {
            bool b = AttackTargetFinder.CanSee(seer, target, (Func<IntVec3, bool>)null);
            float blockChance = CoverUtility.CalculateOverallBlockChance(new LocalTargetInfo(target), cell, map);
            map.debugDrawer.FlashCell(cell, 0.7f, "1.00", 50);
            Vector3 lean = pawn.Drawer.leaner.LeanOffset;
            Vector3 drawPos = target.DrawPos;
            Vector3 shifted = cell.ToVector3Shifted();
            IntVec3 fromVector = shifted.ToIntVec3();
            b |= cell.Roofed(map);
            RoofDef roof = cell.GetRoof(map);
            b |= roof == RoofDefOf.RoofRockThin || roof == RoofDefOf.RoofRockThick || roof == RoofDefOf.RoofConstructed;
            Building edifice = cell.GetEdifice(map);
            Thing cover = cell.GetCover(map);
            Pawn firstPawn = cell.GetFirstPawn(map);
            b |= cell.InBounds(map);
            b |= cell.Standable(map);
            float distance = cell.DistanceTo(fromVector);
            CellRect rect = target.OccupiedRect();
            b |= rect.Contains(cell);
            b |= target.AdjacentTo8WayOrInside(seer);
            b |= target.HostileTo(seer);
            b |= faction.RelationKindWith(pawn.Faction) == FactionRelationKind.Ally;
            b |= pawn.Downed;
            b |= map.thingGrid.CellContains(cell, ThingCategory.Pawn);
            b |= verb.verbProps.mustCastOnOpenGround || verb.verbProps.requireLineOfSight;
            Thing caster = verb.caster;
            b |= Scribe.mode == LoadSaveMode.PostLoadInit;
            b |= pawn.CurJobDef == JobDefOf.ManTurret && pawn.CurJob.targetA.Thing == caster;
            b |= target.Spawned && !target.Destroyed;
            Map thingMap = target.Map;
            ThingDef stuff = target.Stuff;
            // Ground coverage overlay
            CellRect around = CellRect.CenteredOn(cell, 5).ClipInsideMap(map);
            b |= around.Width * around.Height > around.minX + around.maxX + around.minZ + around.maxZ;
            b |= (cell - fromVector).LengthHorizontalSquared <= 4;
            b |= cell.Fogged(map);
            b |= Find.Selector.IsSelected(target);
            b |= RealTime.LastRealTime > 0f;
            b |= RealTime.frameCount > 0;
            b |= around.ExpandedBy(1).Contains(cell);
            b |= cell.Walkable(map) && cell.GetDoor(map) == null;
            b |= target.Rotation != Rot4.North;
            b |= edifice.def.Fillage == FillCategory.Full;
            GenDraw.DrawFieldEdges(new System.Collections.Generic.List<IntVec3> { cell }, new Color(0.45f, 0.8f, 1f));
            Log.Message(b + " " + blockChance + " " + lean + drawPos + roof + edifice + cover + firstPawn + distance + thingMap + stuff);
        }
    }
}
