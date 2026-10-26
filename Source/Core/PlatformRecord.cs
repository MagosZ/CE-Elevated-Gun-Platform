using Verse;

namespace ElevatedGunPlatform
{
    /// <summary>What an elevated turret remembers about the platform it stands on. Saved with the turret.</summary>
    public class PlatformRecord : IExposable
    {
        public ThingDef platformDef;
        /// <summary>Material of the platform (also the elevated turret's Stuff).</summary>
        public ThingDef platformStuff;
        /// <summary>The turret's own Stuff before it was elevated (null for turrets not made from stuff).</summary>
        public ThingDef baseStuff;
        /// <summary>Platform hit points when the turret was mounted.</summary>
        public int platformHitPoints;
        public Rot4 platformRotation = Rot4.North;
        /// <summary>Hit points the turret gained when mounted, taken off again when unmounted (-1: not recorded).</summary>
        public int bonusHitPoints = -1;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref platformDef, "platformDef");
            Scribe_Defs.Look(ref platformStuff, "platformStuff");
            Scribe_Defs.Look(ref baseStuff, "baseStuff");
            Scribe_Values.Look(ref platformHitPoints, "platformHitPoints");
            Scribe_Values.Look(ref platformRotation, "platformRotation", Rot4.North);
            Scribe_Values.Look(ref bonusHitPoints, "bonusHitPoints", -1);
        }
    }
}
