#!/usr/bin/env bash
# Builds the mod, runs tModLoader headless and saves the ShotRunner scenes as PNGs in .shots/out/.
# Uses its own save folder (.shots/save) so real characters, worlds and enabled mods are untouched.
# The first run generates a small test world, which takes a few minutes.
set -euo pipefail

MOD=Zenkai
REPO="$(cd "$(dirname "$0")/.." && pwd)"
TML="${TML_DIR:-$HOME/.local/share/Steam/steamapps/common/tModLoader}"
REAL_MODS="${TML_MODS:-$HOME/.local/share/Terraria/tModLoader/Mods}"
SAVE="$REPO/.shots/save"
OUT="$REPO/.shots/out"
WIDTH=1280
HEIGHT=720

"$REPO/tools/build.sh"

mkdir -p "$SAVE/Mods" "$OUT"
rm -f "$OUT"/*.png "$OUT"/done.txt "$OUT"/error.txt
cp "$REAL_MODS/$MOD.tmod" "$SAVE/Mods/"
echo "[\"$MOD\"]" > "$SAVE/Mods/enabled.json"
# tModLoader shows blocking first-run dialogs before it loads mods unless the config says this
# version has been launched before. Read the version from its own log line, e.g. 1.4.4.9+2026.09.3.1
TML_VERSION="$(grep -m1 -o 'Starting tModLoader client [0-9.]*+[0-9.]*' "$TML/tModLoader-Logs/client.log" 2>/dev/null \
	| sed 's/.*+//; s/\.0*\([0-9]\)/.\1/g' || true)"
cat > "$SAVE/config.json" <<EOF
{
  "LastLaunchedTModLoaderVersion": "${TML_VERSION:-9999.0.0.0}",
  "LastLaunchedVersion": 279,
  "BetaUpgradeWelcomed144": true,
  "SeenFirstLaunchModderWelcomeMessage": true,
  "Language": "en-US",
  "Fullscreen": false,
  "WindowMaximized": false,
  "WindowBorderless": true,
  "DisplayWidth": $WIDTH,
  "DisplayHeight": $HEIGHT,
  "UIScale": 1.0,
  "ResetDefaultUIScale": false,
  "Zoom": 1.0,
  "VolumeSound": 0.0,
  "VolumeAmbient": 0.0,
  "VolumeMusic": 0.0,
  "AutoPause": false,
  "MapEnabled": true
}
EOF

cd "$TML"
# Headless and silent: virtual X display, no audio device. Wayland is unset so SDL uses X.
set +e
env -u WAYLAND_DISPLAY \
	SDL_VIDEODRIVER=x11 ALSOFT_DRIVERS=null SDL_AUDIODRIVER=dummy \
	DOTNET_ROLL_FORWARD=Disable \
	ZENKAI_SHOTS="$OUT" \
	timeout "${SHOTS_TIMEOUT:-900}" \
	xvfb-run -a -s "-screen 0 ${WIDTH}x${HEIGHT}x24" \
	"$TML/dotnet/dotnet" tModLoader.dll -nosteam -tmlsavedirectory "$SAVE" \
	> "$REPO/.shots/run.log" 2>&1
status=$?
set -e

if [ -f "$OUT/done.txt" ]; then
	echo "shots: $(cat "$OUT/done.txt") in $OUT"
	ls "$OUT"/*.png
else
	echo "shots failed (exit $status). See .shots/run.log, .shots/save/Logs/client.log and $OUT/error.txt" >&2
	exit 1
fi
