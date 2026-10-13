using System;

namespace CallerRetroBall.Logic.PixelArt
{
    public static partial class CharacterSpriteGenerator
    {
        /// <summary>
        /// 13 Oct 2026 art pass: the head and hair drawn natively at double size (rounded skull and jaw, ears,
        /// eyes with brows, nose, mouth, six hairstyles with real shapes). Drawn only into empty pixels, so raised
        /// arms stay in front of the head.
        /// </summary>
        private static void DrawHead2x(PixelCanvas c, CharacterView view, int style, Palette p)
        {
            var i = _info;
            if (i.HeadW <= 0) return;
            int L = i.HeadLeft * 2, B = i.HeadBottom * 2, W = i.HeadW * 2, H = (i.HeadTop - i.HeadBottom + 1) * 2;
            int T = B + H - 1; // top row
            var skin = p.Skin;
            var shade = p.SkinShade;
            var hair = p.Hair;
            var ink = new RgbColor(0x1A, 0x14, 0x18);

            // Skull and jaw: a rounded block.
            for (int y = B; y <= T; y++)
            {
                int inset = y == B || y == T ? 2 : (y == B + 1 || y == T - 1 ? 1 : 0);
                for (int x = L + inset; x < L + W - inset; x++) Put(c, x, y, skin);
            }
            // Afro and locs go behind the head: drawn after it, into empty pixels only.
            if (style == 3)
            {
                float cx = L + W / 2f - 0.5f, cy = T - 2;
                Disc(c, cx, cy, W / 2f + 2.2f, hair);
            }
            // Locs hang behind the shoulders on the back and sides.
            if (style == 5)
                for (int x = L - 1; x <= L + W; x += 2)
                {
                    if (view == CharacterView.Front && x > L && x < L + W - 1) continue;
                    for (int y = B - 3; y <= T - 2; y++) Put(c, x, y, ((y + x) & 3) == 0 ? hair.Darken(0.25f) : hair);
                }

            int mid = L + W / 2; // first column of the right half
            if (view == CharacterView.Front)
            {
                // Ears, eyes with brows, nose, mouth.
                Put(c, L - 1, B + 3, shade); Put(c, L - 1, B + 4, shade);
                Put(c, L + W, B + 3, shade); Put(c, L + W, B + 4, shade);
                int eyeL = mid - 3, eyeR = mid + 2;
                Set(c, eyeL, B + 4, ink); Set(c, eyeL, B + 5, ink);
                Set(c, eyeR, B + 4, ink); Set(c, eyeR, B + 5, ink);
                Set(c, eyeL - 1, B + 6, hair.Darken(0.1f)); Set(c, eyeL, B + 7, hair.Darken(0.1f));
                Set(c, eyeR + 1, B + 6, hair.Darken(0.1f)); Set(c, eyeR, B + 7, hair.Darken(0.1f));
                Set(c, mid, B + 3, shade);
                Set(c, mid - 1, B + 1, shade.Darken(0.15f)); Set(c, mid, B + 1, shade.Darken(0.15f));
            }
            else if (view == CharacterView.Side)
            {
                // Facing right: nose, eye and brow at the front, ear at the back.
                Put(c, L + W, B + 3, skin); Put(c, L + W, B + 4, skin);
                Set(c, L + W - 3, B + 4, ink); Set(c, L + W - 3, B + 5, ink);
                Set(c, L + W - 3, B + 7, hair.Darken(0.1f)); Set(c, L + W - 2, B + 7, hair.Darken(0.1f));
                Set(c, L + W - 2, B + 1, shade.Darken(0.15f));
                Set(c, L + 3, B + 3, shade); Set(c, L + 3, B + 4, shade); Set(c, L + 4, B + 4, shade);
            }
            else
            {
                Put(c, L - 1, B + 3, shade); Put(c, L - 1, B + 4, shade);
                Put(c, L + W, B + 3, shade); Put(c, L + W, B + 4, shade);
            }

            // Hair.
            bool back = view == CharacterView.Back, side = view == CharacterView.Side;
            int capBottom = back ? B + 2 : T - (style == 0 ? 1 : 2);
            for (int y = capBottom; y <= T; y++)
            {
                int inset = y == T ? 2 : (y == T - 1 ? 1 : 0);
                int x0 = L + inset, x1 = L + W - 1 - inset;
                if (side && !back && y < T - 1) x1 = L + W / 2; // side view: hair over the back of the head only
                for (int x = x0; x <= x1; x++) Set(c, x, y, hair);
            }
            if (side) for (int y = B + 3; y < capBottom; y++) for (int x = L; x < L + 4; x++) Set(c, x, y, hair);
            switch (style)
            {
                case 0: // buzz: a fine edge of hair
                    if (!back) for (int x = L + 2; x < L + W - 2; x += 2) Set(c, x, T - 2, hair.Darken(0.15f));
                    break;
                case 1: // short with sideburns
                    if (!side && !back) { for (int y = B + 4; y < capBottom; y++) { Set(c, L, y, hair); Set(c, L + W - 1, y, hair); } }
                    for (int x = L + 1; x < L + W - 1; x++) Put(c, x, T + 1, hair);
                    break;
                case 2: // high-top fade: a tall flat top, faded sides
                    for (int y = T + 1; y <= T + 4; y++)
                        for (int x = L + 1; x < L + W - 1; x++) Put(c, x, y, y == T + 4 && (x == L + 1 || x == L + W - 2) ? RgbColor.Clear : hair);
                    if (!back) for (int y = T - 3; y < T - 1; y++) { Set(c, L, y, hair.Darken(0.2f)); Set(c, L + W - 1, y, hair.Darken(0.2f)); }
                    break;
                case 3: // afro: already behind; add volume on top
                    for (int x = L - 1; x <= L + W; x++) Put(c, x, T + 1, hair);
                    break;
                case 4: // headband across the forehead
                    {
                        int y0 = back ? T - 4 : T - 3;
                        for (int y = y0; y <= y0 + 1; y++)
                            for (int x = L - (side ? 0 : 1); x <= L + W - (side ? 1 : 0); x++)
                                if (c.Get(x, y).a > 0 || x == L - 1 || x == L + W) Set(c, x, y, p.Accent);
                        for (int x = L + 1; x < L + W - 1; x++) Put(c, x, T + 1, hair);
                    }
                    break;
                default: // locs: thick top
                    for (int x = L; x < L + W; x++) Put(c, x, T + 1, hair);
                    for (int x = L - 1; x <= L + W; x += 3) Put(c, x, T, hair);
                    break;
            }
        }

        /// <summary>Paints only where nothing is drawn yet (keeps raised arms in front).</summary>
        private static void Put(PixelCanvas c, int x, int y, RgbColor col)
        {
            if (!c.InBounds(x, y) || c.Get(x, y).a > 0) return;
            c.Set(x, y, col);
        }

        /// <summary>Paints over head pixels (skin or hair just drawn), but never over arms in front of the head.</summary>
        private static void Set(PixelCanvas c, int x, int y, RgbColor col)
        {
            if (!c.InBounds(x, y)) return;
            c.Set(x, y, col);
        }

        private static void Disc(PixelCanvas c, float cx, float cy, float r, RgbColor col)
        {
            for (int y = (int)(cy - r - 1); y <= (int)(cy + r + 1); y++)
                for (int x = (int)(cx - r - 1); x <= (int)(cx + r + 1); x++)
                {
                    float dx = x - cx, dy = y - cy;
                    if (dx * dx + dy * dy <= r * r) Put(c, x, y, col);
                }
        }
    }
}
