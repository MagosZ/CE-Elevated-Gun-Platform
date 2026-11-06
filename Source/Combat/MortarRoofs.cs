using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using CombatExtended;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ElevatedGunPlatform
{
    /// <summary>DESIGN 1.4: CE never fires an overhead weapon (mortar, rocket swarm) from a roofed cell. An elevated
    /// turret sits on top of the roof its platform holds up, so only a rock roof over its cell stops it.</summary>
    public static class MortarRoofs
    {
        /// <summary>Whether the roof over this turret's cell stops its overhead fire: any roof on the ground, only a rock
        /// roof on a platform.</summary>
        public static bool BlocksOverheadFire(Thing turret, IntVec3 cell, Map map)
        {
            RoofDef roof = map?.roofGrid.RoofAt(cell);
            return roof != null && (!EGP.IsElevated(turret) || EGP.IsRockRoof(roof));
        }

        /// <summary>Stands in for RoofGrid.Roofed(cell) in the turret's own checks.</summary>
        public static bool GridRoofedFor(RoofGrid grid, IntVec3 cell, Building_TurretGunCE turret) =>
            EGP.IsElevated(turret) ? BlocksOverheadFire(turret, cell, turret.Map) : grid.Roofed(cell);

        /// <summary>Stands in for cell.Roofed(map) in the turret's own checks.</summary>
        public static bool CellRoofedFor(IntVec3 cell, Map map, Building_TurretGunCE turret) =>
            EGP.IsElevated(turret) ? BlocksOverheadFire(turret, cell, map) : cell.Roofed(map);
    }

    /// <summary>DESIGN 1.4: the roofed checks of CE's TryStartShootSomething (it won't start shooting) and
    /// GetInspectString ("Cannot fire: roofed") are given the turret, so an elevated one applies the rock roof rule.</summary>
    [HarmonyPatch]
    public static class MortarRoofsPatch_TurretChecks
    {
        private static readonly MethodInfo GridRoofed = AccessTools.Method(typeof(RoofGrid), nameof(RoofGrid.Roofed), new[] { typeof(IntVec3) });
        private static readonly MethodInfo CellRoofed = AccessTools.Method(typeof(GridsUtility), nameof(GridsUtility.Roofed),
            new[] { typeof(IntVec3), typeof(Map) });
        private static readonly MethodInfo TryStartShootSomething = AccessTools.DeclaredMethod(typeof(Building_TurretGunCE),
            nameof(Building_TurretGunCE.TryStartShootSomething));
        private static readonly MethodInfo GetInspectString = AccessTools.DeclaredMethod(typeof(Building_TurretGunCE),
            nameof(Building_TurretGunCE.GetInspectString));

        public static bool Prepare()
        {
            if (GridRoofed == null || CellRoofed == null || TryStartShootSomething == null || GetInspectString == null)
            {
                MountingUtility.WarnOnce("MortarRoofs",
                    "RoofGrid.Roofed, GridsUtility.Roofed or CombatExtended.Building_TurretGunCE's TryStartShootSomething/GetInspectString not found; an elevated mortar under a roof doesn't fire.");
                return false;
            }
            return true;
        }

        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return TryStartShootSomething;
            yield return GetInspectString;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            MethodInfo gridFor = AccessTools.Method(typeof(MortarRoofs), nameof(MortarRoofs.GridRoofedFor));
            MethodInfo cellFor = AccessTools.Method(typeof(MortarRoofs), nameof(MortarRoofs.CellRoofedFor));
            int replaced = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                MethodInfo helper = instruction.Calls(GridRoofed) ? gridFor : instruction.Calls(CellRoofed) ? cellFor : null;
                if (helper != null)
                {
                    // The call's arguments are on the stack; the turret (this) is added as the helper's last one.
                    var self = new CodeInstruction(OpCodes.Ldarg_0);
                    instruction.MoveLabelsTo(self);
                    yield return self;
                    yield return new CodeInstruction(OpCodes.Call, helper).WithBlocks(instruction.ExtractBlocks());
                    replaced++;
                    continue;
                }
                yield return instruction;
            }
            if (replaced == 0)
            {
                MountingUtility.WarnOnce("MortarRoofs." + __originalMethod.Name,
                    "No roof check found in CombatExtended.Building_TurretGunCE." + __originalMethod.Name + "; an elevated mortar under a roof keeps CE's rule there.");
            }
        }
    }

    /// <summary>DESIGN 1.4: CE greys out an overhead turret's attack button under any roof. An elevated turret under a
    /// roof that doesn't stop it gets the button back.</summary>
    [HarmonyPatch(typeof(Building_TurretGunCE), nameof(Building_TurretGunCE.GetGizmos))]
    public static class MortarRoofsPatch_AttackButton
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Building_TurretGunCE __instance)
        {
            bool restore = __instance.Spawned && EGP.IsElevated(__instance)
                && !MortarRoofs.BlocksOverheadFire(__instance, __instance.Position, __instance.Map);
            // CE's own reason, built the same way.
            string roofed = null;
            if (restore)
            {
                roofed = "CannotFire".Translate() + ": " + "Roofed".Translate().CapitalizeFirst();
            }
            foreach (Gizmo gizmo in __result)
            {
                if (restore && gizmo is Command_VerbTarget command && command.Disabled && command.disabledReason == roofed)
                {
                    command.Disabled = false;
                    command.disabledReason = null;
                }
                yield return gizmo;
            }
        }
    }
}
