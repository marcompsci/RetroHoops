using System;

namespace CallerRetroBall.Logic.PixelArt
{
    /// <summary>
    /// Art pass 2: the ball and baskets at 2x detail (two art pixels per court pixel, like the players).
    /// A shaded, seamed ball that spins in flight; glass backboards; a diamond-mesh net that kicks on a make.
    /// Pivot constants (<see cref="HoopPivotX"/> etc.) stay in court pixels; sprites are <see cref="ArtScale"/>x.
    /// </summary>
    public static partial class PropSpriteGenerator
    {
        public const int ArtScale = 2;
        /// <summary>Spin frames: the seams cover half a turn, which repeats.</summary>
        public const int BallFrames = 8;
        /// <summary>
        /// Net frames (14 Oct 2026, Omari: "make the net move ... natural"). 0 is at rest. 1-3: the ball drops through
        /// (the mesh bulges around it, moving down, and stretches). 4: it whips back up, short and flared. 5-9: it
        /// settles, overshooting and swaying a little less each time.
        /// </summary>
        public const int NetFrames = 10;

        /// <summary>The shape of one net frame: length (art px), bottom half-width, the ball's bulge and sideways sway.</summary>
        public struct NetShape
        {
            public float Length, BottomHalf, BulgeAt, Bulge, Sway;
            public NetShape(float length, float bottomHalf, float bulgeAt = 0f, float bulge = 0f, float sway = 0f)
            { Length = length; BottomHalf = bottomHalf; BulgeAt = bulgeAt; Bulge = bulge; Sway = sway; }
        }

        public static readonly NetShape[] NetShapes =
        {
            new NetShape(11f, 6f),                        // 0 rest
            new NetShape(12f, 6.2f, 0.25f, 2.2f),         // 1 ball in the top of the net
            new NetShape(13f, 5.4f, 0.55f, 2.6f),         // 2 ball halfway: stretched, bulging
            new NetShape(14f, 4.6f, 0.9f, 2.4f),          // 3 ball at the bottom: longest
            new NetShape(8.5f, 8.6f, 0f, 0f, 1.6f),       // 4 whips back up and flares
            new NetShape(10f, 7.6f, 0f, 0f, -1.2f),       // 5
            new NetShape(12.2f, 5.6f, 0f, 0f, 0.9f),      // 6 drops past rest
            new NetShape(10.6f, 6.6f, 0f, 0f, -0.6f),     // 7
            new NetShape(11.4f, 5.8f, 0f, 0f, 0.35f),     // 8
            new NetShape(10.9f, 6.1f, 0f, 0f, -0.15f),    // 9 nearly still
        };

        /// <summary>When each net frame ends (seconds after the make); after the last, the net is at rest.</summary>
        public static readonly float[] NetTimes = { 0f, 0.04f, 0.085f, 0.13f, 0.19f, 0.26f, 0.34f, 0.43f, 0.53f, 0.66f };

        private static readonly RgbColor BallHi = RgbColor.FromHex("#FFD3A0");
        private static readonly RgbColor BallLight = RgbColor.FromHex("#F99A55");
        private static readonly RgbColor BallBase = RgbColor.FromHex("#EE7B30");
        private static readonly RgbColor BallMid = RgbColor.FromHex("#CF5F24");
        private static readonly RgbColor BallDark = RgbColor.FromHex("#93401A");
        private static readonly RgbColor Seam = RgbColor.FromHex("#3A1A0A");

        public static PixelCanvas Ball() => Ball(0);

        /// <summary>16 x 16 ball, lit from the top-left, seams rotated for spin frame <paramref name="frame"/>.</summary>
        public static PixelCanvas Ball(int frame)
        {
            const int n = 16;
            var c = new PixelCanvas(n, n);
            double ang = Mod(frame, BallFrames) * Math.PI / BallFrames;
            double cos = Math.Cos(ang), sin = Math.Sin(ang);
            const double cx = 8, cy = 8, radius = 7.1;
            double lx0 = -0.45, ly0 = 0.55, lz0 = 0.70;
            double ll = Math.Sqrt(lx0 * lx0 + ly0 * ly0 + lz0 * lz0);
            lx0 /= ll; ly0 /= ll; lz0 /= ll;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    double dx = (x + 0.5 - cx) / radius, dy = (y + 0.5 - cy) / radius;
                    double d2 = dx * dx + dy * dy;
                    if (d2 > 1.0) continue;
                    double nz = Math.Sqrt(1.0 - d2);
                    double lam = dx * lx0 + dy * ly0 + nz * lz0;
                    RgbColor col = lam > 0.93 ? BallHi : lam > 0.72 ? BallLight : lam > 0.40 ? BallBase : lam > 0.12 ? BallMid : BallDark;
                    if (d2 > 0.80 && lam < 0.72) col = col.Darken(0.12f); // the edge turns away from the light
                    // Backspin: the ball turns about the screen's x axis.
                    double px = dx, py = dy * cos + nz * sin;
                    bool seam = Math.Abs(px) < 0.085 || Math.Abs(py) < 0.085
                                || Math.Abs(Math.Abs(px) - (0.56 + 0.32 * py * py)) < 0.075;
                    if (seam) col = lam > 0.72 ? RgbColor.Lerp(Seam, BallBase, 0.35f) : Seam;
                    c.Set(x, y, col);
                }
            CharacterSpriteGenerator.AddOutline(c);
            return c;
        }

        /// <summary>Spin frame for the ball: turning while it flies or rolls, still in someone's hands.</summary>
        public static int BallSpinFrame(float time, bool spinning) =>
            spinning ? Mod((int)Math.Floor(time * 16f), BallFrames) : 0;

        /// <summary>Net frame <paramref name="secondsSinceMake"/> after a make (rest when negative or long after).</summary>
        public static int NetFrame(float secondsSinceMake)
        {
            if (secondsSinceMake < 0f) return 0;
            for (int f = 1; f < NetTimes.Length; f++)
                if (secondsSinceMake < NetTimes[f]) return f;
            return 0;
        }

        private static readonly RgbColor Glass = RgbColor.FromHex("#DCEAF5").WithAlpha(190);
        private static readonly RgbColor Frame = RgbColor.FromHex("#F4F6FA");
        private static readonly RgbColor FrameShade = RgbColor.FromHex("#8E97AE");
        private static readonly RgbColor Rim = RgbColor.FromHex("#E4572E");
        private static readonly RgbColor RimBack = RgbColor.FromHex("#B0401F");
        private static readonly RgbColor RimHi = RgbColor.FromHex("#FF9466");
        private static readonly RgbColor RimUnder = RgbColor.FromHex("#9A3218");
        private static readonly RgbColor Metal = RgbColor.FromHex("#5C6378");
        private static readonly RgbColor Pad = RgbColor.FromHex("#23233A");
        private static readonly RgbColor NetCol = RgbColor.FromHex("#FFFFFF").WithAlpha(232);
        private static readonly RgbColor NetShade = RgbColor.FromHex("#D5DCE8").WithAlpha(232);
        private static readonly RgbColor NetShadow = new RgbColor(0x14, 0x14, 0x20, 80);

        public static PixelCanvas Hoop() => Hoop(0);

        /// <summary>Backboard, rim and net from the 3/4 court camera: 64 x 60, rim centre at court pixel (16, 7).</summary>
        public static PixelCanvas Hoop(int netFrame)
        {
            const int w = 64, h = 60;
            var c = new PixelCanvas(w, h);
            var solid = new bool[w * h];

            // Glass backboard with a white frame, a shaded lower edge and two shine streaks.
            const int bx0 = 2, bx1 = 61, by0 = 28, by1 = 55;
            for (int y = by0; y <= by1; y++)
                for (int x = bx0; x <= bx1; x++)
                {
                    bool edge = x <= bx0 + 1 || x >= bx1 - 1 || y <= by0 + 1 || y >= by1 - 1;
                    RgbColor col;
                    if (edge) col = y <= by0 + 1 || x >= bx1 - 1 ? FrameShade : Frame;
                    else
                    {
                        int band = Mod(x - (int)(y * 0.8f), 26);
                        col = band < 3 || band == 6 ? RgbColor.Lerp(Glass, RgbColor.White.WithAlpha(190), 0.6f) : Glass;
                    }
                    Mark(c, solid, x, y, col);
                }
            // Shooter's square (2 px lines).
            for (int x = 22; x <= 43; x++)
                for (int t = 0; t < 2; t++) { Mark(c, solid, x, 30 + t, Rim); Mark(c, solid, x, 45 - t, Rim); }
            for (int y = 30; y <= 45; y++)
                for (int t = 0; t < 2; t++) { Mark(c, solid, 22 + t, y, Rim); Mark(c, solid, 43 - t, y, Rim); }

            // Padding along the bottom of the board, then the bracket down to the rim.
            for (int x = bx0; x <= bx1; x++)
                for (int y = 25; y <= 27; y++) Mark(c, solid, x, y, y == 27 ? Pad.Lighten(0.18f) : Pad);
            for (int y = 19; y <= 24; y++)
                for (int x = 30; x <= 35; x++) Mark(c, solid, x, y, x >= 34 ? Metal.Darken(0.25f) : x == 30 ? Metal.Lighten(0.2f) : Metal);

            const float cx = 33f, cy = 15f, rx = 12.5f, ry = 5f;
            DrawRim(c, solid, cx, cy, rx, ry, 2.0f, 1.8f, back: true);
            DrawNet(c, cx, (int)cy - 1, 11f, netFrame);
            DrawRim(c, solid, cx, cy, rx, ry, 2.0f, 1.8f, back: false);
            OutlineSolid(c, solid);
            return c;
        }

        public static PixelCanvas HoopSide() => HoopSide(0);

        /// <summary>
        /// The basket from the side (landscape Full Court), backboard on the LEFT: 52 x 60, rim centre at court
        /// pixel (<see cref="HoopSidePivotX"/>, <see cref="HoopSidePivotY"/>).
        /// </summary>
        public static PixelCanvas HoopSide(int netFrame)
        {
            const int w = 52, h = 60;
            var c = new PixelCanvas(w, h);
            var solid = new bool[w * h];

            // Board seen at an angle: a narrow glass panel with a frame and the square's edge.
            for (int y = 12; y <= 55; y++)
                for (int x = 6; x <= 13; x++)
                {
                    bool edge = x <= 7 || x >= 12 || y <= 13 || y >= 54;
                    var col = edge ? (x >= 12 || y <= 13 ? FrameShade : Frame) : Glass;
                    Mark(c, solid, x, y, col);
                }
            for (int y = 18; y <= 33; y++) { Mark(c, solid, 10, y, Rim); Mark(c, solid, 11, y, Rim); }
            // Support arm behind the board, bracket out to the rim.
            for (int x = 0; x <= 5; x++)
                for (int y = 28; y <= 31; y++) Mark(c, solid, x, y, y == 31 ? Metal.Lighten(0.2f) : Metal);
            for (int x = 14; x <= 20; x++) { Mark(c, solid, x, 17, Metal.Lighten(0.15f)); Mark(c, solid, x, 16, Metal.Darken(0.2f)); }

            float cx = HoopSidePivotX * ArtScale + 1f, cy = HoopSidePivotY * ArtScale + 1f;
            DrawRim(c, solid, cx, cy, 12.5f, 3.2f, 2.0f, 1.4f, back: true);
            DrawNet(c, cx, (int)cy - 2, 11f, netFrame);
            DrawRim(c, solid, cx, cy, 12.5f, 3.2f, 2.0f, 1.4f, back: false);
            OutlineSolid(c, solid);
            return c;
        }

        /// <summary>Half of the rim's ring: the back (above the centre line, behind the net) or the front.</summary>
        private static void DrawRim(PixelCanvas c, bool[] solid, float cx, float cy, float rx, float ry, float tx, float ty, bool back)
        {
            bool Outer(int x, int y) { float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry; return dx * dx + dy * dy <= 1f; }
            bool Inner(int x, int y) { float dx = (x + 0.5f - cx) / (rx - tx), dy = (y + 0.5f - cy) / (ry - ty); return dx * dx + dy * dy <= 1f; }
            for (int y = 0; y < c.Height; y++)
                for (int x = 0; x < c.Width; x++)
                {
                    if (!Outer(x, y) || Inner(x, y)) continue;
                    bool isBack = y + 0.5f > cy;
                    if (isBack != back) continue;
                    RgbColor col = back ? RimBack : Rim;
                    if (!back && Inner(x, y + 1)) col = RimHi;           // the lit top of the front tube
                    else if (!back && !Outer(x, y - 1)) col = RimUnder;  // its underside
                    Mark(c, solid, x, y, col);
                }
        }

        /// <summary>Diamond-mesh net hanging from <paramref name="topY"/>, shaped by <paramref name="frame"/> (see <see cref="NetShapes"/>).</summary>
        private static void DrawNet(PixelCanvas c, float cx, int topY, float topHalf, int frame)
        {
            var shape = NetShapes[Mod(frame, NetFrames)];
            int length = (int)Math.Round(shape.Length);
            var pts = new System.Collections.Generic.List<(int x, int y, bool rim)>();
            for (int r = 0; r <= length; r++)
            {
                float u = (float)r / length;
                // Tapers from the rim to the bottom loop, widens where the ball is, and the lower part swings
                // further than the top (it hangs from the rim).
                float half = topHalf + (shape.BottomHalf - topHalf) * (1f - (1f - u) * (1f - u));
                if (shape.Bulge > 0f)
                {
                    float d = (u - shape.BulgeAt) / 0.22f;
                    half += shape.Bulge * (float)Math.Exp(-d * d);
                }
                float mid = cx + shape.Sway * u * u;
                int y = topY - r;
                int l = (int)Math.Floor(mid - half), rr = (int)Math.Floor(mid + half - 0.01f);
                pts.Add((l, y, true));
                pts.Add((rr, y, true));
                if (r == length)
                {
                    for (int x = l; x <= rr; x++) pts.Add((x, y, true));
                    continue;
                }
                // Diamond mesh: two families of diagonal strands on a 4-pixel lattice that moves with the net,
                // clipped to the taper, so the holes stay open all the way down.
                int shift = (int)Math.Round(mid - cx);
                for (int x = l + 1; x <= rr - 1; x++)
                {
                    int u2 = x - shift - (int)cx;
                    if (Mod(u2 + r, 4) == 0 || Mod(u2 - r, 4) == 0)
                        pts.Add((x, y, Math.Abs(x + 0.5f - mid) > half * 0.75f));
                }
            }
            foreach (var p in pts)
                if (c.Get(p.x + 1, p.y - 1).a == 0) c.Set(p.x + 1, p.y - 1, NetShadow);
            foreach (var p in pts)
                c.Set(p.x, p.y, p.rim ? NetShade : NetCol);
        }

        private static void Mark(PixelCanvas c, bool[] solid, int x, int y, RgbColor col)
        {
            if (!c.InBounds(x, y)) return;
            c.Set(x, y, col);
            solid[y * c.Width + x] = true;
        }

        /// <summary>Dark outline around the board, rim and bracket only (the net stays light and airy).</summary>
        private static void OutlineSolid(PixelCanvas c, bool[] solid)
        {
            int w = c.Width;
            for (int y = 0; y < c.Height; y++)
                for (int x = 0; x < w; x++)
                {
                    if (solid[y * w + x] || c.Get(x, y).a >= 200) continue;
                    bool edge = (x > 0 && solid[y * w + x - 1]) || (x < w - 1 && solid[y * w + x + 1])
                                || (y > 0 && solid[(y - 1) * w + x]) || (y < c.Height - 1 && solid[(y + 1) * w + x]);
                    if (edge) c.Pixels[y * w + x] = CharacterSpriteGenerator.Outline;
                }
        }

        private static int Mod(int v, int m) => ((v % m) + m) % m;
    }
}
