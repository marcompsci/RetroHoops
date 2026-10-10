using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>Where a Franchise year is: the season, the playoffs, then the off-season steps in order.</summary>
    public enum FranchisePhase { Regular = 0, Playoffs = 1, Draft = 2, ReSign = 3, FreeAgency = 4, Preseason = 5 }

    /// <summary>One player in a Franchise league (independent of every other mode's players).</summary>
    [Serializable]
    public class FrPlayer
    {
        public int id;
        public string first = "";
        public string last = "";
        public int number;
        public int archetype;
        public int skin, hair, hairColor, body = 1, height = 1;
        public AttributeSet attrs;
        public int age = 24;
        /// <summary>Overall this player can grow to.</summary>
        public int potential = 70;
        /// <summary>Salary per season in tenths of a million ($M × 10).</summary>
        public int salary = Franchise.MinSalary;
        /// <summary>Seasons left on the contract (0 = expiring this off-season).</summary>
        public int years = 1;
        /// <summary>Team index, or −1 for a free agent / undrafted prospect.</summary>
        public int team = -1;
        /// <summary>In this year's draft class (not yet drafted).</summary>
        public bool prospect;
        /// <summary>Scouting level on a prospect, 0..3 (3 = fully known).</summary>
        public int scout;
        public int seasons;
        public int titles;
        public int peak;
        /// <summary>This season's games and points (from played and simulated games).</summary>
        public int gp, pts;
        public int careerPts;
        public int draftYear;
        public int draftPick;
        /// <summary>Made in CREATE A PLAYER (yours: AI GMs never ask for him in trades).</summary>
        public bool created;

        public int Overall => attrs.Overall;
        public string Name => string.IsNullOrEmpty(last) ? first : first + " " + last;
        public string Short => string.IsNullOrEmpty(first) ? last : first.Substring(0, 1) + ". " + last;
        public float Ppg => gp == 0 ? 0f : pts / (float)gp;
    }

    /// <summary>A Franchise team: a Caller League club whose roster you or the AI manages.</summary>
    [Serializable]
    public class FrTeam
    {
        /// <summary>The league team whose name, colours, logo and court this club uses.</summary>
        public string baseId;
        /// <summary>Player ids; the order is the depth chart (first five start, the first is the one you control).</summary>
        public List<int> roster = new List<int>();
        /// <summary>Released players' salary still counted this season.</summary>
        public int deadMoney;
    }

    /// <summary>One team's season, for the history book.</summary>
    [Serializable]
    public class FrHistory
    {
        public int year;
        public int team;
        public int wins, losses;
        /// <summary>0 missed the playoffs, 1 lost a semifinal, 2 lost the final, 3 champions.</summary>
        public int finish;
        public int payroll;
        public string best = "";
    }

    [Serializable]
    public class FrAward
    {
        public int year;
        /// <summary>"MVP", "ROOKIE", "HALL".</summary>
        public string kind = "";
        public string name = "";
        public int team = -1;
        public string line = "";
    }

    /// <summary>A trade, for the transactions log.</summary>
    [Serializable]
    public class FrMove
    {
        public int year;
        public string text = "";
    }

    [Serializable]
    public class FranchiseSaveData
    {
        public bool active;
        public int year = 1;
        public uint seed = 1;
        /// <summary>Your team's index in <see cref="teams"/>.</summary>
        public int you;
        public FranchisePhase phase;
        public int nextId = 1;
        public List<FrTeam> teams = new List<FrTeam>();
        public List<FrPlayer> players = new List<FrPlayer>();
        /// <summary>This year's schedule and playoffs (team ids are <see cref="Franchise.TeamId"/>).</summary>
        public SeasonSaveData season = new SeasonSaveData();
        /// <summary>Draft order as team indexes (pick 1 first) and how many picks are made.</summary>
        public List<int> draftOrder = new List<int>();
        public int draftMade;
        /// <summary>What the lottery did, one line per team that moved, for the draft screen.</summary>
        public List<string> lottery = new List<string>();
        public int scoutPoints;
        public List<FrHistory> history = new List<FrHistory>();
        public List<FrAward> awards = new List<FrAward>();
        public List<FrMove> moves = new List<FrMove>();
        public int titles;
        /// <summary>Phase 33: a trade another GM has offered you (null = none), and the week last checked for one.</summary>
        public FrOffer offer;
        public int offerWeek = -1;
        /// <summary>Phase 33: what your team trains between seasons (<see cref="TrainingFocus"/>).</summary>
        public int focus;
        /// <summary>Phase 33: how your players changed last off-season, one line each.</summary>
        public List<string> devReport = new List<string>();
        /// <summary>Phase 35: your team's shots this season (games you played or coached), by spot.</summary>
        public ShotChartData teamChart = new ShotChartData();
        /// <summary>Phase 35: COACH MODE (you call plays and subs while your players play), remembered between games.</summary>
        public bool coach;
    }

    public sealed class TradeVerdict
    {
        public bool Accepted;
        public string Reason = "";
        /// <summary>What the other GM thinks each side is worth.</summary>
        public float TheyGive, TheyGet;
    }

    /// <summary>
    /// Franchise: run one Caller League club for as many seasons as you like. Seven-man rosters under a
    /// salary cap, a 14-game season of Full Court games (play them or simulate them), four-team playoffs,
    /// then the off-season: aging and retirements, a draft lottery and a scouted draft, re-signing your
    /// expiring players, free agency and preseason cuts. Trade with the other seven GMs, who judge deals
    /// on ratings, age, potential and contracts. Everything is seeded, so the league is the same on reload.
    /// </summary>
    public static partial class Franchise
    {
        public const int Teams = 8;
        public const int MinRoster = 7;
        public const int MaxRoster = 9;
        public const int Weeks = 14;
        public const int TradeDeadlineWeek = 10;
        /// <summary>Salary cap, tenths of a million: $85.0M.</summary>
        public const int Cap = 850;
        public const int MinSalary = 15;
        public const int MaxSalary = 300;
        public const int ProspectsPerClass = 12;
        public const int ScoutPointsPerDraft = 6;
        public const int MaxTradePlayers = 3;
        public const int RetireAge = 36;
        public const int HallPeak = 80;
        public const int HallSeasons = 5;
        public const string TeamPrefix = "team.fr.";
        public const string PlayerPrefix = "player.fr.";
        public static readonly int[] RookieScale = { 50, 44, 38, 33, 29, 26, 23, 20 };
        private static readonly int[] LotteryWeights = { 40, 30, 20, 10 };
        private static readonly int[] ScoutRange = { 12, 7, 3, 0 };

        private static readonly string[] Firsts =
        {
            "Abe", "Bryn", "Cass", "Dorian", "Ezra", "Flynn", "Gabe", "Hollis", "Idris", "Jonah", "Kenji", "Lars", "Milo", "Nate",
            "Otis", "Pierce", "Quade", "Reggie", "Silas", "Theo", "Ulric", "Vance", "Wade", "Xavi", "Yusuf", "Zane", "Ansel", "Benji",
            "Cyrus", "Dario", "Emil", "Ferris", "Grady", "Hugo", "Ivo", "Jory", "Kofi", "Lionel", "Moss", "Niko", "Orrin", "Pell",
        };
        private static readonly string[] Lasts =
        {
            "Abara", "Bellweather", "Corrigan", "Dunmore", "Ellery", "Fairbanks", "Galloway", "Hightower", "Ingram", "Jessup",
            "Kettering", "Lockridge", "Marchetti", "Northcott", "Okafor", "Pemberton", "Quayle", "Rourke", "Stanhope", "Thorne",
            "Underhill", "Valdez", "Whitlock", "Yarrow", "Zellner", "Ashgrove", "Blackwood", "Calloway", "Dressler", "Everly",
            "Fenwick", "Greaves", "Halvorsen", "Ivey", "Juneau", "Kimura", "Larkspur", "Mbeki", "Nakamura", "Osei", "Prescott",
        };

        public static string TeamId(int index) => TeamPrefix + index;
        public static string PlayerId(int id) => PlayerPrefix + id;

        public static int IndexOf(string teamId)
        {
            if (teamId == null || !teamId.StartsWith(TeamPrefix, StringComparison.Ordinal)) return -1;
            return int.TryParse(teamId.Substring(TeamPrefix.Length), out int i) ? i : -1;
        }

        public static FrPlayer Player(FranchiseSaveData f, int id) => f.players.Find(p => p.id == id);

        public static List<FrPlayer> Roster(FranchiseSaveData f, int team)
        {
            var list = new List<FrPlayer>();
            if (team < 0 || team >= f.teams.Count) return list;
            foreach (int id in f.teams[team].roster)
            {
                var p = Player(f, id);
                if (p != null) list.Add(p);
            }
            return list;
        }

        public static int Payroll(FranchiseSaveData f, int team)
        {
            int sum = f.teams[team].deadMoney;
            foreach (var p in Roster(f, team)) sum += p.salary;
            return sum;
        }

        public static int CapRoom(FranchiseSaveData f, int team) => Cap - Payroll(f, team);

        /// <summary>"$12.5M".</summary>
        public static string Money(int tenths) => (tenths < 0 ? "-$" : "$") + (Math.Abs(tenths) / 10) + "." + (Math.Abs(tenths) % 10) + "M";

        /// <summary>What a player of this rating earns on the open market.</summary>
        public static int MarketSalary(int overall)
        {
            int d = Math.Max(0, overall - 48);
            return Math.Max(MinSalary, Math.Min(MaxSalary, MinSalary + (int)Math.Round(d * d * 0.3)));
        }

        /// <summary>The salary a player asks for: market rate, more if young and rising, less when old.</summary>
        public static int Demand(FrPlayer p)
        {
            double s = MarketSalary(p.Overall);
            if (p.age <= 25 && p.potential - p.Overall >= 8) s *= 1.12;
            if (p.age >= 32) s *= 0.8;
            return Math.Max(MinSalary, Math.Min(MaxSalary, (int)Math.Round(s)));
        }

        /// <summary>Contract length a player signs for.</summary>
        public static int DemandYears(FrPlayer p) => p.age <= 27 ? 3 : (p.age <= 31 ? 2 : 1);

        /// <summary>
        /// How much a GM values a player: rating counts most (stars far more than role players), young
        /// players with room to grow get a bonus, veterans past 29 lose value, and a cheap contract helps.
        /// </summary>
        public static float Value(FrPlayer p)
        {
            float eff = p.Overall;
            if (p.age <= 25) eff += Math.Max(0, p.potential - p.Overall) * 0.5f;
            if (p.age >= 30) eff -= (p.age - 29) * 1.5f;
            float surplus = (MarketSalary(p.Overall) - p.salary) / 25f;
            eff += Math.Max(-4f, Math.Min(4f, surplus)) * Math.Min(Math.Max(p.years, 1), 3) / 3f;
            float d = Math.Max(0f, eff - 45f);
            return d * d / 40f;
        }

        // ------------------------------------------------------------------ new league

        /// <summary>Starts a fresh Franchise running <paramref name="baseTeamId"/>: the eight Caller League clubs with
        /// their real-to-the-game players plus generated depth, contracts, and a new schedule.</summary>
        public static FranchiseSaveData Create(ContentCatalog c, string baseTeamId, uint seed)
        {
            var f = new FranchiseSaveData { active = true, seed = seed == 0 ? 1u : seed, year = 1, phase = FranchisePhase.Regular };
            var rng = new SeededRandom(f.seed);
            var league = c.TeamsInTier(TeamTier.League);
            league.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            for (int i = 0; i < Math.Min(Teams, league.Count); i++)
            {
                var t = league[i];
                f.teams.Add(new FrTeam { baseId = t.id });
                if (t.id == baseTeamId) f.you = i;
                int team = f.teams.Count - 1;
                foreach (var id in t.rosterPlayerIds)
                {
                    var def = c.Player(id);
                    if (def == null) continue;
                    var arch = c.ArchetypeById(def.archetypeId);
                    var p = new FrPlayer
                    {
                        id = f.nextId++, first = def.firstName, last = def.lastName, number = def.jerseyNumber,
                        archetype = arch != null ? (int)arch.archetype : 0,
                        skin = def.appearance.skinTone, hair = def.appearance.hairStyle, hairColor = def.appearance.hairColor,
                        body = (int)def.appearance.body, height = def.appearance.heightTier,
                        attrs = def.attributes, age = 22 + rng.Range(0, 10), team = team,
                    };
                    p.potential = Math.Min(95, p.Overall + (p.age <= 25 ? 4 + rng.Range(0, 8) : rng.Range(0, 3)));
                    Sign(p, rng.Range(1, 5));
                    f.players.Add(p);
                    f.teams[team].roster.Add(p.id);
                }
                while (f.teams[team].roster.Count < MinRoster + 1)
                {
                    var p = Generate(c, f, rng, 52 + rng.Range(0, 12), 21 + rng.Range(0, 12));
                    p.team = team;
                    Sign(p, rng.Range(1, 4));
                    f.teams[team].roster.Add(p.id);
                }
                SortRoster(f, team);
            }
            // A few free agents so there's somebody to sign from day one.
            for (int i = 0; i < 6; i++)
            {
                var p = Generate(c, f, rng, 50 + rng.Range(0, 10), 24 + rng.Range(0, 10));
                p.salary = Demand(p);
                p.years = 0;
            }
            foreach (var t in f.teams) TrimToCap(f, f.teams.IndexOf(t));
            NewSchedule(f);
            return f;
        }

        private static void Sign(FrPlayer p, int years)
        {
            p.salary = MarketSalary(p.Overall);
            p.years = years;
        }

        /// <summary>Makes an original player at about <paramref name="overall"/>.</summary>
        private static FrPlayer Generate(ContentCatalog c, FranchiseSaveData f, SeededRandom rng, int overall, int age)
        {
            int a = rng.Range(0, 12);
            var arch = c.Archetypes.Find(x => (int)x.archetype == a) ?? c.Archetypes[0];
            var p = new FrPlayer
            {
                id = f.nextId++, first = Firsts[rng.Range(0, Firsts.Length)], last = Lasts[rng.Range(0, Lasts.Length)],
                number = rng.Range(0, 100), archetype = (int)arch.archetype, age = age,
                skin = rng.Range(0, 6), hair = rng.Range(0, 6), hairColor = rng.Range(0, 5), body = rng.Range(0, 3), height = rng.Range(0, 3),
            };
            var attrs = arch.baseline;
            for (int i = 0; i < RatingScale.AttributeCount; i++)
            {
                var t = (AttributeType)i;
                attrs = attrs.With(t, RatingScale.Clamp(attrs.Get(t) + rng.Range(-3, 4)));
            }
            p.attrs = attrs.Offset(overall - attrs.Overall);
            p.potential = Math.Min(95, p.Overall + (age <= 24 ? 3 + rng.Range(0, 12) : (age <= 27 ? rng.Range(0, 5) : 0)));
            p.peak = p.Overall;
            f.players.Add(p);
            return p;
        }

        /// <summary>AI rosters: best players first. Your depth chart is yours to set.</summary>
        public static void SortRoster(FranchiseSaveData f, int team)
        {
            var list = Roster(f, team);
            list.Sort((a, b) => b.Overall != a.Overall ? b.Overall.CompareTo(a.Overall) : a.id.CompareTo(b.id));
            f.teams[team].roster = list.ConvertAll(p => p.id);
        }

        /// <summary>Moves a player one spot up (−1) or down (+1) your depth chart.</summary>
        public static bool MoveInRotation(FranchiseSaveData f, int playerId, int by)
        {
            var r = f.teams[f.you].roster;
            int i = r.IndexOf(playerId), j = i + by;
            if (i < 0 || j < 0 || j >= r.Count) return false;
            (r[i], r[j]) = (r[j], r[i]);
            return true;
        }

        private static void TrimToCap(FranchiseSaveData f, int team)
        {
            // Generated depth is trimmed to minimum deals if a starting roster would be over the cap.
            var roster = Roster(f, team);
            roster.Sort((a, b) => a.Overall.CompareTo(b.Overall));
            foreach (var p in roster)
            {
                if (Payroll(f, team) <= Cap) return;
                p.salary = Math.Max(MinSalary, p.salary / 2);
            }
        }

        // ------------------------------------------------------------------ season

        private static void NewSchedule(FranchiseSaveData f)
        {
            var s = new SeasonSaveData { seed = StableHash.Of("fr:" + f.seed + ":" + f.year), seasonNumber = f.year, weeks = Weeks };
            for (int i = 0; i < f.teams.Count; i++) s.teamIds.Add(TeamId(i));
            // Circle method, twice (home and away swapped the second time round).
            int n = f.teams.Count;
            var rot = new List<int>();
            for (int i = 0; i < n; i++) rot.Add(i);
            for (int week = 0; week < Weeks; week++)
            {
                int r = week % (n - 1);
                var order = new List<int> { rot[0] };
                for (int k = 0; k < n - 1; k++) order.Add(rot[1 + (k + r) % (n - 1)]);
                for (int k = 0; k < n / 2; k++)
                {
                    int a = order[k], b = order[n - 1 - k];
                    bool flip = (week >= n - 1) ^ ((k + week) % 2 == 1);
                    s.games.Add(new ScheduledGame { week = week, homeId = TeamId(flip ? b : a), awayId = TeamId(flip ? a : b) });
                }
            }
            f.season = s;
            f.teamChart = new ShotChartData();
            foreach (var p in f.players) { p.gp = 0; p.pts = 0; }
        }

        /// <summary>Your next game (regular season or playoffs), or null when you have none to play.</summary>
        public static ScheduledGame NextGame(FranchiseSaveData f)
        {
            if (f == null || !f.active || (f.phase != FranchisePhase.Regular && f.phase != FranchisePhase.Playoffs)) return null;
            return SeasonEngine.NextGameFor(f.season, TeamId(f.you));
        }

        public static int CurrentWeek(FranchiseSaveData f) => f.season.currentWeek;

        public static bool TradesOpen(FranchiseSaveData f) =>
            f.phase == FranchisePhase.Preseason || f.phase == FranchisePhase.Draft || f.phase == FranchisePhase.ReSign || f.phase == FranchisePhase.FreeAgency
            || (f.phase == FranchisePhase.Regular && f.season.currentWeek < TradeDeadlineWeek);

        /// <summary>Team strength for simulated games: the five starters count most, the bench a little.</summary>
        public static float Strength(FranchiseSaveData f, int team)
        {
            var r = Roster(f, team);
            var ovr = r.ConvertAll(p => p.Overall);
            ovr.Sort((a, b) => b.CompareTo(a));
            float sum = 0f, w = 0f;
            for (int i = 0; i < ovr.Count && i < 7; i++)
            {
                float weight = i < 5 ? 1f : 0.35f;
                sum += ovr[i] * weight;
                w += weight;
            }
            return w == 0f ? 40f : sum / w;
        }

        private static void Simulate(FranchiseSaveData f, ScheduledGame g, SeededRandom rng)
        {
            int h = IndexOf(g.homeId), a = IndexOf(g.awayId);
            float diff = Strength(f, h) + 1.5f - Strength(f, a);
            float p = 1f / (1f + (float)Math.Exp(-diff / 4f));
            bool homeWins = rng.NextFloat() < p;
            int win = 24 + rng.Range(0, 14);
            int lose = Math.Max(10, win - 2 - rng.Range(0, 11));
            g.homeScore = homeWins ? win : lose;
            g.awayScore = homeWins ? lose : win;
            g.played = true;
            Credit(f, h, g.homeScore, rng);
            Credit(f, a, g.awayScore, rng);
        }

        /// <summary>Shares a team's points among its rotation (better players score more).</summary>
        private static void Credit(FranchiseSaveData f, int team, int points, SeededRandom rng)
        {
            var r = Roster(f, team);
            if (r.Count == 0) return;
            int n = Math.Min(7, r.Count);
            var weights = new float[n];
            float total = 0f;
            for (int i = 0; i < n; i++)
            {
                float minutes = i < 5 ? 1f : 0.4f;
                weights[i] = minutes * (float)Math.Pow(Math.Max(1, r[i].Overall - 35), 1.6) * (0.75f + rng.NextFloat() * 0.5f);
                total += weights[i];
            }
            int given = 0;
            for (int i = 0; i < n; i++)
            {
                int share = i == n - 1 ? points - given : (int)Math.Round(points * weights[i] / total);
                share = Math.Max(0, Math.Min(points - given, share));
                given += share;
                r[i].gp++;
                r[i].pts += share;
                r[i].careerPts += share;
            }
        }

        /// <summary>
        /// Records the game you just played (your score first) and simulates the rest of that week or round.
        /// <paramref name="teamShots"/> (Phase 35) is your team's shot chart from that game.
        /// </summary>
        public static bool RecordYourGame(FranchiseSaveData f, ContentCatalog c, int yourScore, int theirScore, ShotChartData teamShots = null)
        {
            var g = NextGame(f);
            if (g == null) return false;
            if (teamShots != null) ShotZones.Merge(f.teamChart ?? (f.teamChart = new ShotChartData()), teamShots);
            if (yourScore == theirScore) yourScore++;
            bool home = g.homeId == TeamId(f.you);
            g.homeScore = home ? yourScore : theirScore;
            g.awayScore = home ? theirScore : yourScore;
            g.played = true;
            var rng = new SeededRandom(StableHash.Of(f.season.seed + ":you:" + g.week + ":" + g.round));
            Credit(f, f.you, yourScore, rng);
            Credit(f, IndexOf(home ? g.awayId : g.homeId), theirScore, rng);
            FinishSlate(f, c, g.week, g.round);
            return true;
        }

        /// <summary>SIM: your next game (or, with no game of yours left, the next round) is simulated.</summary>
        public static void SimNext(FranchiseSaveData f, ContentCatalog c)
        {
            var g = NextGame(f);
            if (g != null)
            {
                Simulate(f, g, new SeededRandom(StableHash.Of(f.season.seed + ":sim:" + g.week + ":" + g.round)));
                FinishSlate(f, c, g.week, g.round);
                return;
            }
            if (f.phase == FranchisePhase.Playoffs)
            {
                foreach (var other in f.season.games)
                    if (!other.played)
                    {
                        FinishSlate(f, c, other.week, other.round);
                        return;
                    }
            }
        }

        /// <summary>Simulates to the end of the regular season (or playoffs, if that's where you are).</summary>
        public static void SimToEnd(FranchiseSaveData f, ContentCatalog c)
        {
            var phase = f.phase;
            for (int guard = 0; guard < 64 && f.phase == phase && f.active; guard++) SimNext(f, c);
        }

        private static void FinishSlate(FranchiseSaveData f, ContentCatalog c, int week, int round)
        {
            var rng = new SeededRandom(StableHash.Of(f.season.seed + ":slate:" + week + ":" + round));
            foreach (var g in f.season.games)
                if (!g.played && g.week == week && g.round == round) Simulate(f, g, rng);
            if (round == 0) f.season.currentWeek = Math.Max(f.season.currentWeek, week + 1);
            if (round == 0) FranchiseDepth.AfterWeek(f, c);

            if (f.phase == FranchisePhase.Regular && SeasonEngine.RegularSeasonComplete(f.season))
            {
                SeasonEngine.CreatePlayoffs(f.season);
                f.phase = FranchisePhase.Playoffs;
            }
            else if (f.phase == FranchisePhase.Playoffs)
            {
                if (SeasonEngine.RoundComplete(f.season, 1) && !SeasonEngine.HasRound(f.season, 2)) SeasonEngine.CreateFinal(f.season);
                var final = SeasonEngine.FinalGame(f.season);
                if (final != null && final.played) EndSeason(f, c);
            }
        }

        public static string ChampionId(FranchiseSaveData f) => SeasonEngine.FinalGame(f.season)?.WinnerId;

        // ------------------------------------------------------------------ end of season

        /// <summary>Last season's report lines (awards, retirements), for the off-season screen.</summary>
        public static List<string> LastReport(FranchiseSaveData f)
        {
            var lines = new List<string>();
            foreach (var a in f.awards) if (a.year == f.year - 1) lines.Add(a.kind + ": " + a.name + (a.line.Length > 0 ? " (" + a.line + ")" : ""));
            foreach (var m in f.moves) if (m.year == f.year - 1 && m.text.StartsWith("RETIRED", StringComparison.Ordinal)) lines.Add(m.text);
            return lines;
        }

        private static void EndSeason(FranchiseSaveData f, ContentCatalog c)
        {
            var table = SeasonEngine.Standings(f.season);
            string champ = ChampionId(f);
            var final = SeasonEngine.FinalGame(f.season);
            for (int t = 0; t < f.teams.Count; t++)
            {
                string id = TeamId(t);
                var rec = table.Find(r => r.TeamId == id);
                int finish = 0;
                if (id == champ) finish = 3;
                else if (final != null && final.Involves(id)) finish = 2;
                else if (f.season.games.Exists(g => g.round == 1 && g.Involves(id))) finish = 1;
                var roster = Roster(f, t);
                FrPlayer best = null;
                foreach (var p in roster) if (best == null || p.pts > best.pts) best = p;
                f.history.Add(new FrHistory
                {
                    year = f.year, team = t, wins = rec?.Wins ?? 0, losses = rec?.Losses ?? 0, finish = finish, payroll = Payroll(f, t),
                    best = best != null ? best.Name + " " + best.Ppg.ToString("0.0") + " PPG" : "",
                });
                if (finish == 3)
                {
                    foreach (var p in roster) p.titles++;
                    if (t == f.you) f.titles++;
                }
            }

            // MVP: scoring plus rating plus winning. Rookie of the year: best first-year scorer.
            FrPlayer mvp = null, roy = null;
            float mvpScore = float.MinValue, royScore = float.MinValue;
            foreach (var p in f.players)
            {
                if (p.team < 0 || p.gp == 0) continue;
                var rec = table.Find(r => r.TeamId == TeamId(p.team));
                float score = p.Ppg * 2f + p.Overall * 0.5f + (rec?.WinPct ?? 0f) * 20f;
                if (score > mvpScore) { mvpScore = score; mvp = p; }
                if (p.draftYear == f.year && score > royScore) { royScore = score; roy = p; }
            }
            if (mvp != null) f.awards.Add(new FrAward { year = f.year, kind = "MVP", name = mvp.Name, team = mvp.team, line = mvp.Ppg.ToString("0.0") + " PPG" });
            if (roy != null) f.awards.Add(new FrAward { year = f.year, kind = "ROOKIE", name = roy.Name, team = roy.team, line = roy.Ppg.ToString("0.0") + " PPG" });

            Age(f);
            NewDraftClass(f, c);
            f.year++;
            f.phase = FranchisePhase.Draft;
        }

        /// <summary>Everyone gets a year older: young players grow toward their potential, veterans slow down,
        /// the oldest retire (the best into the Hall of Fame), and contracts tick down.</summary>
        private static void Age(FranchiseSaveData f)
        {
            var rng = new SeededRandom(StableHash.Of("fr:age:" + f.seed + ":" + f.year));
            var retired = new List<FrPlayer>();
            f.devReport = new List<string>();
            f.offer = null;
            foreach (var p in f.players)
            {
                if (p.prospect) continue;
                p.age++;
                if (p.gp > 0) p.seasons++;
                if (p.team >= 0) p.years = Math.Max(0, p.years - 1);
                int before = p.Overall;
                int change = Progression(p, rng);
                if (change != 0) p.attrs = p.attrs.Offset(change);
                if (p.team == f.you)
                {
                    // Phase 33: your training focus adds to the young players' growth.
                    p.attrs = FranchiseDepth.ApplyFocus(p, (TrainingFocus)f.focus);
                    int diff = p.Overall - before;
                    f.devReport.Add(p.Name + " (" + p.age + ")  " + before + " → " + p.Overall + (diff > 0 ? "  +" + diff : diff < 0 ? "  " + diff : "  ="));
                }
                p.peak = Math.Max(p.peak, p.Overall);
                bool retire = p.age >= RetireAge || (p.age >= 33 && rng.Chance(0.3f)) || (p.age >= 30 && p.Overall < 48)
                              || (p.team < 0 && p.age >= 31 && rng.Chance(0.5f));
                if (retire) retired.Add(p);
            }
            foreach (var p in retired)
            {
                if (p.team >= 0) f.teams[p.team].roster.Remove(p.id);
                f.players.Remove(p);
                bool hall = p.peak >= HallPeak && p.seasons >= HallSeasons;
                f.moves.Add(new FrMove { year = f.year, text = "RETIRED: " + p.Name + " (" + p.age + ")" + (hall ? " · HALL OF FAME" : "") });
                if (hall) f.awards.Add(new FrAward { year = f.year, kind = "HALL", name = p.Name, team = p.team, line = "peak " + p.peak + ", " + p.seasons + " seasons" });
            }
            foreach (var t in f.teams) t.deadMoney = 0;
        }

        /// <summary>Rating change for one off-season.</summary>
        public static int Progression(FrPlayer p, SeededRandom rng)
        {
            int ovr = p.Overall;
            int change;
            if (p.age <= 22) change = 3;
            else if (p.age <= 25) change = 2;
            else if (p.age <= 27) change = 1;
            else if (p.age <= 29) change = 0;
            else change = -(1 + (p.age - 30) / 2);
            change += rng.Range(-1, 2);
            if (change > 0) change = Math.Min(change, Math.Max(0, p.potential - ovr));
            return change;
        }

        // ------------------------------------------------------------------ draft

        private static void NewDraftClass(FranchiseSaveData f, ContentCatalog c)
        {
            var rng = new SeededRandom(StableHash.Of("fr:draft:" + f.seed + ":" + f.year));
            f.players.RemoveAll(p => p.prospect); // last year's undrafted were made free agents already
            for (int i = 0; i < ProspectsPerClass; i++)
            {
                var p = Generate(c, f, rng, 46 + rng.Range(0, 18), 19 + rng.Range(0, 3));
                p.prospect = true;
                p.potential = Math.Min(94, p.Overall + 6 + rng.Range(0, 20));
                p.salary = 0;
                p.years = 0;
            }
            f.scoutPoints = ScoutPointsPerDraft;

            // Lottery: the four non-playoff teams draw for the first two picks.
            var table = SeasonEngine.Standings(f.season);
            var worst = new List<int>();
            for (int i = table.Count - 1; i >= SeasonEngine.PlayoffTeams; i--) worst.Add(IndexOf(table[i].TeamId));
            var weights = new List<int>(LotteryWeights);
            var order = new List<int>();
            var pool = new List<int>(worst);
            f.lottery.Clear();
            for (int draw = 0; draw < 2 && pool.Count > 0; draw++)
            {
                int total = 0;
                foreach (var w in weights) total += w;
                int roll = rng.Range(0, total), k = 0;
                while (k < weights.Count - 1 && roll >= weights[k]) { roll -= weights[k]; k++; }
                order.Add(pool[k]);
                pool.RemoveAt(k);
                weights.RemoveAt(k);
            }
            order.AddRange(pool);
            for (int pick = 0; pick < order.Count; pick++)
            {
                int was = worst.IndexOf(order[pick]);
                if (was != pick) f.lottery.Add(Abbr(c, f, order[pick]) + (was > pick ? " jumps to pick " : " drops to pick ") + (pick + 1) + " (was " + (was + 1) + ")");
            }
            if (f.lottery.Count == 0 && order.Count > 0) f.lottery.Add("No surprises: " + Abbr(c, f, order[0]) + " keeps the first pick.");
            // Playoff teams pick next: semifinal losers (worse record first), the runner-up, then the champion.
            string champ = ChampionId(f);
            var final = SeasonEngine.FinalGame(f.season);
            for (int i = SeasonEngine.PlayoffTeams - 1; i >= 0; i--)
            {
                string id = table[i].TeamId;
                if (id != champ && (final == null || !final.Involves(id))) order.Add(IndexOf(id));
            }
            if (final != null && final.played) { order.Add(IndexOf(final.LoserId)); order.Add(IndexOf(final.WinnerId)); }
            f.draftOrder = order;
            f.draftMade = 0;
        }

        private static string Abbr(ContentCatalog c, FranchiseSaveData f, int team) =>
            c?.Team(f.teams[team].baseId)?.abbreviation ?? ("TEAM " + (team + 1));

        public static List<FrPlayer> Prospects(FranchiseSaveData f) => f.players.FindAll(p => p.prospect);

        /// <summary>What your scouts can tell you: an overall range that narrows with each scouting visit.</summary>
        public static void ScoutedRange(FrPlayer p, out int low, out int high)
        {
            int hw = ScoutRange[Math.Max(0, Math.Min(3, p.scout))];
            int bias = hw == 0 ? 0 : (int)(StableHash.Of("scout:" + p.id + ":" + p.scout) % (uint)(hw + 1)) - hw / 2;
            low = Math.Max(30, p.Overall + bias - hw);
            high = Math.Min(99, p.Overall + bias + hw);
            if (low > p.Overall) low = p.Overall;
            if (high < p.Overall) high = p.Overall;
        }

        /// <summary>Ceiling grade, A+ to D, visible once a prospect has been scouted twice.</summary>
        public static string PotentialGrade(FrPlayer p)
        {
            if (p.prospect && p.scout < 2) return "?";
            int pot = p.potential;
            if (pot >= 85) return "A+";
            if (pot >= 80) return "A";
            if (pot >= 75) return "B";
            if (pot >= 70) return "C";
            return "D";
        }

        public static bool Scout(FranchiseSaveData f, int prospectId)
        {
            var p = Player(f, prospectId);
            if (f.phase != FranchisePhase.Draft || p == null || !p.prospect || p.scout >= 3 || f.scoutPoints <= 0) return false;
            p.scout++;
            f.scoutPoints--;
            return true;
        }

        public static int OnTheClock(FranchiseSaveData f) =>
            f.phase == FranchisePhase.Draft && f.draftMade < f.draftOrder.Count ? f.draftOrder[f.draftMade] : -1;

        /// <summary>The AI teams pick until it's your turn (or the draft is over).</summary>
        public static void DraftUntilYou(FranchiseSaveData f)
        {
            while (f.phase == FranchisePhase.Draft)
            {
                int team = OnTheClock(f);
                if (team < 0) { FinishDraft(f); return; }
                if (team == f.you) return;
                var rng = new SeededRandom(StableHash.Of("fr:pick:" + f.seed + ":" + f.year + ":" + f.draftMade));
                FrPlayer best = null;
                float bestScore = float.MinValue;
                foreach (var p in Prospects(f))
                {
                    float score = p.Overall + (p.potential - p.Overall) * 0.8f + rng.Range(-4, 5);
                    if (score > bestScore) { bestScore = score; best = p; }
                }
                if (best == null) { FinishDraft(f); return; }
                Take(f, team, best);
            }
        }

        /// <summary>You draft a prospect with your pick.</summary>
        public static bool Pick(FranchiseSaveData f, int prospectId)
        {
            var p = Player(f, prospectId);
            if (OnTheClock(f) != f.you || p == null || !p.prospect) return false;
            Take(f, f.you, p);
            DraftUntilYou(f);
            return true;
        }

        private static void Take(FranchiseSaveData f, int team, FrPlayer p)
        {
            int slot = f.draftMade;
            p.prospect = false;
            p.team = team;
            p.salary = RookieScale[Math.Min(slot, RookieScale.Length - 1)];
            p.years = 3;
            p.draftYear = f.year;
            p.draftPick = slot + 1;
            p.scout = 3;
            p.number = FreeNumber(f, team, p.number);
            f.teams[team].roster.Add(p.id);
            f.draftMade++;
            if (f.draftMade >= f.draftOrder.Count) FinishDraft(f);
        }

        private static int FreeNumber(FranchiseSaveData f, int team, int wanted)
        {
            var used = new HashSet<int>();
            foreach (var q in Roster(f, team)) used.Add(q.number);
            int n = Math.Max(0, Math.Min(99, wanted));
            for (int guard = 0; guard < 100 && used.Contains(n); guard++) n = (n + 7) % 100;
            return n;
        }

        private static void FinishDraft(FranchiseSaveData f)
        {
            if (f.phase != FranchisePhase.Draft) return;
            foreach (var p in Prospects(f))
            {
                p.prospect = false;
                p.team = -1;
                p.scout = 3;
                p.salary = Demand(p);
                p.years = 0;
            }
            f.phase = FranchisePhase.ReSign;
            AiReSign(f);
        }

        // ------------------------------------------------------------------ contracts

        /// <summary>Your players whose contracts are up: re-sign them now or they become free agents.</summary>
        public static List<FrPlayer> Expiring(FranchiseSaveData f) => Roster(f, f.you).FindAll(p => p.years == 0);

        /// <summary>Re-signing your own player may go over the cap (you hold their rights).</summary>
        public static bool ReSign(FranchiseSaveData f, int playerId)
        {
            var p = Player(f, playerId);
            if (f.phase != FranchisePhase.ReSign || p == null || p.team != f.you || p.years != 0) return false;
            p.salary = Demand(p);
            p.years = DemandYears(p);
            return true;
        }

        private static void AiReSign(FranchiseSaveData f)
        {
            for (int t = 0; t < f.teams.Count; t++)
            {
                if (t == f.you) continue;
                foreach (var p in Roster(f, t))
                {
                    if (p.years != 0) continue;
                    float worth = Value(p);
                    bool keep = worth >= 6f && (Payroll(f, t) - p.salary + Demand(p) <= Cap + 60 || worth >= 20f);
                    if (keep)
                    {
                        p.salary = Demand(p);
                        p.years = DemandYears(p);
                    }
                }
            }
        }

        /// <summary>Ends re-signing: anyone still expiring (yours and theirs) becomes a free agent.</summary>
        public static void FinishReSign(FranchiseSaveData f)
        {
            if (f.phase != FranchisePhase.ReSign) return;
            foreach (var p in f.players)
            {
                if (p.team < 0 || p.years != 0) continue;
                f.teams[p.team].roster.Remove(p.id);
                p.team = -1;
                p.salary = Demand(p);
            }
            f.phase = FranchisePhase.FreeAgency;
        }

        public static List<FrPlayer> FreeAgents(FranchiseSaveData f)
        {
            var list = f.players.FindAll(p => p.team < 0 && !p.prospect);
            list.Sort((a, b) => b.Overall != a.Overall ? b.Overall.CompareTo(a.Overall) : a.id.CompareTo(b.id));
            return list;
        }

        /// <summary>Why you can't sign a free agent right now, or null if you can.</summary>
        public static string CannotSign(FranchiseSaveData f, FrPlayer p)
        {
            if (p == null || p.team >= 0 || p.prospect) return "Not a free agent.";
            if (f.phase == FranchisePhase.Draft || f.phase == FranchisePhase.ReSign) return "Free agency opens after the draft and re-signing.";
            if (f.phase == FranchisePhase.Playoffs) return "No signings during the playoffs.";
            if (f.teams[f.you].roster.Count >= MaxRoster) return "Roster full (" + MaxRoster + "). Release someone first.";
            int demand = Demand(p);
            if (demand > MinSalary && Payroll(f, f.you) + demand > Cap) return "Not enough cap room: " + Money(CapRoom(f, f.you)) + " left, asks " + Money(demand) + ".";
            return null;
        }

        public static bool SignFreeAgent(FranchiseSaveData f, int playerId)
        {
            var p = Player(f, playerId);
            if (CannotSign(f, p) != null) return false;
            p.salary = Demand(p);
            p.years = DemandYears(p);
            p.team = f.you;
            p.number = FreeNumber(f, f.you, p.number);
            f.teams[f.you].roster.Add(p.id);
            f.moves.Add(new FrMove { year = f.year, text = "SIGNED: " + p.Name + " · " + Money(p.salary) + " × " + p.years });
            return true;
        }

        /// <summary>Releases one of your players. Half of this season's salary stays on the books.</summary>
        public static bool Release(FranchiseSaveData f, int playerId)
        {
            var p = Player(f, playerId);
            if (p == null || p.team != f.you || f.phase == FranchisePhase.Playoffs) return false;
            if (f.phase == FranchisePhase.Regular && f.teams[f.you].roster.Count <= MinRoster) return false;
            f.teams[f.you].deadMoney += p.salary / 2;
            f.teams[f.you].roster.Remove(p.id);
            p.team = -1;
            p.years = 0;
            p.salary = Demand(p);
            f.moves.Add(new FrMove { year = f.year, text = "RELEASED: " + p.Name });
            return true;
        }

        /// <summary>Ends free agency: AI teams sign the best players they can fit until they have seven.</summary>
        public static void FinishFreeAgency(FranchiseSaveData f, ContentCatalog c)
        {
            if (f.phase != FranchisePhase.FreeAgency) return;
            var rng = new SeededRandom(StableHash.Of("fr:fa:" + f.seed + ":" + f.year));
            for (int round = 0; round < MaxRoster; round++)
            {
                for (int t = 0; t < f.teams.Count; t++)
                {
                    if (t == f.you || f.teams[t].roster.Count >= MinRoster + 1) continue;
                    FrPlayer best = null;
                    foreach (var p in FreeAgents(f))
                    {
                        int d = Demand(p);
                        if (d > MinSalary && Payroll(f, t) + d > Cap) continue;
                        if (best == null || Value(p) > Value(best)) best = p;
                    }
                    if (best == null && f.teams[t].roster.Count < MinRoster)
                    {
                        best = Generate(c, f, rng, 48 + rng.Range(0, 6), 24 + rng.Range(0, 8));
                        best.team = -1;
                    }
                    if (best == null) continue;
                    best.salary = Math.Min(Demand(best), Math.Max(MinSalary, CapRoom(f, t)));
                    best.years = DemandYears(best);
                    best.team = t;
                    best.number = FreeNumber(f, t, best.number);
                    f.teams[t].roster.Add(best.id);
                }
            }
            for (int t = 0; t < f.teams.Count; t++) if (t != f.you) SortRoster(f, t);
            f.phase = FranchisePhase.Preseason;
        }

        /// <summary>Why the new season can't start yet, or null when your roster is legal.</summary>
        public static string CannotStart(FranchiseSaveData f)
        {
            int n = f.teams[f.you].roster.Count;
            if (n < MinRoster) return "You need at least " + MinRoster + " players (you have " + n + "). Sign free agents.";
            if (n > MaxRoster) return "Too many players: cut down to " + MaxRoster + ".";
            return null;
        }

        public static bool StartSeason(FranchiseSaveData f, ContentCatalog c)
        {
            if (f.phase != FranchisePhase.Preseason || CannotStart(f) != null) return false;
            for (int t = 0; t < f.teams.Count; t++)
            {
                if (t == f.you) continue;
                var r = Roster(f, t);
                while (r.Count > MaxRoster)
                {
                    r.Sort((a, b) => Value(a).CompareTo(Value(b)));
                    var cut = r[0];
                    f.teams[t].roster.Remove(cut.id);
                    cut.team = -1;
                    cut.years = 0;
                    r.RemoveAt(0);
                }
                SortRoster(f, t);
            }
            // Free agents nobody wanted retire quietly; the pool keeps the best 30.
            f.players.RemoveAll(p => p.team < 0 && !p.prospect && (p.age >= 33 || (p.age >= 26 && p.Overall < 50)));
            var pool = FreeAgents(f);
            pool.Sort((a, b) => Value(b).CompareTo(Value(a)));
            for (int i = 30; i < pool.Count; i++) f.players.Remove(pool[i]);
            NewSchedule(f);
            f.phase = FranchisePhase.Regular;
            return true;
        }

        // ------------------------------------------------------------------ trades

        /// <summary>
        /// Would <paramref name="other"/> make this trade? They need to win it by a little on their own
        /// valuation, both rosters must stay legal, and each side must fit under the cap or take back
        /// no more than 125% of the salary it sends out.
        /// </summary>
        public static TradeVerdict Evaluate(FranchiseSaveData f, int other, List<int> give, List<int> get)
        {
            var v = new TradeVerdict();
            if (!TradesOpen(f)) { v.Reason = f.phase == FranchisePhase.Playoffs ? "No trades in the playoffs." : "Past the trade deadline (week " + TradeDeadlineWeek + ")."; return v; }
            if (other == f.you || other < 0 || other >= f.teams.Count) { v.Reason = "Pick a team to trade with."; return v; }
            if (give.Count == 0 && get.Count == 0) { v.Reason = "Add players to the deal."; return v; }
            if (give.Count > MaxTradePlayers || get.Count > MaxTradePlayers) { v.Reason = "Up to " + MaxTradePlayers + " players a side."; return v; }
            var gp = new List<FrPlayer>();
            var tp = new List<FrPlayer>();
            foreach (int id in give) { var p = Player(f, id); if (p == null || p.team != f.you) { v.Reason = "That player isn't yours."; return v; } gp.Add(p); }
            foreach (int id in get) { var p = Player(f, id); if (p == null || p.team != other) { v.Reason = "That player isn't theirs."; return v; } tp.Add(p); }

            int minRoster = f.phase == FranchisePhase.Regular ? MinRoster : 5;
            int yourCount = f.teams[f.you].roster.Count - gp.Count + tp.Count;
            int theirCount = f.teams[other].roster.Count - tp.Count + gp.Count;
            if (yourCount < minRoster || theirCount < minRoster) { v.Reason = "Both teams must keep at least " + minRoster + " players."; return v; }
            if (yourCount > MaxRoster || theirCount > MaxRoster) { v.Reason = "That leaves a roster over " + MaxRoster + " players."; return v; }

            int outYou = 0, outThem = 0;
            foreach (var p in gp) outYou += p.salary;
            foreach (var p in tp) outThem += p.salary;
            if (!SalaryOk(Payroll(f, f.you), outYou, outThem)) { v.Reason = "Your payroll would go over the cap: send out more salary."; return v; }
            if (!SalaryOk(Payroll(f, other), outThem, outYou)) { v.Reason = "Their payroll would go over the cap."; return v; }

            foreach (var p in gp) v.TheyGet += Value(p);
            foreach (var p in tp) v.TheyGive += Value(p);
            // Extra bodies clog a roster; consolidating into one better player is worth a little.
            v.TheyGet -= Math.Max(0, gp.Count - tp.Count) * 2f;
            float margin = 1.5f + v.TheyGive * 0.06f;
            if (v.TheyGet >= v.TheyGive + margin)
            {
                v.Accepted = true;
                v.Reason = "They'll do it.";
            }
            else
            {
                float gap = v.TheyGive + margin - v.TheyGet;
                v.Reason = gap < 4f ? "Close. They want a little more." : (gap < 12f ? "They want more value back." : "Not even close.");
            }
            return v;
        }

        private static bool SalaryOk(int payroll, int outgoing, int incoming) =>
            payroll - outgoing + incoming <= Cap || incoming <= outgoing * 5 / 4 + 10;

        public static bool Trade(FranchiseSaveData f, int other, List<int> give, List<int> get)
        {
            if (!Evaluate(f, other, give, get).Accepted) return false;
            var names = new List<string>();
            foreach (int id in give)
            {
                var p = Player(f, id);
                f.teams[f.you].roster.Remove(id);
                f.teams[other].roster.Add(id);
                p.team = other;
                names.Add(p.Name);
            }
            var back = new List<string>();
            foreach (int id in get)
            {
                var p = Player(f, id);
                f.teams[other].roster.Remove(id);
                f.teams[f.you].roster.Add(id);
                p.team = f.you;
                p.number = FreeNumber(f, f.you, p.number);
                back.Add(p.Name);
            }
            SortRoster(f, other);
            f.moves.Add(new FrMove { year = f.year, text = "TRADE: " + (names.Count > 0 ? string.Join(", ", names) : "nothing") + " for " + (back.Count > 0 ? string.Join(", ", back) : "nothing") });
            return true;
        }

        // ------------------------------------------------------------------ playing games

        /// <summary>
        /// Puts the Franchise clubs and players into the catalog so a match can load them: club ids
        /// "team.fr.N" wear their league team's name, colours, logo and court.
        /// </summary>
        public static void Register(ContentCatalog c, FranchiseSaveData f)
        {
            if (c == null || f == null) return;
            foreach (var p in f.players)
            {
                if (p.prospect) continue;
                var arch = c.Archetypes.Find(a => (int)a.archetype == p.archetype) ?? c.Archetypes[0];
                string id = PlayerId(p.id);
                var def = c.Player(id);
                if (def == null) c.Players.Add(def = new PlayerDef { id = id });
                def.firstName = p.first;
                def.lastName = p.last;
                def.jerseyNumber = p.number;
                def.archetypeId = arch.id;
                def.attributes = p.attrs;
                def.appearance = new AppearanceDef(p.skin, p.hair, p.hairColor, (BodyType)Math.Max(0, Math.Min(2, p.body)), Math.Max(0, Math.Min(2, p.height)));
            }
            for (int i = 0; i < f.teams.Count; i++)
            {
                var src = c.Team(f.teams[i].baseId);
                string id = TeamId(i);
                var team = c.Team(id);
                if (team == null) c.Teams.Add(team = new TeamDef { id = id, tier = TeamTier.Franchise });
                if (src != null)
                {
                    team.city = src.city; team.nickname = src.nickname; team.abbreviation = src.abbreviation;
                    team.primary = src.primary; team.secondary = src.secondary; team.accent = src.accent;
                    team.logoShape = src.logoShape; team.logoMotif = src.logoMotif; team.pattern = src.pattern;
                    team.homeCourtId = src.homeCourtId; team.motto = src.motto; team.scheme = src.scheme;
                    team.customKit = src.customKit; team.shorts = src.shorts; team.shoes = src.shoes;
                }
                team.rosterPlayerIds = f.teams[i].roster.ConvertAll(PlayerId);
            }
        }

        /// <summary>Your next game as a Full Court match (you're always the home side in the engine; the court is the real host's).</summary>
        public static MatchRequest NextMatch(FranchiseSaveData f, ContentCatalog c, string difficultyId)
        {
            var g = NextGame(f);
            if (g == null) return null;
            Register(c, f);
            string you = TeamId(f.you);
            string opp = g.homeId == you ? g.awayId : g.homeId;
            return new MatchRequest
            {
                Mode = GameMode.Franchise,
                HomeTeamId = you,
                AwayTeamId = opp,
                CourtId = c.Team(g.homeId)?.homeCourtId,
                RulesId = FullCourt.RulesId,
                FullCourt = true,
                DifficultyId = difficultyId,
                ContextId = "fr:y" + f.year + ":w" + g.week + ":r" + g.round,
                Round = g.round,
                Coach = f.coach,
            };
        }

        public static string RoundName(int round) => round == 1 ? "SEMIFINAL" : (round == 2 ? "FINAL" : "");

        /// <summary>Your team's all-time line: seasons, record, playoff trips and titles.</summary>
        public static (int seasons, int wins, int losses, int playoffs, int titles) Totals(FranchiseSaveData f, int team)
        {
            int s = 0, w = 0, l = 0, po = 0, t = 0;
            foreach (var h in f.history)
            {
                if (h.team != team) continue;
                s++; w += h.wins; l += h.losses;
                if (h.finish >= 1) po++;
                if (h.finish == 3) t++;
            }
            return (s, w, l, po, t);
        }

        public static string FinishName(int finish) =>
            finish == 3 ? "CHAMPIONS" : finish == 2 ? "LOST FINAL" : finish == 1 ? "LOST SEMI" : "MISSED PLAYOFFS";

        public static string PhaseName(FranchisePhase p)
        {
            switch (p)
            {
                case FranchisePhase.Regular: return "REGULAR SEASON";
                case FranchisePhase.Playoffs: return "PLAYOFFS";
                case FranchisePhase.Draft: return "DRAFT";
                case FranchisePhase.ReSign: return "RE-SIGN PLAYERS";
                case FranchisePhase.FreeAgency: return "FREE AGENCY";
                default: return "PRESEASON";
            }
        }
    }
}
