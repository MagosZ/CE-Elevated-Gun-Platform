using System;
using System.Reflection;
using CombatExtended;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ElevatedGunPlatform
{
    /// <summary>DESIGN 3.7: every player-controlled elevated turret can be ordered to attack, manned or not (a target;
    /// a ground cell only if its gun can target locations, as CE's flak). CE allows that only for mannable turrets (CanSetForcedTarget => mannableComp != null &amp;&amp;
    /// PlayerControlled); the same property shows the gizmo and keeps the forced target in Tick. A manned turret still
    /// fires only while manned (CE's Tick checks MannedNow). Subclasses that override the property without calling
    /// base (SOS2 ship turrets) keep their own rule.</summary>
    [HarmonyPatch]
    public static class ForceAttackPatch_CanSetForcedTarget
    {
        // protected virtual bool CanSetForcedTarget => mannableComp != null && PlayerControlled;
        private static readonly MethodInfo Target = AccessTools.PropertyGetter(typeof(Building_TurretGunCE), "CanSetForcedTarget");

        public static bool Prepare()
        {
            if (Target == null)
            {
                MountingUtility.WarnOnce("CanSetForcedTarget",
                    "CombatExtended.Building_TurretGunCE.CanSetForcedTarget not found; unmanned elevated turrets can't be ordered to attack.");
                return false;
            }
            return true;
        }

        public static MethodBase TargetMethod() => Target;

        public static void Postfix(Building_TurretGunCE __instance, ref bool __result)
        {
            if (!__result && EGP.IsElevated(__instance) && __instance.PlayerControlled)
            {
                __result = true;
            }
        }
    }

    /// <summary>DESIGN 3.7: an unmanned CIWS turret on the ground can't hold an attack order, so it always looks for
    /// something to intercept. On a platform it can, and CE's TryStartShootSomething takes a forced target before any
    /// search, so the order would stop interception. For an unmanned elevated CIWS turret with an order, each call:
    /// a target in the air that it is still engaging is let go first (CE's CIWS search skips its own target); if the
    /// search CE would run finds something inside the firing arc, the order is set aside for that call, so CE picks the
    /// interception target; if its interception rounds are still in flight, it waits, as a ground CIWS does; otherwise
    /// it goes back to the order with its main verb (CE leaves the interception verb selected on a forced target).
    /// Manned CIWS turrets keep CE's rule, as on the ground.</summary>
    [HarmonyPatch(typeof(Building_TurretGunCE), nameof(Building_TurretGunCE.TryStartShootSomething))]
    public static class ForceAttackPatch_InterceptFirst
    {
        // private Verb activeVerb: the verb CE's last target search picked, AttackVerb while set.
        private static readonly AccessTools.FieldRef<Building_Turret_MultiVerbs, Verb> ActiveVerb = ResolveActiveVerb();

        private static AccessTools.FieldRef<Building_Turret_MultiVerbs, Verb> ResolveActiveVerb()
        {
            FieldInfo field = AccessTools.Field(typeof(Building_Turret_MultiVerbs), "activeVerb");
            if (field == null || field.IsStatic || field.FieldType != typeof(Verb))
            {
                return null;
            }
            try
            {
                return AccessTools.FieldRefAccess<Building_Turret_MultiVerbs, Verb>(field);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static bool Prepare()
        {
            if (TurretConversion.ForcedTargetRef == null || ActiveVerb == null)
            {
                MountingUtility.WarnOnce("InterceptFirst",
                    "Building_Turret.forcedTarget or CombatExtended.Building_Turret_MultiVerbs.activeVerb not found; an unmanned elevated CIWS turret with an attack order doesn't intercept.");
                return false;
            }
            return true;
        }

        // The state is nullable: Harmony zero-initialises it, and a zero LocalTargetInfo is the valid cell (0, 0, 0).
        public static bool Prefix(Building_TurretGunCE __instance, out LocalTargetInfo? __state)
        {
            __state = null;
            AccessTools.FieldRef<Building_Turret, LocalTargetInfo> forced = TurretConversion.ForcedTargetRef;
            if (!(__instance is Building_CIWS_CE turret) || turret.mannableComp != null || !EGP.IsElevated(turret)
                || !forced(turret).IsValid)
            {
                return true;
            }
            Thing current = turret.CurrentTarget.Thing;
            if (current != null && current.Spawned && ElevatedLine.Airborne(current))
            {
                // Also clears activeVerb.
                turret.ResetCurrentTarget();
            }
            if (TryFindInterceptTarget(turret, out LocalTargetInfo target))
            {
                if (turret.CompFireArc?.WithinFireArc(target) ?? true)
                {
                    __state = forced(turret);
                    forced(turret) = LocalTargetInfo.Invalid;
                    return true;
                }
            }
            else if (OwnRoundsInFlight(turret))
            {
                return false;
            }
            if (ActiveVerb(turret) is ITargetSearcher)
            {
                ActiveVerb(turret) = null;
            }
            return true;
        }

        // Runs whether or not the original ran, and on exceptions too.
        public static void Finalizer(Building_TurretGunCE __instance, LocalTargetInfo? __state)
        {
            AccessTools.FieldRef<Building_Turret, LocalTargetInfo> forced = TurretConversion.ForcedTargetRef;
            if (__state.HasValue && __state.Value.IsValid && !forced(__instance).IsValid)
            {
                forced(__instance) = __state.Value;
            }
        }

        /// <summary>The target CE's own search would pick (Building_Turret_MultiVerbs.TryFindNewTarget): the first
        /// interception verb that is available and finds one.</summary>
        private static bool TryFindInterceptTarget(Building_TurretGunCE turret, out LocalTargetInfo target)
        {
            foreach (Verb verb in turret.GunCompEq.AllVerbs)
            {
                if (verb is ITargetSearcher searcher && verb.Available() && searcher.TryFindNewTarget(out target))
                {
                    return true;
                }
            }
            target = LocalTargetInfo.Invalid;
            return false;
        }

        private static bool OwnRoundsInFlight(Building_TurretGunCE turret)
        {
            foreach (ProjectileCE_CIWS round in ProjectileCE_CIWS.ProjectilesAt(turret.Map))
            {
                if (round.launcher == turret && round.intendedTarget.Thing is Thing target && target.Spawned)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>DESIGN 3.7: CE's Tick drops the forced target whenever the current target is outside the firing arc.
    /// For an unmanned elevated CIWS turret with an order, a target in the air it is intercepting can drift out of the
    /// arc (or land) while the order waits; that target is let go instead, before CE's check, so the order stays.</summary>
    [HarmonyPatch(typeof(Building_TurretGunCE), nameof(Building_TurretGunCE.Tick))]
    public static class ForceAttackPatch_InterceptArc
    {
        public static bool Prepare() => TurretConversion.ForcedTargetRef != null;

        public static void Prefix(Building_TurretGunCE __instance)
        {
            if (__instance is Building_CIWS_CE turret && turret.CompFireArc != null && turret.mannableComp == null
                && EGP.IsElevated(turret) && TurretConversion.ForcedTargetRef(turret).IsValid
                && turret.CurrentTarget.Thing is Thing target && ElevatedLine.Airborne(target)
                && !turret.CompFireArc.WithinFireArc(turret.CurrentTarget))
            {
                // Also clears activeVerb; a burst in progress goes on with the bursting verb.
                turret.ResetCurrentTarget();
            }
        }
    }

    /// <summary>DESIGN 3.7: CE drops a forced target in Tick only when CanSetForcedTarget is false, and its DeSpawn
    /// keeps it, so an unmanned E would keep a forced cell after being moved to another map (a gravship) and fire at
    /// that cell there, or warm up at it forever. An elevated turret forgets its forced target when it despawns.</summary>
    [HarmonyPatch(typeof(Building_TurretGunCE), nameof(Building_TurretGunCE.DeSpawn), new[] { typeof(DestroyMode) })]
    public static class ForceAttackPatch_DeSpawn
    {
        public static void Postfix(Building_TurretGunCE __instance)
        {
            if (EGP.IsElevated(__instance))
            {
                TurretConversion.ResetForcedTarget(__instance);
                // Its firing zone (DESIGN 3.8) is for the place it left.
                __instance.PlatformComp()?.ClearFiringZone();
            }
        }
    }
}
