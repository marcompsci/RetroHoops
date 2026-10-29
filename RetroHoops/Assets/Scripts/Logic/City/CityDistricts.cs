using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>A Baller City district: its name, whether it's indoors, and how to build its map.</summary>
    public sealed class CityDistrictDefinition
    {
        public string Id, Name, Blurb;
        public bool Interior;
        public Func<CityMap> Build;
    }

    /// <summary>
    /// 16 Oct 2026 (Omari): BALLER CITY's districts, laid out by hand in code (so they're the same on every phone and
    /// cost no art downloads):
    /// - FIRST CALLERS NEIGHBORHOOD: home, the First Callers HQ (the Rise hub), the crew's home court, the Baller Gym.
    /// - BLACKTOP DISTRICT: the street courts the game already has (Sunset Cage, Overpass Park, Pier Nine, Static Lot).
    /// - BALLER GYM: the gym inside (Practice Lab drills, the Halloween court, the locker room).
    /// Districts load one at a time (each is its own small map), joined by the roads at their edges.
    /// </summary>
    public static partial class CityDistricts
    {
        public const string FirstCallers = "first_callers";
        public const string Blacktop = "blacktop";
        public const string Gym = "baller_gym";
        public const string HoopHeights = "hoop_heights";
        public const string HolidayPark = "holiday_park";
        public const string Boardwalk = "boardwalk";
        public const string Downtown = "downtown";
        public const string SchoolYard = "school_yard";
        public const string CornerStore = "corner_store";
        public const string FreshCuts = "fresh_cuts";
        public const string Arena = "baller_arena";
        public const string Crib = "crib";

        public static readonly CityDistrictDefinition[] All = Join(new[]
        {
            new CityDistrictDefinition { Id = FirstCallers, Name = "FIRST CALLERS NEIGHBORHOOD", Blurb = "Home, the crew's HQ and the Baller Gym.", Build = BuildFirstCallers },
            new CityDistrictDefinition { Id = Blacktop, Name = "BLACKTOP DISTRICT", Blurb = "Street courts, crews and spectators.", Build = BuildBlacktop },
            new CityDistrictDefinition { Id = Gym, Name = "BALLER GYM", Blurb = "Practice Lab, the Halloween court and the locker room.", Interior = true, Build = BuildGym },
            new CityDistrictDefinition { Id = HoopHeights, Name = "HOOP HEIGHTS", Blurb = "Six courts, six looks, and the best crews in the city.", Build = BuildHoopHeights },
            new CityDistrictDefinition { Id = HolidayPark, Name = "HOLIDAY PARK", Blurb = "A court for every holiday, open all year.", Build = BuildHolidayPark },
            new CityDistrictDefinition { Id = Boardwalk, Name = "THE BOARDWALK", Blurb = "Sand, the pier and the ferry deck.", Build = BuildBoardwalk },
            new CityDistrictDefinition { Id = Downtown, Name = "DOWNTOWN", Blurb = "Towers, the arena and the night buses.", Build = BuildDowntown },
            new CityDistrictDefinition { Id = SchoolYard, Name = "SCHOOL YARD", Blurb = "Baller City High and its courts.", Build = BuildSchoolYard },
            new CityDistrictDefinition { Id = CornerStore, Name = "CORNER STORE", Blurb = "Gear for your game.", Interior = true, Build = BuildCornerStore },
            new CityDistrictDefinition { Id = FreshCuts, Name = "FRESH CUTS", Blurb = "The barbershop.", Interior = true, Build = BuildFreshCuts },
            new CityDistrictDefinition { Id = Arena, Name = "BALLER ARENA", Blurb = "The City Championship.", Interior = true, Build = BuildArena },
            new CityDistrictDefinition { Id = Crib, Name = "YOUR CRIB", Blurb = "Trophies, banners and the safe.", Interior = true, Build = BuildCrib },
        }, Join(WorldDistricts(), GridDistricts()));

        private static CityDistrictDefinition[] Join(CityDistrictDefinition[] a, CityDistrictDefinition[] b)
        {
            var all = new CityDistrictDefinition[a.Length + b.Length];
            a.CopyTo(all, 0);
            b.CopyTo(all, a.Length);
            return all;
        }

        public static CityDistrictDefinition Find(string id) => Array.Find(All, d => d.Id == id);

        private static readonly Dictionary<string, CityMap> Cache = new Dictionary<string, CityMap>();

        /// <summary>The district's map (built once and kept: maps are small and never change while playing).</summary>
        public static CityMap Map(string id)
        {
            var def = Find(id) ?? All[0];
            lock (Cache)
            {
                if (!Cache.TryGetValue(def.Id, out var map))
                {
                    map = def.Build();
                    map.Interior = def.Interior;
                    Cache[def.Id] = map;
                }
                return map;
            }
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>A road across the map: four lanes of asphalt with a dashed centre line, and a sidewalk each side.</summary>
        private static void HRoad(CityMap m, int y, int x0 = 0, int x1 = -1)
        {
            if (x1 < 0) x1 = m.Width;
            m.Fill(x0, y - 1, x1 - x0, 1, CityGround.Sidewalk);
            m.Fill(x0, y, x1 - x0, 4, CityGround.Road);
            m.Fill(x0, y + 2, x1 - x0, 1, CityGround.RoadLine);
            m.Fill(x0, y + 4, x1 - x0, 1, CityGround.Sidewalk);
        }

        private static void VRoad(CityMap m, int x, int y0 = 0, int y1 = -1)
        {
            if (y1 < 0) y1 = m.Height;
            for (int y = y0; y < y1; y++)
            {
                var below = m.Get(x, y);
                bool crossing = below == CityGround.Road || below == CityGround.RoadLine;
                if (!crossing)
                {
                    m.Set(x - 1, y, CityGround.Sidewalk);
                    m.Set(x + 4, y, CityGround.Sidewalk);
                }
                for (int i = 0; i < 4; i++) m.Set(x + i, y, crossing ? CityGround.Road : (i == 2 ? CityGround.RoadLine : CityGround.Road));
            }
        }

        /// <summary>Zebra crossings on the four sides of a crossroads (x, y = the bottom-left of the junction).</summary>
        private static void Crossings(CityMap m, int x, int y)
        {
            for (int i = 0; i < 4; i++)
            {
                m.Set(x + i, y - 1, CityGround.Crosswalk);
                m.Set(x + i, y + 4, CityGround.Crosswalk);
                m.Set(x - 1, y + i, CityGround.Crosswalk);
                m.Set(x + 4, y + i, CityGround.Crosswalk);
            }
        }

        /// <summary>A fence line (solid) from (x, y), <paramref name="len"/> tiles along x or y, with an optional gap.</summary>
        private static void Fence(CityMap m, int x, int y, int len, bool horizontal, int gapAt = -1, int gapLen = 0)
        {
            for (int i = 0; i < len; i++)
            {
                if (gapAt >= 0 && i >= gapAt && i < gapAt + gapLen) continue;
                int fx = horizontal ? x + i : x, fy = horizontal ? y : y + i;
                m.AddProp(CityPropKind.Fence, fx, fy, 1, 1, true, horizontal ? 0 : 1);
            }
        }

        /// <summary>
        /// A half court: asphalt (or hardwood indoors), the hoop at the top, a trigger to walk on and play, and a
        /// hangout spot for the crowd beside it.
        /// </summary>
        private static CityCourt Court(CityMap m, string id, string courtId, string name, int x, int y, int w, int h, CityCourtUse use,
                                       CityGround floor = CityGround.Asphalt, CityCourtLook? look = null)
        {
            m.Fill(x, y, w, h, floor);
            var court = new CityCourt { Id = id, CourtId = courtId, Name = name, X = x, Y = y, W = w, H = h, Use = use, Look = look ?? CityCourtLook.Plain };
            m.Courts.Add(court);
            m.AddProp(CityPropKind.Hoop, x + w / 2, y + h - 1, 1, 1, true);
            m.Portals.Add(new CityPortal
            {
                Id = id, Kind = CityPortalKind.Court, Label = "ENTER COURT", Target = id,
                X = x + 1, Y = y + 1, W = w - 2, H = h - 3,
            });
            m.Hangouts[id] = new Vec2(x + w * 0.5f, y + h * 0.4f);
            return court;
        }

        private static void Door(CityMap m, CityBuilding b, string portalId, string label, string target, string targetSpawn, CityPortalKind kind = CityPortalKind.Building)
        {
            m.Portals.Add(new CityPortal
            {
                Id = portalId, Kind = kind, Label = label, Target = target, TargetSpawn = targetSpawn,
                X = b.DoorX - 0.5f, Y = b.Y - 1.2f, W = 2f, H = 1.2f,
            });
        }

        /// <summary>17 Oct 2026: a THE STREETS graffiti wall by the street courts (walk up to it for THE STREETS).</summary>
        private static void StreetsWall(CityMap m, int x, int y, int variant)
        {
            m.AddProp(CityPropKind.Graffiti, x, y, 6, 1, true, variant);
            m.Portals.Add(new CityPortal { Id = "streets." + x + "." + y, Kind = CityPortalKind.Station, Label = "THE STREETS", Target = "streets",
                                           X = x, Y = y - 1.3f, W = 6, H = 1.3f });
        }

        private static void Trees(CityMap m, params int[] xy)
        {
            for (int i = 0; i + 1 < xy.Length; i += 2) m.AddProp(CityPropKind.Tree, xy[i], xy[i + 1], 1, 1, true, i / 2);
        }

        // ------------------------------------------------------------------ First Callers Neighborhood (36 × 44)

        private static CityMap BuildFirstCallers()
        {
            var m = new CityMap(FirstCallers, 36, 44);
            HRoad(m, 18);
            VRoad(m, 16);
            Crossings(m, 16, 18);
            // Sidewalk along the front of the buildings up top.
            m.Fill(0, 23, 16, 1, CityGround.Sidewalk);
            m.Fill(20, 23, 16, 1, CityGround.Sidewalk);

            // NW: home and the First Callers HQ.
            var home = m.AddBuilding(new CityBuilding { Id = "home", Name = "HOME", X = 2, Y = 35, W = 8, H = 6, Style = 0, DoorX = 5, Sign = "HOME", Seed = 11 });
            m.Fill(4, 34, 3, 1, CityGround.Sidewalk);
            m.Fill(5, 24, 1, 10, CityGround.Sidewalk);
            Door(m, home, "door.home", "GO HOME", Crib, "entrance");
            var hq = m.AddBuilding(new CityBuilding { Id = "hq", Name = "FIRST CALLERS HQ", X = 2, Y = 26, W = 10, H = 6, Style = 4, DoorX = 7, Sign = "FIRST CALLERS", Seed = 12 });
            Door(m, hq, "door.hq", "RISE HUB", "hq", "hq");
            Trees(m, 12, 37, 13, 30, 0, 24, 14, 41);
            m.AddProp(CityPropKind.Bench, 10, 24, 2, 1, true);
            m.AddProp(CityPropKind.Mailbox, 9, 34, 1, 1, true);

            // NE: the Baller Gym and its lot.
            var gym = m.AddBuilding(new CityBuilding { Id = "gym", Name = "BALLER GYM", X = 21, Y = 32, W = 13, H = 9, Style = 3, DoorX = 27, Sign = "BALLER GYM", Seed = 13 });
            m.Fill(26, 24, 3, 8, CityGround.Sidewalk);
            Door(m, gym, "door.gym", "ENTER GYM", Gym, "gym.entrance");
            m.Fill(20, 24, 6, 7, CityGround.Asphalt);
            m.AddProp(CityPropKind.Car, 21, 28, 2, 1, true, 0);
            m.AddProp(CityPropKind.Car, 21, 25, 2, 1, true, 1);
            m.AddProp(CityPropKind.Car, 23, 25, 2, 1, true, 2);
            RideSpot(m, 23, 29); // 28 Oct 2026 (BALLER WORLD): YOUR RIDE, for THE DRIVE
            Trees(m, 34, 24, 34, 30, 30, 42, 20, 42);

            // SW: the First Callers' home court.
            var court = Court(m, "court.first_callers", "court.first_callers_home", "FIRST CALLERS COURT", 2, 3, 11, 10, CityCourtUse.Rise,
                              look: new CityCourtLook { Floor = -1, Logo = true });
            Fence(m, 1, 2, 13, true, 5, 3);
            Fence(m, 1, 13, 13, true);
            Fence(m, 1, 3, 10, false);
            Fence(m, 13, 3, 10, false);
            m.AddProp(CityPropKind.Bleachers, 14, 6, 1, 4, true);
            m.AddProp(CityPropKind.Lamp, 0, 15, 1, 1, true);
            m.AddProp(CityPropKind.Lamp, 14, 15, 1, 1, true);
            m.Fill(6, 0, 3, 2, CityGround.Sidewalk);
            m.Hangouts["court.first_callers.side"] = new Vec2(15.2f, 9f);

            // SE: the corner store and the barber (not open yet), a little park.
            var store = m.AddBuilding(new CityBuilding { Id = "store", Name = "CORNER STORE", X = 21, Y = 9, W = 7, H = 6, Style = 5, DoorX = 24, Sign = "CORNER STORE", Seed = 14 });
            Door(m, store, "door.store", "CORNER STORE", CornerStore, "entrance");
            var barber = m.AddBuilding(new CityBuilding { Id = "barber", Name = "FRESH CUTS", X = 29, Y = 9, W = 6, H = 6, Style = 1, DoorX = 31, Sign = "FRESH CUTS", Seed = 15 });
            Door(m, barber, "door.barber", "FRESH CUTS", FreshCuts, "entrance");
            m.Fill(20, 7, 16, 1, CityGround.Sidewalk);
            Trees(m, 22, 3, 26, 2, 30, 4, 34, 2);
            m.AddProp(CityPropKind.Bench, 27, 5, 2, 1, true);
            m.AddProp(CityPropKind.Hydrant, 20, 16, 1, 1, true);
            m.AddProp(CityPropKind.Trash, 28, 16, 1, 1, true);

            // Streetlights along the main road.
            foreach (int x in new[] { 3, 10, 22, 30 }) m.AddProp(CityPropKind.Lamp, x, 23, 1, 1, true);
            foreach (int x in new[] { 3, 10, 24, 33 }) m.AddProp(CityPropKind.Lamp, x, 17, 1, 1, true);

            // The road east goes to the Blacktop District.
            m.Portals.Add(new CityPortal { Id = "road.blacktop", Kind = CityPortalKind.District, Label = "TO BLACKTOP DISTRICT", Target = Blacktop,
                                           TargetSpawn = "from_first_callers", X = 34.6f, Y = 17, W = 1.4f, H = 6 });

            // 17 Oct 2026: up the avenue to Hoop Heights, down it to Holiday Park.
            m.Portals.Add(new CityPortal { Id = "road.heights", Kind = CityPortalKind.District, Label = "TO HOOP HEIGHTS", Target = HoopHeights,
                                           TargetSpawn = "from_first_callers", X = 16, Y = 42.6f, W = 4, H = 1.4f });
            m.Portals.Add(new CityPortal { Id = "road.holiday", Kind = CityPortalKind.District, Label = "TO HOLIDAY PARK", Target = HolidayPark,
                                           TargetSpawn = "from_first_callers", X = 16, Y = 0, W = 4, H = 1.4f });
            m.AddSpawn("home", 5.5f, 33.2f, 0);
            m.AddSpawn("from_heights", 17.5f, 40.8f, 0);
            m.AddSpawn("from_holiday", 17.5f, 2.8f, 1);
            m.AddSpawn("hq", 7.5f, 24.6f, 0);
            m.AddSpawn("gym.door", 27.5f, 30.4f, 0);
            m.AddSpawn("court.first_callers", 7.5f, 1.2f, 1);
            m.AddSpawn("from_blacktop", 33.4f, 20.5f, 2);
            m.AddSpawn("store", 24.5f, 7.8f, 0);
            m.AddSpawn("barber", 31.5f, 7.8f, 0);
            m.AddSpawn("ride", 24f, 27.3f, 0);
            m.Hangouts["home.porch"] = new Vec2(7.5f, 33f);
            m.Hangouts["hq.front"] = new Vec2(9.5f, 24.6f);
            m.Hangouts["gym.lot"] = new Vec2(24f, 27f);
            m.Hangouts["park.se"] = new Vec2(28f, 4f);
            return m;
        }

        // ------------------------------------------------------------------ Blacktop District (48 × 52)

        private static CityMap BuildBlacktop()
        {
            var m = new CityMap(Blacktop, 48, 52, CityGround.Dirt);
            // The waterfront along the top: water, then the pier.
            m.Fill(0, 46, 48, 6, CityGround.Water);
            m.Fill(0, 42, 48, 4, CityGround.Pier);
            HRoad(m, 18);
            VRoad(m, 22, 0, 41);
            Crossings(m, 22, 18);
            m.Fill(0, 23, 22, 1, CityGround.Sidewalk);
            m.Fill(26, 23, 22, 1, CityGround.Sidewalk);
            m.Fill(0, 41, 48, 1, CityGround.Sidewalk);

            // SW: Sunset Cage (fully fenced, one gate).
            Court(m, "court.sunset_cage", "court.sunset_cage", "SUNSET CAGE", 3, 3, 13, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = (int)FloorStyle.Rubber, Logo = true });
            Fence(m, 2, 2, 15, true, 6, 3);
            Fence(m, 2, 14, 15, true);
            Fence(m, 2, 3, 11, false);
            Fence(m, 16, 3, 11, false);
            m.Fill(8, 0, 3, 2, CityGround.Sidewalk);
            m.AddProp(CityPropKind.Lamp, 1, 15, 1, 1, true);
            m.AddProp(CityPropKind.Lamp, 17, 15, 1, 1, true);
            m.Hangouts["court.sunset_cage.side"] = new Vec2(18.5f, 8f);

            // SE: Overpass Park, under the overpass (a row of pillars along the top of the block).
            Court(m, "court.overpass_park", "court.overpass_park", "OVERPASS PARK", 30, 3, 13, 11, CityCourtUse.Streets);
            foreach (int x in new[] { 28, 32, 40, 44 }) m.AddProp(CityPropKind.Sign, x, 15, 1, 1, true, 1); // overpass pillars
            m.AddProp(CityPropKind.Bleachers, 44, 5, 1, 6, true);
            m.AddProp(CityPropKind.Bench, 27, 6, 1, 2, true);
            m.Hangouts["court.overpass_park.side"] = new Vec2(28.5f, 10f);

            // NW: Static Lot (a car-park court) with parked cars.
            Court(m, "court.static_lot", "court.static_lot", "STATIC LOT", 3, 26, 12, 10, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = (int)FloorStyle.Tiles });
            m.AddProp(CityPropKind.Car, 16, 30, 2, 1, true, 3);
            m.AddProp(CityPropKind.Car, 16, 27, 2, 1, true, 4);
            m.AddProp(CityPropKind.Lamp, 2, 37, 1, 1, true);
            m.Hangouts["court.static_lot.side"] = new Vec2(17f, 34f);

            // NE: Pier Nine, out on the boardwalk.
            Court(m, "court.pier_nine", "court.pier_nine", "PIER NINE", 31, 27, 13, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = (int)FloorStyle.Hardwood, Logo = true });
            m.Fill(30, 26, 15, 1, CityGround.Pier);
            m.Fill(30, 38, 15, 3, CityGround.Pier);
            Fence(m, 30, 39, 15, true, 6, 3);
            m.AddProp(CityPropKind.Bench, 27, 30, 1, 2, true);
            m.AddProp(CityPropKind.Lamp, 29, 39, 1, 1, true);
            m.AddProp(CityPropKind.Lamp, 45, 39, 1, 1, true);
            m.Hangouts["court.pier_nine.side"] = new Vec2(28f, 34f);
            m.Hangouts["pier.rail"] = new Vec2(20f, 43.5f);

            foreach (int x in new[] { 5, 13, 30, 40 }) m.AddProp(CityPropKind.Lamp, x, 23, 1, 1, true);
            foreach (int x in new[] { 18, 32 }) m.AddProp(CityPropKind.Lamp, x, 17, 1, 1, true);
            m.AddProp(CityPropKind.Hydrant, 20, 24, 1, 1, true);
            m.AddProp(CityPropKind.Trash, 27, 24, 1, 1, true);
            Trees(m, 20, 34, 20, 28, 1, 20 + 4 + 1, 46, 24);

            m.Portals.Add(new CityPortal { Id = "road.first_callers", Kind = CityPortalKind.District, Label = "TO FIRST CALLERS", Target = FirstCallers,
                                           TargetSpawn = "from_blacktop", X = 0, Y = 17, W = 1.4f, H = 6 });

            StreetsWall(m, 34, 24, 0);
            m.Portals.Add(new CityPortal { Id = "road.boardwalk", Kind = CityPortalKind.District, Label = "TO THE BOARDWALK", Target = Boardwalk,
                                           TargetSpawn = "from_blacktop", X = 46.6f, Y = 17, W = 1.4f, H = 6 });
            m.AddSpawn("from_first_callers", 2.2f, 20.5f, 3);
            m.AddSpawn("from_boardwalk", 45.4f, 20.5f, 2);
            m.AddSpawn("court.sunset_cage", 9.5f, 1.2f, 1);
            m.AddSpawn("court.overpass_park", 36.5f, 1.4f, 1);
            m.AddSpawn("court.static_lot", 9f, 24.6f, 1);
            m.AddSpawn("court.pier_nine", 37.5f, 40.4f, 0);
            BigRunPoster(m, 8, 44); // 28 Oct 2026 (BALLER WORLD): Baller City's BIG RUN
            return m;
        }

        // ------------------------------------------------------------------ Baller Gym (indoors, 24 × 30)

        /// <summary>Practice stations on the gym floor: the drill each one starts.</summary>
        public static readonly (string id, string label, DrillKind drill)[] Stations =
        {
            ("station.free_shoot", "FREE SHOOT", DrillKind.FreeShoot),
            ("station.passing", "PASSING TARGETS", DrillKind.PassingTargets),
            ("station.dribble", "DRIBBLE LANE", DrillKind.DribbleLane),
            ("station.three", "THREE-POINT DRILL", DrillKind.ThreePoint),
            ("station.world", "AROUND THE WORLD", DrillKind.AroundTheWorld),
        };

        private static CityMap BuildGym()
        {
            var m = new CityMap(Gym, 24, 30, CityGround.Tile);
            // Walls round the edge.
            m.Fill(0, 0, 24, 1, CityGround.Wall);
            m.Fill(0, 29, 24, 1, CityGround.Wall);
            m.Fill(0, 0, 1, 30, CityGround.Wall);
            m.Fill(23, 0, 1, 30, CityGround.Wall);
            // The lobby (tile floor) and a wall with an opening onto the training floor.
            m.Fill(1, 9, 22, 1, CityGround.Wall);
            m.Fill(10, 9, 4, 1, CityGround.Tile);
            m.Fill(1, 10, 22, 19, CityGround.Hardwood);
            // The exit, at the bottom centre of the lobby.
            m.Set(11, 0, CityGround.Tile);
            m.Set(12, 0, CityGround.Tile);
            m.Portals.Add(new CityPortal { Id = "door.exit", Kind = CityPortalKind.Exit, Label = "LEAVE BUILDING", Target = FirstCallers, TargetSpawn = "gym.door",
                                           X = 10.5f, Y = 0, W = 3f, H = 1.6f });
            m.AddProp(CityPropKind.Desk, 15, 4, 4, 1, true);
            m.AddProp(CityPropKind.Cooler, 2, 7, 1, 1, true);
            m.AddProp(CityPropKind.Planter, 2, 2, 1, 1, true);
            m.AddProp(CityPropKind.Planter, 21, 2, 1, 1, true);
            m.Portals.Add(new CityPortal { Id = "desk.reception", Kind = CityPortalKind.Station, Label = "VIEW SCHEDULE", Target = "reception", X = 14.5f, Y = 2.4f, W = 5f, H = 1.6f });

            // The training floor: a half court and the stations.
            Court(m, "court.gym", "court.practice_gym", "TRAINING FLOOR", 5, 15, 14, 13, CityCourtUse.Practice, CityGround.Hardwood);
            m.Portals.RemoveAll(p => p.Id == "court.gym"); // the floor itself isn't a game: the stations are
            float[] sx = { 11.5f, 6.5f, 17.5f, 7f, 16f };
            float[] sy = { 19f, 12.5f, 12.5f, 24f, 24f };
            for (int i = 0; i < Stations.Length; i++)
            {
                var s = Stations[i];
                m.AddProp(CityPropKind.Cone, (int)sx[i], (int)sy[i], 1, 1, false, i);
                m.Portals.Add(new CityPortal { Id = s.id, Kind = CityPortalKind.Station, Label = s.label, Target = "drill:" + s.drill,
                                               X = sx[i] - 1f, Y = sy[i] - 1f, W = 2.5f, H = 2.5f });
            }

            // Side doors: the locker room (right) and the Halloween court (left).
            m.Set(23, 20, CityGround.Hardwood);
            m.Portals.Add(new CityPortal { Id = "door.lockers", Kind = CityPortalKind.Building, Label = "LOCKER ROOM", Target = "lockers",
                                           X = 21.4f, Y = 19, W = 1.6f, H = 3 });
            m.AddProp(CityPropKind.Lockers, 21, 23, 1, 3, true);
            m.Set(0, 20, CityGround.Hardwood);
            m.Portals.Add(new CityPortal { Id = "door.halloween", Kind = CityPortalKind.Building, Label = "HALLOWEEN GAME", Target = "halloween",
                                           X = 1, Y = 19, W = 1.6f, H = 3 });
            m.AddProp(CityPropKind.Rack, 2, 25, 1, 2, true);
            m.AddProp(CityPropKind.Bleachers, 1, 11, 4, 1, true);
            m.AddProp(CityPropKind.Bleachers, 19, 11, 4, 1, true);

            m.AddSpawn("gym.entrance", 12f, 1.6f, 1);
            m.AddSpawn("gym.floor", 12f, 13f, 1);
            m.AddSpawn("gym.lockers", 20f, 20.5f, 2);
            m.AddSpawn("gym.halloween", 3f, 20.5f, 3);
            foreach (var s in Stations) m.AddSpawn("gym." + s.id, sx[Array.IndexOf(Stations, s)], sy[Array.IndexOf(Stations, s)] - 1.6f, 1);
            m.Hangouts["gym.floor"] = new Vec2(12f, 16f);
            m.Hangouts["gym.lobby"] = new Vec2(8f, 5f);
            m.Hangouts["gym.bleachers"] = new Vec2(4f, 12f);
            return m;
        }

        // ------------------------------------------------------------------ Hoop Heights (52 × 72)

        /// <summary>17 Oct 2026 (Omari): six courts, each with its own look, up the avenue from First Callers.</summary>
        private static CityMap BuildHoopHeights()
        {
            var m = new CityMap(HoopHeights, 52, 72, CityGround.Sidewalk);
            HRoad(m, 27);
            HRoad(m, 50);
            VRoad(m, 24);
            Crossings(m, 24, 27);
            Crossings(m, 24, 50);

            // Row 1 west: Coin-Op Court, a neon grid between the arcade cabinets and the laundromat.
            m.Fill(1, 3, 21, 15, CityGround.Asphalt);
            Court(m, "court.coin_op", "court.coin_op", "COIN-OP COURT", 4, 6, 13, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = (int)FloorStyle.NeonGrid, Neon = true, Logo = true });
            m.AddBuilding(new CityBuilding { Id = "laundromat", Name = "COIN LAUNDRY", X = 4, Y = 19, W = 13, H = 5, Style = 4, Sign = "OPEN 24 HOURS", Seed = 31 });
            foreach (int y in new[] { 7, 10, 13 }) m.AddProp(CityPropKind.Arcade, 2, y, 1, 1, true, y);
            foreach (int y in new[] { 7, 10, 13 }) m.AddProp(CityPropKind.Arcade, 18, y, 1, 1, true, y + 1);
            m.AddProp(CityPropKind.Lamp, 1, 17, 1, 1, true);
            m.AddProp(CityPropKind.Lamp, 20, 17, 1, 1, true);
            m.Hangouts["court.coin_op.side"] = new Vec2(20.5f, 11f);

            // Row 1 east: Skate Bowl, ramps all round the deep end.
            Court(m, "court.skate_bowl", "court.skate_bowl", "SKATE BOWL", 34, 6, 13, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = (int)FloorStyle.Tiles, Logo = true });
            m.AddProp(CityPropKind.Ramp, 31, 7, 2, 1, true, 0);
            m.AddProp(CityPropKind.Ramp, 31, 12, 2, 1, true, 0);
            m.AddProp(CityPropKind.Ramp, 48, 7, 2, 1, true, 1);
            m.AddProp(CityPropKind.Ramp, 48, 12, 2, 1, true, 1);
            m.AddProp(CityPropKind.Bench, 36, 19, 2, 1, true);
            m.AddProp(CityPropKind.Bench, 42, 19, 2, 1, true);
            Trees(m, 31, 20, 49, 20, 50, 2, 30, 2);
            m.Hangouts["court.skate_bowl.side"] = new Vec2(32f, 17.5f);

            // Row 2 west: Lantern Market, tiles under the paper lanterns.
            Court(m, "court.lantern_market", "court.lantern_market", "LANTERN MARKET", 4, 34, 13, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = (int)FloorStyle.Tiles, Logo = true });
            foreach (int y in new[] { 35, 39, 43 }) { m.AddProp(CityPropKind.LanternPost, 2, y, 1, 1, true, y); m.AddProp(CityPropKind.LanternPost, 18, y, 1, 1, true, y + 1); }
            m.AddProp(CityPropKind.Stall, 4, 46, 3, 1, true, 0);
            m.AddProp(CityPropKind.Stall, 9, 46, 3, 1, true, 1);
            m.AddProp(CityPropKind.Stall, 14, 46, 3, 1, true, 2);
            m.Hangouts["court.lantern_market.side"] = new Vec2(20.5f, 40f);

            // Row 2 east: Night Market, the stalls after closing.
            Court(m, "court.night_market", "court.night_market", "NIGHT MARKET", 33, 34, 13, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = -1, Logo = true });
            m.AddProp(CityPropKind.Stall, 48, 35, 3, 1, true, 3);
            m.AddProp(CityPropKind.Stall, 48, 40, 3, 1, true, 4);
            m.AddProp(CityPropKind.Stall, 34, 46, 3, 1, true, 5);
            m.AddProp(CityPropKind.Stall, 42, 46, 3, 1, true, 0);
            foreach (int y in new[] { 36, 42 }) m.AddProp(CityPropKind.LanternPost, 31, y, 1, 1, true, y);
            m.Hangouts["court.night_market.side"] = new Vec2(30.5f, 39f);

            // Row 3 west: Rooftop Ring, a fenced deck with a parapet.
            m.Fill(2, 56, 17, 15, CityGround.Tile);
            Court(m, "court.rooftop_ring", "court.rooftop_ring", "ROOFTOP RING", 4, 58, 13, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = -1, Logo = true });
            Fence(m, 2, 70, 17, true);
            Fence(m, 2, 56, 17, true, 7, 3);
            Fence(m, 2, 57, 13, false);
            Fence(m, 18, 57, 13, false);
            m.AddProp(CityPropKind.Lamp, 0, 57, 1, 1, true);
            m.AddProp(CityPropKind.Cooler, 20, 66, 1, 1, true);
            m.Hangouts["court.rooftop_ring.side"] = new Vec2(21f, 62f);

            // Row 3 east: Starlight Rink, maple with neon edges and a mirror-ball glow.
            Court(m, "court.starlight_rink", "court.starlight_rink", "STARLIGHT RINK", 33, 58, 13, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = (int)FloorStyle.Hardwood, Neon = true, Logo = true });
            m.AddProp(CityPropKind.Bleachers, 47, 59, 1, 8, true);
            m.AddProp(CityPropKind.Lamp, 31, 57, 1, 1, true);
            m.AddProp(CityPropKind.Lamp, 31, 69, 1, 1, true);
            m.Hangouts["court.starlight_rink.side"] = new Vec2(30.5f, 63f);

            foreach (int y in new[] { 25, 32, 48, 55 }) { m.AddProp(CityPropKind.Lamp, 21, y, 1, 1, true); m.AddProp(CityPropKind.Lamp, 30, y, 1, 1, true); }
            m.Portals.Add(new CityPortal { Id = "road.first_callers", Kind = CityPortalKind.District, Label = "TO FIRST CALLERS", Target = FirstCallers,
                                           TargetSpawn = "from_heights", X = 24, Y = 0, W = 4, H = 1.4f });
            StreetsWall(m, 13, 2, 1);
            m.Portals.Add(new CityPortal { Id = "road.downtown", Kind = CityPortalKind.District, Label = "TO DOWNTOWN", Target = Downtown,
                                           TargetSpawn = "from_heights", X = 50.6f, Y = 26, W = 1.4f, H = 6 });
            m.AddSpawn("from_first_callers", 25.5f, 2.6f, 1);
            m.AddSpawn("from_downtown", 49.4f, 29.5f, 2);
            m.AddSpawn("court.coin_op", 10.5f, 4.6f, 1);
            m.AddSpawn("court.skate_bowl", 40.5f, 4.6f, 1);
            m.AddSpawn("court.lantern_market", 10.5f, 32.8f, 1);
            m.AddSpawn("court.night_market", 39.5f, 32.8f, 1);
            m.AddSpawn("court.rooftop_ring", 10.5f, 55.2f, 1);
            m.AddSpawn("court.starlight_rink", 39.5f, 56.8f, 1);
            m.Hangouts["plaza.south"] = new Vec2(22f, 8f);
            m.Hangouts["plaza.north"] = new Vec2(29.5f, 60f);
            return m;
        }

        // ------------------------------------------------------------------ Holiday Park (44 × 48)

        /// <summary>17 Oct 2026 (Omari): the four Holiday Game courts, dressed for their holiday, open all year.</summary>
        private static CityMap BuildHolidayPark()
        {
            var m = new CityMap(HolidayPark, 44, 48, CityGround.Grass);
            HRoad(m, 22);
            VRoad(m, 20);
            Crossings(m, 20, 22);

            // NW: Candy Cane Court in the snow, with lit trees and snowmen.
            m.Fill(0, 27, 19, 21, CityGround.Snow);
            Court(m, "court.holiday_christmas", Holidays.ChristmasCourtId, "CANDY CANE COURT", 3, 31, 13, 11, CityCourtUse.Holiday,
                  look: new CityCourtLook { Floor = -1, Theme = HolidayTheme.Christmas, Logo = true });
            foreach (var (x, y) in new[] { (1, 44), (17, 44), (1, 33), (17, 37) }) m.AddProp(CityPropKind.PineLit, x, y, 1, 1, true, x + y);
            m.AddProp(CityPropKind.Snowman, 17, 30, 1, 1, true);
            m.AddProp(CityPropKind.Snowman, 1, 29, 1, 1, true, 1);
            m.Fill(9, 27, 2, 4, CityGround.Sidewalk);
            m.Hangouts["court.holiday_christmas.side"] = new Vec2(17.5f, 41f);

            // NE: Pumpkin Patch Court under the bare trees.
            m.Fill(25, 27, 19, 21, CityGround.Dirt);
            Court(m, "court.holiday_halloween", Holidays.HalloweenCourtId, "PUMPKIN PATCH COURT", 28, 31, 13, 11, CityCourtUse.Holiday,
                  look: new CityCourtLook { Floor = -1, Theme = HolidayTheme.Halloween, Logo = true });
            foreach (var (x, y) in new[] { (26, 30), (42, 30), (26, 37), (42, 36), (30, 44), (38, 44), (34, 45) }) m.AddProp(CityPropKind.Pumpkin, x, y, 1, 1, true, x * 3 + y);
            m.AddProp(CityPropKind.LanternPost, 26, 43, 1, 1, true, 7);
            m.AddProp(CityPropKind.LanternPost, 42, 43, 1, 1, true, 8);
            m.Fill(34, 27, 2, 4, CityGround.Sidewalk);
            m.Hangouts["court.holiday_halloween.side"] = new Vec2(26.5f, 40f);

            // SW: Egg Hunt Court in the spring grass.
            Court(m, "court.holiday_easter", Holidays.EasterCourtId, "EGG HUNT COURT", 3, 5, 13, 11, CityCourtUse.Holiday,
                  look: new CityCourtLook { Floor = (int)FloorStyle.Rubber, Theme = HolidayTheme.Easter, Logo = true });
            foreach (var (x, y) in new[] { (1, 4), (17, 6), (1, 12), (17, 13), (6, 18), (12, 18), (15, 2) }) m.AddProp(CityPropKind.Egg, x, y, 1, 1, false, x + y);
            m.AddProp(CityPropKind.Bush, 1, 8, 1, 1, true);
            m.AddProp(CityPropKind.Bush, 17, 9, 1, 1, true);
            Trees(m, 2, 19, 16, 19);
            m.Hangouts["court.holiday_easter.side"] = new Vec2(17.5f, 16f);

            // SE: Firework Court on the plaza, bunting over both ends.
            m.Fill(25, 0, 19, 21, CityGround.Sidewalk);
            Court(m, "court.holiday_fourth", Holidays.FourthCourtId, "FIREWORK COURT", 28, 5, 13, 11, CityCourtUse.Holiday,
                  look: new CityCourtLook { Floor = (int)FloorStyle.Tiles, Theme = HolidayTheme.FourthOfJuly, Logo = true });
            m.AddProp(CityPropKind.Bunting, 27, 18, 15, 1, false, 0);
            m.AddProp(CityPropKind.Bunting, 27, 2, 15, 1, false, 1);
            m.AddProp(CityPropKind.Bench, 26, 8, 1, 2, true);
            m.AddProp(CityPropKind.Bench, 42, 8, 1, 2, true);
            m.Hangouts["court.holiday_fourth.side"] = new Vec2(25.5f, 13f);

            foreach (int x in new[] { 4, 14, 28, 38 }) { m.AddProp(CityPropKind.Lamp, x, 20, 1, 1, true); m.AddProp(CityPropKind.Lamp, x, 27, 1, 1, true); }
            m.Portals.Add(new CityPortal { Id = "road.first_callers", Kind = CityPortalKind.District, Label = "TO FIRST CALLERS", Target = FirstCallers,
                                           TargetSpawn = "from_holiday", X = 20, Y = 46.6f, W = 4, H = 1.4f });
            m.Portals.Add(new CityPortal { Id = "road.school", Kind = CityPortalKind.District, Label = "TO THE SCHOOL YARD", Target = SchoolYard,
                                           TargetSpawn = "from_holiday_park", X = 0, Y = 21, W = 1.4f, H = 6 });
            m.AddSpawn("from_first_callers", 21.5f, 45f, 0);
            m.AddSpawn("from_school", 2.6f, 24.5f, 3);
            m.AddSpawn("court.holiday_christmas", 9.5f, 29.2f, 1);
            m.AddSpawn("court.holiday_halloween", 34.5f, 29.2f, 1);
            m.AddSpawn("court.holiday_easter", 9.5f, 3.2f, 1);
            m.AddSpawn("court.holiday_fourth", 34.5f, 3.2f, 1);
            m.Hangouts["park.center"] = new Vec2(21.5f, 35f);
            return m;
        }

        // ------------------------------------------------------------------ The Boardwalk (52 × 44)

        /// <summary>17 Oct 2026 (phase 3): the beach east of the Blacktop: three courts on the sand and the ferry deck on the pier.</summary>
        private static CityMap BuildBoardwalk()
        {
            var m = new CityMap(Boardwalk, 52, 44, CityGround.Sand);
            HRoad(m, 18);
            m.Fill(0, 24, 52, 14, CityGround.Pier);
            m.Fill(0, 38, 52, 6, CityGround.Water);

            Court(m, "court.boardwalk_slab", "court.boardwalk_slab", "BOARDWALK SLAB", 3, 3, 13, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = -1, Logo = true });
            foreach (int y in new[] { 4, 8, 12 }) m.AddProp(CityPropKind.Arcade, 1, y, 1, 1, true, y + 3);
            m.Hangouts["court.boardwalk_slab.side"] = new Vec2(17f, 8f);

            Court(m, "court.sundown_yard", "court.sundown_yard", "SUNDOWN YARD", 19, 3, 13, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = (int)FloorStyle.Rubber, Logo = true });
            m.AddProp(CityPropKind.Umbrella, 33, 4, 1, 1, true, 0);
            m.AddProp(CityPropKind.Umbrella, 33, 10, 1, 1, true, 1);
            m.Hangouts["court.sundown_yard.side"] = new Vec2(33.5f, 7f);

            Court(m, "court.lighthouse_point", "court.lighthouse_point", "LIGHTHOUSE POINT", 36, 3, 13, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = (int)FloorStyle.Tiles, Logo = true });
            m.AddProp(CityPropKind.Palm, 50, 4, 1, 1, true, 0);
            m.AddProp(CityPropKind.Palm, 50, 11, 1, 1, true, 1);
            m.Hangouts["court.lighthouse_point.side"] = new Vec2(50.5f, 8f);

            // The pier: the ferry deck, snack stalls and the lighthouse.
            Court(m, "court.ferry_deck", "court.ferry_deck", "FERRY DECK", 19, 25, 13, 11, CityCourtUse.Streets, CityGround.Pier,
                  look: new CityCourtLook { Floor = (int)FloorStyle.Hardwood, Neon = true, Logo = true });
            Fence(m, 0, 37, 52, true);
            m.AddProp(CityPropKind.Stall, 3, 30, 3, 1, true, 2);
            m.AddProp(CityPropKind.Stall, 9, 30, 3, 1, true, 4);
            m.AddBuilding(new CityBuilding { Id = "lighthouse", Name = "LIGHTHOUSE", X = 42, Y = 28, W = 6, H = 8, Style = 2, Sign = "LIGHTHOUSE", Seed = 41 });
            m.AddProp(CityPropKind.Bench, 34, 27, 1, 2, true);
            m.Hangouts["court.ferry_deck.side"] = new Vec2(35f, 31f);
            m.Hangouts["pier.rail"] = new Vec2(12f, 35f);

            foreach (int x in new[] { 6, 18, 34, 46 }) { m.AddProp(CityPropKind.Lamp, x, 16, 1, 1, true); m.AddProp(CityPropKind.Lamp, x, 23, 1, 1, true); }
            StreetsWall(m, 13, 23, 2);
            m.Portals.Add(new CityPortal { Id = "road.blacktop", Kind = CityPortalKind.District, Label = "TO BLACKTOP DISTRICT", Target = Blacktop,
                                           TargetSpawn = "from_boardwalk", X = 0, Y = 17, W = 1.4f, H = 6 });
            m.AddSpawn("from_blacktop", 2.6f, 20.5f, 3);
            m.AddSpawn("court.boardwalk_slab", 9.5f, 15.2f, 0);
            m.AddSpawn("court.sundown_yard", 25.5f, 15.2f, 0);
            m.AddSpawn("court.lighthouse_point", 42.5f, 15.2f, 0);
            m.AddSpawn("court.ferry_deck", 25.5f, 24.4f, 1);
            return m;
        }

        // ------------------------------------------------------------------ Downtown (52 × 48)

        /// <summary>17 Oct 2026 (phase 3): east of Hoop Heights: the towers, the arena, and four courts between them.</summary>
        private static CityMap BuildDowntown()
        {
            var m = new CityMap(Downtown, 52, 48, CityGround.Sidewalk);
            HRoad(m, 27);

            Court(m, "court.rain_alley", "court.rain_alley", "RAIN ALLEY", 3, 3, 13, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = -1, Neon = true });
            m.AddBuilding(new CityBuilding { Id = "tower.w", Name = "TOWER", X = 1, Y = 15, W = 6, H = 9, Style = 3, Seed = 51 });
            m.AddBuilding(new CityBuilding { Id = "tower.w2", Name = "TOWER", X = 10, Y = 16, W = 7, H = 8, Style = 4, Seed = 52 });
            m.AddProp(CityPropKind.Trash, 17, 5, 1, 1, true);
            m.Hangouts["court.rain_alley.side"] = new Vec2(17.5f, 9f);

            m.AddBuilding(new CityBuilding { Id = "heights_tower", Name = "BALLER TOWER", X = 19, Y = 4, W = 13, H = 10, Style = 4, Sign = "BALLER TOWER", Seed = 53 });
            m.AddProp(CityPropKind.Planter, 19, 2, 1, 1, true);
            m.AddProp(CityPropKind.Planter, 31, 2, 1, 1, true);

            Court(m, "court.tram_yard", "court.tram_yard", "TRAM YARD", 35, 3, 13, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = (int)FloorStyle.Rubber, Logo = true });
            m.AddProp(CityPropKind.Car, 35, 16, 3, 1, true, 4);
            m.AddProp(CityPropKind.Car, 41, 16, 3, 1, true, 1);
            m.Hangouts["court.tram_yard.side"] = new Vec2(33.5f, 9f);

            Court(m, "court.record_roof", "court.record_roof", "RECORD SHOP ROOF", 3, 34, 13, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = (int)FloorStyle.Tiles, Logo = true });
            m.Hangouts["court.record_roof.side"] = new Vec2(17.5f, 39f);

            // The arena: a landmark for now (the doors open in a later update).
            // 17 Oct 2026 (phase 5): the arena doors are open.
            var arena = m.AddBuilding(new CityBuilding { Id = "arena", Name = "BALLER ARENA", X = 19, Y = 34, W = 14, H = 12, Style = 5, DoorX = 26, Sign = "BALLER ARENA", Seed = 54 });
            Door(m, arena, "door.arena", "ENTER ARENA", Arena, "entrance");
            m.AddSpawn("arena", 26.5f, 32.2f, 0);

            Court(m, "court.night_bus_depot", "court.night_bus_depot", "NIGHT BUS DEPOT", 35, 34, 13, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = -1, Neon = true, Logo = true });
            m.AddProp(CityPropKind.Car, 48, 35, 3, 1, true, 3);
            m.AddProp(CityPropKind.Car, 48, 41, 3, 1, true, 5);
            m.Hangouts["court.night_bus_depot.side"] = new Vec2(33.5f, 40f);

            foreach (int x in new[] { 4, 14, 37, 47 }) { m.AddProp(CityPropKind.Lamp, x, 25, 1, 1, true); m.AddProp(CityPropKind.Lamp, x, 32, 1, 1, true); }
            StreetsWall(m, 36, 20, 1);
            m.Portals.Add(new CityPortal { Id = "road.heights", Kind = CityPortalKind.District, Label = "TO HOOP HEIGHTS", Target = HoopHeights,
                                           TargetSpawn = "from_downtown", X = 0, Y = 26, W = 1.4f, H = 6 });
            m.AddSpawn("from_heights", 2.6f, 29.5f, 3);
            m.AddSpawn("court.rain_alley", 9.5f, 1.6f, 1);
            m.AddSpawn("court.tram_yard", 41.5f, 1.6f, 1);
            m.AddSpawn("court.record_roof", 9.5f, 32.8f, 1);
            m.AddSpawn("court.night_bus_depot", 41.5f, 32.8f, 1);
            m.Hangouts["arena.front"] = new Vec2(26f, 32.8f);
            return m;
        }

        // ------------------------------------------------------------------ School Yard (52 × 44)

        /// <summary>17 Oct 2026 (phase 3): Baller City High, west of Holiday Park: three courts by the school.</summary>
        private static CityMap BuildSchoolYard()
        {
            var m = new CityMap(SchoolYard, 52, 44, CityGround.Grass);
            HRoad(m, 22);

            Court(m, "court.pinwheel_lot", "court.pinwheel_lot", "PINWHEEL LOT", 3, 3, 13, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = (int)FloorStyle.Rubber, Logo = true });
            m.Hangouts["court.pinwheel_lot.side"] = new Vec2(17f, 8f);

            Court(m, "court.snowline_park", "court.snowline_park", "SNOWLINE PARK", 19, 3, 13, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = (int)FloorStyle.Tiles });
            m.Hangouts["court.snowline_park.side"] = new Vec2(33f, 8f);

            Court(m, "court.canyon_rim", "court.canyon_rim", "CANYON RIM", 35, 3, 13, 11, CityCourtUse.Streets,
                  look: new CityCourtLook { Floor = (int)FloorStyle.Hardwood, Logo = true });
            m.AddProp(CityPropKind.Bleachers, 49, 4, 1, 8, true);
            m.Hangouts["court.canyon_rim.side"] = new Vec2(34f, 15.5f);

            // Baller City High and its front lawn.
            m.Fill(4, 28, 44, 2, CityGround.Sidewalk);
            m.AddBuilding(new CityBuilding { Id = "school", Name = "BALLER CITY HIGH", X = 8, Y = 31, W = 34, H = 10, Style = 0, DoorX = 25, Sign = "BALLER CITY HIGH", Seed = 61 });
            m.Fill(24, 27, 3, 4, CityGround.Sidewalk);
            Trees(m, 4, 31, 45, 31, 4, 38, 45, 38);
            m.AddProp(CityPropKind.Bench, 14, 28, 2, 1, true);
            m.AddProp(CityPropKind.Bench, 34, 28, 2, 1, true);
            m.Portals.Add(new CityPortal { Id = "door.school", Kind = CityPortalKind.Building, Label = "BALLER CITY HIGH", Target = "school",
                                           X = 24.5f, Y = 29.8f, W = 2f, H = 1.2f });

            foreach (int x in new[] { 6, 18, 34, 46 }) { m.AddProp(CityPropKind.Lamp, x, 20, 1, 1, true); m.AddProp(CityPropKind.Lamp, x, 27, 1, 1, true); }
            StreetsWall(m, 3, 17, 0);
            m.Portals.Add(new CityPortal { Id = "road.holiday", Kind = CityPortalKind.District, Label = "TO HOLIDAY PARK", Target = HolidayPark,
                                           TargetSpawn = "from_school", X = 50.6f, Y = 21, W = 1.4f, H = 6 });
            m.AddSpawn("from_holiday_park", 49.4f, 24.5f, 2);
            m.AddSpawn("court.pinwheel_lot", 9.5f, 15.2f, 0);
            m.AddSpawn("court.snowline_park", 25.5f, 15.2f, 0);
            m.AddSpawn("court.canyon_rim", 41.5f, 15.2f, 0);
            m.Hangouts["school.steps"] = new Vec2(28f, 28.5f);
            return m;
        }

        // ------------------------------------------------------------------ the shops (indoors)

        private static CityMap Room(string id, int w, int h, CityGround floor, string exitSpawn, string exitDistrict = FirstCallers)
        {
            var m = new CityMap(id, w, h, floor);
            m.Fill(0, 0, w, 1, CityGround.Wall);
            m.Fill(0, h - 1, w, 1, CityGround.Wall);
            m.Fill(0, 0, 1, h, CityGround.Wall);
            m.Fill(w - 1, 0, 1, h, CityGround.Wall);
            int door = w / 2 - 1;
            m.Set(door, 0, floor);
            m.Set(door + 1, 0, floor);
            m.Portals.Add(new CityPortal { Id = "door.exit", Kind = CityPortalKind.Exit, Label = "LEAVE BUILDING", Target = exitDistrict, TargetSpawn = exitSpawn,
                                           X = door - 0.5f, Y = 0, W = 3f, H = 1.6f });
            m.AddSpawn("entrance", door + 1f, 1.6f, 1);
            return m;
        }

        /// <summary>17 Oct 2026 (phase 5): the Baller Arena: the championship floor, the crowd and the scoreboard.</summary>
        private static CityMap BuildArena()
        {
            var m = Room(Arena, 30, 34, CityGround.Tile, "arena", Downtown);
            Court(m, "court.arena", CityArena.CourtId, "BALLER ARENA", 6, 9, 18, 16, CityCourtUse.Arena, CityGround.Hardwood,
                  look: new CityCourtLook { Floor = (int)FloorStyle.Hardwood, Neon = true, Logo = true });
            m.AddProp(CityPropKind.Crowd, 1, 9, 4, 16, true, 0);
            m.AddProp(CityPropKind.Crowd, 25, 9, 4, 16, true, 1);
            m.AddProp(CityPropKind.Crowd, 6, 29, 18, 3, true, 2);
            m.AddProp(CityPropKind.Scoreboard, 11, 27, 8, 1, true);
            m.AddProp(CityPropKind.Desk, 3, 4, 4, 1, true, 3);
            m.Portals.Add(new CityPortal { Id = "arena.desk", Kind = CityPortalKind.Station, Label = "CITY CHAMPIONSHIP", Target = "arena.desk", X = 2.5f, Y = 2.4f, W = 5f, H = 1.6f });
            m.AddSpawn("court.arena", 15f, 7.6f, 1);
            m.Hangouts["arena.floor"] = new Vec2(15f, 15f);
            m.Hangouts["arena.concourse"] = new Vec2(22f, 5f);
            return m;
        }

        /// <summary>
        /// 17 Oct 2026 (phase 7): YOUR CRIB. The trophy case and the banner wall are drawn from your save (see
        /// <see cref="CityCrib"/>); their tiles are kept clear and solid here.
        /// </summary>
        private static CityMap BuildCrib()
        {
            var m = Room(Crib, 16, 14, CityGround.Hardwood, "home");
            m.Block(2, 10, 4, 1);
            m.Block(9, 11, 6, 1);
            m.AddProp(CityPropKind.Bench, 9, 4, 3, 1, true);
            m.AddProp(CityPropKind.Planter, 13, 11, 1, 1, true);
            m.AddProp(CityPropKind.Lockers, 14, 4, 1, 3, true);
            m.AddProp(CityPropKind.Safe, 2, 4, 1, 1, true);
            m.Portals.Add(new CityPortal { Id = "crib.trophies", Kind = CityPortalKind.Station, Label = "TROPHY CASE", Target = "crib.trophies", X = 2f, Y = 8.6f, W = 4f, H = 1.4f });
            m.Portals.Add(new CityPortal { Id = "crib.banners", Kind = CityPortalKind.Station, Label = "CREW BANNERS", Target = "crib.banners", X = 9f, Y = 9.6f, W = 6f, H = 1.4f });
            m.Portals.Add(new CityPortal { Id = "crib.safe", Kind = CityPortalKind.Station, Label = "THE SAFE", Target = "crib.safe", X = 1.4f, Y = 2.6f, W = 2.2f, H = 1.4f });
            m.Portals.Add(new CityPortal { Id = "crib.wardrobe", Kind = CityPortalKind.Station, Label = "WARDROBE", Target = "lockers", X = 12.4f, Y = 4f, W = 1.6f, H = 3f });
            m.Hangouts["crib.couch"] = new Vec2(10.5f, 3f);
            return m;
        }

        /// <summary>17 Oct 2026 (phase 3): the Corner Store: shelves of gear and the checkout (today's stock).</summary>
        private static CityMap BuildCornerStore()
        {
            var m = Room(CornerStore, 16, 14, CityGround.Tile, "store");
            m.AddProp(CityPropKind.Shelf, 2, 6, 4, 1, true, 0);
            m.AddProp(CityPropKind.Shelf, 2, 10, 4, 1, true, 1);
            m.AddProp(CityPropKind.Shelf, 10, 10, 4, 1, true, 2);
            m.AddProp(CityPropKind.Cooler, 14, 6, 1, 1, true);
            m.AddProp(CityPropKind.Cooler, 14, 4, 1, 1, true);
            m.AddProp(CityPropKind.Desk, 9, 5, 4, 1, true, 1);
            m.Portals.Add(new CityPortal { Id = "counter", Kind = CityPortalKind.Station, Label = "SHOP", Target = "shop", X = 8.5f, Y = 3.4f, W = 5f, H = 1.6f });
            m.Hangouts["store.aisle"] = new Vec2(7.5f, 8.5f);
            return m;
        }

        /// <summary>17 Oct 2026 (phase 3): Fresh Cuts: two chairs, the mirrors and the waiting bench.</summary>
        private static CityMap BuildFreshCuts()
        {
            var m = Room(FreshCuts, 14, 12, CityGround.Tile, "barber");
            m.AddProp(CityPropKind.Mirror, 2, 10, 4, 1, true, 0);
            m.AddProp(CityPropKind.Mirror, 8, 10, 4, 1, true, 1);
            m.AddProp(CityPropKind.BarberChair, 3, 8, 1, 1, true, 0);
            m.AddProp(CityPropKind.BarberChair, 9, 8, 1, 1, true, 1);
            m.AddProp(CityPropKind.Bench, 1, 3, 1, 3, true);
            m.AddProp(CityPropKind.Desk, 9, 3, 4, 1, true, 2);
            m.Portals.Add(new CityPortal { Id = "chair", Kind = CityPortalKind.Station, Label = "HAIRCUT", Target = "barber.chair", X = 2f, Y = 6.4f, W = 9f, H = 1.6f });
            m.Hangouts["barber.bench"] = new Vec2(2.6f, 4.5f);
            return m;
        }

        // ------------------------------------------------------------------ courts ↔ the city

        /// <summary>Where a court lives in the city (district and the spawn at its gate), or null.</summary>
        public static (string district, string spawn)? CourtHome(string courtId)
        {
            if (string.IsNullOrEmpty(courtId)) return null;
            foreach (var d in All)
            {
                var m = Map(d.Id);
                foreach (var c in m.Courts)
                    if (c.CourtId == courtId || c.Id == courtId)
                        return (d.Id, m.TryGetSpawn(c.Id, out _) ? c.Id : m.DefaultSpawn.Id);
            }
            return null;
        }

        /// <summary>Where to put the player when they walk off a court in a game (a Rise game: their own court).</summary>
        public static (string district, string spawn) ExitFor(MatchRequest r)
        {
            if (r != null && !string.IsNullOrEmpty(r.ReturnTo) && TryParseReturn(r.ReturnTo, out var d0, out var s0)) return (d0, s0);
            if (r != null && (r.Mode == GameMode.Rise || r.Mode == GameMode.Rival)) return (FirstCallers, "court.first_callers");
            var home = CourtHome(r?.CourtId);
            return home ?? (FirstCallers, "court.first_callers");
        }

        /// <summary>"city:district:spawn" — where a game started from the city sends you back to.</summary>
        public static string ReturnTag(string district, string spawn) => "city:" + district + ":" + spawn;

        public static bool TryParseReturn(string tag, out string district, out string spawn)
        {
            district = spawn = null;
            if (string.IsNullOrEmpty(tag) || !tag.StartsWith("city:", StringComparison.Ordinal)) return false;
            var parts = tag.Split(':');
            if (parts.Length != 3 || Find(parts[1]) == null) return false;
            district = parts[1];
            spawn = parts[2];
            return true;
        }
    }
}
