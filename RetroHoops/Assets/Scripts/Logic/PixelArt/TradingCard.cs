using System;

namespace CallerRetroBall.Logic.PixelArt
{
    /// <summary>What goes on a trading card.</summary>
    public sealed class CardInfo
    {
        public AppearanceDef Look;
        public KitLook Kit;
        public string Name = "ROOK";
        public int Number;
        public string Team = "";
        public string Role = "";
        public int Overall;
        /// <summary>Up to three stats, e.g. ("WINS", "12").</summary>
        public (string label, string value)[] Stats = new (string, string)[0];
        /// <summary>Optional 32×32 team logo.</summary>
        public PixelCanvas Logo;
        public string Footer = "RETRO HOOPS";
    }

    /// <summary>
    /// A shareable pixel-art trading card of your player in your kit (Locker Room ► KIT ► TRADING CARD):
    /// kit-coloured frame, your player large in the window, OVR badge, team logo, name, role and stats.
    /// Original art, drawn entirely in code.
    /// </summary>
    public static class TradingCard
    {
        public const int Width = 96;
        public const int Height = 136;
        private const int SpriteScale = 3;

        public static PixelCanvas Generate(CardInfo info)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            var c = new PixelCanvas(Width, Height);
            var kit = info.Kit;
            var frameOuter = kit.Trim;
            var frameInner = kit.Accent;
            var bgTop = kit.Jersey.Lighten(0.08f);
            var bgBottom = kit.Jersey.Darken(0.35f);
            var ink = Readable(kit.Jersey);
            var dark = new RgbColor(0x14, 0x14, 0x20);

            // Background gradient, banded in 4-pixel steps (retro).
            for (int y = 0; y < Height; y++)
            {
                float t = (float)((Height - 1 - y) / 4 * 4) / (Height - 1);
                var col = RgbColor.Lerp(bgTop, bgBottom, t);
                for (int x = 0; x < Width; x++) c.Set(x, y, col);
            }
            // Frame: 3px trim with a 1px accent inside, corners cut.
            Border(c, 0, frameOuter, 3);
            Border(c, 3, frameInner, 1);
            foreach (var (x, y) in new[] { (0, 0), (Width - 1, 0), (0, Height - 1), (Width - 1, Height - 1) })
            {
                c.Pixels[y * Width + x] = RgbColor.Clear;
                int nx = x == 0 ? 1 : x - 1, ny = y == 0 ? 1 : y - 1;
                c.Pixels[y * Width + nx] = RgbColor.Clear;
                c.Pixels[ny * Width + x] = RgbColor.Clear;
            }

            // Name bar at the top.
            string name = Fit(info.Name.ToUpperInvariant(), Width - 12);
            c.FillRect(4, Height - 15, Width - 8, 11, dark.WithAlpha(150));
            PixelFont.DrawCentered(c, name, Width / 2, Height - 7, RgbColor.White);

            // Art window: a little court with the player standing in it.
            int winL = 10, winR = Width - 10, winB = 42, winT = Height - 19;
            for (int y = winB; y < winT; y++)
            {
                float t = (float)(y - winB) / (winT - winB);
                var sky = RgbColor.Lerp(kit.Accent.Darken(0.55f), kit.Accent.Darken(0.15f), t);
                for (int x = winL; x < winR; x++) c.Set(x, y, y < winB + 14 ? new RgbColor(0x3A, 0x3A, 0x46) : sky);
            }
            for (int x = winL; x < winR; x++) c.Set(x, winB + 14, new RgbColor(0xF4, 0xF1, 0xDE)); // court line
            Box(c, winL - 1, winB - 1, winR - winL + 2, winT - winB + 2, dark);

            var sheet = CharacterSpriteGenerator.GenerateSheet(info.Look, kit);
            CharacterSpriteGenerator.FrameOrigin(CharacterView.Front, 0, out int fx, out int fy);
            int spriteW = CharacterSpriteGenerator.FrameWidth * SpriteScale;
            int left = (Width - spriteW) / 2, bottom = winB + 4;
            // The sheet is drawn at 2x detail: show it at 1.5x of that (the same 3x of a court pixel as before).
            int spriteH = CharacterSpriteGenerator.FrameHeight * SpriteScale, art = CharacterSpriteGenerator.ArtScale;
            for (int y = 0; y < spriteH; y++)
                for (int x = 0; x < spriteW; x++)
                {
                    var p = sheet.Get(fx + x * art / SpriteScale, fy + y * art / SpriteScale);
                    if (p.a == 0) continue;
                    c.Set(left + x, bottom + y, p);
                }

            // Team logo in the window's top-right corner (half size).
            if (info.Logo != null)
                for (int y = 0; y < info.Logo.Height; y += 2)
                    for (int x = 0; x < info.Logo.Width; x += 2)
                    {
                        var p = info.Logo.Get(x, y);
                        if (p.a > 0) c.Set(winR - 18 + x / 2, winT - 18 + y / 2, p);
                    }

            // OVR badge, top-left of the window.
            c.FillCircle(winL + 11, winT - 11, 10.5f, frameOuter);
            c.FillCircle(winL + 11, winT - 11, 9f, dark);
            PixelFont.DrawCentered(c, Math.Max(0, Math.Min(99, info.Overall)).ToString(), winL + 12, winT - 5, RgbColor.White);
            PixelFont.DrawCentered(c, "OVR", winL + 12, winT - 12, frameOuter);

            // Number and role under the window.
            string line = "#" + info.Number + "  " + (info.Role ?? "").ToUpperInvariant();
            PixelFont.DrawCentered(c, Fit(line, Width - 12), Width / 2, winB - 4, ink);
            PixelFont.DrawCentered(c, Fit((info.Team ?? "").ToUpperInvariant(), Width - 12), Width / 2, winB - 10, ink);

            // Stats row.
            int n = Math.Min(3, info.Stats?.Length ?? 0);
            for (int i = 0; i < n; i++)
            {
                int cx = Width * (2 * i + 1) / (2 * n);
                PixelFont.DrawCentered(c, Fit(info.Stats[i].value, 26), cx, 23, RgbColor.White);
                PixelFont.DrawCentered(c, Fit(info.Stats[i].label.ToUpperInvariant(), 26), cx, 16, frameOuter);
            }
            PixelFont.DrawCentered(c, info.Footer ?? "", Width / 2, 9, frameInner);
            return c;
        }

        /// <summary>Scales a canvas up by an integer factor (crisp pixels for the saved PNG).</summary>
        public static PixelCanvas Scale(PixelCanvas src, int factor)
        {
            var dst = new PixelCanvas(src.Width * factor, src.Height * factor);
            for (int y = 0; y < dst.Height; y++)
                for (int x = 0; x < dst.Width; x++)
                    dst.Pixels[y * dst.Width + x] = src.Pixels[(y / factor) * src.Width + x / factor];
            return dst;
        }

        private static RgbColor Readable(RgbColor bg) => bg.Luminance > 0.45 ? new RgbColor(0x14, 0x14, 0x20) : RgbColor.White;

        /// <summary>Cuts text so it fits in <paramref name="maxWidth"/> pixels.</summary>
        private static string Fit(string text, int maxWidth)
        {
            if (string.IsNullOrEmpty(text)) return "";
            while (text.Length > 1 && PixelFont.Measure(text) > maxWidth) text = text.Substring(0, text.Length - 1);
            return text;
        }

        private static void Border(PixelCanvas c, int inset, RgbColor col, int thickness)
        {
            for (int t = 0; t < thickness; t++)
                Box(c, inset + t, inset + t, Width - 2 * (inset + t), Height - 2 * (inset + t), col);
        }

        private static void Box(PixelCanvas c, int x, int y, int w, int h, RgbColor col)
        {
            c.FillRect(x, y, w, 1, col);
            c.FillRect(x, y + h - 1, w, 1, col);
            c.FillRect(x, y, 1, h, col);
            c.FillRect(x + w - 1, y, 1, h, col);
        }
    }
}
