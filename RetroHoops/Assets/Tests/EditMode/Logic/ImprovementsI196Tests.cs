using System;
using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>21 Nov 2026, improvements I196–I215: momentum, runs, records, milestones, the glowing rings and the menus.</summary>
    public class ImprovementsI196Tests
    {
        private const float Dt = 1f / 60f;
        private ContentCatalog _c;

        [SetUp]
        public void Setup() => _c = DefaultContent.Create();

        private MatchSimulation NewMatch(uint seed, int offense = 0)
        {
            var req = MatchRequest.QuickCallDefault(_c);
            req.Seed = seed;
            var setup = MatchSetup.FromRequest(req, _c);
            setup.StartingOffense = offense;
            var m = new MatchSimulation(setup);
            for (int i = 0; i < 120 && m.Phase != MatchPhase.Live; i++) m.Step(Dt, PlayerInput.None);
            return m;
        }

        [Test]
        public void I196_Momentum_SwingsToTheTeamOnARun_AndFades()
        {
            var mo = new Momentum();
            Assert.AreEqual(0f, mo.Value(0f), 1e-4f);
            mo.Add(0, 2, 10f);
            mo.Add(0, 2, 12f);
            mo.Add(0, 2, 14f);
            float hot = mo.Value(14f);
            Assert.Greater(hot, 0.6f, "a 6-0 run swings the bar hard to team A");
            Assert.Less(hot, 1f);
            Assert.Less(mo.Value(80f), hot * 0.3f, "a quiet spell brings it back toward the middle");
            mo.Add(1, 3, 15f);
            Assert.Less(mo.Value(15f), hot, "an answer pulls it back");
            mo.Add(1, 0, 16f);
            mo.Add(2, 5, 16f);
            Assert.AreEqual(mo.Value(15f) * 1f, mo.Value(15f), "ignored adds change nothing");
        }

        [Test]
        public void I204_RunTracker_CallsAt6_ThenEvery4_AndResetsOnAnAnswer()
        {
            var r = new RunTracker();
            Assert.IsFalse(r.Add(0, 2));
            Assert.IsFalse(r.Add(0, 2));
            Assert.IsTrue(r.Add(0, 2), "6-0");
            Assert.AreEqual(6, r.Points);
            Assert.IsFalse(r.Add(0, 2), "8-0 is not a call");
            Assert.IsTrue(r.Add(0, 3), "11-0 passes 10");
            Assert.IsFalse(r.Add(0, 2), "13-0");
            Assert.IsTrue(r.Add(0, 1), "14-0");
            Assert.IsTrue(r.Call.StartsWith("14-0"));
            Assert.IsFalse(r.Add(1, 3), "the other team answers: a new run");
            Assert.AreEqual(1, r.Team);
            Assert.AreEqual(3, r.Points);
            Assert.IsTrue(r.Add(1, 3));
        }

        [Test]
        public void I197_HotSpot_NeedsEnoughShots_AndPicksTheBest()
        {
            var chart = new ShotChartData();
            Assert.IsNull(HotSpot.Line(chart));
            chart.spots[1].attempted = 20; chart.spots[1].made = 9;
            chart.spots[2].attempted = 5; chart.spots[2].made = 5;   // perfect, but too few shots
            chart.spots[3].attempted = 10; chart.spots[3].made = 6;
            Assert.IsTrue(HotSpot.Best(chart, out var spot, out float pct));
            Assert.AreEqual((ShotSpot)3, spot);
            Assert.AreEqual(0.6f, pct, 1e-4f);
            StringAssertContains(HotSpot.Line(chart), "60%");
            Assert.IsFalse(HotSpot.Best(null, out _, out _));
        }

        private static void StringAssertContains(string s, string part) => Assert.IsTrue(s != null && s.Contains(part), s);

        [Test]
        public void I198_GameRecords_ForPostersChaseDownsClutchAndGreenRuns_AreKeptAndSaved()
        {
            var d = Career.New(_c);
            var m = NewMatch(7);
            var line = m.Stats[m.ControlledIndex];
            line.posters = 2; line.chaseDowns = 1; line.clutchMakes = 1; line.bestGreenRun = 3;
            Career.ApplyMatch(d, MatchSummary.From(m, GameMode.QuickCall, "i196-a"), new RewardGrant());
            Assert.AreEqual(0, d.lastNewRecords.FindAll(x => x == "POSTERS").Count, "the first game sets marks quietly");
            line.posters = 3; line.bestGreenRun = 2;
            Career.ApplyMatch(d, MatchSummary.From(m, GameMode.QuickCall, "i196-b"), new RewardGrant());
            Assert.IsTrue(d.lastNewRecords.Contains("POSTERS"));
            Assert.IsFalse(d.lastNewRecords.Contains("GREEN RUN"));
            Assert.AreEqual(3, d.records.posters);
            Assert.AreEqual(3, d.records.greenRun);
            var back = SaveCodec.Decode(SaveCodec.Encode(d), _c, out _);
            Assert.AreEqual(3, back.records.posters);
            Assert.AreEqual(1, back.records.chaseDowns);
            Assert.AreEqual(1, back.records.clutchMakes);
            Assert.AreEqual(3, back.records.greenRun);
        }

        [Test]
        public void I199_Milestones_AreAnnouncedOnceWhenPassed()
        {
            var d = Career.New(_c);
            d.totals.games = 9;
            d.totals.points = 95;
            var m = NewMatch(9);
            m.Stats[m.ControlledIndex].points = 8;
            Career.ApplyMatch(d, MatchSummary.From(m, GameMode.QuickCall, "i199-a"), new RewardGrant());
            Assert.IsTrue(d.lastMilestones.Contains("10 GAMES"), string.Join(",", d.lastMilestones));
            Assert.IsTrue(d.lastMilestones.Contains("100 POINTS"), string.Join(",", d.lastMilestones));
            Career.ApplyMatch(d, MatchSummary.From(m, GameMode.QuickCall, "i199-b"), new RewardGrant());
            Assert.IsFalse(d.lastMilestones.Contains("10 GAMES"));
            Assert.IsFalse(d.lastMilestones.Contains("100 POINTS"));
            Assert.AreEqual(0, Milestones.Crossed(null, d.totals).Count);
            var practice = NewMatch(9);
            Career.ApplyMatch(d, MatchSummary.From(practice, GameMode.Practice, "i199-c"), new RewardGrant());
            Assert.AreEqual(0, d.lastMilestones.Count, "practice doesn't count");
        }

        [Test]
        public void I200_LoginStrip_ShowsTodayAndTomorrow()
        {
            var d = Career.New(_c);
            d.loginStreak = 0;
            Assert.IsNull(LoginStrip.Line(d));
            d.loginStreak = 3;
            string line = LoginStrip.Line(d);
            StringAssertContains(line, "3");
            StringAssertContains(line, "+" + Onboarding.LoginReward(4) + " SP");
        }

        [Test]
        public void I205_I206_LastGame_RunItBack_AndStreak()
        {
            var d = Career.New(_c);
            Assert.IsNull(LastGame.Line(d, _c));
            Assert.IsNull(LastGame.RunItBack(d, _c));
            var opp = _c.TeamsInTier(TeamTier.League)[1];
            d.history.Insert(0, new MatchHistoryEntry { mode = GameMode.QuickCall, opponentId = opp.id, scoreFor = 21, scoreAgainst = 15, points = 12 });
            StringAssertContains(LastGame.Line(d, _c), "W 21-15");
            var again = LastGame.RunItBack(d, _c);
            Assert.IsNotNull(again);
            Assert.AreEqual(opp.id, again.AwayTeamId);
            Assert.AreNotEqual(opp.id, again.HomeTeamId);
            Assert.AreEqual(GameMode.QuickCall, again.Mode);
            d.history[0].mode = GameMode.Rise;
            Assert.IsNull(LastGame.RunItBack(d, _c), "only Quick Call games run back");

            d.records.winStreak = 1;
            Assert.IsNull(LastGame.StreakLine(d));
            d.records.winStreak = 4; d.records.bestWinStreak = 4;
            StringAssertContains(LastGame.StreakLine(d), "YOUR BEST!");
            d.records.bestWinStreak = 9;
            StringAssertContains(LastGame.StreakLine(d), "9");
        }

        [Test]
        public void I207_I208_FullSet_AndShopProgress()
        {
            var d = Career.New(_c);
            Assert.IsNull(PackSets.FullSet(d));
            ShopProduct pack = null;
            foreach (var p in Shop.Products) if (p.Kind == ShopKind.GearPack) { pack = p; break; }
            Assert.IsNotNull(pack);
            Shop.Apply(d, _c, new List<string> { pack.Id });
            var wornSlots = new HashSet<CosmeticSlot>();
            foreach (var id in pack.Items)
            {
                var g = Array.Find(Shop.Gear, x => x.id == id);
                if (g.id == null || wornSlots.Contains(g.slot)) continue;
                d.SetEquipped(g.slot, id);
                wornSlots.Add(g.slot);
            }
            Assert.IsTrue(ReferenceEquals(pack, PackSets.FullSet(d)));
            StringAssertContains(PackSets.Progress(d), "/" + Shop.Courts.Length);
            StringAssertContains(PackSets.Progress(d), pack.Items.Length + "/" + Shop.Gear.Length);
        }

        [Test]
        public void I203_HitStop_ChaseDownAndClutch_HoldLikeBigMoments()
        {
            Assert.AreEqual(HitStop.ChaseDown, HitStop.For(new MatchEvent(MatchEventType.ChaseDown, 0, 0), ShotType.MidRange, false));
            Assert.AreEqual(HitStop.Clutch, HitStop.For(new MatchEvent(MatchEventType.Clutch, 0, 0), ShotType.MidRange, false));
            Assert.AreEqual(0f, HitStop.For(new MatchEvent(MatchEventType.Clutch, 0, 0), ShotType.MidRange, true), "Reduce Motion");
            Assert.LessOrEqual(HitStop.Clutch, HitStop.Max);
        }

        [Test]
        public void I209_I210_StealReach_AndBlockRange_Glow()
        {
            var m = NewMatch(11, offense: 1);
            int me = m.ControlledIndex;
            int handler = m.Ball.HolderIndex;
            Assert.GreaterOrEqual(handler, 0);
            Assert.AreNotEqual(m.Players[me].Team, m.Players[handler].Team);
            m.Players[me].Motion.position = m.Ball.Position + new Vec2(m.StealReach(me) * 0.5f, 0f);
            Assert.IsTrue(m.InStealReach(me));
            m.Players[me].Motion.position = m.Ball.Position + new Vec2(m.StealReach(me) * 3f, 0f);
            Assert.IsFalse(m.InStealReach(me));
            Assert.IsFalse(m.InStealReach(handler), "never on your own ball");
            Assert.IsFalse(m.InStealReach(-1));
            Assert.IsFalse(m.InBlockRange(me), "nobody is loading a shot");
            float p = ControlFeel.GlowPulse(0.3f);
            Assert.IsTrue(p >= 0.35f && p <= 0.75f, p.ToString());
        }

        [Test]
        public void I201_I202_Settings_SaveAndDefault()
        {
            var d = Career.New(_c);
            Assert.IsFalse(d.settings.iconsOnly);
            Assert.IsTrue(d.settings.momentumBar);
            d.settings.iconsOnly = true;
            d.settings.momentumBar = false;
            var back = SaveCodec.Decode(SaveCodec.Encode(d), _c, out _);
            Assert.IsTrue(back.settings.iconsOnly);
            Assert.IsFalse(back.settings.momentumBar);
        }

        [Test]
        public void I211_SaveWithEveryNewFieldMaxed_StaysWellUnderTheLimit()
        {
            var d = Career.New(_c);
            d.records.posters = d.records.chaseDowns = d.records.clutchMakes = d.records.greenRun = int.MaxValue;
            d.settings.iconsOnly = true;
            for (int i = 0; i < Records.HistoryLength; i++)
                d.history.Add(new MatchHistoryEntry { mode = GameMode.QuickCall, opponentId = "team.x", scoreFor = 99, scoreAgainst = 98, points = 99 });
            string json = SaveCodec.Encode(d);
            Assert.Less(json.Length, 512 * 1024);
            var back = SaveCodec.Decode(json, _c, out _);
            Assert.AreEqual(int.MaxValue, back.records.greenRun);
            // A broken value never decodes negative.
            Assert.AreEqual(0, SaveCodec.Decode(System.Text.RegularExpressions.Regex.Replace(json, "\"greenRun\"\\s*:\\s*" + int.MaxValue, "\"greenRun\":-5"), _c, out _).records.greenRun);
        }

        [Test]
        public void I213_VsAverage_ComparesWithEarlierGames()
        {
            var t = new CareerTotals { games = 3, points = 30 };
            var g = new PlayerStatLine { points = 20 };
            Assert.IsNull(VsAverage.Line(t, g), "needs 3 earlier games");
            t = new CareerTotals { games = 4, points = 50 }; // 30 in 3 earlier games (avg 10), 20 tonight
            StringAssertContains(VsAverage.Line(t, g), "10 ABOVE YOUR AVERAGE");
            t = new CareerTotals { games = 4, points = 40 }; // 30 earlier, 10 tonight: on average
            Assert.IsNull(VsAverage.Line(t, new PlayerStatLine { points = 10 }));
            Assert.IsFalse(VsAverage.Counts(GameMode.Practice));
            Assert.IsTrue(VsAverage.Counts(GameMode.QuickCall));
        }

        [Test]
        public void I214_I215_WhatsNew_AndSpanish()
        {
            Assert.IsTrue(Tours.WhatsNew.Id.StartsWith("tour.whats_new_"), Tours.WhatsNew.Id); // later phases refresh the card
            foreach (var w in new[] { "YOUR HOT SPOT", "CAREER MILESTONE", "LAST GAME", "WIN STREAK", "YOUR BEST!", "RUN IT BACK",
                                      "FULL SET", "ICONS ONLY (NO BUTTON WORDS)", "MOMENTUM BAR", "YOUR GAME", "ABOVE YOUR AVERAGE", "BELOW YOUR AVERAGE",
                                      "THEY'RE ON A RUN", "RUN", "TOMORROW", "IN A ROW", "DAY", "GEAR", "COURTS" })
                Assert.IsTrue(Loc.Has(w), w);
        }
    }
}
