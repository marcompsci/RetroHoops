using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public enum DailyGoal
    {
        WinBy = 0,
        Greens = 1,
        Steals = 2,
        Points = 3,
        Assists = 4,
        WinOnLegend = 5,
    }

    /// <summary>One day's challenge. Fully determined by the day number, so it's the same for everyone and works offline.</summary>
    public sealed class DailyChallenge
    {
        /// <summary>Days since 2000-01-01 (local calendar day).</summary>
        public int Day;
        public DailyGoal Goal;
        public int Target;
        public string HomeTeamId;
        public string OpponentId;
        public string CourtId;
        public string DifficultyId;

        public string Describe()
        {
            switch (Goal)
            {
                case DailyGoal.WinBy: return "Win by " + Target + " or more";
                case DailyGoal.Greens: return "Hit " + Target + " GREEN releases";
                case DailyGoal.Steals: return "Get " + Target + " steals";
                case DailyGoal.Points: return "Score " + Target + " points yourself";
                case DailyGoal.Assists: return "Dish " + Target + " assists";
                default: return "Win on Legend";
            }
        }

        public MatchRequest ToRequest() => new MatchRequest
        {
            Mode = GameMode.Daily,
            HomeTeamId = HomeTeamId,
            AwayTeamId = OpponentId,
            CourtId = CourtId,
            DifficultyId = DifficultyId,
            ContextId = "daily:" + Day,
        };
    }

    [Serializable]
    public class DailySaveData
    {
        /// <summary>Day number of the last completed challenge (-1 = never).</summary>
        public int lastCompletedDay = -1;
        public int streak;
        public int bestStreak;
        public int completed;
    }

    public static class DailyChallenges
    {
        public const int BaseBonus = 75;
        public const int BonusPerStreakDay = 15;
        public const int MaxStreakBonusDays = 10;

        private static readonly DateTime Epoch = new DateTime(2000, 1, 1);

        public static int DayNumber(DateTime localDate) => (int)(localDate.Date - Epoch).TotalDays;

        public static DailyChallenge For(int day, ContentCatalog c)
        {
            var rng = new SeededRandom(StableHash.Of("daily:" + day));
            var league = c.TeamsInTier(TeamTier.League);
            var mine = league.FindAll(t => t.unlockedByDefault);
            var home = mine.Count > 0 ? mine[rng.Range(0, mine.Count)] : league[0];
            var opponents = league.FindAll(t => t.id != home.id);
            var opp = opponents[rng.Range(0, opponents.Count)];

            var goal = (DailyGoal)rng.Range(0, 6);
            int target;
            switch (goal)
            {
                case DailyGoal.WinBy: target = 5 + rng.Range(0, 4); break;      // 5..8
                case DailyGoal.Greens: target = 2 + rng.Range(0, 3); break;     // 2..4
                case DailyGoal.Steals: target = 2 + rng.Range(0, 2); break;     // 2..3
                case DailyGoal.Points: target = 8 + rng.Range(0, 5); break;     // 8..12
                case DailyGoal.Assists: target = 3 + rng.Range(0, 3); break;    // 3..5
                default: target = 1; break;
            }
            string difficulty = goal == DailyGoal.WinOnLegend ? "difficulty.legend" : "difficulty.caller";
            if (c.Difficulty(difficulty) == null) difficulty = DefaultContent.DefaultDifficultyId;
            return new DailyChallenge
            {
                Day = day,
                Goal = goal,
                Target = target,
                HomeTeamId = home.id,
                OpponentId = opp.id,
                CourtId = home.homeCourtId,
                DifficultyId = difficulty,
            };
        }

        /// <summary>Did this finished game meet the challenge?</summary>
        public static bool IsMet(DailyChallenge d, MatchSummary s)
        {
            if (d == null || s == null) return false;
            var line = s.HumanLine?.stats ?? new PlayerStatLine();
            switch (d.Goal)
            {
                case DailyGoal.WinBy: return s.HumanWon && s.Margin >= d.Target;
                case DailyGoal.Greens: return line.greenReleases >= d.Target;
                case DailyGoal.Steals: return line.steals >= d.Target;
                case DailyGoal.Points: return line.points >= d.Target;
                case DailyGoal.Assists: return line.assists >= d.Target;
                default: return s.HumanWon;
            }
        }

        public static bool CompletedToday(DailySaveData save, int day) => save != null && save.lastCompletedDay == day;

        public static int BonusFor(int streak) => BaseBonus + BonusPerStreakDay * Math.Min(Math.Max(0, streak - 1), MaxStreakBonusDays);

        /// <summary>
        /// Marks today's challenge done (once per day). Consecutive days build the streak; a missed
        /// day resets it to 1. Returns the Signal Point bonus granted (0 if already done today).
        /// </summary>
        public static int Complete(DailySaveData save, CareerSaveData career, int day)
        {
            if (save == null || CompletedToday(save, day)) return 0;
            save.streak = save.lastCompletedDay == day - 1 ? save.streak + 1 : 1;
            save.bestStreak = Math.Max(save.bestStreak, save.streak);
            save.lastCompletedDay = day;
            save.completed++;
            int bonus = BonusFor(save.streak);
            if (career != null) career.signalPoints += bonus;
            return bonus;
        }

        /// <summary>Streak shown on the menu: it lapses if yesterday was missed.</summary>
        public static int LiveStreak(DailySaveData save, int today) =>
            save == null ? 0 : (save.lastCompletedDay >= today - 1 ? save.streak : 0);
    }

    /// <summary>One Game Center achievement: id, App Store Connect text and points, and when it's earned.</summary>
    public sealed class AchievementInfo
    {
        public string Id;
        public string Title;
        public string Description;
        /// <summary>Game Center points (each ≤ 100, all together ≤ 1000).</summary>
        public int Points;
        public Func<CareerSaveData, bool> Earned;
    }

    /// <summary>One Game Center leaderboard: id, name, sort order, and the career value it reports.</summary>
    public sealed class LeaderboardInfo
    {
        public string Id;
        public string Name;
        /// <summary>True when a lower score is better (times).</summary>
        public bool LowIsBetter;
        /// <summary>How App Store Connect should format the score.</summary>
        public string Format;
        public Func<CareerSaveData, long> Score;
    }

    /// <summary>Game Center achievement and leaderboard ids, and what has been earned (pure, testable).</summary>
    public static class Achievements
    {
        public const string FirstWin = "retroball.ach.first_win";
        public const string FirstGreen = "retroball.ach.first_green";
        public const string TenWins = "retroball.ach.ten_wins";
        public const string HundredGreens = "retroball.ach.hundred_greens";
        public const string CircuitCleared = "retroball.ach.circuit_cleared";
        public const string CupChampion = "retroball.ach.cup_champion";
        public const string ClassicChampion = "retroball.ach.classic_champion";
        public const string DailyWeek = "retroball.ach.daily_streak_7";
        public const string TutorialDone = "retroball.ach.tutorial_done";
        // Phase 19.
        public const string HeatCheck = "retroball.ach.heat_check";
        public const string AlleyOop = "retroball.ach.alley_oop";
        public const string GlitchBeaten = "retroball.ach.glitch_beaten";
        public const string FirstCode = "retroball.ach.first_code";
        public const string AllCodes = "retroball.ach.all_codes";
        public const string KingFive = "retroball.ach.king_five";
        public const string CallerCup = "retroball.ach.caller_cup";
        public const string AllRivals = "retroball.ach.all_rivals";
        public const string Shootout = "retroball.ach.shootout";
        public const string Horse = "retroball.ach.horse";
        public const string AroundWorld = "retroball.ach.around_world";
        public const string CouchGame = "retroball.ach.couch_game";
        public const string YourColors = "retroball.ach.your_colors";
        public const string LongHaul = "retroball.ach.long_haul";
        // Phase 21.
        public const string FranchiseTitle = "retroball.ach.franchise_title";
        public const string DunkChamp = "retroball.ach.dunk_contest";
        public const string ThreeChamp = "retroball.ach.three_contest";

        public const string BoardWins = "retroball.lb.career_wins";
        public const string BoardGreens = "retroball.lb.career_greens";
        public const string BoardDailyStreak = "retroball.lb.daily_best_streak";
        // Phase 19.
        public const string BoardKing = "retroball.lb.king_streak";
        public const string BoardArcade = "retroball.lb.arcade_clears";
        public const string BoardShootout = "retroball.lb.shootout_wins";
        public const string BoardAroundWorld = "retroball.lb.around_world";
        public const string BoardWinStreak = "retroball.lb.win_streak";
        public const string BoardPoints = "retroball.lb.game_points";
        // Phase 21.
        public const string BoardDunk = "retroball.lb.dunk_round";
        // Phase 22.
        public const string BoardStreetRep = "retroball.lb.street_rep";
        public const string BoardLegacy = "retroball.lb.legacy_points";

        public static readonly List<AchievementInfo> All = new List<AchievementInfo>
        {
            A(FirstWin, "First W", "Win any game.", 10, d => d.totals.wins >= 1),
            A(FirstGreen, "Called It", "Hit your first GREEN release.", 10, d => d.totals.greens >= 1),
            A(TenWins, "Double Digits", "Win 10 games.", 30, d => d.totals.wins >= 10),
            A(HundredGreens, "Green Machine", "Hit 100 GREEN releases.", 50, d => d.totals.greens >= 100),
            A(CircuitCleared, "Off the Blacktop", "Clear The Blacktop Circuit.", 50,
              d => d.rise != null && (d.rise.stage != RiseStage.Circuit || d.rise.seasonsPlayed > 0)),
            A(CupChampion, "Gold Signal", "Win The Gold Signal Cup.", 80, d => d.totals.championships > 0),
            A(ClassicChampion, "First Call", "Win the First Call Classic.", 40, d => d.classic != null && d.classic.titles > 0),
            A(DailyWeek, "Every Day", "Reach a 7-day Daily Challenge streak.", 50, d => d.daily != null && d.daily.bestStreak >= 7),
            A(TutorialDone, "Ready to Call", "Finish How to Play.", 10, d => d.tutorialDone),
            A(HeatCheck, "Heating Up", "Hit three in a row and HEAT UP.", 20, d => d.totals.heatUps >= 1),
            A(AlleyOop, "Up Top", "Throw or finish an alley-oop.", 20, d => d.totals.alleyOops >= 1),
            A(GlitchBeaten, "Game Over, Glitch", "Clear the Arcade Ladder.", 80, d => d.secrets != null && d.secrets.arcade != null && d.secrets.arcade.clears > 0),
            A(FirstCode, "Old-School", "Enter a secret code.", 20, d => d.secrets != null && d.secrets.codesFound.Count > 0),
            A(AllCodes, "Code Breaker", "Find every secret code.", 80,
              d => d.secrets != null && Secrets.All.TrueForAll(x => d.secrets.codesFound.Contains(x.Id))),
            A(KingFive, "Hold the Court", "Win 5 straight in King of the Court.", 50, d => d.king != null && d.king.best >= 5),
            A(CallerCup, "Cup Run", "Win the Caller Cup.", 60, d => d.cup != null && d.cup.titles >= 1),
            A(AllRivals, "Every Rival", "Beat Neon Static, the Sundown Syndicate, the Midnight Tide, the Paper Cranes and the Cassette Club.", 100, RivalEngine.BeatEveryRival),
            A(Shootout, "Sharpshooter", "Win a Shootout.", 20, d => d.practice.shootoutWins >= 1),
            A(Horse, "Spell It Out", "Win a game of H-O-R-S-E against the CPU.", 20, d => d.practice.horseWins >= 1),
            A(AroundWorld, "World Tour", "Finish Around the World.", 20, d => d.practice.aroundWorldTime > 0f),
            A(CouchGame, "Couch Rivals", "Play a 2 Player game.", 10, d => d.totals.versusGames >= 1),
            A(YourColors, "Your Colors", "Create your own team.", 10, d => d.customTeam != null && d.customTeam.created),
            A(LongHaul, "Long Haul", "Play five Rise seasons.", 50, d => d.rise != null && d.rise.seasonsPlayed >= 5),
            A(FranchiseTitle, "Front Office", "Win a title in Franchise.", 50, d => d.franchise != null && d.franchise.titles >= 1),
            A(DunkChamp, "Above the Rim", "Win the Dunk Contest.", 30, d => d.allStar != null && d.allStar.dunkTitles >= 1),
            A(ThreeChamp, "Money Ball", "Win the 3-Point Contest.", 30, d => d.allStar != null && d.allStar.threeTitles >= 1),
        };

        public static readonly List<LeaderboardInfo> Boards = new List<LeaderboardInfo>
        {
            L(BoardWins, "Career Wins", false, "Integer", d => d.totals.wins),
            L(BoardGreens, "Green Releases", false, "Integer", d => d.totals.greens),
            L(BoardDailyStreak, "Best Daily Streak", false, "Integer", d => d.daily?.bestStreak ?? 0),
            L(BoardKing, "King of the Court Streak", false, "Integer", d => d.king?.best ?? 0),
            L(BoardArcade, "Arcade Ladder Clears", false, "Integer", d => d.secrets?.arcade?.clears ?? 0),
            L(BoardShootout, "Shootout Wins", false, "Integer", d => d.practice.shootoutWins),
            L(BoardAroundWorld, "Around the World", true, "Elapsed time (to the hundredth of a second)", d => Hundredths(d.practice.aroundWorldTime)),
            L(BoardWinStreak, "Best Win Streak", false, "Integer", d => d.records?.bestWinStreak ?? 0),
            L(BoardPoints, "Most Points in a Game", false, "Integer", d => d.records?.points ?? 0),
            L(BoardDunk, "Best Dunk Contest Round", false, "Integer", d => d.allStar?.bestDunk ?? 0),
            L(BoardStreetRep, "Street Rep", false, "Integer", d => d.street?.rep ?? 0),
            L(BoardLegacy, "Legacy Points", false, "Integer", d => d.legacy?.legacyPoints ?? 0),
            L(LiveMode.LeaderboardId, "Live Rating (best)", false, "Integer", d => d.live != null && d.live.games > 0 ? d.live.best : 0),
            L(Gauntlet.LeaderboardId, "Skills Gauntlet (best)", false, "Integer", d => d.gauntlet?.best ?? 0),
            L(LiveMode.MonthlyLeaderboardId, "Live Rating (this month)", false, "Integer", d => LiveMode.MonthlyScore(d.live, DateTime.UtcNow)),
            L(Clutch.LeaderboardId, "CLUTCH Stars", false, "Integer", d => Clutch.TotalStars(d.clutch)),
            // 12 Oct 2026: LIVE POINTS (wins + scoring + time played), all-time and this month.
            L(LivePoints.LeaderboardId, "Live Points (all-time)", false, "Integer", d => LivePoints.Total(d.live)),
            L(LivePoints.MonthlyLeaderboardId, "Live Points (this month)", false, "Integer", d => LivePoints.MonthlyScore(d.live, DateTime.UtcNow)),
        };

        public static readonly string[] AllAchievements = All.ConvertAll(a => a.Id).ToArray();

        private static AchievementInfo A(string id, string title, string desc, int points, Func<CareerSaveData, bool> earned) =>
            new AchievementInfo { Id = id, Title = title, Description = desc, Points = points, Earned = earned };

        private static LeaderboardInfo L(string id, string name, bool low, string format, Func<CareerSaveData, long> score) =>
            new LeaderboardInfo { Id = id, Name = name, LowIsBetter = low, Format = format, Score = score };

        /// <summary>Game Center "elapsed time to the hundredth" scores are hundredths of a second.</summary>
        public static long Hundredths(float seconds) => seconds <= 0f ? 0 : (long)Math.Round(seconds * 100.0);

        public static int TotalPoints
        {
            get
            {
                int sum = 0;
                foreach (var a in All) sum += a.Points;
                return sum;
            }
        }

        public static List<string> Earned(CareerSaveData d)
        {
            var list = new List<string>();
            if (d == null) return list;
            foreach (var a in All)
            {
                bool earned;
                try { earned = a.Earned(d); }
                catch (NullReferenceException) { earned = false; } // partial or very old saves
                if (earned) list.Add(a.Id);
            }
            return list;
        }

        /// <summary>Every leaderboard's score. Zero means "nothing to report yet" (never sent).</summary>
        public static List<KeyValuePair<string, long>> Scores(CareerSaveData d)
        {
            var list = new List<KeyValuePair<string, long>>();
            if (d == null) return list;
            foreach (var b in Boards) list.Add(new KeyValuePair<string, long>(b.Id, Math.Max(0, b.Score(d))));
            return list;
        }
    }
}
