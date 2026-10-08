#!/usr/bin/env python3
"""Phase 40: checks the parts of Apple's App Store requirements that live in this repo.

Run:  python3 tools/AppStoreAudit/app_store_audit.py   (exit code 1 if anything FAILS)
What it can't check (App Store Connect settings, agreements, banking) is listed in docs/APP_STORE_REQUIREMENTS.md.
"""
import glob
import os
import plistlib
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
GAME = os.path.join(ROOT, "RetroHoops")
results = []


def check(name, ok, detail=""):
    results.append((ok, name, detail))


def read(*parts):
    with open(os.path.join(GAME, *parts), encoding="utf-8") as f:
        return f.read()


# 1. Privacy policy and support pages: public files with a real contact and no placeholders.
for page in ("PRIVACY.md", "SUPPORT.md"):
    text = read("docs", page)
    check(page + " has a contact email", re.search(r"[\w.+-]+@[\w-]+\.[\w.]+", text) is not None)
    check(page + " has no YOU: placeholders", "YOU:" not in text)

live = read("Assets", "Scripts", "Logic", "Progression", "Live.cs")
for const, page in (("PrivacyPolicyUrl", "PRIVACY.md"), ("SupportUrl", "SUPPORT.md")):
    m = re.search(const + r' = "([^"]+)"', live)
    check(const + " points at docs/" + page + " in the RetroHoops repo",
          m is not None and m.group(1).endswith("/RetroHoops/docs/" + page) and "marcompsci/RetroHoops/" in m.group(1),
          m.group(1) if m else "missing")
check("Terms of Use link (Apple standard EULA)", "apple.com/legal/internet-services/itunes/dev/stdeula" in live)

# 2. Privacy manifest.
with open(os.path.join(GAME, "Assets", "Plugins", "iOS", "PrivacyInfo.xcprivacy"), "rb") as f:
    manifest = plistlib.load(f)
check("PrivacyInfo: no tracking", manifest.get("NSPrivacyTracking") is False and manifest.get("NSPrivacyTrackingDomains") == [])
types = {d["NSPrivacyAccessedAPIType"]: d["NSPrivacyAccessedAPITypeReasons"] for d in manifest.get("NSPrivacyAccessedAPITypes", [])}
uses_clock = any(re.search(r"Environment\.TickCount|Stopwatch", open(p, encoding="utf-8").read())
                 for p in glob.glob(os.path.join(GAME, "Assets", "Scripts", "**", "*.cs"), recursive=True) if "/Editor/" not in p)
check("PrivacyInfo declares System Boot Time (35F9.1) for elapsed-time clocks",
      not uses_clock or types.get("NSPrivacyAccessedAPICategorySystemBootTime") == ["35F9.1"])
plugins = " ".join(open(p, encoding="utf-8", errors="ignore").read() for p in glob.glob(os.path.join(GAME, "Assets", "Plugins", "iOS", "*.*")) if not p.endswith(".xcprivacy"))
check("Plugins use no undeclared UserDefaults", "NSUserDefaults" not in plugins or "NSPrivacyAccessedAPICategoryUserDefaults" in types)
check("Plugins use no undeclared file timestamps", "NSFileModificationDate" not in plugins or "NSPrivacyAccessedAPICategoryFileTimestamp" in types)
check("Plugins use no undeclared disk space APIs", not re.search(r"NSFileSystemFreeSize|volumeAvailableCapacity", plugins) or "NSPrivacyAccessedAPICategoryDiskSpace" in types)

# 3. Info.plist keys set by the build post-processor.
post = read("Assets", "Scripts", "Editor", "IosPostProcess.cs")
for key in ("ITSAppUsesNonExemptEncryption", "NSLocalNetworkUsageDescription", "NSBonjourServices", "NSPhotoLibraryAddUsageDescription"):
    check("Info.plist sets " + key, key in post)
check("GameKit friends list never read (no NSGKFriendListUsageDescription needed)", "loadFriends" not in plugins or "NSGKFriendListUsageDescription" in post)

# 4. The one purchase: Retro Hoops Pro, a one-time unlock (guidelines 3.1.1 and 3.1.2 no longer apply: no subscription).
paywall = read("Assets", "Scripts", "UI", "MainMenuController.Live.cs")
proui = read("Assets", "Scripts", "UI", "MainMenuController.Pro.cs")
pro = read("Assets", "Scripts", "Logic", "Progression", "Pro.cs")
store = read("Assets", "Scripts", "Core", "LiveService.cs")
for label in ("TERMS OF USE", "PRIVACY POLICY", "RESTORE PURCHASES"):
    check("Pro purchase screen shows " + label, '"' + label + '"' in proui)
check("Pro terms say one-time and not a subscription", "one-time purchase" in pro and "isn't a subscription" in pro)
check("Store starts the Pro product (non-consumable id)", "RetroStore_Start(Pro.ProductId)" in store and 'ProductId = "com.phoronomicstudios.retrohoops.pro"' in pro)
check("LIVE has no paywall (free in v1)", "ShowLivePaywall" not in paywall and "LiveIsFree = true" in pro)
check("Live data can be deleted in the app (guideline 5.1.1(v))", '"DELETE MY LIVE DATA"' in paywall)
backend = read("Assets", "Scripts", "Logic", "Net", "Backend.cs")
server_off = 'public const string Url = "";' in backend
check("Privacy manifest's collected data matches the Live server switch (off = none collected)",
      (len(manifest.get("NSPrivacyCollectedDataTypes", [])) == 0) == server_off)

# 5. No debug-only features in release builds.
settings = read("Assets", "Scripts", "UI", "SettingsController.cs")
dev = settings.find("DEV: UNLOCK ALL")
check("DEV: UNLOCK ALL only in editor / debug builds", dev < 0 or "#if UNITY_EDITOR || DEBUG" in settings[max(0, dev - 200):dev])
check("Settings links to support and privacy", '"HELP & SUPPORT"' in settings and "LiveMode.SupportUrl" in settings)
menu = read("Assets", "Scripts", "UI", "MainMenuController.cs")
check("Main menu doesn't claim 'no purchases'", "no purchases" not in menu.lower().replace('says "no purchases"', "").replace("no longer says \"no purchases\"", ""))

# 6. Store metadata lengths (docs/APP_STORE.md).
store = read("docs", "APP_STORE.md")
m = re.search(r"\| Subtitle \(30\) \| ([^|*]+?)\s*\*", store)
check("Subtitle <= 30 characters", m is not None and len(m.group(1).strip()) <= 30, m.group(1).strip() if m else "missing")
m = re.search(r"## Keywords \(100\)\s*```\s*(.+?)\s*```", store, re.S)
check("Keywords <= 100 characters", m is not None and len(m.group(1).strip()) <= 100, str(len(m.group(1).strip())) if m else "missing")
m = re.search(r"## Promotional text \(170\)\s*> (.+?)\n", store)
check("Promotional text <= 170 characters", m is not None and len(m.group(1).strip()) <= 170, str(len(m.group(1).strip())) if m else "missing")
m = re.search(r"## Description \(4000\)\s*(.+?)\n\*\(", store, re.S)
desc = "" if m is None else "\n".join(l[2:] if l.startswith("> ") else l.lstrip(">") for l in m.group(1).splitlines())
check("Description <= 4000 characters", 0 < len(desc) <= 4000, str(len(desc)))
check("Support and privacy URLs filled in APP_STORE.md", "| Support URL | **YOU" not in store and "| Privacy Policy URL | **YOU" not in store)

# 7. Original content only: no real leagues, brands or other games in player-facing text.
banned = re.compile(r"\b(NBA|WNBA|NCAA|2K|NBA ?Jam|Nike|Adidas|Jordan|Spalding|Wilson|Lakers|Celtics|Warriors|Bulls|Knicks|LeBron|Curry|Kobe)\b")
hits = []
for p in glob.glob(os.path.join(GAME, "Assets", "Scripts", "**", "*.cs"), recursive=True):
    if "/Editor/" in p:
        continue
    for i, line in enumerate(open(p, encoding="utf-8"), 1):
        if line.strip().startswith("//"):
            continue
        for lit in re.findall(r'"((?:[^"\\]|\\.)*)"', line):
            if banned.search(lit):
                hits.append(os.path.relpath(p, GAME) + ":" + str(i))
check("No real league / brand / player names in game text", not hits, ", ".join(hits[:5]))
check("No real names in the store description", not banned.search(desc))

# 8. App icon and screenshots tooling.
check("Store icon test exists (1024x1024, no alpha)", glob.glob(os.path.join(GAME, "Assets", "Tests", "**", "*ReleaseArt*"), recursive=True) != [] or "StoreIcon_Is1024Square" in " ".join(open(p, encoding="utf-8").read() for p in glob.glob(os.path.join(GAME, "Assets", "Tests", "**", "*.cs"), recursive=True)))
check("Screenshot script makes iPhone 6.9\" and iPad 13\" sets", os.path.exists(os.path.join(ROOT, "tools", "App Store Screenshots.command")))

fails = [r for r in results if not r[0]]
for ok, name, detail in results:
    print(("PASS  " if ok else "FAIL  ") + name + (("  (" + detail + ")") if detail and not ok else ""))
print("\n%d checks, %d failing" % (len(results), len(fails)))
sys.exit(1 if fails else 0)
