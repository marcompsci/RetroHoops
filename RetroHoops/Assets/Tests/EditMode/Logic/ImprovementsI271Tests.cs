using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>21 Nov 2026, improvements I271–I280: scouting, stat leaders, nemesis, first win, bench notes, turnovers.</summary>
    public class ImprovementsI271Tests
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

        private static MatchHistoryEntry Game(string opp, bool won, int day = 100) =>
            new MatchHistoryEntry { opponentId = opp, scoreFor = won ? 21 : 15, scoreAgainst = won ? 15 : 21, day = day, points = 10 };

        [Test]
        public void I271_Scouting_NamesTheirBestPlayer()
        {
            var m = NewMatch(3);
            int them = 1 - m.Setup.HumanTeam;
            PlayerDef best = null;
            foreach (var p in m.Players) if (p.Team == them && (best == null || Scouting.Rating(p.Def) > Scouting.Rating(best))) best = p.Def;
            string line = Scouting.Line(m, them);
            Contains(line, "#" + best.jerseyNumber);
            Contains(line, best.DisplayName.ToUpperInvariant());
            Assert.IsNull(Scouting.Line(m, 5));
            var shooter = new PlayerDef { attributes = new AttributeSet { shooting = 90, finishing = 50 } };
            Assert.AreEqual("SHOOTER", Scouting.Strength(shooter));
            foreach (var w in new[] { "FINISHER", "SHOOTER", "PLAYMAKER", "STOPPER", "REBOUNDER", "BURNER" }) Assert.IsTrue(Loc.Has(w), w);
        }

        [Test]
        public void I272_StatLeaders_AcrossBothTeams()
        {
            var s = new MatchSummary();
            Assert.IsNull(StatLeaders.Line(s));
            s.lines.Add(new SummaryLine { isHuman = true, name = "Rook", stats = new PlayerStatLine { points = 14, assists = 2 } });
            s.lines.Add(new SummaryLine { name = "Big Sal", stats = new PlayerStatLine { points = 9, rebounds = 8, assists = 5 } });
            string line = StatLeaders.Line(s);
            Contains(line, "PTS: " + Loc.T("YOU") + " 14");
            Contains(line, "AST: BIG SAL 5");
            Contains(line, "REB: BIG SAL 8");
        }

        [Test]
        public void I273_NemesisMenu_NamesI13sNemesis()
        {
            var d = Career.New(_c);
            var opp = _c.TeamsInTier(TeamTier.League)[1];
            d.history.Add(Game(opp.id, false));
            Assert.IsNull(NemesisMenu.Line(d, _c));
            d.history.Add(Game(opp.id, false));
            Contains(NemesisMenu.Line(d, _c), opp.FullName.ToUpperInvariant() + "  0-2");
            Contains(NemesisMenu.Line(d, _c), "+" + Nemesis.BonusSP + " SP");
            d.history.Add(Game(opp.id, true));
            d.history.Add(Game(opp.id, true));
            Assert.IsNull(NemesisMenu.Line(d, _c), "2-2 isn't a nemesis");
        }

        [Test]
        public void I274_FirstWin_OfTheDay()
        {
            var d = Career.New(_c);
            Assert.IsFalse(FirstWin.Today(d));
            d.history.Insert(0, Game("x", false, 100));
            Assert.IsFalse(FirstWin.Today(d), "a loss");
            d.history.Insert(0, Game("x", true, 100));
            Assert.IsTrue(FirstWin.Today(d), "the first win today (after a loss)");
            d.history.Insert(0, Game("x", true, 100));
            Assert.IsFalse(FirstWin.Today(d), "the second win today");
            d.history.Insert(0, Game("x", true, 101));
            Assert.IsTrue(FirstWin.Today(d), "a new day");
        }

        [Test]
        public void I275_I278_BenchNotes()
        {
            Assert.IsNull(BenchNotes.Turnovers(new PlayerStatLine { turnovers = 3 }));
            Contains(BenchNotes.Turnovers(new PlayerStatLine { turnovers = 5 }), "5");
            Assert.AreEqual(DrillKind.FreeShoot, BenchNotes.Drill(new PlayerStatLine { fieldGoalsAttempted = 10, fieldGoalsMade = 3 }));
            Assert.AreEqual(DrillKind.DribbleLane, BenchNotes.Drill(new PlayerStatLine { fieldGoalsAttempted = 10, fieldGoalsMade = 6, turnovers = 4 }));
            Assert.AreEqual(DrillKind.Lockdown, BenchNotes.Drill(new PlayerStatLine { stealAttempts = 6 }));
            Assert.IsNull(BenchNotes.Drill(new PlayerStatLine { fieldGoalsAttempted = 10, fieldGoalsMade = 6 }));
            Contains(BenchNotes.DrillLine(new PlayerStatLine { fieldGoalsAttempted = 10, fieldGoalsMade = 2 }), Loc.T("FREE SHOOT"));
            Assert.IsNull(BenchNotes.DrillLine(null));
        }

        [Test]
        public void I276_Turnovers_AreCounted_Saved_AndShown()
        {
            var d = Career.New(_c);
            var m = NewMatch(7);
            m.Stats[m.ControlledIndex].turnovers = 4;
            Career.ApplyMatch(d, MatchSummary.From(m, GameMode.QuickCall, "tov"), new RewardGrant());
            Assert.AreEqual(4, d.totals.turnovers);
            var back = SaveCodec.Decode(SaveCodec.Encode(d), _c, out _);
            Assert.AreEqual(4, back.totals.turnovers);
            Assert.IsTrue(RecentGames.Rows(back, _c).Exists(r => r.label == "TURNOVERS A GAME" && r.value == "4.0"));
        }

        [Test]
        public void I277_RecentSummary()
        {
            var d = Career.New(_c);
            Assert.IsNull(RecentSummary.Line(d));
            d.history.Add(Game("x", true));
            d.history.Add(Game("x", false));
            Contains(RecentSummary.Line(d), "2: 1-1");
            Contains(RecentSummary.Line(d), "10.0");
        }

        [Test]
        public void I279_I280_SaveGuard_WhatsNew_AndSpanish()
        {
            var d = Career.New(_c);
            d.totals.turnovers = int.MaxValue;
            string json = SaveCodec.Encode(d);
            string broken = System.Text.RegularExpressions.Regex.Replace(json, "\"tov\"\\s*:\\s*" + int.MaxValue, "\"tov\":-2");
            Assert.AreNotEqual(json, broken);
            Assert.AreEqual(0, SaveCodec.Decode(broken, _c, out _).totals.turnovers);

            Assert.AreEqual("tour.whats_new_271", Tours.WhatsNew.Id);
            foreach (var w in new[] { Tours.WhatsNew.Body, "WATCH OUT", "LEADERS", "YOU", "YOUR NEMESIS", "FOR BEATING THEM", "FIRST WIN OF THE DAY!", "CARE WITH THE BALL", "TURNOVERS",
                                      "TRY", "PRACTICE LAB", "FREE SHOOT", "DRIBBLE LANE", "LOCKDOWN", "TURNOVERS A GAME", "LAST", "PTS A GAME" })
                Assert.IsTrue(Loc.Has(w), w);
        }
    }
}
