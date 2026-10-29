using System;
using System.Collections.Generic;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>28 Oct 2026 (Omari, BALLER WORLD W1–W10): regions, the comic, the guide, daily achievements, side jobs, Big Runs and the drive.</summary>
    public class BallerWorldTests
    {
        private static ContentCatalog C => _c ??= DefaultContent.Create();
        private static ContentCatalog _c;

        private static CareerSaveData NewCareer()
        {
            var d = Career.New(C);
            d.world = new CityWorldSaveData();
            d.city = new CitySaveData();
            return d;
        }

        private static MatchRequest CityGame(string district, string courtId = null, string context = null) =>
            new MatchRequest { Mode = GameMode.Streets, ReturnTo = CityDistricts.ReturnTag(district, "x"), CourtId = courtId, ContextId = context };

        // ------------------------------------------------------------------ regions and districts (W6–W10)

        [Test]
        public void EveryOutdoorDistrict_IsInExactlyOneRegion_AndEveryRegionHasARideAndABigRun()
        {
            foreach (var d in CityDistricts.All)
            {
                if (d.Interior) continue;
                int n = 0;
                foreach (var r in WorldRegions.All) if (Array.IndexOf(r.Districts, d.Id) >= 0) n++;
                Assert.AreEqual(1, n, d.Id);
            }
            foreach (var r in WorldRegions.All)
            {
                var main = CityDistricts.Map(r.Main);
                Assert.IsTrue(main.TryGetSpawn("ride", out var ride), r.Id + " has a ride spawn");
                Assert.IsFalse(CityMovement.Overlaps(main, ride.Position.x, ride.Position.y), r.Id);
                Assert.IsNotNull(CityGuide.PortalIn(r, "ride"), r.Id + " ride");
                Assert.IsNotNull(CityGuide.PortalIn(r, "bigrun"), r.Id + " big run poster");
                Assert.IsNotNull(CityBigRun.For(r.Id), r.Id + " big run");
                var (district, spawn) = HighwayDrive.Arrival(r.Id);
                Assert.AreEqual(r.Main, district);
                Assert.AreEqual("ride", spawn);
            }
            Assert.AreEqual(WorldRegions.BallerCity, WorldRegions.RegionOf(CityDistricts.Gym).Id, "indoors counts as the district outside");
            Assert.AreEqual(CityDistricts.Downtown, CityDistricts.OutsideOf(CityDistricts.Arena));
            Assert.AreEqual(WorldRegions.LasVegas, WorldRegions.RegionOf(CityDistricts.LvOldTown).Id);
        }

        [Test]
        public void RegionMaps_MatchTheirRoads_AndFastTravelStaysInTheRegion()
        {
            foreach (var spot in CityAtlas.RegionSpots)
                foreach (var p in CityDistricts.Map(spot.id).Portals)
                {
                    if (p.Kind != CityPortalKind.District) continue;
                    var to = Array.Find(CityAtlas.RegionSpots, x => x.id == p.Target);
                    Assert.IsNotNull(to.id, p.Id);
                    Assert.AreEqual(spot.region, to.region, p.Id);
                    Assert.AreEqual(1, Math.Abs(to.col - spot.col) + Math.Abs(to.row - spot.row), spot.id + " → " + p.Target);
                }
            var s = new CitySaveData();
            s.seen.AddRange(new[] { CityDistricts.FirstCallers, CityDistricts.NyUptown, CityDistricts.NyMidtown });
            Assert.IsTrue(CityAtlas.CanTravel(s, CityDistricts.NyUptown, CityDistricts.NyMidtown));
            Assert.IsFalse(CityAtlas.CanTravel(s, CityDistricts.NyUptown, CityDistricts.FirstCallers), "other regions are a drive");
            Assert.AreEqual(2, CityAtlas.SpotsFor(WorldRegions.TheBay).Count);
            Assert.AreEqual(CityAtlas.Spots.Length, CityAtlas.SpotsFor(WorldRegions.BallerCity).Count);
        }

        [Test]
        public void EachNewRegion_HasSixCourtsWithLooks_AndFourCrews_TwoOfEachTier()
        {
            foreach (var r in WorldRegions.All)
            {
                if (r.Id == WorldRegions.BallerCity) continue;
                int courts = 0;
                foreach (var d in r.Districts)
                    foreach (var k in CityDistricts.Map(d).Courts)
                    {
                        courts++;
                        Assert.IsNotNull(C.Court(k.CourtId), k.CourtId);
                        Assert.AreEqual(CourtCircuit.Blacktop, C.Court(k.CourtId).circuit);
                    }
                Assert.AreEqual(6, courts, r.Id);
                var crews = WorldRegions.Crews(r);
                Assert.AreEqual(4, crews.Count, r.Id);
                Assert.AreEqual(2, crews.FindAll(c => c.Tier == CityCrewTier.Legendary).Count, r.Id);
            }
            CollectionAssert.AllItemsAreUnique(Array.ConvertAll(CityCrews.All, c => c.Id));
            CollectionAssert.AllItemsAreUnique(Array.ConvertAll(CityCrews.All, c => c.CourtId));
            foreach (var crew in CityCrews.All) CityCrews.Register(C, crew);
            CityCrews.Register(C, CityStory.VelvetSquad);
            Assert.IsTrue(ContentValidator.Validate(C).IsValid, ContentValidator.Validate(C).ToString());
        }

        [Test]
        public void OldGoals_DontMove_TheArenaDrawsOnlyBallerCityCrews()
        {
            Assert.AreEqual(9, CityMissions.LegendaryCount, "ALL-CITY still counts Baller City's nine LEGENDARY crews");
            for (int day = 9000; day < 9030; day++)
            {
                var (semi, final) = CityArena.Draw(day);
                Assert.IsTrue(CityMissions.InBallerCity(semi) && CityMissions.InBallerCity(final), "day " + day);
            }
        }

        [Test]
        public void RegionBuildingsAndProps_Draw()
        {
            for (int style = CityDistricts.Brownstone; style <= CityDistricts.Warehouse; style++)
                foreach (bool lit in new[] { false, true })
                {
                    var c = CityArtGenerator.Building(new CityBuilding { Id = "t", X = 0, Y = 0, W = 9, H = 8, Style = style, DoorX = 4, Sign = "SHOP", Seed = 7 }, lit);
                    int opaque = 0;
                    foreach (var px in c.Pixels) if (px.a == 255) opaque++;
                    Assert.Greater(opaque, c.Pixels.Length * 9 / 10, "style " + style + " fills its footprint");
                }
            var dayArt = CityArtGenerator.Prop(new CityProp { Kind = CityPropKind.NeonSign, Variant = 1 }, false, out _, out _);
            var nightArt = CityArtGenerator.Prop(new CityProp { Kind = CityPropKind.NeonSign, Variant = 1 }, true, out _, out _);
            Assert.AreNotEqual(dayArt.Pixels, nightArt.Pixels);
            Assert.IsFalse(ArraysEqual(dayArt.Pixels, nightArt.Pixels), "neon glows at night");
        }

        private static bool ArraysEqual(RgbColor[] a, RgbColor[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (!a[i].Equals(b[i])) return false;
            return true;
        }

        // ------------------------------------------------------------------ W1 the comic

        [Test]
        public void TheComic_EveryGoalPointsAtSomethingReal()
        {
            Assert.AreEqual(11, CityStory.Count);
            for (int i = 0; i < CityStory.Issues.Length; i++)
            {
                var issue = CityStory.Issues[i];
                Assert.AreEqual(i + 1, issue.Number);
                Assert.IsNotNull(WorldRegions.Find(issue.Region), issue.Title);
                Assert.Greater(issue.Intro.Length, 0);
                Assert.Greater(issue.Outro.Length, 0);
                Assert.Greater(issue.Goals.Length, 0);
                foreach (var p in issue.Intro) if (p.Speaker != null && p.Speaker.StartsWith("crew:")) Assert.IsNotNull(CityCrews.Find(p.Speaker.Substring(5)), p.Speaker);
                foreach (var g in issue.Goals)
                    switch (g.Kind)
                    {
                        case ComicGoalKind.WinAt: Assert.IsNotNull(CityDistricts.CourtHome(g.Ref), g.Ref); break;
                        case ComicGoalKind.CrewsIn:
                        case ComicGoalKind.Arrive:
                        case ComicGoalKind.SideJobs:
                        case ComicGoalKind.BigRun:
                            Assert.IsNotNull(WorldRegions.Find(g.Ref), g.Text);
                            break;
                    }
            }
            // Each region opens with the issue that sends you there.
            foreach (var r in WorldRegions.All)
                if (r.OpensAtIssue > 0)
                    Assert.AreEqual(r.Id, CityStory.Issue(r.OpensAtIssue).Region, r.Id);
        }

        [Test]
        public void TheComic_MovesStepByStep_PaysOnce_AndOpensTheRoad()
        {
            var d = NewCareer();
            CityStory.StartStep(d);
            Assert.IsTrue(CityStory.NeedsIntro(d.world));
            CityStory.MarkIntroShown(d.world);
            Assert.IsFalse(CityStory.NeedsIntro(d.world));
            Assert.IsFalse(WorldRegions.Open(d, WorldRegions.Find(WorldRegions.NewYork)), "New York waits for issue #3");

            // Issue 1: talk to 2, win 1, beat a BALLER crew.
            CityMissions.NoteTalk(d.city, "a");
            Assert.AreEqual(0, CityStory.Check(d).GoalsDone.Count);
            CityMissions.NoteTalk(d.city, "b");
            var ev = CityStory.Check(d);
            Assert.AreEqual(1, ev.GoalsDone.Count);
            d.city.cityWins = 1;
            d.city.crewsBeaten.Add("splash");
            long cash0 = d.streetCash.cash;
            int sp0 = d.signalPoints;
            ev = CityStory.Check(d);
            Assert.AreEqual(2, ev.GoalsDone.Count, "goals already met tick in a row");
            Assert.IsNotNull(ev.Finished);
            Assert.AreEqual(1, ev.Finished.Number);
            Assert.AreEqual(2, ev.Opened.Number);
            Assert.AreEqual(cash0 + CityStory.Issue(1).Cash, d.streetCash.cash);
            Assert.AreEqual(sp0 + CityStory.Issue(1).Sp, d.signalPoints);
            Assert.AreEqual(1, d.world.pendingOutro);
            Assert.IsFalse(CityStory.Check(d).Any, "paid once");

            // Issue 2 "win 3 more" counts from now, not from the old total.
            d.city.cityWins += 2;
            Assert.AreEqual(0, CityStory.Check(d).GoalsDone.Count);
            d.city.cityWins += 1;
            Assert.AreEqual(1, CityStory.Check(d).GoalsDone.Count);
            Assert.AreEqual(ComicGoalKind.BigRun, CityStory.CurrentGoal(d.world).Kind);

            // Winning Baller City's Big Run finishes issue 2 and opens the road to New York.
            WinBigRun(d, WorldRegions.BallerCity, 100);
            ev = CityStory.Check(d);
            Assert.AreEqual(2, ev.Finished.Number);
            Assert.IsTrue(WorldRegions.Open(d, WorldRegions.Find(WorldRegions.NewYork)));
            Assert.IsFalse(WorldRegions.Open(d, WorldRegions.Find(WorldRegions.LosAngeles)));
            Assert.AreEqual(ComicGoalKind.Arrive, CityStory.CurrentGoal(d.world).Kind);
            WorldRegions.NoteArrive(d.world, CityDistricts.NyMidtown);
            Assert.AreEqual(1, CityStory.Check(d).GoalsDone.Count);
        }

        [Test]
        public void TheFinale_TheVelvetInvitational()
        {
            var d = NewCareer();
            d.world.issue = 11;
            d.world.step = 1;
            Assert.IsTrue(CityStory.VelvetOpen(d));
            var r = CityStory.VelvetRequest(C, d);
            Assert.AreEqual(CityStory.VelvetContext, r.ContextId);
            Assert.AreEqual(CityArena.CourtId, r.CourtId);
            Assert.AreEqual(CityCrews.LegendaryDifficultyId, r.DifficultyId);
            Assert.IsTrue(CityStory.ApplyGame(d, r, false));
            Assert.IsFalse(d.world.velvetBeaten);
            Assert.IsTrue(CityStory.ApplyGame(d, r, true));
            var ev = CityStory.Check(d);
            Assert.AreEqual(11, ev.Finished.Number);
            Assert.IsTrue(CityStory.Finished(d.world));
            Assert.IsNull(ev.Opened);
            Assert.IsFalse(CityStory.ApplyGame(d, CityGame(CityDistricts.Downtown), true), "other games aren't the finale");
        }

        // ------------------------------------------------------------------ W3 daily achievements

        [Test]
        public void DailyAchievements_SixADay_SameAllDay_PaidByTier_OnlyOnce()
        {
            for (int day = 9000; day < 9040; day++)
            {
                var a = CityAchievements.For(day);
                Assert.AreEqual(CityAchievements.PerDay, a.Count);
                var kinds = new HashSet<AchKind>();
                foreach (var x in a) Assert.IsTrue(kinds.Add(x.Kind), "day " + day + " repeats " + x.Kind);
                Assert.AreEqual(a[0].Kind, CityAchievements.For(day)[0].Kind);
            }
            var d = NewCareer();
            int day0 = 9100;
            var w = d.world;
            CityAchievements.Roll(w, day0);
            // Max out every tally: all six done.
            w.tWalk = w.tTalk = w.tWins = w.tGames = w.tPoints = w.tAssists = w.tStops = w.tCrews = w.tDrives = w.tSide = w.tNear = 1000;
            w.tRegions.AddRange(new[] { WorldRegions.BallerCity, WorldRegions.NewYork });
            long cash0 = d.streetCash.cash;
            var ev = CityAchievements.Update(d, day0);
            Assert.AreEqual(6, ev.Done.Count);
            Assert.AreEqual(3, ev.Tier, "gold");
            long tiers = CityAchievements.TierCash[0] + CityAchievements.TierCash[1] + CityAchievements.TierCash[2];
            Assert.GreaterOrEqual(d.streetCash.cash - cash0, tiers);
            Assert.AreEqual(1, w.achGoldDays);
            Assert.AreEqual(1, w.achStreak);
            var again = CityAchievements.Update(d, day0);
            Assert.AreEqual(0, again.Done.Count);
            Assert.AreEqual(0, again.Tier);
            // Tomorrow keeps the streak; skipping a day resets it.
            CityAchievements.Roll(w, day0 + 1);
            Assert.AreEqual(0, w.tWins, "a new day clears the tallies");
            w.tWalk = w.tTalk = w.tWins = w.tGames = w.tPoints = w.tAssists = w.tStops = w.tCrews = w.tDrives = w.tSide = w.tNear = 1000;
            CityAchievements.Update(d, day0 + 1);
            Assert.AreEqual(2, w.achStreak);
            CityAchievements.Roll(w, day0 + 3);
            w.tWalk = w.tTalk = w.tWins = w.tGames = w.tPoints = w.tAssists = w.tStops = w.tCrews = w.tDrives = w.tSide = w.tNear = 1000;
            CityAchievements.Update(d, day0 + 3);
            Assert.AreEqual(1, w.achStreak, "a missed day starts over");
            Assert.AreEqual(2, w.achBestStreak);
            Assert.IsTrue(w.badges.Contains("first_gold"));
        }

        [Test]
        public void DailyAchievements_CountGamesWalksAndDrives()
        {
            var w = new CityWorldSaveData();
            CityAchievements.NoteGame(w, 5, true, new PlayerStatLine { points = 14, assists = 3, steals = 2, blocks = 1 }, true, WorldRegions.NewYork);
            CityAchievements.NoteGame(w, 5, false, new PlayerStatLine { points = 6 }, false, WorldRegions.NewYork);
            Assert.AreEqual(2, w.tGames);
            Assert.AreEqual(1, w.tWins);
            Assert.AreEqual(1, w.tCrews);
            Assert.AreEqual(20, w.tPoints);
            Assert.AreEqual(3, w.tStops);
            Assert.AreEqual(1, w.tRegions.Count);
            Assert.AreEqual(1, w.RegionWins(WorldRegions.NewYork));
            CityAchievements.NoteWalk(w, 5, 0.6f);
            CityAchievements.NoteWalk(w, 5, 0.6f);
            Assert.AreEqual(1, w.tWalk, "part metres add up");
            CityAchievements.NoteWalk(w, 5, float.NaN);
            CityAchievements.NoteDrive(w, 5, 4);
            Assert.AreEqual(1, w.tDrives);
            Assert.AreEqual(4, w.tNear);
        }

        // ------------------------------------------------------------------ W4 side jobs

        [Test]
        public void SideJobs_AreWhereTheySay_AndEverySpotIsReachable()
        {
            CollectionAssert.AllItemsAreUnique(Array.ConvertAll(CitySideJobs.All, j => j.Id));
            Assert.GreaterOrEqual(CitySideJobs.All.Length, 27);
            foreach (var j in CitySideJobs.All)
            {
                var m = CityDistricts.Map(j.District);
                Assert.AreEqual(j.Region, WorldRegions.RegionOf(j.District).Id, j.Id);
                Assert.IsFalse(CityMovement.Overlaps(m, j.At.x, j.At.y), j.Id + " giver stands in something");
                var start = m.DefaultSpawn.Position;
                Assert.Greater(CityPath.Find(m, start, j.At, m.Width * m.Height).Count, 0, j.Id + " giver unreachable");
                foreach (var s in j.Spots)
                {
                    Assert.IsTrue(m.Walkable(s), j.Id + " spot " + s.x + "," + s.y + " isn't walkable");
                    Assert.Greater(CityPath.Find(m, start, s, m.Width * m.Height).Count, 0, j.Id + " spot unreachable " + s.x + "," + s.y);
                }
                switch (j.Kind)
                {
                    case SideJobKind.BeatCrew: Assert.IsNotNull(CityCrews.Find(j.Ref), j.Id); break;
                    case SideJobKind.WinAt:
                    case SideJobKind.ScoreAt:
                    case SideJobKind.MarginAt: Assert.IsNotNull(CityDistricts.CourtHome(j.Ref), j.Id); break;
                    case SideJobKind.Errand: Assert.IsTrue(j.Spots.Length >= 1 && j.Target > 0, j.Id); break;
                    case SideJobKind.Collect: Assert.AreEqual(j.Target, j.Spots.Length, j.Id); break;
                }
                Assert.IsNotNull(CitySideJobs.Destination(j, new CityWorldSaveData()), j.Id + " has somewhere to go");
            }
            // Every region has side jobs in each district.
            foreach (var r in WorldRegions.All)
                if (r.Id != WorldRegions.BallerCity)
                    foreach (var d in r.Districts) Assert.GreaterOrEqual(CitySideJobs.InDistrict(d).Count, 3, d);
        }

        [Test]
        public void SideJobs_AcceptFinishAndPay()
        {
            var d = NewCareer();
            var cage = CitySideJobs.Find("ny.cage");
            Assert.IsNotNull(CitySideJobs.Locked(d, cage), "New York isn't open yet");
            d.world.regionsSeen.Add(WorldRegions.NewYork);
            Assert.IsNull(CitySideJobs.Locked(d, cage));
            Assert.IsTrue(CitySideJobs.Accept(d, cage));
            Assert.AreEqual("side:ny.cage", d.world.tracking);
            Assert.IsNull(CitySideJobs.ApplyGame(d, CityGame(CityDistricts.NyUptown, "court.ny_brownstone"), true, 3, null, 5), "wrong court");
            Assert.IsNull(CitySideJobs.ApplyGame(d, CityGame(CityDistricts.NyUptown, "court.ny_cage"), false, 0, null, 5), "a loss");
            long cash0 = d.streetCash.cash;
            Assert.AreEqual(cage, CitySideJobs.ApplyGame(d, CityGame(CityDistricts.NyUptown, "court.ny_cage"), true, 2, null, 5));
            Assert.AreEqual(cash0 + cage.Cash, d.streetCash.cash);
            Assert.IsTrue(CitySideJobs.Done(d.world, cage));
            Assert.IsNull(CitySideJobs.Active(d.world));
            Assert.AreEqual(1, CitySideJobs.DoneIn(d.world, WorldRegions.NewYork));
            Assert.AreEqual(1, d.world.tSide);
            Assert.IsNotNull(CitySideJobs.Locked(d, cage), "done is done");
            Assert.AreEqual("", d.world.tracking, "the guide goes back to the comic");
        }

        [Test]
        public void SideJobs_ErrandsBeatTheClock_CollectingFindsEverything()
        {
            var d = NewCareer();
            var lucky = CitySideJobs.Find("bc.lucky"); // pick up in the park, bring it back
            Assert.IsTrue(CitySideJobs.Accept(d, lucky));
            var w = d.world;
            Assert.AreEqual(ErrandStep.Nothing, CitySideJobs.StepErrand(w, lucky, lucky.At, 1f));
            Assert.AreEqual(ErrandStep.PickedUp, CitySideJobs.StepErrand(w, lucky, lucky.Spots[0], 5f));
            Assert.AreEqual(ErrandStep.TimeUp, CitySideJobs.StepErrand(w, lucky, lucky.Spots[1], lucky.Target + 1f));
            Assert.AreEqual(ErrandStep.Delivered, CitySideJobs.StepErrand(w, lucky, lucky.Spots[1], 20f));

            var lost = CitySideJobs.Find("bc.lost");
            Assert.IsTrue(CitySideJobs.Accept(d, lost));
            Assert.AreEqual(-1, CitySideJobs.StepCollect(w, lost, new Vec2(0.5f, 0.5f)));
            for (int i = 0; i < lost.Spots.Length; i++)
            {
                Assert.IsFalse(CitySideJobs.CollectDone(w, lost));
                Assert.AreEqual(i, CitySideJobs.StepCollect(w, lost, lost.Spots[i]));
                Assert.AreEqual(-1, CitySideJobs.StepCollect(w, lost, lost.Spots[i]), "each one once");
            }
            Assert.IsTrue(CitySideJobs.CollectDone(w, lost));
            Assert.AreEqual(lost, CitySideJobs.Complete(d, 9));
        }

        // ------------------------------------------------------------------ W5 Big Runs

        private static void WinBigRun(CareerSaveData d, string region, int day)
        {
            foreach (var crew in WorldRegions.Crews(WorldRegions.Find(region))) { d.city.crewsBeaten.Add(crew.Id); break; }
            for (int round = 1; round <= CityBigRun.RoundsCount; round++)
            {
                var r = CityBigRun.Request(C, d, region, day);
                Assert.IsNotNull(r, region + " round " + round);
                CityBigRun.ApplyGame(d, r, true, day, DayOfWeek.Wednesday, out _);
            }
        }

        [Test]
        public void BigRun_ThreeRounds_PaysEachRound_CrownsYou_OnceADay()
        {
            var d = NewCareer();
            string ny = WorldRegions.NewYork;
            d.world.regionsSeen.Add(ny);
            Assert.IsNotNull(CityBigRun.Locked(d, ny), "you need a crew win in New York first");
            d.city.crewsBeaten.Add("stoop");
            Assert.IsNull(CityBigRun.Locked(d, ny));
            var run = CityBigRun.For(ny);
            Assert.AreEqual(CityCrewTier.Legendary, CityBigRun.CrewFor(run, 3).Tier, "the final is a LEGENDARY crew");
            long cash0 = d.streetCash.cash;
            for (int round = 1; round <= 3; round++)
            {
                Assert.AreEqual(round, CityBigRun.NextRound(d.world, ny, 50));
                var r = CityBigRun.Request(C, d, ny, 50);
                Assert.AreEqual(CityBigRun.CrewFor(run, round), CityCrews.FromContext(r.ContextId));
                Assert.AreEqual((ny, round), CityBigRun.Of(r).Value);
                CityBigRun.ApplyGame(d, r, true, 50, DayOfWeek.Tuesday, out long paid);
                Assert.AreEqual(run.Purse[round - 1], paid);
            }
            Assert.AreEqual(cash0 + run.Purse[0] + run.Purse[1] + run.Purse[2], d.streetCash.cash);
            Assert.AreEqual(1, CityBigRun.Titles(d.world, ny));
            Assert.AreEqual(0, CityBigRun.NextRound(d.world, ny, 50), "one run a day");
            Assert.AreEqual(1, CityBigRun.NextRound(d.world, ny, 51), "a new bracket tomorrow");
            // A loss ends today's run.
            var r2 = CityBigRun.Request(C, d, ny, 51);
            CityBigRun.ApplyGame(d, r2, false, 51, DayOfWeek.Saturday, out long none);
            Assert.AreEqual(0, none);
            Assert.AreEqual(0, CityBigRun.NextRound(d.world, ny, 51));
            Assert.AreEqual(2 * run.Purse[2], CityBigRun.Pay(run, 3, DayOfWeek.Sunday), "weekends pay double");
            Assert.IsTrue(CityAchievements.CheckBadges(d).Exists(b => b.Id == "king_ny"));
        }

        // ------------------------------------------------------------------ W2 the guide

        [Test]
        public void Guide_WalksYouToTheSpot_ThroughTheRoads_OrToYourRide()
        {
            var hop = CityGuide.NextHop(CityDistricts.FirstCallers, CityDistricts.Downtown);
            Assert.IsNotNull(hop);
            Assert.AreEqual(CityPortalKind.District, hop.Value.portal.Kind);
            Assert.IsNotNull(CityGuide.NextHop(CityDistricts.Gym, CityDistricts.Blacktop), "out of a building and on");
            Assert.IsNull(CityGuide.NextHop(CityDistricts.NyUptown, CityDistricts.FirstCallers), "no walking between regions");

            var d = NewCareer();
            d.world.issue = 3; // CONCRETE JUNGLE: drive to New York
            CityStory.StartStep(d);
            var hint = CityGuide.Hint(d, CityDistricts.FirstCallers);
            Assert.AreEqual(GuideKind.Drive, hint.Kind);
            Assert.AreEqual(WorldRegions.NewYork, hint.Region);
            Assert.IsTrue(hint.HasPoint, "points at your ride in First Callers");
            WorldRegions.NoteArrive(d.world, CityDistricts.NyUptown);
            CityStory.Check(d);
            hint = CityGuide.Hint(d, CityDistricts.NyMidtown); // win at THE CAGE (Uptown)
            Assert.AreEqual(GuideKind.Exit, hint.Kind);
            hint = CityGuide.Hint(d, CityDistricts.NyUptown);
            Assert.AreEqual(GuideKind.Spot, hint.Kind);
            Assert.IsTrue(CityDistricts.Map(CityDistricts.NyUptown).Court("court.ny_cage").Contains(hint.Point.x, hint.Point.y));
            d.world.tracking = "off";
            Assert.IsNull(CityGuide.Hint(d, CityDistricts.NyUptown).Text, "the guide can be turned off");
        }

        // ------------------------------------------------------------------ W6 the drive

        [Test]
        public void TheDrive_AlwaysUnder30Seconds_EvenDrivingIntoEveryCar()
        {
            foreach (var a in WorldRegions.All)
                foreach (var b in WorldRegions.All)
                {
                    if (a == b) continue;
                    for (uint seed = 1; seed <= 3; seed++)
                    {
                        float t = HighwayDrive.SimulateWorstCase(a.Id, b.Id, seed * 977u);
                        Assert.Less(t, 30f, a.Id + " → " + b.Id);
                        Assert.LessOrEqual(t, HighwayDrive.MaxSeconds + 0.05f);
                    }
                }
            // Cruising in an empty lane takes about 22 s; boosting is quicker.
            var cruise = new HighwayDrive(WorldRegions.BallerCity, WorldRegions.NewYork, 5);
            cruise.Traffic.Clear();
            while (!cruise.Done) cruise.Step(1f / 60f, 0f, false);
            Assert.AreEqual(HighwayDrive.TargetSeconds, cruise.Time, 1.5f);
            var boosted = new HighwayDrive(WorldRegions.BallerCity, WorldRegions.NewYork, 5);
            boosted.Traffic.Clear();
            while (!boosted.Done) boosted.Step(1f / 60f, 0f, true);
            Assert.Less(boosted.Time, cruise.Time);
            Assert.Greater(boosted.Mph, 0);
        }

        [Test]
        public void TheDrive_BumpsSlowYou_NearMissesCount_AndTheTripIsRecorded()
        {
            var d = new HighwayDrive(WorldRegions.BallerCity, WorldRegions.LasVegas, 1);
            d.Traffic.Clear();
            d.Traffic.Add(new DriveCar { Lane = 1, Y = 12f, Speed = 2f });
            d.Traffic.Add(new DriveCar { Lane = 2, Y = 40f, Speed = 2f });
            while (d.Distance < 30f) d.Step(1f / 60f, 0f, false);
            Assert.AreEqual(1, d.Bumps, "driving straight into the car in your lane");
            while (d.Distance < 60f) d.Step(1f / 60f, 0f, false);
            Assert.AreEqual(1, d.NearMisses, "passing a lane over is a near miss");
            while (!d.Done) d.Step(1f / 60f, 0f, false);

            var career = NewCareer();
            long cash0 = career.streetCash.cash;
            long paid = HighwayDrive.Finish(career, d, 77);
            Assert.AreEqual(HighwayDrive.NearMissPay, paid, "no clean bonus after a bump");
            Assert.AreEqual(cash0 + paid, career.streetCash.cash);
            Assert.AreEqual(1, career.world.drives);
            Assert.AreEqual(0, career.world.cleanDrives);
            Assert.AreEqual(d.Time, career.world.bestDrive, 1e-4);
            Assert.AreEqual(1, career.world.tDrives);
            Assert.AreEqual(WorldBiome.Suburbs, HighwayDrive.BiomeAt(WorldRegions.BallerCity, WorldRegions.LasVegas, 0.05f));
            Assert.AreEqual(WorldBiome.Desert, HighwayDrive.BiomeAt(WorldRegions.BallerCity, WorldRegions.LasVegas, 0.95f));
            Assert.IsTrue(HighwayDrive.OnBridge(WorldRegions.TheBay, 0.95f));
        }

        // ------------------------------------------------------------------ saves

        [Test]
        public void Save_RoundTripsTheWorld_AndDropsBadValues()
        {
            var d = NewCareer();
            var w = d.world;
            w.issue = 4; w.step = 1; w.stepBase = 7; w.issuesOpened.AddRange(new[] { 1, 2, 3, 4 }); w.started = true; w.pendingOutro = 3;
            w.tracking = "side:ny.cage"; w.sideActive = "ny.cage";
            w.achDay = 900; w.achProgress[2] = 2; w.achTierPaid = 1; w.achStreak = 4; w.achBestStreak = 6; w.achLastDay = 899; w.achGoldDays = 2; w.achTotal = 30;
            w.badges.Add("first_gold"); w.tWalk = 345; w.tRegions.Add(WorldRegions.NewYork); w.tNear = 3;
            w.sideDone.Add("bc.lucky"); w.runs.Add(WorldRegions.NewYork + "|900|2|-1|1");
            w.regionsSeen.AddRange(new[] { WorldRegions.BallerCity, WorldRegions.NewYork }); w.drives = 5; w.cleanDrives = 2; w.nearMisses = 40; w.bestDrive = 21.5f;
            w.AddRegionWin(WorldRegions.NewYork); w.walkedTotal = 12345;
            d.city.district = CityDistricts.NyUptown;
            var back = SaveCodec.Decode(SaveCodec.Encode(d), C, out _);
            var b = back.world;
            Assert.AreEqual(4, b.issue); Assert.AreEqual(1, b.step); Assert.AreEqual(7, b.stepBase); Assert.AreEqual(4, b.issuesOpened.Count); Assert.IsTrue(b.started);
            Assert.AreEqual(3, b.pendingOutro); Assert.AreEqual("side:ny.cage", b.tracking); Assert.AreEqual("ny.cage", b.sideActive);
            Assert.AreEqual(2, b.achProgress[2]); Assert.AreEqual(1, b.achTierPaid); Assert.AreEqual(4, b.achStreak); Assert.AreEqual(6, b.achBestStreak);
            Assert.AreEqual(2, b.achGoldDays); Assert.AreEqual(30, b.achTotal); Assert.IsTrue(b.badges.Contains("first_gold")); Assert.AreEqual(345, b.tWalk);
            Assert.AreEqual(3, b.tNear); Assert.IsTrue(b.sideDone.Contains("bc.lucky")); Assert.AreEqual(1, CityBigRun.Titles(b, WorldRegions.NewYork));
            Assert.AreEqual(2, b.regionsSeen.Count); Assert.AreEqual(5, b.drives); Assert.AreEqual(21.5f, b.bestDrive, 1e-4);
            Assert.AreEqual(1, b.RegionWins(WorldRegions.NewYork)); Assert.AreEqual(12345, b.walkedTotal);
            Assert.AreEqual(CityDistricts.NyUptown, back.city.district, "a region district is a real place to be");

            // Corrupt values fall back without costing the career.
            var root = (Dictionary<string, object>)MiniJson.Read(SaveCodec.Encode(d));
            var wd = (Dictionary<string, object>)root["world"];
            wd["is"] = 999.0;
            wd["sa"] = "no.such.job";
            wd["tr"] = "side:no.such.job";
            wd["bd"] = new List<object> { "fake_badge" };
            wd["bdt"] = -3.0;
            var bad = SaveCodec.Decode(MiniJson.Write(root), C, out _).world;
            Assert.AreEqual(CityStory.Count + 1, bad.issue, "past the end means finished, not broken");
            Assert.AreEqual("", bad.sideActive);
            Assert.AreEqual("", bad.tracking);
            Assert.IsFalse(bad.badges.Contains("fake_badge"));
            Assert.AreEqual(0f, bad.bestDrive);
            // Old saves without a world start at issue #1.
            var old = SaveCodec.Decode(SaveCodec.Encode(d).Replace("\"world\":", "\"worldX\":"), C, out _).world;
            Assert.AreEqual(1, old.issue);
            Assert.IsFalse(old.started);
        }
    }
}
