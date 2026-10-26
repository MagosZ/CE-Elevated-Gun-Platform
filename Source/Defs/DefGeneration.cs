using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using CombatExtended;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ElevatedGunPlatform
{
    /// <summary>Generates the platform defs and the hidden elevated turret defs (DESIGN 1.1-1.5). The prefix adds
    /// them before vanilla generates blueprints and frames, so platforms get theirs and elevated defs get none; the
    /// postfix fixes up what vanilla generated.</summary>
    [HarmonyPatch(typeof(DefGenerator), nameof(DefGenerator.GenerateImpliedDefs_PreResolve))]
    public static class DefGeneration
    {
        internal const string LogPrefix = "[ElevatedGunPlatform] ";

        // English fallbacks, identical to Languages/English/Keyed/EGP.xml. GeneratedDefLabels translates them later.
        private const string PlatformLabelHighEnglish = "high turret platform ({0}\u00D7{1})";
        private const string PlatformLabelLowEnglish = "low turret platform ({0}\u00D7{1})";
        private const string PlatformGroupLabelEnglish = "turret platform ({0}\u00D7{1})";
        private const string PlatformDescriptionHighEnglish = "A tall, solid platform for a {0}\u00D7{1} turret: floor 3 by default, set in the mod settings (each floor is a wall's height). Build or install a Combat Extended turret of exactly this size on it to make an elevated gun emplacement that fires over nearby walls. Compared with the low platform it costs twice as much and takes twice as long to build, and it adds more hit points to the turret (+40% by default). Mortars and other indirect-fire turrets can't be mounted. Holds up roofs like a wall, and is airtight if its material is. The marked spot in front is where colonists stand to man or reload the turret.";
        private const string PlatformDescriptionLowEnglish = "A low, solid platform for a {0}\u00D7{1} turret: floor 1 by default, a wall's height, set in the mod settings. Build or install a Combat Extended turret of exactly this size on it to make an elevated gun emplacement that fires over nearby walls. It costs half as much as the high platform and is quicker to build, and it adds fewer hit points to the turret (+20% by default). Mortars and other indirect-fire turrets can't be mounted. Holds up roofs like a wall, and is airtight if its material is. The marked spot in front is where colonists stand to man or reload the turret.";
        private const string ElevatedLabelEnglish = "elevated {0}";
        private const string ElevatedDescriptionSuffixEnglish = "This turret stands on a turret platform. It fires from the platform's height (by default floor 1 on a low platform, floor 3 on a high one) plus its own height, so it shoots over walls near it. Walls near the target still give cover, a roof over the target hides it, and enemies can see and shoot it just as well. It can be ordered to attack a target even if nobody mans it (a spot on the ground too, if its gun can aim at the ground, such as flak); a manned one still needs its crew to fire. The platform adds hit points (more on a high platform), multiplies the ammo capacity (x2 by default) and makes the turret impassable. Packing up or deconstructing the turret gives the platform back.";

        /// <summary>Architect order of the platforms: by size, the high one first (first button, or the one its
        /// dropdown shows).</summary>
        private const int PlatformUiOrder = 2000;
        private const string PlatformTexFolder = "EGP/Platform/";
        private const string MaterialSubMenuPackageId = "cedaro.material.submenu";

        /// <summary>The dropdown group of each footprint size, for relabelling once translations are loaded.</summary>
        internal static readonly Dictionary<DesignatorDropdownGroupDef, IntVec2> PlatformGroups =
            new Dictionary<DesignatorDropdownGroupDef, IntVec2>();

        // Template values from TurretPlatform.xml, used if its statBases lack them.
        private const float TemplateMaxHitPoints = 300f;
        private const float TemplateWorkToBuild = 135f;

        private const BindingFlags DeclaredInstance =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static readonly MethodInfo MemberwiseCloneMethod = AccessTools.Method(typeof(object), "MemberwiseClone");

        // Not public in the reference assemblies (the XML field <isMortar> of a verb).
        private static readonly FieldInfo VerbIsMortarField = AccessTools.Field(typeof(VerbProperties), "isMortar");

        private static bool prefixRan;
        private static bool postfixRan;
        private static bool warnedMissingField;

        [HarmonyPriority(Priority.Last)]
        public static void Prefix()
        {
            if (prefixRan)
            {
                Log.Warning(LogPrefix + "Implied defs are generated again (hot reload?); turret platform defs are not regenerated.");
                return;
            }
            prefixRan = true;
            try
            {
                Generate();
            }
            catch (Exception e)
            {
                Log.Error(LogPrefix + "Def generation failed: " + e);
            }
        }

        [HarmonyPriority(Priority.Last)]
        public static void Postfix()
        {
            if (postfixRan || !prefixRan)
            {
                return;
            }
            postfixRan = true;
            try
            {
                foreach (ThingDef baseDef in EGP.AllBaseDefs.ToList())
                {
                    // Reinstalling an elevated turret places the base turret's install blueprint.
                    ThingDef elevated = EGP.ElevatedOf(baseDef);
                    if (elevated != null)
                    {
                        elevated.minifiedDef = baseDef.minifiedDef;
                        elevated.installBlueprintDef = baseDef.installBlueprintDef;
                    }
                    MakeFrameNonEdifice(baseDef.frameDef);
                }
            }
            catch (Exception e)
            {
                Log.Error(LogPrefix + "Def generation postfix failed: " + e);
            }
        }

        /// <summary>Any turret building def (vanilla's Building_Turret class or a subclass, CE's included), except
        /// the elevated defs.</summary>
        private static bool IsTurretDef(ThingDef def) =>
            def?.thingClass != null && typeof(Building_Turret).IsAssignableFrom(def.thingClass)
            && (def.defName == null || !def.defName.StartsWith(EGP.ElevatedDefPrefix, StringComparison.Ordinal));

        /// <summary>DESIGN 1.1: null for a buildable or installable CE turret that is not indirect fire, else why a
        /// turret def can't be mounted (for the startup log).</summary>
        private static string IneligibleReason(ThingDef def)
        {
            if (!typeof(Building_TurretGunCE).IsAssignableFrom(def.thingClass))
            {
                return "not a Combat Extended turret";
            }
            ThingDef gun = def.building?.turretGunDef;
            if (gun == null)
            {
                return "has no turret gun";
            }
            if (def.designationCategory == null && def.minifiedDef == null)
            {
                return "can't be built or installed";
            }
            return IsIndirectFire(gun) ? "indirect fire (mortar or artillery)" : null;
        }

        private static bool IsIndirectFire(ThingDef gun)
        {
            List<VerbProperties> verbs = gun.Verbs;
            if (verbs != null)
            {
                foreach (VerbProperties verb in verbs)
                {
                    if (verb == null)
                    {
                        continue;
                    }
                    if (VerbIsMortar(verb)
                        || (verb.verbClass != null && typeof(Verb_ShootMortarCE).IsAssignableFrom(verb.verbClass))
                        || verb.defaultProjectile?.projectile?.flyOverhead == true)
                    {
                        return true;
                    }
                }
            }
            // Stands in for building.IsMortar, which is not called before ResolveReferences (it may read unresolved
            // data or cache a wrong value); GeneratedDefLabels checks it afterwards.
            if (gun.comps != null)
            {
                foreach (CompProperties comp in gun.comps)
                {
                    if (comp?.compClass != null && typeof(CompChangeableProjectile).IsAssignableFrom(comp.compClass))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool VerbIsMortar(VerbProperties verb) =>
            VerbIsMortarField != null && VerbIsMortarField.FieldType == typeof(bool) && (bool)VerbIsMortarField.GetValue(verb);

        private static void Generate()
        {
            if (VerbIsMortarField == null)
            {
                Log.Warning(LogPrefix + "VerbProperties.isMortar not found; mortars are detected by verb class and projectile only.");
            }
            ThingDef template = DefDatabase<ThingDef>.GetNamedSilentFail(EGP.PlatformTemplateDefName);
            if (template == null)
            {
                Log.Error(LogPrefix + "Platform def " + EGP.PlatformTemplateDefName + " is missing; nothing was generated.");
                return;
            }
            // Looked up by name: DefOfs may not be bound yet.
            ThingDef wall = DefDatabase<ThingDef>.GetNamedSilentFail("Wall");
            ThingDef component = DefDatabase<ThingDef>.GetNamedSilentFail("ComponentIndustrial");
            StatDef hpStat = DefDatabase<StatDef>.GetNamedSilentFail("MaxHitPoints");
            StatDef workStat = DefDatabase<StatDef>.GetNamedSilentFail("WorkToBuild");

            var eligible = new List<ThingDef>();
            var excluded = new List<KeyValuePair<ThingDef, string>>();
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading.Where(IsTurretDef))
            {
                string reason = IneligibleReason(def);
                if (reason == null)
                {
                    eligible.Add(def);
                }
                else
                {
                    excluded.Add(new KeyValuePair<ThingDef, string>(def, reason));
                }
            }

            // 1.3: before cloning, so base and elevated defs get identical comp lists.
            foreach (ThingDef def in eligible)
            {
                if (def.comps == null)
                {
                    def.comps = new List<CompProperties>();
                }
                if (!def.comps.Any(c => c is CompProperties_ElevatedPlatform))
                {
                    def.comps.Add(new CompProperties_ElevatedPlatform());
                }
            }

            // 1.2: platforms, a high and a low one per footprint size, in one Architect dropdown per size. The 1x1
            // high one is the XML template itself (configured first: the others are cloned from it).
            float baseHp = StatBase(template, hpStat, TemplateMaxHitPoints);
            float baseWork = StatBase(template, workStat, TemplateWorkToBuild);
            var oneByOne = new IntVec2(1, 1);
            var sizes = new List<IntVec2> { oneByOne };
            var otherSizes = new List<IntVec2>();
            foreach (ThingDef def in eligible)
            {
                if (def.size.x > 0 && def.size.z > 0 && def.size != oneByOne && !otherSizes.Contains(def.size))
                {
                    otherSizes.Add(def.size);
                }
            }
            otherSizes.SortBy(s => s.x * s.z, s => s.x);
            sizes.AddRange(otherSizes);

            var platformSizes = new List<string>();
            int uiOrder = PlatformUiOrder;
            bool dropdowns = MaterialSubMenuActive();
            if (!dropdowns)
            {
                Log.Message(LogPrefix + "High and low turret platforms get separate Architect buttons: in vanilla a dropdown "
                    + "hides the material choice of buildings made from materials (Material Sub-Menu brings it back).");
            }
            foreach (IntVec2 size in sizes)
            {
                DesignatorDropdownGroupDef group = dropdowns ? MakePlatformGroup(size) : null;
                bool any = false;
                foreach (PlatformTier tier in new[] { PlatformTier.High, PlatformTier.Low })
                {
                    bool isTemplate = size == oneByOne && tier == PlatformTier.High;
                    string defName = isTemplate ? template.defName : EGP.PlatformDefName(size, tier);
                    if (!isTemplate && DefDatabase<ThingDef>.GetNamedSilentFail(defName) != null)
                    {
                        Log.Warning(LogPrefix + "A def named " + defName + " already exists; platform not generated.");
                        continue;
                    }
                    try
                    {
                        ThingDef platform = isTemplate ? template : CloneDef(template, defName);
                        ConfigurePlatform(platform, size, tier, baseHp, baseWork, hpStat, workStat, component, uiOrder++, group);
                        if (!isTemplate)
                        {
                            AddDef(platform);
                        }
                        EGP.RegisterPlatform(size, tier, platform);
                        any = true;
                    }
                    catch (Exception e)
                    {
                        Log.Error(LogPrefix + "Could not generate " + defName + ": " + e);
                    }
                }
                if (any)
                {
                    platformSizes.Add(SizeText(size));
                }
            }

            // 1.4: elevated defs.
            var mounted = new List<ThingDef>();
            foreach (ThingDef baseDef in eligible)
            {
                ThingDef platform = EGP.PlatformForSize(baseDef.size);
                string defName = EGP.ElevatedDefName(baseDef);
                if (platform == null)
                {
                    excluded.Add(new KeyValuePair<ThingDef, string>(baseDef, "no platform for its size"));
                    continue;
                }
                if (DefDatabase<ThingDef>.GetNamedSilentFail(defName) != null)
                {
                    Log.Warning(LogPrefix + "A def named " + defName + " already exists; " + baseDef.defName + " can't be elevated.");
                    excluded.Add(new KeyValuePair<ThingDef, string>(baseDef, "its elevated def name is taken"));
                    continue;
                }
                try
                {
                    ThingDef elevated = MakeElevated(baseDef, defName, platform, wall);
                    AddDef(elevated);
                    EGP.RegisterElevated(baseDef, elevated);
                    mounted.Add(baseDef);
                }
                catch (Exception e)
                {
                    Log.Error(LogPrefix + "Could not generate " + defName + ": " + e);
                    excluded.Add(new KeyValuePair<ThingDef, string>(baseDef, "its elevated def could not be generated"));
                }
            }

            Log.Message(LogPrefix + "Generated turret platforms (high and low) for " + platformSizes.Count + " sizes ("
                + string.Join(", ", platformSizes) + ") and " + mounted.Count + " elevated turret defs.");
            LogEligibility(mounted, excluded);
        }

        /// <summary>Startup log: every turret def, mountable or not; the others grouped by reason.</summary>
        private static void LogEligibility(List<ThingDef> mounted, List<KeyValuePair<ThingDef, string>> excluded)
        {
            var text = new StringBuilder(LogPrefix);
            text.Append("Turrets that can be mounted on a platform (").Append(mounted.Count).Append("): ")
                .Append(mounted.Count > 0 ? string.Join(", ", mounted.Select(DefText)) : "none").Append('.');
            if (excluded.Count > 0)
            {
                text.Append("\nTurrets that can't be mounted (").Append(excluded.Count).Append("):");
                foreach (IGrouping<string, KeyValuePair<ThingDef, string>> group in excluded.GroupBy(e => e.Value))
                {
                    text.Append("\n- ").Append(group.Key).Append(": ").Append(string.Join(", ", group.Select(e => DefText(e.Key))));
                }
            }
            Log.Message(text.ToString());
        }

        /// <summary>defName and (untranslated) label, e.g. Turret_Autocannon (autocannon turret).</summary>
        private static string DefText(ThingDef def) =>
            def.label.NullOrEmpty() ? def.defName : def.defName + " (" + def.label + ")";

        /// <summary>DESIGN 1.2: size, tier, costs (a high platform costs double and takes twice the work; its own hit
        /// points are the same), art, interaction spot, Architect order and dropdown, English text.</summary>
        private static void ConfigurePlatform(ThingDef def, IntVec2 size, PlatformTier tier, float baseHp, float baseWork,
            StatDef hpStat, StatDef workStat, ThingDef component, int uiOrder, DesignatorDropdownGroupDef group)
        {
            EGPSettings settings = ElevatedGunPlatformMod.Settings;
            int stuffPerCell = settings != null ? Mathf.Max(1, settings.stuffPerCell) : 5;
            int components = settings != null ? Mathf.Max(0, settings.components) : 1;
            int area = size.x * size.z;
            int factor = tier == PlatformTier.High ? 2 : 1;

            def.size = size;
            def.costStuffCount = stuffPerCell * area * factor;
            def.costList = PlatformCostList(def.costList, component, components * factor);
            if (hpStat != null)
            {
                def.SetStatBaseValue(hpStat, baseHp * area);
            }
            if (workStat != null)
            {
                def.SetStatBaseValue(workStat, baseWork * area * factor);
            }
            if (def.graphicData != null)
            {
                def.graphicData.texPath = PlatformTexPath(size, tier);
                def.graphicData.drawSize = new Vector2(size.x, size.z);
                // The front face is drawn toward the bottom of the screen in every rotation, like a wall's.
                def.graphicData.drawRotated = false;
            }
            def.hasInteractionCell = true;
            def.interactionCellOffset = PlatformInteractionCellOffset(size);
            def.uiOrder = uiOrder;
            def.designatorDropdown = group;
            bool high = tier == PlatformTier.High;
            def.label = string.Format(high ? PlatformLabelHighEnglish : PlatformLabelLowEnglish, size.x, size.z);
            def.description = string.Format(high ? PlatformDescriptionHighEnglish : PlatformDescriptionLowEnglish, size.x, size.z);
        }

        /// <summary>The art for a platform: one picture per footprint size and tier (Textures/EGP/Platform/High_2x2.png),
        /// or the stretched 1x1 one for a size that has none.</summary>
        private static string PlatformTexPath(IntVec2 size, PlatformTier tier)
        {
            string prefix = PlatformTexFolder + (tier == PlatformTier.High ? "High_" : "Low_");
            string path = prefix + SizeText(size);
            string root = ElevatedGunPlatformMod.ContentPack?.RootDir;
            if (root != null && !System.IO.File.Exists(System.IO.Path.Combine(root, "Textures", path + ".png")))
            {
                return prefix + SizeText(new IntVec2(1, 1));
            }
            return path;
        }

        /// <summary>Whether the Material Sub-Menu mod is active. In vanilla, a building made from materials and a
        /// dropdown share the same menu, so in a dropdown the material choice is lost; that mod brings it back
        /// (the same check Replace Stuff makes for its own dropdowns, DesignatorBuildDropdownStuffFix).</summary>
        private static bool MaterialSubMenuActive()
        {
            try
            {
                return ModLister.AnyModActiveNoSuffix(new List<string> { MaterialSubMenuPackageId });
            }
            catch (Exception e)
            {
                Log.Warning(LogPrefix + "Could not check for Material Sub-Menu; platforms get separate buttons: " + e.Message);
                return false;
            }
        }

        /// <summary>One Architect button per footprint size (only with Material Sub-Menu, see MaterialSubMenuActive);
        /// the high platform shows first, the low one is in its dropdown.</summary>
        private static DesignatorDropdownGroupDef MakePlatformGroup(IntVec2 size)
        {
            string defName = "EGP_TurretPlatformGroup_" + SizeText(size) + "Cells";
            DesignatorDropdownGroupDef group = DefDatabase<DesignatorDropdownGroupDef>.GetNamedSilentFail(defName);
            if (group != null)
            {
                return group;
            }
            try
            {
                group = new DesignatorDropdownGroupDef
                {
                    defName = defName,
                    label = string.Format(PlatformGroupLabelEnglish, size.x, size.z),
                    generated = true,
                    modContentPack = ElevatedGunPlatformMod.ContentPack,
                };
                group.PostLoad();
                DefDatabase<DesignatorDropdownGroupDef>.Add(group);
                PlatformGroups[group] = size;
                return group;
            }
            catch (Exception e)
            {
                Log.Error(LogPrefix + "Could not generate " + defName + "; its platforms get separate Architect buttons: " + e);
                return null;
            }
        }

        /// <summary>The front middle cell just outside the footprint (unrotated).</summary>
        internal static IntVec3 PlatformInteractionCellOffset(IntVec2 size) => new IntVec3(0, 0, -((size.z - 1) / 2) - 1);

        /// <summary>Whether a def's own interaction spot lies inside its own (unrotated) footprint.</summary>
        internal static bool InteractionSpotInsideFootprint(ThingDef def) =>
            def != null && def.hasInteractionCell
            && GenAdj.OccupiedRect(IntVec3.Zero, Rot4.North, def.size).Contains(def.interactionCellOffset);

        /// <summary>Copy of the template's cost list with the component count from the settings.</summary>
        private static List<ThingDefCountClass> PlatformCostList(List<ThingDefCountClass> source, ThingDef component, int count)
        {
            var list = new List<ThingDefCountClass>();
            ThingDefCountClass componentEntry = null;
            if (source != null)
            {
                foreach (ThingDefCountClass entry in source)
                {
                    if (entry?.thingDef == null)
                    {
                        continue;
                    }
                    if (component != null && entry.thingDef == component)
                    {
                        componentEntry = entry;
                        continue;
                    }
                    list.Add(ShallowCopy(entry));
                }
            }
            if (component != null && count > 0)
            {
                ThingDefCountClass entry = componentEntry != null ? ShallowCopy(componentEntry) : new ThingDefCountClass(component, count);
                entry.count = count;
                list.Add(entry);
            }
            return list.Count > 0 ? list : null;
        }

        private static ThingDef MakeElevated(ThingDef baseDef, string defName, ThingDef platform, ThingDef wall)
        {
            ThingDef def = CloneDef(baseDef, defName);
            def.designationCategory = null;
            // Restored in the postfix; null now so vanilla generates no install blueprint for it.
            def.minifiedDef = null;

            def.passability = Traversability.Impassable;
            def.fillPercent = 1f;
            def.blockWind = true;
            def.blockLight = true;
            def.holdsRoof = true;
            def.coversFloor = true;
            def.rotatable = true;
            def.building.isStuffableAirtight = true;

            def.hasInteractionCell = true;
            // The whole footprint is impassable now, so a spot of B's inside it (CE's 3x3 flak) can't be stood on.
            def.interactionCellOffset = baseDef.hasInteractionCell && !InteractionSpotInsideFootprint(baseDef)
                ? baseDef.interactionCellOffset
                : platform.interactionCellOffset;

            List<StuffCategoryDef> stuffCategories = wall?.stuffCategories ?? platform.stuffCategories;
            def.stuffCategories = stuffCategories != null ? new List<StuffCategoryDef>(stuffCategories) : null;
            def.costStuffCount = platform.costStuffCount;

            // Hidden: an elevated turret only ever exists on its platform, so it is never traded, stocked or crafted.
            def.tradeability = Tradeability.None;
            def.tradeTags = null;
            def.thingSetMakerTags = null;
            def.recipeMaker = null;
            FullTraverseFireArcs(def);

            def.label = string.Format(ElevatedLabelEnglish, baseDef.label ?? baseDef.defName);
            def.description = ElevatedDescription(baseDef.description, ElevatedDescriptionSuffixEnglish);
            return def;
        }

        /// <summary>DESIGN 3.9: CE keeps a firing arc's centre within +/-maxSpanDeviation of the turret's facing, and
        /// on a platform the facing is the platform's (chosen for the loading spot), so the arc could not be turned
        /// toward the threat. The elevated def gets its own copy of the arc properties with a full circle; the arc's
        /// width limits (spanRange) stay.</summary>
        private static void FullTraverseFireArcs(ThingDef def)
        {
            if (def.comps == null)
            {
                return;
            }
            for (int i = 0; i < def.comps.Count; i++)
            {
                if (def.comps[i] is CompProperties_FireArc arc && arc.maxSpanDeviation < 360f)
                {
                    CompProperties_FireArc own = ShallowCopy(arc);
                    own.maxSpanDeviation = 360f;
                    if (arc.maxSpanDeviation < 180f)
                    {
                        // On the ground the swing also caps the width at twice the swing; keep that cap.
                        float width = Mathf.Min(arc.spanRange.max, 2f * arc.maxSpanDeviation);
                        own.spanRange = new FloatRange(Mathf.Min(arc.spanRange.min, width), width);
                    }
                    def.comps[i] = own;
                }
            }
        }

        internal static string ElevatedDescription(string baseDescription, string suffix) =>
            baseDescription.NullOrEmpty() ? suffix : baseDescription + "\n\n" + suffix;

        /// <summary>DESIGN 1.5: a frame of an eligible turret can stand on a platform.</summary>
        private static void MakeFrameNonEdifice(ThingDef frameDef)
        {
            if (frameDef == null)
            {
                return;
            }
            // Own copy: the frame's BuildingProperties may be shared with another def.
            frameDef.building = frameDef.building != null ? ShallowCopy(frameDef.building) : new BuildingProperties();
            frameDef.building.isEdifice = false;
            frameDef.altitudeLayer = AltitudeLayer.BuildingOnTop;
        }

        /// <summary>A copy of a def that shares no mutable state with it, ready for PostLoad and DefDatabase.Add.</summary>
        private static ThingDef CloneDef(ThingDef source, string defName)
        {
            ThingDef fresh = FreshInstance(source) as ThingDef ?? new ThingDef();
            ThingDef clone = ShallowCopy(source);
            // Per-def caches and graphics as in a fresh def; every list (comps, placeWorkers, stuffCategories,
            // researchPrerequisites, ...) its own copy.
            Isolate(clone, fresh, true);
            clone.defName = defName;
            clone.statBases = source.statBases?.Select(m => ShallowCopy(m)).ToList();
            clone.costList = source.costList?.Select(c => ShallowCopy(c)).ToList();
            if (source.graphicData != null)
            {
                var graphicData = new GraphicData();
                graphicData.CopyFrom(source.graphicData);
                ClearGraphicFields(graphicData);
                clone.graphicData = graphicData;
            }
            if (source.building != null)
            {
                clone.building = ShallowCopy(source.building);
                Isolate(clone.building, FreshInstance(source.building) ?? new BuildingProperties(), false);
            }
            clone.index = fresh.index;
            clone.shortHash = fresh.shortHash;
            CopyField(clone, fresh, "uiIcon");
            clone.blueprintDef = null;
            clone.frameDef = null;
            clone.installBlueprintDef = null;
            clone.generated = true;
            clone.modContentPack = ElevatedGunPlatformMod.ContentPack;
            return clone;
        }

        private static void AddDef(ThingDef def)
        {
            def.PostLoad();
            DefDatabase<ThingDef>.Add(def);
        }

        /// <summary>Fields named like a cache (and Graphic fields if asked) take the fresh instance's value; every
        /// List, HashSet or Dictionary becomes a copy of its own.</summary>
        private static void Isolate(object copy, object fresh, bool resetGraphics)
        {
            for (Type type = copy.GetType(); type != null && type != typeof(object); type = type.BaseType)
            {
                foreach (FieldInfo field in type.GetFields(DeclaredInstance))
                {
                    bool takeFresh = field.Name.IndexOf("cache", StringComparison.OrdinalIgnoreCase) >= 0
                        || (resetGraphics && typeof(Graphic).IsAssignableFrom(field.FieldType));
                    if (takeFresh)
                    {
                        if (field.DeclaringType.IsInstanceOfType(fresh))
                        {
                            field.SetValue(copy, field.GetValue(fresh));
                        }
                        continue;
                    }
                    object value = field.GetValue(copy);
                    object own = value != null ? CopyCollection(value) : null;
                    if (own != null)
                    {
                        field.SetValue(copy, own);
                    }
                }
            }
        }

        private static object CopyCollection(object value)
        {
            Type type = value.GetType();
            if (!type.IsGenericType)
            {
                return null;
            }
            Type generic = type.GetGenericTypeDefinition();
            if (generic != typeof(List<>) && generic != typeof(HashSet<>) && generic != typeof(Dictionary<,>))
            {
                return null;
            }
            try
            {
                return Activator.CreateInstance(type, value);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void ClearGraphicFields(object obj)
        {
            for (Type type = obj.GetType(); type != null && type != typeof(object); type = type.BaseType)
            {
                foreach (FieldInfo field in type.GetFields(DeclaredInstance))
                {
                    if (typeof(Graphic).IsAssignableFrom(field.FieldType))
                    {
                        field.SetValue(obj, null);
                    }
                }
            }
        }

        private static void CopyField(object target, object source, string name)
        {
            FieldInfo field = AccessTools.Field(target.GetType(), name);
            if (field == null || field.IsStatic)
            {
                if (!warnedMissingField)
                {
                    warnedMissingField = true;
                    Log.Warning(LogPrefix + "Field " + target.GetType().Name + "." + name + " not found; generated defs keep the source's value.");
                }
                return;
            }
            field.SetValue(target, field.GetValue(source));
        }

        private static object FreshInstance(object like)
        {
            try
            {
                return Activator.CreateInstance(like.GetType(), true);
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal static T ShallowCopy<T>(T source) where T : class =>
            source == null ? null : (T)MemberwiseCloneMethod.Invoke(source, null);

        private static float StatBase(ThingDef def, StatDef stat, float fallback) =>
            stat != null && def.statBases != null ? def.statBases.GetStatValueFromList(stat, fallback) : fallback;

        private static string SizeText(IntVec2 size) => size.x + "x" + size.z;
    }

    /// <summary>Re-labels the generated defs in the active language, once translations are loaded (generation
    /// runs before that and sets the English text).</summary>
    [StaticConstructorOnStartup]
    public static class GeneratedDefLabels
    {
        private static readonly List<FieldInfo> cachedLabelFields = FindCachedLabelFields();

        static GeneratedDefLabels()
        {
            try
            {
                var fresh = new ThingDef();
                foreach (ThingDef platform in EGP.AllPlatformDefs.ToList())
                {
                    int x = platform.size.x;
                    int z = platform.size.z;
                    bool high = EGP.TierOf(platform) == PlatformTier.High;
                    Relabel(platform, (high ? "EGP_PlatformLabelHigh" : "EGP_PlatformLabelLow").Translate(x, z).Resolve(),
                        (high ? "EGP_PlatformDescriptionHigh" : "EGP_PlatformDescriptionLow").Translate(x, z).Resolve(), fresh);
                }
                var freshGroup = new DesignatorDropdownGroupDef();
                foreach (KeyValuePair<DesignatorDropdownGroupDef, IntVec2> group in DefGeneration.PlatformGroups)
                {
                    group.Key.label = "EGP_PlatformGroupLabel".Translate(group.Value.x, group.Value.z).Resolve();
                    ResetCachedLabels(group.Key, freshGroup);
                }
                foreach (ThingDef elevated in EGP.AllElevatedDefs.ToList())
                {
                    ThingDef baseDef = EGP.BaseOf(elevated);
                    if (baseDef == null)
                    {
                        continue;
                    }
                    string label = "EGP_ElevatedLabel".Translate(baseDef.label ?? baseDef.defName).Resolve();
                    string description = DefGeneration.ElevatedDescription(baseDef.description,
                        "EGP_ElevatedDescriptionSuffix".Translate().Resolve());
                    Relabel(elevated, label, description, fresh);
                    WarnIfCompsDiffer(baseDef, elevated);
                    WarnIfMortar(baseDef);
                }
            }
            catch (Exception e)
            {
                Log.Error(DefGeneration.LogPrefix + "Translating generated defs failed: " + e);
            }
        }

        private static void Relabel(ThingDef def, string label, string description, ThingDef fresh)
        {
            string oldLabel = def.label;
            string oldDescription = def.description;
            if (label == oldLabel && description == oldDescription)
            {
                return;
            }
            def.label = label;
            def.description = description;
            ResetCachedLabels(def, fresh);
            // Vanilla labels a blueprint or frame "<label><suffix>": keep the suffix.
            RelabelDerived(def.blueprintDef, oldLabel, label, oldDescription, description, fresh);
            RelabelDerived(def.frameDef, oldLabel, label, oldDescription, description, fresh);
        }

        private static void RelabelDerived(ThingDef derived, string oldLabel, string label, string oldDescription,
            string description, ThingDef fresh)
        {
            if (derived == null)
            {
                return;
            }
            if (!oldLabel.NullOrEmpty() && derived.label != null && derived.label.StartsWith(oldLabel, StringComparison.Ordinal))
            {
                derived.label = label + derived.label.Substring(oldLabel.Length);
            }
            if (derived.description == oldDescription)
            {
                derived.description = description;
            }
            ResetCachedLabels(derived, fresh);
        }

        private static void ResetCachedLabels(Def def, Def fresh)
        {
            foreach (FieldInfo field in cachedLabelFields)
            {
                if (field.DeclaringType.IsInstanceOfType(def) && field.DeclaringType.IsInstanceOfType(fresh))
                {
                    field.SetValue(def, field.GetValue(fresh));
                }
            }
        }

        private static List<FieldInfo> FindCachedLabelFields()
        {
            var fields = new List<FieldInfo>();
            for (Type type = typeof(ThingDef); type != null && typeof(Def).IsAssignableFrom(type); type = type.BaseType)
            {
                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.Name.IndexOf("cachedLabel", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        fields.Add(field);
                    }
                }
            }
            return fields;
        }

        /// <summary>Generation could not ask building.IsMortar (defs were unresolved then); now it can.</summary>
        private static void WarnIfMortar(ThingDef baseDef)
        {
            try
            {
                if (baseDef.building != null && baseDef.building.IsMortar)
                {
                    Log.Warning(DefGeneration.LogPrefix + baseDef.defName + " is a mortar (building.IsMortar) but was made mountable on a platform.");
                }
            }
            catch (Exception e)
            {
                Log.Warning(DefGeneration.LogPrefix + "Could not check whether " + baseDef.defName + " is a mortar: " + e.Message);
            }
        }

        /// <summary>Mounting keeps the turret's comps, so both defs must list the same comp types.</summary>
        private static void WarnIfCompsDiffer(ThingDef baseDef, ThingDef elevated)
        {
            List<CompProperties> a = baseDef.comps;
            List<CompProperties> b = elevated.comps;
            int count = a?.Count ?? 0;
            bool same = count == (b?.Count ?? 0);
            for (int i = 0; same && i < count; i++)
            {
                same = a[i]?.GetType() == b[i]?.GetType() && a[i]?.compClass == b[i]?.compClass;
            }
            if (!same)
            {
                Log.Warning(DefGeneration.LogPrefix + elevated.defName + " no longer has the same comps as " + baseDef.defName
                    + " (changed after def generation); mounting that turret may misbehave.");
            }
        }
    }
}
