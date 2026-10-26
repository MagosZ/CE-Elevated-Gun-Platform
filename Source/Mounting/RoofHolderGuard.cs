using System;
using Verse;

namespace ElevatedGunPlatform
{
    /// <summary>Clears a def's holdsRoof while one of its things is removed and immediately replaced (platform by
    /// elevated turret or the reverse), so the momentary gap cannot start a roof collapse (DESIGN 2.5).
    /// Tokens restore the previous value, so nested guards on the same def unwind correctly.</summary>
    public static class RoofHolderGuard
    {
        public struct Token : IDisposable
        {
            private ThingDef def;
            private bool previous;
            private bool active;

            internal Token(ThingDef def)
            {
                this.def = def;
                previous = def.holdsRoof;
                active = true;
                def.holdsRoof = false;
            }

            public bool Active => active;

            /// <summary>Restores holdsRoof. Safe to call on a default or already ended token.</summary>
            public void End()
            {
                if (!active)
                {
                    return;
                }
                active = false;
                if (def != null)
                {
                    def.holdsRoof = previous;
                }
            }

            public void Dispose()
            {
                End();
            }
        }

        /// <summary>Starts a guard on the def; a null def gives an inactive token.</summary>
        public static Token Begin(ThingDef def)
        {
            return def == null ? default(Token) : new Token(def);
        }
    }
}
