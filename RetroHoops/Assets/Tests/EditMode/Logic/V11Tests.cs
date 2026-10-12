using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Call-for-the-ball coach tip (from the v1.1 list, brought into v1 on 13 Oct).</summary>
    public class V11Tests
    {
        [Test]
        public void CoachTip_CallForTheBall_WhenATeammateHasIt()
        {
            var d = new CareerSaveData();
            d.settings.coachTips = true;
            foreach (var t in CoachTips.All) if (t.Id != "tip.callball") d.tipsSeen.Add(t.Id);
            float last = -99f;
            Assert.IsNull(CoachTips.Next(d, new TipSituation { Live = true, TeammateHasBall = true, MatchTime = 5f }, 30f, ref last), "not in the first seconds");
            var tip = CoachTips.Next(d, new TipSituation { Live = true, TeammateHasBall = true, MatchTime = 20f }, 30f, ref last);
            Assert.IsNotNull(tip);
            Assert.AreEqual("tip.callball", tip.Id);
        }

        [Test]
        public void PassWithoutTheBall_CallsForIt()
        {
            var c = DefaultContent.Create();
            var req = MatchRequest.QuickCallDefault(c);
            var m = new MatchSimulation(MatchSetup.FromRequest(req, c));
            m.Setup.PassiveOpponents = true;
            for (int i = 0; i < 120 && m.Phase != MatchPhase.Live; i++) m.Step(1f / 60f, default);
            // Give the ball to an AI teammate, then tap PASS.
            int mate = -1;
            foreach (var p in m.Players) if (p.Team == m.Setup.HumanTeam && p.Index != m.ControlledIndex) { mate = p.Index; break; }
            Assert.IsTrue(m.HumanHasBall || m.HumanTeamHasBall);
            if (m.HumanHasBall) { m.Step(1f / 60f, new PlayerInput { PassPressed = true }); for (int i = 0; i < 90 && !(m.Ball.IsHeld && m.Ball.HolderIndex != m.ControlledIndex); i++) m.Step(1f / 60f, default); }
            Assert.IsTrue(m.HumanTeamHasBall && !m.HumanHasBall, "a teammate has it");
            m.Step(1f / 60f, new PlayerInput { PassPressed = true });
            for (int i = 0; i < 120 && !m.HumanHasBall; i++) m.Step(1f / 60f, default);
            Assert.IsTrue(m.HumanHasBall, "the teammate passed it back");
            Assert.GreaterOrEqual(mate, 0);
        }
    }
}
