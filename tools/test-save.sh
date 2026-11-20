#!/usr/bin/env bash
# Create and reload-test a persistent training save, then install without replacing any saves.
set -euo pipefail
REPO="$(cd "$(dirname "$0")/.." && pwd)"
TML="${TML_DIR:-$HOME/.local/share/Steam/steamapps/common/tModLoader}"
DEST="${TML_SAVE_DIR:-$HOME/.local/share/Terraria/tModLoader}"
TARGETS=("$DEST/Players/ZenkaiTester.plr" "$DEST/Players/ZenkaiTester.tplr" "$DEST/Worlds/ZenkaiTraining.wld" "$DEST/Worlds/ZenkaiTraining.twld")
for file in "${TARGETS[@]}"; do
    if [ -e "$file" ]; then
        printf 'Refusing to replace an existing test save: %s\n' "$file" >&2
        exit 1
    fi
done
if pgrep -f '[d]otnet.*tModLoader.dll' >/dev/null; then
    echo "Close tModLoader before installing test saves." >&2
    exit 1
fi
"$REPO/tools/build.sh"
RUN="$REPO/.playtest/run-$(date +%Y%m%d-%H%M%S)-$$"
SAVE="$RUN/save"
mkdir -p "$SAVE/Mods"
cp "${TML_MODS:-$HOME/.local/share/Terraria/tModLoader/Mods}/Zenkai.tmod" "$SAVE/Mods/"
printf '["Zenkai"]\n' > "$SAVE/Mods/enabled.json"
python3 - "$SAVE/config.json" <<'PY'
import json, sys
with open(sys.argv[1], 'x') as f:
    json.dump({
        "LastLaunchedTModLoaderVersion": "9999.0.0.0", "LastLaunchedVersion": 279,
        "BetaUpgradeWelcomed144": True, "SeenFirstLaunchModderWelcomeMessage": True,
        "Language": "en-US", "Fullscreen": False, "WindowMaximized": False,
        "DisplayWidth": 1280, "DisplayHeight": 720, "UIScale": 1.0,
        "ResetDefaultUIScale": False, "VolumeSound": 0.0, "VolumeAmbient": 0.0,
        "VolumeMusic": 0.0, "AutoPause": False,
    }, f)
PY
run_game() {
    local mode="$1"
    (
        cd "$TML"
        env -u WAYLAND_DISPLAY -u ZENKAI_SHOTS \
            SDL_VIDEODRIVER=x11 ALSOFT_DRIVERS=null SDL_AUDIODRIVER=dummy \
            DOTNET_ROLL_FORWARD=Disable ZENKAI_TEST_SAVE="$RUN" ZENKAI_TEST_VERIFY="$mode" \
            timeout "${TEST_SAVE_TIMEOUT:-900}" xvfb-run -a -s '-screen 0 1280x720x24' \
            "$TML/dotnet/dotnet" tModLoader.dll -nosteam -tmlsavedirectory "$SAVE"
    ) > "$RUN/run-$mode.log" 2>&1
}
if ! run_game 0 || [ ! -f "$RUN/created.json" ]; then
    echo "Creation failed. See $RUN/run-0.log and $RUN/error.txt" >&2
    exit 1
fi
if ! run_game 1 || [ ! -f "$RUN/verified.txt" ]; then
    echo "Reload verification failed. See $RUN/run-1.log and $RUN/error.txt" >&2
    exit 1
fi
python3 - "$RUN" "$DEST" <<'PY'
import hashlib, json, pathlib, sys, zipfile
run, dest = map(pathlib.Path, sys.argv[1:])
data = json.loads((run / 'created.json').read_text())
player, world = pathlib.Path(data['player']), pathlib.Path(data['world'])
pairs = [
    (player, dest / 'Players/ZenkaiTester.plr'),
    (player.with_suffix('.tplr'), dest / 'Players/ZenkaiTester.tplr'),
    (world, dest / 'Worlds/ZenkaiTraining.wld'),
    (world.with_suffix('.twld'), dest / 'Worlds/ZenkaiTraining.twld'),
]
# Check the complete set before touching the destination; exclusive creation also prevents races.
for source, target in pairs:
    if not source.is_file() or source.stat().st_size == 0:
        raise SystemExit(f'Missing or empty generated save: {source}')
    if target.exists():
        raise SystemExit(f'Refusing to replace {target}')
for source, target in pairs:
    target.parent.mkdir(parents=True, exist_ok=True)
    content = source.read_bytes()
    with target.open('xb') as f:
        f.write(content)
    if hashlib.sha256(target.read_bytes()).digest() != hashlib.sha256(content).digest():
        raise SystemExit(f'Copy validation failed: {target}')
    print(f'Installed: {target}')
archive = run / 'ZenkaiTestSaves.zip'
with zipfile.ZipFile(archive, 'x', zipfile.ZIP_DEFLATED) as z:
    for source, target in pairs:
        z.write(source, f'{target.parent.name}/{target.name}')
    z.write(run / 'verified.txt', 'verified.txt')
data.update(installed=[str(t) for _, t in pairs], archive=str(archive))
(run / 'installed.json').write_text(json.dumps(data, indent=2))
print(f'Backup: {archive}')
print(f"Ready: {data['characterName']} / {data['worldName']}; {data['skills']} skills, {data['forms']} forms, {data['maxKi']} ki")
PY
