using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// 28 Oct 2026 (Omari, BALLER WORLD phases W7–W10): the regions' districts, two each, laid out by hand like the
    /// city's. Every region has its own buildings (CityBuilding.Style 6–13: brownstones and glass towers, mission stucco
    /// and bungalows, neon towers and motels, Victorians and warehouses), its own street furniture, three courts with
    /// their own looks, YOUR RIDE (THE DRIVE), and in its main district the BIG RUN poster.
    /// </summary>
    public static partial class CityDistricts
    {
        public const string NyUptown = "ny_uptown";
        public const string NyMidtown = "ny_midtown";
        public const string LaBeach = "la_beach";
        public const string LaEastside = "la_eastside";
        public const string LvStrip = "lv_strip";
        public const string LvOldTown = "lv_oldtown";
        public const string BayHills = "bay_hills";
        public const string BayTown = "bay_town";

        /// <summary>Building styles for the regions (CityBuilding.Style).</summary>
        public const int Brownstone = 6, GlassTower = 7, Mission = 8, Bungalow = 9, NeonTower = 10, Motel = 11, Victorian = 12, Warehouse = 13;

        private static CityDistrictDefinition[] WorldDistricts() => new[]
        {
            new CityDistrictDefinition { Id = NyUptown, Name = "UPTOWN", Blurb = "New York: brownstones, the Cage and the el.", Build = BuildNyUptown },
            new CityDistrictDefinition { Id = NyMidtown, Name = "MIDTOWN", Blurb = "New York: glass towers, a rooftop run and the river pier.", Build = BuildNyMidtown },
            new CityDistrictDefinition { Id = LaBeach, Name = "OCEAN WALK", Blurb = "Los Angeles: sand, palms and the beach courts.", Build = BuildLaBeach },
            new CityDistrictDefinition { Id = LaEastside, Name = "EASTSIDE", Blurb = "Los Angeles: murals, the overpass and the taco truck lot.", Build = BuildLaEastside },
            new CityDistrictDefinition { Id = LvStrip, Name = "THE STRIP", Blurb = "Las Vegas: neon towers, the fountains and the marquee roof.", Build = BuildLvStrip },
            new CityDistrictDefinition { Id = LvOldTown, Name = "OLD TOWN", Blurb = "Las Vegas: desert lots, motels and the neon yard.", Build = BuildLvOldTown },
            new CityDistrictDefinition { Id = BayHills, Name = "THE HILLS", Blurb = "The Bay: painted Victorians, cable cars and the bridge.", Build = BuildBayHills },
            new CityDistrictDefinition { Id = BayTown, Name = "THE TOWN", Blurb = "The Bay: warehouses, murals, the freeway and the port.", Build = BuildBayTown },
        };

        /// <summary>The outdoor district a building is in (the district whose door leads to it), or the district itself.</summary>
        public static string OutsideOf(string district)
        {
            var def = Find(district);
            if (def == null || !def.Interior) return district;
            foreach (var d in All)
            {
                if (d.Interior) continue;
                foreach (var p in Map(d.Id).Portals)
                    if (p.Kind == CityPortalKind.Building && p.Target == district) return d.Id;
            }
            return FirstCallers;
        }

        // ------------------------------------------------------------------ helpers for the regions

        /// <summary>YOUR RIDE: your car parked at (x, y) (2×1), the RIDE spot in front of it and the "ride" spawn.</summary>
        private static void RideSpot(CityMap m, int x, int y)
        {
            m.AddProp(CityPropKind.Ride, x, y, 2, 1, true);
            m.Portals.Add(new CityPortal { Id = "ride", Kind = CityPortalKind.Station, Label = "YOUR RIDE", Target = "ride", X = x, Y = y - 1.2f, W = 2, H = 1.2f });
        }

        /// <summary>The region's BIG RUN poster (3×1) and the spot in front of it.</summary>
        private static void BigRunPoster(CityMap m, int x, int y)
        {
            m.AddProp(CityPropKind.Poster, x, y, 3, 1, true);
            m.Portals.Add(new CityPortal { Id = "bigrun", Kind = CityPortalKind.Station, Label = "BIG RUN", Target = "bigrun", X = x, Y = y - 1.2f, W = 3, H = 1.2f });
        }

        private static void Row(CityMap m, int y, int h, int style, uint seed, params (int x, int w, string sign)[] buildings)
        {
            int i = 0;
            foreach (var (x, w, sign) in buildings)
            {
                i++;
                m.AddBuilding(new CityBuilding { Id = m.DistrictId + ".b" + y + "." + i, Name = sign ?? "", X = x, Y = y, W = w, H = h, Style = style,
                                                 DoorX = x + w / 2, Sign = sign, Seed = seed + (uint)i * 7 });
            }
        }

        private static void Props(CityMap m, CityPropKind kind, params int[] xy)
        {
            for (int i = 0; i + 1 < xy.Length; i += 2) m.AddProp(kind, xy[i], xy[i + 1], 1, 1, true, i / 2);
        }

        /// <summary>A fence round a court (x, y, w, h is the fenced area) with a gate of 3 in the bottom side.</summary>
        private static void Cage(CityMap m, int x, int y, int w, int h, int gateAt)
        {
            Fence(m, x, y, w, true, gateAt, 3);
            Fence(m, x, y + h - 1, w, true);
            Fence(m, x, y + 1, h - 2, false);
            Fence(m, x + w - 1, y + 1, h - 2, false);
        }

        // ------------------------------------------------------------------ NEW YORK: Uptown (48 × 66)

        private static CityMap BuildNyUptown()
        {
            var m = new CityMap(NyUptown, 48, 66, CityGround.Sidewalk);
            HRoad(m, 28);
            HRoad(m, 48);
            VRoad(m, 22);
            Crossings(m, 22, 28);
            Crossings(m, 22, 48);

            // SW: THE CAGE, fenced all round with a gate, bleachers on the side.
            m.Fill(1, 3, 20, 22, CityGround.Asphalt);
            Court(m, "court.ny_cage", "court.ny_cage", "THE CAGE", 4, 9, 13, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = -1, Logo = true });
            Cage(m, 3, 8, 15, 13, 6);
            m.AddProp(CityPropKind.Bleachers, 19, 10, 1, 6, true);
            m.AddProp(CityPropKind.Lamp, 1, 24, 1, 1, true);
            m.AddProp(CityPropKind.Lamp, 19, 22, 1, 1, true);
            m.Hangouts["court.ny_cage.side"] = new Vec2(20.3f, 18f);
            BigRunPoster(m, 6, 23);
            StreetsWall(m, 12, 23, 2);

            // SE: BROWNSTONE PARK, a red rubber court among the trees.
            m.Fill(27, 3, 20, 22, CityGround.Grass);
            Court(m, "court.ny_brownstone", "court.ny_brownstone", "BROWNSTONE PARK", 31, 9, 13, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = (int)FloorStyle.Rubber, Logo = true });
            Trees(m, 28, 5, 28, 12, 28, 19, 46, 6, 46, 14, 45, 22);
            m.AddProp(CityPropKind.Bench, 32, 22, 2, 1, true);
            m.AddProp(CityPropKind.FoodCart, 36, 24, 2, 1, true, 0);
            m.Hangouts["court.ny_brownstone.side"] = new Vec2(29.5f, 16f);
            RideSpot(m, 41, 25);

            // The subway stairs at the corner.
            m.AddProp(CityPropKind.SubwayStairs, 18, 25, 2, 1, true, 0);
            m.AddProp(CityPropKind.SubwayStairs, 28, 25, 2, 1, true, 1);

            // Brownstones along the avenue (stoops face the street).
            Row(m, 33, 6, Brownstone, 101, (1, 6, "BODEGA"), (7, 6, null), (13, 6, null));
            Row(m, 33, 6, Brownstone, 131, (27, 6, null), (33, 6, "PIZZA"), (39, 6, null));
            foreach (int x in new[] { 2, 10, 30, 40 }) m.AddProp(CityPropKind.Lamp, x, 27, 1, 1, true);
            Props(m, CityPropKind.Hydrant, 15, 32, 45, 32);

            // Behind them: a brick plaza and a community garden.
            m.Fill(1, 40, 20, 7, CityGround.Brick);
            m.AddProp(CityPropKind.FoodCart, 4, 43, 2, 1, true, 1);
            m.AddProp(CityPropKind.Bench, 12, 44, 2, 1, true);
            m.Fill(27, 40, 20, 7, CityGround.Grass);
            Trees(m, 29, 44, 35, 42, 41, 45, 45, 41);
            m.AddProp(CityPropKind.Planter, 32, 45, 1, 1, true);
            m.Hangouts["plaza.brick"] = new Vec2(9f, 43f);
            m.Hangouts["garden"] = new Vec2(38f, 43f);

            // Top: SUBWAY COURT under the elevated tracks, and apartment blocks.
            Court(m, "court.ny_subway", "court.ny_subway", "SUBWAY COURT", 4, 54, 13, 10, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = (int)FloorStyle.Tiles, Logo = true });
            Props(m, CityPropKind.Pillar, 2, 55, 2, 60, 18, 55, 18, 60);
            m.Hangouts["court.ny_subway.side"] = new Vec2(20f, 58f);
            Row(m, 54, 9, 1, 151, (27, 9, null), (37, 9, "LAUNDRY"));

            m.Portals.Add(new CityPortal { Id = "road.midtown", Kind = CityPortalKind.District, Label = "TO MIDTOWN", Target = NyMidtown,
                                           TargetSpawn = "from_uptown", X = 22, Y = 64.6f, W = 4, H = 1.4f });
            m.AddSpawn("ride", 42f, 23.2f, 0);
            m.AddSpawn("from_midtown", 23.5f, 62.6f, 0);
            m.AddSpawn("court.ny_cage", 10.5f, 6.6f, 1);
            m.AddSpawn("court.ny_brownstone", 37.5f, 7f, 1);
            m.AddSpawn("court.ny_subway", 10.5f, 52.6f, 1);
            return m;
        }

        // ------------------------------------------------------------------ NEW YORK: Midtown (48 × 60)

        private static CityMap BuildNyMidtown()
        {
            var m = new CityMap(NyMidtown, 48, 60, CityGround.Sidewalk);
            HRoad(m, 22);
            HRoad(m, 42);
            VRoad(m, 22, 0, 47);
            Crossings(m, 22, 22);
            Crossings(m, 22, 42);

            // SW: STEEL PLAZA, tiles on a brick plaza between the towers.
            m.Fill(1, 2, 20, 18, CityGround.Brick);
            Court(m, "court.ny_plaza", "court.ny_plaza", "STEEL PLAZA", 4, 5, 13, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = (int)FloorStyle.Tiles, Logo = true });
            m.AddProp(CityPropKind.Fountain, 2, 17, 2, 2, true);
            m.AddProp(CityPropKind.Bench, 9, 18, 2, 1, true);
            m.Hangouts["court.ny_plaza.side"] = new Vec2(19f, 10f);

            // SE and the middle: glass towers.
            Row(m, 8, 12, GlassTower, 201, (27, 9, null), (37, 9, null));
            Row(m, 27, 10, GlassTower, 221, (27, 8, null), (36, 10, null));

            // West middle: ROOFTOP 9, a fenced deck.
            m.Fill(2, 27, 17, 14, CityGround.Tile);
            Court(m, "court.ny_rooftop", "court.ny_rooftop", "ROOFTOP 9", 4, 29, 13, 10, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = -1, Logo = true, Neon = true });
            Cage(m, 2, 27, 17, 14, 7);
            m.Hangouts["court.ny_rooftop.side"] = new Vec2(20f, 33f);
            m.AddProp(CityPropKind.Billboard, 37, 38, 4, 1, true);
            StreetsWall(m, 27, 38, 0);

            // Top: the river, and RIVER PIER on the boards.
            m.Fill(0, 53, 48, 7, CityGround.Water);
            m.Fill(3, 47, 18, 12, CityGround.Pier);
            Court(m, "court.ny_pier", "court.ny_pier", "RIVER PIER", 6, 47, 13, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = -1, Logo = true });
            m.Hangouts["court.ny_pier.side"] = new Vec2(21.5f, 50f);
            Row(m, 47, 6, GlassTower, 241, (27, 9, null), (37, 9, null));
            foreach (int x in new[] { 3, 12, 30, 40 }) { m.AddProp(CityPropKind.Lamp, x, 21, 1, 1, true); m.AddProp(CityPropKind.Lamp, x, 41, 1, 1, true); }

            m.Portals.Add(new CityPortal { Id = "road.uptown", Kind = CityPortalKind.District, Label = "TO UPTOWN", Target = NyUptown,
                                           TargetSpawn = "from_midtown", X = 22, Y = 0, W = 4, H = 1.4f });
            m.AddSpawn("from_uptown", 23.5f, 2.8f, 1);
            m.AddSpawn("court.ny_plaza", 10.5f, 3.4f, 1);
            m.AddSpawn("court.ny_rooftop", 10.5f, 26.5f, 1);
            m.AddSpawn("court.ny_pier", 12.5f, 46.5f, 1);
            m.Hangouts["avenue"] = new Vec2(30f, 24.5f);
            return m;
        }

        // ------------------------------------------------------------------ LOS ANGELES: Ocean Walk (48 × 60)

        private static CityMap BuildLaBeach()
        {
            var m = new CityMap(LaBeach, 48, 60, CityGround.Sidewalk);
            m.Fill(0, 0, 48, 5, CityGround.Water);
            m.Fill(0, 5, 48, 13, CityGround.Sand);
            m.Fill(0, 18, 48, 2, CityGround.Pier); // the boardwalk
            HRoad(m, 24);
            VRoad(m, 22, 24, 60);
            Crossings(m, 22, 24);

            // The beach courts on their concrete pads.
            Court(m, "court.la_ocean", "court.la_ocean", "OCEAN WALK COURTS", 4, 6, 13, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = -1, Logo = true });
            Court(m, "court.la_lifeguard", "court.la_lifeguard", "LIFEGUARD COURT", 30, 6, 13, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = (int)FloorStyle.Rubber, Logo = true });
            m.AddProp(CityPropKind.Lifeguard, 22, 9, 3, 2, true);
            Props(m, CityPropKind.Palm, 2, 17, 18, 17, 28, 17, 46, 17);
            m.AddProp(CityPropKind.Umbrella, 19, 13, 1, 1, true, 0);
            m.AddProp(CityPropKind.Umbrella, 26, 13, 1, 1, true, 1);
            m.Hangouts["court.la_ocean.side"] = new Vec2(19.5f, 8f);
            m.Hangouts["court.la_lifeguard.side"] = new Vec2(45f, 11f);
            StreetsWall(m, 8, 21, 3);
            BigRunPoster(m, 34, 21);
            Props(m, CityPropKind.Palm, 1, 21, 17, 21, 30, 21, 45, 21);

            // Mission stucco and bungalows along the road.
            Row(m, 29, 6, Mission, 301, (1, 6, "SURF SHOP"), (8, 6, null), (14, 7, "JUICE"));
            Row(m, 29, 6, Bungalow, 331, (27, 6, null), (34, 6, "TACOS"), (40, 7, null));
            foreach (int x in new[] { 4, 14, 30, 42 }) m.AddProp(CityPropKind.Lamp, x, 23, 1, 1, true);

            // NW: SUNSET COURT in the palm park. NE: the lot where your ride's parked.
            m.Fill(1, 37, 20, 22, CityGround.Grass);
            Court(m, "court.la_sunset", "court.la_sunset", "SUNSET COURT", 4, 42, 13, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = (int)FloorStyle.Hardwood, Logo = true });
            Props(m, CityPropKind.Palm, 2, 39, 18, 39, 2, 55, 18, 56, 10, 56);
            m.Hangouts["court.la_sunset.side"] = new Vec2(19f, 47f);
            m.Fill(27, 37, 20, 9, CityGround.Asphalt);
            m.AddProp(CityPropKind.Car, 28, 43, 2, 1, true, 7);
            m.AddProp(CityPropKind.Car, 31, 43, 2, 1, true, 8);
            RideSpot(m, 40, 40);
            Row(m, 47, 6, Bungalow, 351, (27, 8, null), (37, 9, null));
            m.Hangouts["lot"] = new Vec2(34f, 39f);

            m.Portals.Add(new CityPortal { Id = "road.eastside", Kind = CityPortalKind.District, Label = "TO EASTSIDE", Target = LaEastside,
                                           TargetSpawn = "from_beach", X = 22, Y = 58.6f, W = 4, H = 1.4f });
            m.AddSpawn("ride", 41f, 38.2f, 0);
            m.AddSpawn("from_eastside", 23.5f, 56.6f, 0);
            m.AddSpawn("court.la_ocean", 10.5f, 5.5f, 1);
            m.AddSpawn("court.la_lifeguard", 36.5f, 5.5f, 1);
            m.AddSpawn("court.la_sunset", 10.5f, 40.3f, 1);
            return m;
        }

        // ------------------------------------------------------------------ LOS ANGELES: Eastside (48 × 60)

        private static CityMap BuildLaEastside()
        {
            var m = new CityMap(LaEastside, 48, 60, CityGround.Sidewalk);
            HRoad(m, 20);
            HRoad(m, 44);
            VRoad(m, 22);
            Crossings(m, 22, 20);
            Crossings(m, 22, 44);

            // SW: bungalows with yards and palms.
            m.Fill(1, 1, 20, 9, CityGround.Grass);
            Row(m, 11, 6, Bungalow, 401, (1, 6, null), (8, 6, null), (14, 6, null));
            Props(m, CityPropKind.Palm, 3, 3, 10, 6, 17, 3);

            // SE: the TACO TRUCK LOT.
            m.Fill(27, 1, 20, 17, CityGround.Asphalt);
            Court(m, "court.la_taco", "court.la_taco", "TACO TRUCK LOT", 30, 4, 13, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = -1, Logo = true });
            m.AddProp(CityPropKind.TacoTruck, 27, 15, 3, 2, true);
            m.AddProp(CityPropKind.Car, 43, 16, 2, 1, true, 7);
            m.Hangouts["court.la_taco.side"] = new Vec2(28.5f, 10f);

            // Middle west: THE OVERPASS, under the freeway's pillars.
            Court(m, "court.la_overpass", "court.la_overpass", "THE OVERPASS", 4, 27, 13, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = -1, Logo = true });
            Props(m, CityPropKind.Pillar, 2, 27, 2, 33, 18, 27, 18, 33, 1, 39, 5, 39, 9, 39, 13, 39, 17, 39);
            m.Hangouts["court.la_overpass.side"] = new Vec2(20f, 30f);
            StreetsWall(m, 6, 42, 1);

            // Middle east: MURAL COURT and its wall.
            Court(m, "court.la_mural", "court.la_mural", "MURAL COURT", 30, 26, 13, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = (int)FloorStyle.Tiles, Logo = true });
            m.AddProp(CityPropKind.Mural, 29, 39, 15, 1, true, 0);
            m.Hangouts["court.la_mural.side"] = new Vec2(45.5f, 32f);

            // Top: the shops, and lowriders by the curb.
            Row(m, 49, 6, Mission, 431, (1, 7, "PANADERIA"), (9, 6, null), (15, 6, "LAVANDERIA"));
            Row(m, 49, 6, Mission, 451, (27, 6, null), (34, 6, "MERCADO"), (40, 7, null));
            m.AddProp(CityPropKind.Car, 2, 56, 2, 1, true, 8);
            foreach (int x in new[] { 3, 12, 30, 40 }) m.AddProp(CityPropKind.Lamp, x, 19, 1, 1, true);
            Props(m, CityPropKind.Palm, 27, 41, 46, 41);

            m.Portals.Add(new CityPortal { Id = "road.beach", Kind = CityPortalKind.District, Label = "TO OCEAN WALK", Target = LaBeach,
                                           TargetSpawn = "from_eastside", X = 22, Y = 0, W = 4, H = 1.4f });
            m.AddSpawn("from_beach", 23.5f, 2.8f, 1);
            m.AddSpawn("court.la_taco", 36.5f, 2.5f, 1);
            m.AddSpawn("court.la_overpass", 10.5f, 25.6f, 1);
            m.AddSpawn("court.la_mural", 36.5f, 25f, 1);
            m.Hangouts["lot.curb"] = new Vec2(34f, 18.5f);
            return m;
        }

        // ------------------------------------------------------------------ LAS VEGAS: the Strip (48 × 64)

        private static CityMap BuildLvStrip()
        {
            var m = new CityMap(LvStrip, 48, 64, CityGround.Sidewalk);
            HRoad(m, 26);
            VRoad(m, 22);
            Crossings(m, 22, 26);

            // SW: FOUNTAIN PLAZA with its pools.
            m.Fill(1, 2, 20, 22, CityGround.Tile);
            m.Fill(2, 19, 4, 4, CityGround.Water);
            m.Fill(16, 19, 4, 4, CityGround.Water);
            m.AddProp(CityPropKind.Fountain, 3, 20, 2, 2, true);
            m.AddProp(CityPropKind.Fountain, 17, 20, 2, 2, true);
            Court(m, "court.lv_fountain", "court.lv_fountain", "FOUNTAIN PLAZA", 4, 4, 13, 11, CityCourtUse.Streets, CityGround.Tile,
                  new CityCourtLook { Floor = (int)FloorStyle.Tiles, Logo = true, Neon = true });
            m.Hangouts["court.lv_fountain.side"] = new Vec2(19f, 10f);
            BigRunPoster(m, 9, 24);

            // SE: THE STAGE, with a crowd and the neon signs.
            Court(m, "court.lv_stage", "court.lv_stage", "THE STAGE", 30, 6, 13, 11, CityCourtUse.Streets, CityGround.Hardwood,
                  new CityCourtLook { Floor = (int)FloorStyle.Hardwood, Logo = true, Neon = true });
            m.AddProp(CityPropKind.Crowd, 28, 7, 1, 8, true);
            m.AddProp(CityPropKind.Bleachers, 44, 7, 1, 8, true);
            Props(m, CityPropKind.NeonSign, 27, 20, 46, 20);
            m.Hangouts["court.lv_stage.side"] = new Vec2(26.6f, 10f);
            RideSpot(m, 36, 22);
            StreetsWall(m, 40, 23, 4);

            // The towers along the boulevard.
            Row(m, 31, 10, NeonTower, 501, (1, 10, "SHOWS"), (12, 9, null));
            Row(m, 31, 10, NeonTower, 521, (27, 9, "LIVE"), (37, 10, null));
            Props(m, CityPropKind.NeonSign, 27, 42, 37, 42);

            // Top west: the MARQUEE ROOF, neon on a fenced deck.
            m.Fill(2, 45, 17, 17, CityGround.Tile);
            Court(m, "court.lv_marquee", "court.lv_marquee", "MARQUEE ROOF", 4, 47, 13, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = (int)FloorStyle.NeonGrid, Logo = true, Neon = true });
            Cage(m, 2, 45, 17, 17, 7);
            m.Hangouts["court.lv_marquee.side"] = new Vec2(20.5f, 52f);
            Row(m, 45, 9, NeonTower, 541, (27, 9, null), (37, 10, "STAGE"));
            foreach (int x in new[] { 3, 12, 30, 40 }) m.AddProp(CityPropKind.Lamp, x, 25, 1, 1, true);

            m.Portals.Add(new CityPortal { Id = "road.oldtown", Kind = CityPortalKind.District, Label = "TO OLD TOWN", Target = LvOldTown,
                                           TargetSpawn = "from_strip", X = 22, Y = 62.6f, W = 4, H = 1.4f });
            m.AddSpawn("ride", 37f, 20.3f, 0);
            m.AddSpawn("from_oldtown", 23.5f, 60.6f, 0);
            m.AddSpawn("court.lv_fountain", 10.5f, 2.8f, 1);
            m.AddSpawn("court.lv_stage", 36.5f, 4.6f, 1);
            m.AddSpawn("court.lv_marquee", 10.5f, 43.6f, 1);
            return m;
        }

        // ------------------------------------------------------------------ LAS VEGAS: Old Town (48 × 60)

        private static CityMap BuildLvOldTown()
        {
            var m = new CityMap(LvOldTown, 48, 60, CityGround.Desert);
            HRoad(m, 28);
            VRoad(m, 22);
            Crossings(m, 22, 28);

            // SW: the DESERT LOT among the cactus.
            Court(m, "court.lv_desert", "court.lv_desert", "DESERT LOT", 4, 8, 13, 11, CityCourtUse.Streets, CityGround.Dirt,
                  new CityCourtLook { Floor = -1, Logo = true });
            Props(m, CityPropKind.Cactus, 2, 5, 18, 6, 2, 20, 19, 21);
            m.AddProp(CityPropKind.Rock, 10, 4, 2, 1, true);
            m.Hangouts["court.lv_desert.side"] = new Vec2(20.5f, 13f);
            StreetsWall(m, 2, 25, 5);

            // SE: the motel, its lot and the MOTEL POOL COURT.
            m.Fill(26, 1, 21, 17, CityGround.Asphalt);
            Court(m, "court.lv_motel", "court.lv_motel", "MOTEL POOL COURT", 30, 4, 13, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = (int)FloorStyle.Tiles, Logo = true });
            m.Fill(44, 5, 3, 6, CityGround.Water);
            m.AddBuilding(new CityBuilding { Id = "lv.motel", Name = "MOTEL", X = 27, Y = 19, W = 19, H = 5, Style = Motel, DoorX = 36, Sign = "MOTEL", Seed = 601 });
            m.Hangouts["court.lv_motel.side"] = new Vec2(28f, 10f);
            m.AddProp(CityPropKind.NeonSign, 46, 18, 1, 1, true, 3);

            // NW: the NEON YARD, old signs standing in the dirt.
            m.Fill(1, 34, 20, 25, CityGround.Dirt);
            Court(m, "court.lv_neonyard", "court.lv_neonyard", "NEON YARD", 5, 40, 13, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = -1, Logo = true, Neon = true });
            Props(m, CityPropKind.NeonSign, 2, 36, 2, 46, 19, 38, 19, 48, 8, 55, 14, 55);
            m.Hangouts["court.lv_neonyard.side"] = new Vec2(20f, 44f);

            // NE: adobe houses, cactus and rocks.
            Row(m, 35, 6, Mission, 621, (27, 8, null), (37, 9, null));
            Props(m, CityPropKind.Cactus, 28, 44, 34, 48, 44, 45, 40, 54);
            m.AddProp(CityPropKind.Rock, 30, 52, 2, 1, true);

            m.Portals.Add(new CityPortal { Id = "road.strip", Kind = CityPortalKind.District, Label = "TO THE STRIP", Target = LvStrip,
                                           TargetSpawn = "from_oldtown", X = 22, Y = 0, W = 4, H = 1.4f });
            m.AddSpawn("from_strip", 23.5f, 2.8f, 1);
            m.AddSpawn("court.lv_desert", 10.5f, 6.5f, 1);
            m.AddSpawn("court.lv_motel", 36.5f, 2.6f, 1);
            m.AddSpawn("court.lv_neonyard", 11.5f, 38.5f, 1);
            m.Hangouts["roadside"] = new Vec2(30f, 33f);
            return m;
        }

        // ------------------------------------------------------------------ THE BAY: the Hills (48 × 64)

        private static CityMap BuildBayHills()
        {
            var m = new CityMap(BayHills, 48, 64, CityGround.Sidewalk);
            HRoad(m, 24);
            VRoad(m, 22, 0, 51);
            Crossings(m, 22, 24);
            // Cable-car rails up the middle of the hill street.
            for (int y = 0; y < 51; y++)
            {
                if (y >= 23 && y <= 28) continue;
                m.Set(23, y, CityGround.Rail);
                m.Set(24, y, CityGround.Rail);
            }
            m.AddProp(CityPropKind.CableCar, 23, 40, 2, 3, true);

            // SW: PAINTED LADIES PARK. SE: CABLE CAR COURT on the brick plaza, and your ride.
            m.Fill(1, 1, 20, 21, CityGround.Grass);
            Court(m, "court.bay_painted", "court.bay_painted", "PAINTED LADIES PARK", 4, 6, 13, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = (int)FloorStyle.Rubber, Logo = true });
            Trees(m, 2, 3, 2, 18, 18, 3, 19, 18);
            m.Hangouts["court.bay_painted.side"] = new Vec2(19.5f, 12f);
            StreetsWall(m, 2, 21, 6);
            m.Fill(27, 1, 20, 21, CityGround.Brick);
            Court(m, "court.bay_cable", "court.bay_cable", "CABLE CAR COURT", 30, 6, 13, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = -1, Logo = true });
            m.AddProp(CityPropKind.FoodCart, 44, 3, 2, 1, true, 2);
            m.Hangouts["court.bay_cable.side"] = new Vec2(28f, 12f);
            RideSpot(m, 40, 19);

            // Painted Victorians up the hill.
            Row(m, 29, 7, Victorian, 701, (1, 5, null), (6, 5, null), (11, 5, null), (16, 5, null));
            Row(m, 29, 7, Victorian, 741, (27, 5, null), (32, 5, null), (37, 5, null), (42, 5, null));
            Row(m, 37, 7, Victorian, 781, (27, 5, null), (32, 5, null), (37, 5, null), (42, 5, null));
            foreach (int x in new[] { 3, 12, 30, 40 }) m.AddProp(CityPropKind.Lamp, x, 23, 1, 1, true);

            // NW: BRIDGE VIEW on the waterfront; the bridge's towers stand in the bay.
            m.Fill(1, 37, 20, 21, CityGround.Brick);
            m.Fill(27, 45, 20, 13, CityGround.Brick);
            m.Fill(0, 58, 48, 6, CityGround.Water);
            Court(m, "court.bay_bridge", "court.bay_bridge", "BRIDGE VIEW", 4, 42, 13, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = (int)FloorStyle.Hardwood, Logo = true });
            m.AddProp(CityPropKind.BridgeTower, 12, 59, 2, 2, true);
            m.AddProp(CityPropKind.BridgeTower, 36, 59, 2, 2, true);
            m.AddProp(CityPropKind.Bench, 6, 56, 2, 1, true);
            m.AddProp(CityPropKind.Bench, 30, 56, 2, 1, true);
            BigRunPoster(m, 14, 55);
            m.Hangouts["court.bay_bridge.side"] = new Vec2(19.5f, 48f);
            m.Hangouts["waterfront"] = new Vec2(38f, 52f);

            m.Portals.Add(new CityPortal { Id = "road.town", Kind = CityPortalKind.District, Label = "TO THE TOWN", Target = BayTown,
                                           TargetSpawn = "from_hills", X = 46.6f, Y = 23, W = 1.4f, H = 6 });
            m.AddSpawn("ride", 41f, 17.2f, 0);
            m.AddSpawn("from_town", 45.4f, 26.5f, 2);
            m.AddSpawn("court.bay_painted", 10.5f, 4.5f, 1);
            m.AddSpawn("court.bay_cable", 36.5f, 4.5f, 1);
            m.AddSpawn("court.bay_bridge", 10.5f, 40.5f, 1);
            return m;
        }

        // ------------------------------------------------------------------ THE BAY: the Town (48 × 60)

        private static CityMap BuildBayTown()
        {
            var m = new CityMap(BayTown, 48, 60, CityGround.Sidewalk);
            HRoad(m, 24);
            VRoad(m, 22);
            Crossings(m, 22, 24);
            // The estuary on the east, with the port's pier and cranes.
            m.Fill(44, 0, 4, 60, CityGround.Water);
            m.Fill(38, 30, 6, 29, CityGround.Pier);
            m.AddProp(CityPropKind.Crane, 39, 50, 3, 2, true);
            m.AddProp(CityPropKind.Crane, 39, 38, 3, 2, true, 1);

            // SW: UNDER THE FREEWAY, between the pillars.
            Court(m, "court.bay_freeway", "court.bay_freeway", "UNDER THE FREEWAY", 4, 6, 13, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = -1, Logo = true });
            Props(m, CityPropKind.Pillar, 2, 6, 2, 12, 18, 6, 18, 12, 1, 20, 5, 20, 9, 20, 13, 20, 17, 20);
            m.Hangouts["court.bay_freeway.side"] = new Vec2(20f, 10f);

            // SE: LAKESIDE, on the grass by the lake.
            m.Fill(27, 1, 17, 21, CityGround.Grass);
            m.Fill(36, 1, 7, 5, CityGround.Water);
            Court(m, "court.bay_lake", "court.bay_lake", "LAKESIDE", 28, 9, 13, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = (int)FloorStyle.Rubber, Logo = true });
            Trees(m, 28, 3, 42, 8, 42, 16, 28, 21);
            m.Hangouts["court.bay_lake.side"] = new Vec2(42.5f, 12f);

            // NW: warehouses with murals.
            Row(m, 29, 7, Warehouse, 801, (1, 9, null), (11, 9, null));
            Row(m, 44, 7, Warehouse, 821, (1, 9, null), (11, 9, null));
            StreetsWall(m, 2, 40, 7);

            // NE: PORT CRANES, on the asphalt by the water.
            m.Fill(27, 30, 11, 28, CityGround.Asphalt);
            Court(m, "court.bay_port", "court.bay_port", "PORT CRANES", 27, 40, 11, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = -1, Logo = true });
            m.AddProp(CityPropKind.Poster, 28, 33, 3, 1, true, 1); // a Big Run flyer (the poster you use is in the Hills)
            m.Hangouts["court.bay_port.side"] = new Vec2(26.5f, 45f);
            foreach (int x in new[] { 3, 12, 30 }) m.AddProp(CityPropKind.Lamp, x, 23, 1, 1, true);

            m.Portals.Add(new CityPortal { Id = "road.hills", Kind = CityPortalKind.District, Label = "TO THE HILLS", Target = BayHills,
                                           TargetSpawn = "from_town", X = 0, Y = 23, W = 1.4f, H = 6 });
            m.AddSpawn("from_hills", 2.8f, 25.5f, 3);
            m.AddSpawn("court.bay_freeway", 10.5f, 4.5f, 1);
            m.AddSpawn("court.bay_lake", 31.5f, 7.5f, 1);
            m.AddSpawn("court.bay_port", 32.5f, 38.5f, 1);
            return m;
        }
    }
}
