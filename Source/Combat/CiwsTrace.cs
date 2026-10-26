using System;
using System.Collections.Generic;
using System.Linq;
using CombatExtended;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ElevatedGunPlatform
{
    /// <summary>Diagnostic for an in-game report (elevated CIWS turrets did not intercept drop pods or shuttles; TESTING
    /// 4.4). Only with dev mode and the mod's debug overlay setting on. Every tick of a CE CIWS turret, on the ground or
    /// elevated, it checks the incoming skyfallers and logs once when one is first on the map, whatever its range (with
    /// the turret's state), once when it targets it, and once when it lands without having been targeted (with the
    /// state again).</summary>
    [HarmonyPatch(typeof(Building_TurretGunCE), nameof(Building_TurretGunCE.Tick))]
    public static class CiwsTrace_Tick
    {
        public static void Postfix(Building_TurretGunCE __instance)
        {
            if (CiwsTrace.Active)
            {
                CiwsTrace.LogTick(__instance);
            }
        }
    }

    public static class CiwsTrace
    {
        private const int MaxPairs = 4000;
        private const int LandingTicks = 3;

        // (turret, skyfaller) pairs already logged as seen, as targeted, and as landed; reset for each game.
        private static readonly HashSet<(int, int)> seen = new HashSet<(int, int)>();
        private static readonly HashSet<(int, int)> targeted = new HashSet<(int, int)>();
        private static readonly HashSet<(int, int)> landed = new HashSet<(int, int)>();
        private static readonly HashSet<string> askers = new HashSet<string>();
        private static Game game;

        // protected bool holdFire
        private static readonly AccessTools.FieldRef<Building_TurretGunCE, bool> HoldFire = ResolveHoldFire();

        private static AccessTools.FieldRef<Building_TurretGunCE, bool> ResolveHoldFire()
        {
            try
            {
                return AccessTools.FieldRefAccess<Building_TurretGunCE, bool>("holdFire");
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static bool Active => ConduitTrace.Active;

        /// <summary>This mod's line of fire (asked by a verb) or sight (asked for a seer) was asked about a target in the
        /// air for an elevated turret, and kept CE's answer (line of fire, from E's raised muzzle) or vanilla's (sight).
        /// Logged once per question and asker type; the message's stack trace shows the caller.</summary>
        public static void AskedAboutAirborne(string question, object asker, Thing target, string answer)
        {
            if (!Active)
            {
                return;
            }
            ResetIfNewGame();
            string askerType = asker?.GetType().FullName ?? "null";
            if (askers.Add(question + "|" + askerType))
            {
                Log.Message($"[ElevatedGunPlatform] Debug: this mod's {question} was asked about {target?.def?.defName} in the air "
                    + $"({askerType}); it keeps {answer}. The caller is in this message's stack trace.");
            }
        }

        public static void LogTick(Building_TurretGunCE turret)
        {
            if (!(turret is Building_CIWS_CE) || !turret.Spawned)
            {
                return;
            }
            try
            {
                ResetIfNewGame();
                foreach (Thing thing in turret.Map.listerThings.ThingsInGroup(ThingRequestGroup.ActiveTransporter))
                {
                    // Leaving pods and shuttles are in the group too; their ticksToImpact counts up from below zero.
                    if (thing is Skyfaller skyfaller && !(skyfaller.def.skyfaller?.reversed ?? false))
                    {
                        LogPair(turret, skyfaller);
                    }
                }
            }
            catch (Exception e)
            {
                MountingUtility.WarnOnce("CiwsTrace", "CIWS debug trace failed: " + e);
            }
        }

        private static void ResetIfNewGame()
        {
            if (game != Current.Game || seen.Count > MaxPairs)
            {
                game = Current.Game;
                seen.Clear();
                targeted.Clear();
                landed.Clear();
                askers.Clear();
            }
        }

        private static void LogPair(Building_TurretGunCE turret, Skyfaller skyfaller)
        {
            var pair = (turret.thingIDNumber, skyfaller.thingIDNumber);
            bool first = seen.Add(pair);
            bool nowTargeted = turret.CurrentTarget.Thing == skyfaller && targeted.Add(pair);
            bool landing = skyfaller.ticksToImpact <= LandingTicks && !targeted.Contains(pair) && landed.Add(pair);
            if (!first && !nowTargeted && !landing)
            {
                return;
            }
            string where = $"CIWS {turret.LabelShort} at {turret.Position} ({(EGP.IsElevated(turret) ? "elevated" : "on the ground")}), "
                + $"{skyfaller.def.defName} landing at {skyfaller.Position} ({turret.Position.DistanceTo(skyfaller.Position):0} cells, "
                + $"impact in {skyfaller.ticksToImpact} ticks)";
            if (first)
            {
                string verbs = string.Join("; ", turret.GunCompEq.AllVerbs.Where(v => v is ITargetSearcher)
                    .Select(v => $"{v.GetType().FullName} available {v.Available()}, its own hold fire {(v as VerbCIWS)?.HoldFire}"));
                Log.Message($"[ElevatedGunPlatform] Debug: {where}: seen. {State(turret)}; target searchers: {(verbs.Length > 0 ? verbs : "none")}.");
            }
            if (nowTargeted)
            {
                Log.Message($"[ElevatedGunPlatform] Debug: {where}: targeted.");
            }
            if (landing)
            {
                Log.Message($"[ElevatedGunPlatform] Debug: {where}: lands without having been targeted by this turret. {State(turret)}.");
            }
        }

        /// <summary>What decides whether CE's Tick lets the turret search: hold fire, power and the like, crew, reload,
        /// warmup, cooldown, burst, ammo, and the forced and current targets.</summary>
        private static string State(Building_TurretGunCE turret)
        {
            bool holdFire = HoldFire != null && HoldFire(turret);
            AccessTools.FieldRef<Building_Turret, LocalTargetInfo> forcedRef = TurretConversion.ForcedTargetRef;
            string forced = forcedRef == null ? "unknown" : forcedRef(turret).IsValid ? forcedRef(turret).ToString() : "none";
            string crew = turret.mannableComp == null ? "unmanned" : turret.mannableComp.MannedNow ? "manned" : "not manned";
            return $"Turret: hold fire {holdFire}, active {turret.Active}, {crew}, reloading {turret.isReloading}, "
                + $"warmup {turret.burstWarmupTicksLeft}, cooldown {turret.burstCooldownTicksLeft}, verb {turret.AttackVerb?.state}, "
                + $"rounds {turret.CompAmmo?.CurMagCount.ToString() ?? "n/a"}, forced target {forced}, target {turret.CurrentTarget}";
        }
    }
}
