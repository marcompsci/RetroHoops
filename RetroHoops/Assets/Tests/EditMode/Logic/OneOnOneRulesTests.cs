using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>9 Oct 2026: 1-on-1 is make it, take it, and a ball that goes out of bounds is the other player's.</summary>
    public class OneOnOneRulesTests
    {
        private const float Dt = 1f / 60f;

        private static MatchSimulation OneOnOne(uint seed = 8, GameMode mode = GameMode.OneOnOne)
        {
            var c = DefaultContent.Create();
            var r = new MatchRequest { Mode = mode, HomeTeamId = "team.metro_comets", AwayTeamId = "team.harbor_hounds", RulesId = "rules.oneonone", Seed = seed };
            var setup = MatchSetup.FromRequest(r, c);
            setup.Shot.debugGreenAlwaysMakes = true;
            var m = new MatchSimulation(setup);
            for (int i = 0; i < 120 && m.Phase != MatchPhase.Live; i++) m.Step(Dt, default);
            return m;
        }

        private static List<MatchEvent> RunUntil(MatchSimulation m, System.Func<bool> done, float seconds, PlayerInput input = default)
        {
            var log = new List<MatchEvent>();
            for (int i = 0; i < (int)(seconds * 60) && !done(); i++)
            {
                m.Step(Dt, input);
                log.AddRange(m.Events);
            }
            return log;
        }

        [Test]
        public void OneOnOne_RulesAreMakeItTakeIt()
        {
            var c = DefaultContent.Create();
            Assert.IsTrue(c.Find(c.Rules, "rules.oneonone").makeItTakeIt);
        }

        [Test]
        public void TheScorer_GetsTheBallBack()
        {
            var m = OneOnOne();
            m.Setup.PassiveOpponents = true;
            int me = m.ControlledIndex;
            int myTeam = m.Players[me].Team;
            // A green jumper from the free-throw area (always goes in with debugGreenAlwaysMakes).
            m.Players[me].Motion.position = m.Setup.Court.Hoop + new Vec2(0f, 4.5f);
            m.Step(Dt, new PlayerInput { ShootPressed = true, ShootHeld = true });
            float green = m.Setup.Shot.greenCenter * ShotModel.FillSeconds(m.ChargeType, m.Setup.Shot);
            for (float t = Dt; t < green; t += Dt) m.Step(Dt, new PlayerInput { ShootHeld = true });
            var log = RunUntil(m, () => false, 0.1f);
            log.AddRange(RunUntil(m, () => m.Phase == MatchPhase.Live && m.Ball.IsHeld, 6f));
            Assert.IsTrue(log.Exists(e => e.Type == MatchEventType.ShotMade && e.Team == myTeam), "scored");
            Assert.AreEqual(myTeam, m.OffenseTeam, "make it, take it");
            Assert.IsTrue(m.Ball.IsHeld);
            Assert.AreEqual(myTeam, m.Players[m.Ball.HolderIndex].Team);
        }

        [Test]
        public void ALooseBallOutOfBounds_GoesToTheOtherPlayer()
        {
            var m = OneOnOne();
            int me = m.ControlledIndex;
            int myTeam = m.Players[me].Team;
            Assert.AreEqual(myTeam, m.Players[m.Ball.HolderIndex].Team);
            // Fumble it right by the sideline, rolling out: we touched it last.
            m.Setup.PassiveOpponents = true;
            m.KnockLoose(new Vec2(4f, 0f), 0.3f);
            m.Ball.Position = new Vec2(m.Setup.Court.HalfWidth - 0.4f, 5f);
            var log = RunUntil(m, () => m.Phase == MatchPhase.Live && m.Ball.IsHeld && m.Players[m.Ball.HolderIndex].Team != myTeam, 6f);
            var oob = log.Find(e => e.Type == MatchEventType.Violation);
            Assert.AreEqual(MatchEventType.Violation, oob.Type, "whistle");
            Assert.AreEqual((int)ViolationKind.OutOfBounds, oob.Value);
            Assert.AreEqual(myTeam, oob.Team, "we lost it");
            Assert.AreEqual(1 - myTeam, m.OffenseTeam, "their ball");
            Assert.IsTrue(m.Ball.IsHeld);
            Assert.AreEqual(1 - myTeam, m.Players[m.Ball.HolderIndex].Team);
            Assert.AreEqual("OUT OF BOUNDS!", MatchSimulation.ViolationName(ViolationKind.OutOfBounds));
        }

        [Test]
        public void OtherModes_KeepTheArcadeWalls()
        {
            var m = OneOnOne(mode: GameMode.QuickCall);
            Assert.IsFalse(m.Setup.OneOnOne);
            m.KnockLoose(new Vec2(9f, 0f), 0.5f);
            var log = RunUntil(m, () => false, 2f);
            Assert.IsFalse(log.Exists(e => e.Type == MatchEventType.Violation && e.Value == (int)ViolationKind.OutOfBounds));
            Assert.IsFalse(m.IsOutOfBounds(m.Ball.Position), "bounced back in off the line");
        }

        [Test]
        public void OutOfBoundsLine_HasAMargin()
        {
            var m = OneOnOne();
            var c = m.Setup.Court;
            Assert.IsFalse(m.IsOutOfBounds(new Vec2(c.HalfWidth, 3f)), "on the line is in");
            Assert.IsTrue(m.IsOutOfBounds(new Vec2(c.HalfWidth + 0.5f, 3f)));
            Assert.IsTrue(m.IsOutOfBounds(new Vec2(0f, c.depth + 0.5f)));
            Assert.IsTrue(m.IsOutOfBounds(new Vec2(0f, -0.5f)));
        }
    }
}
