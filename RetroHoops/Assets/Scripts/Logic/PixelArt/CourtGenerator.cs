using System;

namespace CallerRetroBall.Logic.PixelArt
{
    /// <summary>
    /// Renders the half court as a single top-down pixel texture: crowd strip behind the
    /// baseline, floor (asphalt or hardwood), paint, and all line markings. The hoop is a
    /// separate sprite so the ball can pass in front of or behind it.
    ///
    /// Texture space maps from court space with a fixed margin; see <see cref="CourtToPixel"/>.
    /// </summary>
    public static class CourtGenerator
    {
        public const int PixelsPerMeter = 16;
        /// <summary>Extra floor shown beside each sideline (m).</summary>
        public const float SideMargin = 1.0f;
        /// <summary>Crowd/wall strip shown behind the baseline (m).</summary>
        public const float BaselineMargin = 2.0f;
        /// <summary>Extra floor beyond the top of the playing area (m).</summary>
        public const float TopMargin = 1.0f;

        public static int TextureWidth(CourtGeometry g) => (int)Math.Round((g.width + SideMargin * 2f) * PixelsPerMeter);
        public static int TextureHeight(CourtGeometry g) => (int)Math.Round((g.depth + BaselineMargin + TopMargin) * PixelsPerMeter);

        /// <summary>
        /// Court space → texture pixel. The baseline is near the TOP of the texture (hoop at the
        /// top of the screen); texture row 0 is the bottom row.
        /// </summary>
        public static void CourtToPixel(CourtGeometry g, Vec2 p, out float px, out float py)
        {
            px = (p.x + g.HalfWidth + SideMargin) * PixelsPerMeter;
            py = (g.depth + TopMargin - p.y) * PixelsPerMeter;
        }

        /// <summary>Texture pivot (0..1) that sits exactly on court origin (centre of the baseline).</summary>
        public static void OriginPivot(CourtGeometry g, out float pivotX, out float pivotY)
        {
            CourtToPixel(g, Vec2.Zero, out float px, out float py);
            pivotX = px / TextureWidth(g);
            pivotY = py / TextureHeight(g);
        }

        public static PixelCanvas Generate(CourtDef court, CourtGeometry g, uint seed) => Generate(court, g, seed, null, null);

        /// <summary>With an optional two-colour court banner (cosmetic) hung over the crowd.</summary>
        public static PixelCanvas Generate(CourtDef court, CourtGeometry g, uint seed, RgbColor? bannerA, RgbColor? bannerB) =>
            Generate(court, g, seed, bannerA, bannerB, true);

        /// <param name="drawPeople">False leaves the stands empty for the animated crowd (matches).</param>
        public static PixelCanvas Generate(CourtDef court, CourtGeometry g, uint seed, RgbColor? bannerA, RgbColor? bannerB, bool drawPeople)
        {
            if (court == null) throw new ArgumentNullException(nameof(court));
            int w = TextureWidth(g), h = TextureHeight(g);
            var c = new PixelCanvas(w, h);
            var rng = new SeededRandom(seed);
            var style = GameplayFx.StyleOf(court);
            bool hardwood = style == FloorStyle.Hardwood;
            bool grid = style == FloorStyle.NeonGrid;

            // Floor
            var floorDark = court.floor.Darken(0.12f);
            var floorLight = court.floor.Lighten(0.06f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    RgbColor col;
                    if (grid)
                    {
                        // Neon grid: glowing lines every 8 pixels on a dark floor.
                        bool lineX = x % 8 == 0, lineY = y % 8 == 0;
                        col = lineX && lineY ? court.paint.Lighten(0.3f) : (lineX || lineY ? court.paint.Darken(0.35f) : court.floor);
                    }
                    else if (hardwood)
                    {
                        // 13 Oct 2026 art pass: narrow maple boards in three tones, staggered butt joints and long grain streaks.
                        int plank = x / 4;
                        uint ph = Hash((uint)plank * 2654435761u ^ seed);
                        int joint = 28 + (int)(ph % 24);
                        bool seam = x % 4 == 0 || (y + (int)(ph >> 8) % joint) % joint == 0;
                        int tone = (int)(ph % 3);
                        col = tone == 0 ? court.floor : (tone == 1 ? floorLight : court.floor.Darken(0.05f));
                        if (seam) col = floorDark;
                        else if (Hash((uint)(x * 7919) ^ (uint)(y / 5) * 104729u ^ seed) % 29 == 0) col = col.Darken(0.07f);
                    }
                    else if (style == FloorStyle.Tiles)
                    {
                        // Big two-tone tiles with thin grout.
                        bool grout = x % 12 == 0 || y % 12 == 0;
                        col = grout ? floorDark : (((x / 12) + (y / 12)) % 2 == 0 ? court.floor : floorLight);
                    }
                    else if (style == FloorStyle.Rubber)
                    {
                        // Poured rubber: solid colour with confetti flecks in the paint and line colours.
                        uint hsh = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ seed;
                        int fleck = (int)(hsh % 61);
                        col = fleck == 0 ? court.paint : (fleck == 1 ? court.lines.Darken(0.2f) : court.floor);
                    }
                    else
                    {
                        bool speck = ((x * 7 + y * 13) ^ (int)(seed & 0xFF)) % 9 == 0;
                        bool crack = ((x * 3 + y * 5) % 97) == 0;
                        col = speck || crack ? floorDark : court.floor;
                    }
                    c.Set(x, y, col);
                }

            FinishFloor(c, g, court, hardwood);

            // Paint (key) fill
            FillCourtRect(c, g, -g.paintWidth * 0.5f, 0f, g.paintWidth * 0.5f, g.paintLength, court.paint.WithAlpha(hardwood ? (byte)200 : (byte)150));

            if (court.logoMotif >= 0) DrawCenterLogo(c, g, court);

            // Lines
            var line = court.lines;
            float hw = g.HalfWidth;
            CourtLine(c, g, new Vec2(-hw, 0f), new Vec2(hw, 0f), line);            // baseline
            CourtLine(c, g, new Vec2(-hw, 0f), new Vec2(-hw, g.depth), line);      // sidelines
            CourtLine(c, g, new Vec2(hw, 0f), new Vec2(hw, g.depth), line);
            CourtLine(c, g, new Vec2(-hw, g.depth), new Vec2(hw, g.depth), line);  // top of playing area
            float pw = g.paintWidth * 0.5f;
            CourtLine(c, g, new Vec2(-pw, 0f), new Vec2(-pw, g.paintLength), line);
            CourtLine(c, g, new Vec2(pw, 0f), new Vec2(pw, g.paintLength), line);
            CourtLine(c, g, new Vec2(-pw, g.paintLength), new Vec2(pw, g.paintLength), line);
            CourtArc(c, g, new Vec2(0f, g.paintLength), g.freeThrowCircleRadius, 0f, 360f, line);
            CourtArc(c, g, g.Hoop, 1.25f, -90f, 90f, line); // restricted area

            // Arc line + straight corners
            float cornerX = g.CornerLineX, cornerTop = g.CornerLineTopY;
            CourtLine(c, g, new Vec2(-cornerX, 0f), new Vec2(-cornerX, cornerTop), line);
            CourtLine(c, g, new Vec2(cornerX, 0f), new Vec2(cornerX, cornerTop), line);
            float edge = (float)(Math.Asin(cornerX / g.arcRadius) * 180.0 / Math.PI);
            CourtArc(c, g, g.Hoop, g.arcRadius, -edge, edge, line);

            // Centre-top logo circle segment (decor, original)
            CourtArc(c, g, new Vec2(0f, g.depth), 1.8f, 90f, 270f, line);

            LineShadows(c, g, court.lines);
            DrawCrowd(c, g, court, rng, drawPeople);
            if (court.theme != HolidayTheme.None) DrawHoliday(c, g, court, seed);
            if (bannerA.HasValue) DrawBanner(c, g, bannerA.Value, bannerB ?? bannerA.Value);
            return c;
        }

        // ------------------------------------------------------------------ 13 Oct 2026 art pass: floor finish

        private static uint Hash(uint h)
        {
            h ^= h >> 16; h *= 0x7feb352d; h ^= h >> 15; h *= 0x846ca68b; h ^= h >> 16;
            return h;
        }

        /// <summary>
        /// Lights the floor like an arena: a team-tinted apron outside the lines, a soft pool of light around the key
        /// that fades toward the corners, and (on hardwood) a varnish shine near the hoop. Gradients are dithered
        /// into three steps so it stays crisp pixel art.
        /// </summary>
        private static void FinishFloor(PixelCanvas c, CourtGeometry g, CourtDef court, bool hardwood)
        {
            CourtToPixel(g, new Vec2(-g.HalfWidth, 0f), out float xl, out float yBase);
            CourtToPixel(g, new Vec2(g.HalfWidth, g.depth), out float xr, out float yTop);
            CourtToPixel(g, new Vec2(0f, g.paintLength * 0.7f), out float lx, out float ly);
            int baseline = (int)Math.Round(yBase);
            float rx = c.Width * 0.62f, ry = baseline * 0.85f;
            for (int y = 0; y <= Math.Min(baseline, c.Height - 1); y++)
                for (int x = 0; x < c.Width; x++)
                {
                    var p = c.Get(x, y);
                    bool apron = x < xl - 0.5f || x > xr + 0.5f || y < yTop - 0.5f;
                    if (apron && hardwood) p = RgbColor.Lerp(p, court.paint.Darken(0.25f), 0.38f);
                    else if (apron) p = p.Darken(0.1f);
                    float dx = (x - lx) / rx, dy = (y - ly) / ry;
                    float d = dx * dx + dy * dy;                         // 0 at the light, ~1 at the corners
                    float v = 0.5f - d * 0.6f + (hardwood && d < 0.18f ? 0.35f * (1f - d / 0.18f) : 0f);
                    float th = PixelCanvas.BayerThreshold(x, y);
                    int step = v + th * 0.5f > 0.75f ? 1 : (v + th * 0.5f < 0.15f ? -1 : 0);
                    if (step > 0) p = p.Lighten(hardwood ? 0.07f : 0.05f);
                    else if (step < 0) p = p.Darken(0.09f);
                    c.Set(x, y, p);
                }
        }

        /// <summary>A one-pixel shadow under and right of every line pixel: painted lines sit on the floor.</summary>
        private static void LineShadows(PixelCanvas c, CourtGeometry g, RgbColor line)
        {
            CourtToPixel(g, Vec2.Zero, out _, out float baselinePy);
            int top = Math.Min(c.Height - 1, (int)Math.Round(baselinePy));
            var marks = new bool[c.Width * (top + 1)];
            for (int y = 0; y <= top; y++)
                for (int x = 0; x < c.Width; x++)
                    marks[y * c.Width + x] = c.Get(x, y).Equals(line);
            for (int y = 1; y <= top; y++)
                for (int x = 0; x < c.Width - 1; x++)
                {
                    if (!marks[y * c.Width + x]) continue;
                    int sx = x + 1, sy = y - 1;
                    if (marks[sy * c.Width + sx]) continue;
                    c.Set(sx, sy, c.Get(sx, sy).Darken(0.16f));
                }
        }

        // ------------------------------------------------------------------ holiday decorations

        private static readonly RgbColor HolidayRed = RgbColor.FromHex("#D62828");
        private static readonly RgbColor HolidayGreen = RgbColor.FromHex("#2D6A4F");
        private static readonly RgbColor HolidayGold = RgbColor.FromHex("#FFD166");
        private static readonly RgbColor Snow = RgbColor.FromHex("#F8FBFF");

        /// <summary>Christmas, Halloween, Easter and Fourth of July dressing: floor, lines, sidelines and stands.</summary>
        private static void DrawHoliday(PixelCanvas c, CourtGeometry g, CourtDef court, uint seed)
        {
            var rng = new SeededRandom(seed ^ 0x5EA5u);
            CourtToPixel(g, Vec2.Zero, out _, out float baselinePy);
            int baseline = (int)Math.Round(baselinePy);
            int wallBottom = baseline + 2, wallTop = c.Height - 1;
            int side = (int)Math.Round(SideMargin * PixelsPerMeter);
            int courtTop = (int)Math.Round(TopMargin * PixelsPerMeter);
            var line = court.lines;
            switch (court.theme)
            {
                case HolidayTheme.Christmas:
                    // Candy-cane lines, snow on the floor and in drifts along the sidelines, trees, string lights.
                    Restripe(c, line, 0, baseline + 1, 6, HolidayRed);
                    for (int i = 0; i < c.Width * baseline / 150; i++)
                    {
                        int x = rng.Range(0, c.Width), y = rng.Range(0, baseline);
                        if (!c.Get(x, y).Equals(line) && !c.Get(x, y).Equals(HolidayRed)) c.Set(x, y, Snow.WithAlpha(170));
                    }
                    for (int y = courtTop; y < baseline; y += 3)
                    {
                        c.FillRect(0, y, 3 + rng.Range(0, 4), 3, Snow);
                        int w = 3 + rng.Range(0, 4);
                        c.FillRect(c.Width - w, y, w, 3, Snow);
                    }
                    for (int y = courtTop + 20; y < baseline - 12; y += 48)
                    {
                        Tree(c, side / 2, y);
                        Tree(c, c.Width - side / 2 - 1, y + 24);
                    }
                    Lights(c, wallTop - 2, new[] { HolidayRed, HolidayGreen, HolidayGold, RgbColor.FromHex("#4CC9F0") });
                    for (int i = 0; i < 40; i++) c.Set(rng.Range(0, c.Width), rng.Range(wallBottom + 26, wallTop - 4), Snow);
                    break;

                case HolidayTheme.Halloween:
                    // Orange moon, bats over the stands, jack-o'-lanterns down both sidelines.
                    c.FillCircle(22f, wallTop - 9f, 7.5f, RgbColor.FromHex("#FFB347"));
                    c.FillCircle(25f, wallTop - 7f, 2.5f, RgbColor.FromHex("#F4A13C"));
                    for (int i = 0; i < 9; i++) Bat(c, 40 + i * 24 + rng.Range(-4, 5), wallTop - 4 - rng.Range(0, 8));
                    for (int y = courtTop + 8; y < baseline - 6; y += 30)
                    {
                        Pumpkin(c, side / 2, y);
                        Pumpkin(c, c.Width - side / 2 - 1, y + 15);
                    }
                    Restripe(c, line, 0, baseline + 1, 8, RgbColor.FromHex("#7B2CBF"));
                    break;

                case HolidayTheme.Easter:
                    // Flowers in the grass, painted eggs on the sidelines and baseline, pastel bunting.
                    RgbColor[] pastel = { RgbColor.FromHex("#F4A6C0"), RgbColor.FromHex("#BDE0FE"), RgbColor.FromHex("#FFF1A8"),
                                          RgbColor.FromHex("#CDB4DB"), RgbColor.FromHex("#B9FBC0") };
                    for (int i = 0; i < c.Width * baseline / 60; i++)
                    {
                        int x = rng.Range(0, c.Width), y = rng.Range(0, baseline);
                        if (x >= side && x < c.Width - side && rng.NextFloat() < 0.7f) continue; // mostly off the playing floor
                        c.Set(x, y, pastel[rng.Range(0, pastel.Length)]);
                    }
                    for (int y = courtTop + 6; y < baseline - 8; y += 22)
                    {
                        Egg(c, side / 2 - 2, y, pastel[(y / 22) % pastel.Length], pastel[(y / 22 + 2) % pastel.Length]);
                        Egg(c, c.Width - side / 2 - 2, y + 11, pastel[(y / 22 + 1) % pastel.Length], pastel[(y / 22 + 3) % pastel.Length]);
                    }
                    for (int x = side + 6; x < c.Width - side - 6; x += 26) Egg(c, x, baseline - 9, pastel[(x / 26) % pastel.Length], RgbColor.White);
                    Bunting(c, wallTop - 1, pastel);
                    break;

                case HolidayTheme.FourthOfJuly:
                    // Stars in the paint, red-and-white striped lines, fireworks and bunting over the stands.
                    Restripe(c, line, 0, baseline + 1, 4, HolidayRed);
                    CourtToPixel(g, new Vec2(-g.paintWidth * 0.5f, g.paintLength), out float pl, out float pb);
                    CourtToPixel(g, new Vec2(g.paintWidth * 0.5f, 0f), out float pr, out float pt);
                    for (int y = (int)pb + 4; y < (int)pt - 3; y += 9)
                        for (int x = (int)pl + 5 + ((y / 9) % 2) * 5; x < (int)pr - 4; x += 10) Star(c, x, y, RgbColor.White);
                    RgbColor[] fire = { HolidayRed, RgbColor.White, RgbColor.FromHex("#4CC9F0"), HolidayGold };
                    for (int i = 0; i < 6; i++)
                        Burst(c, 20 + i * (c.Width - 40) / 5, wallTop - 8 - rng.Range(0, 6), 5 + rng.Range(0, 3), fire[i % fire.Length]);
                    Bunting(c, wallTop - 1, new[] { HolidayRed, RgbColor.White, RgbColor.FromHex("#1D4ED8") });
                    break;
            }
        }

        /// <summary>Recolours every <paramref name="period"/>-pixel half of the court lines (candy-cane / stars-and-stripes).</summary>
        private static void Restripe(PixelCanvas c, RgbColor line, int y0, int y1, int period, RgbColor alt)
        {
            for (int y = Math.Max(0, y0); y < Math.Min(c.Height, y1); y++)
                for (int x = 0; x < c.Width; x++)
                    if (c.Get(x, y).Equals(line) && (x + y) % period < period / 2) c.Set(x, y, alt);
        }

        private static void Tree(PixelCanvas c, int cx, int y)
        {
            for (int row = 0; row < 9; row++)
            {
                int half = (9 - row) / 2;
                c.FillRect(cx - half, y + 2 + row, half * 2 + 1, 1, HolidayGreen);
            }
            c.FillRect(cx, y, 1, 2, RgbColor.FromHex("#7F5539"));
            c.Set(cx, y + 11, HolidayGold);
            c.Set(cx - 1, y + 5, HolidayRed);
            c.Set(cx + 2, y + 4, HolidayGold);
        }

        private static void Pumpkin(PixelCanvas c, int cx, int y)
        {
            c.FillCircle(cx + 0.5f, y + 3.5f, 3.6f, RgbColor.FromHex("#FF8C1A"));
            c.FillRect(cx, y + 7, 1, 2, HolidayGreen);
            var face = RgbColor.FromHex("#FFE066");
            c.Set(cx - 1, y + 4, face);
            c.Set(cx + 2, y + 4, face);
            c.FillRect(cx - 1, y + 2, 4, 1, face);
        }

        private static void Bat(PixelCanvas c, int x, int y)
        {
            var ink = RgbColor.FromHex("#C77DFF");
            c.FillRect(x + 2, y, 2, 2, ink);
            c.Set(x, y + 1, ink); c.Set(x + 1, y, ink);
            c.Set(x + 4, y, ink); c.Set(x + 5, y + 1, ink);
        }

        private static void Egg(PixelCanvas c, int x, int y, RgbColor shell, RgbColor stripe)
        {
            c.FillRect(x + 1, y, 3, 6, shell);
            c.FillRect(x, y + 1, 5, 3, shell);
            c.FillRect(x, y + 2, 5, 1, stripe);
        }

        private static void Star(PixelCanvas c, int x, int y, RgbColor col)
        {
            c.Set(x, y + 2, col);
            c.FillRect(x - 2, y + 1, 5, 1, col);
            c.Set(x, y + 1, col);
            c.Set(x - 1, y, col);
            c.Set(x + 1, y, col);
        }

        private static void Burst(PixelCanvas c, int cx, int cy, int r, RgbColor col)
        {
            for (int k = 0; k < 12; k++)
            {
                double a = k * Math.PI / 6.0;
                for (int d = 2; d <= r; d += 1 + (d % 2))
                    c.Set(cx + (int)Math.Round(Math.Cos(a) * d), cy + (int)Math.Round(Math.Sin(a) * d), col);
            }
            c.Set(cx, cy, RgbColor.White);
        }

        private static void Lights(PixelCanvas c, int y, RgbColor[] colors)
        {
            var wire = RgbColor.FromHex("#1B4332");
            for (int x = 0; x < c.Width; x++) c.Set(x, y + ((x / 8) % 2 == 0 ? 0 : -1), wire);
            for (int x = 3, i = 0; x < c.Width; x += 8, i++) c.FillRect(x, y - 2, 2, 2, colors[i % colors.Length]);
        }

        private static void Bunting(PixelCanvas c, int top, RgbColor[] colors)
        {
            for (int x = 0, i = 0; x < c.Width; x += 8, i++)
                for (int row = 0; row < 4; row++)
                    c.FillRect(x + row, top - row, 7 - row * 2, 1, colors[i % colors.Length]);
        }

        // ------------------------------------------------------------------ full court

        /// <summary>
        /// Full Court art: two halves back to back (the bottom half turned 180°) with stands behind both
        /// baselines. Court origin (centre of the TOP baseline) sits at <see cref="FullCourtOriginPivot"/>;
        /// the court runs 2 × <paramref name="half"/>.depth down the screen.
        /// </summary>
        public static PixelCanvas GenerateFullCourt(CourtDef court, CourtGeometry half, uint seed, RgbColor? bannerA, RgbColor? bannerB, bool drawPeople)
        {
            var h = Generate(court, half, seed, bannerA, bannerB, drawPeople);
            // The bottom stands are drawn without people (they'd be upside down); the wall pattern stays.
            var hb = drawPeople ? Generate(court, half, seed, null, null, false) : h;
            int keep = h.Height - TopRows;
            var full = new PixelCanvas(h.Width, keep * 2);
            for (int y = 0; y < keep; y++)
                for (int x = 0; x < h.Width; x++)
                {
                    // Top half: the half court as drawn (its rows from the half-court line up).
                    full.Set(x, keep + y, h.Get(x, TopRows + y));
                    // Bottom half: the same half turned 180° about the court's centre line and half-court line.
                    full.Set(x, y, hb.Get(Math.Min(h.Width - 1, h.Width - x), Math.Min(h.Height - 1, h.Height - y)));
                }
            return full;
        }

        /// <summary>Texture rows above the half-court line in a half texture (dropped in the full court).</summary>
        private static int TopRows => (int)Math.Round(TopMargin * PixelsPerMeter);

        public static int FullCourtTextureHeight(CourtGeometry half) => (TextureHeight(half) - TopRows) * 2;

        /// <summary>Pivot (0..1) on the court origin (centre of the top baseline) for the full-court texture.</summary>
        public static void FullCourtOriginPivot(CourtGeometry half, out float pivotX, out float pivotY)
        {
            CourtToPixel(half, Vec2.Zero, out float px, out float py);
            int keep = TextureHeight(half) - TopRows;
            pivotX = px / TextureWidth(half);
            pivotY = (py - TopRows + keep) / (keep * 2f);
        }

        private static void FillCourtRect(PixelCanvas c, CourtGeometry g, float x0, float y0, float x1, float y1, RgbColor col)
        {
            CourtToPixel(g, new Vec2(x0, y1), out float ax, out float ay);
            CourtToPixel(g, new Vec2(x1, y0), out float bx, out float by);
            int left = (int)Math.Round(ax), right = (int)Math.Round(bx);
            int bottom = (int)Math.Round(ay), top = (int)Math.Round(by);
            c.FillRect(left, bottom, right - left, top - bottom, col);
        }

        private static void CourtLine(PixelCanvas c, CourtGeometry g, Vec2 a, Vec2 b, RgbColor col)
        {
            CourtToPixel(g, a, out float ax, out float ay);
            CourtToPixel(g, b, out float bx, out float by);
            c.Line(Clamp((int)Math.Round(ax), c.Width), Clamp((int)Math.Round(ay), c.Height),
                   Clamp((int)Math.Round(bx), c.Width), Clamp((int)Math.Round(by), c.Height), col);
        }

        private static int Clamp(int v, int size) => v < 0 ? 0 : (v >= size ? size - 1 : v);

        /// <summary>
        /// Arc in court space. Angles are degrees measured from +y (toward half-court),
        /// clockwise toward +x, so -90..90 is the half facing away from the baseline.
        /// </summary>
        private static void CourtArc(PixelCanvas c, CourtGeometry g, Vec2 center, float radius, float fromDeg, float toDeg, RgbColor col)
        {
            int steps = Math.Max(16, (int)(radius * PixelsPerMeter * 8f * (toDeg - fromDeg) / 360f));
            for (int i = 0; i <= steps; i++)
            {
                double a = (fromDeg + (toDeg - fromDeg) * i / steps) * Math.PI / 180.0;
                var p = new Vec2(center.x + (float)Math.Sin(a) * radius, center.y + (float)Math.Cos(a) * radius);
                if (p.y < 0f) continue; // never draw behind the baseline
                CourtToPixel(g, p, out float px, out float py);
                c.Set((int)Math.Round(px), (int)Math.Round(py), col);
            }
        }

        private static void DrawBanner(PixelCanvas c, CourtGeometry g, RgbColor a, RgbColor b)
        {
            CourtToPixel(g, Vec2.Zero, out _, out float baselinePy);
            int y0 = (int)Math.Round(baselinePy) + 12;
            // Two banners either side of the backboard, with a zig-zag trim.
            foreach (int x0 in new[] { 20, c.Width - 20 - 64 })
            {
                c.FillRect(x0, y0, 64, 10, a);
                for (int x = 0; x < 64; x++)
                {
                    c.Set(x0 + x, y0, b);
                    if (x % 4 < 2) c.Set(x0 + x, y0 + 1, b);
                    c.Set(x0 + x, y0 + 9, b);
                }
                for (int x = 8; x < 56; x += 8) c.FillRect(x0 + x, y0 + 4, 4, 3, b);
            }
        }

        /// <summary>Pixel row (texture space) of the bottom of crowd row <paramref name="row"/> (0..2).</summary>
        public static int CrowdRowBottom(CourtGeometry g, int row)
        {
            CourtToPixel(g, Vec2.Zero, out _, out float baselinePy);
            return (int)Math.Round(baselinePy) + 2 + 2 + row * 8;
        }

        /// <summary>True where the cosmetic court banner hangs (fans there would cover it).</summary>
        public static bool BannerCovers(CourtGeometry g, int textureWidth, int x, int y, int w, int h)
        {
            CourtToPixel(g, Vec2.Zero, out _, out float baselinePy);
            int y0 = (int)Math.Round(baselinePy) + 12;
            foreach (int x0 in new[] { 20, textureWidth - 20 - 64 })
                if (x + w > x0 && x < x0 + 64 && y + h > y0 && y < y0 + 10) return true;
            return false;
        }

        /// <summary>Centre-court logo just inside the half-court line, blended into the floor.</summary>
        private static void DrawCenterLogo(PixelCanvas c, CourtGeometry g, CourtDef court)
        {
            var logo = LogoGenerator.Generate(court.logoShape, (LogoMotif)Math.Max(0, Math.Min(10, court.logoMotif)),
                                              court.logoColor, court.lines, court.floor.Darken(0.35f));
            CourtToPixel(g, new Vec2(0f, g.depth - 1.05f), out float cx, out float cy);
            int left = (int)Math.Round(cx) - LogoGenerator.Size / 2, bottom = (int)Math.Round(cy) - LogoGenerator.Size / 2;
            for (int y = 0; y < LogoGenerator.Size; y++)
                for (int x = 0; x < LogoGenerator.Size; x++)
                {
                    var p = logo.Get(x, y);
                    if (p.a == 0 || !c.InBounds(left + x, bottom + y)) continue;
                    c.Set(left + x, bottom + y, RgbColor.Lerp(c.Get(left + x, bottom + y), p, 0.85f));
                }
        }

        private static void DrawCrowd(PixelCanvas c, CourtGeometry g, CourtDef court, SeededRandom rng, bool drawPeople = true)
        {
            CourtToPixel(g, Vec2.Zero, out _, out float baselinePy);
            int start = (int)Math.Round(baselinePy) + 2;
            var wall = court.skyTop.Darken(0.35f);
            var wallLight = court.skyTop.Darken(0.15f);
            if (court.stands == StandsStyle.Brick)
            {
                var brick = RgbColor.FromHex("#8C3B2E");
                var mortar = RgbColor.FromHex("#C9B79C");
                for (int y = start; y < c.Height; y++)
                    for (int x = 0; x < c.Width; x++)
                    {
                        int row = (y - start) / 4;
                        bool joint = (y - start) % 4 == 0 || (x + (row % 2) * 4) % 8 == 0;
                        c.Set(x, y, joint ? mortar : (((x / 8) + row) % 3 == 0 ? brick.Darken(0.1f) : brick));
                    }
                return;
            }
            if (court.stands == StandsStyle.Fence)
            {
                // Sky behind a chain-link fence on posts.
                var post = RgbColor.FromHex("#58586A");
                var wire = RgbColor.FromHex("#A8B2BD");
                for (int y = start; y < c.Height; y++)
                    for (int x = 0; x < c.Width; x++)
                    {
                        float t = (y - start) / (float)Math.Max(1, c.Height - start);
                        var sky = RgbColor.Lerp(court.skyBottom, court.skyTop, t);
                        bool link = (x + y) % 6 == 0 || (x - y + 600) % 6 == 0;
                        bool isPost = x % 48 < 2 || y - start < 2;
                        c.Set(x, y, isPost ? post : (link ? wire : sky));
                    }
                return;
            }
            for (int y = start; y < c.Height; y++)
                for (int x = 0; x < c.Width; x++)
                    c.Set(x, y, (y - start) % 8 == 0 ? wallLight : wall);

            if (!drawPeople) return;
            // Seated silhouettes: heads + shoulders in muted sky colours.
            var person = court.skyBottom.Darken(0.55f);
            var personAlt = court.skyBottom.Darken(0.4f);
            for (int row = 0; row < 3; row++)
            {
                int baseY = start + 2 + row * 8;
                for (int x = 2; x < c.Width - 4; x += 5)
                {
                    if (!rng.Chance(court.crowdDensity)) continue;
                    var col = rng.Chance(0.5f) ? person : personAlt;
                    int jitter = rng.Range(-1, 2);
                    c.FillRect(x, baseY + jitter, 4, 3, col);        // shoulders
                    c.FillRect(x + 1, baseY + 3 + jitter, 2, 2, col); // head
                }
            }
        }
    }
}
