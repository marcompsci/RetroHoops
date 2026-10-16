using System;

namespace CallerRetroBall.Logic.PixelArt
{
    public static partial class CharacterSpriteGenerator
    {
        // 16 Oct 2026 graphics phase G2: arms for the four new action frames (defence, slide, dunk, pass). Arms are
        // built from segments: the shoulder segment is sleeve on tees and long sleeves, the forearm is sleeve only on
        // long sleeves, and the hand is always skin with a shaded knuckle.

        private static void Shoulder(PixelCanvas c, int x, int y, int w, int h, Palette p) =>
            Rect(c, x, y, w, h, p.Kit.Cut == JerseyCut.Tank ? p.Skin : p.Jersey);

        private static void Fore(PixelCanvas c, int x, int y, int w, int h, Palette p) =>
            Rect(c, x, y, w, h, p.Kit.Cut == JerseyCut.LongSleeve ? p.Jersey : p.Skin);

        private static void Hand(PixelCanvas c, int x, int y, int w, int h, int knuckleX, Palette p)
        {
            Rect(c, x, y, w, h, p.Skin);
            if (c.InBounds(knuckleX, y)) c.Set(knuckleX, y, p.SkinShade);
        }

        private static void DrawActionArms(PixelCanvas c, CharacterView view, bool defense, bool slide, bool pass,
                                           int torsoLeft, int torsoW, int armTop, int cx, Palette p)
        {
            int L = torsoLeft, R = torsoLeft + torsoW;
            if (view == CharacterView.Side)
            {
                if (pass)
                {
                    // Both arms pushed straight out in front at chest height.
                    Shoulder(c, cx, armTop - 2, 2, 2, p);
                    Fore(c, cx + 2, armTop - 2, 3, 2, p);
                    Hand(c, cx + 5, armTop - 2, 1, 2, cx + 5, p);
                }
                else
                {
                    // Guarding: the near arm out in front, hand up at chin height (the slide holds it a touch lower).
                    int up = slide ? 0 : 1;
                    Shoulder(c, cx, armTop - 1, 2, 2, p);
                    Fore(c, cx + 2, armTop - 1, 2, 2, p);
                    Hand(c, cx + 4, armTop - 1 + up, 1, 2, cx + 4, p);
                }
                return;
            }

            if (pass)
            {
                if (view == CharacterView.Front)
                {
                    // The ball's pushed towards the camera: forearms angle in and the hands meet over the chest.
                    Rect(c, cx - 1, _info.TorsoBottom + 1, 3, 3, p.Jersey); // the chest mark is under the hands
                    _info.Number = false;
                    Shoulder(c, L - 2, armTop - 1, 2, 2, p);
                    Shoulder(c, R, armTop - 1, 2, 2, p);
                    Fore(c, L - 1, armTop - 3, 2, 2, p);
                    Fore(c, R - 1, armTop - 3, 2, 2, p);
                    Hand(c, cx - 2, armTop - 3, 4, 2, cx - 2, p);
                    c.Set(cx + 1, armTop - 3, p.SkinShade);
                }
                else
                {
                    // From behind: elbows tucked, the hands out of sight in front.
                    Shoulder(c, L - 2, armTop - 1, 2, 2, p);
                    Shoulder(c, R, armTop - 1, 2, 2, p);
                    Fore(c, L - 1, armTop - 3, 2, 2, p);
                    Fore(c, R - 1, armTop - 3, 2, 2, p);
                }
                return;
            }

            // Defence: both hands up and out. The slide drops the lead (left) arm out low into the passing lane.
            Shoulder(c, R, armTop - 1, 2, 2, p);
            Fore(c, R + 1, armTop, 2, 3, p);
            Hand(c, R + 1, armTop + 3, 2, 1, R + 2, p);
            Shoulder(c, L - 2, armTop - 1, 2, 2, p);
            if (slide)
            {
                Fore(c, L - 3, armTop - 1, 1, 2, p);
                Hand(c, L - 4, armTop - 1, 1, 2, L - 4, p);
            }
            else
            {
                Fore(c, L - 3, armTop, 2, 3, p);
                Hand(c, L - 3, armTop + 3, 2, 1, L - 3, p);
            }
        }

        /// <summary>The dunk: the ball arm stretched all the way up (in front of the head), the other out for balance.</summary>
        private static void DrawDunkArms(PixelCanvas c, CharacterView view, int torsoLeft, int torsoW, int armTop, int top, int cx, Palette p)
        {
            int L = torsoLeft, R = torsoLeft + torsoW;
            if (view == CharacterView.Side)
            {
                Shoulder(c, cx + 1, armTop - 1, 2, 2, p);
                Fore(c, cx + 1, armTop + 1, 2, top - armTop - 1, p);
                Hand(c, cx + 1, top, 2, 1, cx + 2, p);
                // Off arm swung back.
                Shoulder(c, cx - 2, armTop - 1, 2, 2, p);
                Fore(c, cx - 3, armTop - 3, 2, 2, p);
                return;
            }
            Shoulder(c, R - 1, armTop - 1, 2, 2, p);
            Fore(c, R - 1, armTop + 1, 2, top - armTop - 1, p);
            Hand(c, R - 1, top, 2, 1, R, p);
            Shoulder(c, L - 2, armTop - 1, 2, 2, p);
            Fore(c, L - 3, armTop - 3, 2, 2, p);
            Hand(c, L - 4, armTop - 4, 2, 1, L - 4, p);
        }
    }
}
