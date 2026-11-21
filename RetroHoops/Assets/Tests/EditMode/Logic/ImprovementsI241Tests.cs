using System;
using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>21 Nov 2026, improvements I241–I270: dimes, personal runs, stops, tonight's shooting, history, callouts and Quick Call picks.</summary>
    public class ImprovementsI241Tests
    {
        private const float Dt = 1f / 60f;
        private ContentCatalog _c;

        [SetUp]
        public void Setup() => _c = DefaultContent.Create();

        private MatchSimulation NewMatch(uint seed, string courtId = null)
        {
            var req = MatchRequest.QuickCallDefault(_c);
            req.Seed = seed;
            if (courtId != null) req.CourtId = courtId;
            var m = new MatchSimulation(MatchSetup.FromRequest(req, _c));
            for (int i = 0; i < 120 && m.Phase != MatchPhase.Live; i++) m.Step(Dt, PlayerInput.None);
            return m;
        }

        private static void Contains(string s, string part) => Assert.IsTrue(s != null && s.Contains(part), s ?? "(null)");

        [Test]
        public void I241_Assists_RaiseAnEvent_ForThePasser()
        {
            // Play a few games' worth of CPU basketball and check every assist came with its event.
            int seen = 0;
            for (uint seed = 1; seed <= 6 && seen == 0; seed++)
            {
                var m = NewMatch(seed);
                int assistsBefore = 0;
                for (int step = 0; step < 60 * 90 && m.Phase != MatchPhase.Final; step++)
                {
                    m.Step(Dt, PlayerInput.None);
                    int total = 0;
                    for (int i = 0; i < m.Players.Length; i++) total += m.Stats[i].assists;
                    int events = 0;
                    foreach (var e in m.Events)
                        if (e.Type == MatchEventType.Assist)
                        {
                            events++;
                            Assert.AreEqual(m.Players[e.PlayerIndex].Team, m.Players[e.Value].Team, "passer and scorer are teammates");
                            Assert.AreNotEqual(e.PlayerIndex, e.Value);
                        }
                    Assert.AreEqual(total - assistsBefore, events, "one event per assist");
                    seen += events;
                    assistsBefore = total;
                }
            }
            Assert.Greater(seen, 0, "some assist happened in six CPU games");
        }

        [Test]
        public void I243_StopTracker_CallsAt3_ThenEvery2_AndResetsOnABucket()
        {
            var s = new StopTracker();
            Assert.IsFalse(s.End(false));
            Assert.IsFalse(s.End(false));
            Assert.IsTrue(s.End(false), "3 stops");
            Assert.IsFalse(s.End(false));
            Assert.IsTrue(s.End(false), "5 stops");
            Assert.IsFalse(s.End(true), "they scored");
            Assert.AreEqual(0, s.Run);
            Assert.AreEqual(5, s.Best);
            s.End(false); s.End(false);
            Assert.IsTrue(s.End(false), "3 again after a reset");
        }

        [Test]
        public void I242_PersonalRun_UsesTheRunTracker_MineVersusEveryoneElse()
        {
            var r = new RunTracker();
            Assert.IsFalse(r.Add(0, 2));
            Assert.IsFalse(r.Add(0, 2));
            Assert.IsTrue(r.Add(0, 2));
            Assert.IsFalse(r.Add(1, 1), "a teammate's bucket ends your personal run");
            Assert.AreEqual(1, r.Team);
        }

        [Test]
        public void I244_I245_TonightsShooting()
        {
            var s = new PlayerStatLine();
            Assert.IsNull(TonightShooting.BestSpot(s));
            Assert.IsNull(TonightShooting.PerShot(s));
            s.chart.spots[2].attempted = 4; s.chart.spots[2].made = 3;
            s.chart.spots[5].attempted = 2; s.chart.spots[5].made = 2;
            s.chart.spots[6].attempted = 1; s.chart.spots[6].made = 1;
            Contains(TonightShooting.BestSpot(s), "3/4");
            s.points = 14; s.fieldGoalsAttempted = 10;
            Contains(TonightShooting.PerShot(s), "1.40");
        }

        [Test]
        public void I246_I260_PlayerOfTheGame_IsCounted_AndAMilestone()
        {
            var d = Career.New(_c);
            var m = NewMatch(21);
            int me = m.ControlledIndex;
            m.Stats[me].points = 21;
            m.Score[m.Setup.HumanTeam] = 21;
            var s = MatchSummary.From(m, GameMode.QuickCall, "potg");
            Assert.AreEqual(me, s.playerOfTheGame);
            Assert.IsTrue(PlayerOfTheGameCount.IsHuman(s));
            Career.ApplyMatch(d, s, new RewardGrant());
            Assert.AreEqual(1, d.totals.playerOfGames);
            Assert.IsTrue(d.lastMilestones.Contains("1 PLAYER OF THE GAME"), string.Join(",", d.lastMilestones));
            Contains(PlayerOfTheGameCount.Line(1), "#1");
            Assert.IsNull(PlayerOfTheGameCount.Line(0));
            Assert.AreEqual(1, SaveCodec.Decode(SaveCodec.Encode(d), _c, out _).totals.playerOfGames);
        }

        [Test]
        public void I247_DailyTip_IsTheSameAllDay_AndEveryTipIsTranslated()
        {
            Assert.AreEqual(DailyTip.For(500), DailyTip.For(500));
            Assert.AreNotEqual(DailyTip.For(500), DailyTip.For(501));
            Assert.IsNotNull(DailyTip.For(-3));
            foreach (var t in DailyTip.Tips) Assert.IsTrue(Loc.Has(t), t);
        }

        [Test]
        public void I248_NextMilestone_PicksTheClosest()
        {
            var t = new CareerTotals { games = 9, points = 40 };
            Contains(NextMilestone.Line(t), "10 GAMES");
            Contains(NextMilestone.Line(t), "(1 ");
            t.games = 10;
            Assert.IsFalse(NextMilestone.Line(t).Contains("10 GAMES"));
            Assert.IsNull(NextMilestone.Line(null));
        }

        [Test]
        public void I249_I259_History_KeepsTheCourtStealsAndBlocks()
        {
            var court = _c.Courts[0].id;
            var d = Career.New(_c);
            var m = NewMatch(31, court);
            Assert.AreEqual(court, m.Setup.CourtId);
            m.Stats[m.ControlledIndex].steals = 3;
            m.Stats[m.ControlledIndex].blocks = 2;
            Career.ApplyMatch(d, MatchSummary.From(m, GameMode.QuickCall, "court"), new RewardGrant());
            Assert.AreEqual(court, d.history[0].courtId);
            Assert.AreEqual(3, d.history[0].steals);
            var back = SaveCodec.Decode(SaveCodec.Encode(d), _c, out _);
            Assert.AreEqual(court, back.history[0].courtId);
            Assert.AreEqual(2, back.history[0].blocks);
            Contains(RecentGames.Lines(back, _c)[0], "3 STL");
            Contains(RecentGames.Lines(back, _c)[0], _c.Court(court).displayName.ToUpperInvariant());
        }

        [Test]
        public void I249_I252_FavoriteCourt_BestCourt_Trend_AndModes()
        {
            var d = Career.New(_c);
            string a = _c.Courts[0].id, b = _c.Courts[1].id;
            void G(string court, bool won, int pts, GameMode mode = GameMode.QuickCall) =>
                d.history.Add(new MatchHistoryEntry { courtId = court, scoreFor = won ? 21 : 10, scoreAgainst = won ? 10 : 21, points = pts, mode = mode });
            G(a, false, 20); G(a, false, 20); G(a, true, 20); G(b, true, 20); G(b, true, 20, GameMode.Rise);
            Assert.AreEqual(a, RecentGames.FavoriteCourt(d, out int games));
            Assert.AreEqual(3, games);
            Assert.AreEqual(b, RecentGames.BestCourt(d, out int w, out int l));
            Assert.AreEqual(2, w);
            Assert.AreEqual(0, l);
            d.totals.games = 10; d.totals.points = 100;
            Contains(RecentGames.Trend(d), "TRENDING UP");
            var modes = RecentGames.ByMode(d);
            Assert.AreEqual(1, modes.Count, "Rise has only 1 game");
            Assert.AreEqual("QUICK CALL", modes[0].mode);
            Assert.AreEqual("2-2", modes[0].record);
            Assert.AreEqual("FULL COURT", RecentGames.ModeName(GameMode.FullCourt));
            foreach (var r in RecentGames.Rows(d, _c)) Assert.IsTrue(Loc.Has(r.label) || r.label.StartsWith(Loc.T("RECORD")), r.label);
        }

        [Test]
        public void I254_Callouts_SettingFiltersMinorOnes_AndSaves()
        {
            Assert.IsTrue(Callouts.Show(Callouts.All, true));
            Assert.IsFalse(Callouts.Show(Callouts.Fewer, true));
            Assert.IsTrue(Callouts.Show(Callouts.Fewer, false), "big plays always show");
            Assert.AreEqual(Callouts.All, Callouts.Clamp(7));
            var d = Career.New(_c);
            d.settings.callouts = Callouts.Fewer;
            d.settings.lastQuickTeam = "team.a";
            d.settings.lastQuickOpp = "team.b";
            var back = SaveCodec.Decode(SaveCodec.Encode(d), _c, out _);
            Assert.AreEqual(Callouts.Fewer, back.settings.callouts);
            Assert.AreEqual("team.a", back.settings.lastQuickTeam);
            Assert.AreEqual("team.b", back.settings.lastQuickOpp);
        }

        [Test]
        public void I255_I256_QuickPicks()
        {
            var d = Career.New(_c);
            var mine = Secrets.PlayableTeams(_c, d.secrets);
            var league = Secrets.OpponentTeams(_c, d.secrets);
            Assert.AreEqual(-1, QuickPicks.IndexOf(mine, null));
            Assert.AreEqual(0, QuickPicks.IndexOf(mine, mine[0].id));
            for (uint seed = 1; seed < 40; seed++)
            {
                QuickPicks.Random(mine, league, seed, out int my, out string opp);
                Assert.IsTrue(my >= 0 && my < mine.Count);
                Assert.IsNotNull(opp);
                Assert.AreNotEqual(mine[my].id, opp);
            }
        }

        [Test]
        public void I257_I258_ShareHighlights_AndTheShopsNextPick()
        {
            var s = new MatchSummary { humanTeam = 0, scoreA = 21, scoreB = 15, winner = 0, humanTrailedBy = 7, leadChanges = 3, biggestLeadFor = 6 };
            s.lines.Add(new SummaryLine { isHuman = true, stats = new PlayerStatLine { points = 12, rebounds = 10 } });
            var h = GameCallouts.Highlights(s);
            Assert.IsTrue(h.Exists(x => x.Contains(Loc.T("COMEBACK WIN"))));
            Assert.IsFalse(h.Exists(x => x.StartsWith(Loc.T("LEAD CHANGES"))));
            Contains(GameShare.Text(s, null, null), Loc.T("DOUBLE-DOUBLE"));

            var d = Career.New(_c);
            var next = ShopNext.For(d, 7);
            Assert.IsNotNull(next);
            Assert.IsFalse(next.Consumable);
            var all = new List<string>();
            foreach (var p in Shop.Products) if (!p.Consumable) all.Add(p.Id);
            Shop.Apply(d, _c, all);
            Assert.IsNull(ShopNext.For(d, 7), "nothing left to suggest");
        }

        [Test]
        public void I264_GameLength_ForThePauseMenu()
        {
            var r = new GameRulesDef { targetScore = 21, useGameClock = true };
            Assert.AreEqual(Loc.T("FIRST TO") + " 21  ·  1:12 " + Loc.T("LEFT"), GameLength.Line(r, 71.2f));
            r.useGameClock = false;
            Assert.AreEqual(Loc.T("FIRST TO") + " 21", GameLength.Line(r, 0f));
            Assert.IsNull(GameLength.Line(null, 0f));
        }

        [Test]
        public void I268_SaveWithEveryNewFieldMaxed_StaysSmall()
        {
            var d = Career.New(_c);
            d.totals.playerOfGames = int.MaxValue;
            for (int i = 0; i < Records.HistoryLength; i++)
                d.history.Add(new MatchHistoryEntry { opponentId = "team.long.opponent", teamId = "team.long.home", courtId = "court.some.long.court.id", steals = 99, blocks = 99 });
            d.settings.lastQuickTeam = d.settings.lastQuickOpp = "team.some.long.id";
            string json = SaveCodec.Encode(d);
            Assert.Less(json.Length, 512 * 1024);
            string broken = System.Text.RegularExpressions.Regex.Replace(json, "\"stl\"\\s*:\\s*99", "\"stl\":-4");
            Assert.AreNotEqual(json, broken);
            Assert.AreEqual(0, SaveCodec.Decode(broken, _c, out _).history[0].steals);
            string badCallouts = System.Text.RegularExpressions.Regex.Replace(json, "\"callouts\"\\s*:\\s*0", "\"callouts\":9");
            Assert.AreEqual(Callouts.All, SaveCodec.Decode(badCallouts, _c, out _).settings.callouts);
        }

        [Test]
        public void I269_I270_WhatsNew_AndSpanish()
        {
            Assert.AreEqual("tour.whats_new_241", Tours.WhatsNew.Id);
            foreach (var w in new[] { Tours.WhatsNew.Body, "DIME!", "YOU'RE COOKING", "STRAIGHT", "LOCKDOWN", "STOPS IN A ROW", "BEST SPOT TONIGHT", "POINTS A SHOT",
                                      "PLAYER OF THE GAME", "IN YOUR CAREER", "TIP OF THE DAY", "NEXT MILESTONE", "TO GO", "ASSISTS A GAME", "REBOUNDS A GAME",
                                      "FAVORITE COURT (LAST 20)", "BEST COURT (LAST 20)", "SCORING TREND", "TRENDING UP", "TRENDING DOWN", "STEADY", "RECORD",
                                      "CALLOUTS", "ALL", "BIG PLAYS ONLY", "RANDOM MATCHUP", "RECENT GAMES", "No games yet. Play one!", "NEXT FOR YOUR COLLECTION",
                                      "FIRST TO", "LEFT", "BEST STOP STREAK" })
                Assert.IsTrue(Loc.Has(w), w);
        }
    }
}
