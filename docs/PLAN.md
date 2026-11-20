# Dragon Ball mod for Terraria: design and plan

Status: **phase 1 (ki core) passed the visual gate at 7/10** (2026-11-14; ki bar 8
and icons 8 in round 3; flight pose 7 in round 4; the live procedural aura 7 in
round 11). Milestone 1.2, the user's hand play-test, is in progress (checklist
handed over 2026-11-14, results pending). **Phase 2 (skills) is built and renders in
headless shots but has not passed the visual gate** (the judge could not be run on
2026-11-14, see Screenshots) and has not been played by hand. **Phase 3
(transformations) is built** (2026-11-14) and renders in seven `form_*` scenes; the
user waived the visual gate for it ("Forget the visual gate do phase 3"), so it was
self-checked from crops only. The 3.5 slice play-test is pending. The overhaul (below) reworked phases 2 and 3 on top
of that. Nothing is committed.

### Persistent hand-test saves

Installed in the normal local tModLoader save folders (not the screenshot sandbox):
- Character **Zenkai Tester**, `Players/ZenkaiTester.plr` plus `.tplr`: Classic/softcore,
  500 health, 200 mana, 5,000 maximum/current starting ki; all 15 skills learned,
  all 11 forms unlocked at 100% mastery, eight populated skill slots and Rush on RMB.
  Includes basic tools, a Magic Mirror, healing potions, building supplies and spare dummies.
- World **Zenkai Training Ground**, `Worlds/ZenkaiTraining.wld` plus `.twld`: small,
  Classic, corruption, seed `zenkai-training`. Spawn has a 201-tile-wide brick-topped
  floor, 100 tiles of cleared headroom and three persistent target dummies.
- Normal Save & Exit/autosave preserves later changes. No stat refills, scene automation
  or forced inputs run during normal play. The regular controls/profile are untouched;
  bind Charge Ki (C), Flight (F), Ki Stance (V), Skills (K), Transform (T) in Controls
  if still unbound. Hold Transform for the wheel; stance hands mouse/number keys to skills.
- `tools/test-save.sh` builds, creates fresh saves in a unique ignored `.playtest/run-*`
  sandbox, launches a second real client to reload/verify progression, inventory, arena
  and tile entities, then installs all four files with exclusive creation and SHA-256
  copy verification. Refuses existing destination files and a running tModLoader.
  A portable/reset backup lives at `.playtest/run-*/ZenkaiTestSaves.zip`; it is never
  automatically reapplied to the installed saves. `TML_SAVE_DIR` can override the
  destination for future installations. The generator is inert without `ZENKAI_TEST_SAVE`.
- Verified: build passes (known packer warning only), native save/reload checks pass,
  and a repeated install refuses to overwrite the existing character. On this tML
  version permanent health/mana require `ConsumedLifeCrystals/ConsumedLifeFruit/ConsumedManaCrystals`;
  setting only `statLifeMax/statManaMax` is lost on reload.

### Aura edges and flight animation

- Fixed the random flat aura edges in `Common/Visuals/AuraRenderer.cs`: the old
  64x64-art-pixel canvas was smaller than the tongues, especially when stretched
  behind a flying player. The canvas now grows in 16-pixel chunks to bound the
  envelope and every tongue's length, width and curl, with transparent padding.
  It retains its largest dimensions to avoid resize churn and keeps the body centred.
- `Common/Players/FlightPlayer.cs` now animates relaxed composite arms, leg sway,
  a gentle body bank and draw-only bob, with bent-knee hovering versus a stretched
  glide. Speed blends the cycle from 72 to 40 ticks. Items, skills and ki charging
  retain control of their arms; ending flight/death resets the rotation state.
  `KiAuraLayer.cs` follows the same draw position, so bobbing does not detach it.
- Added successive hover/glide phases, up/down/landing and SSJ3 flight shots to
  `Dev/ShotRunner.cs`. The full run completes **61 scenes**. No sprite PNGs
  were regenerated; motion uses the player's existing equipment.
- Verification: `tools/build.sh` passes with only the known image-packer warning;
  `dotnet run --project tests/Visuals.Tests -c Release` passes **3,600 frames**
  across 12 palettes, three seeds and charge/hover/flight/overspeed/forced peaks.
  The same regression fails against the pre-fix renderer on the first Ki charge
  frame (an opaque pixel on the top texture border). The tests use graphics stubs;
  the 61 real-game captures independently cover integration and visible poses.
- Independent still-frame review: **aura 8/10, flight 7/10**, both above the 7/10
  gate; no squared cutoff or body/aura detachment observed. Remaining polish:
  flight limb articulation is deliberately subtle, not a bespoke sprite sheet.
  Real-time pacing/control feel still needs a hand play-test: hover, accelerate,
  reverse, ascend/descend, use an item/skill, then turn flight off and land.
  Preview loops: `.judge/hover-preview.gif` and `.judge/glide-preview.gif`
  (sampled phases, not continuous recordings).

### Launch-crash investigation

The reported launch crash was not reproduced with the current working tree.
`tools/build.sh` passed (only the known packer image warning), and `tools/shots.sh`
loaded Zenkai, entered the test world and completed all 49 scenes (`.shots/out/done.txt`).
The captured normal-client log also reached a world with Zenkai, Cheat Sheet and
FlyRarria enabled; it contains no managed crash stack. The fresh headless log has
a caught TLS/network read exception and Steam networking assertions at shutdown,
not a Zenkai launch exception. A failing-launch log or exact reproduction steps
are still needed before changing gameplay code. No crash fix is claimed.

## Overhaul (branch `overhaul`, built 2026-11-14, uncommitted)

Phases 2 and 3 were reworked and widened after the baseline commit. It builds, and all 46
`tools/shots.sh` scenes render with no exceptions. The overhaul has **not** been through the
visual gate (judge not run) and has not been played by hand. Self-check on 2026-11-14 was from
contact sheets and crops only.

- **Skills (15):** Ki Blast, Barrage, Homing Blast, Energy Disc (`Content/Skills/KiBlast.cs`);
  Charged, Piercing and Final Beam (`ChargedBeam.cs`, on the `Common/Skills/BeamSkill.cs` base);
  Rush, Heavy Strike (`Rush.cs`); Vanish, Instant Movement (`Vanish.cs`); Explosive Wave,
  Solar Flash, Ki Barrier, Spirit Bomb (`AreaSkills.cs`, projectiles in `AreaProjectiles.cs`).
  Each has `Kind`, `Tier` and a learn condition (boss progression). Skill text is in the hjson.
- **Skill bar:** 8 numbered slots plus a right-mouse slot; skills are used in the ki stance.
- **Forms (11, `Content/Forms/`):** Kaioken, x3, x10, x20 (life drain); Super Saiyan 1 to 3,
  God, Blue, Potential Unleashed, Ultra Instinct (tiers 4 to 11). Hold the Transform key for the
  form wheel (`Common/UI/FormWheelSystem.cs`). The form classes were split out of the old
  `Kaioken.cs` and `SuperSaiyan.cs`. A buffs pass (2026-11-14) gave every form more than stats:
  see Transformations below.
- **Stacking forms:** a form with `Stacks = true` (the Kaioken line) layers on top of any base form
  instead of replacing it. `FormPlayer.Overlay` holds it, `Active` stays the base form. Bonuses,
  ki drain, life drain and dodge add up; mastery grows for both; hair and eyes come from the base
  form and the aura from the stacked one. On the wheel, pick a Kaioken rank while in a base form to
  add it, pick the same rank again to take it off, pick a base form to swap it and keep Kaioken,
  and the centre powers down everything. Low life drops only the Kaioken. Scene `form_ssj_kaioken`.
- **Hair:** forms now recolour the player's own hair (`ModForm.HairColor/HairBlend/HairLift`,
  `FormHairLayer.cs`). The `SuperSaiyanHair` sprite is gone; do not regenerate it.
- **Flight** moved to `Common/Players/FlightPlayer.cs`. Chat messages go through
  `Common/Systems/ZenkaiChat.cs`.
- **Art:** `art/skills2.lua` makes the icons for the new skills and forms and the sprites for
  the new projectiles (piercing and final beam pieces, energy disc, ki wave, spirit bomb, ki
  barrier). Every `ModSkill` needs `Assets/Skills/<Name>.png`, every `ModForm`
  `Assets/Forms/<Name>.png`, and a `BeamSkill` four pieces named `<AssetPrefix>Orb/Start/Body/End`.
  A missing icon throws when the UI draws it.

Gotchas found in the overhaul:
- **hjson:** a quoteless value that starts with `{` (for example `{0}% chance`) parses as an
  object and the whole mod fails to load (`Found '}' where a key name was expected`). Quote it.
  The headless run then hangs on the loading screen instead of exiting, so check
  `.shots/run.log` when `tools/shots.sh` times out.
- **Shot scenes:** a skill starts on a fresh press. If the previous scene left a button held,
  set `DevHeld` from the scene's tick callback (for example `t >= 3`), not in setup, or the
  skill never starts (this hid the Spirit Bomb).
- `ZENKAI_SHOT_FILTER` works with `tools/shots.sh` and is the quick way to re-render one scene.

This file is the source of truth for design decisions and phase status. Update it when
either changes. `docs/ROADMAP.md` breaks every phase into milestones with deliverables,
starting numbers, art lists and acceptance checks; build from that.

The mod is named **Zenkai** (display and internal name, namespace `Zenkai`). The user
left the name to Claude. It avoids the franchise name and refers to the power boost
after surviving near-death, which is also a planned mechanic.

## What this is

A tModLoader mod that adds Dragon Ball combat and progression to Terraria. An older
community mod covers the same theme; this is an independent take and shares no code or
assets with it. The difference we are aiming for is game feel: ki attacks are learned
skills rather than weapon items, and transformations are earned and mastered rather
than crafted.

## Decisions (made by the user, 2026-11-14)

| Topic | Decision |
|---|---|
| Ki attacks | Learned skills slotted to a skill hotbar, hold to charge. Not weapon items. |
| Races | Later phase. Build the transformation system so races can be added, ship one shared progression first. |
| Art | Original pixel art drawn by Claude through Aseprite scripts, in Terraria's style, passed through the visual quality gate (7/10 or higher). |
| Multiplayer | Singleplayer first. Sync is added later. |

Cheap habits to keep now so the later multiplayer pass is not a rewrite: all per-player
state lives in one `ModPlayer`, projectiles act only for their owner
(`Projectile.owner == Main.myPlayer`), and no gameplay state is stored in UI classes.

## Environment

- tModLoader (Steam, targets net8): `~/.local/share/Steam/steamapps/common/tModLoader`
- Mod sources folder: `~/.local/share/Terraria/tModLoader/ModSources`
- tModLoader build: 1.4.4.9, `TML_2026_09`.
- This repo: `/home/bob2142/Projects/modding/dbzmod`. The mod itself is the `Zenkai/`
  subfolder, so docs and tools stay out of the packed `.tmod`. The folder name must
  equal the internal name.
- `ModSources/Zenkai` is a symlink to `Zenkai/`, which makes the mod appear in the
  in-game Mod Sources menu. `tools/build.sh` recreates the link if it is missing.
- Aseprite is installed at `/usr/bin/aseprite`. `*.aseprite` files are excluded from
  the packed mod by `buildIgnore` in `Zenkai/build.txt`.

### Build

`tools/build.sh` compiles the mod and packs `Zenkai.tmod` into
`~/.local/share/Terraria/tModLoader/Mods`. Extra arguments go to `dotnet build`.
Verified working on 2026-11-14. The first build also enabled the mod in
`Mods/enabled.json`.

Gotchas:
- `dotnet` resolves the `ModSources/Zenkai` symlink to the repo path, so the usual
  `..\tModLoader.targets` import in the csproj lands on the repo root, not on
  `ModSources`. The repo-root `tModLoader.targets` forwards to the real file
  (override the location with the `TML_MODSOURCES` environment variable).
- Only the .NET 10 SDK and runtime are installed system-wide; tModLoader targets net8.
  Compiling for net8 with the 10 SDK works. The pack step runs `tModLoader.dll` with
  the system `dotnet`, so the script sets `DOTNET_ROLL_FORWARD=Major`. Whether the
  pack step would also pass without that variable was not tested.
- `icon.png` (80x80) and `icon_small.png` (30x30) sit in `Zenkai/`, drawn by
  `art/icon.lua`. With them present the pack step prints `warning : Image loading failed:
  unknown image type` twice. It is the packer, not mod code: removing the two icons
  silences it, re-encoding them with ImageMagick does not, both are packed as PNG in
  `Zenkai.tmod`, and the game loads them without the old `icon_small.png` warning.
  Cause not found (2026-11-14); treat these two warnings as expected.

### Art

Sprites are generated by Lua scripts in `art/`, run through Aseprite in batch mode by
`tools/art.sh`. Each script writes an editable `.aseprite` file to `art/` and a PNG into
`Zenkai/Assets/`. Art is authored at 1x and exported at 2x nearest-neighbour, which is
how Terraria's own sprites are made, so one art pixel is 2x2 screen pixels. Change the
Lua, not the PNG.

### Screenshots for the visual gate

`tools/shots.sh` builds the mod, starts tModLoader headless and writes one 1280x720 PNG
per scene to `.shots/out/`. A run takes about 40 seconds (the first also generates a
small world, about 20 seconds more). Verified working on 2026-11-14.

- Scenes are listed in `Zenkai/Dev/ShotRunner.cs`. It only activates when the
  `ZENKAI_SHOTS` environment variable is set, so it is inert in normal play. It creates
  a test character and world, poses the player through `KiPlayer`'s `Dev*` fields,
  captures the back buffer after each scene and exits the process.
- It uses its own save folder, `.shots/save` (`-tmlsavedirectory`), so real characters,
  worlds and the enabled-mods list are not touched. Delete `.shots/save/Worlds` to
  regenerate the test world. tModLoader still writes its logs to
  `tModLoader-Logs/` in the Steam install, which is where to look when a run fails
  (`client.log`); `.shots/run.log` has the console output.
- It runs with `-nosteam` under `xvfb-run` with audio off. Steam does not need to be
  running. Rendering uses the real GPU through the virtual display.
- To see what a stuck run is showing, grab the virtual display:
  `XAUTHORITY=<path after -auth in the Xvfb command line> magick import -display :99 -window root out.png`.

Gotchas found while building it:
- A fresh save folder makes tModLoader show a language picker and then a welcome dialog,
  both before it loads any mods, so mod code cannot dismiss them. `shots.sh` writes a
  `config.json` with `Language` and `LastLaunchedTModLoaderVersion` (read from the
  version line in `client.log`) to skip them.
- On a fresh config Terraria replaces `UIScale` with its own default (0.73 at 1280x720)
  unless `"ResetDefaultUIScale": false` is also set. `shots.sh` writes both, and
  `ShotRunner` sets `Main.UIScale = 1f` every tick, so the HUD is judged at 100%.
- Dust draws over the player. Effects that spawn dust on the body hide the face.
- Vanilla bursts sparkle dust (type 45) over the player when mana refills, which looked
  like an aura bug in a shot. `ShotRunner` keeps mana full.
- Do not stop a run with `pkill -f 'tModLoader.dll ...'` from a shell whose own command
  line contains that text; it kills the shell. Use `pkill -f '[t]ModLoader.dll -nosteam'`.

The judge named in the user's global instructions (T3 orchestration, Gemini Hydrogen)
was not available as a tool in the 2026-11-14 sessions. The user chose a Haiku agent
(Agent tool, `model: haiku`) as the judge for this project. Shots and crops for the
judge go in `.judge/` (git-ignored). Reference images of passed screens are in
`docs/images/`; the vanilla HUD and sprites in the same screenshot are the style
reference too. Scenes now: `ki_bar_full`, `ki_bar_partial`, `ki_charge`, `ki_charge_b` and
`ki_charge_c` (6 ticks apart, to show the live aura moving), `ki_flight`,
`ki_flight_level`, `ki_flight_hover`; phase 2: `skill_hotbar_empty`, `skill_hotbar_full`,
`skill_menu`, `blast_fire`, `blast_impact`, `beam_charge_half`, `beam_charge_full`,
`beam_fire`, `beam_wall`, `rush_hit`, `rush_launcher`, `vanish_blink`. A full run takes
about a minute.

- `ZENKAI_SHOT_FILTER=beam,rush tools/shots.sh` captures only scenes whose names start
  with those prefixes. Skipped scenes still run their setup (later scenes depend on the
  state), and `shots.sh` deletes all old PNGs first, so a filtered run leaves only the
  filtered shots.
- Scenes can have a per-tick action (`Tick`) to press and release skill keys through
  `SkillPlayer.DevSlotHeld`; aim comes from `SkillPlayer.DevAimWorld`.
- The skill scenes place a vanilla Target Dummy (`SkillCommand.PlaceDummy`, which clears
  plants first: placement fails on a sunflower) and the runner keeps it alive. It also
  deletes dropped items every tick, because their hover labels cluttered shots.
- The runner binds the default keys in its own input profile so the hotbar shows key
  labels (see the keybind gotcha under Skills).

Judge on 2026-11-14 (phase 2): the Agent call to the Haiku judge was refused twice by
auto mode ("a hook changed this call's input", the context-mode plugin's hook rewrites
Agent prompts). Round-1 shots and 2x crops are ready in `.judge/r1/`. To run the gate,
either approve the Agent call manually, disable that hook, or leave auto mode.

## Core systems

### Ki
- A third resource next to life and mana, with its own HUD bar left of the life hearts.
- Regenerates slowly on its own. Holding the charge key regenerates it fast, with an
  aura, while the player is rooted and takes extra damage.
- Max ki grows through progression items and training, not armour alone.
- Ki damage is its own `DamageClass`, so armour and accessories can scale it (not built yet).

As built in phase 1 (`Zenkai/Common/Players/KiPlayer.cs`, tuning constants at the top):
- 100 max ki to start. Natural regen 1.5 per second, paused for 1.5 seconds after
  spending. Charging (default key C) restores 25% of max per second, blocks walking,
  jumping and item use, and raises damage taken by 25%.
- Flight (default key F, toggle) drains 5 ki per second moving and 2 hovering, and ends
  at 0 ki, on death, when mounted or when grappling. Eight-direction movement at up to
  7 px per tick, with jump also acting as up. Charging while flying hovers in place
  without draining.
- `TrySpendKi(amount)` is the entry point skills will use.
- Max ki and current ki are saved with the character.
- Aura look (user direction, 2026-11-14): the aura must **envelop the whole player**
  with nothing poking out, also when flying, and must be **dynamic, not a fixed sprite
  animation**, so forms can restyle it. It is generated live in code:
  `Common/Visuals/AuraRenderer.cs` computes it every game tick as 1x pixel art
  (64x64 art px, body space, centred on the player), expands it to 2x on the CPU so it
  stays crisp, and uploads it to a per-player texture that `KiAuraLayer` draws. Shape:
  a capsule envelope about 3 art px clear of the hair and shoulders (the sprite is about
  18 art px wide at the hair, wider than its hitbox) with ripples running along its
  edge, plus flame tongues that each spawn at a random angle round the top and upper
  sides, grow, curl and die on their own, so it never loops. Flat bands (navy outline,
  rim, hot, mid, inner), despeckled. Intensity per state (charge 1, flying 0.85, hover
  0.6) scales tongue length, ripples and speed; tongues stretch with flight speed.
- `Common/Visuals/AuraStyle.cs` holds everything about the look: palette, band widths,
  envelope size, tongue count, length, width, life, spread, curl, speed stretch and
  flicker. `AuraStyle.Ki` is the blue base. `KiPlayer.AuraStyle` picks the style;
  forms (phase 3) set their own instead of drawing sheets.
- Drawn with plain alpha blending (textures hold premultiplied colour, so the renderer
  premultiplies) so the navy band darkens the background; an early mostly additive
  blend turned it bright blue. Dropped along the way: a comet-tail flight wisp (did not
  envelop), an aura sized to the hitbox (hair clipped through), and a pre-drawn 4-frame
  sheet (replaced by the live renderer at the user's request).
- Flight pose (user direction): the body tilts head first toward the direction of
  travel (`Player.fullRotation`), about 81 degrees in level flight, upright when rising
  straight up, half the forward lean when descending feet first, upright when hovering
  or charging. Legs use the standing frame. In head-first flight the renderer points
  the tongues toward the feet. The standing-upright and jump poses were both
  rejected by the user as not reading as flight.
- Gotcha: vanilla's `DrawPlayer_TransformDrawData` rotates every `DrawData` in a player
  draw layer by `fullRotation` around the player's centre. A layer must not add the
  rotation itself (doing so doubled the aura's tilt).
- Charge sparks are a custom `KiSpark` dust (2x2 art px, three frames, no fade) that
  spawns only beside the flame, since dust draws over the player.
- Flight keeps its own velocity vector. Vanilla adds gravity before `PreUpdateMovement`,
  so blending toward a target from `Player.velocity` made the player sink.

### Skills
- A skill is data plus behaviour: ki cost, charge time, cooldown, unlock condition,
  and the projectile or melee routine it runs.
- The player slots learned skills into a small hotbar (four to six slots) with their
  own keybinds, separate from the item hotbar.
- Skill kinds: blast (tap), beam (hold to charge, hold to sustain, drains ki),
  barrage, melee rush, movement (vanish dodge, teleport behind target), utility
  (ki sense, flight).
- Skills are learned from trainers (town NPCs), scrolls dropped by bosses, and use
  milestones.
- Beam struggle: when a player beam meets a boss beam, both lock and the player
  pushes by spending ki. Scheduled after the vertical slice.

As built in phase 2 (2026-11-14):
- `Content/DamageClasses/KiDamageClass`: inherits only generic bonuses. Gear modifies it
  with `Player.GetDamage<KiDamageClass>()` etc.; cost reduction is
  `SkillPlayer.KiCostMultiplier` (reset every tick).
- `Common/Skills/ModSkill`: a `ModType` registered through `ModTypeLookup<ModSkill>`,
  listed by `SkillLoader`. A skill is one class (fields set in `SetStaticDefaults`) plus
  `Assets/Skills/<Name>.png` (16x16 art) plus text under `Mods.Zenkai.Skills.<Name>`.
  Hooks: `CanUse`, `OnStart`, `OnChargeTick`, `OnRelease`, `OnSustainTick`, `OnEnd`,
  `PoseArms`. `Spawn` makes owner-only projectiles with ki damage, knockback and crit.
- `Common/Players/SkillPlayer` (the second per-player class; `KiPlayer` keeps ki):
  learned skills saved by full name (unknown names are kept, not dropped), four-slot
  loadout, cooldowns, and the state machine Idle, Charging, Sustaining, Recovery. One
  skill at a time. Ki charging, being stunned, mounting, grappling and death cancel it; a
  hit cancels a charge. Charged skills pay on release, tap skills on press. Skills block
  item use while active. Skills move the player only through
  `KiPlayer.ForcedVelocity` (one tick, works on the ground and in flight), so the order
  in which the two ModPlayers run does not matter.
- Starting skills: Ki Blast and Rush are known from the start and slotted for new
  characters (and old characters with no skill data).
- Skills: Ki Blast (5 ki, 12 dmg, 12-tick cooldown, auto-repeat), Charged Beam (20 ki on
  release + 15 ki/s, 14 dmg per hit at 6 hits/s, x1 to x2 by charge, 0.5 to 2.5 s charge,
  fires 1 to 2.5 s by charge, press again to stop, rooted, turns 2 degrees a tick), Rush
  (jab, jab, launcher; tap again within 0.75 s of the launcher to dash after the target
  for 10 ki; contact damage ignored during lunges; hit pause and shake on hits), Vanish
  (15 ki, 11 tiles in the held direction, stops at walls, 0.5 s invulnerable, 1 s
  cooldown, blue after-image). Numbers are the roadmap's starting points.
- Deviation: the roadmap's beam "hold to sustain" became "fires for a time set by the
  charge, press again to stop", because release is what fires it.
- UI: hotbar at the bottom centre (`SkillHotbarSystem`), skill menu on K
  (`SkillMenuSystem`, click a skill then a slot, right-click a slot to clear), shared
  slot drawing in `SkillSlotDrawer`. Art in `art/skills.lua`.
- Dev command `/zskill list | grant | revoke | slot | ki | dummy` (`Dev/SkillCommand.cs`).
- **Keybind gotcha**: in tModLoader 1.4.4 a mod keybind starts unbound. The default key
  passed to `RegisterKeybind` is only applied when the player resets the bind in
  Settings, Controls; `GetAssignedKeys` returns an empty list until then. This also
  affects phase 1's C and F. `KeybindReminderPlayer` prints a chat line once per session
  when any Zenkai control is unbound. Default keys (user, 2026-11-14): C charge, F
  flight, Z X V G skill slots, K skill menu.
- The vanish after-image redraws the player through `Main.PlayerRenderer.DrawPlayer`
  with the pose saved at the blink, tinted by `AfterimageTintPlayer.ModifyDrawInfo`.
  Vanilla's `shadow` argument alone drew a grey statue.
- Ki blasts and melee strikes do not cut plants (`CanCutTiles` false): cut sunflowers
  dropped items everywhere.

### Melee
- Bare-handed combat as a real option: light combo, launcher, chase after a launched
  enemy. Scales with the ki damage class.

### Flight
- Toggled flight that drains ki, cheaper with mastery. Nimbus is the early mount
  version that costs no ki.

### Transformations
- A transformation is a data definition: stat multipliers, ki drain per second,
  aura and hair visuals, unlock trigger, mastery curve.
- Unlock by trigger, not by crafting. Examples: Kaioken from a trainer, Super Saiyan
  from near-death or a town NPC dying during a boss fight.
- Mastery rises with time spent transformed in combat and lowers drain.
- Definitions carry a race requirement field from the start, unused until races ship.

As built in the form buffs pass (2026-11-14):
- Forms carry more than damage, speed and defence: `ModForm` fields for ki crit, endurance,
  flat max ki, ki regen and charge rate multipliers, skill cost multiplier and life regen,
  applied in `FormPlayer.PostUpdateEquips` (life regen in `UpdateLifeRegen`, which the game
  calls after the drain hook, so a stacked Kaioken nets against Potential Unleashed's regen).
- Signature perks: the Kaioken ranks **surge** (`SurgeBonus`: ki damage multiplied by up to
  `1 + SurgeBonus` as life falls) and charge ki 1.5x faster; Super Saiyan 2 arcs **lightning** onto the
  nearest foe within 320 px every 1.5 s while in combat (30 base ki damage, `UpdateLightning`);
  Ultra Instinct **counters** a slipped hit with a ki bolt at the attacker (1.5x Ki Blast
  damage, no ki cost, `CounterAttack`). The transformation burst now also deals 20 base ki
  damage; bosses take the damage but not the knockback.
- Form damage is **multiplicative** (`KiDamageMultiplier`; user decision 2026-11-14: the boosts
  should feel like Dragon Ball, not +35%). It multiplies gear's ki damage too, and stacked forms
  multiply together. Ladder: Kaioken x2, x3 x3, x10 x6, x20 x10; SSJ x5, SSJ2 x10, SSJ3 x20;
  Potential x12; God x30; Blue x50; Ultra Instinct x40. Blue + x20 Kaioken at 10% life is about
  x860. Other buffs: SSJ1 +10% ki crit; SSJ2 +15% crit; SSJ3 +20% crit and 10% endurance; God 2x
  ki regen and +50 max ki; Blue 0.7x skill cost and 12% endurance; Potential +100 max ki and
  +2 life/s; Kaioken surge 30/45/60/80% by rank.
- The skill menu details panel lists every buff and perk (hjson keys `FormCrit` to
  `FormCounter`). Scenes `form_ssj2_lightning` and `form_instinct_counter` cover the new
  visuals; the runner poses them with `FormPlayer.DevInCombat` and `DevForceDodge`.

### Training
- Weighted clothing accessories: slower now, permanent small gains over time worn.
- Gravity chamber furniture: a room buff that speeds mastery and max-ki growth.
- Zenkai: a small permanent boost after surviving near-death, with a cooldown.

### Dragon Balls
- Seven balls placed at world generation, one set per world.
- Dragon Radar item points to the nearest.
- Placing all seven summons Shenron and opens a wish menu (one wish, then the balls
  scatter and go inert for a number of in-game days).

## Content by Terraria tier

| Terraria stage | Era | Content |
|---|---|---|
| Pre-boss | Early Dragon Ball | Ki blast, flight, Nimbus, Power Pole, Senzu Bean, first trainer |
| Pre-Hardmode | Saiyan saga | Saibamen event, Great Ape on full moons, Kaioken, scouter |
| Early Hardmode | Namek and Frieza | Frieza Force invasion, Frieza boss, Super Saiyan |
| Late Hardmode | Androids and Cell | Cell boss with phases, Super Saiyan 2 |
| Post-Moon Lord | Buu and Super | Super Saiyan 3, God forms, a Beerus-tier superboss |

## Phases

| # | Phase | Contents | Status |
|---|---|---|---|
| 0 | Scaffold | Mod skeleton, symlink, build script, git | done 2026-11-14 (nothing committed yet) |
| 0b | Shot tooling | `tools/shots.sh` and `Zenkai/Dev/ShotRunner.cs`, see Screenshots above | done 2026-11-14 |
| 1 | Ki core | Ki resource, HUD bar, charge key and aura, flight, save and load | visual gate passed 7/10 on 2026-11-14 (ROADMAP 1.1); not yet played by hand (1.2) |
| 2 | Skills | Skill framework, skill hotbar UI, ki blast, Kamehameha-style beam, melee rush, vanish dodge | built 2026-11-14 (2.1 to 2.6 code and art, 2.7 shake, hit pause and sounds); visual gate not run; hand play-test (2.7) pending |
| 3 | Transformations | Framework, Kaioken, Super Saiyan with aura and hair, mastery | built 2026-11-14 (3.1 to 3.4; keys T and Y; Super Saiyan unlocks by near death only); visual gate waived by the user; 3.5 play-test pending |
| 4 | Dragon Balls | World generation, radar, Shenron, wish menu | not started |
| 5 | First tier content | Trainer NPC, Saibamen event, Great Ape, early items | not started |
| 6 | Hardmode content | Frieza invasion and boss, Cell, further forms | not started |
| 7 | Races | Race choice, race perks and form trees | not started |
| 8 | Multiplayer | Sync for ki, skills, forms, beams, Dragon Balls | not started |
| 9 | Endgame | Post-Moon Lord forms and superboss | not started |

`docs/ROADMAP.md` adds a phase 10 (release readiness) and the milestones inside each
phase.

Phases 1 to 3 are the vertical slice. If charging, firing a beam and transforming do
not feel good at the end of phase 3, fix that before adding content.

## Open questions

Working defaults, which the user was offered and did not object to (2026-11-14):
four skill slots to start, skills sit alongside vanilla weapons rather than replacing
them, balance against vanilla only.

- Default keybinds for transform and revert (charge C, flight F, skills Z X V G, menu K).
- Release plan: Steam Workshop or private. A public release of a fan mod uses
  Dragon Ball names and likenesses, which is the user's call to make.
