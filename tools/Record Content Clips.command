#!/usr/bin/env bash
# Double-click: records short gameplay clips from the Simulator build the Build Check made (run that first), for
# TikTok / Reels / Shorts / X. Each clip launches the game with RH_SCREENSHOT=<shot> (it opens straight onto that
# screen; the game shots are AI demo games) and records the Simulator screen for a few seconds with
# `xcrun simctl io recordVideo`. Portrait 1320x2868 .mp4 files land in ~/RetroHoops/ContentClips/.
# No sound (the Simulator recording has none): add music or a voice-over in CapCut / the TikTok editor.
# The summary Claude reads is ~/RetroHoops/Logs/content_clips.txt.
cd "$(dirname "$0")/.." || exit 1
PROJECT="$HOME/RetroHoops"
LOGS="$PROJECT/Logs"
OUT="$PROJECT/ContentClips"
mkdir -p "$LOGS" "$OUT"
exec > >(tee "$LOGS/content_clips.txt") 2>&1
echo "=== $(date '+%Y-%m-%d %H:%M:%S')  Content clips"

APP="$(find "$PROJECT/iOSBuild/Simulator/DerivedData/Build/Products" -maxdepth 2 -name '*.app' -type d 2>/dev/null | head -1)"
if [[ -z "$APP" ]]; then echo "STOPPED: no Simulator build yet. Run 'Build Check (Simulator).command' first."; exit 1; fi
BUNDLE="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$APP/Info.plist")"

# name:seconds to settle:seconds to record (shot names are StoreShots.All in Logic/Feel/StoreShots.cs).
CLIPS="game:6:22 fullcourt:6:22 title:3:8 clutch:4:10 park:4:10 locker:4:8"

DEVICE="$(xcrun simctl list devices available | grep -E 'iPhone [0-9]+ Pro Max' | tail -1 | grep -oE '[0-9A-F-]{36}')"
if [[ -z "$DEVICE" ]]; then echo "STOPPED: no 'Pro Max' iPhone simulator."; exit 1; fi
xcrun simctl boot "$DEVICE" >/dev/null 2>&1
waited=0
until xcrun simctl list devices | grep "$DEVICE" | grep -q Booted; do
  sleep 5; waited=$((waited + 5))
  if [[ $waited -ge 180 ]]; then echo "STOPPED: the simulator didn't boot in 3 minutes"; exit 1; fi
done
sleep 8
open -a Simulator --args -CurrentDeviceUDID "$DEVICE" >/dev/null 2>&1
xcrun simctl status_bar "$DEVICE" override --time "9:41" --batteryState charged --batteryLevel 100 \
  --cellularMode active --cellularBars 4 --wifiBars 3 >/dev/null 2>&1
xcrun simctl install "$DEVICE" "$APP" || { echo "FAILED: couldn't install"; exit 1; }

STAMP="$(date '+%m%d-%H%M')"
for entry in $CLIPS; do
  IFS=: read -r shot settle length <<< "$entry"
  xcrun simctl terminate "$DEVICE" "$BUNDLE" >/dev/null 2>&1
  SIMCTL_CHILD_RH_SCREENSHOT="$shot" xcrun simctl launch "$DEVICE" "$BUNDLE" >/dev/null || { echo "FAILED: launch for $shot"; continue; }
  sleep "$settle"
  file="$OUT/$STAMP-$shot.mp4"
  xcrun simctl io "$DEVICE" recordVideo --codec=h264 --force "$file" >/dev/null 2>&1 &
  rec=$!
  sleep "$length"
  kill -INT "$rec" 2>/dev/null; wait "$rec" 2>/dev/null
  if [[ -s "$file" ]]; then echo "  $(basename "$file")  ${length}s  $(du -h "$file" | cut -f1)"; else echo "  FAILED: $shot"; fi
done
xcrun simctl terminate "$DEVICE" "$BUNDLE" >/dev/null 2>&1
xcrun simctl status_bar "$DEVICE" clear >/dev/null 2>&1

echo
echo "CLIPS DONE: $OUT"
open "$OUT" >/dev/null 2>&1
echo "=== finished $(date '+%H:%M:%S'). You can close this window; Claude reads the logs."
