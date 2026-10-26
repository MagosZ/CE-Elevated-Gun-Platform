using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CombatExtended;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ElevatedGunPlatform
{
    /// <summary>In-place conversion between a base turret and its elevated def (DESIGN 2.4). The Thing object is
    /// kept; only def, stuff, record, HP, graphic caches and stat caches change, and only while it is unspawned.</summary>
    public static class TurretConversion
    {
        /// <summary>Base turret -> elevated turret. The record must already hold platform def/stuff/HP and baseStuff.</summary>
        public static bool ToElevated(Thing turret, ThingDef elevatedDef, PlatformRecord record)
        {
            CompElevatedPlatform comp = turret?.PlatformComp();
            if (comp == null || elevatedDef == null || record == null || turret.Spawned)
            {
                return false;
            }
            ThingDef baseDef = turret.def;
            int baseHp = turret.HitPoints;
            int baseMax = MountingUtility.BaseMaxHitPoints(baseDef, record.baseStuff);
            int platformMax = MountingUtility.PlatformMaxHitPoints(record.platformDef, record.platformStuff);

            turret.def = elevatedDef;
            turret.SetStuffDirect(MountingUtility.StuffOrDefault(elevatedDef, record.platformStuff));
            comp.record = record;
            UseFireArcPropsOf(turret, elevatedDef);
            ClampFireArcSpan(turret);
            ResetCaches(turret);

            int elevatedMax = turret.MaxHitPoints;
            int bonus = Mathf.Max(0, elevatedMax - baseMax);
            float platformFraction = Mathf.Clamp01((float)record.platformHitPoints / platformMax);
            int hp = Mathf.Clamp(baseHp + Mathf.RoundToInt(bonus * platformFraction), 1, elevatedMax);
            // Unmounting takes off exactly this, even if the HP bonus setting changes meanwhile.
            record.bonusHitPoints = Mathf.Max(0, hp - baseHp);
            turret.HitPoints = hp;
            return true;
        }

        /// <summary>Elevated turret -> base turret with its recorded own stuff. Returns the record it removed.</summary>
        public static PlatformRecord ToBase(Thing turret)
        {
            CompElevatedPlatform comp = turret?.PlatformComp();
            PlatformRecord record = comp?.record;
            ThingDef baseDef = EGP.BaseOf(turret?.def);
            if (comp == null || record == null || baseDef == null || turret.Spawned)
            {
                return null;
            }
            int elevatedHp = turret.HitPoints;
            int removed = record.bonusHitPoints;
            if (removed < 0)
            {
                // Older record without the gained HP: recompute them.
                int elevatedMax = turret.MaxHitPoints;
                int baseMax = MountingUtility.BaseMaxHitPoints(baseDef, record.baseStuff);
                int platformMax = MountingUtility.PlatformMaxHitPoints(record.platformDef, record.platformStuff);
                int bonus = Mathf.Max(0, elevatedMax - baseMax);
                removed = Mathf.RoundToInt(bonus * Mathf.Clamp01((float)record.platformHitPoints / platformMax));
            }

            Rot4 elevatedRotation = turret.Rotation;
            // The arc's direction on the map, for a later mount on a platform (the base turret's swing may clip it).
            CompFireArc elevatedArc = (turret as ThingWithComps)?.GetComp<CompFireArc>();
            comp.arcMapAngle = elevatedArc != null
                ? Mathf.DeltaAngle(0f, elevatedArc.CurrentCenterAngle + elevatedRotation.AsAngle)
                : float.NaN;
            turret.def = baseDef;
            turret.SetStuffDirect(MountingUtility.StuffOrDefault(baseDef, record.baseStuff));
            comp.record = null;
            // The elevated def is rotatable, the base def may not be.
            if (!baseDef.rotatable)
            {
                turret.Rotation = Rot4.North;
            }
            // Back to the base turret's arc limits, pointing the same way on the map as far as they allow.
            UseFireArcPropsOf(turret, baseDef);
            RebaseFireArc(turret, elevatedRotation, turret.Rotation);
            ClampFireArcSpan(turret);
            ResetSupplementarySpan(turret);
            ResetCaches(turret);

            turret.HitPoints = Mathf.Clamp(elevatedHp - removed, 1, turret.MaxHitPoints);
            return record;
        }

        /// <summary>DESIGN 3.9: the turret's CE arc comp uses the arc properties of the def it now has (the elevated def
        /// has its own full-circle copy). Comps keep the properties they were made with, so this switches them.</summary>
        private static void UseFireArcPropsOf(Thing turret, ThingDef def)
        {
            CompFireArc arc = (turret as ThingWithComps)?.GetComp<CompFireArc>();
            if (arc?.props == null || def.comps == null)
            {
                return;
            }
            foreach (CompProperties props in def.comps)
            {
                if (props is CompProperties_FireArc arcProps && props.compClass == arc.props.compClass)
                {
                    arc.props = arcProps;
                    return;
                }
            }
        }

        /// <summary>DESIGN 3.9: CE measures a firing arc from the turret's facing. When the facing changes from one
        /// rotation to another (mounting takes the platform's), the arc's centre is shifted so it points the same way
        /// on the map, then kept within the arc's allowed swing around the new facing.</summary>
        public static void RebaseFireArc(Thing turret, Rot4 from, Rot4 to)
        {
            CompFireArc arc = (turret as ThingWithComps)?.GetComp<CompFireArc>();
            if (arc?.Props == null || !from.IsValid || !to.IsValid)
            {
                return;
            }
            float center = Mathf.DeltaAngle(0f, arc.CurrentCenterAngle + from.AsAngle - to.AsAngle);
            float swing = arc.Props.maxSpanDeviation;
            if (swing < 180f)
            {
                center = Mathf.Clamp(center, -swing, swing);
            }
            arc.CurrentCenterAngle = center;
        }

        /// <summary>DESIGN 3.9: on mounting, an arc the turret had as an elevated turret before (its direction on the map
        /// was kept when it was packed up) points that way again, unclipped by the base turret's swing. Returns false
        /// when there is none (then the caller rebases from the blueprint's rotation).</summary>
        public static bool RestoreFireArcMapAngle(Thing turret, Rot4 to)
        {
            CompElevatedPlatform comp = turret.PlatformComp();
            CompFireArc arc = (turret as ThingWithComps)?.GetComp<CompFireArc>();
            if (comp == null || arc == null || float.IsNaN(comp.arcMapAngle) || !to.IsValid)
            {
                return false;
            }
            arc.CurrentCenterAngle = Mathf.DeltaAngle(0f, comp.arcMapAngle - to.AsAngle);
            comp.arcMapAngle = float.NaN;
            return true;
        }

        /// <summary>Keeps a set arc width within the arc properties the comp now uses.</summary>
        private static void ClampFireArcSpan(Thing turret)
        {
            CompFireArc arc = (turret as ThingWithComps)?.GetComp<CompFireArc>();
            if (arc?.Props != null && arc.CurrentSpan >= 0f)
            {
                arc.CurrentSpan = Mathf.Clamp(arc.CurrentSpan, arc.Props.spanRange.min, arc.Props.spanRange.max);
            }
        }

        // CE's private cache of the part of an arc beyond the line behind the facing; CE only refreshes it while the
        // arc is being edited, so an elevated turret's wide arc would otherwise outlive the move back to the ground.
        private static readonly FieldInfo supplementarySpanField = AccessTools.Field(typeof(CompFireArc), "supplementarySpan");

        /// <summary>Recomputes CE's cached wrap part with CE's own formula for the arc properties the comp now uses
        /// (always none for a swing below 180 degrees).</summary>
        private static void ResetSupplementarySpan(Thing turret)
        {
            CompFireArc arc = (turret as ThingWithComps)?.GetComp<CompFireArc>();
            if (arc?.Props == null)
            {
                return;
            }
            if (supplementarySpanField == null || supplementarySpanField.FieldType != typeof(FloatRange?))
            {
                MountingUtility.WarnOnce("supplementarySpan",
                    "CombatExtended.CompFireArc.supplementarySpan not found; a turret packed up from a platform may keep part of its wide arc until its arc is edited.");
                return;
            }
            float swing = arc.Props.maxSpanDeviation;
            float span = arc.CurrentSpan >= 0f ? arc.CurrentSpan : arc.Props.spanRange.max;
            float left = Mathf.Max(arc.CurrentCenterAngle - span / 2f, -swing);
            float right = Mathf.Min(arc.CurrentCenterAngle + span / 2f, swing);
            FloatRange? wrap = left < -180f ? new FloatRange(left + 360f, 180f)
                : right > 180f ? new FloatRange(-180f, right - 360f)
                : (FloatRange?)null;
            supplementarySpanField.SetValue(arc, wrap);
        }

        /// <summary>Graphic and stat caches of the turret and of its gun, and its forced target.</summary>
        public static void ResetCaches(Thing turret)
        {
            ResetGraphics(turret);
            ClearStatCaches(turret);
            Thing gun = GunOf(turret);
            if (gun != null)
            {
                ClearStatCaches(gun);
            }
            ResetForcedTarget(turret);
        }

        // Protected in vanilla's Building_Turret, which CE's turret uses as its forced target.
        private static readonly FieldInfo forcedTargetField = AccessTools.Field(typeof(Building_Turret), "forcedTarget");
        private static readonly AccessTools.FieldRef<Building_Turret, LocalTargetInfo> forcedTargetRef =
            forcedTargetField != null && forcedTargetField.FieldType == typeof(LocalTargetInfo) && !forcedTargetField.IsStatic
                ? AccessTools.FieldRefAccess<Building_Turret, LocalTargetInfo>(forcedTargetField)
                : null;

        /// <summary>Vanilla's protected forcedTarget field, or null when it is not found.</summary>
        internal static AccessTools.FieldRef<Building_Turret, LocalTargetInfo> ForcedTargetRef => forcedTargetRef;

        /// <summary>A forced target survives minifying, and an unmanned E never drops it by itself (DESIGN 3.7), so a
        /// mounted, unmounted or despawned turret starts without one, and without a burst in progress. Clears what
        /// CE's ResetForcedTarget clears on an unspawned turret, field by field: overrides of it (SOS2 ship turrets)
        /// read map state that an unspawned turret lacks.</summary>
        public static void ResetForcedTarget(Thing thing)
        {
            if (!(thing is Building_TurretGunCE turret))
            {
                return;
            }
            try
            {
                if (forcedTargetRef == null)
                {
                    MountingUtility.WarnOnce("forcedTarget", "Building_Turret.forcedTarget not found; turrets keep their forced target when mounted, unmounted or moved.");
                    return;
                }
                forcedTargetRef(turret) = LocalTargetInfo.Invalid;
                turret.globalTargetInfo = GlobalTargetInfo.Invalid;
                turret.targetingWorldMap = false;
                turret.currentTargetInt = LocalTargetInfo.Invalid;
                turret.burstWarmupTicksLeft = 0;
                // A burst in progress would otherwise go on at the old target after the turret is spawned again (its
                // verbs only tick while it is spawned). Verb.Reset drops the burst callback, which CE set (MakeGun).
                List<Verb> verbs = GunOf(turret)?.TryGetComp<CompEquippable>()?.AllVerbs;
                if (verbs != null)
                {
                    foreach (Verb verb in verbs)
                    {
                        verb.Reset();
                        verb.castCompleteCallback = turret.BurstComplete;
                    }
                }
            }
            catch (Exception e)
            {
                MountingUtility.WarnOnce("ResetForcedTarget", "Could not clear the forced target of " + turret.ToStringSafe() + ": " + e.Message);
            }
        }

        public static Thing GunOf(Thing turret)
        {
            // Gun only rebuilds a missing gun while spawned, and conversion only runs unspawned.
            return (turret as Building_TurretGunCE)?.Gun;
        }

        // ---- Graphic reset ----

        private static readonly Dictionary<Type, FieldInfo[]> graphicFieldsByType = new Dictionary<Type, FieldInfo[]>();

        /// <summary>Sets every instance field of type Graphic (or a subclass) on the thing's class hierarchy to null.</summary>
        public static void ResetGraphics(Thing thing)
        {
            if (thing == null)
            {
                return;
            }
            FieldInfo[] fields = GraphicFieldsOf(thing.GetType());
            for (int i = 0; i < fields.Length; i++)
            {
                try
                {
                    fields[i].SetValue(thing, null);
                }
                catch (Exception e)
                {
                    MountingUtility.WarnOnce("graphic:" + fields[i].DeclaringType + "." + fields[i].Name,
                        $"Could not reset graphic field {fields[i].DeclaringType}.{fields[i].Name}: {e.Message}");
                }
            }
        }

        private static FieldInfo[] GraphicFieldsOf(Type type)
        {
            if (graphicFieldsByType.TryGetValue(type, out FieldInfo[] cached))
            {
                return cached;
            }
            var list = new List<FieldInfo>();
            for (Type t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!f.IsInitOnly && typeof(Graphic).IsAssignableFrom(f.FieldType))
                    {
                        list.Add(f);
                    }
                }
            }
            cached = list.ToArray();
            graphicFieldsByType[type] = cached;
            return cached;
        }

        // ---- Stat caches ----

        private class CacheField
        {
            public FieldInfo field;
            public Type keyType;
            /// <summary>IDictionary&lt;K,V&gt;.Remove(K), used when the cache has no non-generic IDictionary.</summary>
            public MethodInfo genericRemove;
        }

        private static readonly FieldInfo statWorkerField = AccessTools.Field(typeof(StatDef), "workerInt");
        private static readonly Dictionary<Type, CacheField[]> cacheFieldsByWorkerType = new Dictionary<Type, CacheField[]>();

        /// <summary>Removes the thing from every stat worker's per-Thing caches: instance fields that are
        /// dictionaries keyed by Thing, plus StatWorker.ClearCacheForThing.</summary>
        public static void ClearStatCaches(Thing thing)
        {
            if (thing == null)
            {
                return;
            }
            if (statWorkerField == null)
            {
                MountingUtility.WarnOnce("workerInt", "StatDef.workerInt not found; stat caches are cleared through StatDef.Worker.");
            }
            List<StatDef> stats = DefDatabase<StatDef>.AllDefsListForReading;
            for (int i = 0; i < stats.Count; i++)
            {
                StatDef stat = stats[i];
                StatWorker worker;
                try
                {
                    // A worker that was never created holds no cache, so it is not created here.
                    worker = statWorkerField != null ? statWorkerField.GetValue(stat) as StatWorker : stat.Worker;
                }
                catch (Exception e)
                {
                    MountingUtility.WarnOnce("worker:" + stat.defName, $"Could not get the worker of stat {stat.defName}: {e.Message}");
                    continue;
                }
                if (worker == null)
                {
                    continue;
                }
                try
                {
                    CacheField[] fields = CacheFieldsOf(worker.GetType());
                    for (int f = 0; f < fields.Length; f++)
                    {
                        RemoveFrom(fields[f], worker, thing);
                    }
                    worker.ClearCacheForThing(thing);
                }
                catch (Exception e)
                {
                    MountingUtility.WarnOnce("cache:" + worker.GetType().FullName,
                        $"Could not clear the stat cache of {worker.GetType().FullName}: {e.Message}");
                }
            }
        }

        private static void RemoveFrom(CacheField cache, StatWorker worker, Thing thing)
        {
            if (!cache.keyType.IsInstanceOfType(thing))
            {
                return;
            }
            object dict = cache.field.GetValue(worker);
            if (dict == null)
            {
                return;
            }
            lock (dict)
            {
                if (dict is IDictionary plain)
                {
                    plain.Remove(thing);
                }
                else if (cache.genericRemove != null)
                {
                    cache.genericRemove.Invoke(dict, new object[] { thing });
                }
            }
        }

        private static CacheField[] CacheFieldsOf(Type workerType)
        {
            if (cacheFieldsByWorkerType.TryGetValue(workerType, out CacheField[] cached))
            {
                return cached;
            }
            var list = new List<CacheField>();
            for (Type t = workerType; t != null && t != typeof(object); t = t.BaseType)
            {
                foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    Type dictInterface = ThingKeyedDictionaryInterface(f.FieldType);
                    if (dictInterface == null)
                    {
                        continue;
                    }
                    Type keyType = dictInterface.GetGenericArguments()[0];
                    list.Add(new CacheField
                    {
                        field = f,
                        keyType = keyType,
                        genericRemove = dictInterface.GetMethod("Remove", new[] { keyType }),
                    });
                }
            }
            cached = list.ToArray();
            cacheFieldsByWorkerType[workerType] = cached;
            return cached;
        }

        /// <summary>The IDictionary&lt;K,V&gt; interface of a type whose key K is Thing or a subclass, or null.</summary>
        private static Type ThingKeyedDictionaryInterface(Type type)
        {
            if (type == null || type.IsPrimitive || type.IsEnum)
            {
                return null;
            }
            if (IsThingKeyedDictionary(type))
            {
                return type;
            }
            foreach (Type i in type.GetInterfaces())
            {
                if (IsThingKeyedDictionary(i))
                {
                    return i;
                }
            }
            return null;
        }

        private static bool IsThingKeyedDictionary(Type type)
        {
            if (!type.IsInterface || !type.IsGenericType || type.GetGenericTypeDefinition() != typeof(IDictionary<,>))
            {
                return false;
            }
            return typeof(Thing).IsAssignableFrom(type.GetGenericArguments()[0]);
        }
    }
}
