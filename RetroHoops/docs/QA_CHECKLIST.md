# Manual QA checklist

Run on a real iPhone (and once in the Editor). Tick each item; note device, iOS version, and build number at the top of your copy. **None of these have been run yet.**

Device: ______  iOS: ______  Build: ______  Date: ______

## First launch
- [ ] Project opens in Unity 6 LTS with no compile errors; Project Setup runs and logs a clean content report.
- [ ] EditMode and PlayMode tests pass in the Test Runner.
- [ ] App boots to the main menu in under 3 seconds; no flash of unstyled text.
- [ ] Portrait only; layout respects the notch / Dynamic Island and home indicator.
- [ ] Music starts quietly; button clicks make a sound.

## Quick Call
- [ ] Team cards highlight the selected team; CHANGE OPPONENT never offers your own team.
- [ ] Difficulty choice is remembered next time.
- [ ] Match starts 0–0 with your ball at the check spot.

## Offense
- [ ] Joystick appears where the thumb lands and moves the player in 8 directions.
- [ ] Hold SHOOT: meter fills; release in green shows GREEN; early/late show TOO EARLY / TOO LATE.
- [ ] Shots inside the arc score 1, beyond the arc 2.
- [ ] PASS aims with the stick; receiver arrow shows the target. With a teammate holding the ball the button reads ASK and they pass back.
- [ ] CALL opens the play menu only on offense with your team holding the ball; each play shows its name and teammates move (screen set, cut, clear-out).
- [ ] After a steal or defensive rebound, SHOOT reads CLEAR until you dribble beyond the arc.
- [ ] Shot clock violation flips possession with a whistle.

## Defense
- [ ] Buttons read STEAL / JUMP / SWITCH on defense and return to SHOOT / PASS / DEF on offense.
- [ ] STEAL near the handler sometimes strips; spamming is limited by the cooldown; a miss briefly freezes you.
- [ ] JUMP lifts the sprite; jumping on a close shot can BLOCK (camera shakes if Screen Shake is on).
- [ ] SWITCH moves you onto the ball handler.
- [ ] BOX OUT appears when you're sealing your man during a rebound.

## End of game
- [ ] Buzzer; post-game shows score, player of the game, both box scores (PTS AST REB STL BLK FG%), and rewards.
- [ ] REMATCH starts a fresh game; HOME returns to the menu.
- [ ] Rewards appear in the wallet once. Backgrounding the app on the post-game screen and returning does not grant them again.
- [ ] A tie at the horn goes to sudden death.

## Rise Mode
- [ ] Hub shows stage, objective, energy, chemistry, next opponent and court.
- [ ] Beating all three circuit crews shows CIRCUIT CLEARED and starts the league.
- [ ] Standings update after each game (W-L, DIFF, streak); your row is gold.
- [ ] An event card appears before some games; each choice changes the numbers it describes.
- [ ] Top 4 makes the playoffs; bracket shows semis then final; winning the final shows CHAMPIONS and adds a championship.
- [ ] START NEXT SEASON works after the season ends.
- [ ] Quitting mid-game and returning does not advance the season.

## Practice Lab
- [ ] Free Shoot: 60 s timer, makes/streak shown; teammates hand the ball back.
- [ ] Passing Targets: a gold arrow marks the target; hitting it moves the target and scores.
- [ ] Dribble Lane: next cone is bright, done cones turn green; time stops at the last cone.
- [ ] End card shows NEW PERSONAL BEST when beaten; Locker Room ▸ Career shows the best.

## Locker Room
- [ ] Nickname edits save (max 14 characters, trimmed) and show on the main menu.
- [ ] Training: cost, level, and the reason a purchase is blocked; buying raises the rating; next purchase needs a game first.
- [ ] Style: buy, equip, and owned states; fan-locked items say how many fans they need; equipped jersey/shoes/banner show up in the next game.
- [ ] Career totals match games played.

## Settings
- [ ] Music and SFX sliders change volume immediately and persist.
- [ ] Haptics off: no vibration anywhere. Screen Shake off: no shake on blocks.
- [ ] UI Scale rebuilds the screen larger/smaller; all text still fits.
- [ ] Team Patterns gives each side a distinct pattern in games.
- [ ] RESET SAVE asks for confirmation; CANCEL keeps everything; RESET returns to a fresh career.
- [ ] Credits and licences text is readable.

## Tutorial, Daily, 2 Player, Game Center
- [ ] Fresh install: the main menu offers the tutorial. Each of the 8 steps advances only when done. The end card grants +100 SP once.
- [ ] Daily Challenge shows today's goal. Completing it pays the bonus once and builds the streak on consecutive days. The streak resets after a missed day.
- [ ] 2 Player: both people move independently (keyboard, and two controllers). P2 has a cyan ring and can shoot, pass, steal, and jump. No rewards are given.
- [ ] Game Center (device): turning on Sign in shows Apple's sign-in. After a win, the leaderboard updates. OPEN GAME CENTER shows the dashboard.

## Robustness
- [ ] Incoming call / home swipe pauses the match; RESUME continues.
- [ ] Airplane mode: everything works (no network use).
- [ ] Rapid double taps never load a scene twice.
- [ ] Corrupt `career.json` (edit it on a dev build) shows the SAVE RESET dialog and keeps a backup.
- [ ] 10 consecutive games: steady 60 fps, no memory growth in Xcode's memory gauge.
- [ ] Device heat and battery drain are reasonable over 15 minutes.

## Accessibility
- [ ] Text readable at default UI scale on the smallest supported iPhone.
- [ ] Teams distinguishable in grayscale with Team Patterns on.
- [ ] Every action has visible and audible feedback, not colour alone.
- [ ] COLOR FILTER: RED-GREEN turns the shot meter blue / yellow / violet; BLUE-YELLOW turns it green / amber / red. A red home team against a green away team switches the away team to its other kit with RED-GREEN on.
- [ ] CAPTIONS: announcer calls ("HEATING UP!", "ALLEY-OOP!") appear as a caption bar, even with sound effects muted. Turning on iOS Settings ▸ Accessibility ▸ Subtitles & Captioning ▸ Closed Captions turns them on too.
- [ ] VoiceOver (triple-click the side button if set as the Accessibility Shortcut): swiping right reads menu text and buttons top to bottom; double-tap presses the focused button; a dialog hides the menu behind it; long Settings lists scroll to the focused item. In a match, the stick and buttons still work.

## Phase 19
- [ ] Tabletop 2 Player (no controllers): P1's controls on the bottom half, P2's upside down on the top half. P2 pushing "up" (towards the bottom of the screen) moves their player down the screen. P2's P&R button calls pick and roll.
- [ ] Shootout vs Friend: P1 shoots 60 s, "PASS THE PHONE TO P2", P2 sees "BEAT P1'S n", the result names the winner (or TIE GAME). Nothing is added to your practice bests.
- [ ] iCloud (two devices on the same Apple Account): play a game on device A, open the app on device B (fresh install) → "Your career was loaded from iCloud". With progress on both, the further-along one is offered with LOAD FROM ICLOUD / KEEP THIS ONE. Settings ▸ RESET SAVE removes the iCloud copy too.
- [ ] Season 4: Rise Season 4 brings the Paper Cranes (Juno Vale) with the chapter 4 scenes; the crane logo draws in Locker Room ▸ TEAM ▸ LOGO ICON.
- [ ] TestFlight: `tools/ship_testflight.sh` uploads a build that appears in App Store Connect ▸ TestFlight and installs from the TestFlight app.

## Full Court and winners' ball
- [ ] Quick Call / Rise: after you score you check the ball again; after they score, they keep it.
- [ ] 1-on-1: possession still alternates after a basket.
- [ ] Full Court: tip-off at half court; after a basket the other team inbounds under that basket and brings it up. The camera follows the ball up and down; both hoops are drawn; shots at the bottom hoop arc into it.
- [ ] Full Court: on a steal or defensive rebound nobody jumps or teleports; the stick still moves your player the same way on screen at both ends.
- [ ] Full Court post-game box score lists all ten players (scrolls).

## Phase 20
- [ ] Locker Room ▸ KIT: the preview loops (idle, run both ways, back view, jumper). Every colour chip opens the picker; palette taps and RGB sliders preview live; CANCEL restores.
- [ ] Each style row changes the preview (sleeves, collar, stripes, chest, pattern, shorts length/stripe/waistband, shoe height/stripe/laces/sole). Locked styles list how to unlock them.
- [ ] RANDOMIZE, UNDO (several steps, across slots), RESET, presets. SAVE KITS, leave, come back: kits kept. Scroll position stays put after RANDOMIZE/UNDO.
- [ ] In a game your crew wears the chosen kit; against a team in a similar colour the Away kit goes on with a toast.
- [ ] SHARE KIT: QR + code. Scanning the QR with another iPhone's camera offers to open Retro Hoops and loads the kit into ALT. COPY CODE → ENTER CODE → PASTE → LOAD KIT works; a mistyped code says it doesn't read.
- [ ] TRADING CARD: card shows your player, kit, OVR, stats; SHARE CARD saves to Photos / AirDrop.
- [ ] Play ▸ HOLIDAY GAMES: all four courts load with decorations; the in-season one (October = Halloween) is on top and on the main menu.
- [ ] Full Court: after a basket the inbounder stands on the baseline and must pass ("INBOUND" label); 8-second and backcourt violations are called; tired AI players sub out ("SUB: … IN"); the box score lists subs; the Rise final is Full Court.

## Phase 21
- [ ] Play ▸ FRANCHISE: CHANGE CLUB cycles the eight clubs; START FRANCHISE opens HOME with week 1. PLAY GAME starts a Full Court game against the right opponent on the right court; after it, CONTINUE returns to the front office with the result counted and the rest of the week simulated.
- [ ] SIM GAME / SIM TO PLAYOFFS / SIM PLAYOFFS advance; LEAGUE shows standings, the playoff line, results, top scorers.
- [ ] ROSTER: UP/DOWN changes the depth chart (the top player is the one you control next game); CUT asks first, leaves dead money, and is blocked at 7 players in season.
- [ ] TRADE: NEXT TEAM cycles; ADD players on both sides; the verdict line updates; PROPOSE TRADE only lights up when they'd accept; after the deadline (week 10) trades are closed.
- [ ] Off-season: DRAFT shows the lottery result, SCOUT narrows ranges (visits left counts down), DRAFT picks when you're on the clock; RE-SIGN, then FREE AGENCY (SIGN, cap-room errors), FINISH FREE AGENCY, PRESEASON → START SEASON 2. HISTORY lists the season, champion, awards, transactions. Quit and relaunch mid-off-season: everything is still there.
- [ ] Play ▸ ALL-STAR CONTESTS ▸ DUNK CONTEST: pick a dunk, the combo arrows light up as you press them (on-screen pad, arrow keys/WASD or d-pad), the timer bar runs down, a wrong arrow or timeout gives one retry, the slam meter + JAM! (or Space / A), the dunker runs, jumps, spins on 360s, the rim shakes, judges' cards flip up one by one, total out of 50. Two dunks a round; the final; the winner gets +150 SP.
- [ ] 3-POINT CONTEST: SHOOT opens the Shootout with "BEAT … n" set to the score you need; CONTINUE returns to the contest; round one → final → result.
- [ ] ALL-STAR GAME: Team Sunrise (you at the top of the lineup) vs Team Moonlight, Full Court; CONTINUE returns to the All-Star screen.
- [ ] Rise: from mid-season the hub shows ALL-STAR WEEKEND with n/3 done; events done there are marked DONE; the button goes away when the regular season ends.
- [ ] Locker Room ▸ COURT: every option changes the preview; SAVE COURT; the court is in Quick Call's COURT list and Locker Room ▸ TEAM ▸ home court; PLAY HERE plays on it (also in Full Court if it's your home court); DELETE COURT removes it and a team using it goes back to a street court.
- [ ] Settings ▸ MUSIC PLAYER: every track plays (new songs take about a second the first time), the meter moves, STOP stops; MENU MUSIC and MATCH MUSIC (and SHUFFLE) are used on the menu and in games and survive a relaunch.
- [ ] Back (Escape / controller B) closes the Franchise, All-Star and Music Player panels without also leaving the screen behind them.

## Phase 22
- [ ] Play menu shows four sections (PLAY NOW, CAREERS, EVENTS, WITH FRIENDS) and every mode still opens.
- [ ] Play ▸ LEGACY ▸ START LEGACY: senior year; PLAY GAME starts Full Court with your player first in the lineup (your look); after the game CONTINUE reopens Legacy with the grade, XP and the week advanced. SIM GAME works.
- [ ] Story moments pop up between games; the next game waits until you choose.
- [ ] End of high school: stars and college offers (COMMIT, or DECLARE to skip college). College season, then DRAFT NIGHT ▸ HEAR YOUR NAME names a team and rookie deal.
- [ ] Pro: role/salary line, SKILLS (learn tier 1 then tier 2; points and ratings update), SPONSORS (offers appear with fans; three max; DROP), trainer costs cash. Contract up → agent offers → SIGN. RETIRE from 33; Hall of Fame vote. Quit and relaunch mid-season: everything kept.
- [ ] Play ▸ THE PARK: locked callers show "???"; CALL OUT plays their format (2-on-2 and 4-on-4 look right: everyone placed, plays work); sharp cuts near a defender sometimes show "ANKLES!" and the defender stops for a moment. After the game: rep change, then HOME reopens the Park.
- [ ] Play ▸ TOURNAMENT BUILDER: SIZE / FORMAT / RANDOM FIELD / ADD / MINE; START only lights up with the right count; bracket, PLAY, results; win it → "<NAME> CHAMPIONS"; NEW TOURNAMENT / ABANDON.
- [ ] Settings ▸ MUSIC PLAYER: a new song shows LOADING then plays without the screen freezing; switching songs repeatedly doesn't stutter.
- [ ] Upload: double-click `tools/Ship Retro Hoops.command` (Unity closed) → `Logs/ship_console.log` is written whatever happens.


## Phase 23
- [ ] Home screen icon is the Sunset Swish icon (ball in the net, striped sun, neon grid).
- [ ] iPad (device or simulator): launches portrait full screen; menus fit top to bottom with nothing cut off; in a game the whole half court and both stands fit above the touch controls; Full Court scrolls.
- [ ] Mac (Apple silicon, from TestFlight/App Store as "Designed for iPad"): window opens, keyboard plays (WASD, K shoot, J pass, L steal, C call, Esc pause), touch controls hide after the first key, keyboard hint shows at tip-off; a controller works.
- [ ] Play ▸ WEEKLY & HOOPS PASS: three goals with bars, days left; play a game and the bars move; finishing a goal shows "WEEKLY DONE" on the post-game screen and pays SP.
- [ ] Hoops Pass: tier and XP bar move after games and the daily; reaching tier 5 unlocks gear that shows as OWNED in the Locker Room cosmetics list (and pass gear you don't have shows "HOOPS PASS TIER n" with no BUY).
- [ ] Pause ▸ PHOTO MODE: HUD and controls disappear; drag moves the view, - / + zoom; < > cycles filters; FRAME ON/OFF; SNAP opens the share sheet with a sharp PNG (Save Image → Photos); DONE puts the camera back and shows the pause menu; RESUME carries on.

## Phase 24
- [ ] Launch: the start screen shows the sunset court, the RETRO HOOPS logo and a spinning, bobbing ball with LOADING..., then the main menu.
- [ ] In a game: ring buttons with icons in the lower-right (SHOOT, PASS, DUNK, LAYUP, CALL); on defence they read BLOCK, SWITCH, STEAL and LAYUP/CALL disappear.
- [ ] DUNK next to the rim with a good finisher: goes straight up and dunks without holding anything. From the arc: runs at the rim and finishes. A weak finisher's DUNK becomes a layup. Pulling the stick away mid-drive stops the drive.
- [ ] LAYUP: same, always a layup.
- [ ] With the ball up top, a teammate sometimes cuts to the corner and runs the baseline; PASS turns gold "OOP"; tapping it lobs to them and they dunk it (ALLEY-OOP!).
- [ ] Settings ▸ CUSTOMIZE CONTROLS: drag each button, resize with − / +, overlap warning, RESET, SAVE; the new layout is used in the next game and survives a relaunch; CANCEL discards.
- [ ] Play ▸ FULL COURT: the phone turns to landscape, baskets left and right, players face the way they run, the hoops look right from the side, the camera follows the ball, buttons and stick fit; pause menu and the post-game screen fit; HOME returns to a portrait menu. Rematch stays landscape.
- [ ] Legacy / Franchise / All-Star games (Full Court) also play in landscape.
- [ ] Keyboard U / I and controller RB / LB dunk and lay up.

## Phase 25
- [ ] Locker Room ▸ DUNK PACKAGE: buy and equip Windmill; your next dunk spins the ball round, rises higher and calls "WINDMILL!". Try each package. AI flyers sometimes throw down fancy dunks.
- [ ] Drive at a defender standing in the lane with a good handler: "EURO STEP" and you go round them. A poor handler runs into them.
- [ ] On your drives, a defender from the weak side rotates to the rim and jumps with you (more often on harder difficulties).
- [ ] Landscape Full Court: stands with fans along the top, cheering on scores; the score bar is a small box in the middle; both sidelines visible.
- [ ] Settings ▸ LANDSCAPE: ALL GAMES on: Quick Call, Park, practice turn sideways; 2 Player stays upright; off again: back to portrait.
- [ ] Rise Season 5: the Cassette Club challenge, Echo's story scenes (English and Spanish), REWOUND badge.
- [ ] Hoops Pass third season: Cassette Deck / Tape Runners / Boombox / Skyline Slam.

## Phase 26
- [ ] Two phones: on phone A, 2 PLAYER ▸ TWO PHONES ▸ HOST A GAME, pick teams, WAIT FOR FRIEND; iOS asks for local network access (Allow). On phone B, JOIN A GAME: A's name appears; tap it; both go to tip-off. Each phone controls its own player; scores and positions match on both screens all game.
- [ ] Two phones: the guest's buttons say SHOOT/PASS on offense and BLOCK/SWITCH/STEAL on defense for team B; CALL runs a pick and roll.
- [ ] Two phones: pausing on one phone shows the menu but the game keeps going on both. HOME on one phone ends the game on the other ("YOUR FRIEND LEFT"). Turning Wi-Fi and Bluetooth off mid-game: "CONNECTION LOST".
- [ ] Two phones with different app versions: the guest sees "Both phones need the same version".
- [ ] Two phones: also try iPhone + iPad, and an iPhone with HIGH FRAME RATE on (120 Hz) against one without.
- [ ] Couch Cup with 5 players: byes shown, games play in order, a tie says "PLAY IT AGAIN", the champion is crowned, the bracket survives closing the app. NEW CUP keeps the names.
- [ ] Summer Story: chapter 1 scene plays, then the 1-on-1. Win: CHAPTER CLEARED +150 SP, HOME plays the closing scene, chapter 2 opens. Miss the goal in chapter 3 (win by less than 4): SO CLOSE. Chapter 8 is Full Court in landscape.
- [ ] Story scenes show Nova, Big Sal, Mic Tally and Kojo with their own colours and portraits; Kojo stands on the right.
- [ ] Rise Season 5 story scenes (Echo of the Cassette Club) open without a crash.
- [ ] Mic Tally lines appear under the score bar on dunks, oops, runs and lead changes, not constantly; Settings ▸ COMMENTARY off hides them.
- [ ] Menus: leave a menu untouched for a few seconds with SHOW FPS on; the game stays responsive when you touch it again. Gameplay stays smooth at 60/120.
- [ ] Build size: compare the .ipa size with build 2 (stripping and size-optimised code).

## Phase 27
- [ ] PLAY ▸ LIVE without a subscription: the paywall shows the App Store price (not "$10.99" if your store uses another currency), the terms, RESTORE PURCHASES, TERMS OF USE and PRIVACY POLICY (both links open).
- [ ] Sandbox tester: SUBSCRIBE; the App Store sheet appears; after paying, the LIVE lobby opens. Relaunch: LIVE opens straight away. After ~5 minutes the sandbox renews; after it lapses (or you cancel) LIVE shows the paywall again.
- [ ] Ask to Buy (child account): "Waiting for approval".
- [ ] RESTORE PURCHASES on a reinstall brings Live back.
- [ ] Not signed in to Game Center: LIVE says to sign in and SIGN IN TO GAME CENTER works.
- [ ] Two devices, two Game Center accounts, both subscribed, same build: FIND A GAME on both, they match within a minute, each plays their own team, scores match on both screens.
- [ ] Live result: winner's rating goes up, loser's down by the same amount for equal ratings (±16); record and tier update; leaderboard shows the best rating.
- [ ] Live: HOME mid-game after 20 s counts a loss for you and a win ("... LEFT") for the opponent. Airplane mode mid-game: CONNECTION LOST, no rating change.
- [ ] Live: BACK while searching stops the search.
- [ ] Two phones: after the final, both tap REMATCH → a new game starts on both with the same teams. One taps HOME instead → the other sees "YOUR FRIEND LEFT".
- [ ] Xcode build: RetroStore.swift compiles (Swift) and the In-App Purchase capability is present.

## Phase 28
- [ ] Dunk (DUNK button near the rim, and an AI dunk): the dunker glides in, the raised hands reach the rim as the ball goes in, they hang on the rim for a moment (the rim dips), then drop. Check a short and a tall player, Full Court in landscape, a 360 and a windmill.
- [ ] Alley-oop finish: the same hands-on-the-rim slam.
- [ ] Reduce Motion on: dunks still reach the rim (no spin or ball swing).
- [ ] Two-phone game with dunks: the scores still match on both phones.
- [ ] Server (after `server/README.md` setup and `BackendConfig.Url`): LIVE shows "Signing in to Retro Hoops Live…", then the server rating; FIND A GAME is enabled only after that.
- [ ] Server: two subscribed devices play a Live game; both post-game screens say the result was sent; within seconds a toast shows the rating change; `curl <server>/v1/leaderboard` lists both players.
- [ ] Server: one player taps HOME mid-game after 20 s: the other gets the win when the game settles (immediately or within 10 minutes).
- [ ] Server: sandbox subscription expires → LIVE says the server couldn't confirm it; refund in sandbox → same after Apple's notification.
- [ ] LIVE ▸ DELETE MY LIVE DATA: the server forgets the player; `/v1/me` returns 404 until the next sign-in.

## Phase 29
- [ ] CALL ▸ BACKDOOR: a teammate jogs out to the wing, then sprints to the rim; pass to them on the cut for a layup or dunk. CALL ▸ POST UP: your big walks to the block on the ball side; feed them.
- [ ] The CALL menu (5 plays) fits in portrait and in landscape Full Court.
- [ ] AI teams sometimes run backdoor cuts and post-ups.
- [ ] Heat up (three in a row) on Caller or Legend and drive toward the rim: a second defender comes ("DOUBLE TEAM!"), and passing to the open teammate gets an easy look. On Rookie there's no double team.
- [ ] Street game vs GLIDE (2-on-2) or a 3-on-3 crew with four players: late in the game, tired AI players sub out ("SUB:" toast, arena horn).
- [ ] Sounds: a block or broken ankles gets a crowd "OOOH"; the final buzzer adds an arena horn; a close game in its last 20 seconds gets rhythmic clapping.
- [ ] Tip-off: the TONIGHT AT … card shows both teams and their starters, fades after ~3 s, and a tap skips it.
- [ ] Post-game: TEAM STATS bars are above the box scores and readable on iPhone and iPad.

## Phase 30
- [ ] Double-click `tools/Build Check (Simulator).command` (Unity closed): it ends with BUILD CHECK PASSED and Retro Hoops opens in the Simulator.
- [ ] Three phones: A hosts a TWO PHONES game, B joins, C opens TWO PHONES ▸ WATCH A GAME and taps A's game. C shows the same game (same score, same plays); A shows "1 FRIEND IS WATCHING".
- [ ] C joins ~1 minute into the game: it fast-forwards and then plays along in real time.
- [ ] REMATCH on A and B: C follows them into the new game. HOME on A: C says the players have left.
- [ ] WATCH A GAME before B has joined: A's game isn't listed (or can't be joined) until both players are in.
- [ ] COUCH CUP with 4 names ▸ 2 PHONES on A; B joins: the first-named player is on A, the other on B. After the final, A's bracket has the result; REMATCH on both plays the next bracket game; a tie replays it.
- [ ] Crossover / Double Cross moves show the low crossover pose; Step Back shows the ball-up step-back pose.
- [ ] Locker Room ▸ CHEST THUMP celebration: after scoring, the player thumps their chest twice and raises a fist.

## Phase 31
- [ ] Update over a Phase 30 install: the career is all there; the next launch shows no SAVE FILE CHANGED notice. A NEW IN RETRO HOOPS card shows once.
- [ ] Edit `career.json` (Xcode ▸ Devices ▸ Download Container, change signalPoints, put it back): next launch shows SAVE FILE CHANGED once, the career keeps its games, coins are capped, a `career.edited.*.json` copy exists, and no leaderboard scores are posted.
- [ ] Delete and reinstall the app with an iCloud career: it loads from iCloud with no SAVE FILE CHANGED notice.
- [ ] Two phones: after the final, SAVE GAME TAPE on both. 2 PLAYER ▸ GAME TAPES ▸ WATCH plays the identical game; tapping the court cycles 1x / 2x / 4x; REMATCH watches it again.
- [ ] GAME TAPES ▸ SEND on one phone, RECEIVE A TAPE on another: the tape arrives and plays there.
- [ ] Watch a game on a third phone: the post-game has PLAY OF THE GAME, SHARE HIGHLIGHT and SAVE GAME TAPE.
- [ ] Rise Season 6: the Lighthouse Keepers' intro scene, rival game (they press full court) and win scene; badges LIGHTS OUT / FULL ROTATION; their logo in Custom Team ▸ LOGO.
- [ ] Week of 12 Oct 2026: weekly goals may include deep shots / alley-oops / heat-ups. Pass season from 19 Oct 2026: Beacon / Fog Runners / Lighthouse Beam / Spotlight.
- [ ] First visit to TWO PHONES, LIVE, COUCH CUP, WATCH A GAME and GAME TAPES each shows its card once.
- [ ] VoiceOver on (Settings ▸ Accessibility): swipe through the main menu; every button is read and double-tap presses it; the focus box sits on the right button (if boxes are mirrored vertically, the screen-space convention needs flipping).
- [ ] iOS Larger Text at a big size: menus use the largest UI scale; text still fits on iPhone SE / mini and iPad. Turning MATCH IPHONE TEXT SIZE off goes back to the chosen UI SCALE.

## Phase 33
- [ ] Franchise: sim week by week to the deadline; a TRADE OFFER appears on HOME some weeks; ACCEPT swaps the two players (MOVES shows it); DECLINE clears it; none after week 10.
- [ ] Franchise ▸ ROSTER ▸ TRAINING FOCUS: pick DEFENSE, finish the season; the draft screen shows PLAYER DEVELOPMENT · DEFENSE with each player's overall before → after.
- [ ] Franchise ▸ LEAGUE and the Rise season screen: the bracket picture fits on iPhone SE / mini and iPad; projected seeds mid-season, scores and the champion after.
- [ ] LIVE ▸ INVITE A FRIEND: Game Center's invite screen opens; the friend accepts the notification, their phone opens LIVE and the game starts.
- [ ] After a Live game both tap REMATCH: a new rated game starts without matchmaking again; HOME on one phone tells the other.
- [ ] Game Center ▸ the monthly Live board shows your rating after a Live game this month.

## Phase 34
- [ ] Unity ▸ Window ▸ General ▸ Test Runner ▸ PlayMode ▸ Run All: NewModesSmokeTests pass (tape plays, tape store, sealed save, Franchise).
- [ ] Xcode (generated project) ▸ Build Phases ▸ Compile Sources: our .mm files show `-fobjc-arc`.
- [ ] EVENTS ▸ SKILLS GAUNTLET: four stations; each end screen offers NEXT: <station>; the fourth shows the run total; the Gauntlet screen lists points per station, today's and all-time best; Game Center board updates.
- [ ] Watch AI offense: when the AI drives, its teammates clear to the far side; AI bigs drop-step on the block.
- [ ] Shoot a slightly late jumper: the callout reads e.g. "CLEAN LOOK · SLIGHTLY LATE · 54%".
- [ ] Settings ▸ DIFFICULTY BY MODE: set RISE to LEGEND and THE PARK to ROOKIE; Rise games play at Legend, Park games at Rookie, Quick Call at the main DIFFICULTY.
- [ ] VoiceOver: the menus are read once (no doubled elements).
- [ ] Rise Season 7: the Comet Couriers' intro scene, game and win scene; badges SIGNED FOR / SEVEN FOR SEVEN.

## Phase 35
- [ ] Play a Quick Call game: the post-game shows YOUR SHOT CHART (discs on the spots you shot from, red hot / blue cold, "made/attempted" under each) and a HOT/COLD line.
- [ ] Locker Room ▸ career: CAREER SHOT CHART adds up your games (practice and 2 Player don't count).
- [ ] Franchise ▸ GAME DAY ▸ COACH ▸ COACH GAME: no stick or buttons; the sideline bar shows CALL A PLAY, O: BALANCED, D: MAN, SUBS.
- [ ] Coach: CALL A PLAY ▸ PICK AND ROLL on defense says "ON THE NEXT TRIP" and runs when you get the ball; the list holds the game while open.
- [ ] Coach: tap D: until ZONE; it stays ZONE all game. Tap O: to LET IT FLY; your team shoots more from deep.
- [ ] Coach: SUBS ▸ pick a player ▸ pick a bench player: the button shows SUBS (1); the swap happens at the next dead ball and the new player is drawn in your kit.
- [ ] Coach: AUTO SUBS OFF: nobody on your team comes out unless you call it.
- [ ] Franchise home: TEAM SHOT CHART · YEAR n appears after your first game and resets next season.
- [ ] Legacy: YOUR SHOT CHART THIS SEASON on game day; CAREER SHOT CHART in history.
- [ ] Watch a saved tape: the theater bar shows clock, scrub bar with gold (auto) and pink (yours) ticks, < MARK, SLOWER, PAUSE, FASTER, MARK >, CAMERA, ADD MARK, ALL MARKS.
- [ ] Theater: 0.25x and 0.5x play smoothly; 4x keeps up; PAUSE freezes; drag the scrub bar forward and back (back restarts at that moment).
- [ ] Theater: ADD MARK, leave, watch the tape again: your mark is still there (pink, "* MARK 1").
- [ ] Theater: CAMERA cycles BALL CAM, PLAYER CAM (each player), RIM CAM, CLOSE-UP (closer zoom, follows up and down).
- [ ] Old tapes (saved before this update) still open.
- [ ] Menus: the wipe changes look between screens; on the way into a game a TIP shows while the screen is covered.
- [ ] Dunk or hit a deep shot: a few camera flashes pop in the crowd (none with Reduce Motion). A 6-0 run starts the wave; a close finish has the crowd up and bouncing.
- [ ] Sound: the music dips under the announcer and comes back; a scramble of bounces doesn't get loud; the crowd swells on a big play and in a close finish.

## Phase 36
- [ ] Unity ▸ Test Runner ▸ PlayMode: Phase36SmokeTests pass (theater jumps, coach game, chart view).
- [ ] Theater on a long Full Court tape: no freeze when it opens (status shows MARKING n%); jumps show SEEKING… briefly; going back has no screen wipe or tip-off card.
- [ ] Theater: jump around 30+ times; memory stays flat (Xcode memory gauge).
- [ ] Theater: hold the scrub bar, swipe home, come back: the tape plays again after RESUME.
- [ ] Coach mode: no release meter or "YOUR BALL" toasts; slot-0 AI player uses ordinary celebrations; career GAMES doesn't go up after a coached game.
- [ ] Rise Season 8: Night Lanterns intro, game (they play zone), win scene; badges LIGHTS OUT / EIGHT FOR EIGHT.
- [ ] Locker Room ▸ SPOT SPECIALIST lists five areas with rank and NEXT; earning a rank shows on the post-game screen.
- [ ] Live screen: SEASON line; at the start of a new month after 3+ games, the reward line shows once (+SP, Live Season Star at ALL-STAR+); FRIENDS THIS MONTH opens Game Center.
- [ ] Skills Gauntlet: FRIENDS' BEST RUNS opens Game Center's friends board (signed in), or the sign-in hint (signed out).
- [ ] Post-game and Locker Room: SHARE SHOT CHART opens the share sheet with a crisp picture card.

## Phase 37
- [ ] Unity ► Test Runner ► PlayMode: Phase37SmokeTests pass (CLUTCH game, store-shot launch).
- [ ] PLAY ► CLUTCH: chapter 1 open, chapters 2 and 3 locked with "N more stars"; each card shows teams, situation, goal and bonus.
- [ ] A CLUTCH game starts from the card's score and clock with the right team in possession; the post-game shows ●○○ stars, √/× per goal and +SP for new stars; REMATCH replays it; back on the menu CLUTCH reopens.
- [ ] Win all of chapter 1 with some extra stars: chapter 2 opens at 8 stars.
- [ ] No empty boxes anywhere: menus with ► (Locker Room ► CREATE...), √ on finished Weekly/Daily/Story lines, × on CLUTCH misses.
- [ ] Main menu footer reads "v1.0.0 · plays offline · no ads".
- [ ] Rise Season 9: Roller Royals intro (Skye), they press full court; win scene; badges LAST SKATE / NINE FOR NINE; Skate logo draws.
- [ ] Settings ► ESPAÑOL: PLAY menu, CLUTCH, Live, two-phone, tapes, Franchise and Legacy screens have no English left except names.
- [ ] VoiceOver on, in a game: the court hint says stick on the left (right with left-handed controls), in the chosen language.
- [ ] 120 Hz iPhone with SHOW FPS: smooth games stay at ~120; a struggling device shows "(steady 60)".
- [ ] Run "App Store Screenshots.command" after a Build Check: 7 PNGs per device in ~/RetroHoops/AppStoreScreenshots; the fullcourt one is upright.

## Phase 38
- [ ] Run "App Store Screenshots.command": 01-game and 03-fullcourt show a live game (not the title), 07-locker shows the Locker Room; the Full Court shot is upright (portrait); iPad shots appear or the log says SKIPPED.
- [ ] CLUTCH ► DAILY CLUTCH: win it, +60 SP and RUN 1; play it again: no second bonus; next day RUN 2.
- [ ] CLUTCH ► MAKE YOUR OWN: steppers change values without the list jumping back to the top; the code line updates; COPY CODE then ENTER A CODE reloads the same scenario; same team on both sides shows "Pick two different teams."
- [ ] A custom game: post-game shows stars plus "Custom scenario: stars aren't saved."; SP, career GAMES and weekly progress don't change.
- [ ] Charge a shot open, then with a defender closing: the meter frame goes white → half pink → pink.
- [ ] Rise Season 10: Wash House intro (Opal), they play pack line; win scene; badges SPIN CYCLE / PERFECT TEN; Bubbles logo draws.
- [ ] Game Center (after creating retrohoops.lb.clutch in App Store Connect): CLUTCH Stars posts your total.

## Phase 39
- [ ] Fresh install: the first dialog has PLAY TUTORIAL / JUST PLAY / MAYBE LATER; JUST PLAY starts a Rookie Quick Call.
- [ ] Main menu shows "NEXT ► …" under the title; tapping it opens that mode; it changes as you play (Rise, then CLUTCH, then DAILY CLUTCH…).
- [ ] Open the game on two days in a row (after one game): WELCOME BACK +20 SP, then +30 SP; same day again: nothing.
- [ ] Settings ► STICK SIZE / STICK DEAD ZONE / HAPTICS STRENGTH: LARGE stick needs a longer push; HIGH dead zone ignores small drift; STRONG buzzes harder (device only).
- [ ] Rise Season 11: Sky Kites intro (Rin), win scene; badges GROUNDED KITES / ELEVEN STRAIGHT; Kite logo draws.

## Phase 40
- [ ] Settings ► HELP & SUPPORT and PRIVACY POLICY open the GitHub pages in Safari.
- [ ] After a game with 2+ big plays: HIGHLIGHTS (n) plays them in order; SHARE RESULT opens the share sheet with a result card image.
- [ ] Shoot 5+ threes in a game: "SCOUTED: THEY'RE CROWDING YOUR JUMPER"; drive every time instead: "SCOUTED: THEY'RE WALLING OFF THE DRIVE".
- [ ] Lose three Rise games by 6+: the next game feels a touch easier; Settings ► SMART DIFFICULTY OFF stops it.
- [ ] Rise Season 12: Putt Club intro (Bex), zone defence; win scene; badges IN THE CUP / FULL DOZEN; Flag logo draws.
- [ ] Work through APP_STORE_REQUIREMENTS.md ► You before submitting.

## Phase 41
- [ ] PLAY ▸ LIVE opens the lobby straight away (no paywall, no MANAGE SUBSCRIPTION).
- [ ] PLAY ▸ RETRO HOOPS PRO: price from the App Store ($3.99 in the US), ONE TIME, the five perks (FRANCHISE first), terms, TERMS OF USE, PRIVACY POLICY, RESTORE PURCHASES.
- [ ] UNLOCK PRO (TestFlight or sandbox): the screen switches to PRO UNLOCKED; the Locker Room has the pass jerseys, shoes, banners, celebrations and the Skyline dunk.
- [ ] Reinstall: RESTORE PURCHASES brings Pro back.
- [ ] Rise without Pro: after season 1 the hub shows GET RETRO HOOPS PRO (opens the Pro screen); with Pro, START NEXT SEASON starts season 2 with a new rival crew.
- [ ] PLAY ▸ FRANCHISE · PRO without Pro opens the Pro screen; with Pro the row reads FRANCHISE and opens the GM screen. NEXT ► FRANCHISE does the same.
- [ ] Spanish: the Pro screen and the Rise lock message are translated.

## 9 Oct: pump fake and rim swing
- [ ] With the ball, tap SHOOT quickly: "PUMP FAKE", the ball pops up and you keep it; hold SHOOT: a normal shot.
- [ ] Fake with a defender in your face: sometimes "PUMP FAKE! HE BIT", he jumps and stands frozen; shoot right away: the meter frame shows open (white).
- [ ] Fake twice in one possession: the second works less often. Legend AI bites less than Rookie.
- [ ] Make a dunk: the dunker hangs on the rim and swings left-right, the rim dips, then he drops. Missed dunks don't swing. REDUCE MOTION: hangs without swinging.
- [ ] On defense, jump at a fake: "YOU BIT ON THE FAKE".
- [ ] Menu overlays (e.g. PLAY ▸ RETRO HOOPS PRO) are solid: no buttons showing through.
- [ ] 1-ON-1: score, and you check it up again (make it, take it). Knock the ball or a long rebound out past a sideline: whistle, OUT OF BOUNDS!, the other player's ball at the top. QUICK CALL 3-on-3: the ball still bounces back in off the lines.

