using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>One stop on THE STREETS tour: a crew that runs a court.</summary>
    public sealed class TourStop
    {
        public string Id, City, CourtId, Crew, Leader, LeaderName, Trash;
        /// <summary>0..3 = the four cities, 4 = the finale.</summary>
        public int Tier;
        /// <summary>Game to 11, 16 or 21.</summary>
        public int Target;
        public Archetype Style;
        public string TeamId => "team.tour." + Id;
        public string LeaderId => "player.tour." + Id + ".0";
        public string ContextId => Streets.TourPrefix + Id;
        public bool Finale => Tier >= StreetsTour.Cities.Length;
    }

    /// <summary>
    /// 15 Oct 2026 (Omari), THE STREETS phase 2: the STREET TOUR.
    /// - Four cities with three crews each, then THE CROWN, the finale.
    /// - Beat every crew in a city to open the next one; beat all twelve to call out THE CROWN.
    /// - Each game has three stars: win, score a SHOWSTOPPER, win by 7 or more.
    /// - A first win pays REP and SP (spend SP in the Locker Room ► TRAIN) and the crew's leader joins your
    ///   street pool. Put up to two of them in your STREETS CREW to run with you.
    /// - City games are shorter (to 11, then 16); from the third city on it's the full 21.
    /// All crews and names are original.
    /// </summary>
    public static class StreetsTour
    {
        public static readonly string[] Cities = { "SOUTHSIDE", "THE HARBOR", "UPTOWN", "THE HEIGHTS" };
        public const string FinaleName = "THE CROWN COURT";

        public static readonly string[] RepNames = { "UNKNOWN", "LOCAL", "CITY NAME", "STREET LEGEND", "KING OF THE STREETS" };
        public static readonly int[] RepNeeded = { 0, 150, 450, 900, 1500 };

        public static readonly TourStop[] Stops =
        {
            S("tollbooth", 0, "court.overpass_park", "OVERPASS KINGS", "TOLLBOOTH", "Marcus Fry", "Pay the toll or go home.", 11, Archetype.PostAnchor),
            S("fuzz", 0, "court.static_lot", "STATIC", "FUZZ", "Leon Pryce", "You'll never hear us coming.", 11, Archetype.QuickCutter),
            S("drizzle", 0, "court.rain_alley", "RAIN DOGS", "DRIZZLE", "Omar Tate", "We play wet. You play scared.", 11, Archetype.HustleGuard),
            S("anchor", 1, "court.pier_nine", "PIER PRESSURE", "ANCHOR", "Boone Maddox", "Nothing gets past the pier.", 16, Archetype.GlassCleaner),
            S("undertow", 1, "court.ferry_deck", "LOW TIDE", "UNDERTOW", "Kai Mercer", "We pull you under.", 16, Archetype.LockdownWing),
            S("saltwater", 1, "court.boardwalk_slab", "BOARDWALK BULLIES", "SALTWATER", "Reggie Vane", "Sand in your shoes, an L on your record.", 16, Archetype.ShotCreator),
            S("penthouse", 2, "court.rooftop_ring", "SKYLINERS", "PENTHOUSE", "Julian Cross", "Look up. Way up.", 21, Archetype.RimRunner),
            S("flicker", 2, "court.lantern_market", "LANTERN BOYS", "FLICKER", "Desmond Ko", "Now you see it.", 21, Archetype.FloorGeneral),
            S("chainlink", 2, "court.sunset_cage", "CAGE RATS", "CHAINLINK", "Tyrell Banks", "Nobody leaves the cage clean.", 21, Archetype.TwoWaySpark),
            S("frostbite", 3, "court.snowline_park", "AVALANCHE", "FROSTBITE", "Sasha Volk", "Cold hands, colder game.", 21, Archetype.DeepShooter),
            S("echo", 3, "court.canyon_rim", "CANYON CREW", "ECHO", "Wes Harlan", "You'll hear about this one for days.", 21, Archetype.StretchForward),
            S("sundown", 3, "court.sundown_yard", "LAST LIGHT", "SUNDOWN", "Marcellus Gray", "When the sun goes down, so do you.", 21, Archetype.Playmaker),
            S("commissioner", 4, "court.rooftop_ring", "THE CROWN", "THE COMMISSIONER", "Ezekiel Monroe", "Every street ends at my court.", 21, Archetype.TwoWaySpark),
        };

        private static TourStop S(string id, int tier, string court, string crew, string leader, string name, string trash, int target, Archetype style) =>
            new TourStop { Id = id, Tier = tier, City = tier < Cities.Length ? Cities[tier] : FinaleName, CourtId = court, Crew = crew, Leader = leader,
                           LeaderName = name, Trash = trash, Target = target, Style = style };

        public static TourStop Find(string id) => Array.Find(Stops, s => s.Id == id);

        public static TourStop FromContext(string contextId) =>
            contextId != null && contextId.StartsWith(Streets.TourPrefix, StringComparison.Ordinal) ? Find(contextId.Substring(Streets.TourPrefix.Length)) : null;

        public static List<TourStop> InCity(int tier)
        {
            var list = new List<TourStop>();
            foreach (var s in Stops) if (s.Tier == tier) list.Add(s);
            return list;
        }

        public static bool Beaten(StreetsSaveData d, TourStop s) => d?.tourBeaten != null && d.tourBeaten.Contains(s.Id);

        /// <summary>A city is open when every crew in the one before it is beaten (the first is always open).</summary>
        public static bool CityOpen(StreetsSaveData d, int tier)
        {
            if (tier <= 0) return true;
            foreach (var s in InCity(tier - 1)) if (!Beaten(d, s)) return false;
            return true;
        }

        public static bool Open(StreetsSaveData d, TourStop s) => CityOpen(d, s.Tier);

        public static int Stars(StreetsSaveData d, TourStop s)
        {
            if (d?.tourStars == null) return 0;
            foreach (var line in d.tourStars)
            {
                int eq = line.IndexOf('=');
                if (eq > 0 && line.Substring(0, eq) == s.Id && int.TryParse(line.Substring(eq + 1), out int n)) return Math.Max(0, Math.Min(3, n));
            }
            return 0;
        }

        private static void SetStars(StreetsSaveData d, TourStop s, int stars)
        {
            if (d.tourStars == null) d.tourStars = new List<string>();
            d.tourStars.RemoveAll(l => l.StartsWith(s.Id + "=", StringComparison.Ordinal));
            d.tourStars.Add(s.Id + "=" + Math.Max(0, Math.Min(3, stars)));
        }

        public static int TotalStars(StreetsSaveData d)
        {
            int n = 0;
            foreach (var s in Stops) n += Stars(d, s);
            return n;
        }

        public static int MaxStars => Stops.Length * 3;

        public static int RepLevel(int rep)
        {
            int level = 0;
            for (int i = 0; i < RepNeeded.Length; i++) if (rep >= RepNeeded[i]) level = i;
            return level;
        }

        /// <summary>Stars for a result: a win, a SHOWSTOPPER scored, and winning by 7 or more.</summary>
        public static int StarsFor(bool won, int showstoppers, int margin) =>
            !won ? 0 : 1 + (showstoppers > 0 ? 1 : 0) + (margin >= 7 ? 1 : 0);

        /// <summary>
        /// Registers a crew's team and players in <paramref name="c"/>: the leader (a little better), and two
        /// running mates. Stronger the further along the tour.
        /// </summary>
        public static TeamDef Register(ContentCatalog c, TourStop s)
        {
            var team = c.Team(s.TeamId);
            if (team == null) c.Teams.Add(team = new TeamDef { id = s.TeamId, tier = TeamTier.Franchise });
            uint h = StableHash.Of("tour." + s.Id);
            team.city = "";
            team.nickname = s.Crew;
            string abbr = s.Crew.Replace("THE ", "").Replace(" ", "");
            team.abbreviation = abbr.Substring(0, Math.Min(3, abbr.Length));
            team.primary = CustomTeams.Palette[h % 16];
            team.secondary = CustomTeams.Palette[(h / 16 + 5) % 16];
            team.accent = RgbColor.FromHex("#1A1A1F");
            team.logoShape = (LogoShape)(h % 5);
            team.logoMotif = (LogoMotif)((h / 5) % 10);
            team.pattern = (TeamPattern)(1 + (h / 50) % 7);
            team.homeCourtId = c.Court(s.CourtId) != null ? s.CourtId : "court.overpass_park";
            team.motto = s.Trash;
            team.rosterPlayerIds.Clear();
            int level = s.Finale ? 84 : 60 + s.Tier * 6;
            string[] first = { "Juke", "Bam", "Smoke", "Dutch", "Rook", "Spud", "Tiny", "Slick", "Nitro", "Hops" };
            string[] last = { "Rivers", "Cole", "Mendez", "Okoye", "Park", "Hayes", "Lund", "Ferro", "Abara", "Quinn" };
            for (int k = 0; k < Streets.TeamSize; k++)
            {
                string pid = "player.tour." + s.Id + "." + k;
                if (c.Player(pid) == null)
                {
                    var arch = c.Archetypes.Find(a => a.archetype == (k == 0 ? s.Style : (Archetype)((int)(h + (uint)k * 7) % 12))) ?? c.Archetypes[0];
                    var attrs = DefaultContent.Personalize(arch.baseline, pid);
                    int target = level + (k == 0 ? 5 : -2);
                    c.Players.Add(new PlayerDef
                    {
                        id = pid,
                        firstName = k == 0 ? s.Leader : first[(h + (uint)k * 3) % (uint)first.Length],
                        lastName = k == 0 ? "" : last[(h / 7 + (uint)k) % (uint)last.Length],
                        jerseyNumber = (int)((h >> (k + 3)) % 60),
                        archetypeId = arch.id,
                        attributes = attrs.Offset(target - attrs.Overall),
                        appearance = DefaultContent.AppearanceFromSeed(pid, arch.archetype),
                    });
                }
                team.rosterPlayerIds.Add(pid);
            }
            return team;
        }

        /// <summary>The tour game: your crew vs theirs on their court, to their target.</summary>
        public static MatchRequest Challenge(ContentCatalog c, CareerSaveData career, TourStop s)
        {
            Register(c, s);
            return new MatchRequest
            {
                Mode = GameMode.Streets,
                HomeTeamId = Streets.MyTeamId(c),
                AwayTeamId = s.TeamId,
                CourtId = c.Team(s.TeamId).homeCourtId,
                RulesId = Streets.RulesFor(s.Target),
                TeamSize = Streets.TeamSize,
                StreetRules = true,
                DifficultyId = career?.settings?.difficultyId ?? DefaultContent.DefaultDifficultyId,
                ContextId = s.ContextId,
                StartWithBall = 0,
            };
        }

        public sealed class TourResult
        {
            public int Stars, NewStars, Rep, Sp;
            public bool FirstWin, CityCleared, TourCleared;
            public string Recruit;
        }

        /// <summary>Records a tour game. First wins pay REP and SP and bring the leader into your pool.</summary>
        public static TourResult Apply(StreetsSaveData d, CareerSaveData career, TourStop s, bool won, int showstoppers, int margin)
        {
            var r = new TourResult();
            if (d == null || s == null) return r;
            if (d.tourBeaten == null) d.tourBeaten = new List<string>();
            if (d.recruits == null) d.recruits = new List<string>();
            int before = Stars(d, s);
            r.Stars = StarsFor(won, showstoppers, margin);
            if (r.Stars > before)
            {
                r.NewStars = r.Stars - before;
                SetStars(d, s, r.Stars);
            }
            if (won)
            {
                r.FirstWin = !d.tourBeaten.Contains(s.Id);
                bool cityWasOpen = s.Finale || CityOpen(d, s.Tier + 1);
                r.Rep = (30 + 25 * s.Tier) * (r.FirstWin ? 2 : 1) + 10 * r.NewStars;
                if (r.FirstWin)
                {
                    d.tourBeaten.Add(s.Id);
                    r.Sp = 60 * (s.Tier + 1);
                    if (career != null) career.signalPoints += r.Sp;
                    if (!d.recruits.Contains(s.LeaderId))
                    {
                        d.recruits.Add(s.LeaderId);
                        r.Recruit = s.Leader;
                    }
                }
                r.CityCleared = !s.Finale && !cityWasOpen && CityOpen(d, s.Tier + 1);
                r.TourCleared = s.Finale && r.FirstWin;
            }
            else r.Rep = 0;
            d.rep = Math.Max(0, d.rep + r.Rep);
            return r;
        }

        /// <summary>Recruits you can put in your STREETS CREW (registered so their players exist).</summary>
        public static List<PlayerDef> Pool(ContentCatalog c, StreetsSaveData d)
        {
            var list = new List<PlayerDef>();
            if (c == null || d?.recruits == null) return list;
            foreach (var id in d.recruits)
            {
                var stop = Array.Find(Stops, s => s.LeaderId == id);
                if (stop != null) Register(c, stop);
                var p = c.Player(id);
                if (p != null) list.Add(p);
            }
            return list;
        }

        /// <summary>Puts a recruit in crew spot 0 or 1 ("" = the usual teammate). False if it isn't one of yours.</summary>
        public static bool SetCrew(StreetsSaveData d, int slot, string playerId)
        {
            if (d == null || slot < 0 || slot > 1) return false;
            if (!string.IsNullOrEmpty(playerId) && (d.recruits == null || !d.recruits.Contains(playerId))) return false;
            if (d.crew == null) d.crew = new List<string>();
            while (d.crew.Count < 2) d.crew.Add("");
            if (!string.IsNullOrEmpty(playerId))
                for (int i = 0; i < 2; i++) if (i != slot && d.crew[i] == playerId) d.crew[i] = "";
            d.crew[slot] = playerId ?? "";
            return true;
        }

        /// <summary>Your STREETS CREW teammates for a game (null entries keep the usual teammate).</summary>
        public static List<PlayerDef> CrewDefs(ContentCatalog c, StreetsSaveData d)
        {
            var list = new List<PlayerDef> { null, null };
            if (c == null || d?.crew == null) return list;
            Pool(c, d);
            for (int i = 0; i < 2 && i < d.crew.Count; i++)
                if (!string.IsNullOrEmpty(d.crew[i]) && d.recruits.Contains(d.crew[i])) list[i] = c.Player(d.crew[i]);
            return list;
        }
    }
}
