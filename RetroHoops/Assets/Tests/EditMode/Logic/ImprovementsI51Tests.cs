using System;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>1 Nov 2026, game improvements I51–I60: smarter AI on the ball, at the rim and at game point.</summary>
    public class ImprovementsI51Tests
    {
        private static ContentCatalog C => _c ??= DefaultContent.Create();
        private static ContentCatalog _c;

        private static BalanceReport _legend, _rookie;
        private static BalanceReport Legend => _legend ??= BalanceProbe.Run(C, "difficulty.legend", 80, 9);
        private static BalanceReport Rookie => _rookie ??= BalanceProbe.Run(C, "difficulty.rookie", 80, 9);

        private static MatchSimulation Sim(GameMode mode = GameMode.Demo, uint seed = 11)
        {
            var r = MatchRequest.QuickCallDefault(C);
            r.Mode = mode;
            r.DifficultyId = "difficulty.caller";
            r.Seed = seed;
            return new MatchSimulation(MatchSetup.FromRequest(r, C));
        }

        [Test]
        public void I51_FakeChance_RisesWithSmarts_AndAiFakesHappen()
        {
            Assert.Greater(GameFeelPlus.FakeChance(0.9f), GameFeelPlus.FakeChance(0.45f));
            Assert.LessOrEqual(GameFeelPlus.FakeChance(5f), 0.6f + 1e-5f);
            Assert.Greater(Legend.AiFakes, 0, Legend.ToString());
            Assert.Greater(Rookie.AiFakes, 0, Rookie.ToString());
        }

        [Test]
        public void I52_I53_ScorersGetFed()
        {
            Assert.Greater(Legend.ScorerFeeds + Rookie.ScorerFeeds, 0, $"{Legend} | {Rookie}");
        }

        [Test]
        public void I53_GamePoint_IsOneArcBasketAway()
        {
            Assert.IsTrue(GameFeelPlus.IsGamePoint(19, 21, 2));
            Assert.IsTrue(GameFeelPlus.IsGamePoint(20, 21, 2));
            Assert.IsFalse(GameFeelPlus.IsGamePoint(18, 21, 2));
            Assert.IsFalse(GameFeelPlus.IsGamePoint(21, 21, 2), "already won");
            Assert.IsFalse(GameFeelPlus.IsGamePoint(998, 999, 2), "no real target, no game point");
            Assert.IsTrue(GameFeelPlus.IsGamePoint(10, 11, 1));
        }

        [Test]
        public void I53_GoToScorer_IsOnTheTeam_AndFollowsPoints()
        {
            var m = Sim();
            for (int team = 0; team < 2; team++)
            {
                int go = m.GoToScorer(team);
                Assert.GreaterOrEqual(go, 0);
                Assert.AreEqual(team, m.Players[go].Team);
            }
            // Points this game outweigh ratings.
            int other = -1;
            for (int i = 0; i < m.Players.Length; i++) if (m.Players[i].Team == 0 && i != m.GoToScorer(0)) { other = i; break; }
            m.Stats[other].points = 12;
            Assert.AreEqual(other, m.GoToScorer(0));
        }

        [Test]
        public void I53_GamePoint_FollowsTheScore()
        {
            var m = Sim();
            Assert.IsFalse(m.IsGamePoint(0));
            m.Score[0] = m.Setup.Rules.targetScore - 1;
            Assert.IsTrue(m.IsGamePoint(0));
            Assert.IsFalse(m.IsGamePoint(1));
            Assert.IsFalse(m.IsGamePoint(2));
        }

        [Test]
        public void I54_Putbacks_Happen()
        {
            Assert.Greater(Legend.Putbacks + Rookie.Putbacks, 0, $"{Legend} | {Rookie}");
            Assert.Less(GameFeelPlus.PutbackWindow, 2f);
        }

        [Test]
        public void I55_SmotheredHandlers_CallScreens()
        {
            Assert.Greater(Legend.ScreenCalls, Legend.Games, Legend.ToString());
        }

        [Test]
        public void I56_TiredLegs_DriveLess()
        {
            Assert.AreEqual(0f, GameFeelPlus.TiredDrivePenalty(1f));
            Assert.AreEqual(0f, GameFeelPlus.TiredDrivePenalty(GameFeelPlus.TiredStamina));
            Assert.Greater(GameFeelPlus.TiredDrivePenalty(0.1f), GameFeelPlus.TiredDrivePenalty(0.3f));
        }

        [Test]
        public void I57_CatchAndShoot_OnlyWhenOpenAndFresh()
        {
            Assert.Greater(GameFeelPlus.CatchAndShootLean(0.2f, 3f), 0f);
            Assert.AreEqual(0f, GameFeelPlus.CatchAndShootLean(2f, 3f), "too late");
            Assert.AreEqual(0f, GameFeelPlus.CatchAndShootLean(0.2f, 0.8f), "covered");
            Assert.AreEqual(0f, GameFeelPlus.CatchAndShootLean(-1f, 3f));
        }

        [Test]
        public void I58_ProbeV2_CountsAssists_AndStaysDeterministic()
        {
            Assert.Greater(Legend.Assists, Rookie.Assists, $"{Legend} | {Rookie}");
            var a = BalanceProbe.Run(C, "difficulty.caller", 5, 33);
            var b = BalanceProbe.Run(C, "difficulty.caller", 5, 33);
            Assert.AreEqual(a.AiFakes, b.AiFakes);
            Assert.AreEqual(a.Assists, b.Assists);
            Assert.AreEqual(a.ToString(), b.ToString());
        }

        [Test]
        public void I59_I60_GamesStillFairAndFinish()
        {
            Assert.AreEqual(0, Legend.Stalls + Rookie.Stalls);
            Assert.IsTrue(Legend.HomeWinShare >= 0.33f && Legend.HomeWinShare <= 0.67f, Legend.ToString());
            Assert.IsTrue(Rookie.HomeWinShare >= 0.33f && Rookie.HomeWinShare <= 0.67f, Rookie.ToString());
            Assert.Greater(Legend.FieldGoalShare, Rookie.FieldGoalShare + 0.05f, $"{Legend} | {Rookie}");
            Assert.IsTrue(Legend.OffensiveReboundShare < 0.48f, Legend.ToString());
        }
    }
}
