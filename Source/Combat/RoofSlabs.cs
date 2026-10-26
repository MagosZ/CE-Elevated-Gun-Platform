using System.Reflection;
using CombatExtended;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ElevatedGunPlatform
{
    /// <summary>DESIGN 3.1: only rock roofs (thin rock roof, overhead mountain) keep CE's taller slab in its 3D roof
    /// test; every other roof gets a built roof's slab (2.0-2.1). CE makes a thick built roof, such as Raise The Roof's
    /// constructed steel roof, 2.0-4.0 like thin rock, and Anomaly's void metal ceiling 2.0-8.0. The slab is used only
    /// where a direct-fire round or a line meets a roof (CE's TryCollideWithRoof, SOS2Compat's ship verb line check, and
    /// this mod's 3D rules through ElevatedLine.RoofTop). Drop pods, mortar shells, infestations and roof collapse go by
    /// the roof def's own flags, so they keep treating thick roofs as thick.</summary>
    [HarmonyPatch]
    public static class RoofSlabs
    {
        // public static Bounds GetBoundsFor(IntVec3 cell, RoofDef roof)
        private static readonly MethodInfo Target = AccessTools.Method(typeof(CE_Utility), nameof(CE_Utility.GetBoundsFor),
            new[] { typeof(IntVec3), typeof(RoofDef) });

        public static bool Prepare()
        {
            if (Target == null)
            {
                MountingUtility.WarnOnce("CE_Utility.GetBoundsFor",
                    "CombatExtended.CE_Utility.GetBoundsFor(IntVec3, RoofDef) not found; thick built roofs keep CE's thickness.");
                return false;
            }
            return true;
        }

        public static MethodBase TargetMethod() => Target;

        public static void Prefix(ref RoofDef __1)
        {
            __1 = SlabRoof(__1);
        }

        /// <summary>The roof whose CE slab a roof gets: a built roof for every roof but rock, while the plain roofs
        /// setting is on.</summary>
        public static RoofDef SlabRoof(RoofDef roof)
        {
            if (roof == null || roof == RoofDefOf.RoofRockThin || roof == RoofDefOf.RoofRockThick || !EGPSettings.PlainRoofsActive)
            {
                return roof;
            }
            return RoofDefOf.RoofConstructed ?? roof;
        }
    }
}
