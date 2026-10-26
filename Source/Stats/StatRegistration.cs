using System.Collections.Generic;
using CombatExtended;
using RimWorld;
using Verse;

namespace ElevatedGunPlatform
{
    /// <summary>Appends the elevated-turret StatParts to their StatDefs once defs are loaded.</summary>
    [StaticConstructorOnStartup]
    public static class StatRegistration
    {
        private static bool registered;

        static StatRegistration()
        {
            Register();
        }

        public static void Register()
        {
            if (registered)
            {
                return;
            }
            registered = true;
            Add(StatDefOf.MaxHitPoints, new StatPart_ElevatedMaxHitPoints());
            Add(StatDefOf.Flammability, new StatPart_ElevatedFlammability());
            // First in the list, so quality and the health curve still apply to the elevated value.
            Add(StatDefOf.MarketValue, new StatPart_ElevatedMarketValue(), first: true);
            // Same value without the health curve; kept consistent with MarketValue.
            Add(DefDatabase<StatDef>.GetNamedSilentFail("MarketValueIgnoreHp"), new StatPart_ElevatedMarketValue(), false, true);
            Add(CE_StatDefOf.MagazineCapacity, new StatPart_ElevatedMagazine());
            // Last in the list: the flat value replaces quality and everything else.
            Add(StatDefOf.Beauty, new StatPart_ElevatedBeauty());
            Add(DefDatabase<StatDef>.GetNamedSilentFail("BeautyOutdoors"), new StatPart_ElevatedBeauty(), false);
        }

        private static void Add(StatDef stat, StatPart part, bool warnIfMissing = true, bool first = false)
        {
            if (stat == null)
            {
                if (warnIfMissing)
                {
                    Log.Warning($"[ElevatedGunPlatform] StatDef for {part.GetType().Name} not found; elevated turrets keep the normal value.");
                }
                return;
            }
            if (stat.parts == null)
            {
                stat.parts = new List<StatPart>();
            }
            foreach (StatPart existing in stat.parts)
            {
                if (existing != null && existing.GetType() == part.GetType())
                {
                    return;
                }
            }
            part.parentStat = stat;
            if (first)
            {
                stat.parts.Insert(0, part);
            }
            else
            {
                stat.parts.Add(part);
            }
        }
    }
}
