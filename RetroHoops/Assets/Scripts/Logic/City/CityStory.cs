using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>The backdrop a comic panel is drawn on.</summary>
    public enum ComicScene { Street = 0, Court = 1, Skyline = 2, Car = 3, Phone = 4, Arena = 5, Highway = 6, Rooftop = 7, Beach = 8, Neon = 9, Bridge = 10, Night = 11, Desert = 12 }

    /// <summary>One comic panel: a caption box, who speaks and what, a sound word, and the backdrop.</summary>
    public sealed class ComicPanel
    {
        /// <summary>Who's talking: "YOU", "DEE", "VELVET", "MATE" (a teammate) or "crew:id" (that crew's leader). Null = caption only.</summary>
        public string Speaker;
        public string Line, Caption, Sfx;
        public ComicScene Scene;
        /// <summary>Which city's skyline (a region id), for Skyline / Street / Night backdrops.</summary>
        public string Region;
    }

    public enum ComicGoalKind
    {
        /// <summary>Talk to N more people.</summary>
        Talk = 0,
        /// <summary>Win N more games started in the city (any region).</summary>
        Wins = 1,
        /// <summary>Win on a court (Ref = court id).</summary>
        WinAt = 2,
        /// <summary>Beat N crews from a region (Ref = region id).</summary>
        CrewsIn = 3,
        /// <summary>Get to a region (Ref = region id).</summary>
        Arrive = 4,
        /// <summary>Finish N side jobs in a region (Ref = region id).</summary>
        SideJobs = 5,
        /// <summary>Win a region's BIG RUN (Ref = region id).</summary>
        BigRun = 6,
        /// <summary>Beat N BALLER crews in all.</summary>
        BallerCrews = 7,
        /// <summary>Win the City Championship at the Baller Arena N times.</summary>
        Titles = 8,
        /// <summary>Earn a gold day on the daily achievements N more times.</summary>
        GoldDays = 9,
        /// <summary>Beat VELVET SQUAD at the Velvet Invitational.</summary>
        Velvet = 10,
        /// <summary>Drive N more times.</summary>
        Drives = 11,
    }

    public sealed class ComicGoal
    {
        public ComicGoalKind Kind;
        public string Ref, Text;
        public int Target = 1;

        /// <summary>Counted from when the goal started ("win 3 more").</summary>
        public bool Delta => Kind == ComicGoalKind.Talk || Kind == ComicGoalKind.Wins || Kind == ComicGoalKind.GoldDays || Kind == ComicGoalKind.Drives;
    }

    public sealed class StoryIssue
    {
        public int Number;
        public string Title, Tagline, Region;
        public ComicPanel[] Intro, Outro;
        public ComicGoal[] Goals;
        public long Cash;
        public int Sp;
    }

    /// <summary>What the comic did after a check: goals met, an issue finished, the next one opened.</summary>
    public sealed class StoryEvents
    {
        public readonly List<ComicGoal> GoalsDone = new List<ComicGoal>();
        public StoryIssue Finished;
        public StoryIssue Opened;
        public bool Any => GoalsDone.Count > 0 || Finished != null;
    }

    /// <summary>
    /// 28 Oct 2026 (Omari, BALLER WORLD phase W1): THE COMIC. BALLER WORLD's story, told in comic issues: a few panels
    /// to open each issue, two or three objectives, and a few panels when you finish it (with a payout in street cash and
    /// SP). It starts in Baller City, takes you on the road to New York, Los Angeles, Las Vegas and the Bay to win every
    /// region's BIG RUN, and ends back home against MR. VELVET's squad in the Baller Arena. Every objective is something
    /// the city already counts (wins, crews, side jobs, Big Runs, drives), so the comic gives the world a purpose without
    /// new rules. Issues open the roads: each region's road opens with the issue that sends you there.
    /// </summary>
    public static class CityStory
    {
        public const string VelvetContext = "velvet:invitational";

        private static ComicPanel P(string speaker, string line, ComicScene scene, string caption = null, string sfx = null, string region = null) =>
            new ComicPanel { Speaker = speaker, Line = line, Scene = scene, Caption = caption, Sfx = sfx, Region = region };

        private static ComicGoal G(ComicGoalKind kind, string text, int target = 1, string reference = null) =>
            new ComicGoal { Kind = kind, Text = text, Target = target, Ref = reference };

        private const string BC = WorldRegions.BallerCity, NY = WorldRegions.NewYork, LA = WorldRegions.LosAngeles, LV = WorldRegions.LasVegas, BAY = WorldRegions.TheBay;

        public static readonly StoryIssue[] Issues =
        {
            new StoryIssue
            {
                Number = 1, Title = "NEW KID ON THE BLOCK", Tagline = "Every legend starts on a corner court.", Region = BC, Cash = 15000, Sp = 100,
                Intro = new[]
                {
                    P(null, null, ComicScene.Skyline, "BALLER CITY. 7:42 PM.", null, BC),
                    P("DEE", "Phone's been buzzing about you. Somebody runs the street games in every big city, and he's looking for new blood.", ComicScene.Phone),
                    P("YOU", "Who's running them?", ComicScene.Street, null, null, BC),
                    P("DEE", "They call him MR. VELVET. Make a name here first. Meet the block, win a run, beat a BALLER crew.", ComicScene.Phone, null, "BZZT!"),
                },
                Goals = new[]
                {
                    G(ComicGoalKind.Talk, "Talk to 2 people in the city", 2),
                    G(ComicGoalKind.Wins, "Win a game on any city court"),
                    G(ComicGoalKind.BallerCrews, "Beat a BALLER crew"),
                },
                Outro = new[]
                {
                    P(null, null, ComicScene.Night, "ACROSS TOWN, SOMEBODY'S WATCHING...", null, BC),
                    P("VELVET", "A BALLER crew? Cute. Let's see what you do when there's real money on the court.", ComicScene.Night, null, "TAP TAP"),
                },
            },
            new StoryIssue
            {
                Number = 2, Title = "THE FLYER", Tagline = "One hundred thousand reasons to show up.", Region = BC, Cash = 25000, Sp = 150,
                Intro = new[]
                {
                    P(null, null, ComicScene.Street, "THE NEXT MORNING. A FLYER ON EVERY POLE.", "RIIIP!", BC),
                    P("DEE", "The BIG RUN. Three crews in a row, cash every round, a big purse for the final. Velvet's running it.", ComicScene.Phone),
                    P("YOU", "Then I'm winning it.", ComicScene.Court, null, "SWISH!"),
                    P("DEE", "Get some wins under you first. The poster's down at the Blacktop pier.", ComicScene.Phone),
                },
                Goals = new[]
                {
                    G(ComicGoalKind.Wins, "Win 3 more city games", 3),
                    G(ComicGoalKind.BigRun, "Win the BALLER CITY BIG RUN", 1, BC),
                },
                Outro = new[]
                {
                    P(null, null, ComicScene.Court, "THE FINAL BUZZER.", "BZZZZT!", BC),
                    P("VELVET", "Not bad, kid. But this town's small. My real games are in New York.", ComicScene.Night),
                    P("DEE", "Your ride's gassed up. Get in the car. Under thirty seconds to the city, the way you drive.", ComicScene.Car, null, "VROOM!"),
                },
            },
            new StoryIssue
            {
                Number = 3, Title = "CONCRETE JUNGLE", Tagline = "New York doesn't care who you were back home.", Region = NY, Cash = 40000, Sp = 200,
                Intro = new[]
                {
                    P(null, null, ComicScene.Highway, "THE HIGHWAY EAST.", "VROOOM!", NY),
                    P(null, null, ComicScene.Skyline, "NEW YORK. THE BRIDGES, THE TOWERS, THE NOISE.", null, NY),
                    P("crew:borough", "Fresh plates on that car. You lost? THE CAGE is for people who belong here.", ComicScene.Court, null, null, NY),
                    P("YOU", "Then I'll belong by tonight.", ComicScene.Street, null, null, NY),
                },
                Goals = new[]
                {
                    G(ComicGoalKind.Arrive, "Drive to NEW YORK", 1, NY),
                    G(ComicGoalKind.WinAt, "Win a game at THE CAGE", 1, "court.ny_cage"),
                    G(ComicGoalKind.CrewsIn, "Beat 2 New York crews", 2, NY),
                },
                Outro = new[]
                {
                    P("crew:borough", "Okay. You can play. That doesn't make you a king.", ComicScene.Court, null, null, NY),
                    P("DEE", "The New York Big Run's up on the poster by the Cage. Help a few locals first. People talk.", ComicScene.Phone),
                },
            },
            new StoryIssue
            {
                Number = 4, Title = "KING OF NEW YORK", Tagline = "The crown sits on the Cage.", Region = NY, Cash = 60000, Sp = 250,
                Intro = new[]
                {
                    P(null, null, ComicScene.Rooftop, "UP ON A ROOFTOP, NINE FLOORS UP.", null, NY),
                    P("VELVET", "Win my Big Run here and you'll get a call from the West Coast. Lose and go home.", ComicScene.Night, null, null, NY),
                    P("YOU", "I'm not here to sightsee.", ComicScene.Street, null, "CLANG!", NY),
                },
                Goals = new[]
                {
                    G(ComicGoalKind.SideJobs, "Finish a side job in New York", 1, NY),
                    G(ComicGoalKind.BigRun, "Win the NEW YORK BIG RUN", 1, NY),
                },
                Outro = new[]
                {
                    P(null, null, ComicScene.Court, "THE CAGE GOES QUIET. THEN IT EXPLODES.", "BOOM!", NY),
                    P("crew:borough", "Long live the king. For now.", ComicScene.Court, null, null, NY),
                    P("DEE", "Los Angeles called. Velvet's got a crew on the beach that's never lost. Drive west.", ComicScene.Car),
                },
            },
            new StoryIssue
            {
                Number = 5, Title = "WEST COAST", Tagline = "Sand in your shoes, sun in your eyes.", Region = LA, Cash = 60000, Sp = 250,
                Intro = new[]
                {
                    P(null, null, ComicScene.Highway, "COAST TO COAST, AS THE CROW DRIVES.", "VROOOM!", LA),
                    P(null, null, ComicScene.Beach, "LOS ANGELES. OCEAN WALK.", null, LA),
                    P("crew:goldenhour", "Out here we run. You walk it up, you're watching from the sand.", ComicScene.Beach, null, null, LA),
                },
                Goals = new[]
                {
                    G(ComicGoalKind.Arrive, "Drive to LOS ANGELES", 1, LA),
                    G(ComicGoalKind.WinAt, "Win at OCEAN WALK COURTS", 1, "court.la_ocean"),
                    G(ComicGoalKind.CrewsIn, "Beat 2 Los Angeles crews", 2, LA),
                },
                Outro = new[]
                {
                    P("crew:goldenhour", "You can run. Didn't see that coming.", ComicScene.Beach, null, null, LA),
                    P("DEE", "Velvet's Big Run poster is on the boardwalk. Keep your days golden and go win it.", ComicScene.Phone),
                },
            },
            new StoryIssue
            {
                Number = 6, Title = "GOLDEN HOUR", Tagline = "The sun sets on Velvet's beach crew.", Region = LA, Cash = 80000, Sp = 300,
                Intro = new[]
                {
                    P(null, null, ComicScene.Beach, "THE SUN DROPS INTO THE PACIFIC.", null, LA),
                    P("VELVET", "Two cities. Impressive. The desert's another story, kid. Nobody likes the heat.", ComicScene.Night, null, null, LA),
                },
                Goals = new[]
                {
                    G(ComicGoalKind.GoldDays, "Earn a GOLD day on the daily achievements"),
                    G(ComicGoalKind.BigRun, "Win the LOS ANGELES BIG RUN", 1, LA),
                },
                Outro = new[]
                {
                    P(null, null, ComicScene.Beach, "THE BOARDWALK CROWD CHANTS YOUR NAME.", "WOOO!", LA),
                    P("DEE", "Vegas. The Strip. The biggest stage in the West. Velvet built it.", ComicScene.Car, null, "VROOM!"),
                },
            },
            new StoryIssue
            {
                Number = 7, Title = "BRIGHT LIGHTS", Tagline = "Neon, desert and nowhere to hide.", Region = LV, Cash = 80000, Sp = 300,
                Intro = new[]
                {
                    P(null, null, ComicScene.Desert, "THE DESERT HIGHWAY. NOTHING BUT HEAT.", "VROOOM!", LV),
                    P(null, null, ComicScene.Neon, "LAS VEGAS. THE STRIP NEVER SLEEPS.", null, LV),
                    P("crew:headliners", "Everybody wants top billing. Earn it on THE STAGE.", ComicScene.Neon, null, null, LV),
                },
                Goals = new[]
                {
                    G(ComicGoalKind.Arrive, "Drive to LAS VEGAS", 1, LV),
                    G(ComicGoalKind.WinAt, "Win a game on THE STAGE", 1, "court.lv_stage"),
                    G(ComicGoalKind.CrewsIn, "Beat 2 Las Vegas crews", 2, LV),
                },
                Outro = new[]
                {
                    P("crew:headliners", "Fine. Your name goes on the sign. Small letters.", ComicScene.Neon, null, null, LV),
                    P("DEE", "Help out in Old Town and the Strip, then take the Big Run. The poster's by the fountains.", ComicScene.Phone),
                },
            },
            new StoryIssue
            {
                Number = 8, Title = "THE MAIN EVENT", Tagline = "Top of the marquee.", Region = LV, Cash = 100000, Sp = 350,
                Intro = new[]
                {
                    P(null, null, ComicScene.Neon, "THE MARQUEE FLICKERS: TONIGHT ONLY.", "BZZT!", LV),
                    P("VELVET", "Win here and I'll see you at the Bay. My best crew's waiting under the bridge.", ComicScene.Night, null, null, LV),
                },
                Goals = new[]
                {
                    G(ComicGoalKind.SideJobs, "Finish a side job in Las Vegas", 1, LV),
                    G(ComicGoalKind.BigRun, "Win the LAS VEGAS BIG RUN", 1, LV),
                },
                Outro = new[]
                {
                    P(null, null, ComicScene.Neon, "THE WHOLE STRIP LIGHTS UP.", "FLASH!", LV),
                    P("DEE", "One more city. The Bay. Then Velvet's got nowhere left to hide.", ComicScene.Car, null, "VROOM!"),
                },
            },
            new StoryIssue
            {
                Number = 9, Title = "FOG CITY", Tagline = "You won't see them coming.", Region = BAY, Cash = 100000, Sp = 350,
                Intro = new[]
                {
                    P(null, null, ComicScene.Bridge, "OVER THE RED BRIDGE, INTO THE FOG.", "VROOOM!", BAY),
                    P("crew:fogcity", "Can't guard what you can't see.", ComicScene.Bridge, null, null, BAY),
                    P("YOU", "Then I'll just have to light it up.", ComicScene.Street, null, null, BAY),
                },
                Goals = new[]
                {
                    G(ComicGoalKind.Arrive, "Drive to THE BAY", 1, BAY),
                    G(ComicGoalKind.WinAt, "Win at BRIDGE VIEW", 1, "court.bay_bridge"),
                    G(ComicGoalKind.CrewsIn, "Beat 2 Bay crews", 2, BAY),
                },
                Outro = new[]
                {
                    P("crew:fogcity", "Fog's lifting. Respect.", ComicScene.Bridge, null, null, BAY),
                    P("DEE", "Velvet's last Big Run is in the Bay. Then he has to come out from behind the curtain.", ComicScene.Phone),
                },
            },
            new StoryIssue
            {
                Number = 10, Title = "THE TOWN", Tagline = "Take care of business.", Region = BAY, Cash = 125000, Sp = 400,
                Intro = new[]
                {
                    P(null, null, ComicScene.Street, "ACROSS THE WATER. THE TOWN.", null, BAY),
                    P("crew:townbiz", "The Town takes care of its own. You're not its own.", ComicScene.Street, null, null, BAY),
                },
                Goals = new[]
                {
                    G(ComicGoalKind.SideJobs, "Finish a side job in the Bay", 1, BAY),
                    G(ComicGoalKind.BigRun, "Win THE BAY BIG RUN", 1, BAY),
                },
                Outro = new[]
                {
                    P(null, null, ComicScene.Court, "UNDER THE CRANES, THE WHOLE TOWN ROARS.", "WHOOOA!", BAY),
                    P("VELVET", "Four cities. Fine. Come home, kid. The Baller Arena. My squad. Everything on the line.", ComicScene.Night),
                },
            },
            new StoryIssue
            {
                Number = 11, Title = "ALL-WORLD", Tagline = "The Velvet Invitational.", Region = BC, Cash = 500000, Sp = 1000,
                Intro = new[]
                {
                    P(null, null, ComicScene.Highway, "THE LONG DRIVE HOME.", "VROOOM!", BC),
                    P(null, null, ComicScene.Arena, "THE BALLER ARENA. SOLD OUT.", null, BC),
                    P("VELVET", "Win the City Championship and my squad will be waiting on the floor.", ComicScene.Arena),
                    P("MATE", "We got you. All of us.", ComicScene.Arena),
                },
                Goals = new[]
                {
                    G(ComicGoalKind.Titles, "Win the City Championship at the Baller Arena"),
                    G(ComicGoalKind.Velvet, "Beat VELVET SQUAD at the Velvet Invitational (the arena desk)"),
                },
                Outro = new[]
                {
                    P(null, null, ComicScene.Arena, "THE FINAL HORN. THE ARENA SHAKES.", "BOOOOM!", BC),
                    P("VELVET", "...ALL-WORLD. The streets are yours, kid. Every one of them.", ComicScene.Arena),
                    P("DEE", "Four cities, four Big Runs and one Velvet. Every crew still wants a rematch. Go get it.", ComicScene.Phone, "THE END. (FOR NOW.)"),
                },
            },
        };

        public static int Count => Issues.Length;

        public static StoryIssue Issue(int number) => number >= 1 && number <= Issues.Length ? Issues[number - 1] : null;

        /// <summary>The issue you're on (Count + 1 = the comic is finished).</summary>
        public static int IssueReached(CityWorldSaveData w) => w == null ? 1 : Math.Max(1, Math.Min(Count + 1, w.issue));

        public static bool Finished(CityWorldSaveData w) => IssueReached(w) > Count;

        public static StoryIssue Current(CityWorldSaveData w) => Issue(IssueReached(w));

        /// <summary>The objective you're on, or null.</summary>
        public static ComicGoal CurrentGoal(CityWorldSaveData w)
        {
            var issue = Current(w);
            if (issue == null) return null;
            int step = Math.Max(0, Math.Min(issue.Goals.Length - 1, w?.step ?? 0));
            return issue.Goals[step];
        }

        /// <summary>The raw counter a goal reads (delta goals subtract the value the goal started from).</summary>
        public static long Counter(CareerSaveData d, ComicGoal g)
        {
            var w = d?.world;
            var s = d?.city;
            if (d == null || w == null) return 0;
            switch (g.Kind)
            {
                case ComicGoalKind.Talk: return s?.talkedTo.Count ?? 0;
                case ComicGoalKind.Wins: return s?.cityWins ?? 0;
                case ComicGoalKind.WinAt: return s != null && s.courtsWon.Contains(g.Ref) ? 1 : 0;
                case ComicGoalKind.CrewsIn: return WorldRegions.CrewsBeaten(s, WorldRegions.Find(g.Ref));
                case ComicGoalKind.Arrive: return w.regionsSeen.Contains(g.Ref) ? 1 : 0;
                case ComicGoalKind.SideJobs: return CitySideJobs.DoneIn(w, g.Ref);
                case ComicGoalKind.BigRun: return CityBigRun.Titles(w, g.Ref);
                case ComicGoalKind.BallerCrews: return CityCrews.BallerBeaten(s);
                case ComicGoalKind.Titles: return d.bigTime?.arenaTitles ?? 0;
                case ComicGoalKind.GoldDays: return w.achGoldDays;
                case ComicGoalKind.Velvet: return w.velvetBeaten ? 1 : 0;
                case ComicGoalKind.Drives: return w.drives;
                default: return 0;
            }
        }

        /// <summary>How far along a goal is: (now, target).</summary>
        public static (int now, int target) Progress(CareerSaveData d, ComicGoal g, bool current)
        {
            long v = Counter(d, g);
            if (g.Delta) v = current ? v - (d?.world?.stepBase ?? 0) : 0;
            return ((int)Math.Max(0, Math.Min(g.Target, v)), g.Target);
        }

        /// <summary>
        /// Moves the comic on as far as it can: every goal that's now met is ticked, a finished issue pays out and the
        /// next opens (its delta goals start counting from now). Safe to call any time.
        /// </summary>
        public static StoryEvents Check(CareerSaveData d)
        {
            var ev = new StoryEvents();
            var w = d?.world;
            if (w == null) return ev;
            for (int guard = 0; guard < 64 && !Finished(w); guard++)
            {
                var issue = Current(w);
                if (w.step < 0) w.step = 0;
                if (w.step >= issue.Goals.Length) { FinishIssue(d, issue, ev); continue; }
                var g = issue.Goals[w.step];
                var (now, target) = Progress(d, g, true);
                if (now < target) break;
                ev.GoalsDone.Add(g);
                w.step++;
                StartStep(d);
                if (w.step >= issue.Goals.Length) FinishIssue(d, issue, ev);
            }
            return ev;
        }

        private static void FinishIssue(CareerSaveData d, StoryIssue issue, StoryEvents ev)
        {
            var w = d.world;
            w.issue = issue.Number + 1;
            w.step = 0;
            StartStep(d);
            d.signalPoints += issue.Sp;
            if (d.streetCash == null) d.streetCash = new StreetCashSaveData();
            d.streetCash.cash += issue.Cash;
            d.streetCash.earned += issue.Cash;
            ev.Finished = issue;
            ev.Opened = Issue(w.issue);
            w.pendingOutro = issue.Number; // the closing pages show in the city (cleared there)
        }

        /// <summary>A goal starts: delta goals count from the counter's value now.</summary>
        public static void StartStep(CareerSaveData d)
        {
            var g = CurrentGoal(d?.world);
            d.world.stepBase = g != null && g.Delta ? Counter(d, g) : 0;
        }

        /// <summary>The cover hasn't been shown for this issue yet (the city opens with it).</summary>
        public static bool NeedsIntro(CityWorldSaveData w) => w != null && !Finished(w) && !w.issuesOpened.Contains(IssueReached(w));

        public static void MarkIntroShown(CityWorldSaveData w)
        {
            if (w == null || Finished(w)) return;
            int n = IssueReached(w);
            if (!w.issuesOpened.Contains(n)) w.issuesOpened.Add(n);
        }

        /// <summary>"ISSUE #3: CONCRETE JUNGLE".</summary>
        public static string Header(StoryIssue i) => i == null ? Loc.T("THE END") : Loc.T("ISSUE") + " #" + i.Number + ": " + Loc.T(i.Title);

        /// <summary>The objective line for the HUD ticker ("Win a game at THE CAGE 0/1").</summary>
        public static string Ticker(CareerSaveData d)
        {
            var g = CurrentGoal(d?.world);
            if (g == null) return null;
            var (now, target) = Progress(d, g, true);
            return Loc.T(g.Text) + (target > 1 ? "  " + now + "/" + target : "");
        }

        // ------------------------------------------------------------------ the finale: the Velvet Invitational

        /// <summary>VELVET SQUAD: Velvet's all-stars (LEGENDARY, a little better than every crew).</summary>
        public static readonly CityCrewDef VelvetSquad = new CityCrewDef
        {
            Id = "velvet", Abbr = "VLV", Name = "VELVET SQUAD", Leader = "VELVET", Style = "ALL-STARS", StyleBlurb = "The best of every city, hand-picked.",
            Trash = "Everything has a price. Yours is this game.", CourtId = CityArena.CourtId, Tier = CityCrewTier.Legendary,
            Scheme = DefenseScheme.ManToMan, Primary = "#5A189A", Secondary = "#FFD166",
            Roster = new[] { Archetype.ShotCreator, Archetype.TwoWaySpark, Archetype.PostAnchor },
        };

        /// <summary>Open on the last issue's last goal (you've won a City Championship first).</summary>
        public static bool VelvetOpen(CareerSaveData d)
        {
            var g = CurrentGoal(d?.world);
            return g != null && g.Kind == ComicGoalKind.Velvet && !d.world.velvetBeaten;
        }

        public static MatchRequest VelvetRequest(ContentCatalog c, CareerSaveData d)
        {
            var r = CityCrews.Challenge(c, d, VelvetSquad);
            r.ContextId = VelvetContext;
            r.CourtId = CityArena.CourtId;
            return r;
        }

        /// <summary>A finished game: the Velvet Invitational (won) ends the comic's last goal. True if it was that game.</summary>
        public static bool ApplyGame(CareerSaveData d, MatchRequest r, bool won)
        {
            if (d?.world == null || r == null || r.ContextId != VelvetContext) return false;
            if (won) d.world.velvetBeaten = true;
            return true;
        }
    }
}

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// 28 Oct 2026 (BALLER WORLD W1): who's who in the comic. Speakers are drawn with the game's own character sprites:
    /// YOU (your player), DEE (your agent), MR. VELVET, a teammate (MATE) and crew leaders ("crew:id").
    /// </summary>
    public static class ComicCast
    {
        public static string Name(string speaker)
        {
            if (string.IsNullOrEmpty(speaker)) return null;
            if (speaker.StartsWith("crew:", System.StringComparison.Ordinal))
            {
                var crew = CityCrews.Find(speaker.Substring(5));
                return crew == null ? "?" : crew.Leader + " (" + crew.Name + ")";
            }
            switch (speaker)
            {
                case "YOU": return "YOU";
                case "DEE": return "DEE";
                case "VELVET": return "MR. VELVET";
                default: return "YOUR CREW";
            }
        }

        private static PlayerDef Made(string id, string first, Archetype a) =>
            new PlayerDef { id = id, firstName = first, lastName = "", jerseyNumber = 0, appearance = DefaultContent.AppearanceFromSeed(id, a) };

        /// <summary>The speaker's player and kit (null if nobody can be drawn).</summary>
        public static PlayerDef Resolve(string speaker, CareerSaveData d, ContentCatalog c, out KitLook kit)
        {
            kit = KitLook.Classic(RgbColor.FromHex("#2E6FD8"), RgbColor.FromHex("#F4F1DE"), RgbColor.FromHex("#FFD166"), null, TeamPattern.Solid, null);
            if (string.IsNullOrEmpty(speaker) || c == null) return null;
            if (speaker.StartsWith("crew:", System.StringComparison.Ordinal))
            {
                var crew = CityCrews.Find(speaker.Substring(5));
                if (crew == null) return null;
                var team = CityCrews.Register(c, crew);
                kit = KitLook.Classic(team.primary, team.secondary, team.accent, null, TeamPattern.Solid, null);
                return team.rosterPlayerIds.Count > 0 ? c.Player(team.rosterPlayerIds[0]) : null;
            }
            var crewTeam = c.Team(DefaultContent.PlayerCrewId);
            if (crewTeam != null) kit = KitLook.Classic(crewTeam.primary, crewTeam.secondary, crewTeam.accent, null, TeamPattern.Solid, null);
            switch (speaker)
            {
                case "YOU": return d == null ? null : PlayerCreator.BasePlayer(d, c);
                case "DEE":
                    kit = KitLook.Classic(RgbColor.FromHex("#14141F"), RgbColor.FromHex("#4CC9F0"), RgbColor.FromHex("#F4F1DE"), null, TeamPattern.Solid, RgbColor.FromHex("#14141F"));
                    return Made("comic.dee", "DEE", Archetype.Playmaker);
                case "VELVET":
                    kit = KitLook.Classic(RgbColor.FromHex("#5A189A"), RgbColor.FromHex("#FFD166"), RgbColor.FromHex("#3C096C"), null, TeamPattern.Solid, RgbColor.FromHex("#3C096C"));
                    return Made("comic.velvet", "VELVET", Archetype.ShotCreator);
                default:
                {
                    if (d == null) return null;
                    foreach (var p in CrewEngine.TeammateDefs(d, c)) if (p != null) return p;
                    return null;
                }
            }
        }
    }
}
