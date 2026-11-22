using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>21 Nov 2026, improvements I281–I300: hot hand, shooting by range, goals, close games, random look.</summary>
    public class ImprovementsI281Tests
    {
        private const float Dt = 1f / 60f;
        private ContentCatalog _c;

        [SetUp]
        public void Setup() => _c = DefaultContent.Create();

        private MatchSimulation NewMatch(uint seed)
        {
            var req = MatchRequest.QuickCallDefault(_c);
            req.Seed = seed;
            var m = new MatchSimulation(MatchSetup.FromRequest(req, _c));
            for (int i = 0; i < 120 && m.Phase != MatchPhase.Live; i++) m.Step(Dt, PlayerInput.None);
            return m;
        }

        private static void Contains(string s, string part) => Assert.IsTrue(s != null && s.Contains(part), s ?? "(null)");

        private static MatchHistoryEntry Game(bool won, int day = 100, int pts = 10, int margin = 6) =>
            new MatchHistoryEntry { opponentId = "x", scoreFor = won ? 21 : 21 - margin, scoreAgainst = won ? 21 - margin : 21, day = day, points = pts };

        [Test]
        public void I281_TheSimulation_KeepsMakeRuns_AndMissesResetThem()
        {
            // CPU basketball: every player's best run is at least their current run, and never more than their makes.
            for (uint seed = 1; seed <= 3; seed++)
            {
                var m = NewMatch(seed);
                for (int step = 0; step < 60 * 60 && m.Phase != MatchPhase.Final; step++)
                {
                    m.Step(Dt, PlayerInput.None);
                    foreach (var e in m.Events)
                        if (e.Type == MatchEventType.ShotMissed) Assert.AreEqual(0, m.Stats[e.PlayerIndex].makeRun);
                }
                for (int i = 0; i < m.Players.Length; i++)
                {
                    var s = m.Stats[i];
                    Assert.LessOrEqual(s.makeRun, s.bestMakeRun);
                    Assert.LessOrEqual(s.bestMakeRun, s.fieldGoalsMade);
                }
            }
            Assert.IsFalse(HotHand.Called(new PlayerStatLine { makeRun = 2 }));
            Assert.IsTrue(HotHand.Called(new PlayerStatLine { makeRun = 3 }));
            Assert.IsFalse(HotHand.Called(new PlayerStatLine { makeRun = 4 }));
            Assert.IsTrue(HotHand.Called(new PlayerStatLine { makeRun = 5 }));
        }

        [Test]
        public void I283_I292_MakeStreakRecord_AndCloseGames()
        {
            var d = Career.New(_c);
            var m = NewMatch(5);
            int me = m.Setup.HumanTeam;
            m.Stats[m.ControlledIndex].bestMakeRun = 6;
            m.Score[me] = 21; m.Score[1 - me] = 20;
            Career.ApplyMatch(d, MatchSummary.From(m, GameMode.QuickCall, "close"), new RewardGrant());
            Assert.AreEqual(6, d.records.makeStreak);
            Assert.AreEqual(1, d.totals.closeWins);
            Assert.IsTrue(StreakLines.CloseOne(MatchSummary.From(m, GameMode.QuickCall, "close2")));
            var back = SaveCodec.Decode(SaveCodec.Encode(d), _c, out _);
            Assert.AreEqual(6, back.records.makeStreak);
            Assert.AreEqual(1, back.totals.closeWins);
            Assert.IsTrue(RecentGames.Rows(back, _c).Exists(r => r.label == "CLOSE GAMES (2 OR LESS)" && r.value == "1-0"));
        }

        [Test]
        public void I284_I285_I286_ShootingByRange()
        {
            var chart = new ShotChartData();
            Assert.IsNull(ShotRanges.Line(chart));
            chart[ShotSpot.Paint].attempted = 5; chart[ShotSpot.Paint].made = 4;
            chart[ShotSpot.ElbowLeft].attempted = 2; chart[ShotSpot.ElbowLeft].made = 1;
            chart[ShotSpot.FoulLine].attempted = 2; chart[ShotSpot.FoulLine].made = 1;
            chart[ShotSpot.CornerRight].attempted = 3; chart[ShotSpot.CornerRight].made = 1;
            string line = ShotRanges.Line(chart);
            Contains(line, "4/5");
            Contains(line, "2/4");
            Contains(line, "1/3");

            var s = new MatchSummary { humanTeam = 0 };
            var them = new PlayerStatLine();
            them.chart[ShotSpot.TopOfKey].attempted = 6; them.chart[ShotSpot.TopOfKey].made = 4;
            them.chart[ShotSpot.Paint].attempted = 2; them.chart[ShotSpot.Paint].made = 1;
            s.lines.Add(new SummaryLine { team = 1, stats = them });
            s.lines.Add(new SummaryLine { team = 0, stats = new PlayerStatLine { chart = chart } });
            Contains(ShotRanges.TheirDamage(s), Loc.T("DEEP") + " (4");
        }

        [Test]
        public void I287_I288_I296_I297_StreakLines()
        {
            var d = Career.New(_c);
            d.records.winStreak = 2;
            Assert.IsNull(StreakLines.WinStreak(d));
            d.records.winStreak = 4;
            Contains(StreakLines.WinStreak(d), "4");

            d.history.Add(Game(true));
            d.history.Add(Game(false));
            Assert.IsFalse(StreakLines.BounceBack(d), "one loss before isn't a bounce back");
            d.history.Add(Game(false));
            Assert.IsTrue(StreakLines.BounceBack(d));

            d.history[0].courtId = "court.a";
            d.history[1].courtId = "court.b";
            Assert.IsFalse(StreakLines.NewCourt(d, "court.a"));
            Assert.IsTrue(StreakLines.NewCourt(d, "court.z"));
            Assert.IsFalse(StreakLines.NewCourt(d, null));
            Assert.AreEqual(2, StreakLines.CourtsPlayed(d));
        }

        [Test]
        public void I290_I291_DailyAndWeeklyGoals()
        {
            var d = Career.New(_c);
            int day = 700;
            var goal = Goals.DailyFor(day);
            Assert.IsFalse(Goals.DailyDone(d, day));
            Assert.IsFalse(Goals.DailyLine(d, day).Contains(Loc.T("DONE!")));
            var g = new MatchHistoryEntry { day = day, scoreFor = 21, scoreAgainst = 10, points = 30, assists = 10, rebounds = 10 };
            Assert.IsTrue(Goals.Meets(g, goal.kind, goal.target), goal.text);
            d.history.Insert(0, g);
            Assert.IsTrue(Goals.DailyJustDone(d, day));
            Assert.IsTrue(Goals.DailyDone(d, day));
            d.history.Insert(0, new MatchHistoryEntry { day = day, scoreFor = 21, scoreAgainst = 10, points = 30, assists = 10, rebounds = 10 });
            Assert.IsFalse(Goals.DailyJustDone(d, day), "already done earlier today");
            Assert.AreEqual(Goals.DailyFor(day).text, Goals.DailyFor(day + 6).text, "six goals rotate");
            Assert.IsFalse(Goals.Meets(new MatchHistoryEntry { scoreFor = 21, scoreAgainst = 18 }, Goals.Kind.WinBy5, 5));

            int start = day - (day % 7);
            d.history.Clear();
            for (int i = 0; i < 6; i++) d.history.Add(Game(true, start + (i % 3)));
            d.history.Add(Game(true, start - 1)); // last week
            Assert.AreEqual(6, Goals.WeekWins(d, start + 3));
            Contains(Goals.WeeklyLine(d, start + 3), "(5/5)");
            foreach (var t in new[] { "SCORE 15 IN A GAME", "DISH 5 ASSISTS IN A GAME", "GRAB 6 REBOUNDS IN A GAME", "WIN A GAME", "WIN A GAME BY 5 OR MORE", "SCORE 20 IN A GAME" })
                Assert.IsTrue(Loc.Has(t), t);
        }

        [Test]
        public void I294_I295_RandomLook_AndPutItBack()
        {
            var d = Career.New(_c);
            var before = RandomLook.Snapshot(d);
            foreach (var slot in RandomLook.Slots)
                foreach (var item in _c.Cosmetics.FindAll(x => x.slot == slot)) if (!d.ownedCosmetics.Contains(item.id)) d.ownedCosmetics.Add(item.id);
            bool changedAny = false;
            for (uint seed = 1; seed < 20; seed++)
            {
                Assert.IsTrue(RandomLook.Shuffle(d, _c, seed));
                foreach (var slot in RandomLook.Slots)
                {
                    string id = d.Equipped(slot);
                    if (id == null) continue;
                    Assert.IsTrue(d.ownedCosmetics.Contains(id), id);
                    Assert.AreEqual(slot, _c.Cosmetics.Find(x => x.id == id).slot);
                }
                if (string.Join(",", RandomLook.Snapshot(d)) != string.Join(",", before)) changedAny = true;
            }
            Assert.IsTrue(changedAny);
            RandomLook.Restore(d, before);
            Assert.AreEqual(string.Join(",", before), string.Join(",", RandomLook.Snapshot(d)));
            var fresh = Career.New(_c);
            fresh.ownedCosmetics.Clear();
            Assert.IsFalse(RandomLook.Shuffle(fresh, _c, 3), "nothing owned, nothing to shuffle");
        }

        [Test]
        public void I298_I299_I300_SaveGuard_WhatsNew_AndSpanish()
        {
            var d = Career.New(_c);
            d.totals.closeWins = int.MaxValue;
            d.settings.shotClockTick = false;
            string json = SaveCodec.Encode(d);
            var back = SaveCodec.Decode(json, _c, out _);
            Assert.IsFalse(back.settings.shotClockTick);
            Assert.IsTrue(Career.New(_c).settings.shotClockTick);
            string broken = System.Text.RegularExpressions.Regex.Replace(json, "\"closeW\"\\s*:\\s*" + int.MaxValue, "\"closeW\":-1");
            Assert.AreNotEqual(json, broken);
            Assert.AreEqual(0, SaveCodec.Decode(broken, _c, out _).totals.closeWins);

            Assert.AreEqual("tour.whats_new_281", Tours.WhatsNew.Id);
            foreach (var w in new[] { Tours.WhatsNew.Body, "HOT HAND", "IN A ROW", "INSIDE", "MID", "DEEP", "THEY SCORED MOST FROM", "BUCKETS", "ON A WIN STREAK",
                                      "FIRST GAME HERE LATELY", "LONGEST MAKE STREAK", "BOUNCE BACK!", "CLOSE ONE!", "TODAY'S GOAL", "DONE!", "WEEKLY GOAL", "WIN 5 GAMES",
                                      "RANDOM OUTFIT", "PUT IT BACK", "SHOT CLOCK TICK", "CLOSE GAMES (2 OR LESS)", "BEST GAME: MAKES IN A ROW", "COURTS PLAYED (LAST 20)" })
                Assert.IsTrue(Loc.Has(w), w);
        }
    }
}
