using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CallerRetroBall.Logic
{
    /// <summary>The on-screen action buttons, in drawing order.</summary>
    public enum ControlButton { Shoot = 0, Pass = 1, Dunk = 2, Layup = 3, Call = 4 }

    /// <summary>One button's place: offset from the lower-right corner of the safe area (canvas units) and size.</summary>
    public struct ControlSlot
    {
        public ControlButton Button;
        public float X, Y;
        public float Size;

        public ControlSlot(ControlButton b, float x, float y, float size)
        {
            Button = b;
            X = x;
            Y = y;
            Size = size;
        }
    }

    /// <summary>
    /// The touch button layout: a thumb arc in the lower-right corner (SHOOT biggest on the outside,
    /// PASS beside it, DUNK above, LAYUP on the diagonal, CALL further in), and the player's own
    /// arrangement from Settings ► CUSTOMIZE CONTROLS, saved as a short string.
    /// </summary>
    public static class ControlLayout
    {
        public const float MinScale = 0.7f;
        public const float MaxScale = 1.5f;
        /// <summary>Furthest a button may sit from the corner (canvas units), so it can't be lost off screen.</summary>
        public const float MaxReachX = 1040f, MaxReachY = 1000f;

        /// <summary>
        /// I131 (19 Nov 2026, Omari: "spread the controls out a bit more for easier thumb pressing"): SPREAD is the new
        /// default. The same arc, with roughly 100 units between rings instead of 50-70, and a bigger STEAL/DUNK button.
        /// CLASSIC is the old, tighter arc.
        /// </summary>
        public const int Spread = 0, Classic = 1;
        public static readonly string[] PresetNames = { "SPREAD", "CLASSIC" };
        public static int ClampPreset(int p) => p == Classic ? Classic : Spread;

        public static ControlSlot[] Default() => Preset(Spread);

        public static ControlSlot[] Preset(int preset)
        {
            if (ClampPreset(preset) == Classic)
                return new[]
                {
                    new ControlSlot(ControlButton.Shoot, -190f, 215f, 250f),
                    new ControlSlot(ControlButton.Pass, -455f, 140f, 190f),
                    new ControlSlot(ControlButton.Dunk, -160f, 480f, 175f),
                    new ControlSlot(ControlButton.Layup, -405f, 375f, 150f),
                    new ControlSlot(ControlButton.Call, -640f, 285f, 120f),
                };
            return new[]
            {
                new ControlSlot(ControlButton.Shoot, -185f, 235f, 260f),
                new ControlSlot(ControlButton.Pass, -500f, 150f, 200f),
                new ControlSlot(ControlButton.Dunk, -170f, 565f, 210f),
                new ControlSlot(ControlButton.Layup, -455f, 445f, 165f),
                new ControlSlot(ControlButton.Call, -700f, 600f, 130f),
            };
        }

        /// <summary>Size limits are measured against the SPREAD sizes (CLASSIC's all sit inside them).</summary>
        public static float DefaultSize(ControlButton b) => Default()[(int)b].Size;

        /// <summary>
        /// I172 (20 Nov 2026): in landscape (Full Court, or every game with LANDSCAPE: ALL GAMES) the buttons are drawn at
        /// 90% so more of the wide court shows; the gaps shrink with them, so touch areas still never overlap.
        /// </summary>
        public const float LandscapeScale = 0.9f;

        public static float ScaleFor(bool landscape) => landscape ? LandscapeScale : 1f;

        /// <summary>Space between two rings' edges (canvas units; negative = they overlap).</summary>
        public static float Gap(ControlSlot a, ControlSlot b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy) - (a.Size + b.Size) * 0.5f;
        }

        /// <summary>Rings closer than this are flagged TOO CLOSE in the editor (easy to hit the wrong one).</summary>
        public const float MinComfortGap = 40f;
        /// <summary>Most a touch area grows past the drawn ring, as a share of the ring's size.</summary>
        public const float MaxHitGrow = 0.2f;

        /// <summary>
        /// I132: how far (canvas units) a button's touch area reaches past its drawn ring: up to 20% of its size, but
        /// never more than half the gap to its nearest neighbour, so two touch areas never overlap.
        /// </summary>
        public static float HitPad(ControlSlot[] slots, int index)
        {
            var me = slots[index];
            float pad = me.Size * MaxHitGrow * 0.5f;
            for (int i = 0; i < slots.Length; i++)
            {
                if (i == index) continue;
                float g = Gap(me, slots[i]);
                pad = Math.Min(pad, g * 0.5f - 2f);
            }
            return Math.Max(0f, pad);
        }

        public static List<ControlButton> TooClose(ControlSlot[] slots)
        {
            var list = new List<ControlButton>();
            for (int i = 0; i < slots.Length; i++)
                for (int j = i + 1; j < slots.Length; j++)
                    if (Gap(slots[i], slots[j]) < MinComfortGap)
                    {
                        if (!list.Contains(slots[i].Button)) list.Add(slots[i].Button);
                        if (!list.Contains(slots[j].Button)) list.Add(slots[j].Button);
                    }
            return list;
        }

        /// <summary>Keeps a slot on screen and within the size limits.</summary>
        public static ControlSlot Clamp(ControlSlot s)
        {
            float baseSize = DefaultSize(s.Button);
            s.Size = Math.Max(baseSize * MinScale, Math.Min(baseSize * MaxScale, s.Size));
            float half = s.Size * 0.5f;
            s.X = Math.Max(-MaxReachX, Math.Min(-half, s.X));
            s.Y = Math.Max(half, Math.Min(MaxReachY, s.Y));
            return s;
        }

        /// <summary>"button:x,y,size;..." (invariant culture). Only buttons that differ from the default are written.</summary>
        public static string Serialize(ControlSlot[] slots) => Serialize(slots, Spread);

        /// <summary>As above, against <paramref name="preset"/>'s positions (only buttons moved from it are written).</summary>
        public static string Serialize(ControlSlot[] slots, int preset)
        {
            if (slots == null) return "";
            var d = Preset(preset);
            var sb = new StringBuilder();
            foreach (var raw in slots)
            {
                var s = Clamp(raw);
                var def = d[(int)s.Button];
                if (Math.Abs(s.X - def.X) < 0.5f && Math.Abs(s.Y - def.Y) < 0.5f && Math.Abs(s.Size - def.Size) < 0.5f) continue;
                if (sb.Length > 0) sb.Append(';');
                sb.Append((int)s.Button).Append(':')
                  .Append(Math.Round(s.X).ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(Math.Round(s.Y).ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(Math.Round(s.Size).ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        /// <summary>The default layout with any saved changes applied. Bad entries are ignored.</summary>
        public static ControlSlot[] Parse(string saved) => Parse(saved, Spread);

        /// <summary><paramref name="preset"/>'s layout with any saved changes applied.</summary>
        public static ControlSlot[] Parse(string saved, int preset)
        {
            var slots = Preset(preset);
            if (string.IsNullOrEmpty(saved)) return slots;
            foreach (var part in saved.Split(';'))
            {
                int colon = part.IndexOf(':');
                if (colon <= 0) continue;
                if (!int.TryParse(part.Substring(0, colon), NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)) continue;
                if (id < 0 || id >= slots.Length) continue;
                var nums = part.Substring(colon + 1).Split(',');
                if (nums.Length != 3) continue;
                if (!float.TryParse(nums[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)) continue;
                if (!float.TryParse(nums[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)) continue;
                if (!float.TryParse(nums[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float size)) continue;
                if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(size)) continue;
                slots[id] = Clamp(new ControlSlot((ControlButton)id, x, y, size));
            }
            return slots;
        }

        /// <summary>A saved string with bad entries dropped and values clamped (whatever preset it was made against).</summary>
        public static string Clean(string saved)
        {
            if (string.IsNullOrEmpty(saved)) return "";
            var seen = new bool[Default().Length];
            var sb = new StringBuilder();
            foreach (var part in saved.Split(';'))
            {
                var one = Parse(part, Spread);
                int colon = part.IndexOf(':');
                if (colon <= 0 || !int.TryParse(part.Substring(0, colon), NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)) continue;
                if (id < 0 || id >= one.Length || seen[id]) continue;
                // Parse ignores a bad entry (the button keeps its default), so only keep entries that actually parsed.
                if (!EntryParses(part)) continue;
                seen[id] = true;
                var c = one[id];
                if (sb.Length > 0) sb.Append(';');
                sb.Append(id).Append(':')
                  .Append(Math.Round(c.X).ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(Math.Round(c.Y).ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(Math.Round(c.Size).ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        private static bool EntryParses(string part)
        {
            int colon = part.IndexOf(':');
            if (colon <= 0) return false;
            var nums = part.Substring(colon + 1).Split(',');
            if (nums.Length != 3) return false;
            foreach (var n in nums)
                if (!float.TryParse(n, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) || float.IsNaN(v)) return false;
            return true;
        }

        /// <summary>True when two buttons overlap by more than a sliver (the editor warns about it).</summary>
        public static bool Overlaps(ControlSlot a, ControlSlot b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y;
            float r = (a.Size + b.Size) * 0.5f * 0.9f;
            return dx * dx + dy * dy < r * r;
        }

        public static List<ControlButton> Overlapping(ControlSlot[] slots)
        {
            var list = new List<ControlButton>();
            for (int i = 0; i < slots.Length; i++)
                for (int j = i + 1; j < slots.Length; j++)
                    if (Overlaps(slots[i], slots[j]))
                    {
                        if (!list.Contains(slots[i].Button)) list.Add(slots[i].Button);
                        if (!list.Contains(slots[j].Button)) list.Add(slots[j].Button);
                    }
            return list;
        }
    }
}
