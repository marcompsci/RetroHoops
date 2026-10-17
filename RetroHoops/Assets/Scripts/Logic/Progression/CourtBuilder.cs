using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>One court from the Court Builder. Colours are RGB555 like kits.</summary>
    [Serializable]
    public class CustomCourtData
    {
        public bool built;
        public string name = "My Court";
        public int floorStyle;
        public int floor = 0x3CE7;
        public int lines = 0x7FFF;
        public int paint = 0x6C42;
        public int sky;
        /// <summary>0 (empty) .. 4 (packed).</summary>
        public int crowd = 2;
        public int stands;
        /// <summary>−1 = no centre logo.</summary>
        public int logoMotif = -1;
        public int logoShape;
        public int logoColor = 0x7EC0;

        public CustomCourtData Clone() => (CustomCourtData)MemberwiseClone();
    }

    /// <summary>A sky preset for a built court: top and bottom of the gradient.</summary>
    public sealed class SkyPreset
    {
        public string Name;
        public RgbColor Top, Bottom;
        public SkyPreset(string name, string top, string bottom) { Name = name; Top = RgbColor.FromHex(top); Bottom = RgbColor.FromHex(bottom); }
    }

    /// <summary>
    /// The Court Builder: up to three courts of your own (floor style and colours, paint, lines, what's
    /// behind the baseline, sky, crowd and a centre-court logo). Built courts join the catalog as
    /// <c>court.custom.N</c>, so they can be played in Quick Call, set as your team's home court, and
    /// are drawn by the same generator as every other court.
    /// </summary>
    public static class CourtBuilder
    {
        public const int Slots = 3;
        public const string IdPrefix = "court.custom.";
        public const int MaxName = 16;

        public static readonly string[] FloorNames = { "ASPHALT", "HARDWOOD", "NEON GRID", "TILES", "RUBBER" };
        public static readonly string[] StandNames = { "CROWD", "FENCE", "BRICK WALL" };
        public static readonly string[] CrowdNames = { "EMPTY", "SPARSE", "HALF FULL", "BUSY", "PACKED" };

        public static readonly SkyPreset[] Skies =
        {
            new SkyPreset("SUNSET", "#2B1A4F", "#FF8C42"),
            new SkyPreset("NOON", "#3A7BD5", "#BDE0FE"),
            new SkyPreset("DUSK", "#14213D", "#F72585"),
            new SkyPreset("MIDNIGHT", "#0B0B16", "#2A2A45"),
            new SkyPreset("STORM", "#2F3640", "#7F8C8D"),
            new SkyPreset("NEON", "#10002B", "#4CC9F0"),
            new SkyPreset("DAWN", "#5A189A", "#FFD166"),
            new SkyPreset("ARENA", "#1A1A2E", "#3A3A55"),
        };

        public static string Id(int slot) => IdPrefix + slot;

        public static int SlotOf(string courtId)
        {
            if (courtId == null || !courtId.StartsWith(IdPrefix, StringComparison.Ordinal)) return -1;
            return int.TryParse(courtId.Substring(IdPrefix.Length), out int i) && i >= 0 && i < Slots ? i : -1;
        }

        /// <summary>Makes sure there are exactly <see cref="Slots"/> entries with sane values.</summary>
        public static List<CustomCourtData> Ensure(List<CustomCourtData> list)
        {
            list = list ?? new List<CustomCourtData>();
            while (list.Count < Slots) list.Add(new CustomCourtData { name = "Court " + (list.Count + 1) });
            if (list.Count > Slots) list.RemoveRange(Slots, list.Count - Slots);
            foreach (var d in list) Clamp(d);
            return list;
        }

        public static void Clamp(CustomCourtData d)
        {
            if (d == null) return;
            d.name = CustomTeams.Clean(d.name, MaxName, "My Court");
            if (!TextSafety.IsClean(d.name)) d.name = "My Court"; // shared court codes (App Review 1.2)
            d.floorStyle = Math.Max(0, Math.Min(FloorNames.Length - 1, d.floorStyle));
            d.floor &= 0x7FFF;
            d.lines &= 0x7FFF;
            d.paint &= 0x7FFF;
            d.logoColor &= 0x7FFF;
            d.sky = Math.Max(0, Math.Min(Skies.Length - 1, d.sky));
            d.crowd = Math.Max(0, Math.Min(CrowdNames.Length - 1, d.crowd));
            d.stands = Math.Max(0, Math.Min(StandNames.Length - 1, d.stands));
            d.logoMotif = Math.Max(-1, Math.Min(10, d.logoMotif));
            d.logoShape = Math.Max(0, Math.Min(4, d.logoShape));
        }

        /// <summary>The court definition the generator draws.</summary>
        public static CourtDef Build(CustomCourtData d, int slot)
        {
            Clamp(d);
            var sky = Skies[d.sky];
            return new CourtDef
            {
                id = Id(slot),
                displayName = d.name,
                circuit = CourtCircuit.Custom,
                description = "Built in the Court Builder.",
                floor = Kits.Unpack(d.floor),
                lines = Kits.Unpack(d.lines),
                paint = Kits.Unpack(d.paint),
                skyTop = sky.Top,
                skyBottom = sky.Bottom,
                // Only stands hold a crowd.
                crowdDensity = d.stands == (int)StandsStyle.Crowd ? d.crowd / 4f : 0f,
                floorStyle = d.floorStyle,
                stands = (StandsStyle)d.stands,
                logoMotif = d.logoMotif,
                logoShape = (LogoShape)d.logoShape,
                logoColor = Kits.Unpack(d.logoColor),
            };
        }

        /// <summary>Puts your built courts in the catalog (and takes out deleted ones). Safe to call after every edit.</summary>
        public static void Apply(ContentCatalog c, List<CustomCourtData> courts)
        {
            if (c == null) return;
            courts = Ensure(courts);
            for (int i = 0; i < Slots; i++)
            {
                string id = Id(i);
                int at = c.Courts.FindIndex(x => x.id == id);
                if (!courts[i].built)
                {
                    if (at >= 0) c.Courts.RemoveAt(at);
                    continue;
                }
                var def = Build(courts[i], i);
                if (at >= 0) c.Courts[at] = def;
                else c.Courts.Add(def);
            }
        }

        /// <summary>A quick starting point in the builder: random but readable colours (lines stand out from the floor).</summary>
        public static CustomCourtData Randomize(uint seed, string name)
        {
            var rng = new SeededRandom(seed == 0 ? 1u : seed);
            var d = new CustomCourtData { built = false, name = name };
            d.floorStyle = rng.Range(0, FloorNames.Length);
            var floor = Kits.Palette64[rng.Range(0, Kits.Palette64.Length)];
            d.floor = Kits.Pack(floor);
            var lines = floor.Luminance > 0.5 ? RgbColor.FromHex("#1A1A1F") : RgbColor.FromHex("#F4F1DE");
            d.lines = Kits.Pack(lines);
            d.paint = Kits.Pack(Kits.Palette64[rng.Range(0, Kits.Palette64.Length)]);
            d.sky = rng.Range(0, Skies.Length);
            d.crowd = rng.Range(1, CrowdNames.Length);
            d.stands = rng.Range(0, StandNames.Length);
            d.logoMotif = rng.Range(-1, 11);
            d.logoShape = rng.Range(0, 5);
            d.logoColor = Kits.Pack(Kits.Palette64[rng.Range(0, Kits.Palette64.Length)]);
            return d;
        }

        /// <summary>Quick Call on a built court with your team.</summary>
        public static MatchRequest PlayHere(ContentCatalog c, CareerSaveData career, int slot)
        {
            var mine = Secrets.PlayableTeams(c, career.secrets);
            var league = c.TeamsInTier(TeamTier.League);
            if (mine.Count == 0 || league.Count == 0) return null;
            var opp = league.Find(t => t.id != mine[0].id) ?? league[0];
            return new MatchRequest
            {
                Mode = GameMode.QuickCall, HomeTeamId = mine[0].id, AwayTeamId = opp.id, CourtId = Id(slot),
                DifficultyId = career.settings.difficultyId,
            };
        }
    }
}
