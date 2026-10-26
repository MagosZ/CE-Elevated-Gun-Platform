using System;
using HarmonyLib;
using Verse;

namespace ElevatedGunPlatform
{
    /// <summary>DESIGN 2.2: an eligible base turret about to spawn exactly over a platform absorbs the platform and
    /// spawns as the elevated turret, in place. Covers build completion, install, reinstall and dev spawning.
    /// Runs first so other Spawn prefixes see the final def, cell and rotation.</summary>
    [HarmonyPatch(typeof(GenSpawn), nameof(GenSpawn.Spawn),
        new[] { typeof(Thing), typeof(IntVec3), typeof(Map), typeof(Rot4), typeof(WipeMode), typeof(bool), typeof(bool) })]
    public static class MountPatch_GenSpawnSpawn
    {
        // The turret mounted by the running Spawn call, for the postfix (Spawn can nest, so it is matched by object).
        [ThreadStatic]
        private static Thing mounted;

        [HarmonyPriority(Priority.First)]
        public static void Prefix(Thing newThing, ref IntVec3 loc, Map map, ref Rot4 rot, bool respawningAfterLoad)
        {
            if (respawningAfterLoad || newThing == null || map == null || !EGP.IsEligibleBase(newThing.def)
                || newThing.Spawned || newThing.Destroyed)
            {
                return;
            }
            try
            {
                TryMount(newThing, ref loc, map, ref rot);
            }
            catch (Exception e)
            {
                Log.Error($"[ElevatedGunPlatform] Mounting {newThing.ToStringSafe()} failed: {e}");
            }
        }

        /// <summary>A turret mounted with fewer hit points than its new maximum (a damaged turret or platform) joins
        /// the repair list once it has spawned.</summary>
        public static void Postfix(Thing newThing)
        {
            if (newThing == null)
            {
                return;
            }
            if (newThing != mounted)
            {
                // Installed on the ground: its arc is the ground turret's again, so forget the platform direction.
                if (EGP.IsEligibleBase(newThing.def) && newThing.PlatformComp() is CompElevatedPlatform comp)
                {
                    comp.arcMapAngle = float.NaN;
                }
                return;
            }
            mounted = null;
            try
            {
                MountingUtility.NotifyRepairable(newThing);
            }
            catch (Exception e)
            {
                MountingUtility.WarnOnce("NotifyRepairable", "Could not queue " + newThing.ToStringSafe() + " for repair: " + e.Message);
            }
        }

        private static void TryMount(Thing turret, ref IntVec3 loc, Map map, ref Rot4 rot)
        {
            ThingDef baseDef = turret.def;
            ThingDef elevatedDef = EGP.ElevatedOf(baseDef);
            if (elevatedDef == null || turret.PlatformComp() == null)
            {
                return;
            }
            Thing platform = MountingUtility.FindMatchingPlatform(map, baseDef, loc, rot);
            if (platform == null)
            {
                return;
            }
            if (platform.Stuff == null && elevatedDef.MadeFromStuff)
            {
                MountingUtility.WarnOnce("platformStuff:" + platform.def.defName,
                    $"Platform {platform.ToStringSafe()} has no stuff; {turret.ToStringSafe()} is not mounted.");
                return;
            }

            var record = new PlatformRecord
            {
                platformDef = platform.def,
                platformStuff = platform.Stuff,
                baseStuff = turret.Stuff,
                platformHitPoints = platform.HitPoints,
                platformRotation = platform.Rotation,
            };
            IntVec3 platformPosition = platform.Position;

            using (RoofHolderGuard.Begin(platform.def))
            {
                platform.Destroy(DestroyMode.Vanish);
            }

            if (!TurretConversion.ToElevated(turret, elevatedDef, record))
            {
                Log.Error($"[ElevatedGunPlatform] Could not convert {turret.ToStringSafe()} after removing its platform.");
                return;
            }
            // Same occupied rect as the platform, also for even sizes on a rotated platform. The firing arc keeps the
            // direction the player gave the turret's blueprint (DESIGN 3.9).
            if (!TurretConversion.RestoreFireArcMapAngle(turret, record.platformRotation))
            {
                TurretConversion.RebaseFireArc(turret, rot, record.platformRotation);
            }
            loc = platformPosition;
            rot = record.platformRotation;
            mounted = turret;
        }
    }
}
