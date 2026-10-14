using System;

namespace CallerRetroBall.Logic.PixelArt
{
    /// <summary>
    /// 13 Oct 2026 (art pass 3): the near side of the court, below the bottom of the court art in portrait.
    /// Before, the lower part of the screen (where the thumbs and buttons sit) was a flat colour. Now, from the
    /// top down:
    /// - League-style arenas: an LED courtside board in both teams' colours.
    /// - Outdoor courts: a painted low wall.
    /// - Then a front row of fans seen from behind.
    /// - Then steps that fade, with ordered dither, into a calm dark colour, so the buttons stay easy to see.
    /// Original pixel art. Canvas rows run bottom to top (row 0 is the bottom). Courts are 1 px per court pixel;
    /// fans are 2x.
    /// </summary>
    public static class CourtsideGenerator
    {
        /// <summary>Height of the near-side art (court pixels): 11 m.</summary>
        public const int Height = 176;
        /// <summary>Extra art each side of the court, so wide screens never see an edge.</summary>
        public const int SidePad = 48;
        /// <summary>LED board / wall height from the top of the art.</summary>
        public const int BoardHeight = 14;
        /// <summary>Fans' feet (bottom of the sprite), in rows from the top of the art. Front-row fans are drawn twice
        /// the size of the far stands (10 x 16 court pixels): they are closer to the camera.</summary>
        public const int FanRowFromTop = 34;
        /// <summary>Spacing between front-row fans (court pixels).</summary>
        public const int FanSpacing = 12;

        /// <summary>Courts that get the LED board (arenas); the rest get a painted wall.</summary>
        public static bool IsArena(CourtDef court) =>
            court.circuit == CourtCircuit.League || court.circuit == CourtCircuit.Secret || court.circuit == CourtCircuit.Holiday;

        /// <summary>The colour the art fades into: also used as the camera background so there is no seam.</summary>
        public static RgbColor DeepColor(CourtDef court) =>
            RgbColor.Lerp(court.skyTop.Darken(0.55f), RgbColor.FromHex("#0B0B16"), 0.55f);

        public static PixelCanvas NearSide(CourtDef court, int courtWidthPx, RgbColor teamA, RgbColor teamB)
        {
            int w = courtWidthPx + SidePad * 2;
            var c = new PixelCanvas(w, Height);
            var deep = DeepColor(court);
            var riser = RgbColor.Lerp(court.skyTop.Darken(0.12f), court.floor.Darken(0.4f), 0.3f);
            int top = Height - 1;

            // Steps: riser colour near the top, dithered into the deep colour.
            const int fadeStart = 40, fadeEnd = 100;
            for (int y = 0; y < Height; y++)
            {
                int fromTop = top - y;
                float t = fromTop <= fadeStart ? 0f : fromTop >= fadeEnd ? 1f : (fromTop - fadeStart) / (float)(fadeEnd - fadeStart);
                for (int x = 0; x < w; x++)
                {
                    bool lit = t < PixelCanvas.BayerThreshold(x, y);
                    c.Pixels[y * w + x] = lit ? riser : deep;
                }
                // Step edges every 12 rows, fading with the steps.
                if (fromTop > BoardHeight + 2 && (fromTop - BoardHeight) % 12 == 0 && t < 0.85f)
                {
                    var edge = RgbColor.Lerp(riser.Lighten(0.12f), deep, t);
                    for (int x = 0; x < w; x++) c.Pixels[y * w + x] = edge;
                    if (y > 0) for (int x = 0; x < w; x++) c.Pixels[(y - 1) * w + x] = RgbColor.Lerp(riser.Darken(0.25f), deep, t);
                }
            }

            if (IsArena(court)) DrawLedBoard(c, w, top, teamA, teamB);
            else DrawWall(c, w, top, court);

            // Soft shadow under the board / wall.
            for (int x = 0; x < w; x++)
            {
                int y = top - BoardHeight;
                c.Pixels[y * w + x] = c.Pixels[y * w + x].Darken(0.45f);
                c.Pixels[(y - 1) * w + x] = c.Pixels[(y - 1) * w + x].Darken(0.25f);
            }
            return c;
        }

        /// <summary>Dot-matrix LED board: panels alternate team colours with scrolling-style chevrons.</summary>
        private static void DrawLedBoard(PixelCanvas c, int w, int top, RgbColor teamA, RgbColor teamB)
        {
            var frameHi = RgbColor.FromHex("#3A3A4E");
            var body = RgbColor.FromHex("#0B0B14");
            var frameLo = RgbColor.FromHex("#05050A");
            for (int x = 0; x < w; x++)
            {
                c.Set(x, top, frameHi);
                for (int r = 1; r < BoardHeight; r++) c.Set(x, top - r, r >= BoardHeight - 2 ? frameLo : body);
            }
            const int panel = 40;
            for (int x = 0; x < w; x++)
            {
                int p = x / panel;
                var team = (p & 1) == 0 ? teamA : teamB;
                if (team.Luminance < 0.05) team = team.Lighten(0.35f);
                int lx = x % panel;
                if (lx == 0) continue; // panel seam
                for (int r = 3; r <= 10; r++)
                {
                    int y = top - r;
                    if (((x + r) & 1) != 0) continue; // dot matrix: every other dot
                    float mid = Math.Abs(r - 6.5f);
                    bool chevron = (((lx - (int)(mid * 1.5f)) % 10) + 10) % 10 < 3;
                    bool word = r >= 5 && r <= 8 && lx >= 24 && lx <= 35 && ((lx - 24) % 4 != 3);
                    c.Set(x, y, chevron || word ? team.Lighten(0.2f) : team.Darken(0.72f));
                }
            }
        }

        /// <summary>Outdoor courts: a low concrete wall with a painted stripe in the court's paint colour.</summary>
        private static void DrawWall(PixelCanvas c, int w, int top, CourtDef court)
        {
            var concrete = RgbColor.FromHex("#6E6A72");
            var cap = concrete.Lighten(0.2f);
            var dark = concrete.Darken(0.35f);
            for (int x = 0; x < w; x++)
            {
                c.Set(x, top, cap);
                c.Set(x, top - 1, cap.Darken(0.08f));
                for (int r = 2; r < BoardHeight; r++)
                {
                    uint h = Hash((uint)x, (uint)r);
                    var px = (h & 7) == 0 ? concrete.Darken(0.1f) : (h & 15) == 1 ? concrete.Lighten(0.06f) : concrete;
                    if (r >= 5 && r <= 8) px = RgbColor.Lerp(court.paint, px, (h & 3) == 0 ? 0.25f : 0.05f);
                    if (r >= BoardHeight - 2) px = dark;
                    c.Set(x, top - r, px);
                }
                if (x % 24 == 0) for (int r = 2; r < BoardHeight - 2; r++) c.Set(x, top - r, dark); // block joints
            }
        }

        /// <summary>
        /// A front-row fan seen from behind (10 x 16 art pixels at 2x, row 0 at the bottom): shoulders, neck, the
        /// back of the head (hair, a cap or a beanie, or bald) and, when cheering, both arms up.
        /// </summary>
        public static PixelCanvas FanBack(RgbColor shirt, RgbColor skin, bool armsUp, int variant)
        {
            int v = ((variant % 6) + 6) % 6;
            var hair = Hairs[(variant * 5 + 1 & 0x7fffffff) % Hairs.Length];
            var hat = Hats[(variant * 7 + 2 & 0x7fffffff) % Hats.Length];
            var c = new PixelCanvas(CrowdGenerator.Width * CrowdGenerator.ArtScale, CrowdGenerator.Height * CrowdGenerator.ArtScale);

            // Shoulders (light from the left, like the other fans).
            c.FillRect(0, 0, 10, 5, shirt);
            c.FillRect(1, 5, 8, 1, shirt);
            for (int y = 0; y < 6; y++) { c.Set(8, y, shirt.Darken(0.18f)); c.Set(9, y, shirt.Darken(0.28f)); c.Set(0, y, shirt.Lighten(0.12f)); }
            c.Set(3, 4, shirt.Darken(0.12f)); c.Set(6, 4, shirt.Darken(0.12f)); // shoulder blades
            c.Set(4, 6, skin.Darken(0.25f)); c.Set(5, 6, skin.Darken(0.25f));   // neck

            // Back of the head.
            for (int y = 7; y <= 13; y++)
                for (int x = 2; x <= 7; x++)
                {
                    bool corner = (y == 7 || y == 13) && (x == 2 || x == 7);
                    if (!corner) c.Set(x, y, skin);
                }
            c.Set(1, 10, skin.Darken(0.1f)); c.Set(8, 10, skin.Darken(0.2f)); // ears
            switch (v)
            {
                case 0: // short hair
                case 3:
                    for (int y = 8; y <= 13; y++) for (int x = 2; x <= 7; x++) if (!((y == 13) && (x == 2 || x == 7))) c.Set(x, y, hair);
                    for (int x = 3; x <= 6; x++) c.Set(x, 7, hair.Darken(0.2f));
                    c.Set(3, 12, hair.Lighten(0.18f));
                    break;
                case 1: // afro
                    c.FillCircle(5f, 11.5f, 4.4f, hair);
                    c.Set(3, 13, hair.Lighten(0.15f)); c.Set(2, 12, hair.Lighten(0.1f));
                    break;
                case 2: // cap: crown plus the strap at the back
                    for (int y = 10; y <= 13; y++) for (int x = 2; x <= 7; x++) if (!((y == 13) && (x == 2 || x == 7))) c.Set(x, y, hat);
                    for (int x = 3; x <= 6; x++) c.Set(x, 10, hat.Darken(0.3f));
                    c.Set(4, 10, skin); c.Set(5, 10, skin); // strap gap
                    for (int x = 3; x <= 6; x++) c.Set(x, 9, hair);
                    c.Set(3, 12, hat.Lighten(0.2f));
                    break;
                case 4: // beanie with a cuff
                    for (int y = 10; y <= 14; y++) for (int x = 2; x <= 7; x++) if (!((y == 14) && (x == 2 || x == 7))) c.Set(x, y, hat);
                    for (int x = 2; x <= 7; x++) c.Set(x, 10, hat.Darken(0.25f));
                    c.Set(4, 15, hat.Lighten(0.3f)); c.Set(5, 15, hat.Lighten(0.3f));
                    break;
                default: // bald / shaved, with a shine
                    c.Set(3, 12, skin.Lighten(0.3f)); c.Set(4, 12, skin.Lighten(0.18f));
                    for (int x = 2; x <= 7; x++) c.Set(x, 8, hair.WithAlpha(110));
                    break;
            }
            // Shade the right side of the head.
            for (int y = 7; y <= 13; y++) { var p = c.Get(7, y); if (p.a > 0) c.Set(7, y, p.Darken(0.18f).WithAlpha(255)); }

            if (armsUp)
            {
                for (int y = 4; y <= 14; y++)
                {
                    c.Set(0, y, y >= 6 ? skin : shirt);
                    c.Set(9, y, y >= 6 ? skin.Darken(0.15f) : shirt.Darken(0.28f));
                }
                c.Set(0, 15, skin); c.Set(9, 15, skin.Darken(0.15f)); // hands
            }
            return c;
        }

        /// <summary>Front-row fan positions (left edge, court pixels from the left of the near-side art).</summary>
        public static int[] FanSlots(int courtWidthPx, uint seed, float density)
        {
            int w = courtWidthPx + SidePad * 2;
            var list = new System.Collections.Generic.List<int>();
            for (int x = 3; x < w - CrowdGenerator.Width * 2 - 2; x += FanSpacing)
            {
                uint h = Hash((uint)x, seed);
                if ((h % 1000) / 1000f >= Math.Max(0.35f, density)) continue;
                list.Add(x + (int)((h >> 10) % 3));
            }
            return list.ToArray();
        }

        private static readonly RgbColor[] Hairs =
        {
            RgbColor.FromHex("#1B1410"), RgbColor.FromHex("#3B2416"), RgbColor.FromHex("#6B4423"),
            RgbColor.FromHex("#C9A050"), RgbColor.FromHex("#8A2E1C"), RgbColor.FromHex("#2A2A30"),
        };
        private static readonly RgbColor[] Hats =
        {
            RgbColor.FromHex("#E4572E"), RgbColor.FromHex("#4CC9F0"), RgbColor.FromHex("#FFD166"),
            RgbColor.FromHex("#F4F1DE"), RgbColor.FromHex("#7B2CBF"), RgbColor.FromHex("#06D6A0"),
        };

        private static uint Hash(uint x, uint y)
        {
            uint h = x * 374761393u + y * 668265263u;
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }
    }
}
