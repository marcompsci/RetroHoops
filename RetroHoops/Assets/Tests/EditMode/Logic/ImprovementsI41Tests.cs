using System;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>1 Nov 2026, game improvements I41–I50: on-court AI and game feel.</summary>
    public class ImprovementsI41Tests
    {
        private static ContentCatalog C => _c ??= DefaultContent.Create();
        private static ContentCatalog _c;

        private static BalanceReport _legend, _rookie;
        private static BalanceReport Legend => _legend ??= BalanceProbe.Run(C, "difficulty.legend", 80);
        private static BalanceReport Rookie => _rookie ??= BalanceProbe.Run(C, "difficulty.rookie", 80);

        private static void InRange(float v, float lo, float hi, string why = "")
        {
            Assert.IsTrue(v >= lo && v <= hi, $"{v:0.000} not in [{lo}, {hi}] {why}");
        }

        private static MatchSimulation Sim(GameMode mode, string difficulty, uint seed = 11)
        {
            var r = MatchRequest.QuickCallDefault(C);
            r.Mode = mode;
            r.DifficultyId = difficulty;
            r.Seed = seed;
            return new MatchSimulation(MatchSetup.FromRequest(r, C));
        }

        // ------------------------------------------------------------------ I41 FAIR DEMO

        [Test]
        public void I41_Demo_BothBenchesGetTheSameBrain()
        {
            var m = Sim(GameMode.Demo, "difficulty.legend");
            Assert.IsTrue(ReferenceEquals(m.AiProfile(0), m.AiProfile(1)));
            Assert.AreEqual("difficulty.legend", m.AiProfile(0).id);
        }

        [Test]
        public void I41_Demo_SeedDecidesWhoStartsWithTheBall()
        {
            bool sawHome = false, sawAway = false;
            for (uint s = 1; s <= 32; s++)
            {
                var m = Sim(GameMode.Demo, "difficulty.caller", s);
                if (m.Setup.StartingOffense == 0) sawHome = true; else sawAway = true;
            }
            Assert.IsTrue(sawHome && sawAway);
            // A played game still starts with team 0 on the ball.
            Assert.AreEqual(0, Sim(GameMode.QuickCall, "difficulty.caller", 9).Setup.StartingOffense);
        }

        [Test]
        public void I41_Demo_HomeAndAwayWinAboutEvenly()
        {
            Assert.AreEqual(0, Legend.Stalls, Legend.ToString());
            InRange(Legend.HomeWinShare, 0.35f, 0.65f, Legend.ToString());
            InRange(Rookie.HomeWinShare, 0.35f, 0.65f, Rookie.ToString());
        }

        // ------------------------------------------------------------------ I42 TEAMMATES KEEP UP

        [Test]
        public void I42_Teammates_NeverThinkWorseThanTheOtherBench()
        {
            foreach (var opp in C.Difficulties)
            {
                var mate = GameFeel.TeammateFor(MatchSimulation.FriendlyAi, opp);
                Assert.LessOrEqual(mate.reactionTime, opp.reactionTime + 1e-4f, opp.id);
                Assert.GreaterOrEqual(mate.decisionQuality, opp.decisionQuality - 1e-4f, opp.id);
                Assert.LessOrEqual(mate.errorRate, opp.errorRate + 1e-4f, opp.id);
                Assert.GreaterOrEqual(mate.releaseAccuracy, MatchSimulation.FriendlyAi.releaseAccuracy - 1e-4f, opp.id);
                Assert.LessOrEqual(mate.movementScale, 1f + 1e-4f, opp.id);
            }
        }

        [Test]
        public void I42_Teammates_ShootingOnlyMeetsAHardBenchHalfway()
        {
            var legend = C.Difficulties.Find(d => d.id == "difficulty.legend");
            var mate = GameFeel.TeammateFor(MatchSimulation.FriendlyAi, legend);
            Assert.Less(mate.releaseAccuracy, legend.releaseAccuracy);
            Assert.Greater(mate.releaseAccuracy, MatchSimulation.FriendlyAi.releaseAccuracy);
            // Against an easy bench nothing gets worse than the old fixed teammate.
            var rookie = C.Difficulties.Find(d => d.id == "difficulty.rookie");
            var easy = GameFeel.TeammateFor(MatchSimulation.FriendlyAi, rookie);
            Assert.AreEqual(MatchSimulation.FriendlyAi.decisionQuality, easy.decisionQuality, 1e-4f);
            Assert.AreEqual(MatchSimulation.FriendlyAi.releaseAccuracy, easy.releaseAccuracy, 1e-4f);
        }

        [Test]
        public void I42_PlayedGame_UsesTheTeammateBrainForYourSide()
        {
            var m = Sim(GameMode.QuickCall, "difficulty.legend");
            Assert.AreEqual("difficulty.legend", m.AiProfile(1).id);
            Assert.AreEqual(MatchSimulation.FriendlyAi.id, m.AiProfile(0).id);
            Assert.GreaterOrEqual(m.AiProfile(0).decisionQuality, m.AiProfile(1).decisionQuality);
        }

        // ------------------------------------------------------------------ I43 ATTACK THE RIM / I44 DRIVE AND KICK

        [Test]
        public void I43_FinishRange_IsInsideLayupRange()
        {
            var t = new ShotTuning();
            Assert.Less(GameFeel.FinishRange(t), t.layupMaxDistance);
            Assert.AreEqual(ShotType.Layup, ShotModel.Classify(GameFeel.FinishRange(t), ShotZone.InsideArc, 50, false, t));
        }

        [Test]
        public void I43_DriveEdge_NeedsRoomAndRange()
        {
            Assert.AreEqual(0f, GameFeel.DriveEdge(0.5f, 5f));
            Assert.AreEqual(0f, GameFeel.DriveEdge(2f, 9f));
            Assert.Greater(GameFeel.DriveEdge(1.6f, 5f), 0f);
            Assert.LessOrEqual(GameFeel.DriveEdge(9f, 5f), 0.25f);
        }

        [Test]
        public void I43_AiDrives_GetToTheRim()
        {
            Assert.Greater(Legend.DriveFinishes, Legend.Games, Legend.ToString());
            Assert.GreaterOrEqual(Legend.RimShotsPerGame, 2f, Legend.ToString());
            Assert.GreaterOrEqual(Rookie.RimShotsPerGame, 2f, Rookie.ToString());
        }

        [Test]
        public void I44_HelpDefense_GetsPunishedWithKickOuts()
        {
            Assert.Greater(Legend.KickOuts, Legend.Games / 2, Legend.ToString());
        }

        // ------------------------------------------------------------------ I45 BOX OUT

        [Test]
        public void I45_Defense_WinsMostRebounds()
        {
            InRange(Legend.OffensiveReboundShare, 0.18f, 0.45f, Legend.ToString());
            InRange(Rookie.OffensiveReboundShare, 0.18f, 0.48f, Rookie.ToString());
            Assert.Greater(new DefenseTuning().defensiveReboundEdge, 0f);
        }

        // ------------------------------------------------------------------ I46 PRESSURE

        [Test]
        public void I46_ReachChance_RisesWithDifficultyAndPressing()
        {
            float rookie = GameFeel.ReachChance(0.5f, 0.45f, false);
            float legend = GameFeel.ReachChance(0.5f, 0.9f, false);
            Assert.Greater(legend, rookie);
            Assert.AreEqual(legend * 2f, GameFeel.ReachChance(0.5f, 0.9f, true), 1e-5f);
            Assert.AreEqual(0f, GameFeel.ReachChance(0f, 0.9f, true));
        }

        [Test]
        public void I46_Turnovers_HappenButDontRunTheGame()
        {
            InRange(Legend.TurnoverShare, 0.06f, 0.25f, Legend.ToString());
            Assert.Greater(Legend.TurnoverShare, Rookie.TurnoverShare * 0.8f, $"{Legend} | {Rookie}");
        }

        // ------------------------------------------------------------------ I47 LATE-CLOCK IQ

        [Test]
        public void I47_ClockUrgency_BuildsThenMustShoot()
        {
            Assert.AreEqual(0f, GameFeel.ClockUrgency(10f, 14f));
            Assert.AreEqual(0f, GameFeel.ClockUrgency(6f, 14f), 1e-5f);
            float mid = GameFeel.ClockUrgency(4.5f, 14f);
            InRange(mid, 0.2f, 0.4f);
            Assert.Less(mid, GameFeel.ClockUrgency(3.2f, 14f));
            Assert.AreEqual(1f, GameFeel.ClockUrgency(2.9f, 14f));
            Assert.AreEqual(0f, GameFeel.ClockUrgency(1f, 999f), "no shot clock, no panic");
        }

        // ------------------------------------------------------------------ I48 CLOSE-OUTS

        [Test]
        public void I48_Defenders_CloseOutOnThePass()
        {
            Assert.Greater(Legend.CloseOuts, Legend.Games * 3, Legend.ToString());
            Assert.Greater(GameFeel.CloseOutGap, 0.3f);
        }

        // ------------------------------------------------------------------ I49 BALANCE PROBE

        [Test]
        public void I49_Probe_IsDeterministic()
        {
            var a = BalanceProbe.Run(C, "difficulty.caller", 6, 21);
            var b = BalanceProbe.Run(C, "difficulty.caller", 6, 21);
            Assert.AreEqual(a.ToString(), b.ToString());
            Assert.AreEqual(a.Shots, b.Shots);
            Assert.AreEqual(6, a.Games);
        }

        [Test]
        public void I49_Probe_EmptyRunIsSafe()
        {
            var r = BalanceProbe.Run(C, "difficulty.caller", 0);
            Assert.AreEqual(0, r.Games);
            Assert.AreEqual(0f, r.HomeWinShare);
            Assert.AreEqual(0f, r.OffensiveReboundShare);
        }

        // ------------------------------------------------------------------ I50 DIFFICULTY STILL MEANS SOMETHING

        [Test]
        public void I50_Legend_ShootsBetterAndScoresMoreThanRookie()
        {
            Assert.Greater(Legend.FieldGoalShare, Rookie.FieldGoalShare + 0.05f, $"{Legend} | {Rookie}");
            Assert.Less(Legend.Stalls + Rookie.Stalls, 1);
        }
    }
}
