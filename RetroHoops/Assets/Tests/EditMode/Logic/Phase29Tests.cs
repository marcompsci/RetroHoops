using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Gameplay depth: the BACKDOOR and POST UP plays, and smart AI doubling a heated-up scorer.</summary>
    public class NewPlaysTests
    {
        private const float Dt = 1f / 60f;

        private static MatchSimulation Live(uint seed, string difficulty = null, bool heated = false, bool passive = true)
        {
            var c = DefaultContent.Create();
            var r = MatchRequest.QuickCallDefault(c);
            r.Seed = seed;
            if (difficulty != null) r.DifficultyId = difficulty;
            r.StartHeated = heated;
            var setup = MatchSetup.FromRequest(r, c);
            setup.PassiveOpponents = passive;
            var m = new MatchSimulation(setup);
            for (int i = 0; i < 240 && m.Phase != MatchPhase.Live; i++) m.Step(Dt, default);
            Assert.AreEqual(MatchPhase.Live, m.Phase);
            return m;
        }

        [Test]
        public void Backdoor_FakesHighThenCutsToTheRim()
        {
            var m = Live(7);
            m.Step(Dt, new PlayerInput { CallPlay = PlayCall.Backdoor });
            Assert.AreEqual(PlayCall.Backdoor, m.ActivePlay);
            int cutter = m.PlayMate;
            Assert.GreaterOrEqual(cutter, 0);
            Assert.AreNotEqual(m.ControlledIndex, cutter);
            var hoop = m.Setup.Court.Hoop;
            float farthest = 0f;
            for (int i = 0; i < (int)(MatchSimulation.BackdoorFakeSeconds / Dt); i++)
            {
                m.Step(Dt, default);
                farthest = System.Math.Max(farthest, Vec2.Distance(m.Players[cutter].Position, hoop));
            }
            float closest = 99f;
            for (int i = 0; i < 150 && m.ActivePlay == PlayCall.Backdoor; i++)
            {
                m.Step(Dt, default);
                closest = System.Math.Min(closest, Vec2.Distance(m.Players[cutter].Position, hoop));
            }
            Assert.Greater(farthest, 3.5f, "the fake takes them away from the rim");
            Assert.Less(closest, 2.2f, "then they cut to the rim");
        }

        [Test]
        public void PostUp_TheBigSealsOnTheBlock_OthersClear()
        {
            var m = Live(9);
            m.Step(Dt, new PlayerInput { CallPlay = PlayCall.PostUp });
            Assert.AreEqual(PlayCall.PostUp, m.ActivePlay);
            int big = m.PlayMate;
            Assert.GreaterOrEqual(big, 0);
            // The best finisher + rebounder on the team (not the ball handler) is the post man.
            int team = m.Players[big].Team;
            for (int i = 0; i < m.Players.Length; i++)
                if (m.Players[i].Team == team && i != big && i != m.ControlledIndex)
                    Assert.GreaterOrEqual(m.Players[big].Def.attributes.finishing + m.Players[big].Def.attributes.rebounding,
                                          m.Players[i].Def.attributes.finishing + m.Players[i].Def.attributes.rebounding);
            for (int i = 0; i < 180; i++) m.Step(Dt, default);
            var p = m.Players[big].Position;
            Assert.Less(System.Math.Abs(p.y - (m.Setup.Court.hoopY + 1.3f)), 0.9f, "on the low block");
            Assert.Less(System.Math.Abs(System.Math.Abs(p.x) - 1.7f), 0.9f);
        }

        [Test]
        public void PlayCalls_TravelBetweenPhones()
        {
            foreach (var play in new[] { PlayCall.Backdoor, PlayCall.PostUp })
                Assert.AreEqual(play, LinkProtocol.Quantize(new PlayerInput { CallPlay = play }).CallPlay);
        }

        private static int TrapsInDrive(string difficulty, bool heated, out float closestHelp)
        {
            var m = Live(21, difficulty, heated, passive: false);
            int traps = 0;
            closestHelp = 99f;
            var hoop = m.Setup.Court.Hoop;
            for (int i = 0; i < 240; i++)
            {
                // Dribble toward the elbow and hang there with the ball.
                var me = m.Controlled.Position;
                var target = new Vec2(1.5f, hoop.y + 3.2f);
                var d = target - me;
                var move = d.Magnitude > 0.2f ? d.Normalized : Vec2.Zero;
                m.Step(Dt, new PlayerInput { Move = move });
                foreach (var e in m.Events) if (e.Type == MatchEventType.Trap) traps++;
                if (!m.HumanHasBall) break;
                int onBall = -1;
                for (int k = 0; k < m.Players.Length; k++) if (m.Players[k].Team != 0 && m.GuardingOf(k) == m.ControlledIndex) onBall = k;
                for (int k = 0; k < m.Players.Length; k++)
                    if (m.Players[k].Team != 0 && k != onBall)
                        closestHelp = System.Math.Min(closestHelp, Vec2.Distance(m.Players[k].Position, m.Controlled.Position));
            }
            return traps;
        }

        [Test]
        public void SmartDefences_DoubleTheHotHand_RookiesDont()
        {
            int legend = TrapsInDrive("difficulty.legend", true, out float legendHelp);
            Assert.Greater(legend, 0, "Legend doubles a heated-up scorer");
            Assert.Less(legendHelp, 1.6f, "a second defender actually arrives");
            Assert.AreEqual(0, TrapsInDrive("difficulty.rookie", true, out _), "Rookie AI doesn't");
            Assert.AreEqual(0, TrapsInDrive("difficulty.legend", false, out _), "no trap for a cold player");
        }
    }

    public class HalfCourtBenchTests
    {
        [Test]
        public void TiredAiPlayers_SubOut_ForTheTeamsExtraPlayer()
        {
            var c = DefaultContent.Create();
            var career = Career.New(c);
            var r = Street.Challenge(c, career, Street.Find("glide")); // 2-on-2, Glide's crew has 4 players
            r.Seed = 3;
            var m = new MatchSimulation(MatchSetup.FromRequest(r, c));
            int theirs = 0;
            foreach (var b in m.Bench) if (b.Team == 1) theirs++;
            Assert.AreEqual(1, theirs, "one of their extras waits on the bench");
            int tired = m.Index(1, 1);
            var before = m.Players[tired].Def;
            m.Players[tired].Stamina = 0.2f;
            m.Step(1f / 60f, default);
            m.CheckBall(0);
            bool subbed = false;
            foreach (var e in m.Events) if (e.Type == MatchEventType.Substitution && e.PlayerIndex == tired) subbed = true;
            Assert.IsTrue(subbed);
            Assert.IsFalse(ReferenceEquals(before, m.Players[tired].Def));
            Assert.Greater(m.Players[tired].Stamina, 0.5f, "fresh legs");
        }

        [Test]
        public void NoBench_InOneOnOne_OrForTeamsWithoutExtras()
        {
            var c = DefaultContent.Create();
            var one = Street.Challenge(c, Career.New(c), Street.Find("slim"));
            Assert.AreEqual(0, new MatchSimulation(MatchSetup.FromRequest(one, c)).Bench.Count);
            var practice = MatchRequest.PracticeDefault();
            Assert.AreEqual(0, new MatchSimulation(MatchSetup.FromRequest(practice, c)).Bench.Count);
        }
    }

    public class DifficultyCurveTests
    {
        /// <summary>
        /// AI-vs-idle simulations (16 games each, Phase 29 report; the check itself runs 16 seeds since 1 Nov 2026 so one
        /// lucky seed can't flip it): opponent FG% 34 / 39 / 51 and average margin
        /// +5.7 / +7.6 / +14.3 for Rookie / Caller / Legend. This guards that the curve keeps climbing.
        /// </summary>
        [Test]
        public void HarderDifficulties_ShootBetter_AndWinBigger()
        {
            var c = DefaultContent.Create();
            float prevFg = -1f, prevMargin = -99f;
            foreach (var d in new[] { "difficulty.rookie", "difficulty.caller", "difficulty.legend" })
            {
                int margin = 0, made = 0, att = 0;
                for (uint s = 1; s <= 16; s++)
                {
                    var r = MatchRequest.QuickCallDefault(c);
                    r.Seed = s * 13;
                    r.DifficultyId = d;
                    var m = new MatchSimulation(MatchSetup.FromRequest(r, c));
                    int g = 0;
                    while (!m.IsOver && g++ < 60 * 600) m.Step(1f / 60f, default);
                    margin += m.Score[1] - m.Score[0];
                    for (int i = 0; i < m.Players.Length; i++)
                        if (m.Players[i].Team == 1) { made += m.Stats.players[i].fieldGoalsMade; att += m.Stats.players[i].fieldGoalsAttempted; }
                }
                float fg = made / (float)System.Math.Max(1, att);
                float avg = margin / 16f;
                Assert.Greater(fg, prevFg, d + " shoots better than the level below");
                Assert.Greater(avg, prevMargin, d + " wins by more than the level below");
                prevFg = fg;
                prevMargin = avg;
            }
        }
    }

    public class ArenaSoundTests
    {
        [Test]
        public void NewCrowdSounds_AreRealAudio()
        {
            foreach (var id in new[] { SfxId.CrowdOooh, SfxId.ArenaHorn, SfxId.ClapChant })
            {
                var pcm = AudioSynth.Sfx(id);
                Assert.Greater(pcm.Length, AudioSynth.SampleRate / 2, id + " lasts a while");
                float peak = 0f;
                double energy = 0;
                foreach (var v in pcm)
                {
                    Assert.IsFalse(float.IsNaN(v) || float.IsInfinity(v), id + " finite");
                    peak = System.Math.Max(peak, System.Math.Abs(v));
                    energy += v * v;
                }
                Assert.LessOrEqual(peak, 1f, id + " no clipping");
                Assert.Greater(peak, 0.2f, id + " audible");
                Assert.Greater(energy / pcm.Length, 1e-4, id + " not silent");
            }
            // Claps are separate hits: there are quiet gaps between them.
            var claps = AudioSynth.Sfx(SfxId.ClapChant);
            int quiet = 0;
            for (int i = 0; i < claps.Length; i += 441) if (System.Math.Abs(claps[i]) < 0.01f) quiet++;
            Assert.Greater(quiet, claps.Length / 441 / 3);
        }
    }
}

