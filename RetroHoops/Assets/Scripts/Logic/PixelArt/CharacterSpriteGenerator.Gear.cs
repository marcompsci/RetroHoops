using System;

namespace CallerRetroBall.Logic.PixelArt
{
    public static partial class CharacterSpriteGenerator
    {
        private const byte LimbArm = 1, LimbLeg = 2;
        [ThreadStatic] private static byte[] _armMask;
        private static readonly RgbColor EyeInk = new RgbColor(0x1A, 0x14, 0x18);

        /// <summary>
        /// I3 GEAR: which 1x pixels are arm and which are leg. At this point the head isn't drawn yet (it's drawn at 2x),
        /// so any skin above the shorts is an arm (the neck excepted) and any skin below them is a leg. Hands (the shaded
        /// knuckle pixel at the end of each arm) stay bare.
        /// </summary>
        private static byte[] LimbMask(PixelCanvas c, Palette p)
        {
            var mask = new byte[c.Width * c.Height];
            var i = _info;
            int neck = i.TorsoTop + 1, cx = FrameWidth / 2;
            for (int y = 0; y < c.Height; y++)
                for (int x = 0; x < c.Width; x++)
                {
                    var px = c.Get(x, y);
                    if (px.a == 0) continue;
                    bool skin = px.Equals(p.Skin), shade = px.Equals(p.SkinShade);
                    if (!skin && !shade) continue;
                    if (y < i.ShortsBottom) { mask[y * c.Width + x] = LimbLeg; continue; }
                    if (y == neck && (x == cx - 1 || x == cx)) continue; // the neck
                    if (shade) continue;                                    // a hand
                    mask[y * c.Width + x] = LimbArm;
                }
            return mask;
        }

        private static void ApplyGear(PixelCanvas c, CharacterView view, byte[] limbs, Palette p)
        {
            var kit = p.Kit;
            var g = kit.Gear;
            int w1 = FrameWidth;
            var sleeve = kit.GearColor;
            var ink = SpriteDetail.Mix(p.Skin.Darken(0.5f), kit.GearColor, 0.45f);
            for (int y = 0; y < c.Height; y++)
                for (int x = 0; x < c.Width; x++)
                {
                    int m = limbs[(y / 2) * w1 + (x / 2)];
                    if (m == 0 || !IsSkin(c.Get(x, y), p)) continue;
                    if (m == LimbArm)
                    {
                        if ((g & GearParts.ArmSleeves) != 0) c.Set(x, y, sleeve);
                        else if ((g & GearParts.Ink) != 0 && ((x + 2 * y) % 5 == 0 || (x - y + 64) % 6 == 0)) c.Set(x, y, ink);
                    }
                    else if ((g & GearParts.LegSleeves) != 0)
                    {
                        c.Set(x, y, sleeve);
                    }
                }
            if ((g & GearParts.Goggles) != 0) { _armMask = limbs; DrawGoggles(c, view, kit.GearColor); _armMask = null; }
        }

        /// <summary>Sport goggles: rims round both eyes and a strap round the head (one lens from the side, the strap from behind).</summary>
        private static void DrawGoggles(PixelCanvas c, CharacterView view, RgbColor rim)
        {
            var i = _info;
            if (i.HeadW <= 0) return;
            int L = i.HeadLeft * 2, B = i.HeadBottom * 2, W = i.HeadW * 2;
            var strap = rim.Darken(0.35f);
            int row = B + 4;
            if (view == CharacterView.Back)
            {
                for (int x = L - 1; x <= L + W; x++) OnHead(c, x, row, strap);
                return;
            }
            if (view == CharacterView.Side)
            {
                int e = L + W - 3; // the eye
                for (int x = L; x < e - 1; x++) OnHead(c, x, row, strap);
                Ring(c, e, row, rim);
                return;
            }
            int mid = L + W / 2, eyeL = mid - 3, eyeR = mid + 2;
            Ring(c, eyeL, row, rim);
            Ring(c, eyeR, row, rim);
            for (int x = L - 1; x < eyeL - 1; x++) OnHead(c, x, row, strap);
            for (int x = eyeR + 2; x <= L + W; x++) OnHead(c, x, row, strap);
            for (int x = eyeL + 2; x < eyeR - 1; x++) OnHead(c, x, row + 1, rim); // the bridge
        }

        /// <summary>A lens rim round an eye (the eye and its catchlight stay).</summary>
        private static void Ring(PixelCanvas c, int ex, int row, RgbColor rim)
        {
            for (int y = row - 2; y <= row + 2; y++)
                for (int x = ex - 1; x <= ex + 1; x++)
                {
                    bool edge = y == row - 2 || y == row + 2 || x == ex - 1 || x == ex + 1;
                    if (edge) OnHead(c, x, y, rim);
                }
        }

        private static void OnHead(PixelCanvas c, int x, int y, RgbColor col)
        {
            if (!c.InBounds(x, y)) return;
            var px = c.Get(x, y);
            // Never into thin air, and never over the eyes (pupil ink and catchlight).
            if (px.a == 0 || px.Equals(EyeInk) || px.Equals(RgbColor.White)) return;
            if (_armMask != null && _armMask[(y / 2) * FrameWidth + (x / 2)] == LimbArm) return; // a raised arm in front of the head
            c.Set(x, y, col);
        }
    }
}
