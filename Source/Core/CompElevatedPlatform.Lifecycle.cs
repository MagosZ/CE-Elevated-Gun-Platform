using System;
using Verse;

namespace ElevatedGunPlatform
{
    /// <summary>Lifecycle side of the record comp: the platform comes back when an elevated turret is deconstructed
    /// (with its recorded HP) or destroyed (at half its max HP). DESIGN 2.3.</summary>
    public partial class CompElevatedPlatform
    {
        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            if (mode != DestroyMode.Deconstruct && mode != DestroyMode.KillFinalize)
            {
                return;
            }
            if (record == null || previousMap == null || !EGP.IsElevated(parent.def))
            {
                return;
            }
            try
            {
                int hitPoints = mode == DestroyMode.Deconstruct ? record.platformHitPoints : -1;
                MountingUtility.RespawnPlatform(record, parent.Position, parent.Rotation, previousMap, parent.Faction, hitPoints);
            }
            catch (Exception e)
            {
                Log.Error($"[ElevatedGunPlatform] Could not put back the platform of {parent.ToStringSafe()}: {e}");
            }
        }
    }
}
