# Game Center setup

Game Center in Retro Hoops is **optional and off by default**. The player turns it on in **Settings ▸ Game Center ▸ Sign in**. Without it, the game works exactly the same offline.

## What's in the project

- **Native bridge:** `Assets/Plugins/iOS/CallerGameCenter.mm` (GameKit). It handles sign-in, leaderboard scores, achievements, and the Game Center dashboard.
- **C# service:** `Core/GameCenterService.cs`. `GameKitGameCenterService` is used on iOS builds and `NullGameCenterService` everywhere else (Editor, other platforms).
- **What gets reported:** `Logic/Progression/Daily.cs` ▸ `Achievements` decides which achievements are earned and which scores are reported. It's pure logic and unit-tested.
- **When it reports:** after every rewarded game, the tutorial, practice and party games, and 2 Player games. Nothing is sent unless the player opted in and is signed in.
- **Xcode setup:** the iOS build post-processor links `GameKit.framework` and adds the **Game Center** capability automatically.

## You need to create these in App Store Connect

In App Store Connect, open **App ▸ Features ▸ Game Center**. The IDs must match exactly.

### Leaderboards (Classic)

| Leaderboard ID | Name | Sort | Score format |
|---|---|---|---|
| `retroball.lb.career_wins` | Career Wins | High to low | Integer |
| `retroball.lb.career_greens` | Green Releases | High to low | Integer |
| `retroball.lb.daily_best_streak` | Best Daily Streak | High to low | Integer |
| `retroball.lb.king_streak` | King of the Court Streak | High to low | Integer |
| `retroball.lb.arcade_clears` | Arcade Ladder Clears | High to low | Integer |
| `retroball.lb.shootout_wins` | Shootout Wins | High to low | Integer |
| `retroball.lb.around_world` | Around the World | Low to high | Elapsed time (to the hundredth of a second) |
| `retroball.lb.win_streak` | Best Win Streak | High to low | Integer |
| `retroball.lb.game_points` | Most Points in a Game | High to low | Integer |
| `retroball.lb.dunk_round` | Best Dunk Contest Round | High to low | Integer |
| `retroball.lb.street_rep` | Street Rep | High to low | Integer |
| `retroball.lb.legacy_points` | Legacy Points | High to low | Integer |
| `retrohoops.live.rating` | Live Rating (best) | High to low | Integer |
| `retrohoops.lb.gauntlet` | Skills Gauntlet (best) | High to low | Integer |
| `retrohoops.lb.clutch` | CLUTCH Stars | High to low | Integer (Phase 38: total CLUTCH stars, 0–54) |
| `retrohoops.live.points` | Live Points (all-time) | High to low | Integer (12 Oct 2026: wins + scoring + time played, see `LivePoints.cs`) |

### Leaderboard (Recurring, Phase 33)

| Leaderboard ID | Name | Sort | Score format | Recurrence |
|---|---|---|---|---|
| `retrohoops.live.monthly` | Live Rating (this month) | High to low | Integer | Starts on the 1st, lasts 1 month, repeats |
| `retrohoops.live.points.monthly` | Live Points (this month) | High to low | Integer | Starts on the 1st, lasts 1 month, repeats |

In App Store Connect choose **Recurring** (not Classic) for this one. The game posts your Live rating to it only in a month you've played Live, so each month's board shows that month's active players.

"Around the World" scores are sent in hundredths of a second (41.23 s = 4123), which is the format Game Center's "Elapsed time (to the hundredth of a second)" expects. A leaderboard only receives a score once you have one: zeros are never sent.

### Achievements

Achievements report 100% when earned. Points total **1,000** (Game Center allows up to 1,000; no single achievement above 100). This list comes from `Achievements.All` in `Logic/Progression/Daily.cs`, and a unit test checks the limits.

| Achievement ID | Title | How it's earned | Points |
|---|---|---|---|
| `retroball.ach.first_win` | First W | Win any game. | 10 |
| `retroball.ach.first_green` | Called It | Hit your first GREEN release. | 10 |
| `retroball.ach.ten_wins` | Double Digits | Win 10 games. | 30 |
| `retroball.ach.hundred_greens` | Green Machine | Hit 100 GREEN releases. | 50 |
| `retroball.ach.circuit_cleared` | Off the Blacktop | Clear The Blacktop Circuit. | 50 |
| `retroball.ach.cup_champion` | Gold Signal | Win The Gold Signal Cup. | 80 |
| `retroball.ach.classic_champion` | First Call | Win the First Call Classic. | 40 |
| `retroball.ach.daily_streak_7` | Every Day | Reach a 7-day Daily Challenge streak. | 50 |
| `retroball.ach.tutorial_done` | Ready to Call | Finish How to Play. | 10 |
| `retroball.ach.heat_check` | Heating Up | Hit three in a row and HEAT UP. | 20 |
| `retroball.ach.alley_oop` | Up Top | Throw or finish an alley-oop. | 20 |
| `retroball.ach.glitch_beaten` | Game Over, Glitch | Clear the Arcade Ladder. | 80 |
| `retroball.ach.first_code` | Old-School | Enter a secret code. | 20 |
| `retroball.ach.all_codes` | Code Breaker | Find every secret code. | 80 |
| `retroball.ach.king_five` | Hold the Court | Win 5 straight in King of the Court. | 50 |
| `retroball.ach.caller_cup` | Cup Run | Win the Caller Cup. | 60 |
| `retroball.ach.all_rivals` | Every Rival | Beat Neon Static, the Sundown Syndicate, the Midnight Tide, the Paper Cranes and the Cassette Club. | 100 |
| `retroball.ach.shootout` | Sharpshooter | Win a Shootout. | 20 |
| `retroball.ach.horse` | Spell It Out | Win a game of H-O-R-S-E against the CPU. | 20 |
| `retroball.ach.around_world` | World Tour | Finish Around the World. | 20 |
| `retroball.ach.couch_game` | Couch Rivals | Play a 2 Player game. | 10 |
| `retroball.ach.your_colors` | Your Colors | Create your own team. | 10 |
| `retroball.ach.long_haul` | Long Haul | Play five Rise seasons. | 50 |
| `retroball.ach.franchise_title` | Front Office | Win a title in Franchise. | 50 |
| `retroball.ach.dunk_contest` | Above the Rim | Win the Dunk Contest. | 30 |
| `retroball.ach.three_contest` | Money Ball | Win the 3-Point Contest. | 30 |

Each achievement needs a title, a pre-earned and an earned description, its points, and a 512×512 or 1024×1024 image. Mark none of them hidden.

## Testing

- **Device or simulator:** Game Center sign-in works on an iPhone or the iOS Simulator with a sandbox Apple Account. In the iPhone's Settings app, go to **Game Center** and sign in.
- **Before submission:** leaderboards and achievements show up only after they're created in App Store Connect and the build uses the same bundle ID.
- **Status:** none of this has been tested on a device yet.

## Privacy

The game sends only scores and achievement progress, and only through Apple's Game Center. App Privacy answers stay **Data Not Collected** for the developer. Confirm this against Apple's current guidance when you submit.
