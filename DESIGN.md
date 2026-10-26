# CE Elevated Gun Platform: design

Combat Extended add-on for RimWorld 1.6 and 1.7. A **turret platform** is a wall-like building. Building or
installing a CE turret on a platform with exactly the same footprint turns the pair into an **elevated turret** (E).
E fires from the platform's height plus its own height, so it fires over nearby walls. There are two platforms per
footprint size: low (floor 1 by default) and high (floor 3 by default; double cost and work, more turret HP); a floor
is a wall's height. Everything else follows from real heights.

Terms: **E** = elevated turret; **base turret** = the normal CE turret def it came from; **platform** = the platform
building; **record** = what E remembers about its platform (kept on E itself, saved with it).

Evidence for vanilla and CE behaviour comes from open-source code only: CE source (`Mods/_CE_Source_Reference`),
Replace Stuff and Harmony source (`Mods/_Mod_Source_Reference`), vanilla XML, and compile checks against the
Krafs reference assemblies for 1.6.4871 and 1.7.4984. Where vanilla internals are unknown, the code is written to be
correct either way, and the assumption is listed in TESTING.md.

## 0. Build and layout
- One net48 DLL in `Assemblies/` for both 1.6 and 1.7, built like Combat Extended itself: against
  Krafs.Rimworld.Ref 1.6.4871. RimWorld 1.7 runs on .NET 10 with Unity redirected to its own stubs and runs CE's net48
  DLL that way (the user's game log shows CE initialising on 1.7.4985), so it runs this one the same way. A net10.0
  build is not possible: CE's API uses UnityEngine.CoreModule types (Bounds, Vector3, Ray), which the 1.7 reference
  assemblies replace with UnityStubs, so the two type sets can't meet in one compilation.
- 1.7 API check: `-p:RW=1.7` compiles everything except `Source/Combat/` as net10.0 against Krafs.Rimworld.Ref
  1.7.4984-unstable (output stays in obj/), plus `Source/ApiCheck17/`, which repeats every vanilla call made in
  `Combat/` (never shipped).
- Lib.Harmony 2.4.3, compile-only (`ExcludeAssets=runtime`); only 2.x API (runs on Harmony 2.4.3 in 1.6 and Harmony 3
  in 1.7). Never ship 0Harmony.dll. Patch parameters use exact or base types (Harmony 3 validates bindings). Never name
  a patch method InnerPrefix/InnerPostfix/InnerFinalizer.
- CombatExtended.dll referenced by HintPath, `Private=false`.
- Non-public vanilla/CE members are reached through `AccessTools` strings, resolved once, null-checked, and their
  patch class has `Prepare()` returning false (with one warning) when the target is missing.

## 1. Defs

### 1.1 Eligible turrets
A ThingDef is eligible when all hold:
- `thingClass` is `CombatExtended.Building_TurretGunCE` or a subclass;
- `building.turretGunDef != null`;
- `designationCategory != null` or `minifiedDef != null` (the player can build or install it);
- not indirect fire: no verb of the gun has a verbClass assignable to `CombatExtended.Verb_ShootMortarCE`, and no verb's
  `defaultProjectile.projectile.flyOverhead` is true, and `building.IsMortar` is false.
(Vanilla mortar and the CE rocketswarm launcher use Verb_ShootMortarCE and are excluded. The foam turret is not a CE
turret class and is excluded.)
At startup the log lists every turret def (thingClass `Building_Turret` or a subclass): the mountable ones, and the
others grouped by reason (not a CE turret, no turret gun, can't be built or installed, indirect fire, no platform for
its size, elevated def name taken, generation failed).

### 1.2 Platform defs
- XML template `EGP_TurretPlatform` (the 1x1 high platform) in `Defs/ThingDefs/TurretPlatform.xml`, wall-like: Impassable, fillPercent
  1, holdsRoof, blockWind, blockLight, coversFloor, castEdgeShadows, staticSunShadowHeight 1, stuff
  Metallic/Woody/Stony, `MaxHitPoints` 300, `WorkToBuild` 135, `Flammability` 1, `building.isInert`,
  `building.isStuffableAirtight`, `building.ai_chillDestination` false, no minifiedDef, **rotatable**,
  `hasInteractionCell` with `interactionCellOffset (0,0,-1)`, Security tab, research GunTurrets, terrain Heavy.
- At load, two platform defs (tiers) per distinct footprint size among eligible turrets: high
  `EGP_TurretPlatform_{x}x{z}Cells` and low `EGP_TurretPlatformLow_{x}x{z}Cells` (vanilla rejects ThingDef names ending
  in a digit, which their blueprint and frame names would inherit). The 1x1 high one is the template itself, adjusted;
  saves made before the low platform know these high names, so their platforms and elevated turrets become high.
  Per def: `size`; `costStuffCount = stuffPerCell x area`, costList `ComponentIndustrial = components`, and
  `WorkToBuild` = template value x area, all doubled for the high tier; `MaxHitPoints` = template value x area for
  both; `interactionCellOffset = (0, 0, -((z - 1) / 2) - 1)` (integer division; the front middle cell just outside the
  footprint); label "high/low turret platform (x×z)". Market value comes from vanilla's own calculation.
- Architect: `uiOrder` from 2000 by size, the high tier first. In vanilla a dropdown and a building made from
  materials share one menu, so a dropdown of platforms would lose the material choice (Replace Stuff's
  `DesignatorBuildDropdownStuffFix` documents this and unpacks its own groups). So the tiers get separate buttons,
  side by side. Only when the Material Sub-Menu mod (`cedaro.material.submenu`) is active, which brings the material
  choice back inside dropdowns, one `DesignatorDropdownGroupDef` per size is generated
  (`EGP_TurretPlatformGroup_{x}x{z}Cells`, "turret platform (x×z)") and both tiers point to it: the button shows the
  high platform and the low one is in its dropdown.
- Art: `graphicData.texPath = EGP/Platform/{High|Low}_{x}x{z}` when that PNG exists in the mod's Textures folder, else
  the 1x1 of the tier (stretched); `drawSize` = size; `drawRotated = false`, so the front face is drawn toward the
  bottom of the screen in every rotation, like a wall's (2.9).
- Generated in a **prefix on `DefGenerator.GenerateImpliedDefs_PreResolve`**, so vanilla then generates their
  blueprint and frame defs. Harmony is applied in the `Mod` constructor so this patch exists before defs load.

### 1.3 Record comp on every eligible turret
`CompProperties_ElevatedPlatform` is appended to every eligible base turret def (before cloning), so base and elevated
defs have identical comp lists and **no comp is ever added or removed at runtime**. On a base turret the comp is inert
(no record).

### 1.4 Elevated defs (hidden)
For each eligible base turret B, `EGP_Elevated_{B.defName}` (plus `_Elevated` if B's name ends in a digit): a clone
of B (same thingClass, comps, gun, size,
graphics, buildingTags) with:
- `designationCategory = null`; no blueprint or frame of its own;
- `minifiedDef = null` during generation (no install blueprint generated), then in the postfix
  `minifiedDef = B.minifiedDef`, `installBlueprintDef = B.installBlueprintDef` (reinstall places B's blueprint);
- wall-like: `passability Impassable`, `fillPercent 1`, `blockWind`, `blockLight`, `holdsRoof`, `coversFloor`,
  `rotatable = true`; `building` cloned with `isStuffableAirtight = true`;
- interaction cell: B's own (`hasInteractionCell`/offset) if B has one and it lies outside B's unrotated footprint,
  else the platform default for its size (CE's 3x3 flak has its spot inside its footprint);
- made from stuff: `stuffCategories` = the Wall's, `costStuffCount` = platform's (display only, see 2.6);
- `recipeMaker = null`, `thingSetMakerTags = null`, `tradeability = None`: an elevated turret never exists as an
  item (it converts back when minified). It keeps the base's thingCategories (vanilla expects minifiable defs to
  have one), so it appears in storage filters;
- label "elevated {B.label}"; description gets a platform note;
- per-def caches reset; `index`/`shortHash` like a fresh def; `generated = true`; `modContentPack` = this mod.
Registry: base <-> elevated maps, platform defs by size, fast `IsElevated(def)`, `IsPlatform(def)`.

### 1.5 Frames of eligible turrets
In the postfix: each eligible turret's `frameDef` gets `building.isEdifice = false` and
`altitudeLayer = BuildingOnTop`, so a frame can stand on a platform (same technique Replace Stuff uses for all frames).

## 2. Mounting and unmounting

### 2.1 Placement
- `GenConstruct.CanPlaceBlueprintOver(newDef, oldDef)` postfix: true when `BuiltDefOf(newDef)` is an eligible turret
  and `oldDef` is a completed platform (not its blueprint or frame: the platform must be finished first). False when `oldDef` is an eligible-turret frame and
  `newDef` is an edifice (frames are no longer edifices but must still block).
- `GenConstruct.CanPlaceBlueprintAt` and `CanPlaceBlueprintAt_NewTemp` postfixes (params `entDef, center, rot, map`):
  if the footprint of an eligible turret touches any platform, it must match one platform exactly (same unrotated
  size, same occupied rect); otherwise reject with "must match the platform". For a turret whose own interaction
  spot lies inside its footprint, a vanilla rejection over an exactly matching platform is re-checked with
  `ignoreInteractionSpots` (its elevated version uses the platform's spot, checked when the platform was placed).
- `GenConstruct.CanReplace` postfix (last): nothing replaces an elevated turret in place (Replace Stuff lets
  wall-like buildings replace each other; the wipe would put the platform back under the new building).
- `GenConstruct.BlocksConstruction(constructible, t)` postfix: a platform never blocks a matching eligible turret's
  blueprint, frame or install blueprint.
- `GenSpawn.SpawningWipes(newEntDef, oldEntDef)` postfix: an eligible turret's frame never wipes a platform. As a
  safeguard, a platform or E spawning never wipes a building that is not an edifice (power conduits, modded pipes),
  nor a blueprint or frame of one. Vanilla most likely already keeps those under any edifice that doesn't transmit
  power, like a wall (TESTING C12), so this only matters if a mod makes the wipe true. It is not a known fix for the
  in-game report of a conduit gone after E was deconstructed: that cause is still open (TESTING 1.3, conduits).
- `GenSpawn.WipeExistingThings` prefix/finalizer: while cells are cleared for an eligible turret that will mount on an
  exactly matching platform (vanilla clears an install blueprint's cells before spawning the installed thing), that
  platform is kept (SpawningWipes returns false for it), so the mount in 2.2 still finds it. No effect if vanilla
  never clears there.
- `CanPlaceBlueprintAt`/`_NewTemp` prefix: a reinstall blueprint of an elevated turret is checked as its base turret,
  which is what will actually be installed.

### 2.2 Mount (in place)
`GenSpawn.Spawn(Thing, IntVec3, Map, Rot4, WipeMode, bool, bool)` prefix (`ref Rot4 rot`): when not respawning after
load, `newThing.def` is an eligible base turret, it is not spawned, and a platform exactly matches the footprint:
1. record = platform def, stuff, current HP, rotation; plus the turret's own stuff (`baseStuff`);
2. remove the platform (`Destroy(Vanish)`, inside the roof-holder guard of 2.5);
3. convert `newThing` **in place** to the elevated def (2.4) with the platform's stuff;
4. HP: `elevHP = baseHP + bonus x (platHP / platMax)` where `bonus = elevMax - baseMax`;
5. `rot = platform rotation`; spawning continues with the same Thing object.
Covers build completion (frame), install and reinstall, dev spawn.

### 2.3 Unmount
- **Uninstall / reinstall** (`MinifyUtility.MakeMinified(thing, destroyMode)` prefix + postfix): when `thing` is E and
  spawned, remember map, cell, rotation, record and set the roof-holder guard; after minifying, convert the inner thing
  in place back to the base def with `baseStuff` (2.4), respawn the platform from the record with its recorded HP, trim
  the magazine to the base capacity and drop the excess as ammo next to the platform (CE convention, see 2.7), and
  release the guard.
  Turret HP after: `clamp(elevHP - bonusHitPoints, 1, baseMax)`, where `bonusHitPoints` (in the record) is the HP the
  turret actually gained at mount, so a changed setting can't take off HP it never received.
- **Deconstruct**: CE drops the whole magazine itself. `CompElevatedPlatform.PostDestroy(Deconstruct)` respawns the
  platform with its recorded HP.
- **Destroyed** (`KillFinalize`): respawn the platform at half its max HP. Ammo is lost as in CE.
- Other destroy modes: no platform.
- `Building.Destroy` prefix/postfix applies the roof-holder guard to E for Deconstruct and KillFinalize.
- Platform respawn: `ThingMaker.MakeThing(platformDef, stuff)`, faction = E's faction, HP, then `GenSpawn.Spawn(...,
  rot, WipeMode.VanishOrMoveAside)` with E's live rotation (captured before conversion). With E's position that gives
  exactly E's footprint even if E was rotated after mounting.
- Repair list (assumption, TESTING D10): in game, a platform spawned at full HP and then set to half HP by a plain
  `HitPoints` write was repaired only after it was hit again. Vanilla's `listerBuildingsRepairable` is assumed to check
  a building's HP when it spawns and when it takes damage, so a write after spawning is missed. HP is therefore set
  before `GenSpawn.Spawn`. As a fallback in case there is no spawn check, a damaged respawned platform and an E
  mounted below its new max HP (GenSpawn.Spawn postfix, for the turret the prefix mounted) also get
  `Notify_BuildingTookDamage` once spawned (`MountingUtility.NotifyRepairable`). A raised HP bonus leaves undamaged E
  below their new max, so when the mod settings are saved in a game, every E's MaxHitPoints cache is cleared and it
  gets the same notice.

### 2.4 In-place conversion (the "blacklist copy")
The Thing object is kept, so every reference to it stays valid: blueprints, designations, other mods, the gun, ammo,
comps, CE's turret state. Only def-derived state changes, and only while the thing is **not spawned**:
- `thing.def` = target def; `SetStuffDirect(platform stuff)` (mount) or `SetStuffDirect(baseStuff)` (unmount);
- the record is set (mount) or cleared (unmount) in the existing `CompElevatedPlatform`;
- reset every instance field of type `Graphic` (or subclass) on the thing's class hierarchy to null;
- clear the thing's per-stat caches: for each StatDef, its worker's instance fields that are dictionaries keyed by
  Thing (`IDictionary.Remove(thing)`), plus `StatWorker.ClearCacheForThing(thing)`; the same for the gun;
- set HP as in 2.2/2.3.
Blacklist (what changes): def, stuff, record, HP, graphic and stat caches. Everything else is untouched.

### 2.5 Roof-holder guard
While removing a platform or an E that is immediately replaced by the other in the same call, temporarily set that
def's `holdsRoof = false` (restored in `finally`), so a momentary gap cannot trigger roof collapse. Used in mount,
`MakeMinified` and `Building.Destroy` (Deconstruct/KillFinalize).

### 2.6 Costs, refunds, value
- `GenLeaving.DoLeavingsFor` (every overload whose first parameter is a Thing) prefix/finalizer tracks the thing whose
  leavings are being made. During that, `CostListAdjusted` postfixes (Thing and def overloads) return:
  for E, the base turret's cost list with `baseStuff` (platform stuff and components excluded: the platform comes back);
  for a platform, its cost list without the components (stuff refunds like a wall).
- Outside leavings, `CostListAdjusted(Thing)` for E returns base costs (with baseStuff) + platform costs.
- Market value (StatPart): E = base turret value (with baseStuff) + platform def value with the platform stuff, both
  from vanilla's own calculation.

### 2.7 Stats (StatParts added to StatDefs at startup, absolute overrides for E)
- `MaxHitPoints`: base turret max HP (with baseStuff) x (1 + bonus x platform stuff's MaxHitPoints factor), where the
  bonus is the setting of the platform's tier (low 0.20, high 0.40 by default). The tier comes from the record's
  platform def (the high tier for a def-only request).
- `Flammability`: max(base turret flammability (baseStuff), Wall flammability with the platform stuff).
- `MarketValue` (and `MarketValueIgnoreHp`): 2.6. Inserted first, so vanilla quality and health parts still apply.
- `Beauty` and `BeautyOutdoors`: a flat -10 for E and for platforms, whatever the base turret's beauty, the material
  or the quality. Added last, so it replaces the final value.
- CE `MagazineCapacity` on the gun: x ammoMultiplier when the gun's `CompAmmoUser.turret` is E (also for an E's own
  info card). Excess on unmount: trim `CurMagCount` to the base capacity; drop whole ammo items
  (`excess / CurAmmoCount`) next to the turret, partial rounds via `partialUnloadAmmoDef` if any (else kept), not
  forbidden for the player.

### 2.8 Reach, reload, autoloaders
- E is impassable. CE's reload job always walks onto the turret's interaction cell, so E always has one (1.4). Its
  inspect panel warns, checked every 250 ticks, when no free colonist can reach the interaction cell.
- CE autoloaders filter only by `buildingTags` (CE never compares `allowedTurrets`), which E inherits; adjacency and
  the `Building_TurretGunCE` class are unchanged. Nothing else is needed.

### 2.9 Drawing
Platform art (`Textures/EGP/Platform/{High|Low}_{x}x{z}.png`, 128 px per cell, 1x1 to 4x4) is drawn by
`Source/Art/make_textures.py`: grey, so the material colour tints it; a top face with one bolted plate per cell, a thin
rim, and a front (south) face whose height shows the tier (thin for low, tall with ribs for high). E draws its normal
turret graphics (stuff-tinted); the comp's `PostPrintOnto` prints the picture of its own platform (from the record,
unrotated, stuff colour) at its footprint, slightly below the building altitude. The firing zone button's icon is
`Textures/EGP/UI/ShowFiringZone.png` from the same script, loaded by `EGPTextures` at startup.

## 3. Combat (heights)

### 3.1 Heights (H1, H2, H3)
`CollisionVertical.CalculateHeightRange(Thing thing, out FloatRange heightRange, out float shotHeight)` (private
static) postfix: for E, range = (0, top), shotHeight = top, where top = base height + the height of E's platform
(`EGP.TopOf`, from the record's platform def) and base height = 2.0 if the base def is full fillage, else its
fillPercent. A platform's height = its tier's floor x 2.0 (floors counted G, 1, 2, ...; defaults low floor 1 = 2.0,
high floor 3 = 6.0; settings floors 1-10 = 2.0-20.0). CE autocannon: 0.85 + 2.0 = 2.85 low, 0.85 + 6.0 = 6.85 high; CE
flak: 4.0 low, 8.0 high. An empty platform spans (0, its height) the same way. A submerged E or platform (a compat
module made CE return height 0) stays at 0. Everything else keeps CE heights. A built roof slab is 2.0-2.1 (below E's
muzzle); thin rock roof 2.0-4.0, overhead mountain 2.0-8.0: rock over the gun blocks it unless the muzzle is above the
slab top (high platform by default: above thin rock, at or below overhead mountain). Unroofed rock is 2.0 like a wall.
Roof slabs (`RoofSlabs`, a `CE_Utility.GetBoundsFor(IntVec3, RoofDef)` prefix, global): with the plainRoofs setting
(default on) only rock roofs (RoofRockThin, RoofRockThick) keep CE's slab, and every other roof, natural or not, gets
the built roof's 2.0-2.1. CE gives a thick built roof (isThickRoof and not isNatural: Raise The Roof's steel,
transparent and solar roofs, the reinforced dock roof) 2.0-4.0 like thin rock, which at the default low floor put
every low E's muzzle inside it (flak at its top) and blocked every line across it; Anomaly's void metal ceiling
(natural and thick) is 2.0-8.0. Off: CE's slab. CE reads the slab only where a direct-fire round meets a roof
(TryCollideWithRoof, also SOS2Compat's ShipProjectileCE override) and in SOS2Compat's ship verb line check; this mod's
3D rules read it through `ElevatedLine.RoofTop`. Fly-overhead rounds never test roofs in flight (ProjectileCE
CheckForCollisionBetween returns early) and land by the def's isThickRoof; drop pods, infestations and collapse go by
the def's flags too, so none of them change.

### 3.2 Line of fire (L1-L6): `Verb_LaunchProjectileCE.CanHitCellFromCellIgnoringRange` prefix
Only when the verb's caster or the target is E (CE makes the turret the caster of its gun's verbs even when manned;
a pawn caster is always firing its own weapon and keeps CE's check). Runs CE's own routine with:
- the cell walk of CE (`GenSightCE.PointsOnLineOfSight`), cells normalised to y = 0;
- per cell, the 3D line's min height inside that cell (from shot source to the target top);
- CE's shortcut skips a cell only when its highest cover is below that min height (not the shooter's height);
- roofs: any roofed cell on the line (source and target cells included) blocks when the line's min height there is
  below the roof slab top (`ElevatedLine.RoofTop`: `CE_Utility.GetBoundsFor(cell, roof).max.y`, slabs as in 3.1). The cells of an elevated shooter's or
  target's own footprint follow one rule instead (`ElevatedLine.OwnRoofBlocks`), and the walk skips their roofs: a
  roof blocks where the segment really crosses the cell (longer than 1e-4 of the segment), and where that thing's end
  of the line (the shooter's muzzle, the target's top) lies in the cell's closed square at or below the slab top, so a
  round can't start inside rock. A cell only touched at a corner above its slab doesn't count. CE's walk misses the first crossed cell when the line starts
  on a cell boundary, which an even-sized turret's centre is. The blocking cell is flashed in the debug overlay;
- walls/cover/pawns: CE's 3D bounds-vs-ray test; E's own body never blocks its own line;
- friendly fire (pawn shooters, manned E): hold fire only if the ray actually crosses that friendly's bounds;
- range, arcs, smoke, lighting and everything else unchanged. No owner-based rules.
- targets in the air (`ElevatedLine.Airborne`: a Skyfaller or a ProjectileCE passed as targetThing, the targets of
  CE's CIWS) keep CE's own check. CE's CIWS verbs override TryFindCEShootLineFromTo and don't call this method. A call
  with no target thing (a cell) is not exempt: the VGE CE patch's postfix on CE's projectile CIWS line checks the cell
  of a VGE asteroid or debris piece this way, and gets the 3D line to that cell at ground height, where those fly. A third-party CIWS verb that falls back to it gets
  CE's check from E's muzzle (above 2.0): CE skips every cell whose highest cover is below the muzzle and has no roof
  rule, so it is more permissive than this mod's 3D line to the skyfaller (aimed at its DrawPos at CE height about 0,
  which a roofed cell near E would block).
- aim (`Verb_LaunchProjectileCE.GetTargetHeight` postfix, E shooters, not fly-overhead): CE aims at centre mass;
  raise the aim to the lowest height that keeps CE's actual firing line (its source and aim point, read from the verb)
  above every full-height obstacle and roof slab between shooter and target (for an obstacle of top T left at distance
  s on a line of length D: h > m - (m - T) x D / s), plus a small margin, never above the target's top and never below
  CE's own aim. Without it, a target whose top shows over a wall partway along the line would pass the check while
  every centre-mass round hits the wall. The roofs of E's own crossed cells (and an elevated target's) count as such
  obstacles too. The margin is set at the target, so near E's own roof the raised line may clear it only narrowly:
  most rounds pass, but CE's spread, sway and recoil still put some into the roof, as with walls.

### 3.3 Spotting (S1-S3): `AttackTargetFinder.CanSee(seer, target, validator)` postfix
When seer or target is E: result = the same 3D test from the seer's muzzle (`CollisionVertical(seer).shotHeight`) to
the target's top, over intermediate cells: validator (gas) still applies, roofs as in 3.2, buildings/cover block by
bounds; pawns don't block sight; leaning ignored; own roofed cells as in 3.2. Symmetric by construction. Targets in
the air keep vanilla's result, as in 3.2.

### 3.4 Projectiles (P1-P5) and suppression
- P1: real CE flight; no wall exemptions.
- P2: CE's own roof hit. `ProjectileCE.Launch` (8-parameter overload) postfix clears `ignoreRoof` on rounds fired by E.
  `CheckCellForCollision` prefix for rounds fired by E: if the cell is roofed, run `TryCollideWithRoof(cell)` first
  (CE's early-out otherwise skips roofs for rounds above the cover height); if it hits, the cell check ends there. Roof slabs as in 3.1.
- P3: `CanCollideWith` postfix: rounds fired by E never collide with a pawn standing in a roofed cell.
- P4: rounds aimed at E are plain CE. P5: mortars unchanged (never E).
- "Fired by E" is decided once at launch: launcher is E, or the pawn manning E (CE's operator: the turret's
  CompMannable.ManningPawn with a ManTurret job on it), recorded per round (re-derived on load). Fragments of E's shells
  (equipment == the projectile) are plain CE rounds. Instant (ray-cast) rounds are not covered by P2/P3, because CE's
  ray-cast never checks roofs or calls CanCollideWith; their suppression uses the beam's own height.
- Suppression, rounds fired by E only: `RayCastSuppression` prefix replaces CE's loop with an identical one that also
  skips a pawn in a roofed cell when the round's height at its closest point to that pawn is at or above the roof
  bottom (2.0). `CheckForCollisionBetween` postfix runs the same suppression when CE skipped it because the round was
  above height 3 (CE's height gate), and only if the round is still spawned.

### 3.5 Targeting (C2)
While an E (or a pawn manning E) is scoring targets (vanilla `AttackTargetFinder.GetShootingTargetScore` and CE's
private `NonSnapAttackTargetFinder.GetShootingTargetScore`), `CoverUtility.CalculateOverallBlockChance` returns 0
(prefix sets a flag, finalizer clears it). E's own line check already handles cover by height.

### 3.6 Debug overlay
Mod setting, dev mode only: flash the cells of E's line and sight checks with CE's colour convention (0 = blocked,
0.7 = clear, 0.4 = path) and the line height as text. The same setting turns on two log traces: power conduits that
are destroyed (ConduitTrace), and CIWS (CiwsTrace, a `Building_TurretGunCE.Tick` postfix acting on every
`Building_CIWS_CE`, on the ground or elevated). For each incoming skyfaller in ThingRequestGroup.ActiveTransporter
(leaving ones, reversed, skipped) it logs once when the pod is first on the map (whatever its range or sight; the line gives the distance), with the
turret's state (hold fire,
Active, crew, reloading, warmup, cooldown, verb state, rounds, forced and current target) and its target-searcher
verbs (type, Available, own hold fire); once when it targets it; and once when it lands untargeted, with the state
again. It also logs once per question and asker type when E's line of fire (asked by a verb) or sight (asked for a
seer) is asked about a target in the air (3.2, 3.3); the caller is in the message's stack trace. The once-per logs
reset when a game is loaded.

### 3.7 Force attack
CE lets the player force a target only on mannable turrets: `Building_TurretGunCE.CanSetForcedTarget =>
mannableComp != null && PlayerControlled` (protected virtual getter). The same property shows the "Attack" gizmo and,
when false, makes Tick drop the forced target. A postfix returns true for every player-controlled E, manned or not.
Firing is unchanged: CE's Tick runs the gun only when `mannableComp == null || MannedNow`, so a manned E still needs
its operator. CE's OrderAttack keeps its range, min range and fire arc checks. A ground cell can be chosen only if
the gun's targetParams allow locations (CE: flak, AGS-30); other guns force-attack things. Subclasses that override
the property without calling base (SOS2 ship turrets) keep their own rule.
Since CE never drops an unmanned E's forced target now, it is cleared field by field (vanilla's protected
`forcedTarget`, CE's `globalTargetInfo`, `targetingWorldMap`, `currentTargetInt`, `burstWarmupTicksLeft`) when E
despawns (`Building_TurretGunCE.DeSpawn` postfix: minify, gravship, destroy) and in every in-place conversion (2.4).
Not through the virtual ResetForcedTarget, which subclasses (SOS2) override with code that needs a spawned turret.
The gun's verbs are reset as well (a burst in progress would go on at the old target once E is spawned again, since
verbs only tick while it is spawned), and CE's burst callback (`BurstComplete`, which Verb.Reset clears) is put back.
Interception first: CE's TryStartShootSomething takes a forced target before any search, so an order would stop an
unmanned E's CIWS, which on the ground could never hold one. A prefix on TryStartShootSomething, for an unmanned E that
is a `Building_CIWS_CE` with a valid forced target:
1. if its current target is a spawned thing in the air (3.2), ResetCurrentTarget (CE's CIWS search skips any target a
   CIWS turret already has, its own included; the MultiVerbs override also clears `activeVerb`);
2. the search CE would run (Building_Turret_MultiVerbs.TryFindNewTarget: the first Available ITargetSearcher verb that
   finds a target); if it finds one inside CompFireArc (CE's CIWS search ignores arcs, and Tick drops the forced target
   when the current one is outside), the forced target (vanilla's field) is set aside for the call, so CE's own search
   picks it; a finalizer puts the order back unless a new one was given;
3. if it finds none but a ProjectileCE_CIWS launched by this turret is still flying at a spawned target (CE's search
   skips that target too), the call is skipped: the turret waits with its order kept, as a ground CIWS searches again
   next tick;
4. otherwise the order goes on; `activeVerb` is cleared if it is an interception verb, since CE picks the verb only in
   TryFindNewTarget, which a forced target skips, and the interception verb can't fire at the order.
A `Building_TurretGunCE.Tick` prefix covers the rest of the arc case: CE's Tick drops the forced target whenever the
current target is outside the arc, so for the same turrets an airborne current target outside the arc (drifting, or
landed) is let go first (ResetCurrentTarget). The choice is made only when CE calls TryStartShootSomething, so an
attack on the order already under way (warmup, burst, cooldown) is finished first.
Manned CIWS turrets keep CE's rule, as on the ground.

### 3.8 Firing zone (human-sized target)
A `Command_Toggle` on the player's E ("Show firing zone against human-sized target", `CompGetGizmosExtra`;
`showFiringZone` on the comp, saved, off by default; selecting several E and clicking toggles each). While it is on
and E is selected, `CompElevatedPlatform.PostDrawExtraSelectionOverlays` (reached through CE's range ring drawing,
which calls ThingWithComps.DrawExtraSelectionOverlays) outlines with `GenDraw.DrawFieldEdges` the cells where E can
hit a standing human, on top of CE's range rings. Switched off, the zone and its computation are released.
- Per candidate cell, the 3.2 test for a human standing there, minus other pawns, with the same code paths: the line
  from the muzzle (CE's shot source: `DrawPos`, `CollisionVertical.shotHeight` = m) to the human's top at the cell
  centre: 1.0 (CE's `BoundsInjector.ForPawn` for a humanlike) plus the height of the low cover the cell holds (CE
  raises a pawn standing in a cover cell by it). The walk is CE's own `GenSightCE.PointsOnLineOfSight` to the cell's
  ground centre, as LineOfFire walks (so even its float drift on large coordinates is the same); heights come from
  `ElevatedLine.HeightRangeInCell` on the line to the human's top; plus the own-cell roof check of 3.2. A walked cell
  blocks when the line's min height in it is below its roof slab top (every cell but E's own), or, except the source
  and target cells, when the segment crosses it at or below the CE height of its cover (`GetCover`, plants skipped as
  CE skips them: walls and rock 2.0, closed doors 2.0, open doors 0, platforms and other E at this mod's heights,
  sandbags and other low cover at their fill; low cover next to E never blocks, as CE's adjacent-cover rule passes a
  shot that starts above it). The tie (exactly at the top) is assumed to block, as CE's ray-vs-bounds test is assumed
  to count a grazing ray. E's own body never blocks.
- Candidate cells: within range (the smaller of the order range `verbProps.range` of the primary verb, as CE's range
  ring and OrderAttack, and its `EffectiveRange`, which follows gun stats and the loaded ammo) and outside min range of
  E's position, walkable (trees, chunks and sandbags included) and not a doorway, not roofed, not fogged, not E's own
  footprint. Drawn only while E is the player's (a turret handed to another faction drops its zone). The fire arc (the arc being edited, while
  editing) filters the finished result, so changing it costs no line walks.
- Cost: CE's walk visits about 1.5 r points per line (some cells more than once), about 2.3 million for range 78 on
  open ground, roughly 0.2 s of CPU on .NET 10 and likely more on Mono. So a cheap, conservative test runs first: a
  grid walk over the crossed cells checks the line's height where it leaves each one, minus a margin of two cells'
  drop (2m/D + 0.01), against the highest obstacle in that cell and its eight neighbours (E's own cells left out).
  CE's walk only ever yields cells next to the crossed ones, so a line that passes this test is clear for CE's walk
  too; only the others (lines near shadow edges, roofs and walls near the target) take CE's walk. Own-cell roofs are
  checked exactly first.
- Scheduling: the computation (fill the grids, spread the obstacle tops to neighbours, test the lines) runs spread
  over frames. All selected E share 2.5 ms per frame (Stopwatch), split equally by last frame's number of
  computations; the clock is read after every CE walk and every 16 cheap cells, and a computation that overran its
  share pays the excess back by skipping later shares. Turrets that show no outline yet go first: while any of those
  ran last frame, refreshes wait. A new frame is detected by `RealTime.frameCount` or `RealTime.LastRealTime`
  changing. A computation is dropped once its turret hasn't been drawn for more than 2 frames and 0.5 s (deselected;
  both, so one long frame doesn't lose the work); at each new frame such computations of other turrets are dropped
  too, so their buffers go back to the pool. A finished outline is recomputed after one second of real time (also
  while paused), and as soon as possible when position, rotation, muzzle height or range change; until then the
  previous outline at the same position stays drawn, and a result for another position is never drawn. After an
  error the turret's outline is skipped for 5 s (one warning per session).
- The fire arc filter re-runs on a new result, at once when CE's arc is switched on or off (its private
  `functionEnabled`, read by reflection; if missing, the arc counts as on), and at most every 0.1 s while the arc is
  dragged. With the arc off, the result is copied without calling WithinFireArc.
- Memory: the outline and the computation state are kept on E's record comp (transient) and dropped when E despawns
  (the DeSpawn postfix of 3.7); the key holds a map id, not the Map. Grid buffers are taken when a computation first
  gets time, and pooled (at most 4 kept).

### 3.9 Firing arcs
CE's firing arcs (`CompFireArc`, active with CE's "Enable arc of fire" setting) are measured from the turret's facing,
and a turret's arc centre may only swing `maxSpanDeviation` either side of it (Local's CE Guns GAU-19: 70; CE's flak:
135). On a platform the facing is the platform's rotation, chosen for the loading spot, so in game the arc could not be
turned toward the threat: dragging it out of the cone only squeezed it against CE's limit line (CE also discards its own
clamp of the new centre). So:
- Each elevated def gets its own copy of the turret's `CompProperties_FireArc` with `maxSpanDeviation` 360: the arc can
  point anywhere; its width limits (`spanRange`) stay. Conversion switches the arc comp to the new def's properties
  (comps keep the properties they were made with), both ways.
- The arc centre is relative to the facing, so when the facing changes it is shifted to point the same way on the map:
  at mount from the turret blueprint's rotation to the platform's, at unmount from E's rotation to the base turret's
  (North if it isn't rotatable), then clamped to the base turret's swing.
- CE computes the part of an arc that crosses the line straight behind the facing (`SupplementarySpan`) only while
  editing, in an unsaved field. For E (whose arc can now cross it anywhere) a getter postfix computes it every time,
  with CE's own formula. When E becomes the base turret again, the cached field is recomputed for the base limits
  (none for a swing below 180), or the ground turret would keep firing in E's wide arc behind its cone.
- On the ground the swing also caps the width at twice the swing, so E's copy keeps spanRange.max at
  min(spanRange.max, 2 x swing); a set width is clamped to the new limits at every conversion.
- Packing E up stores its arc's direction on the map on the comp (`arcMapAngle`, saved); mounting it on a platform
  again restores that direction (unclipped), instead of rebasing the base turret's clipped arc. Spawning on the
  ground clears it.

## 4. Settings (Mod settings page)
highFloor 3 and lowFloor 1 (floors 1-10, 2.0 each), hpBonusHigh 0.40 and hpBonusLow 0.20, ammoMultiplier 2,
stuffPerCell 5 and components 1 (a low platform's cost; a high one costs double), plainRoofs on (3.1), debugOverlay off. Cost settings apply
after a restart (defs are generated at load); the others apply live. The firing zone is switched per turret (3.8).

## 5. Later improvements (not in v1)
1. Height adds range. 2. AI cover-seeking aware of E (C3). 3. Suppression roof rule for rounds fired AT E (or any
round above a roof). 4. Real art. 5. Auto-rebuild: when E is destroyed the platform comes back but the turret is not
queued for rebuilding. 6. Queue a turret on a platform that is still a blueprint or frame.

## 6. Build
```
dotnet build Source/ElevatedGunPlatform.csproj -c Release            (the shipped DLL, Assemblies/)
dotnet build Source/ElevatedGunPlatform.csproj -c Release -p:RW=1.7  (1.7 API check only)
```
