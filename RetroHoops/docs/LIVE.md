# Making money: Retro Hoops Pro (one-time unlock) and free Live

**Phase 41 (Omari's decision, 7 Oct 2026):** version 1 has **one purchase**, **Retro Hoops Pro**, a one-time
**non-consumable** unlock for **$3.99** (changed from $4.99 on 8 Oct, Omari's choice). **LIVE is free** (Game Center matchmaking; Apple runs the servers). A paid Live tier
can come back once there are enough players to match. The old $10.99/month Live subscription is gone from the game.

**What Pro unlocks** (`Logic/Progression/Pro.cs`):
- **FRANCHISE** mode (be the GM of a league club).
- **Rise seasons 2 and up**: eleven more rival crews with their stories, courts and badges. Season 1 is free.
- **Every Hoops Pass item right away** (40: jerseys, shoes, banners, celebrations, the Skyline dunk). Free players still earn them through the pass; a Pro owner gets SP where the gear would be.
- Everything else, including LIVE, Quick Call, Rise season 1, CLUTCH, The Park, Legacy, the contests and every update, stays free. No ads.

Where it shows: PLAY ▸ **RETRO HOOPS PRO** (purchase screen with price, terms, TERMS OF USE, PRIVACY POLICY, RESTORE PURCHASES) the Rise hub when season 1 is done (GET RETRO HOOPS PRO), and PLAY ▸ FRANCHISE · PRO, which opens the Pro screen until Pro is owned.

**Status:** written and unit-tested (807 logic tests), compiled against Unity's reference assemblies. Not yet built in Xcode or tested with a sandbox purchase.

## 1. Paid Apps agreement, tax and banking (once, Omari)

App Store Connect ▸ **Business**: accept the **Paid Apps Agreement**, then add a bank account and the tax forms. App Store Connect won't sell anything until all three are Active, which can take a few days. *(Claude can't do this step.)*

## 2. Create the in-app purchase (once)

App Store Connect ▸ Apps ▸ Retro Hoops ▸ **Monetization ▸ In-App Purchases** ▸ ＋:

| Field | Value |
|---|---|
| Type | **Non-Consumable** |
| Reference Name | Retro Hoops Pro |
| Product ID | `com.phoronomicstudios.retrohoops.pro` (must match `Pro.ProductId` exactly) |
| Price | $3.99 (USD, tier). Accept Apple's prices for other countries. |
| Display name | Retro Hoops Pro |
| Description (≤ 55) | Franchise, Rise seasons 2+ and every Pass item. |
| Review screenshot | PLAY ▸ RETRO HOOPS PRO (the purchase screen). |
| Review notes | See below. |

Add it to the app version under **In-App Purchases** before pressing Submit for Review (a first IAP is reviewed with a version).

If you created the old `com.phoronomicstudios.retrohoops.live.monthly` subscription, leave it **unsubmitted** (or remove it from sale); the game no longer sells it.

## 3. Game Center

Leaderboards: `retrohoops.live.rating`, `retrohoops.live.monthly` (recurring, 1 month), `retrohoops.lb.clutch`, `retrohoops.lb.gauntlet` and the others in `GAME_CENTER.md`. Matchmaking needs no extra setup.

## 4. Testing before release

1. App Store Connect ▸ **Users and Access ▸ Sandbox ▸ Test Accounts**: create a sandbox tester.
2. On a TestFlight build, PLAY ▸ RETRO HOOPS PRO ▸ UNLOCK PRO (TestFlight purchases are free). The screen switches to PRO UNLOCKED; the Locker Room has the pass items.
3. Delete and reinstall: PLAY ▸ RETRO HOOPS PRO ▸ RESTORE PURCHASES brings it back.
4. Rise: finish season 3 without Pro → the hub shows GET RETRO HOOPS PRO instead of START NEXT SEASON; with Pro, season 4 starts (Paper Cranes).
5. LIVE: two devices, two Game Center accounts, same build → FIND A GAME on both. No purchase needed.

## App Review notes

> Retro Hoops is free. LIVE (online play) is free and uses Game Center matchmaking; a match needs a second device on the same build with a different Game Center account (without one, the search keeps looking and BACK stops it). The one in-app purchase, Retro Hoops Pro (non-consumable), unlocks Rise seasons 4 and up and every Hoops Pass cosmetic. To see it: PLAY ▸ RETRO HOOPS PRO. Restore Purchases is on that screen. Everything else is free and works offline.

## How a Live game works

- Each player picks a league team. Game Center pairs players in the same **player group**, which is worked out from the game version, so both always run identical code.
- The player whose Game Center ID sorts first hosts. The host's team is home and the host's home court is used. AI teammates play at Caller difficulty.
- The game uses the same lockstep as two-phone play, with an 8-step (133 ms) input delay for internet latency. Only controller input travels between the phones, and checksums catch any mismatch.
- **Rating:** Elo starting at 1000 (K 32). Tiers are ROOKIE, HOOPER (900), STARTER (1100), ALL-STAR (1300) and LEGEND (1500). Leaving after the first 20 seconds counts as a loss. If your opponent leaves, you win. A dropped connection or a mismatch counts for nobody.
- **Without the server:** ratings live on each player's phone, so a determined cheater could edit their own number.
- **With the server** (`server/README.md`, Phase 28; off in v1): sign-in is checked with Game Center's signature, and ratings change only on the server, when both players' reports agree. The server still has its Phase 28 subscription check; before switching it on with free Live, remove that requirement in `server/src/app.ts` (the game already skips it: `Pro.LiveIsFree`).

## Signed match requests (Phase 32)

`/v1/match/start` and `/v1/match/result` bodies must carry `t` (ms since 1970) and `tag` (HMAC-SHA256, key = the
match key, message = `"{t}|start|{matchKey}|{opponentId}|{seat}"` or
`"{t}|result|{matchKey}|{scoreA}|{scoreB}|{hash}|{outcome}"`, lowercase hex). The server answers 401 for a missing
or wrong tag, or a `t` more than 5 minutes from its clock. The game does this in `Backend.StartBody/ResultBody`
(`server/src/sign.ts` on the server side). Phones need a roughly correct clock (automatic time is the iOS default).

## Friends, rematches and the monthly board (Phase 33)

- **INVITE A FRIEND** (LIVE screen) opens Apple's Game Center invite screen. A friend who accepts the notification lands
  in the LIVE lobby and joins the game. The game uses Apple's
  invite screen, so it needs no Friends-list permission.
- **REMATCH** after a Live game: both tap it and play again with a new seed, a new rated game, over the same
  connection. The server's limit of 5 rated games a day per pair still applies.
- **Monthly leaderboard:** Game Center recurring board `retrohoops.live.monthly` (see `GAME_CENTER.md`), and on the
  server `GET /v1/leaderboard?period=month` (rating points won minus lost in games settled this UTC month).

