using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ElevatedGunPlatform
{
    /// <summary>DESIGN 2.6: tracks the thing whose leavings are being made (every GenLeaving.DoLeavingsFor overload
    /// whose first parameter is a Thing).</summary>
    [HarmonyPatch]
    public static class LeavingsPatch_DoLeavingsFor
    {
        [ThreadStatic]
        private static List<Thing> stack;

        private static List<MethodBase> targets;

        /// <summary>The innermost thing whose leavings are being made on this thread, or null.</summary>
        public static Thing Current => stack != null && stack.Count > 0 ? stack[stack.Count - 1] : null;

        private static List<MethodBase> Targets
        {
            get
            {
                if (targets == null)
                {
                    targets = AccessTools.GetDeclaredMethods(typeof(GenLeaving))
                        .Where(m => m.Name == "DoLeavingsFor" && !m.IsGenericMethodDefinition
                            && m.GetParameters().Length > 0 && m.GetParameters()[0].ParameterType == typeof(Thing))
                        .Cast<MethodBase>()
                        .ToList();
                }
                return targets;
            }
        }

        public static bool Prepare()
        {
            if (Targets.Count == 0)
            {
                MountingUtility.WarnOnce("DoLeavingsFor",
                    "GenLeaving.DoLeavingsFor(Thing, ...) not found; elevated turrets and platforms refund their full cost lists.");
                return false;
            }
            return true;
        }

        public static IEnumerable<MethodBase> TargetMethods()
        {
            return Targets;
        }

        public static void Prefix(Thing __0, out bool __state)
        {
            if (stack == null)
            {
                stack = new List<Thing>();
            }
            stack.Add(__0);
            __state = true;
        }

        public static void Finalizer(bool __state)
        {
            if (__state && stack != null && stack.Count > 0)
            {
                stack.RemoveAt(stack.Count - 1);
            }
        }
    }

    /// <summary>CostListAdjusted(this Thing): elevated turret and platform cost lists (DESIGN 2.6).</summary>
    [HarmonyPatch(typeof(CostListCalculator), nameof(CostListCalculator.CostListAdjusted), new[] { typeof(Thing) })]
    public static class LeavingsPatch_CostListOfThing
    {
        public static void Postfix(Thing __0, ref List<ThingDefCountClass> __result)
        {
            if (__0 == null)
            {
                return;
            }
            ThingDef def = __0.def;
            bool inLeavings = LeavingsPatch_DoLeavingsFor.Current == __0;
            if (EGP.IsElevated(def))
            {
                List<ThingDefCountClass> costs = ElevatedCostLists.ForElevated(__0, inLeavings);
                if (costs != null)
                {
                    __result = costs;
                }
            }
            else if (inLeavings && EGP.IsPlatform(def))
            {
                __result = ElevatedCostLists.PlatformWithoutFixedCosts(def, __0.Stuff, __result);
            }
        }
    }

    /// <summary>CostListAdjusted(this BuildableDef, stuff, errorOnNullStuff): the same rules while the leavings of a
    /// thing of exactly that def are being made.</summary>
    [HarmonyPatch(typeof(CostListCalculator), nameof(CostListCalculator.CostListAdjusted),
        new[] { typeof(BuildableDef), typeof(ThingDef), typeof(bool) })]
    public static class LeavingsPatch_CostListOfDef
    {
        public static void Postfix(BuildableDef __0, ref List<ThingDefCountClass> __result)
        {
            Thing current = LeavingsPatch_DoLeavingsFor.Current;
            if (current == null || __0 == null || current.def != __0)
            {
                return;
            }
            if (EGP.IsElevated(current.def))
            {
                List<ThingDefCountClass> costs = ElevatedCostLists.ForElevated(current, true);
                if (costs != null)
                {
                    __result = costs;
                }
            }
            else if (EGP.IsPlatform(current.def))
            {
                __result = ElevatedCostLists.PlatformWithoutFixedCosts(current.def, current.Stuff, __result);
            }
        }
    }

    /// <summary>Cost lists of DESIGN 2.6. Always new lists: the lists vanilla returns are cached and shared.</summary>
    public static class ElevatedCostLists
    {
        /// <summary>For leavings: the base turret's costs with its own stuff (the platform comes back by itself).
        /// Otherwise: base turret costs + platform costs. Null when the turret has no record.</summary>
        public static List<ThingDefCountClass> ForElevated(Thing elevated, bool forLeavings)
        {
            PlatformRecord record = EGP.RecordOf(elevated);
            ThingDef baseDef = EGP.BaseOf(elevated.def);
            if (record == null || baseDef == null)
            {
                return null;
            }
            var result = new List<ThingDefCountClass>();
            AddAll(result, baseDef.CostListAdjusted(MountingUtility.StuffOrDefault(baseDef, record.baseStuff)));
            if (!forLeavings && record.platformDef != null)
            {
                AddAll(result, record.platformDef.CostListAdjusted(
                    MountingUtility.StuffOrDefault(record.platformDef, record.platformStuff)));
            }
            return result;
        }

        /// <summary>A platform's cost list without its fixed (non-stuff) costs, i.e. without the components.</summary>
        public static List<ThingDefCountClass> PlatformWithoutFixedCosts(ThingDef platformDef, ThingDef stuff,
            List<ThingDefCountClass> costs)
        {
            var result = new List<ThingDefCountClass>();
            if (costs == null)
            {
                return result;
            }
            List<ThingDefCountClass> fixedCosts = platformDef.costList;
            for (int i = 0; i < costs.Count; i++)
            {
                ThingDefCountClass entry = costs[i];
                if (entry?.thingDef == null)
                {
                    continue;
                }
                bool isFixed = entry.thingDef != stuff && fixedCosts != null
                    && fixedCosts.Any(f => f != null && f.thingDef == entry.thingDef);
                if (!isFixed)
                {
                    result.Add(new ThingDefCountClass(entry.thingDef, entry.count));
                }
            }
            return result;
        }

        private static void AddAll(List<ThingDefCountClass> result, List<ThingDefCountClass> costs)
        {
            if (costs == null)
            {
                return;
            }
            for (int i = 0; i < costs.Count; i++)
            {
                ThingDefCountClass entry = costs[i];
                if (entry?.thingDef == null || entry.count <= 0)
                {
                    continue;
                }
                ThingDefCountClass existing = result.FirstOrDefault(r => r.thingDef == entry.thingDef);
                if (existing != null)
                {
                    existing.count += entry.count;
                }
                else
                {
                    result.Add(new ThingDefCountClass(entry.thingDef, entry.count));
                }
            }
        }
    }
}
