using System.Collections.Generic;

namespace CallerRetroBall.Logic.PixelArt
{
    /// <summary>
    /// Tiny original 3×5 pixel font for text drawn straight into generated art (trading cards).
    /// Upper-case letters, digits and a little punctuation; anything else draws as a space.
    /// </summary>
    public static class PixelFont
    {
        public const int GlyphW = 3, GlyphH = 5, Spacing = 1;

        private static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            ['A'] = new[] { ".#.", "#.#", "###", "#.#", "#.#" },
            ['B'] = new[] { "##.", "#.#", "##.", "#.#", "##." },
            ['C'] = new[] { ".##", "#..", "#..", "#..", ".##" },
            ['D'] = new[] { "##.", "#.#", "#.#", "#.#", "##." },
            ['E'] = new[] { "###", "#..", "##.", "#..", "###" },
            ['F'] = new[] { "###", "#..", "##.", "#..", "#.." },
            ['G'] = new[] { ".##", "#..", "#.#", "#.#", ".##" },
            ['H'] = new[] { "#.#", "#.#", "###", "#.#", "#.#" },
            ['I'] = new[] { "###", ".#.", ".#.", ".#.", "###" },
            ['J'] = new[] { "..#", "..#", "..#", "#.#", ".#." },
            ['K'] = new[] { "#.#", "#.#", "##.", "#.#", "#.#" },
            ['L'] = new[] { "#..", "#..", "#..", "#..", "###" },
            ['M'] = new[] { "#.#", "###", "###", "#.#", "#.#" },
            ['N'] = new[] { "##.", "#.#", "#.#", "#.#", "#.#" },
            ['O'] = new[] { ".#.", "#.#", "#.#", "#.#", ".#." },
            ['P'] = new[] { "##.", "#.#", "##.", "#..", "#.." },
            ['Q'] = new[] { ".#.", "#.#", "#.#", "##.", ".##" },
            ['R'] = new[] { "##.", "#.#", "##.", "#.#", "#.#" },
            ['S'] = new[] { ".##", "#..", ".#.", "..#", "##." },
            ['T'] = new[] { "###", ".#.", ".#.", ".#.", ".#." },
            ['U'] = new[] { "#.#", "#.#", "#.#", "#.#", "###" },
            ['V'] = new[] { "#.#", "#.#", "#.#", "#.#", ".#." },
            ['W'] = new[] { "#.#", "#.#", "###", "###", "#.#" },
            ['X'] = new[] { "#.#", "#.#", ".#.", "#.#", "#.#" },
            ['Y'] = new[] { "#.#", "#.#", ".#.", ".#.", ".#." },
            ['Z'] = new[] { "###", "..#", ".#.", "#..", "###" },
            ['0'] = new[] { "###", "#.#", "#.#", "#.#", "###" },
            ['1'] = new[] { ".#.", "##.", ".#.", ".#.", "###" },
            ['2'] = new[] { "##.", "..#", ".#.", "#..", "###" },
            ['3'] = new[] { "##.", "..#", ".#.", "..#", "##." },
            ['4'] = new[] { "#.#", "#.#", "###", "..#", "..#" },
            ['5'] = new[] { "###", "#..", "##.", "..#", "##." },
            ['6'] = new[] { ".##", "#..", "###", "#.#", "###" },
            ['7'] = new[] { "###", "..#", ".#.", ".#.", ".#." },
            ['8'] = new[] { "###", "#.#", "###", "#.#", "###" },
            ['9'] = new[] { "###", "#.#", "###", "..#", "##." },
            ['-'] = new[] { "...", "...", "###", "...", "..." },
            ['.'] = new[] { "...", "...", "...", "...", ".#." },
            [':'] = new[] { "...", ".#.", "...", ".#.", "..." },
            ['#'] = new[] { "#.#", "###", "#.#", "###", "#.#" },
            ['/'] = new[] { "..#", "..#", ".#.", "#..", "#.." },
            ['!'] = new[] { ".#.", ".#.", ".#.", "...", ".#." },
            ['\''] = new[] { ".#.", ".#.", "...", "...", "..." },
            ['&'] = new[] { ".#.", "#.#", ".#.", "#.#", ".##" },
            ['%'] = new[] { "#.#", "..#", ".#.", "#..", "#.#" },
            ['+'] = new[] { "...", ".#.", "###", ".#.", "..." },
            ['?'] = new[] { "##.", "..#", ".#.", "...", ".#." },
            [','] = new[] { "...", "...", "...", ".#.", "#.." },
            ['$'] = new[] { ".##", "##.", ".#.", ".##", "##." },
        };

        public static bool Has(char ch) => Glyphs.ContainsKey(char.ToUpperInvariant(ch));

        /// <summary>Width in pixels of <paramref name="text"/> at <paramref name="scale"/>.</summary>
        public static int Measure(string text, int scale = 1)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return (text.Length * (GlyphW + Spacing) - Spacing) * scale;
        }

        /// <summary>
        /// Draws text with its top-left corner at (<paramref name="left"/>, <paramref name="top"/>) in
        /// canvas rows (row 0 is the bottom, so <paramref name="top"/> is the highest row used).
        /// </summary>
        public static void Draw(PixelCanvas c, string text, int left, int top, RgbColor color, int scale = 1)
        {
            if (string.IsNullOrEmpty(text)) return;
            int x = left;
            foreach (char raw in text)
            {
                if (Glyphs.TryGetValue(char.ToUpperInvariant(raw), out var g))
                    for (int row = 0; row < GlyphH; row++)
                        for (int col = 0; col < GlyphW; col++)
                            if (g[row][col] == '#') c.FillRect(x + col * scale, top - (row + 1) * scale + 1, scale, scale, color);
                x += (GlyphW + Spacing) * scale;
            }
        }

        /// <summary>Draws text centred on <paramref name="centerX"/>.</summary>
        public static void DrawCentered(PixelCanvas c, string text, int centerX, int top, RgbColor color, int scale = 1) =>
            Draw(c, text, centerX - Measure(text, scale) / 2, top, color, scale);
    }
}
