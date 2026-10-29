using System;
using System.Collections.Generic;
using CallerRetroBall.Logic.PixelArt;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// Hit-stop: a tiny freeze on the biggest moments (dunks, blocks, alley-oops, the winning
    /// basket) so they land with weight. Presentation only: the simulation simply isn't stepped.
    /// </summary>
    public static class HitStop
    {
        public const float Dunk = 0.08f;
        public const float AlleyOop = 0.11f;
        public const float Block = 0.07f;
        public const float GameWinner = 0.16f;
        /// <summary>THE STREETS: a SHOWSTOPPER bucket holds a little longer.</summary>
        public const float Showstopper = 0.14f;
        /// <summary>THE STREETS: the freeze on an ANKLE BREAKER (added by the game screen, not by <see cref="For"/>).</summary>
        public const float Ankles = 0.12f;
        public const float Max = 0.2f;

        /// <summary>Freeze length for an event (0 = none). Reduce Motion turns it off.</summary>
        public static float For(MatchEvent e, ShotType lastShot, bool reduceMotion)
        {
            if (reduceMotion) return 0f;
            switch (e.Type)
            {
                case MatchEventType.ShotMade: return lastShot == ShotType.Dunk ? Dunk : 0f;
                case MatchEventType.AlleyOop: return AlleyOop;
                case MatchEventType.Block: return Block;
                case MatchEventType.GameOver: return GameWinner;
                case MatchEventType.BreakerScored: return Showstopper;
                default: return 0f;
            }
        }

        /// <summary>Combines freezes in one step: the longest wins, capped.</summary>
        public static float Combine(float current, float add) => Math.Min(Max, Math.Max(current, add));
    }

    /// <summary>Colour helpers for arcade effects.</summary>
    public static class ArcadeColors
    {
        private static readonly RgbColor[] Rainbow =
        {
            RgbColor.FromHex("#FF4D4D"), RgbColor.FromHex("#FF9F1C"), RgbColor.FromHex("#FFE066"),
            RgbColor.FromHex("#8AFF80"), RgbColor.FromHex("#4CC9F0"), RgbColor.FromHex("#9B5DE5"),
        };

        /// <summary>RAINBOW BALL tint at time <paramref name="t"/> (steps through six colours, 8 per second).</summary>
        public static RgbColor RainbowAt(float t)
        {
            int i = (int)Math.Floor(t * 8f);
            return Rainbow[((i % Rainbow.Length) + Rainbow.Length) % Rainbow.Length];
        }

        /// <summary>HEAT CHECK flame colours, flickering between three.</summary>
        public static RgbColor FlameAt(float t, int salt)
        {
            int i = (int)Math.Floor(t * 20f) + salt * 7;
            switch (((i % 3) + 3) % 3)
            {
                case 0: return RgbColor.FromHex("#FFD166");
                case 1: return RgbColor.FromHex("#FF7E1F");
                default: return RgbColor.FromHex("#E63946");
            }
        }
    }

    /// <summary>
    /// The CRT look: a scanline strip (tiled over the screen) and a vignette, both generated so
    /// no art is bundled. Level 0 = off, 1 = soft, 2 = strong.
    /// </summary>
    public static class CrtPattern
    {
        public const int ScanlineHeight = 3;

        /// <summary>A 1×3 strip: two clear rows and one dark row.</summary>
        public static PixelCanvas Scanlines(int level)
        {
            var c = new PixelCanvas(1, ScanlineHeight);
            byte a = level <= 0 ? (byte)0 : (level == 1 ? (byte)46 : (byte)90);
            c.Set(0, 0, new RgbColor(0, 0, 0, a));
            c.Set(0, 1, new RgbColor(0, 0, 0, 0));
            c.Set(0, 2, new RgbColor(0, 0, 0, 0));
            return c;
        }

        /// <summary>Square vignette (dark corners, clear middle). Level 1 is very light.</summary>
        public static PixelCanvas Vignette(int size, int level)
        {
            var c = new PixelCanvas(size, size);
            float strength = level <= 0 ? 0f : (level == 1 ? 0.25f : 0.5f);
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half, dy = (y - half) / half;
                    float r = (float)Math.Sqrt(dx * dx + dy * dy) / 1.4142f; // 0 centre .. 1 corner
                    float k = Math.Max(0f, (r - 0.55f) / 0.45f);
                    c.Set(x, y, new RgbColor(0, 0, 0, (byte)Math.Round(255f * strength * k * k)));
                }
            return c;
        }
    }

    /// <summary>
    /// Pixel screen wipe between scenes: blocks fill in a diagonal ordered-dither pattern, so
    /// the transition looks like an old console instead of a fade.
    /// </summary>
    public static class ScreenWipe
    {
        public const float Seconds = 0.32f;
        /// <summary>28 Oct 2026: the wipe's real timing (eased, quicker): covering 0.26 s, uncovering 0.24 s.</summary>
        public const float CoverSeconds = 0.26f, RevealSeconds = 0.24f;
        private static readonly int[] Bayer4 = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        /// <summary>
        /// Is block (<paramref name="x"/>, <paramref name="y"/>) of a <paramref name="cols"/>×<paramref name="rows"/>
        /// grid covered at progress <paramref name="t"/> (0 = clear, 1 = fully covered)?
        /// </summary>
        public static bool Covered(int x, int y, int cols, int rows, float t)
        {
            if (t <= 0f) return false;
            if (t >= 1f) return true;
            // Sweep from top-left to bottom-right, with a dither so the edge breaks into pixels.
            float diag = (x / (float)Math.Max(1, cols - 1) + y / (float)Math.Max(1, rows - 1)) * 0.5f;
            float dither = Bayer4[(y & 3) * 4 + (x & 3)] / 16f;
            float threshold = diag * 0.7f + dither * 0.3f;
            return t > threshold;
        }

        /// <summary>Phase 35: the transition looks, taken in turn so menus don't always wipe the same way.</summary>
        public const int Styles = 3;

        /// <summary>
        /// Style 0 is the diagonal sweep above; 1 closes in from the edges like an old TV iris; 2 drops shutters
        /// band by band. Every style only ever covers more as <paramref name="t"/> grows.
        /// </summary>
        public static bool Covered(int style, int x, int y, int cols, int rows, float t)
        {
            if (t <= 0f) return false;
            if (t >= 1f) return true;
            float dither = Bayer4[(y & 3) * 4 + (x & 3)] / 16f;
            switch (((style % Styles) + Styles) % Styles)
            {
                case 1:
                {
                    // Iris: blocks far from the centre go first (aspect-corrected), the middle last.
                    float dx = (x + 0.5f) / cols - 0.5f, dy = ((y + 0.5f) / rows - 0.5f) * rows / (float)cols;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy) / 0.5f / (float)Math.Sqrt(1f + (rows / (float)cols) * (rows / (float)cols));
                    float threshold = (1f - Math.Min(1f, d)) * 0.75f + dither * 0.25f;
                    return t > threshold;
                }
                case 2:
                {
                    // Shutters: six bands top to bottom, each sweeping left to right a little after the one above.
                    int bands = 6;
                    int band = Math.Min(bands - 1, y * bands / Math.Max(1, rows));
                    float across = x / (float)Math.Max(1, cols - 1);
                    float threshold = band / (float)bands * 0.45f + across * 0.4f + dither * 0.15f;
                    return t > threshold;
                }
                default:
                    return Covered(x, y, cols, rows, t);
            }
        }
    }

    /// <summary>
    /// The arcade announcer. Callouts are shown as text and "spoken" as a burst of chiptune voice
    /// blips (one per syllable), like old cartridge games did when real speech didn't fit.
    /// </summary>
    public static class Announcer
    {
        /// <summary>
        /// 15 Oct 2026 (Omari): THE STREETS play-by-play voice: speech rate (0..1 of the iPhone's range) and pitch. Big
        /// calls come faster and higher, like a college broadcast booth losing it.
        /// </summary>
        public const float CallRate = 0.53f, HypeRate = 0.58f, CallPitch = 1.05f, HypePitch = 1.2f;

        /// <summary>
        /// A commentary line made ready to be spoken: lower case with capital letters starting sentences (so names
        /// and words aren't spelled out letter by letter), "6-0" read as "6 to 0", and the "·" separators as pauses.
        /// </summary>
        public static string ForSpeech(string line)
        {
            if (string.IsNullOrEmpty(line)) return "";
            var sb = new System.Text.StringBuilder(line.Length + 8);
            bool start = true;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '·')
                {
                    while (sb.Length > 0 && sb[sb.Length - 1] == ' ') sb.Length--;
                    sb.Append(',');
                    continue;
                }
                if (c == '-' && i > 0 && i + 1 < line.Length && char.IsDigit(line[i - 1]) && char.IsDigit(line[i + 1])) { sb.Append(" to "); continue; }
                if (char.IsLetter(c))
                {
                    sb.Append(start ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
                    start = false;
                }
                else
                {
                    sb.Append(c);
                    if (c == '!' || c == '?' || c == '.') start = true;
                }
            }
            return sb.ToString().Trim();
        }

        /// <summary>Rough syllable count: vowel groups per word (at least one per word).</summary>
        public static int Syllables(string phrase)
        {
            if (string.IsNullOrEmpty(phrase)) return 0;
            int total = 0;
            foreach (var raw in phrase.Split(new[] { ' ', '-', '!', '?', '.', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int n = 0;
                bool inVowel = false;
                foreach (char ch in raw.ToUpperInvariant())
                {
                    bool vowel = "AEIOUY".IndexOf(ch) >= 0;
                    if (vowel && !inVowel) n++;
                    inVowel = vowel;
                }
                if (raw.Length > 2 && raw.EndsWith("E", StringComparison.OrdinalIgnoreCase) && n > 1) n--;
                bool hasLetter = false;
                foreach (char ch in raw) if (char.IsLetter(ch)) { hasLetter = true; break; }
                if (hasLetter) total += Math.Max(1, n);
            }
            return total;
        }

        /// <summary>Pitch contour (MIDI notes), one per syllable. Excited lines ("!") climb at the end.</summary>
        public static List<int> Contour(string phrase)
        {
            int n = Math.Max(1, Math.Min(10, Syllables(phrase)));
            bool excited = phrase != null && phrase.IndexOf('!') >= 0;
            var rng = new SeededRandom(StableHash.Of("voice:" + phrase));
            var notes = new List<int>(n);
            int baseNote = 52;
            for (int i = 0; i < n; i++)
            {
                int step = rng.Range(-2, 3);
                int note = baseNote + step + (excited && i == n - 1 ? 7 : 0) - (excited ? 0 : i / 2);
                notes.Add(note);
            }
            return notes;
        }
    }
}
