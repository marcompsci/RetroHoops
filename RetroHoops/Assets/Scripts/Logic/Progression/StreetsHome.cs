using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// 15 Oct 2026 (Omari), THE STREETS phase 3: HOMECOURT.
    /// - Clear SOUTHSIDE on the STREET TOUR and you can claim a court: any street court, or one you built in
    ///   Locker Room ► COURT. Give it a name.
    /// - Then crews come to take it. Each DEFEND game is to 21 on your court against a crew from the cities you've
    ///   opened; every fifth defense is the toughest crew you've seen.
    /// - Defenses unlock perks (HOME CROWD, BLOCK PARTY, BIG SPEAKERS, LEGEND WALL) and pay REP and SP.
    /// - Lose and the court is TAKEN: the perks are off until you beat that crew in a TAKE IT BACK game.
    /// </summary>
    public static class StreetsHome
    {
        public const string Context = "streets:home";
        public const string UnlockHint = "Clear SOUTHSIDE on the STREET TOUR to claim a court.";

        public static readonly string[] Names =
        {
            "THE CAGE", "THE BLOCK", "THE SLAB", "THE PIT", "THE YARD", "THE LOT", "THE SPOT", "THE TRENCH", "THE FURNACE", "THE TEMPLE",
        };

        public sealed class Perk
        {
            public string Name, Line;
            public int Defenses;
        }

        public static readonly Perk[] Perks =
        {
            new Perk { Name = "HOME CROWD", Defenses = 1, Line = "Home games start with a quarter of your STREET METER." },
            new Perk { Name = "BLOCK PARTY", Defenses = 3, Line = "Your meter fills 10% faster at home." },
            new Perk { Name = "BIG SPEAKERS", Defenses = 6, Line = "Home games start with half a meter." },
            new Perk { Name = "LEGEND WALL", Defenses = 10, Line = "Double REP for every defense." },
        };

        public const int BossEvery = 5;

        public static bool Unlocked(StreetsSaveData d) => d != null && StreetsTour.CityOpen(d, 1);

        public static bool Claimed(StreetsSaveData d) => !string.IsNullOrEmpty(d?.homeCourt);

        public static string Name(StreetsSaveData d) => Names[Mod(d?.homeName ?? 0, Names.Length)];

        /// <summary>Courts you can claim: the street courts, then the courts you've built.</summary>
        public static List<CourtDef> Choices(ContentCatalog c, CareerSaveData career)
        {
            var list = Streets.CourtList(c);
            if (c == null || career?.courts == null) return list;
            for (int i = 0; i < CourtBuilder.Slots && i < career.courts.Count; i++)
            {
                if (career.courts[i] == null || !career.courts[i].built) continue;
                var court = c.Court(CourtBuilder.Id(i));
                if (court != null) list.Add(court);
            }
            return list;
        }

        /// <summary>Claims (or moves to) a court. False when it isn't one you can claim, or HOMECOURT is still locked.</summary>
        public static bool Claim(StreetsSaveData d, ContentCatalog c, CareerSaveData career, string courtId)
        {
            if (!Unlocked(d) || string.IsNullOrEmpty(courtId)) return false;
            if (!Choices(c, career).Exists(k => k.id == courtId)) return false;
            d.homeCourt = courtId;
            return true;
        }

        public static void NextName(StreetsSaveData d, int dir)
        {
            if (d != null) d.homeName = Mod(d.homeName + (dir < 0 ? -1 : 1), Names.Length);
        }

        public static bool PerkOn(StreetsSaveData d, int perk) =>
            d != null && perk >= 0 && perk < Perks.Length && d.homeDefenses >= Perks[perk].Defenses;

        /// <summary>Perks work while the court is yours (not while it's TAKEN).</summary>
        private static bool Active(StreetsSaveData d, int perk) => d != null && !d.homeTaken && PerkOn(d, perk);

        /// <summary>Your STREET METER at tip-off of a home game.</summary>
        public static int StartMeter(StreetsSaveData d) =>
            Active(d, 2) ? MatchSimulation.MeterLevel / 2 : Active(d, 0) ? MatchSimulation.MeterLevel / 4 : 0;

        /// <summary>Extra meter gain at home.</summary>
        public static float Boost(StreetsSaveData d) => Active(d, 1) ? 0.1f : 0f;

        /// <summary>The crews that can come for your court: those in the cities you've opened (THE CROWN once the tour's done).</summary>
        public static List<TourStop> Pool(StreetsSaveData d)
        {
            var list = new List<TourStop>();
            foreach (var s in StreetsTour.Stops)
            {
                if (s.Finale ? StreetsTour.Beaten(d, s) : StreetsTour.CityOpen(d, s.Tier)) list.Add(s);
            }
            return list;
        }

        /// <summary>True when the next defense is a boss defense (every fifth).</summary>
        public static bool BossNext(StreetsSaveData d) => d != null && !d.homeTaken && (d.homeDefenses + 1) % BossEvery == 0;

        /// <summary>Who's coming next: the crew that took it (TAKE IT BACK), a boss, or a crew from your cities.</summary>
        public static TourStop Challenger(StreetsSaveData d)
        {
            if (d == null) return StreetsTour.Stops[0];
            if (d.homeTaken)
            {
                var taker = StreetsTour.Find(d.homeTaker);
                if (taker != null) return taker;
            }
            var pool = Pool(d);
            if (pool.Count == 0) return StreetsTour.Stops[0];
            if (BossNext(d)) return pool[pool.Count - 1];
            uint h = StableHash.Of("home." + (d.homeDefenses + d.homeLosses));
            return pool[(int)(h % (uint)pool.Count)];
        }

        /// <summary>The next home game: your crew vs the challenger on your court, to 21, with your perks.</summary>
        public static MatchRequest Defend(ContentCatalog c, CareerSaveData career)
        {
            var d = career?.streets;
            if (!Claimed(d)) return null;
            var stop = Challenger(d);
            StreetsTour.Register(c, stop);
            return new MatchRequest
            {
                Mode = GameMode.Streets,
                HomeTeamId = Streets.MyTeamId(c),
                AwayTeamId = stop.TeamId,
                CourtId = c.Court(d.homeCourt) != null ? d.homeCourt : c.Team(stop.TeamId).homeCourtId,
                RulesId = Streets.RulesId,
                TeamSize = Streets.TeamSize,
                StreetRules = true,
                DifficultyId = career.settings?.difficultyId ?? DefaultContent.DefaultDifficultyId,
                ContextId = Context + ":" + stop.Id,
                StartWithBall = 0,
                StartMeterA = StartMeter(d),
                StyleBoostA = Boost(d),
            };
        }

        public static bool IsHome(MatchRequest r) => r != null && r.ContextId != null && r.ContextId.StartsWith(Context, StringComparison.Ordinal);

        /// <summary>The challenger's id from a home game's context.</summary>
        public static string ChallengerOf(string contextId) =>
            contextId != null && contextId.StartsWith(Context + ":", StringComparison.Ordinal) ? contextId.Substring(Context.Length + 1) : "";

        public sealed class HomeResult
        {
            public int Rep, Sp;
            public bool Defended, Taken, Retaken;
            public string NewPerk;
        }

        /// <summary>Records a home game.</summary>
        public static HomeResult Apply(StreetsSaveData d, CareerSaveData career, string challengerId, bool won)
        {
            var r = new HomeResult();
            if (!Claimed(d)) return r;
            if (won)
            {
                if (d.homeTaken)
                {
                    r.Retaken = true;
                    d.homeTaken = false;
                    d.homeTaker = "";
                    r.Rep = 60;
                }
                else
                {
                    r.Defended = true;
                    d.homeDefenses++;
                    d.homeStreak++;
                    d.homeBest = Math.Max(d.homeBest, d.homeStreak);
                    r.Rep = (40 + 5 * Math.Min(d.homeStreak, 10)) * (PerkOn(d, 3) ? 2 : 1);
                    foreach (var p in Perks) if (p.Defenses == d.homeDefenses) r.NewPerk = p.Name;
                }
                r.Sp = 25;
            }
            else
            {
                d.homeLosses++;
                d.homeStreak = 0;
                if (!d.homeTaken)
                {
                    r.Taken = true;
                    d.homeTaken = true;
                    d.homeTaker = StreetsTour.Find(challengerId) != null ? challengerId : Challenger(d).Id;
                }
            }
            d.rep += r.Rep;
            if (career != null) career.signalPoints += r.Sp;
            return r;
        }

        private static int Mod(int v, int n) => n <= 0 ? 0 : ((v % n) + n) % n;
    }

    /// <summary>
    /// THE STREETS phase 3: CREW CHEMISTRY. Every game a recruit plays in your STREETS CREW builds chemistry with
    /// him (a win counts double). Each level makes your crew a little sharper and your meter fill a little faster.
    /// </summary>
    public static class StreetsChem
    {
        /// <summary>Chemistry points needed for levels 0..5.</summary>
        public static readonly int[] LevelAt = { 0, 3, 8, 15, 25, 40 };
        public const int MaxLevel = 5;

        public static int Points(StreetsSaveData d, string playerId)
        {
            if (d?.chem == null || string.IsNullOrEmpty(playerId)) return 0;
            foreach (var line in d.chem)
            {
                int eq = line.LastIndexOf('=');
                if (eq > 0 && line.Substring(0, eq) == playerId && int.TryParse(line.Substring(eq + 1), out int n)) return Math.Max(0, n);
            }
            return 0;
        }

        public static int LevelFor(int points)
        {
            int level = 0;
            for (int i = 0; i < LevelAt.Length; i++) if (points >= LevelAt[i]) level = i;
            return level;
        }

        public static int Level(StreetsSaveData d, string playerId) => LevelFor(Points(d, playerId));

        /// <summary>Points to the next level (0 at the top).</summary>
        public static int ToNext(StreetsSaveData d, string playerId)
        {
            int p = Points(d, playerId), l = LevelFor(p);
            return l >= MaxLevel ? 0 : LevelAt[l + 1] - p;
        }

        /// <summary>Your crew's chemistry: the levels of the recruits in it added up (0..10).</summary>
        public static int CrewLevel(StreetsSaveData d)
        {
            if (d?.crew == null) return 0;
            int n = 0;
            for (int i = 0; i < 2 && i < d.crew.Count; i++)
                if (!string.IsNullOrEmpty(d.crew[i]) && d.recruits != null && d.recruits.Contains(d.crew[i])) n += Level(d, d.crew[i]);
            return n;
        }

        /// <summary>Teammate shooting bonus (the same scale as Rise chemistry: up to +0.1).</summary>
        public static float Bonus(StreetsSaveData d) => CrewLevel(d) * 0.01f;

        /// <summary>Extra STREET METER gain (up to +10%).</summary>
        public static float Boost(StreetsSaveData d) => CrewLevel(d) * 0.01f;

        /// <summary>Adds a game for the recruits in your crew. Returns the ones who went up a level.</summary>
        public static List<string> AddGame(StreetsSaveData d, bool won)
        {
            var up = new List<string>();
            if (d?.crew == null) return up;
            if (d.chem == null) d.chem = new List<string>();
            for (int i = 0; i < 2 && i < d.crew.Count; i++)
            {
                string id = d.crew[i];
                if (string.IsNullOrEmpty(id) || d.recruits == null || !d.recruits.Contains(id)) continue;
                int before = Points(d, id), after = before + (won ? 2 : 1);
                d.chem.RemoveAll(l => l.StartsWith(id + "=", StringComparison.Ordinal));
                d.chem.Add(id + "=" + after);
                if (LevelFor(after) > LevelFor(before)) up.Add(id);
            }
            return up;
        }
    }

    /// <summary>A STREET GEAR set: a kit your crew wears in THE STREETS.</summary>
    public sealed class GearSet
    {
        public string Id, Name, Hint;
        public RgbColor Jersey, Trim, Shorts, Shoe;
        public TeamPattern Pattern;
        public Func<StreetsSaveData, bool> Earned;
    }

    /// <summary>
    /// THE STREETS phase 3: STREET GEAR. Kits for your crew in THE STREETS only: BLACKTOP from the start, each tour
    /// crew's colours when you beat them, and gear for your REP and for taking THE CROWN.
    /// </summary>
    public static class StreetsGear
    {
        public static readonly List<GearSet> Sets = Build();

        private static List<GearSet> Build()
        {
            var black = RgbColor.FromHex("#1A1A1F");
            var list = new List<GearSet>
            {
                new GearSet { Id = "blacktop", Name = "BLACKTOP", Hint = "", Jersey = black, Trim = RgbColor.FromHex("#F77F00"), Shorts = black,
                              Shoe = RgbColor.FromHex("#F77F00"), Pattern = TeamPattern.Solid, Earned = d => true },
            };
            foreach (var s in StreetsTour.Stops)
            {
                var stop = s;
                StreetsTour.Colors(stop, out var primary, out var secondary, out var pattern);
                list.Add(new GearSet
                {
                    Id = "crew." + stop.Id, Name = stop.Crew, Hint = "Beat " + stop.Crew + " on the STREET TOUR.",
                    Jersey = primary, Trim = secondary, Shorts = primary.Darken(0.15f), Shoe = secondary, Pattern = pattern,
                    Earned = d => StreetsTour.Beaten(d, stop),
                });
            }
            list.Add(new GearSet
            {
                Id = "legend", Name = "STREET LEGEND", Hint = "Reach STREET LEGEND rep.", Jersey = RgbColor.FromHex("#FFD166"), Trim = black,
                Shorts = black, Shoe = RgbColor.FromHex("#FFD166"), Pattern = TeamPattern.Diagonal,
                Earned = d => d != null && StreetsTour.RepLevel(d.rep) >= 3,
            });
            list.Add(new GearSet
            {
                Id = "homecourt", Name = "HOME GUARD", Hint = "Defend your HOMECOURT 5 times.", Jersey = RgbColor.FromHex("#06D6A0"), Trim = black,
                Shorts = black, Shoe = RgbColor.FromHex("#F2F2F2"), Pattern = TeamPattern.Stripes,
                Earned = d => d != null && d.homeDefenses >= 5,
            });
            return list;
        }

        public static GearSet Find(string id) => string.IsNullOrEmpty(id) ? null : Sets.Find(s => s.Id == id);

        public static bool Unlocked(StreetsSaveData d, GearSet s) => s != null && s.Earned(d);

        public static int UnlockedCount(StreetsSaveData d)
        {
            int n = 0;
            foreach (var s in Sets) if (Unlocked(d, s)) n++;
            return n;
        }

        /// <summary>The worn set (null = your usual kit). A set you no longer have counts as none.</summary>
        public static GearSet Worn(StreetsSaveData d)
        {
            var s = Find(d?.gear);
            return Unlocked(d, s) ? s : null;
        }

        /// <summary>Steps through "" (your usual kit) and the sets you've unlocked.</summary>
        public static void Next(StreetsSaveData d, int dir)
        {
            if (d == null) return;
            var options = new List<string> { "" };
            foreach (var s in Sets) if (Unlocked(d, s)) options.Add(s.Id);
            int at = Math.Max(0, options.IndexOf(Worn(d)?.Id ?? ""));
            int n = options.Count;
            d.gear = options[(((at + (dir < 0 ? -1 : 1)) % n) + n) % n];
        }

        /// <summary>The kit for a set (a tee, in its colours and pattern).</summary>
        public static KitData Kit(GearSet s)
        {
            if (s == null) return null;
            var k = Kits.FromColors(s.Name, s.Jersey, s.Trim, RgbColor.FromHex("#1A1A1F"), s.Shorts, s.Shoe);
            k.cut = (int)JerseyCut.Tee;
            k.pattern = (int)s.Pattern;
            Kits.Clamp(k);
            return k;
        }
    }

    /// <summary>
    /// THE STREETS phase 3: SIGNATURE SHOWSTOPPER. The dunk you throw down when your SHOWSTOPPER is on (the camera
    /// comes in for it). More open up as your REP grows.
    /// </summary>
    public static class StreetsSignature
    {
        public static readonly DunkStyle[] Order =
        {
            DunkStyle.TwoHand, DunkStyle.Tomahawk, DunkStyle.Cradle, DunkStyle.Windmill, DunkStyle.Reverse, DunkStyle.ThreeSixty, DunkStyle.Skyline,
        };

        /// <summary>REP level needed for each entry of <see cref="Order"/>.</summary>
        public static readonly int[] RepLevel = { 0, 0, 1, 1, 2, 3, 4 };

        public static bool Unlocked(StreetsSaveData d, DunkStyle s)
        {
            int i = Array.IndexOf(Order, s);
            return i >= 0 && StreetsTour.RepLevel(d?.rep ?? 0) >= RepLevel[i];
        }

        /// <summary>The chosen signature, or null for your usual dunk package.</summary>
        public static DunkStyle? Chosen(StreetsSaveData d)
        {
            if (d == null || d.signature < 0 || d.signature >= Dunks.Names.Length) return null;
            var s = (DunkStyle)d.signature;
            return Unlocked(d, s) ? s : (DunkStyle?)null;
        }

        /// <summary>Steps through "your dunk package" and the unlocked signatures.</summary>
        public static void Next(StreetsSaveData d, int dir)
        {
            if (d == null) return;
            var options = new List<int> { -1 };
            foreach (var s in Order) if (Unlocked(d, s)) options.Add((int)s);
            var chosen = Chosen(d);
            int at = Math.Max(0, options.IndexOf(chosen.HasValue ? (int)chosen.Value : -1));
            int n = options.Count;
            d.signature = options[(((at + (dir < 0 ? -1 : 1)) % n) + n) % n];
        }

        public static string Label(StreetsSaveData d)
        {
            var s = Chosen(d);
            return s.HasValue ? Dunks.Name(s.Value).TrimEnd('!') : "YOUR DUNK PACKAGE";
        }

        public static int UnlockedCount(StreetsSaveData d)
        {
            int n = 0;
            foreach (var s in Order) if (Unlocked(d, s)) n++;
            return n;
        }
    }
}
