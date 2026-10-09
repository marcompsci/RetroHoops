using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using CallerRetroBall.Utilities;
using UnityEngine;

namespace CallerRetroBall.UI
{
    public enum ButtonStyle { Primary = 0, Secondary = 1, Ghost = 2 }

    /// <summary>Original Retro Hoops UI palette and generated pixel skin.</summary>
    public static class Theme
    {
        public const string Title = "RETRO HOOPS";
        public const string Tagline = "Call your shot. Build your legacy.";

        public static readonly Color Ink = new Color32(0x1A, 0x1A, 0x2E, 255);
        public static readonly Color InkLight = new Color32(0x2A, 0x2A, 0x45, 255);
        public static readonly Color Asphalt = new Color32(0x2F, 0x2F, 0x36, 255);
        public static readonly Color Pink = new Color32(0xF7, 0x25, 0x85, 255);
        public static readonly Color Cyan = new Color32(0x4C, 0xC9, 0xF0, 255);
        public static readonly Color Gold = new Color32(0xFF, 0xD1, 0x66, 255);
        public static readonly Color Cream = new Color32(0xF4, 0xF1, 0xDE, 255);
        public static readonly Color Muted = new Color32(0x8D, 0x99, 0xAE, 255);
        public static readonly Color Shadow = new Color32(0x0B, 0x0B, 0x16, 255);
        public static readonly Color Scrim = new Color32(0x1A, 0x1A, 0x2E, 200);

        /// <summary>Canvas units per art pixel for chunky 9-slice borders.</summary>
        public const float PixelSize = 6f;

        // 8-bit console-style button faces (original colours): red for the main action,
        // blue for secondary, slate for minor actions.
        public static readonly Color ButtonRed = new Color32(0xD8, 0x28, 0x00, 255);
        public static readonly Color ButtonBlue = new Color32(0x20, 0x38, 0xEC, 255);
        public static readonly Color ButtonSlate = new Color32(0x58, 0x58, 0x68, 255);
        public static readonly Color ButtonText = new Color32(0xFC, 0xFC, 0xFC, 255);

        private static readonly System.Collections.Generic.Dictionary<string, Sprite> Buttons =
            new System.Collections.Generic.Dictionary<string, Sprite>();
        private static Sprite _panel;

        public static Color ButtonFace(ButtonStyle style) =>
            style == ButtonStyle.Primary ? ButtonRed : style == ButtonStyle.Secondary ? ButtonBlue : ButtonSlate;

        /// <summary>Raised button face for a style (9-sliced).</summary>
        public static Sprite ButtonSprite(ButtonStyle style) => ButtonVariant(style, 0);

        /// <summary>Pushed-in face, swapped in while the button is held.</summary>
        public static Sprite ButtonPressedSprite(ButtonStyle style) => ButtonVariant(style, 1);

        /// <summary>Greyed-out face for buttons that can't be used right now.</summary>
        public static Sprite ButtonDisabledSprite(ButtonStyle style) => ButtonVariant(style, 2);

        private static Sprite ButtonVariant(ButtonStyle style, int variant)
        {
            string key = style + ":" + variant;
            if (Buttons.TryGetValue(key, out var cached) && cached != null) return cached;
            var face = ButtonFace(style).ToRgb32();
            if (variant == 2) face = RgbColor.Lerp(face, new RgbColor(0x60, 0x60, 0x68), 0.75f).Darken(0.25f);
            var canvas = UiSkinGenerator.Button(face, pressed: variant == 1);
            int b = UiSkinGenerator.ButtonBorder;
            var sprite = TextureFactory.ToSprite(canvas, "ui.button." + key, 100f, new Vector4(b, b, b, b));
            Buttons[key] = sprite;
            return sprite;
        }

        public static Sprite PanelSprite() =>
            _panel != null ? _panel : (_panel = Chunky("ui.panel", new RgbColor(0x1A, 0x1A, 0x2E, 255), Muted.ToRgb32()));

        private static readonly System.Collections.Generic.Dictionary<string, Sprite> Discs =
            new System.Collections.Generic.Dictionary<string, Sprite>();

        /// <summary>
        /// Round console-style action button face (24x24 art pixels): outline, lip, shine and glint.
        /// <paramref name="border"/> is kept for callers; the outline is always near-black for contrast.
        /// </summary>
        public static Sprite DiscSprite(Color fill, Color border) => Disc(fill, false);

        /// <summary>The same disc pushed in (no lip), for while a touch button is held.</summary>
        public static Sprite DiscPressedSprite(Color fill) => Disc(fill, true);

        private static Sprite Disc(Color fill, bool pressed)
        {
            string key = ColorUtility.ToHtmlStringRGBA(fill) + (pressed ? ":p" : "");
            if (Discs.TryGetValue(key, out var cached) && cached != null) return cached;
            var f = fill.ToRgb32();
            // Translucent fills (the joystick base) keep the old flat look.
            var canvas = f.a == 255 ? UiSkinGenerator.Disc(f, pressed) : FlatDisc(f);
            var sprite = TextureFactory.ToSprite(canvas, "ui.disc." + key);
            Discs[key] = sprite;
            return sprite;
        }

        private static PixelCanvas FlatDisc(RgbColor f)
        {
            const int s = UiSkinGenerator.DiscSize;
            var c = new PixelCanvas(s, s);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = x + 0.5f - s / 2f, dy = y + 0.5f - s / 2f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > s / 2f) continue;
                    c.Set(x, y, d > s / 2f - 2f ? new RgbColor(0xF4, 0xF1, 0xDE, 200) : f);
                }
            return c;
        }

        private static RgbColor ToRgb32(this Color c) => ((Color32)c).ToRgb();

        /// <summary>
        /// 12x12 chunky pixel frame: clipped corners, 1px border, top highlight,
        /// bottom shade. Sliced with a 4px border so it scales to any size.
        /// </summary>
        private static Sprite Chunky(string name, RgbColor fill, RgbColor border)
        {
            const int s = 12;
            var c = new PixelCanvas(s, s);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    int dx = Mathf.Min(x, s - 1 - x), dy = Mathf.Min(y, s - 1 - y);
                    if (dx + dy < 2) continue;                       // clipped corner
                    bool edge = dx == 0 || dy == 0 || dx + dy == 2;
                    RgbColor col = edge ? border : fill;
                    if (!edge && y == s - 2) col = fill.a == 255 ? fill.Lighten(0.25f) : fill; // highlight
                    if (!edge && y == 1) col = fill.a == 255 ? fill.Darken(0.25f) : fill;      // shade
                    c.Set(x, y, col);
                }
            return TextureFactory.ToSprite(c, name, 100f, new Vector4(4, 4, 4, 4));
        }
    }
}
