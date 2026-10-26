using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ElevatedGunPlatform
{
    /// <summary>Display side of the record comp: inspect lines, loading-spot reach warning, platform drawing, the
    /// firing zone button.</summary>
    public partial class CompElevatedPlatform
    {
        private const int DisplayReachCheckInterval = 250;
        private const string DisplayPlatformTexPath = "EGP/Platform/High_1x1";

        // Transient reach cache (not saved). Checked lazily, so it does not depend on comps being ticked.
        private bool displayReachChecked;
        private int displayReachTick;
        private bool displayReachOk = true;

        public override string CompInspectStringExtra()
        {
            string baseText = base.CompInspectStringExtra();
            if (!EGP.IsElevated(parent.def))
            {
                return baseText;
            }

            var sb = new StringBuilder();
            if (!baseText.NullOrEmpty())
            {
                sb.AppendLine(baseText);
            }
            if (ElevatedStatInfo.TryGet(StatRequest.For(parent), out ElevatedStatInfo info))
            {
                sb.AppendLine("EGP_InspectPlatform".Translate(info.PlatformLabel));
                float height = EGP.TopOf(parent);
                int floor = EGPSettings.CurrentFloor(EGP.TierOf(EGP.PlatformOf(parent)));
                sb.AppendLine("EGP_InspectHeight".Translate(height.ToString("0.00"), floor));
            }
            if (parent.Spawned && parent.Faction != null && parent.Faction.IsPlayer && !LoadingSpotReachable())
            {
                sb.AppendLine("EGP_InspectUnreachable".Translate().Resolve().Colorize(ColorLibrary.RedReadable));
            }
            return sb.ToString().TrimEndNewlines();
        }

        /// <summary>Whether a free colonist can stand on the interaction cell (CE's reload job walks onto it).
        /// Cached for 250 ticks.</summary>
        private bool LoadingSpotReachable()
        {
            int now = Find.TickManager.TicksGame;
            if (displayReachChecked && now >= displayReachTick && now - displayReachTick < DisplayReachCheckInterval)
            {
                return displayReachOk;
            }
            displayReachChecked = true;
            displayReachTick = now;
            displayReachOk = ComputeLoadingSpotReachable();
            return displayReachOk;
        }

        private bool ComputeLoadingSpotReachable()
        {
            Map map = parent.Map;
            IntVec3 cell = parent.InteractionCell;
            if (map == null || !cell.InBounds(map))
            {
                return false;
            }
            bool anyColonist = false;
            foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
            {
                if (pawn.Downed)
                {
                    continue;
                }
                anyColonist = true;
                if (pawn.CanReach(cell, PathEndMode.OnCell, Danger.Deadly))
                {
                    return true;
                }
            }
            // Nobody on the map to judge by: no warning.
            return !anyColonist;
        }

        public override void PostPrintOnto(SectionLayer layer)
        {
            base.PostPrintOnto(layer);
            if (!EGP.IsElevated(parent.def))
            {
                return;
            }
            // The picture of the platform it stands on (low or high, this size), unrotated like the platform itself.
            string texPath = EGP.PlatformOf(parent)?.graphicData?.texPath ?? DisplayPlatformTexPath;
            Material mat = MaterialPool.MatFrom(texPath, ShaderDatabase.Cutout, parent.DrawColor);
            IntVec2 size = parent.RotatedSize;
            Vector3 center = parent.TrueCenter();
            // Slightly below the turret base, which prints at the Building altitude.
            center.y = Altitudes.AltitudeFor(AltitudeLayer.Building) - Altitudes.AltInc;
            Printer_Plane.PrintPlane(layer, center, new Vector2(size.x, size.z), mat);
        }

        /// <summary>DESIGN 3.8: the toggle that shows the firing zone while this turret is selected (player's only).</summary>
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }
            if (!IsElevated || parent.Faction != Faction.OfPlayer)
            {
                yield break;
            }
            yield return new Command_Toggle
            {
                defaultLabel = "EGP_ShowFiringZone".Translate(),
                defaultDesc = "EGP_ShowFiringZone_Desc".Translate(),
                icon = EGPTextures.ShowFiringZone,
                isActive = () => showFiringZone,
                toggleAction = () => showFiringZone = !showFiringZone,
            };
        }
    }
}
