using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>
    /// 15 Oct 2026: THE STREETS phase 4 (CROSSOVER and the double cross, the SHOOT double-tap step-back, the gather and
    /// landing on street dunks, the rim pulse, the ANKLE BREAKER stamp and the play-by-play booth).
    /// </summary>
    public class StreetsPhase4Tests
    {
        private const float Dt = 1f / 60f;
        private static readonly Vec2 Up = new Vec2(0f, -1f);

        private static SkillMoveKind Feed(StickGestures g, ref float t, Vec2 stick, int frames, List<SkillMoveKind> seen)
        {
            var last = SkillMoveKind.None;
            for (int i = 0; i < frames; i++)
            {
                t += Dt;
                var k = g.Feed(t, stick, Up, 2f);
                if (k != SkillMoveKind.None) { seen?.Add(k); last = k; }
            }
            return last;
        }

        [Test]
        public void Crossover_FiresOnceOnTheSnap_NotOnADriftOrFromStandingStill()
        {
            var g = new StickGestures();
            float t = 0f;
            var seen = new List<SkillMoveKind>();
            Feed(g, ref t, new Vec2(-1f, 0f), 6, seen);
            Feed(g, ref t, new Vec2(1f, 0f), 20, seen);
            CollectionAssertCount(seen, SkillMoveKind.Crossover, 1);

            // From the middle to one side: nothing to cross from.
            g.Clear(); seen.Clear();
            Feed(g, ref t, Vec2.Zero, 10, seen);
            Feed(g, ref t, new Vec2(1f, 0f), 10, seen);
            Assert.AreEqual(0, seen.Count);

            // A slow drift across (over half a second through the middle) isn't a snap.
            g.Clear(); seen.Clear();
            Feed(g, ref t, new Vec2(-1f, 0f), 6, seen);
            Feed(g, ref t, Vec2.Zero, 30, seen);
            Feed(g, ref t, new Vec2(1f, 0f), 10, seen);
            Assert.AreEqual(0, seen.Count);

            // Up and down the line to the rim isn't a crossover either.
            g.Clear(); seen.Clear();
            Feed(g, ref t, new Vec2(0f, -1f), 6, seen);
            Feed(g, ref t, new Vec2(0f, 1f), 6, seen);
            Assert.IsFalse(seen.Contains(SkillMoveKind.Crossover));
        }

        [Test]
        public void Crossover_ThenBackAgain_IsTheShake()
        {
            var g = new StickGestures();
            float t = 0f;
            var seen = new List<SkillMoveKind>();
            Feed(g, ref t, new Vec2(-1f, 0f), 5, seen);
            Feed(g, ref t, new Vec2(1f, 0f), 5, seen);
            Feed(g, ref t, new Vec2(-1f, 0f), 5, seen);
            Assert.AreEqual(2, seen.Count);
            Assert.AreEqual(SkillMoveKind.Crossover, seen[0]);
            Assert.AreEqual(SkillMoveKind.Shake, seen[1]);
        }

        private static MatchSimulation Live(MatchRequest r)
        {
            var m = new MatchSimulation(MatchSetup.FromRequest(r, DefaultContent.Create()));
            for (int i = 0; i < 240 && m.Phase != MatchPhase.Live; i++) m.Step(Dt, new PlayerInput { Move = new Vec2(0f, -0.1f) });
            return m;
        }

        private static MatchSimulation StreetsGame(uint seed)
        {
            var r = Streets.Pickup(DefaultContent.Create(), new CareerSaveData(), null, null);
            r.Seed = seed;
            return Live(r);
        }

        /// <summary>The closest opponent goes right in front of the human (towards the rim); the others go far away.</summary>
        private static int DefenderInFace(MatchSimulation m)
        {
            var me = m.Players[m.ControlledIndex];
            int d = -1;
            foreach (var p in m.Players)
            {
                if (p.Team == me.Team) continue;
                if (d < 0) { d = p.Index; continue; }
                p.Motion.position = new Vec2(p.Position.x < 0f ? -6.5f : 6.5f, 9.5f);
            }
            var toHoop = (m.Setup.Court.Hoop - me.Position).Normalized;
            m.Players[d].Motion.position = me.Position + toHoop * 1.0f;
            return d;
        }

        [Test]
        public void Crossover_InTheStreets_CanBreakAnkles_OnlyWithADefenderOnYou()
        {
            int ankles = 0, beaten = 0, moves = 0;
            for (uint seed = 1; seed <= 40; seed++)
            {
                var m = StreetsGame(seed);
                if (!m.HumanHasBall) { moves++; continue; }
                int me = m.ControlledIndex;
                DefenderInFace(m);
                m.DoSkillMove(me, SkillMoveKind.Crossover);
                foreach (var e in m.Events)
                {
                    if (e.Type == MatchEventType.AnkleBreaker) ankles++;
                    if (e.Type == MatchEventType.DefenderBeaten) beaten++;
                    if (e.Type == MatchEventType.SkillMove && e.Value == (int)SkillMoveKind.Crossover) moves++;
                }
            }
            Assert.Greater(moves, 30, "a crossover whenever a defender is on him (a benched or stunned one doesn't count)");
            Assert.Greater(ankles, 0, "a big cross breaks ankles in the streets");
            Assert.Greater(beaten, 0, "and sometimes just leaves him reaching");
            Assert.Less(ankles + beaten, 40, "good defenders stay with it");

            // Nobody close: no crossover.
            var lone = StreetsGame(3);
            foreach (var p in lone.Players)
                if (p.Team != lone.Players[lone.ControlledIndex].Team) p.Motion.position = new Vec2(6.5f, 9.5f);
            lone.DoSkillMove(lone.ControlledIndex, SkillMoveKind.Crossover);
            Assert.AreEqual(SkillMoveKind.None, lone.SkillMoveOf(lone.ControlledIndex));
        }

        [Test]
        public void Crossover_OutsideStreetRules_NeverBreaksAnkles()
        {
            var c = DefaultContent.Create();
            for (uint seed = 1; seed <= 30; seed++)
            {
                var m = Live(new MatchRequest { Mode = GameMode.OneOnOne, HomeTeamId = "team.metro_comets", AwayTeamId = "team.harbor_hounds", RulesId = "rules.oneonone", Seed = seed });
                if (m.Setup.StreetRules) Assert.Inconclusive("one-on-one has street rules");
                DefenderInFace(m);
                m.DoSkillMove(m.ControlledIndex, SkillMoveKind.Crossover);
                foreach (var e in m.Events) Assert.AreNotEqual(MatchEventType.AnkleBreaker, e.Type);
            }
        }

        [Test]
        public void ShootDoubleTap_StepsBack_ThenRisesIntoTheJumperWhileHeld()
        {
            var m = StreetsGame(5);
            int me = m.ControlledIndex;
            DefenderInFace(m);
            var hoop = m.Setup.Court.Hoop;
            float before = Vec2.Distance(m.Players[me].Position, hoop);
            var log = new List<MatchEvent>();

            // Tap: a pump fake.
            m.Step(Dt, new PlayerInput { ShootPressed = true, ShootHeld = true });
            m.Step(Dt, new PlayerInput { ShootHeld = false });
            Assert.AreEqual(1, m.PumpFakesThisPossession);
            Assert.AreEqual(-1, m.ChargingIndex);
            // Press again quickly and hold: the step-back.
            m.Step(Dt, new PlayerInput { ShootPressed = true, ShootHeld = true });
            log.AddRange(m.Events);
            Assert.IsTrue(log.Exists(e => e.Type == MatchEventType.SkillMove && e.Value == (int)SkillMoveKind.StepBack && e.PlayerIndex == me));
            Assert.AreEqual(-1, m.ChargingIndex, "he hops back first");
            for (int i = 0; i < 30 && m.ChargingIndex < 0; i++) m.Step(Dt, new PlayerInput { ShootHeld = true });
            Assert.AreEqual(me, m.ChargingIndex, "then goes up for the jumper while SHOOT is held");
            Assert.Greater(Vec2.Distance(m.Players[me].Position, hoop), before + 0.3f, "with room: further from the rim");
            // Let go: the shot.
            for (int i = 0; i < 40 && m.Ball.IsHeld; i++) m.Step(Dt, new PlayerInput { ShootHeld = i < 20 });
            Assert.IsFalse(m.Ball.IsHeld, "released the jumper");
        }

        [Test]
        public void ShootDoubleTap_WorksWithNobodyClose_ButASlowSecondPressIsANormalShot()
        {
            var m = StreetsGame(6);
            int me = m.ControlledIndex;
            foreach (var p in m.Players)
                if (p.Team != m.Players[me].Team) p.Motion.position = new Vec2(p.Position.x < 0f ? -6.5f : 6.5f, 9.5f);
            m.Step(Dt, new PlayerInput { ShootPressed = true, ShootHeld = true });
            m.Step(Dt, new PlayerInput { ShootHeld = false });
            m.Step(Dt, new PlayerInput { ShootPressed = true, ShootHeld = false });
            Assert.AreEqual(SkillMoveKind.StepBack, m.SkillMoveOf(me), "the double tap is a step-back even in space");
            for (int i = 0; i < 30; i++) m.Step(Dt, default);
            Assert.AreEqual(-1, m.ChargingIndex, "let go: just the step-back, no shot");

            var n = StreetsGame(7);
            int k = n.ControlledIndex;
            n.Step(Dt, new PlayerInput { ShootPressed = true, ShootHeld = true });
            n.Step(Dt, new PlayerInput { ShootHeld = false });
            for (int i = 0; i < 40; i++) n.Step(Dt, default); // well past the window
            n.Step(Dt, new PlayerInput { ShootPressed = true, ShootHeld = true });
            Assert.AreEqual(k, n.ChargingIndex, "a slow second press is just a shot");
        }

        [Test]
        public void StreetDunk_GathersThenExplodes_AndLands()
        {
            var start = new Vec2(0.5f, 4f);
            var hoop = new Vec2(0f, 1.6f);
            const int lift = 20;
            var g0 = DunkPath.At(start, hoop, start, 0.02f, 0.6f, 0.2f, lift, DunkPath.Approach, DunkPath.StreetGather);
            Assert.IsTrue(g0.Gather);
            Assert.AreEqual(0, g0.LiftPx, "feet on the floor in the gather");
            Assert.Less(Vec2.Distance(g0.Ground, start), 0.2f);
            Assert.IsTrue(g0.BallInHands);

            // After the gather he rises fast: by halfway up the rise he's well past half height.
            float slam = 0.6f, uMid = slam * (DunkPath.StreetGather + (1f - DunkPath.StreetGather) * 0.5f);
            var mid = DunkPath.At(start, hoop, start, uMid, slam, 0.2f, lift, DunkPath.Approach, DunkPath.StreetGather);
            Assert.IsFalse(mid.Gather);
            Assert.Greater(mid.LiftPx, lift * 0.6f);
            var top = DunkPath.At(start, hoop, start, slam - 0.001f, slam, 0.2f, lift, DunkPath.Approach, DunkPath.StreetGather);
            Assert.GreaterOrEqual(top.LiftPx, lift - 1);

            // Rising never goes down.
            int last = -1;
            for (float u = 0f; u < slam; u += 0.01f)
            {
                var f = DunkPath.At(start, hoop, start, u, slam, 0.2f, lift, DunkPath.Approach, DunkPath.StreetGather);
                Assert.GreaterOrEqual(f.LiftPx, last);
                last = f.LiftPx;
            }
            var land = DunkPath.At(start, hoop, start, 0.99f, slam, 0.2f, lift, DunkPath.Approach, DunkPath.StreetGather);
            Assert.IsTrue(land.Landing);

            // Other modes are unchanged.
            var plain = DunkPath.At(start, hoop, start, 0.02f, slam, 0.2f, lift, DunkPath.Approach);
            Assert.IsFalse(plain.Gather);
            Assert.IsFalse(DunkPath.At(start, hoop, start, 0.99f, slam, 0.2f, lift, DunkPath.Approach).Landing);
        }

        [Test]
        public void RimPulse_GrowsAndFades_AsAFlatRing()
        {
            int prev = 0;
            for (int f = 0; f < RimPulseShape.Frames; f++)
            {
                int r = RimPulseShape.RadiusAt(f, 26);
                Assert.GreaterOrEqual(r, prev);
                prev = r;
            }
            Assert.Greater(RimPulseShape.RadiusAt(5, 26), RimPulseShape.RadiusAt(1, 26) + 8, "it grows a lot");
            Assert.AreEqual(26, RimPulseShape.RadiusAt(RimPulseShape.Frames - 1, 26));
            Assert.AreEqual(0, RimPulseShape.FrameAt(0f));
            Assert.AreEqual(-1, RimPulseShape.FrameAt(RimPulseShape.Life));
            Assert.Greater(RimPulseShape.AlphaAt(0.05f), RimPulseShape.AlphaAt(0.3f));
            Assert.AreEqual(0f, RimPulseShape.AlphaAt(1f));

            var ring = RimPulseShape.Ring(12, 2);
            int w = ring.GetLength(0), h = ring.GetLength(1);
            Assert.AreEqual(25, w);
            Assert.Less(h, w, "flatter than wide (the rim at the 3/4 angle)");
            Assert.IsFalse(ring[12, h / 2], "hollow in the middle");
            Assert.IsTrue(ring[0, h / 2] && ring[w - 1, h / 2], "the sides are on the ring");
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    Assert.AreEqual(ring[x, y], ring[w - 1 - x, y], "symmetric");
        }

        [Test]
        public void AnkleStamp_SlamsShakesAndWhipsOff()
        {
            float total = 1.5f;
            Assert.AreEqual(0f, AnkleStampLook.At(0.02f, total, false).WordScale, "the band comes first");
            Assert.Greater(AnkleStampLook.At(AnkleStampLook.SlamStart + 0.01f, total, false).WordScale, 2f, "slams in from huge");
            Assert.Less(AnkleStampLook.At(AnkleStampLook.SlamEnd + 0.01f, total, false).WordScale, 1f, "overshoots");
            Assert.AreEqual(1f, AnkleStampLook.At(0.6f, total, false).WordScale);
            bool shook = false;
            for (float t = AnkleStampLook.SlamEnd; t < AnkleStampLook.ShakeEnd; t += 0.01f)
                if (AnkleStampLook.At(t, total, false).ShakeX != 0f) shook = true;
            Assert.IsTrue(shook);
            Assert.AreEqual(0f, AnkleStampLook.At(0.8f, total, false).ShakeX);
            Assert.AreEqual(0f, AnkleStampLook.At(0.8f, total, false).SubX, "the victim line is in");
            var leaving = AnkleStampLook.At(1.45f, total, false);
            Assert.Greater(leaving.ExitY, 0f);
            Assert.Less(leaving.Alpha, 0.3f);
            Assert.AreEqual(0f, AnkleStampLook.At(total, total, false).Alpha);

            var calm = AnkleStampLook.At(AnkleStampLook.SlamStart + 0.01f, total, true);
            Assert.AreEqual(1f, calm.WordScale);
            Assert.AreEqual(0f, calm.ShakeX);
            Assert.AreEqual(0f, calm.SubX);
        }

        [Test]
        public void Booth_ReadsLinesLikeSpeech()
        {
            Assert.AreEqual("Oh my! Ankle breaker! Jones sent park to the floor!", Announcer.ForSpeech("OH MY! ANKLE BREAKER! JONES SENT PARK TO THE FLOOR!"));
            Assert.AreEqual("Comets on a 6 to 0 run!", Announcer.ForSpeech("COMETS ON A 6-0 RUN!"));
            Assert.AreEqual("A, b", Announcer.ForSpeech("A · B"));
            Assert.Greater(Announcer.HypeRate, Announcer.CallRate);
            Assert.Greater(Announcer.HypePitch, Announcer.CallPitch);
        }

        [Test]
        public void Booth_CallsAnkleBreakersInTheStreets_WithWhoWentDown()
        {
            for (uint seed = 1; seed <= 60; seed++)
            {
                var m = StreetsGame(seed);
                if (!m.HumanHasBall) continue;
                int d = DefenderInFace(m);
                m.DoSkillMove(m.ControlledIndex, SkillMoveKind.Crossover);
                if (!m.Events.Exists(e => e.Type == MatchEventType.AnkleBreaker)) continue;
                var mic = new Commentary("A", "B", 4);
                string line = mic.React(m, k => m.Players[k].Def.lastName.Length > 0 ? m.Players[k].Def.lastName : m.Players[k].Def.firstName, ShotType.Layup);
                Assert.IsNotNull(line);
                var def = m.Players[d].Def;
                string victim = (def.lastName.Length > 0 ? def.lastName : def.firstName).ToUpperInvariant();
                Assert.IsTrue(line.Contains(victim), line);
                Assert.AreEqual(6, mic.LastPriority, "the biggest call");
                return;
            }
            Assert.Fail("no ankle breaker in 60 crossovers");
        }

        [Test]
        public void PlayByPlayVoice_IsOnByDefault_AndSaves()
        {
            var c = DefaultContent.Create();
            var d = new CareerSaveData();
            Assert.IsTrue(d.settings.playByPlayVoice);
            d.settings.playByPlayVoice = false;
            Assert.IsFalse(SaveCodec.Decode(SaveCodec.Encode(d), c, out _).settings.playByPlayVoice);
        }

        [Test]
        public void HowToPlay_TeachesTheNewMoves()
        {
            Assert.IsTrue(System.Array.Exists(Streets.HowTo, l => l.Contains("CROSSOVER")));
            Assert.IsTrue(System.Array.Exists(Streets.HowTo, l => l.Contains("STEP-BACK") && l.Contains("SHOOT")));
            Assert.AreEqual("CROSSOVER", SkillMoveInfo.Name(SkillMoveKind.Crossover));
            Assert.IsFalse(SkillMoveInfo.IsSecret(SkillMoveKind.Crossover));
        }

        private static void CollectionAssertCount(List<SkillMoveKind> list, SkillMoveKind k, int n)
        {
            int c = 0;
            foreach (var x in list) if (x == k) c++;
            Assert.AreEqual(n, c);
        }
    }
}
