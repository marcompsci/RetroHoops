using System;
using System.Collections.Generic;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>20 Nov 2026, improvements I141–I155: the SHOP (courts, gear, Court Designer Plus, ALL-ACCESS, tips).</summary>
    public class ImprovementsI141Tests
    {
        private ContentCatalog _c;
        private CareerSaveData _d;

        [SetUp]
        public void Setup()
        {
            _c = DefaultContent.Create();
            _d = Career.New(_c);
        }

        [Test]
        public void I141_Catalog_IdsAreUnique_Prefixed_AndOnlyTipsAreConsumable()
        {
            var ids = new HashSet<string>();
            foreach (var p in Shop.Products)
            {
                Assert.IsTrue(ids.Add(p.Id), p.Id);
                Assert.IsTrue(p.Id.StartsWith(Shop.Prefix, StringComparison.Ordinal), p.Id);
                Assert.AreEqual(p.Kind == ShopKind.Tip, p.Consumable, p.Id);
                Assert.IsTrue(p.PriceFallback.StartsWith("$", StringComparison.Ordinal), p.PriceFallback);
                Assert.IsTrue(Loc.Has(p.Name) && Loc.Has(p.Blurb), p.Name + " needs Spanish");
                Assert.IsFalse(p.Id.Contains("coins") || p.Id.Contains("cash") || p.Id.Contains("gems"), "no currency for sale");
            }
            Assert.AreNotEqual(Pro.ProductId, Shop.AllAccess);
            Assert.IsFalse(ids.Contains(Pro.ProductId), "Pro keeps its own bridge");
        }

        [Test]
        public void I143_TwelveCourts_InFourPacks_AllInTheCatalog_AndLocked()
        {
            int packs = 0;
            foreach (var p in Shop.Products)
            {
                if (p.Kind != ShopKind.CourtPack) continue;
                packs++;
                Assert.AreEqual(3, p.Items.Length);
                foreach (var id in p.Items)
                {
                    var def = _c.Court(id);
                    Assert.IsNotNull(def, id);
                    Assert.AreEqual(CourtCircuit.Shop, def.circuit);
                    Assert.IsTrue(Shop.IsShopCourt(id));
                    Assert.IsTrue(ReferenceEquals(p, Shop.PackOf(id)), id);
                    Assert.IsFalse(Shop.CourtOpen(_d, id), "locked until bought");
                    Assert.IsTrue(Loc.Has(def.description), def.description);
                }
            }
            Assert.AreEqual(4, packs);
            Assert.AreEqual(12, Shop.Courts.Length);
            Assert.AreEqual(0, Shop.OwnedCourts(_d).Count);
            Assert.IsTrue(Shop.CourtOpen(_d, "court.sunset_cage"), "other courts aren't the shop's business");
            Assert.IsFalse(CustomTeams.HomeCourts(_c, _d).Exists(k => k.circuit == CourtCircuit.Shop));
        }

        [Test]
        public void I143_I144_EveryShopCourtRenders_AndTheNewFloorsDiffer()
        {
            var seen = new HashSet<FloorStyle>();
            foreach (var k in Shop.Courts)
            {
                var canvas = CourtGenerator.Generate(_c.Court(k.Id), CourtGeometry.Default, 11);
                Assert.Greater(canvas.Width, 100);
                seen.Add(k.Style);
            }
            Assert.IsTrue(seen.Contains(FloorStyle.Checker) && seen.Contains(FloorStyle.Sunburst) && seen.Contains(FloorStyle.Parquet));
            // The same court in each of the three new floors draws three different floors.
            var hashes = new HashSet<uint>();
            foreach (var style in new[] { FloorStyle.Checker, FloorStyle.Sunburst, FloorStyle.Parquet, FloorStyle.Asphalt })
            {
                var def = _c.Court("court.shop.harbor");
                var copy = new CourtDef
                {
                    id = def.id, displayName = def.displayName, circuit = def.circuit, floor = def.floor, lines = def.lines, paint = def.paint,
                    skyTop = def.skyTop, skyBottom = def.skyBottom, floorStyle = (int)style, stands = def.stands, logoMotif = -1,
                };
                var cv = CourtGenerator.Generate(copy, CourtGeometry.Default, 11);
                uint h = 2166136261;
                for (int y = 0; y < cv.Height; y += 3)
                    for (int x = 0; x < cv.Width; x += 3) { var p = cv.Get(x, y); h = (h ^ p.r ^ ((uint)p.g << 8) ^ ((uint)p.b << 16)) * 16777619; }
                Assert.IsTrue(hashes.Add(h), style + " looks like another floor");
            }
        }

        [Test]
        public void I141_I146_BuyingAGearPack_PutsItInTheLocker_ARefundTakesItBack()
        {
            var pack = Shop.Product(Shop.ArcadeGlow);
            foreach (var id in pack.Items)
            {
                var item = _c.Find(_c.Cosmetics, id);
                Assert.IsNotNull(item, id);
                Assert.AreEqual(pack.Id, item.shopPack);
                Assert.AreEqual(CosmeticCheck.ShopPack, Career.CanBuy(_d, item), "not for SP");
                Assert.AreEqual(0, item.cost);
            }
            Assert.IsTrue(Shop.Apply(_d, _c, new[] { Shop.ArcadeGlow }));
            foreach (var id in pack.Items) Assert.IsTrue(_d.ownedCosmetics.Contains(id));
            Assert.AreEqual(GearParts.Goggles, Gear.Parts("cosmetic.gear.glow_goggles"));
            Assert.IsTrue(Career.Equip(_d, _c.Find(_c.Cosmetics, "cosmetic.jersey.player_one")));
            Assert.IsFalse(Shop.Apply(_d, _c, new[] { Shop.ArcadeGlow }), "nothing new");

            // Refunded: the gear comes back out and the jersey slot falls back to a default you own.
            Assert.IsTrue(Shop.Apply(_d, _c, new string[0]));
            foreach (var id in pack.Items) Assert.IsFalse(_d.ownedCosmetics.Contains(id));
            Assert.AreNotEqual("cosmetic.jersey.player_one", _d.Equipped(CosmeticSlot.JerseyPalette));
            Assert.IsTrue(_d.ownedCosmetics.Contains(_d.Equipped(CosmeticSlot.JerseyPalette)));
        }

        [Test]
        public void I146_ShopGear_NeverShowsUpInTheSpShops()
        {
            foreach (var g in Shop.Gear)
            {
                var item = _c.Find(_c.Cosmetics, g.id);
                Assert.IsFalse(item.cost > 0 && item.store == null && !item.passOnly, g.id + " would be on the city shelves");
                Assert.IsTrue(Loc.Has(g.name), g.name);
            }
            Assert.AreEqual(20, Shop.Gear.Length);
        }

        [Test]
        public void I151_AllAccess_OwnsEverything_AndGivesPro_ButNotTips()
        {
            Shop.Apply(_d, _c, new[] { Shop.AllAccess });
            foreach (var p in Shop.Products)
            {
                if (p.Kind == ShopKind.Tip) Assert.IsFalse(Shop.Owns(_d, p.Id));
                else Assert.IsTrue(Shop.Owns(_d, p.Id), p.Id);
            }
            Assert.AreEqual(12, Shop.OwnedCourts(_d).Count);
            Assert.IsTrue(Shop.GivesPro(_d));
            Assert.IsTrue(Shop.DesignerPlusOwned(_d));
            foreach (var g in Shop.Gear) Assert.IsTrue(_d.ownedCosmetics.Contains(g.id));
            Assert.IsTrue(CustomTeams.HomeCourts(_c, _d).Exists(k => k.id == "court.shop.finals"), "a shop court can be your home court");
        }

        [Test]
        public void I141_Apply_IgnoresUnknownIds_AndTips()
        {
            Assert.IsTrue(Shop.Apply(_d, _c, new[] { "com.example.other", Shop.TipLarge, Shop.CityLights, Shop.CityLights }));
            Assert.AreEqual(1, _d.shopOwned.Count);
            Assert.IsTrue(Shop.CourtOpen(_d, "court.shop.penthouse"));
            Assert.IsFalse(Shop.CourtOpen(_d, "court.shop.finals"));
        }

        [Test]
        public void I145_DesignerPlus_OpensSlotsFloorsSkiesAndLogos()
        {
            Assert.AreEqual(8, CourtBuilder.Slots);
            Assert.AreEqual(3, Shop.CourtSlots(_d));
            Assert.IsTrue(Shop.FloorOpen(_d, (int)FloorStyle.Rubber));
            Assert.IsFalse(Shop.FloorOpen(_d, (int)FloorStyle.Parquet));
            Assert.IsFalse(Shop.SkyOpen(_d, CourtBuilder.Skies.Length - 1));
            Assert.IsTrue(Shop.MotifOpen(_d, (int)LogoMotif.Crane));
            Assert.IsFalse(Shop.MotifOpen(_d, (int)LogoMotif.Flag));
            Assert.AreEqual(CourtBuilder.FloorNames.Length, Enum.GetValues(typeof(FloorStyle)).Length);
            Assert.AreEqual(14, CourtBuilder.Skies.Length);

            Shop.Apply(_d, _c, new[] { Shop.DesignerPlus });
            Assert.AreEqual(8, Shop.CourtSlots(_d));
            Assert.IsTrue(Shop.FloorOpen(_d, (int)FloorStyle.Parquet));
            Assert.IsTrue(Shop.SkyOpen(_d, CourtBuilder.Skies.Length - 1));
            Assert.IsTrue(Shop.MotifOpen(_d, (int)LogoMotif.Flag));

            // A Plus court still builds and draws (and stays playable if Plus is ever refunded).
            var court = new CustomCourtData { built = true, floorStyle = (int)FloorStyle.Sunburst, sky = 13, logoMotif = (int)LogoMotif.Kite };
            Assert.IsTrue(Shop.UsesPlus(court, 0));
            var def = CourtBuilder.Build(court, 6);
            Assert.AreEqual((int)FloorStyle.Sunburst, def.floorStyle);
            Assert.AreEqual((int)LogoMotif.Kite, def.logoMotif);
            Assert.Greater(CourtGenerator.Generate(def, CourtGeometry.Default, 3).Width, 0);
            Assert.IsFalse(Shop.UsesPlus(new CustomCourtData(), 2));
            Assert.IsTrue(Shop.UsesPlus(new CustomCourtData(), 3), "slot 4 is a Plus slot");
        }

        [Test]
        public void I145_Randomize_UsesFreeOptionsOnly()
        {
            for (uint seed = 1; seed < 200; seed++)
            {
                var d = CourtBuilder.Randomize(seed, "R");
                Assert.IsFalse(Shop.UsesPlus(d, 0), "seed " + seed);
            }
        }

        [Test]
        public void I148_OneFreeTryPerCourtPerDay_AndNoneOnCourtsYouOwn()
        {
            const string court = "court.shop.laser";
            Assert.IsTrue(Shop.CanTry(_d, court, 100));
            Assert.IsTrue(Shop.UseTry(_d, court, 100));
            Assert.IsFalse(Shop.CanTry(_d, court, 100), "used today");
            Assert.IsFalse(Shop.UseTry(_d, court, 100));
            Assert.IsTrue(Shop.CanTry(_d, "court.shop.synthwave", 100), "each court has its own try");
            Assert.IsTrue(Shop.CanTry(_d, court, 101), "back tomorrow");
            Assert.IsFalse(Shop.CanTry(_d, "court.sunset_cage", 100), "only shop courts");
            for (int day = 0; day < 60; day++) Shop.UseTry(_d, court, 200 + day);
            Assert.LessOrEqual(_d.shopTries.Count, 40, "the list stays short");
            Shop.Apply(_d, _c, new[] { Shop.NeonNights });
            Assert.IsFalse(Shop.CanTry(_d, court, 999), "you own it: just play");
            var req = Shop.PlayOn(_c, _d, court);
            Assert.IsNotNull(req);
            Assert.AreEqual(court, req.CourtId);
            Assert.AreEqual(GameMode.QuickCall, req.Mode);
        }

        [Test]
        public void I149_Collection_AndASpotlightThatRotatesThroughEveryPack()
        {
            Shop.Collection(_d, out int have, out int total);
            Assert.AreEqual(0, have);
            Assert.AreEqual(32, total);
            Shop.Apply(_d, _c, new[] { Shop.NeonNights, Shop.ChampionGold });
            Shop.Collection(_d, out have, out _);
            Assert.AreEqual(8, have);
            var seen = new HashSet<string>();
            for (int w = 0; w < 8; w++) seen.Add(Shop.Spotlight(w).Id);
            Assert.AreEqual(8, seen.Count, "every court and gear pack takes a turn");
            Assert.AreEqual(Shop.Spotlight(3).Id, Shop.Spotlight(11).Id, "and comes back");
            Assert.AreEqual(Shop.Spotlight(-1).Id, Shop.Spotlight(7).Id);
            Assert.AreEqual(Shop.WeekNumber(new DateTime(2026, 11, 16)) + 1, Shop.WeekNumber(new DateTime(2026, 11, 23)));
        }

        [Test]
        public void I150_Tips_CountUp_AndTheFirstOneBringsTheBanner()
        {
            Assert.IsFalse(Shop.Supporter(_d));
            Assert.IsFalse(Shop.RecordTip(_d, Shop.NeonNights), "not a tip");
            Assert.IsTrue(Shop.RecordTip(_d, Shop.TipSmall));
            Assert.IsTrue(Shop.RecordTip(_d, Shop.TipLarge));
            Assert.AreEqual(2, _d.shopTips);
            Assert.IsTrue(Shop.Supporter(_d));
            Assert.IsTrue(_d.ownedCosmetics.Contains(Shop.SupporterBanner));
            Assert.IsTrue(Career.Equip(_d, _c.Find(_c.Cosmetics, Shop.SupporterBanner)));
            // A refund sync never takes the thank-you banner back (it isn't part of a gear pack).
            Shop.Apply(_d, _c, new string[0]);
            Assert.IsTrue(_d.ownedCosmetics.Contains(Shop.SupporterBanner));
        }

        [Test]
        public void I152_TermsArePlain_AndHonest()
        {
            string t = Shop.Terms(Shop.Product(Shop.CityLights), "$1.99");
            StringAssert.Contains("one-time purchase of $1.99", t);
            StringAssert.Contains("Not a subscription", t);
            StringAssert.Contains("never your ratings", t);
            StringAssert.Contains("RESTORE PURCHASES", t);
            string tip = Shop.Terms(Shop.Product(Shop.TipSmall), "");
            StringAssert.Contains("$0.99", tip, "falls back to the shop price");
            StringAssert.Contains("doesn't unlock anything", tip);
            Assert.AreEqual("", Shop.Terms(null, "$1"));
        }

        [Test]
        public void I155_ShopState_SurvivesTheSave_AndJunkIsDropped()
        {
            Shop.Apply(_d, _c, new[] { Shop.LegendsArena, Shop.StreetKings });
            Shop.RecordTip(_d, Shop.TipMedium);
            Shop.UseTry(_d, "court.shop.laser", 5);
            var back = SaveCodec.Decode(SaveCodec.Encode(_d), _c, out _);
            Assert.AreEqual(string.Join(",", _d.shopOwned), string.Join(",", back.shopOwned));
            Assert.AreEqual(1, back.shopTips);
            Assert.AreEqual(string.Join(",", _d.shopTries), string.Join(",", back.shopTries));
            Assert.IsTrue(back.ownedCosmetics.Contains("cosmetic.gear.crown_sleeves"));
            Assert.IsTrue(Shop.CourtOpen(back, "court.shop.throwback"));

            back.shopOwned.Add("not.a.product");
            back.shopOwned.Add(Shop.TipLarge);
            var again = SaveCodec.Decode(SaveCodec.Encode(back), _c, out _);
            Assert.AreEqual(2, again.shopOwned.Count, "only real one-time products are kept");
        }

        [Test]
        public void I155_NewWords_HaveSpanish()
        {
            foreach (var k in Shop.Courts) Assert.IsTrue(Loc.Has(k.Name) || k.Name == "Throwback '79", k.Name);
            foreach (var w in new[] { "SHOP", "COLLECTION", "THIS WEEK'S SPOTLIGHT", "COURT PACKS", "COURT DESIGNER", "GEAR PACKS", "EVERYTHING",
                                      "SUPPORT THE STUDIO", "TRY IT FREE", "TRIED TODAY", "IN THE SHOP", "OWNED", "BUY", "TIP", "PLUS", "TIPS",
                                      "Thanks for supporting the studio!", "COURT DESIGNER PLUS",
                                      "Everything here changes how the game looks, never your ratings. Nothing is random, and there are no ads.",
                                      "Waiting for approval (Ask to Buy). It unlocks as soon as it's approved." })
                Assert.IsTrue(Loc.Has(w), w);
            foreach (var n in new[] { "CHECKER", "SUNBURST", "PARQUET", "AURORA", "VAPOR", "DEEP SEA", "EMBER", "MINT", "GOLD HOUR" }) Assert.IsTrue(Loc.Has(n), n);
        }
    }
}
