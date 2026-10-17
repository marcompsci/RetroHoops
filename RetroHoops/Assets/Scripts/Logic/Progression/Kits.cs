using System;
using System.Collections.Generic;
using System.Text;

namespace CallerRetroBall.Logic
{
    public enum JerseyCut { Tank = 0, Tee = 1, LongSleeve = 2 }
    public enum CollarStyle { V = 0, Crew = 1, None = 2 }
    public enum SideStripe { None = 0, Single = 1, Double = 2 }
    public enum ChestMark { Logo = 0, Band = 1, Number = 2 }
    public enum ShortsLength { Classic = 0, Long = 1, Short = 2 }
    public enum ShoeTop { Low = 0, Mid = 1, High = 2 }

    /// <summary>
    /// One kit from the Kit Studio: every colour and style choice for jersey, shorts and shoes.
    /// Colours are stored as 15-bit RGB (5 bits a channel, 32 steps) so a kit fits in a short share code.
    /// </summary>
    [Serializable]
    public class KitData
    {
        public string name = "HOME";
        // Colours (RGB555: see Kits.Pack / Kits.Unpack).
        public int jersey;
        public int trim;
        public int accent;
        public int shorts;
        public int shortsTrim;
        public int shoe;
        public int sole;
        public int laces;
        public int shoeStripe;
        // Styles.
        public int cut;
        public int collar;
        public int sides;
        public int chest;
        public int pattern;
        public int length;
        public bool shortsStripe = true;
        public bool waistband;
        public int top;
        public bool shoeStripeOn;

        public KitData Clone() => (KitData)MemberwiseClone();

        public bool SameLook(KitData o) =>
            o != null && jersey == o.jersey && trim == o.trim && accent == o.accent && shorts == o.shorts && shortsTrim == o.shortsTrim
            && shoe == o.shoe && sole == o.sole && laces == o.laces && shoeStripe == o.shoeStripe && cut == o.cut && collar == o.collar
            && sides == o.sides && chest == o.chest && pattern == o.pattern && length == o.length && shortsStripe == o.shortsStripe
            && waistband == o.waistband && top == o.top && shoeStripeOn == o.shoeStripeOn;
    }

    /// <summary>Your three kits and which one you wear.</summary>
    [Serializable]
    public class KitSaveData
    {
        /// <summary>False until you save a kit in the Kit Studio (until then your team looks as before).</summary>
        public bool designed;
        /// <summary>0 = Home, 1 = Away, 2 = Alt: the kit you wear unless it clashes.</summary>
        public int wear;
        /// <summary>Switch to the Away kit when your kit would look like the opponent's.</summary>
        public bool autoAway = true;
        public List<KitData> slots = new List<KitData>();
    }

    /// <summary>Everything the sprite generator needs to dress a player (runtime only).</summary>
    public struct KitLook
    {
        public RgbColor Jersey, Trim, Accent, Shorts, ShortsTrim, Shoe, Sole, Laces, ShoeStripe;
        public JerseyCut Cut;
        public CollarStyle Collar;
        public SideStripe Sides;
        public ChestMark Chest;
        public TeamPattern Pattern;
        public ShortsLength Length;
        public bool ShortsStripe, Waistband, ShoeStripeOn;
        public ShoeTop Top;
        /// <summary>I3: sleeves, ink and goggles on this player (none on everyone but you), in <see cref="GearColor"/>.</summary>
        public GearParts Gear;
        public RgbColor GearColor;

        /// <summary>The classic look (tank, V-neck, low tops) in the given colours: exactly the original sprites.</summary>
        public static KitLook Classic(RgbColor jersey, RgbColor trim, RgbColor accent, RgbColor? shoes, TeamPattern pattern, RgbColor? shorts)
        {
            var shoe = shoes ?? new RgbColor(0xF2, 0xF2, 0xF2);
            return new KitLook
            {
                Jersey = jersey, Trim = trim, Accent = accent,
                Shorts = shorts ?? jersey.Darken(0.12f), ShortsTrim = trim,
                Shoe = shoe, Sole = new RgbColor(0x9A, 0x9A, 0xA4), Laces = shoe, ShoeStripe = shoe,
                Cut = JerseyCut.Tank, Collar = CollarStyle.V, Sides = SideStripe.None, Chest = ChestMark.Logo,
                Pattern = pattern, Length = ShortsLength.Classic, ShortsStripe = true, Waistband = false,
                Top = ShoeTop.Low, ShoeStripeOn = false,
            };
        }
    }

    /// <summary>A style that has to be earned before the Kit Studio offers it.</summary>
    public sealed class KitUnlock
    {
        public string Part;
        public int Value;
        public string Hint;
        public Func<CareerSaveData, bool> Earned;
    }

    /// <summary>
    /// The Kit Studio rules: colours, styles, unlocks, three kit slots, clash handling and randomising.
    /// Pure logic (tested); the Locker Room screen and the match read it.
    /// </summary>
    public static class Kits
    {
        public const int SlotCount = 3;
        public static readonly string[] SlotNames = { "HOME", "AWAY", "ALT" };

        public const string PartCut = "cut", PartCollar = "collar", PartSides = "sides", PartChest = "chest", PartPattern = "pattern",
                            PartLength = "length", PartTop = "top";

        public static readonly string[] CutNames = { "TANK", "TEE", "LONG SLEEVE" };
        public static readonly string[] CollarNames = { "V-NECK", "CREW", "NONE" };
        public static readonly string[] SideNames = { "NONE", "SINGLE", "DOUBLE" };
        public static readonly string[] ChestNames = { "LOGO", "BAND", "NUMBER" };
        public static readonly string[] LengthNames = { "CLASSIC", "LONG", "SHORT" };
        public static readonly string[] TopNames = { "LOW", "MID", "HIGH" };
        public static string[] PatternNames => CustomTeams.PatternNames;

        // ------------------------------------------------------------------ colour

        /// <summary>RGB → 15-bit value (5 bits a channel).</summary>
        public static int Pack(RgbColor c) => ((c.r >> 3) << 10) | ((c.g >> 3) << 5) | (c.b >> 3);

        public static RgbColor Unpack(int v)
        {
            v &= 0x7FFF;
            return new RgbColor(Expand((v >> 10) & 31), Expand((v >> 5) & 31), Expand(v & 31));
        }

        /// <summary>5-bit channel → 0..255 (31 maps to 255 exactly).</summary>
        public static byte Expand(int c5) => (byte)((c5 * 255 + 15) / 31);

        /// <summary>Snaps a colour to the 32-step grid kits are stored on.</summary>
        public static RgbColor Snap(RgbColor c) => Unpack(Pack(c));

        /// <summary>The 64-colour pixel palette: a grey row, then seven hues from dark to light.</summary>
        public static readonly RgbColor[] Palette64 = BuildPalette();

        private static RgbColor[] BuildPalette()
        {
            var list = new RgbColor[64];
            for (int i = 0; i < 8; i++)
            {
                byte v = (byte)Math.Round(i * 255.0 / 7.0);
                list[i] = Snap(new RgbColor(v, v, v));
            }
            float[] hues = { 0f, 28f, 50f, 120f, 180f, 220f, 280f };
            for (int row = 0; row < hues.Length; row++)
                for (int i = 0; i < 8; i++)
                {
                    // Dark & rich on the left, light & soft on the right.
                    float value = 0.25f + 0.75f * Math.Min(1f, i / 4.5f);
                    float sat = i < 5 ? 0.95f : 0.95f - (i - 4) * 0.22f;
                    list[8 + row * 8 + i] = Snap(Hsv(hues[row], sat, value));
                }
            return list;
        }

        public static RgbColor Hsv(float h, float s, float v)
        {
            h = ((h % 360f) + 360f) % 360f;
            float c = v * s, x = c * (1f - Math.Abs((h / 60f) % 2f - 1f)), m = v - c;
            float r, g, b;
            if (h < 60f) { r = c; g = x; b = 0f; }
            else if (h < 120f) { r = x; g = c; b = 0f; }
            else if (h < 180f) { r = 0f; g = c; b = x; }
            else if (h < 240f) { r = 0f; g = x; b = c; }
            else if (h < 300f) { r = x; g = 0f; b = c; }
            else { r = c; g = 0f; b = x; }
            byte B(float f) => (byte)Math.Round(Math.Max(0f, Math.Min(1f, f + m)) * 255f);
            return new RgbColor(B(r), B(g), B(b));
        }

        // ------------------------------------------------------------------ kits

        /// <summary>A kit in a team's colours (classic styles).</summary>
        public static KitData FromColors(string name, RgbColor jersey, RgbColor trim, RgbColor accent, RgbColor shorts, RgbColor shoe)
        {
            return new KitData
            {
                name = name,
                jersey = Pack(jersey), trim = Pack(trim), accent = Pack(accent),
                shorts = Pack(shorts), shortsTrim = Pack(trim),
                shoe = Pack(shoe), sole = Pack(new RgbColor(0x9A, 0x9A, 0xA4)), laces = Pack(shoe), shoeStripe = Pack(trim),
            };
        }

        /// <summary>
        /// Starting kits for the studio: Home in your current colours (crew, custom team, or equipped
        /// jersey and shoes), Away in the trim colour, Alt in dark colours.
        /// </summary>
        public static List<KitData> Defaults(RgbColor jersey, RgbColor trim, RgbColor accent, RgbColor? shorts, RgbColor? shoe)
        {
            var white = new RgbColor(0xF2, 0xF2, 0xF2);
            var home = FromColors("HOME", jersey, trim, accent, shorts ?? jersey.Darken(0.12f), shoe ?? white);
            var away = FromColors("AWAY", trim, jersey, accent, trim.Darken(0.12f), shoe ?? white);
            var dark = RgbColor.FromHex("#1A1A1F");
            var alt = FromColors("ALT", dark, jersey, accent, dark, jersey);
            alt.collar = (int)CollarStyle.Crew;
            return new List<KitData> { home, away, alt };
        }

        /// <summary>Makes sure there are three valid slots (creating them from your colours if needed).</summary>
        public static void Ensure(KitSaveData s, RgbColor jersey, RgbColor trim, RgbColor accent, RgbColor? shorts, RgbColor? shoe)
        {
            if (s.slots == null) s.slots = new List<KitData>();
            var defaults = Defaults(jersey, trim, accent, shorts, shoe);
            while (s.slots.Count < SlotCount) s.slots.Add(defaults[s.slots.Count]);
            if (s.slots.Count > SlotCount) s.slots.RemoveRange(SlotCount, s.slots.Count - SlotCount);
            for (int i = 0; i < SlotCount; i++)
            {
                if (s.slots[i] == null) s.slots[i] = defaults[i];
                Clamp(s.slots[i]);
                if (string.IsNullOrEmpty(s.slots[i].name)) s.slots[i].name = SlotNames[i];
            }
            s.wear = Mod(s.wear, SlotCount);
        }

        public static void Clamp(KitData k)
        {
            if (k == null) return;
            k.jersey &= 0x7FFF; k.trim &= 0x7FFF; k.accent &= 0x7FFF; k.shorts &= 0x7FFF; k.shortsTrim &= 0x7FFF;
            k.shoe &= 0x7FFF; k.sole &= 0x7FFF; k.laces &= 0x7FFF; k.shoeStripe &= 0x7FFF;
            k.cut = Mod(k.cut, CutNames.Length);
            k.collar = Mod(k.collar, CollarNames.Length);
            k.sides = Mod(k.sides, SideNames.Length);
            k.chest = Mod(k.chest, ChestNames.Length);
            k.pattern = Mod(k.pattern, CustomTeams.PatternNames.Length);
            k.length = Mod(k.length, LengthNames.Length);
            k.top = Mod(k.top, TopNames.Length);
            k.name = CustomTeams.Clean(k.name, 10, "KIT").ToUpperInvariant();
            if (!TextSafety.IsClean(k.name)) k.name = "KIT"; // shared kit codes (App Review 1.2)
        }

        public static KitLook Look(KitData k)
        {
            return new KitLook
            {
                Jersey = Unpack(k.jersey), Trim = Unpack(k.trim), Accent = Unpack(k.accent),
                Shorts = Unpack(k.shorts), ShortsTrim = Unpack(k.shortsTrim),
                Shoe = Unpack(k.shoe), Sole = Unpack(k.sole), Laces = Unpack(k.laces), ShoeStripe = Unpack(k.shoeStripe),
                Cut = (JerseyCut)Mod(k.cut, 3), Collar = (CollarStyle)Mod(k.collar, 3), Sides = (SideStripe)Mod(k.sides, 3),
                Chest = (ChestMark)Mod(k.chest, 3), Pattern = (TeamPattern)Mod(k.pattern, CustomTeams.PatternNames.Length),
                Length = (ShortsLength)Mod(k.length, 3), ShortsStripe = k.shortsStripe, Waistband = k.waistband,
                Top = (ShoeTop)Mod(k.top, 3), ShoeStripeOn = k.shoeStripeOn,
            };
        }

        /// <summary>
        /// The kit to wear in a game: your chosen kit, or the Away kit when the chosen one would look like
        /// the opponent's jersey (and Away doesn't).
        /// </summary>
        public static KitData ForMatch(KitSaveData s, RgbColor opponentJersey, ColorFilter filter, out bool switchedToAway)
        {
            switchedToAway = false;
            if (s == null || !s.designed || s.slots == null || s.slots.Count < SlotCount) return null;
            var chosen = s.slots[Mod(s.wear, SlotCount)];
            if (!s.autoAway || s.wear == 1) return chosen;
            var away = s.slots[1];
            if (ColorAccess.KitsClash(Unpack(chosen.jersey), opponentJersey, filter)
                && ColorAccess.PerceivedDistance(Unpack(away.jersey), opponentJersey, filter) > ColorAccess.PerceivedDistance(Unpack(chosen.jersey), opponentJersey, filter))
            {
                switchedToAway = true;
                return away;
            }
            return chosen;
        }

        // ------------------------------------------------------------------ unlocks

        public static readonly List<KitUnlock> Unlocks = new List<KitUnlock>
        {
            U(PartCut, (int)JerseyCut.LongSleeve, "Win 10 games.", d => d.totals.wins >= 10),
            U(PartChest, (int)ChestMark.Number, "Hit 25 GREEN releases.", d => d.totals.greens >= 25),
            U(PartSides, (int)SideStripe.Double, "Throw or finish 3 alley-oops.", d => d.totals.alleyOops >= 3),
            U(PartTop, (int)ShoeTop.High, "Heat up 3 times.", d => d.totals.heatUps >= 3),
            U(PartLength, (int)ShortsLength.Long, "Play a Full Court game.", d => d.history != null && d.history.Exists(h => h.mode == GameMode.FullCourt || h.mode == GameMode.Franchise)),
            U(PartPattern, (int)TeamPattern.Chevrons, "Win the First Call Classic.", d => d.classic != null && d.classic.titles >= 1),
            U(PartPattern, (int)TeamPattern.Checker, "Clear the Arcade Ladder.", d => d.secrets != null && d.secrets.arcade != null && d.secrets.arcade.clears >= 1),
            U(PartPattern, (int)TeamPattern.Rings, "Win the Caller Cup.", d => d.cup != null && d.cup.titles >= 1),
            U(PartPattern, (int)TeamPattern.Cross, "Find 3 secret codes.", d => d.secrets != null && d.secrets.codesFound.Count >= 3),
        };

        private static KitUnlock U(string part, int value, string hint, Func<CareerSaveData, bool> earned) =>
            new KitUnlock { Part = part, Value = value, Hint = hint, Earned = earned };

        /// <summary>True when a style can be picked (styles without an unlock are always available).</summary>
        public static bool IsUnlocked(string part, int value, CareerSaveData d)
        {
            foreach (var u in Unlocks)
            {
                if (u.Part != part || u.Value != value) continue;
                try { return d != null && u.Earned(d); }
                catch (NullReferenceException) { return false; }
            }
            return true;
        }

        /// <summary>How to unlock a style, or null when it's free.</summary>
        public static string HintFor(string part, int value)
        {
            foreach (var u in Unlocks) if (u.Part == part && u.Value == value) return u.Hint;
            return null;
        }

        /// <summary>
        /// A fresh random kit (RANDOMIZE): a harmonious scheme around a random hue, using only unlocked
        /// styles. Deterministic for a given seed.
        /// </summary>
        public static KitData Randomize(uint seed, CareerSaveData d, string name = "KIT")
        {
            var rng = new SeededRandom(seed == 0 ? 1u : seed);
            float hue = rng.NextFloat() * 360f;
            int scheme = rng.Range(0, 3);
            float hue2 = scheme == 0 ? hue + 180f : (scheme == 1 ? hue + 150f : hue + 30f);
            var jersey = Hsv(hue, 0.55f + rng.NextFloat() * 0.4f, 0.45f + rng.NextFloat() * 0.5f);
            var trim = rng.NextFloat() < 0.35f ? (jersey.Luminance > 0.5 ? RgbColor.FromHex("#1A1A1F") : RgbColor.FromHex("#F4F1DE"))
                                                : Hsv(hue2, 0.7f, 0.85f);
            var accent = Hsv(hue2 + 40f, 0.8f, 0.95f);
            var k = new KitData
            {
                name = name,
                jersey = Pack(jersey), trim = Pack(trim), accent = Pack(accent),
                shorts = Pack(rng.NextFloat() < 0.6f ? jersey : trim), shortsTrim = Pack(rng.NextFloat() < 0.5f ? trim : accent),
                shoe = Pack(rng.NextFloat() < 0.4f ? RgbColor.FromHex("#F2F2F2") : (rng.NextFloat() < 0.5f ? jersey : trim)),
                sole = Pack(rng.NextFloat() < 0.5f ? RgbColor.FromHex("#9A9AA4") : RgbColor.FromHex("#F4F1DE")),
                laces = Pack(accent), shoeStripe = Pack(rng.NextFloat() < 0.5f ? trim : accent),
                shortsStripe = rng.NextFloat() < 0.7f, waistband = rng.NextFloat() < 0.4f, shoeStripeOn = rng.NextFloat() < 0.5f,
            };
            k.cut = PickUnlocked(rng, PartCut, CutNames.Length, d);
            k.collar = PickUnlocked(rng, PartCollar, CollarNames.Length, d);
            k.sides = PickUnlocked(rng, PartSides, SideNames.Length, d);
            k.chest = PickUnlocked(rng, PartChest, ChestNames.Length, d);
            k.pattern = rng.NextFloat() < 0.5f ? 0 : PickUnlocked(rng, PartPattern, CustomTeams.PatternNames.Length, d);
            k.length = PickUnlocked(rng, PartLength, LengthNames.Length, d);
            k.top = PickUnlocked(rng, PartTop, TopNames.Length, d);
            return k;
        }

        private static int PickUnlocked(SeededRandom rng, string part, int count, CareerSaveData d)
        {
            for (int tries = 0; tries < 8; tries++)
            {
                int v = rng.Range(0, count);
                if (IsUnlocked(part, v, d)) return v;
            }
            return 0;
        }

        private static int Mod(int v, int n) => ((v % n) + n) % n;

        // ------------------------------------------------------------------ share codes

        /// <summary>Deep link that opens Retro Hoops and offers the kit (scan the QR with the iPhone camera).</summary>
        public const string LinkPrefix = "retrohoops://kit/";
        /// <summary>Links from before the game was renamed still open.</summary>
        public const string OldLinkPrefix = "retroball://kit/";
        private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ"; // Crockford base32
        private const int CodeVersion = 1;

        /// <summary>Short code for a kit (33 characters, shown in groups). The name isn't included.</summary>
        public static string Encode(KitData k)
        {
            Clamp(k);
            var bits = new BitWriter();
            bits.Write(CodeVersion, 3);
            foreach (int c in Colors(k)) bits.Write(c, 15);
            bits.Write(k.cut, 2); bits.Write(k.collar, 2); bits.Write(k.sides, 2); bits.Write(k.chest, 2);
            bits.Write(k.pattern, 3); bits.Write(k.length, 2); bits.Write(k.shortsStripe ? 1 : 0, 1);
            bits.Write(k.waistband ? 1 : 0, 1); bits.Write(k.top, 2); bits.Write(k.shoeStripeOn ? 1 : 0, 1);
            bits.Write(bits.Checksum(), 8);
            var sb = new StringBuilder();
            foreach (int v in bits.Groups5()) sb.Append(Alphabet[v]);
            return sb.ToString();
        }

        /// <summary>Code split into groups of five for reading out ("KR4ZT-...").</summary>
        public static string Pretty(string code)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < code.Length; i++)
            {
                if (i > 0 && i % 5 == 0) sb.Append('-');
                sb.Append(code[i]);
            }
            return sb.ToString();
        }

        /// <summary>Reads a code or a retrohoops://kit/ (or old retroball://kit/) link. Forgiving: case, dashes, spaces, I/L/O typos.</summary>
        public static bool TryDecode(string text, out KitData kit)
        {
            kit = null;
            if (string.IsNullOrEmpty(text)) return false;
            string s = text.Trim();
            int link = s.IndexOf(LinkPrefix, StringComparison.OrdinalIgnoreCase);
            if (link >= 0) s = s.Substring(link + LinkPrefix.Length);
            else
            {
                link = s.IndexOf(OldLinkPrefix, StringComparison.OrdinalIgnoreCase);
                if (link >= 0) s = s.Substring(link + OldLinkPrefix.Length);
            }
            var values = new List<int>();
            foreach (char raw in s)
            {
                char ch = char.ToUpperInvariant(raw);
                if (ch == '-' || ch == ' ' || ch == '/') continue;
                if (ch == 'I' || ch == 'L') ch = '1';
                if (ch == 'O') ch = '0';
                int v = Alphabet.IndexOf(ch);
                if (v < 0) return false;
                values.Add(v);
            }
            var reader = BitReader.FromGroups5(values);
            int bitsNeeded = 3 + 9 * 15 + 18 + 8;
            if (reader.Length < bitsNeeded) return false;
            if (reader.Read(3) != CodeVersion) return false;
            var colors = new int[9];
            for (int i = 0; i < 9; i++) colors[i] = reader.Read(15);
            var k = new KitData
            {
                name = "SHARED",
                jersey = colors[0], trim = colors[1], accent = colors[2], shorts = colors[3], shortsTrim = colors[4],
                shoe = colors[5], sole = colors[6], laces = colors[7], shoeStripe = colors[8],
                cut = reader.Read(2), collar = reader.Read(2), sides = reader.Read(2), chest = reader.Read(2),
                pattern = reader.Read(3), length = reader.Read(2), shortsStripe = reader.Read(1) == 1,
                waistband = reader.Read(1) == 1, top = reader.Read(2), shoeStripeOn = reader.Read(1) == 1,
            };
            int expected = reader.ChecksumSoFar();
            if (reader.Read(8) != expected) return false;
            if (k.cut > 2 || k.collar > 2 || k.sides > 2 || k.chest > 2 || k.length > 2 || k.top > 2
                || k.pattern >= CustomTeams.PatternNames.Length) return false;
            kit = k;
            return true;
        }

        private static IEnumerable<int> Colors(KitData k)
        {
            yield return k.jersey; yield return k.trim; yield return k.accent; yield return k.shorts; yield return k.shortsTrim;
            yield return k.shoe; yield return k.sole; yield return k.laces; yield return k.shoeStripe;
        }

        private sealed class BitWriter
        {
            private readonly List<bool> _bits = new List<bool>();

            public void Write(int value, int count)
            {
                for (int i = count - 1; i >= 0; i--) _bits.Add(((value >> i) & 1) == 1);
            }

            public int Checksum() => Sum(_bits, _bits.Count);

            public IEnumerable<int> Groups5()
            {
                for (int i = 0; i < _bits.Count; i += 5)
                {
                    int v = 0;
                    for (int j = 0; j < 5; j++) v = (v << 1) | (i + j < _bits.Count && _bits[i + j] ? 1 : 0);
                    yield return v;
                }
            }

            /// <summary>
            /// CRC-8 (polynomial x⁸+x²+x+1) over the bits so far. It catches every error burst up to 8 bits
            /// long, so any single mistyped character (5 bits) is always caught.
            /// </summary>
            public static int Sum(List<bool> bits, int count)
            {
                int crc = 0;
                for (int i = 0; i < count; i++)
                {
                    bool top = ((crc >> 7) & 1) != 0;
                    crc = (crc << 1) & 0xFF;
                    if (top ^ bits[i]) crc ^= 0x07;
                }
                return crc;
            }
        }

        private sealed class BitReader
        {
            private readonly List<bool> _bits = new List<bool>();
            private int _pos;
            public int Length => _bits.Count;

            public static BitReader FromGroups5(List<int> groups)
            {
                var r = new BitReader();
                foreach (int g in groups)
                    for (int i = 4; i >= 0; i--) r._bits.Add(((g >> i) & 1) == 1);
                return r;
            }

            public int Read(int count)
            {
                int v = 0;
                for (int i = 0; i < count; i++) v = (v << 1) | (_pos < _bits.Count && _bits[_pos++] ? 1 : 0);
                return v;
            }

            public int ChecksumSoFar() => BitWriter.Sum(_bits, _pos);
        }
    }
}
