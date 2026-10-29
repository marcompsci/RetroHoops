using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// 17 Oct 2026 (Omari, phase 3): the CORNER STORE. Six pieces of the game's own gear on the shelves each day
    /// (jerseys, shoes, celebrations, dribble moves, dunk packages), bought with SP through the same rules as the Locker
    /// Room (fans needed, Hoops Pass items never sold), and anything you own can be put on from here.
    /// </summary>
    public static class CityShop
    {
        public const int StockSize = 6;

        public static string SlotName(CosmeticSlot slot)
        {
            switch (slot)
            {
                case CosmeticSlot.JerseyPalette: return "JERSEY COLOURS";
                case CosmeticSlot.Shoes: return "SHOES";
                case CosmeticSlot.CourtBanner: return "COURT BANNER";
                case CosmeticSlot.Celebration: return "CELEBRATION";
                case CosmeticSlot.DribbleMove: return "DRIBBLE MOVE";
                case CosmeticSlot.Gear: return "GEAR";
                default: return "DUNK PACKAGE";
            }
        }

        /// <summary>Everything the store can ever carry (not free starters, never Hoops Pass items), in a fixed order.</summary>
        public static List<CosmeticDef> Catalog(ContentCatalog c)
        {
            var list = c?.Cosmetics.FindAll(x => !x.passOnly && !x.unlockedByDefault && x.cost > 0) ?? new List<CosmeticDef>();
            list.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            return list;
        }

        /// <summary>Today's shelves: the same six all day, a new mix tomorrow.</summary>
        public static List<CosmeticDef> Stock(ContentCatalog c, int day)
        {
            var all = Catalog(c);
            var stock = new List<CosmeticDef>();
            if (all.Count == 0) return stock;
            uint h = StableHash.Of("store:" + day);
            int start = (int)(h % (uint)all.Count);
            int step = 1 + (int)((h / 7) % (uint)Math.Max(1, all.Count - 1));
            while (Gcd(step, all.Count) != 1) step++;
            for (int i = 0; i < Math.Min(StockSize, all.Count); i++) stock.Add(all[(start + i * step) % all.Count]);
            return stock;
        }

        private static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);

        /// <summary>The store's choices for today.</summary>
        public static List<CityActivity> Choices(CareerSaveData d, ContentCatalog c, int day)
        {
            var list = new List<CityActivity>();
            if (d == null || c == null) return list;
            foreach (var item in Stock(c, day))
            {
                string name = item.displayName.ToUpperInvariant();
                string slot = Loc.T(SlotName(item.slot));
                if (d.ownedCosmetics.Contains(item.id))
                {
                    bool on = d.Equipped(item.slot) == item.id;
                    list.Add(new CityActivity
                    {
                        Label = (on ? Loc.T("WEARING") : Loc.T("PUT ON")) + ": " + name, ItemId = item.id,
                        Action = on ? CityAction.Info : CityAction.Equip, Detail = slot + "  ·  " + Loc.T("OWNED"),
                    });
                    continue;
                }
                var check = Career.CanBuy(d, item);
                string why = check == CosmeticCheck.NeedsFans ? Loc.T("Needs") + " " + item.fansRequired + " " + Loc.T("fans") + " (" + d.fans + ")."
                           : check == CosmeticCheck.NotEnoughPoints ? Loc.T("Needs") + " " + item.cost + " SP (" + d.signalPoints + ")."
                           : null;
                list.Add(new CityActivity
                {
                    Label = Loc.T("BUY") + ": " + name + "  " + item.cost + " SP", ItemId = item.id, Price = item.cost,
                    Action = CityAction.Buy, Detail = slot, Locked = why,
                });
                // 17 Oct 2026: or pay with street cash (CASHED OUT), once you have some.
                long price = StreetCash.Price(item);
                long have = d.streetCash?.cash ?? 0;
                if (have > 0)
                    list.Add(new CityActivity
                    {
                        Label = Loc.T("PAY CASH") + ": " + name + "  " + StreetCash.Money(price), ItemId = item.id, Action = CityAction.BuyWithCash,
                        Detail = slot + "  ·  " + Loc.T("street cash"),
                        Locked = check == CosmeticCheck.NeedsFans ? why : have < price ? Loc.T("Needs") + " " + StreetCash.Money(price) + " (" + StreetCash.Money(have) + ")." : null,
                    });
            }
            return list;
        }

        /// <summary>Buys (and puts on) an item from today's shelves. Counts toward FRESH GEAR.</summary>
        public static CosmeticCheck Buy(CareerSaveData d, ContentCatalog c, string itemId, int day)
        {
            var item = Stock(c, day).Find(x => x.id == itemId);
            if (item == null) return CosmeticCheck.Unknown; // not on today's shelves
            var result = Career.Buy(d, item);
            if (result == CosmeticCheck.Ok)
            {
                if (d.city == null) d.city = new CitySaveData();
                d.city.purchases++;
            }
            return result;
        }

        /// <summary>Buys (and puts on) an item from today's shelves with street cash (an item's SP price × 100).</summary>
        public static CosmeticCheck BuyWithCash(CareerSaveData d, ContentCatalog c, string itemId, int day)
        {
            var item = Stock(c, day).Find(x => x.id == itemId);
            if (item == null || d == null) return CosmeticCheck.Unknown;
            if (d.ownedCosmetics.Contains(item.id)) return CosmeticCheck.AlreadyOwned;
            if (item.passOnly) return CosmeticCheck.PassOnly;
            if (d.fans < item.fansRequired) return CosmeticCheck.NeedsFans;
            if (!StreetCash.Spend(d, StreetCash.Price(item))) return CosmeticCheck.NotEnoughPoints;
            d.ownedCosmetics.Add(item.id);
            d.SetEquipped(item.slot, item.id);
            if (d.city == null) d.city = new CitySaveData();
            d.city.purchases++;
            return CosmeticCheck.Ok;
        }

        public static bool Equip(CareerSaveData d, ContentCatalog c, string itemId) => Career.Equip(d, c?.Cosmetics.Find(x => x.id == itemId));
    }

    /// <summary>
    /// 17 Oct 2026 (Omari, phase 3): FRESH CUTS. A new hairstyle or hair colour for your own player, paid in SP (the
    /// first cut is free). Needs a player you made in the Locker Room (the default Rook keeps his look).
    /// </summary>
    public static class CityBarber
    {
        public const int CutPrice = 40, ColorPrice = 25;
        public static readonly string[] StyleNames = { "BUZZ", "SHORT", "HIGH-TOP FADE", "AFRO", "HEADBAND", "LOCS" };
        public static readonly string[] ColorNames = { "BLACK", "DARK BROWN", "BROWN", "BLONDE", "RED" };

        public static bool CanCut(CareerSaveData d) => d?.customPlayer != null && d.customPlayer.created;

        public static int PriceFor(CareerSaveData d, int basePrice) => d?.city != null && !d.city.freeCutUsed ? 0 : basePrice;

        public static List<CityActivity> Choices(CareerSaveData d)
        {
            var list = new List<CityActivity>();
            if (d == null) return list;
            if (!CanCut(d))
            {
                list.Add(new CityActivity { Label = "SIT DOWN", Locked = "Make your own player in the LOCKER ROOM first." });
                return list;
            }
            var p = d.customPlayer;
            int cut = PriceFor(d, CutPrice), color = PriceFor(d, ColorPrice);
            for (int i = 0; i < StyleNames.Length && i < PlayerCreator.HairStyleCount; i++)
            {
                bool current = p.hairStyle == i;
                list.Add(new CityActivity
                {
                    Label = Loc.T(StyleNames[i]) + (current ? "  ·  " + Loc.T("YOURS") : "  " + (cut == 0 ? Loc.T("FREE") : cut + " SP")),
                    Action = current ? CityAction.Info : CityAction.Haircut, ItemId = "style:" + i, Price = cut,
                    Locked = !current && d.signalPoints < cut ? Loc.T("Needs") + " " + cut + " SP (" + d.signalPoints + ")." : null,
                    Detail = current ? null : Loc.T("CUT"),
                });
            }
            for (int i = 0; i < ColorNames.Length && i < PlayerCreator.HairColorCount; i++)
            {
                bool current = p.hairColor == i;
                list.Add(new CityActivity
                {
                    Label = Loc.T("COLOUR") + ": " + Loc.T(ColorNames[i]) + (current ? "  ·  " + Loc.T("YOURS") : "  " + (color == 0 ? Loc.T("FREE") : color + " SP")),
                    Action = current ? CityAction.Info : CityAction.Haircut, ItemId = "color:" + i, Price = color,
                    Locked = !current && d.signalPoints < color ? Loc.T("Needs") + " " + color + " SP (" + d.signalPoints + ")." : null,
                });
            }
            return list;
        }

        /// <summary>Applies "style:N" or "color:N". Returns false (nothing changes) if it can't be done.</summary>
        public static bool Apply(CareerSaveData d, string choice)
        {
            if (!CanCut(d) || string.IsNullOrEmpty(choice)) return false;
            var parts = choice.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[1], out int v)) return false;
            bool style = parts[0] == "style";
            if (!style && parts[0] != "color") return false;
            int count = style ? Math.Min(StyleNames.Length, PlayerCreator.HairStyleCount) : Math.Min(ColorNames.Length, PlayerCreator.HairColorCount);
            if (v < 0 || v >= count) return false;
            var p = d.customPlayer;
            if ((style ? p.hairStyle : p.hairColor) == v) return false;
            int price = PriceFor(d, style ? CutPrice : ColorPrice);
            if (d.signalPoints < price) return false;
            d.signalPoints -= price;
            if (style) p.hairStyle = v; else p.hairColor = v;
            if (d.city == null) d.city = new CitySaveData();
            d.city.freeCutUsed = true;
            d.city.haircuts++;
            return true;
        }
    }

    /// <summary>
    /// 17 Oct 2026 (Omari, phase 3): the city MAP. Where each district sits (a small grid), how they connect, and fast
    /// travel: any outdoor district you've already walked into, arriving at its main entrance.
    /// </summary>
    public static class CityAtlas
    {
        /// <summary>Grid spots (column, row; row 0 at the bottom) for the outdoor districts.</summary>
        public static readonly (string id, int col, int row)[] Spots =
        {
            (CityDistricts.SchoolYard, 0, 0), (CityDistricts.HolidayPark, 1, 0),
            (CityDistricts.FirstCallers, 1, 1), (CityDistricts.Blacktop, 2, 1), (CityDistricts.Boardwalk, 3, 1),
            (CityDistricts.HoopHeights, 1, 2), (CityDistricts.Downtown, 2, 2),
        };

        /// <summary>28 Oct 2026 (BALLER WORLD): the other regions' district maps (each region has its own little map).</summary>
        public static readonly (string region, string id, int col, int row)[] RegionSpots =
        {
            (WorldRegions.NewYork, CityDistricts.NyUptown, 0, 0), (WorldRegions.NewYork, CityDistricts.NyMidtown, 0, 1),
            (WorldRegions.LosAngeles, CityDistricts.LaBeach, 0, 0), (WorldRegions.LosAngeles, CityDistricts.LaEastside, 0, 1),
            (WorldRegions.LasVegas, CityDistricts.LvStrip, 0, 0), (WorldRegions.LasVegas, CityDistricts.LvOldTown, 0, 1),
            (WorldRegions.TheBay, CityDistricts.BayHills, 0, 0), (WorldRegions.TheBay, CityDistricts.BayTown, 1, 0),
            // 28 Oct 2026: the bigger grid districts, east of each region's second district.
            (WorldRegions.NewYork, CityDistricts.NyAvenues, 1, 1), (WorldRegions.LosAngeles, CityDistricts.LaBoulevard, 1, 1),
            (WorldRegions.LasVegas, CityDistricts.LvNeonMile, 1, 0), (WorldRegions.TheBay, CityDistricts.BayCrosstown, 2, 0),
        };

        /// <summary>The district map of a region: (id, col, row) for each of its districts.</summary>
        public static List<(string id, int col, int row)> SpotsFor(string region)
        {
            var list = new List<(string, int, int)>();
            if (region == WorldRegions.BallerCity) { foreach (var s in Spots) list.Add(s); return list; }
            foreach (var s in RegionSpots) if (s.region == region) list.Add((s.id, s.col, s.row));
            return list;
        }

        public static bool OnMap(string district) => Array.Exists(Spots, s => s.id == district) || Array.Exists(RegionSpots, s => s.id == district);

        /// <summary>Indoors (the gym, the shops) the map shows the district the building is in.</summary>
        public static string Outside(string district) => OnMap(district) ? district : CityDistricts.OutsideOf(district);

        /// <summary>Fast travel is within a region (between regions you drive).</summary>
        public static bool CanTravel(CitySaveData s, string from, string to) =>
            s != null && OnMap(to) && s.seen.Contains(to) && Outside(from) != to
            && WorldRegions.RegionOf(Outside(from)).Id == WorldRegions.RegionOf(to).Id;

        /// <summary>Where fast travel drops you: the district's first spawn (its main road in).</summary>
        public static string ArrivalSpawn(string district) => CityDistricts.Map(district).DefaultSpawn.Id;

        /// <summary>Fast travel: saves the arrival and counts it. False if you can't go there yet.</summary>
        public static bool Travel(CitySaveData s, string from, string to)
        {
            if (!CanTravel(s, from, to)) return false;
            s.Arrive(to, ArrivalSpawn(to));
            s.fastTravels++;
            return true;
        }

        /// <summary>One line for a district on the map: courts, crews beaten, people out.</summary>
        public static string Summary(CitySaveData s, string district)
        {
            var m = CityDistricts.Map(district);
            int courts = m.Courts.FindAll(k => k.Use != CityCourtUse.Practice).Count, crews = 0, beaten = 0;
            foreach (var k in m.Courts)
            {
                var crew = CityCrews.AtCourt(k.CourtId);
                if (crew == null) continue;
                crews++;
                if (s != null && s.crewsBeaten.Contains(crew.Id)) beaten++;
            }
            return courts + " " + Loc.T(courts == 1 ? "COURT" : "COURTS") + (crews > 0 ? "  ·  " + Loc.T("CREWS") + " " + beaten + "/" + crews : "");
        }
    }
}
