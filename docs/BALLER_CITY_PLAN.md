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
