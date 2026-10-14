using System;
namespace CallerRetroBall.Logic.PixelArt
{
    public enum CrowdMood { Idle = 0, Cheer = 1, Groan = 2 }

    /// <summary>
    /// Seated fans for the animated crowd behind the baseline (a 5x8 court-pixel footprint, drawn at 2x): shoulders,
    /// head, and, when cheering, both arms up. Original pixel art. Row 0 is the bottom row.
    /// </summary>
    public static class CrowdGenerator
    {
        public const int Width = 5;
        public const int Height = 8;

        /// <summary>Fan sprites are drawn at 2x detail: 10 x 16 art pixels over a 5 x 8 court-pixel footprint.</summary>
        public const int ArtScale = 2;
        /// <summary>Distinct looks: six hair styles, each with and without a foam finger.</summary>
        public const int Variants = 12;

        private static readonly RgbColor[] HairColors =
        {
            RgbColor.FromHex("#1B1410"), RgbColor.FromHex("#3B2416"), RgbColor.FromHex("#6B4423"),
            RgbColor.FromHex("#C9A050"), RgbColor.FromHex("#8A2E1C"), RgbColor.FromHex("#2A2A30"),
        };
        private static readonly RgbColor[] HatColors =
        {
            RgbColor.FromHex("#E4572E"), RgbColor.FromHex("#4CC9F0"), RgbColor.FromHex("#FFD166"),
            RgbColor.FromHex("#F4F1DE"), RgbColor.FromHex("#7B2CBF"), RgbColor.FromHex("#06D6A0"),
        };
        private static readonly RgbColor Foam = RgbColor.FromHex("#FFD166");
        private static readonly RgbColor Eye = RgbColor.FromHex("#1A1420");

        public static PixelCanvas Fan(RgbColor shirt, RgbColor skin, bool armsUp) => Fan(shirt, skin, armsUp, 0);

        /// <summary>
        /// A seated fan facing the court (10 x 16, row 0 at the bottom): shaded shirt, face with eyes, one of six
        /// hair styles or hats, and when cheering, both arms up (with a foam finger for variants 6-11).
        /// </summary>
        public static PixelCanvas Fan(RgbColor shirt, RgbColor skin, bool armsUp, int variant)
        {
            int v = ((variant % Variants) + Variants) % Variants;
            int style = v % 6;
            bool finger = v >= 6;
            var hair = HairColors[(v * 5 + 1) % HairColors.Length];
            var hat = HatColors[(v * 7 + 2) % HatColors.Length];
            var c = new PixelCanvas(Width * ArtScale, Height * ArtScale);

            // Afro sits behind the head.
            if (style == 1) c.FillCircle(5f, 12.2f, 4.6f, hair);

            // Shirt: shoulders with light from the left.
            c.FillRect(0, 0, 10, 5, shirt);
            c.FillRect(1, 5, 8, 1, shirt);
            for (int y = 0; y < 6; y++) { c.Set(8, y, shirt.Darken(0.18f)); c.Set(9, y, shirt.Darken(0.28f)); c.Set(0, y, shirt.Lighten(0.12f)); }
            c.Set(4, 5, skin.Darken(0.2f)); c.Set(5, 5, skin.Darken(0.2f));            // collar
            c.Set(4, 6, skin.Darken(0.15f)); c.Set(5, 6, skin.Darken(0.15f));          // neck

            // Head with rounded corners, shaded right side, eyes and a mouth (open when cheering).
            for (int y = 7; y <= 13; y++)
                for (int x = 2; x <= 7; x++)
                {
                    bool corner = (x == 2 || x == 7) && (y == 7 || y == 13);
                    if (corner) continue;
                    c.Set(x, y, x == 7 ? skin.Darken(0.16f) : skin);
                }
            c.Set(3, 10, Eye); c.Set(6, 10, Eye);
            var mouth = armsUp ? RgbColor.FromHex("#5A1E1E") : skin.Darken(0.28f);
            c.Set(4, 8, mouth); c.Set(5, 8, mouth);
            if (armsUp) c.Set(4, 9, skin.Darken(0.1f));

            switch (style)
            {
                case 0: // short
                    for (int x = 3; x <= 6; x++) c.Set(x, 13, hair);
                    for (int x = 2; x <= 7; x++) c.Set(x, 12, hair);
                    c.Set(2, 11, hair); c.Set(7, 11, hair);
                    break;
                case 1: // afro (drawn behind; a fringe over the brow)
                    for (int x = 2; x <= 7; x++) c.Set(x, 13, hair);
                    break;
                case 2: // cap with a brim
                    c.FillRect(2, 13, 6, 2, hat);
                    c.Set(3, 15, hat); c.Set(4, 15, hat); c.Set(5, 15, hat); c.Set(6, 15, hat);
                    for (int x = 1; x <= 8; x++) c.Set(x, 12, hat.Darken(0.3f));
                    break;
                case 3: // long hair down the sides
                    for (int x = 3; x <= 6; x++) c.Set(x, 13, hair);
                    for (int x = 2; x <= 7; x++) c.Set(x, 12, hair);
                    for (int y = 6; y <= 12; y++) { c.Set(1, y, hair); c.Set(8, y, hair); }
                    c.Set(2, 11, hair); c.Set(7, 11, hair);
                    break;
                case 4: // beanie with a pom
                    c.FillRect(2, 12, 6, 3, hat);
                    for (int x = 2; x <= 7; x++) c.Set(x, 12, hat.Lighten(0.3f));
                    c.Set(4, 15, RgbColor.White); c.Set(5, 15, RgbColor.White);
                    break;
                default: // headband
                    for (int x = 3; x <= 6; x++) c.Set(x, 13, hair);
                    for (int x = 2; x <= 7; x++) c.Set(x, 12, hat);
                    break;
            }

            if (armsUp)
            {
                c.FillRect(0, 5, 2, 8, shirt.Lighten(0.06f));   // left arm
                c.FillRect(8, 5, 2, 8, shirt.Darken(0.2f));     // right arm
                c.FillRect(0, 13, 2, 2, skin);                  // hands
                if (finger)
                {
                    c.FillRect(7, 12, 3, 3, Foam);              // foam "#1" finger
                    c.Set(8, 15, Foam); c.Set(8, 14, Foam.Lighten(0.3f));
                    c.Set(9, 12, Foam.Darken(0.25f)); c.Set(9, 13, Foam.Darken(0.25f));
                }
                else c.FillRect(8, 13, 2, 2, skin.Darken(0.12f));
            }
            return c;
        }

        /// <summary>Vertical bob (art pixels) for a fan at time <paramref name="t"/>; each fan has its own phase.</summary>
        public static int Bob(CrowdMood mood, float t, int fanIndex)
        {
            switch (mood)
            {
                case CrowdMood.Cheer:
                    return ((int)(t * 8f) + fanIndex) % 2 == 0 ? 1 : 0;
                case CrowdMood.Groan:
                    return -1;
                default:
                    return ((int)(t * 1.5f) + fanIndex * 3) % 7 == 0 ? 1 : 0;
            }
        }

        /// <summary>Rows of the landscape sideline stands (fans stand on each).</summary>
        public const int StandRows = 3;
        public const int StandRowHeight = 7;

        /// <summary>
        /// Bleachers along the far sideline for landscape Full Court: a low wall, three stepped rows and a
        /// back rail, in the court's colours. <paramref name="widthPx"/> wide; row 0 is the front (lowest) row.
        /// </summary>
        public static PixelCanvas SidelineStands(int widthPx, RgbColor wall, RgbColor seat, RgbColor accent)
        {
            int h = 4 + StandRows * StandRowHeight + 3;
            var c = new PixelCanvas(Math.Max(8, widthPx), h);
            // Front wall with an accent stripe.
            c.FillRect(0, 0, c.Width, 4, wall.Darken(0.2f));
            c.FillRect(0, 2, c.Width, 1, accent);
            for (int row = 0; row < StandRows; row++)
            {
                int y = 4 + row * StandRowHeight;
                var face = row % 2 == 0 ? seat : seat.Darken(0.12f);
                c.FillRect(0, y, c.Width, StandRowHeight, face.Darken(0.1f * row));
                c.FillRect(0, y + StandRowHeight - 1, c.Width, 1, seat.Lighten(0.2f)); // bench edge
                for (int x = (row * 5) % 16; x < c.Width; x += 16) c.FillRect(x, y, 1, StandRowHeight - 1, wall.Darken(0.35f)); // aisle posts
            }
            // Back rail.
            c.FillRect(0, h - 3, c.Width, 3, wall.Darken(0.4f));
            c.FillRect(0, h - 2, c.Width, 1, accent.Darken(0.2f));
            return c;
        }

        /// <summary>Bottom pixel row (in the stands canvas) where fans in <paramref name="row"/> stand.</summary>
        public static int StandRowFloor(int row) => 4 + row * StandRowHeight + 1;
    }
}
