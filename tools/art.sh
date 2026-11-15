#!/usr/bin/env bash
# Rebuilds sprites from the Lua sources in art/. Writes editable .aseprite files to art/
# and 2x PNGs into Zenkai/Assets/. Usage: tools/art.sh [script name without .lua ...]
set -euo pipefail

REPO="$(cd "$(dirname "$0")/.." && pwd)"
mkdir -p "$REPO/Zenkai/Assets/UI" "$REPO/Zenkai/Assets/Effects" "$REPO/Zenkai/Assets/Skills" "$REPO/Zenkai/Assets/Projectiles" "$REPO/Zenkai/Assets/Forms" "$REPO/Zenkai/Assets/Buffs" "$REPO/Zenkai/Assets/Dusts"

scripts=("$@")
if [ ${#scripts[@]} -eq 0 ]; then
	scripts=()
	for f in "$REPO"/art/*.lua; do n="$(basename "$f" .lua)"; [ "$n" = lib ] || scripts+=("$n"); done
fi

for name in "${scripts[@]}"; do
	aseprite -b --script-param root="$REPO" --script "$REPO/art/$name.lua"
done
