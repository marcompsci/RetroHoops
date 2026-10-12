using System;

namespace CallerRetroBall.Logic
{
    /// <summary>What one Live game earned in LIVE POINTS.</summary>
    public sealed class LivePointsAward
    {
        public int Win, Upset, Streak, Scoring, Activity;
        public int Total => Win + Upset + Streak + Scoring + Activity;
        /// <summary>"+100 WIN · +20 UPSET · +31 SCORING · +18 TIME".</summary>
        public string Describe()
        {
            var parts = new System.Collections.Generic.List<string>();
            if (Win > 0) parts.Add("+" + Win + " " + Loc.T("WIN"));
            if (Upset > 0) parts.Add("+" + Upset + " " + Loc.T("UPSET"));
            if (Streak > 0) parts.Add("+" + Streak + " " + Loc.T("STREAK"));
            if (Scoring > 0) parts.Add("+" + Scoring + " " + Loc.T("SCORING"));
            if (Activity > 0) parts.Add("+" + Activity + " " + Loc.T("TIME"));
            return parts.Count == 0 ? Loc.T("No points this game.") : string.Join("  ·  ", parts);
        }
    }

    /// <summary>
    /// LIVE POINTS (12 Oct 2026, Omari's request): one number for the Live leaderboards that adds up winning, scoring and
    /// time played, so the top of the board is the player who wins, scores and shows up.
    ///  • WINS: 100 a win, plus an UPSET bonus for beating a higher-rated player (up to 50) and a STREAK bonus for each
    ///    win in a row after the first (10 each, up to 50). Losses earn no win points.
    ///  • SCORING: 1 point per point your team scores, up to 50 a game.
    ///  • TIME: 2 points per minute of Live game time, up to <see cref="DailyActivityCap"/> a day (about 30 minutes), so
    ///    showing up counts but playing all night can't beat winning.
    ///  • Only games that count earn points: quitting earns nothing.
    /// There's an all-time total and a monthly total (the monthly board resets each month, like Live seasons). Monthly
    /// points also give a DIVISION, shown on the LIVE screen.
    /// </summary>
    public static class LivePoints
    {
        public const string LeaderboardId = "retrohoops.live.points";
        public const string MonthlyLeaderboardId = "retrohoops.live.points.monthly";

        public const int WinPoints = 100;
        public const int UpsetMax = 50;
        /// <summary>Rating points per upset point (beat someone 400 above you: +50).</summary>
        public const int UpsetDivisor = 8;
        public const int StreakStep = 10;
        public const int StreakMax = 50;
        public const int ScoringMax = 50;
        public const int PointsPerMinute = 2;
        public const int DailyActivityCap = 60;
        /// <summary>The most one game can earn (used to check saves).</summary>
        public const int MaxPerGame = WinPoints + UpsetMax + StreakMax + ScoringMax + DailyActivityCap;

        public static readonly string[] Divisions = { "BRONZE", "SILVER", "GOLD", "PLATINUM", "DIAMOND" };
        /// <summary>Monthly points needed for each division.</summary>
        public static readonly int[] DivisionFloor = { 0, 500, 1500, 3000, 6000 };

        public static int DivisionIndex(int monthPoints)
        {
            int d = 0;
            for (int i = 0; i < DivisionFloor.Length; i++) if (monthPoints >= DivisionFloor[i]) d = i;
            return d;
        }

        public static string Division(int monthPoints) => Divisions[DivisionIndex(monthPoints)];

        /// <summary>Points still needed for the next division (0 at DIAMOND).</summary>
        public static int ToNextDivision(int monthPoints)
        {
            int d = DivisionIndex(monthPoints);
            return d >= DivisionFloor.Length - 1 ? 0 : DivisionFloor[d + 1] - monthPoints;
        }

        public static int DayKey(DateTime utc) => utc.Year * 10000 + utc.Month * 100 + utc.Day;

        /// <summary>
        /// Scores a finished Live game and adds it to the totals. <paramref name="counted"/> is false for a game you quit
        /// (it earns nothing and ends your streak). <paramref name="streakBefore"/>-style state lives in the save.
        /// </summary>
        public static LivePointsAward Record(LiveSaveData s, DateTime utcNow, bool counted, bool won, int myRating, int theirRating,
                                             int teamPoints, float secondsPlayed)
        {
            var a = new LivePointsAward();
            if (s == null) return a;
            Roll(s, utcNow);
            if (!counted)
            {
                s.pStreak = 0;
                return a;
            }
            if (won)
            {
                a.Win = WinPoints;
                a.Upset = Math.Max(0, Math.Min(UpsetMax, (theirRating - myRating) / UpsetDivisor));
                a.Streak = Math.Min(StreakMax, s.pStreak * StreakStep);
                s.pStreak++;
                s.pBestStreak = Math.Max(s.pBestStreak, s.pStreak);
            }
            else s.pStreak = 0;
            a.Scoring = Math.Max(0, Math.Min(ScoringMax, teamPoints));
            int minutesPts = (int)(Math.Max(0f, secondsPlayed) / 60f * PointsPerMinute);
            a.Activity = Math.Max(0, Math.Min(minutesPts, DailyActivityCap - s.pDayActivity));
            s.pDayActivity += a.Activity;

            int wins = a.Win + a.Upset + a.Streak;
            s.pWins += wins; s.pScoring += a.Scoring; s.pActivity += a.Activity;
            s.pMonthWins += wins; s.pMonthScoring += a.Scoring; s.pMonthActivity += a.Activity;
            return a;
        }

        public static int Total(LiveSaveData s) => s == null ? 0 : s.pWins + s.pScoring + s.pActivity;
        public static int MonthTotal(LiveSaveData s) => s == null ? 0 : s.pMonthWins + s.pMonthScoring + s.pMonthActivity;

        /// <summary>A new day resets the daily time cap; a new month starts a fresh monthly total.</summary>
        public static void Roll(LiveSaveData s, DateTime utcNow)
        {
            if (s == null) return;
            int day = DayKey(utcNow), month = LiveMode.MonthKey(utcNow);
            if (s.pDay != day) { s.pDay = day; s.pDayActivity = 0; }
            if (s.pMonth != month) { s.pMonth = month; s.pMonthWins = s.pMonthScoring = s.pMonthActivity = 0; }
        }

        /// <summary>The monthly board's score: this month's points, only in the month they were earned.</summary>
        public static long MonthlyScore(LiveSaveData s, DateTime utcNow) =>
            s != null && s.pMonth == LiveMode.MonthKey(utcNow) ? MonthTotal(s) : 0;

        /// <summary>Keeps saved points believable: no negatives, and no more than the games played could have earned.</summary>
        public static void Sanitize(LiveSaveData s)
        {
            if (s == null) return;
            int cap = Math.Max(0, s.games) * MaxPerGame;
            s.pWins = Clamp(s.pWins, Math.Max(0, s.wins) * (WinPoints + UpsetMax + StreakMax));
            s.pScoring = Clamp(s.pScoring, Math.Max(0, s.games) * ScoringMax);
            s.pActivity = Clamp(s.pActivity, Math.Max(0, s.games) * DailyActivityCap);
            s.pMonthWins = Clamp(s.pMonthWins, s.pWins);
            s.pMonthScoring = Clamp(s.pMonthScoring, s.pScoring);
            s.pMonthActivity = Clamp(s.pMonthActivity, s.pActivity);
            s.pDayActivity = Clamp(s.pDayActivity, DailyActivityCap);
            s.pStreak = Clamp(s.pStreak, Math.Max(0, s.wins));
            s.pBestStreak = Math.Max(s.pStreak, Clamp(s.pBestStreak, Math.Max(0, s.wins)));
            if (Total(s) > cap) { s.pWins = s.pScoring = s.pActivity = 0; s.pMonthWins = s.pMonthScoring = s.pMonthActivity = 0; }
        }

        private static int Clamp(int v, int max) => Math.Max(0, Math.Min(max, v));
    }
}
