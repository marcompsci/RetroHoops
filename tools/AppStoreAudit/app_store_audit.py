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
check("DEV: UNLOCK ALL only in editor / development builds", dev < 0 or "#if UNITY_EDITOR || DEVELOPMENT_BUILD" in settings[max(0, dev - 200):dev])
# 16 Oct 2026 compliance gate: DEBUG isn't a symbol Unity promises to leave out of release players; DEVELOPMENT_BUILD is.
loose = [os.path.relpath(p, GAME) for p in glob.glob(os.path.join(GAME, "Assets", "Scripts", "**", "*.cs"), recursive=True)
         if re.search(r"#if[^\n]*\bDEBUG\b", open(p, encoding="utf-8").read())]
check("No dev-only code behind #if DEBUG (use DEVELOPMENT_BUILD)", not loose, ", ".join(loose[:5]))
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

# 9. 16 Oct 2026 Apple compliance gate (docs/apple-compliance-report.md).
link = read("Assets", "Scripts", "Logic", "Net", "Link.cs")
check("1.2: names from another phone go through TextSafety (hello, setup, teams, reject)",
      all(x in link for x in ('Name = TextSafety.Field(parts[1]', 's.HostName = TextSafety.Field(', 's.GuestName = TextSafety.Field(',
                              'nickname = TextSafety.Field(', 'lastName = TextSafety.Field(', 'Error = TextSafety.Message(')))
check("1.2: shared kit and court names are filtered",
      "TextSafety.IsClean(k.name)" in read("Assets", "Scripts", "Logic", "Progression", "Kits.cs")
      and "TextSafety.IsClean(d.name)" in read("Assets", "Scripts", "Logic", "Progression", "CourtBuilder.cs"))
hud = read("Assets", "Scripts", "Gameplay", "MatchHud.cs")
game = read("Assets", "Scripts", "Gameplay", "GameSceneController.cs")
check("1.2: LIVE games offer REPORT PLAYER and BLOCK PLAYER",
      '"REPORT PLAYER"' in hud and '"BLOCK PLAYER"' in hud and "_hud.EnablePlayerSafety()" in game and "BlockOpponent" in game)
check("1.2: matchmaking skips blocked players", "LiveSafety.IsBlocked(App.Career?.live, _link.OpponentId)" in paywall)
check("1.2: blocked players' quick chat is hidden", "&& !OpponentBlocked" in game)
check("1.2: quick chat is preset messages only (one byte index)", "public static byte[] Chat(int id) => new[] { (byte)LinkMessage.Chat" in link)
check("1.2: quick chat can be turned off from LIVE", 'UiControls.ToggleRow(column, "QUICK CHAT"' in paywall)
check("1.2: published contact (SUPPORT.md and the report email match)",
      re.search(r'ReportEmail = "([^"]+)"', read("Assets", "Scripts", "Logic", "Progression", "LiveSafety.cs")).group(1) in read("docs", "SUPPORT.md"))
check("5.1.1: privacy policy describes report and block", "REPORT PLAYER" in read("docs", "PRIVACY.md") and "BLOCK PLAYER" in read("docs", "PRIVACY.md"))
music = re.search(r'"NSAppleMusicUsageDescription", "([^"]+)"', post)
check("5.1.1(ii): Apple Music purpose string matches what it's used for (MY MIX)", music is not None and "MY MIX" in music.group(1), music.group(1) if music else "missing")
check("ATS: post-process removes any arbitrary-loads exception", "NSAllowsArbitraryLoads" in post and "ats.values.Remove(key)" in post)
http_hits = []
for p in glob.glob(os.path.join(GAME, "Assets", "Scripts", "**", "*.cs"), recursive=True) + [p for p in glob.glob(os.path.join(GAME, "Assets", "Plugins", "iOS", "*.*")) if not p.endswith((".xcprivacy", ".plist"))]:
    for i, line in enumerate(open(p, encoding="utf-8", errors="ignore"), 1):
        if re.search(r'"http://', line):
            http_hits.append(os.path.relpath(p, GAME) + ":" + str(i))
check("ATS: no plain-http URLs in code", not http_hits, ", ".join(http_hits[:5]))
check("No tracking SDKs or IDFA (AppTrackingTransparency / AdSupport) in plugins", not re.search(r"AppTrackingTransparency|AdSupport|advertisingIdentifier", plugins))
check("Backend URL, if set, is HTTPS only", server_off or re.search(r'public const string Url = "https://', backend) is not None)
docs_dir = os.path.join(ROOT, "docs")
for doc in ("apple-compliance-report.md", "testflight-release-checklist.md", "app-review-notes.md", "privacy-security-audit.md", "external-tester-guide.md"):
    check("Compliance doc exists: docs/" + doc, os.path.exists(os.path.join(docs_dir, doc)))
reqs = read("docs", "APP_STORE_REQUIREMENTS.md")
check("APP_STORE_REQUIREMENTS doesn't describe the old explicit-song playlist", "All 7 playlist songs are explicit" not in reqs)

# Things only a person can confirm. They're listed so a skipped check never looks like a pass.
MANUAL = [
    "App Store Connect: App Privacy answers match PrivacyInfo.xcprivacy (Data Not Collected while the Live server is off)",
    "App Store Connect: age rating questionnaire answered for Live play with strangers and preset quick chat",
    "App Store Connect: screenshots show the current game (THE STREETS, BALLER CITY)",
    "App Store Connect: Retro Hoops Pro IAP is Ready to Submit and attached to the version",
    "Agreements: BETA_CONTRACT_MISSING resolved before external TestFlight",
    "Xcode signed in (Settings > Accounts) so the export/upload step can run",
    "A fresh-install playthrough on a real iPhone and iPad (onboarding, a game, Pro purchase in sandbox, restore, Live with a second account)",
]

fails = [r for r in results if not r[0]]
for ok, name, detail in results:
    print(("PASS  " if ok else "FAIL  ") + name + (("  (" + detail + ")") if detail and not ok else ""))
for item in MANUAL:
    print("MANUAL  " + item + "  (not checked by this script)")
print("\n%d automated checks, %d failing; %d manual items NOT verified here" % (len(results), len(fails), len(MANUAL)))
sys.exit(1 if fails else 0)
