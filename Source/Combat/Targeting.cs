using System;
using System.Reflection;
using CombatExtended;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ElevatedGunPlatform
{
    /// <summary>DESIGN 3.5 (C2): while an elevated turret (or a pawn manning one) scores targets, vanilla's flat cover
    /// penalty is off. E's own line check already handles cover by height.</summary>
    public static class ElevatedTargeting
    {
        [ThreadStatic]
        private static int scoringDepth;

        /// <summary>True while an elevated shooter's target score is being computed on this thread.</summary>
        public static bool Scoring => scoringDepth > 0;

        /// <summary>Prefix body: enters the scope when the searcher is elevated; returns whether it did.</summary>
        internal static bool Enter(IAttackTargetSearcher searcher)
        {
            if (searcher == null || ElevatedLine.ElevatedShooter(searcher.Thing) == null)
            {
                return false;
            }
            scoringDepth++;
            return true;
        }

        internal static void Exit(bool entered)
        {
            if (entered && scoringDepth > 0)
            {
                scoringDepth--;
            }
        }

        /// <summary>A GetShootingTargetScore overload, or null when it is missing or its parameters can't bind
        /// (the patches bind 'IAttackTargetSearcher searcher').</summary>
        internal static MethodInfo ResolveScore(Type type, Type[] parameters)
        {
            MethodInfo method = null;
            try
            {
                method = AccessTools.Method(type, "GetShootingTargetScore", parameters);
                if (method == null)
                {
                    method = AccessTools.Method(type, "GetShootingTargetScore");
                }
            }
            catch (Exception)
            {
                method = null;
            }
            if (method == null || !method.IsStatic)
            {
                return null;
            }
            ParameterInfo[] ps = method.GetParameters();
            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].Name == "searcher" && ps[i].ParameterType == typeof(IAttackTargetSearcher))
                {
                    return method;
                }
            }
            return null;
        }
    }

    /// <summary>Vanilla AttackTargetFinder.GetShootingTargetScore (not public): scope for elevated searchers.</summary>
    [HarmonyPatch]
    public static class TargetingPatch_VanillaScore
    {
        private static readonly MethodInfo Target = ElevatedTargeting.ResolveScore(typeof(AttackTargetFinder),
            new[] { typeof(IAttackTarget), typeof(IAttackTargetSearcher), typeof(Verb) });

        public static bool Prepare()
        {
            if (Target == null)
            {
                MountingUtility.WarnOnce("VanillaGetShootingTargetScore",
                    "AttackTargetFinder.GetShootingTargetScore not found; elevated turrets keep the flat cover penalty when choosing targets.");
                return false;
            }
            return true;
        }

        public static MethodBase TargetMethod() => Target;

        public static void Prefix(IAttackTargetSearcher searcher, out bool __state)
        {
            __state = ElevatedTargeting.Enter(searcher);
        }

        public static void Finalizer(bool __state)
        {
            ElevatedTargeting.Exit(__state);
        }
    }

    /// <summary>CE's private NonSnapAttackTargetFinder.GetShootingTargetScore: scope for elevated searchers.</summary>
    [HarmonyPatch]
    public static class TargetingPatch_NonSnapScore
    {
        private static readonly MethodInfo Target = ElevatedTargeting.ResolveScore(typeof(NonSnapAttackTargetFinder),
            new[] { typeof(IAttackTarget), typeof(IAttackTargetSearcher), typeof(Verb), typeof(Vector3) });

        public static bool Prepare()
        {
            if (Target == null)
            {
                MountingUtility.WarnOnce("NonSnapGetShootingTargetScore",
                    "CombatExtended NonSnapAttackTargetFinder.GetShootingTargetScore not found; elevated non-snap turrets keep the flat cover penalty when choosing targets.");
                return false;
            }
            return true;
        }

        public static MethodBase TargetMethod() => Target;

        public static void Prefix(IAttackTargetSearcher searcher, out bool __state)
        {
            __state = ElevatedTargeting.Enter(searcher);
        }

        public static void Finalizer(bool __state)
        {
            ElevatedTargeting.Exit(__state);
        }
    }

    /// <summary>No flat cover penalty inside an elevated scoring scope.</summary>
    [HarmonyPatch(typeof(CoverUtility), nameof(CoverUtility.CalculateOverallBlockChance),
        new[] { typeof(LocalTargetInfo), typeof(IntVec3), typeof(Map) })]
    public static class TargetingPatch_CalculateOverallBlockChance
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref float __result)
        {
            if (ElevatedTargeting.Scoring)
            {
                __result = 0f;
            }
        }
    }
}
