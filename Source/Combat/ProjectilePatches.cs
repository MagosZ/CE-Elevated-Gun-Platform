using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using CombatExtended;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace ElevatedGunPlatform
{
    /// <summary>Projectile rules for rounds fired by elevated turrets (DESIGN 3.4). A round is fired by E when E
    /// launched it, or the pawn manning E did (CE launches manned-turret rounds with the operator as launcher). The
    /// operator case is decided once, at launch: the operator's job can change while the round flies. Fragments are
    /// not fired by E (CE launches them with the parent round's launcher and themselves as equipment, from the
    /// impact point). Rounds aimed at E stay plain CE (P4).</summary>
    public static class ElevatedProjectiles
    {
        // Rounds launched by a pawn manning E. Weak keys: an entry goes away with its round.
        private static readonly ConditionalWeakTable<ProjectileCE, object> operatorRounds = new ConditionalWeakTable<ProjectileCE, object>();
        private static readonly object Recorded = new object();
        private static bool anyOperatorRound;

        // Set once the launch postfix has run; until then (or when it is missing) the operator is tested live.
        private static bool launchTracked;

        public static bool FiredByElevated(ProjectileCE projectile)
        {
            Thing launcher = projectile?.launcher;
            if (launcher == null || projectile.equipment == projectile)
            {
                return false;
            }
            if (EGP.IsElevated(launcher.def))
            {
                return true;
            }
            if (!(launcher is Pawn))
            {
                return false;
            }
            // Instant rounds are only checked inside RayCast, while the launcher still mans E.
            if (!launchTracked || IsInstant(projectile))
            {
                return ElevatedLine.ElevatedShooter(launcher) != null;
            }
            return anyOperatorRound && operatorRounds.TryGetValue(projectile, out _);
        }

        /// <summary>Launch postfix: records a round launched by the pawn manning E.</summary>
        internal static void RecordLaunch(ProjectileCE projectile, Thing launcher, Thing equipment)
        {
            launchTracked = true;
            if (launcher is Pawn && equipment != projectile && ElevatedLine.ElevatedShooter(launcher) != null)
            {
                Record(projectile);
            }
        }

        /// <summary>A round loaded from a save: its launcher's saved ManTurret job on E decides (the turret's manning
        /// state may not be restored at load).</summary>
        internal static void RecordLoaded(ProjectileCE projectile)
        {
            if (projectile.launcher is Pawn pawn && projectile.equipment != projectile && EGP.ElevatedManTurretTarget(pawn) != null)
            {
                Record(projectile);
            }
        }

        private static void Record(ProjectileCE projectile)
        {
            operatorRounds.Remove(projectile);
            operatorRounds.Add(projectile, Recorded);
            anyOperatorRound = true;
        }

        private static bool IsInstant(ProjectileCE projectile) =>
            projectile.def.projectile is ProjectilePropertiesCE props && props.isInstant;

        /// <summary>True when the cell (looked up at y = 0) has a roof. False out of bounds or without a map.</summary>
        public static bool Roofed(IntVec3 cell, Map map)
        {
            cell = ElevatedLine.Flat(cell);
            return map != null && cell.InBounds(map) && cell.Roofed(map);
        }
    }

    /// <summary>P2: CE sets ignoreRoof for the whole flight when a round starts at wall height or above in a roofed
    /// cell. Rounds fired by E always check roofs. ProjectileCE_Bursting overrides this overload without calling it
    /// (it calls the short Launch directly), so this postfix does not run for bursting rounds; their ignoreRoof is
    /// never set and stays false.</summary>
    [HarmonyPatch]
    public static class ProjectilePatch_Launch
    {
        // public virtual void Launch(Thing launcher, Vector2 origin, float shotAngle, float shotRotation,
        //     float shotHeight, float shotSpeed, Thing equipment, float distance)
        private static readonly MethodInfo Target = AccessTools.Method(typeof(ProjectileCE), nameof(ProjectileCE.Launch),
            new[] { typeof(Thing), typeof(Vector2), typeof(float), typeof(float), typeof(float), typeof(float), typeof(Thing), typeof(float) });

        // protected bool ignoreRoof
        private static readonly AccessTools.FieldRef<ProjectileCE, bool> IgnoreRoof = ResolveIgnoreRoof();

        private static AccessTools.FieldRef<ProjectileCE, bool> ResolveIgnoreRoof()
        {
            FieldInfo field = AccessTools.Field(typeof(ProjectileCE), "ignoreRoof");
            if (field == null || field.IsStatic || field.FieldType != typeof(bool))
            {
                return null;
            }
            try
            {
                return AccessTools.FieldRefAccess<ProjectileCE, bool>(field);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static bool Prepare()
        {
            if (Target == null || IgnoreRoof == null)
            {
                MountingUtility.WarnOnce("ProjectileCE.Launch",
                    "CombatExtended.ProjectileCE.Launch (8 parameters) or ProjectileCE.ignoreRoof not found; rounds fired by elevated turrets from under a roof ignore roofs.");
                return false;
            }
            return true;
        }

        public static MethodBase TargetMethod() => Target;

        // Runs after the short Launch (called inside this one) has recorded the round.
        public static void Postfix(ProjectileCE __instance)
        {
            if (ElevatedProjectiles.FiredByElevated(__instance))
            {
                IgnoreRoof(__instance) = false;
            }
        }
    }

    /// <summary>Decides at launch whether a round launched by a pawn was fired by E through its operator. The
    /// 8-parameter Launch and ProjectileCE_Bursting's override both end in this overload.</summary>
    [HarmonyPatch]
    public static class ProjectilePatch_LaunchRecord
    {
        // public virtual void Launch(Thing launcher, Vector2 origin, Thing equipment = null)
        private static readonly MethodInfo Target = AccessTools.Method(typeof(ProjectileCE), nameof(ProjectileCE.Launch),
            new[] { typeof(Thing), typeof(Vector2), typeof(Thing) });

        public static bool Prepare()
        {
            if (Target == null)
            {
                MountingUtility.WarnOnce("ProjectileCE.Launch3",
                    "CombatExtended.ProjectileCE.Launch (3 parameters) not found; a round fired by a pawn manning an elevated turret follows that pawn's current job.");
                return false;
            }
            return true;
        }

        public static MethodBase TargetMethod() => Target;

        public static void Postfix(ProjectileCE __instance, Thing launcher, Thing equipment)
        {
            ElevatedProjectiles.RecordLaunch(__instance, launcher, equipment);
        }
    }

    /// <summary>Rounds in flight when the game was saved were not seen at launch: decided once, at load.</summary>
    [HarmonyPatch]
    public static class ProjectilePatch_ExposeData
    {
        // public override void ExposeData(), declared on ProjectileCE (subclasses call it).
        private static readonly MethodInfo Target = AccessTools.DeclaredMethod(typeof(ProjectileCE), nameof(ProjectileCE.ExposeData), Type.EmptyTypes);

        public static bool Prepare()
        {
            if (Target == null)
            {
                MountingUtility.WarnOnce("ProjectileCE.ExposeData",
                    "CombatExtended.ProjectileCE.ExposeData not found; rounds of a manned elevated turret in flight when the game was saved follow plain CE rules after loading.");
                return false;
            }
            return true;
        }

        public static MethodBase TargetMethod() => Target;

        public static void Postfix(ProjectileCE __instance)
        {
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ElevatedProjectiles.RecordLoaded(__instance);
            }
        }
    }

    /// <summary>P2: CE's early-out in CheckCellForCollision (cover lower than the round) skips the roof check for
    /// rounds above the cover height. For rounds fired by E a roofed cell is tested against the roof first; a hit
    /// ends the cell check. It also marks the cell scan for the suppression postfix (Suppression.cs).</summary>
    [HarmonyPatch]
    public static class ProjectilePatch_CheckCellForCollision
    {
        // protected bool CheckCellForCollision(IntVec3 cell)
        internal static readonly MethodInfo Target = ResolveTarget();

        // protected virtual bool TryCollideWithRoof(IntVec3 cell), called virtually (SOS2 overrides it).
        private static readonly Func<ProjectileCE, IntVec3, bool> TryCollideWithRoof = ResolveTryCollideWithRoof();

        private static MethodInfo ResolveTarget()
        {
            MethodInfo method = AccessTools.Method(typeof(ProjectileCE), "CheckCellForCollision", new[] { typeof(IntVec3) });
            return method != null && !method.IsStatic && method.ReturnType == typeof(bool) ? method : null;
        }

        private static Func<ProjectileCE, IntVec3, bool> ResolveTryCollideWithRoof()
        {
            MethodInfo method = AccessTools.Method(typeof(ProjectileCE), "TryCollideWithRoof", new[] { typeof(IntVec3) });
            if (method == null || method.IsStatic || method.ReturnType != typeof(bool))
            {
                return null;
            }
            try
            {
                return AccessTools.MethodDelegate<Func<ProjectileCE, IntVec3, bool>>(method);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static bool Prepare()
        {
            if (Target == null)
            {
                MountingUtility.WarnOnce("ProjectileCE.CheckCellForCollision",
                    "CombatExtended.ProjectileCE.CheckCellForCollision not found; rounds fired by elevated turrets can fly through roofs and do not suppress above height 3.");
                return false;
            }
            if (TryCollideWithRoof == null)
            {
                // Still patched: the suppression postfix needs the cell-scan mark.
                MountingUtility.WarnOnce("ProjectileCE.TryCollideWithRoof",
                    "CombatExtended.ProjectileCE.TryCollideWithRoof not found; rounds fired by elevated turrets can fly through roofs.");
            }
            return true;
        }

        public static MethodBase TargetMethod() => Target;

        public static bool Prefix(ProjectileCE __instance, IntVec3 cell, ref bool __result)
        {
            if (!ElevatedProjectiles.FiredByElevated(__instance))
            {
                return true;
            }
            ElevatedSuppression.MarkCellScan(__instance);
            if (TryCollideWithRoof == null)
            {
                return true;
            }
            IntVec3 flat = ElevatedLine.Flat(cell);
            if (!ElevatedProjectiles.Roofed(flat, __instance.Map))
            {
                return true;
            }
            if (TryCollideWithRoof(__instance, flat))
            {
                __result = true;
                return false;
            }
            return true;
        }
    }

    /// <summary>P3: rounds fired by E never collide with a pawn standing in a roofed cell (E's line of fire never
    /// reaches under a roof, so such a pawn is only ever hit by a stray round).</summary>
    [HarmonyPatch]
    public static class ProjectilePatch_CanCollideWith
    {
        // protected virtual bool CanCollideWith(Thing thing, out float dist)
        private static readonly MethodInfo Target = ResolveTarget();

        private static MethodInfo ResolveTarget()
        {
            MethodInfo method = AccessTools.Method(typeof(ProjectileCE), "CanCollideWith",
                new[] { typeof(Thing), typeof(float).MakeByRefType() });
            return method != null && !method.IsStatic && method.ReturnType == typeof(bool) ? method : null;
        }

        public static bool Prepare()
        {
            if (Target == null)
            {
                MountingUtility.WarnOnce("ProjectileCE.CanCollideWith",
                    "CombatExtended.ProjectileCE.CanCollideWith not found; rounds fired by elevated turrets can hit pawns under roofs.");
                return false;
            }
            return true;
        }

        public static MethodBase TargetMethod() => Target;

        public static void Postfix(ProjectileCE __instance, Thing thing, ref bool __result)
        {
            if (!__result || !(thing is Pawn pawn) || !ElevatedProjectiles.FiredByElevated(__instance))
            {
                return;
            }
            if (ElevatedProjectiles.Roofed(pawn.Position, __instance.Map ?? pawn.Map))
            {
                __result = false;
            }
        }
    }
}
