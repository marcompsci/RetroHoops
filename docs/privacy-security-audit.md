# Retro Hoops: privacy and security audit

*16 October 2026. Checked against the repository and the build 19 archive on the Mac (`7fbffb1`: Info.plist, entitlements and privacy manifests). See `apple-compliance-report.md` for the overall result.*

## Data inventory

| Data | Where it lives | Leaves the device? | To whom | Declared |
|---|---|---|---|---|
| Career, settings, nickname, beta tester card | Save file on the device (signed with an HMAC key kept in the Keychain, `AfterFirstUnlockThisDeviceOnly`, so edits are detected; not encrypted) | Only if iCloud Sync is on: to the player's own iCloud key-value store | Apple (the player's account); the developer can't read it | PRIVACY.md |
| Game Center name and player id, scores | Game Center | Yes, Apple's service | Apple; LIVE opponents see the name | PRIVACY.md (Apple's policy applies) |
| LIVE game data (inputs, team, rating, preset chat index) | Sent peer to peer through Game Center's relay | Yes, to the opponent's device | The opponent | PRIVACY.md |
| Blocked players' Game Center ids | Save file | Only to the player's own iCloud if sync is on | — | PRIVACY.md (16 Oct) |
| A player report | An email draft the player sees | Only if the player sends it | The developer (rodlonbell16@gmail.com) | PRIVACY.md (16 Oct) |
| Two-phone game data and device name | Multipeer Connectivity | Local Wi-Fi / Bluetooth only | The nearby friend | PRIVACY.md |
| MY MIX song ids, titles and artists | Save file | MusicKit requests go to Apple | Apple | PRIVACY.md |
| Purchases | StoreKit | Apple | Apple | PRIVACY.md |
| Live server data | — | **Not used in v1** (`BackendConfig.Url = ""`) | — | PRIVACY.md describes it for a later version |

No analytics, advertising, attribution or crash-reporting SDKs: the plugins hold no `AdSupport`, `AppTrackingTransparency` or `advertisingIdentifier` (audit check). TestFlight crash reports come from Apple under Apple's terms.

## Findings

| # | Area | Severity | Finding | Status | Verified |
|---|---|---|---|---|---|
| P1 | Privacy manifest | INFO | `PrivacyInfo.xcprivacy`: tracking false, no tracking domains, no collected types, System Boot Time 35F9.1 for the game's elapsed-time clocks. Unity's manifests in build 19 add FileTimestamp (0A2A.1, C617.1), DiskSpace (E174.1), UserDefaults (CA92.1) and SystemBootTime (35F9.1). | OK | Build 19 manifests; audit |
| P2 | App Privacy label | INFO | "Data Not Collected" matches: nothing reaches the developer unless the player emails. Apple's App Privacy questions are about data collected by the app; re-read Apple's definitions when you submit. | OK (ASC answer is Omari's) | Manifest/server-off audit check |
| P3 | Purpose strings | MEDIUM → fixed | The Apple Music text named a playlist that no longer uses Apple Music. Now it names MY MIX. Local Network and Photos-add are accurate. | Fixed (next build) | Audit check |
| P4 | Consent | INFO | Each permission is requested only when its feature is opened, and every feature degrades gracefully if declined. Paid features don't depend on any permission. | OK | Code reading |
| P5 | ATS / transport | INFO | No ATS exceptions in build 19. `insecureHttpOption = 0`. No `http://` URLs in code. The server client accepts only HTTPS URLs (`Backend.IsSafeUrl`). A post-process guard now strips any arbitrary-loads key. | OK + hardened | Build 19 Info.plist; audit |
| P6 | Peer input | MEDIUM → fixed | Text from other phones was displayed raw: rich-text injection and offensive names. Now it's filtered and stripped (`TextSafety`), capped in length, with malformed messages rejected (existing parsing returns null on bad data). | Fixed | Logic tests |
| P7 | Secrets in the repo | INFO | No keys, certificates, profiles or tokens are tracked. `server/wrangler.toml` keeps secrets out (`wrangler secret put`), and its D1 id is a placeholder. The team id in `tools/apple_team.txt` isn't secret. `server/node_modules` isn't tracked. | OK | `git ls-files` scan |
| P8 | Save integrity | LOW | The save is signed (HMAC) with a Keychain secret; tampering mostly affects the player's own game. A Pro flag carried by iCloud sync is corrected by StoreKit's answer on launch (revoked or not owned → re-locked). | OK | Code reading |
| P9 | Debug code | HIGH → fixed | Dev unlocks were behind `DEBUG`; now `DEVELOPMENT_BUILD`. | Fixed | Audit check |
| P10 | Clipboard | INFO | Reads only when the player taps a paste button (kit, clutch or song link), so iOS's paste prompt applies. Writes only on COPY or SEND FEEDBACK. | OK | Code reading |
| P11 | Children | INFO | Not in the Kids category; nothing is collected from anyone. Keep Kids unselected (a purchase and a mailto link have no parental gate). | OK | APP_STORE.md |
| P12 | Deletion | INFO | No accounts. Settings ▸ RESET SAVE erases the career; deleting the app removes local data; iCloud data is removed in iOS settings. If the server is ever switched on, DELETE MY LIVE DATA appears (already built, `BackendConfig.Enabled`). | OK | Code; SUPPORT.md |
| P13 | Reports | MEDIUM (process) | Report emails contain another player's Game Center name and id. Keep them only while handling the report (the policy says so), and don't share them. | Policy updated | — |

## Not verified

The fixed build hasn't been made, so P3/P5/P9 are verified in source and by audit, not in a built Info.plist or binary. After the next build, re-run the Info.plist check on the Mac (the `plutil -p` of the archived app) and confirm DEV: UNLOCK ALL is missing in the TestFlight copy.
