#!/usr/bin/env bash
# Compiles the mod and packs Zenkai.tmod into tModLoader's Mods folder.
# The ModSources symlink is what lets the in-game Mod Sources menu see the mod.
set -euo pipefail

MOD=Zenkai
REPO="$(cd "$(dirname "$0")/.." && pwd)"
SOURCES="${TML_MODSOURCES:-$HOME/.local/share/Terraria/tModLoader/ModSources}"

[ -e "$SOURCES/$MOD" ] || ln -sfn "$REPO/$MOD" "$SOURCES/$MOD"

# The pack step runs tModLoader.dll (net8) with the system dotnet, which may only have a newer runtime.
export DOTNET_ROLL_FORWARD=Major

cd "$REPO/$MOD"
exec dotnet build "$MOD.csproj" -c Debug -nologo -v q "$@"
