using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>Phase 34: the Skills Gauntlet.</summary>
    public class Phase34GauntletTests
    {
        [Test]
        public void SameFourDrillsForEveryone_ChangingDaily()
        {
            var a = Gauntlet.For(9800);
            var b = Gauntlet.For(9800);
            Assert.AreEqual(Gauntlet.Stations, a.Length);
            CollectionAssert.AllItemsAreUnique(a);
            for (int i = 0; i < a.Length; i++) Assert.AreEqual(a[i], b[i]);
            var seen = new HashSet<DrillKind>();
            bool changes = false;
            for (int day = 9800; day < 9830; day++)
            {
                var d = Gauntlet.For(day);
                foreach (var k in d) { seen.Add(k); Assert.Contains(k, Gauntlet.Pool); }
                if (d[0] != a[0]) changes = true;
            }
            Assert.IsTrue(changes, "the order changes from day to day");
            Assert.AreEqual(Gauntlet.Pool.Length, seen.Count, "every drill turns up");
        }

        [Test]
        public void Requests_ArePracticeDrills_ThatParseBack()
        {
            var r = Gauntlet.Request(9801, 2);
            Assert.AreEqual(GameMode.Practice, r.Mode);
            Assert.AreEqual((int)Gauntlet.For(9801)[2], r.Drill);
            Assert.AreEqual(Gauntlet.SeedFor(9801, 2), r.Seed);
            Assert.IsTrue(Gauntlet.TryParse(r.ContextId, out int day, out int station));
            Assert.AreEqual(9801, day);
            Assert.AreEqual(2, station);
            Assert.IsFalse(Gauntlet.TryParse("gauntlet:x:1", out _, out _));
            Assert.IsFalse(Gauntlet.TryParse("gauntlet:5:9", out _, out _));
            Assert.IsFalse(Gauntlet.TryParse("shootout:friend", out _, out _));
        }

        [Test]
        public void Points_RewardBetterResults_OnASimilarScale()
        {
            int shoot = Gauntlet.Points(DrillKind.FreeShoot, 18, 6, 0, 0, false, 0, 0);
            int pass = Gauntlet.Points(DrillKind.PassingTargets, 0, 0, 12, 0, false, 0, 0);
            int three = Gauntlet.Points(DrillKind.ThreePoint, 0, 0, 0, 0, false, 24, 0);
            int lockd = Gauntlet.Points(DrillKind.Lockdown, 0, 0, 0, 0, false, 0, 5);
            int lane = Gauntlet.Points(DrillKind.DribbleLane, 0, 0, 0, 12f, true, 0, 0);
            int world = Gauntlet.Points(DrillKind.AroundTheWorld, 0, 0, 0, 35f, true, 0, 0);
            foreach (int p in new[] { shoot, pass, three, lockd, lane, world }) Assert.IsTrue(p >= 200 && p <= 400, "good runs score 200-400, got " + p);
            Assert.Greater(Gauntlet.Points(DrillKind.DribbleLane, 0, 0, 0, 10f, true, 0, 0), lane, "faster is better");
            Assert.Greater(lane, Gauntlet.Points(DrillKind.DribbleLane, 0, 0, 0, 0f, false, 0, 0, 5), "finishing beats not finishing");
            Assert.AreEqual(60, Gauntlet.Points(DrillKind.DribbleLane, 0, 0, 0, 999f, true, 0, 0), "a slow finish still scores something");
        }

        [Test]
        public void A_Run_AddsUp_AndKeepsTodaysAndAllTimeBest()
        {
            var g = new GauntletSaveData();
            Assert.AreEqual(0, Gauntlet.NextStation(g, 9802));
            Gauntlet.Start(g, 9802);
            Assert.IsFalse(Gauntlet.Record(g, 9802, 0, 250));
            Assert.IsFalse(Gauntlet.Record(g, 9802, 0, 999), "a replayed station doesn't count twice");
            Assert.IsFalse(Gauntlet.Record(g, 9802, 2, 999), "nor a station out of order");
            Assert.IsFalse(Gauntlet.Record(g, 9802, 1, 300));
            Assert.IsFalse(Gauntlet.Record(g, 9802, 2, 200));
            Assert.IsTrue(Gauntlet.Record(g, 9802, 3, 250));
            Assert.AreEqual(1000, Gauntlet.Total(g));
            Assert.AreEqual(1000, g.todayBest);
            Assert.AreEqual(1000, g.best);
            Assert.AreEqual(Gauntlet.Stations, Gauntlet.NextStation(g, 9802), "finished");
            // A worse second run doesn't lower the bests; a new day resets today's.
            Gauntlet.Start(g, 9802);
            for (int i = 0; i < 4; i++) Gauntlet.Record(g, 9802, i, 100);
            Assert.AreEqual(1000, g.todayBest);
            Gauntlet.Start(g, 9803);
            for (int i = 0; i < 4; i++) Gauntlet.Record(g, 9803, i, 50);
            Assert.AreEqual(200, g.todayBest);
            Assert.AreEqual(1000, g.best);
            Assert.AreEqual(3, g.runs);
            // A stale run from yesterday restarts today.
            Assert.AreEqual(0, Gauntlet.NextStation(g, 9804));
        }

        [Test]
        public void Gauntlet_Saves_AndHasALeaderboard()
        {
            var c = DefaultContent.Create();
            var d = Career.New(c);
            d.gauntlet.day = 9805;
            d.gauntlet.points = new List<int> { 120, 340 };
            d.gauntlet.best = 1111;
            d.gauntlet.todayBest = 900;
            d.gauntlet.bestDay = 9805;
            d.gauntlet.runs = 7;
            var back = SaveCodec.Decode(SaveCodec.Encode(d), c, out var status).gauntlet;
            Assert.AreEqual(LoadStatus.Ok, status);
            Assert.AreEqual(9805, back.day);
            Assert.AreEqual(2, back.points.Count);
            Assert.AreEqual(340, back.points[1]);
            Assert.AreEqual(1111, back.best);
            Assert.AreEqual(7, back.runs);
            Assert.IsTrue(Achievements.Boards.Exists(b => b.Id == Gauntlet.LeaderboardId));
            Assert.AreEqual(1111L, Achievements.Boards.Find(b => b.Id == Gauntlet.LeaderboardId).Score(d));
        }
    }

    public class Phase34AiTests
    {
        /// <summary>AI offense vs an idle player: how often an AI teammate crowds the AI ball handler, and post moves made.</summary>
        public static void Measure(string difficulty, out float crowded, out int postMoves, out float fg)
        {
            var c = DefaultContent.Create();
            int crowdSteps = 0, offenseSteps = 0, made = 0, att = 0;
            postMoves = 0;
            for (uint s = 1; s <= 16; s++)
            {
                var r = MatchRequest.QuickCallDefault(c);
                r.Seed = s * 17;
                r.DifficultyId = difficulty;
                var m = new MatchSimulation(MatchSetup.FromRequest(r, c));
                int g = 0;
                while (!m.IsOver && g++ < 60 * 600)
                {
                    m.Step(1f / 60f, default);
                    if (m.OffenseTeam != 1 || !m.Ball.IsHeld || m.Phase != MatchPhase.Live) continue;
                    var h = m.Players[m.Ball.HolderIndex];
                    if (h.Team != 1) continue;
                    offenseSteps++;
                    for (int i = 0; i < m.Players.Length; i++)
                        if (i != h.Index && m.Players[i].Team == 1 && Vec2.Distance(m.Players[i].Position, h.Position) < 1.5f) { crowdSteps++; break; }
                }
                postMoves += m.PostMoves;
                for (int i = 0; i < m.Players.Length; i++)
                    if (m.Players[i].Team == 1) { made += m.Stats.players[i].fieldGoalsMade; att += m.Stats.players[i].fieldGoalsAttempted; }
            }
            crowded = crowdSteps / (float)System.Math.Max(1, offenseSteps);
            fg = made / (float)System.Math.Max(1, att);
        }

        [Test]
        public void AiOffense_SpacesTheFloor_AndPostsUp()
        {
            Measure("difficulty.legend", out float crowded, out int posts, out float fg);
            System.Console.WriteLine("legend: crowded " + crowded.ToString("0.000") + ", post moves " + posts + ", fg " + fg.ToString("0.000"));
            Assert.Less(crowded, 0.12f, "an AI teammate is rarely on top of the AI ball handler");
            Assert.Greater(posts, 0, "the AI's bigs make post moves");
        }

        [Test]
        public void ShotCallout_ReadsTheTimingAndChance()
        {
            Assert.AreEqual("GREEN  ·  91%", ShotModel.FeedbackLine(ShotFeedback.Green, TimingGrade.Green, 0.912f));
            Assert.AreEqual("CLEAN LOOK  ·  SLIGHTLY LATE  ·  54%", ShotModel.FeedbackLine(ShotFeedback.CleanLook, TimingGrade.SlightlyLate, 0.54f));
            Assert.AreEqual("TOO EARLY  ·  8%", ShotModel.FeedbackLine(ShotFeedback.TooEarly, TimingGrade.TooEarly, 0.08f));
            Assert.AreEqual("CONTESTED  ·  SLIGHTLY EARLY  ·  0%", ShotModel.FeedbackLine(ShotFeedback.Contested, TimingGrade.SlightlyEarly, -1f));
        }
    }

    public class Phase34DifficultyTests
    {
        [Test]
        public void PerModeDifficulty_OverridesOnlyWhatUsesTheMainSetting()
        {
            var c = DefaultContent.Create();
            var d = Career.New(c);
            var s = d.settings;
            s.difficultyId = "difficulty.caller";
            ModeDifficulty.Set(s, "RISE", "difficulty.legend");
            ModeDifficulty.Set(s, "THE PARK", "difficulty.rookie");
            Assert.AreEqual("difficulty.legend", ModeDifficulty.Get(s, "RISE"));

            var rise = new MatchRequest { Mode = GameMode.Rise, DifficultyId = s.difficultyId };
            Assert.AreEqual("difficulty.legend", ModeDifficulty.Resolve(s, rise, c));
            var park = new MatchRequest { Mode = GameMode.Street, DifficultyId = s.difficultyId };
            Assert.AreEqual("difficulty.rookie", ModeDifficulty.Resolve(s, park, c));
            var quick = new MatchRequest { Mode = GameMode.QuickCall, DifficultyId = s.difficultyId };
            Assert.AreEqual("difficulty.caller", ModeDifficulty.Resolve(s, quick, c), "no override: the main setting");
            var fixedOne = new MatchRequest { Mode = GameMode.Rise, DifficultyId = "difficulty.rookie" };
            Assert.AreEqual("difficulty.rookie", ModeDifficulty.Resolve(s, fixedOne, c), "a fixed difficulty is kept");
            var link = new MatchRequest { Mode = GameMode.Versus, DifficultyId = s.difficultyId, ContextId = "link" };
            Assert.AreEqual(s.difficultyId, ModeDifficulty.Resolve(s, link, c), "two-phone games keep the host's");
            Assert.IsNull(ModeDifficulty.GroupOf(GameMode.Practice));

            ModeDifficulty.Set(s, "RISE", null);
            Assert.IsNull(ModeDifficulty.Get(s, "RISE"), "back to DEFAULT");
            ModeDifficulty.Set(s, "EVENTS", "difficulty.nope");
            Assert.AreEqual(s.difficultyId, ModeDifficulty.Resolve(s, new MatchRequest { Mode = GameMode.Arcade, DifficultyId = s.difficultyId }, c), "unknown ids are ignored");

            var back = SaveCodec.Decode(SaveCodec.Encode(d), c, out _).settings;
            Assert.AreEqual("difficulty.rookie", ModeDifficulty.Get(back, "THE PARK"));
        }
    }

    public class Phase34SeasonSevenTests
    {
        [Test]
        public void CometCouriers_AreTheSeventhRival_WithStoryBadgesAndCourts()
        {
            var c = DefaultContent.Create();
            Assert.IsTrue(ContentValidator.Validate(c).IsValid, ContentValidator.Validate(c).ToString());
            var team = c.Team(DefaultContent.Rival7CrewId);
            Assert.IsNotNull(team);
            Assert.AreEqual(TeamTier.Rival, team.tier);
            Assert.IsNotNull(c.Court("court.tram_yard"));
            Assert.IsNotNull(c.Court("court.dispatch_roof"));
            Assert.IsNotNull(c.Player(DefaultContent.Rival7LeaderId));
            for (int season = 1; season <= 14; season++)
                Assert.AreEqual(season % 12 == 7, RivalEngine.RivalFor(season) == DefaultContent.Rival7CrewId, "season " + season);

            var d = Career.New(c);
            foreach (var id in new[] { Story.Intro, Story.CircuitCleared, Story.Season2 }) Story.MarkSeen(d, id);
            d.rise.stage = RiseStage.Season;
            d.rise.season = new SeasonSaveData { seasonNumber = 7, currentWeek = 6 };
            Assert.AreEqual(Story.Rival7Intro, Story.Pending(d));
            var s = new MatchSummary { mode = GameMode.Rival, humanTeam = 0, winner = 0, teamAId = DefaultContent.PlayerCrewId, teamBId = DefaultContent.Rival7CrewId };
            RivalEngine.ApplyResult(d, s);
            Assert.AreEqual(1, d.rival.courierWins);
            Assert.AreEqual(0, RivalEngine.StaticWins(d));
            Story.MarkSeen(d, Story.Rival7Intro);
            Assert.AreEqual(Story.Rival7Beaten, Story.Pending(d));
            Assert.IsTrue(Badges.All.Find(b => b.Id == "badge.couriers").Earned(d));
            d.rival.wins = 7;
            d.rival.sundownWins = d.rival.tideWins = d.rival.cranesWins = d.rival.cassetteWins = d.rival.keeperWins = 1;
            Assert.IsTrue(RivalEngine.BeatAllSeven(d));
            foreach (var b in Badges.All) { Assert.IsTrue(Loc.Has(b.Title), b.Title); Assert.IsTrue(Loc.Has(b.Description), b.Description); }
            Assert.AreEqual(1, SaveCodec.Decode(SaveCodec.Encode(d), c, out _).rival.courierWins);
        }

        [Test]
        public void SeasonSevenPass_AndWeekly_StartOnTheirDates()
        {
            Assert.AreEqual("cosmetic.pass.jersey.beacon", HoopsPass.GearFor(HoopsPass.FourSetsFrom, 5), "Season 6's season is unchanged");
            Assert.AreEqual("cosmetic.pass.jersey.courier", HoopsPass.GearFor(HoopsPass.FiveSetsFrom, 5));
            Assert.AreEqual(HoopsPass.FiveSetsFrom, HoopsPass.SeasonOf(DailyChallenges.DayNumber(new System.DateTime(2026, 11, 30))));
            Assert.AreEqual(CelebrationKind.VictoryLap, Flair.CelebrationFor("cosmetic.pass.celebration.victory_lap"));
            bool moved = false;
            for (float t = 0f; t < Flair.CelebrationSeconds; t += 0.05f) moved |= Flair.Celebration(CelebrationKind.VictoryLap, t).OffsetX != 0;
            Assert.IsTrue(moved);
            for (int week = Weekly.Season6Week; week < Weekly.Season7Week; week++)
                foreach (var g in Weekly.For(week)) Assert.AreNotEqual(WeeklyGoal.FullCourtWins, g.Goal, "not before Season 7");
            var won = new MatchSummary { mode = GameMode.Franchise, humanTeam = 0, winner = 0 };
            Assert.AreEqual(1, Weekly.Amount(new WeeklyChallenge { Goal = WeeklyGoal.FullCourtWins, Target = 2 }, won, false));
            won.mode = GameMode.QuickCall;
            Assert.AreEqual(0, Weekly.Amount(new WeeklyChallenge { Goal = WeeklyGoal.FullCourtWins, Target = 2 }, won, false));
        }
    }
}

