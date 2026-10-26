using UnityEngine;
using Verse;

namespace ElevatedGunPlatform
{
    /// <summary>Textures for buttons, loaded on the main thread at startup.</summary>
    [StaticConstructorOnStartup]
    public static class EGPTextures
    {
        public static readonly Texture2D ShowFiringZone = ContentFinder<Texture2D>.Get("EGP/UI/ShowFiringZone");
    }
}
