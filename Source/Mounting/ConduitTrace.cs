using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ElevatedGunPlatform
{
    /// <summary>Diagnostic for an unexplained in-game report (a power conduit under an elevated turret was gone after
    /// the turret was deconstructed with its own button; TESTING 1.3, conduits). Only with dev mode and the mod's debug
    /// overlay setting on: every power conduit that is destroyed logs where, how and from which code path.</summary>
    [HarmonyPatch(typeof(Building), nameof(Building.Destroy), new[] { typeof(DestroyMode) })]
    public static class ConduitTrace_BuildingDestroy
    {
        public static void Prefix(Building __instance, DestroyMode mode)
        {
            if (!ConduitTrace.Active || __instance?.def?.building == null || !__instance.def.building.isPowerConduit
                || !__instance.Spawned)
            {
                return;
            }
            string others = string.Join(", ", __instance.Position.GetThingList(__instance.Map)
                .Where(t => t != __instance).Select(t => t.def.defName));
            Log.Message($"[ElevatedGunPlatform] Debug: power conduit at {__instance.Position} destroyed ({mode}); "
                + $"also in the cell: {(others.Length > 0 ? others : "nothing")}.\n{Environment.StackTrace}");
        }
    }

    public static class ConduitTrace
    {
        public static bool Active => ElevatedGunPlatformMod.Settings != null && ElevatedGunPlatformMod.Settings.DebugOverlayActive;

        /// <summary>Logs vanilla's own wipe decision for a platform or elevated turret spawning over a power conduit
        /// (TESTING C12), before this mod's SpawningWipes postfix changes anything.</summary>
        public static void LogWipe(ThingDef newDef, ThingDef oldDef, bool vanillaResult)
        {
            if (Active && oldDef?.building != null && oldDef.building.isPowerConduit
                && (EGP.IsPlatform(newDef) || EGP.IsElevated(newDef)))
            {
                Log.Message($"[ElevatedGunPlatform] Debug: vanilla SpawningWipes({newDef.defName}, {oldDef.defName}) = {vanillaResult}");
            }
        }
    }
}
