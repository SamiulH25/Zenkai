# Zenkai

A tModLoader mod that adds Dragon Ball style ki combat and progression to Terraria.
Early development: ki, charging, flight and a ki HUD bar exist; skills come next.

- Design, decisions, phase status and build gotchas: [docs/PLAN.md](docs/PLAN.md)
- Milestone-by-milestone build plan: [docs/ROADMAP.md](docs/ROADMAP.md)
- Mod source: `Zenkai/`
- Build and pack into tModLoader's Mods folder: `tools/build.sh`
- Regenerate sprites: `tools/art.sh`. Headless screenshots: `tools/shots.sh`

## Persistent play-test saves

In Single Player, select **Zenkai Tester**, then **Zenkai Training Ground**.
The installed tester has 500 health, 200 mana, 5,000 ki, every skill and mastered
form, tools and supplies. The world has a cleared spawn arena with three dummies.
Use Save & Exit normally; these are permanent local saves, not screenshot scenes.

If needed, bind Zenkai controls in Settings → Controls: C charge, F flight,
V ki stance, K skills, T transform (hold for the form wheel). Existing bindings
were not changed. In ki stance, use 1–8 and the mouse buttons for the skill bar.

For a fresh installation, `tools/test-save.sh` generates, reload-tests and installs
a new pair without replacing any existing saves. It refuses if the target saves
already exist or tModLoader is running. Backups are in
`.playtest/run-*/ZenkaiTestSaves.zip`. See [the plan](docs/PLAN.md) for details.

Requires tModLoader (Steam) and a .NET SDK.
