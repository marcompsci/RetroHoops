using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>Your own team (Locker Room ► TEAM): name, colours, kit, logo, home court.</summary>
    [Serializable]
    public class CustomTeamData
    {
        public bool created;
        public string city = "";
        public string nickname = "Callers";
        public string abbreviation = "YOU";
        /// <summary>Indexes into <see cref="CustomTeams.Palette"/>.</summary>
        public int primary = 0;
        public int secondary = 15;
        public int accent = 5;
        public int shorts = 0;
        public int shoes = 14;
        public int pattern;
        public int logoShape;
        public int logoMotif = (int)LogoMotif.Ball;
        public string homeCourtId = "court.overpass_park";
        /// <summary>Your Rise Mode crew wears this team's name, colours and logo too.</summary>
        public bool useInRise;
    }

    /// <summary>
    /// Builds a playable team from <see cref="CustomTeamData"/>. Your team is your player plus your
    /// crew (the same people as Rise Mode), dressed in your colours. It plays Quick Call, King of the
    /// Court, the Arcade Ladder and 2 Player.
    /// </summary>
    public static class CustomTeams
    {
        public const string TeamId = "team.custom";
        public const int MaxNameLength = 14;
        public const int MaxCityLength = 12;
        public const int MaxAbbreviation = 4;

        public static readonly string[] PaletteNames =
        {
            "RED", "ORANGE", "GOLD", "LIME", "GREEN", "TEAL", "SKY", "BLUE",
            "NAVY", "PURPLE", "PINK", "MAROON", "BROWN", "SILVER", "WHITE", "BLACK",
        };

        public static readonly RgbColor[] Palette =
        {
            RgbColor.FromHex("#E63946"), RgbColor.FromHex("#FF8C42"), RgbColor.FromHex("#FFD166"), RgbColor.FromHex("#A7E34B"),
            RgbColor.FromHex("#2A9D4F"), RgbColor.FromHex("#1FB5A6"), RgbColor.FromHex("#4CC9F0"), RgbColor.FromHex("#3A5BD9"),
            RgbColor.FromHex("#14213D"), RgbColor.FromHex("#7B2CBF"), RgbColor.FromHex("#F72585"), RgbColor.FromHex("#7A1E2C"),
            RgbColor.FromHex("#7F5539"), RgbColor.FromHex("#A8B2BD"), RgbColor.FromHex("#F4F1DE"), RgbColor.FromHex("#1A1A1F"),
        };

        public static readonly string[] PatternNames = { "SOLID", "STRIPES", "DOTS", "CHEVRONS", "CHECKER", "DIAGONAL", "RINGS", "CROSS" };
        public static readonly string[] ShapeNames = { "CIRCLE", "SHIELD", "DIAMOND", "HEXAGON", "BADGE" };
        public static readonly string[] MotifNames = { "BOLT", "WAVE", "PAW", "TREE", "COMET", "DUNE", "OWL", "CROWN", "BALL", "SIGNAL", "CRANE", "LIGHTHOUSE" };

        public static RgbColor Color(int index) => Palette[Mod(index, Palette.Length)];

        private static int Mod(int v, int n) => ((v % n) + n) % n;

        /// <summary>Keeps a draft valid: names trimmed to letters/digits/spaces, indexes in range, a real court.</summary>
        public static void Clamp(CustomTeamData d, ContentCatalog c)
        {
            if (d == null) return;
            d.nickname = Clean(d.nickname, MaxNameLength, "Callers");
            d.city = Clean(d.city, MaxCityLength, "");
            string abbr = Clean(d.abbreviation, MaxAbbreviation, "").Replace(" ", "").ToUpperInvariant();
            if (abbr.Length == 0) abbr = AbbreviationFrom(d.nickname);
            d.abbreviation = abbr;
            d.primary = Mod(d.primary, Palette.Length);
            d.secondary = Mod(d.secondary, Palette.Length);
            d.accent = Mod(d.accent, Palette.Length);
            d.shorts = Mod(d.shorts, Palette.Length);
            d.shoes = Mod(d.shoes, Palette.Length);
            d.pattern = Mod(d.pattern, PatternNames.Length);
            d.logoShape = Mod(d.logoShape, ShapeNames.Length);
            d.logoMotif = Mod(d.logoMotif, MotifNames.Length);
            if (c != null)
            {
                var court = c.Court(d.homeCourtId);
                if (court == null || court.circuit == CourtCircuit.Practice || court.circuit == CourtCircuit.Secret)
                    d.homeCourtId = "court.overpass_park";
            }
        }

        public static string Clean(string s, int max, string fallback)
        {
            if (string.IsNullOrEmpty(s)) return fallback;
            var chars = new List<char>(s.Length);
            foreach (char ch in s.Trim())
            {
                if (char.IsLetterOrDigit(ch) || ch == ' ' || ch == '-' || ch == '\'') chars.Add(ch);
                if (chars.Count == max) break;
            }
            string result = new string(chars.ToArray()).Trim();
            return result.Length == 0 ? fallback : result;
        }

        public static string AbbreviationFrom(string name)
        {
            var letters = new List<char>();
            foreach (char ch in name ?? "") if (char.IsLetter(ch)) letters.Add(char.ToUpperInvariant(ch));
            if (letters.Count == 0) return "YOU";
            return new string(letters.GetRange(0, Math.Min(3, letters.Count)).ToArray());
        }

        /// <summary>Courts you can choose as home: every street and league court (not practice or hidden ones).</summary>
        public static List<CourtDef> HomeCourts(ContentCatalog c) => HomeCourts(c, null);

        /// <summary>As above, plus the SHOP courts this career owns (I143).</summary>
        public static List<CourtDef> HomeCourts(ContentCatalog c, CareerSaveData career) =>
            c.Courts.FindAll(x => x.circuit == CourtCircuit.Blacktop || x.circuit == CourtCircuit.League || x.circuit == CourtCircuit.Custom
                                  || (x.circuit == CourtCircuit.Shop && career != null && Shop.CourtOpen(career, x.id)));

        /// <summary>
        /// Puts (or refreshes) your team in the catalog so matches, logos and menus can find it, and
        /// dresses the Rise crew in it when asked. Safe to call after every edit.
        /// </summary>
        public static TeamDef Apply(ContentCatalog c, CustomTeamData d)
        {
            if (c == null) return null;
            var existing = c.Team(TeamId);
            if (d == null || !d.created)
            {
                if (existing != null) c.Teams.Remove(existing);
                RestoreCrew(c);
                return null;
            }
            Clamp(d, c);
            var crew = c.Team(DefaultContent.PlayerCrewId);
            var team = existing ?? new TeamDef { id = TeamId, tier = TeamTier.Custom };
            team.city = d.city;
            team.nickname = d.nickname;
            team.abbreviation = UniqueAbbreviation(c, d.abbreviation);
            team.primary = Color(d.primary);
            team.secondary = Color(d.secondary);
            team.accent = Color(d.accent);
            team.logoShape = (LogoShape)d.logoShape;
            team.logoMotif = (LogoMotif)d.logoMotif;
            team.pattern = (TeamPattern)d.pattern;
            team.homeCourtId = d.homeCourtId;
            team.motto = "Our court, our call.";
            team.customKit = true;
            team.shorts = Color(d.shorts);
            team.shoes = Color(d.shoes);
            team.rosterPlayerIds = crew != null ? new List<string>(crew.rosterPlayerIds) : new List<string>();
            if (existing == null) c.Teams.Add(team);

            if (d.useInRise) DressCrew(c, team);
            else RestoreCrew(c);
            return team;
        }

        private static string UniqueAbbreviation(ContentCatalog c, string abbr)
        {
            bool Taken(string a) => c.Teams.Exists(t => t.id != TeamId && t.id != DefaultContent.PlayerCrewId && t.tier != TeamTier.Franchise && t.abbreviation == a);
            if (!Taken(abbr)) return abbr;
            for (int i = 2; i < 10; i++)
            {
                string alt = (abbr.Length >= MaxAbbreviation ? abbr.Substring(0, MaxAbbreviation - 1) : abbr) + i;
                if (!Taken(alt)) return alt;
            }
            return "YOU";
        }

        private static void DressCrew(ContentCatalog c, TeamDef look)
        {
            var crew = c.Team(DefaultContent.PlayerCrewId);
            if (crew == null) return;
            if (c.CrewOriginalLook == null) c.CrewOriginalLook = Snapshot(crew);
            crew.city = look.city;
            crew.nickname = look.nickname;
            crew.primary = look.primary;
            crew.secondary = look.secondary;
            crew.accent = look.accent;
            crew.logoShape = look.logoShape;
            crew.logoMotif = look.logoMotif;
            crew.pattern = look.pattern;
            crew.customKit = true;
            crew.shorts = look.shorts;
            crew.shoes = look.shoes;
        }

        private static void RestoreCrew(ContentCatalog c)
        {
            var crew = c.Team(DefaultContent.PlayerCrewId);
            var o = c.CrewOriginalLook;
            if (crew == null || o == null) return;
            crew.city = o.city;
            crew.nickname = o.nickname;
            crew.primary = o.primary;
            crew.secondary = o.secondary;
            crew.accent = o.accent;
            crew.logoShape = o.logoShape;
            crew.logoMotif = o.logoMotif;
            crew.pattern = o.pattern;
            crew.customKit = false;
            c.CrewOriginalLook = null;
        }

        private static TeamDef Snapshot(TeamDef t) => new TeamDef
        {
            id = t.id, city = t.city, nickname = t.nickname, primary = t.primary, secondary = t.secondary, accent = t.accent,
            logoShape = t.logoShape, logoMotif = t.logoMotif, pattern = t.pattern,
        };

        /// <summary>Is this your team (your player and crew play for it)?</summary>
        public static bool IsYours(string teamId) => teamId == TeamId || teamId == DefaultContent.PlayerCrewId;
    }
}
