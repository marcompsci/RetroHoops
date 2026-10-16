using System;

namespace CallerRetroBall.Logic.PixelArt
{
    /// <summary>
    /// 16 Oct 2026 (Omari): BALLER CITY's pixel art, all generated (no downloads): the ground of a district in one
    /// canvas (16 art pixels per tile, the court scale), building facades with roofs and lit-at-night windows, and
    /// the props (trees, streetlights, parked cars, chain-link fences, hoops...). Row 0 is the bottom, like every
    /// canvas in the game. Deterministic per seed.
    /// </summary>
    public static class CityArtGenerator
    {
        public const int Tile = 16;

        private static readonly RgbColor Outline = RgbColor.FromHex("#14141F");

        private static uint Hash(int x, int y, uint seed)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + seed * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        private static float Noise(int x, int y, uint seed) => (Hash(x, y, seed) & 0xFFFF) / 65535f;

        // ------------------------------------------------------------------ ground

        /// <summary>
        /// The whole district's ground. <paramref name="courtColors"/> gives a court's floor, lines and paint by court id
        /// (null for the defaults).
        /// </summary>
        public static PixelCanvas Ground(CityMap map, uint seed, Func<string, (RgbColor floor, RgbColor lines, RgbColor paint)?> courtColors)
        {
            var c = new PixelCanvas(map.Width * Tile, map.Height * Tile);
            for (int ty = 0; ty < map.Height; ty++)
                for (int tx = 0; tx < map.Width; tx++)
                    DrawTile(c, map, tx, ty, seed);
            foreach (var court in map.Courts)
            {
                var colors = courtColors?.Invoke(court.CourtId);
                DrawCourt(c, court, colors, map.Get(court.X, court.Y) == CityGround.Hardwood);
            }
            return c;
        }

        private static void DrawTile(PixelCanvas c, CityMap map, int tx, int ty, uint seed)
        {
            var g = map.Get(tx, ty);
            int ox = tx * Tile, oy = ty * Tile;
            for (int y = 0; y < Tile; y++)
                for (int x = 0; x < Tile; x++)
                {
                    int px = ox + x, py = oy + y;
                    float n = Noise(px, py, seed);
                    c.Pixels[py * c.Width + px] = GroundPixel(map, g, tx, ty, x, y, px, py, n);
                }
        }

        private static RgbColor GroundPixel(CityMap map, CityGround g, int tx, int ty, int x, int y, int px, int py, float n)
        {
            switch (g)
            {
                case CityGround.Grass:
                {
                    var baseC = RgbColor.FromHex("#4C8A3A");
                    if (n > 0.995f) return Hash(px, py, 7) % 2 == 0 ? RgbColor.FromHex("#F2E863") : RgbColor.FromHex("#F28FB1"); // flowers
                    if (n > 0.8f) return RgbColor.FromHex("#5E9E46");
                    if (n < 0.15f) return RgbColor.FromHex("#3F7A31");
                    return baseC;
                }
                case CityGround.Sidewalk:
                {
                    if (x == 0 || y == 0) return RgbColor.FromHex("#9D978C");
                    if (x == 1 || y == Tile - 1) return RgbColor.FromHex("#C9C3B8");
                    return n > 0.93f ? RgbColor.FromHex("#ADA79C") : RgbColor.FromHex("#B8B2A6");
                }
                case CityGround.Road:
                case CityGround.RoadLine:
                {
                    var asphalt = n > 0.9f ? RgbColor.FromHex("#45454E") : n < 0.1f ? RgbColor.FromHex("#303038") : RgbColor.FromHex("#3A3A42");
                    if (g != CityGround.RoadLine) return asphalt;
                    bool horizontal = map.Get(tx - 1, ty) == CityGround.RoadLine || map.Get(tx + 1, ty) == CityGround.RoadLine;
                    if (horizontal ? (y == 0 || y == 1) && x < 9 : (x == 0 || x == 1) && y < 9) return RgbColor.FromHex("#E9C46A");
                    return asphalt;
                }
                case CityGround.Crosswalk:
                {
                    bool rowCrossing = map.Get(tx - 1, ty) == CityGround.Crosswalk || map.Get(tx + 1, ty) == CityGround.Crosswalk;
                    int stripe = rowCrossing ? x : y;
                    if (stripe % 6 < 3) return n > 0.9f ? RgbColor.FromHex("#D9D9D9") : RgbColor.FromHex("#EEEEEA");
                    return RgbColor.FromHex("#3A3A42");
                }
                case CityGround.Asphalt:
                    return n > 0.9f ? RgbColor.FromHex("#55555F") : n < 0.08f ? RgbColor.FromHex("#3F3F48") : RgbColor.FromHex("#4A4A54");
                case CityGround.Hardwood:
                {
                    int plank = py / 4;
                    bool seam = py % 4 == 0 || (px + plank * 11) % 32 == 0;
                    var wood = plank % 2 == 0 ? RgbColor.FromHex("#C68E52") : RgbColor.FromHex("#BC8449");
                    return seam ? RgbColor.FromHex("#9E6B38") : (n > 0.92f ? wood.Lighten(0.08f) : wood);
                }
                case CityGround.Tile:
                {
                    bool dark = ((px / 8) + (py / 8)) % 2 == 0;
                    if (px % 8 == 0 || py % 8 == 0) return RgbColor.FromHex("#B3AC9E");
                    return dark ? RgbColor.FromHex("#CFC8B8") : RgbColor.FromHex("#DDD7C9");
                }
                case CityGround.Water:
                {
                    bool crest = ((px + (py / 3) * 5) % 13 == 0) && py % 3 == 0;
                    if (crest) return RgbColor.FromHex("#7FC4E6");
                    return n > 0.85f ? RgbColor.FromHex("#2F7BA6") : RgbColor.FromHex("#2A6F97");
                }
                case CityGround.Pier:
                {
                    if (px % 5 == 0) return RgbColor.FromHex("#4E3A28");
                    var wood = (px / 5) % 2 == 0 ? RgbColor.FromHex("#8B6B4A") : RgbColor.FromHex("#7F6143");
                    return n > 0.95f ? RgbColor.FromHex("#5E4631") : wood;
                }
                case CityGround.Sand: return n > 0.8f ? RgbColor.FromHex("#D6BC7E") : RgbColor.FromHex("#E2C98F");
                case CityGround.Dirt: return n > 0.85f ? RgbColor.FromHex("#7C7468") : n < 0.12f ? RgbColor.FromHex("#5F584E") : RgbColor.FromHex("#6E675C");
                case CityGround.Rubber: return n > 0.85f ? RgbColor.FromHex("#C25E44") : RgbColor.FromHex("#B5533C");
                case CityGround.Snow:
                {
                    if (n > 0.97f) return RgbColor.FromHex("#CFE3F2");
                    if (n < 0.06f) return RgbColor.FromHex("#E4EEF6");
                    return RgbColor.FromHex("#F4F8FB");
                }
                default: // interior wall, seen from above: a dark top with a lighter lip
                {
                    bool lip = map.Get(tx, ty - 1) != CityGround.Wall && y < 3;
                    if (lip) return y == 0 ? RgbColor.FromHex("#2A2A38") : RgbColor.FromHex("#6D6D86");
                    return n > 0.9f ? RgbColor.FromHex("#45455A") : RgbColor.FromHex("#3D3D50");
                }
            }
        }

        /// <summary>Half-court markings: the boundary, the key under the hoop, the free-throw circle and the arc.</summary>
        private static void DrawCourt(PixelCanvas c, CityCourt court, (RgbColor floor, RgbColor lines, RgbColor paint)? colors, bool indoor)
        {
            var floor = colors?.floor ?? (indoor ? RgbColor.FromHex("#C68E52") : RgbColor.FromHex("#4A4A54"));
            var lines = colors?.lines ?? RgbColor.FromHex("#F2F2F2");
            var paint = colors?.paint ?? RgbColor.FromHex("#B5533C");
            int x0 = court.X * Tile, y0 = court.Y * Tile, w = court.W * Tile, h = court.H * Tile;
            var look = court.Look;
            // 17 Oct 2026: each court's own floor.
            if (!indoor || look.Floor >= 0)
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        c.Pixels[(y0 + y) * c.Width + x0 + x] = FloorPixel(look.Floor, floor, lines, x, y, Noise(x0 + x, y0 + y, 99));
            // Paint (the key) under the hoop at the top.
            int keyW = 5 * Tile, keyH = (int)(5.8f * Tile);
            int kx = x0 + (w - keyW) / 2, ky = y0 + h - 4 - keyH;
            c.FillRect(kx, ky, keyW, keyH, paint);
            // Lines.
            var lc = lines;
            Rect(c, x0 + 2, y0 + 2, w - 4, h - 4, lc);
            Rect(c, kx, ky, keyW, keyH, lc);
            float cx = x0 + w / 2f, ftY = ky;
            Circle(c, cx, ftY, 1.8f * Tile, lc, top: false);
            // The arc around the hoop, and the corners straight down to the baseline.
            float hoopY = y0 + h - 4 - 1.2f * Tile;
            float arcR = Math.Min(w / 2f - 6, 6.6f * Tile);
            Circle(c, cx, hoopY, arcR, lc, top: false);
            int cornerX0 = (int)(cx - arcR), cornerX1 = (int)(cx + arcR);
            for (int y = (int)hoopY; y < y0 + h - 2; y++)
            {
                c.Set(cornerX0, y, lc);
                c.Set(cornerX1, y, lc);
            }
            // The centre circle on the half-court line at the bottom.
            Circle(c, cx, y0 + 2, 1.8f * Tile, lc, top: true);
            if (look.Logo)
            {
                // A painted half-disc logo inside the centre circle, with a ring.
                for (int y = 0; y < (int)(1.4f * Tile); y++)
                    for (int x = -(int)(1.4f * Tile); x <= (int)(1.4f * Tile); x++)
                        if (x * x + y * y <= (1.4f * Tile) * (1.4f * Tile))
                            c.Set((int)cx + x, y0 + 3 + y, (x + y) % 7 == 0 ? paint.Lighten(0.25f) : paint);
                Circle(c, cx, y0 + 3, 0.7f * Tile, lc, top: true);
            }
            if (look.Neon)
            {
                // Glow strips: a soft second line inside the boundary.
                var glow = lines.Lighten(0.15f);
                BlendRect(c, x0 + 4, y0 + 4, w - 8, h - 8, glow, 0.6f);
                BlendRect(c, x0 + 5, y0 + 5, w - 10, h - 10, glow, 0.28f);
            }
            DrawTheme(c, look.Theme, x0, y0, w, h, kx, ky, keyW, keyH);
        }

        /// <summary>One floor pixel in a court style (x, y inside the court).</summary>
        private static RgbColor FloorPixel(int style, RgbColor floor, RgbColor lines, int x, int y, float n)
        {
            switch ((FloorStyle)style)
            {
                case FloorStyle.Hardwood:
                {
                    int plank = y / 4;
                    bool seam = y % 4 == 0 || (x + plank * 11) % 32 == 0;
                    var wood = plank % 2 == 0 ? floor : floor.Darken(0.05f);
                    return seam ? floor.Darken(0.2f) : (n > 0.93f ? wood.Lighten(0.06f) : wood);
                }
                case FloorStyle.NeonGrid:
                {
                    var dark = floor.Darken(0.25f);
                    if (x % 16 == 0 || y % 16 == 0) return Blend(dark, lines, 0.35f);
                    if (x % 16 == 1 || y % 16 == 1) return Blend(dark, lines, 0.12f);
                    return n > 0.97f ? dark.Lighten(0.1f) : dark;
                }
                case FloorStyle.Tiles:
                {
                    bool alt = ((x / 8) + (y / 8)) % 2 == 0;
                    if (x % 8 == 0 || y % 8 == 0) return floor.Darken(0.18f);
                    return alt ? floor : floor.Lighten(0.07f);
                }
                case FloorStyle.Rubber:
                {
                    if (n > 0.93f) return floor.Lighten(0.22f);
                    if (n < 0.07f) return floor.Darken(0.18f);
                    return floor;
                }
                default:
                    return n > 0.9f ? floor.Lighten(0.06f) : n < 0.08f ? floor.Darken(0.08f) : floor;
            }
        }

        private static void BlendRect(PixelCanvas c, int x, int y, int w, int h, RgbColor col, float t)
        {
            void B(int px, int py)
            {
                if (px < 0 || py < 0 || px >= c.Width || py >= c.Height) return;
                c.Pixels[py * c.Width + px] = Blend(c.Pixels[py * c.Width + px], col, t);
            }
            for (int i = 0; i < w; i++) { B(x + i, y); B(x + i, y + h - 1); }
            for (int j = 1; j < h - 1; j++) { B(x, y + j); B(x + w - 1, y + j); }
        }

        private static RgbColor Blend(RgbColor a, RgbColor b, float t) =>
            new RgbColor((byte)(a.r + (b.r - a.r) * t), (byte)(a.g + (b.g - a.g) * t), (byte)(a.b + (b.b - a.b) * t), 255);

        /// <summary>Holiday dressing painted on a court: candy-cane lines, cobwebs, eggs, stars and stripes.</summary>
        private static void DrawTheme(PixelCanvas c, HolidayTheme theme, int x0, int y0, int w, int h, int kx, int ky, int keyW, int keyH)
        {
            switch (theme)
            {
                case HolidayTheme.Christmas:
                {
                    // Candy-cane boundary and snow drifts in the corners.
                    var red = RgbColor.FromHex("#C1121F");
                    for (int i = 0; i < w - 4; i++)
                        if ((i / 4) % 2 == 0) { c.Set(x0 + 2 + i, y0 + 2, red); c.Set(x0 + 2 + i, y0 + h - 3, red); }
                    for (int j = 0; j < h - 4; j++)
                        if ((j / 4) % 2 == 0) { c.Set(x0 + 2, y0 + 2 + j, red); c.Set(x0 + w - 3, y0 + 2 + j, red); }
                    var snow = RgbColor.FromHex("#F4F8FB");
                    foreach (var (cx, cy) in new[] { (x0 + 3, y0 + 3), (x0 + w - 4, y0 + 3), (x0 + 3, y0 + h - 4), (x0 + w - 4, y0 + h - 4) })
                        for (int y = -10; y <= 10; y++)
                            for (int x = -10; x <= 10; x++)
                                if (x * x + y * y < 90 && Noise(cx + x, cy + y, 5) > 0.25f) c.Set(cx + x, cy + y, snow);
                    // Snowflakes on the floor.
                    for (int y = 6; y < h - 6; y += 13)
                        for (int x = 6 + (y % 2) * 6; x < w - 6; x += 23)
                            Flake(c, x0 + x, y0 + y, RgbColor.FromHex("#DDEBF5"));
                    break;
                }
                case HolidayTheme.Halloween:
                {
                    // Cobwebs in the top corners and an orange moon in the key.
                    var web = RgbColor.FromHex("#D9D2E9");
                    Web(c, x0 + 3, y0 + h - 4, 1, -1, web);
                    Web(c, x0 + w - 4, y0 + h - 4, -1, -1, web);
                    c.FillCircle(kx + keyW / 2, ky + keyH / 2, 12, RgbColor.FromHex("#FF8C1A"));
                    c.FillCircle(kx + keyW / 2 + 4, ky + keyH / 2 + 3, 10, RgbColor.FromHex("#5A189A"));
                    for (int i = 0; i < 6; i++) Bat(c, x0 + 12 + i * (w - 24) / 5, y0 + 18 + (i % 2) * 10, RgbColor.FromHex("#10002B"));
                    break;
                }
                case HolidayTheme.Easter:
                {
                    // Painted eggs and pastel dots.
                    string[] pastel = { "#F4A6C0", "#BDE0FE", "#FFF1A8", "#B9FBC0", "#CDB4DB" };
                    int k = 0;
                    for (int y = 10; y < h - 10; y += 22)
                        for (int x = 10 + (y / 22 % 2) * 11; x < w - 10; x += 26)
                        {
                            if (x > kx - x0 - 6 && x < kx - x0 + keyW + 6 && y > ky - y0 - 6) continue;
                            EggShape(c, x0 + x, y0 + y, RgbColor.FromHex(pastel[k++ % pastel.Length]));
                        }
                    break;
                }
                case HolidayTheme.FourthOfJuly:
                {
                    // Stripes in the key, stars along the baseline.
                    var red = RgbColor.FromHex("#C1121F");
                    var white = RgbColor.FromHex("#F4F1DE");
                    for (int y = 1; y < keyH - 1; y++)
                        for (int x = 1; x < keyW - 1; x++)
                            c.Set(kx + x, ky + y, (y / 6) % 2 == 0 ? red : white);
                    for (int x = 10; x < w - 10; x += 18) Star(c, x0 + x, y0 + 10, RgbColor.FromHex("#F4F1DE"));
                    for (int x = 10; x < w - 10; x += 36) Star(c, x0 + x + 9, y0 + h / 2, RgbColor.FromHex("#4CC9F0"));
                    break;
                }
            }
        }

        private static void Flake(PixelCanvas c, int x, int y, RgbColor col)
        {
            c.Set(x, y, col);
            for (int i = 1; i <= 2; i++) { c.Set(x + i, y, col); c.Set(x - i, y, col); c.Set(x, y + i, col); c.Set(x, y - i, col); }
            c.Set(x + 1, y + 1, col); c.Set(x - 1, y - 1, col); c.Set(x + 1, y - 1, col); c.Set(x - 1, y + 1, col);
        }

        private static void Web(PixelCanvas c, int x, int y, int dx, int dy, RgbColor col)
        {
            for (int i = 0; i < 18; i++) { c.Set(x + dx * i, y, col); c.Set(x, y + dy * i, col); c.Set(x + dx * i, y + dy * i, col); }
            for (int r = 5; r <= 15; r += 5)
                for (int i = 0; i <= r; i++) c.Set(x + dx * i, y + dy * (r - i), col);
        }

        private static void Bat(PixelCanvas c, int x, int y, RgbColor col)
        {
            c.FillRect(x - 1, y, 3, 2, col);
            c.FillRect(x - 5, y + 1, 4, 1, col);
            c.FillRect(x + 2, y + 1, 4, 1, col);
            c.Set(x - 5, y + 2, col);
            c.Set(x + 5, y + 2, col);
        }

        private static void EggShape(PixelCanvas c, int x, int y, RgbColor col)
        {
            for (int j = -5; j <= 6; j++)
                for (int i = -4; i <= 4; i++)
                {
                    float ry = j < 0 ? 5f : 6.5f;
                    if (i * i / 16f + j * j / (ry * ry) <= 1f) c.Set(x + i, y + j, (j == 0 || j == 1) && (i + 8) % 3 != 0 ? col.Darken(0.25f) : col);
                }
        }

        private static void Star(PixelCanvas c, int x, int y, RgbColor col)
        {
            c.FillRect(x - 1, y - 3, 3, 7, col);
            c.FillRect(x - 3, y - 1, 7, 3, col);
            c.Set(x - 2, y - 3, col); c.Set(x + 2, y - 3, col);
        }

        private static void Rect(PixelCanvas c, int x, int y, int w, int h, RgbColor col)
        {
            for (int i = 0; i < w; i++) { c.Set(x + i, y, col); c.Set(x + i, y + h - 1, col); }
            for (int j = 0; j < h; j++) { c.Set(x, y + j, col); c.Set(x + w - 1, y + j, col); }
        }

        /// <summary>A circle outline, or only its top or bottom half.</summary>
        private static void Circle(PixelCanvas c, float cx, float cy, float r, RgbColor col, bool top)
        {
            int steps = (int)(r * 8);
            for (int i = 0; i <= steps; i++)
            {
                double a = Math.PI * i / steps;
                float x = cx + (float)Math.Cos(a) * r;
                float y = cy + (float)Math.Sin(a) * r * (top ? 1f : -1f);
                c.Set((int)Math.Round(x), (int)Math.Round(y), col);
            }
        }

        // ------------------------------------------------------------------ buildings

        private static readonly string[] Wall = { "#9B3D2E", "#7A5236", "#D8C7A3", "#8E9196", "#4A6FA5", "#E2B94B" };
        private static readonly string[] Mortar = { "#7A2E22", "#5C3D28", "#C2B08C", "#7A7D82", "#3C5C8A", "#C9A23D" };

        /// <summary>A building: a flat roof (with a water tower or AC units) over a front wall of windows, the door and a sign.</summary>
        public static PixelCanvas Building(CityBuilding b, bool lit)
        {
            int w = b.W * Tile, h = b.H * Tile;
            var c = new PixelCanvas(w, h);
            int style = Math.Max(0, Math.Min(Wall.Length - 1, b.Style));
            var wall = RgbColor.FromHex(Wall[style]);
            var mortar = RgbColor.FromHex(Mortar[style]);
            int roofH = Math.Min(44, (int)(h * 0.42f));
            int facadeH = h - roofH;

            // Front wall.
            for (int y = 0; y < facadeH; y++)
                for (int x = 0; x < w; x++)
                {
                    RgbColor col = wall;
                    if (style <= 1) // brick
                    {
                        int row = y / 4;
                        bool joint = y % 4 == 0 || (x + (row % 2) * 4) % 8 == 0;
                        col = joint ? mortar : (Noise(x, y, b.Seed) > 0.9f ? wall.Lighten(0.06f) : wall);
                    }
                    else if (style == 4) col = y % 4 == 0 ? mortar : wall; // siding
                    else col = Noise(x, y, b.Seed) > 0.93f ? wall.Darken(0.05f) : wall;
                    c.Set(x, y, col);
                }
            // A darker band at the foot of the wall, and the cornice.
            c.FillRect(0, 0, w, 2, wall.Darken(0.35f));
            c.FillRect(0, facadeH - 3, w, 3, wall.Lighten(0.2f));

            // Windows: one per tile, on each floor above the ground floor (the ground floor has the door and shopfront).
            var glass = lit ? RgbColor.FromHex("#FFD98A") : RgbColor.FromHex("#2B3A55");
            var glassHi = lit ? RgbColor.FromHex("#FFF1C4") : RgbColor.FromHex("#4F6A92");
            int doorCol = b.DoorX >= 0 ? b.DoorX - b.X : -99;
            int floors = Math.Max(1, (facadeH - 6) / 18);
            for (int f = 0; f < floors; f++)
            {
                int wy = 6 + f * 18;
                for (int col = 0; col < b.W; col++)
                {
                    int wx = col * Tile + 3;
                    if (f == 0 && Math.Abs(col - doorCol) <= 0) continue;
                    bool on = lit && Hash(col, f, b.Seed) % 3 != 0;
                    c.FillRect(wx - 1, wy - 1, 12, 13, wall.Darken(0.45f));
                    c.FillRect(wx, wy, 10, 11, on ? glass : RgbColor.FromHex("#2B3A55"));
                    c.FillRect(wx, wy + 8, 10, 3, on ? glassHi : RgbColor.FromHex("#4F6A92"));
                    c.FillRect(wx + 4, wy, 1, 11, wall.Darken(0.45f));
                    c.FillRect(wx - 1, wy - 2, 12, 1, wall.Lighten(0.25f)); // sill
                }
            }

            // The door, with a lit lamp at night.
            if (doorCol >= 0 && doorCol < b.W)
            {
                int dx = doorCol * Tile + 2;
                c.FillRect(dx - 1, 0, 14, 17, Outline);
                c.FillRect(dx, 0, 12, 16, RgbColor.FromHex("#5B3A22"));
                c.FillRect(dx + 1, 8, 10, 6, lit ? RgbColor.FromHex("#FFD98A") : RgbColor.FromHex("#3E5274"));
                c.FillRect(dx + 9, 4, 2, 2, RgbColor.FromHex("#E9C46A"));
            }

            // The sign over the ground floor.
            if (!string.IsNullOrEmpty(b.Sign))
            {
                string text = b.Sign.ToUpperInvariant();
                int tw = PixelFont.Measure(text);
                int sw = Math.Min(w - 4, tw + 6);
                int sx = (w - sw) / 2, sy = Math.Min(facadeH - 10, 24);
                c.FillRect(sx - 1, sy - 1, sw + 2, 10, Outline);
                c.FillRect(sx, sy, sw, 8, RgbColor.FromHex("#1A1A2E"));
                PixelFont.DrawCentered(c, text, w / 2, sy + 6, lit ? RgbColor.FromHex("#FFD166") : RgbColor.FromHex("#F2E8CF"));
            }

            // The roof (seen from above): a parapet, gravel, and something on top.
            var roof = RgbColor.FromHex("#5B5F6B");
            for (int y = facadeH; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool edge = x < 2 || x >= w - 2 || y >= h - 2 || y < facadeH + 2;
                    c.Set(x, y, edge ? RgbColor.FromHex("#7D828F") : (Noise(x, y, b.Seed + 3) > 0.85f ? roof.Lighten(0.06f) : roof));
                }
            uint pick = Hash(b.W, b.H, b.Seed);
            if (pick % 2 == 0 && w >= 48)
            {
                // A water tower.
                int tx = 8 + (int)(pick % (uint)Math.Max(1, w - 32));
                int ty = facadeH + 6;
                c.FillRect(tx + 2, ty, 2, 10, Outline);
                c.FillRect(tx + 14, ty, 2, 10, Outline);
                c.FillRect(tx, ty + 8, 18, 16, RgbColor.FromHex("#8B5A2B"));
                for (int i = 0; i < 18; i += 3) c.FillRect(tx + i, ty + 8, 1, 16, RgbColor.FromHex("#6E4520"));
                c.FillRect(tx - 1, ty + 23, 20, 3, RgbColor.FromHex("#4A2E1A"));
            }
            else
            {
                // AC units.
                for (int k = 0; k < Math.Max(1, b.W / 4); k++)
                {
                    int ax = 6 + k * 28 + (int)(Hash(k, 1, b.Seed) % 8);
                    if (ax + 14 > w - 4) break;
                    c.FillRect(ax, facadeH + 8, 14, 10, RgbColor.FromHex("#B8BCC6"));
                    c.FillRect(ax + 2, facadeH + 10, 10, 6, RgbColor.FromHex("#8E929C"));
                    c.FillRect(ax + 6, facadeH + 10, 2, 6, RgbColor.FromHex("#6B6F78"));
                }
            }
            return c;
        }

        // ------------------------------------------------------------------ props

        private static readonly string[] CarColors = { "#D62828", "#1D4ED8", "#F2F2F2", "#E9C46A", "#2EC4B6", "#7209B7" };

        /// <summary>
        /// A prop's art. <paramref name="pivotX"/> / <paramref name="pivotY"/> (art pixels) is where it touches the ground,
        /// measured from the canvas's bottom-left: put that point at the prop's tile position.
        /// </summary>
        public static PixelCanvas Prop(CityProp p, bool lit, out int pivotX, out int pivotY)
        {
            pivotX = 0;
            pivotY = 0;
            switch (p.Kind)
            {
                case CityPropKind.Tree: return Tree(p.Variant, out pivotX, out pivotY);
                case CityPropKind.Lamp:
                {
                    var c = new PixelCanvas(12, 46);
                    pivotX = 6 - Tile / 2;
                    c.FillRect(4, 0, 4, 3, RgbColor.FromHex("#3A3A48"));
                    c.FillRect(5, 3, 2, 36, RgbColor.FromHex("#4A4A5A"));
                    c.FillRect(6, 3, 1, 36, RgbColor.FromHex("#6A6A7E"));
                    c.FillRect(1, 38, 10, 4, RgbColor.FromHex("#3A3A48"));
                    c.FillRect(2, 36, 8, 3, lit ? RgbColor.FromHex("#FFE8A3") : RgbColor.FromHex("#C9C9B8"));
                    return c;
                }
                case CityPropKind.Bench:
                {
                    int w = p.W * Tile, h = Math.Max(12, p.H * Tile);
                    var c = new PixelCanvas(w, h);
                    bool vertical = p.H > p.W;
                    if (vertical)
                    {
                        c.FillRect(3, 0, 10, h - 2, RgbColor.FromHex("#8B5A2B"));
                        for (int y = 2; y < h - 2; y += 3) c.FillRect(3, y, 10, 1, RgbColor.FromHex("#6E4520"));
                        c.FillRect(2, 0, 1, h - 2, Outline);
                    }
                    else
                    {
                        c.FillRect(1, 0, 2, 5, Outline);
                        c.FillRect(w - 3, 0, 2, 5, Outline);
                        c.FillRect(0, 4, w, 4, RgbColor.FromHex("#8B5A2B"));
                        c.FillRect(0, 8, w, 3, RgbColor.FromHex("#A0693A"));
                        c.FillRect(0, 7, w, 1, RgbColor.FromHex("#6E4520"));
                    }
                    return c;
                }
                case CityPropKind.Hydrant:
                {
                    var c = new PixelCanvas(10, 14);
                    pivotX = 5 - Tile / 2;
                    c.FillRect(2, 0, 6, 10, RgbColor.FromHex("#D62828"));
                    c.FillRect(0, 5, 10, 3, RgbColor.FromHex("#B01E1E"));
                    c.FillRect(3, 10, 4, 3, RgbColor.FromHex("#E85D5D"));
                    c.FillRect(4, 13, 2, 1, RgbColor.FromHex("#B01E1E"));
                    return c;
                }
                case CityPropKind.Trash:
                {
                    var c = new PixelCanvas(12, 16);
                    pivotX = 6 - Tile / 2;
                    c.FillRect(1, 0, 10, 13, RgbColor.FromHex("#2E6B3A"));
                    for (int x = 2; x < 11; x += 3) c.FillRect(x, 1, 1, 11, RgbColor.FromHex("#24552E"));
                    c.FillRect(0, 13, 12, 3, RgbColor.FromHex("#3F8A4E"));
                    return c;
                }
                case CityPropKind.Car: return Car(p, out pivotX, out pivotY);
                case CityPropKind.Fence:
                {
                    bool side = p.Variant == 1;
                    var c = new PixelCanvas(Tile, side ? Tile + 4 : 20);
                    var post = RgbColor.FromHex("#6B6F78");
                    var mesh = RgbColor.FromHex("#9AA0A6");
                    if (side)
                    {
                        c.FillRect(7, 0, 2, Tile + 4, post);
                        for (int y = 0; y < Tile + 4; y += 2) c.Set(6 + (y / 2) % 3, y, mesh);
                    }
                    else
                    {
                        c.FillRect(0, 0, 2, 20, post);
                        c.FillRect(0, 18, Tile, 2, post);
                        for (int y = 1; y < 18; y++)
                            for (int x = 2; x < Tile; x++)
                                if ((x + y) % 4 == 0 || (x - y + 64) % 4 == 0) c.Set(x, y, mesh);
                    }
                    return c;
                }
                case CityPropKind.Hoop:
                {
                    var c = new PixelCanvas(28, 52);
                    pivotX = 14 - Tile / 2;
                    c.FillRect(12, 0, 4, 30, RgbColor.FromHex("#3A3A48"));
                    c.FillRect(13, 0, 1, 30, RgbColor.FromHex("#5A5A6E"));
                    c.FillRect(0, 30, 28, 18, Outline);
                    c.FillRect(1, 31, 26, 16, RgbColor.FromHex("#F2F2F2"));
                    Rect(c, 8, 33, 12, 9, RgbColor.FromHex("#D62828"));
                    // Rim and net hanging below the board.
                    c.FillRect(7, 27, 14, 2, RgbColor.FromHex("#F77F00"));
                    for (int y = 20; y < 27; y++)
                        for (int x = 8 + (27 - y) / 3; x < 20 - (27 - y) / 3; x++)
                            if ((x + y) % 2 == 0) c.Set(x, y, RgbColor.FromHex("#EDEDED"));
                    return c;
                }
                case CityPropKind.Bush:
                {
                    var c = new PixelCanvas(Tile, 12);
                    c.FillCircle(8, 5, 6, RgbColor.FromHex("#3F7A31"));
                    c.FillCircle(6, 7, 3, RgbColor.FromHex("#5E9E46"));
                    return c;
                }
                case CityPropKind.Bleachers:
                {
                    int w = p.W * Tile, h = p.H * Tile + 8;
                    var c = new PixelCanvas(w, h);
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            bool rail = p.W > p.H ? y % 6 == 0 : x % 6 == 0;
                            c.Set(x, y, rail ? RgbColor.FromHex("#6B6F78") : RgbColor.FromHex("#B8BCC6"));
                        }
                    return c;
                }
                case CityPropKind.Desk:
                {
                    int w = p.W * Tile;
                    var c = new PixelCanvas(w, 20);
                    c.FillRect(0, 0, w, 14, RgbColor.FromHex("#5B3A22"));
                    c.FillRect(0, 14, w, 6, RgbColor.FromHex("#D8C7A3"));
                    PixelFont.DrawCentered(c, p.Variant == 1 ? "CHECKOUT" : p.Variant == 2 ? "FRESH CUTS" : "FRONT DESK", w / 2, 10, RgbColor.FromHex("#FFD166"));
                    return c;
                }
                case CityPropKind.Rack:
                {
                    var c = new PixelCanvas(Tile, p.H * Tile + 6);
                    c.FillRect(1, 0, 14, c.Height, RgbColor.FromHex("#4A4A5A"));
                    for (int k = 0; k < p.H * 2; k++) c.FillCircle(8, 6 + k * 8, 3.5f, RgbColor.FromHex("#E07A2E"));
                    return c;
                }
                case CityPropKind.Cooler:
                {
                    var c = new PixelCanvas(12, 22);
                    pivotX = 6 - Tile / 2;
                    c.FillRect(1, 0, 10, 12, RgbColor.FromHex("#D9D9D9"));
                    c.FillRect(2, 12, 8, 9, RgbColor.FromHex("#4CC9F0"));
                    c.FillRect(3, 13, 2, 7, RgbColor.FromHex("#9EE3F7"));
                    return c;
                }
                case CityPropKind.Lockers:
                {
                    int h = p.H * Tile + 10;
                    var c = new PixelCanvas(Tile, h);
                    c.FillRect(0, 0, Tile, h, RgbColor.FromHex("#3C5C8A"));
                    for (int y = 0; y < h; y += 8) c.FillRect(0, y, Tile, 1, RgbColor.FromHex("#2B4468"));
                    return c;
                }
                case CityPropKind.Sign: // an overpass pillar
                {
                    var c = new PixelCanvas(14, 40);
                    pivotX = 7 - Tile / 2;
                    c.FillRect(1, 0, 12, 40, RgbColor.FromHex("#8E9196"));
                    c.FillRect(1, 0, 3, 40, RgbColor.FromHex("#7A7D82"));
                    PixelFont.Draw(c, "X", 6, 22, RgbColor.FromHex("#F72585"));
                    return c;
                }
                case CityPropKind.Cone:
                {
                    var c = new PixelCanvas(Tile, 12);
                    c.FillCircle(8, 3, 6, RgbColor.FromHex("#FFD166").WithAlpha(110));
                    c.FillRect(6, 1, 4, 3, RgbColor.FromHex("#F77F00"));
                    c.FillRect(7, 4, 2, 5, RgbColor.FromHex("#F77F00"));
                    c.FillRect(7, 6, 2, 1, RgbColor.FromHex("#F2F2F2"));
                    return c;
                }
                case CityPropKind.Mailbox:
                {
                    var c = new PixelCanvas(10, 16);
                    pivotX = 5 - Tile / 2;
                    c.FillRect(4, 0, 2, 8, RgbColor.FromHex("#3A3A48"));
                    c.FillRect(0, 8, 10, 7, RgbColor.FromHex("#1D4ED8"));
                    c.FillRect(0, 15, 10, 1, RgbColor.FromHex("#3C6AE8"));
                    return c;
                }
                case CityPropKind.Pumpkin: return Pumpkin(p.Variant, lit, out pivotX);
                case CityPropKind.PineLit: return PineLit(p.Variant, lit, out pivotX);
                case CityPropKind.Snowman: return Snowman(p.Variant, out pivotX);
                case CityPropKind.Egg: return BigEgg(p.Variant, out pivotX);
                case CityPropKind.Bunting: return Bunting(p, lit);
                case CityPropKind.Arcade: return Arcade(p.Variant, lit, out pivotX);
                case CityPropKind.LanternPost: return LanternPost(p.Variant, lit, out pivotX);
                case CityPropKind.Stall: return Stall(p, lit);
                case CityPropKind.Ramp: return Ramp(p);
                case CityPropKind.Shelf: return Shelf(p);
                case CityPropKind.BarberChair: return BarberChair(p.Variant, out pivotX);
                case CityPropKind.Mirror: return Mirror(p, lit);
                case CityPropKind.Palm: return Palm(p.Variant, out pivotX);
                case CityPropKind.Umbrella: return Umbrella(p.Variant, out pivotX);
                default: // Planter
                {
                    var c = new PixelCanvas(Tile, 18);
                    c.FillRect(2, 0, 12, 7, RgbColor.FromHex("#8B5A2B"));
                    c.FillCircle(8, 11, 6, RgbColor.FromHex("#3F7A31"));
                    c.FillCircle(6, 13, 3, RgbColor.FromHex("#5E9E46"));
                    return c;
                }
            }
        }

        // ------------------------------------------------------------------ 17 Oct 2026: court dressing and holidays

        private static readonly string[] Bright = { "#F72585", "#4CC9F0", "#FFD166", "#7CE577", "#FF8C42", "#C74DFF" };

        private static PixelCanvas Pumpkin(int variant, bool lit, out int pivotX)
        {
            var c = new PixelCanvas(16, 15);
            pivotX = 0;
            var body = RgbColor.FromHex(variant % 3 == 0 ? "#F77F00" : "#FF8C1A");
            c.FillCircle(8, 6, 6.5f, body.Darken(0.2f));
            c.FillCircle(5, 6, 4, body);
            c.FillCircle(11, 6, 4, body);
            c.FillCircle(8, 6, 4, body.Lighten(0.08f));
            c.FillRect(7, 11, 2, 3, RgbColor.FromHex("#3E6B2A"));
            // Carved face (glows at night).
            var face = lit ? RgbColor.FromHex("#FFE066") : RgbColor.FromHex("#3B1F00");
            c.Set(5, 7, face); c.Set(6, 7, face); c.Set(10, 7, face); c.Set(11, 7, face);
            c.FillRect(5, 3, 7, 1, face);
            c.Set(6, 4, face); c.Set(8, 4, face); c.Set(10, 4, face);
            return c;
        }

        private static PixelCanvas PineLit(int variant, bool lit, out int pivotX)
        {
            var c = new PixelCanvas(26, 44);
            pivotX = 13 - Tile / 2;
            c.FillRect(11, 0, 4, 6, RgbColor.FromHex("#5B3A22"));
            var green = RgbColor.FromHex("#1F6B3A");
            var light = RgbColor.FromHex("#2E8B4A");
            for (int tier = 0; tier < 3; tier++)
            {
                int baseY = 5 + tier * 10, half = 12 - tier * 3;
                for (int y = 0; y < 14; y++)
                {
                    int span = half - y * half / 14;
                    for (int x = -span; x <= span; x++) c.Set(13 + x, baseY + y, x < -span / 3 ? light : green);
                }
            }
            // Snow on the tips, lights and a star.
            for (int tier = 0; tier < 3; tier++) c.FillRect(13 - (12 - tier * 3), 5 + tier * 10, 2 * (12 - tier * 3) + 1, 1, RgbColor.FromHex("#F4F8FB"));
            uint h = (uint)(variant * 2654435761u);
            for (int i = 0; i < 12; i++)
            {
                int y = 7 + i * 2 + (int)(h >> i) % 2;
                int span = Math.Max(1, 10 - y / 4);
                int x = 13 + (int)((h >> (i * 2)) % (uint)(2 * span)) - span;
                var bulb = RgbColor.FromHex(Bright[(i + variant) % Bright.Length]);
                c.Set(x, y, lit ? bulb.Lighten(0.2f) : bulb.Darken(0.25f));
            }
            var star = lit ? RgbColor.FromHex("#FFE066") : RgbColor.FromHex("#E9C46A");
            c.FillRect(12, 38, 3, 5, star);
            c.FillRect(10, 40, 7, 1, star);
            return c;
        }

        private static PixelCanvas Snowman(int variant, out int pivotX)
        {
            var c = new PixelCanvas(18, 30);
            pivotX = 9 - Tile / 2;
            var snow = RgbColor.FromHex("#F4F8FB");
            var shade = RgbColor.FromHex("#C9DCEB");
            c.FillCircle(9, 6, 6.5f, shade);
            c.FillCircle(8, 7, 5.5f, snow);
            c.FillCircle(9, 15, 4.8f, shade);
            c.FillCircle(8, 16, 4f, snow);
            c.FillCircle(9, 22, 3.6f, snow);
            c.Set(8, 23, Outline); c.Set(10, 23, Outline);
            c.FillRect(10, 22, 3, 1, RgbColor.FromHex("#F77F00"));
            var scarf = RgbColor.FromHex(variant % 2 == 0 ? "#C1121F" : "#1D4ED8");
            c.FillRect(5, 19, 8, 2, scarf);
            c.FillRect(11, 16, 2, 3, scarf);
            c.FillRect(6, 25, 7, 1, Outline);
            c.FillRect(7, 26, 5, 4, Outline);
            c.Set(9, 15, Outline); c.Set(9, 12, Outline); c.Set(9, 9, Outline);
            return c;
        }

        private static PixelCanvas BigEgg(int variant, out int pivotX)
        {
            var c = new PixelCanvas(12, 15);
            pivotX = 0;
            string[] pastel = { "#F4A6C0", "#BDE0FE", "#FFF1A8", "#B9FBC0", "#CDB4DB" };
            var col = RgbColor.FromHex(pastel[Math.Abs(variant) % pastel.Length]);
            var band = RgbColor.FromHex(pastel[(Math.Abs(variant) + 2) % pastel.Length]).Darken(0.15f);
            for (int y = 0; y < 15; y++)
                for (int x = 0; x < 12; x++)
                {
                    float dx = (x + 0.5f - 6f) / 5.5f, dy = (y + 0.5f - 6f) / (y < 6 ? 6f : 8.5f);
                    if (dx * dx + dy * dy > 1f) continue;
                    bool zig = y == 6 + ((x / 2) % 2) || y == 7 + ((x / 2) % 2);
                    c.Set(x, y, zig ? band : (x < 4 && y > 6 ? col.Lighten(0.2f) : col));
                }
            return c;
        }

        private static PixelCanvas Bunting(CityProp p, bool lit)
        {
            int w = Math.Max(2, p.W) * Tile;
            var c = new PixelCanvas(w, 34);
            var pole = RgbColor.FromHex("#6B6F78");
            c.FillRect(1, 0, 2, 34, pole);
            c.FillRect(w - 3, 0, 2, 34, pole);
            string[] cols = { "#C1121F", "#F4F1DE", "#1D4ED8" };
            for (int x = 3; x < w - 3; x++)
            {
                float t = (x - 3f) / (w - 6f);
                int y = 31 - (int)(Math.Sin(t * Math.PI) * 5);
                c.Set(x, y, Outline);
                int k = (x - 3) / 8;
                int inFlag = (x - 3) % 8;
                if (inFlag < 7)
                {
                    int depth = 6 - Math.Abs(inFlag - 3) * 2;
                    var flag = RgbColor.FromHex(cols[k % 3]);
                    for (int d = 1; d <= depth; d++) c.Set(x, y - d, lit ? flag.Lighten(0.1f) : flag);
                }
            }
            return c;
        }

        private static PixelCanvas Arcade(int variant, bool lit, out int pivotX)
        {
            var c = new PixelCanvas(16, 30);
            pivotX = 0;
            var body = RgbColor.FromHex(Bright[Math.Abs(variant) % Bright.Length]).Darken(0.35f);
            c.FillRect(1, 0, 14, 27, body);
            c.FillRect(1, 0, 2, 27, body.Darken(0.25f));
            c.FillRect(1, 27, 14, 3, RgbColor.FromHex(Bright[(Math.Abs(variant) + 1) % Bright.Length]));
            var screen = lit ? RgbColor.FromHex(Bright[(Math.Abs(variant) + 2) % Bright.Length]).Lighten(0.3f) : RgbColor.FromHex("#1B263B");
            c.FillRect(3, 15, 10, 9, Outline);
            c.FillRect(4, 16, 8, 7, screen);
            c.FillRect(2, 10, 12, 3, RgbColor.FromHex("#2B2D42"));
            c.Set(5, 11, RgbColor.FromHex("#D62828"));
            c.Set(10, 11, RgbColor.FromHex("#FFD166"));
            c.Set(11, 12, RgbColor.FromHex("#4CC9F0"));
            return c;
        }

        private static PixelCanvas LanternPost(int variant, bool lit, out int pivotX)
        {
            var c = new PixelCanvas(14, 42);
            pivotX = 7 - Tile / 2;
            c.FillRect(6, 0, 2, 38, RgbColor.FromHex("#4E3A28"));
            c.FillRect(2, 37, 10, 1, RgbColor.FromHex("#4E3A28"));
            var paper = RgbColor.FromHex(variant % 3 == 0 ? "#F59E0B" : "#D62828");
            var glow = lit ? paper.Lighten(0.35f) : paper;
            foreach (int x in new[] { 3, 10 })
            {
                c.FillCircle(x, 32, 3f, glow);
                c.FillRect(x - 2, 28, 5, 1, Outline);
                c.FillRect(x - 2, 35, 5, 1, Outline);
                c.Set(x, 27, RgbColor.FromHex("#FFD166"));
            }
            return c;
        }

        private static PixelCanvas Stall(CityProp p, bool lit)
        {
            int w = Math.Max(2, p.W) * Tile;
            var c = new PixelCanvas(w, 34);
            var wood = RgbColor.FromHex("#8B5A2B");
            c.FillRect(1, 0, 2, 26, wood);
            c.FillRect(w - 3, 0, 2, 26, wood);
            c.FillRect(0, 6, w, 8, wood.Darken(0.15f));
            c.FillRect(0, 13, w, 2, wood.Lighten(0.15f));
            // Goods on the counter.
            for (int x = 4; x < w - 4; x += 5) c.FillCircle(x, 17, 2, RgbColor.FromHex(Bright[(x / 5 + p.Variant) % Bright.Length]));
            // Striped awning.
            var a = RgbColor.FromHex(Bright[Math.Abs(p.Variant) % Bright.Length]);
            var b = RgbColor.FromHex("#F4F1DE");
            for (int y = 24; y < 33; y++)
                for (int x = 0; x < w; x++)
                    c.Set(x, y, (x / 4) % 2 == 0 ? a : b);
            for (int x = 0; x < w; x += 4) c.FillRect(x, 22, 2, 2, (x / 4) % 2 == 0 ? a : b);
            if (lit) c.FillRect(2, 21, w - 4, 1, RgbColor.FromHex("#FFE8A3"));
            return c;
        }

        private static PixelCanvas Ramp(CityProp p)
        {
            int w = Math.Max(1, p.W) * Tile;
            var c = new PixelCanvas(w, 16);
            bool left = p.Variant == 0;
            var face = RgbColor.FromHex("#9AA0A6");
            var dark = RgbColor.FromHex("#6B6F78");
            for (int x = 0; x < w; x++)
            {
                int t = left ? x : w - 1 - x;
                int top = 2 + (int)(12 * Math.Pow(t / (float)(w - 1), 2));
                for (int y = 0; y < top; y++) c.Set(x, y, y == top - 1 ? RgbColor.FromHex("#D9D9D9") : (y % 4 == 0 ? dark : face));
            }
            c.FillRect(left ? w - 2 : 0, 0, 2, 15, RgbColor.FromHex("#3A3A48")); // coping
            return c;
        }

        private static PixelCanvas Shelf(CityProp p)
        {
            int w = Math.Max(1, p.W) * Tile;
            var c = new PixelCanvas(w, 30);
            var wood = RgbColor.FromHex("#5B3A22");
            c.FillRect(0, 0, w, 30, wood.Darken(0.2f));
            for (int row = 0; row < 3; row++)
            {
                int y = 2 + row * 9;
                c.FillRect(1, y, w - 2, 1, wood.Lighten(0.2f));
                for (int x = 2; x < w - 3; x += 4)
                {
                    // Shoe boxes, jerseys and balls.
                    var col = RgbColor.FromHex(Bright[(x / 4 + row * 2 + p.Variant) % Bright.Length]);
                    if ((x / 4 + row) % 3 == 0) c.FillCircle(x + 1.5f, y + 3.5f, 2.5f, RgbColor.FromHex("#E07A2E"));
                    else c.FillRect(x, y + 1, 3, row == 1 ? 6 : 4, col);
                }
            }
            return c;
        }

        private static PixelCanvas BarberChair(int variant, out int pivotX)
        {
            var c = new PixelCanvas(16, 24);
            pivotX = 0;
            var seat = RgbColor.FromHex(variant % 2 == 0 ? "#C1121F" : "#1D4ED8");
            c.FillRect(6, 0, 4, 6, RgbColor.FromHex("#B8BCC6"));
            c.FillRect(3, 0, 10, 2, RgbColor.FromHex("#8E9196"));
            c.FillRect(2, 6, 12, 5, seat);
            c.FillRect(3, 11, 10, 11, seat.Darken(0.1f));
            c.FillRect(5, 22, 6, 2, seat.Lighten(0.15f));
            c.FillRect(1, 9, 2, 3, RgbColor.FromHex("#B8BCC6"));
            c.FillRect(13, 9, 2, 3, RgbColor.FromHex("#B8BCC6"));
            return c;
        }

        private static PixelCanvas Mirror(CityProp p, bool lit)
        {
            int w = Math.Max(1, p.W) * Tile;
            var c = new PixelCanvas(w, 26);
            c.FillRect(0, 0, w, 26, RgbColor.FromHex("#3C3C4E"));
            c.FillRect(2, 6, w - 4, 18, RgbColor.FromHex("#9EC7DB"));
            for (int i = 0; i < 6; i++) c.Set(4 + i, 20 - i, RgbColor.FromHex("#E4F3FA"));
            for (int x = 3; x < w - 3; x += 6) c.FillCircle(x, 3, 1.5f, lit ? RgbColor.FromHex("#FFE8A3") : RgbColor.FromHex("#C9C9B8"));
            return c;
        }

        private static PixelCanvas Palm(int variant, out int pivotX)
        {
            var c = new PixelCanvas(30, 46);
            pivotX = 15 - Tile / 2;
            for (int y = 0; y < 32; y++)
            {
                int x = 14 + (int)(Math.Sin(y / 10.0) * 2);
                c.FillRect(x, y, 3, 1, (y / 3) % 2 == 0 ? RgbColor.FromHex("#8B5A2B") : RgbColor.FromHex("#6E4520"));
            }
            var leaf = RgbColor.FromHex(variant % 2 == 0 ? "#2E8B4A" : "#3F9E52");
            foreach (var (dx, dy) in new[] { (-1, 0), (1, 0), (-1, 1), (1, 1), (0, 1), (-1, -1), (1, -1) })
                for (int i = 0; i < 12; i++)
                    c.FillRect(15 + dx * i, 33 + dy * i / 2 - (i * i) / 18, 2, 2, leaf);
            c.FillCircle(15, 32, 2.5f, RgbColor.FromHex("#6E4520"));
            return c;
        }

        private static PixelCanvas Umbrella(int variant, out int pivotX)
        {
            var c = new PixelCanvas(28, 30);
            pivotX = 14 - Tile / 2;
            c.FillRect(13, 0, 2, 22, RgbColor.FromHex("#F4F1DE"));
            var a = RgbColor.FromHex(Bright[Math.Abs(variant) % Bright.Length]);
            for (int y = 0; y < 8; y++)
                for (int x = -13 + y; x <= 13 - y; x++)
                    c.Set(14 + x, 20 + y, ((x + 14) / 4) % 2 == 0 ? a : RgbColor.FromHex("#F4F1DE"));
            return c;
        }

        private static PixelCanvas Tree(int variant, out int pivotX, out int pivotY)
        {
            var c = new PixelCanvas(32, 44);
            pivotX = 16 - Tile / 2;
            pivotY = 0;
            c.FillRect(14, 0, 4, 14, RgbColor.FromHex("#5B3A22"));
            c.FillRect(14, 0, 1, 14, RgbColor.FromHex("#3E2716"));
            var dark = variant % 2 == 0 ? RgbColor.FromHex("#2F6B2A") : RgbColor.FromHex("#3B6E2E");
            var mid = variant % 2 == 0 ? RgbColor.FromHex("#3F8A36") : RgbColor.FromHex("#4C8A3A");
            var light = RgbColor.FromHex("#6DB34F");
            c.FillCircle(16, 24, 13, dark);
            c.FillCircle(11, 28, 8, mid);
            c.FillCircle(21, 29, 8, mid);
            c.FillCircle(16, 34, 8, mid);
            c.FillCircle(13, 34, 3, light);
            c.FillCircle(19, 31, 2, light);
            return c;
        }

        private static PixelCanvas Car(CityProp p, out int pivotX, out int pivotY)
        {
            int w = p.W * Tile;
            var c = new PixelCanvas(w, 24);
            pivotX = 0;
            pivotY = 0;
            var body = RgbColor.FromHex(CarColors[Math.Abs(p.Variant) % CarColors.Length]);
            // Wheels.
            c.FillRect(4, 0, 6, 5, Outline);
            c.FillRect(w - 10, 0, 6, 5, Outline);
            // Body, cabin and windows.
            c.FillRect(1, 3, w - 2, 9, body);
            c.FillRect(1, 11, w - 2, 2, body.Lighten(0.2f));
            c.FillRect(6, 12, w - 12, 9, body.Darken(0.1f));
            c.FillRect(8, 14, w - 16, 5, RgbColor.FromHex("#2B3A55"));
            c.FillRect(8, 18, w - 16, 1, RgbColor.FromHex("#4F6A92"));
            c.FillRect(w - 3, 6, 2, 3, RgbColor.FromHex("#FFE8A3"));
            c.FillRect(1, 6, 2, 3, RgbColor.FromHex("#D62828"));
            return c;
        }

        /// <summary>A soft round glow (for streetlights at night), white with alpha falling off.</summary>
        public static PixelCanvas Glow(int radius)
        {
            var c = new PixelCanvas(radius * 2, radius * 2);
            for (int y = 0; y < c.Height; y++)
                for (int x = 0; x < c.Width; x++)
                {
                    float dx = (x + 0.5f - radius) / radius, dy = (y + 0.5f - radius) / radius;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (d >= 1f) continue;
                    // Banded falloff for the pixel look.
                    float a = (float)Math.Floor((1f - d) * 4f) / 4f;
                    c.Pixels[y * c.Width + x] = new RgbColor(255, 255, 255, (byte)(a * 255));
                }
            return c;
        }
    }
}
