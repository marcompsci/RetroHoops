using System;
using System.Collections.Generic;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>17 Oct 2026: BALLER CITY part 2 — more courts with their own looks, Holiday Park, CPU crews (BALLER / LEGENDARY) and missions.</summary>
    public class BallerCityMissionsTests
    {
        private static ContentCatalog C => _c ??= DefaultContent.Create();
        private static ContentCatalog _c;

        private static IEnumerable<CityCourt> AllCourts()
        {
            foreach (var d in CityDistricts.All)
                foreach (var k in CityDistricts.Map(d.Id).Courts) yield return k;
        }

        // ------------------------------------------------------------------ courts

        [Test]
        public void NewDistricts_AreReachableFromFirstCallers_AndBack()
        {
            var fc = CityDistricts.Map(CityDistricts.FirstCallers);
            foreach (var (road, district) in new[] { ("road.heights", CityDistricts.HoopHeights), ("road.holiday", CityDistricts.HolidayPark) })
            {
                var p = fc.Portal(road);
                Assert.IsNotNull(p, road);
                Assert.AreEqual(district, p.Target);
                var there = CityDistricts.Map(district);
                Assert.IsTrue(there.TryGetSpawn(p.TargetSpawn, out var arrive));
                Assert.IsNull(CityInteraction.RoadAt(there, arrive.Position), "you don't land on the road back");
                var back = there.Portals.Find(x => x.Kind == CityPortalKind.District && x.Target == CityDistricts.FirstCallers);
                Assert.IsNotNull(back, district);
                Assert.IsTrue(fc.TryGetSpawn(back.TargetSpawn, out var home));
                Assert.IsNull(CityInteraction.RoadAt(fc, home.Position));
            }
        }

        [Test]
        public void TheCityHasSixteenCourts_EachWithItsOwnLook()
        {
            var courts = new List<CityCourt>(AllCourts());
            Assert.GreaterOrEqual(courts.Count, 16);
            var looks = new HashSet<string>();
            courts.RemoveAll(k => k.Use == CityCourtUse.Practice); // the gym's training floor isn't a game court
            foreach (var k in courts)
            {
                var def = C.Court(k.CourtId);
                string colors = def == null ? "city" : def.floor + "/" + def.lines + "/" + def.paint;
                Assert.IsTrue(looks.Add(k.Look.Floor + "|" + k.Look.Theme + "|" + k.Look.Neon + "|" + k.Look.Logo + "|" + colors), k.Id + " looks like another court");
            }
            // A spread of floors, not one kind.
            var floors = new HashSet<int>();
            foreach (var k in courts) floors.Add(k.Look.Floor);
            Assert.GreaterOrEqual(floors.Count, 5);
        }

        [Test]
        public void HolidayPark_HasAllFourHolidayCourts_Dressed_AndPlayable()
        {
            var m = CityDistricts.Map(CityDistricts.HolidayPark);
            var d = new CareerSaveData();
            foreach (var h in Holidays.All)
            {
                var court = m.Courts.Find(k => k.CourtId == h.CourtId);
                Assert.IsNotNull(court, h.Name);
                Assert.AreEqual(h.Theme, court.Look.Theme);
                Assert.AreEqual(CityCourtUse.Holiday, court.Use);
                var list = CityActivities.ForCourt(m, court, d, C, 9000);
                Assert.AreEqual("HOLIDAY GAME", list[0].Label);
                Assert.AreEqual(h.CourtId, list[0].Request.CourtId);
                Assert.IsTrue(CityDistricts.TryParseReturn(list[0].Request.ReturnTo, out var dd, out var ss));
                Assert.AreEqual(CityDistricts.HolidayPark, dd);
                Assert.AreEqual(court.Id, ss);
            }
            Assert.IsTrue(m.Props.Exists(p => p.Kind == CityPropKind.PineLit));
            Assert.IsTrue(m.Props.Exists(p => p.Kind == CityPropKind.Pumpkin));
            Assert.IsTrue(m.Props.Exists(p => p.Kind == CityPropKind.Egg));
            Assert.IsTrue(m.Props.Exists(p => p.Kind == CityPropKind.Bunting));
        }

        [Test]
        public void PickupOnANewCourt_IsPlayedOnThatCourt()
        {
            var m = CityDistricts.Map(CityDistricts.HoopHeights);
            foreach (var court in m.Courts)
            {
                var list = CityActivities.ForCourt(m, court, new CareerSaveData(), C, 1);
                Assert.AreEqual("PICKUP 3 ON 3", list[0].Label);
                Assert.AreEqual(court.CourtId, list[0].Request.CourtId, court.Id);
            }
        }

        [Test]
        public void EveryDistrict_AndEveryPropKind_Draws()
        {
            foreach (var d in CityDistricts.All)
            {
                var m = CityDistricts.Map(d.Id);
                var g = CityArtGenerator.Ground(m, 7, id => { var k = C.Court(id); return k == null ? ((RgbColor, RgbColor, RgbColor)?)null : (k.floor, k.lines, k.paint); });
                Assert.AreEqual(m.Width * CityArtGenerator.Tile, g.Width);
                foreach (var px in g.Pixels) Assert.AreEqual(255, px.a, d.Id + ": the ground is fully opaque");
            }
            foreach (CityPropKind kind in Enum.GetValues(typeof(CityPropKind)))
                foreach (bool lit in new[] { false, true })
                {
                    var c = CityArtGenerator.Prop(new CityProp { Kind = kind, W = 3, H = 1, Variant = 2 }, lit, out _, out _);
                    int painted = 0;
                    foreach (var px in c.Pixels) if (px.a > 0) painted++;
                    Assert.Greater(painted, 20, kind + " draws something");
                }
        }

        // ------------------------------------------------------------------ crews and difficulty

        [Test]
        public void BallerAndLegendary_AreHarderThanLegend_ButFair_AndNotInTheUsualPickers()
        {
            var legend = C.Difficulty("difficulty.legend");
            var baller = C.Difficulty(CityCrews.BallerDifficultyId);
            var legendary = C.Difficulty(CityCrews.LegendaryDifficultyId);
            Assert.IsNotNull(baller);
            Assert.IsNotNull(legendary);
            Assert.Less(baller.reactionTime, legend.reactionTime);
            Assert.Less(legendary.reactionTime, baller.reactionTime);
            Assert.Greater(baller.releaseAccuracy, legend.releaseAccuracy);
            Assert.Greater(legendary.releaseAccuracy, baller.releaseAccuracy);
            Assert.Less(legendary.errorRate, baller.errorRate);
            Assert.LessOrEqual(legendary.movementScale, 1f, "never extra speed");
            foreach (var crew in CityCrews.All) CityCrews.Register(C, crew);
            foreach (var ch in Street.Challengers) Street.Register(C, ch);
            Assert.IsTrue(ContentValidator.Validate(C).IsValid, ContentValidator.Validate(C).ToString());
            var picks = CityCrews.Pickable(C);
            Assert.IsFalse(picks.Exists(x => CityCrews.IsCityDifficulty(x.id)));
            Assert.AreEqual(3, picks.Count);
        }

        [Test]
        public void EveryCrew_HasACityCourt_ThreePlayersOfItsStyle_AndTheRightRating()
        {
            var ids = new HashSet<string>();
            var courts = new HashSet<string>();
            foreach (var crew in CityCrews.All)
            {
                Assert.IsTrue(ids.Add(crew.Id));
                Assert.IsTrue(courts.Add(crew.CourtId), crew.Id + ": one crew per court");
                Assert.IsNotNull(CityDistricts.CourtHome(crew.CourtId), crew.Id + " plays somewhere in the city");
                var team = CityCrews.Register(C, crew);
                Assert.AreEqual(3, team.rosterPlayerIds.Count);
                int target = crew.Tier == CityCrewTier.Legendary ? CityCrews.LegendaryRating : CityCrews.BallerRating;
                for (int k = 0; k < 3; k++)
                {
                    var p = C.Player(team.rosterPlayerIds[k]);
                    Assert.AreEqual(crew.Roster[k], C.ArchetypeById(p.archetypeId).archetype, crew.Id + " plays its style");
                    Assert.LessOrEqual(Math.Abs(p.attributes.Overall - (target + (k == 0 ? 3 : 0))), 4, crew.Id + " rated for its tier (ratings top out at 99)");
                }
                Assert.AreEqual(crew.Scheme, team.scheme);
                // Registering twice changes nothing.
                Assert.AreEqual(3, CityCrews.Register(C, crew).rosterPlayerIds.Count);
            }
            Assert.GreaterOrEqual(CityCrews.All.Length, 12);
            var styles = new HashSet<string>();
            foreach (var crew in CityCrews.All) styles.Add(crew.Style);
            Assert.GreaterOrEqual(styles.Count, 8, "lots of different ways to play");
            // Every court in the two new districts has a crew.
            foreach (var d in new[] { CityDistricts.HoopHeights, CityDistricts.HolidayPark })
                foreach (var k in CityDistricts.Map(d).Courts) Assert.IsNotNull(CityCrews.AtCourt(k.CourtId), k.Id);
        }

        [Test]
        public void CrewGames_AreStreetsRules_OnTheirCourt_AtTheirTier()
        {
            var d = new CareerSaveData();
            foreach (var crew in CityCrews.All)
            {
                var r = CityCrews.Challenge(C, d, crew);
                Assert.AreEqual(GameMode.Streets, r.Mode);
                Assert.AreEqual(crew.CourtId, r.CourtId);
                Assert.AreEqual(crew.TeamId, r.AwayTeamId);
                Assert.AreEqual(CityCrews.DifficultyFor(crew), r.DifficultyId);
                Assert.IsTrue(ReferenceEquals(crew, CityCrews.FromContext(r.ContextId)));
            }
        }

        [Test]
        public void LegendaryCrews_OpenAfterThreeBallerWins()
        {
            var s = new CitySaveData();
            var legendary = Array.Find(CityCrews.All, x => x.Tier == CityCrewTier.Legendary);
            var ballers = Array.FindAll(CityCrews.All, x => x.Tier == CityCrewTier.Baller);
            Assert.IsFalse(CityCrews.Unlocked(s, legendary));
            StringAssertContains(CityCrews.LockedReason(s, legendary), "0/3");
            for (int i = 0; i < 3; i++)
            {
                Assert.IsFalse(CityCrews.Unlocked(s, legendary));
                s.crewsBeaten.Add(ballers[i].Id);
            }
            Assert.IsTrue(CityCrews.Unlocked(s, legendary));
            Assert.IsNull(CityCrews.LockedReason(s, legendary));

            // On the court card: locked first, then a real game.
            var d = new CareerSaveData();
            var home = CityDistricts.CourtHome(legendary.CourtId).Value;
            var m = CityDistricts.Map(home.district);
            var court = m.Courts.Find(k => k.CourtId == legendary.CourtId);
            var locked = CityActivities.ForCourt(m, court, d, C, 1).Find(a => a.Label.Contains(legendary.Name));
            Assert.IsNotNull(locked.Locked);
            Assert.IsNull(locked.Request);
            d.city = s;
            var open = CityActivities.ForCourt(m, court, d, C, 1).Find(a => a.Label.Contains(legendary.Name));
            Assert.IsNull(open.Locked);
            Assert.AreEqual(CityCrews.LegendaryDifficultyId, open.Request.DifficultyId);
        }

        [Test]
        public void CrewsWalkTheirCourts_AndYouCanCallThemOut()
        {
            var d = new CareerSaveData();
            var cast = CityNpcScheduler.Cast(d, C);
            var crew = cast.FindAll(n => n.Role == CityNpcRole.Crew);
            Assert.AreEqual(CityCrews.All.Length * 3, crew.Count);
            var npc = crew.Find(n => n.Ref == "splash");
            var m = CityDistricts.Map(CityDistricts.HoopHeights);
            var acts = CityActivities.ForNpc(m, npc, d, C, m.Hangouts["court.coin_op"]);
            Assert.AreEqual(1, acts.Count);
            Assert.AreEqual(CityCrews.BallerDifficultyId, acts[0].Request.DifficultyId);
            Assert.IsFalse(string.IsNullOrEmpty(CityDialogue.Line(npc, d, 5)));
            // More people out in Hoop Heights in the afternoon than before (crews hang out on their courts).
            int day = CityNpcScheduler.DayNumber(new DateTime(2026, 10, 17));
            Assert.Greater(CityNpcScheduler.InDistrict(cast, CityDistricts.HoopHeights, day, CityTimeOfDay.Afternoon).Count, 6);
        }

        [Test]
        public void ALegendaryCrew_BeatsSomeoneWhoDoesNothing()
        {
            var c = DefaultContent.Create();
            var crew = Array.Find(CityCrews.All, x => x.Tier == CityCrewTier.Legendary);
            var r = CityCrews.Challenge(c, new CareerSaveData(), crew);
            r.Seed = 77;
            var m = new MatchSimulation(MatchSetup.FromRequest(r, c));
            int g = 0;
            while (!m.IsOver && g++ < 60 * 900) m.Step(1f / 60f, default);
            Assert.IsTrue(m.IsOver, "the game finishes");
            Assert.AreEqual(1, m.Winner, "the crew wins");
        }

        // ------------------------------------------------------------------ missions

        [Test]
        public void Missions_AreWellFormed_InFiveChapters()
        {
            var ids = new HashSet<string>();
            for (int ch = 1; ch <= CityMissions.Chapters; ch++)
                Assert.IsTrue(Array.Exists(CityMissions.All, m => m.Chapter == ch), "chapter " + ch);
            foreach (var m in CityMissions.All)
            {
                Assert.IsTrue(ids.Add(m.Id), m.Id);
                Assert.Greater(m.Sp, 0);
                Assert.Greater(CityMissions.TargetOf(m), 0, m.Id);
                if (m.Kind == CityMissionKind.Crew) Assert.IsNotNull(CityCrews.Find(m.Ref), m.Id);
                if (m.Kind == CityMissionKind.Visit) Assert.IsNotNull(CityDistricts.Find(m.Ref), m.Id);
            }
            var s = new CitySaveData();
            Assert.AreEqual(1, CityMissions.CurrentChapter(s));
            Assert.AreEqual(3, CityMissions.Active(s).Count);
        }

        [Test]
        public void Missions_PayOnce_AndOpenTheNextChapter()
        {
            var d = new CareerSaveData();
            int sp0 = d.signalPoints;
            d.city.seen.Add(CityDistricts.Blacktop);
            var done = CityMissions.Check(d);
            Assert.AreEqual(1, done.Count);
            Assert.AreEqual("c1.blacktop", done[0].Id);
            Assert.AreEqual(sp0 + done[0].Sp, d.signalPoints);
            Assert.AreEqual(0, CityMissions.Check(d).Count, "pays once");
            Assert.AreEqual(sp0 + done[0].Sp, d.signalPoints);

            Assert.IsTrue(CityMissions.NoteTalk(d.city, "mate.a"));
            Assert.IsFalse(CityMissions.NoteTalk(d.city, "mate.a"), "the same person twice counts once");
            CityMissions.NoteTalk(d.city, "mate.b");
            CityMissions.NoteTalk(d.city, "rival.c");
            d.city.seen.Add(CityDistricts.HoopHeights); // already met chapter 2's walk: it completes when chapter 2 opens
            var win = Pickup("court.sunset_cage");
            var r = CityMissions.ApplyGame(d, win, true, 5, new PlayerStatLine { points = 6 });
            Assert.AreEqual(1, d.city.cityWins);
            Assert.IsTrue(r.Completed.Exists(m => m.Id == "c1.talk"));
            Assert.IsTrue(r.Completed.Exists(m => m.Id == "c1.firstwin"));
            Assert.IsTrue(r.Completed.Exists(m => m.Id == "c2.heights"), "the next chapter opened and its met mission paid");
            Assert.AreEqual(2, CityMissions.CurrentChapter(d.city));
            StringAssertContains(r.Note(), "MISSION COMPLETE");
        }

        [Test]
        public void ACityGame_RecordsCourtsCrewsAndBestLines_ButNotOtherGames()
        {
            var d = new CareerSaveData();
            var crew = CityCrews.Find("splash");
            var req = CityCrews.Challenge(C, d, crew);
            req.ReturnTo = CityDistricts.ReturnTag(CityDistricts.HoopHeights, "court.coin_op");
            var r = CityMissions.ApplyGame(d, req, true, 9, new PlayerStatLine { points = 14, arcMade = 5, assists = 4, steals = 2, blocks = 2, ankleBreakers = 2 });
            Assert.IsTrue(ReferenceEquals(crew, r.Crew));
            Assert.IsTrue(r.CrewFirstWin);
            Assert.IsTrue(d.city.crewsBeaten.Contains("splash"));
            Assert.IsTrue(d.city.courtsWon.Contains("court.coin_op"));
            Assert.AreEqual(14, d.city.bestPoints);
            Assert.AreEqual(5, d.city.bestThrees);
            Assert.AreEqual(4, d.city.bestStops);
            Assert.AreEqual(9, d.city.bestMargin);
            // Beating them again isn't a "first".
            Assert.IsFalse(CityMissions.ApplyGame(d, req, true, 2, null).CrewFirstWin);
            // A loss: no crew, no court, still a game played.
            int games = d.city.cityGames;
            var lost = CityMissions.ApplyGame(d, Pickup("court.skate_bowl"), false, -4, null);
            Assert.IsNull(lost.Crew);
            Assert.IsFalse(d.city.courtsWon.Contains("court.skate_bowl"));
            Assert.AreEqual(games + 1, d.city.cityGames);
            // Games not started in the city, and practice, don't count.
            var outside = Pickup("court.pier_nine");
            outside.ReturnTo = null;
            CityMissions.ApplyGame(d, outside, true, 10, null);
            var drill = MatchRequest.PracticeDefault();
            drill.ReturnTo = CityDistricts.ReturnTag(CityDistricts.Gym, "gym.floor");
            CityMissions.ApplyGame(d, drill, true, 10, null);
            Assert.AreEqual(games + 1, d.city.cityGames);
            Assert.IsFalse(d.city.courtsWon.Contains("court.pier_nine"));
        }

        [Test]
        public void HolidayWins_CountOnlyHolidayCourts_AndEveryHolidayFinishesTheMission()
        {
            var d = new CareerSaveData();
            foreach (var h in Holidays.All)
            {
                var r = Holidays.Request(h.Theme, DefaultContent.PlayerCrewId, "team.eastbay_voltage", DefaultContent.DefaultDifficultyId);
                r.ReturnTo = CityDistricts.ReturnTag(CityDistricts.HolidayPark, "x");
                CityMissions.ApplyGame(d, r, true, 3, null);
            }
            CityMissions.ApplyGame(d, Pickup("court.sunset_cage"), true, 3, null);
            Assert.AreEqual(4, d.city.holidayWins.Count);
            var all = CityMissions.Find("c5.allholiday");
            Assert.AreEqual((4, 4), CityMissions.Progress(d.city, d, all));
        }

        [Test]
        public void TheLastMission_NeedsEveryLegendaryCrew()
        {
            var d = new CareerSaveData();
            var last = CityMissions.Find("c6.legends");
            Assert.AreEqual(CityMissions.LegendaryCount, CityMissions.TargetOf(last));
            foreach (var crew in CityCrews.All) d.city.crewsBeaten.Add(crew.Id);
            Assert.AreEqual(CityMissions.LegendaryCount, CityMissions.Progress(d.city, d, last).now);
        }

        [Test]
        public void MissionSaves_RoundTrip_AndJunkIsDropped()
        {
            var d = new CareerSaveData();
            d.city.missionsDone.Add("c1.talk");
            d.city.missionsDone.Add("not.a.mission");
            d.city.crewsBeaten.Add("splash");
            d.city.crewsBeaten.Add("nobody");
            d.city.courtsWon.Add("court.coin_op");
            d.city.holidayWins.Add(Holidays.EasterCourtId);
            d.city.holidayWins.Add("court.sunset_cage");
            d.city.talkedTo.Add("mate.x");
            d.city.cityGames = 4;
            d.city.cityWins = 9; // more wins than games: impossible
            d.city.bestPoints = 17;
            d.city.bestMargin = -3;
            var back = SaveCodec.Decode(SaveCodec.Encode(d), C, out var status);
            Assert.AreEqual(LoadStatus.Ok, status);
            Assert.AreEqual(1, back.city.missionsDone.Count);
            Assert.AreEqual(1, back.city.crewsBeaten.Count);
            Assert.AreEqual("court.coin_op", back.city.courtsWon[0]);
            Assert.AreEqual(1, back.city.holidayWins.Count);
            Assert.AreEqual(1, back.city.talkedTo.Count);
            Assert.AreEqual(4, back.city.cityGames);
            Assert.AreEqual(4, back.city.cityWins);
            Assert.AreEqual(17, back.city.bestPoints);
            Assert.AreEqual(0, back.city.bestMargin);
        }

        private static MatchRequest Pickup(string court)
        {
            var r = Streets.Pickup(C, new CareerSaveData(), null, court);
            r.CourtId = court;
            var home = CityDistricts.CourtHome(court);
            r.ReturnTo = CityDistricts.ReturnTag(home?.district ?? CityDistricts.FirstCallers, court);
            return r;
        }

        private static void StringAssertContains(string s, string part) => Assert.IsTrue(s != null && s.Contains(part), s);
    }
}
