using System;
using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>14 Oct 2026: stick skill moves (spin, jab, secret hesi / step-back / shake) and bank layups.</summary>
    public class SkillMoveTests
    {
        private const float Dt = 1f / 60f;
        private static readonly Vec2 Up = new Vec2(0f, -1f); // toward the rim (it sits at the top)

        // ------------------------------------------------------------------ gestures

        private static SkillMoveKind Run(StickGestures g, ref float t, IEnumerable<(Vec2 stick, float speed)> samples)
        {
            var found = SkillMoveKind.None;
            foreach (var s in samples)
            {
                t += Dt;
                var k = g.Feed(t, s.stick, Up, s.speed);
                if (k != SkillMoveKind.None) found = k;
            }
            return found;
        }

        private static IEnumerable<(Vec2, float)> Hold(Vec2 stick, int frames, float speed)
        {
            for (int i = 0; i < frames; i++) yield return (stick, speed);
        }

        private static IEnumerable<(Vec2, float)> Swirl(float fromDeg, float degrees, int frames, float speed = 3f)
        {
            for (int i = 0; i <= frames; i++)
            {
                double a = (fromDeg + degrees * i / frames) * Math.PI / 180.0;
                yield return (new Vec2((float)Math.Cos(a), (float)Math.Sin(a)), speed);
            }
        }

        [Test]
        public void Spin_IsAFastHalfCircle_ButNotASlowTurnOrAStraightPush()
        {
            var g = new StickGestures();
            float t = 0f;
            Assert.AreEqual(SkillMoveKind.Spin, Run(g, ref t, Swirl(0f, 230f, 20)));

            g.Clear();
            Assert.AreEqual(SkillMoveKind.None, Run(g, ref t, Swirl(0f, 230f, 80)), "too slow: just steering");
            g.Clear();
            Assert.AreEqual(SkillMoveKind.None, Run(g, ref t, Hold(Up, 60, 4f)));
        }

        [Test]
        public void Jab_IsAQuickFlickAtTheRimFromStandingStill()
        {
            var g = new StickGestures();
            float t = 0f;
            var seq = new List<(Vec2, float)>();
            seq.AddRange(Hold(Vec2.Zero, 5, 0f));
            seq.AddRange(Hold(Up, 7, 0.4f));
            seq.AddRange(Hold(Vec2.Zero, 2, 0.5f));
            Assert.AreEqual(SkillMoveKind.Jab, Run(g, ref t, seq));

            // Held too long: that's walking, not a jab.
            g.Clear();
            seq.Clear();
            seq.AddRange(Hold(Vec2.Zero, 5, 0f));
            seq.AddRange(Hold(Up, 30, 0.4f));
            seq.AddRange(Hold(Vec2.Zero, 2, 0.5f));
            Assert.AreEqual(SkillMoveKind.None, Run(g, ref t, seq));

            // Moving fast: not a jab either.
            g.Clear();
            seq.Clear();
            seq.AddRange(Hold(Vec2.Zero, 5, 4f));
            seq.AddRange(Hold(Up, 7, 4f));
            seq.AddRange(Hold(Vec2.Zero, 2, 4f));
            Assert.AreEqual(SkillMoveKind.None, Run(g, ref t, seq));
        }

        [Test]
        public void Hesi_IsABlinkOfLetGoAtSpeed_ThenTheSameWay()
        {
            var g = new StickGestures();
            float t = 0f;
            var dir = new Vec2(0.6f, -0.8f);
            var seq = new List<(Vec2, float)>();
            seq.AddRange(Hold(dir, 20, 4.5f));
            seq.AddRange(Hold(Vec2.Zero, 7, 3.5f));
            seq.AddRange(Hold(dir, 2, 2.5f));
            Assert.AreEqual(SkillMoveKind.Hesi, Run(g, ref t, seq));

            g.Clear();
            seq.Clear();
            seq.AddRange(Hold(dir, 20, 4.5f));
            seq.AddRange(Hold(Vec2.Zero, 40, 0.5f)); // a real stop
            seq.AddRange(Hold(dir, 2, 0.5f));
            Assert.AreEqual(SkillMoveKind.None, Run(g, ref t, seq));
        }

        [Test]
        public void StepBack_IsASnapStraightBackWhileGoingAtTheRim()
        {
            var g = new StickGestures();
            float t = 0f;
            var seq = new List<(Vec2, float)>();
            seq.AddRange(Hold(Up, 15, 3f));
            seq.AddRange(Hold(new Vec2(0f, 1f), 2, 2.5f));
            Assert.AreEqual(SkillMoveKind.StepBack, Run(g, ref t, seq));
        }

        [Test]
        public void Shake_IsLeftRightLeftFast()
        {
            var g = new StickGestures();
            float t = 0f;
            var seq = new List<(Vec2, float)>();
            seq.AddRange(Hold(new Vec2(-1f, 0f), 6, 2f));
            seq.AddRange(Hold(new Vec2(1f, 0f), 6, 2f));
            seq.AddRange(Hold(new Vec2(-1f, 0f), 6, 2f));
            Assert.AreEqual(SkillMoveKind.Shake, Run(g, ref t, seq));

            // One crossover is not a shake.
            g.Clear();
            seq.Clear();
            seq.AddRange(Hold(new Vec2(-1f, 0f), 6, 2f));
            seq.AddRange(Hold(new Vec2(1f, 0f), 20, 2f));
            Assert.AreEqual(SkillMoveKind.None, Run(g, ref t, seq));
        }

        // ------------------------------------------------------------------ in a game

        private static MatchSimulation OneOnOne(uint seed = 8)
        {
            var c = DefaultContent.Create();
            var r = new MatchRequest { Mode = GameMode.OneOnOne, HomeTeamId = "team.metro_comets", AwayTeamId = "team.harbor_hounds", RulesId = "rules.oneonone", Seed = seed };
            var setup = MatchSetup.FromRequest(r, c);
            var m = new MatchSimulation(setup);
            for (int i = 0; i < 120 && m.Phase != MatchPhase.Live; i++) m.Step(Dt, default);
            if (m.Phase != MatchPhase.Live) m.Step(Dt, new PlayerInput { Move = new Vec2(0f, -0.1f) });
            return m;
        }

        private static int Defender(MatchSimulation m)
        {
            foreach (var p in m.Players) if (p.Team != m.Players[m.ControlledIndex].Team) return p.Index;
            return -1;
        }

        [Test]
        public void SpinGesture_InAGame_FiresTheMove_AndBursts()
        {
            var m = OneOnOne();
            Assert.IsTrue(m.HumanHasBall);
            int me = m.ControlledIndex;
            var log = new List<MatchEvent>();
            foreach (var s in Swirl(-90f, 240f, 20))
            {
                m.Step(Dt, new PlayerInput { Move = s.Item1 });
                log.AddRange(m.Events);
            }
            Assert.IsTrue(log.Exists(e => e.Type == MatchEventType.SkillMove && e.PlayerIndex == me && e.Value == (int)SkillMoveKind.Spin), "spin");
        }

        [Test]
        public void Moves_BeatTheDefenderSometimes_AndWorkLessWhenRepeated()
        {
            var m = OneOnOne();
            int me = m.ControlledIndex, d = Defender(m);
            var hp = m.Players[me];
            var dp = m.Players[d];
            float first = MatchSimulation.BeatChance(hp, dp, SkillMoveKind.Spin, 0);
            float third = MatchSimulation.BeatChance(hp, dp, SkillMoveKind.Spin, 2);
            Assert.Greater(first, third);
            Assert.GreaterOrEqual(first, 0.06f);
            Assert.LessOrEqual(first, 0.8f);

            // Over many tries, some spins beat him (he's frozen) and some don't.
            int beaten = 0;
            for (uint seed = 1; seed <= 40; seed++)
            {
                var g = OneOnOne(seed);
                int gm = g.ControlledIndex, gd = Defender(g);
                g.Players[gd].Motion.position = g.Players[gm].Position + new Vec2(0f, -1.0f);
                g.DoSkillMove(gm, SkillMoveKind.Spin);
                bool beat = false;
                foreach (var e in g.Events) if (e.Type == MatchEventType.DefenderBeaten && e.Value == gd) beat = true;
                if (beat)
                {
                    beaten++;
                    Assert.Greater(g.Players[gd].StunnedUntil, g.Time, "spun past: he's frozen for a beat");
                }
            }
            Assert.GreaterOrEqual(beaten, 3, "a real chance");
            Assert.LessOrEqual(beaten, 37, "not a sure thing");
        }

        [Test]
        public void StepBack_NeedsADefender_AndGivesRoomOnTheJumper()
        {
            var m = OneOnOne();
            int me = m.ControlledIndex, d = Defender(m);
            var hoop = m.Setup.Court.Hoop;
            m.Players[me].Motion.position = hoop + new Vec2(0f, 4.0f);
            m.Players[d].Motion.position = m.Players[me].Position + new Vec2(4f, 4f); // far away
            m.DoSkillMove(me, SkillMoveKind.StepBack);
            Assert.AreEqual(SkillMoveKind.None, m.SkillMoveOf(me), "nobody to step back from");

            m.Players[d].Motion.position = m.Players[me].Position + new Vec2(0f, -0.9f);
            float before = m.ShotContextFor(me, ShotType.MidRange, 0.5f).NearestDefenderDistance;
            m.DoSkillMove(me, SkillMoveKind.StepBack);
            Assert.AreEqual(SkillMoveKind.StepBack, m.SkillMoveOf(me));
            float after = m.ShotContextFor(me, ShotType.MidRange, 0.5f).NearestDefenderDistance;
            Assert.Greater(after, before + 0.5f, "room for the jumper (or he lost you)");
            float y0 = m.Players[me].Position.y;
            for (int i = 0; i < 10; i++) m.Step(Dt, default);
            Assert.Greater(m.Players[me].Position.y, y0 + 0.4f, "hopped back away from the rim");
        }

        [Test]
        public void Shake_CanBreakAnklesOutsideStreetRules()
        {
            int ankles = 0;
            for (uint seed = 1; seed <= 40; seed++)
            {
                var g = OneOnOne(seed);
                int gm = g.ControlledIndex, gd = Defender(g);
                g.Players[gd].Motion.position = g.Players[gm].Position + new Vec2(0f, -1.0f);
                g.DoSkillMove(gm, SkillMoveKind.Shake);
                foreach (var e in g.Events) if (e.Type == MatchEventType.AnkleBreaker) ankles++;
            }
            Assert.Greater(ankles, 0);
            Assert.Less(ankles, 40);
        }

        // ------------------------------------------------------------------ bank layups

        [Test]
        public void BankAngle_IsTheSides_NotStraightOnOrTheBaseline()
        {
            var hoop = new Vec2(0f, 1.6f);
            Assert.IsTrue(MatchSimulation.IsBankAngle(hoop + new Vec2(1.2f, 1.2f), hoop), "45° off the baseline");
            Assert.IsTrue(MatchSimulation.IsBankAngle(hoop + new Vec2(-1.2f, 1.0f), hoop));
            Assert.IsFalse(MatchSimulation.IsBankAngle(hoop + new Vec2(0f, 1.8f), hoop), "straight on");
            Assert.IsFalse(MatchSimulation.IsBankAngle(hoop + new Vec2(1.8f, 0.2f), hoop), "along the baseline");
            Assert.IsFalse(MatchSimulation.IsBankAngle(hoop + new Vec2(0.3f, 0.3f), hoop), "right under it");
        }

        [Test]
        public void BankLayup_HitsTheGlass_ThenTheRim()
        {
            var m = OneOnOne();
            m.Setup.PassiveOpponents = true;
            int me = m.ControlledIndex;
            var hoop = m.Setup.Court.Hoop;
            m.Players[me].Motion.position = hoop + new Vec2(1.3f, 1.2f);
            m.Step(Dt, new PlayerInput { LayupPressed = true });
            var log = new List<MatchEvent>(m.Events);
            bool sawBank = false, kissed = false;
            Vec2 kissAt = default;
            for (int i = 0; i < 240; i++)
            {
                m.Step(Dt, default);
                log.AddRange(m.Events);
                if (m.Ball.Phase == BallPhase.Shot && m.Ball.ShotBank) sawBank = true;
                foreach (var e in m.Events) if (e.Type == MatchEventType.BankKiss) { kissed = true; kissAt = m.Ball.Position; }
                if (log.Exists(e => e.Type == MatchEventType.ShotMade || e.Type == MatchEventType.ShotMissed)) break;
            }
            Assert.IsTrue(sawBank, "a bank shot");
            Assert.IsTrue(kissed, "it kissed the glass");
            Assert.Less(kissAt.y, hoop.y, "the glass is behind the rim");
            Assert.Greater(kissAt.x, hoop.x, "on the shooter's side");
        }

        [Test]
        public void TheGlass_TakesSomeOfTheContestAway()
        {
            var t = ShotTuning.Default;
            var a = new AttributeSet { finishing = 70, shooting = 60 };
            var ctx = new ShotContext { Type = ShotType.Layup, Distance = 1.5f, Meter = t.greenCenter, Shooter = a, NearestDefenderDistance = 0.6f, NearestDefenderDefense = 70, Stamina01 = 1f };
            var plain = ShotModel.Evaluate(ctx, t);
            ctx.Bank = true;
            var bank = ShotModel.Evaluate(ctx, t);
            Assert.Greater(plain.ContestPenalty, 0f);
            Assert.Less(bank.ContestPenalty, plain.ContestPenalty);
            Assert.GreaterOrEqual(bank.MakeChance, plain.MakeChance);
        }

        // ------------------------------------------------------------------ secrets list and save

        [Test]
        public void SecretMoves_AreHiddenUntilFound_AndSaved()
        {
            var c = DefaultContent.Create();
            var d = new CareerSaveData();
            string list = SkillMoveInfo.ListText(d);
            StringAssert.Contains("SPIN MOVE", list);
            StringAssert.Contains("JAB STEP", list);
            StringAssert.DoesNotContain("HESI", list);
            StringAssert.Contains("???", list);

            Assert.IsFalse(SkillMoveInfo.Discover(d, SkillMoveKind.Spin), "spin isn't a secret");
            Assert.IsTrue(SkillMoveInfo.Discover(d, SkillMoveKind.Hesi), "first hesi: found!");
            Assert.IsFalse(SkillMoveInfo.Discover(d, SkillMoveKind.Hesi), "only once");
            StringAssert.Contains("HESI", SkillMoveInfo.ListText(d));

            var back = SaveCodec.Decode(SaveCodec.Encode(d), c, out _);
            Assert.IsTrue(SkillMoveInfo.Found(back, SkillMoveKind.Hesi));
            Assert.IsTrue(SkillMoveInfo.Found(back, SkillMoveKind.Spin));
            Assert.IsFalse(SkillMoveInfo.Found(back, SkillMoveKind.Shake));
        }

        // ------------------------------------------------------------------ AI handlers

        [Test]
        public void AiHandlers_PutMovesOnAPersonGuardingThem_ButNeverOnTheAi()
        {
            int aiMoves = 0;
            for (uint seed = 1; seed <= 3; seed++)
            {
                var m = OneOnOne(seed);
                int me = m.ControlledIndex;
                for (int i = 0; i < 60 * 90 && m.Phase != MatchPhase.Final; i++)
                {
                    var input = default(PlayerInput);
                    if (m.Ball.IsHeld && !m.HumanHasBall)
                    {
                        // Stay in front of the handler (between him and the rim), tight.
                        var h = m.Players[m.Ball.HolderIndex].Position;
                        var spot = h + (m.Setup.Court.Hoop - h).Normalized * 0.9f;
                        var to = spot - m.Players[me].Position;
                        if (to.SqrMagnitude > 0.01f) input.Move = to.Magnitude > 1f ? to.Normalized : to;
                    }
                    else if (m.HumanHasBall)
                        input.ShootPressed = i % 90 == 0; // take quick shots to give the ball back
                    input.ShootHeld = input.ShootPressed;
                    m.Step(Dt, input);
                    foreach (var e in m.Events)
                        if (e.Type == MatchEventType.SkillMove && e.PlayerIndex != me) aiMoves++;
                }
            }
            Assert.Greater(aiMoves, 0, "the AI used moves on a person");

            // AI against AI: no moves (so the difficulty tuning is the same as before).
            var c = DefaultContent.Create();
            var r = new MatchRequest { Mode = GameMode.Demo, HomeTeamId = "team.metro_comets", AwayTeamId = "team.harbor_hounds", Seed = 5 };
            var demo = new MatchSimulation(MatchSetup.FromRequest(r, c));
            for (int i = 0; i < 60 * 60 && demo.Phase != MatchPhase.Final; i++)
            {
                demo.Step(Dt, default);
                foreach (var e in demo.Events) Assert.AreNotEqual(MatchEventType.SkillMove, e.Type);
            }
        }
    }
}
