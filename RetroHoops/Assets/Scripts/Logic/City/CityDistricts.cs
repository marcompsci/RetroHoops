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
    public static class CityDistricts
    {
        public const string FirstCallers = "first_callers";
        public const string Blacktop = "blacktop";
        public const string Gym = "baller_gym";

        public static readonly CityDistrictDefinition[] All =
        {
            new CityDistrictDefinition { Id = FirstCallers, Name = "FIRST CALLERS NEIGHBORHOOD", Blurb = "Home, the crew's HQ and the Baller Gym.", Build = BuildFirstCallers },
            new CityDistrictDefinition { Id = Blacktop, Name = "BLACKTOP DISTRICT", Blurb = "Street courts, crews and spectators.", Build = BuildBlacktop },
            new CityDistrictDefinition { Id = Gym, Name = "BALLER GYM", Blurb = "Practice Lab, the Halloween court and the locker room.", Interior = true, Build = BuildGym },
        };

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
                                       CityGround floor = CityGround.Asphalt)
        {
            m.Fill(x, y, w, h, floor);
            var court = new CityCourt { Id = id, CourtId = courtId, Name = name, X = x, Y = y, W = w, H = h, Use = use };
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
            Door(m, home, "door.home", "GO HOME", "home", "home");
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
            Trees(m, 34, 24, 34, 30, 30, 42, 20, 42);

            // SW: the First Callers' home court.
            var court = Court(m, "court.first_callers", "court.first_callers_home", "FIRST CALLERS COURT", 2, 3, 11, 10, CityCourtUse.Rise);
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
            Door(m, store, "door.store", "CORNER STORE", "store", null);
            var barber = m.AddBuilding(new CityBuilding { Id = "barber", Name = "FRESH CUTS", X = 29, Y = 9, W = 6, H = 6, Style = 1, DoorX = 31, Sign = "FRESH CUTS", Seed = 15 });
            Door(m, barber, "door.barber", "FRESH CUTS", "barber", null);
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

            m.AddSpawn("home", 5.5f, 33.2f, 0);
            m.AddSpawn("hq", 7.5f, 24.6f, 0);
            m.AddSpawn("gym.door", 27.5f, 30.4f, 0);
            m.AddSpawn("court.first_callers", 7.5f, 1.2f, 1);
            m.AddSpawn("from_blacktop", 33.4f, 20.5f, 2);
            m.AddSpawn("store", 24.5f, 7.8f, 0);
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
            Court(m, "court.sunset_cage", "court.sunset_cage", "SUNSET CAGE", 3, 3, 13, 11, CityCourtUse.Streets);
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
            Court(m, "court.static_lot", "court.static_lot", "STATIC LOT", 3, 26, 12, 10, CityCourtUse.Streets);
            m.AddProp(CityPropKind.Car, 16, 30, 2, 1, true, 3);
            m.AddProp(CityPropKind.Car, 16, 27, 2, 1, true, 4);
            m.AddProp(CityPropKind.Lamp, 2, 37, 1, 1, true);
            m.Hangouts["court.static_lot.side"] = new Vec2(17f, 34f);

            // NE: Pier Nine, out on the boardwalk.
            Court(m, "court.pier_nine", "court.pier_nine", "PIER NINE", 31, 27, 13, 11, CityCourtUse.Streets);
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

            m.AddSpawn("from_first_callers", 2.2f, 20.5f, 3);
            m.AddSpawn("court.sunset_cage", 9.5f, 1.2f, 1);
            m.AddSpawn("court.overpass_park", 36.5f, 1.4f, 1);
            m.AddSpawn("court.static_lot", 9f, 24.6f, 1);
            m.AddSpawn("court.pier_nine", 37.5f, 40.4f, 0);
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
