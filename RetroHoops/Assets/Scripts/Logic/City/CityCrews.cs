using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>How tough a city crew is: BALLER (very good) or LEGENDARY (the best in the city).</summary>
    public enum CityCrewTier { Baller = 0, Legendary = 1 }

    /// <summary>One CPU crew that runs a city court: three players who all play one way.</summary>
    public sealed class CityCrewDef
    {
        public string Id, Abbr, Name, Leader, Style, StyleBlurb, Trash, CourtId;
        public CityCrewTier Tier;
        public Archetype[] Roster;
        public DefenseScheme Scheme;
        public string Primary, Secondary;

        public string TeamId => "team.city." + Id;
        public string ContextId => CityCrews.ContextPrefix + Id;
        public string TierName => Tier == CityCrewTier.Legendary ? "LEGENDARY" : "BALLER";
    }

    /// <summary>
    /// 17 Oct 2026 (Omari): the CPU crews of BALLER CITY. Every court has a crew with its own way of playing
    /// (shooters, dunkers, lockdown defenders, passers, iso handles, bigs, run-and-gun, crafty vets...), built from the
    /// game's own archetypes so the AI really plays that way, and every crew is BALLER or LEGENDARY: two new
    /// difficulty levels above Legend for the AI's decisions and timing (never extra speed: the fairness rule holds),
    /// and crews rated well above the street. LEGENDARY crews open once you've beaten three BALLER crews.
    /// Games are THE STREETS rules (to 21, win by 2, 3 on 3) on the crew's own court.
    /// </summary>
    public static class CityCrews
    {
        public const string BallerDifficultyId = "difficulty.baller";
        public const string LegendaryDifficultyId = "difficulty.legendary";
        public const string ContextPrefix = "citycrew:";
        public const int BallerWinsForLegendary = 3;
        public const int BallerRating = 82, LegendaryRating = 89;

        public static readonly CityCrewDef[] All =
        {
            // Hoop Heights.
            C("splash", "SPF", "SPLASH FACTORY", "DROP", "SHARPSHOOTERS", "They live behind the arc. Close out or lose.", "Every shot's a three if you're scared to close out.",
              "court.coin_op", CityCrewTier.Baller, DefenseScheme.ManToMan, "#00F0FF", "#FF2E88", Archetype.DeepShooter, Archetype.DeepShooter, Archetype.StretchForward),
            C("wreckers", "RWK", "RIM WRECKERS", "HAMMER", "HIGH FLYERS", "Lobs, dunks and putbacks. Keep them off the rim.", "We don't shoot. We finish.",
              "court.skate_bowl", CityCrewTier.Baller, DefenseScheme.PackLine, "#F72585", "#F1FAEE", Archetype.RimRunner, Archetype.QuickCutter, Archetype.PostAnchor),
            C("dimes", "DMD", "DIME DROPPERS", "VISION", "PASS FIRST", "The ball never stops moving. Watch the cutters.", "You'll be guarding air all game.",
              "court.lantern_market", CityCrewTier.Baller, DefenseScheme.Zone, "#F59E0B", "#4A2C2A", Archetype.Playmaker, Archetype.FloorGeneral, Archetype.QuickCutter),
            C("lockdown", "LKD", "LOCKDOWN UNIT", "CLAMPS", "DEFENSE", "Pressure on every dribble. Protect the ball.", "Count your turnovers. We do.",
              "court.night_market", CityCrewTier.Baller, DefenseScheme.Pressure, "#2B2D42", "#FCBF49", Archetype.LockdownWing, Archetype.HustleGuard, Archetype.GlassCleaner),
            C("ankles", "ANK", "ANKLE TAKERS", "SHIFTY", "ISO HANDLES", "One-on-one all day. Stay down on the crossover.", "Hope you stretched.",
              "court.rooftop_ring", CityCrewTier.Legendary, DefenseScheme.ManToMan, "#3D5A80", "#E0FBFC", Archetype.ShotCreator, Archetype.FloorGeneral, Archetype.TwoWaySpark),
            C("showtime", "SHO", "SHOWTIME", "NEON", "RUN AND GUN", "Fast breaks and fancy passes. Get back on defense.", "Lights on. Showtime.",
              "court.starlight_rink", CityCrewTier.Legendary, DefenseScheme.Pressure, "#F72585", "#4CC9F0", Archetype.QuickCutter, Archetype.RimRunner, Archetype.Playmaker),
            // The Blacktop District.
            C("glass", "GLS", "GLASS GANG", "BOARDS", "BIG MEN", "Every miss is theirs. Box out.", "Miss one. I dare you.",
              "court.overpass_park", CityCrewTier.Baller, DefenseScheme.PackLine, "#6B6F78", "#FFD166", Archetype.GlassCleaner, Archetype.PostAnchor, Archetype.StretchForward),
            C("oldheads", "OLD", "THE OLD HEADS", "UNC", "CRAFTY VETS", "Slow, smart and never wrong. Don't bite on the fakes.", "We were hooping here before you were born.",
              "court.sunset_cage", CityCrewTier.Legendary, DefenseScheme.Zone, "#9B3D2E", "#F4F1DE", Archetype.Playmaker, Archetype.PostAnchor, Archetype.DeepShooter),
            C("tide", "TDE", "HIGH TIDE", "WAVES", "TWO-WAY", "Good at everything. No easy buckets either way.", "The tide always comes in.",
              "court.pier_nine", CityCrewTier.Baller, DefenseScheme.ManToMan, "#0EA5E9", "#E0F2FE", Archetype.TwoWaySpark, Archetype.LockdownWing, Archetype.ShotCreator),
            C("nightshift", "NSH", "NIGHT SHIFT", "GRAVEYARD", "CLOSERS", "Clutch shooters and stoppers. Win it early.", "We play best after midnight.",
              "court.static_lot", CityCrewTier.Legendary, DefenseScheme.Pressure, "#14141F", "#7209B7", Archetype.ShotCreator, Archetype.LockdownWing, Archetype.StretchForward),
            // Holiday Park.
            C("frost", "FRG", "FROST GIANTS", "BLIZZARD", "BIG MEN", "Tall, slow and everywhere in the paint.", "Snow day. You're not scoring.",
              "court.holiday_christmas", CityCrewTier.Legendary, DefenseScheme.Zone, "#C1121F", "#FFFFFF", Archetype.PostAnchor, Archetype.GlassCleaner, Archetype.StretchForward),
            C("pumpkins", "PKK", "PUMPKIN KINGS", "JACK", "TRICKSTERS", "Steals, fakes and backdoor cuts.", "Trick or treat? Trick.",
              "court.holiday_halloween", CityCrewTier.Baller, DefenseScheme.Pressure, "#FF8C1A", "#5A189A", Archetype.HustleGuard, Archetype.QuickCutter, Archetype.ShotCreator),
            C("bunnies", "BNH", "BUNNY HOPS", "SPRINGS", "HIGH FLYERS", "Quick feet and big bounce. Box out.", "Catch us if you can.",
              "court.holiday_easter", CityCrewTier.Baller, DefenseScheme.ManToMan, "#F4A6C0", "#FFFDF5", Archetype.RimRunner, Archetype.QuickCutter, Archetype.TwoWaySpark),
            C("fireworks", "FWK", "FIREWORKS", "BOOM", "SHARPSHOOTERS", "Threes that light up the sky.", "Look up. That's us.",
              "court.holiday_fourth", CityCrewTier.Legendary, DefenseScheme.ManToMan, "#C1121F", "#1D3557", Archetype.DeepShooter, Archetype.ShotCreator, Archetype.DeepShooter),
        };

        private static CityCrewDef C(string id, string abbr, string name, string leader, string style, string blurb, string trash, string court, CityCrewTier tier,
                                     DefenseScheme scheme, string primary, string secondary, params Archetype[] roster) =>
            new CityCrewDef { Id = id, Abbr = abbr, Name = name, Leader = leader, Style = style, StyleBlurb = blurb, Trash = trash, CourtId = court, Tier = tier,
                              Scheme = scheme, Primary = primary, Secondary = secondary, Roster = roster };

        public static CityCrewDef Find(string id) => Array.Find(All, c => c.Id == id);

        public static CityCrewDef AtCourt(string courtId) => Array.Find(All, c => c.CourtId == courtId);

        public static CityCrewDef FromContext(string contextId) =>
            contextId != null && contextId.StartsWith(ContextPrefix, StringComparison.Ordinal) ? Find(contextId.Substring(ContextPrefix.Length)) : null;

        public static string DifficultyFor(CityCrewDef crew) => crew != null && crew.Tier == CityCrewTier.Legendary ? LegendaryDifficultyId : BallerDifficultyId;

        /// <summary>BALLER crews you've beaten (for opening the LEGENDARY ones).</summary>
        public static int BallerBeaten(CitySaveData s)
        {
            int n = 0;
            if (s?.crewsBeaten == null) return 0;
            foreach (var id in s.crewsBeaten)
            {
                var c = Find(id);
                if (c != null && c.Tier == CityCrewTier.Baller) n++;
            }
            return n;
        }

        public static bool Unlocked(CitySaveData s, CityCrewDef crew) =>
            crew != null && (crew.Tier == CityCrewTier.Baller || BallerBeaten(s) >= BallerWinsForLegendary);

        public static string LockedReason(CitySaveData s, CityCrewDef crew) =>
            Unlocked(s, crew) ? null : "Beat " + BallerWinsForLegendary + " BALLER crews to call out the LEGENDARY ones (" + BallerBeaten(s) + "/" + BallerWinsForLegendary + ").";

        private static readonly string[] First = { "Kato", "Ren", "Ayo", "Dax", "Lio", "Remy", "Zuri", "Bo", "Cass", "Nico", "Tavi", "Quin", "Jules", "Moss" };
        private static readonly string[] Last = { "Vale", "Rook", "Sterling", "Okoro", "Mendez", "Frost", "Delane", "Ashby", "Kim", "Moreau", "Tate", "Baptiste" };

        /// <summary>Puts the crew (team and players) in the catalog, rated for its tier.</summary>
        public static TeamDef Register(ContentCatalog c, CityCrewDef crew)
        {
            EnsureDifficulties(c);
            var team = c.Team(crew.TeamId);
            if (team == null) c.Teams.Add(team = new TeamDef { id = crew.TeamId, tier = TeamTier.Franchise });
            uint h = StableHash.Of(crew.Id);
            team.city = "";
            team.nickname = crew.Name;
            team.abbreviation = crew.Abbr;
            team.primary = RgbColor.FromHex(crew.Primary);
            team.secondary = RgbColor.FromHex(crew.Secondary);
            team.accent = RgbColor.FromHex("#14141F");
            team.logoShape = (LogoShape)(h % 5);
            team.logoMotif = (LogoMotif)((h / 5) % 17);
            team.pattern = (TeamPattern)(1 + (h / 50) % 7);
            team.homeCourtId = c.Court(crew.CourtId) != null ? crew.CourtId : "court.overpass_park";
            team.motto = crew.Trash;
            team.scheme = crew.Scheme;
            team.rosterPlayerIds.Clear();
            int level = crew.Tier == CityCrewTier.Legendary ? LegendaryRating : BallerRating;
            for (int k = 0; k < crew.Roster.Length; k++)
            {
                string pid = "player.city." + crew.Id + "." + k;
                var arch = c.Archetypes.Find(a => a.archetype == crew.Roster[k]) ?? c.Archetypes[0];
                var p = c.Player(pid);
                if (p == null) c.Players.Add(p = new PlayerDef { id = pid });
                var attrs = DefaultContent.Personalize(arch.baseline, pid);
                int target = level + (k == 0 ? 3 : 0);
                p.firstName = k == 0 ? crew.Leader : First[(h + (uint)k * 7) % (uint)First.Length];
                p.lastName = k == 0 ? "" : Last[(h / 3 + (uint)k * 5) % (uint)Last.Length];
                p.jerseyNumber = (int)((h % 50 + (uint)k * 17) % 99); // three different numbers
                p.archetypeId = arch.id;
                p.attributes = attrs.Offset(target - attrs.Overall);
                p.appearance = DefaultContent.AppearanceFromSeed(pid, arch.archetype);
                team.rosterPlayerIds.Add(pid);
            }
            return team;
        }

        /// <summary>The crew game: your Streets crew against theirs, on their court, at their tier.</summary>
        public static MatchRequest Challenge(ContentCatalog c, CareerSaveData career, CityCrewDef crew)
        {
            var team = Register(c, crew);
            return new MatchRequest
            {
                Mode = GameMode.Streets,
                HomeTeamId = Streets.MyTeamId(c),
                AwayTeamId = team.id,
                CourtId = team.homeCourtId,
                RulesId = Streets.RulesId,
                TeamSize = Streets.TeamSize,
                StreetRules = true,
                DifficultyId = DifficultyFor(crew),
                ContextId = crew.ContextId,
                StartWithBall = 0,
            };
        }

        // ------------------------------------------------------------------ the two new difficulties

        /// <summary>BALLER: sharper than Legend. LEGENDARY: near-perfect decisions and timing. Same speed as everyone (≤ 1.0).</summary>
        public static DifficultyDef Baller => new DifficultyDef
        {
            id = BallerDifficultyId, displayName = "Baller", sortOrder = 3,
            reactionTime = 0.15f, decisionQuality = 0.94f, shotQualityThreshold = 0.50f,
            errorRate = 0.03f, movementScale = 1.0f, releaseAccuracy = 0.85f,
        };

        public static DifficultyDef Legendary => new DifficultyDef
        {
            id = LegendaryDifficultyId, displayName = "Legendary", sortOrder = 4,
            reactionTime = 0.11f, decisionQuality = 0.98f, shotQualityThreshold = 0.53f,
            errorRate = 0.01f, movementScale = 1.0f, releaseAccuracy = 0.94f,
        };

        public static bool IsCityDifficulty(string id) => id == BallerDifficultyId || id == LegendaryDifficultyId;

        /// <summary>Adds BALLER and LEGENDARY to a catalog that doesn't have them (both are in the built-in content).</summary>
        public static void EnsureDifficulties(ContentCatalog c)
        {
            if (c == null) return;
            if (c.Difficulty(BallerDifficultyId) == null) c.Difficulties.Add(Baller);
            if (c.Difficulty(LegendaryDifficultyId) == null) c.Difficulties.Add(Legendary);
        }

        /// <summary>The difficulties the game's pickers offer (BALLER and LEGENDARY are for city crews).</summary>
        public static List<DifficultyDef> Pickable(ContentCatalog c) => c.Difficulties.FindAll(d => !IsCityDifficulty(d.id));
    }
}
