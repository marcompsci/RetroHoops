# App Store requirements: where Retro Hoops stands (Phase 40)

This is the checklist for App Review. **Done in code** items are checked by `tools/AppStoreAudit/app_store_audit.py` (35 checks, all passing on 7 Oct 2026) or by tests. **You** items happen in App Store Connect or on the Mac and can only be done by Omari (sign-in, agreements, bank, tax).

## Done in code / in the repo

| Requirement (App Review Guideline) | Where |
|---|---|
| Privacy policy reachable in the app and in metadata (5.1.1) | `docs/PRIVACY.md` (public on GitHub); Settings ► PRIVACY POLICY and LIVE screen |
| Support page with a way to contact you (1.5) | `docs/SUPPORT.md` (FAQ + rodlonbell16@gmail.com); Settings ► HELP & SUPPORT |
| Privacy manifest (required-reason APIs, collected data) | `Assets/Plugins/iOS/PrivacyInfo.xcprivacy` (System boot time 35F9.1 for frame timing; no data collected while the Live server is off; no tracking. Unity ships its own manifest for the engine) |
| Account / data deletion (5.1.1(v)) | No accounts. LIVE ► DELETE MY LIVE DATA erases server data |
| The one purchase (Retro Hoops Pro, non-consumable) shows its price and what it unlocks, says it's one-time, has RESTORE PURCHASES, Terms of Use and privacy links (3.1.1) | `MainMenuController.Pro.cs`, `Pro.cs`; checked by the audit |
| Free game is complete on its own; LIVE is free; Pro only adds content | By design (Phase 41) |
| No debug or DEV menus in release builds (2.3.1) | DEV tools are compiled only in development builds (audit) |
| Export compliance | `ITSAppUsesNonExemptEncryption = NO` set by the build post-processor |
| No real leagues, teams, players or brands (5.2) | Original content; the audit scans names and metadata (Phase 40 renamed four Legacy names that matched real players) |
| Metadata fits App Store limits (name 30, subtitle 30, keywords 100, promo 170) | `APP_STORE.md`; checked by the audit |
| App icon (1024, no alpha) and launch screen | Icon tests + build settings |
| Screenshots at the required sizes | `tools/App Store Screenshots.command`: iPhone 6.9" 1320×2868 and iPad 13" 2064×2752, 7 each |
| Stability (2.1) | Phase 40 release tests: damaged saves load, every mode survives random play, Full Court step < 0.5 ms |
| Accessibility basics | VoiceOver labels, left-handed controls, text size, reduce flashing (earlier phases) |

## You (App Store Connect, Mac)

1. **Agreements, Tax and Banking:** accept the Paid Apps agreement and add bank + tax info. Without it Retro Hoops Pro can't be sold or even reviewed. *(Claude can't do this.)*
2. **Upload a build:** run `tools/Ship Retro Hoops.command` on the Mac (build 3). Then wait for it to finish processing and add it to TestFlight.
3. **In-App Purchase:** create **Retro Hoops Pro** (Non-Consumable, `com.phoronomicstudios.retrohoops.pro`, $3.99, name/description, review screenshot of PLAY ▸ RETRO HOOPS PRO, review notes) and attach it to the first app version. Steps in `LIVE.md`.
4. **Game Center:** enable it on the version and create the leaderboards `retrohoops.lb.clutch`, `retrohoops.lb.gauntlet` and the recurring `retrohoops.live.monthly` (Claude will do this once you're signed in in the browser pane).
5. **App Privacy:** **Data Not Collected** for v1 (Live server off), as in `APP_STORE.md` ► App Privacy.
6. **Age rating:** answer as in `APP_STORE.md` ► Age rating (expected 4+).
7. **Metadata:** paste name, subtitle, description, keywords, What's New, Support URL and Privacy Policy URL from `APP_STORE.md`; upload the screenshots from `~/RetroHoops/AppStoreScreenshots`.
8. **Review notes:** explain Live (needs two Game Center players; offer a sandbox tester note) and that everything else is offline. Template in `SUBMISSION.md`.
9. **Live server:** v1 ships with it off (ratings kept on each phone). Switching it on later needs your Cloudflare account, the manifest entries back and new App Privacy answers.
10. **MusicKit (14 Oct 2026, the phone's MUSIC app):** ✅ ticked under App Services for `com.phoronomicstudios.retrohoops` (15 Oct 2026, with Omari's OK). App Privacy stays **Data Not Collected**. **Explicit songs (15 Oct 2026, Omari):** EXPLICIT SONGS is **off by default** and the age rating stays as it is. All 7 playlist songs are explicit, so with the switch off the playlist shows them with a note to turn the switch on; the player opts in, and iPhone Screen Time limits on explicit music still apply. App Review can still ask for a higher rating because the switch exists. If they do, answer "Profanity or Crude Humor" as Frequent/Intense (likely 17+).
11. **Device test:** play the TestFlight build on a real iPhone and iPad and work through `QA_CHECKLIST.md` before you press Submit for Review.

## Not verified yet

- Phases 39–40 haven't been compiled in Unity/Xcode on the Mac or run in the Simulator.
- Nothing has been uploaded to App Store Connect; App Review may still raise things these checks can't see.

## App Privacy answers if the Live server is switched on (moved from PRIVACY.md, 12 Oct 2026)


- App Privacy ▸ Data Collection: **Yes**.
  - **Identifiers ▸ User ID** (Game Center player ID). Linked to the user. Used for App Functionality. Not used for tracking.
  - **Usage Data ▸ Product Interaction**, or **Other Data ▸ Gameplay Content**, for Live game results and ratings. Linked to the user. App Functionality. Not used for tracking.
- `Assets/Plugins/iOS/PrivacyInfo.xcprivacy`: add the matching `NSPrivacyCollectedDataTypes` entries (`NSPrivacyCollectedDataTypeUserID`, `NSPrivacyCollectedDataTypeGameplayContent`), each with linked = true, tracking = false, and purpose `NSPrivacyCollectedDataTypePurposeAppFunctionality`.
- While `BackendConfig.Url` is empty, none of this applies, and the "Data Not Collected" answer stands.


