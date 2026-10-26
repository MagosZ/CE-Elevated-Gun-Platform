using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ElevatedGunPlatform
{
    /// <summary>Shared checks for mounting: which turret a def or constructible builds, exact platform matches,
    /// hit point maxima and platform respawning.</summary>
    public static class MountingUtility
    {
        private static readonly HashSet<string> warned = new HashSet<string>();

        internal static void WarnOnce(string key, string message)
        {
            if (warned.Add(key))
            {
                Log.Warning("[ElevatedGunPlatform] " + message);
            }
        }

        /// <summary>The eligible base turret a def builds: the def itself, the def a blueprint/frame builds, or the
        /// base of an elevated def (reinstalling an elevated turret places its base turret). Null otherwise.</summary>
        public static ThingDef EligibleTurretOf(BuildableDef def)
        {
            ThingDef thingDef = def as ThingDef;
            if (thingDef == null)
            {
                return null;
            }
            ThingDef built = GenConstruct.BuiltDefOf(thingDef) as ThingDef ?? thingDef;
            if (EGP.IsEligibleBase(built))
            {
                return built;
            }
            if (EGP.IsElevated(built))
            {
                return EGP.BaseOf(built);
            }
            return null;
        }

        /// <summary>The eligible base turret a blueprint, frame or install blueprint will produce, or null.</summary>
        public static ThingDef EligibleTurretOf(Thing constructible)
        {
            if (constructible is Blueprint_Install install)
            {
                return EligibleTurretOf(install.ThingToInstall?.def);
            }
            if (constructible is Blueprint || constructible is Frame)
            {
                return EligibleTurretOf(constructible.def);
            }
            return null;
        }

        public static bool IsPlatformBlueprintOrFrame(ThingDef def)
        {
            return def != null && (def.IsBlueprint || def.IsFrame) && EGP.IsPlatform(def.entityDefToBuild as ThingDef);
        }

        public static bool SameRect(CellRect a, CellRect b)
        {
            return a.minX == b.minX && a.maxX == b.maxX && a.minZ == b.minZ && a.maxZ == b.maxZ;
        }

        /// <summary>Exact match: same unrotated size and the same occupied rect.</summary>
        public static bool Matches(Thing platformLike, IntVec2 turretSize, CellRect turretRect)
        {
            return platformLike != null && platformLike.def.size == turretSize
                && SameRect(platformLike.OccupiedRect(), turretRect);
        }

        /// <summary>A spawned, completed platform that exactly matches the turret's footprint, or null.</summary>
        public static Thing FindMatchingPlatform(Map map, ThingDef turret, IntVec3 center, Rot4 rot)
        {
            if (map == null || turret == null)
            {
                return null;
            }
            CellRect rect = GenAdj.OccupiedRect(center, rot, turret.size);
            foreach (IntVec3 cell in rect)
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }
                List<Thing> things = cell.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing t = things[i];
                    if (EGP.IsPlatform(t.def) && t.Spawned && !t.Destroyed && Matches(t, turret.size, rect))
                    {
                        return t;
                    }
                }
            }
            return null;
        }

        /// <summary>The platform an eligible base turret spawning here would absorb (DESIGN 2.2), or null: the same
        /// checks the GenSpawn.Spawn mount makes before it removes the platform.</summary>
        public static Thing MountablePlatform(ThingDef baseDef, IntVec3 center, Rot4 rot, Map map)
        {
            ThingDef elevatedDef = EGP.ElevatedOf(baseDef);
            if (elevatedDef == null)
            {
                return null;
            }
            Thing platform = FindMatchingPlatform(map, baseDef, center, rot);
            if (platform == null || (platform.Stuff == null && elevatedDef.MadeFromStuff))
            {
                return null;
            }
            return platform;
        }

        public enum PlatformFit
        {
            /// <summary>The footprint touches no platform.</summary>
            NoPlatform,
            /// <summary>The footprint exactly covers one finished platform.</summary>
            Fits,
            /// <summary>The footprint touches a finished platform of another size or position.</summary>
            WrongPlatform,
            /// <summary>The footprint touches a platform that is still a blueprint or frame.</summary>
            Unfinished,
        }

        /// <summary>Placement rule of DESIGN 2.1: if the turret footprint touches any platform (or a platform
        /// blueprint/frame), it must exactly match one completed platform. An elevated turret given as
        /// thingToIgnore (it is being reinstalled) counts as the platform it will leave behind.</summary>
        public static PlatformFit CheckFootprint(ThingDef turret, IntVec3 center, Rot4 rot, Map map, Thing thingToIgnore)
        {
            if (turret == null || map == null)
            {
                return PlatformFit.NoPlatform;
            }
            CellRect rect = GenAdj.OccupiedRect(center, rot, turret.size);
            bool wrong = false, unfinished = false;
            foreach (IntVec3 cell in rect)
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }
                List<Thing> things = cell.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing t = things[i];
                    bool platformLike = EGP.IsPlatform(t.def) || (t == thingToIgnore && EGP.IsElevated(t.def));
                    if (platformLike)
                    {
                        if (Matches(t, turret.size, rect))
                        {
                            return PlatformFit.Fits;
                        }
                        wrong = true;
                    }
                    else if (IsPlatformBlueprintOrFrame(t.def))
                    {
                        unfinished = true;
                    }
                }
            }
            if (wrong)
            {
                return PlatformFit.WrongPlatform;
            }
            return unfinished ? PlatformFit.Unfinished : PlatformFit.NoPlatform;
        }

        /// <summary>True if a building placed at center/rot would have its interaction spot (where pawns stand to man
        /// or reload it) on a platform, a platform blueprint/frame, or an elevated turret other than thingToIgnore.
        /// Vanilla rejects such a spot as blocked.</summary>
        public static bool InteractionSpotOnPlatform(ThingDef def, IntVec3 center, Rot4 rot, Map map, Thing thingToIgnore)
        {
            if (def == null || !def.hasInteractionCell || map == null)
            {
                return false;
            }
            IntVec3 spot = center + def.interactionCellOffset.RotatedBy(rot);
            if (!spot.InBounds(map))
            {
                return false;
            }
            List<Thing> things = spot.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                Thing t = things[i];
                if (t != thingToIgnore && (EGP.IsPlatform(t.def) || EGP.IsElevated(t.def) || IsPlatformBlueprintOrFrame(t.def)))
                {
                    return true;
                }
            }
            return false;
        }

        public static ThingDef StuffOrDefault(ThingDef def, ThingDef stuff)
        {
            if (def == null || !def.MadeFromStuff)
            {
                return null;
            }
            return stuff ?? GenStuff.DefaultStuffFor(def);
        }

        /// <summary>Max HP of the base turret with its own stuff, as the MaxHitPoints StatPart uses it.</summary>
        public static int BaseMaxHitPoints(ThingDef baseDef, ThingDef baseStuff)
        {
            if (baseDef == null)
            {
                return 1;
            }
            float value = baseDef.GetStatValueAbstract(StatDefOf.MaxHitPoints, StuffOrDefault(baseDef, baseStuff));
            return Mathf.Max(1, Mathf.RoundToInt(value));
        }

        public static int PlatformMaxHitPoints(ThingDef platformDef, ThingDef platformStuff)
        {
            if (platformDef == null)
            {
                return 1;
            }
            float value = platformDef.GetStatValueAbstract(StatDefOf.MaxHitPoints, StuffOrDefault(platformDef, platformStuff));
            return Mathf.Max(1, Mathf.RoundToInt(value));
        }

        /// <summary>Spawns the recorded platform at an elevated turret's former position and rotation (DESIGN 2.3),
        /// so it covers exactly the turret's former footprint. hitPoints below 0 means half the platform's max HP.</summary>
        public static Thing RespawnPlatform(PlatformRecord record, IntVec3 cell, Rot4 rot, Map map, Faction faction, int hitPoints)
        {
            if (record == null || record.platformDef == null || map == null || !cell.IsValid || !cell.InBounds(map))
            {
                return null;
            }
            Thing platform = ThingMaker.MakeThing(record.platformDef, StuffOrDefault(record.platformDef, record.platformStuff));
            if (faction != null && platform.def.CanHaveFaction)
            {
                platform.SetFaction(faction);
            }
            // Hit points before spawning, so everything that looks at the platform as it spawns sees them.
            int max = platform.MaxHitPoints;
            int hp = hitPoints < 0 ? max / 2 : hitPoints;
            platform.HitPoints = Mathf.Clamp(hp, 1, max);
            GenSpawn.Spawn(platform, cell, map, rot.IsValid ? rot : record.platformRotation, WipeMode.VanishOrMoveAside);
            NotifyRepairable(platform);
            return platform;
        }

        /// <summary>A damaged building whose hit points were set by code joins its faction's repair list. Vanilla
        /// updates that list when a building takes damage (seen in game: a respawned half-HP platform was repaired
        /// only after it took damage again), so this sends the same notice. Does nothing for an undamaged one.</summary>
        public static void NotifyRepairable(Thing thing)
        {
            if (thing is Building building && building.Spawned && building.Faction != null
                && building.HitPoints < building.MaxHitPoints)
            {
                building.Map.listerBuildingsRepairable.Notify_BuildingTookDamage(building);
            }
        }
    }
}
