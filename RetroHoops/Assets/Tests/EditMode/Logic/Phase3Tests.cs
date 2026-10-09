using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    public class ShotModelTests
    {
        private readonly ShotTuning _t = ShotTuning.Default;

        private static AttributeSet Ratings(int shooting, int finishing = 60) =>
            new AttributeSet(finishing, shooting, 60, 60, 60, 60, 60, 60);

        private ShotContext Ctx(ShotType type, float meter, float defender = 10f, int shooting = 70, float distance = 7f) =>
            new ShotContext
            {
                Type = type, Distance = distance, Meter = meter, Shooter = Ratings(shooting),
                NearestDefenderDistance = defender, NearestDefenderDefense = 70, Stamina01 = 1f,
            };

        [Test]
        public void Grade_CoversAllFiveWindows()
        {
            Assert.AreEqual(TimingGrade.Green, ShotModel.Grade(_t.greenCenter, 70, _t));
            Assert.AreEqual(TimingGrade.SlightlyEarly, ShotModel.Grade(_t.greenCenter - 0.1f, 70, _t));
            Assert.AreEqual(TimingGrade.TooEarly, ShotModel.Grade(0.3f, 70, _t));
            Assert.AreEqual(TimingGrade.SlightlyLate, ShotModel.Grade(_t.greenCenter + 0.1f, 70, _t));
            Assert.AreEqual(TimingGrade.TooLate, ShotModel.Grade(1.05f, 70, _t));
        }

        [Test]
        public void BetterShooters_GetWiderGreenWindows()
        {
            Assert.Greater(ShotModel.GreenHalfWidth(95, _t), ShotModel.GreenHalfWidth(40, _t));
        }

        [Test]
        public void Green_BeatsEveryOtherTiming()
        {
            float green = ShotModel.Evaluate(Ctx(ShotType.Arc, _t.greenCenter), _t).MakeChance;
            foreach (float m in new[] { 0.2f, _t.greenCenter - 0.1f, _t.greenCenter + 0.1f, 1.2f })
                Assert.Greater(green, ShotModel.Evaluate(Ctx(ShotType.Arc, m), _t).MakeChance, "meter " + m);
        }

        [Test]
        public void Green_IsNotAGuaranteedMake_ByDefault()
        {
            var e = ShotModel.Evaluate(Ctx(ShotType.Arc, _t.greenCenter, 0.3f, 50, 9f), _t);
            Assert.Less(e.MakeChance, 1f);
            Assert.IsFalse(e.GuaranteedMake);
        }

        [Test]
        public void DebugFlag_MakesGreenGuaranteed()
        {
            var t = new ShotTuning { debugGreenAlwaysMakes = true };
            Assert.IsTrue(ShotModel.Evaluate(Ctx(ShotType.Arc, t.greenCenter), t).GuaranteedMake);
            Assert.IsFalse(ShotModel.Evaluate(Ctx(ShotType.Arc, 0.2f), t).GuaranteedMake);
        }

        [Test]
        public void Contest_LowersChance_AndIsReported()
        {
            var open = ShotModel.Evaluate(Ctx(ShotType.MidRange, _t.greenCenter, 5f, 70, 4f), _t);
            var contested = ShotModel.Evaluate(Ctx(ShotType.MidRange, _t.greenCenter, 0.4f, 70, 4f), _t);
            Assert.Greater(open.MakeChance, contested.MakeChance);
            Assert.IsTrue(contested.Contested);
            Assert.IsTrue(open.Open);
        }

        [Test]
        public void DeepShots_ArePenalised()
        {
            var close = ShotModel.Evaluate(Ctx(ShotType.Arc, _t.greenCenter, 10f, 70, 7f), _t);
            var deep = ShotModel.Evaluate(Ctx(ShotType.Arc, _t.greenCenter, 10f, 70, 10f), _t);
            Assert.Greater(close.MakeChance, deep.MakeChance);
        }

        [Test]
        public void HotStreak_Helps_UpToACap()
        {
            var c = Ctx(ShotType.Arc, _t.greenCenter);
            float cold = ShotModel.Evaluate(c, _t).MakeChance;
            c.HotStreak = 3;
            float hot = ShotModel.Evaluate(c, _t).MakeChance;
            c.HotStreak = 50;
            var capped = ShotModel.Evaluate(c, _t);
            Assert.Greater(hot, cold);
            Assert.AreEqual(_t.hotStreakMax, capped.HotStreakBonus, 1e-5);
        }

        [Test]
        public void Chance_IsAlwaysClamped()
        {
            var awful = Ctx(ShotType.Arc, 0.05f, 0.1f, 1, 14f);
            var elite = new ShotContext
            {
                Type = ShotType.Dunk, Distance = 0.5f, Meter = _t.greenCenter, Shooter = Ratings(99, 99),
                NearestDefenderDistance = 20f, HotStreak = 10, Stamina01 = 1f,
            };
            Assert.AreEqual(_t.minChance, ShotModel.Evaluate(awful, _t).MakeChance, 1e-5);
            Assert.AreEqual(_t.maxChance, ShotModel.Evaluate(elite, _t).MakeChance, 1e-5);
        }

        [Test]
        public void Fatigue_Hurts()
        {
            var fresh = Ctx(ShotType.MidRange, _t.greenCenter, 10f, 70, 4f);
            var tired = fresh;
            tired.Stamina01 = 0.2f;
            Assert.Greater(ShotModel.Evaluate(fresh, _t).MakeChance, ShotModel.Evaluate(tired, _t).MakeChance);
        }

        [Test]
        public void Classify_UsesDistanceZoneAndFinishing()
        {
            Assert.AreEqual(ShotType.Arc, ShotModel.Classify(7.5f, ShotZone.BeyondArc, 90, false, _t));
            Assert.AreEqual(ShotType.MidRange, ShotModel.Classify(4f, ShotZone.InsideArc, 90, false, _t));
            Assert.AreEqual(ShotType.Layup, ShotModel.Classify(1.2f, ShotZone.InsideArc, 40, true, _t));
            Assert.AreEqual(ShotType.Dunk, ShotModel.Classify(1.0f, ShotZone.InsideArc, 85, true, _t));
            Assert.AreEqual(ShotType.Layup, ShotModel.Classify(1.0f, ShotZone.InsideArc, 85, false, _t));
        }

        [Test]
        public void FeedbackText_MatchesCallouts()
        {
            Assert.AreEqual("GREEN", ShotModel.FeedbackText(ShotFeedback.Green));
            Assert.AreEqual("CLEAN LOOK", ShotModel.FeedbackText(ShotFeedback.CleanLook));
            Assert.AreEqual("CONTESTED", ShotModel.FeedbackText(ShotFeedback.Contested));
            Assert.AreEqual("TOO EARLY", ShotModel.FeedbackText(ShotFeedback.TooEarly));
            Assert.AreEqual("TOO LATE", ShotModel.FeedbackText(ShotFeedback.TooLate));
        }
    }

    public class PassModelTests
    {
        private readonly PassTuning _t = PassTuning.Default;

        [Test]
        public void LaneClearance_IgnoresDefendersBehindThePasser()
        {
            float c = PassModel.LaneClearance(new Vec2(0f, 0f), new Vec2(10f, 0f), new Vec2(-2f, 0f), out _);
            Assert.AreEqual(float.MaxValue, c);
            float inLane = PassModel.LaneClearance(new Vec2(0f, 0f), new Vec2(10f, 0f), new Vec2(5f, 0.3f), out float along);
            Assert.AreEqual(0.3f, inLane, 1e-4);
            Assert.AreEqual(0.5f, along, 1e-4);
        }

        [Test]
        public void SelectTarget_PrefersTheOpenTeammate()
        {
            var candidates = new List<PassCandidate>
            {
                new PassCandidate { PlayerIndex = 1, Position = new Vec2(-5f, 5f) },
                new PassCandidate { PlayerIndex = 2, Position = new Vec2(5f, 5f) },
            };
            var defenders = new List<Vec2> { new Vec2(-5f, 5.3f) }; // covering #1
            int k = PassModel.SelectTarget(new Vec2(0f, 9f), Vec2.Zero, candidates, defenders, _t);
            Assert.AreEqual(1, k);
        }

        [Test]
        public void SelectTarget_FollowsTheStick()
        {
            var candidates = new List<PassCandidate>
            {
                new PassCandidate { PlayerIndex = 1, Position = new Vec2(-5f, 5f) },
                new PassCandidate { PlayerIndex = 2, Position = new Vec2(5f, 5f) },
            };
            var defenders = new List<Vec2>();
            Assert.AreEqual(0, PassModel.SelectTarget(new Vec2(0f, 9f), new Vec2(-1f, 0f), candidates, defenders, _t));
            Assert.AreEqual(1, PassModel.SelectTarget(new Vec2(0f, 9f), new Vec2(1f, 0f), candidates, defenders, _t));
        }

        [Test]
        public void ChooseType_UsesBouncePassWhenTheLaneIsTight()
        {
            Assert.AreEqual(PassType.Chest, PassModel.ChooseType(3f, _t));
            Assert.AreEqual(PassType.Bounce, PassModel.ChooseType(0.5f, _t));
        }

        [Test]
        public void InterceptChance_IsZeroForAnOpenLane()
        {
            Assert.AreEqual(0f, PassModel.InterceptChance(PassType.Chest, 2f, 99, 1, _t));
            Assert.Greater(PassModel.InterceptChance(PassType.Chest, 0.05f, 90, 40, _t), 0.3f);
        }

        [Test]
        public void GoodPassers_ThreadTighterLanes()
        {
            float poor = PassModel.InterceptChance(PassType.Chest, 0.2f, 70, 20, _t);
            float elite = PassModel.InterceptChance(PassType.Chest, 0.2f, 70, 95, _t);
            Assert.Greater(poor, elite);
        }
    }

    public class MatchFlowTests
    {
        private const float Dt = 1f / 60f;

        private static MatchSimulation NewMatch(uint seed = 7, bool greenAlwaysMakes = false, string difficulty = null)
        {
            var c = DefaultContent.Create();
            var req = MatchRequest.QuickCallDefault(c);
            req.Seed = seed;
            if (difficulty != null) req.DifficultyId = difficulty;
            var setup = MatchSetup.FromRequest(req, c);
            setup.Shot.debugGreenAlwaysMakes = greenAlwaysMakes;
            return new MatchSimulation(setup);
        }

        private static void Run(MatchSimulation m, float seconds, PlayerInput input = default)
        {
            int frames = (int)(seconds * 60f);
            for (int i = 0; i < frames; i++) m.Step(Dt, input);
        }

        /// <summary>Holds shoot for the frames that land a green release, then lets go.</summary>
        private static void GreenJumper(MatchSimulation m)
        {
            m.Step(Dt, new PlayerInput { ShootPressed = true, ShootHeld = true });
            Assert.AreEqual(m.ControlledIndex, m.ChargingIndex, "Shot did not start");
            while (m.ChargeMeter < m.Setup.Shot.greenCenter - 0.01f) m.Step(Dt, new PlayerInput { ShootHeld = true });
            m.Step(Dt, new PlayerInput { ShootHeld = false });
        }

        private static bool Saw(List<MatchEvent> log, MatchEventType type) => log.Exists(e => e.Type == type);

        private static List<MatchEvent> RunUntil(MatchSimulation m, System.Func<bool> done, float maxSeconds, PlayerInput input = default)
        {
            var log = new List<MatchEvent>();
            for (int i = 0; i < maxSeconds * 60f && !done(); i++)
            {
                m.Step(Dt, input);
                log.AddRange(m.Events);
            }
            return log;
        }

        [Test]
        public void HumanGreenJumper_FromCheckSpot_ScoresTwo_AndKeepsTheBall()
        {
            var m = NewMatch(greenAlwaysMakes: true);
            m.Setup.PassiveOpponents = true; // no contests, no steals: isolate the scoring path
            Assert.IsTrue(m.Setup.WinnersBall, "half-court 3-on-3 is winners' ball");
            GreenJumper(m);
            Assert.AreEqual(BallPhase.Shot, m.Ball.Phase);
            RunUntil(m, () => m.Phase == MatchPhase.CheckBall, 5f);
            Assert.AreEqual(2, m.Score[0]);
            Assert.AreEqual(0, m.OffenseTeam, "the team that scores gets the ball back");
            Assert.AreEqual(MatchSimulation.IndexOf(0, 0), m.HolderIndex);
        }

        [Test]
        public void HumanGreenJumper_WithoutWinnersBall_GivesTheBallAway()
        {
            var m = NewMatch(greenAlwaysMakes: true);
            m.Setup.PassiveOpponents = true; // no contests, no steals: isolate the scoring path
            m.Setup.WinnersBall = false;
            GreenJumper(m);
            Assert.AreEqual(BallPhase.Shot, m.Ball.Phase);
            var log = RunUntil(m, () => m.Phase == MatchPhase.CheckBall, 5f);
            Assert.IsTrue(Saw(log, MatchEventType.ShotMade));
            Assert.AreEqual(2, m.Score[0]);
            Assert.AreEqual(0, m.Score[1]);
            Assert.AreEqual(2, m.Stats[0].points);
            Assert.AreEqual(1, m.Stats[0].fieldGoalsMade);
            Assert.AreEqual(1, m.Stats[0].arcMade);
            Assert.AreEqual(1, m.OffenseTeam, "Possession should change after a made basket");
            Assert.AreEqual(MatchSimulation.IndexOf(1, 0), m.HolderIndex);
        }

        [Test]
        public void TooEarlyRelease_IsCalledOut()
        {
            var m = NewMatch();
            m.Step(Dt, new PlayerInput { ShootPressed = true, ShootHeld = true });
            // Hold past the pump-fake window (a one-frame tap is a pump fake since 9 Oct 2026), then let go early.
            for (float t = 0f; t < 0.3f; t += Dt) m.Step(Dt, new PlayerInput { ShootHeld = true });
            m.Step(Dt, new PlayerInput { ShootHeld = false });
            var released = m.Events.Find(e => e.Type == MatchEventType.ShotReleased);
            Assert.AreEqual(MatchEventType.ShotReleased, released.Type);
            Assert.AreEqual((int)ShotFeedback.TooEarly, released.Value);
        }

        [Test]
        public void HoldingTooLong_AutoReleasesLate()
        {
            var m = NewMatch();
            m.Step(Dt, new PlayerInput { ShootPressed = true, ShootHeld = true });
            var log = RunUntil(m, () => m.Ball.Phase == BallPhase.Shot, 3f, new PlayerInput { ShootHeld = true });
            var released = log.Find(e => e.Type == MatchEventType.ShotReleased);
            Assert.AreEqual((int)ShotFeedback.TooLate, released.Value);
        }

        [Test]
        public void Pass_ReachesTeammate_AndMadeShotCreditsAnAssist()
        {
            var m = NewMatch(greenAlwaysMakes: true);
            m.Setup.PassiveOpponents = true;
            m.Step(Dt, new PlayerInput { PassPressed = true, Move = new Vec2(-1f, 0f) }); // aim left wing
            Assert.AreEqual(BallPhase.Pass, m.Ball.Phase);
            var log = RunUntil(m, () => m.Ball.IsHeld, 2f);
            Assert.IsTrue(Saw(log, MatchEventType.PassCaught));
            int receiver = m.HolderIndex;
            Assert.AreEqual(0, m.Players[receiver].Team);
            Assert.AreNotEqual(m.ControlledIndex, receiver, "Human keeps controlling their own player");
            Assert.AreEqual(m.ControlledIndex, m.ControlledIndex);

            // Ask for it back, then score: the teammate gets the assist.
            m.Step(Dt, new PlayerInput { PassPressed = true });
            RunUntil(m, () => m.HumanHasBall, 3f);
            Assert.IsTrue(m.HumanHasBall);
            GreenJumper(m);
            RunUntil(m, () => m.Phase != MatchPhase.Live, 4f);
            Assert.AreEqual(1, m.Stats[receiver].assists);
        }

        [Test]
        public void Interception_ChangesPossession_AndForcesAClear()
        {
            var m = NewMatch();
            Run(m, 1f); // past check ball
            // Put a defender right in the lane to the left wing.
            var passer = m.Controlled;
            var wing = m.Players[1];
            var thief = m.Players[MatchSimulation.IndexOf(1, 1)];
            thief.Motion.position = Vec2.Lerp(passer.Position, wing.Position, 0.5f);
            // Try a few seeds' worth of passes until one is picked off (deterministic per seed).
            bool intercepted = false;
            for (int attempt = 0; attempt < 12 && !intercepted; attempt++)
            {
                if (!m.HumanHasBall) break;
                thief.Motion.position = Vec2.Lerp(m.Controlled.Position, wing.Position, 0.5f);
                m.Step(Dt, new PlayerInput { PassPressed = true, Move = (wing.Position - m.Controlled.Position).Normalized });
                var log = RunUntil(m, () => m.Ball.IsHeld, 2f);
                intercepted = Saw(log, MatchEventType.Interception);
                if (!intercepted && m.HolderIndex != m.ControlledIndex)
                {
                    m.Step(Dt, new PlayerInput { PassPressed = true });
                    RunUntil(m, () => m.HumanHasBall, 3f);
                }
            }
            Assert.IsTrue(intercepted, "A defender standing in the lane never intercepted");
            Assert.AreEqual(1, m.OffenseTeam);
            Assert.GreaterOrEqual(m.Stats[m.HolderIndex].steals, 1);
            Assert.GreaterOrEqual(m.Stats[0].turnovers, 1);
            Assert.IsTrue(m.MustClear || m.Setup.Court.ZoneOf(m.Holder.Position) == ShotZone.BeyondArc);
        }

        [Test]
        public void MustClear_BlocksShots_UntilBeyondTheArc()
        {
            var m = NewMatch();
            Run(m, 1f);
            // Hand the ball to the human's defender under the rim via a loose ball.
            var d = m.Players[MatchSimulation.IndexOf(1, 0)];
            d.Motion.position = new Vec2(0.5f, 2.5f);
            m.KnockLoose(Vec2.Zero, 0f);
            m.Ball.Position = d.Position;
            m.Ball.Height = 0.3f;
            for (int i = 0; i < 3; i++) m.Players[i].Motion.position = m.Players[i].Position + new Vec2(0f, 4f);
            RunUntil(m, () => m.OffenseTeam == 1, 1f);
            Assert.AreEqual(1, m.OffenseTeam);
            Assert.IsTrue(m.MustClear);
            Assert.IsFalse(m.CanShoot(m.HolderIndex));
        }

        [Test]
        public void ShotClock_Violation_TurnsTheBallOver()
        {
            var m = NewMatch();
            m.Setup.PassiveOpponents = true;
            var log = RunUntil(m, () => m.OffenseTeam == 1, m.Setup.Rules.shotClockSeconds + 4f);
            Assert.IsTrue(Saw(log, MatchEventType.ShotClockViolation));
            Assert.AreEqual(1, m.OffenseTeam);
            Assert.AreEqual(1, m.Stats[0].turnovers);
        }

        [Test]
        public void Game_EndsAtTargetScore()
        {
            var m = NewMatch(greenAlwaysMakes: true);
            m.Setup.PassiveOpponents = true;
            m.Score[0] = m.Setup.Rules.targetScore - 2;
            GreenJumper(m);
            var log = RunUntil(m, () => m.IsOver, 5f);
            Assert.IsTrue(m.IsOver);
            Assert.AreEqual(0, m.Winner);
            Assert.AreEqual(GameOverReason.TargetScore, m.EndReason);
            Assert.AreEqual(1, log.FindAll(e => e.Type == MatchEventType.GameOver).Count, "GameOver must fire exactly once");
        }

        [Test]
        public void Game_EndsWhenTheClockRunsOut_WithALeader()
        {
            var m = NewMatch();
            m.Setup.PassiveOpponents = true;
            m.Score[0] = 3;
            var log = RunUntil(m, () => m.IsOver, m.Setup.Rules.gameClockSeconds + 30f);
            Assert.IsTrue(m.IsOver);
            Assert.AreEqual(0, m.Winner);
            Assert.AreEqual(GameOverReason.ClockExpired, m.EndReason);
            Assert.IsTrue(Saw(log, MatchEventType.GameOver));
        }

        [Test]
        public void TiedAtTheHorn_KeepsPlaying()
        {
            var m = NewMatch();
            m.Setup.PassiveOpponents = true;
            RunUntil(m, () => m.GameClock <= 0f, m.Setup.Rules.gameClockSeconds + 30f);
            Run(m, 2f);
            Assert.AreEqual(0f, m.GameClock, 1e-5);
            Assert.IsFalse(m.IsOver, "0-0 at the horn should go to sudden death");
        }

        [Test]
        public void Practice_KeepsTheBallAfterScoring_AndHasNoShotClock()
        {
            var c = DefaultContent.Create();
            var setup = MatchSetup.FromRequest(MatchRequest.PracticeDefault(), c);
            setup.Shot.debugGreenAlwaysMakes = true;
            var m = new MatchSimulation(setup);
            GreenJumper(m);
            RunUntil(m, () => m.Phase == MatchPhase.CheckBall, 5f);
            Assert.Greater(m.Score[0], 0);
            Assert.AreEqual(0, m.OffenseTeam);
            Assert.IsTrue(m.HumanHasBall);
            var log = RunUntil(m, () => false, 30f);
            Assert.IsFalse(Saw(log, MatchEventType.ShotClockViolation));
            Assert.IsFalse(m.IsOver);
        }

        [TestCase(1u, "difficulty.rookie")]
        [TestCase(2u, "difficulty.caller")]
        [TestCase(3u, "difficulty.legend")]
        public void AiVersusAi_FullGame_FinishesCleanly(uint seed, string difficulty)
        {
            // The human idles in the corner, so both teams' AI (and the clock) decide the game.
            var m = NewMatch(seed, difficulty: difficulty);
            var log = RunUntil(m, () => m.IsOver, 600f, new PlayerInput { Move = Vec2.Zero });
            Assert.IsTrue(m.IsOver, "Game never ended (seed " + seed + ")");
            Assert.Greater(m.Score[0] + m.Score[1], 0, "Nobody scored");
            Assert.IsTrue(Saw(log, MatchEventType.PassThrown), "AI never passed");
            Assert.IsTrue(Saw(log, MatchEventType.ShotReleased), "AI never shot");
            var court = m.Setup.Court;
            foreach (var p in m.Players) Assert.IsTrue(court.Contains(p.Position));
            // Box score adds up.
            Assert.AreEqual(m.Score[0], m.Stats.TeamPoints(0, MatchSimulation.PlayersPerTeam));
            Assert.AreEqual(m.Score[1], m.Stats.TeamPoints(1, MatchSimulation.PlayersPerTeam));
        }

        [Test]
        public void SameSeed_SameGame()
        {
            var a = NewMatch(42);
            var b = NewMatch(42);
            RunUntil(a, () => a.IsOver, 300f);
            RunUntil(b, () => b.IsOver, 300f);
            Assert.AreEqual(a.Score[0], b.Score[0]);
            Assert.AreEqual(a.Score[1], b.Score[1]);
            Assert.AreEqual(a.Time, b.Time, 1e-4);
        }

        [Test]
        public void PlayerOfTheGame_ComesFromTheWinners()
        {
            var m = NewMatch(5);
            RunUntil(m, () => m.IsOver, 600f);
            Assert.IsTrue(m.IsOver);
            int team = m.Winner < 0 ? 0 : m.Winner;
            int potg = m.Stats.PlayerOfTheGame(team, MatchSimulation.PlayersPerTeam);
            Assert.AreEqual(team, m.Players[potg].Team);
        }
    }
}
