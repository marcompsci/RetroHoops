using System;
using System.Collections.Generic;
using CallerRetroBall.Logic;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>17 Oct 2026: BALLER CITY phase 3 — new districts, the MAP and fast travel, the shops, and the living city.</summary>
    public class BallerCityPhase3Tests
    {
        private static ContentCatalog C => _c ??= DefaultContent.Create();
        private static ContentCatalog _c;

        // ------------------------------------------------------------------ districts and the map

        [Test]
        public void TheMap_MatchesTheRoads()
        {
            foreach (var spot in CityAtlas.Spots)
            {
                Assert.IsNotNull(CityDistricts.Find(spot.id), spot.id);
                Assert.IsFalse(CityDistricts.Find(spot.id).Interior);
                foreach (var p in CityDistricts.Map(spot.id).Portals)
                {
                    if (p.Kind != CityPortalKind.District) continue;
                    var to = Array.Find(CityAtlas.Spots, x => x.id == p.Target);
                    Assert.IsNotNull(to.id, p.Id);
                    Assert.AreEqual(1, Math.Abs(to.col - spot.col) + Math.Abs(to.row - spot.row), spot.id + " → " + p.Target + " should be next door on the map");
                }
            }
            CollectionAssert.AllItemsAreUnique(Array.ConvertAll(CityAtlas.Spots, s => s.col + "," + s.row));
            // Every outdoor district is on the map; every indoor one maps to First Callers.
            foreach (var d in CityDistricts.All)
                Assert.AreEqual(!d.Interior, CityAtlas.OnMap(d.Id), d.Id);
            Assert.AreEqual(CityDistricts.FirstCallers, CityAtlas.Outside(CityDistricts.CornerStore));
        }

        [Test]
        public void ThreeNewDistricts_EachWithCourtsAndCrews()
        {
            foreach (var id in new[] { CityDistricts.Boardwalk, CityDistricts.Downtown, CityDistricts.SchoolYard })
            {
                var m = CityDistricts.Map(id);
                Assert.GreaterOrEqual(m.Courts.Count, 3, id);
                int crews = 0;
                foreach (var k in m.Courts)
                {
                    Assert.IsNotNull(C.Court(k.CourtId), k.CourtId);
                    if (CityCrews.AtCourt(k.CourtId) != null) crews++;
                }
                Assert.GreaterOrEqual(crews, 2, id);
            }
            Assert.GreaterOrEqual(CityCrews.All.Length, 22);
            int courts = 0;
            foreach (var d in CityDistricts.All) courts += CityDistricts.Map(d.Id).Courts.FindAll(k => k.Use != CityCourtUse.Practice).Count;
            Assert.GreaterOrEqual(courts, 26);
        }

        [Test]
        public void FastTravel_OnlyToPlacesYouveBeen()
        {
            var s = new CitySaveData();
            s.seen.Add(CityDistricts.FirstCallers);
            Assert.IsFalse(CityAtlas.CanTravel(s, CityDistricts.FirstCallers, CityDistricts.Downtown), "not visited");
            Assert.IsFalse(CityAtlas.Travel(s, CityDistricts.FirstCallers, CityDistricts.Downtown));
            Assert.AreEqual(0, s.fastTravels);
            s.seen.Add(CityDistricts.Downtown);
            Assert.IsFalse(CityAtlas.CanTravel(s, CityDistricts.Downtown, CityDistricts.Downtown), "already there");
            Assert.IsFalse(CityAtlas.CanTravel(s, CityDistricts.FirstCallers, CityDistricts.Gym), "indoors isn't on the map");
            Assert.IsTrue(CityAtlas.Travel(s, CityDistricts.CornerStore, CityDistricts.Downtown));
            Assert.AreEqual(CityDistricts.Downtown, s.district);
            Assert.AreEqual(1, s.fastTravels);
            var m = CityDistricts.Map(CityDistricts.Downtown);
            Assert.IsFalse(CityMovement.Overlaps(m, s.x, s.y));
            Assert.IsNull(CityInteraction.RoadAt(m, new Vec2(s.x, s.y)), "you don't arrive on a road out");
            StringAssertContains(CityAtlas.Summary(s, CityDistricts.Downtown), "COURTS");
        }

        // ------------------------------------------------------------------ the shops

        [Test]
        public void Store_SixItemsADay_TheSameAllDay_NeverPassItems()
        {
            var first = CityShop.Stock(C, 9000);
            Assert.AreEqual(CityShop.StockSize, first.Count);
            CollectionAssert.AllItemsAreUnique(first.ConvertAll(x => x.id));
            Assert.AreEqual(string.Join(",", first.ConvertAll(x => x.id)), string.Join(",", CityShop.Stock(C, 9000).ConvertAll(x => x.id)));
            bool changed = false;
            for (int d = 9001; d < 9008; d++)
            {
                var other = CityShop.Stock(C, d);
                foreach (var x in other) Assert.IsFalse(x.passOnly || x.unlockedByDefault || x.cost <= 0, x.id);
                if (string.Join(",", other.ConvertAll(x => x.id)) != string.Join(",", first.ConvertAll(x => x.id))) changed = true;
            }
            Assert.IsTrue(changed, "new stock on other days");
        }

        [Test]
        public void Store_BuysWithSP_PutsItOn_AndOnlyFromTodaysShelves()
        {
            var d = new CareerSaveData();
            d.fans = 100000;
            var item = CityShop.Stock(C, 9000)[0];
            d.signalPoints = item.cost - 1;
            var choices = CityShop.Choices(d, C, 9000);
            Assert.IsNotNull(choices[0].Locked, "can't afford it yet");
            Assert.AreEqual(CosmeticCheck.NotEnoughPoints, CityShop.Buy(d, C, item.id, 9000));
            Assert.AreEqual(item.cost - 1, d.signalPoints);
            d.signalPoints = item.cost + 50;
            Assert.AreEqual(CosmeticCheck.Ok, CityShop.Buy(d, C, item.id, 9000));
            Assert.AreEqual(50, d.signalPoints);
            Assert.IsTrue(d.ownedCosmetics.Contains(item.id));
            Assert.AreEqual(item.id, d.Equipped(item.slot));
            Assert.AreEqual(1, d.city.purchases);
            Assert.AreEqual(CosmeticCheck.AlreadyOwned, CityShop.Buy(d, C, item.id, 9000));
            StringAssertContains(CityShop.Choices(d, C, 9000)[0].Label, "WEARING");

            // Something not on today's shelves can't be bought here.
            var all = CityShop.Catalog(C);
            var stock = CityShop.Stock(C, 9000);
            var missing = all.Find(x => !stock.Exists(y => y.id == x.id));
            Assert.IsNotNull(missing);
            d.signalPoints = 100000;
            Assert.AreEqual(CosmeticCheck.Unknown, CityShop.Buy(d, C, missing.id, 9000));
            Assert.IsFalse(d.ownedCosmetics.Contains(missing.id));
        }

        [Test]
        public void Barber_NeedsYourOwnPlayer_FirstCutFree_ThenPaid()
        {
            var d = new CareerSaveData();
            d.signalPoints = 100;
            var locked = CityBarber.Choices(d);
            Assert.AreEqual(1, locked.Count);
            Assert.IsNotNull(locked[0].Locked);
            Assert.IsFalse(CityBarber.Apply(d, "style:2"), "the default Rook keeps his look");

            d.customPlayer.created = true;
            d.customPlayer.hairStyle = 0;
            Assert.AreEqual(CityBarber.StyleNames.Length + CityBarber.ColorNames.Length, CityBarber.Choices(d).Count);
            Assert.IsTrue(CityBarber.Apply(d, "style:3"));
            Assert.AreEqual(3, d.customPlayer.hairStyle);
            Assert.AreEqual(100, d.signalPoints, "first cut free");
            Assert.IsTrue(CityBarber.Apply(d, "color:4"));
            Assert.AreEqual(100 - CityBarber.ColorPrice, d.signalPoints);
            Assert.AreEqual(2, d.city.haircuts);
            Assert.IsFalse(CityBarber.Apply(d, "style:3"), "already your style");
            Assert.IsFalse(CityBarber.Apply(d, "style:99"));
            Assert.IsFalse(CityBarber.Apply(d, "dye:1"));
            d.signalPoints = 10;
            Assert.IsFalse(CityBarber.Apply(d, "style:1"), "can't afford it");
            Assert.AreEqual(3, d.customPlayer.hairStyle);
            Assert.AreEqual(10, d.signalPoints);
        }

        [Test]
        public void ShopsAndSchool_DoorsWork()
        {
            var d = new CareerSaveData();
            var store = CityDistricts.Map(CityDistricts.CornerStore);
            var acts = CityActivities.ForPortal(store, store.Portal("counter"), d, C, 9000);
            Assert.AreEqual(CityShop.StockSize, acts.Count);
            var barber = CityDistricts.Map(CityDistricts.FreshCuts);
            Assert.AreEqual(1, CityActivities.ForPortal(barber, barber.Portal("chair"), d, C).Count);
            var school = CityDistricts.Map(CityDistricts.SchoolYard);
            Assert.AreEqual(1, CityActivities.ForPortal(school, school.Portal("door.school"), d, C).Count);
            var fc = CityDistricts.Map(CityDistricts.FirstCallers);
            Assert.AreEqual(CityDistricts.CornerStore, fc.Portal("door.store").Target);
            Assert.AreEqual(CityDistricts.FreshCuts, fc.Portal("door.barber").Target);
        }

        // ------------------------------------------------------------------ the living city

        [Test]
        public void Weather_IsTheSameAllDay_MostlyClear_SnowInWinter_OffIndoors()
        {
            Assert.AreEqual(CityWeather.For(9000, 10, "blacktop", false), CityWeather.For(9000, 10, "blacktop", false));
            Assert.AreEqual(CityWeatherKind.Clear, CityWeather.For(9000, 10, "baller_gym", true));
            int clear = 0, rain = 0, snow = 0;
            for (int d = 0; d < 365; d++)
            {
                var k = CityWeather.For(d, 10, "blacktop", false);
                if (k == CityWeatherKind.Clear) clear++;
                if (k == CityWeatherKind.Rain) rain++;
                Assert.AreEqual(CityWeatherKind.Clear, CityWeather.For(d, 10, "blacktop", false, off: true));
                if (CityWeather.For(d, 1, "blacktop", false) == CityWeatherKind.Snow) snow++;
                Assert.AreNotEqual(CityWeatherKind.Rain, CityWeather.For(d, 1, "blacktop", false), "rain is snow in January");
            }
            Assert.Greater(clear, 365 / 2);
            Assert.Greater(rain, 0);
            Assert.Greater(snow, 0);
            Assert.AreEqual(0, CityWeather.Particles(CityWeatherKind.Rain, reduceMotion: true));
        }

        [Test]
        public void Decorations_FollowTheCalendar_AndNeverGetInTheWay()
        {
            Assert.AreEqual(HolidayTheme.Halloween, CityDecor.Season(new DateTime(2026, 10, 17)));
            var fc = CityDistricts.Map(CityDistricts.FirstCallers);
            var pumpkins = CityDecor.For(fc, HolidayTheme.Halloween);
            Assert.Greater(pumpkins.Count, 3);
            foreach (var p in pumpkins)
            {
                Assert.AreEqual(CityPropKind.Pumpkin, p.Kind);
                Assert.IsFalse(p.Solid);
                Assert.IsFalse(fc.Blocked(p.X, p.Y));
                var g = fc.Get(p.X, p.Y);
                Assert.IsFalse(g == CityGround.Road || g == CityGround.RoadLine || g == CityGround.Crosswalk, "not on the road");
                foreach (var portal in fc.Portals) Assert.IsFalse(portal.Contains(p.X + 0.5f, p.Y + 0.5f), "not on a door");
            }
            Assert.AreEqual(0, CityDecor.For(fc, HolidayTheme.None).Count);
            Assert.AreEqual(0, CityDecor.For(CityDistricts.Map(CityDistricts.HolidayPark), HolidayTheme.Christmas).Count, "dressed all year already");
            Assert.IsTrue(CityDecor.For(fc, HolidayTheme.Christmas).Exists(p => p.Kind == CityPropKind.PineLit));
            // Decorations don't change the map.
            Assert.IsFalse(fc.Props.Exists(p => p.Kind == CityPropKind.Pumpkin));
        }

        [Test]
        public void Traffic_DrivesOnTheRoads_StopsForYou_AndLoops()
        {
            var fc = CityDistricts.Map(CityDistricts.FirstCallers);
            var lanes = CityTraffic.Lanes(fc);
            Assert.AreEqual(2, lanes.Count, "one road, two lanes");
            foreach (var l in lanes)
            {
                var g = fc.Get(5, (int)l.Y);
                Assert.IsTrue(g == CityGround.Road || g == CityGround.RoadLine, "lane on the road");
            }
            Assert.AreEqual(4, CityTraffic.Lanes(CityDistricts.Map(CityDistricts.HoopHeights)).Count);
            Assert.AreEqual(0, CityTraffic.Lanes(CityDistricts.Map(CityDistricts.Gym)).Count);

            var cars = new List<CityCar> { new CityCar { Lane = 0, X = 5f, Cruise = 4f, Speed = 4f } };
            var you = new Vec2(7f, lanes[0].Y);
            for (int i = 0; i < 120; i++) CityTraffic.Step(cars, lanes, fc.Width, you, 1f / 60f);
            Assert.AreEqual(0f, cars[0].Speed, 0.01, "stopped for you");
            Assert.Less(cars[0].X, you.x, "never drives through you");
            for (int i = 0; i < 60 * 30; i++) CityTraffic.Step(cars, lanes, fc.Width, new Vec2(-50f, -50f), 1f / 60f);
            Assert.Greater(cars[0].Speed, 3f, "drives on once you're gone");
            Assert.IsTrue(cars[0].X >= -3f && cars[0].X <= fc.Width + 3f, "loops round");
            Assert.Greater(CityTraffic.Cars(fc, lanes, 9000).Count, 1);
        }

        [Test]
        public void Walkers_AreMadeUpPeople_OnTheSidewalks_FewerAtNight()
        {
            var fc = CityDistricts.Map(CityDistricts.FirstCallers);
            var day = CityWalkers.For(fc, 9000, CityTimeOfDay.Afternoon);
            var night = CityWalkers.For(fc, 9000, CityTimeOfDay.Night);
            Assert.Greater(day.Count, night.Count);
            Assert.AreEqual(day.Count, CityWalkers.For(fc, 9000, CityTimeOfDay.Afternoon).Count);
            foreach (var w in day)
            {
                Assert.IsTrue(fc.Walkable(w.Anchor));
                Assert.IsNull(C.Player(w.Player.id), "not a real player in the game");
                Assert.IsNull(CityInteraction.RoadAt(fc, w.Anchor));
            }
            Assert.AreEqual(0, CityWalkers.For(CityDistricts.Map(CityDistricts.Gym), 9000, CityTimeOfDay.Afternoon).Count);
        }

        // ------------------------------------------------------------------ chapter 6 and saves

        [Test]
        public void ChapterSix_CountsShoppingCutsAndTravel()
        {
            var d = new CareerSaveData();
            d.city.purchases = 1;
            d.city.haircuts = 1;
            d.city.fastTravels = 1;
            Assert.AreEqual((1, 1), CityMissions.Progress(d.city, d, CityMissions.Find("c6.shop")));
            Assert.AreEqual((1, 1), CityMissions.Progress(d.city, d, CityMissions.Find("c6.cut")));
            Assert.AreEqual((1, 1), CityMissions.Progress(d.city, d, CityMissions.Find("c6.travel")));
            Assert.AreEqual(6, CityMissions.TargetOf(CityMissions.Find("c5.legends")), "chapter 5's goal doesn't grow with new crews");
            Assert.AreEqual(CityMissions.LegendaryCount, CityMissions.TargetOf(CityMissions.Find("c6.legends")));
            Assert.AreEqual("ALL CITY", CityMissions.ChapterNames[6]);
        }

        [Test]
        public void Phase3Saves_RoundTrip()
        {
            var d = new CareerSaveData();
            d.city.purchases = 3;
            d.city.haircuts = 2;
            d.city.fastTravels = 5;
            d.city.freeCutUsed = true;
            d.city.weatherOff = true;
            var back = SaveCodec.Decode(SaveCodec.Encode(d), C, out _).city;
            Assert.AreEqual(3, back.purchases);
            Assert.AreEqual(2, back.haircuts);
            Assert.AreEqual(5, back.fastTravels);
            Assert.IsTrue(back.freeCutUsed);
            Assert.IsTrue(back.weatherOff);
        }

        private static void StringAssertContains(string s, string part) => Assert.IsTrue(s != null && s.Contains(part), s);
    }
}
