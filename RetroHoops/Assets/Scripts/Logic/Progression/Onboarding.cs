using System;

namespace CallerRetroBall.Logic
{
    /// <summary>What the main menu's NEXT button suggests.</summary>
    public enum NextAction { Tutorial = 0, QuickCall = 1, Rise = 2, Clutch = 3, DailyClutch = 4, Daily = 5, Weekly = 6, Park = 7, Franchise = 8 }

    public sealed class NextStep
    {
        public NextAction Action;
        public string Title, Detail;
    }

    /// <summary>
    /// Phase 39 onboarding and coming back: a first game in one tap, a NEXT suggestion on the main menu that walks a new
    /// player through the modes (and later points at what's waiting today), and a small daily welcome-back bonus.
    /// No notifications: everything shows when you open the game.
    /// </summary>
    public static class Onboarding
    {
        // ------------------------------------------------------------------ first game

        /// <summary>
        /// JUST PLAY on the first launch: a Quick Call on Rookie against the friendliest league side, so the first
        /// minute is a game you can win, not a menu.
        /// </summary>
        public static MatchRequest FirstGame(ContentCatalog c)
        {
            var r = MatchRequest.QuickCallDefault(c);
            if (r == null) return null;
            r.DifficultyId = "difficulty.rookie";
            r.Seed = 101;
            r.ContextId = "first-game";
            return r;
        }

        // ------------------------------------------------------------------ create your player (15 Oct 2026)

        /// <summary>
        /// A brand-new player builds their Rise player (look, outfit, style of play) right after Coach Dee's hello,
        /// before the tutorial. Offered once; SKIP leaves the default player, and CREATE MY PLAYER stays in Rise.
        /// </summary>
        public static bool OfferCreatePlayer(CareerSaveData d) =>
            d != null && !d.tutorialDone && (d.totals?.games ?? 0) == 0
            && (d.customPlayer == null || !d.customPlayer.created) && !Tours.Seen(d, Tours.MakePlayer);

        // ------------------------------------------------------------------ NEXT

        /// <summary>The one thing to suggest next, given where this career is (and what's waiting today).</summary>
        public static NextStep Next(CareerSaveData d, int today)
        {
            if (d == null) return Step(NextAction.QuickCall, "QUICK CALL", "Pick a team and play one game.");
            int games = d.totals?.games ?? 0;
            if (!d.tutorialDone && games == 0)
                return Step(NextAction.Tutorial, "HOW TO PLAY", "Two minutes to learn the stick, shooting and passing.");
            if (games == 0)
                return Step(NextAction.QuickCall, "QUICK CALL", "Your first game: pick a team and tip off.");
            bool riseStarted = d.rise != null && (d.rise.stage != RiseStage.Circuit || d.rise.circuitBeaten.Count > 0);
            if (games >= 1 && !riseStarted)
                return Step(NextAction.Rise, "RISE MODE", "Take your crew from the street circuit to the league.");
            var clutch = d.clutch ?? new ClutchSaveData();
            if (games >= 3 && clutch.played == 0)
                return Step(NextAction.Clutch, "CLUTCH", "The clock's running and the score is set. Can you close it out?");
            if (clutch.played > 0 && !Clutch.DailyDone(clutch, today))
                return Step(NextAction.DailyClutch, "DAILY CLUTCH", "Today's scenario: " + Clutch.DailyFor(today).Title + ". +" + Clutch.DailyBonusSp + " SP.");
            if (!DailyChallenges.CompletedToday(d.daily, today))
                return Step(NextAction.Daily, "DAILY CHALLENGE", "Today's goal is waiting.");
            if (d.weekly != null && d.weekly.done != null && d.weekly.week == Weekly.WeekOf(today) && d.weekly.done.Contains(false))
                return Step(NextAction.Weekly, "WEEKLY CHALLENGES", "Finish this week's goals for SP and Hoops Pass XP.");
            if ((d.street?.wins ?? 0) == 0)
                return Step(NextAction.Park, "THE PARK", "Call out a street legend.");
            return Step(NextAction.Franchise, "FRANCHISE", "Run a club: trades, the draft and a title chase.");
        }

        private static NextStep Step(NextAction a, string title, string detail) => new NextStep { Action = a, Title = title, Detail = detail };

        // ------------------------------------------------------------------ welcome back

        public const int LoginBaseSp = 20, LoginStepSp = 10, LoginMaxDay = 7, LoginWeekBonusSp = 100;

        /// <summary>SP for the <paramref name="streak"/>-th day in a row: 20, 30 ... 80, and +100 on every 7th day.</summary>
        public static int LoginReward(int streak)
        {
            if (streak <= 0) return 0;
            int sp = LoginBaseSp + LoginStepSp * (Math.Min(streak, LoginMaxDay) - 1);
            if (streak % LoginMaxDay == 0) sp += LoginWeekBonusSp;
            return sp;
        }

        /// <summary>
        /// The first open of a new day pays <see cref="LoginReward"/> into the career and extends the run of days (a
        /// missed day starts again at 1). Returns the SP paid (0 if today was already claimed, or the clock went back).
        /// </summary>
        public static int ClaimLogin(CareerSaveData d, int today)
        {
            if (d == null || today < 0 || today <= d.loginDay) return 0;
            d.loginStreak = d.loginDay == today - 1 ? d.loginStreak + 1 : 1;
            d.loginBest = Math.Max(d.loginBest, d.loginStreak);
            d.loginDay = today;
            int sp = LoginReward(d.loginStreak);
            d.signalPoints += sp;
            return sp;
        }
    }
}
