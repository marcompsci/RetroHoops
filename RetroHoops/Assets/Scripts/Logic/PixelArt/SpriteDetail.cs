using System;

namespace CallerRetroBall.Logic.PixelArt
{
    /// <summary>
    /// 13 Oct 2026 art pass: turns a 16 x 24 player frame (drawn without an outline) into a 32 x 48 sprite with
    /// smoothed edges (Scale2x), light from the upper left (a highlight on lit edges, a shade on the far edges),
    /// a thin outline tinted by the colour it borders (dark under the feet), finer eyes and a real jersey number.
    /// Pure pixel maths, so the result is the same on every device.
    /// </summary>
    public static class SpriteDetail
    {
        public const int Scale = 2;

        /// <summary>Where <see cref="CharacterSpriteGenerator"/> put things in the 1x frame.</summary>
        public struct FrameInfo
        {
            public bool Number;
            public int NumX, NumY, NumW;
            public int HeadLeft, HeadBottom, HeadW, HeadTop;
            /// <summary>16 Oct 2026 graphics phase G1: torso box (1x) for the detail pass.</summary>
            public int TorsoLeft, TorsoW, TorsoBottom, TorsoTop, ShortsBottom;
        }

        public const float ShadeAmount = 0.24f;
        public const float SoftShadeAmount = 0.12f;
        public const float LightAmount = 0.14f;

        public static PixelCanvas Render(PixelCanvas src, FrameInfo info, CharacterView view, RgbColor jersey, RgbColor trim, RgbColor skin, int number)
        {
            var big = Scale2x(src);
            var lit = Light(big, skin);
            ThinEyes(lit, src, info);
            if (info.Number && number >= 0) DrawNumber(lit, info, view, jersey, trim, number);
            return Outline(lit);
        }

        /// <summary>Shading, number and outline for a frame that's already at double size.</summary>
        public static PixelCanvas Finish(PixelCanvas big, FrameInfo info, CharacterView view, RgbColor jersey, RgbColor trim, RgbColor skin, int number)
        {
            var lit = Light(big, skin);
            if (info.Number && number >= 0) DrawNumber(lit, info, view, jersey, trim, number);
            return Outline(lit);
        }

        /// <summary>EPX / Scale2x: doubles the frame and rounds stair-step corners.</summary>
        public static PixelCanvas Scale2x(PixelCanvas s)
        {
            var d = new PixelCanvas(s.Width * 2, s.Height * 2);
            for (int y = 0; y < s.Height; y++)
                for (int x = 0; x < s.Width; x++)
                {
                    var p = s.Get(x, y);
                    var a = s.Get(x, y + 1); // above (canvas y grows upward)
                    var b = s.Get(x + 1, y);
                    var c = s.Get(x - 1, y);
                    var e = s.Get(x, y - 1); // below
                    var tl = c.Equals(a) && !c.Equals(e) && !a.Equals(b) ? a : p;
                    var tr = a.Equals(b) && !a.Equals(c) && !b.Equals(e) ? b : p;
                    var bl = e.Equals(c) && !e.Equals(b) && !c.Equals(a) ? c : p;
                    var br = b.Equals(e) && !b.Equals(a) && !e.Equals(c) ? e : p;
                    d.Set(2 * x, 2 * y + 1, tl);
                    d.Set(2 * x + 1, 2 * y + 1, tr);
                    d.Set(2 * x, 2 * y, bl);
                    d.Set(2 * x + 1, 2 * y, br);
                }
            return d;
        }

        /// <summary>Light from the upper left: lit edges of each colour area brighten, far edges darken.</summary>
        public static PixelCanvas Light(PixelCanvas s, RgbColor skin)
        {
            var d = new PixelCanvas(s.Width, s.Height);
            for (int y = 0; y < s.Height; y++)
                for (int x = 0; x < s.Width; x++)
                {
                    var p = s.Get(x, y);
                    if (p.a == 0) continue;
                    if (p.Luminance < 0.08f) { d.Set(x, y, p); continue; } // eyes and other ink stay put
                    // Rounded forms lit from the left: the right edge of each colour area falls into shade, the left edge catches light.
                    // Thin details (under three pixels across) keep their colour.
                    bool right = !s.Get(x + 1, y).Equals(p) && s.Get(x - 1, y).Equals(p) && s.Get(x - 2, y).Equals(p);
                    bool left = !s.Get(x - 1, y).Equals(p) && s.Get(x + 1, y).Equals(p) && s.Get(x + 2, y).Equals(p);
                    RgbColor o;
                    if (right) o = p.Darken(ShadeAmount);
                    else if (left) o = Lighten(p, p.Equals(skin) ? LightAmount * 0.8f : LightAmount);
                    else o = p;
                    d.Set(x, y, o);
                }
            return d;
        }

        /// <summary>Eyes become 1 x 2 instead of 2 x 2 at double size, which reads as a face, not a mask.</summary>
        private static void ThinEyes(PixelCanvas d, PixelCanvas src, FrameInfo info)
        {
            if (info.HeadW <= 0) return;
            for (int y = info.HeadBottom; y <= info.HeadTop; y++)
                for (int x = info.HeadLeft; x < info.HeadLeft + info.HeadW; x++)
                {
                    var p = src.Get(x, y);
                    if (p.a == 0 || p.Luminance >= 0.08f) continue;
                    // Is this an eye (a dark pixel with skin on both sides)? Then keep only its inner column.
                    var l = src.Get(x - 1, y);
                    var r = src.Get(x + 1, y);
                    if (l.a == 0 || r.a == 0 || l.Luminance < 0.08f || r.Luminance < 0.08f) continue;
                    bool leftHalf = x < info.HeadLeft + info.HeadW / 2;
                    int keep = leftHalf ? 2 * x + 1 : 2 * x;
                    int clear = leftHalf ? 2 * x : 2 * x + 1;
                    var fill = d.Get(clear + (leftHalf ? -1 : 1), 2 * y);
                    if (fill.a == 0) continue;
                    d.Set(clear, 2 * y, fill);
                    d.Set(clear, 2 * y + 1, fill);
                    d.Set(keep, 2 * y + 1, new RgbColor(0xFF, 0xFF, 0xFF)); // catchlight on top
                    d.Set(keep, 2 * y, p);
                }
        }

        private static readonly string[] Digits =
        {
            "111101101101111", "010110010010111", "111001111100111", "111001111001111", "101101111001001",
            "111100111001111", "111100111101111", "111001010010010", "111101111101111", "111101111001111",
        };

        /// <summary>The player's number on the back (and on the chest for number kits), in the trim colour.</summary>
        private static void DrawNumber(PixelCanvas d, FrameInfo info, CharacterView view, RgbColor jersey, RgbColor trim, int number)
        {
            if (view == CharacterView.Side) return;
            number = Math.Max(0, Math.Min(99, number));
            var ink = RgbColor.Distance(trim, jersey) > 80 ? trim : (jersey.Luminance > 0.45f ? new RgbColor(0x14, 0x14, 0x20) : RgbColor.White);
            // Clear the 1x number block (3 x 3 at the torso centre) back to the jersey colour.
            int cx = CharacterSpriteGenerator.FrameWidth / 2;
            for (int y = info.NumY * 2; y < (info.NumY + 3) * 2; y++)
                for (int x = (cx - 1) * 2; x < (cx + 2) * 2; x++)
                    if (d.Get(x, y).a > 0) d.Set(x, y, jersey);
            string text = number.ToString();
            int w = text.Length * 3 + (text.Length - 1);
            int left = cx * 2 - w / 2;
            int top = info.NumY * 2 + (view == CharacterView.Front ? 4 : 5); // front: one row lower, clear of the collar
            // G1: a one-pixel drop shadow down and right of each digit so the number reads like stitched twill.
            var drop = jersey.Darken(0.32f);
            for (int i = 0; i < text.Length; i++)
            {
                var g = Digits[text[i] - '0'];
                for (int row = 0; row < 5; row++)
                    for (int col = 0; col < 3; col++)
                        if (g[row * 3 + col] == '1')
                        {
                            int x = left + i * 4 + col + 1, y = top - row - 1;
                            if (d.Get(x, y).Equals(jersey)) d.Set(x, y, drop);
                        }
            }
            for (int i = 0; i < text.Length; i++)
            {
                var g = Digits[text[i] - '0'];
                for (int row = 0; row < 5; row++)
                    for (int col = 0; col < 3; col++)
                        if (g[row * 3 + col] == '1')
                        {
                            int x = left + i * 4 + col, y = top - row;
                            if (d.Get(x, y).a > 0) d.Set(x, y, ink);
                        }
            }
        }

        /// <summary>1px outline around the sprite, tinted by the colour it touches; solid dark below (where it meets the floor).</summary>
        public static PixelCanvas Outline(PixelCanvas s)
        {
            var d = new PixelCanvas(s.Width, s.Height);
            var ink = CharacterSpriteGenerator.Outline;
            for (int y = 0; y < s.Height; y++)
                for (int x = 0; x < s.Width; x++)
                {
                    var p = s.Get(x, y);
                    if (p.a > 0) { d.Set(x, y, p); continue; }
                    var up = s.Get(x, y + 1);
                    var l = s.Get(x - 1, y);
                    var r = s.Get(x + 1, y);
                    var dn = s.Get(x, y - 1);
                    if (up.a > 0) { d.Set(x, y, ink); continue; } // under something: full dark
                    RgbColor? n = r.a > 0 ? r : l.a > 0 ? l : dn.a > 0 ? dn : (RgbColor?)null;
                    if (n.HasValue) d.Set(x, y, Mix(ink, n.Value.Darken(0.55f), 0.4f));
                }
            return d;
        }

        public static RgbColor Lighten(RgbColor c, float t) =>
            new RgbColor((byte)(c.r + (255 - c.r) * t), (byte)(c.g + (255 - c.g) * t), (byte)(c.b + (255 - c.b) * t), c.a);

        public static RgbColor Mix(RgbColor a, RgbColor b, float t) =>
            new RgbColor((byte)(a.r + (b.r - a.r) * t), (byte)(a.g + (b.g - a.g) * t), (byte)(a.b + (b.b - a.b) * t), 255);
    }
}
