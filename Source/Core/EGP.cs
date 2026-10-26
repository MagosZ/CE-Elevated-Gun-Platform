using System.Collections.Generic;
using CombatExtended;
using RimWorld;
using Verse;

namespace ElevatedGunPlatform
{
    /// <summary>The two platform heights (DESIGN 1.2). Each has its own floor and HP bonus setting; the high one costs
    /// and takes twice as long to build.</summary>
    public enum PlatformTier
    {
        Low,
        High,
    }

    /// <summary>Registry of platform and elevated defs, filled by def generation at load.</summary>
    public static class EGP
    {
        /// <summary>The XML template: the 1x1 high platform (the name every save made before the low platform knows).</summary>
        public const string PlatformTemplateDefName = "EGP_TurretPlatform";
        public const string PlatformDefPrefix = "EGP_TurretPlatform_";
        public const string LowPlatformDefPrefix = "EGP_TurretPlatformLow_";
        public const string ElevatedDefPrefix = "EGP_Elevated_";

        /// <summary>Generated platform def, e.g. EGP_TurretPlatform_2x2Cells (high) or EGP_TurretPlatformLow_2x2Cells.
        /// The 1x1 high platform is the template itself. Vanilla rejects ThingDef names that end in a digit (and so
        /// would their blueprint and frame names).</summary>
        public static string PlatformDefName(IntVec2 size, PlatformTier tier) =>
            (tier == PlatformTier.High ? PlatformDefPrefix : LowPlatformDefPrefix) + size.x + "x" + size.z + "Cells";

        /// <summary>Elevated def of a base turret, EGP_Elevated_{base}; "_Elevated" is added if the base name ends in a
        /// digit.</summary>
        public static string ElevatedDefName(ThingDef baseDef)
        {
            string name = ElevatedDefPrefix + baseDef.defName;
            return char.IsDigit(name[name.Length - 1]) ? name + "_Elevated" : name;
        }

        private static readonly Dictionary<ThingDef, ThingDef> elevatedByBase = new Dictionary<ThingDef, ThingDef>();
        private static readonly Dictionary<ThingDef, ThingDef> baseByElevated = new Dictionary<ThingDef, ThingDef>();
        private static readonly Dictionary<IntVec2, ThingDef> highBySize = new Dictionary<IntVec2, ThingDef>();
        private static readonly Dictionary<IntVec2, ThingDef> lowBySize = new Dictionary<IntVec2, ThingDef>();
        private static readonly Dictionary<ThingDef, PlatformTier> platformTiers = new Dictionary<ThingDef, PlatformTier>();

        public static IEnumerable<ThingDef> AllElevatedDefs => baseByElevated.Keys;
        public static IEnumerable<ThingDef> AllBaseDefs => elevatedByBase.Keys;
        public static IEnumerable<ThingDef> AllPlatformDefs => platformTiers.Keys;

        public static void RegisterElevated(ThingDef baseDef, ThingDef elevatedDef)
        {
            elevatedByBase[baseDef] = elevatedDef;
            baseByElevated[elevatedDef] = baseDef;
        }

        public static void RegisterPlatform(IntVec2 size, PlatformTier tier, ThingDef platformDef)
        {
            (tier == PlatformTier.High ? highBySize : lowBySize)[size] = platformDef;
            platformTiers[platformDef] = tier;
        }

        public static bool IsElevated(ThingDef def) => def != null && baseByElevated.ContainsKey(def);
        public static bool IsElevated(Thing thing) => thing != null && IsElevated(thing.def);
        /// <summary>An eligible base turret def (one that has an elevated version).</summary>
        public static bool IsEligibleBase(ThingDef def) => def != null && elevatedByBase.ContainsKey(def);
        public static bool IsPlatform(ThingDef def) => def != null && platformTiers.ContainsKey(def);
        public static bool IsPlatform(Thing thing) => thing != null && IsPlatform(thing.def);

        public static ThingDef BaseOf(ThingDef elevatedDef) =>
            elevatedDef != null && baseByElevated.TryGetValue(elevatedDef, out var b) ? b : null;

        public static ThingDef ElevatedOf(ThingDef baseDef) =>
            baseDef != null && elevatedByBase.TryGetValue(baseDef, out var e) ? e : null;

        /// <summary>Platform def of a tier for an unrotated footprint size, or null.</summary>
        public static ThingDef PlatformFor(IntVec2 size, PlatformTier tier) =>
            (tier == PlatformTier.High ? highBySize : lowBySize).TryGetValue(size, out var p) ? p : null;

        /// <summary>The default platform for a footprint size: the high one (the low one if there is no high one).</summary>
        public static ThingDef PlatformForSize(IntVec2 size) =>
            PlatformFor(size, PlatformTier.High) ?? PlatformFor(size, PlatformTier.Low);

        /// <summary>Tier of a platform def; the high tier for anything else (a missing record falls back to it).</summary>
        public static PlatformTier TierOf(ThingDef platformDef) =>
            platformDef != null && platformTiers.TryGetValue(platformDef, out PlatformTier tier) ? tier : PlatformTier.High;

        /// <summary>Height of a platform's top (its floor x 2.0 from the settings).</summary>
        public static float PlatformHeight(ThingDef platformDef) => EGPSettings.CurrentHeight(TierOf(platformDef));

        /// <summary>The platform an elevated turret stands on: its record's, or the default for its size.</summary>
        public static ThingDef PlatformOf(Thing elevated)
        {
            ThingDef recorded = RecordOf(elevated)?.platformDef;
            if (recorded != null)
            {
                return recorded;
            }
            ThingDef baseDef = BaseOf(elevated?.def);
            return baseDef != null ? PlatformForSize(baseDef.size) : null;
        }

        /// <summary>Top (and muzzle height) of an elevated turret: its base turret's CE height plus the height of the
        /// platform it stands on (DESIGN 3.1). For anything else, its plain CE building height.</summary>
        public static float TopOf(Thing elevated)
        {
            ThingDef baseDef = BaseOf(elevated?.def);
            if (baseDef == null)
            {
                return elevated != null ? BaseBuildingHeight(elevated.def) : 0f;
            }
            return BaseBuildingHeight(baseDef) + PlatformHeight(PlatformOf(elevated));
        }

        /// <summary>The elevated turret behind this thing: the turret itself, or the turret a pawn is manning (CE's
        /// operator, the turret's ManningPawn; a pawn still walking there on its ManTurret job is not). Null if none.
        /// Works for projectile launchers too (CE launches manned-turret rounds with the operator).</summary>
        public static Thing ResolveElevated(Thing thing)
        {
            if (thing == null)
            {
                return null;
            }
            if (IsElevated(thing.def))
            {
                return thing;
            }
            if (thing is Pawn pawn && ElevatedManTurretTarget(pawn) is Building_TurretGunCE turret
                && turret.mannableComp?.ManningPawn == pawn)
            {
                return turret;
            }
            return null;
        }

        /// <summary>The elevated turret the pawn's current ManTurret job is for (whether it mans it yet or not), or null.</summary>
        public static Thing ElevatedManTurretTarget(Pawn pawn)
        {
            if (pawn == null || pawn.CurJobDef != JobDefOf.ManTurret)
            {
                return null;
            }
            Thing manned = pawn.CurJob?.targetA.Thing;
            return manned != null && IsElevated(manned.def) ? manned : null;
        }

        /// <summary>The record comp every eligible turret (base or elevated) carries.</summary>
        public static CompElevatedPlatform PlatformComp(this Thing thing) =>
            (thing as ThingWithComps)?.GetComp<CompElevatedPlatform>();

        /// <summary>Platform record of an elevated turret, or null.</summary>
        public static PlatformRecord RecordOf(Thing thing) => IsElevated(thing) ? thing.PlatformComp()?.record : null;

        /// <summary>CE height of a non-elevated building def, as CE's CollisionVertical computes it.</summary>
        public static float BaseBuildingHeight(ThingDef def) =>
            def.Fillage == FillCategory.Full ? CollisionVertical.WallCollisionHeight : def.fillPercent;
    }
}
