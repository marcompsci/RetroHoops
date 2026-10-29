using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// 28 Oct 2026, improvement I30 REGION STORES: a store in each region's grid district selling that region's own
    /// shoes and jersey colours, only there and only for street cash (play money won in games, never real money).
    /// Anything you own can be put on from the store too.
    /// </summary>
    public static class RegionStore
    {
        public const string Prefix = "store.";

        public static List<CosmeticDef> Items(ContentCatalog c, string region) =>
            c?.Cosmetics.FindAll(x => x.store == region) ?? new List<CosmeticDef>();

        public static string Name(string region) => (WorldRegions.Find(region)?.Short ?? "") + " STORE";

        public static List<CityActivity> Choices(CareerSaveData d, ContentCatalog c, string region)
        {
            var list = new List<CityActivity>();
            if (d == null || c == null) return list;
            long have = d.streetCash?.cash ?? 0;
            foreach (var item in Items(c, region))
            {
                string name = item.displayName.ToUpperInvariant();
                string slot = Loc.T(CityShop.SlotName(item.slot));
                if (d.ownedCosmetics.Contains(item.id))
                {
                    bool on = d.Equipped(item.slot) == item.id;
                    list.Add(new CityActivity { Label = (on ? Loc.T("WEARING") : Loc.T("PUT ON")) + ": " + name, ItemId = item.id,
                                                Action = on ? CityAction.Info : CityAction.Equip, Detail = slot + "  ·  " + Loc.T("OWNED") });
                    continue;
                }
                long price = StreetCash.Price(item);
                list.Add(new CityActivity
                {
                    Label = Loc.T("BUY") + ": " + name + "  " + StreetCash.Money(price), ItemId = item.id, Action = CityAction.RegionBuy,
                    Detail = slot + "  ·  " + Loc.T("Only sold here.") + "  " + Loc.T("street cash"),
                    Locked = have < price ? Loc.T("Needs") + " " + StreetCash.Money(price) + " (" + StreetCash.Money(have) + ")." : null,
                });
            }
            return list;
        }

        /// <summary>How many region store pieces there are (12: three in each of the four regions).</summary>
        public const int TotalItems = 12;

        public static int OwnedCount(CareerSaveData d)
        {
            if (d == null) return 0;
            int n = 0;
            foreach (var id in d.ownedCosmetics) if (id != null && id.StartsWith("cosmetic.region.", StringComparison.Ordinal)) n++;
            return n;
        }

        public static bool OwnedAll(CareerSaveData d) => OwnedCount(d) >= TotalItems;

        /// <summary>Buys (and puts on) a region store item with street cash.</summary>
        public static CosmeticCheck Buy(CareerSaveData d, ContentCatalog c, string itemId)
        {
            var item = c?.Cosmetics.Find(x => x.id == itemId);
            if (item == null || item.store == null || d == null) return CosmeticCheck.Unknown;
            if (d.ownedCosmetics.Contains(item.id)) return CosmeticCheck.AlreadyOwned;
            if (!StreetCash.Spend(d, StreetCash.Price(item))) return CosmeticCheck.NotEnoughPoints;
            d.ownedCosmetics.Add(item.id);
            d.SetEquipped(item.slot, item.id);
            if (d.city == null) d.city = new CitySaveData();
            d.city.purchases++;
            return CosmeticCheck.Ok;
        }
    }
}
