using System;
using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Phase 11: local 2-player, tutorial, Daily Challenge, Game Center ids.</summary>
    public class VersusTests
    {
        private const float Dt = 1f / 60f;
        private readonly ContentCatalog _c = DefaultContent.Create();

        private MatchSimulation Versus(uint seed = 5)
        {
            var r = MatchRequest.QuickCallDefault(_c);
            r.Mode = GameMode.Versus;
            r.Seed = seed;
            return new MatchSimulation(MatchSetup.FromRequest(r, _c));
        }

        [Test]
        public void Versus_SlotZeroOfEachTeamIsHuman()
        {
            var m = Versus();
            Assert.AreEqual(0, m.ControlledIndex);
            Assert.AreEqual(MatchSimulation.IndexOf(1, 0), m.SecondControlledIndex);
            Assert.IsTrue(m.Players[0].IsHuman);
            Assert.IsTrue(m.Players[m.SecondControlledIndex].IsHuman);
            Assert.AreEqual(m.SecondControlledIndex, m.HumanIndexOf(1));
        }

        [Test]
        public void SinglePlayer_HasNoSecondHuman()
        {
            var r = MatchRequest.QuickCallDefault(_c);
            var m = new MatchSimulation(MatchSetup.FromRequest(r, _c));
            Assert.AreEqual(-1, m.SecondControlledIndex);
            Assert.AreEqual(-1, m.HumanIndexOf(1));
        }

        [Test]
        public void Versus_Player2MovesOnlyWithTheirOwnInput()
        {
            var m = Versus();
            int p2 = m.SecondControlledIndex;
            var start = m.Players[p2].Position;
            for (int i = 0; i < 120; i++) m.Step(Dt, default, default);
            Assert.Less(Vec2.Distance(start, m.Players[p2].Position), 0.6f, "AI must not drive player 2");
            var p2Input = new PlayerInput { Move = new Vec2(1f, 0f) };
            for (int i = 0; i < 60; i++) m.Step(Dt, default, p2Input);
            Assert.Greater(m.Players[p2].Position.x, start.x + 1.5f);
        }

        [Test]
        public void Versus_Player2CanStealOnDefense()
        {
            var m = Versus();
            int p2 = m.SecondControlledIndex;
            bool attempted = false;
            for (int i = 0; i < 600 && !attempted; i++)
            {
                var holder = m.Holder;
                var toBall = holder == null ? Vec2.Zero : (holder.Position - m.Players[p2].Position);
                var input2 = new PlayerInput { Move = toBall.SqrMagnitude > 0.01f ? toBall.Normalized : Vec2.Zero, DefensePressed = i % 20 == 0 };
                m.Step(Dt, new PlayerInput { Move = new Vec2(0.3f, 0f) }, input2);
                foreach (var e in m.Events) if (e.Type == MatchEventType.StealAttempt && e.PlayerIndex == p2) attempted = true;
            }
            Assert.IsTrue(attempted);
        }

        [Test]
        public void Versus_PaysNoRewardsAndDoesNotCountInCareer()
        {
            var s = Fake.Summary(GameMode.Versus, true, "v1");
            Assert.AreEqual(0, Rewards.For(s, RewardTuning.Default).signalPoints);
            var d = Career.New(_c);
            Career.ApplyMatch(d, s, default);
            Assert.AreEqual(0, d.totals.games);
        }
    }

    public class TutorialTests
    {
        private const float Dt = 1f / 60f;
        private readonly ContentCatalog _c = DefaultContent.Create();

        private MatchSimulation Tutorial()
        {
            var r = MatchRequest.PracticeDefault();
            r.Mode = GameMode.Tutorial;
            r.Seed = 3;
            return new MatchSimulation(MatchSetup.FromRequest(r, _c));
        }

        [Test]
        public void Tutorial_EveryStepHasText()
        {
            for (int i = 0; i <= TutorialSession.StepCount; i++)
            {
                var step = (TutorialStep)i;
                Assert.IsNotEmpty(TutorialSession.TitleOf(step));
                Assert.IsNotEmpty(TutorialSession.TouchHintOf(step));
            }
        }

        [Test]
        public void Tutorial_WalksThroughEveryStepWithRealInputs()
        {
            var m = Tutorial();
            var t = new TutorialSession();
            int frames = 0;
            int hold = 0;
            while (!t.Finished && frames < 60 * 240)
            {
                var input = new PlayerInput();
                switch (t.Step)
                {
                    case TutorialStep.Move:
                        input.Move = new Vec2(frames % 240 < 120 ? 1f : -1f, 0f);
                        break;
                    case TutorialStep.Shoot:
                    case TutorialStep.Green:
                        if (m.HumanHasBall && m.ChargingIndex < 0 && hold == 0) { input.ShootPressed = true; input.ShootHeld = true; hold = 1; }
                        else if (hold > 0 && hold < 40) { input.ShootHeld = true; hold++; }
                        else hold = 0;
                        break;
                    case TutorialStep.Pass:
                        if (m.HumanHasBall && frames % 10 == 0) input.PassPressed = true;
                        break;
                    case TutorialStep.Ask:
                        if (!m.HumanHasBall && m.HumanTeamHasBall && frames % 10 == 0) input.PassPressed = true;
                        break;
                    case TutorialStep.Call:
                        if (m.HumanTeamHasBall && m.Phase == MatchPhase.Live && frames % 10 == 0) input.CallPlay = PlayCall.ClearOut;
                        break;
                    case TutorialStep.Steal:
                    {
                        var h = m.Holder;
                        if (h != null) input.Move = (h.Position - m.Controlled.Position).Normalized;
                        if (frames % 15 == 0) input.DefensePressed = true;
                        break;
                    }
                    case TutorialStep.Jump:
                        if (frames % 30 == 0) input.ShootPressed = true;
                        break;
                }
                m.Step(Dt, input);
                t.Update(m);
                frames++;
            }
            Assert.IsTrue(t.Finished, "stuck at " + t.Step);
        }

        [Test]
        public void CompleteTutorial_RewardsOnce()
        {
            var d = Career.New(_c);
            int sp = d.signalPoints;
            Assert.AreEqual(Career.TutorialReward, Career.CompleteTutorial(d));
            Assert.AreEqual(0, Career.CompleteTutorial(d));
            Assert.AreEqual(sp + Career.TutorialReward, d.signalPoints);
            Assert.IsTrue(d.tutorialDone);
        }
    }

    public class DailyTests
    {
        private readonly ContentCatalog _c = DefaultContent.Create();

        [Test]
        public void SameDay_SameChallenge_DifferentDaysVary()
        {
            var a = DailyChallenges.For(9000, _c);
            var b = DailyChallenges.For(9000, _c);
            Assert.AreEqual(a.Goal, b.Goal);
            Assert.AreEqual(a.Target, b.Target);
            Assert.AreEqual(a.OpponentId, b.OpponentId);
            var seen = new HashSet<string>();
            for (int d = 9000; d < 9030; d++)
            {
                var c = DailyChallenges.For(d, _c);
                seen.Add(c.Goal + ":" + c.OpponentId);
                Assert.AreNotEqual(c.HomeTeamId, c.OpponentId);
                Assert.IsNotNull(_c.Team(c.OpponentId));
                Assert.IsNotNull(_c.Difficulty(c.DifficultyId));
                Assert.IsNotEmpty(c.Describe());
                Assert.AreEqual(GameMode.Daily, c.ToRequest().Mode);
            }
            Assert.Greater(seen.Count, 10);
        }

        [Test]
        public void DayNumber_CountsCalendarDays()
        {
            int a = DailyChallenges.DayNumber(new DateTime(2026, 10, 1, 23, 59, 0));
            int b = DailyChallenges.DayNumber(new DateTime(2026, 10, 2, 0, 1, 0));
            Assert.AreEqual(a + 1, b);
        }

        [Test]
        public void IsMet_ChecksEachGoal()
        {
            var s = Fake.Summary(GameMode.Daily, true, "d", humanPoints: 10); // wins 21-15, 2 greens, 1 steal, 2 assists
            Assert.IsTrue(DailyChallenges.IsMet(new DailyChallenge { Goal = DailyGoal.WinBy, Target = 6 }, s));
            Assert.IsFalse(DailyChallenges.IsMet(new DailyChallenge { Goal = DailyGoal.WinBy, Target = 7 }, s));
            Assert.IsTrue(DailyChallenges.IsMet(new DailyChallenge { Goal = DailyGoal.Greens, Target = 2 }, s));
            Assert.IsFalse(DailyChallenges.IsMet(new DailyChallenge { Goal = DailyGoal.Steals, Target = 2 }, s));
            Assert.IsTrue(DailyChallenges.IsMet(new DailyChallenge { Goal = DailyGoal.Points, Target = 10 }, s));
            Assert.IsFalse(DailyChallenges.IsMet(new DailyChallenge { Goal = DailyGoal.Assists, Target = 3 }, s));
            Assert.IsTrue(DailyChallenges.IsMet(new DailyChallenge { Goal = DailyGoal.WinOnLegend }, s));
            Assert.IsFalse(DailyChallenges.IsMet(new DailyChallenge { Goal = DailyGoal.WinOnLegend }, Fake.Summary(GameMode.Daily, false, "l")));
        }

        [Test]
        public void Streak_GrowsOnConsecutiveDays_ResetsAfterAGap_PaysOncePerDay()
        {
            var save = new DailySaveData();
            var career = Career.New(_c);
            Assert.AreEqual(DailyChallenges.BaseBonus, DailyChallenges.Complete(save, career, 100));
            Assert.AreEqual(0, DailyChallenges.Complete(save, career, 100), "once per day");
            Assert.AreEqual(DailyChallenges.BaseBonus + DailyChallenges.BonusPerStreakDay, DailyChallenges.Complete(save, career, 101));
            Assert.AreEqual(2, save.streak);
            Assert.AreEqual(2, DailyChallenges.LiveStreak(save, 102));
            Assert.AreEqual(0, DailyChallenges.LiveStreak(save, 103), "missed a day");
            DailyChallenges.Complete(save, career, 104);
            Assert.AreEqual(1, save.streak);
            Assert.AreEqual(2, save.bestStreak);
            Assert.AreEqual(3, save.completed);
        }

        [Test]
        public void StreakBonus_IsCapped()
        {
            Assert.AreEqual(DailyChallenges.BonusFor(1 + DailyChallenges.MaxStreakBonusDays), DailyChallenges.BonusFor(50));
        }

        [Test]
        public void Daily_PaysQuickCallRate()
        {
            var t = RewardTuning.Default;
            Assert.AreEqual(Rewards.For(Fake.Summary(GameMode.QuickCall, true, "q"), t).signalPoints,
                            Rewards.For(Fake.Summary(GameMode.Daily, true, "d"), t).signalPoints);
        }
    }

    public class GameCenterLogicTests
    {
        private readonly ContentCatalog _c = DefaultContent.Create();

        [Test]
        public void FreshCareer_EarnsNothing()
        {
            Assert.AreEqual(0, Achievements.Earned(Career.New(_c)).Count);
        }

        [Test]
        public void Achievements_FollowCareerProgress()
        {
            var d = Career.New(_c);
            d.totals.wins = 10;
            d.totals.greens = 100;
            d.totals.championships = 1;
            d.classic.titles = 1;
            d.daily.bestStreak = 7;
            d.tutorialDone = true;
            d.rise.stage = RiseStage.Season;
            d.rise.seasonsPlayed = 5;
            d.totals.heatUps = 1;
            d.totals.alleyOops = 1;
            d.totals.versusGames = 1;
            d.secrets.arcade.clears = 1;
            foreach (var code in Secrets.All) d.secrets.codesFound.Add(code.Id);
            d.king.best = 5;
            d.cup.titles = 1;
            d.rival.wins = 5;
            d.rival.sundownWins = d.rival.tideWins = d.rival.cranesWins = d.rival.cassetteWins = 1;
            d.practice.shootoutWins = 1;
            d.practice.horseWins = 1;
            d.practice.aroundWorldTime = 42f;
            d.customTeam.created = true;
            d.franchise.titles = 1;
            d.allStar.dunkTitles = d.allStar.threeTitles = 1;
            // I170 (20 Nov 2026).
            d.totals.posters = 1; d.totals.rimHangs = 25; d.totals.steals = 25; d.loginBest = 7;
            var earned = Achievements.Earned(d);
            foreach (var id in Achievements.AllAchievements) Assert.Contains(id, earned);
        }

        [Test]
        public void Ids_AreUnique()
        {
            Assert.AreEqual(Achievements.AllAchievements.Length, new HashSet<string>(Achievements.AllAchievements).Count);
            Assert.AreEqual(Achievements.Boards.Count, Achievements.Scores(Career.New(_c)).Count);
        }

        [Test]
        public void NewFields_RoundTripThroughTheSaveFile()
        {
            var d = Career.New(_c);
            d.tutorialDone = true;
            d.settings.gameCenter = true;
            DailyChallenges.Complete(d.daily, d, 500);
            DailyChallenges.Complete(d.daily, d, 501);
            var back = SaveCodec.Decode(SaveCodec.Encode(d), _c, out _);
            Assert.IsTrue(back.tutorialDone);
            Assert.IsTrue(back.settings.gameCenter);
            Assert.AreEqual(501, back.daily.lastCompletedDay);
            Assert.AreEqual(2, back.daily.streak);
            Assert.AreEqual(2, back.daily.bestStreak);
        }

        [Test]
        public void OldSaves_WithoutNewFields_LoadWithDefaults()
        {
            var json = "{\"version\":1,\"nickname\":\"Ace\"}";
            var d = SaveCodec.Decode(json, _c, out var status);
            Assert.AreEqual(LoadStatus.Ok, status);
            Assert.IsFalse(d.tutorialDone);
            Assert.IsFalse(d.settings.gameCenter);
            Assert.AreEqual(-1, d.daily.lastCompletedDay);
            Assert.AreEqual(0, d.classic.titles);
        }
    }
}
