using System;

namespace CallerRetroBall.Logic.PixelArt
{
    public static partial class CharacterSpriteGenerator
    {
        /// <summary>
        /// 16 Oct 2026 graphics phase G1 (Omari: "the next 3 phases of the character and gameplay graphics"): a detail
        /// pass on the 2x frame, run after the head and before the lighting. It separates the arms from the jersey with
        /// a dark seam, pipes the tank-top armholes in the trim colour, adds cloth folds to the jersey and shorts, a
        /// darker shorts hem, crew socks above the shoes, and eye whites and cheeks on the face. Everything is pure
        /// pixel maths on known palette colours, so the sheet is identical on every device.
        /// </summary>
        private static void Polish(PixelCanvas c, CharacterView view, int frame, Palette p)
        {
            var i = _info;
            if (i.TorsoW <= 0) return;
            var kit = p.Kit;
            int L = i.TorsoLeft * 2, R = (i.TorsoLeft + i.TorsoW) * 2 - 1;   // torso columns (inclusive)
            int jB = i.TorsoBottom * 2, jT = i.TorsoTop * 2 + 1;              // jersey rows (inclusive)
            int sB = i.ShortsBottom * 2;
            int bodyTop = i.HeadBottom * 2 - 1;                               // nothing above this row is touched by the body pass
            int mid = FrameWidth;                                             // 2x centre column
            var seam = SpriteDetail.Mix(Outline, p.Skin.Darken(0.55f), 0.35f);

            // 1. Arm seams: a skin pixel whose neighbour towards the body is cloth gets a dark seam, so the arm reads as
            //    a separate limb in front of the jersey instead of a slab glued to it. Side view: the back edge only.
            var seamAt = new bool[c.Width * c.Height];
            for (int y = 0; y <= bodyTop && y < c.Height; y++)
                for (int x = 0; x < c.Width; x++)
                {
                    var px = c.Get(x, y);
                    if (!IsSkin(px, p)) continue;
                    bool hit;
                    if (view == CharacterView.Side) hit = IsCloth(c.Get(x - 1, y), p) && IsSkin(c.Get(x + 1, y), p);
                    else if (x < mid) hit = IsCloth(c.Get(x + 1, y), p) && IsSkin(c.Get(x - 1, y), p);
                    else hit = IsCloth(c.Get(x - 1, y), p) && IsSkin(c.Get(x + 1, y), p);
                    if (hit && y >= sB) seamAt[y * c.Width + x] = true;
                }
            for (int k = 0; k < seamAt.Length; k++)
                if (seamAt[k]) c.Set(k % c.Width, k / c.Width, seam);

            // 2. Tank tops: trim piping round the armholes (the two outer columns of the top three jersey rows).
            if (kit.Cut == JerseyCut.Tank && view != CharacterView.Side)
                for (int y = jT - 2; y <= jT; y++)
                {
                    if (c.Get(L, y).Equals(p.Jersey)) c.Set(L, y, p.Trim);
                    if (c.Get(R, y).Equals(p.Jersey)) c.Set(R, y, p.Trim);
                }

            // 3. Cloth folds: short creases at the jersey's sides just above the waist (kept clear of the number in the
            //    middle), shifting a row with the run cycle; the side view gets one diagonal across the torso.
            var fold = p.Jersey.Darken(0.14f);
            int phase = IsRunFrame(frame) ? (frame - IdleFrames) % 2 : 0;
            if (view == CharacterView.Side)
                for (int k = 0; k < 3; k++) Crease(c, L + 2 - k + 2, jB + 2 + k, p.Jersey, fold);
            else
            {
                Crease(c, L + 1, jB + 3 + phase, p.Jersey, fold); Crease(c, L + 2, jB + 2 + phase, p.Jersey, fold);
                Crease(c, R - 1, jB + 3 + phase, p.Jersey, fold); Crease(c, R - 2, jB + 2 + phase, p.Jersey, fold);
            }

            // 4. Shorts: a darker hem.
            var hem = p.Shorts.Darken(0.16f);
            for (int x = L; x <= R; x++)
                if (c.Get(x, sB).Equals(p.Shorts)) c.Set(x, sB, hem);

            // 5. Crew socks: the two skin rows just above each shoe become a sock with a trim band, when the leg is
            //    long enough to still show skin above it.
            var sock = new RgbColor(0xE4, 0xE6, 0xEC);
            for (int x = 0; x < c.Width; x++)
                for (int y = 1; y < sB; y++)
                {
                    if (!IsSkin(c.Get(x, y), p) || !IsShoe(c.Get(x, y - 1), kit)) continue;
                    int run = 0;
                    while (y + run < c.Height && IsSkin(c.Get(x, y + run), p)) run++;
                    if (run >= 4)
                    {
                        c.Set(x, y, sock);
                        c.Set(x, y + 1, Distinct(p.Trim, sock) ? p.Trim : p.Jersey);
                    }
                    break;
                }

            // 6. Face: eye whites beside the pupils and a warm cheek on each side (front view).
            if (view == CharacterView.Front && i.HeadW > 0)
            {
                int hl = i.HeadLeft * 2, hb = i.HeadBottom * 2, hw = i.HeadW * 2;
                int m = hl + hw / 2;
                var white = new RgbColor(0xF4, 0xF4, 0xF0);
                SetIf(c, m - 4, hb + 4, p.Skin, white);
                SetIf(c, m + 3, hb + 4, p.Skin, white);
                var cheek = SpriteDetail.Mix(p.Skin, new RgbColor(0xD0, 0x60, 0x58), 0.18f);
                SetIf(c, m - 4, hb + 2, p.Skin, cheek);
                SetIf(c, m + 3, hb + 2, p.Skin, cheek);
            }
        }

        private static void Crease(PixelCanvas c, int x, int y, RgbColor on, RgbColor col) => SetIf(c, x, y, on, col);

        private static void SetIf(PixelCanvas c, int x, int y, RgbColor on, RgbColor col)
        {
            if (c.InBounds(x, y) && c.Get(x, y).Equals(on)) c.Set(x, y, col);
        }

        private static bool IsSkin(RgbColor c, Palette p) => c.a > 0 && (c.Equals(p.Skin) || c.Equals(p.SkinShade));

        private static bool IsShoe(RgbColor c, KitLook kit) =>
            c.a > 0 && (c.Equals(kit.Shoe) || c.Equals(kit.Sole) || c.Equals(kit.Laces) || (kit.ShoeStripeOn && c.Equals(kit.ShoeStripe)));

        /// <summary>Jersey, shorts and their trims (anything on the body that isn't skin, hair, shoes or ink).</summary>
        private static bool IsCloth(RgbColor c, Palette p)
        {
            if (c.a == 0 || IsSkin(c, p) || c.Equals(p.Hair) || c.Luminance < 0.08f) return false;
            return !IsShoe(c, p.Kit);
        }

        private static bool Distinct(RgbColor a, RgbColor b) => RgbColor.Distance(a, b) > 60;
    }
}
