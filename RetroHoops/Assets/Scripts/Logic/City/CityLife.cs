using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public enum CityWeatherKind { Clear = 0, Cloudy = 1, Rain = 2, Wind = 3, Snow = 4 }

    /// <summary>
    /// 17 Oct 2026 (Omari, phase 3): the city's weather. One kind per district per day (the same on every launch that
    /// day, offline). Mostly clear; clouds, rain and wind now and then; rain falls as snow from December to February.
    /// Indoors is always clear. MENU ▸ WEATHER turns it off.
    /// </summary>
    public static class CityWeather
    {
        public static CityWeatherKind For(int day, int month, string district, bool interior, bool off = false)
        {
            if (interior || off) return CityWeatherKind.Clear;
            uint h = StableHash.Of("weather:" + day + ":" + district) % 100;
            var kind = h < 58 ? CityWeatherKind.Clear : h < 78 ? CityWeatherKind.Cloudy : h < 91 ? CityWeatherKind.Rain : CityWeatherKind.Wind;
            bool winter = month == 12 || month == 1 || month == 2;
            if (kind == CityWeatherKind.Rain && winter) kind = CityWeatherKind.Snow;
            return kind;
        }

        /// <summary>How much the sky dims the daylight (added to the night darkness, capped).</summary>
        public static float Gloom(CityWeatherKind k) =>
            k == CityWeatherKind.Rain ? 0.28f : k == CityWeatherKind.Snow ? 0.18f : k == CityWeatherKind.Cloudy ? 0.14f : k == CityWeatherKind.Wind ? 0.06f : 0f;

        public static string Name(CityWeatherKind k) =>
            k == CityWeatherKind.Rain ? "RAIN" : k == CityWeatherKind.Snow ? "SNOW" : k == CityWeatherKind.Cloudy ? "CLOUDY" : k == CityWeatherKind.Wind ? "WINDY" : "CLEAR";

        /// <summary>Falling particles on screen at once (rain or snow; 0 otherwise). Kept small for the frame budget.</summary>
        public static int Particles(CityWeatherKind k, bool reduceMotion) =>
            reduceMotion ? 0 : k == CityWeatherKind.Rain ? 90 : k == CityWeatherKind.Snow ? 60 : k == CityWeatherKind.Wind ? 14 : 0;
    }

    /// <summary>
    /// 17 Oct 2026 (Omari, phase 3): seasonal decorations around the city that follow the calendar: jack-o'-lanterns in
    /// October, lit trees and snowmen in December, eggs at Easter, bunting for the Fourth. They sit beside doors and
    /// along the sidewalks (never in the way: decorations aren't solid). Holiday Park is dressed all year already.
    /// </summary>
    public static class CityDecor
    {
        public static HolidayTheme Season(DateTime date) => Holidays.InSeason(date);

        public static List<CityProp> For(CityMap map, HolidayTheme theme)
        {
            var list = new List<CityProp>();
            if (map == null || theme == HolidayTheme.None || map.DistrictId == CityDistricts.HolidayPark) return list;
            var spots = new List<(int x, int y)>();
            foreach (var b in map.Buildings)
            {
                if (b.DoorX >= 0) { spots.Add((b.DoorX - 2, b.Y - 1)); spots.Add((b.DoorX + 2, b.Y - 1)); }
                else spots.Add((b.X + b.W / 2, b.Y - 1));
            }
            foreach (var k in map.Courts)
            {
                if (k.Use == CityCourtUse.Practice) continue;
                spots.Add((k.X - 1, k.Y + k.H - 1));
                spots.Add((k.X + k.W, k.Y + k.H - 1));
            }
            int i = 0;
            foreach (var (x, y) in spots)
            {
                if (!Free(map, x, y)) continue;
                var kind = theme == HolidayTheme.Halloween ? CityPropKind.Pumpkin
                         : theme == HolidayTheme.Christmas ? (i % 3 == 2 ? CityPropKind.Snowman : CityPropKind.PineLit)
                         : theme == HolidayTheme.Easter ? CityPropKind.Egg
                         : CityPropKind.Bunting;
                list.Add(new CityProp { Kind = kind, X = x, Y = y, W = kind == CityPropKind.Bunting ? 2 : 1, H = 1, Solid = false, Variant = x * 7 + y });
                i++;
            }
            return list;
        }

        private static bool Free(CityMap m, int x, int y)
        {
            if (!m.InBounds(x, y) || m.Blocked(x, y)) return false;
            var g = m.Get(x, y);
            if (g == CityGround.Road || g == CityGround.RoadLine || g == CityGround.Crosswalk || g == CityGround.Water) return false;
            foreach (var p in m.Portals)
                if (p.Contains(x + 0.5f, y + 0.5f)) return false;
            foreach (var k in m.Courts)
                if (k.Contains(x + 0.5f, y + 0.5f)) return false;
            foreach (var p in m.Props)
                if (p.X == x && p.Y == y) return false;
            return true;
        }
    }

    /// <summary>One car driving along a road lane (drawn only; it never blocks you, and it stops for you).</summary>
    public struct CityCar
    {
        public float X, Speed, Cruise;
        public int Lane;
        public int Variant;
    }

    public struct CityLane
    {
        /// <summary>Centre line of the lane (tile units) and the way it drives (+1 east, -1 west).</summary>
        public float Y;
        public int Dir;
    }

    /// <summary>
    /// 17 Oct 2026 (Omari, phase 3): the city's background life: people out walking on the sidewalks and cars on the
    /// roads. Deterministic per district and day. Cars drive on the right, brake for you when you're in front of them
    /// and loop round at the edges; walkers are generated people (never real players) who wander the sidewalks.
    /// </summary>
    public static class CityTraffic
    {
        public const float StopGap = 2.6f;

        /// <summary>Lanes on the district's east-west roads (built by the same road helper, four rows with a centre line).</summary>
        public static List<CityLane> Lanes(CityMap map)
        {
            var lanes = new List<CityLane>();
            if (map == null || map.Interior) return lanes;
            for (int y = 0; y + 3 < map.Height; y++)
            {
                int line = 0, road = 0;
                for (int x = 0; x < map.Width; x++)
                {
                    if (map.Get(x, y + 2) == CityGround.RoadLine) line++;
                    if (IsRoad(map.Get(x, y)) && IsRoad(map.Get(x, y + 3))) road++;
                }
                if (line < map.Width * 0.6f || road < map.Width * 0.9f) continue;
                lanes.Add(new CityLane { Y = y + 0.95f, Dir = 1 });
                lanes.Add(new CityLane { Y = y + 3.05f, Dir = -1 });
                y += 3;
            }
            return lanes;
        }

        private static bool IsRoad(CityGround g) => g == CityGround.Road || g == CityGround.RoadLine || g == CityGround.Crosswalk;

        public static List<CityCar> Cars(CityMap map, List<CityLane> lanes, int day)
        {
            var cars = new List<CityCar>();
            for (int l = 0; l < lanes.Count; l++)
            {
                uint h = StableHash.Of(map.DistrictId + ":cars:" + day + ":" + l);
                int n = 1 + (int)(h % 2);
                for (int i = 0; i < n; i++)
                {
                    float cruise = 3.2f + ((h >> (4 + i * 3)) % 20) / 10f;
                    cars.Add(new CityCar { Lane = l, X = (map.Width / (float)n) * i + (h % 11), Cruise = cruise, Speed = cruise, Variant = (int)((h >> (i * 5)) % 6) });
                }
            }
            return cars;
        }

        /// <summary>Moves every car one step. A car stops when you (or the car ahead) are just in front of it.</summary>
        public static void Step(List<CityCar> cars, List<CityLane> lanes, int mapWidth, Vec2 player, float dt)
        {
            for (int i = 0; i < cars.Count; i++)
            {
                var c = cars[i];
                var lane = lanes[c.Lane];
                float ahead = float.MaxValue;
                if (Math.Abs(player.y - lane.Y) < 0.9f)
                {
                    float d = (player.x - c.X) * lane.Dir;
                    if (d > -0.5f) ahead = Math.Min(ahead, d);
                }
                for (int j = 0; j < cars.Count; j++)
                {
                    if (j == i || cars[j].Lane != c.Lane) continue;
                    float d = (cars[j].X - c.X) * lane.Dir;
                    if (d > 0f) ahead = Math.Min(ahead, d - 1.2f);
                }
                float target = ahead < StopGap ? 0f : ahead < StopGap * 2f ? c.Cruise * (ahead - StopGap) / StopGap : c.Cruise;
                c.Speed += Math.Max(-12f * dt, Math.Min(4f * dt, target - c.Speed));
                if (c.Speed < 0f) c.Speed = 0f;
                c.X += c.Speed * lane.Dir * dt;
                float span = mapWidth + 6f;
                if (c.X > mapWidth + 3f) c.X -= span;
                if (c.X < -3f) c.X += span;
                cars[i] = c;
            }
        }
    }

    /// <summary>A person out walking (not a player you can call out).</summary>
    public sealed class CityWalker
    {
        public PlayerDef Player;
        public RgbColor Shirt, Shorts;
        public Vec2 Anchor;
    }

    public static class CityWalkers
    {
        private static readonly string[] Names = { "Ana", "Theo", "Mo", "Priya", "Sol", "June", "Rafa", "Kemi", "Lou", "Ida", "Ezra", "Noor" };

        /// <summary>People out on the sidewalks today: fewer at night, a couple of shoppers indoors.</summary>
        public static List<CityWalker> For(CityMap map, int day, CityTimeOfDay time)
        {
            var list = new List<CityWalker>();
            if (map == null || map.DistrictId == CityDistricts.Gym) return list;
            int count = map.Interior ? 1 : time == CityTimeOfDay.Night ? 3 : 7;
            var walkable = new List<Vec2>();
            for (int y = 1; y < map.Height - 1; y++)
                for (int x = 1; x < map.Width - 1; x++)
                {
                    var g = map.Get(x, y);
                    bool path = map.Interior ? g == CityGround.Tile : g == CityGround.Sidewalk || g == CityGround.Pier || g == CityGround.Sand;
                    if (path && !map.Blocked(x, y) && !InPortal(map, x, y)) walkable.Add(new Vec2(x + 0.5f, y + 0.5f));
                }
            if (walkable.Count == 0) return list;
            for (int i = 0; i < count; i++)
            {
                uint h = StableHash.Of(map.DistrictId + ":walker:" + day + ":" + i);
                string id = "walker." + map.DistrictId + "." + i;
                var arch = (Archetype)(h % 12);
                list.Add(new CityWalker
                {
                    Player = new PlayerDef { id = id + "." + day, firstName = Names[h % (uint)Names.Length], lastName = "", jerseyNumber = -1,
                                             appearance = DefaultContent.AppearanceFromSeed(id + day, arch) },
                    Shirt = CustomTeams.Palette[(h / 13) % 16],
                    Shorts = CustomTeams.Palette[(h / 211) % 16],
                    Anchor = walkable[(int)((h / 7) % (uint)walkable.Count)],
                });
            }
            return list;
        }

        private static bool InPortal(CityMap m, int x, int y)
        {
            foreach (var p in m.Portals)
                if (p.Contains(x + 0.5f, y + 0.5f)) return true;
            return false;
        }
    }
}
