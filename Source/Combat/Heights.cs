using System.Reflection;
using CombatExtended;
using HarmonyLib;
using Verse;

namespace ElevatedGunPlatform
{
    /// <summary>Heights of elevated turrets (DESIGN 3.1).</summary>
    public static class ElevatedHeights
    {
        /// <summary>Top (and muzzle height) of an elevated turret: its base turret's CE height plus the height of the
        /// platform it stands on (low or high, from the floor settings).</summary>
        public static float TopOf(Thing elevated) => EGP.TopOf(elevated);
    }

    /// <summary>DESIGN 3.1: an elevated turret spans (0, top) and shoots from top; an empty platform spans (0, its
    /// tier's height). Every CE height consumer (bounds, cover heights, shot height, aim) builds a CollisionVertical, so this
    /// one postfix covers them all.</summary>
    [HarmonyPatch]
    public static class HeightsPatch_CalculateHeightRange
    {
        // private static void CalculateHeightRange(Thing thing, out FloatRange heightRange, out float shotHeight)
        private static readonly MethodInfo Target = AccessTools.Method(typeof(CollisionVertical), "CalculateHeightRange",
            new[] { typeof(Thing), typeof(FloatRange).MakeByRefType(), typeof(float).MakeByRefType() });

        public static bool Prepare()
        {
            if (Target == null)
            {
                MountingUtility.WarnOnce("CalculateHeightRange",
                    "CombatExtended.CollisionVertical.CalculateHeightRange not found; elevated turrets keep plain wall height.");
                return false;
            }
            return true;
        }

        public static MethodBase TargetMethod() => Target;

        public static void Postfix(Thing thing, ref FloatRange heightRange, ref float shotHeight)
        {
            // CE gives E and platforms (full fillage) a height of 2.0; 0 means a compat module reports it submerged.
            if (thing == null || heightRange.max <= 0f)
            {
                return;
            }
            float top;
            if (EGP.IsElevated(thing.def))
            {
                top = ElevatedHeights.TopOf(thing);
            }
            else if (EGP.IsPlatform(thing.def))
            {
                top = EGP.PlatformHeight(thing.def);
            }
            else
            {
                return;
            }
            heightRange = new FloatRange(0f, top);
            shotHeight = top;
        }
    }
}
