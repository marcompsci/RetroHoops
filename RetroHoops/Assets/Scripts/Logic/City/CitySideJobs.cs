using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public enum SideJobKind
    {
        /// <summary>Win a game on a court (Ref = court id).</summary>
        WinAt = 0,
        /// <summary>Score N in one game on a court (Ref = court id).</summary>
        ScoreAt = 1,
        /// <summary>Win by N or more on a court (Ref = court id).</summary>
        MarginAt = 2,
        /// <summary>Beat a crew (Ref = crew id).</summary>
        BeatCrew = 3,
        /// <summary>A timed run: (pick up at Spots[0], then) get to the last spot in N seconds.</summary>
        Errand = 4,
        /// <summary>Find everything at Spots (N of them).</summary>
        Collect = 5,
    }

    public sealed class SideJobDef
    {
        public string Id, Region, District, Giver, Title, Pitch, Thanks, Ref, Item;
        /// <summary>Where the giver stands.</summary>
        public Vec2 At;
        public SideJobKind Kind;
        /// <summary>Points to score / margin / seconds for an errand.</summary>
        public int Target;
        /// <summary>Errands: [dropoff] or [pickup, dropoff]. Collect: every item.</summary>
        public Vec2[] Spots = new Vec2[0];
        public long Cash;
        public int Sp;
        public string Sfx;

        public string NpcId => "side." + Id;
    }

    /// <summary>What an errand step did.</summary>
    public enum ErrandStep { Nothing = 0, PickedUp = 1, Delivered = 2, TimeUp = 3 }

    /// <summary>
    /// 28 Oct 2026 (Omari, BALLER WORLD phase W4): SIDE JOBS. People around the world with a "!" over their heads ask for
    /// a hand, told in a quick comic panel: win on their court, score big, win by a lot, settle a beef with a crew, run
    /// something across the district against the clock, or find the things they lost. Take one at a time (the guide
    /// tracks it), finish it for cash and SP, and it counts toward the comic and the daily achievements.
    /// </summary>
    public static class CitySideJobs
    {
        private static Vec2 V(float x, float y) => new Vec2(x, y);

        private static SideJobDef J(string id, string region, string district, string giver, float x, float y, string title, string pitch, string thanks,
                                    SideJobKind kind, string reference, int target, long cash, int sp, string sfx, string item = null, params Vec2[] spots) =>
            new SideJobDef { Id = id, Region = region, District = district, Giver = giver, At = V(x, y), Title = title, Pitch = pitch, Thanks = thanks,
                             Kind = kind, Ref = reference, Target = target, Cash = cash, Sp = sp, Sfx = sfx, Item = item, Spots = spots };

        private const string BC = WorldRegions.BallerCity, NY = WorldRegions.NewYork, LA = WorldRegions.LosAngeles, LV = WorldRegions.LasVegas, BAY = WorldRegions.TheBay;

        public static readonly SideJobDef[] All =
        {
            // Baller City.
            J("bc.lucky", BC, CityDistricts.FirstCallers, "MS. ODETTE", 9.5f, 22.5f, "LUCKY BALL",
              "My grandson left his lucky ball in the park by the corner store. Fetch it back here in 45 seconds?", "That's the one! He'll never lose again.",
              SideJobKind.Errand, null, 45, 8000, 40, "WHOOSH!", "LUCKY BALL", V(28f, 4f), V(12.5f, 22.5f)),
            J("bc.sunset", BC, CityDistricts.Blacktop, "PIER PETE", 20f, 41.5f, "SUNSET SHOOTOUT",
              "Nobody scores at Sunset Cage like they used to. Drop 12 there and I'll pay you myself.", "Twelve! Just like the old days.",
              SideJobKind.ScoreAt, "court.sunset_cage", 12, 10000, 50, "SWISH!"),
            J("bc.lost", BC, CityDistricts.Blacktop, "LIL MO", 12f, 41.5f, "LOST BALLS",
              "I kicked five balls all over the Blacktop. Help me find them before my coach does?", "All five! Coach will never know.",
              SideJobKind.Collect, null, 5, 8000, 40, "BOING!", "BALL", V(4f, 26f), V(18f, 26f), V(45f, 13f), V(20f, 44f), V(34f, 2f)),
            // New York.
            J("ny.bodega", NY, CityDistricts.NyUptown, "MR. PAK", 4.5f, 32.5f, "HOT DELIVERY",
              "Sandwiches for the guys at the Cage. Get them there in 35 seconds, before they go cold.", "Still warm! You're hired.",
              SideJobKind.Errand, null, 35, 12000, 60, "VROOM!", "SANDWICHES", V(10f, 22f)),
            J("ny.cage", NY, CityDistricts.NyUptown, "OLD MAN RIO", 20.5f, 6f, "CAGE MATCH",
              "Nobody from out of town wins in the Cage. Change my mind.", "Huh. Maybe you belong here after all.",
              SideJobKind.WinAt, "court.ny_cage", 1, 15000, 75, "CLANG!"),
            J("ny.stoop", NY, CityDistricts.NyUptown, "LIL TEE", 29.5f, 24f, "STOOP BEEF",
              "The STOOP KIDS took our court and won't give it back. Beat them for us?", "The park's ours again!",
              SideJobKind.BeatCrew, "stoop", 1, 15000, 75, "POW!"),
            J("ny.tickets", NY, CityDistricts.NyMidtown, "DOT", 14.5f, 21.5f, "TICKET STUBS",
              "The wind took my five ticket stubs across the plaza and down to the pier. Help me find them?", "All five! The show's tonight!",
              SideJobKind.Collect, null, 5, 12000, 60, "FLAP!", "TICKET", V(2f, 4f), V(19f, 3f), V(12f, 20f), V(21.5f, 50f), V(4f, 57f)),
            J("ny.rooftop", NY, CityDistricts.NyMidtown, "SKY", 20.3f, 30f, "TOP FLOOR",
              "Score 15 on Rooftop 9 and you'll have the whole skyline watching.", "The whole city saw that.",
              SideJobKind.ScoreAt, "court.ny_rooftop", 15, 15000, 75, "SWISH!"),
            J("ny.tug", NY, CityDistricts.NyMidtown, "CAPTAIN JO", 21.5f, 48f, "RIVER RIVALS",
              "The TUGBOATS push everybody around on my pier. Push back.", "Now that's how you dock a boat.",
              SideJobKind.BeatCrew, "tugboat", 1, 20000, 100, "BOOM!"),
            // Los Angeles.
            J("la.sunscreen", LA, CityDistricts.LaBeach, "MAMA LU", 20f, 21f, "SUNSCREEN RUN",
              "The lifeguards are out of sunscreen. Run this to their tower in 30 seconds!", "You saved a lot of noses today.",
              SideJobKind.Errand, null, 30, 12000, 60, "WHOOSH!", "SUNSCREEN", V(23f, 8f)),
            J("la.ocean", LA, CityDistricts.LaBeach, "SURF DAVE", 19.5f, 8f, "BLOWOUT ON THE SAND",
              "Win at the Ocean Walk Courts by 6 or more. Show the boardwalk something.", "Gnarly. The whole boardwalk saw it.",
              SideJobKind.MarginAt, "court.la_ocean", 6, 15000, 75, "BOOM!"),
            J("la.riptide", LA, CityDistricts.LaBeach, "COACH PAZ", 44.5f, 15f, "CALM THE TIDE",
              "RIP TIDE dunks on my kids every day. Beat them.", "My kids are going to talk about this for weeks.",
              SideJobKind.BeatCrew, "riptide", 1, 18000, 90, "SLAM!"),
            J("la.hubcaps", LA, CityDistricts.LaEastside, "RAY RAY", 28.5f, 10f, "HUBCAP HUNT",
              "Hit a bump and my lowrider lost five hubcaps all over the Eastside. Find them?", "Back on the ride. Looking clean.",
              SideJobKind.Collect, null, 5, 12000, 60, "CLINK!", "HUBCAP", V(3f, 8f), V(19f, 5f), V(28f, 19f), V(45f, 48f), V(10f, 57f)),
            J("la.mural", LA, CityDistricts.LaEastside, "ARTIST VEE", 45.5f, 32f, "PAINT THE COURT",
              "Win at Mural Court and I'll paint you on the wall.", "Hold still. You're going on the wall.",
              SideJobKind.WinAt, "court.la_mural", 1, 15000, 75, "SPLAT!"),
            J("la.lowslow", LA, CityDistricts.LaEastside, "UNCLE BEE", 20f, 30f, "SPEED IT UP",
              "LOW & SLOW have run the overpass for ten years. Speed things up.", "Ten years. Over in one game.",
              SideJobKind.BeatCrew, "lowslow", 1, 18000, 90, "ZOOM!"),
            // Las Vegas.
            J("lv.flyers", LV, CityDistricts.LvStrip, "JAX", 19f, 10f, "FLYER DROP",
              "Get these show flyers to the marquee roof gate in 40 seconds. Doors open soon!", "Just in time. Showtime!",
              SideJobKind.Errand, null, 40, 14000, 70, "WHOOSH!", "FLYERS", V(10.5f, 43.6f)),
            J("lv.stage", LV, CityDistricts.LvStrip, "STAGEHAND MO", 26.6f, 10f, "ENCORE",
              "Score 18 on The Stage. The crowd wants an encore.", "Encore! Encore!",
              SideJobKind.ScoreAt, "court.lv_stage", 18, 18000, 90, "WOOO!"),
            J("lv.neon", LV, CityDistricts.LvStrip, "DJ GLOW", 20.5f, 52f, "LIGHTS OUT",
              "NEON NIGHTS think they own the fountains. Turn their lights out.", "Lights out. What a show.",
              SideJobKind.BeatCrew, "neonnights", 1, 20000, 100, "FLASH!"),
            J("lv.letters", LV, CityDistricts.LvOldTown, "NEON NELL", 20f, 44f, "FALLEN LETTERS",
              "Five letters fell off my old sign in the wind. Find them in the desert?", "My sign spells something again!",
              SideJobKind.Collect, null, 5, 14000, 70, "BZZT!", "NEON LETTER", V(3f, 10f), V(19f, 15f), V(30f, 33f), V(46f, 43f), V(10f, 57f)),
            J("lv.motel", LV, CityDistricts.LvOldTown, "NIGHT CLERK", 28f, 10f, "NO VACANCY",
              "Win at the Motel Pool Court by 5 and your room's on the house. Well, it's a court.", "Checkout's whenever you want.",
              SideJobKind.MarginAt, "court.lv_motel", 5, 16000, 80, "DING!"),
            J("lv.dust", LV, CityDistricts.LvOldTown, "SANDY", 20.5f, 13f, "DUST STORM",
              "DUST DEVILS kick sand in everybody's face. Settle it.", "The dust has settled.",
              SideJobKind.BeatCrew, "dustdevils", 1, 20000, 100, "WHOOSH!"),
            // The Bay.
            J("bay.cable", BAY, CityDistricts.BayHills, "GRIPMAN GUS", 21.5f, 38f, "BELL RINGER",
              "My cable car's bell fell off down in the park. Get it back up here in 45 seconds?", "Ding ding! Back on the line.",
              SideJobKind.Errand, null, 45, 14000, 70, "DING!", "BELL", V(10f, 19f), V(21.5f, 37f)),
            J("bay.bridge", BAY, CityDistricts.BayHills, "FOGGY", 19.5f, 48f, "VIEW FROM THE TOP",
              "Win at Bridge View with the fog rolling in. I bet you can't.", "I owe you a coffee.",
              SideJobKind.WinAt, "court.bay_bridge", 1, 16000, 80, "SWISH!"),
            J("bay.cablecars", BAY, CityDistricts.BayHills, "MISS LEE", 28f, 12f, "END OF THE LINE",
              "The CABLE CARS crew blocks the whole plaza. End of the line for them.", "Next stop: nowhere.",
              SideJobKind.BeatCrew, "cablecars", 1, 20000, 100, "DING!"),
            J("bay.records", BAY, CityDistricts.BayTown, "DJ OAK", 20f, 10f, "CRATE DIGGING",
              "My crate tipped over and five records rolled all over the Town. Find them?", "The whole collection. You're a real one.",
              SideJobKind.Collect, null, 5, 14000, 70, "SCRATCH!", "RECORD", V(3f, 37f), V(20f, 52f), V(33f, 33f), V(40f, 56f), V(30f, 3f)),
            J("bay.port", BAY, CityDistricts.BayTown, "DOCKER RAE", 26.5f, 45f, "UNDER THE CRANES",
              "Score 15 at Port Cranes. The whole shift's watching from up top.", "The cranes are honking for you!",
              SideJobKind.ScoreAt, "court.bay_port", 15, 18000, 90, "HONK!"),
            J("bay.hype", BAY, CityDistricts.BayTown, "YOUNG KEV", 42.5f, 12f, "TURN IT DOWN",
              "THE HYPE never stop yelling at the lake. Turn them down.", "Finally. Peace and quiet.",
              SideJobKind.BeatCrew, "hype", 1, 20000, 100, "SHHH!"),
            // 28 Oct 2026: the grid districts (the Avenues, the Boulevard, Neon Mile, Crosstown).
            J("ny.fare", NY, CityDistricts.NyAvenues, "CABBIE GUS", 12.5f, 24.5f, "FARE RUSH",
              "A package for the far corner of the Avenues and my cab's stuck in traffic. 40 seconds. Run!", "Right on the meter. Here's your cut.",
              SideJobKind.Errand, null, 40, 12000, 60, "HONK!", "PACKAGE", V(62.5f, 46.5f)),
            J("ny.flyers", NY, CityDistricts.NyAvenues, "MS. GRACE", 40.5f, 24.5f, "BLOCK PARTY FLYERS",
              "The wind took my block party flyers all over the Avenues. Find five and you're on the guest list.", "Everybody's coming now. Thank you, baby.",
              SideJobKind.Collect, null, 5, 12000, 60, "FLAP!", "FLYER", V(8.5f, 4.5f), V(60.5f, 21.5f), V(35.5f, 46.5f), V(66.5f, 46.5f), V(28.5f, 32.5f)),
            J("ny.blockparty", NY, CityDistricts.NyAvenues, "DJ RAY", 45.5f, 24.5f, "SPIN IT BACK",
              "I need a winner on Block Party Court before the beat drops. Win one for the block.", "That's the record I'm playing all night.",
              SideJobKind.WinAt, "court.ny_blockparty", 1, 15000, 75, "SCRATCH!"),
            J("la.parts", LA, CityDistricts.LaBoulevard, "BIG SAL", 12.5f, 24.5f, "SPARE PARTS",
              "My lowrider needs a part from the lot up the boulevard. Grab it and bring it back in 50 seconds.", "She bounces again! Gracias.",
              SideJobKind.Errand, null, 50, 14000, 70, "BOUNCE!", "SPARE PART", V(62.5f, 46.5f), V(12.5f, 24.5f)),
            J("la.shades", LA, CityDistricts.LaBoulevard, "STARLA", 40.5f, 24.5f, "LOST SHADES",
              "Five pairs of my sunglasses got scattered down the boulevard. Golden hour's coming. Help?", "Now I can see the sunset again.",
              SideJobKind.Collect, null, 5, 14000, 70, "SHINE!", "SHADES", V(8.5f, 4.5f), V(60.5f, 21.5f), V(35.5f, 46.5f), V(66.5f, 46.5f), V(28.5f, 32.5f)),
            J("la.boulevard", LA, CityDistricts.LaBoulevard, "COACH NITA", 45.5f, 24.5f, "BOULEVARD BUCKETS",
              "Score 14 at Boulevard Park and the kids in my program will never stop talking about it.", "They're already practicing your moves.",
              SideJobKind.ScoreAt, "court.la_boulevard", 14, 16000, 80, "SWISH!"),
            J("lv.shakes", LV, CityDistricts.LvNeonMile, "DINER DOT", 12.5f, 24.5f, "MILKSHAKE DASH",
              "Three milkshakes for the band at the far end of the Mile. 40 seconds before they melt!", "Not a drop spilled. Encore!",
              SideJobKind.Errand, null, 40, 15000, 75, "SLURP!", "MILKSHAKES", V(62.5f, 46.5f)),
            J("lv.bulbs", LV, CityDistricts.LvNeonMile, "SPARKY", 40.5f, 24.5f, "BURNT-OUT BULBS",
              "My sign's missing five bulbs and they rolled all over the Mile. Find them and the sign lights up for you.", "Look at her shine!",
              SideJobKind.Collect, null, 5, 15000, 75, "FZZT!", "BULB", V(8.5f, 4.5f), V(60.5f, 21.5f), V(35.5f, 46.5f), V(66.5f, 46.5f), V(28.5f, 32.5f)),
            J("lv.neonmile", LV, CityDistricts.LvNeonMile, "THE HOST", 45.5f, 24.5f, "LIGHT UP THE MILE",
              "Win on Neon Mile Court and I'll put your name in lights. Well, in bulbs.", "Your name, in lights. Told you.",
              SideJobKind.WinAt, "court.lv_neonmile", 1, 18000, 90, "BZZT!"),
            J("bay.coffee", BAY, CityDistricts.BayCrosstown, "BARISTA JUNE", 12.5f, 24.5f, "COLD BREW RUN",
              "An order for the loft on the far corner of Crosstown. 40 seconds, the hill's steep!", "Still cold. You're a legend.",
              SideJobKind.Errand, null, 40, 16000, 80, "CLINK!", "COLD BREW", V(62.5f, 46.5f)),
            J("bay.bells", BAY, CityDistricts.BayCrosstown, "WHEELS", 40.5f, 24.5f, "BIKE BELLS",
              "Somebody swiped five bells off the bike rack and dropped them around Crosstown. Find 'em?", "Ring ring! Thanks, friend.",
              SideJobKind.Collect, null, 5, 16000, 80, "DING!", "BELL", V(8.5f, 4.5f), V(60.5f, 21.5f), V(35.5f, 46.5f), V(66.5f, 46.5f), V(28.5f, 32.5f)),
            J("bay.crosstown", BAY, CityDistricts.BayCrosstown, "GRIP", 45.5f, 24.5f, "CABLE CAR CROWD",
              "The whole cable car stops to watch when somebody scores 15 on Crosstown Court. Make it stop.", "The car stopped. The whole car!",
              SideJobKind.ScoreAt, "court.bay_crosstown", 15, 20000, 100, "CLANG!"),
        };

        public static SideJobDef Find(string id) => Array.Find(All, j => j.Id == id);

        private static readonly string[] Shirts = { "#E63946", "#2A9D8F", "#F4A261", "#8338EC", "#FFB703", "#3A86FF", "#06D6A0", "#FF006E" };

        /// <summary>The person who gives the job (drawn like everyone else; never a real player).</summary>
        public static PlayerDef Giver(SideJobDef j, out KitLook kit)
        {
            uint h = StableHash.Of(j.Id);
            var shirt = RgbColor.FromHex(Shirts[h % (uint)Shirts.Length]);
            kit = KitLook.Classic(shirt, shirt.Darken(0.25f), RgbColor.FromHex("#F4F1DE"), null, TeamPattern.Solid, RgbColor.FromHex(h % 2 == 0 ? "#2B2D42" : "#5C677D"));
            var arch = (Archetype)(h / 7 % 12);
            return new PlayerDef { id = j.NpcId, firstName = j.Giver, lastName = "", jerseyNumber = (int)(h % 60), appearance = DefaultContent.AppearanceFromSeed(j.NpcId, arch) };
        }

        public static List<SideJobDef> InDistrict(string district) => new List<SideJobDef>(Array.FindAll(All, j => j.District == district));

        public static int DoneIn(CityWorldSaveData w, string region)
        {
            int n = 0;
            if (w?.sideDone == null) return 0;
            foreach (var id in w.sideDone)
            {
                var j = Find(id);
                if (j != null && j.Region == region) n++;
            }
            return n;
        }

        public static bool Done(CityWorldSaveData w, SideJobDef j) => w != null && j != null && w.sideDone.Contains(j.Id);

        public static SideJobDef Active(CityWorldSaveData w) => w == null || string.IsNullOrEmpty(w.sideActive) ? null : Find(w.sideActive);

        /// <summary>Can you take it? (null = yes) A region you can't drive to yet has nothing to take.</summary>
        public static string Locked(CareerSaveData d, SideJobDef j)
        {
            if (j == null) return Loc.T("No job here.");
            if (Done(d?.world, j)) return Loc.T("Done. Thanks again!");
            var r = WorldRegions.Find(j.Region);
            if (!WorldRegions.Open(d, r)) return WorldRegions.LockedReason(d, r);
            if (j.Kind == SideJobKind.BeatCrew)
            {
                var crew = CityCrews.Find(j.Ref);
                string why = CityCrews.LockedReason(d?.city, crew);
                if (why != null) return Loc.T(why);
            }
            return null;
        }

        /// <summary>Takes the job (dropping any other one). False if it's locked.</summary>
        public static bool Accept(CareerSaveData d, SideJobDef j)
        {
            if (d == null || Locked(d, j) != null) return false;
            var w = d.world ?? (d.world = new CityWorldSaveData());
            w.sideActive = j.Id;
            w.sideStage = 0;
            w.sideFound = new List<int>();
            w.tracking = "side:" + j.Id;
            return true;
        }

        public static void Drop(CityWorldSaveData w)
        {
            if (w == null) return;
            w.sideActive = "";
            w.sideStage = 0;
            w.sideFound = new List<int>();
            if (w.tracking != null && w.tracking.StartsWith("side:", StringComparison.Ordinal)) w.tracking = "";
        }

        /// <summary>Marks the active job done and pays it. Returns the job (null if nothing was active).</summary>
        public static SideJobDef Complete(CareerSaveData d, int day)
        {
            var w = d?.world;
            var j = Active(w);
            if (j == null) return null;
            if (!w.sideDone.Contains(j.Id)) w.sideDone.Add(j.Id);
            d.signalPoints += j.Sp;
            if (d.streetCash == null) d.streetCash = new StreetCashSaveData();
            d.streetCash.cash += j.Cash;
            d.streetCash.earned += j.Cash;
            Drop(w);
            CityAchievements.NoteSideJob(w, day);
            return j;
        }

        /// <summary>A finished game: completes the active court or crew job if it was met. Returns the job, or null.</summary>
        public static SideJobDef ApplyGame(CareerSaveData d, MatchRequest r, bool won, int margin, PlayerStatLine line, int day)
        {
            var j = Active(d?.world);
            if (j == null || r == null) return null;
            bool met;
            switch (j.Kind)
            {
                case SideJobKind.WinAt: met = won && r.CourtId == j.Ref; break;
                case SideJobKind.ScoreAt: met = r.CourtId == j.Ref && line != null && line.points >= j.Target; break;
                case SideJobKind.MarginAt: met = won && r.CourtId == j.Ref && margin >= j.Target; break;
                case SideJobKind.BeatCrew: met = won && CityCrews.FromContext(r.ContextId)?.Id == j.Ref; break;
                default: met = false; break;
            }
            return met ? Complete(d, day) : null;
        }

        // ------------------------------------------------------------------ errands and collecting (walking jobs)

        public const float Reach = 1.0f;

        /// <summary>The errand's spot to reach now (the pickup, then the dropoff).</summary>
        public static Vec2? ErrandTarget(SideJobDef j, int stage)
        {
            if (j == null || j.Kind != SideJobKind.Errand || j.Spots.Length == 0) return null;
            int i = j.Spots.Length == 1 ? 0 : Math.Min(stage, j.Spots.Length - 1);
            return j.Spots[i];
        }

        /// <summary>
        /// One step of an errand: reaching the pickup picks it up (the clock starts when you take the job), reaching the
        /// dropoff in time finishes it, running out of time fails it (talk to the giver to try again).
        /// </summary>
        public static ErrandStep StepErrand(CityWorldSaveData w, SideJobDef j, Vec2 me, float elapsed)
        {
            if (w == null || j == null || j.Kind != SideJobKind.Errand) return ErrandStep.Nothing;
            if (elapsed > j.Target) return ErrandStep.TimeUp;
            var target = ErrandTarget(j, w.sideStage);
            if (target == null || Vec2.Distance(target.Value, me) > Reach) return ErrandStep.Nothing;
            bool lastLeg = j.Spots.Length == 1 || w.sideStage >= j.Spots.Length - 1;
            if (!lastLeg) { w.sideStage++; return ErrandStep.PickedUp; }
            return ErrandStep.Delivered;
        }

        /// <summary>Picks up any item in reach. Returns its index, or -1.</summary>
        public static int StepCollect(CityWorldSaveData w, SideJobDef j, Vec2 me)
        {
            if (w == null || j == null || j.Kind != SideJobKind.Collect) return -1;
            for (int i = 0; i < j.Spots.Length; i++)
            {
                if (w.sideFound.Contains(i)) continue;
                if (Vec2.Distance(j.Spots[i], me) <= Reach) { w.sideFound.Add(i); return i; }
            }
            return -1;
        }

        public static bool CollectDone(CityWorldSaveData w, SideJobDef j) => w != null && j != null && j.Kind == SideJobKind.Collect && w.sideFound.Count >= j.Spots.Length;

        /// <summary>"Find 3/5 TICKETs" / "SANDWICHES: get to the marker in 35 s" / "Win at THE CAGE".</summary>
        public static string Goal(SideJobDef j, CityWorldSaveData w)
        {
            if (j == null) return null;
            switch (j.Kind)
            {
                case SideJobKind.WinAt: return Loc.T("Win at") + " " + CourtName(j.Ref);
                case SideJobKind.ScoreAt: return Loc.T("Score") + " " + j.Target + " " + Loc.T("at") + " " + CourtName(j.Ref);
                case SideJobKind.MarginAt: return Loc.T("Win by") + " " + j.Target + " " + Loc.T("at") + " " + CourtName(j.Ref);
                case SideJobKind.BeatCrew: return Loc.T("Beat") + " " + (CityCrews.Find(j.Ref)?.Name ?? j.Ref);
                case SideJobKind.Errand:
                    return Loc.T(j.Item) + ": " + (j.Spots.Length > 1 && (w?.sideStage ?? 0) == 0 ? Loc.T("pick it up, then bring it back") : Loc.T("get it to the marker")) + " (" + j.Target + " s)";
                default: return Loc.T("Find") + " " + (w?.sideFound.Count ?? 0) + "/" + j.Spots.Length + " " + Loc.T(j.Item);
            }
        }

        private static string CourtName(string courtId)
        {
            var home = CityDistricts.CourtHome(courtId);
            if (home == null) return courtId;
            var court = CityDistricts.Map(home.Value.district).Courts.Find(k => k.CourtId == courtId);
            return court?.Name ?? courtId;
        }

        /// <summary>Where a job's court is (district, spot), for the guide.</summary>
        public static (string district, Vec2 at)? Destination(SideJobDef j, CityWorldSaveData w)
        {
            if (j == null) return null;
            switch (j.Kind)
            {
                case SideJobKind.Errand:
                {
                    var t = ErrandTarget(j, w?.sideStage ?? 0);
                    return t == null ? ((string, Vec2)?)null : (j.District, t.Value);
                }
                case SideJobKind.Collect:
                    for (int i = 0; i < j.Spots.Length; i++)
                        if (w == null || !w.sideFound.Contains(i)) return (j.District, j.Spots[i]);
                    return (j.District, j.At);
                case SideJobKind.BeatCrew:
                    return CityGuide.CourtSpot(CityCrews.Find(j.Ref)?.CourtId);
                default:
                    return CityGuide.CourtSpot(j.Ref);
            }
        }
    }
}
