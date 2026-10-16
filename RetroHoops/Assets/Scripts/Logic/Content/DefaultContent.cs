using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// The handcrafted MVP content for The Caller League — every name, colour, and
    /// line of copy here is original to Retro Hoops. The editor setup tool
    /// writes these into ScriptableObject assets; the runtime falls back to this
    /// class if the assets are missing, so the game is always playable.
    /// </summary>
    public static class DefaultContent
    {
        public const string LeagueName = "The Caller League";
        public const string ChampionshipName = "The Gold Signal Cup";
        public const string CircuitName = "The Blacktop Circuit";
        public const string RookieTournamentName = "First Call Classic";
        public const string GameName = "Retro Hoops";

        public const string RookPlayerId = "player.crew.rook";
        public const string PlayerCrewId = "crew.first_callers";
        public const string RivalCrewId = "crew.neon_static";
        public const string RivalLeaderId = "player.nst.marlowe";
        /// <summary>Season 2 rival (even-numbered Rise seasons).</summary>
        public const string Rival2CrewId = "crew.sundown_syndicate";
        public const string Rival2LeaderId = "player.sds.sol";
        /// <summary>Season 3 rival (every third Rise season).</summary>
        public const string Rival3CrewId = "crew.midnight_tide";
        public const string Rival3LeaderId = "player.mdt.quill";
        /// <summary>Season 4 rival (every fourth Rise season).</summary>
        public const string Rival4CrewId = "crew.paper_cranes";
        /// <summary>Season 5 rival (every fifth Rise season).</summary>
        public const string Rival5CrewId = "crew.cassette_club";
        public const string Rival5LeaderId = "player.csc.rivera";
        public const string Rival4LeaderId = "player.crn.vale";
        /// <summary>Season 6 rival (every sixth Rise season).</summary>
        public const string Rival6CrewId = "crew.lighthouse_keepers";
        public const string Rival6LeaderId = "player.lhk.marsh";
        /// <summary>Season 7 rival (every seventh Rise season).</summary>
        public const string Rival7CrewId = "crew.comet_couriers";
        public const string Rival7LeaderId = "player.cmc.okoro";
        /// <summary>Season 8 rival (every eighth Rise season).</summary>
        public const string Rival8CrewId = "crew.night_lanterns";
        public const string Rival8LeaderId = "player.nln.akande";
        /// <summary>Season 9 rival (every ninth Rise season).</summary>
        public const string Rival9CrewId = "crew.roller_royals";
        public const string Rival9LeaderId = "player.rry.varo";
        /// <summary>Season 10 rival (every tenth Rise season).</summary>
        public const string Rival10CrewId = "crew.wash_house";
        public const string Rival10LeaderId = "player.wsh.whitaker";
        /// <summary>Season 11 rival (every eleventh Rise season).</summary>
        public const string Rival11CrewId = "crew.sky_kites";
        public const string Rival11LeaderId = "player.skt.takahashi";
        /// <summary>Season 12 rival (every twelfth Rise season).</summary>
        public const string Rival12CrewId = "crew.putt_club";
        public const string Rival12LeaderId = "player.ptc.quarrie";
        public const string DefaultRulesId = "rules.default";
        public const string DefaultDifficultyId = "difficulty.caller";
        public const string PracticeCourtId = "court.practice_lab";
        /// <summary>Arcade Ladder boss (secret team) and its hidden court.</summary>
        public const string BossTeamId = "team.the_glitch";
        public const string BossCourtId = "court.glitch_grid";
        /// <summary>Code-unlocked secret crew and hidden court.</summary>
        public const string SecretCrewId = "crew.cartridge_kids";
        public const string SecretCourtId = "court.pixel_void";

        public static ContentCatalog Create()
        {
            var c = new ContentCatalog();
            c.Archetypes = Archetypes.CreateAll();
            AddCourts(c);
            AddLeagueTeams(c);
            AddCircuitCrews(c);
            AddRules(c);
            AddDifficulties(c);
            AddUpgrades(c);
            AddCosmetics(c);
            AddSeason(c);
            AddSchemes(c);
            return c;
        }

        // ------------------------------------------------------------------ courts

        private static void AddCourts(ContentCatalog c)
        {
            // Blacktop Circuit (Rise Mode starting courts)
            c.Courts.Add(Court("court.sunset_cage", "Sunset Cage", CourtCircuit.Blacktop,
                "A chain-link cage that glows orange at golden hour.",
                "#4A4A52", "#F4F1DE", "#E07A5F", "#2E1460", "#FF6B50", 0.35f));
            c.Courts.Add(Court("court.pier_nine", "Pier Nine Blacktop", CourtCircuit.Blacktop,
                "Salt air, loose rims, and the loudest benches on the water.",
                "#3D4A56", "#FFFFFF", "#2A9D8F", "#8ECAE6", "#FFB4A2", 0.30f));
            c.Courts.Add(Court("court.overpass_park", "Overpass Park", CourtCircuit.Blacktop,
                "Every dribble echoes under the highway. Night games only.",
                "#2F2F36", "#FFE066", "#9B5DE5", "#1B1B3A", "#F15BB5", 0.45f));
            c.Courts.Add(Court("court.rooftop_ring", "Rooftop Ring", CourtCircuit.Blacktop,
                "A fenced court on top of the old cannery. The wind always wins.",
                "#3A3F4B", "#E0FBFC", "#3D5A80", "#0B132B", "#5BC0BE", 0.40f));
            c.Courts.Add(Court("court.static_lot", "The Static Lot", CourtCircuit.League,
                "A parking-lot court under buzzing neon. Neon Static's home.",
                "#26262E", "#00F5D4", "#7209B7", "#10002B", "#3A0CA3", 0.55f));
            c.Courts.Add(Court("court.boardwalk_slab", "Boardwalk Slab", CourtCircuit.Blacktop,
                "Sun-bleached concrete between the arcade and the sand.",
                "#5C5552", "#FFF3B0", "#E09F3E", "#FF9F1C", "#FFBF69", 0.50f));

            // Caller League venues (one per team)
            c.Courts.Add(Court("court.volt_box", "The Volt Box", CourtCircuit.League,
                "A buzzing warehouse gym wired with yellow strip lights.",
                "#C89B5C", "#2B2B2E", "#4B1F7A", "#231942", "#5E548E", 0.80f));
            c.Courts.Add(Court("court.breakwater", "Breakwater Court", CourtCircuit.League,
                "A seaside gym with windows that rattle when the crowd gets up.",
                "#D2A56D", "#14213D", "#1FB5A6", "#14213D", "#1FB5A6", 0.75f));
            c.Courts.Add(Court("court.kennel_yard", "Kennel Yard", CourtCircuit.League,
                "Brick walls, low ceiling, and fans who bark on defense.",
                "#B5835A", "#F3E9D2", "#CC5500", "#3E2415", "#CC5500", 0.85f));
            // 17 Oct 2026: BALLER CITY's arena (the City Championship).
            c.Courts.Add(Court(CityArena.CourtId, "Baller Arena", CourtCircuit.League,
                "Downtown's big room: polished maple, a scoreboard over centre court and a sold-out crowd.",
                "#D8A35D", "#14141F", "#F72585", "#0B0B1A", "#4CC9F0", 0.95f));
            c.Courts.Add(Court("court.grove_gym", "Grove Gym", CourtCircuit.League,
                "Timber rafters and a floor with a hand-painted tree at centre.",
                "#C49A6C", "#111111", "#1E5631", "#0B2E1A", "#D4A017", 0.70f));
            c.Courts.Add(Court("court.orbit_dome", "Orbit Dome", CourtCircuit.League,
                "A round arena under a starfield ceiling.",
                "#CFA878", "#FFFFFF", "#7B2CBF", "#10002B", "#5BC0EB", 0.80f));
            c.Courts.Add(Court("court.mirage_pavilion", "Mirage Pavilion", CourtCircuit.League,
                "Open sides, hot air, and heat shimmer off the baseline.",
                "#D9B98C", "#191970", "#B7410E", "#191970", "#E6C79C", 0.65f));
            c.Courts.Add(Court("court.nightroost_hall", "Nightroost Hall", CourtCircuit.League,
                "A quiet, cold gym where every squeak carries.",
                "#BFA27E", "#0B0B0B", "#A8DADC", "#0B0B0B", "#457B9D", 0.70f));
            c.Courts.Add(Court("court.sunspire", "Sunspire Court", CourtCircuit.League,
                "A glass-roofed arena that turns pink at sunset.",
                "#D4A373", "#0F1A3C", "#D1127A", "#0F1A3C", "#FF8C1A", 0.90f));

            // Season 2 street courts.
            c.Courts.Add(Court("court.sundown_yard", "Sundown Yard", CourtCircuit.Blacktop,
                "A rail-yard court where the sunset turns every rim to copper.",
                "#5A3E36", "#FFE8D6", "#D1495B", "#3D1E6D", "#FF8C42", 0.50f));
            c.Courts.Add(Court("court.rain_alley", "Rain Alley", CourtCircuit.Blacktop,
                "Wet asphalt, neon puddles, and a hoop bolted to a fire escape.",
                "#2C3440", "#B8F2E6", "#5E60CE", "#0B0F1A", "#5390D9", 0.35f));
            c.Courts.Add(Court("court.snowline_park", "Snowline Park", CourtCircuit.Blacktop,
                "Shovelled clear at dawn. Breath clouds and cold fingers.",
                "#8D99AE", "#FFFFFF", "#457B9D", "#A8DADC", "#F1FAEE", 0.30f));

            // Season 3 courts.
            c.Courts.Add(Court("court.ferry_deck", "Ferry Deck", CourtCircuit.Blacktop,
                "A court painted on the top deck of the night ferry. The horn is the buzzer.",
                "#2B3A4A", "#E0F2FE", "#0EA5E9", "#020617", "#1E3A8A", 0.40f));
            c.Courts.Add(Court("court.lantern_market", "Lantern Market", CourtCircuit.Blacktop,
                "Between the food stalls, under a hundred paper lanterns.",
                "#4A2C2A", "#FFF1C1", "#F59E0B", "#1F0A0A", "#B91C1C", 0.60f));
            c.Courts.Add(Court("court.canyon_rim", "Canyon Rim", CourtCircuit.Blacktop,
                "Red rock, thin air, and a long way down past the baseline.",
                "#9A4A2E", "#FFE4CC", "#7C2D12", "#FDBA74", "#FB923C", 0.25f));

            // Season 5 courts.
            c.Courts.Add(Court("court.record_roof", "Record Shop Roof", CourtCircuit.Blacktop,
                "Above the record shop. Somebody always has a tape playing.",
                "#3F3A4F", "#F2E9E4", "#C9184A", "#22223B", "#9A8C98", 0.4f));
            c.Courts.Add(Court("court.night_bus_depot", "Night Bus Depot", CourtCircuit.Blacktop,
                "Under the depot lights, between the last bus and the first.",
                "#2F3E46", "#CAD2C5", "#F4A261", "#0B132B", "#3A506B", 0.3f));

            // Season 12 courts.
            c.Courts.Add(Court("court.hole_eighteen", "Hole 18", CourtCircuit.Blacktop,
                "A half court built over the last hole of the glow-in-the-dark mini golf. Mind the little bridge.",
                "#1B4332", "#D8F3DC", "#F72585", "#081C15", "#52B788", 0.5f));
            c.Courts.Add(Court("court.clubhouse_roof", "Clubhouse Roof", CourtCircuit.Blacktop,
                "Up on the mini-golf clubhouse, under the giant spinning flag.",
                "#2D6A4F", "#FFFFFF", "#FFD60A", "#1B263B", "#F72585", 0.45f));

            // Season 11 courts.
            c.Courts.Add(Court("court.bluff_top", "Bluff Top", CourtCircuit.Blacktop,
                "A court on the sea cliff where the kite flyers meet. The wind takes every long shot somewhere new.",
                "#4A6FA5", "#F7F7FF", "#FFB703", "#023047", "#FB8500", 0.5f));
            c.Courts.Add(Court("court.pinwheel_lot", "Pinwheel Lot", CourtCircuit.Blacktop,
                "A parking lot by the boardwalk, fenced with spinning pinwheels.",
                "#5E6472", "#FFFFFF", "#219EBC", "#22223B", "#FFB703", 0.45f));

            // Season 10 courts.
            c.Courts.Add(Court("court.suds_alley", "Suds Alley", CourtCircuit.Blacktop,
                "Behind the all-night laundromat. Steam from the dryer vents rolls across the key.",
                "#3D405B", "#F4F1DE", "#81B29A", "#22223B", "#F2CC8F", 0.45f));
            c.Courts.Add(Court("court.coin_op", "Coin-Op Court", CourtCircuit.Blacktop,
                "A half court painted on the laundromat roof, under a buzzing OPEN 24 HOURS sign.",
                "#2B2D42", "#EDF2F4", "#EF233C", "#14213D", "#8ECAE6", 0.5f));

            // Season 9 courts.
            c.Courts.Add(Court("court.starlight_rink", "Starlight Rink", CourtCircuit.Blacktop,
                "The roller rink after the last skate. A hoop at each end of the maple and a mirror ball overhead.",
                "#2D1B4E", "#F72585", "#4CC9F0", "#14082B", "#FFD166", 0.45f));
            c.Courts.Add(Court("court.skate_bowl", "Skate Bowl", CourtCircuit.Blacktop,
                "A hoop bolted above the deep end of an empty skate bowl. Mind the coping.",
                "#5C677D", "#F1FAEE", "#F72585", "#1B263B", "#4CC9F0", 0.55f));

            // Season 8 courts.
            c.Courts.Add(Court("court.night_market", "Night Market", CourtCircuit.Blacktop,
                "Between the food stalls after closing. Paper lanterns for floodlights.",
                "#2B2D42", "#FCBF49", "#D62828", "#1D1A31", "#F77F00", 0.4f));
            c.Courts.Add(Court("court.lantern_steps", "Lantern Steps", CourtCircuit.Blacktop,
                "A half court at the top of the old stone steps, strung with lights.",
                "#3C2F2F", "#FFF3E0", "#FCBF49", "#1D1A31", "#D62828", 0.5f));

            // Season 7 courts.
            c.Courts.Add(Court("court.tram_yard", "Tram Yard", CourtCircuit.Blacktop,
                "Between the parked trams at the end of the line. The bell is the shot clock.",
                "#3D3D3D", "#F8F9FA", "#FB5607", "#1B1B1E", "#3A0CA3", 0.35f));
            c.Courts.Add(Court("court.dispatch_roof", "Dispatch Roof", CourtCircuit.Blacktop,
                "On the courier depot roof, where the riders wait for the next run.",
                "#264653", "#FFFFFF", "#FFBE0B", "#0B132B", "#FB5607", 0.45f));

            // Season 6 courts.
            c.Courts.Add(Court("court.lighthouse_point", "Lighthouse Point", CourtCircuit.Blacktop,
                "On the rocks under the lighthouse. The beam sweeps the court every eight seconds.",
                "#2E4057", "#F4F1DE", "#FFD60A", "#0B2545", "#13315C", 0.35f));
            c.Courts.Add(Court("court.glasshouse_roof", "Glasshouse Roof", CourtCircuit.Blacktop,
                "On top of the old greenhouse. The glass fogs up in the fourth quarter.",
                "#52796F", "#F1FAEE", "#E9C46A", "#84A98C", "#CAD2C5", 0.4f));

            // Season 4 courts.
            c.Courts.Add(Court("court.laundromat_lot", "Laundromat Lot", CourtCircuit.Blacktop,
                "Behind the all-night laundromat. The dryers hum like a crowd.",
                "#5B6475", "#F8FAFC", "#38BDF8", "#1E1B4B", "#A78BFA", 0.35f));
            c.Courts.Add(Court("court.drive_in", "Drive-In Lot", CourtCircuit.Blacktop,
                "Between the parked cars, under a giant blank movie screen.",
                "#334155", "#FDE68A", "#EF4444", "#0F172A", "#475569", 0.55f));
            c.Courts.Add(Court("court.paper_garden", "Paper Garden", CourtCircuit.Blacktop,
                "A rooftop garden strung with a thousand folded paper birds.",
                "#7C9A6D", "#FFFBEB", "#E11D48", "#FDF2F8", "#FBCFE8", 0.45f));

            // Hidden courts (secrets): neon grid floors.
            c.Courts.Add(Court(BossCourtId, "The Glitch Grid", CourtCircuit.Secret,
                "A court that shouldn't exist, flickering at the edge of the game.",
                "#0B0B1A", "#FF2E88", "#00F0FF", "#000000", "#240046", 0.60f));
            c.Courts.Add(Court(SecretCourtId, "Pixel Void", CourtCircuit.Secret,
                "Nothing but grid lines and the hum of an old console.",
                "#120E24", "#FFE066", "#8AFF80", "#000000", "#1B1036", 0.40f));

            // Holiday Games courts.
            Holiday(c, Holidays.ChristmasCourtId, "Candy Cane Court", HolidayTheme.Christmas,
                "Fresh snow on the blacktop, lights on every fence, and a tree behind the hoop.",
                "#5B7083", "#FFFFFF", "#C1121F", "#0B2545", "#13315C", 0.6f);
            Holiday(c, Holidays.HalloweenCourtId, "Pumpkin Patch Court", HolidayTheme.Halloween,
                "Jack-o'-lanterns on the sideline and bats under a big orange moon.",
                "#2E1F3E", "#FF8C1A", "#5A189A", "#10002B", "#3C096C", 0.55f);
            Holiday(c, Holidays.EasterCourtId, "Egg Hunt Court", HolidayTheme.Easter,
                "Spring grass, pastel paint, and painted eggs hidden along the baseline.",
                "#7FB77E", "#FFFDF5", "#F4A6C0", "#BDE0FE", "#FFF1A8", 0.5f);
            Holiday(c, Holidays.FourthCourtId, "Firework Court", HolidayTheme.FourthOfJuly,
                "Stars in the paint, stripes on the lines, and fireworks over the stands.",
                "#14213D", "#F4F1DE", "#C1121F", "#03045E", "#1D3557", 0.65f);

            c.Courts.Add(Court(PracticeCourtId, "Practice Lab", CourtCircuit.Practice,
                "An empty rec-centre court with chalk targets on the floor.",
                "#8D99AE", "#EDF2F4", "#2B2D42", "#2B2D42", "#8D99AE", 0f));
        }

        private static void Holiday(ContentCatalog c, string id, string name, HolidayTheme theme, string description,
                                    string floor, string lines, string paint, string skyTop, string skyBottom, float crowd)
        {
            var court = Court(id, name, CourtCircuit.Holiday, description, floor, lines, paint, skyTop, skyBottom, crowd);
            court.theme = theme;
            c.Courts.Add(court);
        }

        private static CourtDef Court(string id, string name, CourtCircuit circuit, string description,
                                      string floor, string lines, string paint, string skyTop, string skyBottom, float crowd)
        {
            return new CourtDef
            {
                id = id,
                displayName = name,
                circuit = circuit,
                description = description,
                floor = RgbColor.FromHex(floor),
                lines = RgbColor.FromHex(lines),
                paint = RgbColor.FromHex(paint),
                skyTop = RgbColor.FromHex(skyTop),
                skyBottom = RgbColor.FromHex(skyBottom),
                crowdDensity = crowd,
            };
        }

        // ------------------------------------------------------------------ teams

        private static void AddLeagueTeams(ContentCatalog c)
        {
            AddTeam(c, "team.eastbay_voltage", "Eastbay", "Voltage", "EBV", TeamTier.League,
                "#FFD400", "#2B2B2E", "#4B1F7A", LogoShape.Hexagon, LogoMotif.Bolt, TeamPattern.Stripes,
                "court.volt_box", "Stay charged.", true,
                P("castellan", "Rio", "Castellan", 3, Archetype.FloorGeneral, 3),
                P("vance", "Marlo", "Vance", 11, Archetype.DeepShooter, 2),
                P("tremont", "Obi", "Tremont", 32, Archetype.RimRunner, 1),
                P("park", "Sully", "Park", 5, Archetype.HustleGuard, 0));

            AddTeam(c, "team.baycity_breakers", "Bay City", "Breakers", "BCB", TeamTier.League,
                "#1FB5A6", "#FF6F59", "#14213D", LogoShape.Circle, LogoMotif.Wave, TeamPattern.Diagonal,
                "court.breakwater", "Crash the shore.", true,
                P("loren", "Kai", "Loren", 7, Archetype.Playmaker, 3),
                P("brightwater", "Teo", "Brightwater", 24, Archetype.StretchForward, 2),
                P("delacroix", "Ash", "Delacroix", 9, Archetype.LockdownWing, 1),
                P("stoll", "Remy", "Stoll", 15, Archetype.QuickCutter, 0));

            AddTeam(c, "team.harbor_hounds", "Harbor", "Hounds", "HHD", TeamTier.League,
                "#CC5500", "#F3E9D2", "#3E2415", LogoShape.Shield, LogoMotif.Paw, TeamPattern.Checker,
                "court.kennel_yard", "Never let go.", false,
                P("ketteridge", "Bo", "Ketteridge", 44, Archetype.PostAnchor, 4),
                P("sato", "Lenny", "Sato", 2, Archetype.ShotCreator, 2),
                P("halloway", "Cruz", "Halloway", 21, Archetype.GlassCleaner, 1),
                P("varga", "Pip", "Varga", 12, Archetype.TwoWaySpark, 0));

            AddTeam(c, "team.redwood_runners", "Redwood", "Runners", "RWR", TeamTier.League,
                "#1E5631", "#D4A017", "#111111", LogoShape.Badge, LogoMotif.Tree, TeamPattern.Chevrons,
                "court.grove_gym", "Deep roots, quick feet.", false,
                P("adair", "Finn", "Adair", 1, Archetype.QuickCutter, 3),
                P("mercer", "Dash", "Mercer", 10, Archetype.FloorGeneral, 2),
                P("ekwe", "Rowan", "Ekwe", 33, Archetype.RimRunner, 2),
                P("hart", "Quill", "Hart", 14, Archetype.DeepShooter, 0));

            AddTeam(c, "team.metro_comets", "Metro", "Comets", "MCM", TeamTier.League,
                "#5BC0EB", "#FFFFFF", "#7B2CBF", LogoShape.Diamond, LogoMotif.Comet, TeamPattern.Dots,
                "court.orbit_dome", "Light the lane.", false,
                P("lumen", "Zeke", "Lumen", 0, Archetype.ShotCreator, 3),
                P("castano", "Ivo", "Castano", 8, Archetype.Playmaker, 2),
                P("pelletier", "Orin", "Pelletier", 23, Archetype.LockdownWing, 1),
                P("brandt", "Tully", "Brandt", 50, Archetype.GlassCleaner, 0));

            AddTeam(c, "team.desert_drifters", "Desert", "Drifters", "DSD", TeamTier.League,
                "#E6C79C", "#B7410E", "#191970", LogoShape.Badge, LogoMotif.Dune, TeamPattern.Rings,
                "court.mirage_pavilion", "Heat checks welcome.", false,
                P("arrieta", "Sol", "Arrieta", 30, Archetype.DeepShooter, 3),
                P("venn", "Mako", "Venn", 4, Archetype.TwoWaySpark, 2),
                P("dunmore", "Cy", "Dunmore", 41, Archetype.PostAnchor, 1),
                P("fairbanks", "Lio", "Fairbanks", 6, Archetype.HustleGuard, 0));

            AddTeam(c, "team.northline_owls", "Northline", "Owls", "NLO", TeamTier.League,
                "#A8DADC", "#0B0B0B", "#C0C0C0", LogoShape.Shield, LogoMotif.Owl, TeamPattern.Cross,
                "court.nightroost_hall", "See it first.", false,
                P("quade", "Ezra", "Quade", 25, Archetype.StretchForward, 3),
                P("halvorsen", "Juno", "Halvorsen", 13, Archetype.FloorGeneral, 2),
                P("aberdeen", "Wes", "Aberdeen", 22, Archetype.LockdownWing, 1),
                P("roe", "Tamsin", "Roe", 17, Archetype.QuickCutter, 0));

            AddTeam(c, "team.solar_crowns", "Solar", "Crowns", "SOL", TeamTier.League,
                "#D1127A", "#FF8C1A", "#0F1A3C", LogoShape.Circle, LogoMotif.Crown, TeamPattern.Solid,
                "court.sunspire", "Shine last.", false,
                P("solano", "Rafe", "Solano", 3, Archetype.ShotCreator, 4),
                P("maddox", "Onyx", "Maddox", 34, Archetype.RimRunner, 2),
                P("farrow", "Ziggy", "Farrow", 11, Archetype.Playmaker, 1),
                P("olander", "Brick", "Olander", 45, Archetype.GlassCleaner, 0));
        }

        private static void AddCircuitCrews(ContentCatalog c)
        {
            // Season 12 rival: every twelfth Rise season. The mini-golf crew: patient, precise, every shot lined up.
            AddTeam(c, Rival12CrewId, "", "Putt Club", "PTC", TeamTier.Rival,
                "#52B788", "#081C15", "#F72585", LogoShape.Circle, LogoMotif.Flag, TeamPattern.Checker,
                "court.hole_eighteen", "Read the green.", false,
                P("quarrie", "Bex", "Quarrie", 18, Archetype.ShotCreator, 4),
                P("penhallow", "Arlo", "Penhallow", 3, Archetype.Playmaker, 4),
                P("merriweather", "Dot", "Merriweather", 44, Archetype.PostAnchor, 4));

            // Season 11 rival: every eleventh Rise season. Cliff-top kite flyers: shooters everywhere, the ball always in the air.
            AddTeam(c, Rival11CrewId, "", "Sky Kites", "SKT", TeamTier.Rival,
                "#FFB703", "#023047", "#219EBC", LogoShape.Diamond, LogoMotif.Kite, TeamPattern.Diagonal,
                "court.bluff_top", "Catch the wind.", false,
                P("takahashi", "Rin", "Takahashi", 9, Archetype.DeepShooter, 4),
                P("abernathy", "Sunny", "Abernathy", 22, Archetype.TwoWaySpark, 4),
                P("ostrowski", "Kalani", "Ostrowski", 50, Archetype.RimRunner, 4));

            // Season 10 rival: every tenth Rise season. The all-night laundromat crew: the ball never stops moving.
            AddTeam(c, Rival10CrewId, "", "Wash House", "WSH", TeamTier.Rival,
                "#81B29A", "#22223B", "#F2CC8F", LogoShape.Badge, LogoMotif.Bubbles, TeamPattern.Dots,
                "court.suds_alley", "Open all night.", false,
                P("whitaker", "Opal", "Whitaker", 4, Archetype.FloorGeneral, 4),
                P("haldane", "Dmitri", "Haldane", 12, Archetype.QuickCutter, 4),
                P("corrigan", "June", "Corrigan", 41, Archetype.GlassCleaner, 4));

            // Season 9 rival: every ninth Rise season. A roller-rink crew: full-court pressure and quick hands.
            AddTeam(c, Rival9CrewId, "", "Roller Royals", "RRY", TeamTier.Rival,
                "#7209B7", "#F72585", "#4CC9F0", LogoShape.Shield, LogoMotif.Skate, TeamPattern.Stripes,
                "court.starlight_rink", "Couples skate is over.", false,
                P("varo", "Skye", "Varo", 7, Archetype.HustleGuard, 4),
                P("lindell", "Bree", "Lindell", 21, Archetype.RimRunner, 4),
                P("salas", "Lu", "Salas", 30, Archetype.DeepShooter, 4));

            // Season 8 rival: every eighth Rise season. A night-market crew: a sagging zone and lobs over the top.
            AddTeam(c, Rival8CrewId, "", "Night Lanterns", "NLN", TeamTier.Rival,
                "#D62828", "#1D1A31", "#FCBF49", LogoShape.Hexagon, LogoMotif.Lantern, TeamPattern.Rings,
                "court.night_market", "Lights up at closing time.", false,
                P("akande", "Juno", "Akande", 8, Archetype.Playmaker, 4),
                P("lindqvist", "Mae", "Lindqvist", 33, Archetype.PostAnchor, 4),
                P("ferreira", "Tavi", "Ferreira", 17, Archetype.StretchForward, 4));

            // Season 7 rival: every seventh Rise season. Bike couriers: fast breaks, all game.
            AddTeam(c, Rival7CrewId, "", "Comet Couriers", "CMC", TeamTier.Rival,
                "#FB5607", "#1B1B1E", "#FFBE0B", LogoShape.Circle, LogoMotif.Comet, TeamPattern.Chevrons,
                "court.tram_yard", "Delivered before you set up.", false,
                P("okoro", "Remy", "Okoro", 2, Archetype.QuickCutter, 4),
                P("sato", "Kit", "Sato", 9, Archetype.ShotCreator, 4),
                P("brandt", "Ilse", "Brandt", 41, Archetype.RimRunner, 4));

            // Season 6 rival: every sixth Rise season.
            AddTeam(c, Rival6CrewId, "", "Lighthouse Keepers", "LHK", TeamTier.Rival,
                "#0B2545", "#F4F1DE", "#FFD60A", LogoShape.Shield, LogoMotif.Lighthouse, TeamPattern.Diagonal,
                "court.lighthouse_point", "We see you coming.", false,
                P("marsh", "Wren", "Marsh", 1, Archetype.Playmaker, 4),
                P("calder", "Otis", "Calder", 34, Archetype.PostAnchor, 4),
                P("holt", "Juniper", "Holt", 30, Archetype.DeepShooter, 4));

            // Season 5 rival: every fifth Rise season.
            AddTeam(c, Rival5CrewId, "", "Cassette Club", "CSC", TeamTier.Rival,
                "#F2E9E4", "#22223B", "#C9184A", LogoShape.Circle, LogoMotif.Signal, TeamPattern.Stripes,
                "court.record_roof", "Side B hits harder.", false,
                P("rivera", "Echo", "Rivera", 8, Archetype.ShotCreator, 3),
                P("banks", "Tully", "Banks", 23, Archetype.RimRunner, 3),
                P("adeyemi", "Sade", "Adeyemi", 11, Archetype.Playmaker, 3));

            AddTeam(c, "crew.cage_regulars", "", "Cage Regulars", "CGR", TeamTier.Circuit,
                "#E07A5F", "#3D405B", "#F2CC8F", LogoShape.Shield, LogoMotif.Ball, TeamPattern.Stripes,
                "court.sunset_cage", "First come, first served.", false,
                P("bram", "Nico", "Bram", 2, Archetype.FloorGeneral, -6),
                P("juarez", "Hal", "Juarez", 31, Archetype.GlassCleaner, -6),
                P("parrish", "Dee", "Parrish", 13, Archetype.DeepShooter, -5));

            AddTeam(c, "crew.pier_pressure", "", "Pier Pressure", "PRP", TeamTier.Circuit,
                "#2A9D8F", "#E9C46A", "#264653", LogoShape.Circle, LogoMotif.Wave, TeamPattern.Dots,
                "court.pier_nine", "Hold the pier.", false,
                P("quon", "Marty", "Quon", 9, Archetype.ShotCreator, -5),
                P("whitlow", "Gus", "Whitlow", 40, Archetype.PostAnchor, -5),
                P("tenney", "Ray", "Tenney", 18, Archetype.QuickCutter, -4));

            AddTeam(c, "crew.underpass_union", "", "Underpass Union", "UPU", TeamTier.Circuit,
                "#9B5DE5", "#FEE440", "#1B1B3A", LogoShape.Diamond, LogoMotif.Signal, TeamPattern.Chevrons,
                "court.overpass_park", "Loud under the lights.", false,
                P("ansel", "Kip", "Ansel", 6, Archetype.TwoWaySpark, -4),
                P("sturgis", "Mo", "Sturgis", 35, Archetype.RimRunner, -3),
                P("corwin", "Jax", "Corwin", 1, Archetype.Playmaker, -3));

            AddTeam(c, "crew.rooftop_relay", "", "Rooftop Relay", "RFR", TeamTier.Circuit,
                "#3D5A80", "#E0FBFC", "#EE6C4D", LogoShape.Badge, LogoMotif.Comet, TeamPattern.Rings,
                "court.rooftop_ring", "Pass it up.", false,
                P("okafor", "Tobi", "Okafor", 7, Archetype.Playmaker, -3),
                P("lindqvist", "Saul", "Lindqvist", 33, Archetype.StretchForward, -2),
                P("pryce", "Remy", "Pryce", 15, Archetype.LockdownWing, -2));

            AddTeam(c, "crew.boardwalk_bandits", "", "Boardwalk Bandits", "BWB", TeamTier.Circuit,
                "#E09F3E", "#540B0E", "#FFF3B0", LogoShape.Circle, LogoMotif.Dune, TeamPattern.Checker,
                "court.boardwalk_slab", "Take what's open.", false,
                P("castellan", "Vic", "Castellan", 0, Archetype.ShotCreator, -2),
                P("brightwater", "Ike", "Brightwater", 24, Archetype.HustleGuard, -1),
                P("moreau", "Dax", "Moreau", 44, Archetype.RimRunner, -1));

            // The rival crew: shows up once a season in Rise Mode.
            AddTeam(c, RivalCrewId, "", "Neon Static", "NST", TeamTier.Rival,
                "#00F5D4", "#10002B", "#F72585", LogoShape.Diamond, LogoMotif.Bolt, TeamPattern.Diagonal,
                "court.static_lot", "Turn it up.", false,
                P("marlowe", "Vex", "Marlowe", 13, Archetype.ShotCreator, 2),
                P("okonjo", "Ira", "Okonjo", 9, Archetype.LockdownWing, 1),
                P("delacroix", "Bo", "Delacroix", 50, Archetype.RimRunner, 1));

            // Season 2 rival: shows up in even-numbered Rise seasons.
            AddTeam(c, Rival2CrewId, "", "Sundown Syndicate", "SDS", TeamTier.Rival,
                "#FF8C42", "#3D1E6D", "#FFE8D6", LogoShape.Shield, LogoMotif.Dune, TeamPattern.Chevrons,
                "court.sundown_yard", "Last light, last word.", false,
                P("sol", "Kaia", "Sol", 7, Archetype.Playmaker, 3),
                P("brandt", "Otto", "Brandt", 31, Archetype.GlassCleaner, 2),
                P("ivers", "Nell", "Ivers", 22, Archetype.DeepShooter, 2));

            // Season 3 rival: every third Rise season.
            AddTeam(c, Rival3CrewId, "", "Midnight Tide", "MDT", TeamTier.Rival,
                "#0EA5E9", "#020617", "#E0F2FE", LogoShape.Circle, LogoMotif.Wave, TeamPattern.Rings,
                "court.ferry_deck", "The tide always comes in.", false,
                P("quill", "Mara", "Quill", 4, Archetype.TwoWaySpark, 3),
                P("osei", "Tobi", "Osei", 15, Archetype.QuickCutter, 3),
                P("lindqvist", "Sven", "Lindqvist", 42, Archetype.PostAnchor, 2));

            // Season 4 rival: every fourth Rise season.
            AddTeam(c, Rival4CrewId, "", "Paper Cranes", "CRN", TeamTier.Rival,
                "#FFFBEB", "#E11D48", "#1F2937", LogoShape.Hexagon, LogoMotif.Crane, TeamPattern.Diagonal,
                "court.paper_garden", "Folded a thousand times.", false,
                P("vale", "Juno", "Vale", 7, Archetype.Playmaker, 3),
                P("okafor", "Ari", "Okafor", 21, Archetype.LockdownWing, 3),
                P("castillo", "Wren", "Castillo", 33, Archetype.StretchForward, 2));

            // Secret teams (Arcade Ladder boss and a code-unlocked crew). Original characters.
            AddTeam(c, BossTeamId, "", "The Glitch", "GLT", TeamTier.Secret,
                "#FF2E88", "#0B0B1A", "#00F0FF", LogoShape.Diamond, LogoMotif.Signal, TeamPattern.Cross,
                BossCourtId, "Error: defense not found.", false,
                P("frame", "Ghost", "Frame", 0, Archetype.ShotCreator, 7),
                P("okafor", "Byte", "Okafor", 8, Archetype.PostAnchor, 6),
                P("moreno", "Lag", "Moreno", 64, Archetype.LockdownWing, 6));
            AddTeam(c, SecretCrewId, "", "Cartridge Kids", "CTK", TeamTier.Secret,
                "#8AFF80", "#120E24", "#FFE066", LogoShape.Badge, LogoMotif.Ball, TeamPattern.Checker,
                SecretCourtId, "Blow on it and try again.", false,
                P("park", "Pixel", "Park", 16, Archetype.QuickCutter, 4),
                P("tuner", "Chip", "Tuner", 2, Archetype.DeepShooter, 4),
                P("vale", "Sprite", "Vale", 32, Archetype.RimRunner, 4));

            // The player's crew. Rook is the default avatar; nickname is stored in save data.
            AddTeam(c, PlayerCrewId, "", "First Callers", "FCL", TeamTier.PlayerCrew,
                "#F72585", "#4CC9F0", "#1A1A2E", LogoShape.Hexagon, LogoMotif.Signal, TeamPattern.Diagonal,
                "court.overpass_park", "Call your shot.", true,
                PFull(RookPlayerId, "Rook", "", 1, Archetype.TwoWaySpark, -10),
                P("quarles", "Benny", "Quarles", 12, Archetype.HustleGuard, -8, "crew"),
                P("ashby", "Dre", "Ashby", 21, Archetype.StretchForward, -8, "crew"));
        }

        private struct PlayerSeed
        {
            public string id;
            public string first;
            public string last;
            public int number;
            public Archetype archetype;
            public int offset;
        }

        /// <summary>Each AI team's favourite defence (they adjust in-game; see MatchSimulation.AdjustScheme).</summary>
        private static void AddSchemes(ContentCatalog c)
        {
            void S(string id, DefenseScheme s)
            {
                var t = c.Team(id);
                if (t != null) t.scheme = s;
            }
            S("team.eastbay_voltage", DefenseScheme.Pressure);
            S("team.baycity_breakers", DefenseScheme.Zone);
            S("team.harbor_hounds", DefenseScheme.Pressure);
            S("team.metro_comets", DefenseScheme.PackLine);
            S("team.desert_drifters", DefenseScheme.Zone);
            S("team.northline_owls", DefenseScheme.PackLine);
            S("crew.pier_pressure", DefenseScheme.Pressure);
            S("crew.underpass_union", DefenseScheme.PackLine);
            S("crew.rooftop_relay", DefenseScheme.Zone);
            S(RivalCrewId, DefenseScheme.Pressure);
            S(Rival2CrewId, DefenseScheme.Zone);
            S(Rival3CrewId, DefenseScheme.PackLine);
            S(Rival4CrewId, DefenseScheme.ManToMan);
            S(Rival5CrewId, DefenseScheme.Zone);
            S(Rival6CrewId, DefenseScheme.Pressure);
            S(Rival7CrewId, DefenseScheme.ManToMan);
            S(Rival8CrewId, DefenseScheme.Zone);
            S(Rival9CrewId, DefenseScheme.Pressure);
            S(Rival10CrewId, DefenseScheme.PackLine);
            S(Rival11CrewId, DefenseScheme.ManToMan);
            S(Rival12CrewId, DefenseScheme.Zone);
            S(BossTeamId, DefenseScheme.Pressure);
            S(SecretCrewId, DefenseScheme.Zone);
        }

        private static PlayerSeed P(string slug, string first, string last, int number, Archetype a, int offset, string prefix = null)
        {
            return new PlayerSeed { id = prefix == null ? slug : "player." + prefix + "." + slug, first = first, last = last, number = number, archetype = a, offset = offset };
        }

        private static PlayerSeed PFull(string id, string first, string last, int number, Archetype a, int offset)
        {
            return new PlayerSeed { id = id, first = first, last = last, number = number, archetype = a, offset = offset };
        }

        private static void AddTeam(ContentCatalog c, string id, string city, string nickname, string abbr, TeamTier tier,
                                    string primary, string secondary, string accent,
                                    LogoShape shape, LogoMotif motif, TeamPattern pattern,
                                    string courtId, string motto, bool unlocked, params PlayerSeed[] roster)
        {
            var team = new TeamDef
            {
                id = id,
                city = city,
                nickname = nickname,
                abbreviation = abbr,
                tier = tier,
                primary = RgbColor.FromHex(primary),
                secondary = RgbColor.FromHex(secondary),
                accent = RgbColor.FromHex(accent),
                logoShape = shape,
                logoMotif = motif,
                pattern = pattern,
                homeCourtId = courtId,
                motto = motto,
                unlockedByDefault = unlocked,
            };

            string teamSlug = abbr.ToLowerInvariant();
            foreach (var seed in roster)
            {
                string playerId = seed.id.StartsWith("player.", System.StringComparison.Ordinal)
                    ? seed.id
                    : "player." + teamSlug + "." + seed.id;
                var archetypeDef = FindArchetype(c.Archetypes, seed.archetype);
                c.Players.Add(new PlayerDef
                {
                    id = playerId,
                    firstName = seed.first,
                    lastName = seed.last,
                    jerseyNumber = seed.number,
                    archetypeId = archetypeDef.id,
                    attributes = Personalize(archetypeDef.baseline.Offset(seed.offset), playerId),
                    appearance = AppearanceFromSeed(playerId, seed.archetype),
                });
                team.rosterPlayerIds.Add(playerId);
            }
            c.Teams.Add(team);
        }

        private static ArchetypeDef FindArchetype(List<ArchetypeDef> list, Archetype a)
        {
            foreach (var def in list) if (def.archetype == a) return def;
            throw new System.InvalidOperationException("Missing archetype " + a);
        }

        /// <summary>Deterministic ±3 variation per attribute so same-archetype players differ.</summary>
        internal static AttributeSet Personalize(AttributeSet baseline, string playerId)
        {
            uint h = StableHash.Of(playerId);
            var result = baseline;
            for (int i = 0; i < RatingScale.AttributeCount; i++)
            {
                h = StableHash.Next(h);
                int delta = (int)(h % 7) - 3;
                var t = (AttributeType)i;
                result = result.With(t, RatingScale.Clamp(result.Get(t) + delta));
            }
            return result;
        }

        internal static AppearanceDef AppearanceFromSeed(string playerId, Archetype a)
        {
            uint h = StableHash.Of(playerId + ".look");
            int skin = (int)(h % 6); h = StableHash.Next(h);
            int hair = (int)(h % 6); h = StableHash.Next(h);
            int hairColor = (int)(h % 5);
            BodyType body;
            int height;
            switch (a)
            {
                case Archetype.PostAnchor:
                case Archetype.GlassCleaner:
                    body = BodyType.Broad; height = 2; break;
                case Archetype.StretchForward:
                case Archetype.RimRunner:
                    body = BodyType.Standard; height = 2; break;
                case Archetype.HustleGuard:
                case Archetype.FloorGeneral:
                case Archetype.QuickCutter:
                    body = BodyType.Slim; height = 0; break;
                default:
                    body = BodyType.Standard; height = 1; break;
            }
            return new AppearanceDef(skin, hair, hairColor, body, height);
        }

        // ------------------------------------------------------------------ rules & difficulty

        private static void AddRules(ContentCatalog c)
        {
            c.Rules.Add(new GameRulesDef { id = DefaultRulesId });
            c.Rules.Add(new GameRulesDef
            {
                id = "rules.king",
                targetScore = 11,
                useGameClock = true,
                gameClockSeconds = 90f,
            });
            c.Rules.Add(new GameRulesDef
            {
                id = "rules.21",
                targetScore = 21,
                useGameClock = false,
                bustRule = true,
                bustScore = 13,
                makeItTakeIt = true,
            });
            c.Rules.Add(new GameRulesDef
            {
                // Full Court 5-on-5: four minutes, 2s and 3s, a longer shot clock to bring it up the floor.
                id = FullCourt.RulesId,
                targetScore = 99,
                useGameClock = true,
                gameClockSeconds = 240f,
                shotClockSeconds = 20f,
                insideArcPoints = 2,
                beyondArcPoints = 3,
            });
            // 15 Oct 2026: THE STREETS (first to 21, win by two, no clock).
            foreach (var r in Streets.AllRules()) c.Rules.Add(r);
            c.Rules.Add(new GameRulesDef
            {
                // The Park: first to 15 by 1s and 2s, win by two, make it take it.
                id = "rules.street",
                targetScore = 15,
                useGameClock = true,
                gameClockSeconds = 180f,
                winByTwo = true,
                makeItTakeIt = true,
            });
            c.Rules.Add(new GameRulesDef
            {
                id = "rules.oneonone",
                targetScore = 11,
                useGameClock = true,
                gameClockSeconds = 120f,
                // 9 Oct 2026 (Omari): make it take it, the scorer gets the ball back.
                makeItTakeIt = true,
            });
            c.Rules.Add(new GameRulesDef
            {
                // Phase 37 CLUTCH (half court): the clock decides it; a tie at the horn goes to sudden death.
                id = Clutch.RulesId,
                targetScore = 99,
                useGameClock = true,
                gameClockSeconds = 60f,
            });
            c.Rules.Add(new GameRulesDef
            {
                id = "rules.demo",
                targetScore = 11,
                useGameClock = true,
                gameClockSeconds = 70f,
            });
            c.Rules.Add(new GameRulesDef
            {
                id = "rules.arcade",
                targetScore = 15,
                useGameClock = true,
                gameClockSeconds = 150f,
            });
            c.Rules.Add(new GameRulesDef
            {
                id = "rules.practice",
                targetScore = 999,
                useGameClock = false,
                shotClockSeconds = 999f,
                checkBallAfterScore = false,
                suddenDeathOnTie = false,
            });
        }

        private static void AddDifficulties(ContentCatalog c)
        {
            c.Difficulties.Add(new DifficultyDef
            {
                id = "difficulty.rookie", displayName = "Rookie", sortOrder = 0,
                reactionTime = 0.55f, decisionQuality = 0.45f, shotQualityThreshold = 0.30f,
                errorRate = 0.22f, movementScale = 0.90f, releaseAccuracy = 0.35f,
            });
            c.Difficulties.Add(new DifficultyDef
            {
                id = DefaultDifficultyId, displayName = "Caller", sortOrder = 1,
                reactionTime = 0.35f, decisionQuality = 0.70f, shotQualityThreshold = 0.40f,
                errorRate = 0.12f, movementScale = 1.0f, releaseAccuracy = 0.55f,
            });
            c.Difficulties.Add(new DifficultyDef
            {
                id = "difficulty.legend", displayName = "Legend", sortOrder = 2,
                reactionTime = 0.20f, decisionQuality = 0.90f, shotQualityThreshold = 0.48f,
                errorRate = 0.05f, movementScale = 1.0f, releaseAccuracy = 0.75f,
            });
            // 17 Oct 2026: BALLER CITY's crews play at BALLER or LEGENDARY (not offered in the game's own pickers).
            c.Difficulties.Add(CityCrews.Baller);
            c.Difficulties.Add(CityCrews.Legendary);
        }

        // ------------------------------------------------------------------ progression

        private static void AddUpgrades(ContentCatalog c)
        {
            c.Upgrades.Add(Upgrade("upgrade.finishing", "Finishing Drills", "Touch at the rim through contact.", AttributeType.Finishing));
            c.Upgrades.Add(Upgrade("upgrade.shooting", "Shooting Reps", "Late-night reps from every spot.", AttributeType.Shooting));
            c.Upgrades.Add(Upgrade("upgrade.playmaking", "Film Study", "See the pass before it opens.", AttributeType.Playmaking));
            c.Upgrades.Add(Upgrade("upgrade.defense", "Slide Work", "Stay in front, hands active.", AttributeType.Defense));
            c.Upgrades.Add(Upgrade("upgrade.rebounding", "Box-Out Circuit", "Find a body, then find the ball.", AttributeType.Rebounding));
            c.Upgrades.Add(Upgrade("upgrade.speed", "Sprint Ladders", "A quicker first step.", AttributeType.Speed));
            c.Upgrades.Add(Upgrade("upgrade.stamina", "Hill Runs", "Legs that last to the final possession.", AttributeType.Stamina));
            c.Upgrades.Add(Upgrade("upgrade.clutch", "Pressure Sets", "Practise with the clock running down.", AttributeType.Clutch));
        }

        private static UpgradeDef Upgrade(string id, string name, string description, AttributeType attribute)
        {
            return new UpgradeDef
            {
                id = id, displayName = name, description = description, attribute = attribute,
                amountPerLevel = 2, maxLevel = 5, baseCost = 150, costGrowth = 1.5f, trainingGames = 1, attributeCap = 90,
            };
        }

        private static void AddCosmetics(ContentCatalog c)
        {
            c.Cosmetics.Add(Cosmetic("cosmetic.jersey.crew_home", "Crew Home", CosmeticSlot.JerseyPalette, 0, 0, true, "#F72585", "#4CC9F0"));
            c.Cosmetics.Add(Cosmetic("cosmetic.jersey.sunset_fade", "Sunset Fade", CosmeticSlot.JerseyPalette, 200, 50, false, "#FF7E5F", "#FEB47B"));
            c.Cosmetics.Add(Cosmetic("cosmetic.jersey.midnight_neon", "Midnight Neon", CosmeticSlot.JerseyPalette, 300, 150, false, "#0F0C29", "#00F5D4"));
            c.Cosmetics.Add(Cosmetic("cosmetic.jersey.arcade_mint", "Arcade Mint", CosmeticSlot.JerseyPalette, 350, 300, false, "#2EC4B6", "#FFBF69"));
            c.Cosmetics.Add(Cosmetic("cosmetic.jersey.gold_rush", "Gold Rush", CosmeticSlot.JerseyPalette, 500, 600, false, "#FFD166", "#1A1A2E"));
            c.Cosmetics.Add(Cosmetic("cosmetic.jersey.chalk_brick", "Chalk & Brick", CosmeticSlot.JerseyPalette, 300, 250, false, "#EDE6D6", "#A23E48"));

            // Season 2 kits.
            c.Cosmetics.Add(Cosmetic("cosmetic.jersey.rain_slick", "Rain Slick", CosmeticSlot.JerseyPalette, 350, 400, false, "#5390D9", "#0B0F1A"));
            c.Cosmetics.Add(Cosmetic("cosmetic.jersey.snow_day", "Snow Day", CosmeticSlot.JerseyPalette, 300, 350, false, "#F1FAEE", "#457B9D"));
            c.Cosmetics.Add(Cosmetic("cosmetic.jersey.last_light", "Last Light", CosmeticSlot.JerseyPalette, 450, 700, false, "#FF8C42", "#3D1E6D"));

            // Season 3 kits.
            c.Cosmetics.Add(Cosmetic("cosmetic.jersey.night_ferry", "Night Ferry", CosmeticSlot.JerseyPalette, 400, 800, false, "#0EA5E9", "#020617"));
            c.Cosmetics.Add(Cosmetic("cosmetic.jersey.lantern", "Lantern Glow", CosmeticSlot.JerseyPalette, 450, 900, false, "#F59E0B", "#B91C1C"));
            c.Cosmetics.Add(Cosmetic("cosmetic.jersey.red_rock", "Red Rock", CosmeticSlot.JerseyPalette, 400, 850, false, "#9A4A2E", "#FFE4CC"));

            // Season 4 kits.
            c.Cosmetics.Add(Cosmetic("cosmetic.jersey.origami", "Origami", CosmeticSlot.JerseyPalette, 450, 1000, false, "#FFFBEB", "#E11D48"));
            c.Cosmetics.Add(Cosmetic("cosmetic.jersey.wash_fold", "Wash and Fold", CosmeticSlot.JerseyPalette, 400, 950, false, "#38BDF8", "#1E1B4B"));
            c.Cosmetics.Add(Cosmetic("cosmetic.jersey.double_feature", "Double Feature", CosmeticSlot.JerseyPalette, 450, 1050, false, "#EF4444", "#FDE68A"));

            c.Cosmetics.Add(Cosmetic("cosmetic.shoes.classic", "Classic Whites", CosmeticSlot.Shoes, 0, 0, true, "#FFFFFF", "#D9D9D9"));
            c.Cosmetics.Add(Cosmetic("cosmetic.shoes.volt_laces", "Volt Laces", CosmeticSlot.Shoes, 150, 40, false, "#1A1A1A", "#FFD400"));
            c.Cosmetics.Add(Cosmetic("cosmetic.shoes.glacier", "Glacier Highs", CosmeticSlot.Shoes, 200, 120, false, "#E0FBFC", "#3D5A80"));
            c.Cosmetics.Add(Cosmetic("cosmetic.shoes.cosmic", "Cosmic Runners", CosmeticSlot.Shoes, 350, 350, false, "#7209B7", "#F72585"));
            c.Cosmetics.Add(Cosmetic("cosmetic.shoes.lava_soles", "Lava Soles", CosmeticSlot.Shoes, 250, 200, false, "#2B2B2B", "#FF4D00"));

            c.Cosmetics.Add(Cosmetic("cosmetic.shoes.puddle_jumpers", "Puddle Jumpers", CosmeticSlot.Shoes, 250, 300, false, "#B8F2E6", "#5E60CE"));
            c.Cosmetics.Add(Cosmetic("cosmetic.shoes.copper_tops", "Copper Tops", CosmeticSlot.Shoes, 300, 500, false, "#B87333", "#FFE8D6"));

            c.Cosmetics.Add(Cosmetic("cosmetic.shoes.tide_runners", "Tide Runners", CosmeticSlot.Shoes, 350, 750, false, "#E0F2FE", "#0EA5E9"));
            c.Cosmetics.Add(Cosmetic("cosmetic.shoes.ember_highs", "Ember Highs", CosmeticSlot.Shoes, 400, 950, false, "#B91C1C", "#F59E0B"));

            c.Cosmetics.Add(Cosmetic("cosmetic.shoes.paper_planes", "Paper Planes", CosmeticSlot.Shoes, 350, 1000, false, "#FFFBEB", "#E11D48"));
            c.Cosmetics.Add(Cosmetic("cosmetic.shoes.marquee", "Marquee Lights", CosmeticSlot.Shoes, 400, 1100, false, "#FDE68A", "#0F172A"));

            c.Cosmetics.Add(Cosmetic("cosmetic.banner.blacktop", "Blacktop Banner", CosmeticSlot.CourtBanner, 0, 0, true, "#2F2F36", "#FFE066"));
            c.Cosmetics.Add(Cosmetic("cosmetic.banner.signal_flag", "Signal Flag", CosmeticSlot.CourtBanner, 200, 100, false, "#4CC9F0", "#F72585"));
            c.Cosmetics.Add(Cosmetic("cosmetic.banner.boardwalk", "Boardwalk Pennant", CosmeticSlot.CourtBanner, 250, 180, false, "#2EC4B6", "#FFBF69"));
            c.Cosmetics.Add(Cosmetic("cosmetic.banner.sundown", "Sundown Stripe", CosmeticSlot.CourtBanner, 300, 450, false, "#FF8C42", "#D1495B"));
            c.Cosmetics.Add(Cosmetic("cosmetic.banner.gold_signal", "Gold Signal", CosmeticSlot.CourtBanner, 600, 400, false, "#D4A017", "#1A1A2E"));

            c.Cosmetics.Add(Cosmetic("cosmetic.celebration.fist_pump", "Fist Pump", CosmeticSlot.Celebration, 0, 0, true, "#FFFFFF", "#FFFFFF"));
            c.Cosmetics.Add(Cosmetic("cosmetic.celebration.call_it", "Call It", CosmeticSlot.Celebration, 250, 120, false, "#FFFFFF", "#FFFFFF"));
            c.Cosmetics.Add(Cosmetic("cosmetic.celebration.raise_roof", "Raise the Roof", CosmeticSlot.Celebration, 400, 450, false, "#FFFFFF", "#FFFFFF"));
            c.Cosmetics.Add(Cosmetic("cosmetic.celebration.shimmy_step", "Shimmy Step", CosmeticSlot.Celebration, 350, 300, false, "#FFFFFF", "#FFFFFF"));

            c.Cosmetics.Add(Cosmetic("cosmetic.celebration.pixel_wave", "Pixel Wave", CosmeticSlot.Celebration, 300, 500, false, "#FFFFFF", "#FFFFFF"));
            c.Cosmetics.Add(Cosmetic("cosmetic.celebration.take_a_bow", "Take a Bow", CosmeticSlot.Celebration, 450, 900, false, "#FFFFFF", "#FFFFFF"));

            c.Cosmetics.Add(Cosmetic("cosmetic.celebration.shoulder_brush", "Shoulder Brush", CosmeticSlot.Celebration, 350, 1000, false, "#FFFFFF", "#FFFFFF"));
            c.Cosmetics.Add(Cosmetic("cosmetic.celebration.paper_plane", "Paper Plane", CosmeticSlot.Celebration, 450, 1200, false, "#FFFFFF", "#FFFFFF"));
            // Phase 30: its own sprite frame (fist on the chest).
            c.Cosmetics.Add(Cosmetic("cosmetic.celebration.chest_thump", "Chest Thump", CosmeticSlot.Celebration, 400, 1400, false, "#FFFFFF", "#FFFFFF"));

            c.Cosmetics.Add(Cosmetic("cosmetic.move.basic_cross", "Basic Crossover", CosmeticSlot.DribbleMove, 0, 0, true, "#FFFFFF", "#FFFFFF"));
            c.Cosmetics.Add(Cosmetic("cosmetic.move.hesi_hop", "Hesitation Hop", CosmeticSlot.DribbleMove, 300, 150, false, "#FFFFFF", "#FFFFFF"));
            c.Cosmetics.Add(Cosmetic("cosmetic.move.behind_back", "Behind the Back", CosmeticSlot.DribbleMove, 400, 250, false, "#FFFFFF", "#FFFFFF"));
            c.Cosmetics.Add(Cosmetic("cosmetic.move.double_cross", "Double Cross", CosmeticSlot.DribbleMove, 400, 600, false, "#FFFFFF", "#FFFFFF"));
            c.Cosmetics.Add(Cosmetic("cosmetic.move.step_back", "Step Back", CosmeticSlot.DribbleMove, 500, 1000, false, "#FFFFFF", "#FFFFFF"));
            c.Cosmetics.Add(Cosmetic("cosmetic.move.rocker_step", "Rocker Step", CosmeticSlot.DribbleMove, 450, 1100, false, "#FFFFFF", "#FFFFFF"));
            c.Cosmetics.Add(Cosmetic("cosmetic.move.snatch_back", "Snatch Back", CosmeticSlot.DribbleMove, 500, 1300, false, "#FFFFFF", "#FFFFFF"));
            // Season 5: dunk packages (how your dunks look; Dunks.cs).
            c.Cosmetics.Add(Cosmetic("cosmetic.dunk.two_hand", "Two-Hand Jam", CosmeticSlot.DunkPackage, 0, 0, true, "#FFFFFF", "#FFFFFF"));
            c.Cosmetics.Add(Cosmetic("cosmetic.dunk.tomahawk", "Tomahawk", CosmeticSlot.DunkPackage, 250, 150, false, "#FFFFFF", "#FFFFFF"));
            c.Cosmetics.Add(Cosmetic("cosmetic.dunk.reverse", "Reverse Jam", CosmeticSlot.DunkPackage, 350, 300, false, "#FFFFFF", "#FFFFFF"));
            c.Cosmetics.Add(Cosmetic("cosmetic.dunk.windmill", "Windmill", CosmeticSlot.DunkPackage, 450, 500, false, "#FFFFFF", "#FFFFFF"));
            c.Cosmetics.Add(Cosmetic("cosmetic.dunk.cradle", "Cradle Rock", CosmeticSlot.DunkPackage, 500, 750, false, "#FFFFFF", "#FFFFFF"));
            c.Cosmetics.Add(Cosmetic("cosmetic.dunk.three_sixty", "Three-Sixty", CosmeticSlot.DunkPackage, 650, 1000, false, "#FFFFFF", "#FFFFFF"));
            // I3 GEAR (16 Oct 2026): looks only. colorA is the sleeve / ink / goggle colour.
            c.Cosmetics.Add(Cosmetic(Gear.NoneId, "No Gear", CosmeticSlot.Gear, 0, 0, true, "#FFFFFF", "#FFFFFF"));
            c.Cosmetics.Add(Cosmetic("cosmetic.gear.shooter_sleeves", "Shooter Sleeves", CosmeticSlot.Gear, 150, 50, false, "#1C1C24", "#FFFFFF"));
            c.Cosmetics.Add(Cosmetic("cosmetic.gear.cloud_sleeves", "Cloud Sleeves", CosmeticSlot.Gear, 150, 100, false, "#F2F2F2", "#FFFFFF"));
            c.Cosmetics.Add(Cosmetic("cosmetic.gear.knee_sleeves", "Knee Sleeves", CosmeticSlot.Gear, 120, 50, false, "#1C1C24", "#FFFFFF"));
            c.Cosmetics.Add(Cosmetic("cosmetic.gear.full_compression", "Full Compression", CosmeticSlot.Gear, 300, 400, false, "#2B2B44", "#FFFFFF"));
            c.Cosmetics.Add(Cosmetic("cosmetic.gear.ink_sleeve", "Ink Sleeve", CosmeticSlot.Gear, 250, 300, false, "#22406E", "#FFFFFF"));
            c.Cosmetics.Add(Cosmetic("cosmetic.gear.goggles", "Sport Goggles", CosmeticSlot.Gear, 200, 200, false, "#FFD166", "#FFFFFF"));
            c.Cosmetics.Add(Cosmetic("cosmetic.gear.vet_package", "Vet Package", CosmeticSlot.Gear, 350, 600, false, "#E8E8EC", "#FFFFFF"));

            // Hoops Pass gear (Weekly.cs): never sold, only unlocked on the free pass track.
            c.Cosmetics.Add(PassGear("cosmetic.pass.jersey.vapor_court", "Vapor Court", CosmeticSlot.JerseyPalette, "#FF4FA3", "#3BD5FF"));
            c.Cosmetics.Add(PassGear("cosmetic.pass.shoes.horizon", "Horizon Runners", CosmeticSlot.Shoes, "#FFE066", "#FF7A3D"));
            c.Cosmetics.Add(PassGear("cosmetic.pass.banner.neon_grid", "Neon Grid", CosmeticSlot.CourtBanner, "#9B4DFF", "#3BD5FF"));
            c.Cosmetics.Add(PassGear("cosmetic.pass.jersey.sunset_swish", "Sunset Swish", CosmeticSlot.JerseyPalette, "#1B0B3A", "#FF7A3D"));
            c.Cosmetics.Add(PassGear("cosmetic.pass.jersey.static_bloom", "Static Bloom", CosmeticSlot.JerseyPalette, "#2B2D42", "#EF233C"));
            c.Cosmetics.Add(PassGear("cosmetic.pass.shoes.cloud_nine", "Cloud Nines", CosmeticSlot.Shoes, "#F8F8FF", "#8ECAE6"));
            c.Cosmetics.Add(PassGear("cosmetic.pass.banner.checker_flag", "Checker Flag", CosmeticSlot.CourtBanner, "#F4F1DE", "#14141F"));
            c.Cosmetics.Add(PassGear("cosmetic.pass.jersey.tropic_night", "Tropic Night", CosmeticSlot.JerseyPalette, "#023047", "#FB8500"));
            // Season 5 pass set.
            c.Cosmetics.Add(PassGear("cosmetic.pass.jersey.cassette_deck", "Cassette Deck", CosmeticSlot.JerseyPalette, "#F2E9E4", "#C9184A"));
            c.Cosmetics.Add(PassGear("cosmetic.pass.shoes.tape_runners", "Tape Runners", CosmeticSlot.Shoes, "#22223B", "#9A8C98"));
            c.Cosmetics.Add(PassGear("cosmetic.pass.banner.boombox", "Boombox Banner", CosmeticSlot.CourtBanner, "#FF9F1C", "#2EC4B6"));
            c.Cosmetics.Add(PassGear("cosmetic.pass.dunk.skyline", "Skyline Slam", CosmeticSlot.DunkPackage, "#FFFFFF", "#FFFFFF"));
            // Season 6 pass set (the lighthouse), topped by a pass-only celebration.
            c.Cosmetics.Add(PassGear("cosmetic.pass.jersey.beacon", "Beacon", CosmeticSlot.JerseyPalette, "#0B2545", "#FFD60A"));
            c.Cosmetics.Add(PassGear("cosmetic.pass.shoes.fog_runners", "Fog Runners", CosmeticSlot.Shoes, "#CAD2C5", "#52796F"));
            c.Cosmetics.Add(PassGear("cosmetic.pass.banner.lighthouse_beam", "Lighthouse Beam", CosmeticSlot.CourtBanner, "#FFD60A", "#0B2545"));
            c.Cosmetics.Add(PassGear("cosmetic.pass.celebration.spotlight", "Spotlight", CosmeticSlot.Celebration, "#FFFFFF", "#FFFFFF"));
            // Phase 36 LIVE SEASONS: the banner for finishing a month ALL-STAR or better (not sold, not in the pass).
            c.Cosmetics.Add(PassGear(LiveSeason.StarBannerId, "Live Season Star", CosmeticSlot.CourtBanner, "#FFD166", "#3A0CA3"));
            // Season 12 pass set (the mini-golf course), topped by a pass-only celebration.
            c.Cosmetics.Add(PassGear("cosmetic.pass.jersey.fairway", "Fairway", CosmeticSlot.JerseyPalette, "#52B788", "#081C15"));
            c.Cosmetics.Add(PassGear("cosmetic.pass.shoes.glow_spikes", "Glow Spikes", CosmeticSlot.Shoes, "#F72585", "#D8F3DC"));
            c.Cosmetics.Add(PassGear("cosmetic.pass.banner.hole_in_one", "Hole in One", CosmeticSlot.CourtBanner, "#FFD60A", "#1B4332"));
            c.Cosmetics.Add(PassGear("cosmetic.pass.celebration.putt_drop", "Putt Drop", CosmeticSlot.Celebration, "#FFFFFF", "#FFFFFF"));
            c.Cosmetics.Add(Cosmetic("cosmetic.jersey.fairway_green", "Fairway Green", CosmeticSlot.JerseyPalette, 350, 2000, false, "#2D6A4F", "#FFFFFF"));
            // Season 11 pass set (the kite flyers), topped by a pass-only celebration.
            c.Cosmetics.Add(PassGear("cosmetic.pass.jersey.tailwind", "Tailwind", CosmeticSlot.JerseyPalette, "#FFB703", "#023047"));
            c.Cosmetics.Add(PassGear("cosmetic.pass.shoes.updraft", "Updraft", CosmeticSlot.Shoes, "#219EBC", "#FFFFFF"));
            c.Cosmetics.Add(PassGear("cosmetic.pass.banner.kite_string", "Kite String", CosmeticSlot.CourtBanner, "#FB8500", "#4A6FA5"));
            c.Cosmetics.Add(PassGear("cosmetic.pass.celebration.kite_run", "Kite Run", CosmeticSlot.Celebration, "#FFFFFF", "#FFFFFF"));
            c.Cosmetics.Add(Cosmetic("cosmetic.jersey.gust_yellow", "Gust Yellow", CosmeticSlot.JerseyPalette, 350, 1900, false, "#FFB703", "#023047"));
            // Season 10 pass set (the laundromat), topped by a pass-only celebration.
            c.Cosmetics.Add(PassGear("cosmetic.pass.jersey.fresh_press", "Fresh Press", CosmeticSlot.JerseyPalette, "#81B29A", "#F4F1DE"));
            c.Cosmetics.Add(PassGear("cosmetic.pass.shoes.tumble_treads", "Tumble Treads", CosmeticSlot.Shoes, "#F2CC8F", "#22223B"));
            c.Cosmetics.Add(PassGear("cosmetic.pass.banner.open_all_night", "Open All Night", CosmeticSlot.CourtBanner, "#EF233C", "#14213D"));
            c.Cosmetics.Add(PassGear("cosmetic.pass.celebration.tumble_dry", "Tumble Dry", CosmeticSlot.Celebration, "#FFFFFF", "#FFFFFF"));
            c.Cosmetics.Add(Cosmetic("cosmetic.jersey.rinse_blue", "Rinse Blue", CosmeticSlot.JerseyPalette, 350, 1800, false, "#8ECAE6", "#22223B"));
            // Season 9 pass set (the roller rink), topped by a pass-only celebration.
            c.Cosmetics.Add(PassGear("cosmetic.pass.jersey.rink", "Rink", CosmeticSlot.JerseyPalette, "#7209B7", "#4CC9F0"));
            c.Cosmetics.Add(PassGear("cosmetic.pass.shoes.quad_glide", "Quad Glide", CosmeticSlot.Shoes, "#F72585", "#FFFFFF"));
            c.Cosmetics.Add(PassGear("cosmetic.pass.banner.mirror_ball", "Mirror Ball", CosmeticSlot.CourtBanner, "#E0E1DD", "#7209B7"));
            c.Cosmetics.Add(PassGear("cosmetic.pass.celebration.skate_glide", "Skate Glide", CosmeticSlot.Celebration, "#FFFFFF", "#FFFFFF"));
            c.Cosmetics.Add(Cosmetic("cosmetic.jersey.rink_violet", "Rink Violet", CosmeticSlot.JerseyPalette, 350, 1700, false, "#7209B7", "#F1FAEE"));
            // Season 8 pass set (the night market), topped by a pass-only celebration.
            c.Cosmetics.Add(PassGear("cosmetic.pass.jersey.lantern", "Lantern", CosmeticSlot.JerseyPalette, "#D62828", "#FCBF49"));
            c.Cosmetics.Add(PassGear("cosmetic.pass.shoes.paper_soles", "Paper Soles", CosmeticSlot.Shoes, "#FFF3E0", "#D62828"));
            c.Cosmetics.Add(PassGear("cosmetic.pass.banner.glow_row", "Glow Row", CosmeticSlot.CourtBanner, "#FCBF49", "#1D1A31"));
            c.Cosmetics.Add(PassGear("cosmetic.pass.celebration.lantern_release", "Lantern Release", CosmeticSlot.Celebration, "#FFFFFF", "#FFFFFF"));
            c.Cosmetics.Add(Cosmetic("cosmetic.jersey.lantern_red", "Lantern Red", CosmeticSlot.JerseyPalette, 350, 1600, false, "#D62828", "#FFF3E0"));
            // Season 7 pass set (the couriers), topped by a pass-only celebration.
            c.Cosmetics.Add(PassGear("cosmetic.pass.jersey.courier", "Courier", CosmeticSlot.JerseyPalette, "#FB5607", "#1B1B1E"));
            c.Cosmetics.Add(PassGear("cosmetic.pass.shoes.spoke_runners", "Spoke Runners", CosmeticSlot.Shoes, "#FFBE0B", "#3A0CA3"));
            c.Cosmetics.Add(PassGear("cosmetic.pass.banner.express_lane", "Express Lane", CosmeticSlot.CourtBanner, "#FB5607", "#FFBE0B"));
            c.Cosmetics.Add(PassGear("cosmetic.pass.celebration.victory_lap", "Victory Lap", CosmeticSlot.Celebration, "#FFFFFF", "#FFFFFF"));
            c.Cosmetics.Add(Cosmetic("cosmetic.jersey.signal_orange", "Signal Orange", CosmeticSlot.JerseyPalette, 350, 1600, false, "#FB5607", "#F8F9FA"));
            // Season 6 store kit.
            c.Cosmetics.Add(Cosmetic("cosmetic.jersey.harbor_fog", "Harbor Fog", CosmeticSlot.JerseyPalette, 350, 1500, false, "#52796F", "#CAD2C5"));
            c.Cosmetics.Add(Cosmetic("cosmetic.move.spin_cycle", "Spin Cycle", CosmeticSlot.DribbleMove, 450, 350, false, "#FFFFFF", "#FFFFFF"));
        }

        private static CosmeticDef PassGear(string id, string name, CosmeticSlot slot, string a, string b)
        {
            var d = Cosmetic(id, name, slot, 0, 0, false, a, b);
            d.passOnly = true;
            return d;
        }

        private static CosmeticDef Cosmetic(string id, string name, CosmeticSlot slot, int cost, int fans, bool unlocked, string a, string b)
        {
            return new CosmeticDef
            {
                id = id, displayName = name, slot = slot, cost = cost, fansRequired = fans,
                unlockedByDefault = unlocked, colorA = RgbColor.FromHex(a), colorB = RgbColor.FromHex(b),
            };
        }

        private static void AddSeason(ContentCatalog c)
        {
            var season = new SeasonConfigDef();
            foreach (var t in c.Teams)
                if (t.tier == TeamTier.League) season.teamIds.Add(t.id);
            c.Seasons.Add(season);
        }
    }
}
