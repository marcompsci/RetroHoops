using System;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>1 Nov 2026, improvements I71–I80: game length, club records, rival, owner's goal, trade requests, captain, sell-outs, season review.</summary>
    public class ImprovementsI71Tests
    {
        private ContentCatalog _c;
        private FranchiseSaveData _f;

        [SetUp]
        public void Setup()
        {
            _c = DefaultContent.Create();
            var league = _c.TeamsInTier(TeamTier.League);
            league.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            _f = Franchise.Create(_c, league[3].id, 71);
        }

        [Test]
        public void I71_GameLength_ChangesOnlyTheClock()
        {
            Assert.IsNull(Franchise.NextMatch(_f, _c, "difficulty.caller").RulesOverride, "standard uses the normal rules");
            FrontOffice.Of(_f).length = 1;
            var quick = Franchise.NextMatch(_f, _c, "difficulty.caller").RulesOverride;
            Assert.IsNotNull(quick);
            Assert.AreEqual(120f, quick.gameClockSeconds);
            var normal = _c.Find(_c.Rules, FullCourt.RulesId);
            Assert.AreEqual(240f, normal.gameClockSeconds, "the catalog's rules are untouched");
            Assert.AreEqual(normal.beyondArcPoints, quick.beyondArcPoints);
            Assert.AreEqual(normal.id, quick.id);
            FrontOffice.Of(_f).length = 2;
            Assert.AreEqual(360f, Franchise.NextMatch(_f, _c, "difficulty.caller").RulesOverride.gameClockSeconds);
            var m = new MatchSimulation(MatchSetup.FromRequest(Franchise.NextMatch(_f, _c, "difficulty.caller"), _c));
            Assert.AreEqual(360f, m.Setup.Rules.gameClockSeconds);
        }

        [Test]
        public void I72_ClubRecords_TrackStreakMarginAndPoints()
        {
            var o = FrontOffice.Of(_f);
            Franchise.RecordYourGame(_f, _c, 40, 30);
            Assert.AreEqual(1, o.bestStreak);
            Assert.AreEqual(10, o.biggestWin);
            Assert.AreEqual(40, o.mostPoints);
            Franchise.RecordYourGame(_f, _c, 33, 31);
            Assert.AreEqual(2, o.bestStreak);
            Assert.AreEqual(10, o.biggestWin);
            Assert.AreEqual(40, o.mostPoints);
            Franchise.RecordYourGame(_f, _c, 20, 30);
            Assert.AreEqual(2, o.bestStreak);
            Assert.GreaterOrEqual(FrontOffice.RecordLines(_f).Count, 4);
        }

        [Test]
        public void I73_RivalGames_PayMore()
        {
            var g = Franchise.NextGame(_f);
            string you = Franchise.TeamId(_f.you);
            int opp = Franchise.IndexOf(g.homeId == you ? g.awayId : g.homeId);
            var o = FrontOffice.Of(_f);
            o.rival = opp;
            int before = o.credits;
            Franchise.RecordYourGame(_f, _c, 30, 20);
            Assert.GreaterOrEqual(o.credits - before, 1 + 2 + 2, "a win over the rival pays two extra");
        }

        [Test]
        public void I73_LosingInThePlayoffs_MakesARival()
        {
            for (int guard = 0; guard < 4 && _f.phase != FranchisePhase.Draft; guard++) Franchise.SimToEnd(_f, _c);
            var o = FrontOffice.Of(_f);
            if (_f.titles > 0) Assert.AreEqual(-1, o.rival, "champions have nobody to blame");
            else if (_f.history.Exists(h => h.team == _f.you && h.finish >= 1)) Assert.GreaterOrEqual(o.rival, 0);
            Assert.AreNotEqual(_f.you, o.rival);
        }

        [Test]
        public void I74_OwnerGoal_IsSetOnTheFirstGame_AndJudgedAtTheEnd()
        {
            var o = FrontOffice.Of(_f);
            Assert.AreEqual(-1, o.goal);
            Franchise.SimNext(_f, _c);
            Assert.GreaterOrEqual(o.goal, 0);
            Assert.IsFalse(string.IsNullOrEmpty(FrontOffice.GoalText(_f)));
            Franchise.SimToEnd(_f, _c);
            Franchise.SimToEnd(_f, _c);
            Assert.AreEqual(FranchisePhase.Draft, _f.phase);
            Assert.AreEqual(-1, o.goal, "cleared for next season");
            Assert.GreaterOrEqual(o.review.Count, 4);
            Assert.IsTrue(o.review[0].Contains("MET") || o.review[0].Contains("MISSED"), o.review[0]);
        }

        [Test]
        public void I75_UnhappyPlayer_AsksForATrade_AndTalkingHelps()
        {
            var o = FrontOffice.Of(_f);
            var r = Franchise.Roster(_f, _f.you);
            var sad = r[r.Count - 1];
            FrontOffice.AddMood(sad, -100);
            for (int i = 0; i < 3 && o.tradeAsk < 0; i++) Franchise.SimNext(_f, _c);
            Assert.AreEqual(sad.id, o.tradeAsk);
            o.credits = 0;
            Assert.IsFalse(FrontOffice.TalkItOut(_f), "it costs credits");
            o.credits = 10;
            Assert.IsTrue(FrontOffice.TalkItOut(_f));
            Assert.AreEqual(10 - FrontOffice.TalkCost, o.credits);
            Assert.IsFalse(FrontOffice.TalkItOut(_f), "once a season");
        }

        [Test]
        public void I75_TradingHimAway_ClearsTheRequest()
        {
            var o = FrontOffice.Of(_f);
            var r = Franchise.Roster(_f, _f.you);
            var sad = r[r.Count - 1];
            o.tradeAsk = sad.id;
            _f.teams[_f.you].roster.Remove(sad.id);
            sad.team = -1;
            Franchise.SimNext(_f, _c);
            Assert.AreEqual(-1, o.tradeAsk);
        }

        [Test]
        public void I76_Captain_MustBeYours_AndSteadiesWhenHappy()
        {
            var r = Franchise.Roster(_f, _f.you);
            Assert.IsNull(FrontOffice.Captain(_f));
            Assert.IsFalse(FrontOffice.CaptainSteadies(_f));
            int other = Franchise.Roster(_f, (_f.you + 1) % Franchise.Teams)[0].id;
            Assert.IsFalse(FrontOffice.SetCaptain(_f, other));
            Assert.IsTrue(FrontOffice.SetCaptain(_f, r[0].id));
            Assert.AreEqual(r[0].id, FrontOffice.Captain(_f).id);
            Assert.IsTrue(FrontOffice.CaptainSteadies(_f));
            FrontOffice.AddMood(r[0], -100);
            Assert.IsFalse(FrontOffice.CaptainSteadies(_f));
        }

        [Test]
        public void I77_Sellouts_AtEightyFans()
        {
            Assert.IsFalse(FrontOffice.IsSellout(_f));
            Assert.AreEqual(0f, FrontOffice.HomeEdge(_f, _f.you));
            FrontOffice.AddFans(_f, FrontOffice.SelloutFans - FrontOffice.BaseFans);
            Assert.IsTrue(FrontOffice.IsSellout(_f));
            Assert.Greater(FrontOffice.HomeEdge(_f, _f.you), 0f);
            Assert.AreEqual(0f, FrontOffice.HomeEdge(_f, (_f.you + 1) % Franchise.Teams));
        }

        [Test]
        public void I78_I80_Save_RoundTrips_TheNewFields()
        {
            Franchise.RecordYourGame(_f, _c, 41, 30);
            var o = FrontOffice.Of(_f);
            o.length = 2; o.rival = 3; o.captain = Franchise.Roster(_f, _f.you)[0].id; o.tradeAsk = 5; o.talked = true;
            o.review.Add("line one");
            var back = FrontOffice.Of(SaveCodec.Decode(SaveCodec.Encode(new CareerSaveData { franchise = _f }), _c, out _).franchise);
            Assert.AreEqual(2, back.length);
            Assert.AreEqual(3, back.rival);
            Assert.AreEqual(o.captain, back.captain);
            Assert.AreEqual(5, back.tradeAsk);
            Assert.IsTrue(back.talked);
            Assert.AreEqual(o.goal, back.goal);
            Assert.AreEqual(o.goalYear, back.goalYear);
            Assert.AreEqual(41, back.mostPoints);
            Assert.AreEqual(11, back.biggestWin);
            Assert.AreEqual(o.seasonCredits, back.seasonCredits);
            Assert.AreEqual("line one", back.review[0]);
        }

        [Test]
        public void I80_EveryNewLabel_HasSpanish()
        {
            foreach (var s in FrontOffice.LengthNames) Assert.IsTrue(Loc.Has(s), s);
            foreach (var s in FrontOffice.GoalNames) Assert.IsTrue(Loc.Has(s), s);
            foreach (var s in new[] { "CLUB RECORDS", "SEASON REVIEW", "TRADE REQUEST", "TALK IT OUT", "MAKE CAPTAIN", "OWNER'S GOAL", "GAME LENGTH" })
                Assert.IsTrue(Loc.Has(s), s);
        }

        [Test]
        public void I80_ThreeSeasons_StillRunCleanly()
        {
            for (int year = 0; year < 3; year++)
            {
                Franchise.SimToEnd(_f, _c);
                Franchise.SimToEnd(_f, _c);
                Assert.AreEqual(FranchisePhase.Draft, _f.phase);
                Franchise.DraftUntilYou(_f);
                if (Franchise.OnTheClock(_f) == _f.you) Franchise.Pick(_f, Franchise.Prospects(_f)[0].id);
                foreach (var p in Franchise.Expiring(_f)) Franchise.ReSign(_f, p.id);
                Franchise.FinishReSign(_f);
                foreach (var p in Franchise.FreeAgents(_f))
                    if (_f.teams[_f.you].roster.Count < Franchise.MinRoster && Franchise.CannotSign(_f, p) == null) Franchise.SignFreeAgent(_f, p.id);
                Franchise.FinishFreeAgency(_f, _c);
                while (Franchise.CannotStart(_f) != null && _f.teams[_f.you].roster.Count > Franchise.MaxRoster)
                    Franchise.Release(_f, _f.teams[_f.you].roster[_f.teams[_f.you].roster.Count - 1]);
                Assert.IsTrue(Franchise.StartSeason(_f, _c), Franchise.CannotStart(_f));
            }
            var o = FrontOffice.Of(_f);
            Assert.Greater(o.credits, 0);
            Assert.GreaterOrEqual(o.bestStreak, 1);
            Assert.IsTrue(FrontOffice.Fans(_f) >= 0 && FrontOffice.Fans(_f) <= 100);
        }
    }
}
