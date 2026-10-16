using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>16 Oct 2026 improvement phases I1-I3 (from App Store basketball reviews): big moments, fair play, gear and legacy.</summary>
    public class ImprovementTests
    {
        // ---- I1: big moments

        [Test]
        public void I1_GameWinner_OnlyForTheWinnersLastBasket()
        {
            Assert.IsTrue(GameWinnerMob.IsGameWinner(0, 0, 10f, 10.1f));
            Assert.IsFalse(GameWinnerMob.IsGameWinner(0, 1, 10f, 10.1f), "the losers scored last");
            Assert.IsFalse(GameWinnerMob.IsGameWinner(0, 0, 5f, 10f), "the clock ran out long after the last basket");
            Assert.IsFalse(GameWinnerMob.IsGameWinner(-1, 0, 10f, 10f), "a tie has no winner");
            Assert.IsTrue(GameWinnerMob.IsBuzzerBeater(true, 0.4f));
            Assert.IsFalse(GameWinnerMob.IsBuzzerBeater(true, 8f));
            Assert.IsFalse(GameWinnerMob.IsBuzzerBeater(false, 0f), "no clock, no buzzer");
            Assert.AreEqual("BUZZER BEATER!", GameWinnerMob.Title(true));
            Assert.AreEqual("GAME WINNER!", GameWinnerMob.Title(false));
        }

        [Test]
        public void I1_Mob_RunsInThenJumps_InARingRoundTheShooter()
        {
            var shooter = new Vec2(0f, 5f);
            var from = new Vec2(4f, 1f);
            GameWinnerMob.Pose(from, shooter, 0, 2, 0f, out var g0, out int l0, out bool a0);
            Assert.AreEqual(from.x, g0.x, 1e-4f);
            Assert.AreEqual(0, l0);
            Assert.IsFalse(a0, "still running");
            bool jumped = false;
            Vec2 a = default, b = default;
            for (float t = GameWinnerMob.ArriveSeconds; t < GameWinnerMob.Seconds; t += 0.05f)
            {
                GameWinnerMob.Pose(from, shooter, 0, 2, t, out a, out int lift, out bool arms);
                Assert.IsTrue(arms);
                Assert.LessOrEqual(lift, 5);
                jumped |= lift >= 3;
                Assert.AreEqual(GameWinnerMob.Radius, Dist(a, shooter, 0.7f), 0.01f, "in the ring");
            }
            Assert.IsTrue(jumped, "they jump");
            GameWinnerMob.Pose(from, shooter, 1, 2, 1f, out b, out _, out _);
            Assert.Greater((a - b).Magnitude, 0.5f, "two teammates don't stand on the same spot");
        }

        private static float Dist(Vec2 p, Vec2 c, float squash)
        {
            float dx = p.x - c.x, dy = (p.y - c.y) / squash;
            return (float)System.Math.Sqrt(dx * dx + dy * dy);
        }

        [Test]
        public void I1_Chants_FollowTheGame()
        {
            var score = new[] { 18, 19 };
            // Home = team 0 defending while team 1 needs 2: DEFENSE.
            Assert.AreEqual(CrowdChant.Defense, CrowdChants.For(true, 0, 1, score, new[] { 3, 2 }, -1f, false));
            // Tight late on a clock.
            Assert.AreEqual(CrowdChant.Defense, CrowdChants.For(true, 0, 1, new[] { 40, 41 }, null, 20f, false));
            // Home ball with a heated-up scorer: MVP.
            Assert.AreEqual(CrowdChant.Mvp, CrowdChants.For(true, 0, 0, new[] { 10, 4 }, new[] { 11, 17 }, -1f, true));
            // Down four: LET'S GO.
            Assert.AreEqual(CrowdChant.LetsGo, CrowdChants.For(true, 0, 0, new[] { 4, 9 }, new[] { 17, 12 }, -1f, false));
            // Nothing to shout about, dead ball, and versus (no home crowd).
            Assert.AreEqual(CrowdChant.None, CrowdChants.For(true, 0, 0, new[] { 6, 6 }, new[] { 15, 15 }, -1f, false));
            Assert.AreEqual(CrowdChant.None, CrowdChants.For(false, 0, 1, score, new[] { 3, 2 }, -1f, false));
            Assert.AreEqual(CrowdChant.None, CrowdChants.For(true, -1, 1, score, new[] { 3, 2 }, -1f, false));
            Assert.AreEqual("LET'S GO COMETS!", CrowdChants.Text(CrowdChant.LetsGo, "comets"));
        }
        // ---- I2: fair play and house rules

        [Test]
        public void I2_FairPlay_AddsUpTheOddsForBothSides()
        {
            var t = new FairPlayTally();
            t.Apply(new MatchEvent(MatchEventType.ShotReleased, 0, 0, 0, 0.6f));
            t.Apply(new MatchEvent(MatchEventType.ShotMade, 0, 0, 1));
            t.Apply(new MatchEvent(MatchEventType.ShotReleased, 0, 0, 0, 0.4f));
            t.Apply(new MatchEvent(MatchEventType.ShotMissed, 0, 0));
            t.Apply(new MatchEvent(MatchEventType.ShotReleased, 3, 1, 0, 0.5f));
            t.Apply(new MatchEvent(MatchEventType.ShotMade, 3, 1, 2));
            Assert.AreEqual(2, t.Attempts[0]); Assert.AreEqual(1, t.Makes[0]);
            Assert.AreEqual(1.0f, t.Expected[0], 1e-4f);
            Assert.AreEqual(0.5f, t.Luck(1), 1e-4f);
            string line = t.Line(0, "YOU", "CPU");
            StringAssert.Contains("YOU 1/2", line);
            StringAssert.Contains("CPU 1/1", line);
            StringAssert.Contains("1.0", line);
            StringAssert.Contains("ABOUT WHAT THE ODDS SAID", line);
            Assert.IsNull(new FairPlayTally().Line(0, "YOU", "CPU"), "nobody shot");
        }

        [Test]
        public void I2_FairPlay_CallsAHotNight()
        {
            var t = new FairPlayTally();
            for (int i = 0; i < 6; i++)
            {
                t.Apply(new MatchEvent(MatchEventType.ShotReleased, 3, 1, 0, 0.3f));
                t.Apply(new MatchEvent(MatchEventType.ShotMade, 3, 1, 1));
            }
            StringAssert.Contains("ABOVE THE ODDS", t.Line(0, "YOU", "CPU"));
        }

        [Test]
        public void I2_HouseRules_RoundTripAndApply()
        {
            var cat = DefaultContent.Create();
            var defaults = cat.Find(cat.Rules, DefaultContent.DefaultRulesId);
            var d = HouseRules.From(defaults);
            var plain = HouseRules.Apply(defaults, d);
            Assert.AreEqual(defaults.targetScore, plain.targetScore, "the default choice is the default game");
            Assert.AreEqual(defaults.useGameClock, plain.useGameClock);
            Assert.AreEqual(defaults.insideArcPoints, plain.insideArcPoints);

            var c = new HouseRules.Choice { Target = 0, Clock = 0, Scoring = 1, WinByTwo = 1 };
            var back = HouseRules.Parse(HouseRules.Pack(c), defaults);
            Assert.AreEqual(c.Target, back.Target); Assert.AreEqual(c.Clock, back.Clock);
            Assert.AreEqual(c.Scoring, back.Scoring); Assert.AreEqual(c.WinByTwo, back.WinByTwo);
            var r = HouseRules.Apply(defaults, c);
            Assert.AreEqual(11, r.targetScore);
            Assert.IsFalse(r.useGameClock, "no clock: first to 11");
            Assert.AreEqual(2, r.insideArcPoints); Assert.AreEqual(3, r.beyondArcPoints);
            Assert.IsTrue(r.winByTwo);
            Assert.AreEqual(GameOverReason.None, Scoring.Evaluate(11, 10, 0f, r), "win by two");
            Assert.AreEqual(GameOverReason.TargetScore, Scoring.Evaluate(12, 10, 0f, r));
            Assert.IsFalse(ReferenceEquals(defaults, r), "the catalog rules are never changed");
            Assert.AreEqual(4 * 60f, HouseRules.Apply(defaults, new HouseRules.Choice { Target = 2, Clock = 3 }).gameClockSeconds);

            // Broken or missing codes fall back to the default game.
            foreach (var bad in new[] { null, "", "x|y", "9|9|9|9|9", "a|b|c|d" })
            {
                var p = HouseRules.Parse(bad, defaults);
                Assert.IsTrue(p.Target >= 0 && p.Target < HouseRules.Targets.Length, bad);
            }
            var clamped = HouseRules.Parse("9|-4|7|3", defaults);
            Assert.AreEqual(HouseRules.Targets.Length - 1, clamped.Target);
            Assert.AreEqual(0, clamped.Clock);
            StringAssert.Contains("GAME TO 11", HouseRules.Summary(c));
        }

        [Test]
        public void I2_HouseRules_ReachTheMatch_AndSurviveASave()
        {
            var cat = DefaultContent.Create();
            var rules = HouseRules.Apply(null, new HouseRules.Choice { Target = 1, Clock = 2, Scoring = 0, WinByTwo = 0 });
            var req = new MatchRequest { Mode = GameMode.QuickCall, HomeTeamId = cat.Teams[0].id, AwayTeamId = cat.Teams[1].id, RulesOverride = rules };
            var setup = MatchSetup.FromRequest(req, cat);
            Assert.AreEqual(15, setup.Rules.targetScore);
            Assert.AreEqual(180f, setup.Rules.gameClockSeconds);

            var d = new CareerSaveData();
            d.settings.houseRules = "0|1|1|1";
            var back = SaveCodec.Decode(SaveCodec.Encode(d), cat, out _);
            Assert.AreEqual("0|1|1|1", back.settings.houseRules);
        }
    }
}
