using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// 28 Oct 2026 (Omari): "add more buildings and streets to the other states, make them bigger and complete looking".
    /// Each region gets a third, bigger district: a full street grid (two avenues by two cross streets, nine blocks) lined
    /// with that region's own buildings, shops, lots, parks and street furniture, and a court in the park in the middle.
    /// </summary>
    public static partial class CityDistricts
    {
        public const string NyAvenues = "ny_avenues";
        public const string LaBoulevard = "la_boulevard";
        public const string LvNeonMile = "lv_neonmile";
        public const string BayCrosstown = "bay_crosstown";

        /// <summary>How a region's grid district looks.</summary>
        private sealed class GridLook
        {
            public int[] Styles;            // building styles, used in turn
            public int[] Widths;            // footprint width per style (same order)
            public int[] Heights;           // footprint height per style
            public CityGround Block = CityGround.Sidewalk, Park = CityGround.Grass, Lot = CityGround.Asphalt, Plaza = CityGround.Brick;
            public CityPropKind Green = CityPropKind.Tree, Curb = CityPropKind.Hydrant, Extra = CityPropKind.Bench, Plaza1 = CityPropKind.FoodCart;
            public int[] Cars = { 0, 1, 2 };
            public string[] Signs = new string[0];
            public CityGround ParkFloor = CityGround.Asphalt;
            public CityCourtLook Look = new CityCourtLook { Floor = -1, Logo = true };
        }

        private static CityDistrictDefinition[] GridDistricts() => new[]
        {
            new CityDistrictDefinition { Id = NyAvenues, Name = "THE AVENUES", Blurb = "New York: block after block of brownstones and towers, cabs at the curb.", Build = BuildNyAvenues },
            new CityDistrictDefinition { Id = LaBoulevard, Name = "THE BOULEVARD", Blurb = "Los Angeles: palms down the boulevard, stucco shops and lowriders.", Build = BuildLaBoulevard },
            new CityDistrictDefinition { Id = LvNeonMile, Name = "NEON MILE", Blurb = "Las Vegas: a mile of neon towers and motels on the desert grid.", Build = BuildLvNeonMile },
            new CityDistrictDefinition { Id = BayCrosstown, Name = "CROSSTOWN", Blurb = "The Bay: Victorians, lofts and warehouses on the cable-car line.", Build = BuildBayCrosstown },
        };

        // ------------------------------------------------------------------ the grid (72 × 64)

        private const int GridW = 72, GridH = 64;
        private static readonly int[] GridCols = { 1, 27, 53 };   // block left edges
        private static readonly int[] GridColW = { 20, 20, 18 };  // block widths
        private static readonly int[] GridRows = { 1, 25, 47 };   // block bottoms
        private static readonly int[] GridRowH = { 18, 16, 16 };  // block heights

        /// <summary>
        /// The grid district: cross streets at y 20 and 42, avenues at x 22 and 48, nine blocks. The middle block is the
        /// park with the court; the bottom row is shops behind a plaza; the rest are buildings on the street with lots
        /// and gardens behind. The road out west (to <paramref name="from"/>) is on the lower cross street.
        /// </summary>
        private static CityMap BuildGrid(string id, string from, string fromLabel, string courtId, string courtName, GridLook look, uint seed)
        {
            var m = new CityMap(id, GridW, GridH, look.Block);
            HRoad(m, 20);
            HRoad(m, 42);
            VRoad(m, 22);
            VRoad(m, 48);
            foreach (int x in new[] { 22, 48 })
                foreach (int y in new[] { 20, 42 })
                    Crossings(m, x, y);

            int styleAt = 0, signAt = 0, carAt = 0;
            uint s = seed;
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                {
                    int x0 = GridCols[c], bw = GridColW[c], y0 = GridRows[r], bh = GridRowH[r];
                    if (r == 1 && c == 1) { GridPark(m, look, courtId, courtName, x0, y0, bw, bh); continue; }

                    // Buildings across the block (front on the street; on the bottom row, on the plaza).
                    int by = r == 0 ? y0 + 8 : y0;
                    int maxH = r == 0 ? bh - 8 : bh - 5;
                    int x = x0;
                    int n = 0, tallest = 0;
                    while (true)
                    {
                        int k = styleAt % look.Styles.Length;
                        int w = look.Widths[k], h = Math.Min(look.Heights[k], maxH);
                        int left = x0 + bw - x;
                        if (left < 4) break;
                        if (w > left) w = left; // the last one on the block fills the corner
                        string sign = null;
                        if (look.Signs.Length > 0 && (styleAt + r) % 2 == 0) sign = look.Signs[signAt++ % look.Signs.Length];
                        n++;
                        m.AddBuilding(new CityBuilding { Id = id + ".b" + r + c + "." + n, Name = sign ?? "", X = x, Y = by, W = w, H = h, Style = look.Styles[k],
                                                         DoorX = x + w / 2, Sign = sign, Seed = s += 17 });
                        styleAt++;
                        tallest = Math.Max(tallest, h);
                        x += w;
                        if (x0 + bw - x >= 2 && n % 2 == 0) x++; // an alley now and then
                    }

                    if (r == 0)
                    {
                        // The plaza in front of the shops.
                        m.Fill(x0, y0, bw, 7, look.Plaza);
                        m.AddProp(look.Plaza1, x0 + 2, y0 + 3, 2, 1, true, c);
                        m.AddProp(CityPropKind.Bench, x0 + bw - 6, y0 + 2, 2, 1, true);
                        m.AddProp(look.Green, x0 + 1, y0 + 1, 1, 1, true, c);
                        m.AddProp(look.Green, x0 + bw - 2, y0 + 5, 1, 1, true, c + 1);
                        m.AddProp(CityPropKind.Planter, x0 + bw / 2, y0 + 1, 1, 1, true);
                        m.Hangouts["plaza." + c] = new Vec2(x0 + bw * 0.5f, y0 + 4.5f);
                    }
                    else
                    {
                        // Behind the buildings: a lot with parked cars, or a garden.
                        int ly = by + tallest + 1;
                        int lh = y0 + bh - ly;
                        if (lh >= 3)
                        {
                            if ((r + c) % 2 == 0)
                            {
                                m.Fill(x0, ly, bw, lh, look.Lot);
                                for (int cx = x0 + 1; cx + 2 <= x0 + bw - 1; cx += 4)
                                    m.AddProp(CityPropKind.Car, cx, ly + 1, 2, 1, true, look.Cars[carAt++ % look.Cars.Length]);
                                m.Hangouts["lot." + r + c] = new Vec2(x0 + bw * 0.5f, ly + lh - 0.8f);
                            }
                            else
                            {
                                m.Fill(x0, ly, bw, lh, look.Park);
                                for (int gx = x0 + 1; gx < x0 + bw - 1; gx += 5) m.AddProp(look.Green, gx, ly + lh / 2, 1, 1, true, gx);
                                m.AddProp(CityPropKind.Bench, x0 + 3, ly + lh - 1, 2, 1, true);
                                m.Hangouts["garden." + r + c] = new Vec2(x0 + bw * 0.5f, ly + 0.6f);
                            }
                        }
                    }
                }

            // Street furniture on the sidewalks: lamps at the block corners, the region's curb props in between.
            foreach (int y in new[] { 19, 41 })
                for (int c = 0; c < 3; c++)
                {
                    int x0 = GridCols[c], bw = GridColW[c];
                    m.AddProp(CityPropKind.Lamp, x0 + 1, y, 1, 1, true);
                    m.AddProp(CityPropKind.Lamp, x0 + bw - 2, y, 1, 1, true);
                    m.AddProp(look.Curb, x0 + bw / 2, y, 1, 1, true, c);
                }

            // I30: the region's store, a stall on the middle plaza.
            string region = WorldRegions.RegionOf(id).Id;
            m.AddProp(CityPropKind.Stall, 33, 5, 2, 1, true, 4);
            m.Portals.Add(new CityPortal { Id = "store", Kind = CityPortalKind.Station, Label = RegionStore.Name(region), Target = RegionStore.Prefix + region,
                                           X = 32, Y = 3.8f, W = 4, H = 1.2f });

            // The road west and the spot you arrive at.
            m.Portals.Add(new CityPortal { Id = "road." + from, Kind = CityPortalKind.District, Label = fromLabel, Target = from,
                                           TargetSpawn = "from_" + GridShort(id), X = 0, Y = 19, W = 1.4f, H = 6 });
            m.AddSpawn("from_" + GridShort(from), 2.8f, 21.5f, 3);
            m.Hangouts["avenue"] = new Vec2(50f, 30f);
            m.Hangouts["crossing"] = new Vec2(30f, 23.5f);
            return m;
        }

        /// <summary>The park in the middle block: the court, trees round it and a bench or two.</summary>
        private static void GridPark(CityMap m, GridLook look, string courtId, string courtName, int x0, int y0, int bw, int bh)
        {
            m.Fill(x0, y0, bw, bh, look.Park);
            Court(m, courtId, courtId, courtName, x0 + 3, y0 + 3, 13, 11, CityCourtUse.Streets, look.ParkFloor, look.Look);
            m.AddProp(look.Green, x0, y0 + 1, 1, 1, true, 1);
            m.AddProp(look.Green, x0 + bw - 1, y0 + 1, 1, 1, true, 2);
            m.AddProp(look.Green, x0, y0 + bh - 1, 1, 1, true, 3);
            m.AddProp(look.Green, x0 + bw - 1, y0 + bh - 1, 1, 1, true, 4);
            m.AddProp(CityPropKind.Bench, x0 + 17, y0 + 6, 1, 1, true);
            m.Hangouts[courtId + ".side"] = new Vec2(x0 + 17.5f, y0 + 10f);
            m.AddSpawn(courtId, x0 + 9.5f, y0 + 1.6f, 1);
        }

        /// <summary>The spawn suffix for a district ("ny_midtown" → "midtown").</summary>
        private static string GridShort(string district)
        {
            int i = district.IndexOf('_');
            return i < 0 ? district : district.Substring(i + 1);
        }

        // ------------------------------------------------------------------ the four

        private static CityMap BuildNyAvenues() => BuildGrid(NyAvenues, NyMidtown, "TO MIDTOWN", "court.ny_blockparty", "BLOCK PARTY COURT", new GridLook
        {
            Styles = new[] { Brownstone, Brownstone, GlassTower, 1, Brownstone, 0 },
            Widths = new[] { 6, 6, 9, 7, 6, 6 },
            Heights = new[] { 7, 7, 11, 8, 7, 8 },
            Plaza = CityGround.Brick, Lot = CityGround.Asphalt,
            Curb = CityPropKind.Hydrant, Plaza1 = CityPropKind.FoodCart,
            Cars = new[] { 6, 6, 0, 6, 2 },
            Signs = new[] { "DELI", "PIZZA", "BODEGA", "KICKS", "DINER", "BARBER", "BAGELS", "RECORDS" },
            ParkFloor = CityGround.Asphalt,
            Look = new CityCourtLook { Floor = -1, Logo = true },
        }, 9101);

        private static CityMap BuildLaBoulevard() => BuildGrid(LaBoulevard, LaEastside, "TO EASTSIDE", "court.la_boulevard", "BOULEVARD PARK", new GridLook
        {
            Styles = new[] { Mission, Bungalow, Mission, 5, Bungalow },
            Widths = new[] { 7, 6, 6, 7, 6 },
            Heights = new[] { 7, 6, 7, 7, 6 },
            Plaza = CityGround.Tile, Lot = CityGround.Asphalt, Park = CityGround.Grass,
            Green = CityPropKind.Palm, Curb = CityPropKind.Palm, Plaza1 = CityPropKind.TacoTruck,
            Cars = new[] { 7, 8, 3, 7 },
            Signs = new[] { "TACOS", "SURF", "JUICE", "LAUNDRY", "BURGERS", "KICKS", "MERCADO", "DONUTS" },
            ParkFloor = CityGround.Asphalt,
            Look = new CityCourtLook { Floor = (int)FloorStyle.Hardwood, Logo = true },
        }, 9201);

        private static CityMap BuildLvNeonMile() => BuildGrid(LvNeonMile, LvStrip, "TO THE STRIP", "court.lv_neonmile", "NEON MILE COURT", new GridLook
        {
            Styles = new[] { NeonTower, Motel, NeonTower, Mission },
            Widths = new[] { 9, 10, 9, 7 },
            Heights = new[] { 11, 5, 10, 6 },
            Block = CityGround.Sidewalk, Plaza = CityGround.Tile, Lot = CityGround.Asphalt, Park = CityGround.Desert,
            Green = CityPropKind.Cactus, Curb = CityPropKind.NeonSign, Plaza1 = CityPropKind.Fountain,
            Cars = new[] { 5, 4, 1, 3 },
            Signs = new[] { "LIVE", "SHOWS", "DINER", "ARCADE", "BUFFET", "MOTEL", "WEDDINGS", "ICE CREAM" },
            ParkFloor = CityGround.Tile,
            Look = new CityCourtLook { Floor = (int)FloorStyle.NeonGrid, Logo = true, Neon = true },
        }, 9301);

        private static CityMap BuildBayCrosstown()
        {
            var m = BuildGrid(BayCrosstown, BayTown, "TO THE TOWN", "court.bay_crosstown", "CROSSTOWN COURT", new GridLook
            {
                Styles = new[] { Victorian, Victorian, Warehouse, Victorian, GlassTower },
                Widths = new[] { 5, 5, 9, 5, 9 },
                Heights = new[] { 8, 8, 7, 8, 11 },
                Plaza = CityGround.Brick, Lot = CityGround.Asphalt, Park = CityGround.Grass,
                Green = CityPropKind.Tree, Curb = CityPropKind.Hydrant, Plaza1 = CityPropKind.FoodCart,
                Cars = new[] { 1, 2, 0, 5 },
                Signs = new[] { "COFFEE", "BAKERY", "BOOKS", "TECH", "RECORDS", "NOODLES", "BIKES" },
                ParkFloor = CityGround.Rubber,
                Look = new CityCourtLook { Floor = (int)FloorStyle.Rubber, Logo = true },
            }, 9401);
            // The cable-car line up the east avenue.
            for (int y = 0; y < GridH; y++)
            {
                var g = m.Get(49, y);
                if (g != CityGround.Road && g != CityGround.RoadLine) continue;
                m.Set(49, y, CityGround.Rail);
                m.Set(50, y, CityGround.Rail);
            }
            m.AddProp(CityPropKind.CableCar, 49, 52, 2, 3, true);
            return m;
        }
    }
}
