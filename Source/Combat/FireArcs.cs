using System.Reflection;
using CombatExtended;
using HarmonyLib;
using Verse;

namespace ElevatedGunPlatform
{
    /// <summary>DESIGN 3.9: an elevated turret's arc can point anywhere (full-circle swing), so it may cross the line
    /// straight behind its facing. CE computes the part of such an arc beyond that line (SupplementarySpan) only while
    /// the arc is being edited and keeps it in an unsaved field, so after a load the part was lost. For an elevated
    /// turret it is computed every time, the same way CE computes it while editing.</summary>
    [HarmonyPatch]
    public static class FireArcPatch_SupplementarySpan
    {
        // FloatRange? SupplementarySpan { get; } (private)
        private static readonly MethodInfo Target = AccessTools.PropertyGetter(typeof(CompFireArc), "SupplementarySpan");

        public static bool Prepare()
        {
            if (Target == null || Target.ReturnType != typeof(FloatRange?))
            {
                MountingUtility.WarnOnce("SupplementarySpan",
                    "CombatExtended.CompFireArc.SupplementarySpan not found; an elevated turret's arc that crosses the line behind it may lose that part after loading.");
                return false;
            }
            return true;
        }

        public static MethodBase TargetMethod() => Target;

        public static void Postfix(CompFireArc __instance, ref FloatRange? __result)
        {
            if (__instance?.parent == null || !EGP.IsElevated(__instance.parent.def))
            {
                return;
            }
            // The full-circle swing never clips the arc, so its edges are the centre +/- half the span.
            float left = __instance.CenterAngle - __instance.Span / 2f;
            float right = __instance.CenterAngle + __instance.Span / 2f;
            if (left < -180f)
            {
                __result = new FloatRange(left + 360f, 180f);
            }
            else if (right > 180f)
            {
                __result = new FloatRange(-180f, right - 360f);
            }
            else
            {
                __result = null;
            }
        }
    }
}
