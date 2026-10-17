using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>16 Oct 2026 improvement phases I12-I17.</summary>
    public class SixPhasesTests
    {
        [Test]
        public void I12_RecordWatch_CallsEachRecordOnce()
        {
            var r = new CareerRecords { points = 12, assists = 2, rebounds = 5, steals = 3 };
            var seen = new HashSet<string>();
            var s = new PlayerStatLine { points = 12, assists = 9, rebounds = 5 };
            Assert.AreEqual(0, RecordWatch.Check(r, s, seen, false).Count, "tying isn't breaking; a record under 3 stays quiet");
            s.points = 13;
            var calls = RecordWatch.Check(r, s, seen, false);
            Assert.AreEqual(1, calls.Count);
            StringAssert.Contains("NEW CAREER HIGH: 13 PTS", calls[0]);
            s.points = 15;
            Assert.AreEqual(0, RecordWatch.Check(r, s, seen, false).Count, "once per game");
            s.steals = 4;
            Assert.AreEqual(1, RecordWatch.Check(r, s, seen, false).Count);
            Assert.AreEqual(0, RecordWatch.Check(r, new PlayerStatLine { points = 30 }, new HashSet<string>(), true).Count, "Full Court points don't compare");
            Assert.IsFalse(RecordWatch.Applies(GameMode.Clutch));
            Assert.IsTrue(RecordWatch.Applies(GameMode.Rise));
        }

        private static MatchHistoryEntry G(string opp, int f, int a, int day = 0, int pts = 0) =>
            new MatchHistoryEntry { opponentId = opp, scoreFor = f, scoreAgainst = a, day = day, points = pts };

        [Test]
        public void I13_Nemesis()
        {
            var h = new List<MatchHistoryEntry> { G("x", 10, 21), G("y", 21, 3), G("x", 21, 19), G("x", 15, 21), G("z", 18, 21) };
            var n = Nemesis.Find(h);
            Assert.AreEqual("x", n.TeamId);
            Assert.AreEqual(2, n.Losses); Assert.AreEqual(1, n.Wins);
            StringAssert.Contains("NEMESIS", Nemesis.Line(n));
            Assert.IsFalse(Nemesis.Find(new List<MatchHistoryEntry> { G("x", 10, 21), G("x", 21, 3), G("x", 21, 3) }).Exists, "you're beating them");
            Assert.IsFalse(Nemesis.Find(new List<MatchHistoryEntry> { G("x", 10, 21) }).Exists, "one loss isn't a grudge");
            Assert.IsNull(Nemesis.Line(default));
            Assert.Greater(Nemesis.BonusSP, 0);
        }

        [Test]
        public void I14_ForgivingMeter()
        {
            var t = new ShotTuning();
            var f = ShotModel.Forgiving(t);
            Assert.AreEqual(t.greenHalfWidth * ShotModel.ForgivingGreen, f.greenHalfWidth, 1e-5f);
            Assert.Greater(ShotModel.GreenHalfWidth(70, f), ShotModel.GreenHalfWidth(70, t));
            Assert.AreEqual(new ShotTuning().greenHalfWidth, t.greenHalfWidth, "the original isn't touched");
            // A release just outside the standard green is green on the forgiving meter.
            float edge = t.greenCenter + ShotModel.GreenHalfWidth(70, t) * 1.2f;
            Assert.AreNotEqual(TimingGrade.Green, ShotModel.Grade(edge, 70, t));
            Assert.AreEqual(TimingGrade.Green, ShotModel.Grade(edge, 70, f));
            Assert.IsFalse(ForgivingMeterRules.Allowed(GameMode.Daily, null), "the Daily has a leaderboard");
            Assert.IsFalse(ForgivingMeterRules.Allowed(GameMode.QuickCall, "link"), "two phones must match");
            Assert.IsTrue(ForgivingMeterRules.Allowed(GameMode.Rise, null));
            var d = new CareerSaveData();
            Assert.IsFalse(d.settings.forgivingMeter, "off by default");
            d.settings.forgivingMeter = true;
            Assert.IsTrue(SaveCodec.Decode(SaveCodec.Encode(d), DefaultContent.Create(), out _).settings.forgivingMeter);
        }

        [Test]
        public void I14_ForgivingMeter_OnlyForYourShots()
        {
            var cat = DefaultContent.Create();
            var req = new MatchRequest { Mode = GameMode.QuickCall, HomeTeamId = cat.Teams[0].id, AwayTeamId = cat.Teams[1].id, ForgivingMeter = true };
            var sim = new MatchSimulation(MatchSetup.FromRequest(req, cat));
            int you = sim.ControlledIndex;
            int cpu = -1;
            for (int i = 0; i < sim.Players.Length; i++) if (sim.Players[i].Team != sim.Players[you].Team) { cpu = i; break; }
            Assert.IsFalse(ReferenceEquals(sim.Setup.Shot, sim.TuningFor(you)), "you get the forgiving meter");
            Assert.IsTrue(ReferenceEquals(sim.Setup.Shot, sim.TuningFor(cpu)), "the CPU keeps the standard meter");
            req.ForgivingMeter = false;
            var plain = new MatchSimulation(MatchSetup.FromRequest(req, cat));
            Assert.IsTrue(ReferenceEquals(plain.Setup.Shot, plain.TuningFor(plain.ControlledIndex)));
        }

        [Test]
        public void I15_Comebacks()
        {
            Assert.IsTrue(Comebacks.IsComeback(true, 6, false));
            Assert.IsFalse(Comebacks.IsComeback(true, 5, false));
            Assert.IsFalse(Comebacks.IsComeback(false, 10, false), "a loss isn't a comeback");
            Assert.IsFalse(Comebacks.IsComeback(true, 8, true), "Full Court needs 12");
            Assert.IsTrue(Comebacks.IsComeback(true, 12, true));
            var s = new MatchSummary { mode = GameMode.QuickCall, teamAId = "a", teamBId = "b", scoreA = 21, scoreB = 19, humanTeam = 0, winner = 0, playerOfTheGame = -1 };
            s.lines.Add(new SummaryLine { team = 0, isHuman = true, stats = new PlayerStatLine() });
            var m = MomentsBook.FromGame(s, false, false, 8);
            Assert.IsTrue(m.Exists(x => x.kind == MomentKind.Comeback && x.value == 8));
            StringAssert.Contains("COMEBACK: DOWN 8", MomentsBook.Line(m.Find(x => x.kind == MomentKind.Comeback), "B"));
            var d = new CareerSaveData();
            MomentsBook.Add(d, m);
            var back = SaveCodec.Decode(SaveCodec.Encode(d), DefaultContent.Create(), out _);
            Assert.IsTrue(back.moments.Exists(x => x.kind == MomentKind.Comeback), "comebacks survive a reload");
        }

        [Test]
        public void I16_ClockCalls()
        {
            Assert.AreEqual("LAST MINUTE", ClockCalls.For(60.2f, 59.9f, 120f, 10, 2, true, true, true));
            Assert.IsNull(ClockCalls.For(60.2f, 59.9f, 90f, 10, 2, true, true, true), "short games skip it");
            Assert.AreEqual("10 SECONDS!", ClockCalls.For(10.1f, 9.9f, 120f, 10, 9, true, true, true));
            Assert.IsNull(ClockCalls.For(10.1f, 9.9f, 120f, 15, 9, true, true, true), "not close");
            Assert.AreEqual("SUDDEN DEATH: NEXT BUCKET WINS", ClockCalls.For(0.02f, 0f, 120f, 12, 12, true, true, true));
            Assert.IsNull(ClockCalls.For(0.02f, 0f, 120f, 12, 11, true, true, true), "someone's ahead: it's over");
            Assert.IsNull(ClockCalls.For(30f, 29f, 120f, 1, 1, true, true, true), "nothing to call");
            Assert.IsNull(ClockCalls.For(-1f, 50f, 120f, 1, 1, true, true, true), "first frame of a late start");
            Assert.IsNull(ClockCalls.For(60.2f, 59.9f, 120f, 1, 1, false, true, true), "no clock");
        }

        [Test]
        public void I17_YourWeek()
        {
            var h = new List<MatchHistoryEntry> { G("x", 21, 10, 100, 14), G("y", 15, 21, 98, 9), G("z", 21, 3, 90, 25) };
            var m = new List<MomentEntry> { new MomentEntry { day = 99 }, new MomentEntry { day = 80 } };
            string line = WeekInReview.Line(h, m, 100);
            StringAssert.Contains("YOUR WEEK: 1-1", line);
            StringAssert.Contains("BEST 14 PTS", line, "the 25 was over a week ago");
            StringAssert.Contains("1 BIG MOMENT", line);
            Assert.IsNull(WeekInReview.Line(h, m, 200), "nothing this week");
            Assert.IsNull(WeekInReview.Line(null, null, 5));
        }
    }
}
