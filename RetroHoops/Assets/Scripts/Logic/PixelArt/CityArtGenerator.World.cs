using System;

namespace CallerRetroBall.Logic.PixelArt
{
    /// <summary>
    /// 28 Oct 2026 (Omari, BALLER WORLD W7–W10): the regions' look, generated like the rest of the city. New York
    /// brownstones (stoops, fire escapes, water towers) and glass towers; Los Angeles mission stucco under red tile
    /// roofs and pastel bungalows; Las Vegas neon towers with bulb-lit marquees and a desert motel; the Bay's painted
    /// Victorians and Oakland-style warehouses with murals. Plus their streets: brick pavers, cracked desert, cable-car
    /// rails, subway stairs, cabs, food carts, cactus, neon signs, the lifeguard tower, the taco truck, fountains, the
    /// cable car, the red bridge, the port cranes, murals, the Big Run poster and YOUR RIDE.
    /// </summary>
    public static partial class CityArtGenerator
    {
        private static RgbColor H(string hex) => RgbColor.FromHex(hex);

        // ------------------------------------------------------------------ ground

        private static RgbColor WorldGround(CityMap map, CityGround g, int tx, int ty, int x, int y, int px, int py, float n)
        {
            switch (g)
            {
                case CityGround.Brick:
                {
                    // Herringbone-ish pavers: rows of 8×4 bricks, offset every other row.
                    int row = py / 4;
                    bool joint = py % 4 == 0 || (px + (row % 2) * 4) % 8 == 0;
                    if (joint) return H("#8A5A44");
                    var brick = (Hash(px / 8, row, 21) % 3) switch { 0 => H("#B5654A"), 1 => H("#A85A40"), _ => H("#BE6E52") };
                    return n > 0.93f ? brick.Lighten(0.08f) : brick;
                }
                case CityGround.Desert:
                {
                    // Pale sand with dry cracks and pebbles.
                    bool crack = Hash(px / 3, py / 3, 7) % 37 == 0 || (Hash(px, py / 2, 9) % 211 == 0);
                    if (crack) return H("#B08A5A");
                    if (n > 0.97f) return H("#8C7B66"); // a pebble
                    return n > 0.7f ? H("#E3C48E") : n < 0.2f ? H("#D2B07A") : H("#DCBB84");
                }
                default: // Rail: road with two steel rails and a cable slot between them
                {
                    var asphalt = n > 0.9f ? H("#45454E") : n < 0.1f ? H("#303038") : H("#3A3A42");
                    bool vertical = map.Get(tx, ty - 1) == CityGround.Rail || map.Get(tx, ty + 1) == CityGround.Rail;
                    int across = vertical ? x : y;
                    int along = vertical ? py : px;
                    if (across == 3 || across == 12) return H("#B8BCC6");
                    if (across == 4 || across == 11) return H("#6B6F78");
                    if (across == 7 || across == 8) return along % 6 == 0 ? H("#1E1E24") : H("#2A2A31");
                    if (along % 8 == 0 && across > 1 && across < 14) return H("#5C4033"); // ties
                    return asphalt;
                }
            }
        }

        // ------------------------------------------------------------------ buildings

        private static PixelCanvas WorldBuilding(CityBuilding b, bool lit)
        {
            int w = b.W * Tile, h = b.H * Tile;
            var c = new PixelCanvas(w, h);
            switch (b.Style)
            {
                case CityDistricts.Brownstone: Brownstone(c, b, lit, w, h); break;
                case CityDistricts.GlassTower: GlassTower(c, b, lit, w, h); break;
                case CityDistricts.Mission: Mission(c, b, lit, w, h); break;
                case CityDistricts.Bungalow: Bungalow(c, b, lit, w, h); break;
                case CityDistricts.NeonTower: NeonTower(c, b, lit, w, h); break;
                case CityDistricts.Motel: MotelBlock(c, b, lit, w, h); break;
                case CityDistricts.Victorian: Victorian(c, b, lit, w, h); break;
                default: Warehouse(c, b, lit, w, h); break;
            }
            return c;
        }

        /// <summary>A window: frame, glass (warm when lit), a highlight.</summary>
        private static void Window(PixelCanvas c, int x, int y, int ww, int wh, RgbColor frame, bool on)
        {
            c.FillRect(x - 1, y - 1, ww + 2, wh + 2, frame);
            c.FillRect(x, y, ww, wh, on ? H("#FFD98A") : H("#2B3A55"));
            c.FillRect(x, y + wh - 2, ww, 2, on ? H("#FFF1C4") : H("#4F6A92"));
        }

        private static void SignBoard(PixelCanvas c, CityBuilding b, int w, int y, bool lit, string bg = "#1A1A2E", string fg = "#FFD166")
        {
            if (string.IsNullOrEmpty(b.Sign)) return;
            string text = b.Sign.ToUpperInvariant();
            int sw = Math.Min(w - 4, PixelFont.Measure(text) + 6);
            int sx = (w - sw) / 2;
            c.FillRect(sx - 1, y - 1, sw + 2, 10, Outline);
            c.FillRect(sx, y, sw, 8, H(bg));
            PixelFont.DrawCentered(c, text, w / 2, y + 6, lit ? H(fg) : H("#F2E8CF"));
        }

        private static void Door(PixelCanvas c, CityBuilding b, int col, RgbColor wood, bool lit)
        {
            if (col < 0 || col >= b.W) return;
            int dx = col * Tile + 2;
            c.FillRect(dx - 1, 0, 14, 17, Outline);
            c.FillRect(dx, 0, 12, 16, wood);
            c.FillRect(dx + 1, 9, 10, 5, lit ? H("#FFD98A") : H("#3E5274"));
        }

        private static void FlatRoof(PixelCanvas c, int w, int h, int from, string roofHex, uint seed)
        {
            var roof = H(roofHex);
            for (int y = from; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool edge = x < 2 || x >= w - 2 || y >= h - 2 || y < from + 2;
                    c.Set(x, y, edge ? roof.Lighten(0.18f) : (Noise(x, y, seed) > 0.86f ? roof.Lighten(0.06f) : roof));
                }
        }

        private static void Brownstone(PixelCanvas c, CityBuilding b, bool lit, int w, int h)
        {
            int roofH = Math.Min(30, h / 3), facadeH = h - roofH;
            var stone = H("#6B4226");
            for (int y = 0; y < facadeH; y++)
                for (int x = 0; x < w; x++)
                {
                    bool course = y % 6 == 0;
                    c.Set(x, y, course ? stone.Darken(0.18f) : Noise(x, y, b.Seed) > 0.9f ? stone.Lighten(0.07f) : stone);
                }
            // Party walls between houses.
            for (int x = 0; x < w; x += 6 * Tile) c.FillRect(x, 0, 1, facadeH, stone.Darken(0.4f));
            // Tall windows with white lintels.
            int floors = Math.Max(1, (facadeH - 22) / 20);
            for (int f = 0; f < floors; f++)
                for (int col = 0; col < b.W; col++)
                {
                    int wx = col * Tile + 4, wy = 22 + f * 20;
                    if (wy + 16 > facadeH - 4) break;
                    Window(c, wx, wy, 8, 14, stone.Darken(0.5f), lit && Hash(col, f, b.Seed) % 3 != 0);
                    c.FillRect(wx - 2, wy + 15, 12, 2, H("#E9E2D0"));
                }
            // Black fire escapes: a platform under each upper floor and a ladder zigzag.
            var iron = H("#16161C");
            int fx = w / 2 - 14;
            for (int f = 1; f < floors; f++)
            {
                int py = 22 + f * 20 - 3;
                c.FillRect(fx, py, 28, 2, iron);
                for (int x = fx; x < fx + 28; x += 3) c.FillRect(x, py + 2, 1, 5, iron);
                for (int k = 0; k < 18; k++) c.Set(fx + 4 + k, py - 18 + k, iron);
            }
            // The stoop: stone steps up to each door with railings.
            for (int i = 0; i < b.W / 6 + 1; i++)
            {
                int col = b.X % 6 + i * 6 + 2;
                if (col >= b.W) break;
                int sx = col * Tile;
                for (int s = 0; s < 4; s++) c.FillRect(sx - 4 + s * 2, s * 3, 20 - s * 4, 3, H("#9C7A5B").Darken(s * 0.06f));
                c.FillRect(sx - 5, 0, 1, 14, iron);
                c.FillRect(sx + 16, 0, 1, 14, iron);
                Door(c, b, col, H("#3B2416"), lit);
            }
            SignBoard(c, b, w, 4, lit, "#2B2D42", "#7CE577");
            // Cornice.
            c.FillRect(0, facadeH - 4, w, 4, stone.Darken(0.3f));
            for (int x = 0; x < w; x += 4) c.FillRect(x, facadeH - 6, 2, 2, stone.Darken(0.3f));
            FlatRoof(c, w, h, facadeH, "#4A4A52", b.Seed);
            if (w >= 48 && Hash(b.W, 3, b.Seed) % 2 == 0)
            {
                int tx = 10 + (int)(Hash(b.W, 5, b.Seed) % (uint)Math.Max(1, w - 34)), ty = facadeH + 4;
                c.FillRect(tx + 2, ty, 2, 8, Outline);
                c.FillRect(tx + 13, ty, 2, 8, Outline);
                c.FillRect(tx, ty + 6, 17, 14, H("#8B5A2B"));
                for (int i = 0; i < 17; i += 3) c.FillRect(tx + i, ty + 6, 1, 14, H("#6E4520"));
                for (int i = 0; i < 9; i++) c.FillRect(tx - 1 + i, ty + 20 + i / 2, 19 - i * 2, 1, H("#4A2E1A"));
            }
        }

        private static void GlassTower(PixelCanvas c, CityBuilding b, bool lit, int w, int h)
        {
            int roofH = Math.Min(26, h / 5), facadeH = h - roofH;
            var glass = H(Hash(b.W, b.H, b.Seed) % 2 == 0 ? "#2E6F8E" : "#3D5A80");
            var mullion = H("#1B2A3A");
            for (int y = 0; y < facadeH; y++)
                for (int x = 0; x < w; x++)
                {
                    bool grid = x % 8 == 0 || y % 10 == 0;
                    bool streak = ((x + y * 2 + (int)(b.Seed % 50)) % 60) < 6; // a sky reflection sweeping across
                    var col = grid ? mullion : streak ? glass.Lighten(0.35f) : glass;
                    if (lit && !grid && Hash(x / 8, y / 10, b.Seed) % 4 == 0) col = H("#FFE39F");
                    c.Set(x, y, col);
                }
            // A lobby of tall glass and a steel canopy.
            c.FillRect(0, 0, w, 14, H("#9DB4C0"));
            for (int x = 0; x < w; x += 10) c.FillRect(x, 0, 2, 14, H("#4A5560"));
            c.FillRect(0, 14, w, 3, H("#C0C6CC"));
            FlatRoof(c, w, h, facadeH, "#6C757D", b.Seed);
            // Penthouse, antenna and a blinking beacon at night.
            int px = w / 2 - 12;
            c.FillRect(px, facadeH + 5, 24, 12, H("#ADB5BD"));
            c.FillRect(px + 11, facadeH + 17, 2, Math.Max(2, roofH - 19), H("#495057"));
            c.FillRect(px + 10, h - 3, 4, 3, lit ? H("#FF3B30") : H("#8D1C17"));
        }

        private static void Mission(PixelCanvas c, CityBuilding b, bool lit, int w, int h)
        {
            int roofH = Math.Min(34, (int)(h * 0.42f)), facadeH = h - roofH;
            string[] walls = { "#F1E3C8", "#EAD7B7", "#F4EBDD", "#E9C9A3" };
            var wall = H(walls[b.Seed % (uint)walls.Length]);
            for (int y = 0; y < facadeH; y++)
                for (int x = 0; x < w; x++) c.Set(x, y, Noise(x, y, b.Seed) > 0.92f ? wall.Darken(0.06f) : wall);
            // Arched windows with dark wood.
            var wood = H("#5C3D2E");
            for (int col = 0; col < b.W; col++)
            {
                if (col == b.DoorX - b.X) continue;
                int wx = col * Tile + 3, wy = 10;
                bool on = lit && Hash(col, 7, b.Seed) % 3 != 0;
                c.FillRect(wx - 1, wy - 1, 12, 15, wood);
                c.FillRect(wx, wy, 10, 12, on ? H("#FFD98A") : H("#2B3A55"));
                c.FillCircle(wx + 5, wy + 12, 5.5f, wood);
                c.FillCircle(wx + 5, wy + 12, 4.5f, on ? H("#FFE7B0") : H("#3E5274"));
                c.FillRect(wx - 1, wy - 3, 12, 2, wall.Darken(0.2f));
            }
            Door(c, b, b.DoorX - b.X, wood, lit);
            SignBoard(c, b, w, Math.Min(facadeH - 10, 28), lit, "#7C2D12", "#FFD166");
            // Red tile roof seen from above: rows of rounded tiles.
            for (int y = facadeH; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int row = (y - facadeH) / 4;
                    int t = (x + (row % 2) * 4) % 8;
                    var tile = row % 2 == 0 ? H("#C1440E") : H("#B23A0B");
                    c.Set(x, y, t == 0 ? tile.Darken(0.35f) : (y - facadeH) % 4 == 3 ? tile.Lighten(0.18f) : tile);
                }
            c.FillRect(0, facadeH, w, 2, H("#7A2A08"));
        }

        private static void Bungalow(PixelCanvas c, CityBuilding b, bool lit, int w, int h)
        {
            int roofH = Math.Min(28, (int)(h * 0.38f)), facadeH = h - roofH;
            string[] pastels = { "#F7C6C7", "#BDE0C9", "#FFD8B1", "#CDE7F0", "#F5E1A4" };
            var wall = H(pastels[b.Seed % (uint)pastels.Length]);
            for (int y = 0; y < facadeH; y++)
                for (int x = 0; x < w; x++) c.Set(x, y, y % 5 == 0 ? wall.Darken(0.08f) : wall);
            // A striped awning over the front and wide windows.
            string[] stripes = { "#E63946", "#2A9D8F", "#F4A261", "#457B9D" };
            var stripe = H(stripes[(b.Seed / 3) % (uint)stripes.Length]);
            for (int x = 0; x < w; x++) c.FillRect(x, facadeH - 12, 1, 6, (x / 6) % 2 == 0 ? stripe : H("#FFFFFF"));
            for (int col = 0; col < b.W; col += 2)
            {
                if (Math.Abs(col - (b.DoorX - b.X)) < 1) continue;
                Window(c, col * Tile + 4, 12, 22, 12, H("#FFFFFF"), lit && Hash(col, 3, b.Seed) % 2 == 0);
            }
            Door(c, b, b.DoorX - b.X, H("#2E4057"), lit);
            SignBoard(c, b, w, Math.Min(facadeH - 22, 30), lit, "#264653", "#E9C46A");
            FlatRoof(c, w, h, facadeH, "#D9D5CC", b.Seed);
            // A rooftop palm shadow and a swamp cooler.
            c.FillRect(w - 22, facadeH + 6, 14, 10, H("#B8BCC6"));
            c.FillRect(w - 20, facadeH + 8, 10, 6, H("#8E929C"));
        }

        private static void NeonTower(PixelCanvas c, CityBuilding b, bool lit, int w, int h)
        {
            int roofH = Math.Min(30, h / 5), facadeH = h - roofH;
            string[] skins = { "#22223B", "#1B1B3A", "#2D1B4E" };
            var skin = H(skins[b.Seed % (uint)skins.Length]);
            for (int y = 0; y < facadeH; y++)
                for (int x = 0; x < w; x++)
                {
                    bool win = x % 6 > 1 && y % 8 > 2 && y > 30;
                    var col = skin;
                    if (win) col = (lit || Hash(x / 6, y / 8, b.Seed) % 5 == 0) && Hash(x / 6, y / 8, b.Seed + 1) % 3 != 0 ? H("#FFE39F") : H("#3A3A5C");
                    c.Set(x, y, col);
                }
            // Neon strips up both edges.
            var pink = lit ? H("#FF2E88") : H("#A01A57");
            var cyan = lit ? H("#00F0FF") : H("#0A7C85");
            c.FillRect(1, 30, 2, facadeH - 30, pink);
            c.FillRect(w - 3, 30, 2, facadeH - 30, cyan);
            // The marquee: a band of bulbs around a lit board.
            int my = 14, mh = 14;
            c.FillRect(0, my, w, mh, H("#14141F"));
            for (int x = 1; x < w - 1; x += 3)
            {
                bool on = lit || (x / 3) % 2 == 0;
                c.Set(x, my + 1, on ? H("#FFD60A") : H("#7A6A1A"));
                c.Set(x, my + mh - 2, on ? H("#FFD60A") : H("#7A6A1A"));
            }
            string text = string.IsNullOrEmpty(b.Sign) ? "TONIGHT" : b.Sign.ToUpperInvariant();
            PixelFont.DrawCentered(c, text, w / 2, my + mh - 4, lit ? H("#FF2E88") : H("#F2E8CF"));
            // The entrance under the marquee.
            c.FillRect(w / 2 - 14, 0, 28, 13, H("#C9A227"));
            c.FillRect(w / 2 - 12, 0, 24, 11, lit ? H("#FFE7B0") : H("#5A4A1A"));
            // A big star on the face.
            var star = lit ? H("#FFD60A") : H("#8A7A2A");
            int sx = w / 2, sy = facadeH - 20;
            c.FillRect(sx - 1, sy - 8, 3, 17, star);
            c.FillRect(sx - 8, sy - 1, 17, 3, star);
            for (int i = -5; i <= 5; i++) { c.Set(sx + i, sy + i, star); c.Set(sx + i, sy - i, star); }
            FlatRoof(c, w, h, facadeH, "#3A3A48", b.Seed);
            // A rooftop helipad.
            int cx = w / 2, cy = facadeH + roofH / 2;
            c.FillCircle(cx, cy, Math.Min(10, roofH / 2 - 2), H("#5C5C6E"));
            PixelFont.DrawCentered(c, "H", cx, cy + 2, H("#F2F2F2"));
        }

        private static void MotelBlock(PixelCanvas c, CityBuilding b, bool lit, int w, int h)
        {
            int roofH = Math.Min(24, (int)(h * 0.36f)), facadeH = h - roofH;
            var wall = H("#F2E2C4");
            for (int y = 0; y < facadeH; y++)
                for (int x = 0; x < w; x++) c.Set(x, y, Noise(x, y, b.Seed) > 0.93f ? wall.Darken(0.06f) : wall);
            c.FillRect(0, facadeH - 6, w, 6, H("#2EC4B6"));
            // Two floors of rooms: doors with numbers and a window by each, a walkway with a railing upstairs.
            int upper = Math.Max(26, facadeH / 2);
            for (int floor = 0; floor < 2; floor++)
            {
                int fy = floor == 0 ? 0 : upper;
                if (fy + 22 > facadeH - 6) break;
                int k = floor == 0 ? 101 : 201;
                for (int x = 6; x + 22 < w; x += 26)
                {
                    c.FillRect(x, fy, 10, 18, H("#2EC4B6").Darken(0.3f));
                    c.FillRect(x + 7, fy + 8, 2, 2, H("#E9C46A"));
                    Window(c, x + 13, fy + 7, 10, 9, H("#FFFFFF"), lit && Hash(x, floor, b.Seed) % 2 == 0);
                    PixelFont.Draw(c, (k++).ToString(), x, fy + 24, H("#14141F"));
                }
                if (floor == 1)
                {
                    c.FillRect(0, upper - 2, w, 2, H("#ADB5BD"));
                    for (int x = 0; x < w; x += 4) c.FillRect(x, upper, 1, 7, H("#6C757D"));
                    c.FillRect(0, upper + 7, w, 1, H("#6C757D"));
                }
            }
            SignBoard(c, b, w, facadeH - 10 > 22 ? 22 : facadeH - 10, lit, "#FF006E", "#FFFFFF");
            FlatRoof(c, w, h, facadeH, "#C9B79C", b.Seed);
        }

        private static void Victorian(PixelCanvas c, CityBuilding b, bool lit, int w, int h)
        {
            int roofH = Math.Min(34, (int)(h * 0.36f)), facadeH = h - roofH;
            string[] bodies = { "#C8B6E2", "#B9E4C9", "#F9E79F", "#F5B7B1", "#AED6F1", "#FAD7A0" };
            string[] trims = { "#6C3483", "#1E8449", "#B9770E", "#943126", "#1F618D", "#A04000" };
            int pick = (int)(b.Seed % (uint)bodies.Length);
            var body = H(bodies[pick]);
            var trim = H(trims[pick]);
            for (int y = 0; y < facadeH; y++)
                for (int x = 0; x < w; x++) c.Set(x, y, y % 4 == 0 ? body.Darken(0.08f) : body); // clapboard
            // A gable over the top of the facade.
            for (int y = 0; y < 18; y++)
            {
                int half = Math.Max(0, w / 2 - y * w / 36);
                c.FillRect(w / 2 - half, facadeH - 18 + y, half * 2, 1, y % 4 == 0 ? body.Darken(0.12f) : body.Lighten(0.04f));
            }
            for (int y = 0; y < 18; y++) { int half = Math.Max(0, w / 2 - y * w / 36); c.Set(w / 2 - half, facadeH - 18 + y, trim); c.Set(w / 2 + half - 1, facadeH - 18 + y, trim); }
            // Bay window bump and tall trimmed windows.
            c.FillRect(4, 18, w - 8, 36 > facadeH - 22 ? facadeH - 26 : 34, body.Lighten(0.08f));
            int floors = Math.Max(1, (facadeH - 30) / 20);
            for (int f = 0; f < floors; f++)
                for (int x = 8; x + 8 < w - 4; x += 14)
                {
                    int wy = 22 + f * 20;
                    if (wy + 14 > facadeH - 20) break;
                    Window(c, x, wy, 7, 13, trim, lit && Hash(x, f, b.Seed) % 3 != 0);
                    c.FillRect(x - 2, wy + 14, 11, 2, H("#FFFFFF"));
                }
            // Steps up to the door.
            Door(c, b, b.DoorX - b.X, trim.Darken(0.2f), lit);
            for (int s = 0; s < 3; s++) c.FillRect((b.DoorX - b.X) * Tile - 2 + s * 2, s * 2, 20 - s * 4, 2, H("#FFFFFF").Darken(0.1f + s * 0.05f));
            // Steep shingled roof from above, with a little turret cap.
            for (int y = facadeH; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int row = (y - facadeH) / 3;
                    bool seam = (y - facadeH) % 3 == 0 || (x + (row % 2) * 3) % 6 == 0;
                    c.Set(x, y, seam ? H("#2F2F3A") : H("#4A4A5A"));
                }
            c.FillCircle(w - 10, facadeH + roofH / 2, 6f, trim);
        }

        private static void Warehouse(PixelCanvas c, CityBuilding b, bool lit, int w, int h)
        {
            int roofH = Math.Min(30, (int)(h * 0.36f)), facadeH = h - roofH;
            for (int y = 0; y < facadeH; y++)
                for (int x = 0; x < w; x++)
                {
                    int row = y / 4;
                    bool joint = y % 4 == 0 || (x + (row % 2) * 4) % 8 == 0;
                    c.Set(x, y, joint ? H("#5A2E22") : H("#8B3A2B"));
                }
            // A mural across the wall: sunset bands, a ball, a skyline and a raised fist of a hoop silhouette.
            int my = 22, mh = Math.Max(10, facadeH - 30);
            string[] bands = { "#FF9F1C", "#F15BB5", "#9B5DE5", "#00BBF9", "#00F5D4" };
            for (int y = 0; y < mh; y++)
                c.FillRect(4, my + y, w - 8, 1, H(bands[(y * bands.Length) / mh]));
            c.FillCircle(w / 3, my + mh / 2, Math.Min(10, mh / 2 - 1), H("#E36414"));
            for (int i = -9; i <= 9; i++) c.Set(w / 3 + i, my + mh / 2, H("#14141F"));
            for (int x = w / 2; x < w - 6; x += 7) c.FillRect(x, my, 5, 4 + (int)(Hash(x, 2, b.Seed) % (uint)Math.Max(1, mh - 6)), H("#14141F"));
            // A roll-up door and a side door.
            c.FillRect(8, 0, 26, 18, H("#9DA3AF"));
            for (int y = 2; y < 18; y += 3) c.FillRect(8, y, 26, 1, H("#6B6F78"));
            Door(c, b, b.W - 2, H("#2B2D42"), lit);
            // A sawtooth roof.
            for (int y = facadeH; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int t = (x % 16);
                    bool glass = t >= 12;
                    c.Set(x, y, glass ? (lit ? H("#FFE39F") : H("#5E7C99")) : (y - facadeH) % 4 == 0 ? H("#5B5F6B") : H("#6E727E"));
                }
        }

        // ------------------------------------------------------------------ markers (side jobs)

        /// <summary>Side-job markers: 0 the "!" over a giver, 1 a thing to pick up (a gold gem), 2 the errand's drop spot (a ring), 3 the "?" of a job you've taken.</summary>
        public static PixelCanvas Marker(int kind)
        {
            switch (kind)
            {
                case 1:
                {
                    var c = new PixelCanvas(12, 14);
                    for (int y = 0; y < 12; y++) { int half = y < 6 ? y : 11 - y; c.FillRect(6 - half, y + 1, half * 2 + 1, 1, y < 6 ? H("#FFB703") : H("#FFD60A")); }
                    c.Set(4, 9, H("#FFFFFF")); c.Set(5, 10, H("#FFFFFF"));
                    c.FillRect(2, 0, 8, 1, new RgbColor(0, 0, 0, 90));
                    return c;
                }
                case 2:
                {
                    var c = new PixelCanvas(28, 14);
                    for (int i = 0; i < 64; i++)
                    {
                        double a = Math.PI * 2 * i / 64;
                        int x = 14 + (int)Math.Round(Math.Cos(a) * 12), y = 7 + (int)Math.Round(Math.Sin(a) * 5);
                        c.Set(x, y, H("#4CC9F0"));
                        c.Set(x, y + 1, H("#4CC9F0").Darken(0.3f));
                    }
                    return c;
                }
                default:
                {
                    var c = new PixelCanvas(12, 16);
                    var fill = kind == 3 ? H("#4CC9F0") : H("#FFD166");
                    c.FillRect(1, 4, 10, 11, Outline);
                    c.FillRect(2, 5, 8, 9, fill);
                    c.FillRect(5, 2, 2, 3, Outline);
                    c.Set(6, 1, Outline);
                    if (kind == 3) { c.FillRect(4, 12, 4, 1, Outline); c.FillRect(7, 10, 1, 2, Outline); c.FillRect(5, 9, 2, 1, Outline); c.FillRect(5, 6, 2, 2, Outline); }
                    else { c.FillRect(5, 8, 2, 5, Outline); c.FillRect(5, 6, 2, 1, Outline); }
                    return c;
                }
            }
        }

        // ------------------------------------------------------------------ props

        private static PixelCanvas WorldProp(CityProp p, bool lit, out int pivotX, out int pivotY)
        {
            pivotX = 0;
            pivotY = 0;
            int w = Math.Max(1, p.W) * Tile;
            switch (p.Kind)
            {
                case CityPropKind.SubwayStairs:
                {
                    var c = new PixelCanvas(w, 30);
                    c.FillRect(0, 0, w, 14, H("#2B2B33"));
                    for (int s = 0; s < 5; s++) c.FillRect(3, s * 3, w - 6, 2, H("#5C5C66").Lighten(s * 0.06f));
                    var rail = H("#1E6B3A");
                    c.FillRect(0, 0, 2, 18, rail);
                    c.FillRect(w - 2, 0, 2, 18, rail);
                    c.FillRect(0, 16, w, 2, rail);
                    foreach (int x in new[] { 1, w - 2 })
                    {
                        c.FillRect(x, 18, 1, 6, rail);
                        c.FillCircle(x, 26, 3f, lit ? H("#C8F7C5") : H("#5E9E6E"));
                    }
                    PixelFont.DrawCentered(c, "SUBWAY", w / 2, 13, H("#F2F2F2"));
                    return c;
                }
                case CityPropKind.FoodCart:
                {
                    var c = new PixelCanvas(w, 34);
                    string[] tops = { "#FFD60A", "#E63946", "#2A9D8F" };
                    var top = H(tops[Math.Abs(p.Variant) % tops.Length]);
                    c.FillRect(4, 0, 4, 4, Outline);
                    c.FillRect(w - 8, 0, 4, 4, Outline);
                    c.FillRect(2, 3, w - 4, 12, H("#C0C6CC"));
                    c.FillRect(2, 13, w - 4, 2, H("#ADB5BD"));
                    c.FillRect(w / 2, 15, 1, 8, H("#6C757D"));
                    for (int y = 0; y < 8; y++)
                        for (int x = -w / 2 + 2 + y; x < w / 2 - 2 - y; x++)
                            c.Set(w / 2 + x, 23 + y, ((x + 40) / 4) % 2 == 0 ? top : H("#FFFFFF"));
                    c.FillRect(6, 7, w - 12, 4, lit ? H("#FFE7B0") : H("#8D99AE"));
                    return c;
                }
                case CityPropKind.Cactus:
                {
                    var c = new PixelCanvas(20, 40);
                    pivotX = 10 - Tile / 2;
                    var g = H("#3F7D3A");
                    c.FillRect(8, 0, 5, 36, g);
                    c.FillRect(9, 0, 1, 36, g.Lighten(0.2f));
                    int armL = 12 + Math.Abs(p.Variant) % 6, armR = 18 + Math.Abs(p.Variant * 3) % 6;
                    c.FillRect(3, armL, 6, 3, g);
                    c.FillRect(3, armL, 3, 12, g);
                    c.FillRect(12, armR, 6, 3, g);
                    c.FillRect(15, armR, 3, 10, g);
                    c.FillCircle(10, 36, 2.5f, g);
                    if (p.Variant % 3 == 0) c.FillCircle(10, 38, 1.5f, H("#F72585"));
                    return c;
                }
                case CityPropKind.NeonSign:
                {
                    var c = new PixelCanvas(28, 60);
                    pivotX = 14 - Tile / 2;
                    c.FillRect(12, 0, 4, 34, H("#4A4A5A"));
                    string[] cols = { "#FF2E88", "#00F0FF", "#FFD60A", "#7CE577", "#FF8C42" };
                    var neon = H(cols[Math.Abs(p.Variant) % cols.Length]);
                    var on = lit ? neon : neon.Darken(0.45f);
                    int kind = Math.Abs(p.Variant) % 3;
                    c.FillRect(1, 32, 26, 24, H("#14141F"));
                    if (kind == 0) { Rect(c, 2, 33, 24, 22, on); Rect(c, 4, 35, 20, 18, on); }
                    else if (kind == 1) { for (int i = 0; i < 12; i++) { c.Set(14 + i, 44 + i / 2, on); c.Set(14 + i, 44 - i / 2, on); } c.FillRect(3, 43, 12, 3, on); }
                    else { c.FillCircle(14, 44, 9f, on); c.FillCircle(14, 44, 7f, H("#14141F")); c.FillCircle(14, 44, 3f, on); }
                    if (lit) c.FillRect(12, 30, 4, 2, neon.Lighten(0.3f));
                    return c;
                }
                case CityPropKind.Lifeguard:
                {
                    var c = new PixelCanvas(w, 56);
                    var wood = H("#E9C46A");
                    c.FillRect(4, 0, 3, 26, wood.Darken(0.3f));
                    c.FillRect(w - 7, 0, 3, 26, wood.Darken(0.3f));
                    for (int i = 0; i < 20; i++) { c.Set(6 + i, 4 + i, wood.Darken(0.3f)); c.Set(w - 7 - i, 4 + i, wood.Darken(0.3f)); }
                    c.FillRect(2, 24, w - 4, 4, wood.Darken(0.15f));
                    c.FillRect(6, 28, w - 12, 18, H("#4CC9F0"));
                    c.FillRect(10, 32, w - 20, 9, H("#2B3A55"));
                    for (int y = 46; y < 52; y++) c.FillRect(4 + (y - 46), y, w - 8 - (y - 46) * 2, 1, H("#E63946"));
                    for (int x = 2; x < w - 2; x += 6) c.FillRect(x, 24, 3, 1, H("#FFFFFF"));
                    return c;
                }
                case CityPropKind.TacoTruck:
                {
                    var c = new PixelCanvas(w, 40);
                    c.FillRect(6, 0, 8, 6, Outline);
                    c.FillRect(w - 14, 0, 8, 6, Outline);
                    c.FillRect(1, 4, w - 2, 26, H("#F4F1DE"));
                    c.FillRect(1, 4, w - 2, 4, H("#E76F51"));
                    c.FillRect(w - 12, 12, 10, 12, H("#2B3A55"));
                    c.FillRect(6, 12, w - 22, 12, lit ? H("#FFD98A") : H("#3E5274"));
                    for (int x = 4; x < w - 16; x += 6) c.FillRect(x, 24, 4, 4, (x / 6) % 2 == 0 ? H("#E63946") : H("#2A9D8F"));
                    PixelFont.DrawCentered(c, "TACOS", (w - 12) / 2, 36, H("#E76F51"));
                    c.FillRect(1, 30, w - 2, 1, H("#ADB5BD"));
                    return c;
                }
                case CityPropKind.Fountain:
                {
                    var c = new PixelCanvas(w, 40);
                    c.FillRect(0, 0, w, 10, H("#ADB5BD"));
                    c.FillRect(2, 2, w - 4, 7, lit ? H("#7FD3F2") : H("#4FA3C7"));
                    c.FillRect(w / 2 - 2, 8, 4, 14, H("#ADB5BD"));
                    var spray = lit ? H("#E0FBFF") : H("#BDE7F5");
                    for (int i = 0; i < 9; i++)
                    {
                        c.FillRect(w / 2 - 1, 22 + i * 2, 2, 2, spray);
                        c.Set(w / 2 - 3 - i, 36 - i * i / 3, spray);
                        c.Set(w / 2 + 2 + i, 36 - i * i / 3, spray);
                    }
                    return c;
                }
                case CityPropKind.CableCar:
                {
                    var c = new PixelCanvas(w, 52);
                    var red = H("#B5121B");
                    c.FillRect(2, 0, 4, 4, Outline);
                    c.FillRect(w - 6, 0, 4, 4, Outline);
                    c.FillRect(0, 3, w, 30, red);
                    c.FillRect(0, 30, w, 3, H("#F4E3C1"));
                    for (int y = 8; y < 28; y += 7) c.FillRect(3, y, w - 6, 5, lit ? H("#FFD98A") : H("#2B3A55"));
                    c.FillRect(0, 33, w, 10, H("#F4E3C1"));
                    c.FillRect(2, 43, w - 4, 3, red.Darken(0.3f));
                    c.FillCircle(w / 2, 48, 3f, H("#E9C46A")); // the bell
                    return c;
                }
                case CityPropKind.BridgeTower:
                {
                    var c = new PixelCanvas(w + 32, 150);
                    pivotX = 16;
                    var red = H("#C0392B");
                    c.FillRect(18, 0, 6, 140, red);
                    c.FillRect(w + 8, 0, 6, 140, red);
                    for (int y = 30; y < 140; y += 34) c.FillRect(18, y, w - 4, 5, red.Darken(0.15f));
                    var cable = H("#E74C3C").Lighten(0.1f);
                    for (int i = 0; i < 40; i++)
                    {
                        c.Set(18 - i * 16 / 40, 138 - i * 3, cable);
                        c.Set(w + 14 + i * 16 / 40, 138 - i * 3, cable);
                    }
                    c.FillRect(0, 18, w + 32, 4, H("#8E2A20")); // the deck passing through
                    if (lit) { c.FillRect(19, 140, 4, 3, H("#FF3B30")); c.FillRect(w + 9, 140, 4, 3, H("#FF3B30")); }
                    return c;
                }
                case CityPropKind.Crane:
                {
                    var c = new PixelCanvas(w + 24, 120);
                    pivotX = 12;
                    var steel = H(p.Variant % 2 == 0 ? "#E36414" : "#3A86FF");
                    c.FillRect(14, 0, 4, 80, steel);
                    c.FillRect(w + 6, 0, 4, 80, steel);
                    for (int i = 0; i < 70; i += 10) for (int k = 0; k < w - 8; k++) c.Set(18 + k, i + k * 10 / Math.Max(1, w - 8), steel.Darken(0.25f));
                    c.FillRect(0, 80, w + 24, 6, steel);
                    c.FillRect(w + 2, 86, 16, 10, H("#2B2D42"));
                    c.FillRect(6, 60, 1, 20, H("#14141F"));
                    c.FillRect(2, 54, 10, 6, H(p.Variant % 2 == 0 ? "#2A9D8F" : "#E63946")); // a container on the hook
                    if (lit) c.FillRect(w + 10, 96, 3, 3, H("#FF3B30"));
                    return c;
                }
                case CityPropKind.Mural:
                {
                    var c = new PixelCanvas(w, 44);
                    c.FillRect(0, 0, w, 44, H("#F1E3C8"));
                    string[] cols = { "#E63946", "#F4A261", "#E9C46A", "#2A9D8F", "#264653", "#8338EC" };
                    for (int y = 0; y < 40; y++) c.FillRect(0, y, w, 1, H(cols[(y / 7) % cols.Length]).Lighten(0.1f));
                    c.FillCircle(w / 4, 24, 12f, H("#FFB703"));
                    for (int i = 0; i < 12; i++) c.Set(w / 4 - 11 + i * 2, 24, H("#14141F"));
                    for (int x = w / 2; x < w - 8; x += 22)
                    {
                        // A player rising for a dunk, in silhouette.
                        c.FillCircle(x + 6, 30, 3f, H("#14141F"));
                        c.FillRect(x + 4, 14, 5, 14, H("#14141F"));
                        c.FillRect(x + 8, 26, 2, 10, H("#14141F"));
                        c.FillRect(x + 2, 6, 2, 9, H("#14141F"));
                        c.FillRect(x + 8, 8, 2, 7, H("#14141F"));
                    }
                    c.FillRect(0, 40, w, 4, H("#8E9196"));
                    return c;
                }
                case CityPropKind.Poster:
                {
                    var c = new PixelCanvas(w, 40);
                    c.FillRect(2, 0, 2, 14, H("#4A4A5A"));
                    c.FillRect(w - 4, 0, 2, 14, H("#4A4A5A"));
                    c.FillRect(0, 12, w, 28, Outline);
                    c.FillRect(1, 13, w - 2, 26, p.Variant % 2 == 0 ? H("#FFD60A") : H("#F4F1DE"));
                    c.FillRect(1, 29, w - 2, 10, H("#E63946"));
                    PixelFont.DrawCentered(c, "BIG RUN", w / 2, 37, H("#FFFFFF"));
                    PixelFont.DrawCentered(c, "$$$", w / 2, 26, H("#14141F"), 2);
                    if (lit) for (int x = 1; x < w - 1; x += 4) c.Set(x, 39, H("#FFF1C4"));
                    return c;
                }
                case CityPropKind.Rock:
                {
                    var c = new PixelCanvas(w, 16);
                    c.FillCircle(w / 2 - 4, 6, 7f, H("#9C6644"));
                    c.FillCircle(w / 2 + 5, 5, 5f, H("#B07D56"));
                    c.FillRect(2, 0, w - 4, 3, H("#7F5539"));
                    return c;
                }
                case CityPropKind.Billboard:
                {
                    var c = new PixelCanvas(w, 70);
                    c.FillRect(w / 2 - 3, 0, 6, 40, H("#4A4A5A"));
                    c.FillRect(0, 38, w, 30, Outline);
                    c.FillRect(2, 40, w - 4, 26, lit ? H("#3A0CA3") : H("#22223B"));
                    PixelFont.DrawCentered(c, "HOOPS", w / 2, 60, lit ? H("#FFD60A") : H("#C9A227"), 2);
                    PixelFont.DrawCentered(c, "ALL NIGHT", w / 2, 47, lit ? H("#4CC9F0") : H("#8D99AE"));
                    return c;
                }
                case CityPropKind.Ride:
                {
                    var car = Car(new CityProp { Kind = CityPropKind.Car, W = p.W, Variant = 3 }, out _, out _);
                    // Your ride: gold with a white racing stripe and a glow underneath.
                    for (int i = 0; i < car.Pixels.Length; i++)
                    {
                        var px = car.Pixels[i];
                        if (px.a == 0) continue;
                        if (px.r == 0xE9 && px.g == 0xC4 && px.b == 0x6A) car.Pixels[i] = H("#FFB703");
                    }
                    car.FillRect(2, 8, car.Width - 4, 2, H("#FFFFFF"));
                    if (lit) car.FillRect(1, 0, car.Width - 2, 1, H("#4CC9F0"));
                    return car;
                }
                default: // Pillar
                {
                    var c = new PixelCanvas(Tile, 70);
                    c.FillRect(3, 0, 10, 60, H("#9DA3AF"));
                    c.FillRect(3, 0, 2, 60, H("#B8BCC6"));
                    c.FillRect(0, 58, Tile, 12, H("#7D828F"));
                    c.FillRect(0, 58, Tile, 2, H("#5C606B"));
                    return c;
                }
            }
        }
    }
}
