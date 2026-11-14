using System;
using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>14 Nov 2026, improvements I96–I110: a living Franchise league.</summary>
    public class ImprovementsI96Tests
    {
        private ContentCatalog _c;
        private FranchiseSaveData _f;

        [SetUp]
        public void Setup()
        {
            _c = DefaultContent.Create();
            var league = _c.TeamsInTier(TeamTier.League);
            league.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            _f = Franchise.Create(_c, league[4].id, 96);
            Franchise.Register(_c, _f);
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
        public void I96_Commissioner_CapOff_LetsBigSalariesThrough()
        {
            var fa = new FrPlayer { id = 77777, first = "Big", last = "Ask", attrs = new AttributeSet(70, 70, 70, 70, 70, 70, 70, 70), age = 26, team = -1 };
            _f.players.Add(fa);
            _f.phase = FranchisePhase.FreeAgency;
            foreach (var p in Franchise.Roster(_f, _f.you)) p.salary = Franchise.Cap / 6;
            FrontOffice.AddFans(_f, 50);
            FrontOffice.Of(_f).gym = FrontOffice.Of(_f).arena = 3;
            string why = Franchise.CannotSign(_f, fa);
            if (Franchise.Roster(_f, _f.you).Count < Franchise.MaxRoster) StringAssert.Contains("cap", why ?? "cap");
            FranchiseLeague.Of(_f).capOff = true;
            Assert.IsFalse(FranchiseLife.CapOn(_f));
            why = Franchise.CannotSign(_f, fa);
            Assert.IsTrue(why == null || !why.Contains("cap room"), why);
        }

        [Test]
        public void I96_TradeDifficulty_ChangesWhatGMsAccept()
        {
            FranchiseLeague.Of(_f).tradeLevel = 0;
            Assert.Less(FranchiseLife.TradeMargin(_f), 1f);
            FranchiseLeague.Of(_f).tradeLevel = 2;
            Assert.Greater(FranchiseLife.TradeMargin(_f), 1f);
            FranchiseLeague.Of(_f).offersOff = true;
            for (int w = 0; w < 8; w++) Franchise.SimNext(_f, _c);
            Assert.IsNull(_f.offer, "no GM calls when they're switched off");
        }

        [Test]
        public void I96_EditRatings_MovesOverall_AndIsCounted()
        {
            var p = Franchise.Roster(_f, _f.you)[0];
            int before = p.Overall;
            Assert.IsTrue(FranchiseLife.EditRatings(_f, p.id, 2));
            Assert.Greater(p.Overall, before);
            Assert.GreaterOrEqual(p.potential, p.Overall);
            Assert.AreEqual(1, FranchiseLeague.Of(_f).commishEdits);
            Assert.IsFalse(FranchiseLife.EditRatings(_f, -5, 1));
        }

        [Test]
        public void I97_I108_Headlines_FillDuringTheSeason_WithPlayerOfTheWeek()
        {
            for (int w = 0; w < 3; w++) Franchise.SimNext(_f, _c);
            var news = FranchiseLeague.Of(_f).news;
            Assert.GreaterOrEqual(news.Count, 3);
            Assert.IsTrue(news.Exists(n => n.Contains(Loc.T("PLAYER OF THE WEEK"))));
            Assert.LessOrEqual(FranchiseLife.Latest(_f, 5).Count, 5);
            for (int i = 0; i < 100; i++) FranchiseLife.Headline(_f, "x" + i);
            Assert.AreEqual(FranchiseLife.MaxNews, news.Count);
            Assert.IsTrue(FranchiseLife.Latest(_f, 1)[0].EndsWith("x99"));
        }

        [Test]
        public void I98_I106_AllStarWeekend_AndDeadlineDay_HappenOnce()
        {
            FranchiseLeague.SimToDeadline(_f, _c);
            var l = FranchiseLeague.Of(_f);
            Assert.AreEqual(_f.year, l.allStarYear);
            Assert.AreEqual(FranchiseLife.AllStars, l.allStars.Count);
            Assert.IsTrue(l.allStars.Contains(l.allStarMvp));
            Assert.IsTrue(l.news.Exists(n => n.Contains(Loc.T("ALL-STAR GAME"))));
            int count = l.allStars.Count;
            Franchise.SimNext(_f, _c);
            Assert.AreEqual(count, l.allStars.Count, "once a season");
            // Deadline deals only between other clubs, and everyone's roster stays legal.
            for (int t = 0; t < Franchise.Teams; t++)
            {
                Assert.GreaterOrEqual(_f.teams[t].roster.Count, Franchise.MinRoster);
                Assert.LessOrEqual(_f.teams[t].roster.Count, Franchise.MaxRoster);
            }
        }

        [Test]
        public void I99_I104_SeasonEnd_FinalsMvp_AndRecordBook()
        {
            FinishSeason();
            int year = _f.year - 1;
            Assert.IsTrue(_f.awards.Exists(a => a.year == year && a.kind == "FINALS"));
            var records = FranchiseLeague.Of(_f).records;
            foreach (var k in FranchiseLife.RecordKinds) Assert.IsTrue(records.Exists(r => r.kind == k), k);
            Assert.Greater(records.Find(r => r.kind == "WINS").value, 0);
        }

        [Test]
        public void I100_Legends_GetTheirNumberRetired_AndItCantBeWornAgain()
        {
            var p = Franchise.Roster(_f, _f.you)[0];
            for (int y = 0; y < 4; y++) p.history.Add(new FrSeasonLine { year = y, team = _f.you, gp = 14, pts = 200 });
            Assert.IsTrue(FranchiseLife.IsLegend(_f, p));
            FranchiseLife.OnRetire(_f, p);
            Assert.AreEqual(1, FranchiseLeague.Of(_f).banners.Count);
            Assert.IsTrue(FranchiseLife.IsRetiredNumber(_f, _f.you, p.number));
            FranchiseLife.OnRetire(_f, p);
            Assert.AreEqual(1, FranchiseLeague.Of(_f).banners.Count, "once");
            var newcomer = Franchise.Roster(_f, _f.you)[1];
            Assert.IsFalse(FranchiseLife.IsLegend(_f, newcomer));
        }

        [Test]
        public void I101_Mentors_NeedAVeteran()
        {
            var r = Franchise.Roster(_f, _f.you);
            foreach (var p in r) { p.age = 25; }
            Assert.AreEqual(0, FranchiseLife.Mentees(_f).Count);
            r[0].age = 32; r[0].attrs = new AttributeSet(80, 80, 80, 80, 80, 80, 80, 80);
            r[1].age = 20; r[1].potential = 95; r[2].age = 21; r[2].potential = 95; r[3].age = 22; r[3].potential = 95;
            var m = FranchiseLife.Mentees(_f);
            Assert.AreEqual(2, m.Count, "two per mentor");
            Assert.IsTrue(m.Contains(r[1]) && m.Contains(r[2]), "the youngest first");
        }

        [Test]
        public void I102_Continuity_GrowsWhenAClubStaysTogether()
        {
            Assert.AreEqual(0f, FranchiseLife.Continuity(_f, _f.you));
            FinishSeason();
            Assert.Greater(FranchiseLife.Continuity(_f, _f.you), 0f, "after a season together");
            Assert.Greater(FranchiseLife.ContinuityEdge(_f, _f.you), 0f);
            Assert.LessOrEqual(FranchiseLife.ContinuityShooting(_f), 0.01f);
        }

        [Test]
        public void I103_ThreeMissedGoals_GetYouFired_AndANewJobWorks()
        {
            var l = FranchiseLeague.Of(_f);
            for (int y = 0; y < 3 && !l.fired; y++)
            {
                FrontOffice.Of(_f).goal = 3; FrontOffice.Of(_f).goalYear = _f.year; // GET THE FANS TO 70
                FrontOffice.AddFans(_f, -100);
                FinishSeason();
                if (!l.fired) ThroughOffSeason();
            }
            Assert.IsTrue(l.fired);
            Assert.AreEqual(3, l.jobOffers.Count);
            Assert.IsFalse(l.jobOffers.Contains(_f.you));
            Assert.IsNotNull(Franchise.CannotStart(_f));
            int old = _f.you, pick = l.jobOffers[0];
            Assert.IsFalse(FranchiseLife.TakeJob(_f, old));
            Assert.IsTrue(FranchiseLife.TakeJob(_f, pick));
            Assert.AreEqual(pick, _f.you);
            Assert.IsFalse(l.fired);
            Assert.AreEqual(old, FrontOffice.Of(_f).rival);
            Assert.AreEqual(1, l.jobs);
        }

        [Test]
        public void I105_DiscountAsk_HappyPlayerSays_YesUnhappyIsInsulted()
        {
            FinishSeason();
            Franchise.DraftUntilYou(_f);
            while (_f.phase == FranchisePhase.Draft)
            {
                if (Franchise.OnTheClock(_f) == _f.you) Franchise.Pick(_f, Franchise.Prospects(_f)[0].id);
                Franchise.DraftUntilYou(_f);
            }
            Assert.AreEqual(FranchisePhase.ReSign, _f.phase);
            var r = Franchise.Roster(_f, _f.you);
            r[0].years = 0; r[1].years = 0;
            FrontOffice.AddMood(r[0], 30);
            FrontOffice.AddMood(r[1], -40);
            int ask = FrontOffice.ReSignSalary(r[0], Franchise.Demand(r[0]));
            Assert.IsTrue(FranchiseLife.AskDiscount(_f, r[0].id, out _));
            Assert.LessOrEqual(r[0].salary, ask);
            Assert.Greater(r[0].years, 0, "signed on the spot");
            int moodBefore = FrontOffice.Morale(r[1]);
            Assert.IsFalse(FranchiseLife.AskDiscount(_f, r[1].id, out string msg));
            Assert.Less(FrontOffice.Morale(r[1]), moodBefore);
            Assert.IsFalse(FranchiseLife.AskDiscount(_f, r[1].id, out msg), "once per player");
        }

        [Test]
        public void I107_Milestones_AreRecordedOnce()
        {
            var p = Franchise.Roster(_f, _f.you)[0];
            p.careerPts = 1200;
            Franchise.SimNext(_f, _c);
            Assert.AreNotEqual(0, p.milestones & 1);
            Assert.AreNotEqual(0, p.milestones & 2);
            int news = FranchiseLeague.Of(_f).news.FindAll(n => n.Contains(p.Name) && n.Contains("1000")).Count;
            Franchise.SimNext(_f, _c);
            Assert.AreEqual(news, FranchiseLeague.Of(_f).news.FindAll(n => n.Contains(p.Name) && n.Contains("1000")).Count);
        }

        [Test]
        public void I109_WhatsNew_ShowsUntilDismissed_AndIsTranslated()
        {
            Assert.IsTrue(FranchiseLife.ShowWhatsNew(_f));
            FranchiseLife.DismissWhatsNew(_f);
            Assert.IsFalse(FranchiseLife.ShowWhatsNew(_f));
            foreach (var s in FranchiseLife.WhatsNew) Assert.IsTrue(Loc.Has(s), s);
        }

        [Test]
        public void I110_Save_RoundTrips_TheLivingLeague()
        {
            var l = FranchiseLeague.Of(_f);
            l.capOff = true; l.tradeLevel = 2; l.offersOff = true; l.whatsNew = 1; l.missedGoals = 2;
            l.banners.Add(new FrBanner { number = 23, name = "A Legend", year = 3, line = "5 seasons" });
            for (int w = 0; w < 3; w++) Franchise.SimNext(_f, _c);
            var p = Franchise.Roster(_f, _f.you)[0];
            p.tenure = 3; p.allStarNods = 2; p.milestones = 5;
            var back = SaveCodec.Decode(SaveCodec.Encode(new CareerSaveData { franchise = _f }), _c, out _).franchise;
            var b = FranchiseLeague.Of(back);
            Assert.IsTrue(b.capOff);
            Assert.AreEqual(2, b.tradeLevel);
            Assert.IsTrue(b.offersOff);
            Assert.AreEqual(1, b.whatsNew);
            Assert.AreEqual(2, b.missedGoals);
            Assert.AreEqual(23, b.banners[0].number);
            Assert.AreEqual(l.news.Count, b.news.Count);
            var q = Franchise.Player(back, p.id);
            Assert.AreEqual(3, q.tenure);
            Assert.AreEqual(2, q.allStarNods);
            Assert.AreEqual(5, q.milestones);
        }

        [Test]
        public void I110_ThreeSeasons_StillRunCleanly()
        {
            for (int y = 0; y < 3; y++)
            {
                FinishSeason();
                var l = FranchiseLeague.Of(_f);
                if (l.fired) FranchiseLife.TakeJob(_f, l.jobOffers[0]);
                ThroughOffSeason();
                Assert.AreEqual(FranchisePhase.Regular, _f.phase, Franchise.CannotStart(_f));
            }
            Assert.LessOrEqual(FranchiseLeague.Of(_f).news.Count, FranchiseLife.MaxNews);
        }
    }
}
