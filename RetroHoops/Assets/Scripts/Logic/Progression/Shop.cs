using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>What kind of App Store product a shop item is.</summary>
    public enum ShopKind { CourtPack = 0, GearPack = 1, Designer = 2, Bundle = 3, Tip = 4 }

    /// <summary>One thing for sale in the SHOP (an App Store in-app purchase).</summary>
    public sealed class ShopProduct
    {
        public string Id, Name, Blurb, PriceFallback;
        public ShopKind Kind;
        /// <summary>Court ids (court packs) or cosmetic ids (gear packs) it unlocks.</summary>
        public string[] Items;
        /// <summary>Tips are consumable (can be bought again); everything else is a one-time, non-consumable unlock.</summary>
        public bool Consumable => Kind == ShopKind.Tip;

        public ShopProduct(string id, ShopKind kind, string name, string blurb, string price, params string[] items)
        {
            Id = id; Kind = kind; Name = name; Blurb = blurb; PriceFallback = price; Items = items ?? new string[0];
        }
    }

    /// <summary>A court sold in a court pack (drawn by the same generator as every other court).</summary>
    public sealed class ShopCourt
    {
        public string Id, Name, Description, Floor, Lines, Paint, SkyTop, SkyBottom;
        public float Crowd;
        public FloorStyle Style;
        public StandsStyle Stands;
        public int Motif;
        public LogoShape Shape;
        public string LogoColor;
    }

    /// <summary>
    /// I141 (20 Nov 2026, Omari: in-game purchases for cooler gear, cool designed courts and customizable courts, so
    /// players have their own look and the studio is paid to keep the game going). The SHOP's catalog and every rule
    /// about what a purchase unlocks, in plain logic so it is tested.
    ///
    /// What the other retro sports games on the App Store do (checked 20 Nov 2026): Retro Bowl sells a $0.99
    /// "Unlimited" unlock plus credit packs; Retro Goal a $1.99 unlock plus "Bux" packs and players; the bigger
    /// basketball games sell gems, season passes and random "loot box" items with ads on top. Retro Hoops keeps to the
    /// friendliest end of that: everything here is cosmetic (no ratings, no wins for sale), nothing is random, there
    /// are no ads and no countdown timers, every unlock is one-time and restorable, and the free game stays whole.
    /// The App Store decides what's owned; the career keeps the last answer so menus are right on launch.
    /// </summary>
    public static class Shop
    {
        public const string Prefix = "com.phoronomicstudios.retrohoops.";

        public const string NeonNights = Prefix + "courts.neon";
        public const string CoastToCoast = Prefix + "courts.coast";
        public const string CityLights = Prefix + "courts.city";
        public const string LegendsArena = Prefix + "courts.legends";
        public const string DesignerPlus = Prefix + "designer";
        public const string StreetKings = Prefix + "gear.streetkings";
        public const string ArcadeGlow = Prefix + "gear.arcade";
        public const string OldSchool = Prefix + "gear.oldschool";
        public const string ChampionGold = Prefix + "gear.champion";
        public const string AllAccess = Prefix + "allaccess";
        public const string TipSmall = Prefix + "tip.small";
        public const string TipMedium = Prefix + "tip.medium";
        public const string TipLarge = Prefix + "tip.large";

        /// <summary>
        /// I168: what a gear pack looks like on your player: its first jersey (with its second colour as trim), its shoes,
        /// and its gear piece.
        /// </summary>
        public static KitLook PreviewLook(ShopProduct pack, ContentCatalog c)
        {
            CosmeticDef jersey = null, shoes = null, gear = null;
            if (pack != null && c != null)
                foreach (var id in pack.Items)
                {
                    var item = c.Find(c.Cosmetics, id);
                    if (item == null) continue;
                    if (item.slot == CosmeticSlot.JerseyPalette && jersey == null) jersey = item;
                    else if (item.slot == CosmeticSlot.Shoes && shoes == null) shoes = item;
                    else if (item.slot == CosmeticSlot.Gear && gear == null) gear = item;
                }
            var look = KitLook.Classic(jersey?.colorA ?? RgbColor.White, jersey?.colorB ?? RgbColor.Black, jersey?.colorB ?? RgbColor.Black,
                                       shoes?.colorA, TeamPattern.Solid, null);
            if (gear != null)
            {
                look.Gear = GearPartsOf(gear.id);
                look.GearColor = gear.colorA;
            }
            return look;
        }

        /// <summary>The cosmetic a first tip puts in your locker (a thank-you; not sold on its own).</summary>
        public const string SupporterBanner = "cosmetic.banner.supporter";
        public const string ShopCourtPrefix = "court.shop.";

        // ------------------------------------------------------------------ I143: the designed courts

        public static readonly ShopCourt[] Courts =
        {
            // NEON NIGHTS
            C("court.shop.synthwave", "Synthwave Deck", "A glowing grid on the deck under a purple sky. Every bucket hums.",
              "#14002B", "#00F0FF", "#FF2E88", "#0B0019", "#FF2E88", 0.7f, FloorStyle.NeonGrid, StandsStyle.Crowd, (int)LogoMotif.Bolt, LogoShape.Hexagon, "#00F0FF"),
            C("court.shop.laser", "Laser Lanes", "Light beams fan out from centre court. Bring sunglasses at midnight.",
              "#1A0B2E", "#F8F32B", "#7B2CBF", "#10002B", "#4CC9F0", 0.6f, FloorStyle.Sunburst, StandsStyle.Crowd, (int)LogoMotif.Comet, LogoShape.Circle, "#F8F32B"),
            C("court.shop.nightmarket", "Night Market", "Checker tiles between the food stalls, lanterns overhead.",
              "#2D1E4F", "#FFE066", "#06D6A0", "#120A2A", "#F77F00", 0.65f, FloorStyle.Checker, StandsStyle.Fence, (int)LogoMotif.Lantern, LogoShape.Badge, "#FFE066"),
            // COAST TO COAST
            C("court.shop.boardwalk", "Boardwalk Pier", "Weathered boards at the end of the pier. Gulls are the crowd.",
              "#C9A27E", "#FFFFFF", "#2A9D8F", "#48CAE4", "#FFD6A5", 0.5f, FloorStyle.Parquet, StandsStyle.Fence, (int)LogoMotif.Wave, LogoShape.Circle, "#2A9D8F"),
            C("court.shop.palmshade", "Palm Shade", "Sun rays painted on the court in the palm grove.",
              "#F4A261", "#264653", "#E76F51", "#FFB703", "#FB8500", 0.55f, FloorStyle.Sunburst, StandsStyle.Crowd, (int)LogoMotif.Tree, LogoShape.Shield, "#264653"),
            C("court.shop.harbor", "Harbor Lights", "Navy and white tiles on the dock, the lighthouse sweeping the fog.",
              "#1D3557", "#F1FAEE", "#E63946", "#0D1B2A", "#457B9D", 0.5f, FloorStyle.Checker, StandsStyle.Brick, (int)LogoMotif.Lighthouse, LogoShape.Badge, "#F1FAEE"),
            // CITY LIGHTS
            C("court.shop.penthouse", "Penthouse", "Dark walnut parquet forty floors up, the city glittering below.",
              "#5C3D2E", "#F2E9E4", "#C9A227", "#0B132B", "#3A506B", 0.4f, FloorStyle.Parquet, StandsStyle.Crowd, (int)LogoMotif.Crown, LogoShape.Diamond, "#C9A227"),
            C("court.shop.mosaic", "Subway Mosaic", "A tiled court in the old station hall. The trains keep time.",
              "#E9ECEF", "#1B263B", "#2A9D8F", "#22223B", "#9A8C98", 0.6f, FloorStyle.Checker, StandsStyle.Brick, (int)LogoMotif.Signal, LogoShape.Circle, "#1B263B"),
            C("court.shop.crosswalk", "Crosswalk", "Street paint on a closed-off block party street. The whole block came out.",
              "#3A3A42", "#FFFFFF", "#FFD166", "#1B1B3A", "#F15BB5", 0.75f, FloorStyle.Sunburst, StandsStyle.Crowd, (int)LogoMotif.Ball, LogoShape.Hexagon, "#FFD166"),
            // LEGENDS ARENA
            C("court.shop.goldstandard", "Gold Standard", "Polished gold maple under a packed house. Win here and they remember.",
              "#D4A017", "#1A1A2E", "#7B2CBF", "#1A1A2E", "#3A3A55", 0.95f, FloorStyle.Hardwood, StandsStyle.Crowd, (int)LogoMotif.Crown, LogoShape.Shield, "#7B2CBF"),
            C("court.shop.throwback", "Throwback '79", "Orange and brown parquet, short shorts and tube socks.",
              "#B5651D", "#FFF3B0", "#6F1D1B", "#432818", "#BB9457", 0.85f, FloorStyle.Parquet, StandsStyle.Crowd, (int)LogoMotif.Owl, LogoShape.Circle, "#FFF3B0"),
            C("court.shop.finals", "Championship Night", "Royal blue rays out from the centre circle. The biggest night of the year.",
              "#1D4ED8", "#FFFFFF", "#FBBF24", "#0B132B", "#1E3A8A", 1f, FloorStyle.Sunburst, StandsStyle.Crowd, (int)LogoMotif.Flag, LogoShape.Diamond, "#FBBF24"),
        };

        private static ShopCourt C(string id, string name, string description, string floor, string lines, string paint, string skyTop, string skyBottom,
                                   float crowd, FloorStyle style, StandsStyle stands, int motif, LogoShape shape, string logo) =>
            new ShopCourt
            {
                Id = id, Name = name, Description = description, Floor = floor, Lines = lines, Paint = paint, SkyTop = skyTop, SkyBottom = skyBottom,
                Crowd = crowd, Style = style, Stands = stands, Motif = motif, Shape = shape, LogoColor = logo,
            };

        // ------------------------------------------------------------------ I146: the gear

        /// <summary>Gear in the shop's gear packs: id, name, slot, two colours, and (for the Gear slot) which parts.</summary>
        public static readonly (string id, string name, CosmeticSlot slot, string a, string b, GearParts parts)[] Gear =
        {
            ("cosmetic.jersey.kingpin", "Kingpin", CosmeticSlot.JerseyPalette, "#4B1F7A", "#FFD166", GearParts.None),
            ("cosmetic.jersey.blacktop_royal", "Blacktop Royal", CosmeticSlot.JerseyPalette, "#1B1B2F", "#E0AAFF", GearParts.None),
            ("cosmetic.shoes.crown_highs", "Crown Highs", CosmeticSlot.Shoes, "#FFD166", "#4B1F7A", GearParts.None),
            ("cosmetic.banner.kings_court", "Kings Court", CosmeticSlot.CourtBanner, "#4B1F7A", "#FFD166", GearParts.None),
            ("cosmetic.gear.crown_sleeves", "Crown Sleeves", CosmeticSlot.Gear, "#FFD166", "#FFFFFF", GearParts.ArmSleeves),

            ("cosmetic.jersey.player_one", "Player One", CosmeticSlot.JerseyPalette, "#00F5D4", "#F15BB5", GearParts.None),
            ("cosmetic.jersey.high_score", "High Score", CosmeticSlot.JerseyPalette, "#FEE440", "#9B5DE5", GearParts.None),
            ("cosmetic.shoes.pixel_kicks", "Pixel Kicks", CosmeticSlot.Shoes, "#9B5DE5", "#00F5D4", GearParts.None),
            ("cosmetic.banner.insert_coin", "Insert Coin", CosmeticSlot.CourtBanner, "#F15BB5", "#FEE440", GearParts.None),
            ("cosmetic.gear.glow_goggles", "Glow Goggles", CosmeticSlot.Gear, "#00F5D4", "#FFFFFF", GearParts.Goggles),

            ("cosmetic.jersey.tube_sock", "Tube Sock Classic", CosmeticSlot.JerseyPalette, "#F4F1DE", "#E07A5F", GearParts.None),
            ("cosmetic.jersey.disco_gold", "Disco Gold", CosmeticSlot.JerseyPalette, "#C9A227", "#5C3D2E", GearParts.None),
            ("cosmetic.shoes.canvas_lows", "Canvas Lows", CosmeticSlot.Shoes, "#F4F1DE", "#3D405B", GearParts.None),
            ("cosmetic.banner.funk_pennant", "Funk Pennant", CosmeticSlot.CourtBanner, "#E07A5F", "#F2CC8F", GearParts.None),
            ("cosmetic.gear.knee_pads_79", "'79 Knee Pads", CosmeticSlot.Gear, "#E07A5F", "#FFFFFF", GearParts.LegSleeves),

            ("cosmetic.jersey.ring_night", "Ring Night", CosmeticSlot.JerseyPalette, "#D4A017", "#111111", GearParts.None),
            ("cosmetic.jersey.platinum", "Platinum", CosmeticSlot.JerseyPalette, "#E5E4E2", "#1F2A44", GearParts.None),
            ("cosmetic.shoes.gold_highs", "24K Highs", CosmeticSlot.Shoes, "#D4A017", "#FFF8DC", GearParts.None),
            ("cosmetic.banner.banner_season", "Banner Season", CosmeticSlot.CourtBanner, "#D4A017", "#1F2A44", GearParts.None),
            ("cosmetic.gear.champion_compression", "Champion Compression", CosmeticSlot.Gear, "#D4A017", "#FFFFFF", GearParts.ArmSleeves | GearParts.LegSleeves),
        };

        /// <summary>Sleeves, goggles and pads for the shop's gear items (None for anything else).</summary>
        public static GearParts GearPartsOf(string cosmeticId)
        {
            foreach (var g in Gear) if (g.id == cosmeticId) return g.parts;
            return GearParts.None;
        }

        private static string[] GearIds(int from) => new[] { Gear[from].id, Gear[from + 1].id, Gear[from + 2].id, Gear[from + 3].id, Gear[from + 4].id };

        // ------------------------------------------------------------------ I141: the catalog

        public static readonly ShopProduct[] Products =
        {
            new ShopProduct(NeonNights, ShopKind.CourtPack, "NEON NIGHTS", "Three glowing night courts: Synthwave Deck, Laser Lanes and Night Market.", "$1.99",
                            "court.shop.synthwave", "court.shop.laser", "court.shop.nightmarket"),
            new ShopProduct(CoastToCoast, ShopKind.CourtPack, "COAST TO COAST", "Three courts by the water: Boardwalk Pier, Palm Shade and Harbor Lights.", "$1.99",
                            "court.shop.boardwalk", "court.shop.palmshade", "court.shop.harbor"),
            new ShopProduct(CityLights, ShopKind.CourtPack, "CITY LIGHTS", "Three courts in the city: Penthouse, Subway Mosaic and Crosswalk.", "$1.99",
                            "court.shop.penthouse", "court.shop.mosaic", "court.shop.crosswalk"),
            new ShopProduct(LegendsArena, ShopKind.CourtPack, "LEGENDS ARENA", "Three big-night arenas: Gold Standard, Throwback '79 and Championship Night.", "$1.99",
                            "court.shop.goldstandard", "court.shop.throwback", "court.shop.finals"),
            new ShopProduct(DesignerPlus, ShopKind.Designer, "COURT DESIGNER PLUS",
                            "Build up to 8 courts of your own (3 free), with 3 more floors (checker, sunburst, parquet), 6 more skies and every centre-court logo.", "$2.99"),
            new ShopProduct(StreetKings, ShopKind.GearPack, "STREET KINGS GEAR", "Two jerseys, Crown Highs, a court banner and Crown Sleeves, in purple and gold.", "$1.99", GearIds(0)),
            new ShopProduct(ArcadeGlow, ShopKind.GearPack, "ARCADE GLOW GEAR", "Two jerseys, Pixel Kicks, the Insert Coin banner and Glow Goggles.", "$1.99", GearIds(5)),
            new ShopProduct(OldSchool, ShopKind.GearPack, "OLD SCHOOL '79 GEAR", "Two jerseys, Canvas Lows, the Funk Pennant and '79 Knee Pads.", "$1.99", GearIds(10)),
            new ShopProduct(ChampionGold, ShopKind.GearPack, "CHAMPION GOLD GEAR", "Ring Night and Platinum jerseys, 24K Highs, a title banner and gold compression.", "$2.99", GearIds(15)),
            new ShopProduct(AllAccess, ShopKind.Bundle, "ALL-ACCESS",
                            "Everything in the shop (all 12 courts, Court Designer Plus and all 4 gear packs) plus Retro Hoops Pro, in one purchase.", "$12.99"),
            new ShopProduct(TipSmall, ShopKind.Tip, "COURTSIDE SNACK", "A small thank-you to the one-person studio that makes Retro Hoops.", "$0.99"),
            new ShopProduct(TipMedium, ShopKind.Tip, "FRESH PAIR OF KICKS", "A bigger thank-you. Keeps the updates coming.", "$2.99"),
            new ShopProduct(TipLarge, ShopKind.Tip, "MVP SUPPORTER", "The biggest thank-you. You're the reason this game keeps growing.", "$9.99"),
        };

        public static ShopProduct Product(string id) => Array.Find(Products, p => p.Id == id);

        public static string[] ProductIds()
        {
            var ids = new string[Products.Length];
            for (int i = 0; i < Products.Length; i++) ids[i] = Products[i].Id;
            return ids;
        }

        /// <summary>The pack a court or cosmetic comes in (null if it isn't a shop item).</summary>
        public static ShopProduct PackOf(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            foreach (var p in Products)
                if (Array.IndexOf(p.Items, itemId) >= 0) return p;
            return null;
        }

        public static bool IsShopCourt(string courtId) => courtId != null && courtId.StartsWith(ShopCourtPrefix, StringComparison.Ordinal);

        // ------------------------------------------------------------------ content

        /// <summary>Puts the shop's courts and gear in the catalog (locked until bought). Called by DefaultContent.</summary>
        public static void AddContent(ContentCatalog c)
        {
            foreach (var k in Courts)
            {
                if (c.Court(k.Id) != null) continue;
                c.Courts.Add(new CourtDef
                {
                    id = k.Id, displayName = k.Name, circuit = CourtCircuit.Shop, description = k.Description,
                    floor = RgbColor.FromHex(k.Floor), lines = RgbColor.FromHex(k.Lines), paint = RgbColor.FromHex(k.Paint),
                    skyTop = RgbColor.FromHex(k.SkyTop), skyBottom = RgbColor.FromHex(k.SkyBottom), crowdDensity = k.Crowd,
                    floorStyle = (int)k.Style, stands = k.Stands, logoMotif = k.Motif, logoShape = k.Shape, logoColor = RgbColor.FromHex(k.LogoColor),
                });
            }
            foreach (var g in Gear)
            {
                if (c.Find(c.Cosmetics, g.id) != null) continue;
                c.Cosmetics.Add(new CosmeticDef
                {
                    id = g.id, displayName = g.name, slot = g.slot, cost = 0, colorA = RgbColor.FromHex(g.a), colorB = RgbColor.FromHex(g.b),
                    shopPack = PackOf(g.id)?.Id,
                });
            }
            if (c.Find(c.Cosmetics, SupporterBanner) == null)
                c.Cosmetics.Add(new CosmeticDef
                {
                    id = SupporterBanner, displayName = "Studio Supporter", slot = CosmeticSlot.CourtBanner, cost = 0,
                    colorA = RgbColor.FromHex("#FF6B8B"), colorB = RgbColor.FromHex("#FFD166"), shopPack = TipSmall,
                });
        }

        // ------------------------------------------------------------------ I141 + I151: who owns what

        /// <summary>True if the career owns this product (directly, or through ALL-ACCESS). Tips are never "owned".</summary>
        public static bool Owns(CareerSaveData d, string productId)
        {
            if (d == null || d.shopOwned == null || string.IsNullOrEmpty(productId)) return false;
            var p = Product(productId);
            if (p != null && p.Consumable) return false;
            if (d.shopOwned.Contains(productId)) return true;
            return p != null && p.Kind != ShopKind.Bundle && d.shopOwned.Contains(AllAccess);
        }

        /// <summary>ALL-ACCESS includes Retro Hoops Pro.</summary>
        public static bool GivesPro(CareerSaveData d) => d != null && d.shopOwned != null && d.shopOwned.Contains(AllAccess);

        /// <summary>A shop court is playable once its pack is owned (other courts aren't the shop's business).</summary>
        public static bool CourtOpen(CareerSaveData d, string courtId)
        {
            if (!IsShopCourt(courtId)) return true;
            if (d?.shopEarned != null && d.shopEarned.Contains(courtId)) return true; // I166: a LOYALTY COURT
            var pack = PackOf(courtId);
            return pack != null && Owns(d, pack.Id);
        }

        /// <summary>The shop courts this career can play, in shop order.</summary>
        public static List<string> OwnedCourts(CareerSaveData d)
        {
            var list = new List<string>();
            foreach (var k in Courts) if (CourtOpen(d, k.Id)) list.Add(k.Id);
            return list;
        }

        /// <summary>
        /// Records the App Store's settled answer (the non-consumables it says are owned). New gear goes in the locker;
        /// gear from a refunded pack comes back out (and anything equipped from it is swapped for a default by
        /// <see cref="Career.EnsureDefaults"/>). Returns true if anything changed (the caller saves).
        /// </summary>
        public static bool Apply(CareerSaveData d, ContentCatalog c, IEnumerable<string> ownedProductIds)
        {
            if (d == null) return false;
            d.shopOwned = d.shopOwned ?? new List<string>();
            d.ownedCosmetics = d.ownedCosmetics ?? new List<string>();
            var now = new List<string>();
            if (ownedProductIds != null)
                foreach (var id in ownedProductIds)
                {
                    var p = Product(id);
                    if (p != null && !p.Consumable && !now.Contains(id)) now.Add(id);
                }
            bool changed = now.Count != d.shopOwned.Count || !now.TrueForAll(d.shopOwned.Contains);
            d.shopOwned.Clear();
            d.shopOwned.AddRange(now);
            foreach (var p in Products)
            {
                if (p.Kind != ShopKind.GearPack) continue;
                bool owned = Owns(d, p.Id);
                foreach (var item in p.Items)
                {
                    bool has = d.ownedCosmetics.Contains(item);
                    if (owned && !has) { d.ownedCosmetics.Add(item); changed = true; }
                    else if (!owned && has) { d.ownedCosmetics.Remove(item); changed = true; }
                }
            }
            if (changed && c != null) Career.EnsureDefaults(d, c);
            return changed;
        }

        // ------------------------------------------------------------------ I145: Court Designer Plus

        public const int FreeCourtSlots = 3;
        public const int FreeFloors = 5;
        public const int FreeSkies = 8;
        /// <summary>Centre-court logos 0..10 are free; the season logos after them come with Designer Plus.</summary>
        public const int FreeMotifs = 11;

        public static bool DesignerPlusOwned(CareerSaveData d) => Owns(d, DesignerPlus);
        public static int CourtSlots(CareerSaveData d) => DesignerPlusOwned(d) ? CourtBuilder.Slots : FreeCourtSlots;
        public static bool FloorOpen(CareerSaveData d, int floor) => floor < FreeFloors || DesignerPlusOwned(d);
        public static bool SkyOpen(CareerSaveData d, int sky) => sky < FreeSkies || DesignerPlusOwned(d);
        public static bool MotifOpen(CareerSaveData d, int motif) => motif < FreeMotifs || DesignerPlusOwned(d);

        /// <summary>A built court that uses Plus options or a Plus slot still plays (it's yours), but can't be edited further without Plus.</summary>
        public static bool UsesPlus(CustomCourtData court, int slot) =>
            court != null && (slot >= FreeCourtSlots || court.floorStyle >= FreeFloors || court.sky >= FreeSkies || court.logoMotif >= FreeMotifs);

        // ------------------------------------------------------------------ I148: try before you buy

        public const int TriesPerDay = 1;

        /// <summary>One free game a day on each shop court you don't own yet.</summary>
        public static bool CanTry(CareerSaveData d, string courtId, int day)
        {
            if (d == null || !IsShopCourt(courtId) || CourtOpen(d, courtId)) return false;
            return d.shopTries == null || !d.shopTries.Contains(TryKey(courtId, day));
        }

        /// <summary>Uses today's try on a court. Keeps the last 40 tries.</summary>
        public static bool UseTry(CareerSaveData d, string courtId, int day)
        {
            if (!CanTry(d, courtId, day)) return false;
            d.shopTries = d.shopTries ?? new List<string>();
            d.shopTries.Add(TryKey(courtId, day));
            while (d.shopTries.Count > 40) d.shopTries.RemoveAt(0);
            return true;
        }

        private static string TryKey(string courtId, int day) => courtId + "@" + day;

        /// <summary>A Quick Call on a shop court with your team (to play one you own, or today's free try).</summary>
        public static MatchRequest PlayOn(ContentCatalog c, CareerSaveData career, string courtId)
        {
            if (c == null || career == null || c.Court(courtId) == null) return null;
            var mine = Secrets.PlayableTeams(c, career.secrets);
            var league = c.TeamsInTier(TeamTier.League);
            if (mine.Count == 0 || league.Count == 0) return null;
            var opp = league.Find(t => t.id != mine[0].id) ?? league[0];
            return new MatchRequest
            {
                Mode = GameMode.QuickCall, HomeTeamId = mine[0].id, AwayTeamId = opp.id, CourtId = courtId,
                DifficultyId = career.settings.difficultyId,
            };
        }

        // ------------------------------------------------------------------ I149: collection and spotlight

        /// <summary>How many shop courts and gear items you own, out of all of them.</summary>
        public static void Collection(CareerSaveData d, out int owned, out int total)
        {
            owned = 0;
            total = Courts.Length + Gear.Length;
            foreach (var k in Courts) if (CourtOpen(d, k.Id)) owned++;
            foreach (var g in Gear) if (d?.ownedCosmetics != null && d.ownedCosmetics.Contains(g.id)) owned++;
        }

        /// <summary>
        /// The pack in the spotlight this week (a court pack or a gear pack, in turn). It's a fixed rotation by the week
        /// number: every pack comes back, it isn't cheaper, and there's no countdown.
        /// </summary>
        public static ShopProduct Spotlight(int weekNumber)
        {
            var packs = new List<ShopProduct>();
            foreach (var p in Products) if (p.Kind == ShopKind.CourtPack || p.Kind == ShopKind.GearPack) packs.Add(p);
            int i = ((weekNumber % packs.Count) + packs.Count) % packs.Count;
            return packs[i];
        }

        /// <summary>Weeks since 1 Jan 2024 (Monday-based), for the spotlight.</summary>
        public static int WeekNumber(DateTime day) => (int)Math.Floor((day.Date - new DateTime(2024, 1, 1)).TotalDays / 7.0);

        // ------------------------------------------------------------------ I150: the tip jar

        /// <summary>Records a tip the App Store delivered. The first one puts the Studio Supporter banner in your locker.</summary>
        public static bool RecordTip(CareerSaveData d, string productId)
        {
            var p = Product(productId);
            if (d == null || p == null || p.Kind != ShopKind.Tip) return false;
            d.shopTips++;
            d.ownedCosmetics = d.ownedCosmetics ?? new List<string>();
            if (!d.ownedCosmetics.Contains(SupporterBanner)) d.ownedCosmetics.Add(SupporterBanner);
            return true;
        }

        public static bool Supporter(CareerSaveData d) => d != null && d.shopTips > 0;

        // ------------------------------------------------------------------ I152: plain terms (App Review 3.1.1)

        /// <summary>What a purchase is, in plain words, under its BUY button.</summary>
        public static string Terms(ShopProduct p, string price)
        {
            if (p == null) return "";
            string pr = string.IsNullOrEmpty(price) ? p.PriceFallback : price;
            if (p.Consumable)
                return Loc.Language == Loc.Spanish
                    ? "Una propina de " + pr + " al estudio. No desbloquea nada más que un agradecimiento (la pancarta Studio Supporter con la primera). Puedes dar más de una."
                    : "A " + pr + " tip to the studio. It doesn't unlock anything except a thank-you (the Studio Supporter banner with your first). You can tip more than once.";
            return Loc.Language == Loc.Spanish
                ? "Compra única de " + pr + ". No es una suscripción. Solo cambia cómo se ve el juego, no tus valoraciones. Se carga a tu cuenta de Apple al confirmar; usa RESTAURAR COMPRAS en otro dispositivo."
                : "A one-time purchase of " + pr + ". Not a subscription. It changes how the game looks, never your ratings. Charged to your Apple Account when you confirm; use RESTORE PURCHASES on another device.";
        }
    }
}
