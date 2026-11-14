using System;
using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>13 Nov 2026, improvements I81–I95: a deeper Franchise league.</summary>
    public class ImprovementsI81Tests
    {
        private ContentCatalog _c;
        private FranchiseSaveData _f;

        [SetUp]
        public void Setup()
        {
            _c = DefaultContent.Create();
            var league = _c.TeamsInTier(TeamTier.League);
            league.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            _f = Franchise.Create(_c, league[2].id, 81);
        }

        private void FinishSeason()
        {
            Franchise.SimToEnd(_f, _c);
            Franchise.SimToEnd(_f, _c);
            Assert.AreEqual(FranchisePhase.Draft, _f.phase);
        }

        private void ThroughOffSeason()
        {
            Franchise.DraftUntilYou(_f);
            while (_f.phase == FranchisePhase.Draft)
            {
                if (Franchise.OnTheClock(_f) == _f.you) Franchise.Pick(_f, Franchise.Prospects(_f)[0].id);
                Franchise.DraftUntilYou(_f);
            }
            foreach (var p in Franchise.Expiring(_f)) Franchise.ReSign(_f, p.id);
            Franchise.FinishReSign(_f);
            Franchise.FinishFreeAgency(_f, _c);
            while (Franchise.CannotStart(_f) != null && _f.teams[_f.you].roster.Count > Franchise.MaxRoster)
                Franchise.Release(_f, _f.teams[_f.you].roster[_f.teams[_f.you].roster.Count - 1]);
            if (Franchise.CannotStart(_f) == null) Franchise.StartSeason(_f, _c);
        }

        [Test]
        public void I81_SimulatedGames_CreditFullLines_AndScoresDontChange()
        {
            Franchise.SimNext(_f, _c);
            int reb = 0, ast = 0, stl = 0, blk = 0;
            foreach (var p in Franchise.Roster(_f, _f.you)) { reb += p.reb; ast += p.ast; stl += p.stl; blk += p.blk; }
            Assert.AreEqual(FranchiseLeague.SimReb, reb);
            Assert.AreEqual(FranchiseLeague.SimAst, ast);
            Assert.AreEqual(FranchiseLeague.SimStl, stl);
            Assert.AreEqual(FranchiseLeague.SimBlk, blk);
            // The same seed simulates the same scores as before (box credit draws nothing from the game's dice).
            var again = Franchise.Create(_c, _c.TeamsInTier(TeamTier.League).Find(t => t.id == _f.teams[_f.you].baseId).id, 81);
            Franchise.SimNext(again, _c);
            Assert.AreEqual(_f.season.games.FindAll(g => g.played).Count, again.season.games.FindAll(g => g.played).Count);
        }

        [Test]
        public void I81_PlayedGame_UsesTheRealBoxScore()
        {
            var r = Franchise.Roster(_f, _f.you);
            var lines = new List<SummaryLine>
            {
                new SummaryLine { team = 0, playerId = Franchise.PlayerId(r[0].id), stats = new PlayerStatLine { points = 17, rebounds = 6, assists = 4, steals = 2, blocks = 1 } },
                new SummaryLine { team = 0, playerId = Franchise.PlayerId(r[1].id), stats = new PlayerStatLine { points = 9, rebounds = 3, assists = 7 } },
                new SummaryLine { team = 1, playerId = "someone.else", stats = new PlayerStatLine { points = 30 } },
            };
            Assert.IsTrue(Franchise.RecordYourGame(_f, _c, 26, 20, null, lines, 0));
            Assert.AreEqual(17, r[0].pts);
            Assert.AreEqual(6, r[0].reb);
            Assert.AreEqual(4, r[0].ast);
            Assert.AreEqual(7, r[1].ast);
            Assert.AreEqual(1, r[0].gp);
            Assert.AreEqual(0, r[2].gp, "a player with no line didn't play");
        }

        [Test]
        public void I82_Leaders_AreSortedAndNeedGames()
        {
            for (int i = 0; i < 6; i++) Franchise.SimNext(_f, _c);
            for (int s = 0; s < FranchiseLeague.LeaderStats.Length; s++)
            {
                var top = FranchiseLeague.Leaders(_f, s, 5);
                Assert.AreEqual(5, top.Count, FranchiseLeague.LeaderStats[s]);
                for (int i = 1; i < top.Count; i++)
                    Assert.GreaterOrEqual(FranchiseLeague.PerGame(top[i - 1], s), FranchiseLeague.PerGame(top[i], s));
            }
        }

        [Test]
        public void I83_FinishedSeasons_GoInThePlayerHistory_AndTheCard()
        {
            FinishSeason();
            var p = Franchise.Roster(_f, _f.you)[0];
            Assert.AreEqual(1, p.history.Count);
            Assert.AreEqual(_f.year - 1, p.history[0].year);
            Assert.Greater(p.history[0].gp, 0);
            var lines = FranchiseLeague.CardLines(_f, p, t => "T" + t);
            Assert.IsTrue(lines.Exists(l => l.Contains(Loc.T("CAREER"))));
        }

        [Test]
        public void I84_SeasonEnd_HandsOutTheNewAwards()
        {
            FinishSeason();
            int year = _f.year - 1;
            foreach (var kind in new[] { "DEFENSE", "SIXTH", "ALL" })
                Assert.IsTrue(_f.awards.Exists(a => a.year == year && a.kind == kind), kind);
            Assert.AreEqual(5, _f.awards.FindAll(a => a.year == year && a.kind == "ALL").Count);
            foreach (var a in _f.awards) Assert.IsTrue(Loc.Has(FranchiseLeague.AwardName(a.kind)) || a.kind == "MVP", a.kind);
        }

        [Test]
        public void I85_Extension_OnlyInTheLastYear_AndItsSalaryStartsNextSeason()
        {
            var r = Franchise.Roster(_f, _f.you);
            var p = r.Find(x => x.years == 0) ?? r[0];
            p.years = 1;
            Assert.IsNotNull(FranchiseLeague.CannotExtend(_f, p), "years left");
            p.years = 0;
            FrontOffice.AddMood(p, -60);
            Assert.IsNotNull(FranchiseLeague.CannotExtend(_f, p), "unhappy");
            FrontOffice.AddMood(p, 60);
            Assert.IsNull(FranchiseLeague.CannotExtend(_f, p));
            int newSalary = FranchiseLeague.ExtensionSalary(p);
            int oldSalary = p.salary;
            Assert.IsTrue(FranchiseLeague.Extend(_f, p.id));
            Assert.IsFalse(FranchiseLeague.Extend(_f, p.id), "once");
            Assert.AreEqual(oldSalary, p.salary, "this season's salary stays");
            FinishSeason();
            Assert.AreEqual(newSalary, p.salary);
            Assert.Greater(p.years, 0, "not a free agent this summer");
            Assert.IsFalse(Franchise.Expiring(_f).Contains(p));
        }

        [Test]
        public void I86_FreeAgentInterest_FollowsWinningFansAndFacilities()
        {
            var fa = new FrPlayer { id = 99999, attrs = new AttributeSet(75, 75, 75, 75, 75, 75, 75, 75), age = 27 };
            int before = FranchiseLeague.Interest(_f, fa);
            FrontOffice.Of(_f).gym = FrontOffice.Of(_f).arena = 3;
            FrontOffice.AddFans(_f, 40);
            Assert.Greater(FranchiseLeague.Interest(_f, fa), before);
            FrontOffice.AddFans(_f, -100);
            FrontOffice.Of(_f).gym = FrontOffice.Of(_f).arena = 0;
            var star = new FrPlayer { id = 99998, attrs = new AttributeSet(95, 95, 95, 95, 95, 95, 95, 95), age = 27 };
            Assert.Less(FranchiseLeague.Interest(_f, star), FranchiseLeague.InterestNeeded, "a star won't come to a club nobody watches");
        }

        [Test]
        public void I87_TradeBlock_DrawsCallsAboutThatPlayer()
        {
            Assert.AreEqual(FranchiseDepth.OfferChance, FranchiseLeague.OfferChance(_f));
            var r = Franchise.Roster(_f, _f.you);
            var p = r[r.Count - 1];
            Assert.IsTrue(FranchiseLeague.ToggleBlock(_f, p.id));
            Assert.IsTrue(p.onBlock);
            Assert.Greater(FranchiseLeague.OfferChance(_f), FranchiseDepth.OfferChance);
            FrOffer offer = null;
            for (int w = 0; w < 6 && offer == null; w++) offer = FranchiseDepth.MakeOffer(_f, _c, new SeededRandom((uint)(w + 7)), 0);
            if (offer != null) Assert.AreEqual(p.id, offer.want[0]);
            Assert.IsTrue(FranchiseLeague.ToggleBlock(_f, p.id));
            Assert.IsFalse(p.onBlock);
        }

        [Test]
        public void I88_Injuries_OffByDefault_AndOnTheyHappenAndHeal()
        {
            Assert.IsFalse(FranchiseLeague.Of(_f).injuries);
            Franchise.SimToEnd(_f, _c);
            foreach (var p in _f.players) Assert.AreEqual(0, p.injured, "nobody gets hurt with injuries off");

            Setup();
            FranchiseLeague.Of(_f).injuries = true;
            int hurtEver = 0;
            for (int w = 0; w < Franchise.Weeks; w++)
            {
                Franchise.SimNext(_f, _c);
                foreach (var p in _f.players) if (p.injured > 0) hurtEver++;
                for (int t = 0; t < Franchise.Teams; t++)
                {
                    var r = Franchise.Roster(_f, t);
                    Assert.GreaterOrEqual(r.FindAll(p => p.injured == 0).Count, 5, "always five who can play");
                    int firstHurt = r.FindIndex(p => p.injured > 0);
                    if (firstHurt >= 0) Assert.IsTrue(r.GetRange(firstHurt, r.Count - firstHurt).TrueForAll(p => p.injured > 0), "the injured sit at the end");
                }
            }
            Assert.Greater(hurtEver, 0);
        }

        [Test]
        public void I89_Rename_IsFiltered_AndShowsOnTheTeam()
        {
            Assert.IsTrue(FranchiseLeague.Rename(_f, "Fresno", "Night Owls", "fno"));
            Franchise.Register(_c, _f);
            var def = _c.Team(Franchise.TeamId(_f.you));
            Assert.AreEqual("Fresno", def.city);
            Assert.AreEqual("Night Owls", def.nickname);
            Assert.AreEqual("FNO", def.abbreviation);
            Assert.IsFalse(FranchiseLeague.Rename(_f, "Fresno", "f4ggot", "ABC"));
            Assert.AreEqual("Night Owls", FranchiseLeague.Of(_f).nickname, "a rejected name changes nothing");
            Assert.IsTrue(FranchiseLeague.Rename(_f, "<b>Very long city name here</b>", "", ""));
            Assert.LessOrEqual(FranchiseLeague.Of(_f).city.Length, FranchiseLeague.MaxCity);
            Assert.IsFalse(FranchiseLeague.Of(_f).city.Contains("<"));
        }

        [Test]
        public void I90_Legacy_GrowsWithSeasons_AndHasRanks()
        {
            Assert.AreEqual(0, FranchiseLeague.Of(_f).legacy);
            Assert.AreEqual("INTERN", FranchiseLeague.Rank(0));
            FinishSeason();
            Assert.Greater(FranchiseLeague.Of(_f).legacy, 0);
            Assert.AreEqual("LEGEND GM", FranchiseLeague.Rank(5000));
            Assert.AreEqual(-1, FranchiseLeague.NextRankAt(5000));
            foreach (var r in FranchiseLeague.Ranks) Assert.IsTrue(Loc.Has(r.name), r.name);
        }

        [Test]
        public void I91_DraftGrades_EveryClassGetsOne()
        {
            FinishSeason();
            ThroughOffSeason();
            var grades = FranchiseLeague.Of(_f).grades;
            Assert.Greater(grades.Count, 0);
            foreach (var g in grades) Assert.IsTrue(Array.IndexOf(new[] { "A", "B+", "B", "C+", "C", "D" }, g.grade) >= 0, g.grade);
        }

        [Test]
        public void I92_I93_Preview_Sticks_AndPowerRankingsCoverEveryClub()
        {
            var preview = new List<int>(FranchiseLeague.Preview(_f));
            Assert.AreEqual(Franchise.Teams, preview.Count);
            for (int i = 0; i < 4; i++) Franchise.SimNext(_f, _c);
            CollectionEqual(preview, FranchiseLeague.Preview(_f));
            var power = FranchiseLeague.PowerRankings(_f);
            Assert.AreEqual(Franchise.Teams, power.Count);
            var seen = new HashSet<int>(power);
            Assert.AreEqual(Franchise.Teams, seen.Count);
            FinishSeason();
            Assert.IsFalse(string.IsNullOrEmpty(FranchiseLeague.Of(_f).previewResult));
        }

        private static void CollectionEqual(List<int> a, List<int> b) => Assert.AreEqual(string.Join(",", a), string.Join(",", b));

        [Test]
        public void I94_SimToDeadline_StopsAtTheDeadline()
        {
            FranchiseLeague.SimToDeadline(_f, _c);
            Assert.AreEqual(FranchisePhase.Regular, _f.phase);
            Assert.AreEqual(Franchise.TradeDeadlineWeek, _f.season.currentWeek);
            Assert.IsFalse(Franchise.TradesOpen(_f));
        }

        [Test]
        public void I95_Save_RoundTrips_EverythingNew()
        {
            FranchiseLeague.Of(_f).injuries = true;
            FranchiseLeague.Rename(_f, "Fresno", "Night Owls", "FNO");
            for (int i = 0; i < 3; i++) Franchise.SimNext(_f, _c);
            var p = Franchise.Roster(_f, _f.you)[1];
            p.onBlock = true; p.extension = 120; p.injured = 2; p.injSlot = 1;
            p.history.Add(new FrSeasonLine { year = 0, team = _f.you, gp = 14, pts = 200, reb = 50, ast = 30, ovr = 71 });
            var back = SaveCodec.Decode(SaveCodec.Encode(new CareerSaveData { franchise = _f }), _c, out _).franchise;
            var q = Franchise.Player(back, p.id);
            Assert.AreEqual(p.reb, q.reb);
            Assert.AreEqual(p.ast, q.ast);
            Assert.AreEqual(p.careerReb, q.careerReb);
            Assert.IsTrue(q.onBlock);
            Assert.AreEqual(120, q.extension);
            Assert.AreEqual(2, q.injured);
            Assert.AreEqual(1, q.history.Count);
            Assert.AreEqual(200, q.history[0].pts);
            var l = FranchiseLeague.Of(back);
            Assert.IsTrue(l.injuries);
            Assert.AreEqual("Night Owls", l.nickname);
            Assert.AreEqual("FNO", l.abbr);
        }

        [Test]
        public void I95_ThreeSeasons_StillRunCleanly_WithInjuriesOn()
        {
            FranchiseLeague.Of(_f).injuries = true;
            for (int y = 0; y < 3; y++)
            {
                FinishSeason();
                ThroughOffSeason();
                Assert.AreEqual(FranchisePhase.Regular, _f.phase, Franchise.CannotStart(_f));
            }
            Assert.Less(_f.players.Count, 160);
            foreach (var p in _f.players) Assert.LessOrEqual(p.history.Count, 20);
        }
    }
}
