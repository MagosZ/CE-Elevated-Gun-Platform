using CombatExtended;
using RimWorld;

namespace ElevatedGunPlatform
{
    /// <summary>Selection overlay side of the record comp: the firing zone (DESIGN 3.8), drawn while the player has
    /// switched it on with the comp's button. CE's turret draws its range rings through
    /// ThingWithComps.DrawExtraSelectionOverlays, which calls this for every comp.</summary>
    public partial class CompElevatedPlatform
    {
        // Transient (not saved): this turret's zone and its computation.
        private FiringZone.Cache firingZone;

        public override void PostDrawExtraSelectionOverlays()
        {
            base.PostDrawExtraSelectionOverlays();
            // The player's only, like its button: a turret handed to another faction stops drawing it.
            if (showFiringZone && IsElevated && parent.Spawned && parent.Faction == Faction.OfPlayer
                && parent is Building_TurretGunCE turret)
            {
                FiringZone.Draw(turret, ref firingZone);
            }
            else if (firingZone != null)
            {
                ClearFiringZone();
            }
        }

        /// <summary>The zone is switched off or for a place the turret left (it despawned).</summary>
        internal void ClearFiringZone()
        {
            FiringZone.Release(firingZone);
            firingZone = null;
        }
    }
}
