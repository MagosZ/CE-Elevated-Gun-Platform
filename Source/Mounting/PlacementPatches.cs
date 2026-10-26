using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ElevatedGunPlatform
{
    /// <summary>DESIGN 2.1: an eligible turret may be placed over a completed platform; an eligible turret's frame
    /// (no longer an edifice) still blocks edifice blueprints.</summary>
    [HarmonyPatch(typeof(GenConstruct), nameof(GenConstruct.CanPlaceBlueprintOver),
        new[] { typeof(BuildableDef), typeof(ThingDef), typeof(ThingDef), typeof(ThingDef) })]
    public static class MountPatch_CanPlaceBlueprintOver
    {
        public static void Postfix(BuildableDef newDef, ThingDef oldDef, ref bool __result)
        {
            if (newDef == null || oldDef == null)
            {
                return;
            }
            if (EGP.IsPlatform(oldDef))
            {
                if (MountingUtility.EligibleTurretOf(newDef) != null)
                {
                    // Exact footprint match is checked in CanPlaceBlueprintAt.
                    __result = true;
                }
                return;
            }
            if (__result && oldDef.IsFrame && newDef.IsEdifice() && MountingUtility.EligibleTurretOf(oldDef) != null)
            {
                __result = false;
            }
        }
    }

    /// <summary>DESIGN 2.1: CanPlaceBlueprintAt and CanPlaceBlueprintAt_NewTemp. A turret footprint that touches a
    /// platform must exactly match one completed platform.</summary>
    [HarmonyPatch]
    public static class MountPatch_CanPlaceBlueprintAt
    {
        private static List<MethodBase> targets;
        private static readonly Dictionary<MethodBase, int> ignoreSpotsIndex = new Dictionary<MethodBase, int>();
        // Set while the check runs again from inside this postfix.
        private static bool rerunning;

        private static List<MethodBase> Targets
        {
            get
            {
                if (targets == null)
                {
                    targets = new List<MethodBase>();
                    foreach (string name in new[] { "CanPlaceBlueprintAt", "CanPlaceBlueprintAt_NewTemp" })
                    {
                        MethodInfo method = AccessTools.GetDeclaredMethods(typeof(GenConstruct))
                            .FirstOrDefault(m => m.Name == name && IsUsable(m));
                        if (method != null)
                        {
                            targets.Add(method);
                        }
                    }
                }
                return targets;
            }
        }

        /// <summary>(BuildableDef entDef, IntVec3 center, Rot4 rot, Map map, bool, Thing thingToIgnore, ...)
        /// returning AcceptanceReport.</summary>
        private static bool IsUsable(MethodInfo m)
        {
            if (!m.IsStatic || m.IsGenericMethod || m.ReturnType != typeof(AcceptanceReport))
            {
                return false;
            }
            ParameterInfo[] p = m.GetParameters();
            return p.Length >= 6
                && p[0].ParameterType == typeof(BuildableDef) && p[0].Name == "entDef"
                && p[1].ParameterType == typeof(IntVec3) && p[1].Name == "center"
                && p[2].ParameterType == typeof(Rot4) && p[2].Name == "rot"
                && p[3].ParameterType == typeof(Map) && p[3].Name == "map"
                && p[5].ParameterType == typeof(Thing) && p[5].Name == "thingToIgnore";
        }

        public static bool Prepare()
        {
            if (Targets.Count == 0)
            {
                MountingUtility.WarnOnce("CanPlaceBlueprintAt",
                    "GenConstruct.CanPlaceBlueprintAt not found; turrets are not limited to exact platform matches.");
                return false;
            }
            return true;
        }

        public static IEnumerable<MethodBase> TargetMethods()
        {
            return Targets;
        }

        /// <summary>Reinstalling E places E's own def, but the thing installed is its base turret (it converts back
        /// when it is uninstalled). Vanilla checks the base turret, not E's interaction spot and impassability.</summary>
        [HarmonyPriority(Priority.First)]
        public static void Prefix(ref BuildableDef entDef)
        {
            if (entDef is ThingDef def && EGP.IsElevated(def))
            {
                entDef = EGP.BaseOf(def);
            }
        }

        /// <summary>Platform rules and their messages. A footprint touching a platform of the wrong size or position,
        /// or an unfinished platform, is rejected with a message saying so (it takes precedence over whatever vanilla
        /// said). When vanilla rejects because the interaction spot lands on a platform, its message is replaced with
        /// one explaining that. All other vanilla rejections are kept as they are.</summary>
        /// <param name="thingToIgnore">The elevated turret itself when it is being reinstalled.</param>
        public static void Postfix(BuildableDef entDef, IntVec3 center, Rot4 rot, Map map, Thing thingToIgnore,
            ref AcceptanceReport __result, MethodBase __originalMethod, object[] __args)
        {
            if (rerunning || map == null)
            {
                return;
            }
            ThingDef turret = MountingUtility.EligibleTurretOf(entDef);
            if (turret != null && !__result.Accepted)
            {
                TryIgnoringOwnSpot(turret, center, rot, map, thingToIgnore, __originalMethod, __args, ref __result);
            }

            MountingUtility.PlatformFit fit = turret != null
                ? MountingUtility.CheckFootprint(turret, center, rot, map, thingToIgnore)
                : MountingUtility.PlatformFit.NoPlatform;
            if (fit == MountingUtility.PlatformFit.WrongPlatform)
            {
                __result = new AcceptanceReport("EGP_NeedsPlatform".Translate(turret.size.x, turret.size.z));
                return;
            }
            if (fit == MountingUtility.PlatformFit.Unfinished)
            {
                __result = new AcceptanceReport("EGP_PlatformUnfinished".Translate());
                return;
            }
            // A turret whose own spot is inside its footprint (CE's flak) always has it on its matching platform; it
            // uses the platform's spot once mounted, so turning it would not help and vanilla's reason is kept.
            bool ownSpotInside = fit == MountingUtility.PlatformFit.Fits && DefGeneration.InteractionSpotInsideFootprint(turret);
            if (!__result.Accepted && !ownSpotInside
                && MountingUtility.InteractionSpotOnPlatform(entDef as ThingDef, center, rot, map, thingToIgnore))
            {
                __result = new AcceptanceReport("EGP_InteractionSpotOnPlatform".Translate());
            }
        }

        /// <summary>A turret whose own interaction spot lies inside its footprint (CE's 3x3 flak) has that spot on
        /// the platform it is placed on, which vanilla may reject. Its elevated version uses the platform's spot,
        /// which was checked when the platform was placed, so the check runs again without interaction spots. An
        /// elevated turret reinstalled in place (thingToIgnore) counts as the platform it leaves behind.</summary>
        private static bool TryIgnoringOwnSpot(ThingDef turret, IntVec3 center, Rot4 rot, Map map, Thing thingToIgnore,
            MethodBase original, object[] args, ref AcceptanceReport result)
        {
            if (!DefGeneration.InteractionSpotInsideFootprint(turret) || original == null || args == null)
            {
                return false;
            }
            bool onPlatform = MountingUtility.FindMatchingPlatform(map, turret, center, rot) != null
                || (EGP.IsElevated(thingToIgnore)
                    && MountingUtility.Matches(thingToIgnore, turret.size, GenAdj.OccupiedRect(center, rot, turret.size)));
            if (!onPlatform)
            {
                return false;
            }
            int index = IgnoreSpotsIndex(original);
            if (index < 0 || index >= args.Length || !(args[index] is bool ignore) || ignore)
            {
                return false;
            }
            // A copy: Harmony writes edits of __args back to the arguments.
            var copy = (object[])args.Clone();
            copy[index] = true;
            rerunning = true;
            try
            {
                if (original.Invoke(null, copy) is AcceptanceReport report && report.Accepted)
                {
                    result = report;
                    return true;
                }
            }
            catch (Exception e)
            {
                MountingUtility.WarnOnce("rerunPlacement", $"Placement check of {turret.defName} without interaction spots failed: {e.InnerException?.Message ?? e.Message}");
            }
            finally
            {
                rerunning = false;
            }
            return false;
        }

        private static int IgnoreSpotsIndex(MethodBase method)
        {
            if (!ignoreSpotsIndex.TryGetValue(method, out int index))
            {
                ParameterInfo[] p = method.GetParameters();
                index = Array.FindIndex(p, x => x.Name == "ignoreInteractionSpots" && x.ParameterType == typeof(bool));
                ignoreSpotsIndex[method] = index;
                if (index < 0)
                {
                    MountingUtility.WarnOnce("ignoreInteractionSpots:" + method.Name,
                        $"GenConstruct.{method.Name} has no ignoreInteractionSpots parameter; turrets with their interaction spot inside their footprint may not be placeable on a platform.");
                }
            }
            return index;
        }
    }

    /// <summary>An elevated turret is never replaced in place by another building (Replace Stuff lets wall-like
    /// buildings replace each other): the wipe would put its platform back under the new building.</summary>
    [HarmonyPatch]
    public static class MountPatch_CanReplace
    {
        // CanReplace(BuildableDef placing, BuildableDef existing, ThingDef placingStuff, ThingDef existingStuff).
        private static readonly MethodInfo Target = AccessTools.GetDeclaredMethods(typeof(GenConstruct))
            .FirstOrDefault(m => m.Name == "CanReplace" && m.IsStatic && m.ReturnType == typeof(bool)
                && m.GetParameters().Length >= 2 && m.GetParameters()[1].Name == "existing"
                && m.GetParameters()[1].ParameterType == typeof(BuildableDef));

        public static bool Prepare()
        {
            if (Target == null)
            {
                MountingUtility.WarnOnce("CanReplace",
                    "GenConstruct.CanReplace not found; other buildings may be allowed to replace an elevated turret.");
                return false;
            }
            return true;
        }

        public static MethodBase TargetMethod() => Target;

        /// <summary>Last, after Replace Stuff's own CanReplace postfixes.</summary>
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(BuildableDef existing, ref bool __result)
        {
            if (__result && existing is ThingDef def && EGP.IsElevated(GenConstruct.BuiltDefOf(def) as ThingDef ?? def))
            {
                __result = false;
            }
        }
    }

    /// <summary>DESIGN 2.1: a platform never blocks a matching eligible turret's blueprint, frame or install blueprint.</summary>
    [HarmonyPatch(typeof(GenConstruct), nameof(GenConstruct.BlocksConstruction), new[] { typeof(Thing), typeof(Thing) })]
    public static class MountPatch_BlocksConstruction
    {
        public static void Postfix(Thing constructible, Thing t, ref bool __result)
        {
            if (!__result || constructible == null || t == null || !EGP.IsPlatform(t.def))
            {
                return;
            }
            ThingDef turret = MountingUtility.EligibleTurretOf(constructible);
            if (turret == null)
            {
                return;
            }
            CellRect rect = GenAdj.OccupiedRect(constructible.Position, constructible.Rotation, turret.size);
            if (MountingUtility.Matches(t, turret.size, rect))
            {
                __result = false;
            }
        }
    }

    /// <summary>DESIGN 2.1: an eligible turret's frame never wipes a platform. Blueprints (build and install) are
    /// covered too, in case vanilla would let them wipe. The reverse also holds: a platform put back by unmounting
    /// keeps an eligible turret's blueprint or frame on its cells (an elevated turret reinstalled in place). During
    /// a wipe for a base turret that will mount on the platform (MountPatch_WipeExistingThings), it keeps it.
    /// Like a wall, a platform or elevated turret spawning never wipes a building that is not an edifice (power
    /// conduits and similar pipes that sit under walls), nor a blueprint or frame of one: in game, a conduit under an
    /// elevated turret was gone after the turret was deconstructed and its platform put back.</summary>
    [HarmonyPatch(typeof(GenSpawn), nameof(GenSpawn.SpawningWipes), new[] { typeof(BuildableDef), typeof(BuildableDef), typeof(bool) })]
    public static class MountPatch_SpawningWipes
    {
        public static void Postfix(BuildableDef newEntDef, BuildableDef oldEntDef, ref bool __result)
        {
            if (ConduitTrace.Active)
            {
                ConduitTrace.LogWipe(newEntDef as ThingDef, oldEntDef as ThingDef, __result);
            }
            if (!__result || !(oldEntDef is ThingDef oldDef) || !(newEntDef is ThingDef newDef))
            {
                return;
            }
            if (EGP.IsPlatform(oldDef))
            {
                if (((newDef.IsFrame || newDef.IsBlueprint) && MountingUtility.EligibleTurretOf(newDef) != null)
                    || newDef == MountPatch_WipeExistingThings.keepPlatformFor)
                {
                    __result = false;
                }
            }
            else if (EGP.IsPlatform(newDef) && (oldDef.IsFrame || oldDef.IsBlueprint)
                && MountingUtility.EligibleTurretOf(oldDef) != null)
            {
                __result = false;
            }
            else if ((EGP.IsPlatform(newDef) || EGP.IsElevated(newDef)) && SitsUnderWalls(oldDef))
            {
                __result = false;
            }
        }

        /// <summary>A building that is not an edifice (a power conduit), or a blueprint or frame of one.</summary>
        private static bool SitsUnderWalls(ThingDef def)
        {
            ThingDef built = def.IsBlueprint || def.IsFrame ? def.entityDefToBuild as ThingDef : def;
            return built != null && built.category == ThingCategory.Building && built.building != null && !built.building.isEdifice;
        }
    }

    /// <summary>DESIGN 2.2 (install, reinstall): an install blueprint, before it spawns its turret, wipes its cells
    /// for that turret (vanilla Blueprint.TryReplaceWithSolidThing, DestroyMode.Deconstruct). The turret is the base
    /// turret itself, an edifice that would wipe the platform, so the mount in GenSpawn.Spawn would find none. While
    /// the cells are wiped for an eligible base turret that will mount on an exactly matching platform there, that
    /// platform is kept (MountPatch_SpawningWipes) and the Spawn prefix absorbs it.</summary>
    [HarmonyPatch(typeof(GenSpawn), nameof(GenSpawn.WipeExistingThings),
        new[] { typeof(IntVec3), typeof(Rot4), typeof(BuildableDef), typeof(Map), typeof(DestroyMode) })]
    public static class MountPatch_WipeExistingThings
    {
        /// <summary>The base turret def whose matching platform the current wipe keeps, or null.</summary>
        [ThreadStatic]
        internal static ThingDef keepPlatformFor;

        public static void Prefix(IntVec3 thingPos, Rot4 thingRot, BuildableDef thingDef, Map map, out ThingDef __state)
        {
            __state = keepPlatformFor;
            keepPlatformFor = thingDef is ThingDef def && MountingUtility.MountablePlatform(def, thingPos, thingRot, map) != null
                ? def
                : null;
        }

        public static void Finalizer(ThingDef __state)
        {
            keepPlatformFor = __state;
        }
    }
}
