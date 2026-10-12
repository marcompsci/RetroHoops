using System;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>12 Oct 2026: LIVE POINTS (wins + scoring + time played) for the Live leaderboards.</summary>
    public class LivePointsTests
    {
        private static readonly DateTime Day1 = new DateTime(2026, 10, 12, 18, 0, 0, DateTimeKind.Utc);

        [Test]
        public void AWin_EarnsWinScoringAndTime()
        {
            var s = new LiveSaveData();
            var a = LivePoints.Record(s, Day1, true, true, 1000, 1000, 21, 300f);
            Assert.AreEqual(100, a.Win);
            Assert.AreEqual(0, a.Upset, "even match: no upset");
            Assert.AreEqual(0, a.Streak, "first win: no streak yet");
            Assert.AreEqual(21, a.Scoring);
            Assert.AreEqual(10, a.Activity, "5 minutes x 2");
            Assert.AreEqual(131, LivePoints.Total(s));
            Assert.AreEqual(131, LivePoints.MonthTotal(s));
        }

        [Test]
        public void ALoss_StillEarnsScoringAndTime_NotWinPoints()
        {
            var s = new LiveSaveData();
            var a = LivePoints.Record(s, Day1, true, false, 1000, 1200, 18, 240f);
            Assert.AreEqual(0, a.Win + a.Upset + a.Streak);
            Assert.AreEqual(18 + 8, a.Total);
        }

        [Test]
        public void Upsets_AndStreaks_PayMore_UpToTheirCaps()
        {
            var s = new LiveSaveData();
            var a = LivePoints.Record(s, Day1, true, true, 1000, 1400, 10, 60f);
            Assert.AreEqual(LivePoints.UpsetMax, a.Upset, "beat someone 400 above you");
            Assert.AreEqual(LivePoints.UpsetMax, LivePoints.Record(new LiveSaveData(), Day1, true, true, 1000, 3000, 0, 0f).Upset, "capped");
            for (int i = 0; i < 8; i++) a = LivePoints.Record(s, Day1, true, true, 1000, 1000, 10, 0f);
            Assert.AreEqual(LivePoints.StreakMax, a.Streak, "streak bonus is capped");
            Assert.AreEqual(9, s.pStreak);
            LivePoints.Record(s, Day1, true, false, 1000, 1000, 10, 0f);
            Assert.AreEqual(0, s.pStreak, "a loss ends the streak");
            Assert.AreEqual(9, s.pBestStreak);
        }

        [Test]
        public void TimePoints_AreCappedPerDay_AndResetTomorrow()
        {
            var s = new LiveSaveData();
            int earned = 0;
            for (int i = 0; i < 10; i++) earned += LivePoints.Record(s, Day1, true, false, 1000, 1000, 0, 600f).Activity;
            Assert.AreEqual(LivePoints.DailyActivityCap, earned, "100 minutes in a day still earns only the daily cap");
            Assert.AreEqual(20, LivePoints.Record(s, Day1.AddDays(1), true, false, 1000, 1000, 0, 600f).Activity, "a new day");
        }

        [Test]
        public void Scoring_IsCappedPerGame_AndQuittingEarnsNothing()
        {
            var s = new LiveSaveData();
            Assert.AreEqual(LivePoints.ScoringMax, LivePoints.Record(s, Day1, true, true, 1000, 1000, 400, 0f).Scoring);
            var quit = LivePoints.Record(s, Day1, false, false, 1000, 1000, 30, 300f);
            Assert.AreEqual(0, quit.Total);
            Assert.AreEqual(0, s.pStreak, "quitting ends the streak");
        }

        [Test]
        public void MonthlyPoints_ResetEachMonth_AllTimeKeeps_AndDivisions()
        {
            var s = new LiveSaveData();
            LivePoints.Record(s, Day1, true, true, 1000, 1000, 20, 600f);
            int total = LivePoints.Total(s);
            Assert.AreEqual(total, LivePoints.MonthlyScore(s, Day1));
            var nov = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);
            Assert.AreEqual(0, LivePoints.MonthlyScore(s, nov), "the monthly board only shows this month's points");
            LivePoints.Roll(s, nov);
            Assert.AreEqual(0, LivePoints.MonthTotal(s));
            Assert.AreEqual(total, LivePoints.Total(s), "all-time keeps everything");

            Assert.AreEqual("BRONZE", LivePoints.Division(0));
            Assert.AreEqual("SILVER", LivePoints.Division(500));
            Assert.AreEqual("DIAMOND", LivePoints.Division(99999));
            Assert.AreEqual(1000, LivePoints.ToNextDivision(500));
            Assert.AreEqual(0, LivePoints.ToNextDivision(6000));
        }

        [Test]
        public void Points_SurviveSaving_AndImpossibleTotalsAreCleared()
        {
            var c = DefaultContent.Create();
            var d = Career.New(c);
            d.live = new LiveSaveData();
            d.live.games = d.live.wins = 1;
            LivePoints.Record(d.live, DateTime.UtcNow, true, true, 1000, 1000, 21, 300f);
            var back = SaveCodec.Decode(SaveCodec.Encode(d), c, out _);
            Assert.AreEqual(LivePoints.Total(d.live), LivePoints.Total(back.live));
            Assert.AreEqual(1, back.live.pBestStreak);

            var cheat = new LiveSaveData { games = 1, wins = 1, pWins = 999999, pScoring = 50, pActivity = 60 };
            LivePoints.Sanitize(cheat);
            Assert.LessOrEqual(LivePoints.Total(cheat), LivePoints.MaxPerGame);
        }

        [Test]
        public void BothBoards_AreReportedToGameCenter()
        {
            Assert.IsTrue(Achievements.Boards.Exists(b => b.Id == LivePoints.LeaderboardId));
            Assert.IsTrue(Achievements.Boards.Exists(b => b.Id == LivePoints.MonthlyLeaderboardId));
        }
    }
}
