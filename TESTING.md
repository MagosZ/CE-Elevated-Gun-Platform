# In-game test checklist

Each item is a setup and the result it should give. **E** is an elevated turret. The **base turret** is the normal CE turret it was made from. Section numbers such as "DESIGN 3.2" point into `DESIGN.md`.

## Setup and tools

- Load Harmony, Combat Extended, this mod and nothing else, unless a test names another mod. Run every phase on 1.6, and repeat at least 1.1, 2.1 and 3.1 on 1.7.
- Start a new colony. Saves from builds before the "Cells" platform names are not supported: their bare 2×2 and larger platforms are dropped on load (vanilla logs a missing def), and an elevated turret's record falls back to the platform of its size, with one `[ElevatedGunPlatform]` warning.
- Turn on Dev mode. You will need these debug actions: spawn thing, spawn pawn, kill, destroy (vanish), damage, and explosion. God mode makes building faster.
- **Overlays:**
  - This mod: Mod settings > CE Elevated Gun Platform > Debug overlay. It flashes cells with 0 (blocked), 0.7 (clear) or 0.4 (path), and shows the line's lowest height in each cell as text.
  - Roofs: the vanilla roof overlay.
  - CE: Mod settings > Combat Extended, with debug mode on. Turn on "Draw partial LoS checks" for CE's own line checks, and "Draw intercept checks" to see where rounds collide. A red "x" means a round hit a roof.
- **Log.** After startup the log should contain `[ElevatedGunPlatform] Generated turret platforms (high and low) for 3 sizes (1x1, 2x2, 3x3) and M elevated turret defs.`, followed by `Turrets that can be mounted on a platform (M): ...` and a list of the turrets that can't, grouped by reason. It should contain no `[ElevatedGunPlatform]` errors. Any `... not found` warning means one rule is switched off; note which.
- Reference heights at the default floors (low platform floor 1, high platform floor 3):
  - CE mini-turret or autocannon: E top 2.85 on a low platform, 6.85 on a high one. CE flak: 4.0 low, 8.0 high.
  - Empty platform: 2.0 low, 6.0 high.
  - Walls and unroofed rock: 2.0.
  - Roof slab tops: built roof 2.1, thin rock 4.0, overhead mountain 8.0. Thick built roofs from other mods (Raise The Roof's steel roof): 2.1 with "Non-rock roofs are as thin as a built roof for rounds and lines of fire" on, 4.0 with it off. Anomaly's void metal ceiling: 2.1 on, 8.0 off.
  - A standing human is shorter than a wall (2.0).

## Phase 1: the building

### 1.1 Load and platform defs
- [ ] **Clean start on 1.6, then on 1.7.** Expected: the generation log line and the eligibility list appear. There are no errors and no "Failed to apply patch class" lines. There is no config error "ends with a numerical digit" for any `EGP_` def; generated platforms are named `EGP_TurretPlatform_2x2Cells` (high), `EGP_TurretPlatformLow_2x2Cells` (low) and so on; the 1x1 high one is `EGP_TurretPlatform`, the 1x1 low one `EGP_TurretPlatformLow_1x1Cells`. No warning that Combat Extended lacks a Workshop URL.
- [ ] **Eligibility list.** Expected: CE's mini-turret, autocannon, M240B and flak are under "can be mounted"; the mortar and rocketswarm launcher under "indirect fire"; the vanilla foam turret under "not a Combat Extended turret".
- [ ] **Architect menu.** Open Architect > Security before and after researching Gun turrets. Expected: only after the research, "high turret platform (1×1)" and "low turret platform (1×1)" side by side, then the same for (2×2) and (3×3). Clicking either opens the material menu (metal, wood, stone), and the chosen material is built. The log says that the platforms get separate buttons. No "elevated ..." entry appears in any menu.
- [ ] **With Material Sub-Menu.** Activate the Material Sub-Menu mod and restart. Expected: one button per size, "turret platform (1×1)" and so on, showing the high platform, with the low one in its dropdown; both can be built in any wall material.
- [ ] **Costs and stats** (defaults 5 material per cell, 1 component). Expected:
  - Low: 1×1 costs 5, 2×2 costs 20, 3×3 costs 45, each plus 1 industrial component. High: double, 10, 40 and 90, each plus 2 components. Metal, wood and stone are all allowed.
  - Max HP is 300 × cells for both, scaled by the material. Work is 135 × cells for low, 270 × cells for high.
- [ ] **Art.** Expected: platforms show plates and a front face, thin on the low one and tall with ribs on the high one, tinted by the material; the face stays at the bottom whatever the rotation; blueprints and frames show the same picture. An elevated turret shows its own platform's picture under it.
  - On terrain without the Heavy affordance, placement is rejected with vanilla's terrain message.
- [ ] **Loading spot.** Place 1×1, 2×2 and 3×3 platforms and rotate each before placing. Expected:
  - The interaction spot is the middle cell just outside the front edge. For the 3×3 that cell is two cells from the centre.
  - The spot follows the rotation.
  - A platform can't be placed so its spot overlaps another interaction spot.
- [ ] **A bare platform.**
  - It is impassable.
  - It holds up a roof: remove the other supports and the roof stays.
  - It blocks light and wind.
  - A wooden one burns.
  - Deconstructing it refunds material like a wall. The component is never refunded.

### 1.2 Placement rules (DESIGN 2.1)
- [ ] A mini-turret blueprint placed exactly on a finished 1×1 platform is accepted.
- [ ] Each of the following is rejected with "This turret takes up N×M cells. To mount it, place it exactly over one finished N×M turret platform." (N×M = the turret's size):
  - a 2×2 autocannon blueprint over a 1×1 platform;
  - a 2×2 blueprint one cell off a 2×2 platform;
  - a 1×1 turret blueprint on one cell of a 2×2 platform.
- [ ] A turret blueprint on a platform that is still a blueprint or frame is rejected with "The turret platform here isn't finished yet...".
- [ ] A manned 1×1 turret (for example CE's M240B) placed on a 1×1 platform, turned so its loading spot faces a second platform, is rejected with "The spot where colonists stand to man or reload this would be on a turret platform...". Turned to face open ground, it is accepted. The same message appears for a manned turret placed next to a platform with its spot on it.
- [ ] A wall, sandbags or another non-turret building over a platform is rejected because the space is occupied.
- [ ] A mortar or rocketswarm launcher over a platform is rejected. These turrets are never mountable.
- [ ] **Frame on a platform.** Place a turret frame on a platform with no materials delivered yet. Expected: the platform stays and the frame draws on top. Nobody deconstructs the platform. A wall blueprint over that frame is rejected. When the frame is finished, the result is E.
- [ ] **Install from a crate.** A minified mini-turret installed on a platform becomes E. An install blueprint over a platform is accepted only on an exact match. The platform is absorbed, not deconstructed first: no platform material drops, and a roof that only the platform holds stays up.

### 1.3 Mount results (DESIGN 2.2, 2.7, 2.8)
- [ ] **Appearance.** Build a mini-turret on a steel platform. Expected: it is labelled "elevated mini-turret", the turret is drawn over a steel-tinted platform base, and it faces the platform's rotation.
- [ ] **Inspect panel.** Expected lines on a high platform: "Platform: steel high turret platform (1×1)" and "Firing height: 6.85 (platform on floor 3)"; on a low one: "steel low turret platform (1×1)" and "2.85 (platform on floor 1)".
- [ ] **Info card.**
  - Max HP explanation: "Elevated on ...: base x (1 + 40% x factor) = ..." on a high platform, 20% on a low one.
  - Magazine capacity: ×2, with the line "Turret platform autoloader: x2".
  - Market value: turret + platform.
  - Flammability: the higher of the turret's and a wall made of the platform material.
  - Beauty: -10, also on a gold or marble platform and for a turret of good quality; a bare platform's info card shows -10 too. A room with E in it counts -10 for it.
- [ ] **HP from a damaged platform.** Damage a platform to half HP, then mount. Expected: the turret gains half of the HP bonus. Its HP = base HP + bonus × (platform HP / platform max). Colonists then repair E up to its new max without it taking damage first.
- [ ] **Conduits under E and platforms.** Lay a power conduit under several E and under a bare platform, and connect a powered building through them. The reported loss (a conduit gone after deconstructing E) has no known cause yet, so try each way separately and, right after designating, check whether the conduit got its own deconstruct mark (cycle-select the cell):
  - (a) select E and use its own Deconstruct button, with god mode off, then on;
  - (b) Orders > Deconstruct: one click on E, then a drag over all of E's cells, with god mode off;
  - (c) uninstall an E; kill one with the dev kill action (not an explosion, which can destroy the conduit itself);
  - (d) deconstruct the bare platform; mount a turret on a platform with a conduit under it.
  Expected: every conduit stays and still carries power. If it is lost only in (b), vanilla's deconstruct tool marked it too; tell which case lost it.
  With dev mode and the mod's debug overlay on, the log names every destroyed conduit with a stack trace ("Debug: power conduit at ... destroyed"), and shows vanilla's wipe decision when a platform or E spawns over a conduit ("Debug: vanilla SpawningWipes(...) = ...").
- [ ] **Damaged platform back.** Damage a platform to half HP, mount a turret on it, then uninstall E (and, separately, deconstruct a second E mounted the same way). The platform must stand in the home area. Expected: the platform comes back at half HP (the HP it had when mounted; damage to E and repairs of E don't carry over to it), and colonists repair it without it taking damage first.
- [ ] **Impassable.** Pawns path around E.
- [ ] **Roof.** A roof supported only by E stays up.
- [ ] **Airtight (Odyssey).** Use an airtight material and make E part of a vacuum-sealed room's wall. Expected: the room stays sealed.
- [ ] **Reload and manning.** A colonist reloads E from the loading spot. A manned turret's operator, for example CE's M240B, stands on that spot.
- [ ] **Reach warning.** Wall off the loading spot. Expected: within about 4 seconds of game time the inspect panel shows the red line "No colonist can reach this turret's loading spot." It disappears once the spot is reachable again. Enemy or neutral E never shows it.

### 1.4 Mini-turret made of stuff
- [ ] **Mount.** Build a **plasteel** mini-turret on a **granite** high platform (and repeat on a low one). Expected:
  - E is drawn granite-tinted.
  - Max HP = plasteel mini-turret HP × (1 + 0.4 × granite's HP factor) on the high platform, 0.2 on the low one.
  - Market value = plasteel mini-turret + granite platform.
  - Flammability is the higher of the two.
- [ ] **Uninstall.** Expected: the crate holds a plasteel mini-turret. Its HP is its HP before mounting, after any damage taken, and its rotation is reset to north. A granite platform is left behind.
- [ ] **Deconstruct.** Expected: the refund is the mini-turret's own cost list with plasteel, not granite. The granite platform stays with its recorded HP.

### 1.5 CE flak (3×3)
- [ ] **Placement.** Build a 3×3 platform, then place CE's flak turret on it. Its own interaction spot lies inside its footprint. Expected: placement is accepted on an exact match.
- [ ] **Built E.** Expected: E's loading spot is the platform's (two cells in front of the centre). A colonist can man and reload it from there. The firing height is 8.0 on a high platform, 4.0 on a low one.
- [ ] **Install.** Uninstall a flak E and install the crate on another 3×3 platform. Expected: placement is accepted on an exact match, and it mounts there.
- [ ] **Off-platform.** A flak placed off any platform behaves exactly as without this mod.

### 1.6 Autoloader next to E
This needs a mod that adds CE autoloaders (`Building_AutoloaderCE`).
- [ ] Place an autoloader adjacent to E, with matching turret tags. Expected: it reloads E up to the doubled capacity. Its turret filter treats E like the base turret. No colonist has to stand on the loading spot.

### 1.7 Uninstall with an overfull magazine (DESIGN 2.3, 2.7)
- [ ] **Uninstall.** Fill E's magazine above the base capacity (anything up to 2× base). Uninstall it. Expected:
  - The crate holds the base turret with exactly the base capacity loaded.
  - The excess lies next to the platform as ammo items, not forbidden.
  - If one ammo item holds several rounds, the leftover partial rounds become the ammo's partial-unload item, or stay loaded if it has none.
  - The platform comes back with its recorded HP.
- [ ] **Reinstall the crate.** Install it on a ground spot. Expected: it is a normal turret with base capacity and normal stats.

### 1.8 Reinstall
- [ ] **Elsewhere.** Reinstall E on open ground. Expected:
  - It arrives as a normal base turret.
  - The old platform stays on the original cells with its recorded HP.
  - The magazine is trimmed as in 1.7.
  - Placement is checked as for the base turret, which is what gets installed. An automatic E (mini-turret, autocannon) can be reinstalled on a cell with a wall directly in front of it, because the base turret has no loading spot.
- [ ] **Onto another matching platform.** Expected: it mounts there. Both platforms exist until the second one is absorbed. The second platform is not deconstructed first: no platform material drops.
- [ ] **In place, same rotation.** Expected: it mounts again on the returned platform. The install blueprint survives the platform coming back.
- [ ] **In place, different rotation.** Expected: it mounts with the platform's rotation (documented limitation). For a 2×2, an install blueprint that no longer covers the platform rect is rejected.

### 1.9 Deconstruct (DESIGN 2.3, 2.6)
- [ ] Deconstruct a fully loaded E. Expected:
  - CE drops the whole magazine.
  - The refund is the base turret's costs with its own material, at the normal deconstruct fraction. No platform material or components are refunded at this point.
  - The platform reappears on exactly the same cells with its recorded HP.
  - Refunded items and ammo end up beside the platform, not deleted.
  - The roof above stays.

### 1.10 Destroy (DESIGN 2.3)
- [ ] **Kill.** Kill E with the dev kill action, or let raiders destroy it. Expected:
  - The platform is left at half its max HP, with E's faction.
  - If E stood in the home area, colonists with construction enabled go and repair the platform at once, without it taking damage first.
  - The ammo is lost.
  - No rebuild blueprint appears, even with auto-rebuild on.
  - No errors; the roof stays.
- [ ] **Vanish.** Use the dev destroy action (vanish) on E. Expected: no platform comes back.
- [ ] **Rotated 2×2.** Repeat the kill with a 2×2 E on a rotated platform. Expected: the platform covers exactly E's former cells.

### 1.11 Roof safety while swapping (DESIGN 2.5)
- [ ] Build a roofed area whose only support within range is the platform. Mount a turret, uninstall it, mount again, deconstruct E, mount again, then kill E. Expected: the roof never collapses. It may collapse only when the bare platform itself is finally removed.

### 1.12 Save and load
- [ ] **Saved state.** Save with several E mounted: mixed sizes and materials, one damaged, one with an overfull magazine, one manned and firing with rounds in flight. Load. Expected:
  - Each E keeps its label, HP, magazine and inspect lines.
  - Uninstall, deconstruct and kill still return the right platform with the right HP and material.
  - Rounds that were in flight still follow E's roof rules (phase 2.7).
  - No errors.
- [ ] **Changed costs.** Change the material and component settings, restart, and load. Expected: existing platforms and E load fine, and new platforms use the new costs.

### 1.13 Gravship move (Odyssey)
- [ ] Put E on a gravship (one 1×1 and one 2×2 if possible). Launch, and land with a different rotation. Expected:
  - E is still elevated, with the same HP, magazine and inspect lines.
  - Uninstalling or deconstructing it afterwards puts the platform exactly under E's current footprint.
  - Nothing is wiped next to it.

### 1.14 Replace Stuff installed
- [ ] **Replacing E.** Try to place a wall, door or platform blueprint directly over E, as a Replace Stuff replacement. Expected: rejected. No duplicate platform and no two buildings on one cell.
- [ ] **Building on a platform.** Build a turret on a platform with Replace Stuff loaded. Expected: the frame sits on the platform and finishes as E. Mounting works for build, install and reinstall.
- [ ] **Changing the platform material.** Use Replace Stuff's tool on a bare platform. Expected: a platform of the new material, with no errors.
- [ ] **Rebuild after a kill.** Kill E, then rebuild the base turret by hand on the platform that came back. Expected: it mounts normally. Replace Stuff's settings carry-over for destroyed buildings causes no errors.

### 1.15 Settings
- [ ] **HP bonus.** Change the high platform's bonus from 40% to 60% while E stands on a high platform and another on a low one. Expected: only the first E's max HP rises, at once, and its current HP is not rescaled. After the settings window closes, colonists repair that E up to its new max. On uninstall, E loses exactly the HP it gained when it was mounted. The same with the low setting for the low E.
- [ ] **Unmount gives the same kind back.** Uninstall an E from a low platform and one from a high platform. Expected: each leaves its own kind of platform.
- [ ] **Ammo multiplier.** Change it to ×1. Expected: magazine capacity follows. An overfull magazine is trimmed only when E is unmounted.
- [ ] **Debug overlay with dev mode off.** Expected: nothing is drawn.
- [ ] **Platform floors.** Move the high platform slider from floor 3 to floor 1, then to floor 10, with a mini-turret E on a high platform and another on a low one. Expected: the label reads "floor 1 (2.0 above the ground)" and "floor 10 (20.0 above the ground)", the slider stops at whole floors, and only the high E's inspect panel changes (2.85, then 20.85), at once. The low slider changes only the low E. After a restart the chosen floors are kept.
- [ ] **Empty platform height.** Turn the mod's debug overlay on. Put a 1×1 mini-turret E on a low platform (top 2.85) and a standing human enemy 15 cells apart on one row, and an empty low platform 4 cells in front of E on the enemy's side. Expected: the overlay shows the platform cell clear at about 2.3, and the enemy can shoot E. Replace it with an empty high platform (6.0): that cell is blocked at about 2.3, and the enemy can't shoot E over it. (CE's own partial-LoS debug labels cells with half the height, "3" for a 6.0 platform.)

## Phase 2: combat with heights (DESIGN 3.1, 3.2, 3.4)

The numbers in this phase were worked out for a muzzle of 2.85. **Use low platforms at the default floor 1** for this phase unless a test says otherwise. Use an automatic mini-turret E (top 2.85) and human enemies 15-20 cells away. Turn the debug overlay on.

### 2.1 Walls near E vs walls near the target
- [ ] **Walls next to E.** Put a wall line directly in front of E, and an enemy in the open. Expected: E fires over the wall. The overlay shows the wall cell as clear, with a height well above 2.0. The enemy can see and shoot E: its line to E's top clears the wall too.
- [ ] **Wall next to the target.** Put the enemy directly behind a full wall on E's side, one cell away from it. Expected: E does not fire. The overlay shows the wall cell as blocked, with a height below 2.0.
- [ ] **Wall moving out.** Move that wall away from the target, one cell at a time. Expected: E starts firing once the line height at the wall cell exceeds 2.0.
- [ ] **Aim over a wall partway.** Use a 1×1 E and a standing human enemy 15 cells away on the same row or column. Put a full wall in the sixth cell from E (five open cells between them), so the wall's far edge is about 43% of the way. For a human the rule only acts when that far edge is between about 39% and 46% of the way; closer to E, CE's own centre-mass aim already clears the wall. Expected: the overlay shows the wall cell clear at about 2.05, so only the enemy's upper body shows over the wall. E fires. A large share of its rounds pass over the wall and hit the enemy's upper body or fly past. CE's sway and spread still put part of them into the wall (CE intercept debug shows the impacts), but clearly fewer than without the aim rule (DESIGN 3.2, aim): there, CE's centre-mass line would pass about 0.1 below the wall top and most rounds would hit the wall.
- [ ] **Low cover next to the target.** Use sandbags instead. Expected: E fires whenever the overlay shows the line above the sandbags' height.
- [ ] **Platform height.** Put a standing enemy one cell behind a wall, then move E away from it cell by cell, first on a low platform and then on a high one. Expected: on the high platform E keeps seeing and hitting the enemy's upper body over the wall from much farther away than on the low one.

### 2.2 Roofed storage behind E, and E under rock
- [ ] **Built roof over E.** Put E in a wall line with a roofed storeroom directly behind it, the roof also covering E's cell. Expected:
  - E fires out normally: the built roof slab (2.0-2.1) is below its 2.85 muzzle, and its rounds don't strike the roof over its own cell (no red "x" there).
  - Enemies outside can see and shoot E.
  - Pawns inside the storeroom can't be seen or shot by enemies over the wall.
- [ ] **Thick built roof over E (Raise The Roof).** Put Raise The Roof's constructed steel roof over a low-platform E's cell and over the cells in front of it. Expected:
  - Setting "Non-rock roofs are as thin as a built roof for rounds and lines of fire" on: E fires out as under a built roof (its 2.85 muzzle is above 2.1), its firing zone matches the one under a built roof, and its rounds don't hit the steel over its own cell.
  - Setting off: E can't fire (its muzzle is inside CE's 2.0-4.0 slab), its own cell is shown blocked, and its firing zone is empty. Switching the setting back takes effect at once.
  - Either way, drop pods still don't land under the steel roof, and a mortar shell landing on it is stopped as by a thick roof.
- [ ] **Rounds aimed at a steel-roofed E.** Put a low E in the open (no walls around it) under Raise The Roof's steel roof, with raiders 15-20 cells away. Expected:
  - Setting on: raiders see, target and shoot E. CE aims ground shooters at E's middle (about 1.4), so their rounds hit E's body; a stray round that reaches E's cell above 2.1 hits E too, not the roof (no red "x" above 2.1).
  - Setting off: raiders neither see nor shoot E, because E's top (2.85) lies inside CE's 2.0-4.0 steel slab over its own cell, as under thin rock. Switching the setting back on makes E a target again at once.
  - To see rounds meet the 2.1-4.0 band, use a shooter above the roof (a hostile E on a high platform) or the steel porch item in 2.7.
- [ ] **Ground shooters.** Setting on: colonists in a steel-roofed room fire out through a doorway; no change from a built roof (their rounds stay below 2.0).
- [ ] **E under rock roof.** Put E under thin rock roof, then under overhead mountain. Expected: E can't fire at anything; its own cell is shown blocked. A flak E (top 4.0) under thin rock is blocked too.
- [ ] **Roof over a 2×2 E's own cells.** A 2×2 E (autocannon) on a low platform with a built roof over exactly its four cells (muzzle 2.85). Put an enemy in the cell right next to E's west or south edge, in one of E's two rows. Expected: the line check is blocked (the line leaves E's roofed cell at about 1.6, below the 2.1 slab top), the overlay flashes that own cell, and E doesn't fire into its own roof. The same holds for sight from that E. A standing enemy in the second cell out is on the boundary (about 2.11): the line check passes, and E raises its aim over its own roof, so most rounds clear it, though spread and sway still put some into it; a crouching one there is blocked. Targets farther away are fine.
- [ ] **Muzzle inside rock.** A 2×2 E at the edge of an overhead mountain, with rock roof over only the one own cell that touches the footprint's centre corner, on a high platform (muzzle 6.85, slab top 8.0). Expected: E can't fire or see in any direction, also toward targets whose line never enters that cell; the overlay flashes that cell.
- [ ] **E under rock roof, high platform.** Repeat on a high platform. Expected: a mini-turret E (6.85) at the edge of a thin rock roof fires out at targets in the open where the line stays above 4.0 across the roofed cells. Under overhead mountain (8.0) it still can't fire. With the high platform set to floor 4 (8.85) it fires out from under overhead mountain too.

### 2.3 Porch roof over an enemy
- [ ] **Enemy under a porch.** Put an enemy under a built 3×3 porch roof in the open. Expected:
  - E can't target or hit that enemy. The target cell shows blocked.
  - The enemy can't see or shoot E either, because its own roofed cell blocks the line.
- [ ] **Enemy behind the porch.** Put the enemy just beyond the porch, so the line crosses roofed porch cells near the target. Expected: blocked wherever the line height in a porch cell is below 2.1.
- [ ] **Bystanders under the porch.** E fires at enemies standing next to the porch. Expected: rounds that stray into the porch hit the roof (red "x" with CE intercept debug) and never the pawns under it. Pawns under the porch are not suppressed by rounds passing at 2.0 or higher.

### 2.4 Rock outcrop vs mountain
- [ ] **Unroofed outcrop.** Put an enemy beyond a small unroofed rock outcrop; check with the roof overlay that there is no roof. Expected: the rock counts as a 2.0 wall. E fires over it when the outcrop is near E, and the outcrop gives cover when it is near the enemy.
- [ ] **Mountain.** Put an enemy beyond rock under thin rock roof or overhead mountain. Expected: blocked on a low platform (slab tops 4.0 and 8.0 are above the line). The roofed rock cells show blocked. At high floors, E fires over the mountain wherever the line stays above the slab top.

### 2.5 Friendly crowd
- [ ] **Crowd near E.** Use a manned E, for example CE's M240B. Put five colonists in the line 2-3 cells in front of E, and an enemy 15 cells away. Expected: E fires over their heads; the overlay shows those cells clear.
- [ ] **Crowd near the target.** Move the colonists to 1-2 cells in front of the enemy. Expected: E holds fire, because the line passes through their bodies.
- [ ] **Ground shooter for comparison.** A colonist rifleman behind the same near-E crowd holds fire, as in plain CE.
- [ ] **Unmanned E.** Use an automatic E. Expected: the same outcome; colonists block only if the line passes through them.
- [ ] **Friendlies near an enemy pawn shooting at E.** Expected: that pawn holds fire only if the line to E passes through a friendly's body.

### 2.6 RPG misses at E
- [ ] Raiders with rocket launchers fire at E from 20 or more cells. Expected: plain CE flight (P4):
  - Hits damage E only. There is no separate platform object to hit.
  - Rockets that miss high or wide fly on and land beyond or beside E.
  - E's own roof, if it has one, is checked like any other roof.
  - Explosions follow CE, and no errors appear.
- [ ] Note: a blast at E's upper part (above 2.0) is treated by CE as above the roofs and can damage unroofed cells behind nearby walls (documented limitation).

### 2.7 E's rounds and roofs (P2, P3)
- [ ] **Strays through a porch.** Put E under a built roof and let it fire at an enemy standing next to a porch. Expected: stray rounds that pass through porch cells below 2.1 hit the porch roof (red "x" with CE intercept debug). Plain CE would have set those rounds to ignore roofs for the whole flight, because they were launched at 2.85 from a roofed cell.
- [ ] **Strays through a steel porch.** Repeat with Raise The Roof's steel roof on the porch. Expected: setting on, strays hit the porch only below 2.1, as with a built roof; setting off, they also hit it at 2.1-4.0.
- [ ] **HE rounds.** An HE-firing E, for example an autocannon, fires at enemies next to a porch, and a round hits the porch roof. Expected: the round explodes at the roof. Under CE's above-roof blast rules, pawns under the roof should not be hit by the blast.
- [ ] **Operator leaves.** A manned E's operator is drafted away while a burst is still in flight. Expected: those rounds still follow E's rules.

### 2.8 Suppression
- [ ] **Above height 3.** A flak E (top 4.0) fires at long range past a group of enemies near the first part of the path, where the round is still above 3. Expected: those enemies are suppressed, as shown by the CE suppression status in the inspect panel. A plain CE round at that height would suppress nobody.
- [ ] **Under a roof.** Enemies standing under a roof near E's line of fire, while the round passes at 2.0 or higher, are not suppressed. Enemies in the open next to them are.
- [ ] **Rounds aimed at E.** Expected: they suppress colonists near E exactly as plain CE does.

### 2.9 Unchanged things
- [ ] **Mortars.** Mortars and their shells behave as in CE; mortars can't be mounted.
- [ ] **No E on the map.** A firefight on a map without E behaves exactly as with CE alone.
- [ ] **Performance.** A large firefight, 40 or more pawns and several turrets, with no E on the map. Expected: no noticeable TPS drop compared with the mod disabled. Then add several E. Expected: no large drop.

## Phase 3: spotting, targeting, debug overlay (DESIGN 3.3, 3.5, 3.6)

High platforms at the default floor 3, unless a test says otherwise.

### 3.1 E spots over its walls
- [ ] **Behind its own wall.** Put E behind a closed wall line, with enemies approaching in the open. Expected: E acquires them and fires. With vanilla's flat sight it would see nothing through its own wall.
- [ ] **Enemy behind a wall.** Expected: an enemy standing right behind a full wall on its own side of the field is not acquired.

### 3.2 Enemies spot E
- [ ] **Over a wall.** Raiders approach a walled base with E behind the wall. Expected: they see E over the wall and shoot it, though they can't see the ground turrets or pawns behind the same wall.
- [ ] **From under a roof.** An enemy standing under a roof can't see E.

### 3.3 Pawns and smoke
- [ ] **Pawns.** Put a crowd of pawns between E and an enemy. Expected: E still acquires the enemy, because pawns don't block sight. Whether it fires depends on 2.5.
- [ ] **Smoke.** Lay CE smoke (blind smoke) between E and the enemy. Expected: E loses sight, and the enemy can't see E through the smoke either. This works when the target search is gas-blockable (TargetScanFlags.LOSBlockableByGas), which ordinary turret and pawn searches normally are.

### 3.4 Manned E
- [ ] **Seeing.** Use an M240B E. Expected: the operator sees and targets from E's muzzle (the M240B's CE height + 6.0 on a high platform, + 2.0 on a low one, as the inspect panel shows), not from the ground.
- [ ] **Before manning.** Expected: a colonist still walking to the loading spot gets no elevated sight.

### 3.5 Target choice (C2)
- [ ] Put one enemy behind sandbags and another in the open at a similar distance. Expected: E does not systematically skip the one behind sandbags, because no flat cover penalty applies when E scores targets. Test both a normal CE turret E and one using CE's non-snap targeting, if available.

### 3.6 Debug overlay
- [ ] **Line-of-fire checks.** Turn the overlay on in dev mode and watch E's checks. Expected: each cell on the line flashes 0 if blocked and 0.7 if clear, with the line's lowest height in that cell as text.
- [ ] **Sight checks.** Expected: 0 for blocked (roof, gas or obstacle), 0.7 where an obstacle was tested and passed, 0.4 for path cells.
- [ ] **Dev mode off.** Expected: nothing is drawn.
- [ ] **CE's own overlay.** CE's partial-LoS overlay still works alongside, and in the same cells.

## Phase 4: force attack and firing zone (DESIGN 3.7, 3.8)

High platforms at the default floor 3, unless a test says otherwise.

### 4.1 Force attack
- [ ] **Unmanned E.** Select an automatic E (mini-turret or autocannon). Expected: CE's "Attack" button is there. Order it to attack an enemy or a building in range. Expected: it keeps firing at it until you press "Stop forced attack" or the target is gone. Clicking empty ground gives no order for these guns.
- [ ] **Ground spot.** A flak E (its gun can target locations; it is a manned gun). Order it at an empty cell in range while nobody mans it. Expected: the order is accepted, and it fires at that cell once a colonist mans it.
- [ ] **Forgotten on despawn.** Give an unmanned E a forced target, then uninstall and reinstall it on another platform (and, with Odyssey, move it by gravship). Expected: it has no forced target afterwards and picks targets by itself.
- [ ] **Range and arc.** Order an attack beyond range, inside the minimum range, or outside a firing arc. Expected: CE's own rejection messages, and no forced target.
- [ ] **Manned E, unmanned.** Use an M240B E with nobody manning it. Expected: the Attack button is there and a target can be set, but E doesn't fire until a colonist mans it; then it fires at the forced target.
- [ ] **Base turret.** The same automatic turret off a platform. Expected: no Attack button, as in plain CE.
- [ ] **Enemy E.** An E of another faction. Expected: no control buttons.
- [ ] **Hold fire.** With hold fire on, a forced target is set but E doesn't fire, with CE's message.

### 4.2 Firing zone
- [ ] **Button.** Select a player's E. Expected: a "Show firing zone against human-sized target" button with the zone icon, off. No outline. Turn it on: a blue outline appears inside CE's range ring a moment later; turn it off: it disappears. Save and load: the button keeps its state. Select several E and click it: each switches. An enemy E has no button.
- [ ] **Open ground.** Turn the zone on for an E in the open. Expected: the outline fills CE's range ring, minus E's own cells and the minimum range.
- [ ] **Wall shadow.** Use a mini-turret E on a high platform (muzzle 6.85) and put a wall line in the fifth cell in front of it (far edge 5.5 cells from E's centre). Expected: a human is hidden only about a cell behind the wall: the shadow reaches about 5.5 × (6.85 - 1.0) / (6.85 - 2.0) ≈ 6.6 cells from E. Move the wall closer to E: the shadow shrinks. On a low platform (muzzle 2.85) the same wall's shadow reaches about 5.5 × 1.85 / 0.85 ≈ 12 cells.
- [ ] **Roofs.** A built-roof room in range is left out of the outline; ground right behind it shows a short shadow. Rock overhead above E's muzzle hides everything.
- [ ] **Doors, trees and low cover.** A closed door casts a shadow like a wall; an open door doesn't. A doorway itself is never outlined (no one stands in it). Tree and chunk cells are outlined like open ground (trees never block a shot, as in CE). A sandbag line far from E hides almost nothing (a human's head shows over it); sandbag cells themselves are outlined, since a human standing in one stands higher.
- [ ] **Fog and arcs.** Unexplored cells are never outlined. A turret with a firing arc is outlined only inside it.
- [ ] **Matches the line check.** With the debug overlay on and no pawns around, put a standing human enemy (spawned, held in place) on a cell just inside the outline and one just outside it, and let E acquire them, or order an attack on them. Expected: inside, the line check passes; outside, it is blocked, and the overlay shows the cell that blocks it. A crouching pawn (in combat, next to cover) is lower and can be hidden just inside the edge; that is expected.
- [ ] **Ammo range.** Load ammo with a shorter effective range. Expected: the outline shrinks to that range, inside CE's range ring.
- [ ] **Diagonal gaps.** Two walls touching only at a corner, in E's line on an exact diagonal. Expected: no outlined streak through the gap.
- [ ] **Updates.** Build a wall in range while E is selected. Expected: the outline changes within one to two seconds (longer for a long-range E among many walls, or with several E selected), also while paused (god mode). Change the platform's floor in the settings: the outline changes as soon as it is worked out again. Edit a turret's firing arc while paused: the outline follows the arc being edited. Switch CE's arc off with its button: the outline shows all directions at once.
- [ ] **Many selected.** Box-select ten or more E, including long-range ones (autocannon, flak). Expected: every one shows its outline within a few seconds; turrets without an outline yet are served first, and the game doesn't slow down.
- [ ] **Button off.** Expected: no outline; CE's range rings stay.
- [ ] **Performance.** Select an E with a long range (60 or more cells) and watch frame time, in the open and inside a walled, roofed base. Then select several E at once, and drag a firing arc while editing it. Expected: no stutter; frame time rises by at most about 2.5 ms while outlines are being worked out, plus the outline drawing, a re-filter after each refreshed outline and, while dragging an arc, about ten re-filters a second. With many E selected, each outline refreshes more slowly than once a second.
- [ ] **Deselect mid-way.** Select a long-range E and deselect it at once, then select it again after a few seconds. Expected: the outline is worked out fresh and matches the map as it is now.

### 4.3 Firing arcs (DESIGN 3.9)
Turn on CE's "Enable arc of fire". Use a turret with a limited swing (Local's CE Guns GAU-19, ±70°; or CE's flak, ±135°).
- [ ] **Turn the arc anywhere.** Mount it on a platform whose loading spot faces away from the threat. Click "Adjust arc of fire", point at the threat and left-click. Expected: the arc points at the threat, with no red limit lines, and its width can still be changed (middle-click) within 10-140°. The inspect line shows the new arc. The turret fires only inside it.
- [ ] **Behind the facing.** Set an arc centred straight behind the platform's facing, then save and load. Expected: the whole arc still works after loading, on both sides of that line.
- [ ] **Direction kept on mounting.** Place the turret's blueprint facing one way on a platform that faces another way. Expected: once built, its arc points the way the blueprint faced.
- [ ] **Back on the ground.** Uninstall it and install it on the ground. Expected: its arc is within its normal ±70° again, as close to its old direction as that allows; the red limit lines are back. It never fires outside that cone, also when its arc on the platform pointed behind the platform's facing.
- [ ] **From platform to platform.** Point an E's arc somewhere outside ±70° of its platform's facing, then reinstall it on another platform. Expected: it points the same way on the map again, not clipped to ±70°.
- [ ] **Full-circle turret.** An M240B behaves as before, on the ground and on a platform.

### 4.4 Interception (CIWS)
Use a CE CIWS turret, for example the MLG27 (PB_MLG27_TurretBase). Turn on dev mode and the debug overlay setting, then open the debug log and clear it first: once the log reaches its message limit, nothing more is written.
- [ ] **Side by side.** One MLG27 on a platform and one on the ground in the open, both powered, loaded and not on Hold fire; in "Manage CIWS targets", drop pods and shuttles are not ignored. Raise a hostile drop-pod raid 40-70 cells away (several pods: CE lets only one turret engage each pod). Expected: both turrets fire at pods. The log shows, per turret and pod, a "Debug: CIWS ... seen" line with the turret's state, "targeted" for the pods it engaged, and "lands without having been targeted" with the state for each pod that reaches the ground without this turret targeting it; a pod shot down by the other turret has only the "seen" line, which is written for every incoming pod on the map whatever its distance. The once-per lines start over after loading a save.
- [ ] **Hold fire.** Put both on Hold fire and repeat. Expected: neither intercepts, as in CE; the log shows "Turret: hold fire True".
- [ ] **Roofed.** Repeat with a built roof over the elevated turret. Expected: no change.
- [ ] **Attack order.** Order the elevated MLG27 to attack an empty spot, then raise the drop-pod raid. Expected: after the attack on the spot it is in the middle of, it turns to the pods, keeps at a pod over several bursts until it is down or lands, and goes back to firing at the spot with its main gun once none are left and its rounds have arrived; the "seen" lines show "forced target" with the spot. Pods that fall during an attack on the spot already under way can land untargeted. Give the order again while pods are falling: same result. With CE's arc of fire on and a CIWS turret that has an arc, the order is still there after a pod leaves the arc. A manned CIWS (CE's KPV) with an order keeps firing at its order, as on the ground.
- [ ] **Other CIWS mods.** A line "this mod's line of fire was asked about ... in the air" names another mod's verb type, which calls CE's base line check and gets CE's answer. A line "this mod's sight was asked" names only the seer (the turret or the pawn manning it); the sight answer stays vanilla's. Select the message in the debug log to see the caller in its stack trace.

## Assumptions to verify

The code relies on these points about vanilla RimWorld, and in a few cases about the installed CE and Harmony builds. The open-source references don't establish them. The code is written to be correct either way where it can be. For each assumption: how to check it, and what breaks if it is wrong.

### Loading, build and patching
- **A1. RimWorld 1.7 loads this net48 DLL the way it loads CE's net48 DLL** (DESIGN 0).
  - Check: start on 1.7. The generation log line appears, with no TypeLoadException or MissingMethodException naming ElevatedGunPlatform.
  - If wrong: the mod doesn't load on 1.7. A net10 build can't replace it, because CE's API uses Unity types.
- **A2. The Harmony 2.x API used here behaves the same on Harmony 3 (1.7), including Harmony 3's patch-binding validation.** Parameters use exact or base types and the original names.
  - Check: no "Failed to apply patch class" errors on 1.6 or 1.7.
  - If wrong: that patch class is skipped with one error, and its rule is off.
- **A3. A postfix may bind the original's `out` parameters as `ref`.** This applies to CalculateHeightRange's `heightRange` and `shotHeight`. The research notes cite the Harmony source for this on 2.4.3 and Harmony 3.
  - Check: the inspect panel shows "Firing height: 6.85 (platform on floor 3)" for a mini-turret on a high platform, the overlay line starts at 6.85, and E fires over an adjacent wall (2.1).
  - If wrong: that patch fails or does nothing, and E fights at plain wall height (2.0).
- **A4. The installed CE DLL uses the parameter names of the CE source.** Harmony binds by name:
  - CalculateHeightRange: `thing`, `heightRange`, `shotHeight`
  - CanHitCellFromCellIgnoringRange: `shotSource`, `targetLoc`, `targetThing`
  - CheckCellForCollision: `cell`
  - CanCollideWith: `thing`
  - RayCastSuppression: `muzzle`, `destination`, `map`
  - the 3-parameter Launch: `launcher`, `equipment`

  The probe only checked types and access for the protected methods. The private CalculateHeightRange can't be checked at compile time.
  - Check: no patch errors at startup, and phase 2 works.
  - If wrong: a "Parameter not found" error, and that rule is off.
- **A5. The installed CE release matches the reference source for every member found by name.** These are: CalculateHeightRange, CanHitCellFromCellIgnoringRange, ignoreRoof, the two Launch overloads, ExposeData, CheckCellForCollision, TryCollideWithRoof, CE_Utility.GetBoundsFor(IntVec3, RoofDef), CanCollideWith, ApplySuppression, RayCastSuppression, CheckForCollisionBetween, and NonSnapAttackTargetFinder.GetShootingTargetScore.
  - Check: no `[ElevatedGunPlatform] ... not found` warnings.
  - If wrong: the rule named in the warning is off.
- **A6. The vanilla APIs used under `Source/Combat/` are unchanged on 1.7.** The 1.7 API check skips `Source/Combat/`, so these are only compile-checked against 1.6:
  - `AttackTargetFinder.CanSee(Thing, Thing, Func<IntVec3, bool>)`
  - `CoverUtility.CalculateOverallBlockChance(LocalTargetInfo, IntVec3, Map)`
  - `Pawn.Drawer.leaner.LeanOffset`, public in the 1.6 references
  - `DebugDrawer.FlashCell(IntVec3, float, string, int)`
  - `JobDefOf.ManTurret`
  - Check: on 1.7, no patch errors for SpottingPatch_CanSee or TargetingPatch_CalculateOverallBlockChance. No MissingMethodException or MissingFieldException while E fires at pawns with the overlay on.
  - If wrong: a patch class fails to apply, or the line-of-fire prefix throws on every check against a pawn. That gives error spam, and E can't fire at pawns.

### Defs (DESIGN 1)
- **B1. Vanilla resolves XML cross-references before `DefGenerator.GenerateImpliedDefs_PreResolve`.** These are stuff categories, research, cost lists, turretGunDef, the verbs' defaultProjectile, designationCategory, minifiedDef and statBases. ResolveReferences and short hashes come after it. `DefDatabase.Add` assigns the index, as Replace Stuff also relies on.
  - Check: the generation counts are above 0. With dev mode on there are no config errors or duplicate short-hash errors.
  - If wrong: no or wrong turrets are eligible, or def errors appear at load.
- **B2. Vanilla generates blueprints and frames for defs added in the PreResolve prefix, and none for defs that have neither designationCategory nor minifiedDef at that time.**
  - Check: platforms can be built. The dev spawn list has no blueprint, frame or install blueprint named after an `EGP_Elevated_` def.
  - If wrong: platforms can't be built, or elevated defs become buildable directly.
- **B3. The check that stands in for `building.IsMortar` matches vanilla's IsMortar for vanilla and CE turrets.** IsMortar is not called before ResolveReferences. Instead the code checks the verb's isMortar, Verb_ShootMortarCE, flyOverhead, and whether the gun has CompChangeableProjectile.
  - Check: no "is a mortar (building.IsMortar) but was made mountable" warning at startup. The mortar and the rocketswarm launcher can't be mounted.
  - If wrong: a mortar-like turret becomes mountable; the warning names it.
- **B4. Vanilla labels a blueprint or frame as "label + suffix".** Generated platforms are relabelled by swapping that prefix.
  - Check: in another language, the 2×2 platform blueprint shows the translated name.
  - If wrong: blueprint and frame labels of generated platforms stay in English (cosmetic).
- **B5. CompProperties can safely be resolved twice, once for each parent def.** The base def and its elevated def share the same CompProperties objects.
  - Check: no load errors. E's comps work: ammo, power, mannable, breakdowns.
  - If wrong: errors at load, or comps misbehave on E.
- **B6. A minifiable ThingDef must have a thingCategory.** This is why elevated defs keep the base's categories.
  - Check: none needed.
  - If wrong: only filter-tree clutter, which could then be removed.
- **B7. DefOfs may not be bound yet at PreResolve,** so Wall, ComponentIndustrial and the stats are looked up by name.
  - If wrong: nothing breaks.

### Placement and mounting (DESIGN 2.1, 2.2, 2.4, 2.5)
- **C1. Every path that creates or installs a turret ends in the 7-argument `GenSpawn.Spawn`, given the same Thing object.** The paths are frame completion, Blueprint_Install, reinstall and dev spawn. Replace Stuff's CompleteConstruction wrapper still calls that overload. Before it spawns the turret, an install blueprint (vanilla `Blueprint.TryReplaceWithSolidThing`) wipes its cells for that base turret through the 5-argument `GenSpawn.WipeExistingThings` with DestroyMode.Deconstruct; during a wipe for a base turret that will mount on an exactly matching platform, that platform is kept.
  - Check: 1.2, 1.3, 1.5 and 1.8 mount in every path, with and without Replace Stuff. Installing or reinstalling onto a platform drops no platform material.
  - If wrong: that path spawns a plain turret that wipes the platform. If the install wipe goes through another method, install and reinstall onto a platform deconstruct the platform first and leave a plain turret on bare ground.
- **C2. In `CanPlaceBlueprintAt` and `_NewTemp`, the first parameters are `entDef`, `center`, `rot`, `map`, and parameter 5 is `thingToIgnore`, the thing being reinstalled, with the same meaning in both.**
  - Check: reinstalling E in place with the same rotation is allowed.
  - If wrong: in-place reinstall is rejected, or the placement rule is off with a warning.
- **C3. For CE's flak:**
  - (a) vanilla rejects an interaction spot that lies on an impassable thing;
  - (b) `ignoreInteractionSpots` skips only interaction-spot checks;
  - (c) invoking the original method runs the patched method;
  - (d) vanilla does not re-check the base turret's own spot while its frame is built.
  - Placement of a reinstalled E is checked with its base def, so the in-place reinstall of a flak E counts E as the platform it leaves behind for this re-check.
  - Check: 1.5, and reinstalling a flak E in place (1.8).
  - If (b) is wrong, placement is too lenient. If (d) is wrong, a flak frame on a platform can't be finished.
- **C4. Vanilla placement consults `GenConstruct.CanReplace` for existing buildings.** This is the Replace Stuff path.
  - Check: 1.14.
  - If wrong: the guard does nothing, and Replace Stuff might let a building replace E, which duplicates the platform.
- **C5. `SpawningWipes(base turret, platform)` is true; it is left as vanilla, except during the install wipe of C1.** Vanilla blueprints may or may not wipe what they overlap, so both directions are forced off for eligible turrets' blueprints and frames.
  - Check: a turret frame never removes the platform early, and an in-place reinstall keeps its blueprint (1.8).
  - If wrong: the platform vanishes early, or the reinstall blueprint is lost.
- **C6. Nothing outside `BlocksConstruction` orders the platform deconstructed to make room for the turret blueprint.** One candidate is vanilla 1.6's WorkGiver_DeconstructForBlueprint.
  - Check: 1.2, the frame test with builders available.
  - If wrong: colonists tear the platform down before building the turret.
- **C7. Roof collapse is triggered at despawn through `def.holdsRoof`.** Clearing it during the swap is therefore enough.
  - Check: 1.11.
  - If wrong: the guard does nothing. The replacement holds the roof anyway, unless vanilla checks within the same tick.
- **C8. Graphic fields on Thing and MinifiedThing are rebuilt lazily after being set to null.**
  - Check: E, the crate and the unmounted turret all draw with the right graphic and colour at once.
  - If wrong: the wrong graphic until reload, or render errors.
- **C9. `StatWorker.ClearCacheForThing`, plus removing the thing from every Thing-keyed dictionary on the stat workers, clears all per-thing stat caches,** vanilla's temporary and immutable caches included.
  - Check: right after mounting and unmounting, the info card shows the new HP and magazine capacity.
  - If wrong: stale stats for up to about 250 ticks, or until reload.
- **C10. Setting a non-rotatable base turret to Rotation North on unmount is safe.** This goes beyond the design's list of what conversion changes.
  - Check: 1.4, uninstalling from a rotated platform.
  - If wrong: render or placement oddities.
- **C11. Vanilla `CompMannable.ManningPawn` is set only while a pawn actually mans the turret, and may be empty right after load. The ManTurret job's targetA is the turret.**
  - Check: 3.4, and 1.12 with rounds in flight.
  - If wrong: a manning pawn's sight, targeting and rounds are treated as from the ground, or a pawn walking to E is treated as elevated too early.
- **C12. Vanilla `SpawningWipes` is false for a platform or E (edifices that don't transmit power) over a power conduit, as for a wall.** The mod's own rule only turns a wipe off, so it changes nothing then.
  - Check: 1.3, conduits; with dev mode and the debug overlay on, the log line "Debug: vanilla SpawningWipes(...) = False".
  - If wrong: vanilla wipes the conduit, and the mod's rule keeps it.

### Unmounting and lifecycle (DESIGN 2.3)
- **D1. Uninstall and reinstall both go through `MinifyUtility.MakeMinified(Thing, DestroyMode)` while E is still spawned.**
  - Check: 1.7 and 1.8.
  - If wrong: E travels as E with its record. It doesn't mount on a matching platform and wipes it as vanilla would; elsewhere it stays elevated with no platform; the old platform is not restored.
- **D2. `Thing.Position` and `Thing.Rotation` are still valid in a comp's `PostDestroy`.** Replace Stuff reads Position at the same point.
  - Check: 1.9 and 1.10 with a rotated 2×2.
  - If wrong: the platform lands on the wrong cells, or is not restored.
- **D3. `ThingWithComps.Destroy` calls the comps' `PostDestroy` inside `Building.Destroy`,** so the roof guard is still active when the platform respawns.
  - Check: 1.11.
  - If wrong: a roof may collapse when E is killed or deconstructed.
- **D4. Spawning the platform with `WipeMode.VanishOrMoveAside` moves aside items lying on E's cells.** These are the deconstruct refund, CE's dropped magazine and kill debris.
  - Check: 1.9.
  - If wrong: refunds or dropped ammo vanish.
- **D5. Kills use `DestroyMode.KillFinalize`.**
  - Check: 1.10.
  - If wrong: no platform after E is destroyed.
- **D6. Vanilla auto-rebuild skips the elevated def without errors, because it has no blueprintDef.**
  - Check: 1.10 with auto-rebuild on.
  - If wrong: errors on destruction, or a rebuild blueprint of the wrong def.
- **D7. Odyssey gravship moves keep the same Thing objects, despawned and spawned again without Destroy or MakeMinified, and may rotate them.** An E reaching Spawn directly is deliberately left alone, and platform respawns use E's live rotation.
  - Check: 1.13.
  - If wrong: E loses its record, or a platform appears or disappears.
- **D8. The leavings rule applies in every destroy mode, as written,** including Refund and Cancel.
  - Check: none practical.
  - If wrong: only dev or edge cases are affected.
- **D9. Edge case: `GenSpawn.Refund` minifying an E that another spawn is wiping, in dev mode or with mods,** respawns the platform inside that outer spawn.
  - Check: none practical.
  - If wrong: two things may conflict on one cell.
- **D10. Vanilla's repair list (`listerBuildingsRepairable`) checks a building's HP when it spawns and when it takes damage; `Notify_BuildingTookDamage` adds a damaged building once per faction and is safe to call again; `Mod.WriteSettings` runs when the mod settings window closes.**
  - Check: 1.10 (a killed E's half-HP platform is repaired at once), 1.3 (mount on a half-HP platform), 1.15 (raised HP bonus).
  - If wrong: those buildings wait for repair until they take damage again; or a building is listed twice (harmless: repair stops at max HP).

### Stats (DESIGN 2.6, 2.7, 2.9)
- **E1. Vanilla applies `StatDef.parts` before its final processing: the scenario factor, rounding and clamping.**
  - Check: none needed without scenario stat factors.
  - If wrong: with a scenario factor on MaxHitPoints, Flammability or MarketValue, E gets that factor twice.
- **E2. No def-level value of an elevated def is cached before the StatParts are added at startup.**
  - Check: the info card of an elevated def, opened from the dev spawn list, shows elevated values.
  - If wrong: the def-only info card shows base values (cosmetic).
- **E3. Printing the platform at Building altitude minus `Altitudes.AltInc` puts it below the turret and above floors.**
  - Check: 1.3; the platform texture shows under the turret base.
  - If wrong: the texture covers the turret or is hidden (cosmetic).
- **E4. CE's magazine stat label for a def-only request shows the base size, even though the value includes the multiplier.**
  - Check: compare the def info card with a spawned E's.
  - If wrong: nothing; cosmetic.
- **E5. `isStuffableAirtight` makes the building airtight when its material is.**
  - Check: 1.3, the Odyssey test.
  - If wrong: E and platforms leak air.

### Line of fire (DESIGN 3.1, 3.2)
- **F1. Whether `Vector3.ToIntVec3` keeps y, and whether `IntVec3.DistanceTo` is horizontal, is unknown.** Neither matters: every cell is flattened before lookups, comparisons and distances. Where CE compares unflattened cells, the target cell was sometimes not excluded when the shot height is 1 or more. This version always excludes the flattened source and target cells, as the spec says.
  - Check: the overlay never shows the target's own cell blocked by the target itself.
  - If wrong: nothing expected.
- **F2. `Bounds.IntersectRay(Ray, out distance)` can report a distance of 0 or less when the ray starts inside the box.** The friendly-fire test counts any distance up to the segment length as crossing, so "starts inside" counts as crossing.
  - Check: 2.5.
  - If wrong: rare wrong hold-fire decisions for friendlies right at the muzzle.
- **F3. A missing VerbPropsCE is treated as `ignorePartialLoSBlocker = false`, and a missing `Controller.settings` as CE debug off.** CE itself would throw in both cases.
  - If wrong: nothing; no CE rule changes.
- **F4. When the caster has no map or no LightingTracker, CE's original routine runs.**
  - If wrong: nothing.
- **F5. `Pawn.Drawer.leaner.LeanOffset`** is covered by A6.

### Projectiles and suppression (DESIGN 3.4)
- **G1. A Harmony patch on a virtual method affects only that method's body,** so CE overrides that don't call base are unaffected:
  - ProjectileCE_Bursting's 8-parameter Launch doesn't call base. It never sets ignoreRoof, and its launch is still recorded through the 3-parameter Launch.
  - ProjectileCE_CIWS's CanCollideWith and CheckForCollisionBetween don't call base. P3 and the high-suppression rule therefore don't apply to CIWS rounds, which never suppress anyway.
  - Check: an elevated CIWS-class turret (CE's KPV) fires at ground targets with no errors.
  - If wrong: nothing breaks.
- **G2. Every roof lookup flattens the cell,** because whether `ToIntVec3` keeps y is unknown. The flattened cell is what goes to TryCollideWithRoof; CE's roof bounds overwrite y anyway. Both cells in the high-suppression call are flattened. Arguments from CE's own RayCastSuppression callers are passed on unchanged.
  - If wrong: nothing expected.
- **G3. A round counts as fired by E when E is its launcher, or when the pawn manning E launched it.** This differs from the earlier implementer note, which tested the operator on every check. The operator case is decided once:
  - at launch, by the 3-parameter Launch postfix;
  - for rounds in flight in a save, at load, from the launcher's saved ManTurret job on E;
  - instant rounds are tested live inside RayCast;
  - without the Launch postfix, the operator is tested live.
  - Check: 2.7 (operator drafted mid-burst) and 1.12.
  - If wrong: some operator rounds follow plain CE rules. They ignore roofs when E is roofed, and can hit pawns under roofs.
- **G4. CE's CompFragments launches fragments with the shell's launcher as instigator and the fragment itself as equipment.** The code treats such a round as not fired by E, so fragments follow plain CE rules. This replaces the earlier note that fragments count as fired by E.
  - Check: 2.7, the HE test with fragments; no errors.
  - If wrong: fragments may count as E's rounds. That is harmless: they check roofs and spare pawns under roofs.
- **G5. CE's RayCast stores origin, shotHeight, shotAngle and shotRotation (in radians, +90°) before calling RayCastSuppression.** The roof rule rebuilds the beam from these values. RayCast itself never checks roofs or CanCollideWith; this is unchanged CE behaviour.
  - Check: with a mod that adds an instant-hit CE turret, its E suppresses pawns near the beam, but not pawns under a roof that the beam passes at 2.0 or higher.
  - If wrong: wrong suppression of pawns under roofs by instant rounds.
- **G6. If ProjectilePropertiesCE is missing, the RayCastSuppression prefix runs CE's original,** which fails exactly as CE would. The shared routine returns quietly when props or map are missing.
  - If wrong: nothing.
- **G7. Harmony skips later bool-returning prefixes once an earlier prefix has chosen to skip the original.** This was checked in the reference Harmony source. If another mod's earlier prefix skips CheckCellForCollision, neither the roof check nor the scan mark happens for that cell; this is acceptable.
  - If wrong: nothing.
- **G8. A roof's slab matters only where a direct-fire round or a line meets a roof.** CE reads it only through `CE_Utility.GetBoundsFor(IntVec3, RoofDef)`: in TryCollideWithRoof (and SOS2Compat's ShipProjectileCE override) and in SOS2Compat's ship verb line check. This mod reads it through ElevatedLine.RoofTop. Fly-overhead rounds return from CheckForCollisionBetween before any cell check and land by the def's isThickRoof (ProjectileCE.ImpactSomething). Drop pods (vanilla, and Raise The Roof's own patch), infestations and roof collapse use the def's flags. The JIT doesn't inline GetBoundsFor (it is far above Mono's inlining size), so the global prefix reaches every caller.
  - Check: setting on, an E's strays over a steel-roofed porch above 2.1 fly on; a mortar shell on the steel roof still stops there; drop pods still avoid it.
  - If wrong: rounds hit thick built roofs at CE's 4.0 while the line of fire allows the shot, or a thick roof's protection from shells changes.

### Spotting and targeting (DESIGN 3.3, 3.5)
- **H1. Vanilla `AttackTargetFinder.GetShootingTargetScore` is static with parameters (IAttackTarget, IAttackTargetSearcher, Verb), one of them named `searcher`.** It is not public in the reference assemblies. It is resolved by types, then by name only. It is accepted only if it is static and has `IAttackTargetSearcher searcher`.
  - Check: no "GetShootingTargetScore not found" warning on 1.6 or 1.7.
  - If wrong: the scope patch is off with one warning, and E keeps the flat cover penalty when it scores targets.
- **H2. It is not known whether vanilla `GetShootingTargetScore` calls `CoverUtility.CalculateOverallBlockChance`.** The patch is correct either way: if vanilla never calls it, nothing changes. CE's non-snap scorer does call it, through the only public overload, converting IntVec3 to LocalTargetInfo.
  - Check: 3.5.
  - If wrong: nothing breaks.
- **H3. Vanilla target finders use `AttackTargetFinder.CanSee` for their line-of-sight filters,** both for turrets and for pawns looking for targets.
  - Check: 3.1 and 3.2.
  - If wrong: target acquisition ignores the 3D sight. E may not acquire targets over its walls, though it can still hit them once targeted; enemies may not acquire E over walls.
- **H4. When the seer or the target is elevated, the 3D test replaces vanilla CanSee's result outright,** turning true into false or false into true. Any other condition inside vanilla CanSee besides line of sight is not kept. Leaning sources and lean-shootable cells are ignored, per the spec.
  - Check: 3.1 to 3.3.
  - If wrong: if vanilla CanSee checks more than line of sight (range, fog), E lines skip that check.
- **H5. Endpoints of the sight line:** besides the seer's and target's occupied rects, the cells of the line's two end points count as endpoints, as CE does with the shot source and target cells. For a pawn manning E, both the pawn's rect and E's rect are excluded from the validator and obstacle checks. The roof check still runs on every cell.
  - If wrong: nothing expected.
- **H6. "Any thing in their rects" is read as: the thing's Position lies inside the seer's, effective seer's or target's rect.**
  - If wrong: nothing expected.
- **H7. Unity's Ray constructor normalises the direction,** so the IntersectRay distance is in world units and is compared with the 3D segment length. A segment shorter than 1e-4 skips the obstacle test; the roof and validator checks still apply.
  - If wrong: obstacles beyond the target might block sight.
- **H8. Overlay colours follow CE's partial-LoS debug convention, one flash per cell.**
  - Sight: 0 means blocked by roof, gas or obstacle; 0.7 means an obstacle was tested and passed; 0.4 means path.
  - Line of fire: 0 means blocked and 0.7 everything else.
  - The text is the lowest line height in the cell.
  - Check: 3.6.

### Force attack, firing zone and platforms (DESIGN 1.2, 3.7, 3.8)
- **J1. The Attack button's targeter orders a turret's attack through `Building_Turret.OrderAttack`, for unmanned turrets the same way as for manned ones.** CE's manned turrets already rely on this path.
  - Check: 4.1.
  - If wrong: the button shows, but choosing a target does nothing on an unmanned E.
- **J2. `ThingWithComps.DrawExtraSelectionOverlays` calls every comp's `PostDrawExtraSelectionOverlays`.** CE's own CompFireArc relies on this.
  - Check: 4.2.
  - If wrong: no outline.
- **J3. `GenDraw.DrawFieldEdges` with a few thousand cells every frame is cheap enough.** CE uses it for its fire arcs. `RealTime.frameCount` or `RealTime.LastRealTime` changes every frame, also while paused; the overlay uses them to share its per-frame budget. CE's private `CompFireArc.functionEnabled` is found by reflection.
  - Check: 4.2, performance.
  - If wrong: frame drops while E is selected with its zone on; switch the button off.
- **J4. Vanilla's "ends with a numerical digit" rule applies to every ThingDef.** Generated platform names end in "Cells", and an elevated def gets "_Elevated" if its base's name ends in a digit.
  - Check: 1.1.
  - If wrong: nothing; the names are only longer.

- **J5. A ray that exactly grazes the top of a wall's CE bounds counts as a hit in Unity's `Bounds.IntersectRay`.** The overlay blocks on that tie.
  - Check: 4.2, matches the line check, along shadow edges.
  - If wrong: a few cells along shadow edges are left out of the outline although E can hit them.
- **J6. Vanilla's `Building_Turret` keeps its forced target in a field named `forcedTarget`.**
  - Check: no "forcedTarget not found" warning; 4.1, forgotten on despawn.
  - If wrong: forced targets survive mounting, unmounting and moves; one warning.
- **J7. Platforms in the Architect menu.** Without Material Sub-Menu: vanilla orders buttons by `uiOrder`, and each platform's button opens the material menu. With it: vanilla builds one dropdown button per `DesignatorDropdownGroupDef` (added during `GenerateImpliedDefs_PreResolve`), shows the member that comes first by `uiOrder`, and Material Sub-Menu offers the materials inside it (as Replace Stuff relies on). `ModLister.AnyModActiveNoSuffix` finds that mod by package id.
  - In game (2026-10-23), the dropdown without Material Sub-Menu lost the material choice; that is why the buttons are separate now.
  - Check: 1.1, Architect menu, with and without the mod.
  - If wrong: platforms come in the wrong order, or with the mod the material choice is missing in the dropdown.
- **J8. `graphicData.drawRotated = false` draws a Graphic_Single the same way up for every rotation, for buildings, blueprints and frames.**
  - Check: 1.1, art.
  - If wrong: the front face turns with the platform (cosmetic).
- **J9. Vanilla's gizmo grid toggles every selected thing's `Command_Toggle` when several are selected.**
  - Check: 4.2, button.
  - If wrong: only one of the selected E switches.
- **J10. A comp's `props` can be switched to another CompProperties object of the same class at runtime, and CE's `CompFireArc` reads its limits from `Props` each time (it does in the reference source).**
  - Check: 4.3.
  - If wrong: the arc keeps its ±70° limit on a platform, or keeps the full circle on the ground.

### Other vanilla behaviour
- **I1. A fillPercent of 1 gives Full fillage,** so vanilla treats E and platforms as walls for flat line of sight and cover between third parties.
  - Check: a pawn next to E gets wall cover, and two pawns can't see each other through E.
  - If wrong: E behaves like partial cover for others.
- **I2. Map generators, quests, faction bases and traders never spawn an elevated def directly.** Elevated defs have no trade, recipe or thing-set tags, but keep the base's buildingTags.
  - Check: visit several enemy bases and quest sites, and look for "elevated ..." turrets.
  - If wrong: an E without a record exists. It fights as elevated, but uninstall, deconstruct and destruction give no platform, and uninstalling leaves it elevated.
