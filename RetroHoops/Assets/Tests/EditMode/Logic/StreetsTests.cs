using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>15 Oct 2026: THE STREETS (rules, tricks, the STREET METER and the SHOWSTOPPER).</summary>
    public class StreetsTests
    {
        private const float Dt = 1f / 60f;

        private static MatchSimulation Game(uint seed, bool coach)
        {
            var c = DefaultContent.Create();
            var r = Streets.Pickup(c, new CareerSaveData(), null, null);
            r.Seed = seed;
            r.Coach = coach;
            return new MatchSimulation(MatchSetup.FromRequest(r, c));
        }

        private static MatchSimulation Live(uint seed)
        {
            var m = Game(seed, false);
            for (int i = 0; i < 240 && m.Phase != MatchPhase.Live; i++) m.Step(Dt, new PlayerInput { Move = new Vec2(0f, -0.1f) });
            return m;
        }

        [Test]
        public void Rules_AreFirstTo21_WinByTwo_NoClock_AndTheBallChangesHands()
        {
            var r = Streets.Rules();
            Assert.AreEqual(21, r.targetScore);
            Assert.IsTrue(r.winByTwo);
            Assert.IsFalse(r.useGameClock);
            Assert.AreEqual(1, r.insideArcPoints);
            Assert.AreEqual(2, r.beyondArcPoints);
            Assert.IsFalse(r.makeItTakeIt);
            Assert.AreEqual(GameOverReason.None, Scoring.Evaluate(21, 20, 0f, r), "win by two");
            Assert.AreEqual(GameOverReason.TargetScore, Scoring.Evaluate(22, 20, 0f, r));

            var c = DefaultContent.Create();
            var req = Streets.Pickup(c, new CareerSaveData(), null, null);
            Assert.AreEqual(GameMode.Streets, req.Mode);
            Assert.IsNotNull(c.Team(req.AwayTeamId));
            Assert.IsNotNull(c.Court(req.CourtId));
            var setup = MatchSetup.FromRequest(req, c);
            Assert.IsTrue(setup.StyleMeter);
            Assert.IsTrue(setup.StreetRules);
            Assert.IsFalse(setup.WinnersBall);
            Assert.AreEqual(3, setup.TeamSize);
            Assert.AreEqual(Streets.RulesId, setup.Rules.id);
            Assert.Greater(Streets.Opponents(c).Count, 12);
            Assert.AreEqual(Streets.Courts.Length, Streets.CourtList(c).Count, "every street court exists");
        }

        [Test]
        public void TheStick_PicksTheTrick()
        {
            var m = Live(3);
            Assert.IsTrue(m.HumanHasBall);
            int me = m.ControlledIndex;
            var toHoop = (m.Setup.Court.Hoop - m.Players[me].Position).Normalized;
            Assert.AreEqual(StreetTrick.Halo, m.TrickFor(me, Vec2.Zero));
            Assert.AreEqual(StreetTrick.ThroughLegs, m.TrickFor(me, toHoop * -1f));
            Assert.AreEqual(StreetTrick.BehindBack, m.TrickFor(me, new Vec2(-toHoop.y, toHoop.x)));
            var at = m.TrickFor(me, toHoop);
            Assert.IsTrue(at == StreetTrick.RollTheArm || at == StreetTrick.OffTheHead);
            Assert.AreEqual("OFF THE HEAD!", MatchSimulation.TrickName(StreetTrick.OffTheHead));
        }

        [Test]
        public void Tricks_BuildACombo_ThatGrows_AndRepeatsAreWorthLess()
        {
            var m = Live(4);
            int me = m.ControlledIndex;
            int team = m.Players[me].Team;
            Assert.IsTrue(m.DoTrick(me, StreetTrick.Halo));
            int first = m.ComboPoints[team];
            Assert.Greater(first, 0);
            Assert.IsFalse(m.DoTrick(me, StreetTrick.Halo), "cooldown");
            for (int i = 0; i < 40; i++) m.Step(Dt, default);
            if (!m.HumanHasBall) Assert.Inconclusive("lost the ball while waiting");
            Assert.IsTrue(m.DoTrick(me, StreetTrick.Halo));
            int second = m.ComboPoints[team] - first;
            Assert.Less(second, first * 1.25f + 1, "the same trick again: half value, even with the combo bonus");
            Assert.AreEqual(2, m.ComboCount[team]);
            Assert.AreEqual(2, m.Stats[me].tricks);

            // The TRICK button does it in a step.
            for (int i = 0; i < 40; i++) m.Step(Dt, default);
            if (!m.HumanHasBall) Assert.Inconclusive("lost the ball while waiting");
            var log = new List<MatchEvent>();
            m.Step(Dt, new PlayerInput { TrickPressed = true, Move = new Vec2(1f, 0f) });
            log.AddRange(m.Events);
            Assert.IsTrue(log.Exists(e => e.Type == MatchEventType.StreetTrick && e.PlayerIndex == me));
        }

        [Test]
        public void Showstopper_NeedsAFullMeter_AndIsCalledOnOffense()
        {
            var m = Live(5);
            int team = m.Players[m.ControlledIndex].Team;
            Assert.AreEqual(0, m.BreakerReady(team));
            Assert.IsFalse(m.CallBreaker(team));
            m.StyleMeter[team] = MatchSimulation.MeterLevel * 2 + 50;
            Assert.AreEqual(2, m.BreakerReady(team));
            Assert.IsFalse(m.CanCallBreaker(1 - team), "not the defense");
            m.Step(Dt, new PlayerInput { BreakerPressed = true });
            Assert.AreEqual(team, m.BreakerTeam);
            Assert.AreEqual(2, m.BreakerLevel);
            Assert.AreEqual(50, m.StyleMeter[team], "both levels used");
            Assert.IsFalse(m.CallBreaker(team), "one at a time");
        }

        [Test]
        public void FullGames_EndAt21ByTwo_WithTricksBankingAndShowstoppers()
        {
            int tricks = 0, banked = 0, breakersOn = 0, breakersScored = 0;
            for (uint seed = 1; seed <= 6; seed++)
            {
                var m = Game(seed, coach: true);
                int steps = 0;
                while (m.Phase != MatchPhase.Final && steps++ < 60 * 60 * 25)
                {
                    m.Step(Dt, default);
                    foreach (var e in m.Events)
                    {
                        if (e.Type == MatchEventType.StreetTrick) tricks++;
                        if (e.Type == MatchEventType.StyleBanked) banked++;
                        if (e.Type == MatchEventType.BreakerOn) breakersOn++;
                        if (e.Type == MatchEventType.BreakerScored) breakersScored++;
                    }
                    for (int t = 0; t < 2; t++)
                    {
                        Assert.GreaterOrEqual(m.StyleMeter[t], 0);
                        Assert.LessOrEqual(m.StyleMeter[t], MatchSimulation.MeterLevel * MatchSimulation.MaxBreakerLevel);
                        Assert.GreaterOrEqual(m.Score[t], 0);
                    }
                }
                Assert.AreEqual(MatchPhase.Final, m.Phase, "seed " + seed + " finished");
                int hi = System.Math.Max(m.Score[0], m.Score[1]), lo = System.Math.Min(m.Score[0], m.Score[1]);
                Assert.GreaterOrEqual(hi, 21, "seed " + seed);
                Assert.GreaterOrEqual(hi - lo, 2, "seed " + seed + " won by two");
            }
            Assert.Greater(tricks, 20, "the AI tricks");
            Assert.Greater(banked, 10, "buckets bank style");
            Assert.Greater(breakersOn, 0, "someone filled the meter and called it");
            Assert.Greater(breakersScored, 0, "and scored one");
        }

        [Test]
        public void TrickAndShowstopper_TravelOverTheLink()
        {
            var b = new byte[LinkProtocol.InputBytes];
            LinkProtocol.PackInput(new PlayerInput { TrickPressed = true, BreakerPressed = true, DunkPressed = true }, b, 0);
            var back = LinkProtocol.UnpackInput(b, 0);
            Assert.IsTrue(back.TrickPressed);
            Assert.IsTrue(back.BreakerPressed);
            Assert.IsTrue(back.DunkPressed);
            Assert.IsFalse(back.LayupPressed);
            var none = LinkProtocol.Quantize(new PlayerInput { ShootPressed = true });
            Assert.IsFalse(none.TrickPressed);
            Assert.IsFalse(none.BreakerPressed);
        }

        [Test]
        public void Record_SavesAndLoads()
        {
            var s = new StreetsSaveData();
            string line = Streets.ApplyResult(s, true, 2, 14, 1800);
            StringAssert.Contains("2 SHOWSTOPPERS", line);
            Streets.ApplyResult(s, false, 0, 3, 200);
            Assert.AreEqual(2, s.games);
            Assert.AreEqual(1, s.wins);
            Assert.AreEqual(1, s.losses);
            Assert.AreEqual(0, s.streak);
            Assert.AreEqual(1, s.bestStreak);
            Assert.AreEqual(1800, s.bestBank);

            var c = DefaultContent.Create();
            var d = new CareerSaveData { streets = s };
            s.lastOpponent = "team.st.slim";
            s.lastCourt = "court.pier_nine";
            var back = SaveCodec.Decode(SaveCodec.Encode(d), c, out _);
            Assert.AreEqual(2, back.streets.games);
            Assert.AreEqual(17, back.streets.tricks);
            Assert.AreEqual(2, back.streets.showstoppers);
            Assert.AreEqual("court.pier_nine", back.streets.lastCourt);
            Assert.AreEqual(0, SaveCodec.Decode(SaveCodec.Encode(new CareerSaveData()), c, out _).streets.games);
        }
    }
}
