# Zenkai roadmap

Written 2026-11-14 as the long-range build plan for whichever model continues this mod.
It assumes no memory of earlier sessions.

- `docs/PLAN.md` holds the design decisions, the environment and tooling notes, the
  gotchas and the one-line status of each phase. Read it first.
- This file breaks every phase into milestones with deliverables, starting numbers,
  art lists, screenshot scenes and acceptance checks.
- When the two disagree, `PLAN.md` wins on decisions the user made, and this file wins
  on how to build. Fix the disagreement in the same session.

Numbers here (costs, multipliers, damage) are starting points for tuning, not decisions.
Anything marked **User decision** needs the user's answer before it is built.
tModLoader type and member names in this file were written from memory unless a line
says they were verified. Check each one before relying on it (see Working rules).

## 1. Where the project stands

Done and verified by running:
- Scaffold, build script, headless screenshot tooling (`tools/build.sh`,
  `tools/art.sh`, `tools/shots.sh`).
- Phase 1 code: ki pool, charge, flight, HUD bar, aura, sparks, mod icons, save and
  load. It builds, loads in tModLoader and renders in the eight screenshot scenes
  listed in `PLAN.md`.
- Phase 1 **passed the visual gate** on 2026-11-14 (milestone 1.1; the live aura
  passed in round 11 at 7/10).
- Milestone 1.2 (hand play-test) started 2026-11-14: the checklist was handed to the
  user; results go in 1.2 below.
- Phase 2 milestones 2.1 to 2.6 and the code part of 2.7 were built on 2026-11-14 at the
  user's request ("finish phase 2"). They build, load and render in 12 new scenes. Not
  done: the visual gate (judge call blocked, see `PLAN.md`, Screenshots), any hand
  play-test, and the tuning pass against real enemies.
- Phase 3 milestones 3.1 to 3.4 were built on 2026-11-14 ("do phase 3"). They build,
  load and render in seven `form_*` scenes. The user waived the visual gate for this
  phase; it was self-checked from 4x crops (hair alignment both facings, aura colours,
  menu Forms row). As-built notes are under each 3.x milestone. Not done: the 3.5
  vertical slice play-test.

Not done:
- Nothing has been played by hand. Input feel, keybinds and save/load are untested
  outside the scripted scenes.
- Nothing is committed. The repo is initialised on `main` with all files untracked.
  Commit only when the user asks.

## 2. Working rules

### Session routine
1. Read `docs/PLAN.md`, then the milestone you are about to build here.
2. Run `tools/build.sh` before changing anything, to confirm the tree builds.
3. Build one milestone at a time. A milestone is sized to fit one session.
4. Before reporting a milestone done, run through "Definition of done" below.
5. Update `PLAN.md` (status table, as-built notes, gotchas) and tick the milestone here.

### Definition of done for a milestone
- `tools/build.sh` passes with no warnings from mod code.
- Every new visual has a scene in `Zenkai/Dev/ShotRunner.cs` and `tools/shots.sh`
  produces it.
- The visual gate in the user's global instructions is passed at 7/10 or higher, each
  judge finding checked against the pixels before acting on it, each score reported
  to the user.
- Player-facing text is in `Zenkai/Localization/en-US_Mods.Zenkai.hjson`, not in code.
- Docs updated. State plainly what was verified by running and what was not.
- If the milestone changes how the game feels to control, tell the user it needs a
  hand play-test and list what to try. Scripted scenes cannot judge feel.

### Checking the tModLoader API
The installed build is tModLoader 1.4.4.9, `TML_2026_09`. Do not trust remembered
signatures. Two checks that worked on 2026-11-14:
- Documented members: grep `tModLoader.xml` in the Steam install, for example
  `grep -o 'name="M:Terraria.ModLoader.ModPlayer.ModifyHurt[^"]*"' tModLoader.xml`.
- Vanilla members with no XML docs: `strings tModLoader.dll | grep '^playWorld$'`.
  Events show up as `add_OnPostDraw`.
The official ExampleMod is the best reference for patterns (custom damage class, boss,
town NPC, mount, invasion-style spawning, UI). It is not on this machine; fetch the
relevant file from the tModLoader GitHub repository when a pattern is new.

### Code conventions
- Layout inside `Zenkai/`: `Common/Players`, `Common/Systems`, `Common/UI`,
  `Common/GlobalNPCs`, `Content/Skills`, `Content/Forms`, `Content/Projectiles`,
  `Content/Items/<kind>`, `Content/NPCs/<kind>`, `Content/Tiles`, `Content/Buffs`,
  `Assets/<kind>`, `Dev/`. Namespace follows the folder.
- Per-player state lives only in `ModPlayer` classes, one per system (`KiPlayer`,
  `SkillPlayer`, `FormPlayer`, `TrainingPlayer`). `PLAN.md` says "one `ModPlayer`";
  splitting by system keeps the same intent, which is that no gameplay state lives in
  UI, projectiles or statics. Update `PLAN.md` when the second one is added.
- Tuning values are named constants at the top of the class, or fields on a
  definition object. No magic numbers in the middle of logic.
- Content is data-driven: a skill or a form is a definition class the framework
  discovers, so adding one is adding one file plus art.
- Multiplayer comes in phase 8, but write for it now: projectiles act only when
  `Projectile.owner == Main.myPlayer`, spawn NPCs and items only when
  `Main.netMode != NetmodeID.MultiplayerClient`, and keep anything another client
  would need to see as plain fields that can be sent later.
- `Zenkai/Dev/` is test tooling. Gameplay classes may expose `Dev*` fields for the
  shot runner to pose the player, and nothing else may read them.

### Art conventions
- Sprites are generated by Lua in `art/`, authored at 1x and exported at 2x. Edit the
  Lua, never the PNG. One Lua file per feature area.
- Terraria's look is hard-edged: a dark outline, two to four flat shades per material,
  no soft gradients and no anti-aliasing. The first aura failed the gate for smooth
  alpha falloff. Use stepped bands on the 1x grid.
- Auras and glows use plain alpha blending with a dark outline band (see
  `AuraRenderer`). Additive or mostly additive blending washes out to white over the
  daytime sky and turns dark outlines bright.
- Effects that must vary or be restyled per form (auras) are generated in code from a
  style definition, not drawn as sheets. They still follow the rules above: 1x art
  shown at 2x, flat bands, no specks.
- Use original designs. Do not trace or copy sprites from the anime, the games or the
  older community mod.
- Reference style for the judge is vanilla Terraria in the same screenshot. Once a
  screen passes the gate, copy it to `docs/images/` so later work is judged against it.

### Screenshot scenes
- Add scenes to the list in `ShotRunner.cs`. Keep old scenes so regressions show.
- When the list grows past about ten scenes, add a `ZENKAI_SHOT_FILTER` environment
  variable so a run can render only the scenes that changed.
- Scenes that need enemies, bosses or night must set that up themselves; the runner
  forces midday and removes hostile NPCs every tick, so make that per-scene first.
- The HUD is judged at 100% UI scale (fixed in 1.1; see the gotcha in `PLAN.md`).

## 3. Design pillars

Use these to settle design questions that this file does not cover.
1. **Feel before content.** Charging, firing and transforming must be satisfying with
   placeholder numbers before any boss or item is added.
2. **Ki is a real resource.** Every strong action costs ki or leaves you open.
   Nothing is free and nothing is a plain stat stick.
3. **Earned, not crafted.** Skills are learned and forms are unlocked by doing things.
   Crafting is for gear and consumables.
4. **Sits inside Terraria.** A ki build is one more way to play, next to melee,
   ranged, magic and summoner. It uses vanilla tools, armour slots, accessories and
   bosses, and must not make them pointless.
5. **Readable.** The player can always tell their ki, their form and what a boss is
   about to do.

## 4. Balance frame

Balance against vanilla only (working default in `PLAN.md`). The target is that a ki
build's sustained damage is close to a vanilla class at the same point in progression,
with higher burst paid for by ki and charge time.

Rough vanilla weapon base damage by stage, to anchor skill damage. These are from
memory; check the wiki before final tuning.

| Stage | Typical weapon damage | Ki skill base damage target |
|---|---|---|
| Before any boss | 8 to 15 | 10 to 14 |
| After Eye, Eater/Brain, Skeletron | 15 to 30 | 18 to 30 |
| Before Wall of Flesh | 30 to 45 | 32 to 45 |
| Early Hardmode | 40 to 60 | 45 to 60 |
| After mechanical bosses | 55 to 85 | 60 to 85 |
| After Plantera and Golem | 70 to 120 | 80 to 120 |
| After Moon Lord | 150 and up | 160 and up |

Rules of thumb:
- Skill damage comes from the skill's base value times the player's ki damage
  multiplier (armour, accessories, form). Skills are tiered, so an early skill does
  not scale forever; later tiers are separate skills or ranks.
- Forms multiply. Terraria's multiplicative bonuses stack fast, so keep form damage
  bonuses modest and let availability, drain and mastery carry the fantasy.
- Max ki: 100 at start, about 300 before Hardmode, about 600 before Moon Lord, about
  1000 at the end.
- A ki player wears ki armour for damage. Vanilla accessories keep working.

## 5. Milestones

Each milestone lists what to build, starting numbers, the art it needs, the scenes to
add and how to tell it is done.

---

### Phase 1: Ki core (close-out)

#### 1.1 Pass the visual gate (done 2026-11-14)
Rounds 1 and 2 scored 6/10; round 3 passed at 7 (ki bar 8, charge aura 7, flight 7,
icons 8). The user then rejected the round-3 flight (upright standing pose did not
read as flight; the aura was clipped by the hair), so the aura was enlarged and flight
became a head-first tilt with the aura turning with the body; round 4 passed that at 7
(charge 7, flight 7, hover 7; nothing pokes out of the aura).
The user then asked for a dynamic aura instead of a fixed sprite animation, so it can
be restyled per form. The live renderer (`Common/Visuals/AuraRenderer.cs`) took seven
judge rounds (5 to 11: 6, 6, 6, 6, 6, 6.5, 7) and passed at 7 in round 11 (charge 7,
flight 7, hover 7, motion 6.5).
What changed is in `PLAN.md` (Ki, as built). Shots in `docs/images/` are the latest
passing ones.

Polish left open, not required (round 11, not yet checked against the pixels):
- Some long tongues still taper to thin blades; give roots at least 3 art px.
- Hover side tongues read as short claws more than licks.
- Thin 1 to 2 px tips at the end of trailing flight tongues.
- Motion: vary where tongues root, not only their length.
- `icon_small.png` could carry a small flame lobe.

The original findings, kept for history:
The judge's findings from 2026-11-14 follow. They were **not checked against the
pixels** before the session moved on, so verify each one first.

Ki bar (7/10):
- Sits about 5 screen px above the hearts' centreline. This is likely true. The bar
  was positioned assuming hearts centred near UI y 22; measured against the shot they
  sit lower. Measure the hearts in UI space and centre the bar on them.
- Frame reads thinner than the minimap's gold frame. Consider a 1 px dark inner line.

Charge aura (5/10):
- Edges are smooth alpha gradients, not hard pixel steps. Redraw with two or three
  flat bands and no per-pixel jitter in alpha.
- Too large: the tip is about 55 px above the head and the flame about three times the
  player's width. Target about 1.5 times player height, hugging the body.
- Sparks nearly invisible. Use a custom 2x2 bright dust, more of them, rising.

Flight aura (5/10):
- Same soft edges.
- Turns muddy teal over tree canopy. Needs a dark blue edge so it holds over any
  background.
- Leaning is done with `Player.fullRotation`, which rotates the whole sprite. If the
  rotated pixels look ragged at 2x, reduce the lean or replace it with a pose.

Also in this milestone:
- Find the cause of the 0.7 HUD scale in shots (see Screenshot scenes).
- Draw `icon.png` (80x80) and `icon_small.png` (30x30) to clear the load warning.
  Check the required sizes first.
- Consider a dedicated flight look instead of reusing the charge aura at half
  opacity: a small trailing wisp behind the direction of travel reads better.

Done when: the existing scenes pass at 7 or higher, and the passing shots are
copied to `docs/images/`. (Done.)

#### 1.2 Hand play-test checklist for the user
Give the user this list and record what they report:
- Charge key roots you, fills ki in about four seconds, lets go cleanly.
- Flight toggles, moves in eight directions, does not sink and stops at 0 ki. Flying
  down to the ground causes no fall damage; running out of ki high up and falling does.
- Ki and max ki survive save, quit and reload.
- Keybinds appear under Controls with readable names, and rebinding works. They start
  **unbound** (tModLoader behaviour, see `PLAN.md`); bind C and F first. A chat line
  says so on entering a world while any are unbound.
- The bar does not overlap vanilla HUD in any of the three resource styles (Classic,
  Fancy, Bars) or with the map modes.
- Flight with wings or rocket boots equipped: holding jump while flying should not
  trigger wing flight or burn wing time. Code-read risk: `KiPlayer` sets velocity in
  `PreUpdateMovement` but does not suppress vanilla wing logic.

Status (2026-11-14): checklist handed to the user, build verified passing. Code-read
notes before the test: the bar's position is fixed (300 px + gap left of the right
edge, y 12) and does not adapt to the resource style, so Bars is the likeliest overlap.

Results: none recorded yet.

---

### Phase 2: Skills

The centre of the mod. Budget the most iteration here.

Status (2026-11-14): 2.1 to 2.6 built; 2.7 has screen shake, hit pause and vanilla
sounds in, but no tuning and no play-test. As-built notes and deviations are in
`PLAN.md` (Skills, as built). Default keys decided: Z X V G for slots, K for the menu.
Open before calling phase 2 done:
1. Visual gate on the 12 phase 2 scenes (round-1 material in `.judge/r1/`), then copy
   the passing shots to `docs/images/`.
2. User play-test (2.7 checklist below), then a tuning pass from their notes.

#### 2.1 Ki damage class and skill framework
Build:
- `KiDamageClass : DamageClass`. It should not inherit bonuses from other classes
  except generic damage. Add the `ModPlayer` fields for ki damage, crit and cost
  reduction that armour will later modify.
- `ModSkill`, an abstract definition type the mod discovers automatically. tModLoader
  supports custom content types by subclassing `ModType` and registering in
  `Register()`; confirm the pattern against a current example before building on it.
  Fields: display name and description (localised), icon, kind, tier, ki cost,
  ki drain per second while sustained, charge time min and max, cooldown, base
  damage, knockback, unlock rule, and whether it can be used while flying or moving.
  Behaviour hooks: `CanUse`, `OnStart`, `OnChargeTick`, `OnRelease`, `OnSustainTick`,
  `OnEnd`.
- `SkillPlayer : ModPlayer`: learned skills (saved by full name string so removed
  skills do not break saves), a loadout of four slots, cooldown timers, and a small
  state machine for the active skill: Idle, Charging, Sustaining, Recovery. Only one
  skill is active at a time. Charging ki, taking a hit that staggers, and dying all
  cancel it.
- Skills spend ki through `KiPlayer.TrySpendKi`.
- A dev-only way to grant skills (a chat command through `ModCommand`) so later
  milestones can be tested before trainers exist.

Done when: a test skill with no art can be granted, slotted in code, triggered,
spends ki, goes on cooldown, and the learned list survives a reload.

#### 2.2 Skill hotbar and skill menu
Build:
- Four keybinds for the slots and one to open the skill menu.
  **User decision:** default keys. Suggestion: Z, X, V, G for slots and K for the
  menu; check they are free in vanilla's defaults.
- Hotbar HUD: four slots showing icon, key label, cooldown sweep, a dim state when ki
  is short, and a charge meter on the active slot. Suggested place: bottom centre,
  above nothing vanilla uses. Check it against the inventory, chat and map overlays.
- Skill menu as a `UIState`: grid of learned skills with name, cost, description and
  how it was learned; click a skill then a slot to assign. Unlearned skills show as
  silhouettes with a hint about how to learn them.
- Loadout is saved with the character.

Art: slot frame, selected-slot frame, cooldown overlay, skill menu panel pieces (or
reuse vanilla panel textures through `UIPanel`), one 32x32 icon per skill.

Scenes: hotbar empty, hotbar full with one slot on cooldown and one unaffordable,
skill menu open.

#### 2.3 Ki Blast
The basic tap attack. Cost 5 ki, base damage 12, short cooldown (about 12 ticks),
fast straight projectile from the leading hand toward the cursor, small burst on hit,
light. Holding the key fires repeatedly.

Art: projectile (two or three frame loop), impact burst (four frames), cast flash,
icon. Scenes: blast in flight, blast impact on a target dummy.

Target dummy: add a dev-only scene helper that spawns a vanilla Target Dummy or a
passive NPC at a fixed offset so hit effects can be captured.

#### 2.4 Charged beam
The signature attack, in the style of a Kamehameha. This is the hardest skill; the
framework from 2.1 should be shaped by what this needs.

Behaviour:
- Hold the key to charge, 0.5 to 2.5 seconds, with a growing orb at the hands and a
  rising sound. The player is slowed while charging and can aim.
- Release to fire. The beam sustains while ki lasts or for a fixed time scaled by
  charge, drains ki per second, and the player is rooted and turns slowly.
- Damage ticks several times a second along the beam, pierces, and stops at solid
  tiles.
- Start numbers: 20 ki to start, 15 ki per second to sustain, base damage 14 per
  tick at about 6 ticks per second, scaled up to 2x by charge.

Implementation notes:
- One projectile owns the whole beam. Each tick it ray-casts from the hands to find
  the length (tile collision), stores it, and uses a line-versus-box check for hits
  (`Collision.CheckAABBvLineCollision` is the usual call; verify). It is drawn as a
  start cap, a tiled body and an end cap. ExampleMod's laser example shows the
  pattern.
- Keep the charge level and the aim on the projectile's `ai` fields so they will
  sync later.

Art: charge orb (three sizes), beam start cap, body tile, end cap with impact
splash, each animated two or three frames, plus icon. Scenes: charging at half and
full, beam firing across open air, beam stopped by a wall.

#### 2.5 Melee: rush and launcher
Bare-handed combat that scales with ki damage.
- Rush: tap for a three-hit combo of short forward lunges with brief hit pause. Costs
  little or no ki. Each hit is a short-lived hitbox projectile in front of the player.
- Launcher: the combo finisher knocks the target away hard. Pressing the skill again
  within a short window dashes after it (costs ki) for a follow-up hit.
- The player needs brief damage reduction or invulnerability frames during lunges or
  melee will be unusable against contact-damage enemies. Tune this carefully.

Art: punch and kick arcs (hit sparks), dash streak, icons. The player's own arm
poses come from vanilla `Player.SetCompositeArmFront`; verify.
Scenes: mid-combo hit, launcher impact.

#### 2.6 Vanish dodge
Tap to blink a short distance in the held direction with invulnerability frames.
Cost 15 ki, cooldown about 1 second, cannot pass through solid tiles (step back
along the path to the last free spot). Leaves an after-image.

Art: after-image is the player drawn tinted (no sprite needed), a short streak,
icon. Scene: after-image with the player at the destination.

#### 2.7 Feel pass and play-test gate
- Screen shake on beam fire and heavy hits (through the camera modifier system;
  verify the current API), a few ticks of hit pause on melee, recoil, dust, light.
- Sounds for charge loop, blast, beam start, beam loop, impact, vanish. Use vanilla
  `SoundID` entries pitched and layered first; generate custom ones later if the user
  wants (the `sound-effects` skill needs an ElevenLabs key).
- Tuning pass over costs, cooldowns and damage against early enemies and the Eye of
  Cthulhu.
- **Stop for the user to play.** This is the first point the mod can be judged as a
  game. Record their feedback in `PLAN.md` and adjust before phase 3.
  Checklist: bind the keys; `/zskill grant all`, `/zskill dummy`; blast held down
  (rate, recoil, feel of hits); beam at short and full charge (slow walk while
  charging, aim turn while firing, press to stop, stops at walls, ki drain); Rush combo
  timing and the chase dash after the launcher, against a few slimes and zombies
  (does contact damage feel fair?); Vanish in eight directions and into walls; skills
  while flying; the hotbar and menu (K) with the inventory, chat and map open; save,
  quit and reload keeps learned skills and slots.

---

### Phase 3: Transformations

#### 3.1 Form framework
Build:
- `ModForm` definition type, discovered like skills. Fields: name, description,
  tier, ki damage multiplier, speed and defence bonuses, ki drain per second at zero
  mastery and at full mastery, optional life drain, aura palette, hair style and
  colour, eye colour, unlock rule, required race (unused until phase 7), and which
  form it upgrades from.
- `FormPlayer : ModPlayer`: unlocked forms, mastery per form (0 to 1), the active
  form, and the transform state (Idle, Transforming, Active, Reverting).
- Keybinds: transform (tap to enter the highest unlocked form, hold a direction or a
  menu to pick; keep it simple at first) and revert. **User decision:** default keys.
- Drain: forms drain ki every tick. At 0 ki the player drops out of the form and is
  briefly exhausted (a debuff that slows ki regen).
- Stats are applied each tick from the definition, in the same hooks armour uses.
- Mastery rises with time spent in the form while in combat (recently dealt or took
  damage) and lowers drain. Show it in the skill menu.

Done when: a test form with no art can be unlocked by command, entered, drains ki,
changes damage, reverts at 0 ki, and unlock state and mastery survive a reload.

#### 3.2 Form visuals
- Aura: give each form an `AuraStyle` (palette, size, tongue count and behaviour) and
  have `FormPlayer` set `KiPlayer.AuraStyle` while the form is active. The aura is
  generated live (`Common/Visuals/AuraRenderer.cs`), so no sheets are needed. Ideas:
  Kaioken red with fast, short tongues; Super Saiyan gold, taller tongues, more of
  them; lightning for Super Saiyan 2 as a new renderer feature.
- Hair: a draw layer that hides the vanilla hair and draws the form's hair in its
  place, following the head frame. Check how `PlayerDrawSet` exposes hair hiding and
  the head position before designing the sheet. Hats and helmets must still work;
  decide whether form hair shows through them (suggest: hair hidden under a full
  helmet, as vanilla does).
- Eyes: override the eye colour while in a form.
- Transformation sequence: a short power-up (about one second) with the camera
  shaking, dust pulled inward, a flash and a burst that knocks back nearby enemies.

Art: upright spiky hair sheet matching vanilla hair frame layout, per-form aura
styles (code, not sheets), burst ring, flash. Scenes: standing in form, mid-transformation burst,
in form while flying.

#### 3.3 Kaioken
The first form, available before Hardmode. Red aura, no hair change.
- Taught by the trainer (phase 5); until then by dev command.
- Start numbers: +20% ki damage, +10% movement speed, drains 2 life per second
  instead of ki. Mastery lowers the life drain.
- Later ranks (x3, x4, x10, x20) raise the bonus and the drain. They can stack on top
  of other forms much later; do not build that until forms are stable.

#### 3.4 Super Saiyan
The first true form, at early Hardmode.
- Unlock trigger: **User decision.** Options discussed: reach near death during a
  Hardmode boss fight; or a town NPC dies while a boss is alive. Suggestion: either
  condition unlocks it, with a one-time dramatic sequence, so no player is stuck
  waiting on luck.
- Start numbers: +35% ki damage, +15% speed, +8 defence, drain 4 ki per second at
  zero mastery down to 1.5 at full.
- Gold aura, gold upright hair, teal eyes.

#### Phase 3 as built (2026-11-14)
- Code: `Common/Forms/ModForm.cs` and `FormLoader.cs` (registry like skills),
  `Common/Players/FormPlayer.cs` (states Normal, Transforming, Active; no separate
  Reverting state, revert is instant), `Common/Players/FormHairLayer.cs`,
  `Content/Forms/Kaioken.cs` and `SuperSaiyan.cs`, `Content/Buffs/KiExhaustion.cs`,
  `Content/Dusts/TintSpark.cs`, `FormBurst` in `Content/Projectiles/EffectProjectile.cs`,
  dev command `Dev/FormCommand.cs`. Art in `art/forms.lua`.
- Keys (user decision): **T** steps up one form on the ladder per tap (Normal ->
  Kaioken -> Super Saiyan, skipping locked ones); **Y** reverts. Mod keybinds start
  unbound in tML 1.4.4, so bind them in Controls.
- Transforming takes 50 ticks (100 for the first, dramatic unlock), then a burst
  tinted by the form's aura knocks back NPCs within 180 px.
- Out of ki in a ki-draining form: drop out and get `KiExhaustion` for 5 s (natural ki
  regen x0.25). Kaioken drains life instead (2/s down to 1/s at full mastery) and
  reverts below 10% life.
- Mastery rises only while in combat; it is saved per form and shown as a bar under
  each form in the skill menu (K), whose details panel lists the form's stats.
- Super Saiyan unlock (user decision): **near death only**. In `PostHurt`, Hardmode,
  life under 10% and any boss alive. The town-NPC trigger was dropped.
- Hair: the layer runs after `PlayerDrawLayers.Head`, hides vanilla hair by making
  `colorHair` transparent, and draws a 20x34 sprite (6 pad rows above the 20x28 frame)
  at the head anchor plus `Main.OffsetsPlayerHeadgear`. It is skipped under a full
  helmet (head slot set and not `fullHair`), as vanilla does. Gotcha: the face must stay
  clear in `art/forms.lua` (`face()` cut and short bangs); the first version hid the
  eyes.
- Scenes: `form_kaioken`, `form_transform`, `form_burst`, `form_ssj`, `form_ssj_left`
  (forces facing in the per-tick hook; vanilla resets direction otherwise),
  `form_ssj_flight`, `form_menu`. Run them with `ZENKAI_SHOT_FILTER=form_`.
- Known gap: `form_burst` catches the end of the burst, so the ring barely shows in
  the shot; judge the burst in play.

#### 3.5 Vertical slice review
Phases 1 to 3 together are the slice. **Stop for the user to play** from a fresh
character through the first Hardmode boss with dev commands for anything not yet
earnable. Do not start phase 4 until the user is happy with how it feels.

Checklist for forms (bind T and Y first): `/zform unlock all`, tap T twice to reach
Super Saiyan, watch ki drain and revert with Y; run ki to zero and confirm the
exhaustion debuff; in Kaioken, let life fall under 10% and confirm the auto-revert;
`/zform awaken SuperSaiyan` for the dramatic sequence; check hair with and without
helmets and while flying; `/zform mastery SuperSaiyan 100` and confirm the drain
drops; save, quit and reload to confirm unlocks and mastery persist.

---

### Phase 4: Dragon Balls

#### 4.1 Items, tiles and world generation
- Seven Dragon Balls, one to seven stars. Each is an item that places a tile and a
  tile that drops the item.
- Generation: add a pass through the world generation task list that places one ball
  in each of seven distinct regions, so collecting them tours the world. Suggested:
  forest surface, desert, snow, jungle, underground cavern, ocean floor, sky island
  or the evil biome.
- Worlds made before the mod was added: on world load, if the world has no record of
  its balls, place them then.
- A world-level system saves each ball's state (in world at x,y; held by a player;
  inert until day N).

Art: seven ball items (one sprite with one to seven stars), placed tile, a glint.
Scenes: a ball sitting in the world, the seven in an inventory.

#### 4.2 Dragon Radar
An item that, while held or in a dedicated slot, shows an arrow at the screen edge
toward the nearest ball and marks balls on the map. Crafted early (iron or lead,
gems, wire).

Art: item, HUD arrow, map icon. Scenes: arrow on screen, map with an icon.

#### 4.3 Summoning and the wish menu
- Place all seven within a few tiles of each other and use any of them to summon.
- The sky darkens, a large dragon rises behind the balls, and the wish menu opens.
  After the wish the balls scatter to new positions and stay inert (stone) for a
  number of in-game days. **User decision:** how long (suggest 7 days).
- The dragon is the largest sprite in the mod. Plan it as its own art task: a long
  serpentine body built from repeating segments plus a head, so it can be big
  without one huge hand-made image.

Scenes: summoning in progress, wish menu open.

#### 4.4 Wishes
One wish per summoning. Starting list:
- Power: permanent max ki increase (capped per game stage).
- Knowledge: learn one skill you do not have, from a tier-limited list.
- Wealth: a pile of coins and ore scaled to progression.
- Life: revive every town NPC that has died, or full heal and a long regeneration buff.
- Potential: raise mastery of one form by a large step.
**User decision:** whether to include immortality-style or boss-skipping wishes.
Suggest not; they break progression.

---

### Phase 5: Pre-Hardmode content

#### 5.1 Trainer NPC
A town NPC who teaches skills, in the role of the old martial arts master.
- Moves in once the player has a house and has used ki a certain amount.
- His shop is a "train" menu: skills listed with a coin price and a requirement
  (max ki, a boss defeated, another skill known).
- Teaches Ki Blast upgrades, the charged beam, the rush, the vanish dodge, flight
  efficiency and Kaioken across pre-Hardmode.
- Dialogue lines that react to progress. Use the writing skills installed on this
  machine (`llm-writing`, `character-sim`) for his voice.

Art: full town NPC sheet in vanilla layout (walk, sit, talk, attack frames), head
icon for the map and housing menu. This is a large sprite job; study the vanilla
frame layout before starting.

#### 5.2 Gear and consumables
- Ki armour: three pre-Hardmode sets styled as martial arts gi (cloth, weighted,
  reinforced), giving ki damage, max ki and ki regen. Each set needs head, body and
  legs with equip textures in vanilla layout.
- Accessories: scouter (shows nearby enemies' life and a threat estimate), weighted
  wristbands and boots (see 5.3), a turtle shell backpiece (defence, slower).
- Senzu Bean: full heal and full ki, long cooldown shared with potions, grown slowly
  from a planter the trainer sells. Rare.
- Power Pole: a melee weapon with long reach that extends on use.
- Flying cloud mount: free flight at modest speed with no ki cost, cannot attack with
  ki skills while riding. Makes early exploration pleasant without replacing flight.

#### 5.3 Training
- Weighted gear: while worn, movement and attack speed are reduced; time spent in
  combat while wearing it banks training points that convert to small permanent
  bonuses (max ki, ki damage) up to a cap per game stage.
- Gravity chamber: a furniture controller that makes a closed room a high-gravity
  zone. Inside, the player moves slower and takes chip damage, and mastery and
  training points build faster. Higher settings unlock with progression.
- Zenkai: surviving a drop below 10% life and then leaving combat alive grants a
  small permanent ki damage bonus, with a long cooldown and a cap per stage. Required
  race later (Saiyan); until races ship everyone gets it.

`TrainingPlayer : ModPlayer` owns all of this.

#### 5.4 Saibamen event
The first mod event, after the Eye of Cthulhu.
- Triggered by an item (seeds planted at night). Small green creatures sprout from
  the ground in waves.
- Three enemy types: a basic leaper, an acid spitter, and one that grabs on and
  self-destructs with a clear wind-up.
- tModLoader has no ready-made event class. The usual approach is a `ModSystem` that
  tracks event state and progress, a `GlobalNPC` that replaces the spawn pool while
  it runs, and the vanilla invasion progress bar. Check ExampleMod's current approach.
- Drops: crafting material for the reinforced gi, the seeds, a rare skill scroll.

Art: three enemies with walk and attack frames, sprout animation, event icon.

#### 5.5 Great Ape
The pre-Hardmode boss of the mod, fought at night.
- Summoned by an item during a full moon, or appears on its own on some full moons
  after the Saibamen event is cleared.
- A giant: slow stomps with shockwaves, boulder throws, a mouth beam it charges
  visibly. Destroying or severing the tail (a separate hit zone behind it) ends the
  fight early or weakens it; decide during design.
- Target: between Skeletron and the Wall of Flesh in difficulty.
- Drops: a treasure bag in Expert, material for the best pre-Hardmode ki set, a
  trophy and mask, and the scroll for a mouth-beam-style skill.
- Register with the Boss Checklist mod through `Mod.Call` if it is loaded.

Art: the biggest sprite before the dragon. Build it from parts (body, arms, head,
tail) so it can animate without drawing every frame whole.

---

### Phase 6: Hardmode content

Design each of these in detail when reached. Outline:

#### 6.1 Early Hardmode skills and gear
New skill tier from the trainer and scrolls: a wide energy wave, a homing disc that
pierces, a barrage of small blasts, a short-range explosive wave around the player,
instant movement to the cursor. Two Hardmode ki armour sets.

#### 6.2 Invasion force
An army event after the first mechanical boss: soldiers with blasters, armoured
elites, flying pods that drop reinforcements, and a named lieutenant as a mini-boss.
Uses the event system from 5.4.

#### 6.3 The tyrant boss
A multi-form boss after the invasion: each form is a phase with a new move set and a
short transformation between them. The final phase introduces the **beam struggle**:
when the player's beam meets the boss's, both lock, a meter appears, and the player
pushes by spending ki (and tapping). Winning deals heavy damage; losing hurts.
Defeating it is tied to the Super Saiyan unlock (see 3.4).

Beam struggle needs its own design pass: how it starts, the meter UI, what the
player does, and how it ends. Prototype it against a dummy before the boss exists.

#### 6.4 Androids and the bio-android boss
After Plantera. Two android mini-bosses that fight as a pair (they do not use ki, so
ki sense does not show them). Then the bio-android boss: absorbs enemies or the
defeated androids to change form, spawns small copies of itself, uses the player's
own learned skills back at them, and ends with a huge beam struggle.
Unlocks Super Saiyan 2 (suggest: by winning the final beam struggle).

#### 6.5 Late Hardmode forms and gear
Super Saiyan 2 (lightning in the aura, sharper hair), further Kaioken ranks, a late
Hardmode ki armour set, and skill ranks to keep up through Golem, the Cultist and the
pillars.

---

### Phase 7: Races

Chosen at character creation or with a one-time item for existing characters.
**User decision:** which of the two, and whether race can be changed later.
- Saiyan: Zenkai, the Super Saiyan line, Great Ape state on full moons while a tail
  accessory is worn.
- Human: faster skill learning and cheaper skills, the Kaioken line goes further,
  unlocked-potential form.
- Namekian: natural life regeneration, limb stretch for long melee reach, a giant
  form, can make Dragon Balls inert time shorter.
- Frost demon: starts strong, forms work in reverse (suppression forms that release
  power), high ki pool.
- Android: no ki charging; ki refills on its own or by absorbing enemy projectiles.

Each race needs its own form tree, so the form framework's race field (3.1) is what
makes this phase possible. Add a race only when its form art exists.

---

### Phase 8: Multiplayer

Singleplayer came first by the user's decision. This phase makes everything sync.
- Ki, charging, flying, active form, active skill state: sync through the
  `ModPlayer` sync hooks (`SyncPlayer`, `CopyClientState`, `SendClientChanges`) and a
  mod packet handler in `Zenkai.cs`.
- Beams: owner simulates, others draw from synced length and aim.
- Dragon Balls: world state is server-owned; wishes are requested by the client and
  granted by the server.
- Events and bosses: spawning and progress on the server.
- Test with a local dedicated server (`start-tModLoaderServer.sh`) and two clients.
  Extend the shot tooling to launch a server and a client if visual checks need it.
- Player-versus-player balance is out of scope unless the user asks.

---

### Phase 9: Endgame

After Moon Lord.
- Super Saiyan 3 (very high drain, long hair, no eyebrows), then god-tier forms with
  a different feel: lower multipliers than the numbers suggest but ki that stops
  draining at high mastery.
- A god of destruction superboss with an attendant, tuned as the hardest fight in
  the mod.
- A tournament arena structure: fight waves of earlier bosses with rules (no
  potions, ring-out) for unique rewards. Optional; build only if the user wants it.

---

### Phase 10: Release readiness

- `ModConfig` options: HUD position and scale, screen shake strength, aura opacity,
  dev tools toggle.
- Mod icon, description, changelog, Workshop preview image.
- Localisation review: every string in hjson, no hard-coded English.
- Compatibility: Boss Checklist entries, Census town NPC info, a quick check next to
  a large content mod for obvious breakage (the balance target stays vanilla).
- Performance: profile a busy fight. Watch per-frame allocations in draw code and
  the beam ray-cast.
- Remove or gate `Zenkai/Dev/` so it cannot be triggered by players.
- **User decision:** Steam Workshop or private. The mod uses Dragon Ball names and
  likenesses; the art is original but the names are not. That is the user's call.

## 6. Content tables

Working lists to draw from. Names can use the series terms; sprites must be original.

### Skills

| Skill | Kind | Stage | Cost | Learned from |
|---|---|---|---|---|
| Ki Blast | blast | start | 5 | known from the start |
| Rush | melee | start | 0 to 5 | known from the start |
| Vanish | movement | pre-boss | 15 | trainer |
| Charged beam (Kamehameha) | beam | after first boss | 20 + 15/s | trainer |
| Double blast | blast | pre-Hardmode | 12 | trainer |
| Solar flash (blinds nearby enemies) | utility | pre-Hardmode | 25 | trainer |
| Mouth beam | beam | pre-Hardmode | 30 + 20/s | Great Ape scroll |
| Energy disc (piercing, homing) | blast | early Hardmode | 35 | trainer |
| Explosive wave (around player) | burst | early Hardmode | 45 | scroll |
| Barrage | blast | early Hardmode | 8 each | trainer |
| Instant movement | movement | mid Hardmode | 40 | trainer quest |
| Piercing beam (narrow, long charge, armour-piercing) | beam | mid Hardmode | 50 | scroll |
| Spirit bomb (very long charge, gathers from nearby NPCs) | ultimate | late Hardmode | all ki | trainer, late |
| Final beam (two-handed, widest) | beam | post-Plantera | 60 + 30/s | boss scroll |

### Forms

| Form | Stage | Ki damage | Other | Drain (zero to full mastery) |
|---|---|---|---|---|
| Kaioken | pre-Hardmode | +20% | +10% speed | 2 to 1 life/s |
| Kaioken x3 and up | Hardmode | +35% and up | more speed | rising life drain |
| Super Saiyan | early Hardmode | +35% | +15% speed, +8 defence | 4 to 1.5 ki/s |
| Super Saiyan 2 | post-Plantera | +55% | +20% speed, +14 defence | 7 to 3 ki/s |
| Super Saiyan 3 | post-Moon Lord | +85% | +25% speed, +20 defence | 14 to 7 ki/s |
| God forms | endgame | +100% and up | ki efficiency | low at mastery |

### Bosses and events

| Encounter | Stage | Unlocks |
|---|---|---|
| Saibamen event | after Eye of Cthulhu | reinforced gi material |
| Great Ape | before Wall of Flesh | best pre-Hardmode set, mouth beam |
| Invasion force | after first mechanical boss | Hardmode gear |
| Tyrant (multi-form) | after the mechanical bosses | Super Saiyan tie-in |
| Android pair | after Plantera | late Hardmode gear |
| Bio-android | after Golem | Super Saiyan 2 |
| God of destruction | after Moon Lord | god forms |

## 7. Decisions waiting on the user

Ask these when the milestone that needs them comes up, not all at once.

| Needed for | Question | Suggestion |
|---|---|---|
| 2.2 | Default keys for the four skill slots and the skill menu | decided 2026-11-14: Z, X, V, G and K |
| 3.1 | Default keys for transform and revert | decided 2026-11-14: T (tap steps up a form) and Y (revert) |
| 3.4 | How Super Saiyan unlocks | decided 2026-11-14: near death in a Hardmode boss fight only |
| 4.3 | Days the Dragon Balls stay inert after a wish | 7 in-game days |
| 4.4 | Whether any wish may skip progression | no |
| 7 | Race chosen at creation or by item; changeable or not | at creation, changeable once by wish |
| 10 | Workshop release or private | user's call |
| any | Commit and push policy | nothing is committed yet; ask before the first commit |

## 8. Risks

- **Art volume.** Bosses, the town NPC, armour equip sheets and the dragon are large
  sprite jobs, and every one must pass a strict gate. Build big sprites from parts,
  reuse palettes, and expect several judge rounds each. If art becomes the
  bottleneck, tell the user early; they may want to supply or commission some.
- **Feel cannot be tested headless.** The shot runner proves things render. Only the
  user playing proves they feel right. Do not stack three phases of content on
  untested feel; the play-test gates in 2.7 and 3.5 exist for this.
- **Beam and beam struggle complexity.** Both touch input, projectiles, drawing and
  later sync. Prototype early and keep them isolated.
- **Balance creep from multiplicative bonuses.** Form, armour and accessory
  multipliers compound. Re-check total ki damage against the table in section 4 at
  each tier.
- **tModLoader updates.** The game was mid-update toward Terraria 1.4.5 on
  2026-11-14 (the build's commit log mentions back-porting 1.4.5 behaviour). An API
  break is possible; re-run `tools/build.sh` and the shots after any update.
- **Judge availability.** The judge route in the user's global instructions was not
  connected in the 2026-11-14 session. If it is missing again, say so and use the
  stated fallback rather than skipping the gate.
