using System;

namespace CallerRetroBall.Logic.PixelArt
{
    /// <summary>
    /// 28 Oct 2026 (Omari, BALLER WORLD W6): THE DRIVE's art, seen from above: a three-lane highway strip (the red
    /// bridge's deck for the Bay), cars from above (your gold ride and the traffic), and the roadside for each kind of
    /// country (suburb trees and roofs, city blocks, palms, cactus and mesas, painted roofs and the water, fields).
    /// 16 pixels per lane, like the city's 16 per tile.
    /// </summary>
    public static class DriveArt
    {
        public const int Lane = 16, Shoulder = 6;
        public const int RoadWidth = HighwayDrive.Lanes * Lane + Shoulder * 2;
        public const int RoadTile = 32;

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

        /// <summary>One repeating stretch of road (RoadWidth × RoadTile).</summary>
        public static PixelCanvas Road(bool bridge)
        {
            var c = new PixelCanvas(RoadWidth, RoadTile);
            for (int y = 0; y < RoadTile; y++)
                for (int x = 0; x < RoadWidth; x++)
                {
                    float n = (Hash(x, y, 3) & 0xFF) / 255f;
                    var col = n > 0.9f ? H("#45454E") : n < 0.1f ? H("#303038") : H("#3A3A42");
                    if (x < Shoulder || x >= RoadWidth - Shoulder) col = bridge ? H("#8E2A20") : H("#5C5C66");
                    c.Set(x, y, col);
                }
            // Edge lines and dashed lane lines.
            c.FillRect(Shoulder, 0, 1, RoadTile, H("#F2F2F2"));
            c.FillRect(RoadWidth - Shoulder - 1, 0, 1, RoadTile, H("#F2F2F2"));
            for (int k = 1; k < HighwayDrive.Lanes; k++) c.FillRect(Shoulder + k * Lane, 4, 1, 14, H("#E9C46A"));
            if (bridge)
            {
                c.FillRect(0, 0, 2, RoadTile, H("#C0392B"));
                c.FillRect(RoadWidth - 2, 0, 2, RoadTile, H("#C0392B"));
                c.FillRect(0, 0, 4, 3, H("#E74C3C"));
                c.FillRect(RoadWidth - 4, 0, 4, 3, H("#E74C3C"));
            }
            return c;
        }

        private static readonly string[] Traffic = { "#D62828", "#1D4ED8", "#F2F2F2", "#2EC4B6", "#7209B7", "#FFD60A", "#6C757D", "#0B6E4F" };

        /// <summary>A car from above, nose up (12 × 22). Variant −1 = your ride (gold with a white stripe).</summary>
        public static PixelCanvas Car(int variant)
        {
            var c = new PixelCanvas(12, 22);
            bool mine = variant < 0;
            var body = mine ? H("#FFB703") : H(Traffic[Math.Abs(variant) % Traffic.Length]);
            var dark = body.Darken(0.3f);
            c.FillRect(1, 1, 10, 20, body);
            c.FillRect(0, 3, 1, 4, H("#14141F"));
            c.FillRect(11, 3, 1, 4, H("#14141F"));
            c.FillRect(0, 15, 1, 4, H("#14141F"));
            c.FillRect(11, 15, 1, 4, H("#14141F"));
            c.FillRect(2, 13, 8, 4, H("#2B3A55")); // windshield
            c.FillRect(2, 6, 8, 6, dark); // roof
            c.FillRect(2, 3, 8, 2, H("#2B3A55")); // rear window
            c.FillRect(2, 20, 2, 1, H("#FFF1C4"));
            c.FillRect(8, 20, 2, 1, H("#FFF1C4"));
            c.FillRect(2, 1, 2, 1, H("#FF3B30"));
            c.FillRect(8, 1, 2, 1, H("#FF3B30"));
            if (mine) c.FillRect(5, 1, 2, 20, H("#FFFFFF"));
            return c;
        }

        /// <summary>The ground beside the road.</summary>
        public static RgbColor Ground(WorldBiome b)
        {
            switch (b)
            {
                case WorldBiome.Metro: return H("#55555F");
                case WorldBiome.Coast: return H("#E2C98F");
                case WorldBiome.Desert: return H("#DCBB84");
                case WorldBiome.Bay: return H("#457B9D");
                case WorldBiome.Plains: return H("#A3B18A");
                default: return H("#4C8A3A");
            }
        }

        /// <summary>A roadside thing seen from above (24 × 24): trees and roofs, blocks, palms, cactus and rocks, painted roofs, fields.</summary>
        public static PixelCanvas Roadside(WorldBiome b, int variant)
        {
            var c = new PixelCanvas(24, 24);
            int v = Math.Abs(variant);
            switch (b)
            {
                case WorldBiome.Metro:
                {
                    var roof = v % 2 == 0 ? H("#6C757D") : H("#495057");
                    c.FillRect(0, 0, 24, 24, roof);
                    c.FillRect(0, 0, 24, 2, roof.Darken(0.3f));
                    c.FillRect(4, 4, 6, 5, H("#ADB5BD"));
                    if (v % 3 == 0) { c.FillCircle(16, 15, 4f, H("#8B5A2B")); c.FillCircle(16, 15, 2f, H("#6E4520")); }
                    else c.FillRect(13, 12, 8, 8, H("#343A40"));
                    break;
                }
                case WorldBiome.Coast:
                    if (v % 3 == 0) { c.FillRect(0, 0, 24, 24, H("#0077B6")); for (int x = 0; x < 24; x += 5) c.FillRect(x, 10 + x % 3, 3, 1, H("#CAF0F8")); }
                    else Palm(c);
                    break;
                case WorldBiome.Desert:
                    if (v % 3 == 0) { c.FillCircle(12, 12, 9f, H("#B5654A")); c.FillCircle(12, 13, 6f, H("#C97C5D")); }
                    else if (v % 3 == 1) { c.FillCircle(12, 12, 4f, H("#3F7D3A")); c.FillRect(4, 11, 16, 2, H("#3F7D3A")); }
                    else { c.FillCircle(10, 10, 5f, H("#9C6644")); c.FillCircle(16, 14, 3f, H("#B07D56")); }
                    break;
                case WorldBiome.Bay:
                {
                    string[] roofs = { "#C8B6E2", "#B9E4C9", "#F9E79F", "#F5B7B1" };
                    if (v % 4 == 3) { c.FillRect(0, 0, 24, 24, H("#457B9D")); c.FillRect(6, 8, 10, 4, H("#F1FAEE")); }
                    else
                    {
                        c.FillRect(2, 2, 20, 20, H(roofs[v % roofs.Length]));
                        for (int y = 2; y < 22; y += 3) c.FillRect(2, y, 20, 1, H("#4A4A5A"));
                        c.FillRect(11, 2, 2, 20, H("#2F2F3A"));
                    }
                    break;
                }
                case WorldBiome.Plains:
                    for (int y = 0; y < 24; y += 4) c.FillRect(0, y, 24, 2, v % 2 == 0 ? H("#E9C46A") : H("#8AA36B"));
                    if (v % 3 == 0) { c.FillRect(6, 6, 10, 8, H("#9B2226")); c.FillRect(6, 14, 10, 2, H("#F4F1DE")); }
                    break;
                default:
                    if (v % 2 == 0) { c.FillCircle(12, 12, 9f, H("#2E7D32")); c.FillCircle(10, 14, 5f, H("#43A047")); }
                    else
                    {
                        c.FillRect(3, 3, 18, 18, H("#9B3D2E"));
                        for (int y = 3; y < 21; y += 3) c.FillRect(3, y, 18, 1, H("#7A2E22"));
                        c.FillRect(11, 3, 2, 18, H("#5C2018"));
                    }
                    break;
            }
            return c;
        }

        /// <summary>A green sign over the road (an exit, the city ahead): two lines on a gantry across all three lanes.</summary>
        public static PixelCanvas Gantry(string text, string sub)
        {
            text = (text ?? "").ToUpperInvariant();
            sub = (sub ?? "").ToUpperInvariant();
            int w = Math.Max(RoadWidth + 10, Math.Max(PixelFont.Measure(text, 1), PixelFont.Measure(sub, 1)) + 14);
            var c = new PixelCanvas(w, 30);
            var steel = H("#5C6370");
            c.FillRect(1, 0, 3, 30, steel);
            c.FillRect(w - 4, 0, 3, 30, steel);
            c.FillRect(0, 26, w, 2, steel);
            c.FillRect(4, 9, w - 8, 18, H("#F4F1DE"));
            c.FillRect(5, 10, w - 10, 16, H("#11663A"));
            PixelFont.DrawCentered(c, text, w / 2, 24, H("#FFFFFF"));
            PixelFont.DrawCentered(c, sub, w / 2, 16, H("#FFE08A"));
            return c;
        }

        /// <summary>A roadside sign: NOW LEAVING (green), STATE LINE (blue) or a mile marker (small green).</summary>
        public static PixelCanvas Sign(DriveFeatureKind kind, string text, string sub)
        {
            text = (text ?? "").ToUpperInvariant();
            sub = (sub ?? "").ToUpperInvariant();
            if (kind == DriveFeatureKind.MileMarker)
            {
                int mw = PixelFont.Measure(text, 1) + 6;
                var m = new PixelCanvas(mw, 16);
                m.FillRect(mw / 2 - 1, 0, 2, 8, H("#5C6370"));
                m.FillRect(0, 7, mw, 9, H("#F4F1DE"));
                m.FillRect(1, 8, mw - 2, 7, H("#11663A"));
                PixelFont.DrawCentered(m, text, mw / 2, 14, H("#FFFFFF"));
                return m;
            }
            int w = Math.Max(PixelFont.Measure(text, 1), PixelFont.Measure(sub, 1)) + 12;
            var c = new PixelCanvas(w, 26);
            var face = kind == DriveFeatureKind.StateLine ? H("#1D4E89") : H("#11663A");
            c.FillRect(w / 2 - 1, 0, 3, 10, H("#5C6370"));
            c.FillRect(0, 9, w, 17, H("#F4F1DE"));
            c.FillRect(1, 10, w - 2, 15, face);
            PixelFont.DrawCentered(c, text, w / 2, 23, H("#FFFFFF"));
            PixelFont.DrawCentered(c, sub, w / 2, 16, H("#FFE08A"));
            return c;
        }

        private static void Palm(PixelCanvas c)
        {
            var leaf = H("#2E8B4A");
            for (int i = 0; i < 11; i++)
            {
                c.Set(12 + i, 12, leaf); c.Set(12 - i, 12, leaf); c.Set(12, 12 + i, leaf); c.Set(12, 12 - i, leaf);
                c.Set(12 + i * 7 / 10, 12 + i * 7 / 10, leaf); c.Set(12 - i * 7 / 10, 12 - i * 7 / 10, leaf);
                c.Set(12 + i * 7 / 10, 12 - i * 7 / 10, leaf); c.Set(12 - i * 7 / 10, 12 + i * 7 / 10, leaf);
            }
            c.FillCircle(12, 12, 2.5f, H("#6E4520"));
        }

        /// <summary>The sign over the road at the end: WELCOME TO (region).</summary>
        public static PixelCanvas Welcome(WorldRegion r)
        {
            string name = (r?.Name ?? "HOME").ToUpperInvariant();
            int w = Math.Max(RoadWidth + 8, PixelFont.Measure(name, 2) + 12);
            var c = new PixelCanvas(w, 30);
            c.FillRect(2, 0, 3, 30, H("#4A4A5A"));
            c.FillRect(w - 5, 0, 3, 30, H("#4A4A5A"));
            c.FillRect(0, 8, w, 22, H("#14141F"));
            c.FillRect(1, 9, w - 2, 20, H("#1B5E20"));
            PixelFont.DrawCentered(c, "WELCOME TO", w / 2, 27, H("#FFFFFF"));
            PixelFont.DrawCentered(c, name, w / 2, 20, H("#FFFFFF"), 2);
            return c;
        }
    }
}
