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
    }
}
