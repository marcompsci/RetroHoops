# Retro Hoops: TestFlight release checklist

Use this for every build that goes to testers. Tick only what you have actually seen happen. A ticked box in this file is never a substitute for doing the step. Claude never uploads a build, accepts an agreement, or presses Submit without Omari saying so in chat.

Build: ______   Commit: ______   Date: ______   Checked by: ______

## A. Before building (anyone, any machine)

- [ ] `bash tools/release_gate.sh` ends with **RELEASE GATE: automated checks pass**. The logic tests, text audit and App Store audit all run; any FAIL stops the release.
- [ ] The GitHub **Release gate** workflow is green on the commit being shipped, or you know why it didn't run.
- [ ] UnityCheck (cloud) or a Unity Editor compile on the Mac shows 0 errors.
- [ ] Nothing new collects data, tracks, or adds an SDK. If something does, update `PrivacyInfo.xcprivacy`, `PRIVACY.md` and the App Privacy answers **first**.
- [ ] `BackendConfig.Url` is still empty (server off). If it isn't, stop: the App Privacy answers and the manifest must change.

## B. Build and upload (Mac, Omari's go-ahead only)

- [ ] Unity **6000.6.3f1** (not 6000.6.4f1). Xcode 26.x: uploads need the iOS 26 SDK.
- [ ] Xcode ▸ Settings ▸ Accounts shows the Apple ID and team X6LZQ3FS36 with no sign-in warning.
- [ ] Run `tools/Ship Retro Hoops.command`. Read `Logs/ship_console.log`: it must end with the upload step succeeding, not just "archive".
- [ ] The build number went up (the script raises it; App Store Connect rejects repeats).
- [ ] App Store Connect ▸ TestFlight shows the build as **Processing**, then **Ready to Test**. No "Missing Compliance" (the build already says it uses no non-exempt encryption).
- [ ] Any email from Apple about the build (ITMS-9xxxx warnings, privacy manifest notices) is read and handled.

## C. Fresh-install playthrough (real devices: iPhone, then iPad)

Delete the app first, so it's a first launch.

- [ ] Launches to the menu with no crash. The tutorial and first-visit tours show and can be skipped.
- [ ] Play a full QUICK CALL game, a THE STREETS game and a BALLER CITY visit. Leave and come back (app switcher); the game pauses and resumes.
- [ ] Settings: change difficulty and text size, use VoiceOver on a menu, check the Privacy Policy and Help & Support links open.
- [ ] **DEV: UNLOCK ALL is not in Settings** in the TestFlight build.
- [ ] Permission prompts appear only when the feature is used, with the right words. Declining each one leaves the game working:
  - [ ] Local Network: 2 PLAYER ▸ TWO PHONES.
  - [ ] Photos (add only): saving a highlight.
  - [ ] Apple Music: PHONE ▸ MUSIC ▸ MY MIX ▸ CONNECT. The text should mention MY MIX.
- [ ] **Pro (sandbox):** PLAY ▸ RETRO HOOPS PRO shows the price from the App Store, the one-time terms, Terms of Use, Privacy Policy and Restore Purchases.
  - [ ] Buy with a sandbox account. FRANCHISE unlocks.
  - [ ] Delete and reinstall, then RESTORE PURCHASES unlocks again.
- [ ] **LIVE** (two devices, two Game Center accounts):
  - [ ] FIND A GAME connects.
  - [ ] Quick chat sends a preset line, and turning QUICK CHAT off on the LIVE screen hides incoming lines.
  - [ ] Pause ▸ **REPORT PLAYER** opens Mail with the other player's Game Center name and id filled in.
  - [ ] Pause ▸ **BLOCK PLAYER** asks first, then hides their chat. After the game, REMATCH returns you to the menu instead of starting a game.
  - [ ] FIND A GAME again with the same two accounts says the blocked player was skipped.
  - [ ] LIVE ▸ UNBLOCK ALL clears the block.
- [ ] Two phones nearby: a game starts, and a custom team's name shows on the other phone as typed (clean names aren't changed).
- [ ] Airplane mode: every offline mode still plays, and LIVE says it can't connect without hanging.
- [ ] Note the device, iOS version and any crash. Check TestFlight ▸ Crashes a day later.

## D. TestFlight metadata (App Store Connect)

- [ ] **What to Test** pasted from `app-review-notes.md` and updated for this build.
- [ ] **Beta App Description**, **Feedback Email** (rodlonbell16@gmail.com), **Privacy Policy URL** and **Marketing URL** (optional) filled in under Test Information.
- [ ] **Beta App Review Information:** contact name, phone and email; "Sign-in required" **off** (no login); the notes from `app-review-notes.md`.
- [ ] Age rating answers are current (see `apple-compliance-report.md` finding 10).
- [ ] App Privacy says **Data Not Collected** (still true while the server is off).

## E. External testing

- [ ] Agreements: no **BETA_CONTRACT_MISSING** or other pending item (Business ▸ Agreements; support cases 102994756852 and 102994756299). Only Omari accepts agreements.
- [ ] Add the build to the external group and submit it for **Beta App Review** (Omari presses the button).
- [ ] After approval, the public link (https://testflight.apple.com/join/6F4CqA1R) shows the new build.
- [ ] Send testers `external-tester-guide.md` (or its text).

## F. After testers start

- [ ] Read TestFlight feedback and crashes every day for the first week.
- [ ] Answer REPORT PLAYER emails promptly, and record what you did. Apple expects timely responses to reports.
- [ ] Anything that blocks play gets fixed before the next external build.
