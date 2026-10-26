using CombatExtended;
using RimWorld;
using UnityEngine;
using Verse;

namespace ElevatedGunPlatform
{
    /// <summary>The elevated turret behind a stat request, resolved to its base turret and both materials.
    /// Works for a turret thing (from its record) and for a def-only request (elevated def + platform stuff).</summary>
    public struct ElevatedStatInfo
    {
        public Thing thing;
        public ThingDef elevatedDef;
        public ThingDef baseDef;
        /// <summary>The turret's own stuff; null when the base turret is not made from stuff.</summary>
        public ThingDef baseStuff;
        public ThingDef platformDef;
        public ThingDef platformStuff;

        public static bool TryGet(StatRequest req, out ElevatedStatInfo info)
        {
            info = default(ElevatedStatInfo);
            ThingDef def;
            ThingDef stuff;
            PlatformRecord record = null;
            if (req.HasThing)
            {
                Thing thing = req.Thing;
                def = thing.def;
                if (!EGP.IsElevated(def))
                {
                    return false;
                }
                info.thing = thing;
                record = EGP.RecordOf(thing);
                stuff = thing.Stuff;
            }
            else
            {
                def = req.Def as ThingDef;
                if (!EGP.IsElevated(def))
                {
                    return false;
                }
                stuff = req.StuffDef;
            }

            ThingDef baseDef = EGP.BaseOf(def);
            if (baseDef == null)
            {
                return false;
            }
            info.elevatedDef = def;
            info.baseDef = baseDef;
            info.platformDef = record?.platformDef ?? EGP.PlatformForSize(baseDef.size);
            info.platformStuff = record?.platformStuff ?? stuff;
            if (info.platformStuff == null)
            {
                info.platformStuff = GenStuff.DefaultStuffFor(def);
            }
            if (baseDef.MadeFromStuff)
            {
                info.baseStuff = record?.baseStuff ?? GenStuff.DefaultStuffFor(baseDef);
            }
            return true;
        }

        /// <summary>"steel high turret platform (2×2)".</summary>
        public string PlatformLabel
        {
            get
            {
                if (platformDef != null)
                {
                    return GenLabel.ThingLabel(platformDef, platformStuff);
                }
                return platformStuff != null ? platformStuff.LabelAsStuff : elevatedDef?.label ?? string.Empty;
            }
        }

        /// <summary>Factor of the platform stuff on MaxHitPoints (1 without stuff).</summary>
        public float PlatformStuffHitPointFactor =>
            platformStuff?.stuffProps?.statFactors?.GetStatFactorFromList(StatDefOf.MaxHitPoints) ?? 1f;
    }

    /// <summary>Shared base of the absolute overrides on elevated turrets.</summary>
    public abstract class StatPart_ElevatedBase : StatPart
    {
        protected StatDef Stat(StatDef fallback) => parentStat ?? fallback;

        protected string Format(float value) =>
            parentStat != null ? parentStat.ValueToString(value) : value.ToString("0.##");

        protected static EGPSettings Settings => ElevatedGunPlatformMod.Settings;
    }

    /// <summary>MaxHitPoints: base turret max HP (with its own stuff) x (1 + bonus x platform stuff HP factor), with the
    /// bonus of the platform's tier (low or high).</summary>
    public class StatPart_ElevatedMaxHitPoints : StatPart_ElevatedBase
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (ElevatedStatInfo.TryGet(req, out ElevatedStatInfo info))
            {
                val = Calculate(info, out _, out _);
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!ElevatedStatInfo.TryGet(req, out ElevatedStatInfo info))
            {
                return null;
            }
            float result = Calculate(info, out float baseHp, out float factor);
            return "EGP_StatExplain_MaxHitPoints".Translate(info.PlatformLabel, Format(baseHp),
                HpBonus(info).ToStringPercent(), factor.ToStringPercent(), Format(result));
        }

        private static float HpBonus(ElevatedStatInfo info) => EGPSettings.CurrentHpBonus(EGP.TierOf(info.platformDef));

        private float Calculate(ElevatedStatInfo info, out float baseHp, out float factor)
        {
            // Base def, not the elevated one: no recursion into this part.
            baseHp = info.baseDef.GetStatValueAbstract(Stat(StatDefOf.MaxHitPoints), info.baseStuff);
            factor = info.PlatformStuffHitPointFactor;
            return Mathf.Max(1f, Mathf.Round(baseHp * (1f + HpBonus(info) * factor)));
        }
    }

    /// <summary>Flammability: the higher of the base turret's and a Wall's made from the platform stuff.</summary>
    public class StatPart_ElevatedFlammability : StatPart_ElevatedBase
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (ElevatedStatInfo.TryGet(req, out ElevatedStatInfo info))
            {
                val = Calculate(info, out _, out _);
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!ElevatedStatInfo.TryGet(req, out ElevatedStatInfo info))
            {
                return null;
            }
            float result = Calculate(info, out float turret, out float wall);
            return "EGP_StatExplain_Flammability".Translate(info.PlatformLabel, Format(turret), Format(wall), Format(result));
        }

        private float Calculate(ElevatedStatInfo info, out float turret, out float wall)
        {
            StatDef stat = Stat(StatDefOf.Flammability);
            turret = info.baseDef.GetStatValueAbstract(stat, info.baseStuff);
            wall = ThingDefOf.Wall != null ? ThingDefOf.Wall.GetStatValueAbstract(stat, info.platformStuff) : turret;
            return Mathf.Max(turret, wall);
        }
    }

    /// <summary>MarketValue: base turret value (with its own stuff) + platform value (with the platform stuff).</summary>
    public class StatPart_ElevatedMarketValue : StatPart_ElevatedBase
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (ElevatedStatInfo.TryGet(req, out ElevatedStatInfo info))
            {
                val = Calculate(info, out _, out _);
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!ElevatedStatInfo.TryGet(req, out ElevatedStatInfo info))
            {
                return null;
            }
            float result = Calculate(info, out float turret, out float platform);
            return "EGP_StatExplain_MarketValue".Translate(info.PlatformLabel, Format(turret), Format(platform), Format(result));
        }

        private float Calculate(ElevatedStatInfo info, out float turret, out float platform)
        {
            StatDef stat = Stat(StatDefOf.MarketValue);
            turret = info.baseDef.GetStatValueAbstract(stat, info.baseStuff);
            platform = info.platformDef != null ? info.platformDef.GetStatValueAbstract(stat, info.platformStuff) : 0f;
            return turret + platform;
        }
    }

    /// <summary>Beauty and outdoor beauty: a flat -10 for elevated turrets and turret platforms, whatever the turret's
    /// own beauty, the material or the quality. Added last, so it is the final value.</summary>
    public class StatPart_ElevatedBeauty : StatPart_ElevatedBase
    {
        public const float FlatBeauty = -10f;

        public override void TransformValue(StatRequest req, ref float val)
        {
            if (AppliesTo(req))
            {
                val = FlatBeauty;
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            return AppliesTo(req) ? (string)"EGP_StatExplain_Beauty".Translate(Format(FlatBeauty)) : null;
        }

        private static bool AppliesTo(StatRequest req)
        {
            ThingDef def = req.HasThing ? req.Thing.def : req.Def as ThingDef;
            return EGP.IsElevated(def) || EGP.IsPlatform(def);
        }
    }

    /// <summary>CE MagazineCapacity: x ammoMultiplier for the gun of an elevated turret, and for the elevated
    /// turret itself (its info card; CE's StatWorker_Magazine maps a turret building def to its gun).</summary>
    public class StatPart_ElevatedMagazine : StatPart_ElevatedBase
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (AppliesTo(req))
            {
                val *= Multiplier;
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            return AppliesTo(req) ? (string)"EGP_StatExplain_Magazine".Translate(Multiplier.ToString("0.##")) : null;
        }

        private static float Multiplier => Settings?.ammoMultiplier ?? 1f;

        private static bool AppliesTo(StatRequest req)
        {
            if (req.HasThing)
            {
                Thing thing = req.Thing;
                if (EGP.IsElevated(thing.def))
                {
                    return true;
                }
                Building_Turret turret = thing.TryGetComp<CompAmmoUser>()?.turret;
                return turret != null && EGP.IsElevated(turret.def);
            }
            return EGP.IsElevated(req.Def as ThingDef);
        }
    }
}
