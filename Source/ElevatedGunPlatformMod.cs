using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ElevatedGunPlatform
{
    public class ElevatedGunPlatformMod : Mod
    {
        public const string HarmonyId = "you.ce.elevatedgunplatform";

        public static EGPSettings Settings;
        public static ModContentPack ContentPack;
        public static Harmony HarmonyInstance;

        private string stuffPerCellBuffer;
        private string componentsBuffer;

        public ElevatedGunPlatformMod(ModContentPack content) : base(content)
        {
            ContentPack = content;
            Settings = GetSettings<EGPSettings>();
            // Patched here (not in a static constructor) so the def generation patch exists before defs load.
            HarmonyInstance = new Harmony(HarmonyId);
            PatchAllClasses();
        }

        /// <summary>Patches each class on its own, so one missing target can't stop the rest. Reading a class's
        /// attributes resolves the types they name, so that happens inside the per-class try too.</summary>
        private static void PatchAllClasses()
        {
            foreach (Type type in AccessTools.GetTypesFromAssembly(typeof(ElevatedGunPlatformMod).Assembly))
            {
                try
                {
                    if (type.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0)
                    {
                        continue;
                    }
                    HarmonyInstance.CreateClassProcessor(type).Patch();
                }
                catch (Exception e)
                {
                    Log.Error($"[ElevatedGunPlatform] Failed to apply patch class {type.FullName}: {e}");
                }
            }
        }

        public override string SettingsCategory() => "EGP_SettingsCategory".Translate();

        /// <summary>A raised hit point bonus leaves elevated turrets below their new max HP without them taking damage,
        /// so vanilla's repair list doesn't know about them: queue them once the settings are saved.</summary>
        public override void WriteSettings()
        {
            base.WriteSettings();
            if (Current.ProgramState != ProgramState.Playing || Find.Maps == null)
            {
                return;
            }
            try
            {
                foreach (Map map in Find.Maps)
                {
                    foreach (Thing thing in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial).ToList())
                    {
                        if (EGP.IsElevated(thing.def))
                        {
                            TurretConversion.ClearStatCaches(thing);
                            MountingUtility.NotifyRepairable(thing);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warning("[ElevatedGunPlatform] Could not queue elevated turrets for repair after a settings change: " + e.Message);
            }
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var s = Settings;
            var list = new Listing_Standard();
            list.Begin(inRect);

            s.highFloor = FloorSlider(list, "EGP_Setting_HighFloor", s.highFloor);
            s.lowFloor = FloorSlider(list, "EGP_Setting_LowFloor", s.lowFloor);
            s.hpBonusHigh = HpSlider(list, "EGP_Setting_HpBonusHigh", s.hpBonusHigh);
            s.hpBonusLow = HpSlider(list, "EGP_Setting_HpBonusLow", s.hpBonusLow);

            s.ammoMultiplier = list.SliderLabeled("EGP_Setting_AmmoMultiplier".Translate(s.ammoMultiplier.ToString("0.##")),
                s.ammoMultiplier, 1f, 4f, tooltip: "EGP_Setting_AmmoMultiplier_Tip".Translate());
            s.ammoMultiplier = Mathf.Round(s.ammoMultiplier * 4f) / 4f;
            list.CheckboxLabeled("EGP_Setting_PlainRoofs".Translate(), ref s.plainRoofs, "EGP_Setting_PlainRoofs_Tip".Translate());

            list.Gap();
            list.Label("EGP_Setting_RestartNote".Translate());
            list.TextFieldNumericLabeled("EGP_Setting_StuffPerCell".Translate(), ref s.stuffPerCell, ref stuffPerCellBuffer, 1, 100);
            list.TextFieldNumericLabeled("EGP_Setting_Components".Translate(), ref s.components, ref componentsBuffer, 0, 10);

            list.Gap();
            list.CheckboxLabeled("EGP_Setting_Debug".Translate(), ref s.debugOverlay, "EGP_Setting_Debug_Tip".Translate());

            list.End();
        }

        private static int FloorSlider(Listing_Standard list, string key, int floor)
        {
            float value = list.SliderLabeled(key.Translate(floor, (floor * EGPSettings.FloorHeight).ToString("0.0")), floor,
                EGPSettings.MinFloor, EGPSettings.MaxFloor, tooltip: "EGP_Setting_Floor_Tip".Translate());
            return Mathf.Clamp(Mathf.RoundToInt(value), EGPSettings.MinFloor, EGPSettings.MaxFloor);
        }

        private static float HpSlider(Listing_Standard list, string key, float bonus)
        {
            float value = list.SliderLabeled(key.Translate(bonus.ToStringPercent()), bonus, 0f, 1f,
                tooltip: "EGP_Setting_HpBonus_Tip".Translate());
            return Mathf.Round(value * 100f) / 100f;
        }
    }

    public class EGPSettings : ModSettings
    {
        /// <summary>One floor is a wall's height in CE (CollisionVertical.WallCollisionHeight).</summary>
        public const float FloorHeight = 2f;
        public const int DefaultLowFloor = 1;
        public const int DefaultHighFloor = 3;
        public const int MinFloor = 1;
        public const int MaxFloor = 10;
        public const float DefaultHpBonusLow = 0.2f;
        public const float DefaultHpBonusHigh = 0.4f;

        public float ammoMultiplier = 2f;
        /// <summary>Material per cell and components of a low platform; a high one costs double.</summary>
        public int stuffPerCell = 5;
        public int components = 1;
        /// <summary>Floor a turret on a low or high platform stands on, counted G (ground), 1, 2, ...: floor N is
        /// N x 2.0 above the ground.</summary>
        public int lowFloor = DefaultLowFloor;
        public int highFloor = DefaultHighFloor;
        /// <summary>Turret hit points added by a low or high platform (times the platform material's HP factor).</summary>
        public float hpBonusLow = DefaultHpBonusLow;
        public float hpBonusHigh = DefaultHpBonusHigh;
        /// <summary>Every roof but rock (thin rock roof, overhead mountain) has a built roof's slab (2.0-2.1) in CE's 3D
        /// roof test (RoofSlabs).</summary>
        public bool plainRoofs = true;
        public bool debugOverlay = false;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref hpBonusLow, "hpBonusLow", DefaultHpBonusLow);
            Scribe_Values.Look(ref hpBonusHigh, "hpBonusHigh", DefaultHpBonusHigh);
            Scribe_Values.Look(ref ammoMultiplier, "ammoMultiplier", 2f);
            Scribe_Values.Look(ref stuffPerCell, "stuffPerCell", 5);
            Scribe_Values.Look(ref components, "components", 1);
            Scribe_Values.Look(ref lowFloor, "lowFloor", DefaultLowFloor);
            Scribe_Values.Look(ref highFloor, "highFloor", DefaultHighFloor);
            Scribe_Values.Look(ref plainRoofs, "plainRoofs", true);
            Scribe_Values.Look(ref debugOverlay, "debugOverlay", false);
        }

        /// <summary>Floor of a platform tier from the settings (the defaults before they load).</summary>
        public static int CurrentFloor(PlatformTier tier)
        {
            EGPSettings s = ElevatedGunPlatformMod.Settings;
            int floor = tier == PlatformTier.High
                ? (s != null ? s.highFloor : DefaultHighFloor)
                : (s != null ? s.lowFloor : DefaultLowFloor);
            return Mathf.Clamp(floor, MinFloor, MaxFloor);
        }

        /// <summary>Height of a platform tier's top above the ground, added to the turret's own height.</summary>
        public static float CurrentHeight(PlatformTier tier) => CurrentFloor(tier) * FloorHeight;

        /// <summary>Turret hit point bonus of a platform tier (before the material factor).</summary>
        public static float CurrentHpBonus(PlatformTier tier)
        {
            EGPSettings s = ElevatedGunPlatformMod.Settings;
            return tier == PlatformTier.High
                ? (s != null ? s.hpBonusHigh : DefaultHpBonusHigh)
                : (s != null ? s.hpBonusLow : DefaultHpBonusLow);
        }

        /// <summary>The plain roofs setting (on before the settings load).</summary>
        public static bool PlainRoofsActive => ElevatedGunPlatformMod.Settings == null || ElevatedGunPlatformMod.Settings.plainRoofs;

        /// <summary>Debug overlay is only ever drawn in dev mode.</summary>
        public bool DebugOverlayActive => debugOverlay && Prefs.DevMode;
    }
}
