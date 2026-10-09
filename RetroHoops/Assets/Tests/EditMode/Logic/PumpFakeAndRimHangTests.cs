using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>9 Oct 2026: pump fakes (tap SHOOT) and swinging on the rim after a made dunk.</summary>
    public class PumpFakeAndRimHangTests
    {
        private const float Dt = 1f / 60f;

        private static MatchSimulation NewMatch(uint seed = 7, string difficulty = null)
        {
            var c = DefaultContent.Create(); // fresh content: the tests change ratings
            var req = MatchRequest.QuickCallDefault(c);
            req.Seed = seed;
            if (difficulty != null) req.DifficultyId = difficulty;
            return new MatchSimulation(MatchSetup.FromRequest(req, c));
        }

        private static List<MatchEvent> Tap(MatchSimulation m)
        {
            var log = new List<MatchEvent>();
            m.Step(Dt, new PlayerInput { ShootPressed = true, ShootHeld = true });
            log.AddRange(m.Events);
            m.Step(Dt, new PlayerInput { ShootHeld = false });
            log.AddRange(m.Events);
            return log;
        }

        /// <summary>One opponent right in front of the human (towards the hoop), the rest far away.</summary>
        private static int PutDefenderInFace(MatchSimulation m)
        {
            var me = m.Players[m.ControlledIndex];
            int d = -1;
            foreach (var p in m.Players)
            {
                if (p.Team == me.Team) continue;
                if (d < 0)
                {
                    d = p.Index;
                    var toHoop = (m.Setup.Court.Hoop - me.Position).Normalized;
                    p.Motion.position = me.Position + toHoop * 0.8f;
                }
                else p.Motion.position = me.Position + new Vec2(p.Index % 2 == 0 ? 6f : -6f, 4f);
            }
            return d;
        }

        [Test]
        public void QuickTap_IsAPumpFake_NotAShot()
        {
            var m = NewMatch();
            var log = Tap(m);
            Assert.IsTrue(log.Exists(e => e.Type == MatchEventType.PumpFake), "a pump fake");
            Assert.IsFalse(log.Exists(e => e.Type == MatchEventType.ShotReleased), "no shot");
            Assert.IsTrue(m.Ball.IsHeld);
            Assert.AreEqual(m.ControlledIndex, m.Ball.HolderIndex, "still your ball");
            Assert.AreEqual(-1, m.ChargingIndex);
            Assert.AreEqual(1, m.PumpFakesThisPossession);
            Assert.IsTrue(m.CanShoot(m.ControlledIndex), "you can shoot straight away");
        }

        [Test]
        public void HoldingSHOOT_StillShoots()
        {
            var m = NewMatch();
            var log = new List<MatchEvent>();
            m.Step(Dt, new PlayerInput { ShootPressed = true, ShootHeld = true });
            for (float t = 0f; t < 0.5f; t += Dt) { m.Step(Dt, new PlayerInput { ShootHeld = true }); log.AddRange(m.Events); }
            m.Step(Dt, new PlayerInput { ShootHeld = false });
            log.AddRange(m.Events);
            Assert.IsTrue(log.Exists(e => e.Type == MatchEventType.ShotReleased));
            Assert.IsFalse(log.Exists(e => e.Type == MatchEventType.PumpFake));
        }

        [Test]
        public void ADefenderWhoBites_IsOffBalance_NoContestNoBlock_CantMove()
        {
            var m = NewMatch();
            int d = PutDefenderInFace(m);
            m.Step(Dt, new PlayerInput { ShootPressed = true, ShootHeld = true });
            Assert.IsTrue(m.Jump(d) || m.IsJumping(d), "the defender leaves his feet at the fake");
            m.Step(Dt, new PlayerInput { ShootHeld = false });
            var fake = m.Events.Find(e => e.Type == MatchEventType.PumpFake);
            Assert.AreEqual(MatchEventType.PumpFake, fake.Type);
            Assert.AreEqual(d, fake.Value, "he bit");
            Assert.IsTrue(m.IsFaked(d));
            Assert.AreEqual(0f, m.ContestLevel(m.ControlledIndex), 1e-6f, "an open look while he's off balance");
            var at = m.Players[d].Position;
            for (int i = 0; i < 20; i++) m.Step(Dt, default);
            Assert.AreEqual(at.x, m.Players[d].Position.x, 1e-4f, "stuck while he recovers");
            Assert.AreEqual(at.y, m.Players[d].Position.y, 1e-4f);
            for (float t = 0f; t < m.Setup.Defense.fakeBiteRecovery; t += Dt) m.Step(Dt, default);
            Assert.IsFalse(m.IsFaked(d), "recovered");
        }

        [Test]
        public void BiteChance_GoodDefendersAndSmartAiBiteLess_RepeatsWorkLess()
        {
            var m = NewMatch();
            var me = m.Players[m.ControlledIndex];
            var them = m.Players[PutDefenderInFace(m)];
            them.Def.attributes.defense = 30;
            float weak = m.BiteChance(them, me);
            them.Def.attributes.defense = 95;
            float strong = m.BiteChance(them, me);
            Assert.Greater(weak, strong, "good defenders bite less");

            var rookie = NewMatch(difficulty: "difficulty.rookie");
            var legend = NewMatch(difficulty: "difficulty.legend");
            var a = rookie.Players[PutDefenderInFace(rookie)];
            var b = legend.Players[PutDefenderInFace(legend)];
            a.Def.attributes.defense = b.Def.attributes.defense = 60;
            Assert.Greater(rookie.BiteChance(a, rookie.Players[rookie.ControlledIndex]),
                           legend.BiteChance(b, legend.Players[legend.ControlledIndex]), "Legend AI bites less than Rookie AI");

            them.Def.attributes.defense = 60;
            Tap(m);
            float first = m.BiteChance(them, me);
            Tap(m);
            Assert.AreEqual(2, m.PumpFakesThisPossession);
            Assert.Less(m.BiteChance(them, me), first, "the second fake works less often");
        }

        [Test]
        public void PumpFake_IsDeterministic()
        {
            string Run()
            {
                var m = NewMatch(31);
                PutDefenderInFace(m);
                var sb = new System.Text.StringBuilder();
                for (int k = 0; k < 4; k++)
                {
                    foreach (var e in Tap(m)) if (e.Type == MatchEventType.PumpFake) sb.Append(e.Value).Append(',');
                    for (int i = 0; i < 30; i++) m.Step(Dt, default);
                }
                return sb.ToString();
            }
            Assert.AreEqual(Run(), Run());
        }

        [Test]
        public void MadeDunk_HangsOnTheRim()
        {
            bool sawOne = false;
            for (uint seed = 1; seed <= 12 && !sawOne; seed++)
            {
                var m = NewMatch(seed);
                m.Setup.PassiveOpponents = true;
                for (int i = 0; i < 120 && m.Phase != MatchPhase.Live; i++) m.Step(Dt, default);
                var me = m.Players[m.ControlledIndex];
                me.Def.attributes.finishing = 95;
                me.Motion.position = m.Setup.Court.Hoop + new Vec2(0.2f, 1.5f);
                for (int i = 0; i < 180 && !sawOne; i++)
                {
                    m.Step(Dt, m.Ball.Phase == BallPhase.Held ? new PlayerInput { DunkPressed = true } : default);
                    foreach (var e in m.Events)
                    {
                        if (e.Type != MatchEventType.ShotMade || m.Ball.ShotType != ShotType.Dunk) continue;
                        sawOne = true;
                        var dunker = m.Players[e.PlayerIndex];
                        Assert.GreaterOrEqual(dunker.StunnedUntil, m.Time + m.Setup.Flow.rimHangSeconds - 0.02f, "hangs on the rim");
                    }
                }
            }
            Assert.IsTrue(sawOne, "a made dunk in 12 tries");
        }

        [Test]
        public void DunkPath_SwingsOnTheRim_ThenDrops()
        {
            var start = new Vec2(0f, 4f);
            var hoop = new Vec2(0f, 1f);
            const float slamU = 0.3f, hangU = 0.45f;
            int maxSwing = 0, sign = 0, changes = 0;
            for (float u = slamU + 0.001f; u < slamU + hangU; u += 0.01f)
            {
                var f = DunkPath.At(start, hoop, start, u, slamU, hangU, 30, DunkPath.Approach);
                Assert.IsTrue(f.OnRim, "on the rim at " + u);
                Assert.IsFalse(f.BallInHands);
                Assert.LessOrEqual(System.Math.Abs(f.SwingPx), DunkPath.SwingAmplitudePx);
                Assert.GreaterOrEqual(f.RimDipPx, 1);
                maxSwing = System.Math.Max(maxSwing, System.Math.Abs(f.SwingPx));
                int s = System.Math.Sign(f.SwingPx);
                if (s != 0 && s != sign) { if (sign != 0) changes++; sign = s; }
            }
            Assert.AreEqual(DunkPath.SwingAmplitudePx, maxSwing, "a real swing");
            Assert.GreaterOrEqual(changes, 2, "back and forth");
            Assert.AreEqual(2, DunkPath.At(start, hoop, start, slamU + 0.01f, slamU, hangU, 30, DunkPath.Approach).RimDipPx, "slam pulls the rim down");
            var after = DunkPath.At(start, hoop, start, slamU + hangU + 0.05f, slamU, hangU, 30, DunkPath.Approach);
            Assert.IsFalse(after.OnRim, "drops");
            Assert.AreEqual(0, after.SwingPx);
            // The old short hang (a missed dunk) doesn't swing.
            var miss = DunkPath.At(start, hoop, start, slamU + 0.05f, slamU, 30, DunkPath.Approach);
            Assert.AreEqual(0, miss.SwingPx);
        }

        [Test]
        public void CoachTip_PumpFake_WhenTightlyGuarded()
        {
            var d = new CareerSaveData();
            d.settings.coachTips = true;
            foreach (var t in CoachTips.All) if (t.Id != "tip.fake") d.tipsSeen.Add(t.Id);
            float last = -99f;
            var tip = CoachTips.Next(d, new TipSituation { Live = true, HumanHasBall = true, TightlyGuarded = true, MatchTime = 20f }, 30f, ref last);
            Assert.IsNotNull(tip);
            Assert.AreEqual("tip.fake", tip.Id);
        }
    }
}
