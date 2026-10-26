# CE Elevated Gun Platform

[![License: CC BY-NC-SA 4.0](https://img.shields.io/badge/License-CC%20BY--NC--SA%204.0-lightgrey.svg)](https://creativecommons.org/licenses/by-nc-sa/4.0/)

A Combat Extended add-on for RimWorld 1.6 and 1.7. You build a **turret platform**, a solid tower, and put a CE turret on it. The pair becomes an **elevated turret**, called **E** in this file and in DESIGN.md. E fires from the platform's height plus its own height, so it shoots over walls near it. There are two platforms: a **low** one (floor 1 by default) and a **high** one (floor 3 by default) that costs double and adds more hit points. Heights are set in floors, each as tall as a wall, from floor 1 to floor 10. All other combat effects come from the real heights: walls near the target still give cover, a roof hides whoever is under it, and enemies can see and shoot E just as well as E can see and shoot them.

The design spec is in `DESIGN.md`. The in-game test checklist and the list of assumptions still to check are in `TESTING.md`.

## How to use

1. Research **Gun turrets**. Then open **Architect > Security > turret platform** and build one. You can make it from any wall material (metal, wood or stone). Each footprint size used by a mountable turret gets its own platforms: 1×1, 2×2, 3×3 and so on, each as a **high** and a **low** platform with buttons side by side, the high one first. Clicking one opens the usual material menu. With the **Material Sub-Menu** mod active, each size gets one button instead, which builds the high platform and has the low one in its dropdown; without that mod, a vanilla dropdown would hide the material choice.

   | | Low platform | High platform |
   |---|---|---|
   | Height (setting) | floor 1 (2.0) | floor 3 (6.0) |
   | Turret hit points added (setting) | +20% × material factor | +40% × material factor |
   | Cost per cell (setting, low) | 5 material, plus 1 component per platform | double: 10 material, plus 2 components |
   | Work to build | 135 per cell | 270 per cell |

   Everything else is the same: the platform's own hit points, airtight, roof support, ammo capacity, refunds, and half hit points left when E is destroyed.
2. **Rotate the platform** before you place it. The marked cell in front of it is the **loading spot**. Colonists stand there to man and reload the turret, because the platform itself can't be walked on. Keep that cell free and reachable.
3. Wait until the platform is **finished**. Then build a turret of exactly the same size on it, or install or reinstall one from a crate. The turret blueprint must cover the platform exactly. If it doesn't, placement is rejected with a message that says what's wrong:
   - wrong size or position: it names the platform size the turret needs ("This turret takes up 2×2 cells...");
   - the platform is still a blueprint or frame: finish the platform first;
   - the turret's loading spot (where colonists stand to man or reload it) would be on a platform: turn or move the turret so that spot is on open ground.
4. When the turret is completed or installed, it absorbs the platform and becomes "elevated *turret name*". It takes the platform's rotation and material colour, and its height and hit point bonus come from the kind of platform it stands on. The inspect panel then shows the platform, the firing height and floor, and a warning if no colonist can reach the loading spot. Packing it up gives back the same kind of platform.
5. **Ordering an attack.** Every elevated turret of yours has CE's **Attack** button, manned or not, so you can make it fire at a chosen enemy or building. Guns that can aim at the ground, such as CE's flak and AGS-30, can also be ordered onto an empty spot. A manned one still fires only while a colonist mans it. CE's range, minimum range and firing arc limits still apply. An elevated turret forgets its forced target when it is packed up, mounted or moved to another map.
6. **Firing zone.** Each elevated turret of yours has a button, **Show firing zone against human-sized target** (off by default, saved with the turret). While it is on and the turret is selected, a blue outline inside CE's range ring shows where the turret can hit a standing human (CE height 1.0, or 1.75 m). A cell is outlined when E's line to the head of a human standing there passes the same check E fires by, with the same cell walk, and only within the range of the loaded ammo. Walls, closed doors and other solid buildings hide the ground right behind them, roofed ground is left out, and rock overhead that reaches above the muzzle blocks it. Sandbags and other low cover count as CE counts them (they rarely block a line to a head), and a human standing in a sandbag cell stands higher, as in CE. Other pawns, plants, smoke and light aren't counted. The outline is worked out over several frames within a small time budget, so it appears a moment after you select the turret (longer for long-range turrets, or with many selected), and then follows changes about once a second, also while paused.

Mountable turrets: any CE turret (`CombatExtended.Building_TurretGunCE` or a subclass) that the player can build or install, except indirect-fire guns. Mortars and the rocketswarm launcher can't be mounted, and neither can the vanilla foam turret, which is not a CE turret. CE turrets from other mods are picked up automatically.

## Rules of engagement

Heights are in CE units. Walls and unroofed rock are 2.0 tall.

- **Firing height.** E's top and muzzle sit at the base turret's CE height plus the height of the platform it stands on. A platform's height is its floor × 2.0, with floors counted G (ground), 1, 2, ...: the low platform is floor 1 (a wall's top, **2.0**) and the high one floor 3 (**6.0**) by default; each can be set from floor 1 (2.0) to floor 10 (20.0). The base height is 2.0 for a full-height turret and its fill percent otherwise. A CE mini-turret or autocannon gives 0.85 + 2.0 = **2.85** on a low platform and 0.85 + 6.0 = **6.85** on a high one; CE's 3×3 flak gives **4.0** and **8.0**. E occupies the whole column from 0 up to its top. An empty platform is as tall as its height.
- **Walls.** The firing line is a real 3D line from E's muzzle to the top of the target. A wall blocks only where the line is below 2.0 in that wall's cell. So walls next to E don't block (the line is still high there), but walls near the target do (the line has come down to the target's height).
- **Aiming.** CE aims at a pawn's centre mass. When only the upper part of a target shows over a wall partway along the line, E raises its aim to the lowest point that clears every wall and roof slab on the way, the way CE aims at the visible part of a target behind sandbags. The target is still partly covered, so it is harder to hit, but E doesn't pour rounds into the wall.
- **Roofs.** A roofed cell anywhere on the line blocks it when the line is below the top of the roof slab in that cell. This includes E's own cell and the target's cell. Slab tops are 2.1 for a built roof, 4.0 for thin rock roof and 8.0 for overhead mountain. Only rock roofs are thicker: every other roof is a built roof's slab, including thick built roofs from other mods such as Raise The Roof's constructed steel roof, which CE makes 4.0 like thin rock, and Anomaly's void metal ceiling (setting, on by default). This holds wherever CE checks a round or a line against a roof, for every shooter, so E's lines and everyone's rounds meet the same slab. What else a roof does, such as stopping drop pods and mortar shells, comes from its definition and doesn't change. As a result:
  - A target standing under a roof (a porch, a room) is hidden from E.
  - A built roof over E, or over cells near E, doesn't stop E: its muzzle is above 2.1.
  - Under rock roof, E fires only where its line stays above the slab top in every roofed cell. On a low platform (2.85) E is inside thin rock roof (4.0) and can't fire from under it. On a high platform (6.85 or more) it is above thin rock, so it can fire out from under a thin rock edge, but still at or below overhead mountain (8.0). From floor 4 up, E even reaches over overhead mountain.
- **Mountains.** Unroofed rock is a 2.0 wall, so E fires over low rock outcrops. Rock under a mountain roof blocks the line.
- **Seeing and being seen.** Sight uses the same 3D line, from the seer's muzzle to the target's top. Gas and smoke, roofs, and buildings or cover block sight. Pawns never block sight, and leaning is ignored. Enemies spot E over walls the same way E spots them. Someone standing under a roof can't see E either.
- **Bodies and friendly fire.** Cover and non-hostile pawns still stop the line where it passes through their CE bounds. Hostile pawns never do, as in CE. A manned E, or a pawn shooting at E, holds fire for a friendly only when the line actually passes through that friendly's body. Plain CE holds fire for any friendly on the line who isn't adjacent to the shooter.
- **Interception (CIWS).** This mod's 3D rules don't judge a line or sight to a target in the air (a drop pod, shuttle or other skyfaller, or a round in flight) when that thing is the target being checked. CE's CIWS verbs don't call CE's base line check themselves. A line check to a cell with no target thing still gets the 3D line from E's muzzle to the ground there (for example the VGE CE patch's check toward the intercept point of an asteroid or debris piece, which fly at ground height). A mod whose CIWS verb asks CE's base line check gets CE's answer, from E's muzzle, so cover below the muzzle never blocks it and CE has no roof rule; sight keeps vanilla's answer. As in CE, a turret on Hold fire doesn't intercept: its own CIWS buttons only switch interception off, not on. An automatic (crewless) elevated CIWS with an attack order still intercepts: each time it picks its next target, it engages the target CE's own CIWS search picks first if that is inside its firing arc, waits while that search finds nothing but its own interception rounds are still flying, and otherwise fires at the order. An attack on the order already under way (warmup, burst, cooldown) is finished first, so pods falling in meanwhile can land untargeted; with CE's arcs on, a first pick outside the arc means the order goes on. On the ground it couldn't take the order at all.
- **Projectiles.** Every round flies with real CE ballistics, with no exemptions for walls. Rounds fired by E always check roofs, even when E stands under a roof (plain CE ignores roofs for rounds launched from 2.0 or higher in a roofed cell). They never hit a pawn standing in a roofed cell, because the roof shields that pawn.
- **Suppression.** E's rounds suppress pawns along their path like CE rounds do. They keep doing so above height 3, where plain CE stops. They don't suppress a pawn in a roofed cell when they pass that pawn at 2.0 or higher, the bottom of the roof.
- **Rounds aimed at E** follow plain CE rules, and so do mortars, which are never elevated.
- **Target choice.** When E picks targets it ignores vanilla's flat cover penalty, because its own line check already accounts for cover by height.
- **Firing arcs.** With CE's "Enable arc of fire" setting on, a turret on a platform can turn its arc to any direction, whatever way the platform faces; the arc's width limits stay (the GAU-19's arc is still 10-140° wide). When it is mounted, its arc keeps pointing the way it did relative to the blueprint you placed; when it is packed up, it goes back to its normal limits.
- Range, smoke and lighting work as in CE. No rule depends on who owns the turret.

## What the platform adds

- **Hit points.** Max HP is the turret's own max HP × (1 + bonus × the platform material's hit point factor), with a bonus of 20% on a low platform and 40% on a high one by default, so wood adds less and plasteel more. When the turret is mounted it gains part of that bonus, in proportion to the platform's HP at the time.
- **Ammo.** The magazine holds 2× as much. An autoloader in the platform, as the info card calls it.
- **Body.** The turret becomes impassable, full height and wall-like: it blocks wind and light, holds up the roof, covers the floor, and is airtight if its material is.
- **Flammability.** The higher of the turret's own and a wall made of the platform material.
- **Beauty.** A flat -10, whatever the turret's own beauty, the material or the quality. A bare platform is -10 too.
- **Market value.** The turret's value with its own material plus the platform's value. Quality and damage still apply.
- **Loading spot.** The turret uses its own interaction spot if that spot lies outside its footprint. Otherwise it uses the platform's spot; CE's 3×3 flak is one such turret.
- CE autoloaders next to E work as before, because E keeps the base turret's building tags.

## Uninstall, deconstruct, destruction

| What happens to E | Turret | Platform | Ammo |
|---|---|---|---|
| **Uninstall / reinstall** | Becomes the normal turret again, with its own material. HP loses exactly the bonus it gained at mount. | Comes back on the same cells with the HP it had when mounted. | Magazine is cut to the normal capacity. The excess is dropped next to the platform as ammo items (not forbidden). |
| **Deconstruct** | Refunds the normal turret's costs with its own material. | Comes back with the HP it had when mounted. | CE drops the whole magazine. |
| **Destroyed** | Gone. It is not queued for rebuilding. | Comes back at half its max HP. | Lost, as in CE. |
| Any other removal (for example a dev-mode destroy) | Gone. | Not restored. | Lost. |

Deconstructing a bare platform refunds its material like a wall. Components are never refunded. If you reinstall E somewhere without a platform, it is a normal turret there and the old platform stays where it was. The roof above is never dropped while a turret and its platform swap places.

**Removing the mod from a save.** Elevated turrets and platforms are defs from this mod. Pack up or deconstruct every elevated turret first, then deconstruct the platforms.

## Settings

Options > Mod settings > CE Elevated Gun Platform.

| Setting | Default | Range | Applies |
|---|---|---|---|
| High platform height | floor 3 (6.0) | floor 1-10 (2.0-20.0) | Live |
| Low platform height | floor 1 (2.0) | floor 1-10 (2.0-20.0) | Live |
| Hit points added by a high platform | 40% | 0-100% | Live for max HP. HP already gained isn't rescaled. |
| Hit points added by a low platform | 20% | 0-100% | Live for max HP. HP already gained isn't rescaled. |
| Ammo capacity on a platform | ×2 | ×1-×4 (steps of 0.25) | Live |
| Non-rock roofs are as thin as a built roof for rounds and lines of fire | on | | Live, for every round and line check. Off: CE's thickness, where a thick built roof (such as Raise The Roof's steel roof) is 2.0-4.0 like thin rock, so at the default floors a turret on a low platform can't fire from under it or across it. |
| Material per low platform cell (high: double) | 5 | 1-100 | After a restart (defs are generated at load) |
| Components per low platform (high: double; never refunded) | 1 | 0-10 | After a restart |
| Debug overlay | off | | Only in dev mode. It flashes the cells E checks when it looks for or fires at a target: 0 = blocked, 0.7 = clear, 0.4 = path, with the line's height in each cell. It also logs, for every CIWS turret on the ground or elevated, what it sees, targets and misses when drop pods or shuttles come in. |

## Compatibility

- **RimWorld 1.6 and 1.7.** One net48 DLL serves both, built the same way as Combat Extended. RimWorld 1.7 runs it the way it runs CE's own net48 DLL. Everything outside the combat code, plus every vanilla call the combat code makes, is also compile-checked against the 1.7 reference assemblies.
- **Combat Extended** is required, and this mod loads after it. **Harmony** is required. This mod does not ship its own Harmony DLL.
- **Replace Stuff.** Frames on platforms use the same technique as Replace Stuff. With Replace Stuff loaded, no other building can replace an elevated turret in place, because the wipe would bring its platform back under the new building. Replace Stuff sees platforms as wall-like buildings.
- **D-FunctionalAmmunition Library** replaces CE's CIWS verbs with its own at startup. Its source isn't public, so its interception isn't checked against this mod; the debug overlay setting logs what each CIWS turret sees and targets (see Settings).
- **Raise The Roof** and other mods with thick built roofs: those roofs are as thin as built roofs wherever rounds and lines meet them (setting above). Drop pods and mortar shells still treat them as thick, because those go by the roof's definition.
- Mods that override CE methods without calling the base version are unaffected by the matching rule: for example SOS2 ship verbs, and CIWS projectiles for the roof-pawn and high-suppression rules.
- CE turret classes that override `CanSetForcedTarget` without calling the base version, such as SOS2 ship turrets, keep their own force-attack rule when elevated.
- When E is the shooter or the target, this mod replaces CE's line-of-fire routine (`Verb_LaunchProjectileCE.CanHitCellFromCellIgnoringRange`). Other patches to that routine don't take effect on those lines.
- Every patch target that isn't public is looked up at startup. If one is missing, that rule is switched off with a single `[ElevatedGunPlatform]` warning in the log, and the rest keeps working.

## Known limitations

- Height does not add range.
- AI pawns choosing cover don't account for E's height. A flat cover rating may tell them they are safe from E.
- The suppression roof rule only covers rounds fired by E. Rounds fired at E, or other high rounds, use CE's plain suppression.
- The art is simple and generated by `Source/Art/make_textures.py`: material-tinted plates with a front face, thin for low platforms and tall for high ones, and a drawn button icon. Hand-drawn PNGs with the same names and sizes can replace it. Footprints other than 1×1 to 4×4 squares use the stretched 1×1 picture.
- A destroyed E is not auto-rebuilt. Only the platform comes back.
- You can't queue a turret on a platform that is still a blueprint or frame.
- Instant (ray-cast) rounds fired by E don't hit roofs and can hit pawns under roofs. CE's ray-cast never checks roofs. E's line check still keeps it from aiming at targets under roofs.
- Fragments from E's shells follow plain CE rules.
- Some CE behaviour stays as it is:
  - Explosions use 2D distance, and a blast at E's upper part counts as above the roofs.
  - CE caps the aim height at 2.0 when the target half of the line has a roofed cell.
  - Mid-burst retargeting uses vanilla's flat line of sight.
  - CE's hit-chance readout can be off for elevated targets behind cover.
  - The info card's "Cover height" for platforms and elevated turrets shows a wall's height; combat uses the real heights.
- When a target is just low enough to clear an elevated turret's own roof, its rounds clear it only narrowly; spread and sway still put some into the roof.
- Elevated turret defs appear in storage filter lists, but they never exist as items.
- The firing zone is for a standing human. Crouching pawns, children and animals can be hidden where the zone shows a hit, and taller targets can be hit beyond it. It ignores other pawns, plants, smoke and light.
- Saves made before the low platform keep their platforms and elevated turrets as high ones; their elevated turrets' max HP rises to the high bonus, and current HP isn't rescaled.
- Reinstalling E in place with a new rotation mounts it with the platform's rotation.

## Later improvements (not in v1)

1. Height adds range.
2. AI cover-seeking that is aware of E.
3. The suppression roof rule for rounds fired at E, or for any round above a roof.
4. Real art.
5. Auto-rebuild. When E is destroyed the platform comes back, but the turret is not queued for rebuilding.
6. Queuing a turret on a platform that is still a blueprint or frame.

## Building from source

Requirements:
- A .NET SDK that can build net48. On Windows this needs the .NET Framework 4.8 targeting pack.
- For the 1.7 check, an SDK that can build net10.0.
- The NuGet packages restore by themselves: Krafs.Rimworld.Ref 1.6.4871 and 1.7.4984-unstable, and Lib.Harmony 2.4.3, which is compile-only.
- CombatExtended.dll, referenced from `$(CEDir)\Assemblies`. Set `CEDir` in `Source/ElevatedGunPlatform.csproj` to your Combat Extended folder, or pass `-p:CEDir=...` on the command line.

```
dotnet build Source/ElevatedGunPlatform.csproj -c Release            (the shipped DLL, Assemblies/)
dotnet build Source/ElevatedGunPlatform.csproj -c Release -p:RW=1.7  (1.7 API check only)
```

The first command writes `Assemblies/ElevatedGunPlatform.dll`, which is the only file to ship. The second compiles everything except `Source/Combat/` (plus `Source/ApiCheck17/`, which repeats the combat code's vanilla calls) as net10.0 against the 1.7 reference assemblies, and writes its output under `Source/obj/` only. Run the two builds one after the other, not in parallel.

## License

CC BY-NC-SA 4.0, the same license as Combat Extended, whose line-of-fire code `Source/Combat/LineOfFire.cs` adapts. See `LICENSE`.
