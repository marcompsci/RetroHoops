using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>A line in the club and league record book (I104).</summary>
    [Serializable]
    public class FrRecord
    {
        public string kind = "";
        /// <summary>Tenths (24.3 PPG = 243); wins are whole numbers × 10 too.</summary>
        public int value;
        public string name = "";
        public int team = -1;
        public int year;
    }

    /// <summary>A retired number hanging in your arena (I100).</summary>
    [Serializable]
    public class FrBanner
    {
        public int number;
        public string name = "";
        public int year;
        public string line = "";
    }

    /// <summary>
    /// I96–I110: a living league. Commissioner tools, headlines, All-Star weekend, Finals MVP, retired numbers,
    /// mentors, continuity, the hot seat (and new jobs), a record book, contract talks, deadline-day trades between
    /// other clubs, career milestones, Player of the Week and a WHAT'S NEW card. Every rule is here; Franchise calls it.
    /// </summary>
    public static class FranchiseLife
    {
        static FrLeagueData L(FranchiseSaveData f) => FranchiseLeague.Of(f);

        // ------------------------------------------------------------------ I96 commissioner

        public static readonly string[] TradeLevels = { "EASY", "NORMAL", "HARD" };

        public static bool CapOn(FranchiseSaveData f) => !L(f).capOff;

        /// <summary>How much more value the other GM wants back: EASY 0.4×, NORMAL 1×, HARD 1.6×.</summary>
        public static float TradeMargin(FranchiseSaveData f)
        {
            switch (L(f).tradeLevel)
            {
                case 0: return 0.4f;
                case 2: return 1.6f;
                default: return 1f;
            }
        }

        public static bool OffersOn(FranchiseSaveData f) => !L(f).offersOff;

        /// <summary>Commissioner: nudges one player's ratings up or down (every attribute by the same step). Turns COMMISSIONER on in the history.</summary>
        public static bool EditRatings(FranchiseSaveData f, int playerId, int delta)
        {
            var p = Franchise.Player(f, playerId);
            if (p == null || delta == 0) return false;
            int before = p.Overall;
            p.attrs = p.attrs.Offset(Math.Max(-5, Math.Min(5, delta)));
            if (p.Overall == before) return false;
            p.potential = Math.Max(p.potential, p.Overall);
            L(f).commishEdits++;
            return true;
        }

        // ------------------------------------------------------------------ I97 headlines

        public const int MaxNews = 40;

        public static void Headline(FranchiseSaveData f, string text)
        {
            if (f == null || string.IsNullOrEmpty(text)) return;
            var l = L(f);
            int week = f.phase == FranchisePhase.Regular ? f.season.currentWeek : -1;
            l.news.Add((week >= 0 ? Loc.T("WEEK") + " " + week + "  " : "") + text);
            if (l.news.Count > MaxNews) l.news.RemoveRange(0, l.news.Count - MaxNews);
        }

        public static List<string> Latest(FranchiseSaveData f, int count)
        {
            var news = L(f).news;
            var list = new List<string>();
            for (int i = news.Count - 1; i >= 0 && list.Count < count; i--) list.Add(news[i]);
            return list;
        }

        static string Club(FranchiseSaveData f, ContentCatalog c, int team) =>
            c?.Team(Franchise.TeamId(team))?.abbreviation ?? c?.Team(f.teams[team].baseId)?.abbreviation ?? ("T" + (team + 1));

        // ------------------------------------------------------------------ after each regular-season week (I98, I106, I107, I108)

        public static void AfterWeek(FranchiseSaveData f, ContentCatalog c, int week)
        {
            if (f.phase != FranchisePhase.Regular && !(f.phase == FranchisePhase.Playoffs && week == Franchise.Weeks - 1)) return;
            var l = L(f);
            if (l.lastWeekDone == f.year * 100 + week) return;
            l.lastWeekDone = f.year * 100 + week;
            PlayerOfTheWeek(f, c, week);
            Milestones(f, c);
            if (week == Franchise.TradeDeadlineWeek - 1) DeadlineDay(f, c);
            if (week == Franchise.TradeDeadlineWeek - 1) AllStarWeekend(f, c);
            Streaks(f, c);
        }

        // I108: the most points in a week (stored as season points at the start of the week).
        static void PlayerOfTheWeek(FranchiseSaveData f, ContentCatalog c, int week)
        {
            FrPlayer best = null;
            int bestPts = 0;
            foreach (var p in f.players)
            {
                if (p.team < 0) { p.weekPts = p.pts; continue; }
                int gained = p.pts - p.weekPts;
                if (gained > bestPts || (gained == bestPts && best != null && p.id < best.id)) { bestPts = gained; best = p; }
                p.weekPts = p.pts;
            }
            if (best == null || bestPts <= 0) return;
            Headline(f, Loc.T("PLAYER OF THE WEEK") + ": " + best.Name + " (" + Club(f, c, best.team) + "), " + bestPts + " " + Loc.T("points"));
            if (best.team == f.you)
            {
                FrontOffice.AddMood(best, 4);
                L(f).potw++;
            }
        }

        // I107: career milestones.
        public static readonly (int bit, string stat, int at)[] MilestoneList =
        {
            (1, "POINTS", 500), (2, "POINTS", 1000), (4, "POINTS", 2000), (8, "POINTS", 3000),
            (16, "REBOUNDS", 300), (32, "REBOUNDS", 750), (64, "ASSISTS", 300), (128, "ASSISTS", 750),
        };

        static void Milestones(FranchiseSaveData f, ContentCatalog c)
        {
            foreach (var p in f.players)
            {
                if (p.team < 0) continue;
                foreach (var m in MilestoneList)
                {
                    if ((p.milestones & m.bit) != 0) continue;
                    int v = m.stat == "POINTS" ? p.careerPts : m.stat == "REBOUNDS" ? p.careerReb : p.careerAst;
                    if (v < m.at) continue;
                    p.milestones |= m.bit;
                    if (p.team == f.you || m.at >= 1000)
                        Headline(f, p.Name + " (" + Club(f, c, p.team) + ") " + Loc.T("reaches") + " " + m.at + " " + Loc.T("career") + " " + Loc.T(m.stat));
                    if (p.team == f.you) FrontOffice.AddMood(p, 3);
                }
            }
        }

        static void Streaks(FranchiseSaveData f, ContentCatalog c)
        {
            foreach (var row in SeasonEngine.Standings(f.season))
            {
                if (row.Streak == 5) Headline(f, (c?.Team(row.TeamId)?.abbreviation ?? "?") + " " + Loc.T("have won five straight"));
                if (row.Streak == -5) Headline(f, (c?.Team(row.TeamId)?.abbreviation ?? "?") + " " + Loc.T("have lost five straight"));
            }
        }

        // ------------------------------------------------------------------ I98 All-Star weekend

        public const int AllStars = 10;

        /// <summary>The league's ten best seasons so far go to the All-Star game (simulated); it's in the news and lifts their moods.</summary>
        static void AllStarWeekend(FranchiseSaveData f, ContentCatalog c)
        {
            var l = L(f);
            if (l.allStarYear == f.year) return;
            l.allStarYear = f.year;
            var pool = f.players.FindAll(p => p.team >= 0 && p.gp > 0);
            pool.Sort((a, b) =>
            {
                int k = Score(b).CompareTo(Score(a));
                return k != 0 ? k : a.id.CompareTo(b.id);
            });
            if (pool.Count > AllStars) pool.RemoveRange(AllStars, pool.Count - AllStars);
            l.allStars.Clear();
            foreach (var p in pool)
            {
                l.allStars.Add(p.id);
                p.allStarNods++;
                if (p.team == f.you) FrontOffice.AddMood(p, 5);
            }
            if (pool.Count < 2) return;
            // East = 1st, 4th, 5th, 8th, 9th; West = the rest (a fair snake), simulated by ratings.
            float east = 0f, west = 0f;
            for (int i = 0; i < pool.Count; i++) { if (i % 4 == 0 || i % 4 == 3) east += pool[i].Overall; else west += pool[i].Overall; }
            uint h = StableHash.Of("fr:allstar:" + f.seed + ":" + f.year);
            int a = 120 + (int)(h % 25u), b = 120 + (int)(h / 25u % 25u);
            if ((east > west) != (a > b)) { int t = a; a = b; b = t; }
            if (a == b) a++;
            var mvp = pool[(int)(h / 625u % (uint)pool.Count)];
            l.allStarMvp = mvp.id;
            mvp.allStarMvps++;
            int mine = l.allStars.FindAll(id => Franchise.Player(f, id)?.team == f.you).Count;
            Headline(f, Loc.T("ALL-STAR GAME") + ": " + Loc.T("EAST") + " " + a + ", " + Loc.T("WEST") + " " + b + ". " + Loc.T("MVP") + ": " + mvp.Name
                        + (mine > 0 ? "  (" + mine + " " + Loc.T("from your club") + ")" : ""));
        }

        static float Score(FrPlayer p) => p.Ppg * 2f + p.Rpg + p.Apg * 1.5f + p.Overall * 0.4f;

        // ------------------------------------------------------------------ I106 deadline day

        /// <summary>On deadline week the other clubs make up to two fair trades between themselves.</summary>
        static void DeadlineDay(FranchiseSaveData f, ContentCatalog c)
        {
            if (!OffersOn(f)) return;
            var rng = new SeededRandom(StableHash.Of("fr:deadline:" + f.seed + ":" + f.year));
            int made = 0;
            for (int attempt = 0; attempt < 12 && made < 2; attempt++)
            {
                int a = rng.Range(0, f.teams.Count), b = rng.Range(0, f.teams.Count);
                if (a == b || a == f.you || b == f.you) continue;
                var ra = Franchise.Roster(f, a);
                var rb = Franchise.Roster(f, b);
                if (ra.Count <= Franchise.MinRoster || rb.Count <= Franchise.MinRoster) continue;
                // A bench piece from each side, value within 15% of each other, salaries that fit.
                FrPlayer pa = ra[Math.Min(ra.Count - 1, 4 + rng.Range(0, 3))];
                FrPlayer pb = null;
                float va = Franchise.Value(pa), best = float.MaxValue;
                foreach (var q in rb)
                {
                    if (q.created || rb.IndexOf(q) < 4) continue;
                    float gap = Math.Abs(Franchise.Value(q) - va);
                    if (gap < best) { best = gap; pb = q; }
                }
                if (pb == null || pa.created || best > Math.Max(2f, va * 0.15f)) continue;
                if (CapOn(f))
                {
                    int payA = Franchise.Payroll(f, a) - pa.salary + pb.salary, payB = Franchise.Payroll(f, b) - pb.salary + pa.salary;
                    if ((payA > Franchise.Cap && pb.salary > pa.salary) || (payB > Franchise.Cap && pa.salary > pb.salary)) continue;
                }
                f.teams[a].roster.Remove(pa.id); f.teams[b].roster.Add(pa.id); pa.team = b;
                f.teams[b].roster.Remove(pb.id); f.teams[a].roster.Add(pb.id); pb.team = a;
                Franchise.SortRoster(f, a);
                Franchise.SortRoster(f, b);
                string text = "TRADE: " + Club(f, c, a) + " send " + pa.Name + " to " + Club(f, c, b) + " for " + pb.Name;
                f.moves.Add(new FrMove { year = f.year, text = text });
                Headline(f, Loc.T("DEADLINE DEAL") + ": " + Club(f, c, a) + " - " + Club(f, c, b) + " (" + pa.Short + " / " + pb.Short + ")");
                made++;
            }
        }

        // ------------------------------------------------------------------ end of season (I99, I103, I104)

        /// <summary>Called by Franchise at the end of a season, after the other awards.</summary>
        public static void EndOfSeason(FranchiseSaveData f, ContentCatalog c, List<TeamRecord> table, string championId)
        {
            var l = L(f);
            // I99 FINALS MVP: the champion's best season.
            int champ = Franchise.IndexOf(championId ?? "");
            if (champ >= 0)
            {
                FrPlayer best = null;
                foreach (var p in Franchise.Roster(f, champ)) if (best == null || Score(p) > Score(best)) best = p;
                if (best != null)
                {
                    f.awards.Add(new FrAward { year = f.year, kind = "FINALS", name = best.Name, team = champ, line = best.Ppg.ToString("0.0") + " PPG" });
                    Headline(f, Loc.T("FINALS MVP") + ": " + best.Name + " (" + Club(f, c, champ) + ")");
                }
                Headline(f, Club(f, c, champ) + " " + Loc.T("win the title") + "!");
            }
            // I104: the record book (season per-game bests and team wins).
            foreach (var p in f.players)
            {
                if (p.team < 0 || p.gp < Franchise.Weeks / 2) continue;
                Record(f, c, "PPG", (int)Math.Round(p.Ppg * 10f), p.Name, p.team);
                Record(f, c, "RPG", (int)Math.Round(p.Rpg * 10f), p.Name, p.team);
                Record(f, c, "APG", (int)Math.Round(p.Apg * 10f), p.Name, p.team);
            }
            foreach (var row in table)
            {
                int t = Franchise.IndexOf(row.TeamId);
                Record(f, c, "WINS", row.Wins * 10, c?.Team(row.TeamId)?.FullName ?? row.TeamId, t);
            }
            // I103 THE HOT SEAT: the owner's goal, missed three seasons running, costs you the job.
            var o = FrontOffice.Of(f);
            if (o.lastGoalMet) l.missedGoals = 0;
            else l.missedGoals++;
            if (l.missedGoals == 2) Headline(f, Loc.T("HOT SEAT") + ": " + Loc.T("the owner wants results next season."));
            if (l.missedGoals >= 3)
            {
                l.fired = true;
                l.jobOffers = JobOffers(f, table);
                Headline(f, Loc.T("FIRED") + ": " + Loc.T("the owner has made a change. Other clubs are calling."));
            }
        }

        static void Record(FranchiseSaveData f, ContentCatalog c, string kind, int value, string name, int team)
        {
            var l = L(f);
            var r = l.records.Find(x => x.kind == kind);
            if (r != null && value <= r.value) return;
            if (r == null) l.records.Add(r = new FrRecord { kind = kind });
            bool beaten = r.value > 0;
            r.value = value; r.name = name; r.team = team; r.year = f.year;
            if (beaten) Headline(f, Loc.T("NEW LEAGUE RECORD") + ": " + name + ", " + (value / 10f).ToString(kind == "WINS" ? "0" : "0.0") + " " + kind);
        }

        public static readonly string[] RecordKinds = { "PPG", "RPG", "APG", "WINS" };

        /// <summary>Three clubs that want you: the ones that finished lowest, except your own.</summary>
        public static List<int> JobOffers(FranchiseSaveData f, List<TeamRecord> table)
        {
            var offers = new List<int>();
            for (int i = table.Count - 1; i >= 0 && offers.Count < 3; i--)
            {
                int t = Franchise.IndexOf(table[i].TeamId);
                if (t >= 0 && t != f.you) offers.Add(t);
            }
            return offers;
        }

        /// <summary>
        /// Take a new job: you run another club from now on. Your GM legacy, credits and history stay with you; the new
        /// club's building (facilities), fans, captain, rival and name are its own; the owner's patience starts fresh.
        /// </summary>
        public static bool TakeJob(FranchiseSaveData f, int team)
        {
            var l = L(f);
            if (!l.fired || !l.jobOffers.Contains(team)) return false;
            int old = f.you;
            f.you = team;
            var o = FrontOffice.Of(f);
            o.gym = o.film = o.arena = o.lounge = 0;
            o.fanDelta = 0;
            o.captain = -1;
            o.rival = old; // they let you go
            o.tradeAsk = -1;
            l.city = l.nickname = l.abbr = "";
            l.fired = false;
            l.jobOffers.Clear();
            l.missedGoals = 0;
            l.jobs++;
            foreach (var p in f.players) p.onBlock = false;
            f.moves.Add(new FrMove { year = f.year, text = "NEW JOB: you take over team " + (team + 1) + "." });
            return true;
        }

        // ------------------------------------------------------------------ I100 retired numbers

        /// <summary>A club legend: four seasons with you, or a title with you and three seasons.</summary>
        public static bool IsLegend(FranchiseSaveData f, FrPlayer p)
        {
            int withYou = 0, titlesWithYou = 0;
            foreach (var h in p.history) if (h.team == f.you) withYou++;
            foreach (var h in f.history) if (h.team == f.you && h.finish >= 3 && p.history.Exists(x => x.year == h.year && x.team == f.you)) titlesWithYou++;
            return withYou >= 4 || (titlesWithYou >= 1 && withYou >= 3);
        }

        /// <summary>Called by Franchise for each of your players who retires: legends get their number raised.</summary>
        public static void OnRetire(FranchiseSaveData f, FrPlayer p)
        {
            if (p.team != f.you || !IsLegend(f, p)) return;
            var l = L(f);
            if (l.banners.Exists(b => b.number == p.number)) return;
            int seasons = p.history.FindAll(h => h.team == f.you).Count;
            l.banners.Add(new FrBanner { number = p.number, name = p.Name, year = f.year, line = seasons + " " + Loc.T("seasons") + " · " + p.careerPts + " " + Loc.T("points") });
            Headline(f, Loc.T("NUMBER RETIRED") + ": #" + p.number + " " + p.Name);
        }

        public static bool IsRetiredNumber(FranchiseSaveData f, int team, int number) =>
            team == f.you && L(f).banners.Exists(b => b.number == number);

        // ------------------------------------------------------------------ I101 mentors, I102 continuity

        /// <summary>
        /// A veteran (30+, OVR 70+) on your club takes up to two of your youngest players (23 and under, below their
        /// potential) under his wing: +1 growth each off-season.
        /// </summary>
        public static List<FrPlayer> Mentees(FranchiseSaveData f)
        {
            var r = Franchise.Roster(f, f.you);
            int mentors = r.FindAll(p => p.age >= 30 && p.Overall >= 70).Count;
            var young = r.FindAll(p => p.age <= 23 && p.Overall < p.potential);
            young.Sort((a, b) => a.age != b.age ? a.age.CompareTo(b.age) : a.id.CompareTo(b.id));
            int take = Math.Min(young.Count, mentors * 2);
            return young.GetRange(0, take);
        }

        /// <summary>
        /// Continuity: how long your rotation has played together (average seasons with this club, top seven). Worth up to
        /// +1 in simulated games and up to +1% shooting in games you play.
        /// </summary>
        public static float Continuity(FranchiseSaveData f, int team)
        {
            var r = Franchise.Roster(f, team);
            int n = Math.Min(7, r.Count);
            if (n == 0) return 0f;
            float sum = 0f;
            for (int i = 0; i < n; i++) sum += Math.Min(4, r[i].tenure);
            return sum / n;
        }

        public static float ContinuityEdge(FranchiseSaveData f, int team) => Math.Min(1f, Continuity(f, team) * 0.35f);

        public static float ContinuityShooting(FranchiseSaveData f) => Math.Min(0.01f, Continuity(f, f.you) * 0.004f);

        /// <summary>At the end of each season: one more year together, or a fresh start on a new club.</summary>
        public static void UpdateTenure(FranchiseSaveData f)
        {
            foreach (var p in f.players)
            {
                if (p.team >= 0 && p.team == p.lastTeam) p.tenure++;
                else p.tenure = 0;
                p.lastTeam = p.team;
            }
        }

        // ------------------------------------------------------------------ I105 contract talks

        /// <summary>
        /// At re-signing you can ask for a hometown discount (10% off his ask), once per player. A happy player (60+ mood)
        /// says yes and signs on the spot; anyone else is insulted (−8 mood) and his ask stays.
        /// </summary>
        public static bool AskDiscount(FranchiseSaveData f, int playerId, out string result)
        {
            var p = Franchise.Player(f, playerId);
            var l = L(f);
            result = "";
            if (p == null || f.phase != FranchisePhase.ReSign || p.team != f.you || p.years != 0) { result = Loc.T("Not now."); return false; }
            if (l.asked.Contains(p.id)) { result = Loc.T("You already asked him."); return false; }
            l.asked.Add(p.id);
            if (FrontOffice.Morale(p) >= 60)
            {
                p.salary = Math.Max(Franchise.MinSalary, FrontOffice.ReSignSalary(p, Franchise.Demand(p)) * 9 / 10);
                p.years = Franchise.DemandYears(p);
                result = p.Name + " " + Loc.T("takes the hometown discount.");
                FrontOffice.AddMood(p, 2);
                return true;
            }
            FrontOffice.AddMood(p, -8);
            result = p.Name + " " + Loc.T("is insulted. His ask stays.");
            return false;
        }

        // ------------------------------------------------------------------ I109 what's new

        public const int WhatsNewVersion = 1;
        public static readonly string[] WhatsNew =
        {
            "PLAYER OF THE WEEK, ALL-STAR WEEKEND and DEADLINE DAY in the HEADLINES.",
            "Ask for a HOMETOWN DISCOUNT when re-signing.",
            "Club legends get their NUMBER RETIRED; veterans MENTOR your youngest players.",
            "Missing the owner's goal three seasons running puts you on the HOT SEAT, then out of a job.",
            "COMMISSIONER tools in OFFICE: cap, trade difficulty, GM calls, edit ratings.",
        };

        public static bool ShowWhatsNew(FranchiseSaveData f) => L(f).whatsNew < WhatsNewVersion;

        public static void DismissWhatsNew(FranchiseSaveData f) => L(f).whatsNew = WhatsNewVersion;
    }
}
