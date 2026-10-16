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
            if (!indoor)
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float n = Noise(x0 + x, y0 + y, 99);
                        c.Pixels[(y0 + y) * c.Width + x0 + x] = n > 0.9f ? floor.Lighten(0.06f) : n < 0.08f ? floor.Darken(0.08f) : floor;
                    }
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
                    PixelFont.DrawCentered(c, "FRONT DESK", w / 2, 10, RgbColor.FromHex("#FFD166"));
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
