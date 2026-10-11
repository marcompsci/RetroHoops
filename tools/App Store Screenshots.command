#!/usr/bin/env bash
# Double-click: takes the App Store screenshots from the Simulator build the Build Check made (run that first).
# For each shot the game is launched with RH_SCREENSHOT=<shot>, opens straight onto that screen, and the picture is
# saved to ~/RetroHoops/AppStoreScreenshots/<simulator>/. iPhone 6.9" (iPhone 17 Pro Max: 1320 x 2868) is the size
# App Store Connect needs; a 13" iPad set is taken too if an iPad simulator is installed.
# The summary Claude reads is ~/RetroHoops/Logs/store_screenshots.txt.
cd "$(dirname "$0")/.." || exit 1
PROJECT="$HOME/RetroHoops"
LOGS="$PROJECT/Logs"
OUT_ROOT="$PROJECT/AppStoreScreenshots"
mkdir -p "$LOGS" "$OUT_ROOT"
exec > >(tee "$LOGS/store_screenshots.txt") 2>&1
echo "=== $(date '+%Y-%m-%d %H:%M:%S')  App Store screenshots"

APP="$(find "$PROJECT/iOSBuild/Simulator/DerivedData/Build/Products" -maxdepth 2 -name '*.app' -type d 2>/dev/null | head -1)"
if [[ -z "$APP" ]]; then echo "STOPPED: no Simulator build yet. Run 'Build Check (Simulator).command' first."; exit 1; fi
echo "App: $APP (built $(stat -f '%Sm' "$APP" 2>/dev/null))"
BUNDLE="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$APP/Info.plist")"

# name:seconds to wait, in App Store order (keep in step with StoreShots.All in Logic/Feel/StoreShots.cs).
SHOTS="game:14 title:8 fullcourt:14 play:9 clutch:9 park:9 locker:10 pro:9"
# 08-pro.png is for the Retro Hoops Pro in-app purchase review screenshot, not the store listing.

shoot_on() {
  local NAME="$1" DEVICE="$2"
  local DIR="$OUT_ROOT/$NAME"
  mkdir -p "$DIR"
  echo "--- $NAME ($DEVICE)"
  xcrun simctl boot "$DEVICE" >/dev/null 2>&1
  # Wait for it to boot, but never forever (a first boot of a new runtime can stall): 3 minutes at most.
  local waited=0
  until xcrun simctl list devices | grep "$DEVICE" | grep -q Booted; do
    sleep 5; waited=$((waited + 5))
    if [[ $waited -ge 180 ]]; then echo "  SKIPPED: $NAME didn't finish booting in 3 minutes"; return 1; fi
  done
  sleep 10  # let SpringBoard settle
  open -a Simulator --args -CurrentDeviceUDID "$DEVICE" >/dev/null 2>&1
  # A tidy status bar: 9:41, full battery and signal.
  xcrun simctl status_bar "$DEVICE" override --time "9:41" --batteryState charged --batteryLevel 100 \
    --cellularMode active --cellularBars 4 --wifiBars 3 >/dev/null 2>&1
  xcrun simctl install "$DEVICE" "$APP" || { echo "FAILED: couldn't install on $NAME"; return 1; }
  local n=1
  for entry in $SHOTS; do
    local shot="${entry%%:*}" wait="${entry##*:}"
    xcrun simctl terminate "$DEVICE" "$BUNDLE" >/dev/null 2>&1
    SIMCTL_CHILD_RH_SCREENSHOT="$shot" xcrun simctl launch "$DEVICE" "$BUNDLE" >/dev/null || { echo "FAILED: launch for $shot"; continue; }
    sleep "$wait"
    local file="$DIR/$(printf '%02d' $n)-$shot.png"
    if xcrun simctl io "$DEVICE" screenshot --type=png "$file" >/dev/null 2>&1; then
      # (Phase 38: the Full Court shot is an AI demo game, which stays upright, so no picture is turned any more.)
      echo "  $(basename "$file")  $(sips -g pixelWidth -g pixelHeight "$file" 2>/dev/null | awk '/pixel/{printf "%s ", $2}')"
    else
      echo "  FAILED: screenshot $shot"
    fi
    n=$((n + 1))
  done
  xcrun simctl terminate "$DEVICE" "$BUNDLE" >/dev/null 2>&1
  xcrun simctl status_bar "$DEVICE" clear >/dev/null 2>&1
}

PHONE="$(xcrun simctl list devices available | grep -E 'iPhone [0-9]+ Pro Max' | tail -1 | grep -oE '[0-9A-F-]{36}')"
if [[ -z "$PHONE" ]]; then echo "STOPPED: no 'Pro Max' iPhone simulator. Xcode > Settings > Components > iOS."; exit 1; fi
shoot_on "iPhone-6.9" "$PHONE"

IPAD="$(xcrun simctl list devices available | grep -E 'iPad Pro 13-inch' | tail -1 | grep -oE '[0-9A-F-]{36}')"
if [[ -n "$IPAD" ]]; then shoot_on "iPad-13" "$IPAD"; else echo "(No 13-inch iPad Pro simulator: iPad screenshots skipped.)"; fi

echo
echo "SCREENSHOTS DONE: $OUT_ROOT"
open "$OUT_ROOT" >/dev/null 2>&1
echo "=== finished $(date '+%H:%M:%S'). You can close this window; Claude reads the logs."
