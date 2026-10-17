# Retro Hoops: Apple App Review and external TestFlight compliance report

*16 October 2026. Repository state: the commit that adds this file (on top of `ca33f18`). Bundle `com.phoronomicstudios.retrohoops`, version 1.0.0.*

**Source of truth:** Apple's [App Review Guidelines](https://developer.apple.com/app-store/review/guidelines/) as published on 16 Oct 2026, and Apple's [upcoming requirements](https://www.developer.apple.com/news/upcoming-requirements/) page (Xcode 26 / iOS 26 SDK for uploads since 28 Apr 2026; age-rating questions due 31 Jan 2026). Nothing here guarantees approval: App Review decides.

## Release-readiness result: **CONDITIONAL**

No code-side blocker is left open in the repository. The release is still **not ready to ship today**, because:

1. No build with these fixes exists yet. It has to be built, uploaded and processed. Build 19's upload failed: Xcode needs Omari to sign in again.
2. External TestFlight is blocked on the account side (`BETA_CONTRACT_MISSING`, Apple support cases 102994756852 and 102994756299).
3. Nobody has played the current code on a real iPhone or iPad. `QA_CHECKLIST.md` has never been run.

It is **not PASS**. Compiling and passing tests isn't a device test, and the conditions above are real. It is **not FAIL**: every finding that could be fixed in the repository has been fixed and checked as far as the cloud workspace allows.

### What "verified" means below

| Verified by | What it covers | What it doesn't |
|---|---|---|
| Logic tests (1068, `tools/LogicTests`) | Engine-free game rules, save files, the word filter, block/report logic | Unity UI, iOS plugins, device behaviour |
| UnityCheck (`ios`) | The whole Unity project compiles against Unity 6000.6.3f1's reference assemblies, iOS defines (0 errors, 0 warnings) | Running anything; Xcode's Swift/ObjC compile |
| App Store audit (`tools/AppStoreAudit`) | Static checks of repo files: manifests, purpose strings, links, docs | App Store Connect settings |
| Build 19 archive on the Mac (`7fbffb1`) | Info.plist, entitlements and privacy manifests as Xcode really produced them | Changes made after `7fbffb1` |
| Not verified | Device play, Live between two real accounts, the sandbox purchase on this build, App Store Connect answers | — |

## Phase 1: Discovery

**Architecture.**
- **Engine:** Unity 6000.6.3f1 (iOS/iPadOS 15+, iPhone and iPad, Mac "Designed for iPad").
- **Game rules:** an engine-free C# assembly (`Scripts/Logic`, about 68K lines) holds every rule, the simulation, progression and the save codec. Unity front ends sit on top (`Gameplay`, `UI`, `City`, `Core`).
- **iOS plugins** (`Assets/Plugins/iOS`):
  - StoreKit 2 for the purchase (`RetroStore.swift`).
  - Game Center: sign-in, leaderboards, and real-time matchmaking for LIVE (`CallerGameCenter.mm`, `RetroLive.mm`).
  - Multipeer Connectivity for two phones nearby (`RetroLink.mm`).
  - iCloud key-value save sync (`RetroCloud.mm`) and a Keychain-held save key (`RetroKeychain.mm`).
  - MusicKit for MY MIX (`RetroMusic.swift`).
  - Built-in speech voices (`RetroSpeech.mm`), the share sheet (`RetroShare.mm`), haptics, and the TestFlight receipt check (`RetroBeta.mm`).
- **Server:** a Cloudflare Workers server (`server/`) exists but is **switched off** in v1 (`BackendConfig.Url = ""`), so the app makes no network requests of its own. Live play goes through Apple's Game Center relay.
- **Data:** saves stay on the device, plus the player's own iCloud if they turn sync on. There are no accounts of our own, no ads, no analytics and no tracking SDKs.

**Highest risks found before any change:**
1. Peer-supplied text in LIVE was shown unfiltered, and there was no block or report.
2. Dev cheats were behind `#if DEBUG`.
3. The Apple Music purpose string was out of date.
4. The age-rating notes said "no chat".
5. The screenshots are out of date.
6. External TestFlight is blocked by an agreement.

## Findings

Severity is BLOCKER / HIGH / MEDIUM / LOW, plus INFO for areas checked and found compliant. "Implemented: yes" means the change is in this commit.

| # | Guideline | Where | Severity | Finding | Remediation | Implemented | Verified by |
|---|---|---|---|---|---|---|---|
| 1 | 1.2 | `Logic/Net/Link.cs` | HIGH | LIVE matches you with strangers through Game Center. The other phone's player name, team city, nickname, abbreviation, player names and even its "reject" message were shown exactly as received. A modified client could put anything on screen. | New `TextSafety` on-device word filter (English and Spanish; number-for-letter swaps; stretched letters; whole-word and prefix rules to avoid false positives like "Scunthorpe" and "Nigeria"). It's applied to every name read from another phone; reject messages that aren't clean become a neutral line. Clean names are kept exactly as sent. | Yes | `ComplianceGateTests` (8 tests): catches, false-positive list, every built-in name clean, Link round trips. UnityCheck 0 errors. Audit check "names from another phone go through TextSafety". |
| 2 | 1.2 | `Gameplay/MatchHud.cs`, `GameSceneController.cs`, `UI/MainMenuController.Live.cs`, `Logic/Progression/LiveSafety.cs` | HIGH | No way to block or report an abusive LIVE opponent. | **BLOCK PLAYER** (pause menu and end screen): stores the Game Center player id on the device, hides that player's quick chat at once, refuses rematches, and makes matchmaking drop them. **UNBLOCK ALL** on the LIVE screen. **REPORT PLAYER** opens an email to the published support address, pre-filled with the player's Game Center name, id, the version and the time; the player sends it themselves. Quick chat can be switched off on the LIVE screen. | Yes | Logic tests (block dedupe, cap, save/load, mailto escaping). UnityCheck. Audit checks. **UI not run on a device.** |
| 3 | 1.2, security | `TextSafety.Plain` | MEDIUM | Peer text could carry TextMeshPro rich-text tags (`<size=900>`, `<color>`) and restyle or flood the screen. | `<`, `>` and control characters are stripped; names are capped at 24 characters and messages at 160. | Yes | Logic tests. |
| 4 | 1.2 | Live design | INFO | Quick chat is a one-byte index into 8 fixed phrases, so no free text goes between players. Live teams are built-in League teams. Your typed nicknames are never sent: opponents see your Game Center name. | None needed. | — | Code reading; audit check "quick chat is preset messages only". |
| 5 | 1.2 | Process | MEDIUM (open) | Apple asks for "timely responses" to reports. Reports arrive as email at rodlonbell16@gmail.com; there is no moderation server, and none is claimed. | Omari must watch that inbox and answer reports, and block or document repeat abusers. Game Center names themselves are Apple's. | Process only | — |
| 6 | 1.2 | `Kits.cs`, `CourtBuilder.cs` | LOW | Kit and court share codes carry a short player-chosen name that another player imports. | Imported (and own) kit and court names go through the filter (fallbacks "KIT" / "My Court"). | Yes | Logic tests. |
| 7 | 2.1, 2.3.1 | `SettingsController.cs`, `App.cs`, `GameSceneController.cs` | HIGH | DEV: UNLOCK ALL (free currency and every cosmetic), a ball-knock debug key and content validation were behind `#if UNITY_EDITOR \|\| DEBUG`. Unity doesn't document `DEBUG` as absent from release players. | Changed to `UNITY_EDITOR \|\| DEVELOPMENT_BUILD`, which Unity defines only for Development Builds. The App Store build uses `BuildOptions.None` with development off. | Yes | Audit check "No dev-only code behind #if DEBUG". UnityCheck. Not checked in a built binary yet. |
| 8 | 5.1.1(ii) | `Editor/IosPostProcess.cs` | MEDIUM | The Apple Music permission text said "Play the Retro Hoops playlist from your Apple Music". That playlist is now the game's own offline beats; Apple Music is only used by MY MIX. | "Play songs you pick from your Apple Music in the game's MY MIX while you play." | Yes | Audit check. Needs the next build. |
| 9 | ATS / security | `IosPostProcess.cs` | INFO + LOW | Build 19's Info.plist has **no** `NSAppTransportSecurity` exceptions, and `insecureHttpOption` is 0 (not allowed). | Added a guard that strips any arbitrary-loads exception if one ever appears; there are no plain-http URLs in code (audit). | Yes | Build 19 Info.plist (Mac); audit checks. |
| 10 | 2.3.6, age rating | `RetroHoops/docs/APP_STORE.md` | MEDIUM (open) | The questionnaire notes said "There's no chat or messaging" and "User-generated content: No". Quick chat (preset) and shared kit/court names exist. | Notes corrected. Recommended answers: Messaging and Chat **Yes (preset only)**, User-Generated Content **Yes** (filtered, report/block), no Social Media capability, content questions None. **Omari must update the answers in App Store Connect.** | Doc yes; ASC no | — |
| 11 | 2.3.3 | App Store Connect | MEDIUM (open) | The screenshots predate THE STREETS and BALLER CITY. App Store **submission** risk; not a TestFlight blocker. | Run `tools/App Store Screenshots.command` on the Mac (with Omari's go-ahead) and upload. | No | — |
| 12 | 2.1 | Whole app | HIGH (open) | Nothing since review #3 has been played on a device by anyone, and `QA_CHECKLIST.md` has never been run. Crashes on device can't be ruled out from the cloud. | Fresh-install playthrough on iPhone and iPad (see `testflight-release-checklist.md`). Check TestFlight crash reports for builds 11–12. | No | — |
| 13 | TestFlight | App Store Connect | BLOCKER (external only) | `BETA_CONTRACT_MISSING` stops external testing (support cases 102994756852 and 102994756299). Internal testing isn't affected. | Omari follows up with Apple, or checks Business ▸ Agreements for anything pending. Claude must not accept agreements. | No | — |
| 14 | TestFlight | Mac / Xcode | BLOCKER | The last upload failed ("exportArchive Failed to Use Accounts"). No build containing these fixes has been made. | Omari signs in again in Xcode ▸ Settings ▸ Accounts, then runs **Ship Retro Hoops** (a new build, 20+). Upload only with his say-so. | No | — |
| 15 | 3.1.1 | `RetroStore.swift`, `Pro.cs`, `MainMenuController.Pro.cs` | INFO | Retro Hoops Pro: a non-consumable StoreKit 2 purchase. Only verified transactions are accepted (revocations re-lock), it shows the App Store's localized price, terms say one-time and not a subscription, and Restore Purchases, Terms of Use and Privacy Policy links are on the screen. No external links, codes or crypto. Hoops Pass items are earnable for free. No loot boxes. Secret codes never unlock Pro content. | None. | — | Code reading; audit checks. |
| 16 | 3.1.2 | `Live.cs` | LOW | Old Live-subscription terms text and a subscription check remain in code, but are never shown (`LiveIsFree = true`, server off). | Leave it (it would come back with a server) or remove later. Don't describe a subscription in metadata. | No | Audit "LIVE has no paywall". |
| 17 | 5.1.1(i)/(v) | `PRIVACY.md`, `SUPPORT.md` | LOW | There are no accounts, so in-app account deletion doesn't apply. SUPPORT pointed to DELETE MY LIVE DATA, which only appears when the server is on. The policy didn't cover report/block. | SUPPORT corrected; the policy now covers block (device-only), report (user-sent email, kept only to handle it), the word filter, and the Apple Music change. | Yes | Audit checks. |
| 18 | 5.1.1, 5.1.2, App Privacy | `PrivacyInfo.xcprivacy`, ASC | INFO | No tracking and no collected data types. The game's own required-reason API (elapsed-time clocks) is declared as System Boot Time 35F9.1. Unity's two framework manifests in build 19 declare their own (FileTimestamp, DiskSpace, UserDefaults, SystemBootTime). "Data Not Collected" stays correct while the server is off. A report email is something the player chooses to send, like the support email. | Keep the App Privacy answers as they are. Re-read Apple's definitions when submitting. | — | Build 19 manifests on the Mac; audit. |
| 19 | 5.1.1(ii)/(iv) | Purpose strings | INFO | Local network (two phones), add-only Photos (save a highlight GIF), and Apple Music (MY MIX) are each asked for only when the feature is used. Everything works if they're declined. | None. | — | Build 19 Info.plist; code reading. |
| 20 | 1.3 | Store listing | LOW | The game isn't in the Kids category: Games ▸ Sports / Arcade. It has a purchase and a mailto link without a parental gate, so don't select Kids. | Keep the current category. | — | `APP_STORE.md`. |
| 21 | 2.3.1 | Review notes | LOW | Secret codes (Settings ▸ SECRETS ▸ ENTER A CODE) unlock fun toggles and hidden teams and courts. They're in plain sight, but reviewers should hear about them so they don't read as hidden features. | Mentioned in `app-review-notes.md`. | Yes | — |
| 22 | 2.3.7/2.3.8 | Metadata, art | INFO | Original names only; the audit's real-brand scan is clean. The metadata length checks pass. | None. | — | Audit. |
| 23 | 4.2 / 2.5.2 / 4.7 | App | INFO | A full native game. No downloaded code, mini apps, emulators or web views. | None. | — | Code reading. |
| 24 | 4.8 | Sign-in | INFO | Game Center only, optional, and not an account of ours. Sign in with Apple isn't required. | None. | — | Code reading. |
| 25 | 5.2 | Content | INFO | Original art and sound, LiberationSans with its licence in the credits, and no real teams, players or brands. MY MIX plays the player's own Apple Music through MusicKit (licensed by Apple; Screen Time explicit limits apply; EXPLICIT SONGS is off by default). | None. | — | Audit; docs. |
| 26 | 5.4 | — | N/A | No VPN or network extension. | — | — | — |
| 27 | Upload requirements | Build 19 | INFO | Xcode 26.6 (2660), iOS 26.5 SDK, minimum iOS 15.0, arm64. This meets the 28 Apr 2026 SDK rule and the iOS 13+ minimum. `ITSAppUsesNonExemptEncryption = NO` is right: only Apple's own HTTPS/TLS is used. | Keep shipping from Xcode 26.x. | — | Build 19 Info.plist. |
| 28 | 1.2 filter limits | `TextSafety.cs` | LOW | A word filter can't catch everything (letters spelled out with spaces, look-alike letters from other alphabets, new slang). | Block and report cover what the filter misses. Extend the list when a report shows a gap. | Documented | Test notes the spaced-letter limit. |

## Phases 3–9 summaries

- **Functionality and crashes:** 1068 logic tests pass, and UnityCheck compiles all five assemblies with 0 errors. The save decoder rejects bad or old data without crashing (existing tests). Network failures fall back offline: the server is off, and Game Center or matchmaking loss shows a message. No device run of this code, so crash-freedom isn't claimed.
- **Privacy and security:** see `privacy-security-audit.md`.
- **Social and multiplayer safety:** findings 1–6 and 28.
- **Payments:** finding 15. No external checkout, no steering, no subscriptions.
- **Xcode and TestFlight:** findings 13, 14 and 27, and `testflight-release-checklist.md`.
- **TestFlight review package:** `app-review-notes.md` (What to Test, Beta App Description, review notes) and `external-tester-guide.md`.
- **CI:** `tools/release_gate.sh` and `.github/workflows/release-gate.yml` run the logic tests, the text audit and the App Store audit (now 58 checks). The audit lists the 7 things it can't check as **MANUAL, not verified**. Unity and Xcode steps are printed as **NOT RUN**, never as passed. The workflow hasn't run on GitHub yet; it runs on the next push.

## What Omari needs to do (in order)

1. Read this report and the changed docs. The new in-game safety features change what players see.
2. Sign in again in Xcode ▸ Settings ▸ Accounts. Then say the word and Claude runs **Install on iPhone**, **Push to GitHub** and **Ship Retro Hoops** (build 20+).
3. Play the fresh install (checklist). Include one LIVE game between two Game Center accounts, using REPORT PLAYER and BLOCK PLAYER.
4. In App Store Connect:
   - Update the age-rating answers (finding 10).
   - Paste the What to Test and review notes.
   - Keep App Privacy as Data Not Collected.
5. Chase `BETA_CONTRACT_MISSING` with Apple before adding external testers.
6. Before App Store submission (still on hold): new screenshots, Pro IAP attached to the version, and the full QA checklist.
