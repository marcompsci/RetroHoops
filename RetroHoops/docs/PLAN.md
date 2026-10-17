# Implementation plan & phase log

| Phase | Scope | Status |
|---|---|---|
| 1 | Audit, architecture, data model, default content, validator, scene skeleton, boot flow, menu shell | **Done** (logic tests pass in .NET; Unity compile pending first open) |
| 2 | Court rendering, player sprites, movement, ball possession, camera, joystick + action buttons | **Done** (99 logic tests pass in .NET; Unity compile pending first open) |
| 3 | Passing, shot meter + shot model, scoring, game/shot clocks, possession, basic AI | **Done** (133 logic tests pass in .NET, incl. full AI-vs-AI games at every difficulty; Unity compile pending first open) |
| 4 | Defense (steal/contest/block), rebounds + box-out, match state machine, post-game stats & rewards | **Done** (logic tested; Unity compile pending) |
| 5 | Quick Call team select, Practice Lab drills, Rise Mode season/standings/bracket, versioned save | **Done** (logic tested; Unity compile pending) |
| 6 | Locker Room, upgrades, cosmetics, Settings (audio, haptics, shake, UI scale, colourblind, difficulty, reset), audio hooks | **Done** (logic tested; Unity compile pending) |
| 7 | Test pass, performance cleanup, full README/DESIGN, manual QA checklist, final report | **Done** (192 logic tests pass; QA checklist written but not yet run) |
| 18 | One-command Simulator run, power/battery (Low Power + thermal → 60 fps, SHOW FPS, profiler markers, pooled sparks), onboarding (Coach Dee welcome, one-time coach tips) and juice (button pops, panel pops, jingles), Season 3 (Midnight Tide rival, story chapter 3, 3 courts, kits, 2 celebrations, 2 dribble moves, 3 badges, 2 codes) | **Done** (366 logic tests pass; 0 errors via tools/UnityCheck; Simulator run in progress) |
| 17 | Party games (H-O-R-S-E vs CPU/friend, 21, Around the World), Dynasty mode (aging, retirements, Hall of Fame, draft, league history), shareable highlight GIFs, iPhone build support (release log, UnityCheck) | **Done** (357 logic tests pass; compiles against Unity 6000.6.3f1 reference assemblies with 0 errors via tools/UnityCheck; not yet run on iPhone) |
| 16 | Create-a-team (colours, jerseys, shorts, shoes, logo); Season 2 (Sundown Syndicate rival, 3 courts, story chapter 2, kits, 7 badges); AI defensive schemes; 1-on-1; Shootout; 8-team Caller Cup; App Store launch kit | **Done** (342 logic tests pass; Unity changes not yet compiled; iPhone build in progress with Omari) |
| 15 | HEAT CHECK, alley-oops, hit-stop, heavy haptics, 120 Hz; CRT filter, title demo, pixel wipes, announcer voice; secret codes, hidden courts/teams, big heads, Arcade Ladder + boss; controller menus and prompts | **Done** (324 logic tests pass; Unity changes not yet compiled; iPhone build still to do with Omari) |
| 14 | Instant replay + play of the game, 3-Point Contest and Lockdown drills, King of the Court mode, Spanish language (partial) | **Done** (296 logic tests pass; Unity changes not yet compiled; iPhone build still to do with Omari) |
| 13 | Recruit your crew, Neon Static rival + Rival Challenge, story scenes, 16 badges + trophy room, iOS readiness check | **Done** (278 logic tests pass; Unity changes not yet compiled; iPhone build still to do with Omari) |
| 12 | Create-a-player, accessibility (left-handed, large buttons, tap-to-shoot, reduce motion), dunk/layup leaps, animated crowd, 3 music tracks + stingers, records/history/seasons | **Done** (262 logic tests pass; Unity changes not yet compiled in Unity) |
| 11 | How to Play tutorial, opt-in Game Center (GameKit bridge), local 2-player, Daily Challenge | **Done** (243 logic tests pass incl. a scripted tutorial run and 2-player sim tests; Unity UI and GameKit not yet run) |
| 10 | Rename to Retro Hoops; content: 2 courts + crews, 4 event cards, 7 cosmetics, First Call Classic tournament | **Done** (224 logic tests pass; Unity UI not yet seen running) |
| 9 | Polish: celebrations, dribble moves, pixel bursts, dunk SLAM + shake, crowd ambience, score bounce, reward count-up | **Done** (207 logic tests pass; not yet seen running) |
| 8 | Release prep: app icon, launch image, release settings, iOS build tooling, Info.plist, privacy manifest, App Store kit | **Done** (198 logic tests pass; icon previewed; no build run yet) |

## Phase 1 — what exists

- **Logic (engine-free):** `AttributeSet`/`RatingScale` (1–99), `RgbColor`, all definitions
  (Archetype, Player, Team, Court, GameRules, Difficulty, Upgrade, Cosmetic, SeasonConfig),
  `DefaultContent` (12 archetypes, 8 league teams × 4 players, 3 Blacktop crews, player crew with Rook,
  12 courts, 2 rule sets, 3 difficulties, 8 upgrades, 16 cosmetics, 1 season), `ContentValidator`,
  `Scoring` (1/2-point zones, target score, clock, sudden death, win-by-two), `MatchRequest`,
  `StableHash`/`SeededRandom`, `PixelCanvas`, `LogoGenerator`, `BackdropGenerator`.
- **Unity layer:** ScriptableObject wrappers + `ContentDatabase`; `App`, `SceneFlow` (fade + input
  blocker + double-load guard), `BootController`; `UiKit`, `Theme`, `SafeAreaFitter`, `ScreenBase`;
  Main Menu, Game (match preview), Rise Mode (league preview), Locker Room (Rook card), Settings (credits).
- **Editor:** `ProjectSetup` (auto on first open + menu).
- **Tests:** 42 logic tests (EditMode/Logic), 3 editor checks (EditMode/Unity), 2 PlayMode tests.

## Phase 2 — what exists

- **Logic:** `Vec2`; `CourtGeometry` (15 x 11 m half court, hoop, arc + straight corners, paint,
  check spot, 1/2-point zone test, bounds clamp); `Movement` (rating-based top speed, dribble
  penalty, accel/brake, 8-way facing, arrive steering, body separation); `BallState`/`BallPhysics`
  (dribble curve, loose-ball gravity/bounce/friction, pickup rule); `Formation` (spacing spots,
  guard-between-man-and-hoop); `MatchSimulation` (6 players, check-ball setup, fixed-step update,
  possession + control switching, loose-ball pickups, frozen clocks until Phase 3, event list);
  `InputBuffer`, `JoystickMath`, `CameraMath` (follow, clamp, integer pixel zoom).
- **Art (procedural):** `CourtGenerator` (asphalt or hardwood floor, paint, lines, crowd strip),
  `CharacterSpriteGenerator` (16x24 sheets: front/back/side x idle 2 + run 4, 6 hair styles,
  3 builds, 3 heights, 6 skin tones, 1px outline), `PropSpriteGenerator` (ball, shadow, ring, hoop).
- **Unity:** `GameSceneController` (60 Hz fixed step, keyboard/gamepad fallback, pause on
  background), `MatchArt`, `PlayerView`, `BallView`, `CourtCameraRig`, `CourtSpace`, `MatchHud`
  (scores, clocks, possession highlight, pause menu, toasts), `TouchControls` + `VirtualJoystick`
  + `TouchActionButton` (floating stick, hold/release buttons, "SOON" feedback for actions that
  arrive in Phases 3–4).
- **Tests:** +57 logic tests (Phase2Tests.cs), +3 PlayMode tests (GameSceneTests.cs).
- Placeholder behaviour to be replaced in Phase 3: AI teammates hold spacing spots, defenders
  shadow their matchup, everyone chases loose balls; a defender picking up a loose ball takes
  possession immediately (no clear-the-ball rule yet).

## Phase 3 — what exists

- **Shot model** (`ShotModel`, all numbers in `ShotTuning`): hold-to-charge meter (0.9 s jumpers,
  0.6 s layups/dunks), five timing grades with a green window that widens with Shooting,
  layup / dunk / mid-range / arc profiles, and
  `makeChance = base + timing + openLook + hotStreak + clutch − distance − contest − fatigue`,
  clamped to 2–96 %. Green is best but not guaranteed (debug flag `debugGreenAlwaysMakes`).
  Callouts: GREEN, CLEAN LOOK, CONTESTED, TOO EARLY, TOO LATE. Seeded, so tests are exact.
- **Pass model** (`PassModel`, `PassTuning`): best-target scoring (openness, stick direction,
  distance, lane risk), automatic chest/bounce choice, interception only when a defender is
  genuinely in the lane (defense vs. playmaking), and a receiver arrow while you have the ball.
- **Match flow** (`MatchSimulation`): check-ball → live → dead ball → final; game clock and
  14 s shot clock (paused while a shot is in the air, reset on rim contact); violations;
  1/2-point scoring from the release spot; possession change after makes; clear-the-ball rule
  after steals and defensive rebounds; misses carom off the rim into predictable loose balls;
  offensive rebounds keep possession; sudden death if tied at the horn; box score
  (`MatchStats`: points, FG, arc, assists, rebounds, steals, turnovers) and player of the game.
- **Control model:** the human always controls their own player (slot 0). With a teammate
  holding the ball, PASS becomes **ASK** and the AI teammate passes it back.
- **AI** (`AiBrain`): utility decisions every reaction-time interval — shoot vs. the difficulty's
  quality bar, pass (friendly AI looks for the human), drive when the lane is open, hold; clears
  the ball when required; give-and-go and backdoor cuts; on-ball defense that closes out on
  shooters; help defense on drives (late rotations via error rate); rebound crashing by
  archetype. Difficulty changes reaction time, decision quality, shot selection, release
  accuracy, and mistakes only.
- **Unity:** shot meter beside your player, receiver arrow, arms-up shooting sprite frame,
  contextual buttons (SHOOT/CLEAR, PASS/ASK; defense actions dimmed until Phase 4), toasts
  (GREEN, SWISH +2, PICKED OFF, BOARD!, TAKE IT BACK, SHOT CLOCK), final card with Rematch/Home.
- **Practice Lab:** opponents are passive, no shot clock, you keep the ball after scoring.
- Balance snapshot (20 AI-vs-AI games per level, AI team only): FG% Rookie 33 / Caller 38 /
  Legend 51; 0 shot-clock violations by the AI.

## Phases 4–6 — what exists

- **Defense & plays** (`DefenseAndPlays.cs`, `DefenseTuning`): matchups, steal with cooldown and stun on a miss,
  jump to contest and block, switch onto the handler, box-outs and weighted rebounding, stamina, and
  three play calls (Pick & Roll, Give & Go, Clear Out). AI defenders jump at shooters and gamble for steals
  by archetype; AI offenses run pick-and-rolls.
- **Progression** (`Progression/`): `MatchSummary`, `Rewards`, `Career` (apply once per match id, upgrades,
  cosmetics, practice bests), `SeasonEngine` (8 teams, 10 games, tie-breakers, playoffs), `RiseEngine`
  (circuit, season, playoffs, energy, chemistry, six event cards), `PracticeSession` (three drills).
- **Save** (`Save/`, `Core/SaveStore`): MiniJson + versioned `SaveCodec`; atomic writes; corrupt files backed up.
- **Audio** (`AudioSynth`, `AudioManager`): 12 synthesised SFX and a 32-beat chiptune loop.
- **Unity:** defense buttons, CALL menu, jump lift and BOX OUT line, post-game box score with player of the game
  and rewards, Rise hub (stage, objective, energy, chemistry, next game, standings, bracket, event modal),
  Quick Call and Practice pickers, Locker Room tabs, full Settings, iOS haptics plugin, colourblind jersey
  patterns, cosmetic jersey/shoe/banner colours in games.

## Phase 7 — what was done

- Removed empty folders; throttled the per-frame HUD info string; added PlayMode tests that every menu
  scene and a practice drill build without errors.
- Rewrote README (setup, controls, iOS build, architecture, limitations, asset disclosure) and completed DESIGN.
- Added `docs/QA_CHECKLIST.md` for a manual pass on device.

## Phase 8 — release prep

- **Art:** `AppIconGenerator`, an original 64x64 pixel basketball with gold "signal" arcs on a dithered sunset, scaled to an
  opaque 1024x1024 icon. The launch image is the main-menu backdrop with the ball emblem and no text.
- **Editor:** `ReleaseTools`, with menu items to generate and assign the icon and launch image, apply release Player Settings
  (1.0.0 / build 1, iOS 15+, portrait, full screen, no Unity splash), and build Simulator or Device Xcode projects.
  The same builds run headless via `tools/build_ios.sh`. `IosPostProcess` sets the Info.plist keys.
- **iOS:** `PrivacyInfo.xcprivacy` (no tracking, no data collected).
- **Docs:** `docs/APP_STORE.md` (metadata within Apple's character limits, privacy policy text, privacy label, age rating,
  export compliance, screenshot plan, review notes) and `docs/RELEASE_CHECKLIST.md`.
- **Version:** the menus now show the Player Settings version (`Application.version`) instead of a hard-coded string.

## Phase 9 — polish (presentation only, no gameplay or balance changes)

- `Logic/Feel/Flair.cs`: celebrations (Fist Pump, Call It, Shimmy Step) and dribble moves (Crossover, Hesi Hop,
  Spin Cycle) as pure pose functions chosen by the equipped cosmetic. A sharp cut (>110°) with the ball triggers
  your move, with a 0.7 s cooldown. Also deterministic `Bursts` for sparks.
- `AudioSynth.CrowdAmbience`: a 4 s seamless crowd loop under matches. Music ducks while it plays.
- Unity: `PixelBursts` (pooled 1-px sparks: white on makes, gold on greens, bigger on dunks; dust on blocks;
  cyan on steals), SLAM! toast and camera shake on dunks, score bounce in the HUD, post-game rewards count up.
- `BallState.ShotType` records the kind of shot in the air, for presentation.
- First-compile fixes from Unity 6000.6.3f1: a `PlayerInput` name clash, a definite-assignment error in Quick
  Call, and the deprecated `DEVELOPMENT_BUILD` define replaced by `DEBUG`. The overlays now scroll, and the keyboard
  controls show on screen.

## Phase 10 — Retro Hoops + content

- Renamed the game to **Retro Hoops** everywhere players see it: the app name (`productName`), Settings credits,
  Editor menus, docs, and the App Store kit. The placeholder bundle ID is now `com.retroball.game`. Code
  folders and namespaces keep their `CallerRetroBall` names so Unity references don't break.
- **Blacktop Circuit:** two new courts (Rooftop Ring, Boardwalk Slab) and two crews (Rooftop Relay, Boardwalk
  Bandits), each a bit tougher than the last. The circuit is now five games.
- **Rise:** four new event cards (Rain Delay, Highlight Tape, Trash Talk, Shoe Drop), ten in total.
- **Cosmetics:** Arcade Mint and Gold Rush jerseys, Glacier Highs and Cosmic Runners shoes, a Boardwalk Pennant
  banner, the Raise the Roof celebration, and the Behind the Back dribble move (both animated).
- **First Call Classic** (`ClassicEngine`, `GameMode.Tournament`, saved in `career.classic`): crew vs three
  league teams drawn by seed in a 1v4 / 2v3 bracket. Other games are simulated. The title bonus is 200 SP
  (smaller than the Cup), and titles count in the Locker Room.

## Phase 11 — tutorial, Game Center, 2-player, Daily Challenge

- **Engine:** `MatchSimulation` supports a second human (`MatchSetup.SecondHuman`, `Step(dt, p1, p2)`,
  `HumanIndexOf`, `IsHumanControlled`). One-player behaviour is unchanged: every earlier seeded test still passes.
- **Tutorial** (`TutorialSession`, `GameMode.Tutorial`): eight steps that advance only when the player does the
  thing. On offense the ball is handed back; for defense the ball goes to the other side. Offered on first launch,
  replayable from PLAY and Settings, with a 100 SP one-time reward.
- **Daily Challenge** (`DailyChallenges`, `GameMode.Daily`): seeded by calendar day, six goal types, streaks, and
  a capped streak bonus, once per day. Saved in `career.daily`.
- **2 Player** (`GameMode.Versus`): P1 uses touch/WASD and the first controller, P2 uses arrows + numpad or a
  controller. The camera follows the ball, P2 has a cyan ring, there are no rewards, and nothing counts toward the career.
- **Game Center:** `CallerGameCenter.mm` (GameKit) plus `GameKitGameCenterService`. It's opt-in in Settings, and
  achievements and leaderboards come from `Achievements`. The post-processor adds GameKit and the capability. The IDs
  to create are in `docs/GAME_CENTER.md`.
- **Menus:** PLAY now opens Quick Call, Daily Challenge, 2 Player, First Call Classic, and How to Play.

## Phase 12: create-a-player, accessibility, animation & sound, stats

- **Create-a-player** (Locker Room ▸ CREATE): skin tone, hair, hair colour, build, height, number, and style of play
  (archetype baseline −10, like Rook), with a live sprite preview. `PlayerCreator` builds the human's `PlayerDef`.
  Training upgrades apply on top. It's used in Rise, the Classic, Practice, and How to Play. "Go back to Rook" is
  always available.
- **Accessibility** (Settings): left-handed layout, large buttons (+25%), tap to shoot (`TapShoot`), and reduce
  motion (no shake, sparks, score bounce, or crowd bob). VoiceOver labels were **not** added: uGUI has no
  screen-reader support, and Unity's newer accessibility API wasn't verified for this project.
- **Animation & sound:** dunk and layup leaps (`Flair.Leap`); an animated crowd (`CrowdGenerator`, fans cheer and
  groan, they skip banner spots, and the court stops drawing static people in matches); three music loops
  (menus / matches / Rise hub); stingers for HEATING UP (3 makes in a row), ON FIRE (4+), dunks, greens, records,
  and titles. These are musical stings, not a spoken announcer.
- **Stats & records** (`Records`): single-game bests with NEW RECORD callouts (announced after the first game),
  current and best win streak, the last 20 games, and per-season Rise history with the result. All of it is shown
  in Locker Room ▸ STATS.

## Phase 13: crew, rivals, story, badges, iOS readiness

- **Recruit your crew** (`CrewEngine`): beating a street crew in Rise unlocks its three players. Beating a league
  team unlocks its bench player, the fourth roster player who never plays for them, so a recruit is never also
  on the other side. A signing fee scales with overall rating; afterwards, swaps are free. Two teammate spots,
  saved in `rise.teammates/recruitable/signed`. Shown in Rise hub ▸ YOUR CREW.
- **Rival:** Neon Static (new `TeamTier.Rival`, court The Static Lot, led by Vex Marlowe). A Rival Challenge
  (`GameMode.Rival`) appears once per season from week 5. It isn't in the standings, and a win pays +150 SP
  and +50 fans.
- **Story** (`Story`, `StoryView`): seven original scenes with Coach Dee and Vex (intro, circuit cleared, rival
  intro / beaten / lost, playoffs, champions). Each plays once in the Rise hub, with a typewriter box, portraits,
  and SKIP.
- **Badges** (`Badges`): 16 in-game badges, announced once after games; Locker Room ▸ TROPHY shows titles
  and all badges.
- **Retro Hoops ▸ Release ▸ Check iOS Readiness:** iOS module, Mac/Xcode, scenes, bundle ID, team, app name, and
  icon, with an automatic fix for the icon and release settings.

## Phase 14: replay, drills, King of the Court, Spanish

- **Instant replay** (`ReplayRecorder`): a 5-second ring buffer of positions and ball state, recorded after every
  step. Big plays (dunks, green releases, blocks, steals) offer a REPLAY button; the replay plays at half speed
  with a letterbox (tap to skip). The best play is kept as **PLAY OF THE GAME** on the post-game screen.
- **Drills:** 3-POINT CONTEST (60 s, arc shots only, a rotating gold money spot is worth 2) and LOCKDOWN
  (defense: stop 6 possessions). Bests are saved.
- **King of the Court** (`KingEngine`, `GameMode.King`): pick a league team, then play short games (first to 11
  or 90 s) against the others in a shuffled order. Each win adds to the streak and pays a bonus (20 SP per
  streak win, capped at 200); one loss ends the run. Best streak is saved.
- **Spanish** (`Loc`): Settings ▸ LANGUAGE. Menus, controls, HUD callouts, tutorial, events, badges and story
  scenes are translated. **Coverage is partial:** some built-up strings (numbers + text), archetype descriptions,
  and team/player names stay English.

## Phase 15: arcade feel, retro presentation, secrets, controllers

- **HEAT CHECK:** three straight makes heat a player up (+8% make chance, +6% speed, flame embers, glowing
  ball) until they miss, get blocked, or the other team scores. "HEATING UP" warns one make early.
- **Alley-oops:** pass to a teammate within 2.3 m of the rim (finishing 60+) and it becomes a lob they dunk in
  the air. The pass arrow turns gold when a pass will be an oop. Lobs are harder to intercept; jumpers can still
  block the finish.
- **Feel:** hit-stop on dunks, alley-oops, blocks and the final buzzer (off with Reduce Motion); heavy haptics
  on dunks/oops/heating up; Settings ▸ HIGH FRAME RATE runs the simulation and screen at 120 Hz on ProMotion
  iPhones (Info.plist `CADisableMinimumFrameDurationOnPhone`).
- **Presentation:** Settings ▸ CRT FILTER (off / soft / strong scanlines + vignette); pixel block wipe between
  screens; the title logo bobs; after 30 s idle on the title an AI-vs-AI demo game plays (any input exits);
  the announcer "speaks" callouts as synthesised chiptune voice blips.
- **Secrets:** Settings ▸ SECRETS ▸ ENTER A CODE (four symbols). Codes: BIG HEADS, RAINBOW BALL, ALWAYS HOT
  (toggles), PIXEL VOID (hidden court), CARTRIDGE KIDS (secret crew). Hints are earned by playing and appear
  in Locker Room ▸ TROPHY.
- **Arcade Ladder** (`ArcadeEngine`, `GameMode.Arcade`): five league teams weakest to strongest, then the
  secret boss THE GLITCH on The Glitch Grid. Difficulty climbs Rookie → Caller → Legend. Three continues.
  Clearing unlocks the boss team and court for Quick Call (+500 SP first time, +150 after).
- **Controllers:** menus are navigable with a gamepad (gold pixel cursor, A select, B back); in matches the
  touch buttons hide when a controller is used and the controls hint shows controller buttons; START pauses.
  Info.plist declares extended-gamepad support.

## Phase 16: create-a-team, Season 2, smarter AI, new modes, launch kit

- **Create-a-team** (Locker Room ▸ TEAM, `CustomTeams`): name, city, scoreboard name, 16-colour palette for
  jersey / trim / accent / shorts / shoes, 8 jersey patterns, logo shape + icon, home court. Your player and crew
  play for it in Quick Call, King, Arcade Ladder, 1-on-1, Caller Cup and 2 Player; "Wear it in Rise Mode" dresses
  the First Callers in it. Kits draw the pattern always and use their own shorts and shoe colours.
- **Season 2:** the Sundown Syndicate (led by Kaia Sol) is the rival in even Rise seasons; four new story scenes
  (Season 2 opener, Syndicate intro / beaten, two-time champions) in English and Spanish; courts Sundown Yard,
  Rain Alley, Snowline Park (all playable in Quick Call); 3 jerseys, 2 shoes, 1 banner; badges for the
  Syndicate, two titles, 10 alley-oops, 10 heat checks, creating a team, the Caller Cup and the Shootout.
- **Smarter AI:** every AI team has a defence (man, full pressure, pack the paint, zone). Caller/Legend AI
  switch out of a zone or pack-line after two threes in a row, and into pack-line after two baskets at the rim;
  the HUD says when they change.
- **Modes:** 1-on-1 (teammates sit out at the far corners), Practice ▸ SHOOTOUT (3-point contest against a CPU
  shooter whose round is simulated with the real shot model), and the Caller Cup (8-team knockout, +300 SP title).
- **Launch kit:** `docs/LAUNCH_KIT.md` (screenshot sizes and shot list with EN/ES captions, app preview
  storyboard, TestFlight checklist, privacy answers, launch-day list, press blurb); updated `APP_STORE.md`
  with the new features and a Spanish (Mexico) listing; **Capture Store Screenshot** saves opaque PNGs; the
  readiness check and builds write `Logs/RetroHoops-release.txt`.

## Phase 17: party games, Dynasty, highlights, iPhone prep

- **Party games** (PLAY ▸ PARTY GAMES): H-O-R-S-E against the CPU (its shots use the real shot model from a
  league team's best shooter) or a friend (pass the phone); 21 (1-on-1 to exactly 21, make it take it, bust
  back to 13); Around the World (seven mid-range spots in order, timed); the Shootout. Shooting games hand the
  ball straight back where you stand (`MatchSimulation.ResumeWithBall`).
- **Dynasty** (`DynastyEngine`): when a Rise season ends, START NEXT SEASON runs an off-season. Players age
  (≤23 grow +2, 24–26 +1, 27–29 hold, 30–32 −1, 33+ −2, plus a little luck), veterans retire (35, or sometimes
  33+), great careers (peak 78+, 4+ seasons) enter the Hall of Fame, teams draft generated rookies into the
  open spots, and you pick one of three prospects for your crew. LEAGUE HISTORY lists champions by season,
  titles by team and the Hall of Fame. Your own player never declines. Everything is saved and rebuilt on
  launch (`DynastyEngine.Apply`).
- **Share highlights:** post-game SHARE HIGHLIGHT replays the play of the game, records it at 15 fps / 270 px
  wide, encodes an animated GIF with an original encoder (`GifEncoder`, tested by decoding it back), saves it in
  the app's Highlights folder and opens the iOS share sheet (`RetroShare.mm`; Save Image needs
  `NSPhotoLibraryAddUsageDescription`, set by the build post-processor).
- **iPhone prep:** iOS readiness checks, builds and screenshots are logged to `Logs/RetroHoops-release.txt`;
  `tools/UnityCheck` compiles every assembly against Unity's own reference assemblies before delivery.

## Phase 18: Simulator, performance, onboarding, Season 3

- **Run it:** `tools/play_on_simulator.sh` (quit Unity first) builds with the project's exact Unity version,
  compiles with xcodebuild for the Simulator (no signing), boots an iPhone simulator and launches the game;
  every step logs to `Logs/` and failures are summarised in `Logs/RetroHoops-release.txt`.
- **Power:** `PowerMonitor` checks Low Power Mode and thermal state (iOS plugin `RetroPower.mm`) every 5 s;
  while saving power the screen runs at 60 fps and embers/crowd bobbing switch off. Settings ▸ SHOW FPS shows
  a rolling fps/avg/worst readout. Sim step and view sync have Profiler markers; spark bursts no longer
  allocate.
- **Onboarding:** first launch plays a short Coach Dee welcome scene before the tutorial offer. During your
  first 6 games Coach Dee gives one-time tips at the right moment (meter, early/late release, clearing,
  defence, alley-oop, heating up, shot clock); Settings ▸ COACH TIPS turns them off.
- **Juice:** every button squashes and springs back; menus and dialogs pop open; tip-off, victory and defeat
  jingles (all off or plain with Reduce Motion where it matters).
- **Season 3:** Midnight Tide (Mara Quill) is the rival every third Rise season, with intro/beaten scenes and
  a three-peat scene (EN/ES); courts Ferry Deck, Lantern Market, Canyon Rim; kits; Pixel Wave and Take a Bow
  celebrations; Double Cross and Step Back moves; badges Tide Turner, Three-Peat, Dynasty; codes POCKET
  GREEN (old-handheld green screen) and SKY HIGH (double-height jumps, looks only).

## Phase 19: App Store, Game Center + iCloud, local multiplayer, accessibility, Season 4

- **TestFlight / App Store:** `tools/ship_testflight.sh` runs Unity's `ReleaseTools.BuildAppStore` (raises the
  build number, Release config, `iOSBuild/AppStore`), then `xcodebuild archive` and `-exportArchive` with an
  App Store Connect export (upload, or `.ipa` with `--export-only`). The team comes from `$TEAM`, Unity's
  Signing Team ID, or the last Xcode device build. The app is iPhone-only. Step-by-step guide: `docs/SUBMISSION.md`.
- **Game Center:** 23 achievements (890 points) and 9 leaderboards (King streak, Arcade clears, Shootout wins,
  Around the World time, best win streak, most points), defined as data in `Achievements` (tested against
  Apple's limits). Reported after practice, party and 2 Player games too.
- **iCloud:** the career syncs through the player's own iCloud key-value storage (`CloudSync`, `RetroCloud.mm`,
  capability added by `IosPostProcess`). The career that's further along wins; a fresh install loads it,
  otherwise the menu asks LOAD FROM ICLOUD / KEEP THIS ONE. Settings stay per device; Reset also clears iCloud.
- **Local multiplayer:** tabletop 2 Player on one phone (P1 bottom half, P2 top half turned 180°) when no
  controller is connected; Shootout vs Friend (pass and play, two 60-second rounds).
- **Accessibility:** COLOR FILTER (red-green / blue-yellow meter palettes and automatic away-kit swap, chosen
  with colour-vision simulation and ΔE checks in `ColorAccess`); CAPTIONS for announcer calls (also follows
  iOS Closed Captions); VoiceOver for menus via Unity's screen-reader API (`ScreenReader`).
- **Season 4:** the Paper Cranes (Juno Vale) take their turn as the fourth rival (rivals now rotate every four
  seasons), chapter 4 scenes (EN/ES), courts Laundromat Lot, Drive-In Lot and Paper Garden, kits, Shoulder
  Brush and Paper Plane celebrations, Rocker Step and Snatch Back moves, a crane logo, and four badges.

## Full Court and winners' ball (after Phase 19)

- **Full Court 5-on-5** (Play ▸ FULL COURT, `GameMode.FullCourt`): both baskets, 2s and 3s, four minutes,
  20-second shot clock, tip-off at half court, inbound from the far baseline after a basket. Teams with
  fewer than five players get deterministic generated reserves (`FullCourt.Reserves`). The simulation runs
  in the attacking team's frame (its hoop always "up"), and the picture turns 180° when possession changes
  (`MatchSimulation.TurnFrame`, `Flipped`, `ToWorldCourt`), so shooting, passing and the AI are unchanged.
  Off-ball defenders get back to half court unless pressing; on-ball defenders give a cushion in the
  backcourt. Views draw on the fixed court through `CourtSpace.Flip`; the camera scrolls up and down;
  the art is two half courts back to back with a second hoop. Pays like Quick Call; its points don't count
  toward the half-court single-game points record.
- **Winners' ball:** every half-court 3-on-3 game (Quick Call, Rise, King, Cup, Arcade, 2 Player and the
  rest) keeps the ball with the team that scores. Full Court and 1-on-1 still alternate.

## Phase 20: Kit Studio, Holiday Games, Full Court rules, sharing

- **Kit Studio** (Locker Room ▸ KIT): Home / Away / Alt kits. Jersey: colour, trim, accent, cut (tank, tee,
  long sleeve), collar (V, crew, none), side stripes, chest (logo, band, number), pattern. Shorts: colour, trim,
  length (classic, long, short), side stripe, waistband. Shoes: colour, sole, laces, stripe, height (low, mid,
  high). 64-colour palette + RGB sliders (32 steps a channel, the grid kits are stored on). Live looping
  preview, RANDOMIZE (harmonious schemes, unlocked styles only), UNDO, RESET, presets from owned shop items.
  Long sleeves, high tops, long shorts, double stripes, chest number and four patterns unlock by playing.
  Your team wears the kit in every game; the Away kit goes on automatically when colours clash (ΔE check).
  The classic look is pixel-identical to the old sprites (tested).
- **Sharing (offline):** a kit is a 33-character code (15-bit colours + styles + CRC-8, so every single-character
  typo is caught) and a QR code (own encoder: byte mode, level M, versions 1–10, checked against OpenCV's
  decoder) for `retroball://kit/<code>`; the iPhone camera opens the app straight into the Kit Studio. ENTER
  CODE / PASTE loads a friend's kit. TRADING CARD draws your player in your kit with OVR, role and stats
  (PNG via the share sheet).
- **Holiday Games** (Play ▸ HOLIDAY GAMES): Christmas (snow, candy-cane lines, trees, lights), Halloween
  (pumpkins, bats, moon), Easter (eggs, flowers, bunting) and Fourth of July (stars in the paint, striped
  lines, fireworks) courts; the one in season (all of October, Dec 1–26, Easter week, Jul 1–7) gets a main
  menu button. Holiday courts are also in the Quick Call court list.
- **Full Court polish:** inbound pass from the baseline after a basket (5-second count), backcourt and
  eight-second violations, fast-break lanes and get-back defence, two-player benches with substitutions at
  dead balls when AI players tire (subs keep their own box-score lines), and the Rise Mode final is Full Court.

## Phase 21: Franchise, All-Star Weekend, Court Builder, soundtrack, ship 1.0

- **Franchise** (Play ▸ FRANCHISE): run one of the eight Caller League clubs for as many seasons as you like.
  7–9 man rosters (the league's players plus generated depth), contracts in tenths of a million under an
  $85.0M cap (minimum deals always fit; your own players can be re-signed over the cap; releasing leaves
  half a season's salary as dead money). A 14-game double round robin of Full Court games (PLAY or SIM),
  top-four playoffs, MVP and rookie awards. Off-season in order: aging (growth toward potential before 26,
  decline after 29), retirements and a Hall of Fame, a draft lottery for the four non-playoff teams (40/30/20/10
  for the top two picks), a 12-man draft class with scouting (each visit narrows the rating range; the potential
  grade shows after two), rookie scale contracts, re-signing, free agency (AI clubs fill their rosters), preseason
  cuts. Trades with the seven AI GMs (up to 3 players a side, deadline week 10): they value rating (squared,
  so stars beat depth), youth with potential, age and contract value, need to win the deal by a margin, and
  both sides must stay roster- and cap-legal (or take back ≤125% of outgoing salary). History book: every
  season's record and finish, champions, awards, transactions.
- **All-Star Weekend**: Dunk Contest (11 original dunks with arrow combos 2–7 long, a clock to enter them,
  a slam meter, two tries a dunk; five judges score 5–10 on difficulty, execution, timing, with repeats marked
  down; you plus three league dunkers, two rounds), 3-Point Contest (the Shootout drill against three of the
  league's best shooters, top two to a final), and the All-Star Game (Team Sunrise vs Team Moonlight, the
  league's best split by snake draft, Full Court, you start). In Rise it opens at mid-season and stays open to
  the end of the regular season; everything is also on Play ▸ ALL-STAR CONTESTS.
- **Court Builder** (Locker Room ▸ COURT): three slots; floor style (asphalt, hardwood, neon grid, tiles,
  rubber) and colour, lines, paint, what's behind the baseline (crowd, chain-link fence, brick wall), sky
  presets, crowd size, centre-court logo (motif, shape, colour); live preview from the real court generator.
  Saved courts are in Quick Call and can be your team's home court.
- **Soundtrack + Music Player** (Settings ▸ MUSIC PLAYER): six new original songs written by an in-code
  composer (pulse lead over chord tones, triangle bass, arpeggio, noise drums, A-A-B-A loops, all in key)
  plus the three original loops; level meter; menu track and match track (or shuffle) are saved.
- **Game Center**: three new achievements (Franchise title, Dunk Contest, 3-Point Contest; 1,000 points total)
  and a Best Dunk Contest Round leaderboard.
- **Ship it**: store listing, screenshots plan and review notes updated for 1.0; upload via `tools/ship_testflight.sh`.

## Phase 22: Legacy, The Park, Tournament Builder, polish

- **Legacy** (Play ▸ LEGACY): your created player's career. Senior year of high school (7 games + state
  playoffs) earns 1–5 recruiting stars; college offers by stars (or skip college and declare); a college season
  sets draft stock; draft night (the weakest league team picks first; low stock = undrafted); then pro seasons
  with a Caller League team (10 games + playoffs) until you retire (33+, forced at 38). Every game is Full
  Court with you at the top of the lineup, or SIM for half XP. Game grades (A+..F) give XP; levels give skill
  points for a 24-node skill tree in three branches (tiers need the tier below). You start a senior at overall
  52 whatever your base, grow with age and skills, decline after 30. Coach's trust and your role, salaries
  and agent offers when a contract ends, up to three invented sponsors (unlocked by fans), a private trainer
  bought with cash, 12 story moments with choices, awards (State/Conference champion, All-State,
  All-American, MVP, All-League, Rookie of the Year), legacy points and a Hall of Fame vote.
- **The Park** (Play ▸ THE PARK): twelve original street legends with crews, formats 1-on-1 to 4-on-4 on their
  own courts. Street rules (first to 15, win by two, make it take it) and **ankle breakers**: a sharp cut near a
  defender can make them stumble for 0.9 s (handle vs feet, 8–55%, 1.6 s cooldown; "ANKLES!"). Rep from
  Rookie to Legend opens tougher callers; first wins pay double; beat everyone for King of the Park.
- **Half-court 2-on-2 and 4-on-4** in the engine (MatchRequest.TeamSize), tested with AI-vs-AI games.
- **Tournament Builder** (Play ▸ TOURNAMENT BUILDER): name, 4/8/16 teams from everything you can play,
  2-on-2, 3-on-3 or Full Court, your team; seeded by strength with a standard bracket; others simulated.
- **Polish:** the PLAY menu is grouped (Play now, Careers, Events, With friends); composed songs are written
  on a worker thread so screens never hitch, and only the playing song stays in memory; two leaderboards.
- **Ship it:** the upload script now logs every run to `Logs/ship_console.log`; double-click
  `tools/Ship Retro Hoops.command`.

### Phase 23: launch 1.0, iPad + Mac, online-free extras

- **App icon "Sunset Swish"** (AppIconGenerator): ball dropping through the net under a backboard, striped
  synthwave sun, neon grid floor, gold sparkles; 64x64 pixel art scaled to 1024, fully opaque.
- **iPad + Mac:** release settings now target iPhone and iPad (portrait, full screen); Apple silicon Macs run
  the iPad build as "Designed for iPad". The court camera picks the largest integer zoom that shows 11.5 m
  across *and* 20 m top to bottom, so wide screens no longer crop the court (phones are unchanged). Canvases
  match height on anything wider than 9:16 (and re-match when a window changes shape). iPad launch image set.
  A keyboard (WASD/J/K/L) hides the touch controls like a controller does, until the next touch; Macs show
  the keyboard hint. 13" iPad screenshot sizes accepted by Capture Store Screenshot.
- **Weekly Challenges** (Play ▸ WEEKLY & HOOPS PASS): three goals each Monday from the week number (offline,
  same for everyone), progress adds up over every game vs the CPU; +100 SP each, +150 for all three.
- **Hoops Pass:** free 20-tier season track (6-week seasons): XP from games (+25, +15 for a win), weekly goals
  (+120) and the daily (+60, once a day). Tiers pay SP; tiers 5/10/15/20 unlock pass-only gear (two alternating
  sets of four; owned gear pays 250 SP instead). No purchases, no ads.
- **Photo mode** (pause ▸ PHOTO MODE): frozen game, HUD hidden, drag/zoom (or stick/triggers), six filters
  (Arcade scanlines, 4-Color dither, Dusk, Neon, Mono), optional frame with RETRO HOOPS, score and date;
  SNAP samples one pixel per art pixel, scales up crisp, saves a PNG and opens the share sheet.

### Phase 24: controls, finishes, alley-oops, landscape Full Court

- **Boot screen:** sunset court, RETRO HOOPS logo, spinning pixel basketball loader (was the old text card).
- **Button layout** (TouchControls + ControlLayout): ring buttons with original pixel icons in a thumb arc in the
  lower-right corner: SHOOT (biggest, outside), PASS beside it, DUNK above, LAYUP on the diagonal, CALL further in;
  the stick's resting place is shown bottom-left. On defence they become BLOCK, SWITCH and STEAL (LAYUP and CALL
  hide). PASS turns gold and reads OOP when a lob is on.
- **Customize controls** (Settings ▸ CUSTOMIZE CONTROLS): drag buttons, tap and −/+ to resize (70–150%), RESET,
  SAVE; warns about overlaps; saved in the career; used in portrait and landscape.
- **DUNK / LAYUP:** with the ball, close enough you go straight up; further out you attack the rim at full speed
  (up to 2.6 s; pull the stick away to back out) and finish there. Release is automatic, spread by Finishing;
  contests and blocks apply. DUNK becomes a layup without the rating (65) or distance (2.2 m). Keys U / I,
  controller RB / LB.
- **Baseline alley-oops:** while you handle the ball away from the rim, an athletic AI teammate (Finishing 60+)
  sometimes sprints to the corner and runs the baseline; within 4.6 m of the rim they're a lob target and PASS
  throws them the oop (whatever the stick aim), which they catch and throw down.
- **Full Court 5-on-5 in landscape:** the screen turns sideways for Full Court (not the attract demo or 2 Player),
  baskets left and right, side-view hoops, camera pans with the ball (the whole floor fits on most phones),
  sprites face the right way, UI lays out on 1920 x 1080. Info.plist allows portrait + both landscapes; the game
  locks portrait at boot and returns to portrait after the game. The stands' animated fans are off in landscape.

### Phase 25: feel, landscape polish, Season 5

- **Dunk packages** (Dunks.cs, Locker Room ▸ DUNK PACKAGE): Two-Hand Jam (default), Tomahawk, Reverse Jam,
  Windmill, Cradle Rock, Three-Sixty (SP + fans), and the pass-only Skyline Slam. Each has its own pose, extra
  lift/hang time, shake and callout ("WINDMILL! +2"). The AI picks by Finishing (only flyers go 360).
- **Euro step:** a driving handler with Playmaking 55+ side-steps a defender planted in the lane ("EURO STEP").
- **Rim protection:** on a DUNK/LAYUP drive the defender nearest the rim (not the driver's man) steps in front
  and goes up with the finish; a defender whose man runs the baseline stays between him and the rim.
  Both scale with difficulty. "NO DUNK FROM THERE: LAYUP" when a DUNK has to be a layup.
- **Landscape polish:** bleachers with cheering fans along the far sideline; the camera fits the near sideline
  to the top of the stands; a compact centred score bar. Settings ▸ LANDSCAPE: ALL GAMES plays every mode
  sideways (2 Player and the demo stay portrait).
- **Season 5:** the Cassette Club (fifth rival, every fifth Rise season; Echo Rivera; story scenes in English
  and Spanish; REWOUND badge; NO RIVALS LEFT now needs all five), Record Shop Roof and Night Bus Depot courts,
  and a third Hoops Pass gear set (Cassette Deck jersey, Tape Runners, Boombox banner, Skyline Slam).

### Phase 26: two phones, Couch Cup, Summer Story, performance

- **Two-phone play** (Logic/Net/Link.cs, Core/NearbyLink.cs, Plugins/iOS/RetroLink.mm): Multipeer Connectivity,
  no server. The host picks both teams; both teams' rosters and ratings travel in the setup, so career changes
  on either phone don't matter. Hand-shake checks the link version, app version and content (courts, rules,
  play styles). Deterministic lockstep: input is quantized to 4 bytes per step and scheduled 4 steps (67 ms)
  ahead; a step runs only when both inputs are in; checksums are compared every second. Both phones simulate
  at 60 Hz; no hit-stop, mid-game replays or photo mode; the pause menu doesn't stop the game. A friend leaving,
  a dropped connection or a checksum mismatch ends the game with no result. Info.plist gets
  NSLocalNetworkUsageDescription and NSBonjourServices (_retrohoops._tcp/_udp).
- **Couch Cup:** 2-8 players, shuffled knockout with byes, ties replayed, saved between games.
- **Summer Story:** 8 chapters with goals, scenes before and after, SP for first clears (150, finale 600).
  New original cast in StoryMode.Cast. Fixed a crash in story scenes for the fifth rival (the portrait cache
  was one slot short).
- **Mic Tally commentary** (Logic/Feel/Commentary.cs): runs, lead changes, ties, game point, dunks, threes,
  oops, blocks, steals, heat, euro steps, broken ankles, buzzer beaters; one line per 5 s at most (big
  moments can cut in), no back-to-back repeats.
- **Performance and battery:** menus draw at 60 fps while touched and 30 when idle (15 when saving power);
  gameplay draws every frame (OnDemandRendering, PowerPolicy.RenderInterval). Low-memory warnings drop cached
  art. Boot timing goes to the device log. Release builds strip engine code, use Low managed stripping with
  Assets/link.xml keeping our assemblies, and size-optimised IL2CPP code. Measured here (desktop .NET):
  a simulation step costs about 10 µs and the biggest generated texture (Full Court floor) about 5 ms, so
  neither is a battery or load-time problem; drawing is.

### Phase 27: Retro Hoops Live, ship-ready docs, two-phone rematch

- **Retro Hoops Live** (Omari's request: online play for $10.99/month): PLAY ▸ LIVE. The subscription uses
  StoreKit 2 (`Plugins/iOS/RetroStore.swift`, product `com.phoronomicstudios.retrohoops.live.monthly`). The App
  Store decides whether it's active, and the career caches the end date. The paywall shows the localised price,
  terms, Restore, Terms of Use and Privacy Policy (guideline 3.1.2). Matchmaking uses Game Center
  (`Plugins/iOS/RetroLive.mm`, GKMatchmaker); the player group is derived from the game version. The host is the
  lower Game Center ID. Each player brings their own team (the Hello message), and the host's team is home.
  Same lockstep as two phones with an 8-step input delay. Elo rating (1000 start, K 32), tiers ROOKIE to
  LEGEND, Game Center leaderboard `retrohoops.live.rating`. Quitting after 20 s is a loss; an opponent leaving
  is a win; a drop or desync counts for nobody. Setup steps in `docs/LIVE.md`.
- **Ship-ready docs:** `docs/LIVE.md` (subscription, agreements, sandbox testing, review notes), `docs/PRIVACY.md`
  (hostable policy covering Live and two phones), APP_STORE.md (description sections, age-rating answers,
  privacy label notes, review notes) and SUBMISSION.md (upload error fix, subscription step).
- **Two-phone rematch:** after a two-phone game both players tap REMATCH. The same teams play with a new seed
  and no reconnecting. Hand-shake messages that arrive while the other phone finishes are kept.
- **Not done this phase (moved to Phase 28):** spectator view, a two-phone Couch Cup, sharing two-phone
  replays, gameplay depth (fatigue/subs in half court, new plays, AI defence, tuning) and the polish items
  (crowd sounds, new animation frames, court intro, post-game screen).

### Phase 28: dunks that reach the rim, the Live server (backend security)

- **Real dunks** (Omari's request: "the hands of the players touch the basket"): `DunkPath` (Logic/Feel/Dunks.cs). The dunker
  glides from the take-off spot to just in front of the rim while rising, the raised hands meet the rim
  (+2 px over it) at the exact moment the simulation's ball arrives, then they hang on the rim for a beat (the rim dips a
  pixel) and drop back to where the simulation has them. The ball rides in the hands until the slam, and the dunk packages
  swing it. The lift comes from the sprite's real hand height (`CharacterSpriteGenerator.HandReachPx`) and the rim's screen
  height, so it works for every player height and in landscape. Drawing only; the simulation is unchanged, so two-phone
  and Live games stay in sync.
- **Live server** (`server/`, Cloudflare Workers + D1, TypeScript): Game Center identity verification
  (RSA-SHA256 over teamPlayerID + bundle + timestamp + salt, cert only from https://*.apple.com, 10-minute window,
  single use), HS256 sessions (1 h), subscription checks through Apple's App Store Server API (ES256 key; one
  subscription ↔ one player, `appAccountToken` tagging), App Store Server Notifications V2 (re-check with Apple), server-side
  Elo that only changes when both reports agree (score + final SimHash), stale games settled by cron, a pair farming cap,
  rate limits, an 8 KB body cap with strict validation, parameterised SQL, security headers with no CORS, and data deletion. 20 tests;
  type-checked; ran in the local Workers runtime. Game side: `BackendConfig.Url` (off until deployed), `BackendClient`
  (UnityWebRequest, token in memory only), Game Center identity fetch in RetroLive.mm, `originalTransactionId` +
  `appAccountToken` in RetroStore.swift, rated Live games report results, LIVE ▸ DELETE MY LIVE DATA.
- **Merged with the Phase 28 commit made on the Mac** (6cbc1f7, Claude Sonnet): `SaveGuard` (an HMAC envelope for the save
  file; the code and tests are in, but it isn't wired into SaveStore yet because that needs a Keychain key on iOS), a per-session nonce in
  `LinkSetup.Session` (rematches get a new one), and `LiveMode.Sanitize` / `DeltaIsPlausible` (now applied when loading the
  save). Its tamper test was fixed: it looked for compact JSON, but saves are pretty-printed.
- **Not done this phase (moved to Phase 29):** gameplay depth, polish & juice, spectator / two-phone Couch Cup / replay sharing.

### Phase 29: gameplay depth, arena polish

- **Two new plays** in the CALL menu: **BACKDOOR** (the best cutter jogs out high for 0.9 s, then cuts behind
  their defender to the rim; the others stay wide) and **POST UP** (the best finisher + rebounder seals on the
  ball-side block; the others clear to the corners). AI teams now mix them in with the pick-and-roll. Both
  travel in two-phone and Live games.
- **Smart defences double the hot hand:** Caller and Legend AI send the nearest off-ball defender at a
  heated-up scorer within 8 m of the rim ("DOUBLE TEAM! FIND THE OPEN MAN"), which leaves someone open. Rookie
  AI never does. Mic Tally calls it.
- **Half-court benches:** a team's own extra player (street crews, story crews, your crew) sits on the bench
  and checks in for a tired AI player at a dead ball, as in Full Court. There's no bench in 1-on-1, practice
  or the tutorial.
- **Difficulty curve check** (AI vs an idle player, 16 games each): opponent FG% 34 / 39 / 51 and an average margin of
  +5.7 / +7.6 / +14.3 for Rookie / Caller / Legend. The curve climbs. Rookie and Caller are close, so there's room
  to spread them once real players give feedback. A test now guards the ordering.
- **Arena sounds:** a crowd "OOOH" swell (blocks, broken ankles), an arena horn (final buzzer, substitutions)
  and rhythmic crowd clapping in a tight finish (last 20 s, within 3).
- **Tip-off card:** tonight's court, both teams in their colours, and the starters' numbers and names. It's
  skippable and fades after 3 s without stopping play.
- **Post-game TEAM STATS:** FG%, deep shots, assists, rebounds, steals, blocks and turnovers side by side, with
  split bars, above the box scores.
- Two-phone games already offer PLAY OF THE GAME and SHARE HIGHLIGHT after the final.
- **Not done (still open):** spectator view on a third phone, a Couch Cup across two phones, new animation frames.

### Phase 30: build-ready pass, watch on a third phone, Couch Cup on two phones, new animation frames

- **Build-ready pass:** reviewed the Phase 26–29 native code and Unity settings for anything Unity, IL2CPP or
  Xcode could reject. `link.xml` now marks the package assemblies `ignoreIfMissing` so a renamed package can't
  fail the linker. Both Objective-C plugins that use Multipeer / Game Center pass a syntax check under ARC and
  manual retain/release. New double-click **`tools/Build Check (Simulator).command`**: Unity build → Xcode
  Simulator compile (no signing) → launch, with the C#, linker, Xcode and Swift errors summarised in
  `~/RetroHoops/Logs/build_check.txt`. This is the real compile check; it hasn't been run yet.
- **WATCH A GAME (third phone):** 2 PLAYER ▸ TWO PHONES ▸ WATCH A GAME lists nearby games; up to 3 phones can
  watch once both players are in. The host forwards both players' inputs for every step it simulates (8 bytes a
  step), so the watcher runs the identical deterministic game. A late watcher gets the game from tip-off and
  fast-forwards (up to 10x) to catch up. Watchers follow the players into a rematch or the next cup game. The
  host's phone shows "1 FRIEND IS WATCHING". Natively, the host keeps advertising after its opponent joins,
  accepts "watch" invitations separately, and listens only to its opponent; a joined phone listens only to its host.
- **COUCH CUP on two phones:** the bracket's new **2 PHONES** button hosts the next cup game. The first-named
  player plays on the host phone, the other on the second phone (JOIN A GAME). After the final, the host records the
  result in its bracket (only once, and only for that game), and REMATCH on both phones plays the bracket's next
  game (a tie is replayed). Names show in the toasts and the post-game screen.
- **New animation frames** (each player sheet now has 10 frames per view): a low, wide **crossover dribble**
  pose (Crossover and Double Cross moves), a **step-back** pose with the ball held up, and a **chest thump**
  (a new celebration, CHEST THUMP, 400 coins: two thumps, then the fist goes up).
- **Not verified:** none of this has run in Unity, Xcode or on a device yet. Watching and the two-phone cup
  need three iPhones nearby to test properly.

### Phase 31: save protection, game tapes, Season 6, onboarding & accessibility

- **Save protection:** the career file is now sealed (HMAC-SHA256, `SaveGuard`) with a random 32-byte key kept in
  the iOS Keychain (`RetroKeychain.mm`, this device only, readable after first unlock). On load (`SaveIntegrity`):
  a plain pre-Phase 31 save is read and sealed (*Legacy*); a matching seal is *Verified*; a seal that can't be
  checked because the key is new (restored to another phone, Keychain cleared, or Keychain locked at launch) is
  trusted and resealed (*KeyLost*, never flagged); a mismatch with an existing key is *Tampered*. A tampered
  career is **never wiped**: it loads with its numbers clamped to what the game can produce, a copy of the edited
  file is kept, and it stops posting Game Center leaderboard scores (`saveFlagged`, kept inside the sealed file).
  A one-time SAVE FILE CHANGED notice explains this. iCloud copies stay unsealed (other devices have their own
  keys) and are clamped when adopted. This stops casual editing. Someone with a jailbroken phone can read the
  Keychain, so this doesn't stop them.
- **GAME TAPES:** after a two-phone, Live or watched game, SAVE GAME TAPE stores the setup plus both players'
  inputs for every step (run-length coded: a whole game is a few KB). 2 PLAYER ▸ GAME TAPES replays a tape
  exactly (tap the court for 1x/2x/4x), SEND / RECEIVE A TAPE moves one to a friend's phone over the two-phone
  link, and up to 12 are kept. Tapes only play on the game version that recorded them. Watched games now get
  the full post-game screen with PLAY OF THE GAME and SHARE HIGHLIGHT.
- **Season 6:** a sixth rival, the **Lighthouse Keepers** (full-court press; leader Wren Marsh; a new lighthouse
  logo, also in Custom Team), with their own story scenes (English + Spanish), badges LIGHTS OUT and FULL
  ROTATION, two courts (Lighthouse Point, Glasshouse Roof), a store jersey (Harbor Fog), a Hoops Pass set
  (Beacon, Fog Runners, Lighthouse Beam, and the pass-only **Spotlight** celebration), and three new weekly goals
  (deep shots, alley-oops, heat-ups). Rivals now rotate every six seasons. The weekly pool only grows from the
  week of 12 Oct 2026 and the four-set pass rotation starts with the pass season of 19 Oct 2026, so nothing
  changes mid-week or mid-season. The Game Center "Every Rival" achievement still means the original five, so its
  App Store Connect text doesn't need changing.
- **Onboarding:** a short card the first time each newer mode is opened (Two Phones, Live, Couch Cup, Watch a
  Game, Game Tapes), and one NEW IN RETRO HOOPS card for careers from before the update.
- **Accessibility:** **VoiceOver** reads the menus via Unity's screen-reader API (`ScreenReader.cs`). Every
  visible button and label is an element, and a double-tap presses it. The menus also grow with the iPhone's
  **Larger Text** setting, up to the biggest UI scale (Settings ▸ MATCH IPHONE TEXT SIZE, on by default).
  Gameplay itself isn't playable with VoiceOver.
- **Updated old tests on purpose:** the rival rotation (Phases 16/19/25), the pass gear rotation (Phases 23/25),
  the street-court count and the weekly-goal variety test now expect Season 6.
- **Not verified:** Unity/Xcode compile and device behaviour, in particular the Keychain plugin, VoiceOver
  element positions (Unity's screen-space convention is assumed), and tape transfer between two phones.

### Phase 32: joining up with the Mac session's work

- **Found on the Mac:** another session had added `Assets/Tests/EditMode/Logic/Phase30SecurityTests.cs` (21 tests for
  signed, time-limited Live server requests: `Backend.RequestTag`, `Backend.IsExpired`,
  `BackendConfig.RequestWindowMs`, and signed `StartBody` / `ResultBody`) straight into `~/RetroHoops`, without the
  code behind them. As it stood, Unity's test assembly would not have compiled. Nothing else in the Mac project
  differed from the repo.
- **Done:** the test file is now in the repo unchanged, and the code it describes is in `Backend.cs`. Match bodies
  carry `t` (ms) and `tag` = HMAC-SHA256 keyed by the match key over `"{t}|start|key|opponent|seat"` or
  `"{t}|result|key|scoreA|scoreB|hash|outcome"`. The game signs every `/v1/match/start` and `/v1/match/result`.
  The server (`server/src/sign.ts`) now **requires** the signature and a time within 5 minutes; it was never
  deployed, so no old client is affected. Shared test vectors prove the game and server compute identical tags.
  This is defence in depth on top of the session token and HTTPS.
- **Xcode state on the Mac:** the last ship run (5 Oct, 15:30 PDT) built and archived build 2 but failed the
  upload with "Error Downloading App Information". That was before App Store Connect's bundle ID was switched
  to com.phoronomicstudios.retrohoops, so the upload hasn't been retried since the fix. The Device Xcode project
  in `iOSBuild/Device` is from 4 Oct (Phase 25 code). It was opened in Xcode later, but there's no log of a
  newer device build.

### Phase 33: season depth, Live polish, store copy

- **Merged GitHub:** the Mac session had pushed Phases 29–32 as snapshots of `~/RetroHoops` (game code only).
  They matched this repo's code, so the merge kept the server, tools and docs they didn't include, plus
  `RetroKeychain.mm`. Both `.gitignore` files were combined. The two sessions shouldn't edit the Mac project at the
  same time; this session now does the code.
- **Franchise depth:** the other GMs **call with trade offers** during the season (about 40% of weeks before the deadline).
  Each offer is a deal their own valuation accepts, it never asks for your best player, and it lapses at the deadline.
  ACCEPT or DECLINE on HOME. A **TRAINING FOCUS** (Balanced, Shooting, Defense, Playmaking, Athleticism) gives your
  players aged 27 and under who are below their potential +2 in the two matching attributes each off-season. A
  **PLAYER DEVELOPMENT** report lists every player's overall before → after.
- **Playoff bracket picture** (Franchise ▸ LEAGUE and the Rise season screen): semifinals joined to the final, seeds,
  scores, winners in bold, your team in gold. From mid-season it shows the bracket "if the season ended today".
- **Live:** **INVITE A FRIEND** through Game Center's own invite screen, and an invite accepted from a notification
  opens LIVE and joins. **REMATCH** after a Live game, with ratings carried over. A **monthly** recurring Game Center
  leaderboard plus a `?period=month` server leaderboard.
- **Store & marketing:** `APP_STORE.md` and `LAUNCH_KIT.md` were wrong after Live: they said "no purchases" and "Data
  Not Collected". Both are corrected, with the App Privacy answer depending on whether the Live server is on. New
  promo text (161 chars), description lines for Watch, Game Tapes, Season 6 and friend invites, a Phase 33
  screenshot list, and a second 30 s preview storyboard ("Play your friends"). Keywords are unchanged (99 chars).
- **Fix real build errors:** waiting on the Mac's `build_check.txt`. The Build Check hasn't been run yet, because the
  other session was using the Mac.

### Phase 34: pre-compile hardening, Skills Gauntlet, AI & feel, Season 7

- **Pre-compile hardening** (a separate review pass over Phases 26–33, then fixes):
  - **Correction to Phase 31:** VoiceOver support already existed (`UI/ScreenReader.cs`, an earlier phase). Phase 31's
    `Core/ScreenReader.cs` duplicated it, more weakly, and was the one actually running. It's deleted, and `App`
    now starts the original.
  - `ReleaseTools` builds never go below iOS 15 (StoreKit 2 needs it), even if Project Setup wasn't run.
  - `IosPostProcess`: all our `.mm` plugins compile with `-fobjc-arc` (Unity's project doesn't turn ARC on). They
    were correct either way, and ARC removes their small leaks. Also sets
    `CLANG_ALLOW_NON_MODULAR_INCLUDES_IN_FRAMEWORK_MODULES=YES` for the Swift file in UnityFramework.
  - `Packages/manifest.json`: added the built-in modules the code uses (UnityWebRequest, Image Conversion, Screen
    Capture, Accessibility).
  - `RetroLink.mm`: player names are trimmed by whole characters (an emoji cut in half could crash), and the
    session/hosting properties are atomic (Multipeer's callbacks race teardown).
  - Live invites register right after Game Center sign-in (`CallerGameCenter.mm`) and retry until they're
    registered. `RetroStore.swift` reads the product id under its lock.
  - New PlayMode smoke tests (`Tests/PlayMode/NewModesSmokeTests.cs`: a game tape playing in the game scene, the
    tape store, a sealed save through the real store, and Franchise with offers). Run them in Unity's Test Runner.
- **SKILLS GAUNTLET** (EVENTS): four practice drills back to back, the same four for everyone each day. Each result
  becomes points (a strong station is about 300), and the run total is kept as today's best and an all-time best.
  The all-time best goes to Game Center board `retrohoops.lb.gauntlet`. A replayed or out-of-order station never
  counts twice.
- **AI & feel:**
  - Off-ball AI players never stand where their ball handler has driven (they take the mirror spot for the
    kick-out). Over 6 Legend games, teammates crowding the handler fell from 5.1% to 2.7% of possessions and AI FG%
    rose from 43.8% to 50.9%.
  - AI bigs with a finishing rating of 60+ and a defender on their back **drop-step** to the baseline side of the
    rim. This adds no random draws, so two-phone and seeded games stay in step.
  - The release callout now shows the timing and the make chance ("CLEAN LOOK · SLIGHTLY LATE · 54%").
  - **DIFFICULTY BY MODE** (Settings): Quick Call, Rise, Franchise, The Park, Events and Legacy can each have their
    own level. Fixed-difficulty games (two phones, Live) keep theirs.
- **Season 7:** the **Comet Couriers** (bike couriers who run the break off every rebound; leader Remy Okoro),
  story scenes in English and Spanish, badges SIGNED FOR and SEVEN FOR SEVEN, Tram Yard and Dispatch Roof courts,
  a Signal Orange store jersey, a Hoops Pass set (Courier, Spoke Runners, Express Lane, pass-only **Victory Lap**
  celebration) from the pass season of 30 Nov 2026, and a Full Court wins weekly goal from the week of 9 Nov 2026.
  Rivals now rotate every seven seasons; the rotation tests were updated again.
- **Still not compiled for real:** the Mac's Build Check hasn't run, because another session was using the Mac.

### Phase 35: shot charts, replay theater, coach mode, polish

- **SHOT CHARTS** (built on `ShotZones` from the Mac session): every shot is filed under one of nine spots in the box
  score (`PlayerStatLine.chart`, `Logic/Match/ShotCharts.cs`, art in `Logic/PixelArt/ShotChartArt.cs`). Spots are
  hot or cold against what that spot usually gives up (rim 55%, mid-range 42%, deep 34%; 3+ shots needed).
  Shown on the post-game screen (yours, or your team's), Locker Room career, Franchise team (per season) and Legacy
  (season and career). Saved as `shots` in the career, franchise and legacy blocks.
  The AI leans toward shooting from spots it's hot from that game, away from cold ones, and off-ball shooters drift
  toward their hot spot. It uses no extra random draws, so seeded and two-phone games stay in step.
- **REPLAY THEATER** (game tapes): pause, 0.25x / 0.5x / 1x / 2x / 4x, a scrub bar, previous/next mark, BALL /
  PLAYER / RIM / CLOSE-UP cameras, and marks. Every basket, block and steal is marked by simulating the tape once
  when it opens. ADD MARK saves your own marks with the tape: tapes with marks are written as "RHT2", and RHT1
  tapes still read. Going forward simulates ahead; going back restarts the game scene and simulates up to that
  moment before the first frame.
  Measured here: a Full Court game is about 16,500 steps and simulates in about 0.18 s on the cloud machine. iPhone
  timing hasn't been measured.
- **COACH MODE** (Franchise GAME DAY: PLAY / COACH): all your players are AI. From the sideline you can:
  - call a play: pick-and-roll, backdoor or post-up (it runs on the next trip if you're on defense);
  - set the offense's focus: BALANCED, ATTACK THE RIM, LET IT FLY or FEED THE HOT HAND;
  - pick the defense: MAN, PRESSURE, PACK LINE or ZONE (your AI never changes it);
  - make subs, which go in at the next dead ball, with an AUTO SUBS switch.

  The play and sub lists hold the game while they're open. Coached games count for the record and the team chart,
  but not for your player's career stats. Over 3 seeds, LET IT FLY took 64 deep shots and ATTACK THE RIM took 40.
- **Polish:**
  - The screen wipe rotates between three looks: diagonal, iris and shutters (plain fade with Reduce Motion).
  - A tip shows while the screen is covered (24 original tips, in turn).
  - Camera flashes pop in the crowd after dunks and deep shots (not with Reduce Motion or Low Power). The crowd does
    the wave on a 6-0 run and stands for a close finish.
  - Sound mix: the announcer has his own voice channel (no longer cut off by SFX). The music dips 45% and the crowd
    25% under him. The crowd's volume follows its mood (louder in a close finish). The same sound repeated within
    60 ms is softened.
- **Still not compiled for real:** the cloud checks pass (logic tests, the Unity API check, Objective-C syntax),
  but no Unity or Xcode build has run since Phase 25.

### Phase 36: pre-ship hardening, Season 8, SPOT SPECIALIST, Live seasons & sharing

- **Hardening** (a review pass over Phases 30–35, then fixes):
  - **Replay theater:**
    - The marking pass and every jump now run a slice at a time (about 4 ms and 22 ms a frame) behind "SEEKING…", so long tapes can't freeze an older iPhone.
    - Going back rebuilds the match in place instead of reloading the scene, so there's no wipe, no tip-off card and the music doesn't restart.
    - After a jump, only slots whose player changed are redrawn, and player sheets are cached per slot. That fixes a texture leak in jumps and in Full Court subs.
    - The replay buffer, play of the game, arena effects and commentary reset after a jump.
    - The scrub bar can't get stuck if the app is backgrounded mid-drag.
    - The theater holds the same 64 marks a tape does.
  - **Coach mode:**
    - `HumanHasBall` and `HumanTeamHasBall` are false in a coached game. Your dribble moves, dunk package, celebration, release meter and timing toast, pass arrow and "YOUR BALL" toasts no longer go to the AI's slot-0 player.
    - Coached games pay rewards but don't add to your player's career totals.
  - **New PlayMode smoke tests** (`Tests/PlayMode/Phase36SmokeTests.cs`): theater marking and jumping both ways, a coached Franchise game, and the shot chart view.
- **Season 8: the Night Lanterns.**
  - A night-market crew that plays zone and throws lobs; leader Juno Akande.
  - Lantern logo, story scenes in English and Spanish, and badges LIGHTS OUT and EIGHT FOR EIGHT.
  - Night Market and Lantern Steps courts, and a Lantern Red store jersey.
  - A Hoops Pass set (Lantern, Paper Soles, Glow Row, pass-only **Lantern Release** celebration) from the pass season of 11 Jan 2027.
  - A corner-threes weekly goal (counted from the shot chart) from the week of 7 Dec 2026.
  - Rivals now rotate every eight seasons; the older rotation tests were updated.
- **SPOT SPECIALIST** (Locker Room): your career chart earns BRONZE / SILVER / GOLD in five areas (rim, mid-range, corners, wings, top of the key).
  - A rank needs makes (20 / 60 / 150) and a make rate at or above what that area usually gives up (+0 / +5 / +10 points).
  - Each rank adds +2 / +4 / +6 points to your own make chance from that area. This applies in single-player games only: never in two-phone, Live or tape games (both phones must simulate the same game), practice, the tutorial or coached games.
  - New ranks show on the post-game screen. Badge SPECIALIST for any GOLD.
- **LIVE SEASONS:** each UTC month is a season. Your best Live rating that month pays when it ends: 100–800 SP by tier, plus the **Live Season Star** banner at ALL-STAR or better. You need 3 Live games in the month to qualify.
  - The Live screen shows the season standing and FRIENDS THIS MONTH (Game Center, friends only).
- **Sharing and friends:**
  - SHARE SHOT CHART (post-game and Locker Room) sends a crisp pixel card through the share sheet.
  - The Skills Gauntlet has FRIENDS' BEST RUNS: `CallerGC_ShowLeaderboard`, a friends-only Game Center board view. It only passed a syntax check against stand-in headers.
- **The Mac project rebuilt, and the first real compile since Phase 25.** `~/RetroHoops` on the Mac had lost ProjectSettings, the scenes, the content data and the TextMeshPro essentials (none are in git; they are all generated).
  - `tools/Restore Project Settings.command` tries Time Machine, then Unity Version Control, then rebuilds them. Neither backup existed, so it rebuilt: `ProjectSetup.RestoreSettingsBatch` (bundle ID, team, build 2, Input System only, IL2CPP), then `SetupBatch` (scenes, content, TMP essentials). Then it runs the Build Check.
  - The repo's `Packages/manifest.json` had a module that doesn't exist (`com.unity.modules.vibration`) and old versions. It now matches the packages of the last working build (Input System 1.20.0, URP 17.6.0, uGUI 2.6.0).
  - **Result:** the Build Check PASSED. Unity compiled the C# and Xcode compiled IL2CPP, Objective-C and Swift. The app launched on the iPhone 17 Pro Max simulator and the court and players draw. Text didn't show (the TMP essentials import doesn't finish in batch mode); it now imports straight from the uGUI package, not yet re-run.
  - `RetroStore.swift`: the Swift 6 warning about NSLock in async code is fixed (synchronous helpers). Not compiled here, since there's no Swift toolchain in the cloud.
  - The Build Check now ignores Xcode projects and apps left over from earlier runs and stops when Unity reports a failed build.

### Phase 37: CLUTCH scenarios, Season 9, device-ready polish, Spanish everywhere

- **CLUTCH** (PLAY ► CLUTCH, `Logic/Progression/Clutch.cs`): 18 late-game situations in three chapters (CRUNCH TIME, ICE IN THE VEINS, LATE-NIGHT LEGENDS).
  - You take over a Caller League team with the score and clock already set, e.g. down two with 20 seconds left, or up one with their ball. Chapter 1 is half court; chapters 2 and 3 are mostly Full Court.
  - A win earns one star. The scenario's goal (win by N, hold them to N, score N yourself, threes, assists, steals, blocks, no turnovers) earns the second, and its bonus the third.
  - Each new star pays 25 SP. Stars open chapter 2 (8 stars) and chapter 3 (20 stars).
  - Badges: CLUTCH GENE (win every scenario) and ICE IN THE VEINS (all 54 stars).
  - `MatchRequest`/`MatchSetup` gained `StartScoreA/B`, `StartClock` and `StartWithBall`. Every other mode keeps its defaults, and the simulation stays deterministic.
  - CLUTCH games don't set game records or count toward Big Wins weeklies, since they start from a set score. Base rewards are a quarter of a normal game's.
  - The commentator knows who was ahead at the start, so a comeback's lead change gets called.
- **Season 9: the Roller Royals.**
  - A roller-rink crew that presses full court; leader Skye Varo.
  - Skate logo, story scenes in English and Spanish, and badges LAST SKATE and NINE FOR NINE.
  - Starlight Rink and Skate Bowl courts, and a Rink Violet store jersey.
  - A Hoops Pass set (Rink, Quad Glide, Mirror Ball, pass-only **Skate Glide** celebration) from the pass season of 22 Feb 2027.
  - A "win N CLUTCH scenarios" weekly goal from the week of 4 Jan 2027.
  - Rivals rotate every nine seasons. As in Phase 36, a career already in season 9 or later sees its rival order shift.
- **Device-ready polish** (from the first Simulator run with text):
  - **Glyphs:** the TextMesh Pro font (LiberationSans) has no ▸ ✓ ✗ ✕ ▶, so those drew as empty boxes on the iPhone. They are now ► √ × throughout.
  - **Text audit:** `tools/TextAudit/text_audit.py` checks every runtime string against the font's code points and lists literal UI text without Spanish. Both are now 0.
  - **Footer:** the main-menu footer said "no purchases", but Live is a subscription (App Review 2.3.1). It now says "plays offline · no ads". LAUNCH_KIT and DESIGN were fixed too.
  - **FrameBudget** (`Logic/Feel/Power.cs`): if a ProMotion phone can't hold 120 fps in play (a quarter of the frames over 1.5× budget across 4 s), the game drops to a steady 60. After a calm minute it tries 120 again, but only once per session.
  - **App Store screenshots:**
    - `tools/App Store Screenshots.command` launches the Simulator build once per shot with `RH_SCREENSHOT=<shot>`.
    - `StoreShots` and the menu open that screen with no first-launch dialogs, attract demo or DEMO PLAY banner.
    - Shots are saved for the 6.9" iPhone (and the 13" iPad if one is installed) with a 9:41 status bar.
- **Spanish:** about 210 new entries cover every menu line the audit found, the CLUTCH and Season 9 text, and the VoiceOver court hint. That hint now also follows left-handed controls. Weekly goals are now translated too. Spanish is now complete for literal UI text; lines built from pieces are spot-checked.
- **Tests:** 770 logic tests pass (19 new in `Phase37FeatureTests.cs`), and there are new PlayMode smoke tests (`Phase37SmokeTests.cs`: a CLUTCH game, a store-shot launch). UnityCheck is clean for both the editor and iOS.

### Phase 38: ship-readiness, gameplay feel, Season 10, CLUTCH expansion

- **Ship-readiness:**
  - Phase 37's Build Check PASSED (Unity, Xcode IL2CPP/ObjC/Swift, Simulator launch), and GitHub has b6ddd72.
  - **App Store screenshots:** the first run made 7 iPhone 6.9" shots at 1320×2868, plus a 2868×1320 Full Court shot.
    - PLAY, CLUTCH and The Park opened correctly.
    - The gameplay, Full Court and Locker Room shots showed the title screen instead. Their scene change was being ignored during the menu's entrance transition; it now waits for the transition (`StoreShotTick`).
    - The 13" iPad run hung while booting; the script now gives up on a device after 3 minutes.
  - **Store listing:** `APP_STORE.md` has a CLUTCH section, "ten rotating rival crews" and the screenshot script. `GAME_CENTER.md` has the new `retrohoops.lb.clutch` board (**YOU:** create it in App Store Connect).
  - **Text audit:** it now also checks ChoiceRow, ToggleRow, SliderRow, Stat, steppers and overlay titles, which found 17 more untranslated lines (all done).
- **Gameplay feel:**
  - **CLUTCH balance:** each scenario was played by the AI for both teams at Caller (24 seeds).
    - PROTECT THE LEAD was 96% and EIGHT DOWN 0%. Six scenarios were retuned (scores, clocks, stories, Spanish).
    - Chapter win rates are now about 49% / 43% / 35%. `Phase38ClutchBalanceTests` keeps the chapters getting harder.
  - **Contest you can see:** while you charge a shot, the meter frame turns half-tinted when a defender is near and fully tinted when you're well contested.
    - It uses `MatchSimulation.ContestLevel`, which reads the same contest the shot model uses and draws no random numbers; a test checks it doesn't change the game.
- **Season 10: the Wash House.**
  - An all-night laundromat crew (pack-line defense, the ball never stops moving); leader Opal Whitaker.
  - Bubbles logo, story scenes in English and Spanish, and badges SPIN CYCLE and PERFECT TEN.
  - Suds Alley and Coin-Op Court, and a Rinse Blue jersey.
  - A pass set (Fresh Press, Tumble Treads, Open All Night, pass-only **Tumble Dry** celebration) from 5 Apr 2027.
  - A Stops weekly goal (steals plus blocks) from 1 Feb 2027.
  - Rivals now rotate every ten seasons.
- **CLUTCH expansion:**
  - **DAILY CLUTCH:** one of the 18 scenarios each day, the same for everyone that day. The first win pays 60 SP, and a run of days is tracked (the best run too). Moving the phone's clock back and forth can't pay twice.
  - **MAKE YOUR OWN:** set both league teams, half or full court, clock (5–155 s), both scores, possession, goal and bonus.
    - Each scenario gets a 13-character code (XXXXX-XXXXX-XXX) with a checksum. COPY CODE puts it on the clipboard, and ENTER A CODE reads a copied code.
    - Editor games pay nothing and keep no stars, records or weekly progress.
  - Game Center board **CLUTCH Stars** (`retrohoops.lb.clutch`).
- **Review fixes:** custom games no longer pay rewards; the daily run shows as broken after a missed day; COPY refuses same-team scenarios; codes with stray high bits are rejected; codes are read with the live catalog.
- **Tests:** 783 logic tests pass (13 new in `Phase38FeatureTests.cs`). UnityCheck is clean for the editor and iOS, and the text audit shows 0/0.

### Phase 39: TestFlight push, onboarding & retention, Season 11, controls feel

- **TestFlight:**
  - The ship pipeline (`tools/Ship Retro Hoops.command`: Unity App Store build, Xcode archive with automatic signing, export and upload) is unchanged. It raises the build number each run (the restored settings start at build 2, so the next upload is build 3).
  - Claude couldn't start it this phase: another Claude session held the Mac's screen. The last upload error ("Error Downloading App Information", 5 Oct) came before the bundle ID was fixed in App Store Connect.
  - App Store Connect leaderboards: Claude opened App Store Connect in the browser pane, but it needs Omari to sign in with his Apple ID. Leaderboards to create: `retrohoops.lb.clutch`, `retrohoops.lb.gauntlet`, `retrohoops.live.monthly` (recurring).
- **Onboarding & retention** (`Logic/Progression/Onboarding.cs`):
  - **JUST PLAY:** on first launch, the tutorial offer has a third button that starts a Quick Call on Rookie right away.
  - **NEXT ►** line under the title: one suggestion based on the career.
    - For a new player it walks through tutorial → first game → Rise → CLUTCH.
    - After that it points at what's waiting today: DAILY CLUTCH, the Daily Challenge, unfinished weeklies, then The Park and Franchise.
  - **Welcome back:** the first open each day (after your first game) pays 20, 30 … 80 SP over a run of days, plus 100 on every 7th day. Moving the clock back can't pay twice. No notifications are used.
- **Season 11: the Sky Kites.**
  - Cliff-top kite flyers who shoot from anywhere and play man-to-man; leader Rin Takahashi.
  - Kite logo, story scenes in English and Spanish, and badges GROUNDED KITES and ELEVEN STRAIGHT.
  - Bluff Top and Pinwheel Lot courts, and a Gust Yellow jersey.
  - A pass set (Tailwind, Updraft, Kite String, pass-only **Kite Run** celebration) from 17 May 2027.
  - A CLEAN WINS weekly goal (win without a turnover of your own) from 1 Mar 2027.
  - Rivals rotate every eleven seasons.
- **Controls & feel** (Settings, `Logic/Input/ControlFeel.cs`):
  - STICK SIZE (small / normal / large: 100 / 130 / 165 travel).
  - STICK DEAD ZONE (low / normal / high: 0.06 / 0.12 / 0.20).
  - HAPTICS STRENGTH (light / normal / strong: every buzz one step softer or firmer).
  - NORMAL is exactly the old behaviour.
- **Tests:** 790 logic tests pass (7 new in `Phase39FeatureTests.cs`). UnityCheck is clean for the editor and iOS, and the text audit shows 0/0.

### Phase 40: Release candidate, App Store requirements, Season 12, highlights, smarter AI

- **App Store requirements** (full list in `APP_STORE_REQUIREMENTS.md`):
  - `PRIVACY.md` lists rodlonbell16@gmail.com; new `SUPPORT.md` (FAQ and contact). Both are public in the GitHub repo and open from Settings ► HELP & SUPPORT / PRIVACY POLICY and the LIVE screen.
  - The privacy manifest declares the System Boot Time API (reason 35F9.1).
  - Four Legacy-mode names that matched real players were renamed.
  - New `tools/AppStoreAudit/app_store_audit.py`: 35 checks (privacy/support pages and URLs, manifest, Info.plist keys, paywall labels, DEV gating, metadata lengths, real names, icon, screenshots). All pass.
- **Release candidate:** release tests load old and damaged saves, run every game mode with random input, and time Full Court (< 0.5 ms a step).
- **Season 12: the Putt Club.** Mini-golf hustlers who play zone; leader Bex Quarrie. Flag logo, EN/ES story, badges IN THE CUP and FULL DOZEN, Hole Eighteen and Clubhouse Roof courts, Fairway Green kit, a pass set from 28 Jun 2027 with the **Putt Drop** celebration, and a PLAYMAKER WINS weekly goal (win with 4+ assists). Rivals rotate every twelve seasons.
- **Highlights & sharing:** each game keeps its top three plays (one per moment). The post-game shows **HIGHLIGHTS (n)** to play them back to back and **SHARE RESULT**, a pixel-art result card (PNG) for the share sheet.
- **Smarter AI:**
  - In-game scouting: after 5+ shots, a player who mostly shoots from deep is closed out tighter, and a driver gets sagged off to wall off the lane. A toast says when the defence adjusts. Smarter difficulties scout harder.
  - SMART DIFFICULTY (Rise, on by default): three straight losses by 6+ make the AI a little less accurate (up to −5%); three straight blowout wins make it sharper. Settings can turn it off.
- **Tests:** 802 logic tests pass. UnityCheck is clean for the editor and iOS; the text audit and the App Store audit pass.

### Phase 41: launch mode — free Live, Retro Hoops Pro, feature freeze

- **Decision (Omari, 7 Oct, after the council review):** ship it and stop building. Feature freeze until launch (rules in `LAUNCH_PLAN.md`).
- **Money model:**
  - **LIVE is free** (Game Center matchmaking). The paywall, MANAGE SUBSCRIPTION and the subscription check are gone. `Pro.LiveIsFree` keeps the server's old check off.
  - **Retro Hoops Pro** (`Logic/Progression/Pro.cs`, `UI/MainMenuController.Pro.cs`) is a one-time **$3.99 non-consumable** (`com.phoronomicstudios.retrohoops.pro`; was $4.99 until 8 Oct). It unlocks FRANCHISE and Rise seasons 2 and up (season 1 stays free), and puts all 40 Hoops Pass items in the locker at once. Free players still earn the items through the pass.
  - The Pro screen shows price, terms, RESTORE PURCHASES, TERMS OF USE and PRIVACY POLICY.
  - At the end of season 1 the Rise hub shows GET RETRO HOOPS PRO; PLAY ▸ FRANCHISE · PRO opens the Pro screen until it's owned.
  - A refund re-locks new seasons but leaves items in the locker.
  - `RetroStore.swift` now sells the Pro product (same StoreKit 2 code; non-consumables have no expiry date).
- **Privacy:** v1 ships with the Live server off, so the privacy manifest declares **no collected data** and App Privacy is **Data Not Collected**. The App Store audit checks the two stay in step.
- **Docs:**
  - `LIVE.md` is rewritten as the IAP setup.
  - `APP_STORE.md`, `SUPPORT.md`, `PRIVACY.md`, `SUBMISSION.md` and `APP_STORE_REQUIREMENTS.md` are updated.
  - New `LAUNCH_PLAN.md`: business setup, a 30-minute playtest script, freeze rules, the TestFlight beta kit with invite texts, a dated timeline to submission (23 Oct), and a build-in-public content calendar.
- **Tests:** 807 logic tests pass (5 new in `Phase41MoneyTests.cs`). UnityCheck is clean for the editor and iOS. The text audit is 0/0, and the App Store audit passes 37/37.

- **8 Oct update (Omari chose the money model):** Retro Hoops Pro is **$3.99** and unlocks **FRANCHISE**, **Rise seasons 2 and up** (season 1 is free) and every Hoops Pass item. PLAY ▸ FRANCHISE · PRO and NEXT ► FRANCHISE open the Pro screen until it's owned. The store text marks Franchise "with Retro Hoops Pro" (guideline 2.3.2). New `CONTENT_KIT.md` (TikTok, Reels, Shorts, X/Threads posts) and `tools/Record Content Clips.command` (Simulator gameplay clips). 807 logic tests pass.

- **9 Oct (Omari asked during the freeze):**
  - **Pump fake:** tap SHOOT and let go at once (under 0.18 s) to show the ball instead of shooting. The closest defender may bite (leaves his feet, off balance 0.75 s: no contest, no block, can't move), so you shoot over him or drive by. Good defenders and smarter AI bite less; each extra fake in a possession works half as often. Toasts: PUMP FAKE / PUMP FAKE! HE BIT / YOU BIT ON THE FAKE. New coach tip when you're tightly guarded. `Logic/Match/PumpFake.cs`.
  - **Rim swing:** after a made dunk the dunker hangs on the rim for 0.7 s and swings back and forth (pendulum that dies down) before dropping; the rim is pulled down 2 px at the slam and 1 px while hanging. The simulation holds the dunker there too (can't move), so it costs you getting back on D. `DunkPath.At(..., hangU, ...)`.
  - Overlay panels are now fully opaque (menu buttons no longer show through, e.g. the Pro screen).
  - 815 logic tests pass (8 new in `PumpFakeAndRimHangTests.cs`).
  - **1-on-1 rules:** make it, take it (the scorer gets the ball back; `rules.oneonone` and any 1-on-1 game). A loose ball that rolls out of bounds (0.25 m past a line) is the other player's: whoever touched it last loses it (a blocker's touch counts), whistle and OUT OF BOUNDS!, then a check at the top. Other modes keep the arcade walls. 820 logic tests pass (5 new in `OneOnOneRulesTests.cs`).

- **10 Oct (Omari asked): Franchise ► CREATE A PLAYER.** ROSTER ► CREATE A PLAYER builds your own player: first and last name, number, skin tone, hair, hair colour, build, height, style of play and a starting level, with a live preview in your club's colours (or start from your Locker Room player). ROOKIE (OVR ~64, age 20, high ceiling, league minimum, allowed over the cap), STARTER (~72, age 24) or STAR (~80, age 26) at market salary with cap room needed. Up to three per franchise, any time except the playoffs, if there's a roster spot; he joins the end of your bench (move him to #1 to control him). Created players can be edited later (name, number, look; not ratings), are saved with the franchise, grow and age like everyone, and AI GMs never ask for them in trades. `Logic/Progression/FranchiseCreate.cs`, `UI/FranchiseScreen.Create.cs`. 827 logic tests pass (7 new in `FranchiseCreateTests.cs`).
- **10 Oct (Omari asked): Rise ► CREATE A PLAYER.** The Rise hub has CREATE A PLAYER (MY PLAYER or A TEAMMATE) and YOUR CREW shows YOU with CREATE/EDIT MY PLAYER plus CREATED TEAMMATES. MY PLAYER edits the player you control in Rise (same as Locker Room ► CREATE; the name is your nickname; training carries over). Up to three created teammates (first/last name, number, look, style), free to put in either crew spot, at the original First Callers' level (style baseline −8); EDIT or REMOVE any time (an original First Caller takes the spot back). Saved in rise.made, registered in the catalog on load. Logic in RiseCreate.cs, UI in PlayerMaker.cs; 4 tests in RiseCreateTests.

### Graphics G1–G3 (16 Oct 2026): character detail, action frames, gameplay effects
- **G1 character detail pass** (`CharacterSpriteGenerator.Polish.cs`, runs on the 2x frame before lighting): a dark seam
  separates each arm from the jersey (back edge only in side view), trim piping round tank-top armholes, small cloth
  creases at the jersey's sides (clear of the number), a darker shorts hem, crew socks with a trim band above the shoes
  (only where the leg still shows skin), eye whites and a warm cheek on front faces, and a one-pixel twill drop shadow
  under jersey numbers.
- **G2 action frames** (`CharacterSpriteGenerator.Actions.cs`): four frames appended after the chest thump, so every
  older index is unchanged — 10 DEFENSE (crouched, wide base, both hands up), 11 SLIDE (wider step, lead arm low in the
  passing lane), 12 DUNK (knees tucked, ball arm all the way up; drawn airborne on purpose), 13 PASS (two-hand chest
  pass). Sheets are now 14 frames wide (448 x 144). `Stances.For` picks them: the passer holds PASS for 0.24 s;
  a defender within 2.4 m of the ball handler sets in DEFENSE and shuffles DEFENSE/SLIDE at 7 steps a second while
  moving slower than 3 m/s; sprinting still runs. The dunk uses DUNK on the way up (hanging on the rim keeps the
  two-hands frame) and the ball rides over the raised hand.
- **G3 gameplay effects** (`GameplayFx`, `BallTrail`): faint floor reflections of every player on hardwood (16%),
  neon glass (13%) and tile (8%) floors, none outdoors; player shadows shrink to 65% and fade to half as a player
  reaches the top of a jump; a three-ghost afterimage trail behind shots, passes and big bounces; a puff of floor
  dust when a player at speed cuts 70 degrees or more; sweat drops off players whose stamina is under 35%.
  Trail, dust and sweat are off with Reduce Motion and Battery Saver.
- Tests: `GraphicsPassTests` (16). Two older tests updated for the wider sheet: Phase 30's frame count (10 → 14) and
  Phase 2's feet-on-the-floor check, which now exempts the airborne dunk frame.
- Not yet seen on a device: everything here was checked with logic tests, UnityCheck and rendered previews only.

### Improvements I1–I3 (16 Oct 2026): what App Store basketball players ask for
Research (App Store listings and reviews of Hoop Land, the 2K Arcade editions, NBA 2K Mobile, NBA Infinite, Basketball
Stars, Basketball Battle and Basketball Arena; TouchArcade, Common Sense Media, a 2026 review-complaint analysis).
Most common asks: no pay-to-win, energy or forced ads (Retro Hoops already has none); a CPU that doesn't feel rigged;
celebrations and a crowd that reacts; flexible game settings; deeper customisation; records and history that last;
offline and couch play (already here). Built:
- **I1 BIG MOMENTS** (`BigMoments.cs`): when the winners' last basket ends the game, teammates run in and mob the shooter
  for 2.4 s before the result screen (GAME WINNER!, or BUZZER BEATER! when the clock showed 1.5 s or less at release;
  off with Reduce Motion, in replays and two-phone games, which still get the call and the crowd). The home crowd
  (your side) chants DE-FENSE when the other team needs 3 or fewer or it's within 3 in the last 30 s, M-V-P for your
  heated-up scorer with the ball, LET'S GO <TEAM> when you're down 4+, at most every 9 s, and the fans clap along.
- **I2 FAIR PLAY** (`FairPlay.cs`): the result screen adds up the make chance of every shot at release, for both sides,
  and shows makes against the odds ("YOU 9/14 (ODDS SAID 8.2) · SPL 7/15 (ODDS SAID 7.4)") with a verdict on the other
  team's shooting. **HOUSE RULES** on Quick Call: game to 11/15/21, clock off or 2/3/4 minutes, 1s & 2s or 2s & 3s,
  win by 2 — remembered in Settings (`settings.houseRules`).
- **I3 GEAR & LEGACY** (`Moments.cs`, `CharacterSpriteGenerator.Gear.cs`): a new GEAR cosmetic slot (Locker Room and the
  Corner Store, SP or street cash): Shooter Sleeves, Cloud Sleeves, Knee Sleeves, Full Compression, Ink Sleeve, Sport
  Goggles, Vet Package (looks only, worn by you like shoes). A MOMENTS book in the Locker Room's records: your
  game-winners and buzzer-beaters, 20-point games (30 in Full Court), perfect shooting nights (5+ shots), 4-steal games
  and 12+ point wins, with opponent and score (newest 40 kept, saved).
- Tests: `ImprovementTests` (11). Not yet seen on a device.

### Improvements I4–I6 (16 Oct 2026)
- **I4 TALE OF THE TAPE** (`TaleOfTheTape.cs`): the tip-off card adds your recent series with tonight's opponent (from
  your last 20 saved games), the last meeting's score, their danger man (best overall) and one KEY TO THE GAME from
  what their roster does best (shooting → close out, finishing → wall off the paint, and so on). Not in 2 Player.
- **I5 STORM THE COURT** (`CourtStorm` in `BigMoments.cs`): a game-winner is now the play of the game (and a
  highlight). Beat a Baller or Legendary CPU, a roster rated 4+ above yours, or win a championship game on a
  game-winner and up to 14 fans leave their seats and run out to the mob (the celebration runs 3.4 s).
- **I6 YOUR LOOK EVERYWHERE**: your shoes and GEAR show on your Baller City avatar, updating as soon as you buy or put
  something on. Gear earned by playing: first buzzer-beater → Sport Goggles, three game-winners → Vet Package, five big
  games → Full Compression. Fix: saved recent games kept Clutch and THE STREETS games as Quick Call after a reload.
- Tests: `ImprovementTests` (now 15). Not yet seen on a device.

### Improvements I7–I11 (16 Oct 2026)
- **I7 CLUTCH CAM** (`ClutchCam.cs`): a shot that could decide the game (it would reach the target, or in the last 4 s
  it would tie or take the lead) plays out at 45% speed, with an "oooh" from the crowd (THE STREETS adds the letterbox).
  The outcome isn't peeked at; the simulation runs the same steps, only slower in real time. Not in replays, drills,
  the demo or two-phone games.
- **I8 MY LINE** (`MyLine.cs`): your live box score under the score bar ("8 PTS · 4/7 FG · 1/3 DEEP · 2 AST"),
  Settings ► LIVE STAT LINE (on by default). Not in 2 Player or THE STREETS (its meters sit there).
- **I9 LIVING ARENA** (`LivingArena` in `BigMoments.cs`): the crowd backs the home side (team A), so road games groan at
  your baskets and chant against you ("ROAD CROWD: DE-FENSE!"); the scorer's nearest teammate leaps up, arms raised,
  after every basket (off with Reduce Motion).
- **I10 GAME GRADE** (`GameGrade.cs`): a letter grade (F to A+) for your game on the result screen, with the two
  biggest reasons (efficient scoring, assists, boards, defence, cold shooting, turnovers).
- **I11 PICK UP WHERE YOU LEFT OFF** (`QuickResume.cs`): a Quick Call game that goes to the background is saved (teams,
  court, difficulty, house rules, score, clock). PLAY ► RESUME tips off again from that score and clock (box score
  starts fresh); finishing or leaving the game clears it; it keeps 7 days.
- Tests: `MorePhasesTests` (5). 1049 logic tests pass. Not yet seen on a device.

### Improvements I12–I17 (16 Oct 2026)
- **I12 RECORD WATCH** (`RecordWatch.cs`): passing one of your one-game records (points, assists, boards, steals,
  blocks, greens) is called live, once per record per game ("NEW CAREER HIGH: 13 PTS"); records under 3 stay quiet;
  Full Court points aren't compared; not in practice, 2 Player, the demo or CLUTCH.
- **I13 NEMESIS** (`Nemesis.cs`): the team that has beaten you most in your recent games (2+ losses, more losses than
  wins) is flagged on the tip-off card; beating them pays +50 SP (play points) on the result screen.
- **I14 FORGIVING METER** (Settings, off by default; `ShotModel.Forgiving`, `MatchSimulation.TuningFor`): your shot
  meter's green window is 50% wider with wider "slightly" bands; make chances, the CPU's meter and everyone else are
  unchanged. Not in the Daily (leaderboard), two-phone/LIVE games, the tutorial or the demo. GREENS (stat, record and
  board) still count by the standard window, so it never pads them.
- **I15 COMEBACKS** (`Comebacks.cs`): climbing back level from 6+ down (12 in Full Court) gets "ALL TIED UP!" and the
  crowd; a win from there is a COMEBACK moment in the book and "COMEBACK COMPLETE" on the result screen.
- **I16 CLOCK CALLS** (`ClockCalls.cs`): LAST MINUTE (games of 100 s or more), 10 SECONDS! in a game within 3, and
  SUDDEN DEATH: NEXT BUCKET WINS when the horn goes tied.
- **I17 YOUR WEEK** (`WeekInReview.cs`): the top of PLAY shows the last 7 days: record, best scoring game, big moments.
- Tests: `SixPhasesTests` (7). 1056 logic tests pass. Not yet seen on a device.

## Deviations from the brief (deliberate)

- "Oakland Voltage" → **Eastbay Voltage** (real city + electric branding sat too close to real pro-sports naming).
- "Solar Kings" → **Solar Crowns** ("Kings" is an existing pro team nickname).
- Northline Owls use ice blue / **black** / silver as primary / secondary / accent: ice blue and silver were too similar for readability.
- Cinemachine omitted: a single fixed half court needs only a small custom camera (Phase 2).
