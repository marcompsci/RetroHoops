# Launch kit

This is everything for getting Retro Hoops from a working build to the App Store. Store text is in `APP_STORE.md`, and the step-by-step build checklist is in `RELEASE_CHECKLIST.md`.

Apple's specs below were checked against App Store Connect Help in October 2026. Re-check them before you upload, because they change from time to time.

## 1. Screenshots

**What Apple accepts**
- **iPhone 6.9" portrait:** 1320×2868, 1290×2796 or 1260×2736.
- **Count:** 1 to 10 screenshots.
- **Format:** PNG or JPEG with **no transparency**.
- If you upload a 6.9" set, smaller iPhones are scaled from it.

**How to take them in Unity**
1. Open the **Game** view. In the resolution menu, add a fixed resolution of **1320 × 2868** and select it.
2. Press **Play** and set up the moment you want.
3. Choose **Retro Hoops ▸ Release ▸ Capture Store Screenshot** (or press ⌘⇧K).
4. The images save to `StoreScreenshots/` in the project, as opaque PNGs (no transparency).
5. Every capture is also logged in `Logs/RetroHoops-release.txt`.

**Shot list and captions** (put the captions on the image or leave them off)

| # | Moment | Caption (EN) | Caption (ES) |
|---|---|---|---|
| 1 | Heated-up player mid-jumper, flames on | Heat up. Stay hot. | Ponte al rojo vivo. |
| 2 | Franchise ▸ TRADE, deal accepted | Be the GM. | Sé el gerente. |
| 3 | Dunk Contest: 360 slam, judges' cards | Win the Dunk Contest. | Gana el concurso de clavadas. |
| 4 | Full Court on a Court Builder court | Build your own court. | Construye tu cancha. |
| 5 | Kit Studio live preview | Your team. Your colours. | Tu equipo. Tus colores. |
| 6 | Alley-oop: gold pass arrow, then the slam | Throw it up. | Lánzala arriba. |
| 7 | Franchise ▸ DRAFT (scouting + lottery) | Scout. Draft. Repeat. | Explora. Elige. Repite. |
| 8 | Christmas court or THE GLITCH | Holiday Games and secrets. | Juegos festivos y secretos. |
| 9 | Music Player | Nine original chiptune tracks. | Nueve temas chiptune originales. |
| 10 | Rise hub, ALL-STAR WEEKEND | Rise from the blacktop. | Asciende desde el asfalto. |

**Rules for the images**
- Only show real gameplay.
- No device frames that show non-Apple hardware.
- No real leagues, teams, players or other games.

## 2. App preview (trailer), optional

**What Apple accepts**
- 15 to 30 seconds long.
- Up to 3 previews.
- 886×1920 portrait for 6.3" to 6.9" iPhones.
- Use footage of the app itself. Captions and titles are fine. Don't show hands or devices.

**Storyboard (about 28 s)**

| Time | Shot | On-screen text |
|---|---|---|
| 0–2 s | The title logo bobs, and CRT scanlines fade in | RETRO HOOPS |
| 2–6 s | Check ball, crossover, then a green release (SWISH) | Hold. Release on green. |
| 6–10 s | Three makes in a row: HEATING UP, then HEAT CHECK! flames | Heat up. |
| 10–14 s | A gold arrow on a cutter, then the alley-oop slam with hit-stop | Throw the oop. |
| 14–17 s | A block on defense, then the AI switches to a ZONE | The AI adjusts. |
| 17–21 s | The Arcade Ladder screen, then the boss court flickers | Find the secret boss. |
| 21–24 s | Locker Room ▸ TEAM, cycling through colours | Build your team. |
| 24–28 s | The post-game PLAY OF THE GAME replay, then the logo | No ads. Plays offline. |

**Second preview, "Play your friends" (about 30 s, Phase 33)**

| Time | Shot | On-screen text |
|---|---|---|
| 0–3 s | 2 PLAYER ▸ TWO PHONES: HOST A GAME, the friend's phone name appears | Two phones. No internet. |
| 3–9 s | The two-phone game: a steal, a fast break, a dunk with hands on the rim | Each on your own screen. |
| 9–13 s | WATCH A GAME on a third phone: "1 FRIEND IS WATCHING" toast, the same play | Friends can watch live. |
| 13–17 s | COUCH CUP bracket ▸ 2 PHONES, then the next game's tip-off card | Run a Couch Cup. |
| 17–22 s | LIVE: rating and tier, INVITE A FRIEND, a Live game's final buzzer | Play anyone. Climb to Legend. |
| 22–26 s | GAME TAPES: a tape playing, tap to 2x, the PLAY OF THE GAME | Save every big game. |
| 26–30 s | The Lighthouse Keepers' scene, then the logo | Retro Hoops. Free to play, Live included. |

Record each phone's screen separately in its own Simulator or with QuickTime from a real device. Don't film the
phones themselves: Apple wants app footage only.

**How to record it**
1. Run the Simulator build.
2. In the Simulator, choose **File ▸ Record Screen**.
3. Trim the clip and export it at 886×1920 in iMovie or QuickTime.
4. For audio, use the game's own sound (it's all synthesised and original).

## 3. TestFlight checklist

- ☐ **Product ▸ Archive** in Xcode. **Distribute App ▸ App Store Connect ▸ Upload**.
- ☐ Wait for processing. Answer export compliance automatically (Info.plist already says no non-exempt encryption).
- ☐ Install the build from the TestFlight app on your iPhone.
- ☐ Play a full first session: tutorial, Quick Call, one Rise game, then quit and relaunch. The save must still be there.
- ☐ Try a Bluetooth controller in the menus and in a game, if you have one.
- ☐ Turn on Settings ▸ HIGH FRAME RATE on a ProMotion iPhone and check that it feels smoother. Check that battery use stays reasonable.
- ☐ Spanish: switch the language, play a game, and look for clipped text.
- ☐ Background the app mid-game (home swipe, phone call). It must come back paused.
- ☐ Invite 2–5 friends as internal testers and ask them two questions: "What confused you?" and "What made you want one more game?"

## 4. Privacy answers (App Store Connect ▸ App Privacy)

| Question | Answer |
|---|---|
| Do you or your partners collect data? | **Depends on the Live server** (Phase 33 correction). With the server switched on: **Yes**, User ID and Gameplay Content, linked, App Functionality, not tracking. With it off: see `APP_STORE.md` ▸ App Privacy. |
| Tracking? | **No** |
| Third-party SDKs that collect data? | None. Game Center and StoreKit are Apple's. |
| Privacy manifest | `Assets/Plugins/iOS/PrivacyInfo.xcprivacy` (no tracking; declares User ID and Gameplay Content for the Live server) |

## 5. Launch day

- ☐ Pick the release option: manual release after approval.
- ☐ Post the 3 best screenshots and the preview clip on Instagram and TikTok.
- ☐ Pin a short "how to find the secret codes" teaser. Don't give the codes away, because players earn hints in the game.
- ☐ Read reviews daily for the first week and note bugs for a 1.0.1 build.

## 6. Press blurb (copy and paste)

> **Retro Hoops** is an original pixel-art 3v3 street basketball game for iPhone. Heat up after three straight buckets, throw alley-oops, climb a six-stage Arcade Ladder to a secret boss, and build your own team's colours, kit and logo. It plays fully offline with no ads and no tracking; the only purchase is the optional Retro Hoops Live subscription for online games. English and Spanish, touch or controller.
