using System;
using CombatExtended;
using CombatExtended.Compatibility;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ElevatedGunPlatform
{
    /// <summary>DESIGN 2.3 uninstall/reinstall: minifying an elevated turret converts it back to its base turret
    /// in place, trims the magazine to the base capacity and gives the platform back with its recorded HP.</summary>
    [HarmonyPatch(typeof(MinifyUtility), nameof(MinifyUtility.MakeMinified), new[] { typeof(Thing), typeof(DestroyMode) })]
    public static class UnmountPatch_MakeMinified
    {
        public class State
        {
            public Map map;
            public IntVec3 cell;
            /// <summary>Taken before ToBase may reset it; with the cell it gives the turret's exact footprint.</summary>
            public Rot4 rotation;
            public Faction faction;
            public PlatformRecord record;
            public RoofHolderGuard.Token guard;
        }

        public static void Prefix(Thing thing, out State __state)
        {
            __state = null;
            if (thing == null || !thing.Spawned || !EGP.IsElevated(thing.def))
            {
                return;
            }
            PlatformRecord record = EGP.RecordOf(thing);
            if (record == null)
            {
                return;
            }
            __state = new State
            {
                map = thing.Map,
                cell = thing.Position,
                rotation = thing.Rotation,
                faction = thing.Faction,
                record = record,
                guard = RoofHolderGuard.Begin(thing.def),
            };
        }

        public static void Postfix(Thing thing, MinifiedThing __result, State __state)
        {
            // Still spawned: nothing was minified.
            if (__state == null || thing == null || thing.Spawned || !EGP.IsElevated(thing.def))
            {
                return;
            }
            try
            {
                if (TurretConversion.ToBase(thing) == null)
                {
                    return;
                }
                if (__result != null)
                {
                    // The crate shows the inner thing's graphic and stats.
                    TurretConversion.ResetGraphics(__result);
                    TurretConversion.ClearStatCaches(__result);
                }
                MountingUtility.RespawnPlatform(__state.record, __state.cell, __state.rotation, __state.map, __state.faction,
                    __state.record.platformHitPoints);
                MagazineTrim.TrimToCapacity(thing, __state.cell, __state.map);
            }
            catch (Exception e)
            {
                Log.Error($"[ElevatedGunPlatform] Unmounting {thing.ToStringSafe()} failed: {e}");
            }
        }

        public static void Finalizer(State __state)
        {
            __state?.guard.End();
        }
    }

    /// <summary>DESIGN 2.3/2.5: roof-holder guard while an elevated turret is deconstructed or destroyed; its comp's
    /// PostDestroy puts the platform back inside the same call.</summary>
    [HarmonyPatch(typeof(Building), nameof(Building.Destroy), new[] { typeof(DestroyMode) })]
    public static class UnmountPatch_BuildingDestroy
    {
        public static void Prefix(Building __instance, DestroyMode mode, out RoofHolderGuard.Token __state)
        {
            __state = default(RoofHolderGuard.Token);
            if (mode != DestroyMode.Deconstruct && mode != DestroyMode.KillFinalize)
            {
                return;
            }
            if (__instance == null || !__instance.Spawned || EGP.RecordOf(__instance) == null)
            {
                return;
            }
            __state = RoofHolderGuard.Begin(__instance.def);
        }

        public static void Finalizer(RoofHolderGuard.Token __state)
        {
            __state.End();
        }
    }

    /// <summary>Trims a turret's magazine down to its current capacity and drops the excess rounds as ammo, following
    /// CE's own unload (CompAmmoUser.TryUnload) for just the excess (DESIGN 2.7).</summary>
    public static class MagazineTrim
    {
        public static CompAmmoUser AmmoOf(Thing turret)
        {
            if (turret is Building_TurretGunCE ceTurret)
            {
                return ceTurret.CompAmmo;
            }
            if (turret is Building_Turret other)
            {
                return other.GetAmmo();
            }
            return null;
        }

        public static void TrimToCapacity(Thing turret, IntVec3 cell, Map map)
        {
            CompAmmoUser ammo = AmmoOf(turret);
            if (ammo == null)
            {
                return;
            }
            // As CE does after a capacity change, so MagSize reflects the base capacity now.
            if (ammo.parent != null && CE_StatDefOf.MagazineCapacity != null)
            {
                CE_StatDefOf.MagazineCapacity.Worker.ClearCacheForThing(ammo.parent);
            }
            int capacity = ammo.MagSize;
            int count = ammo.CurMagCount;
            if (capacity <= 0 || count <= capacity)
            {
                return;
            }
            int excess = count - capacity;
            AmmoDef ammoDef = ammo.UseAmmo ? ammo.CurrentAmmo : null;
            if (ammoDef == null)
            {
                // Ammo system off: CE's own unload drops nothing either.
                ammo.CurMagCount = capacity;
                return;
            }
            if (map == null || !cell.IsValid)
            {
                return;
            }

            bool forbid = turret.Faction != Faction.OfPlayer;
            int perItem = Mathf.Max(1, ammo.CurAmmoCount);
            int whole = excess / perItem;
            int remainder = excess % perItem;
            int kept = capacity;

            int droppedWhole = Drop(ammoDef, whole, cell, map, forbid);
            kept += (whole - droppedWhole) * perItem;

            if (remainder > 0)
            {
                // Partial rounds stay loaded unless the ammo has a partial-unload item.
                ThingDef partialDef = ammoDef.partialUnloadAmmoDef;
                int droppedPartial = partialDef != null ? Drop(partialDef, remainder, cell, map, forbid) : 0;
                kept += remainder - droppedPartial;
            }
            ammo.CurMagCount = kept;
        }

        /// <summary>Places count items near the cell; returns how many were placed.</summary>
        private static int Drop(ThingDef def, int count, IntVec3 cell, Map map, bool forbid)
        {
            int placed = 0;
            int stackLimit = Mathf.Max(1, def.stackLimit);
            while (placed < count)
            {
                Thing item = ThingMaker.MakeThing(def);
                int n = Mathf.Min(count - placed, stackLimit);
                item.stackCount = n;
                if (!GenPlace.TryPlaceThing(item, cell, map, ThingPlaceMode.Near,
                        placedAction: (t, c) => t.SetForbidden(forbid, false)))
                {
                    // Whatever could not be placed stays loaded.
                    int unplaced = item.Destroyed || item.Spawned ? 0 : item.stackCount;
                    placed += Mathf.Max(0, n - unplaced);
                    if (!item.Destroyed && !item.Spawned)
                    {
                        item.Destroy();
                    }
                    MountingUtility.WarnOnce("ammoDrop:" + def.defName, $"Could not drop all {def.defName} near {cell}; the rest stays loaded.");
                    break;
                }
                placed += n;
            }
            return placed;
        }
    }
}
