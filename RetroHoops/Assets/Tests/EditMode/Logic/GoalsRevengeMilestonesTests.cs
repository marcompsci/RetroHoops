using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>16 Oct 2026 improvement phases I18-I20: game goals, revenge games, career milestones.</summary>
    public class GoalsRevengeMilestonesTests
    {
        [Test]
        public void I18_Goals_ThreeDistinct_Seeded()
        {
            for (uint seed = 0; seed < 300; seed++)
            {
                var g = GameGoals.Pick(seed, false);
                Assert.AreEqual(3, g.Count, "seed " + seed);
                var kinds = new HashSet<GoalKind>();
                foreach (var x in g) { Assert.IsTrue(kinds.Add(x.Kind), "no repeats"); Assert.Greater(x.Target, 0); Assert.Greater(x.Reward, 0); }
                Assert.IsFalse(kinds.Contains(GoalKind.Win) && kinds.Contains(GoalKind.WinBy), "WIN and WIN BY never together");
                var again = GameGoals.Pick(seed, false);
                for (int i = 0; i < 3; i++) Assert.AreEqual(g[i].Kind, again[i].Kind, "same game, same goals");
            }
            int half = 0, full = 0;
            for (uint seed = 0; seed < 200; seed++)
            {
                foreach (var x in GameGoals.Pick(seed, false)) if (x.Kind == GoalKind.Points) half += x.Target;
                foreach (var x in GameGoals.Pick(seed, true)) if (x.Kind == GoalKind.Points) full += x.Target;
            }
            Assert.Greater(full, half, "Full Court asks for more");
        }

        [Test]
        public void I18_Goals_Score()
        {
            var goals = new List<GameGoal>
            {
                new GameGoal { Kind = GoalKind.Points, Target = 8, Reward = 15 },
                new GameGoal { Kind = GoalKind.WinBy, Target = 5, Reward = 20 },
                new GameGoal { Kind = GoalKind.CleanHands, Target = 1, Reward = 15 },
            };
            var s = new PlayerStatLine { points = 9, turnovers = 1 };
            Assert.AreEqual(15 + 20 + 15 + GameGoals.AllThreeBonus, GameGoals.Score(goals, s, true, 6, out string all));
            StringAssert.Contains("GOALS 3/3", all);
            Assert.AreEqual(15 + 15, GameGoals.Score(goals, s, true, 3, out string two), "won by 3: not 5");
            StringAssert.Contains("GOALS 2/3", two);
            s.turnovers = 3;
            Assert.AreEqual(15, GameGoals.Score(goals, s, false, -2, out _));
            Assert.AreEqual(0, GameGoals.Score(null, s, true, 5, out string none));
            Assert.IsNull(none);
            StringAssert.Contains("GOALS: 8 PTS", GameGoals.Card(goals));
            Assert.IsFalse(GameGoals.Applies(GameMode.Versus));
            Assert.IsFalse(GameGoals.Applies(GameMode.Clutch));
            Assert.IsTrue(GameGoals.Applies(GameMode.Streets));
        }

        [Test]
        public void I19_Revenge()
        {
            Assert.IsTrue(Revenge.Applies(GameMode.QuickCall));
            Assert.IsFalse(Revenge.Applies(GameMode.Daily), "the Daily is once a day");
            Assert.IsFalse(Revenge.Applies(GameMode.Versus));
            Assert.Greater(Revenge.BonusSP, 0);
            Assert.IsFalse(new MatchRequest().Revenge, "off unless it's a rematch after a loss");
        }

        [Test]
        public void I20_Milestones()
        {
            var a = new CareerTotals { games = 9, wins = 4, points = 95, assists = 240 };
            var b = new CareerTotals { games = 10, wins = 5, points = 112, assists = 251 };
            var m = CareerMilestones.Crossed(a, b);
            Assert.AreEqual(3, m.Count);
            Assert.Contains("10 CAREER GAMES", m);
            Assert.Contains("100 CAREER POINTS", m);
            Assert.Contains("250 CAREER ASSISTS", m);
            // A jump past two steps calls only the bigger one.
            Assert.AreEqual("50 CAREER POINTS", CareerMilestones.Crossed(new CareerTotals { points = 9 }, new CareerTotals { points = 60 })[0]);
            Assert.AreEqual(0, CareerMilestones.Crossed(b, b).Count);
            var snap = CareerMilestones.Snapshot(b);
            b.points = 999;
            Assert.AreEqual(112, snap.points, "a snapshot is a copy");
        }
    }
}
