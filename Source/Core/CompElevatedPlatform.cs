using Verse;

namespace ElevatedGunPlatform
{
    /// <summary>Added to every eligible base turret def and copied to its elevated def, so a turret never gains or
    /// loses a comp when it is mounted or unmounted. Inert (no record) while the turret is not elevated.</summary>
    public class CompProperties_ElevatedPlatform : CompProperties
    {
        public CompProperties_ElevatedPlatform()
        {
            compClass = typeof(CompElevatedPlatform);
        }
    }

    /// <summary>Holds the platform record. Lifecycle (destroy) and display (inspect, reach check, drawing) live in
    /// the other partial files.</summary>
    public partial class CompElevatedPlatform : ThingComp
    {
        public PlatformRecord record;
        /// <summary>The player switched on this turret's firing zone (DESIGN 3.8). Saved; off by default.</summary>
        public bool showFiringZone;
        /// <summary>DESIGN 3.9: the firing arc's direction on the map when this turret last left a platform, or NaN.
        /// Mounting again restores it; spawning on the ground clears it.</summary>
        public float arcMapAngle = float.NaN;

        public bool IsElevated => record != null && EGP.IsElevated(parent.def);

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Deep.Look(ref record, "egpPlatform");
            Scribe_Values.Look(ref showFiringZone, "egpShowFiringZone", false);
            Scribe_Values.Look(ref arcMapAngle, "egpArcMapAngle", float.NaN);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && record != null && record.platformDef == null)
            {
                // A platform def saved under a name that no longer exists (generated names changed once, to end in
                // "Cells"): the platform of the turret's size, so unmounting still gives a platform back.
                ThingDef baseDef = EGP.BaseOf(parent.def);
                record.platformDef = baseDef != null ? EGP.PlatformForSize(baseDef.size) : null;
                MountingUtility.WarnOnce("MissingPlatformDef", "An elevated turret's saved platform def no longer exists; "
                    + "it was replaced by the platform of the turret's size (" + record.platformDef.ToStringSafe() + ").");
            }
        }
    }
}
