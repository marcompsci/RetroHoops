using System;
using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>21 Nov 2026, improvements I216–I240: how the score moved, comebacks, rematches, form and more career stats.</summary>
    public class ImprovementsI216Tests
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

        private static MatchHistoryEntry Game(string opp, bool won, int day = 100, int pts = 10) =>
            new MatchHistoryEntry { mode = GameMode.QuickCall, opponentId = opp, scoreFor = won ? 21 : 15, scoreAgainst = won ? 15 : 21, day = day, points = pts };

        [Test]
        public void I218_ScoreFlow_CountsLeadChangesTiesAndBiggestLeads()
        {
            var f = new ScoreFlow();
            Assert.AreEqual(ScoreFlowChange.LeadChange, f.Observe(2, 0), "the first lead");
            Assert.AreEqual(0, f.LeadChanges, "a first lead isn't a change");
            Assert.AreEqual(ScoreFlowChange.None, f.Observe(2, 0));
            f.Observe(8, 0);
            Assert.AreEqual(ScoreFlowChange.Tie, f.Observe(8, 8));
            Assert.AreEqual(1, f.Ties);
            Assert.AreEqual(ScoreFlowChange.LeadChange, f.Observe(8, 10));
            Assert.AreEqual(1, f.LeadChanges);
            f.Observe(10, 10);
            f.Observe(10, 12);
            Assert.AreEqual(1, f.LeadChanges, "team 1 leading again after a tie is not a lead change");
            Assert.AreEqual(8, f.BiggestLead[0]);
            Assert.AreEqual(2, f.BiggestLead[1]);
            f.Observe(13, 12);
            Assert.AreEqual(2, f.LeadChanges);
            Assert.AreEqual(0, f.Leader);
        }

        [Test]
        public void I218_TheSimulation_FeedsTheFlow_AndTheSummaryGetsIt()
        {
            var m = NewMatch(3);
            Assert.IsNotNull(m.Flow);
            m.Score[1] = 7;
            m.Step(Dt, PlayerInput.None);
            m.Score[0] = 9;
            m.Step(Dt, PlayerInput.None);
            Assert.AreEqual(1, m.Flow.LeadChanges);
            var s = MatchSummary.From(m, GameMode.QuickCall, "flow");
            Assert.AreEqual(1, s.leadChanges);
            int me = s.humanTeam;
            Assert.AreEqual(m.Flow.BiggestLead[me], s.biggestLeadFor);
            Assert.AreEqual(m.Flow.BiggestLead[1 - me], s.humanTrailedBy);
        }

        [Test]
        public void I220_I221_ComebackWins_AreCalledCountedAndRecorded()
        {
            var d = Career.New(_c);
            var m = NewMatch(5);
            int me = m.Setup.HumanTeam;
            m.Score[1 - me] = 8;
            m.Step(Dt, PlayerInput.None);
            m.Score[me] = 21;
            m.Step(Dt, PlayerInput.None);
            var s = MatchSummary.From(m, GameMode.QuickCall, "comeback");
            s.winner = me;
            Assert.AreEqual(8, GameCallouts.Comeback(s));
            Contains(string.Join("|", GameCallouts.For(s)), "COMEBACK WIN");
            Career.ApplyMatch(d, s, new RewardGrant());
            Assert.AreEqual(1, d.totals.comebacks);
            Assert.AreEqual(8, d.records.biggestComeback);
            Assert.IsTrue(d.lastMilestones.Contains("1 COMEBACK WINS"), string.Join(",", d.lastMilestones));
            var back = SaveCodec.Decode(SaveCodec.Encode(d), _c, out _);
            Assert.AreEqual(1, back.totals.comebacks);
            Assert.AreEqual(8, back.records.biggestComeback);

            s.humanTrailedBy = 5;
            Assert.AreEqual(0, GameCallouts.Comeback(s), "down 5 isn't a comeback");
            s.humanTrailedBy = 9;
            s.winner = 1 - me;
            Assert.AreEqual(0, GameCallouts.Comeback(s), "a loss isn't a comeback");
        }

        [Test]
        public void I228_GamePoint_OneBucketFromTheTarget()
        {
            var r = new GameRulesDef { targetScore = 21, insideArcPoints = 1, beyondArcPoints = 2 };
            Assert.IsFalse(GamePoint.At(r, 18, 0));
            Assert.IsTrue(GamePoint.At(r, 19, 0), "a 2 wins it");
            Assert.IsTrue(GamePoint.At(r, 20, 25));
            Assert.IsFalse(GamePoint.At(r, 21, 0), "already there");
            r.winByTwo = true;
            Assert.IsTrue(GamePoint.At(r, 20, 20), "win by two: a 2 makes it 22-20");
            r.beyondArcPoints = 1;
            Assert.IsFalse(GamePoint.At(r, 20, 20), "win by two with only 1s: 21-20 doesn't end it");
            r.beyondArcPoints = 2;
            Assert.IsTrue(GamePoint.At(r, 20, 18));
            r.winByTwo = false;
            r.bustRule = true;
            Assert.IsTrue(GamePoint.At(r, 19, 0), "exactly 21 with a 2");
            Assert.IsFalse(GamePoint.At(r, 17, 0));
            Assert.IsFalse(GamePoint.At(null, 20, 0));
        }

        [Test]
        public void I216_I217_HeadToHead_AndRematchLines()
        {
            var d = Career.New(_c);
            Assert.IsNull(HeadToHead.Line(d, "x"));
            Assert.IsNull(HeadToHead.TipLine(d, "x"));
            d.history.Add(Game("x", false));
            d.history.Add(Game("y", true));
            d.history.Add(Game("x", true));
            HeadToHead.Record(d, "x", out int w, out int l);
            Assert.AreEqual(1, w);
            Assert.AreEqual(1, l);
            Contains(HeadToHead.Line(d, "x"), "1-1");
            Contains(HeadToHead.TipLine(d, "x"), "REMATCH");
            d.history.Clear();
            for (int i = 0; i < 3; i++) d.history.Add(Game("x", true));
            d.history.Add(Game("x", false));
            Contains(HeadToHead.TipLine(d, "x"), "3");
            d.history.RemoveAt(0);
            Assert.IsNull(HeadToHead.TipLine(d, "x"), "two straight isn't called");
        }

        [Test]
        public void I223_I224_Form_AndTheWeek()
        {
            var d = Career.New(_c);
            d.history.Add(Game("x", true, 100, 12));
            d.history.Add(Game("x", false, 99, 8));
            Assert.IsNull(Form.Line(d), "needs 3 games");
            d.history.Add(Game("y", true, 90, 20));
            Assert.AreEqual(Loc.T("FORM") + "  W L W", Form.Line(d));
            string week = Form.Week(d, 100);
            Contains(week, "2 ");
            Contains(week, "1-1");
            Contains(week, "20 PTS");
            Assert.IsNull(Form.Week(d, 200));
        }

        [Test]
        public void I231_I232_I233_DoubleDoubles_AndPerfectGames()
        {
            var line = new PlayerStatLine { points = 12, rebounds = 10, assists = 3, fieldGoalsMade = 6, fieldGoalsAttempted = 6 };
            Assert.AreEqual(2, GameCallouts.Tens(line));
            Assert.IsTrue(GameCallouts.Perfect(line));
            line.assists = 11;
            Assert.AreEqual(3, GameCallouts.Tens(line));
            Assert.IsFalse(GameCallouts.Perfect(new PlayerStatLine { fieldGoalsMade = 4, fieldGoalsAttempted = 4 }), "needs 5 shots");

            var d = Career.New(_c);
            var m = NewMatch(9);
            var mine = m.Stats[m.ControlledIndex];
            mine.points = 12; mine.rebounds = 10; mine.fieldGoalsMade = 5; mine.fieldGoalsAttempted = 5;
            Career.ApplyMatch(d, MatchSummary.From(m, GameMode.QuickCall, "dd"), new RewardGrant());
            Assert.AreEqual(1, d.totals.doubleDoubles);
            Assert.AreEqual(1, d.totals.perfectGames);
            var s = MatchSummary.From(m, GameMode.QuickCall, "dd2");
            string all = string.Join("|", GameCallouts.For(s));
            Contains(all, "DOUBLE-DOUBLE");
            Contains(all, "PERFECT");
            var back = SaveCodec.Decode(SaveCodec.Encode(d), _c, out _);
            Assert.AreEqual(1, back.totals.doubleDoubles);
            Assert.AreEqual(1, back.totals.perfectGames);
        }

        [Test]
        public void I225_I226_I227_CareerStatsMore()
        {
            var d = Career.New(_c);
            var opp = _c.TeamsInTier(TeamTier.League)[1];
            d.history.Add(Game(opp.id, true));
            d.history.Add(Game(opp.id, false));
            d.shotChart.spots[0].attempted = 10; d.shotChart.spots[0].made = 7;
            d.shotChart.spots[4].attempted = 10; d.shotChart.spots[4].made = 2;
            var rows = CareerStatsMore.Rows(d, _c);
            string all = string.Join("|", rows.ConvertAll(r => r.label + "=" + r.value));
            Contains(all, "HOT SPOT=");
            Contains(all, "70%");
            Contains(all, "COLD SPOT=");
            Contains(all, "20%");
            Contains(all, "MOST PLAYED (LAST 20)=" + opp.FullName.ToUpperInvariant() + "  1-1");
            Contains(all, "BIGGEST COMEBACK=");
            foreach (var r in rows) Assert.IsTrue(Loc.Has(r.label), r.label);
        }

        [Test]
        public void I237_RunItBack_UsesTheTeamYouPlayedAs()
        {
            var d = Career.New(_c);
            var mine = Secrets.PlayableTeams(_c, d.secrets);
            Assert.GreaterOrEqual(mine.Count, 2);
            var me = mine[mine.Count - 1];
            var opp = _c.TeamsInTier(TeamTier.League).Find(t => t.id != me.id && t.id != mine[0].id);
            Assert.IsNotNull(opp);
            var g = Game(opp.id, true);
            g.teamId = me.id;
            d.history.Add(g);
            var req = LastGame.RunItBack(d, _c);
            Assert.AreEqual(me.id, req.HomeTeamId);
            Assert.AreEqual(opp.id, req.AwayTeamId);
            var back = SaveCodec.Decode(SaveCodec.Encode(d), _c, out _);
            Assert.AreEqual(me.id, back.history[0].teamId);

            var m = NewMatch(13);
            Career.ApplyMatch(d, MatchSummary.From(m, GameMode.QuickCall, "team"), new RewardGrant());
            Assert.AreEqual(m.Setup.HumanTeam == 0 ? m.Setup.TeamA.id : m.Setup.TeamB.id, d.history[0].teamId);
        }

        [Test]
        public void I230_I236_ShopCounts_AndThePauseSituation()
        {
            foreach (var p in Shop.Products)
            {
                string line = ShopCounts.Line(p);
                if (p.Kind == ShopKind.CourtPack || p.Kind == ShopKind.GearPack) Contains(line, p.Items.Length.ToString());
                else Assert.IsNull(line, p.Id);
            }
            Contains(Situation.Line(10, 6), "UP BY 4");
            Contains(Situation.Line(6, 10), "DOWN BY 4");
            Contains(Situation.Line(7, 7), "TIED");
        }

        [Test]
        public void I238_SaveWithEveryNewFieldMaxed_StaysSmall_AndNeverGoesNegative()
        {
            var d = Career.New(_c);
            d.totals.comebacks = d.totals.doubleDoubles = d.totals.perfectGames = int.MaxValue;
            d.records.biggestComeback = int.MaxValue;
            for (int i = 0; i < Records.HistoryLength; i++)
            {
                var g = Game("team.some.long.opponent.id", true);
                g.teamId = "team.some.long.home.id";
                d.history.Add(g);
            }
            string json = SaveCodec.Encode(d);
            Assert.Less(json.Length, 512 * 1024);
            var back = SaveCodec.Decode(json, _c, out _);
            Assert.AreEqual(int.MaxValue, back.totals.comebacks);
            string broken = System.Text.RegularExpressions.Regex.Replace(json, "\"comebacks\"\\s*:\\s*" + int.MaxValue, "\"comebacks\":-3");
            Assert.AreNotEqual(json, broken);
            Assert.AreEqual(0, SaveCodec.Decode(broken, _c, out _).totals.comebacks);
        }

        [Test]
        public void I239_I240_WhatsNew_AndSpanish()
        {
            Assert.IsTrue(Tours.WhatsNew.Id.StartsWith("tour.whats_new_"), Tours.WhatsNew.Id); // later phases refresh the card
            foreach (var w in new[] { "RECENT RECORD vs THEM", "REMATCH: THEY GOT YOU LAST TIME", "STRAIGHT WINS vs THEM", "FORM", "THIS WEEK",
                                      "GAME", "GAMES", "COMEBACK WIN", "DOWN BY", "UP BY", "TIED", "TRIPLE-DOUBLE!", "DOUBLE-DOUBLE", "PERFECT FROM THE FIELD",
                                      "LEAD CHANGES", "YOUR BIGGEST LEAD", "THEIRS", "LEAD CHANGE", "YOU TAKE THE LEAD", "THEY TAKE THE LEAD", "GAME POINT",
                                      "FIRST MEETING LATELY", "COURT", "COURTS", "GEAR ITEMS", "COMEBACK WINS", "DOUBLE-DOUBLES", "BIGGEST COMEBACK" })
                Assert.IsTrue(Loc.Has(w), w);
        }
    }
}
