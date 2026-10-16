using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>What the ground of one Baller City tile is (how it's drawn, and whether you can walk on it).</summary>
    public enum CityGround : byte
    {
        Grass = 0,
        Sidewalk = 1,
        Road = 2,
        RoadLine = 3,
        Crosswalk = 4,
        Asphalt = 5,
        Hardwood = 6,
        Tile = 7,
        Water = 8,
        Pier = 9,
        Sand = 10,
        Dirt = 11,
        Rubber = 12,
        /// <summary>An interior wall (solid).</summary>
        Wall = 13,
        /// <summary>17 Oct 2026: snow (the Christmas court's lawn).</summary>
        Snow = 14,
    }

    public enum CityPropKind : byte
    {
        Tree = 0, Lamp = 1, Bench = 2, Hydrant = 3, Trash = 4, Car = 5, Fence = 6, Hoop = 7, Bush = 8, Bleachers = 9,
        Desk = 10, Rack = 11, Cooler = 12, Lockers = 13, Sign = 14, Cone = 15, Mailbox = 16, Planter = 17,
        // 17 Oct 2026: court dressing and the holidays.
        Pumpkin = 18, PineLit = 19, Snowman = 20, Egg = 21, Bunting = 22, Arcade = 23, LanternPost = 24, Stall = 25, Ramp = 26,
        // 17 Oct 2026 (phase 3): shops, the beach.
        Shelf = 27, BarberChair = 28, Mirror = 29, Palm = 30, Umbrella = 31,
        /// <summary>17 Oct 2026: a graffiti wall: THE STREETS.</summary>
        Graffiti = 32,
    }

    /// <summary>
    /// 17 Oct 2026 (Omari): how a city court is painted, so every court looks like itself: the floor (asphalt,
    /// hardwood, neon grid, tiles, rubber), a centre logo, and holiday dressing (candy-cane lines, cobwebs, eggs, stars).
    /// </summary>
    public struct CityCourtLook
    {
        /// <summary>A <see cref="FloorStyle"/> value; -1 = plain (asphalt outside, hardwood inside).</summary>
        public int Floor;
        public HolidayTheme Theme;
        /// <summary>A painted centre logo.</summary>
        public bool Logo;
        /// <summary>Glow lines on the edges (neon courts).</summary>
        public bool Neon;

        public static CityCourtLook Plain => new CityCourtLook { Floor = -1 };
    }

    /// <summary>A building on the map: its footprint (solid), how it looks, and its door.</summary>
    public sealed class CityBuilding
    {
        public string Id, Name;
        /// <summary>Footprint in tiles (bottom-left origin, y up).</summary>
        public int X, Y, W, H;
        /// <summary>0 brick red, 1 brick brown, 2 stucco, 3 concrete, 4 blue siding, 5 yellow stucco.</summary>
        public int Style;
        /// <summary>Door column (absolute tile x) on the bottom edge; -1 = no door.</summary>
        public int DoorX = -1;
        /// <summary>A sign over the door ("BALLER GYM").</summary>
        public string Sign;
        public uint Seed;

        public bool Contains(int x, int y) => x >= X && x < X + W && y >= Y && y < Y + H;
    }

    /// <summary>A prop on the map: a tree, a lamp, a parked car. Solid tiles block walking.</summary>
    public sealed class CityProp
    {
        public CityPropKind Kind;
        public int X, Y, W = 1, H = 1;
        public bool Solid = true;
        public int Variant;
    }

    /// <summary>A court in the city (drawn with the real court's colours) you can walk onto and play on.</summary>
    public sealed class CityCourt
    {
        public string Id, CourtId, Name;
        public int X, Y, W, H;
        /// <summary>The kinds of games this court runs.</summary>
        public CityCourtUse Use;
        /// <summary>The hoop is at the top edge (true) or the bottom.</summary>
        public bool HoopTop = true;
        public CityCourtLook Look = CityCourtLook.Plain;
        public bool Contains(float x, float y) => x >= X && x < X + W && y >= Y && y < Y + H;
    }

    public enum CityCourtUse { Streets = 0, Park = 1, Rise = 2, Practice = 3, /** 17 Oct 2026: a Holiday Game court. */ Holiday = 4 }

    public enum CityPortalKind { Court = 0, Building = 1, District = 2, Exit = 3, Station = 4 }

    /// <summary>
    /// Something you can walk up to and use: a court, a door, the road to another district, a training station.
    /// <see cref="Target"/> says where it goes (a court id, a building id, a district id, a station id).
    /// </summary>
    public sealed class CityPortal
    {
        public string Id;
        public CityPortalKind Kind;
        public string Label;
        public string Target;
        /// <summary>The spawn this portal puts you at on the other side (district portals and doors).</summary>
        public string TargetSpawn;
        /// <summary>Trigger zone in tiles.</summary>
        public float X, Y, W, H;
        public bool Contains(float x, float y) => x >= X && x < X + W && y >= Y && y < Y + H;
        public Vec2 Center => new Vec2(X + W * 0.5f, Y + H * 0.5f);
    }

    /// <summary>A named place to put the player (on arrival, after a match, out of a door).</summary>
    public struct CitySpawn
    {
        public string Id;
        public Vec2 Position;
        /// <summary>Which way to face (0 down, 1 up, 2 left, 3 right).</summary>
        public int Facing;
    }

    /// <summary>
    /// 16 Oct 2026 (Omari): one Baller City district: a grid of ground tiles (x right, y up, one tile = one court
    /// metre = 16 art pixels), plus the buildings, props, courts and portals on it. Pure data, so movement, collision
    /// and interactions are tested without Unity.
    /// </summary>
    public sealed class CityMap
    {
        public readonly string DistrictId;
        public readonly int Width, Height;
        public readonly CityGround[] Ground;
        private readonly bool[] _solid;
        public readonly List<CityBuilding> Buildings = new List<CityBuilding>();
        public readonly List<CityProp> Props = new List<CityProp>();
        public readonly List<CityCourt> Courts = new List<CityCourt>();
        public readonly List<CityPortal> Portals = new List<CityPortal>();
        public readonly List<CitySpawn> Spawns = new List<CitySpawn>();
        /// <summary>Where NPCs hang out here (location id → spot).</summary>
        public readonly Dictionary<string, Vec2> Hangouts = new Dictionary<string, Vec2>();
        /// <summary>Indoors (no day/night, no streetlights).</summary>
        public bool Interior;

        public CityMap(string districtId, int width, int height, CityGround fill = CityGround.Grass)
        {
            DistrictId = districtId;
            Width = width;
            Height = height;
            Ground = new CityGround[width * height];
            _solid = new bool[width * height];
            for (int i = 0; i < Ground.Length; i++) Ground[i] = fill;
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public CityGround Get(int x, int y) => InBounds(x, y) ? Ground[y * Width + x] : CityGround.Wall;

        public void Set(int x, int y, CityGround g)
        {
            if (InBounds(x, y)) Ground[y * Width + x] = g;
        }

        public void Fill(int x, int y, int w, int h, CityGround g)
        {
            for (int j = y; j < y + h; j++)
                for (int i = x; i < x + w; i++) Set(i, j, g);
        }

        public void Block(int x, int y, int w = 1, int h = 1)
        {
            for (int j = y; j < y + h; j++)
                for (int i = x; i < x + w; i++)
                    if (InBounds(i, j)) _solid[j * Width + i] = true;
        }

        public void Unblock(int x, int y)
        {
            if (InBounds(x, y)) _solid[y * Width + x] = false;
        }

        /// <summary>True when nothing can stand on this tile: off the map, water, walls, buildings, solid props.</summary>
        public bool Blocked(int x, int y)
        {
            if (!InBounds(x, y)) return true;
            var g = Ground[y * Width + x];
            return g == CityGround.Water || g == CityGround.Wall || _solid[y * Width + x];
        }

        public bool Walkable(Vec2 p) => !Blocked((int)Math.Floor(p.x), (int)Math.Floor(p.y));

        public CityBuilding AddBuilding(CityBuilding b)
        {
            Buildings.Add(b);
            Block(b.X, b.Y, b.W, b.H);
            return b;
        }

        public CityProp AddProp(CityPropKind kind, int x, int y, int w = 1, int h = 1, bool solid = true, int variant = 0)
        {
            var p = new CityProp { Kind = kind, X = x, Y = y, W = w, H = h, Solid = solid, Variant = variant };
            Props.Add(p);
            if (solid) Block(x, y, w, h);
            return p;
        }

        public void AddSpawn(string id, float x, float y, int facing = 0) =>
            Spawns.Add(new CitySpawn { Id = id, Position = new Vec2(x, y), Facing = facing });

        public bool TryGetSpawn(string id, out CitySpawn spawn)
        {
            foreach (var s in Spawns)
                if (s.Id == id) { spawn = s; return true; }
            spawn = default;
            return false;
        }

        /// <summary>The district's default spawn (the first one).</summary>
        public CitySpawn DefaultSpawn => Spawns.Count > 0 ? Spawns[0] : new CitySpawn { Id = "", Position = new Vec2(Width * 0.5f, Height * 0.5f) };

        public CityPortal Portal(string id) => Portals.Find(p => p.Id == id);

        public CityCourt Court(string id) => Courts.Find(c => c.Id == id);

        /// <summary>The nearest walkable tile centre to <paramref name="p"/> (searching outward), or the default spawn.</summary>
        public Vec2 NearestWalkable(Vec2 p)
        {
            int cx = (int)Math.Floor(p.x), cy = (int)Math.Floor(p.y);
            for (int r = 0; r < Math.Max(Width, Height); r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        int x = cx + dx, y = cy + dy;
                        if (!Blocked(x, y) && !Blocked(x - 1, y) && !Blocked(x + 1, y)) return new Vec2(x + 0.5f, y + 0.5f);
                    }
            return DefaultSpawn.Position;
        }
    }
}
