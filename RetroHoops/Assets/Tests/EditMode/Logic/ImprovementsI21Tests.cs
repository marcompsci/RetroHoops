using System;
using System.Collections.Generic;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>28 Oct 2026, improvements I21–I30: the BALLER WORLD polish pass.</summary>
    public class ImprovementsI21Tests
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

        [Test]
        public void I23_EveryBallerWorldLine_HasItsSpanish()
        {
            var missing = new List<string>();
            foreach (var s in WorldText.All()) if (!Loc.Has(s)) missing.Add(s);
            Assert.AreEqual(0, missing.Count, "No Spanish for: " + string.Join(" | ", missing));
            Assert.Greater(WorldText.All().Count, 250);
        }

        [Test]
        public void I24_WorldCourts_JoinQuickPlay_OnceYouveBeenToTheirRegion()
        {
            var d = NewCareer();
            Assert.AreEqual(WorldRegions.NewYork, WorldAtlas.CourtRegion("court.ny_cage"));
            Assert.IsNull(WorldAtlas.CourtRegion("court.sunset_cage"), "Baller City courts aren't stamped");
            Assert.IsFalse(WorldAtlas.CourtStamped(d, "court.ny_cage"));
            Assert.IsTrue(WorldAtlas.CourtStamped(d, "court.sunset_cage"));
            d.world.regionsSeen.Add(WorldRegions.NewYork);
            Assert.IsTrue(WorldAtlas.CourtStamped(d, "court.ny_cage"));
            Assert.IsFalse(WorldAtlas.CourtStamped(d, "court.lv_stage"));
            Assert.AreEqual("THE CAGE  ·  NYC", WorldAtlas.CourtLabel("court.ny_cage", "The Cage"));
            // Every BALLER WORLD court belongs to a region.
            int n = 0;
            foreach (var k in C.Courts) if (WorldAtlas.CourtRegion(k.id) != null) n++;
            Assert.AreEqual(28, n);
        }

        [Test]
        public void I25_RouteRecords_KeepTheBest_AndSurviveASave()
        {
            var d = NewCareer();
            Assert.IsTrue(d.world.NoteRoute(WorldRegions.LosAngeles, WorldRegions.NewYork, 24.5f));
            Assert.IsFalse(d.world.NoteRoute(WorldRegions.LosAngeles, WorldRegions.NewYork, 25f), "slower isn't a record");
            Assert.IsTrue(d.world.NoteRoute(WorldRegions.LosAngeles, WorldRegions.NewYork, 21.25f));
            Assert.AreEqual(21.25f, d.world.RouteBest(WorldRegions.LosAngeles, WorldRegions.NewYork), 0.01f);
            Assert.AreEqual(0f, d.world.RouteBest(WorldRegions.NewYork, WorldRegions.LosAngeles), "each direction is its own route");
            d.world.minimap = false;
            d.world.easyDrive = true;
            d.world.mapLabels = 2;
            d.world.mapPins = 5;
            var back = SaveCodec.Decode(SaveCodec.Encode(d), C, out _);
            Assert.AreEqual(21.25f, back.world.RouteBest(WorldRegions.LosAngeles, WorldRegions.NewYork), 0.01f);
            Assert.IsFalse(back.world.minimap);
            Assert.IsTrue(back.world.easyDrive);
            Assert.AreEqual(2, back.world.mapLabels);
            Assert.AreEqual(5, back.world.mapPins);
            // A finished drive notes its route.
            var drive = new HighwayDrive(WorldRegions.LosAngeles, WorldRegions.LasVegas, 3);
            while (!drive.Done) drive.Step(1f / 60f, drive.AutoSteer(), false);
            HighwayDrive.Finish(d, drive, 100);
            Assert.IsTrue(drive.RouteRecord);
            Assert.AreEqual(drive.Time, d.world.RouteBest(WorldRegions.LosAngeles, WorldRegions.LasVegas), 0.01f);
        }

        [Test]
        public void I26_Pins_MarkWhatsLeftToDo_InOpenRegionsOnly()
        {
            var d = NewCareer();
            var pins = WorldAtlas.Pins(d, CityDistricts.FirstCallers);
            Assert.IsTrue(pins.Exists(p => p.Kind == MapPinKind.Ride && p.District == CityDistricts.FirstCallers));
            Assert.IsTrue(pins.Exists(p => p.Kind == MapPinKind.Crew));
            Assert.IsFalse(pins.Exists(p => WorldRegions.RegionOf(p.District).Id == WorldRegions.NewYork), "New York isn't open yet");
            foreach (var p in pins) Assert.IsTrue(CityDistricts.Map(p.District).InBounds((int)p.At.x, (int)p.At.y), p.Kind + " " + p.Label);
            d.world.regionsSeen.Add(WorldRegions.NewYork);
            pins = WorldAtlas.Pins(d, CityDistricts.NyUptown);
            Assert.IsTrue(pins.Exists(p => p.Kind == MapPinKind.Store && p.District == CityDistricts.NyAvenues));
            Assert.IsTrue(pins.Exists(p => p.Kind == MapPinKind.SideJob && p.District == CityDistricts.NyAvenues));
            Assert.IsTrue(pins.Exists(p => p.Kind == MapPinKind.BigRun && p.District == CityDistricts.NyUptown));
            // Beaten crews and finished jobs come off the map.
            var crew = pins.Find(p => p.Kind == MapPinKind.Crew);
            var crewDef = Array.Find(CityCrews.All, c => c.Name == crew.Label);
            d.city.crewsBeaten.Add(crewDef.Id);
            Assert.IsFalse(WorldAtlas.Pins(d, CityDistricts.NyUptown).Exists(p => p.Kind == MapPinKind.Crew && p.Label == crew.Label));
            // The filter.
            d.world.mapPins = -1 & ~(1 << (int)MapPinKind.Crew);
            Assert.IsFalse(WorldAtlas.PinShown(d.world, MapPinKind.Crew));
            Assert.IsTrue(WorldAtlas.PinShown(d.world, MapPinKind.Ride));
            foreach (MapPinKind k in Enum.GetValues(typeof(MapPinKind))) Assert.Greater(WorldMapArt.Pin(k).OpaqueCount(), 40, k.ToString());
        }

        [Test]
        public void I27_EasyDrive_SteersRoundTraffic_AndBumpsLessThanNobodySteering()
        {
            int easyBumps = 0, worstBumps = 0;
            for (uint seed = 1; seed <= 10; seed++)
            {
                var easy = new HighwayDrive(WorldRegions.BallerCity, WorldRegions.NewYork, seed);
                while (!easy.Done) easy.Step(1f / 60f, easy.AutoSteer(), false);
                easyBumps += easy.Bumps;
                Assert.Less(easy.Time, HighwayDrive.MaxSeconds);
                var still = new HighwayDrive(WorldRegions.BallerCity, WorldRegions.NewYork, seed);
                while (!still.Done) still.Step(1f / 60f, 0f, false);
                worstBumps += still.Bumps;
            }
            Assert.Less(easyBumps, worstBumps, "the assist dodges cars a stationary driver hits");
            Assert.LessOrEqual(easyBumps, 10, "about one bump a drive at most");
        }

        [Test]
        public void I28_Today_SaysWhatsOnInBallerWorld()
        {
            var d = NewCareer();
            Assert.AreEqual(0, WorldToday.Lines(d, 50).Count, "nothing before your first walk in the city");
            d.world.started = true;
            CityStory.StartStep(d);
            var lines = WorldToday.Lines(d, 50);
            Assert.GreaterOrEqual(lines.Count, 2);
            StringAssert.Contains("#1", lines[0]);
            StringAssert.Contains("0/" + CityAchievements.PerDay, lines[1]);
            d.world.sideActive = "bc.lucky";
            lines = WorldToday.Lines(d, 50);
            StringAssert.Contains("LUCKY BALL", lines[lines.Count - 1]);
        }

        [Test]
        public void I30_RegionStores_SellTheirOwnGear_ForStreetCashOnly()
        {
            var d = NewCareer();
            foreach (var r in WorldRegions.All)
            {
                if (r.Id == WorldRegions.BallerCity) continue;
                var items = RegionStore.Items(C, r.Id);
                Assert.AreEqual(3, items.Count, r.Id);
                // Each region's grid district has the store.
                string grid = r.Districts[r.Districts.Length - 1];
                var store = CityDistricts.Map(grid).Portals.Find(p => p.Target == RegionStore.Prefix + r.Id);
                Assert.IsNotNull(store, r.Id);
                Assert.IsTrue(CityDistricts.Map(grid).Walkable(new Vec2(store.X + store.W * 0.5f, store.Y + store.H * 0.5f)), r.Id);
            }
            var shoes = RegionStore.Items(C, WorldRegions.NewYork).Find(x => x.slot == CosmeticSlot.Shoes);
            // Not in the Corner Store, and not for SP.
            Assert.IsFalse(CityShop.Catalog(C).Exists(x => x.id == shoes.id));
            d.signalPoints = 100000;
            Assert.AreEqual(CosmeticCheck.StoreOnly, Career.CanBuy(d, shoes));
            Assert.AreNotEqual(CosmeticCheck.Ok, Career.Buy(d, shoes));
            // Street cash buys it.
            d.streetCash = new StreetCashSaveData { cash = 10 };
            Assert.AreEqual(CosmeticCheck.NotEnoughPoints, RegionStore.Buy(d, C, shoes.id));
            d.streetCash.cash = StreetCash.Price(shoes) + 5;
            Assert.AreEqual(CosmeticCheck.Ok, RegionStore.Buy(d, C, shoes.id));
            Assert.AreEqual(5, d.streetCash.cash);
            Assert.AreEqual(shoes.id, d.Equipped(CosmeticSlot.Shoes));
            Assert.AreEqual(CosmeticCheck.AlreadyOwned, RegionStore.Buy(d, C, shoes.id));
            var choices = RegionStore.Choices(d, C, WorldRegions.NewYork);
            Assert.IsTrue(choices.Exists(c => c.ItemId == shoes.id && c.Action == CityAction.Info), "wearing it");
        }
    }
}
