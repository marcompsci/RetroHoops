using System;
using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>17 Oct 2026: BALLER CITY phases 5–7 — the Baller Arena (City Championship), City Challenges and call-outs, and your crib.</summary>
    public class BallerCityBigTimeTests
    {
        private static ContentCatalog C => _c ??= DefaultContent.Create();
        private static ContentCatalog _c;
        private const int Day = 9100;

        private static CareerSaveData Ready()
        {
            var d = new CareerSaveData();
            foreach (var crew in Array.FindAll(CityCrews.All, x => x.Tier == CityCrewTier.Baller))
            {
                d.city.crewsBeaten.Add(crew.Id);
                if (d.city.crewsBeaten.Count == 3) break;
            }
            return d;
        }

        // ------------------------------------------------------------------ phase 5: the arena

        [Test]
        public void Arena_DoorsOpenDowntown_AndTheFloorIsTheChampionship()
        {
            var dt = CityDistricts.Map(CityDistricts.Downtown);
            var door = dt.Portal("door.arena");
            Assert.IsNotNull(door);
            Assert.AreEqual(CityDistricts.Arena, door.Target);
            var arena = CityDistricts.Map(CityDistricts.Arena);
            Assert.IsTrue(arena.Interior);
            Assert.IsTrue(arena.TryGetSpawn(door.TargetSpawn, out _));
            Assert.AreEqual(CityDistricts.Downtown, arena.Portal("door.exit").Target);
            Assert.IsTrue(dt.TryGetSpawn(arena.Portal("door.exit").TargetSpawn, out _));
            var court = arena.Courts.Find(k => k.Use == CityCourtUse.Arena);
            Assert.IsNotNull(court);
            Assert.IsNotNull(C.Court(CityArena.CourtId), "a real court in the catalog");
            Assert.IsTrue(arena.Props.Exists(p => p.Kind == CityPropKind.Crowd));
            Assert.IsTrue(arena.Props.Exists(p => p.Kind == CityPropKind.Scoreboard));
        }

        [Test]
        public void Championship_NeedsThreeBallerWins_ThenSemiThenFinal()
        {
            var locked = CityArena.Choices(new CareerSaveData(), C, Day, "city:baller_arena:court.arena");
            Assert.IsNotNull(locked[0].Locked);
            Assert.IsNull(CityArena.Request(C, new CareerSaveData(), Day));

            var d = Ready();
            var draw = CityArena.Draw(Day);
            Assert.AreEqual(CityCrewTier.Baller, draw.semi.Tier);
            Assert.AreEqual(CityCrewTier.Legendary, draw.final.Tier);
            var semi = CityArena.Request(C, d, Day);
            Assert.AreEqual(1, CityArena.RoundOf(semi));
            Assert.AreEqual(CityArena.CourtId, semi.CourtId);
            Assert.IsTrue(ReferenceEquals(draw.semi, CityCrews.FromContext(semi.ContextId)), "the crew still counts as that crew");
            Assert.AreEqual(CityCrews.BallerDifficultyId, semi.DifficultyId);
            StringAssertContains(CityArena.ApplyGame(d, semi, true, Day), "FINAL");
            var final = CityArena.Request(C, d, Day);
            Assert.AreEqual(2, CityArena.RoundOf(final));
            Assert.AreEqual(CityCrews.LegendaryDifficultyId, final.DifficultyId);
            long cash = d.streetCash.cash;
            int sp = d.signalPoints;
            StringAssertContains(CityArena.ApplyGame(d, final, true, Day), "CITY CHAMPIONS");
            Assert.AreEqual(1, d.bigTime.arenaTitles);
            Assert.AreEqual(cash + CityArena.TitlePrize, d.streetCash.cash);
            Assert.AreEqual(sp + CityArena.TitleSp, d.signalPoints);
            Assert.AreEqual(0, CityArena.NextRound(d.bigTime, Day), "one run a day");
            Assert.IsNull(CityArena.Request(C, d, Day));
            Assert.AreEqual(1, CityArena.NextRound(d.bigTime, Day + 1), "a new draw tomorrow");
            Assert.IsNull(CityArena.ApplyGame(d, Streets.Pickup(C, d, null, null), true, Day), "other games don't touch the arena");
        }

        [Test]
        public void Championship_ALossEndsTodaysRun()
        {
            var d = Ready();
            var semi = CityArena.Request(C, d, Day);
            StringAssertContains(CityArena.ApplyGame(d, semi, false, Day), "KNOCKED OUT");
            Assert.AreEqual(0, CityArena.NextRound(d.bigTime, Day));
            Assert.AreEqual(0, d.bigTime.arenaTitles);
            Assert.IsNotNull(CityArena.Choices(d, C, Day, "x").Find(a => a.Label == "CITY CHAMPIONSHIP").Locked);
        }

        // ------------------------------------------------------------------ phase 6: challenges and call-outs

        [Test]
        public void ThreeChallengesADay_TheSameAllDay_AllDifferent()
        {
            var a = CityDaily.For(Day);
            Assert.AreEqual(3, a.Count);
            var kinds = new HashSet<CityDailyKind>();
            foreach (var x in a) Assert.IsTrue(kinds.Add(x.Kind), "no repeats in a day");
            Assert.AreEqual(string.Join("|", a.ConvertAll(x => x.Text)), string.Join("|", CityDaily.For(Day).ConvertAll(x => x.Text)));
            bool changed = false;
            for (int k = 1; k < 8; k++) if (string.Join("|", CityDaily.For(Day + k).ConvertAll(x => x.Text)) != string.Join("|", a.ConvertAll(x => x.Text))) changed = true;
            Assert.IsTrue(changed);
        }

        [Test]
        public void Challenges_PayEach_AndABonusForAllThree_Once()
        {
            // Find a day whose challenges are all doable with one big night win in a district.
            for (int day = 1; day < 400; day++)
            {
                var defs = CityDaily.For(day);
                if (defs.Exists(x => x.Kind == CityDailyKind.Play)) continue;
                var d = new CareerSaveData();
                string district = defs.Find(x => x.Kind == CityDailyKind.WinIn)?.Ref ?? CityDistricts.Blacktop;
                var r = CityCrews.Challenge(C, d, CityCrews.All[0]);
                r.ReturnTo = CityDistricts.ReturnTag(district, "x");
                var line = new PlayerStatLine { points = 20, assists = 5, steals = 2, blocks = 2 };
                long paid = CityDaily.ApplyGame(d, r, true, line, day, 22, out var done);
                Assert.AreEqual(3, done.Count, "day " + day);
                Assert.AreEqual(3 * CityDaily.EachPay + CityDaily.SetBonus, paid);
                Assert.AreEqual(paid, d.streetCash.cash);
                Assert.AreEqual(1, d.bigTime.dailySets);
                Assert.AreEqual(0, CityDaily.ApplyGame(d, r, true, line, day, 22, out _), "pays once");
                Assert.AreEqual(0, CityDaily.Progress(d.bigTime, day + 1)[0], "resets tomorrow");
                return;
            }
            Assert.Fail("no suitable day found");
        }

        [Test]
        public void Challenges_OnlyCountCityGames()
        {
            var d = new CareerSaveData();
            var r = Streets.Pickup(C, d, null, null);
            Assert.AreEqual(0, CityDaily.ApplyGame(d, r, true, new PlayerStatLine { points = 30 }, Day, 12, out var done));
            Assert.AreEqual(0, done.Count);
        }

        [Test]
        public void CallOuts_OneADay_FromACrewThatPlaysThere_WinPaysExtra()
        {
            var d = Ready();
            CityCrewDef crew = null;
            string where = null;
            int day = Day;
            for (; day < Day + 30 && crew == null; day++)
                foreach (var id in new[] { CityDistricts.HoopHeights, CityDistricts.Blacktop, CityDistricts.Downtown })
                {
                    crew = CityCallout.For(d, id, day);
                    if (crew != null) { where = id; break; }
                }
            day--;
            Assert.IsNotNull(crew);
            Assert.IsNotNull(CityDistricts.Map(where).Courts.Find(k => k.CourtId == crew.CourtId), "a crew from that district");
            Assert.IsTrue(CityCrews.Unlocked(d.city, crew));
            Assert.IsNull(CityCallout.For(d, CityDistricts.Gym, day), "nobody calls you out in the gym");
            var r = CityCallout.Request(C, d, crew);
            Assert.IsTrue(CityCallout.IsCallout(r));
            Assert.IsTrue(ReferenceEquals(crew, CityCrews.FromContext(r.ContextId)));
            Assert.AreEqual(CityCallout.Bonus, CityCallout.ApplyGame(d, r, true, day));
            Assert.AreEqual(1, d.bigTime.calloutsWon);
            Assert.IsNull(CityCallout.For(d, where, day), "answered: no more today");
            Assert.AreEqual(0, CityCallout.ApplyGame(d, CityCrews.Challenge(C, d, crew), true, day), "a normal crew game isn't a call-out");

            var npc = new CityNpc { Id = "callout.x", Role = CityNpcRole.Crew, Ref = crew.Id, Callout = true, Player = C.Players[0] };
            var acts = CityActivities.ForNpc(CityDistricts.Map(where), npc, d, C, new Vec2(5, 5));
            Assert.AreEqual("ACCEPT CALL-OUT", acts[0].Label);
            Assert.IsTrue(CityCallout.IsCallout(acts[0].Request));
            Assert.IsNotNull(acts[0].Request.ReturnTo);
        }

        // ------------------------------------------------------------------ phase 7: your crib

        [Test]
        public void YourFrontDoor_OpensYourCrib()
        {
            var fc = CityDistricts.Map(CityDistricts.FirstCallers);
            Assert.AreEqual(CityDistricts.Crib, fc.Portal("door.home").Target);
            var crib = CityDistricts.Map(CityDistricts.Crib);
            Assert.AreEqual("home", crib.Portal("door.exit").TargetSpawn);
            Assert.AreEqual(CityAction.OpenLockerRoom, CityActivities.ForPortal(crib, crib.Portal("crib.wardrobe"), new CareerSaveData(), C)[0].Action);
            // The trophy case and banner wall stand on tiles the map keeps solid.
            foreach (var p in CityCrib.Decor(new CareerSaveData()))
                for (int x = p.X; x < p.X + p.W; x++) Assert.IsTrue(crib.Blocked(x, p.Y), p.Kind + " at " + x);
        }

        [Test]
        public void Crib_ShowsYourTrophiesBannersAndBank()
        {
            var d = new CareerSaveData();
            Assert.AreEqual(0, CityCrib.TrophyVariant(d));
            Assert.AreEqual(0, CityCrib.BannerMask(d));
            d.bigTime.arenaTitles = 2;
            d.streetCash.achievements.Add("first_cash");
            d.city.crewsBeaten.Add(CityCrews.All[0].Id);
            d.city.crewsBeaten.Add(CityCrews.All[3].Id);
            d.streetCash.cash = 12345;
            Assert.AreEqual(12, CityCrib.TrophyVariant(d));
            Assert.AreEqual((1 << 0) | (1 << 3), CityCrib.BannerMask(d));
            var crib = CityDistricts.Map(CityDistricts.Crib);
            StringAssertContains(CityActivities.ForPortal(crib, crib.Portal("crib.safe"), d, C)[0].Label, "$12,345");
            StringAssertContains(CityActivities.ForPortal(crib, crib.Portal("crib.trophies"), d, C)[0].Label, "2");
            Assert.AreEqual(CityCrews.All.Length, CityActivities.ForPortal(crib, crib.Portal("crib.banners"), d, C).Count);
            foreach (var p in CityCrib.Decor(d))
            {
                var art = Logic.PixelArt.CityArtGenerator.Prop(p, false, out _, out _);
                int painted = 0;
                foreach (var px in art.Pixels) if (px.a > 0) painted++;
                Assert.Greater(painted, 100, p.Kind.ToString());
            }
        }

        // ------------------------------------------------------------------ missions and saves

        [Test]
        public void ChapterSeven_BigTime()
        {
            var d = new CareerSaveData();
            d.bigTime.arenaTitles = 1;
            d.bigTime.dailySets = 1;
            d.bigTime.calloutsWon = 1;
            d.streetCash.earned = 300000;
            Assert.AreEqual((1, 1), CityMissions.Progress(d.city, d, CityMissions.Find("c7.title")));
            Assert.AreEqual((1, 3), CityMissions.Progress(d.city, d, CityMissions.Find("c7.dynasty")));
            Assert.AreEqual((1, 1), CityMissions.Progress(d.city, d, CityMissions.Find("c7.daily")));
            Assert.AreEqual((1, 1), CityMissions.Progress(d.city, d, CityMissions.Find("c7.callout")));
            Assert.AreEqual((250000, 250000), CityMissions.Progress(d.city, d, CityMissions.Find("c7.bank")));
            Assert.AreEqual("BIG TIME", CityMissions.ChapterNames[7]);
        }

        [Test]
        public void BigTimeSaves_RoundTrip_AndCleanUp()
        {
            var d = new CareerSaveData();
            d.bigTime.arenaRound = 2;
            d.bigTime.arenaDay = 77;
            d.bigTime.arenaTitles = 4;
            d.bigTime.dailyDay = 77;
            d.bigTime.dailyProgress = new[] { 1, 0, 3 };
            d.bigTime.dailyPaid = true;
            d.bigTime.dailySets = 6;
            d.bigTime.calloutsWon = 2;
            var b = SaveCodec.Decode(SaveCodec.Encode(d), C, out _).bigTime;
            Assert.AreEqual(2, b.arenaRound);
            Assert.AreEqual(77, b.arenaDay);
            Assert.AreEqual(4, b.arenaTitles);
            Assert.AreEqual(3, b.dailyProgress[2]);
            Assert.IsTrue(b.dailyPaid);
            Assert.AreEqual(6, b.dailySets);
            Assert.AreEqual(2, b.calloutsWon);
            d.bigTime.arenaRound = 9;
            d.bigTime.arenaTitles = -3;
            b = SaveCodec.Decode(SaveCodec.Encode(d), C, out _).bigTime;
            Assert.AreEqual(2, b.arenaRound);
            Assert.AreEqual(0, b.arenaTitles);
        }

        private static void StringAssertContains(string s, string part) => Assert.IsTrue(s != null && s.Contains(part), s);
    }
}
