# Retro Hoops launch plan (from 7 Oct 2026)

Decided on 7 Oct after the council review:
- **Ship it, stop building.** Feature freeze until launch.
- **Money (v1):**
  - LIVE is free.
  - The one purchase is **Retro Hoops Pro**, a one-time **$3.99** unlock for FRANCHISE, Rise seasons 2+ and every Hoops Pass item (Omari's choice, 8 Oct) (`LIVE.md`).
  - A paid Live tier comes back only once there are enough players to match.
- **Target:** submit for review by **Fri 23 Oct**, launch the week of **26 Oct**.

---

## 0. Business setup (Omari, today; Claude can't do these)

App Store Connect is open in the browser pane. Sign in with your Apple ID, then:

1. **Business**: accept the **Paid Apps Agreement**.
2. In the same place, add your **bank account**.
3. Then fill in the **tax forms** (W-9 for the US).
4. All three need to say **Active**. That can take a few days, so start today. Nothing is sold until they're Active, but TestFlight works meanwhile.
5. Tell Claude when you're signed in. Claude then creates:
   - the leaderboards: `retrohoops.lb.clutch`, `retrohoops.lb.gauntlet`, and the recurring `retrohoops.live.monthly`;
   - the **Retro Hoops Pro** in-app purchase;
   - the TestFlight groups.

   Each save is shown to you first.

## 1. Playtest kit: your 30 minutes with the build

Install from the **TestFlight** app on your iPhone:

- **Build 3** (Phase 40) is there now.
- **Build 4** (Phase 41, with Pro) follows once it's uploaded.

Play it like a stranger: no menus you already know, sound on, one hand, then two.

| Minute | Do this | Watch for |
|---|---|---|
| 0–3 | Fresh install. First dialog: **JUST PLAY**. | Do you know what to press in the first 10 seconds? Does the first game feel winnable? |
| 3–8 | Finish that game. Look at the post-game, then **NEXT ►** on the main menu. | Is the score clear? Does NEXT point at something you want? |
| 8–14 | **RISE MODE**: play two games. | Do the shot meter, passing and stick feel good? Is anything laggy, tiny or hidden under your thumb? |
| 14–18 | **CLUTCH**: one scenario, then DAILY CLUTCH. | Is the goal of the scenario obvious? |
| 18–22 | **FULL COURT** (5 on 5). | Frame rate, readability, camera. |
| 22–25 | **LIVE** (free now): FIND A GAME. If a second phone is free, try a real match. | Does it explain what's happening while it searches? |
| 25–28 | **PLAY ▸ FRANCHISE · PRO** (build 4): it opens the Pro screen. Unlock it ($3.99; TestFlight purchases are free). | Is it clear what you get? Does FRANCHISE open now, and did the Locker Room fill up? |
| 28–30 | **Settings**: text size, stick size, Spanish. | Anything cut off or untranslated? |

**For every problem, write one line:**

```
[screen] what happened → what you expected  (bug / feel / confusing)  — how bad 1-3
e.g. [Rise game] shot meter hidden under my right thumb → see it above the button (feel) — 2
```

Send Claude the list as it is. It becomes the fix list. In TestFlight you can also take a screenshot in the game and tap **Share Beta Feedback**, which attaches the device details.

## 2. Feature freeze (until launch)

- **Allowed:**
  - bug fixes, crash fixes and performance;
  - text and translation fixes;
  - balance numbers;
  - anything a tester finds confusing;
  - App Store / App Review requirements.
- **Not allowed:** new modes, new seasons or rivals, new settings, new systems, "quick extras".
- Exception (9 Oct): Omari asked for **pump fakes** and **swinging on the rim after dunks**; both are in build 5.
- Further exceptions (9–10 Oct): 1-on-1 make-it-take-it + out of bounds (build 6), Franchise create-a-player (build 7), Rise create-a-player (build 8).
- **11 Oct: v1 is FEATURE-COMPLETE (Omari's call after council review #2).** Build 8 is the v1 feature set. From here: fixes only, App Store items, the build-number line in Settings. Every new idea goes to the v1.1 list below.
- An idea during the freeze goes in **"After launch"** at the bottom of this file, not in the build.
- Every build still passes the logic tests, UnityCheck, the text audit, the App Store audit and the Build Check before upload.

## 3. One-week TestFlight beta (10–20 outside testers)

### Set up (Claude can do this once you're signed in; each save is shown to you first)

- **TestFlight ▸ External Testing ▸ ＋ group** "Beta crew".
- Add the latest build. The first external build goes through **Beta App Review**, usually within a day.
- **Test Information:**
  - Beta App Description: *Retro Hoops is an original pixel-art 3v3 street basketball game. Everything works offline; LIVE online games are free.*
  - Feedback email: rodlonbell16@gmail.com.
  - Sign-in required: No.
- **What to Test:**

  > Thanks for testing! Play like you normally would — no need to finish everything. We especially want to know:
  > 1) Did the first game make sense without help? 2) Do the controls feel good on your phone? 3) Anything confusing, too hard, too easy, or broken?
  > Take a screenshot in the game and tap "Share Beta Feedback" to send it straight to me. Purchases in TestFlight are free.

- Turn on the **Public Link** (cap it at 50 testers) for followers and communities. Friends and crews can get a direct email invite or the same link.

### Invite messages (copy and paste)

**Friends & family (text message):**

> Yo — I built a retro basketball game for iPhone and it's almost out. Want to test it this week before anyone else? 15 minutes is plenty. Install TestFlight from the App Store, then open this link: [PUBLIC LINK]. Tell me straight up what sucks 🙏🏀

**Ball and skate crews (in person, or in the group chat):**

> Made a pixel streetball game — 3v3, The Park, call out street legends, 1-on-1 to 11. Need people who actually hoop to tell me if it feels right. TestFlight link: [PUBLIC LINK]. Beat my CLUTCH score and I'll put your crew name in the credits.

(Only promise credits if you'll add them. That's a text-only change, so it's allowed in the freeze.)

**Followers (Instagram / TikTok / YouTube Community caption):**

> I built a whole basketball game with AI in a few weeks. Retro Hoops — pixel art, 3v3 streetball, rival crews, free online games. 20 beta spots for iPhone this week. Link in bio → comment "HOOPS" and I'll send it. #indiegame #buildinpublic #pixelart #basketball

**Online communities:** r/TestFlight, r/iosgaming (check each sub's self-promo rules first; some only allow it on certain days), and indie-game Discords with a #playtesting channel.

> **[TestFlight] Retro Hoops — original pixel-art 3v3 streetball (iPhone, free)**
> Solo dev here. It's an arcade basketball game: 3v3 and 5v5, a Rise career against 12 rival crews, CLUTCH late-game scenarios, The Park 1-on-1s, free online Live games via Game Center, and it all works offline. No ads, no tracking. Looking for ~20 testers for a week, honest feedback welcome (especially controls and first-minute confusion).
> Link: [PUBLIC LINK]

### What to ask testers at the end of the week (a 5-question form or DM)

1. How many times did you open it this week?
2. What was the most fun thing?
3. Where were you confused or annoyed?
4. Would you pay $3.99 once for Franchise, more Rise seasons and every cosmetic? (Yes / Maybe / No — why?)
5. Would you tell a friend about it? (0–10)

**Go / no-go for submitting:**

- no crashes reported;
- most testers finished a first game without help;
- answers to question 5 average 7 or more.

Otherwise fix the top three complaints first and run 3–4 more days.

## 4. Timeline

| When | Who | What |
|---|---|---|
| **Wed 7 – Thu 8 Oct** | Omari | Business setup (agreement, bank, tax). Play build 3 for 30 min and send the list. |
| **Thu 8 Oct** | Claude | Upload build 4 (Phase 41: Pro + free Live). Once Omari is signed in, create the leaderboards, Pro and the beta group. |
| **Fri 9 – Sun 11 Oct** | Claude, Omari | Fix Omari's list (build 5). Submit the beta for Beta App Review. Post "beta spots open". |
| **Mon 12 – Sun 18 Oct** | Testers | Beta week. Claude fixes in daily builds when needed. Omari forwards feedback. |
| **Mon 19 – Wed 21 Oct** | Claude, Omari | Fix the top three complaints. Final screenshots (Claude re-runs the script). App Privacy: Data Not Collected. Age rating. Metadata pasted in. |
| **Thu 22 – Fri 23 Oct** | Omari | **Submit for Review** with Retro Hoops Pro attached. Choose "Manually release". |
| **~26–30 Oct** | Omari | Approved → **release**, launch posts the same day. |

## 5. Build in public (start this week)

This fits your **Claude/AI content lane**. The story — *"I built a full basketball game with AI"* — works for people who don't play basketball games, and every clip ends on a reason to join the beta and, later, to download.

Cadence: **3–4 posts a week** on TikTok, IG Reels and YouTube Shorts (the same clip), the text version on **X / Threads**, plus one long-form YouTube video after launch.
Ready-to-post hooks, captions and X/Threads text for every post below are in **`CONTENT_KIT.md`**. Clips without your phone: `tools/Record Content Clips.command`.

| Date | Post | Hook (first 2 seconds) | Shots |
|---|---|---|---|
| Thu 8 Oct | "Day 1 of shipping my AI-built game" | "I built a 20-mode basketball game with AI in 9 days. Now I have to actually ship it." | Screen-record the title → a dunk → the Season 12 rival scene. You on camera at the end. |
| Sat 10 Oct | Beta call | "I need 20 people to break my game." | Fast montage: Park ankle-breaker, CLUTCH buzzer shot, Full Court. Ends on "comment HOOPS". |
| Mon 12 Oct | How it was made (AI lane) | "Here's what building a game with Claude actually looks like." | Real screens: a phase plan, tests passing (802 → 807), the Build Check on the Mac, the Simulator. |
| Wed 14 Oct | Playtest reactions | "My friends tried my game. Here's what they said." | Film a friend or crew playing (ask first). Split screen: their face and the phone. |
| Fri 16 Oct | Rival crews | "Every season you face a new crew. This one plays mini-golf." | Rival logos one by one (Neon Static → Putt Club). |
| Sun 18 Oct | Honest money post | "I changed how my game makes money before it even launched. Here's why." | Talk to camera: free Live, a one-time $3.99, no ads, no subscription. Great for comments. |
| Tue 20 Oct | Bug-fix montage | "Testers found 37 bugs. Fixing them one by one." | Before/after clips. |
| Fri 23 Oct | Submission | "I just pressed Submit for Review. Apple decides now." | The App Store Connect button press (blur personal info). |
| Launch day | Launch | "It's out. Retro Hoops. Free. Link in bio." | The best 15 seconds of gameplay plus the SHARE RESULT card. Pin it. |
| Launch +2 | Long-form YouTube (8–12 min) | "I built and shipped an iPhone game with AI as a non-traditional dev." | The full story. Link it in every Short. |

**Tips:**

- Record gameplay with **iOS Screen Recording** in portrait with sound, 15–25 seconds per clip.
- The in-game **SHARE RESULT** card and **HIGHLIGHTS** make ready-made content. Ask testers to post theirs and tag you.
- Keep one tag across platforms: **#RetroHoops**.

---

## After launch: the v1.1 list (ideas parked by the freeze)

Add new ideas here, not to the build. Pick from this list only after v1 is live.

- Share created players by QR/link, like kits (every shared player is an ad).
- Supabase (or other) online backend: accounts, cloud leaderboards. Needs a privacy review first.
- Cosmetics pass / extra Pro content.
- iPad-first marketing assets.

- Paid Live tier or Live season pass (only once matchmaking regularly finds games).
- Android version (Unity).
- Studio cross-promotion with Retro Sk8 and Pixel Kingdom Rumble.
