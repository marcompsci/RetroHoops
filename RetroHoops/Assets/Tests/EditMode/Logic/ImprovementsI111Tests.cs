using System;
using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>14 Nov 2026, improvements I111–I125: slots, trophies and the tools around a Franchise.</summary>
    public class ImprovementsI111Tests
    {
        private ContentCatalog _c;
        private FranchiseSaveData _f;

        [SetUp]
        public void Setup()
        {
            _c = DefaultContent.Create();
            _f = NewFranchise(5, 111);
            Franchise.Register(_c, _f);
        }

        private FranchiseSaveData NewFranchise(int club, uint seed)
        {
            var league = _c.TeamsInTier(TeamTier.League);
            league.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            return Franchise.Create(_c, league[club].id, seed);
        }

        private void FinishSeason()
        {
            Franchise.SimToEnd(_f, _c);
            Franchise.SimToEnd(_f, _c);
            Assert.AreEqual(FranchisePhase.Draft, _f.phase);
        }

        [Test]
        public void I111_Slots_ParkSwitchAndCapAtThree()
        {
            var d = new CareerSaveData { franchise = _f };
            Assert.AreEqual(1, FranchiseExtras.SlotsUsed(d));
            Assert.IsTrue(FranchiseExtras.Park(d));
            Assert.IsFalse(d.franchise.active);
            d.franchise = NewFranchise(1, 2);
            Assert.IsTrue(FranchiseExtras.Park(d));
            d.franchise = NewFranchise(2, 3);
            Assert.AreEqual(3, FranchiseExtras.SlotsUsed(d));
            Assert.IsFalse(FranchiseExtras.Park(d), "three is the most");
            var third = d.franchise;
            Assert.IsTrue(FranchiseExtras.Switch(d, 0));
            Assert.IsTrue(ReferenceEquals(_f, d.franchise));
            Assert.IsTrue(FranchiseExtras.Parked(d).Contains(third));
            Assert.AreEqual(3, FranchiseExtras.SlotsUsed(d));
            Assert.IsFalse(FranchiseExtras.Switch(d, 9));
        }

        [Test]
        public void I111_Slots_SaveAndLoad()
        {
            var d = new CareerSaveData { franchise = _f };
            FranchiseExtras.Park(d);
            d.franchise = NewFranchise(1, 2);
            var back = SaveCodec.Decode(SaveCodec.Encode(d), _c, out _);
            Assert.IsTrue(back.franchise.active);
            Assert.AreEqual(1, back.franchiseSlots.Count);
            Assert.AreEqual(_f.you, back.franchiseSlots[0].you);
            Assert.AreEqual(_f.players.Count, back.franchiseSlots[0].players.Count);
        }

        [Test]
        public void I112_Trophies_EarnOnce_PayOnce()
        {
            var d = new CareerSaveData { franchise = _f };
            FinishSeason();
            var first = Array.Find(FranchiseExtras.Trophies, t => t.Name == "FIRST WIN");
            Assert.IsTrue(FranchiseExtras.Earned(_f, first));
            int sp = d.signalPoints;
            Assert.AreEqual(first.Sp, FranchiseExtras.Claim(d, _f, first));
            Assert.AreEqual(sp + first.Sp, d.signalPoints);
            Assert.AreEqual(0, FranchiseExtras.Claim(d, _f, first), "once");
            var legend = Array.Find(FranchiseExtras.Trophies, t => t.Name == "LEGEND GM");
            Assert.AreEqual(0, FranchiseExtras.Claim(d, _f, legend), "not earned yet");
            var bits = new HashSet<int>();
            foreach (var t in FranchiseExtras.Trophies)
            {
                Assert.IsTrue(bits.Add(t.Bit), t.Name);
                Assert.IsTrue(Loc.Has(t.Name) && Loc.Has(t.Detail), t.Name);
            }
        }

        [Test]
        public void I113_I124_SeasonEnd_TimelineAndTitleStreak()
        {
            FinishSeason();
            var l = FranchiseLeague.Of(_f);
            Assert.AreEqual(1, l.timeline.Count);
            StringAssert.Contains(Loc.T("YEAR") + " " + (_f.year - 1), l.timeline[0]);
            Assert.AreEqual(_f.titles > 0 ? 1 : 0, l.titleStreak);
        }

        [Test]
        public void I114_ScoutingReport_HasOddsAndADangerMan()
        {
            var lines = FranchiseExtras.ScoutingReport(_f, _c);
            Assert.GreaterOrEqual(lines.Count, 2);
            StringAssert.Contains("%", lines[0]);
            StringAssert.Contains(Loc.T("DANGER MAN"), lines[1]);
        }

        [Test]
        public void I115_I120_YourGames_MakeRecapsAndHeadToHead()
        {
            var g = Franchise.NextGame(_f);
            int opp = Franchise.IndexOf(g.homeId == Franchise.TeamId(_f.you) ? g.awayId : g.homeId);
            Franchise.RecordYourGame(_f, _c, 33, 21);
            var h = FranchiseExtras.HeadToHead(_f, opp);
            Assert.AreEqual(1, h.wins);
            Assert.AreEqual(0, h.losses);
            Assert.IsTrue(FranchiseLeague.Of(_f).news.Exists(n => n.Contains("33-21")));
        }

        [Test]
        public void I116_TrainingCamp_OncePerPreseason_ForYoungPlayers()
        {
            var p = Franchise.Roster(_f, _f.you)[0];
            Assert.IsNotNull(FranchiseExtras.CannotCamp(_f, p), "not in season");
            _f.phase = FranchisePhase.Preseason;
            p.age = 30;
            Assert.IsNotNull(FranchiseExtras.CannotCamp(_f, p), "too old");
            p.age = 21;
            int before = p.Overall;
            Assert.IsTrue(FranchiseExtras.Camp(_f, p.id));
            Assert.GreaterOrEqual(p.Overall, before);
            Assert.IsFalse(FranchiseExtras.Camp(_f, p.id), "once a year");
        }

        [Test]
        public void I117_NextSeasonPayroll_CountsOnlyContractsThatRunOn()
        {
            var r = Franchise.Roster(_f, _f.you);
            foreach (var p in r) { p.years = 0; p.extension = 0; }
            Assert.AreEqual(0, FranchiseExtras.NextSeasonPayroll(_f));
            r[0].years = 2;
            r[1].extension = 111;
            Assert.AreEqual(r[0].salary + 111, FranchiseExtras.NextSeasonPayroll(_f));
        }

        [Test]
        public void I118_AutoOffSeason_GetsYouToTheNextSeason()
        {
            FinishSeason();
            var log = FranchiseExtras.AutoOffSeason(_f, _c);
            Assert.AreEqual(FranchisePhase.Regular, _f.phase, string.Join(" | ", log));
            Assert.GreaterOrEqual(_f.teams[_f.you].roster.Count, Franchise.MinRoster);
            Assert.LessOrEqual(_f.teams[_f.you].roster.Count, Franchise.MaxRoster);
            Assert.Greater(log.Count, 0);
        }

        [Test]
        public void I119_TeamStats_HaveRanks()
        {
            Assert.AreEqual(0, FranchiseExtras.TeamStats(_f, _f.you).pfRank);
            for (int i = 0; i < 4; i++) Franchise.SimNext(_f, _c);
            var s = FranchiseExtras.TeamStats(_f, _f.you);
            Assert.IsTrue(s.pfRank >= 1 && s.pfRank <= Franchise.Teams);
            Assert.IsTrue(s.paRank >= 1 && s.paRank <= Franchise.Teams);
            Assert.Greater(s.pf, 0f);
        }

        [Test]
        public void I121_AssistantGM_FlagsTheHuddleAndCapsAtThree()
        {
            Franchise.SimNext(_f, _c);
            var tips = FranchiseExtras.Tips(_f);
            Assert.LessOrEqual(tips.Count, 3);
            Assert.IsTrue(tips.Exists(t => t.Contains("huddle")));
        }

        [Test]
        public void I122_AwardRaces_ListContenders()
        {
            for (int i = 0; i < 5; i++) Franchise.SimNext(_f, _c);
            Assert.AreEqual(3, FranchiseExtras.Race(_f, "MVP").Count);
            Assert.AreEqual(3, FranchiseExtras.Race(_f, "DEFENSE").Count);
        }

        [Test]
        public void I123_ShareText_SaysWhatItShould()
        {
            string text = FranchiseExtras.ShareText(_f, _c);
            StringAssert.Contains("RETRO HOOPS", text);
            StringAssert.Contains(Loc.T("TROPHIES"), text);
            Assert.IsFalse(text.Contains("<"), "no rich-text tags in shared text");
        }

        [Test]
        public void I125_Save_RoundTrips_TheNewFields()
        {
            var l = FranchiseLeague.Of(_f);
            l.trophies = 7; l.trophiesClaimed = 3; l.titleStreak = 2; l.bestTitleStreak = 2; l.campYear = 1;
            l.timeline.Add("YEAR 0: 10-4");
            Franchise.RecordYourGame(_f, _c, 30, 20);
            var back = FranchiseLeague.Of(SaveCodec.Decode(SaveCodec.Encode(new CareerSaveData { franchise = _f }), _c, out _).franchise);
            Assert.AreEqual(7, back.trophies & 7);
            Assert.AreEqual(3, back.trophiesClaimed);
            Assert.AreEqual(2, back.titleStreak);
            Assert.AreEqual(1, back.campYear);
            Assert.AreEqual("YEAR 0: 10-4", back.timeline[0]);
            Assert.AreEqual(string.Join(",", l.h2hWins), string.Join(",", back.h2hWins));
        }
    }
}
