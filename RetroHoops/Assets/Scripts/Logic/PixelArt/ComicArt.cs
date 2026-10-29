using System;

namespace CallerRetroBall.Logic.PixelArt
{
    /// <summary>
    /// 28 Oct 2026 (Omari, BALLER WORLD W1): the comic's panel backdrops, generated: each city's skyline at dusk, a
    /// street, a hoop up close, your ride with speed lines, a phone, the arena's lights, the highway, a rooftop, the
    /// beach, neon, the red bridge in the fog, a dark street for Mr. Velvet, the desert. Finished with comic halftone
    /// dots. Row 0 is the bottom, like every canvas in the game.
    /// </summary>
    public static class ComicArt
    {
        public const int Width = 120, Height = 72;

        private static RgbColor H(string hex) => RgbColor.FromHex(hex);

        private static uint Hash(int x, int y, uint seed)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + seed * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        private static void Sky(PixelCanvas c, RgbColor top, RgbColor bottom)
        {
            for (int y = 0; y < c.Height; y++)
            {
                var col = RgbColor.Lerp(bottom, top, y / (float)(c.Height - 1));
                c.FillRect(0, y, c.Width, 1, col);
            }
        }

        private static (RgbColor top, RgbColor bottom) Dusk(string region)
        {
            switch (region)
            {
                case WorldRegions.NewYork: return (H("#1B1B3A"), H("#F77F00"));
                case WorldRegions.LosAngeles: return (H("#8338EC"), H("#FFBE0B"));
                case WorldRegions.LasVegas: return (H("#10002B"), H("#FF2E88"));
                case WorldRegions.TheBay: return (H("#457B9D"), H("#F1FAEE"));
                default: return (H("#22223B"), H("#FF8C42"));
            }
        }

        /// <summary>A panel backdrop.</summary>
        public static PixelCanvas Panel(ComicScene scene, string region, uint seed = 1)
        {
            var c = new PixelCanvas(Width, Height);
            var (top, bottom) = Dusk(region);
            switch (scene)
            {
                case ComicScene.Skyline: Sky(c, top, bottom); Skyline(c, region, seed, 0); break;
                case ComicScene.Rooftop:
                    Sky(c, H("#10002B"), H("#3A0CA3"));
                    Skyline(c, region, seed, 10);
                    c.FillRect(0, 0, Width, 12, H("#3A3A42"));
                    for (int x = 0; x < Width; x += 4) c.FillRect(x, 12, 1, 14, H("#8E9196"));
                    c.FillRect(0, 25, Width, 1, H("#8E9196"));
                    break;
                case ComicScene.Street: Street(c, region, seed); break;
                case ComicScene.Court: Court(c, top, bottom); break;
                case ComicScene.Car: Car(c); break;
                case ComicScene.Phone: Phone(c); break;
                case ComicScene.Arena: Arena(c, seed); break;
                case ComicScene.Highway: Highway(c, region); break;
                case ComicScene.Beach: Beach(c); break;
                case ComicScene.Neon: Neon(c, seed); break;
                case ComicScene.Bridge: Bridge(c); break;
                case ComicScene.Desert: Desert(c); break;
                default: Night(c); break;
            }
            Halftone(c);
            return c;
        }

        private static void Skyline(PixelCanvas c, string region, uint seed, int baseY)
        {
            var dark = H("#14141F");
            var lit = H("#FFD98A");
            switch (region)
            {
                case WorldRegions.LosAngeles:
                    // Low towns, hills and palms.
                    for (int x = 0; x < Width; x++) c.FillRect(x, baseY, 1, 14 + (int)(Math.Sin(x / 9.0) * 4 + 4), H("#3D2C5E"));
                    for (int x = 4; x < Width; x += 13 + (int)(Hash(x, 1, seed) % 6)) c.FillRect(x, baseY, 9, 10 + (int)(Hash(x, 2, seed) % 10), dark);
                    for (int i = 0; i < 4; i++)
                    {
                        int px = 10 + i * 30;
                        c.FillRect(px, baseY, 2, 34, dark);
                        for (int k = -7; k <= 7; k++) c.Set(px + k, baseY + 34 - Math.Abs(k) / 2, dark);
                    }
                    break;
                case WorldRegions.LasVegas:
                    for (int x = 2; x < Width; x += 14)
                    {
                        int hh = 22 + (int)(Hash(x, 3, seed) % 30);
                        c.FillRect(x, baseY, 11, hh, dark);
                        var neon = (x / 14) % 2 == 0 ? H("#FF2E88") : H("#00F0FF");
                        c.FillRect(x, baseY + hh - 1, 11, 1, neon);
                        c.FillRect(x, baseY, 1, hh, neon);
                    }
                    break;
                case WorldRegions.TheBay:
                    for (int x = 0; x < Width; x++) c.FillRect(x, baseY, 1, 10 + (int)(Math.Sin(x / 14.0) * 6 + 6), H("#5B6C5D"));
                    for (int x = 60; x < Width; x += 7) c.FillRect(x, baseY + 14, 5, 8, H("#C8B6E2"));
                    // The red bridge on the left.
                    c.FillRect(10, baseY, 3, 46, H("#C0392B"));
                    c.FillRect(40, baseY, 3, 46, H("#C0392B"));
                    c.FillRect(0, baseY + 12, 60, 2, H("#C0392B"));
                    for (int x = 0; x < 52; x++) c.Set(x, baseY + 44 - (int)(Math.Abs(x - 26) * Math.Abs(x - 26) / 22.0), H("#E74C3C"));
                    break;
                default:
                    // Towers of all heights, one with a spire (New York has more, and taller).
                    bool ny = region == WorldRegions.NewYork;
                    for (int x = 0; x < Width; x += 8 + (int)(Hash(x, 4, seed) % 5))
                    {
                        int hh = (ny ? 24 : 14) + (int)(Hash(x, 5, seed) % (ny ? 36u : 22u));
                        c.FillRect(x, baseY, 7, hh, dark);
                        for (int wy = baseY + 3; wy < baseY + hh - 2; wy += 4)
                            for (int wx = x + 1; wx < x + 6; wx += 2)
                                if (Hash(wx, wy, seed) % 3 == 0) c.Set(wx, wy, lit);
                    }
                    if (ny)
                    {
                        // A tall tower with a pointed glass crown (no particular building).
                        c.FillRect(70, baseY, 10, 54, dark);
                        for (int k = 0; k < 10; k++) c.FillRect(70 + k / 2, baseY + 54 + k, 10 - k, 1, dark);
                        for (int wy = baseY + 4; wy < baseY + 52; wy += 4) c.Set(75, wy, lit);
                    }
                    break;
            }
        }

        private static void Street(PixelCanvas c, string region, uint seed)
        {
            var (top, bottom) = Dusk(region);
            Sky(c, top, bottom);
            c.FillRect(0, 0, Width, 10, H("#3A3A42"));
            c.FillRect(0, 10, Width, 4, H("#B8B2A6"));
            string[] walls = region == WorldRegions.NewYork ? new[] { "#6B4226", "#7A5236" }
                           : region == WorldRegions.LosAngeles ? new[] { "#F1E3C8", "#F7C6C7" }
                           : region == WorldRegions.LasVegas ? new[] { "#22223B", "#2D1B4E" }
                           : region == WorldRegions.TheBay ? new[] { "#C8B6E2", "#B9E4C9" } : new[] { "#9B3D2E", "#4A6FA5" };
            for (int x = 0, i = 0; x < Width; x += 30, i++)
            {
                c.FillRect(x, 14, 29, 50, H(walls[i % 2]));
                for (int wy = 22; wy < 60; wy += 12)
                    for (int wx = x + 4; wx < x + 26; wx += 9) c.FillRect(wx, wy, 5, 7, Hash(wx, wy, seed) % 2 == 0 ? H("#FFD98A") : H("#2B3A55"));
            }
            for (int x = 6; x < Width; x += 20) c.FillRect(x, 0, 10, 1, H("#E9C46A"));
        }

        private static void Court(PixelCanvas c, RgbColor top, RgbColor bottom)
        {
            Sky(c, top, bottom);
            // The floor in perspective.
            for (int y = 0; y < 22; y++) c.FillRect(0, y, Width, 1, RgbColor.Lerp(H("#4A4A54"), H("#5A5A64"), y / 22f));
            for (int i = -6; i <= 6; i++) for (int y = 0; y < 22; y++) c.Set(Width / 2 + i * 10 + (int)(i * (22 - y) * 0.6f), y, H("#F2F2F2"));
            // The pole, backboard, rim and net.
            c.FillRect(57, 18, 6, 26, H("#6B6F78"));
            c.FillRect(36, 44, 48, 26, H("#F4F1DE"));
            c.FillRect(37, 45, 46, 24, H("#BCD4E6"));
            c.FillRect(52, 50, 16, 12, H("#F4F1DE"));
            c.FillRect(53, 51, 14, 10, H("#BCD4E6"));
            c.FillRect(48, 46, 24, 2, H("#FF6B1A"));
            for (int y = 0; y < 10; y++) for (int x = 49 + y / 3; x < 71 - y / 3; x += 3) c.Set(x + (y % 2), 45 - y, H("#FFFFFF"));
        }

        private static void Car(PixelCanvas c)
        {
            Sky(c, H("#264653"), H("#E9C46A"));
            c.FillRect(0, 0, Width, 18, H("#3A3A42"));
            for (int x = 0; x < Width; x += 16) c.FillRect(x, 8, 8, 2, H("#E9C46A"));
            // Speed lines.
            for (int i = 0; i < 9; i++) c.FillRect((int)(Hash(i, 1, 3) % 40), 22 + i * 5, 20 + (int)(Hash(i, 2, 3) % 30), 1, H("#FFFFFF"));
            // Your ride, side on: gold with a white stripe.
            c.FillRect(40, 12, 70, 16, H("#FFB703"));
            c.FillRect(52, 28, 40, 12, H("#FFB703").Darken(0.1f));
            c.FillRect(56, 30, 32, 8, H("#2B3A55"));
            c.FillRect(40, 18, 70, 2, H("#FFFFFF"));
            c.FillCircle(54, 12, 6f, H("#14141F"));
            c.FillCircle(96, 12, 6f, H("#14141F"));
            c.FillCircle(54, 12, 2f, H("#8E9196"));
            c.FillCircle(96, 12, 2f, H("#8E9196"));
            c.FillRect(108, 20, 3, 3, H("#FFF1C4"));
        }

        private static void Phone(PixelCanvas c)
        {
            Sky(c, H("#3A0CA3"), H("#7209B7"));
            for (int i = 0; i < 14; i++) c.FillRect(0, i * 6, Width, 1, H("#4E1FA8"));
            c.FillRect(40, 4, 40, 64, H("#14141F"));
            c.FillRect(43, 10, 34, 52, H("#4CC9F0"));
            c.FillRect(46, 46, 22, 8, H("#FFFFFF"));
            c.FillRect(52, 34, 22, 8, H("#7CE577"));
            c.FillRect(46, 22, 18, 8, H("#FFFFFF"));
            c.FillRect(56, 6, 8, 2, H("#3A3A48"));
            // Buzz marks.
            foreach (int s in new[] { -1, 1 })
                for (int k = 0; k < 3; k++) c.FillRect(60 + s * (26 + k * 4), 30 + k * 4, 2, 10 - k * 2, H("#FFD60A"));
        }

        private static void Arena(PixelCanvas c, uint seed)
        {
            Sky(c, H("#0B0B1A"), H("#1B1B3A"));
            for (int y = 20; y < 60; y += 3)
                for (int x = (y / 3) % 2; x < Width; x += 3)
                    c.Set(x, y, Hash(x, y, seed) % 4 == 0 ? H("#F72585") : Hash(x, y, seed) % 3 == 0 ? H("#FFD166") : H("#3A3A5C"));
            c.FillRect(0, 0, Width, 20, H("#D8A35D"));
            for (int y = 0; y < 20; y += 3) c.FillRect(0, y, Width, 1, H("#C68E52"));
            // Spotlights.
            for (int i = 0; i < 40; i++)
            {
                c.Set(20 + i, 72 - i * 2 / 3, H("#FFF1C4"));
                c.Set(100 - i, 72 - i * 2 / 3, H("#FFF1C4"));
            }
            c.FillRect(48, 56, 24, 12, H("#14141F"));
            c.FillRect(50, 58, 20, 8, H("#FF2E88"));
        }

        private static void Highway(PixelCanvas c, string region)
        {
            var (top, bottom) = Dusk(region);
            Sky(c, top, bottom);
            c.FillRect(0, 0, Width, 34, H("#6E675C"));
            // The road to the horizon.
            for (int y = 0; y < 34; y++)
            {
                int half = 50 - y * 46 / 34;
                c.FillRect(Width / 2 - half, y, half * 2, 1, H("#3A3A42"));
                if ((y / 3) % 2 == 0) c.Set(Width / 2, y, H("#E9C46A"));
            }
            c.FillCircle(Width / 2, 44, 10f, H("#FFD166"));
            // A car heading away.
            c.FillRect(52, 4, 16, 9, H("#FFB703"));
            c.FillRect(54, 13, 12, 5, H("#2B3A55"));
            c.FillRect(52, 6, 3, 2, H("#FF3B30"));
            c.FillRect(65, 6, 3, 2, H("#FF3B30"));
        }

        private static void Beach(PixelCanvas c)
        {
            Sky(c, H("#48CAE4"), H("#FFD166"));
            c.FillCircle(90, 40, 12f, H("#FFB703"));
            c.FillRect(0, 18, Width, 14, H("#0077B6"));
            for (int x = 0; x < Width; x += 6) c.FillRect(x, 31, 3, 1, H("#CAF0F8"));
            c.FillRect(0, 0, Width, 18, H("#E2C98F"));
            c.FillRect(14, 18, 2, 30, H("#8B5A2B"));
            for (int k = -9; k <= 9; k++) c.Set(15 + k, 48 - Math.Abs(k) / 2, H("#2E8B4A"));
        }

        private static void Neon(PixelCanvas c, uint seed)
        {
            Sky(c, H("#10002B"), H("#240046"));
            string[] cols = { "#FF2E88", "#00F0FF", "#FFD60A", "#7CE577" };
            for (int i = 0; i < 6; i++)
            {
                var col = H(cols[i % cols.Length]);
                int x = 6 + i * 19, y = 10 + (int)(Hash(i, 3, seed) % 30);
                for (int k = 0; k < 14; k++) { c.Set(x + k, y, col); c.Set(x + k, y + 10, col); }
                for (int k = 0; k <= 10; k++) { c.Set(x, y + k, col); c.Set(x + 13, y + k, col); }
            }
            c.FillRect(0, 0, Width, 6, H("#14141F"));
        }

        private static void Bridge(PixelCanvas c)
        {
            Sky(c, H("#8D99AE"), H("#EDF2F4"));
            c.FillRect(0, 0, Width, 22, H("#457B9D"));
            for (int x = 0; x < Width; x += 5) c.Set(x, 21, H("#A8DADC"));
            var red = H("#C0392B");
            c.FillRect(28, 10, 5, 60, red);
            c.FillRect(88, 10, 5, 60, red);
            c.FillRect(0, 24, Width, 3, red);
            for (int x = 0; x < Width; x++)
            {
                int a = Math.Abs(x - 60);
                c.Set(x, 68 - (int)(a * 0.3f) - (a > 30 ? (a - 30) : 0), H("#E74C3C"));
            }
            // Fog across the middle.
            for (int y = 30; y < 44; y++)
                for (int x = 0; x < Width; x++)
                    if (Hash(x / 3, y / 2, 9) % 3 != 0) c.Set(x, y, RgbColor.Lerp(c.Get(x, y), H("#F8F9FA"), 0.6f));
        }

        private static void Desert(PixelCanvas c)
        {
            Sky(c, H("#F4A261"), H("#FFE8D6"));
            c.FillCircle(30, 46, 11f, H("#FFB703"));
            c.FillRect(0, 0, Width, 20, H("#DCBB84"));
            c.FillRect(70, 20, 34, 18, H("#B5654A"));
            c.FillRect(76, 38, 22, 6, H("#B5654A"));
            c.FillRect(18, 4, 4, 26, H("#3F7D3A"));
            c.FillRect(12, 14, 6, 3, H("#3F7D3A"));
            c.FillRect(12, 14, 3, 10, H("#3F7D3A"));
        }

        private static void Night(PixelCanvas c)
        {
            Sky(c, H("#0B0B1A"), H("#22223B"));
            c.FillRect(0, 0, Width, 12, H("#2B2B33"));
            c.FillRect(10, 12, 30, 56, H("#14141F"));
            c.FillRect(80, 12, 40, 50, H("#14141F"));
            // A streetlight and a figure in a long purple coat.
            c.FillRect(60, 12, 2, 48, H("#3A3A48"));
            c.FillRect(56, 58, 10, 3, H("#3A3A48"));
            for (int y = 0; y < 44; y++)
            {
                int half = y / 3;
                for (int x = -half; x <= half; x++) if (Hash(x, y, 5) % 5 == 0) c.Set(61 + x, 56 - y, H("#FFD98A"));
            }
            c.FillRect(66, 12, 10, 20, H("#5A189A"));
            c.FillRect(68, 32, 6, 8, H("#3C096C"));
            c.FillCircle(71, 43, 3.5f, H("#3C096C"));
            c.FillRect(66, 46, 10, 2, H("#14141F")); // the hat brim
            c.FillRect(68, 48, 6, 4, H("#14141F"));
        }

        /// <summary>Comic halftone: a dot screen that darkens the lower part of the panel.</summary>
        private static void Halftone(PixelCanvas c)
        {
            for (int y = 0; y < c.Height; y++)
                for (int x = 0; x < c.Width; x++)
                {
                    bool dot = (x % 4 == 0 && y % 4 == 0) || (x % 4 == 2 && y % 4 == 2);
                    if (!dot) continue;
                    float k = Math.Max(0f, 0.4f - y / (float)c.Height) * 0.6f;
                    if (k <= 0f) continue;
                    c.Set(x, y, c.Get(x, y).Darken(0.15f + k));
                }
        }
    }
}
