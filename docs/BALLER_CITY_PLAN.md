# BALLER CITY: plan, audit and status

Branch: `feature/baller-city` (from `c9f6ace`, main). Unity 6000.6.3f1. Started 16 Oct 2026.

BALLER CITY is an explorable pixel-art city in Retro Hoops. You walk between districts, step onto courts, go into the Baller Gym and talk to teammates, rivals and Park legends. Every game you start there is one of the game's own modes, and afterwards you come back to the same spot. You can walk off a Rise or STREETS court straight into the city (LEAVE COURT).

It is offline, has no accounts and fakes no online players. The art is original and generated in code; there are no real teams, players or brands.

---

## 1. Repository audit: integration points used

| Need | Existing code used (exact) |
|---|---|
| Scenes | `Core/SceneNames.cs` (`All` drives Build Settings) and `Editor/ProjectSetup.cs` (`SceneControllers`, `CreateScenes`, `ConfigureBuildSettings`). Builds go through `Editor/ReleaseTools.cs` `Build()`. |
| Scene changes | `Core/SceneFlow.GoTo` (pixel wipe; refuses a second load while one runs) |
| Career / save | `App.Career`, `App.SaveCareer()`, `Logic/Save/SaveCodec` (forgiving JSON; partial class) |
| Starting games | `App.PendingMatch` + `SceneNames.Game`; `MatchRequest` |
| Mode builders | `Streets.Pickup/Opponents`, `StreetsTour.Stops/Open/Challenge/Stars/Find/Register`, `StreetsHome.Defend/Claimed/Challenger/Name`, `Street.Challengers/Challenge/Unlocked/Find/Register/RepNames` (THE PARK), `RivalEngine.Challenge/RivalFor/ChallengeFromWeek`, `CrewEngine.TeammateDefs`, `MatchRequest.PracticeDefault()` + `DrillKind`, `Holidays.Request(HolidayTheme.Halloween, …)`, `Secrets.PlayableTeams/OpponentTeams` |
| Rise hub / Locker Room | `SceneNames.Season`, `SceneNames.LockerRoom` (both `ScreenBase`) |
| Match end / pause | `GameSceneController` (`Continue`, `Quit`, `Update`, `SetPaused`, `_finalShown`) and `MatchHud` (`BuildPausePanel`, `ShowPostGame`, `ShowFinal`, `ShowPracticeEnd`) |
| Characters | `CharacterSpriteGenerator.GenerateSheet(look, KitLook, number)`, the same frame layout as `MatchArt.Slice`, `KitLook.Classic`, `PlayerCreator.BasePlayer` |
| Pixel art | `PixelCanvas`, `TextureFactory.ToTexture`, `PixelFont` |
| Touch | `Controls.VirtualJoystick`, `Controls.TouchActionButton`, `TouchControls.Face`, `ControlFeel` (stick size, dead zone, opacity) |
| Settings | `SettingsData.leftHanded`, `largeButtons`, `reduceMotion`, `controlsOpacity`; `App.ApplyFrameRate()` |
| Text | `Loc.T` (English and Spanish table in `Logic/Localization/Loc.cs`) |

---

## 2. Architecture

The engine-free rules are in `Assets/Scripts/Logic/City/`, which is part of the `RetroHoops.Logic` assembly. They are unit-tested in the .NET harness. Unity only draws them.

- **`CityMap`**: the tile grid (ground types, solid tiles), buildings, props, courts, portals (court, building, district road, exit, station), spawns and hangouts.
- **`CityDistricts`**: three hand-laid districts, each built once and cached:
  - **FIRST CALLERS NEIGHBORHOOD** (36×44): Home, the First Callers HQ (opens the Rise hub), the Baller Gym, the crew's home court, a store and a barber (both "opening soon"), and the road east.
  - **BLACKTOP DISTRICT** (48×52): Sunset Cage, Overpass Park, Static Lot and Pier Nine (all existing courts from the catalog), plus the water, the pier and the road west.
  - **BALLER GYM** (interior, 24×30): the lobby and front desk; the training floor with five Practice Lab stations; doors to the locker room and the Halloween court; LEAVE BUILDING.
  - Also here: `ExitFor(MatchRequest)`, `ReturnTag` / `TryParseReturn` (`"city:district:spawn"`) and `CourtHome`.
- **`CityMovement` / `CityCamera`**: acceleration, walk and sprint, axis-separated move-and-slide in sub-steps (no tunnelling), and `Settle`, which moves a bad saved spot out of a wall. The camera follows with exponential easing and is clamped to the district (centred when the map is smaller than the view). There are three zoom steps.
- **`CityInteraction`**: decides what the action button does: the nearest portal, or an NPC who is clearly closer. District roads are not prompts; walking into one takes you through.
- **`CityTransition`**: the LEAVE COURT state machine `Idle → ExitRequested → Validating → Saving → WalkOff → LoadingCity → Spawn → FreeRoam` (plus `Failed`):
  - A live game needs confirmation; declining cancels back to Idle.
  - A second request while one is running returns `Busy` (reentry protection).
  - Out-of-order steps are ignored.
  - A failed save goes to `Failed`, then `Recover()` returns to Idle with the reason kept.
- **`CityNpcScheduler` / `CityNpcBrain` / `CityPath` / `CityDialogue`**: the people.
  - The cast is your teammates, this season's rival crew, Park legends whose court is in the city, and tour crew leaders whose stop is open.
  - The daily schedule is deterministic (a hash of person, day and time block). Each person is in one place per block, and most are home at night.
  - Brains wander along short BFS paths around their hangout, shoot on courts, cheer, keep apart from each other, get unstuck, and stop and face you to talk.
  - Dialogue uses your real Rise record.
- **`CityActivities`**: what a court, door, station or person offers. Each choice is built with the existing mode builders above, so rules, unlocks and rewards are unchanged.
  - Locked choices give their reason and never carry a request.
  - Every request is tagged with `MatchRequest.ReturnTo`.
- **`CityClock` / `CityOnboarding`**: day and night from the phone's clock (smooth dusk and dawn, lights on), and a three-step first visit (walk, use something, step onto a court) that only moves forward.
- **`CitySaveData`** (`CareerSaveData.city`, save key `"city"`): district, position, facing, last court, onboarding, visits, districts seen, and battery saver.
  - Old saves without it start at home.
  - Bad values (an unknown district; NaN, infinite or off-map positions; an out-of-range facing or onboarding step) fall back safely without costing the career.
  - `Arrive(district, spawn)` puts you at a gate.
- **`CityArtGenerator`** (`Logic/PixelArt`): all city art, in code. Ground tiles (grass with flowers, sidewalk, road with a dashed line, crosswalk, asphalt, hardwood, tile, water, pier, sand), court paint in each court's catalog colours, buildings (brick, siding or stucco, window floors lit at night, door, sign, roof with a water tower or AC units), every prop, and the lamp glow.

Unity side, `Assets/Scripts/City/` (runtime assembly):

- **`CityController`** (the root of `CityScene`):
  - Loads the save and spawns you safely.
  - Builds the world, you (your created player in First Callers colours) and up to 12 people for today.
  - Runs walking (touch stick, plus WASD, arrow keys or a controller), prompts, cards, travel between districts behind the wipe, and the camera. The camera snaps to the art-pixel grid and can zoom.
  - Updates the clock every 20 s, and saves on leaving and on backgrounding.
  - The MENU has RESUME, ZOOM, BATTERY SAVER (30 fps) and MAIN MENU.
- **`CityWorld`**: the baked ground sprite; building and prop sprites depth-sorted by their base, with day and night variants; a darkness layer; and lamp and door glows. Prop sprites are shared by kind, variant and size. Every generated texture is destroyed with the scene.
- **`CityActorView`**: a person drawn from the same generated sheets used on court (2×-detail frames at 32 px per tile), with a run cycle, an arms-up shot or wave, and a shadow. Reduce Motion stops idle bobbing and flicker.
- **`CityHud`**:
  - The floating stick on one half of the screen; an action button whose label says what it does, with the full prompt above it; SPRINT; MENU; the district and time; the onboarding hint; toasts; and choice cards.
  - Left-handed mirrors the layout. Large Buttons makes buttons 1.25× bigger. Control opacity follows the setting.

Game-side changes:

- **`GameSceneController.City.cs`**: LEAVE COURT for Rise, Rival and STREETS games, and for any game started from the city.
  - In a live game it asks first, then ends with no result and no rewards (the same path as QUIT). After the final it just leaves.
  - It saves your arrival spot, then plays the walk-off: you walk to the sideline while your teammates turn and wave (shortened with Reduce Motion). Then the wipe takes you into the city at that court's gate.
  - CONTINUE after a city-started game returns to the same spot. Rise and Rival still continue to the Rise hub as before.
- **`MatchHud`**: LEAVE COURT in the pause menu and on the end screens (only where enabled), a confirm card, and `HideFinal`.
- **`PlayerView.SyncScripted`**: draw-only walk and wave poses. The simulation is never touched.
- **Main menu**: a **BALLER CITY** button next to PLAY. Every existing option is kept.
- **`App.BackToCity`**: the Rise hub and Locker Room opened from the city go back to the city. The main menu clears the flag.
- **`SceneNames.City = "CityScene"`**, registered in `ProjectSetup`. `ReleaseTools.Build` calls `ProjectSetup.EnsureScenesForBuild()`, which creates any missing scene (never overwrites) and syncs Build Settings. The iPhone and TestFlight builds therefore create `CityScene` on their own.

---

## 3. Milestones

| # | Milestone | Status |
|---|---|---|
| 1 | Audit and plan (this doc) | Done |
| 2 | City scene: camera, movement, collision, a neighbourhood | Code done; logic tested; compiles against Unity 6 iOS references; **not yet run in Unity** |
| 3 | Main menu entry and persistence | Code done; save round-trip, migration and corruption tested |
| 4 | LEAVE COURT walk-off from Rise and THE STREETS | Code done; state machine tested; walk-off animation **not yet seen in Unity** |
| 5 | Courts and Rise connected through the existing builders; return to the same court | Code done; return tags tested for every court and station |
| 6 | Roaming NPCs, daily schedules, interactions | Code done; schedule determinism, one place per person, wandering inside walls, and talk tested |
| 7 | Baller Gym: lobby, Practice Lab stations, locker room, Halloween Game | Code done; every drill station, the locker door and the Halloween game tested |
| 8 | More districts, ambient detail, audio, polish | Partly: day and night lighting, lamp glows, ambience on and off by interior. More districts are in the backlog |
| 9 | Tests, Unity compile, QA | Logic tests and the Unity reference compile pass. A PlayMode test was added (every district builds) but **has not run here** (no Unity in the cloud workspace) |

---

## 4. Risks and how they're handled

- **A new scene on an existing project.** Scenes aren't in git. `EnsureScenesForBuild` creates `CityScene` during any release build, and *Retro Hoops ▸ Run Project Setup* does it in the Editor. Until one of those runs, Play from the Editor can't load the city.
- **Rewards from walking off.** LEAVE COURT never applies the match: in a live game it uses the same path as QUIT, and only the city position is saved. This is tested (`Arrive_…_WithNothingElseChanged`).
- **Double loads or half transitions.** `CityTransition` returns `Busy` on reentry, `SceneFlow` refuses a second load, and the city controller's `_leaving` guard stops further input. A failed save recovers to Idle with a toast.
- **Bad saves.** The codec sanitises every city value, and `Settle` moves you out of walls.
- **Performance.**
  - World art is generated once per scene and shared per prop kind.
  - Off-screen people aren't drawn, and there are at most 12 people.
  - No per-frame allocations: reused position lists, struct enumerators, and labels only update when they change.
  - The 30 fps battery saver can be toggled.
  - Not yet profiled on device.
- **Portrait only.** The city uses the app's portrait lock (Full Court still turns landscape on its own).

---

## 5. Acceptance tests

Executed in the .NET harness (`BallerCityTests`, 29 tests): map validity, reachability, drill coverage, movement and collision, camera, interaction, the LEAVE COURT state machine (straight-through, confirm, cancel, busy and reentry, failed save), exit routing, saves (round-trip, older saves, corrupted values, arrival), cast, schedule, NPC brain, paths, dialogue, activities (street courts, home court, gym), personal bests, onboarding and the clock.

To do on device (not yet executed):

1. Main menu ▸ BALLER CITY opens First Callers at Home with the onboarding hint, and the stick walks.
2. Walk into a wall and a building: you slide along them, and walk behind the tree tops.
3. Walk east down the road: you arrive in the Blacktop District at its west road.
4. At Sunset Cage, ENTER COURT ▸ PICKUP 3 ON 3: you play and win or lose; CONTINUE brings you back to the cage gate.
5. Rise game ▸ pause ▸ LEAVE COURT ▸ LEAVE: no result, no SP or fans; you walk off, teammates wave, and you're at the home court in First Callers.
6. A STREETS game final ▸ LEAVE COURT: no confirmation, straight to that court.
7. The gym: each station starts its drill and returns to the station. LOCKER ROOM ▸ Back returns to the gym. HALLOWEEN GAME starts.
8. At night (or with the phone clock set to 22:00), windows and lamps are lit and the darkness and glows show. MENU ▸ BATTERY SAVER holds 30 fps.
9. Left-handed and Large Buttons mirror and enlarge the controls. Reduce Motion: the walk-off is short and the camera doesn't ease.
10. Background the app in the city and reopen it: you're on the same spot.

---

## 6. Backlog

- Multiplayer city: other players walking around, only through existing Game Center or Live services when signed in. No fake players.
- More districts (the waterfront boardwalk, downtown with the arena, the school yard) and the store and barber interiors (cosmetics through the existing Locker Room and Kits).
- Weather (rain puddles, wind) as an option.
- A city map screen and fast travel between visited districts.
- Ambient pedestrians and traffic (pooled), birds and sounds per district.
- NPC conversations that react to tour stars, the Park rep and homecourt status in more detail.
- Seasonal decorations (Halloween pumpkins in October, using `Holidays.InSeason`).

---

## 7. Update, 17 Oct 2026: missions, more courts, Holiday Park and the CPU crews

- **Two new districts.** Both connect through the avenue in First Callers (north and south).
  - **HOOP HEIGHTS** (52×72): six courts, each with its own look:
    - Coin-Op Court: a neon grid floor with arcade cabinets.
    - Skate Bowl: tiles with ramps.
    - Lantern Market: tiles under paper lanterns, with market stalls.
    - Night Market: asphalt with stalls.
    - Rooftop Ring: a fenced deck.
    - Starlight Rink: maple with neon edges and bleachers.
  - **HOLIDAY PARK** (44×48): the four Holiday Game courts, dressed for their holiday and open all year:
    - Candy Cane Court: candy-cane lines, snow, lit trees and snowmen.
    - Pumpkin Patch Court: cobwebs, a moon in the key, bats and jack-o'-lanterns.
    - Egg Hunt Court: painted eggs.
    - Firework Court: a striped key, stars and bunting.
- **Every court has its own design.** `CityCourtLook` sets the floor style (asphalt, hardwood, neon grid, tiles, rubber), a centre logo, neon edges and the holiday dressing. These are painted by `CityArtGenerator` from each court's own catalog colours. The city has 17 game courts (plus the gym floor), and a test checks no two look the same.
- **CPU crews** (`CityCrews`, 14 crews). Every court in the two new districts, plus four Blacktop courts, has a crew of three players who all play one style:
  - The styles: sharpshooters, high flyers, pass first, defense, iso handles, run and gun, big men, crafty vets, two-way, closers and tricksters.
  - They are built from the game's own archetypes and defensive schemes, so the AI really plays that way.
  - Each crew is **BALLER** (rated about 82) or **LEGENDARY** (about 89). These are two new difficulty levels above Legend: faster reactions, better decisions and release timing, and never extra speed (the content validator's fairness rule).
  - BALLER and LEGENDARY are kept out of the normal difficulty pickers.
  - LEGENDARY crews open after you beat 3 BALLER crews.
  - Games use THE STREETS rules on the crew's court.
  - The crews walk their courts in the city; you can CHALLENGE them in person or from the court card.
- **Missions** (`CityMissions`): 21 missions in 5 chapters (NEW IN TOWN, MAKE A NAME, CREW HUNTER, HOLIDAY SEASON, LEGENDARY).
  - They cover exploring, talking, winning on new courts, beating specific crews, holiday wins, single-game lines (points, shots from beyond the arc, assists, steals plus blocks, ankle breakers, winning margin) and a gym drill.
  - Each one pays SP and fans once. The next chapter opens when one is done.
  - Find them under MENU ▸ MISSIONS in the city. The end-of-game screen of a city game lists crews beaten and missions completed.
- **Saves:** the mission and crew progress is in the city save. Unknown ids and impossible numbers are dropped on load.

Tests: `BallerCityMissionsTests` (17) plus the updated `BallerCityTests`. The full logic suite is 978 passing, the Unity reference compile is clean, and the Spanish text and App Store audits pass. Not yet run on the phone.

---

## 8. Phase 3, 17 Oct 2026: new districts, the map and fast travel, the shops, and the living city

- **New districts:**
  - **THE BOARDWALK**, east of the Blacktop: Boardwalk Slab, Sundown Yard, Lighthouse Point, and Ferry Deck on the pier, plus the lighthouse, snack stalls, umbrellas, palms and arcade cabinets.
  - **DOWNTOWN**, east of Hoop Heights: Rain Alley, Tram Yard, Record Shop Roof and Night Bus Depot around Baller Tower and the Baller Arena (a landmark; its doors aren't open yet).
  - **SCHOOL YARD**, west of Holiday Park: Pinwheel Lot, Snowline Park and Canyon Rim in front of Baller City High.
  - That makes 26 game courts, each with its own look.
- **8 more crews**, for 22 in total: SAND SHARKS, SUNSET SQUAD, THE KEEPERS (LEGENDARY), LAST STOP, NIGHT RIDERS (LEGENDARY), THE B-SIDES, RECESS and VARSITY (LEGENDARY). Their styles: hustle, inside-out, paint protectors, slashers, scorers, space and pace, high energy, and fundamentals.
- **MAP and fast travel** (`CityAtlas`), under MENU ▸ MAP. The districts appear as blocks on a grid with the roads between them, and each block shows its courts and how many of its crews you've beaten. Tap a district you've already walked into to fast travel to its main entrance. A test checks that the map matches the real roads.
- **CORNER STORE** (`CityShop`):
  - Six pieces of the game's own gear on the shelves each day (jerseys, shoes, celebrations, dribble moves, dunk packages), a new mix every day.
  - It uses the same buying rules as the Locker Room: fans required, Hoops Pass items never sold.
  - Anything you own can be put on there.
- **FRESH CUTS** (`CityBarber`):
  - Six hairstyles and five hair colours for the player you made in the Locker Room.
  - The first cut is free; after that a cut is 40 SP and a colour is 25 SP. You see your new look straight away.
  - The default Rook keeps his look.
- **The living city:**
  - **Passers-by** on the sidewalks: generated people, never real players, fewer at night.
  - **Cars:** they drive on the right on the east-west roads, stop for you and loop round.
  - **Weather** (`CityWeather`): the same for a whole day in each district; mostly clear, with clouds, rain and wind, and snow instead of rain from December to February. It shows as dimming and pooled particles; there's no rain or snow with Reduce Motion. MENU ▸ WEATHER turns it off.
  - **Court sounds:** dribbles and swishes from the nearest player shooting.
  - **Seasonal decorations** (`CityDecor`, from the calendar): jack-o'-lanterns now (October), lit trees and snowmen in December, eggs at Easter and bunting for the Fourth. They aren't solid and are never placed on roads or doors.
- **Chapter 6, ALL CITY**: visit the three new districts, fast travel, buy gear, get a haircut, beat 12 crews, win 25 city games, and beat every LEGENDARY crew. Chapter 5's last mission is now "beat 6 LEGENDARY crews", so new crews don't move its goal.

**Tests:**
- `BallerCityPhase3Tests` (13 tests); the full logic suite passes, 991 of 991.
- The Unity reference compile is clean, and the Spanish text and App Store audits pass.
- Not yet run on the phone.

---

## 9. Phase 4, 17 Oct 2026: THE STREETS moves into BALLER CITY, and CASHED OUT

- **Main menu:** the separate THE STREETS button is gone. The BALLER CITY button carries a green graffiti "THE STREETS" tag.
- **Graffiti walls** in the Blacktop District, Hoop Heights, Downtown, the Boardwalk and the School Yard, by the street courts.
  - Each wall is tagged THE STREETS in outlined spray letters with drips, and says "THINK YOU CAN HANG?".
  - Walk up to a wall for three choices:
    - **RUN IT AT** the nearest street court (a pickup game that brings you back there afterwards).
    - **THE STREETS HQ**: the full hub, with the Street Tour, your crew, Homecourt, gear, Live and games with friends. Its BACK button returns you to the city.
    - **CASH & MILESTONES.**
  - The card leads with "The street courts are meant for legends. Think you can hang?".
- **CASHED OUT** (`StreetCash`). It counts in every street game (THE STREETS, THE PARK and the city crews):
  - A win pays $5,000. On a streak of three or more wins in a row, every win pays $10,000.
  - Scoring more than 20 yourself pays a secret extra $20,000, even in a loss.
  - Bonus SP: +10 for playing, +20 for a win, +25 for leading everyone in scoring.
  - **12 milestones**, two of them secret, with SP rewards for streaks, being the top scorer, games played, wins, and the bank ($100K, $500K, $1M).
  - A big graffiti-style **CASHED OUT $X** stamp shows on the end screen.
  - Street cash is play money. The Corner Store takes it for gear at the item's SP price × 100.
  - Your bank shows in the city's top bar and in THE STREETS hub.
- **The CPUs catch on:** each game over 20 points raises your HEAT, and ordinary games cool it off. When it passes halfway, the end screen says "Word's out. The CPUs are keying on you now."
  - While the HEAT is up, street defences key on you: the on-ball defender plays up to 0.35 m tighter, and help defenders double you near the rim.
  - These are only positioning and decisions. Nobody gets faster, so the fairness rule still holds, and nothing changes when the HEAT is zero.
- **Fix:** a city crew now always plays at its own level (BALLER or LEGENDARY), even if Settings ▸ DIFFICULTY BY MODE says otherwise.
- **Pixel font:** gained ? $ and , (a photo-mode caption test now uses @ for an unsupported character).

**Tests:**
- `StreetCashTests` (11), including one checking the defender plays you tighter when the CPUs are keying on you.
- The full logic suite is 1,002 passing; the Unity reference compile is clean; the Spanish text and App Store audits pass.
- Not yet run on the phone.

---

## 10. Phases 5–7, 17 Oct 2026: the Baller Arena, City Challenges and call-outs, and your crib

**Phase 5: BALLER ARENA**
- The arena Downtown now has an open door (ENTER ARENA). Inside are a polished maple championship floor with neon edges, packed stands on three sides, a scoreboard and a ticket desk.
- The **CITY CHAMPIONSHIP** (`CityArena`) is one run a day:
  - A semifinal against a BALLER crew, then the final against a LEGENDARY crew. The draw is the same all day and new each day.
  - Winning the final pays the title, a trophy, **$50,000** and +250 SP. A loss ends the run until tomorrow.
  - You get in after beating three BALLER crews.
- The new catalog court `court.baller_arena` (League circuit) is where these games are played.
- Arena games are still that crew's game, at its own difficulty, so they count for crews beaten and missions.

**Phase 6: CITY CHALLENGES and CALL-OUTS**
- **City Challenges** (`CityDaily`), under MENU ▸ CITY CHALLENGES: three a day, the same all day.
  - The pool: win in a given district, score N, 3 assists, 3 steals plus blocks, beat any crew, play 3 games, or win at night.
  - Each pays **$7,500**, and finishing all three pays **$15,000** more. Games started in the city count.
- **Call-outs** (`CityCallout`): once a day, a crew that plays in the district you walk into sends its leader to wait where you came in ("... is calling you out!"). Tap ACCEPT CALL-OUT; winning pays an extra **$10,000** on top of the usual payout. Win or lose, that's it for the day.

**Phase 7: YOUR CRIB**
- Your front door in First Callers now opens **YOUR CRIB** (`CityCrib`):
  - **Trophy case**: a gold cup per City Championship and a medal per street milestone.
  - **Banner wall**: a pennant in the crew's colours for each crew you've beaten.
  - **The safe**: your bank.
  - **The wardrobe**: opens the Locker Room.
- The trophy case and banner wall are drawn from your save each time you walk in.

**Chapter 7, BIG TIME:** walk into your crib, walk into the arena, win a call-out, finish a day's City Challenges, win the City Championship, earn $250,000, and win three titles (DYNASTY).

**Tests:**
- `BallerCityBigTimeTests` (12); the full logic suite passes, 1,013 of 1,013.
- The Unity reference compile is clean, and the Spanish text and App Store audits pass.
- Not yet run on the phone.

---

## BALLER WORLD (W1–W10), 28 Oct 2026

The city gets a purpose: a comic you play through, a guide to point the way, daily achievements, side jobs, Big Runs for big street cash, and four more regions to drive to. All play money, all offline, no new data collected.

**W1: THE COMIC** (`CityStory`, `ComicArt`, `ComicCast`)
- 11 issues: NEW KID ON THE BLOCK, THE FLYER, CONCRETE JUNGLE, KING OF NEW YORK, WEST COAST, GOLDEN HOUR, BRIGHT LIGHTS, THE MAIN EVENT, FOG CITY, THE TOWN, ALL-WORLD.
- Each issue opens with comic pages (pixel backdrops, halftone, speech, SFX), sets 3–4 goals, and pays cash and SP when it's done. Original cast: DEE, your agent, and the villain MR. VELVET.
- The finale is THE VELVET INVITATIONAL at the Baller Arena against the VELVET SQUAD.
- MENU ► THE COMIC shows the goals, rereads the issue, and turns the guide on or off.

**W2: THE GUIDE** (`CityGuide`): an objective ticker at the top and an arrow with a distance. It finds a route across districts, and across regions it says to drive. It tracks the comic or your side job.

**W3: DAILY ACHIEVEMENTS** (`CityAchievements`)
- Six a day (the same all day). Hitting 3, 5 and 6 pays BRONZE, SILVER and GOLD ($10K / $15K / $25K plus SP).
- A bronze day keeps the streak alive, with bonuses at 3, 7, 14 and 30 days.
- 23 badges. MENU ► ACHIEVEMENTS.

**W4: SIDE JOBS** (`CitySideJobs`): 27 jobs from locals marked "!". Kinds: win, score or margin at a court, beat a crew, timed errands, and find-the-items. They're comic-panel pitches, one active at a time.

**W5: BIG RUNS** (`CityBigRun`): a three-round bracket per region, once a day, entered at the region's poster. The purse grows each round (up to $250K in the Bay) and doubles at weekends. Win all three for the region's KING title.

**W6: THE WORLD** (`World`, `HighwayDrive`, `CityDrive`)
- Five regions: Baller City, New York, Los Angeles, Las Vegas and The Bay. The comic opens them in order.
- YOUR RIDE stands in each main district, and MENU ► WORLD MAP shows every region.
- THE DRIVE is a three-lane highway run with steering, boost, traffic, near misses and the roadside changing by biome. It's tuned to about 22 s, and a catch-up assist guarantees arrival under 28 s. A clean drive pays a bonus.

**W7–W10: the regions:** two districts each, with their own building styles, ground, props, six courts and four crews (two LEGENDARY).
- **New York:** brownstones, glass towers, a subway, a cage court, a rooftop, a pier.
- **Los Angeles:** a beach boardwalk, lifeguard towers, bungalows, Spanish-mission buildings, a taco truck, murals.
- **Las Vegas:** a neon strip, a fountain, a desert edge, a motel. No casino or gambling imagery.
- **The Bay:** painted Victorians, cable cars, a bridge, a freeway, warehouses, the port.

**Tests:** `BallerWorldTests` (18) cover these:
- every region and district is reachable and walkable
- every court id exists
- every side-job spot is walkable
- the story advances
- achievements pay once
- the save round-trips
- the drive's worst case finishes under 30 s

The full logic suite passes (1,086 of 1,086). The Unity reference compile is clean, and the Spanish text audit, App Store audit and release gate pass.

**Not yet done:**
- It hasn't run in the Unity editor or on a phone.
- Comic dialogue, side-job pitches and achievement texts are English-only in Spanish mode for now.
- The 24 new courts also appear in Quick Play's court list.
