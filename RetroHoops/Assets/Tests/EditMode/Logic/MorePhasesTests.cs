using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>16 Oct 2026 improvement phases I7-I11: clutch cam, my line, living arena, game grade, quick resume.</summary>
    public class MorePhasesTests
    {
        [Test]
        public void I7_ClutchCam_OnlyForShotsThatCouldDecideIt()
        {
            // Game point: 20 + 1 reaches 21.
            Assert.IsTrue(ClutchCam.Active(true, true, 0, new[] { 20, 12 }, 1, 21, false, 0f));
            Assert.IsFalse(ClutchCam.Active(true, true, 0, new[] { 18, 12 }, 1, 21, false, 0f), "not yet game point");
            // Last seconds: down 1 with a 2-pointer, or tied.
            Assert.IsTrue(ClutchCam.Active(true, true, 1, new[] { 30, 29 }, 2, 99, true, 2f));
            Assert.IsTrue(ClutchCam.Active(true, true, 1, new[] { 30, 30 }, 1, 99, true, 3f));
            Assert.IsFalse(ClutchCam.Active(true, true, 1, new[] { 30, 25 }, 2, 99, true, 2f), "can't catch up with this one");
            Assert.IsFalse(ClutchCam.Active(true, true, 1, new[] { 25, 30 }, 2, 99, true, 2f), "already ahead");
            Assert.IsFalse(ClutchCam.Active(true, true, 1, new[] { 30, 29 }, 2, 99, true, 30f), "too early in the clock");
            Assert.IsFalse(ClutchCam.Active(true, false, 0, new[] { 20, 12 }, 1, 21, false, 0f), "no shot in the air");
            Assert.IsFalse(ClutchCam.Active(false, true, 0, new[] { 20, 12 }, 1, 21, false, 0f));
            Assert.Greater(ClutchCam.Scale, 0.2f); Assert.Less(ClutchCam.Scale, 1f);
        }

        [Test]
        public void I8_MyLine_ShowsWhatYouDid()
        {
            var s = new PlayerStatLine { points = 8, fieldGoalsMade = 4, fieldGoalsAttempted = 7, arcMade = 1, arcAttempted = 3, assists = 2 };
            string t = MyLine.Text(s);
            StringAssert.Contains("8 PTS", t);
            StringAssert.Contains("4/7 FG", t);
            StringAssert.Contains("1/3 DEEP", t);
            StringAssert.Contains("2 AST", t);
            Assert.IsFalse(t.Contains("REB"), "zeros stay out");
            Assert.AreEqual("0 PTS  ·  0/0 FG", MyLine.Text(new PlayerStatLine()));
            Assert.AreEqual("", MyLine.Text(null));
            var d = new CareerSaveData();
            Assert.IsTrue(d.settings.liveStatLine, "on by default");
            d.settings.liveStatLine = false;
            Assert.IsFalse(SaveCodec.Decode(SaveCodec.Encode(d), DefaultContent.Create(), out _).settings.liveStatLine);
        }

        [Test]
        public void I9_LivingArena()
        {
            Assert.AreEqual(0, LivingArena.CrowdTeam(false), "the home side");
            Assert.AreEqual(-1, LivingArena.CrowdTeam(true), "nobody's crowd in 2 Player");
            Assert.IsTrue(LivingArena.Hostile(0, 1));
            Assert.IsFalse(LivingArena.Hostile(0, 0));
            Assert.IsFalse(LivingArena.Hostile(-1, 1));
            Assert.AreEqual(0, LivingArena.DapLift(0f));
            Assert.Greater(LivingArena.DapLift(LivingArena.DapSeconds / 2f), 2);
            Assert.AreEqual(0, LivingArena.DapLift(LivingArena.DapSeconds + 0.1f));
            var pos = new[] { new Vec2(0, 0), new Vec2(5, 0), new Vec2(1, 0), new Vec2(0.5f, 0) };
            var teams = new[] { 0, 0, 0, 1 };
            Assert.AreEqual(2, LivingArena.NearestTeammate(pos, teams, 0), "the closest teammate, not the opponent");
            teams = new[] { 0, 1, 1, 1 };
            Assert.AreEqual(-1, LivingArena.NearestTeammate(pos, teams, 0), "1-on-1: nobody to celebrate with");
        }

        [Test]
        public void I10_GameGrade()
        {
            var big = new PlayerStatLine { points = 14, fieldGoalsMade = 9, fieldGoalsAttempted = 12, assists = 4, rebounds = 3, steals = 2 };
            var bad = new PlayerStatLine { points = 2, fieldGoalsMade = 2, fieldGoalsAttempted = 11, turnovers = 4 };
            Assert.AreEqual("A+", GameGrade.Letter(GameGrade.Score(big, true, false)));
            Assert.AreEqual("F", GameGrade.Letter(GameGrade.Score(bad, false, false)));
            Assert.Less(GameGrade.Score(big, false, false), GameGrade.Score(big, true, false), "winning helps");
            Assert.Less(GameGrade.Score(big, true, true), GameGrade.Score(big, true, false), "Full Court points count for less");
            StringAssert.Contains("EFFICIENT SCORING", GameGrade.Reasons(big, true));
            StringAssert.Contains("COLD SHOOTING", GameGrade.Reasons(bad, false));
            StringAssert.Contains("4 TURNOVERS", GameGrade.Reasons(bad, false));
            StringAssert.Contains("YOUR GRADE: A+", GameGrade.Line(big, true, false));
            Assert.AreEqual(GameGrade.Letters.Length, GameGrade.Steps.Length + 1);
            for (int i = 1; i < GameGrade.Steps.Length; i++) Assert.Greater(GameGrade.Steps[i], GameGrade.Steps[i - 1]);
        }

        [Test]
        public void I11_QuickResume_RoundTrip()
        {
            var cat = DefaultContent.Create();
            var req = new MatchRequest { Mode = GameMode.QuickCall, HomeTeamId = cat.Teams[0].id, AwayTeamId = cat.Teams[1].id, CourtId = cat.Courts[0].id, DifficultyId = DefaultContent.DefaultDifficultyId };
            var g = QuickResume.Capture(req, 14, 11, 72.4f, "2|1|0|0", 500);
            Assert.IsTrue(QuickResume.Valid(g, cat, 503));
            Assert.IsFalse(QuickResume.Valid(g, cat, 500 + QuickResume.KeepDays + 1), "too old");
            Assert.IsFalse(QuickResume.Valid(QuickResume.Capture(req, 21, 11, 30f, "2|1|0|0", 500), cat, 500), "already won");
            Assert.IsFalse(QuickResume.Valid(null, cat, 500));
            var r = QuickResume.Request(g, cat);
            Assert.AreEqual(GameMode.QuickCall, r.Mode);
            Assert.AreEqual(14, r.StartScoreA); Assert.AreEqual(11, r.StartScoreB);
            Assert.AreEqual(72.4f, r.StartClock, 1e-3f);
            Assert.AreEqual(21, r.RulesOverride.targetScore);
            var setup = MatchSetup.FromRequest(r, cat);
            var sim = new MatchSimulation(setup);
            Assert.AreEqual(14, sim.Score[0]);
            Assert.AreEqual(72.4f, sim.GameClock, 1e-3f);
            StringAssert.Contains("14 - 11", QuickResume.Label(g, cat));
            StringAssert.Contains("1:13", QuickResume.Label(g, cat));
            Assert.IsTrue(QuickResume.Supports(GameMode.QuickCall, false, false));
            Assert.IsFalse(QuickResume.Supports(GameMode.Rise, false, false), "career games can't be re-run from a score");
            Assert.IsFalse(QuickResume.Supports(GameMode.QuickCall, true, false), "two phones");

            var d = new CareerSaveData { suspended = g };
            var back = SaveCodec.Decode(SaveCodec.Encode(d), cat, out _);
            Assert.AreEqual(g.homeTeamId, back.suspended.homeTeamId);
            Assert.AreEqual(11, back.suspended.scoreB);
            Assert.AreEqual(72.4f, back.suspended.clock, 1e-3f);
            Assert.IsNull(SaveCodec.Decode(SaveCodec.Encode(new CareerSaveData()), cat, out _).suspended);
        }
    }
}
