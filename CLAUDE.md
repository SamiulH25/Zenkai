# Zenkai (Terraria Dragon Ball mod)

A tModLoader mod. Start every session by reading, in this order:

1. `docs/PLAN.md`: design decisions, environment, tooling, gotchas, phase status.
2. `docs/ROADMAP.md`: the milestone-by-milestone build plan and working rules.

Commands:
- `tools/build.sh` compiles and packs the mod into tModLoader's Mods folder.
- `tools/art.sh` regenerates sprites from the Lua sources in `art/`.
- `tools/shots.sh` runs the game headless and writes screenshots to `.shots/out/`.

Rules that are easy to miss:
- The mod source is `Zenkai/`. Docs, tools and art sources stay outside it.
- Edit sprite Lua in `art/`, never the PNGs in `Zenkai/Assets/`.
- Check tModLoader API names against the installed build before using them
  (`docs/ROADMAP.md`, "Checking the tModLoader API").
- Every visual change goes through the visual gate in the user's global instructions.
- Commit only when the user asks. Nothing was committed as of 2026-11-14.
- Mod keybinds start unbound in tModLoader 1.4.4; bind them in Controls to test.
- Keep `docs/PLAN.md` and `docs/ROADMAP.md` current in the same task as the change.
