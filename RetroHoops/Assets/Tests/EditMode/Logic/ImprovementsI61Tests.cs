using System;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>1 Nov 2026, improvements I61–I70: the Franchise FRONT OFFICE (ideas from the retro football and soccer hits).</summary>
    public class ImprovementsI61Tests
    {
        private ContentCatalog _c;
        private FranchiseSaveData _f;

        [SetUp]
        public void Setup()
        {
            _c = DefaultContent.Create();
            var league = _c.TeamsInTier(TeamTier.League);
            league.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            _f = Franchise.Create(_c, league[1].id, 61);
        }

        [Test]
        public void I61_EveryGame_PaysCredits_AndOpensTheHuddle()
        {
            var o = FrontOffice.Of(_f);
            Assert.AreEqual(0, o.credits);
            Assert.IsFalse(o.huddle);
            Franchise.SimNext(_f, _c);
            Assert.AreEqual(1, o.games);
            Assert.GreaterOrEqual(o.credits, 1);
            Assert.IsTrue(o.huddle);
            int before = o.credits;
            Assert.IsTrue(FrontOffice.Huddle(_f, HuddleChoice.Owner));
            Assert.AreEqual(before + 3, o.credits);
            Assert.IsFalse(o.huddle);
            Assert.IsFalse(FrontOffice.Huddle(_f, HuddleChoice.Owner), "one pick per game");
        }

        [Test]
        public void I61_HuddleChoices_DoWhatTheySay()
        {
            Franchise.SimNext(_f, _c);
            float mood = FrontOffice.TeamMorale(_f, _f.you);
            FrontOffice.Huddle(_f, HuddleChoice.Staff);
            Assert.Greater(FrontOffice.TeamMorale(_f, _f.you), mood);
            Franchise.SimNext(_f, _c);
            int fans = FrontOffice.Fans(_f);
            FrontOffice.Huddle(_f, HuddleChoice.Fans);
            Assert.AreEqual(Math.Min(100, fans + 4), FrontOffice.Fans(_f));
        }

        [Test]
        public void I62_Facilities_CostCredits_AndCapAtMax()
        {
            Assert.IsFalse(FrontOffice.Upgrade(_f, Facility.Gym), "no credits yet");
            FrontOffice.Of(_f).credits = 1000;
            for (int i = 0; i < FrontOffice.MaxLevel; i++) Assert.IsTrue(FrontOffice.Upgrade(_f, Facility.Gym));
            Assert.AreEqual(FrontOffice.MaxLevel, FrontOffice.Level(_f, Facility.Gym));
            Assert.AreEqual(0, FrontOffice.UpgradeCost(_f, Facility.Gym));
            Assert.IsFalse(FrontOffice.Upgrade(_f, Facility.Gym));
            Assert.AreEqual(1000 - 6 - 12 - 18, FrontOffice.Of(_f).credits);
        }

        [Test]
        public void I62_GymGrowth_OnlyForYoungPlayersWithRoom()
        {
            var p = new FrPlayer { age = 21, potential = 90, attrs = new AttributeSet(60, 60, 60, 60, 60, 60, 60, 60) };
            Assert.AreEqual(0, FrontOffice.GymGrowth(_f, p));
            FrontOffice.Of(_f).gym = 3;
            Assert.AreEqual(2, FrontOffice.GymGrowth(_f, p));
            p.age = 29;
            Assert.AreEqual(0, FrontOffice.GymGrowth(_f, p));
            p.age = 21;
            p.potential = p.Overall;
            Assert.AreEqual(0, FrontOffice.GymGrowth(_f, p));
        }

        [Test]
        public void I62_FilmRoom_AddsScoutPoints()
        {
            FrontOffice.Of(_f).film = 2;
            Franchise.SimToEnd(_f, _c);
            Franchise.SimToEnd(_f, _c);
            Assert.AreEqual(FranchisePhase.Draft, _f.phase);
            Assert.AreEqual(Franchise.ScoutPointsPerDraft + 2, _f.scoutPoints);
        }

        [Test]
        public void I63_Morale_ClampsAndNames()
        {
            var p = new FrPlayer();
            Assert.AreEqual(FrontOffice.BaseMorale, FrontOffice.Morale(p));
            FrontOffice.AddMood(p, 100);
            Assert.AreEqual(100, FrontOffice.Morale(p));
            Assert.AreEqual("FIRED UP", FrontOffice.MoodName(FrontOffice.Morale(p)));
            FrontOffice.AddMood(p, -500);
            Assert.AreEqual(0, FrontOffice.Morale(p));
            Assert.AreEqual("WANTS OUT", FrontOffice.MoodName(0));
        }

        [Test]
        public void I63_DeepBench_GetsRestless_StartersStayHappier()
        {
            for (int i = 0; i < 6; i++) Franchise.SimNext(_f, _c);
            var r = Franchise.Roster(_f, _f.you);
            Assert.GreaterOrEqual(r.Count, 8);
            Assert.Greater(FrontOffice.Morale(r[0]), FrontOffice.Morale(r[r.Count - 1]));
        }

        [Test]
        public void I63_ReSignSalary_FollowsMood()
        {
            var p = new FrPlayer();
            Assert.AreEqual(100, FrontOffice.ReSignSalary(p, 100));
            FrontOffice.AddMood(p, 30);
            Assert.Less(FrontOffice.ReSignSalary(p, 100), 100);
            FrontOffice.AddMood(p, -80);
            Assert.Greater(FrontOffice.ReSignSalary(p, 100), 100);
        }

        [Test]
        public void I64_PressRoom_ComesEveryFewGames_AndAnswersApply()
        {
            var o = FrontOffice.Of(_f);
            for (int i = 0; i < 3 && o.press < 0; i++) Franchise.SimNext(_f, _c);
            Assert.GreaterOrEqual(o.press, 0, "the press want you by the third game");
            Assert.IsFalse(string.IsNullOrEmpty(FrontOffice.PressText(_f)));
            Assert.IsFalse(FrontOffice.PressText(_f).Contains("{P}"));
            Assert.IsTrue(FrontOffice.AnswerPress(_f, 0));
            Assert.AreEqual(-1, o.press);
            Assert.IsFalse(FrontOffice.AnswerPress(_f, 0));
        }

        [Test]
        public void I64_EveryQuestionAndEvent_IsWellFormed()
        {
            foreach (var q in FrontOffice.Questions)
            {
                Assert.AreEqual(3, q.answers.Length, q.text);
                Assert.AreEqual(3, q.effects.GetLength(0), q.text);
                Assert.AreEqual(4, q.effects.GetLength(1), q.text);
                foreach (var a in q.answers) Assert.IsTrue(Loc.Has(a), "Spanish for " + a);
                Assert.IsTrue(Loc.Has(q.text), "Spanish for " + q.text);
            }
            foreach (var e in FrontOffice.Events)
            {
                Assert.AreEqual(2, e.choices.Length, e.text);
                Assert.AreEqual(2, e.effects.GetLength(0), e.text);
                foreach (var ch in e.choices) Assert.IsTrue(Loc.Has(ch), "Spanish for " + ch);
                Assert.IsTrue(Loc.Has(e.text), "Spanish for " + e.text);
            }
        }

        [Test]
        public void I65_Fans_ClampAndMoveWithResults()
        {
            Assert.AreEqual(FrontOffice.BaseFans, FrontOffice.Fans(_f));
            FrontOffice.AddFans(_f, 500);
            Assert.AreEqual(100, FrontOffice.Fans(_f));
            FrontOffice.AddFans(_f, -500);
            Assert.AreEqual(0, FrontOffice.Fans(_f));
        }

        [Test]
        public void I66_LockerRoom_ShowsUpDuringTheSeason_AndCostsAreChecked()
        {
            var o = FrontOffice.Of(_f);
            for (int i = 0; i < Franchise.Weeks && o.locker < 0; i++) Franchise.SimNext(_f, _c);
            Assert.GreaterOrEqual(o.locker, 0, "about one week in four");
            Assert.IsFalse(FrontOffice.LockerText(_f).Contains("{P}"));
            o.credits = 0;
            var e = FrontOffice.Events[o.locker];
            int paid = e.effects[0, 0] < 0 ? 0 : (e.effects[1, 0] < 0 ? 1 : -1);
            if (paid >= 0) Assert.IsFalse(FrontOffice.DecideLocker(_f, paid), "can't pay with no credits");
            int free = paid == 0 ? 1 : 0;
            Assert.IsTrue(FrontOffice.DecideLocker(_f, free));
            Assert.AreEqual(-1, o.locker);
        }

        [Test]
        public void I67_LeagueHeat_RisesWithTitles_ForTheOtherClubsOnly()
        {
            int other = (_f.you + 1) % Franchise.Teams;
            float you0 = Franchise.Strength(_f, _f.you), them0 = Franchise.Strength(_f, other);
            _f.titles = 2;
            Assert.AreEqual(2, FrontOffice.Heat(_f));
            Assert.AreEqual(you0, Franchise.Strength(_f, _f.you), 1e-4f);
            Assert.AreEqual(them0 + 1f, Franchise.Strength(_f, other), 1e-4f);
            _f.titles = 9;
            Assert.AreEqual(4, FrontOffice.Heat(_f));
        }

        [Test]
        public void I68_HappyRoom_ShootsATouchBetter_InPlayedGames()
        {
            Assert.AreEqual(0f, FrontOffice.ChemistryFor(_f), 1e-5f);
            foreach (var p in Franchise.Roster(_f, _f.you)) FrontOffice.AddMood(p, 30);
            Assert.AreEqual(0.03f, FrontOffice.ChemistryFor(_f), 1e-4f);
            Assert.AreEqual(0.03f, Franchise.NextMatch(_f, _c, "difficulty.caller").ChemistryBonus, 1e-4f);
            foreach (var p in Franchise.Roster(_f, _f.you)) FrontOffice.AddMood(p, -100);
            Assert.Less(FrontOffice.ChemistryFor(_f), 0f);
            Assert.GreaterOrEqual(FrontOffice.ChemistryFor(_f), -0.02f);
        }

        [Test]
        public void I69_PlayedGame_FeedsTheOfficeToo()
        {
            var o = FrontOffice.Of(_f);
            Assert.IsTrue(Franchise.RecordYourGame(_f, _c, 30, 20));
            Assert.AreEqual(1, o.games);
            Assert.AreEqual(1, o.streak);
            Assert.GreaterOrEqual(o.credits, 3);
            Assert.IsTrue(o.huddle);
        }

        [Test]
        public void I70_Save_RoundTrips_TheFrontOffice()
        {
            for (int i = 0; i < 4; i++) Franchise.SimNext(_f, _c);
            var o = FrontOffice.Of(_f);
            o.gym = 2; o.arena = 1; o.credits = 17;
            Franchise.Roster(_f, _f.you)[0].mood = 12;
            var back = SaveCodec.Decode(SaveCodec.Encode(new CareerSaveData { franchise = _f }), _c, out _).franchise;
            var b = FrontOffice.Of(back);
            Assert.AreEqual(17, b.credits);
            Assert.AreEqual(2, b.gym);
            Assert.AreEqual(1, b.arena);
            Assert.AreEqual(o.games, b.games);
            Assert.AreEqual(o.fanDelta, b.fanDelta);
            Assert.AreEqual(o.huddle, b.huddle);
            Assert.AreEqual(o.press, b.press);
            Assert.AreEqual(o.streak, b.streak);
            Assert.AreEqual(12, Franchise.Player(back, Franchise.Roster(_f, _f.you)[0].id).mood);
        }

        [Test]
        public void I70_AFullSeason_StillRunsToTheDraft()
        {
            Franchise.SimToEnd(_f, _c);
            Assert.AreEqual(FranchisePhase.Playoffs, _f.phase);
            Franchise.SimToEnd(_f, _c);
            Assert.AreEqual(FranchisePhase.Draft, _f.phase);
            var o = FrontOffice.Of(_f);
            Assert.IsFalse(o.huddle, "the season's over: nothing left waiting");
            Assert.AreEqual(-1, o.press);
            Assert.Greater(o.credits, Franchise.Weeks);
        }
    }
}
