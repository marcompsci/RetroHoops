using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>One player's finished season, for his PLAYER CARD (I83).</summary>
    [Serializable]
    public class FrSeasonLine
    {
        public int year;
        public int team = -1;
        public int gp, pts, reb, ast, ovr;
    }

    /// <summary>One club's draft class grade (I91).</summary>
    [Serializable]
    public class FrDraftGrade
    {
        public int year;
        public int team;
        public string grade = "";
        public string line = "";
    }

    /// <summary>League-wide extras for Franchise (I81–I95).</summary>
    [Serializable]
    public class FrLeagueData
    {
        /// <summary>I88: optional injuries (off by default).</summary>
        public bool injuries;
        /// <summary>I89: your club's own name (empty = the base club's).</summary>
        public string city = "", nickname = "", abbr = "";
        /// <summary>I90: the GM legacy score and its best season.</summary>
        public int legacy;
        /// <summary>I92: this season's preseason prediction (team indexes, best first) and the year it's for.</summary>
        public List<int> preview = new List<int>();
        public int previewYear;
        /// <summary>I92: how last season's prediction for you turned out, one line.</summary>
        public string previewResult = "";
        /// <summary>I91: the latest draft grades.</summary>
        public List<FrDraftGrade> grades = new List<FrDraftGrade>();
        /// <summary>I85: extensions signed this season (one per player per season).</summary>
        public List<int> extended = new List<int>();

        // ---- I96–I110 (FranchiseLife)
        /// <summary>I96 commissioner: salary cap off, trade difficulty (0 EASY, 1 NORMAL, 2 HARD), GM calls off, rating edits made.</summary>
        public bool capOff, offersOff;
        public int tradeLevel = 1, commishEdits;
        /// <summary>I97: the latest headlines, oldest first.</summary>
        public List<string> news = new List<string>();
        public int lastWeekDone;
        /// <summary>I108: your PLAYER OF THE WEEK wins.</summary>
        public int potw;
        /// <summary>I98: this season's All-Stars and the game's MVP.</summary>
        public int allStarYear, allStarMvp = -1;
        public List<int> allStars = new List<int>();
        /// <summary>I103: owner's goals missed in a row, fired, the clubs calling, and jobs you've taken.</summary>
        public int missedGoals, jobs;
        public bool fired;
        public List<int> jobOffers = new List<int>();
        /// <summary>I104 record book, I100 retired numbers, I105 discount asks this off-season, I109 WHAT'S NEW seen.</summary>
        public List<FrRecord> records = new List<FrRecord>();
        public List<FrBanner> banners = new List<FrBanner>();
        public List<int> asked = new List<int>();
        public int whatsNew;
    }

    /// <summary>
    /// I81–I95: a deeper league, after what players love in Hoop Land and the retro sports hits, and what they ask for.
    /// Full stat lines and league leaders, player cards with career history, more awards, contract extensions,
    /// free agents who weigh up your club, a trade block, optional injuries, renaming your club, a GM legacy score,
    /// draft grades, a preseason preview, power rankings and SIM TO THE DEADLINE.
    /// </summary>
    public static class FranchiseLeague
    {
        public static FrLeagueData Of(FranchiseSaveData f) => f.league ?? (f.league = new FrLeagueData());

        // ------------------------------------------------------------------ I81 full stat lines

        /// <summary>Team totals per simulated game (Full Court 5 on 5, four minutes): rebounds, assists, steals, blocks.</summary>
        public const int SimReb = 18, SimAst = 8, SimStl = 3, SimBlk = 2;

        /// <summary>
        /// Shares a simulated game's rebounds, assists, steals and blocks among a rotation by rating, with a small
        /// per-game wobble from a hash (no draws from the game's random stream, so simulated scores are unchanged).
        /// </summary>
        public static void CreditBox(FranchiseSaveData f, int team, string gameKey)
        {
            var r = Franchise.Roster(f, team);
            int n = Math.Min(7, r.Count);
            if (n == 0) return;
            Share(r, n, SimReb, p => p.attrs.rebounding, gameKey + ":r", (p, v) => { p.reb += v; p.careerReb += v; });
            Share(r, n, SimAst, p => p.attrs.playmaking, gameKey + ":a", (p, v) => { p.ast += v; p.careerAst += v; });
            Share(r, n, SimStl, p => p.attrs.defense, gameKey + ":s", (p, v) => p.stl += v);
            Share(r, n, SimBlk, p => (p.attrs.defense + p.attrs.rebounding) / 2, gameKey + ":b", (p, v) => p.blk += v);
        }

        private static void Share(List<FrPlayer> r, int n, int total, Func<FrPlayer, int> rating, string key, Action<FrPlayer, int> add)
        {
            var w = new float[n];
            float sum = 0f;
            for (int i = 0; i < n; i++)
            {
                float minutes = i < 5 ? 1f : 0.4f;
                float wobble = 0.7f + (StableHash.Of(key + ":" + r[i].id) % 600u) / 1000f;
                w[i] = minutes * (float)Math.Pow(Math.Max(1, rating(r[i]) - 30), 1.5) * wobble;
                sum += w[i];
            }
            int given = 0;
            for (int i = 0; i < n; i++)
            {
                int share = i == n - 1 ? total - given : (int)Math.Round(total * w[i] / sum);
                share = Math.Max(0, Math.Min(total - given, share));
                given += share;
                if (share > 0) add(r[i], share);
            }
        }

        /// <summary>
        /// A game you played: your players get their real box score lines (by player id); anyone missing (bench who didn't
        /// appear) gets nothing. Returns false when the box doesn't match your roster, so the caller falls back to the sim share.
        /// </summary>
        public static bool CreditRealBox(FranchiseSaveData f, IList<SummaryLine> lines, int humanTeam)
        {
            if (lines == null || lines.Count == 0) return false;
            int matched = 0;
            foreach (var l in lines)
            {
                if (l == null || l.team != humanTeam || l.stats == null || string.IsNullOrEmpty(l.playerId)) continue;
                if (!l.playerId.StartsWith(Franchise.PlayerPrefix, StringComparison.Ordinal)) continue;
                if (!int.TryParse(l.playerId.Substring(Franchise.PlayerPrefix.Length), out int id)) continue;
                var p = Franchise.Player(f, id);
                if (p == null || p.team != f.you) continue;
                var s = l.stats;
                p.gp++;
                p.pts += s.points; p.careerPts += s.points;
                p.reb += s.rebounds; p.careerReb += s.rebounds;
                p.ast += s.assists; p.careerAst += s.assists;
                p.stl += s.steals;
                p.blk += s.blocks;
                matched++;
            }
            return matched > 0;
        }

        public static void ResetSeason(FrPlayer p)
        {
            p.reb = p.ast = p.stl = p.blk = 0;
            p.startOvr = p.Overall;
        }

        // ------------------------------------------------------------------ I82 league leaders

        public static readonly string[] LeaderStats = { "PPG", "RPG", "APG", "SPG", "BPG" };

        public static float PerGame(FrPlayer p, int stat)
        {
            if (p.gp == 0) return 0f;
            switch (stat)
            {
                case 1: return p.reb / (float)p.gp;
                case 2: return p.ast / (float)p.gp;
                case 3: return p.stl / (float)p.gp;
                case 4: return p.blk / (float)p.gp;
                default: return p.pts / (float)p.gp;
            }
        }

        /// <summary>Top players in a stat this season (at least a third of the season's games played).</summary>
        public static List<FrPlayer> Leaders(FranchiseSaveData f, int stat, int count = 5)
        {
            int maxGp = 0;
            foreach (var p in f.players) if (p.team >= 0) maxGp = Math.Max(maxGp, p.gp);
            int minGp = Math.Max(1, maxGp / 3);
            var list = f.players.FindAll(p => p.team >= 0 && !p.prospect && p.gp >= minGp);
            list.Sort((a, b) =>
            {
                int c = PerGame(b, stat).CompareTo(PerGame(a, stat));
                return c != 0 ? c : a.id.CompareTo(b.id);
            });
            if (list.Count > count) list.RemoveRange(count, list.Count - count);
            return list;
        }

        // ------------------------------------------------------------------ I83 player card

        public static List<string> CardLines(FranchiseSaveData f, FrPlayer p, Func<int, string> teamName)
        {
            var lines = new List<string>();
            if (p.gp > 0)
                lines.Add(Loc.T("THIS SEASON") + "  " + p.gp + " GP · " + PerGame(p, 0).ToString("0.0") + " PPG · " + PerGame(p, 1).ToString("0.0") + " RPG · "
                          + PerGame(p, 2).ToString("0.0") + " APG · " + PerGame(p, 3).ToString("0.0") + " SPG · " + PerGame(p, 4).ToString("0.0") + " BPG");
            int gp = p.gp, pts = p.pts, reb = p.reb, ast = p.ast;
            foreach (var s in p.history) { gp += s.gp; pts += s.pts; reb += s.reb; ast += s.ast; }
            if (gp > 0)
                lines.Add(Loc.T("CAREER") + "  " + gp + " GP · " + (pts / (float)gp).ToString("0.0") + " PPG · " + (reb / (float)gp).ToString("0.0") + " RPG · "
                          + (ast / (float)gp).ToString("0.0") + " APG · " + p.titles + " " + Loc.T("TITLES"));
            for (int i = p.history.Count - 1; i >= 0 && i >= p.history.Count - 8; i--)
            {
                var s = p.history[i];
                float g = Math.Max(1, s.gp);
                lines.Add(Loc.T("YEAR") + " " + s.year + "  " + (s.team >= 0 ? teamName(s.team) : "FA") + "  OVR " + s.ovr + "  " + (s.pts / g).ToString("0.0") + " / "
                          + (s.reb / g).ToString("0.0") + " / " + (s.ast / g).ToString("0.0"));
            }
            foreach (var a in f.awards)
                if (a.name == p.Name) lines.Add("* " + Loc.T(AwardName(a.kind)) + "  " + Loc.T("YEAR") + " " + a.year);
            return lines;
        }

        // ------------------------------------------------------------------ I84 more awards

        public static string AwardName(string kind)
        {
            switch (kind)
            {
                case "MVP": return "MVP";
                case "ROOKIE": return "ROOKIE OF THE YEAR";
                case "HALL": return "HALL OF FAME";
                case "DEFENSE": return "DEFENSIVE PLAYER";
                case "SIXTH": return "SIXTH MAN";
                case "IMPROVED": return "MOST IMPROVED";
                case "ALL": return "ALL-LEAGUE FIRST TEAM";
                case "GM": return "EXECUTIVE OF THE YEAR";
                case "FINALS": return "FINALS MVP";
                default: return kind;
            }
        }

        /// <summary>Called by Franchise at the end of a season, after MVP and ROOKIE and before players age.</summary>
        public static void EndOfSeason(FranchiseSaveData f, List<TeamRecord> table)
        {
            var o = Of(f);
            // DEFENSIVE PLAYER: steals and blocks per game, plus the defense rating.
            FrPlayer def = null, sixth = null, improved = null;
            float defScore = float.MinValue, sixthScore = float.MinValue;
            int bestGain = 0;
            foreach (var p in f.players)
            {
                if (p.team < 0 || p.gp == 0) continue;
                float d = (PerGame(p, 3) + PerGame(p, 4)) * 4f + p.attrs.defense * 0.1f;
                if (d > defScore) { defScore = d; def = p; }
                int slot = f.teams[p.team].roster.IndexOf(p.id);
                if (slot >= 5 && p.Ppg > sixthScore) { sixthScore = p.Ppg; sixth = p; }
                int gain = p.Overall - (p.startOvr > 0 ? p.startOvr : p.Overall);
                if (gain > bestGain) { bestGain = gain; improved = p; }
            }
            if (def != null) Award(f, "DEFENSE", def, (PerGame(def, 3) + PerGame(def, 4)).ToString("0.0") + " STL+BLK");
            if (sixth != null) Award(f, "SIXTH", sixth, sixth.Ppg.ToString("0.0") + " PPG off the bench");
            if (improved != null) Award(f, "IMPROVED", improved, "+" + bestGain + " OVR");
            // ALL-LEAGUE FIRST TEAM: the five best seasons (scoring, all-round play and rating).
            var all = f.players.FindAll(p => p.team >= 0 && p.gp > 0);
            all.Sort((a, b) => Season(b).CompareTo(Season(a)));
            for (int i = 0; i < 5 && i < all.Count; i++) Award(f, "ALL", all[i], all[i].Ppg.ToString("0.0") + " PPG");
            // EXECUTIVE OF THE YEAR: the club that beat its preseason ranking by the most (ties: better record).
            int gm = -1, bestJump = int.MinValue;
            for (int t = 0; t < f.teams.Count; t++)
            {
                int predicted = o.previewYear == f.year ? o.preview.IndexOf(t) : -1;
                int actual = table.FindIndex(r => r.TeamId == Franchise.TeamId(t));
                if (predicted < 0 || actual < 0) continue;
                int jump = predicted - actual;
                if (jump > bestJump) { bestJump = jump; gm = t; }
            }
            if (gm >= 0 && bestJump > 0)
                f.awards.Add(new FrAward { year = f.year, kind = "GM", name = gm == f.you ? "YOU" : "GM", team = gm, line = "+" + bestJump + " places on the preview" });

            // I92: how the preview called your season.
            int you = table.FindIndex(r => r.TeamId == Franchise.TeamId(f.you));
            int pick = o.previewYear == f.year ? o.preview.IndexOf(f.you) : -1;
            o.previewResult = pick >= 0 && you >= 0
                ? Loc.T("PREVIEW") + ": #" + (pick + 1) + "  ·  " + Loc.T("FINISHED") + ": #" + (you + 1)
                : "";

            // I83: everyone's season goes in his history.
            foreach (var p in f.players)
            {
                if (p.prospect || p.gp == 0) continue;
                p.history.Add(new FrSeasonLine { year = f.year, team = p.team, gp = p.gp, pts = p.pts, reb = p.reb, ast = p.ast, ovr = p.Overall });
                if (p.history.Count > 20) p.history.RemoveAt(0);
            }

            // I90: the GM legacy score.
            o.legacy += LegacyForSeason(f, table);
            o.extended.Clear();
            o.asked.Clear();
        }

        private static float Season(FrPlayer p) => p.Ppg * 2f + PerGame(p, 1) + PerGame(p, 2) * 1.5f + (PerGame(p, 3) + PerGame(p, 4)) * 2f + p.Overall * 0.3f;

        private static void Award(FranchiseSaveData f, string kind, FrPlayer p, string line) =>
            f.awards.Add(new FrAward { year = f.year, kind = kind, name = p.Name, team = p.team, line = line });

        // ------------------------------------------------------------------ I85 contract extensions

        /// <summary>Why you can't extend him now, or null. In-season, before the deadline, players in their last year only.</summary>
        public static string CannotExtend(FranchiseSaveData f, FrPlayer p)
        {
            if (p == null || p.team != f.you) return "Not on your club.";
            if (f.phase != FranchisePhase.Regular) return "Extensions happen during the regular season.";
            if (!Franchise.TradesOpen(f)) return "Past the deadline: re-sign him in the off-season.";
            if (p.years != 0) return "He still has years left.";
            if (Of(f).extended.Contains(p.id)) return "Already extended.";
            if (FrontOffice.Morale(p) < 45) return "He won't talk while he's unhappy.";
            return null;
        }

        /// <summary>What an extension costs: his demand, with the mood discount or premium of re-signing.</summary>
        public static int ExtensionSalary(FrPlayer p) => FrontOffice.ReSignSalary(p, Franchise.Demand(p));

        public static bool Extend(FranchiseSaveData f, int playerId)
        {
            var p = Franchise.Player(f, playerId);
            if (CannotExtend(f, p) != null) return false;
            // He keeps this season's salary; the new deal starts next season (years counts down at the end of this one).
            p.extension = ExtensionSalary(p);
            p.years = Franchise.DemandYears(p) + 1;
            Of(f).extended.Add(p.id);
            FrontOffice.AddMood(p, 6);
            FranchiseLife.Headline(f, Loc.T("EXTENDED") + ": " + p.Name);
            f.moves.Add(new FrMove { year = f.year, text = "EXTENDED: " + p.Name + " · " + Franchise.Money(p.extension) + " × " + (p.years - 1) });
            return true;
        }

        /// <summary>At the end of the season an extended player's new salary starts.</summary>
        public static void StartExtensions(FranchiseSaveData f)
        {
            foreach (var p in f.players)
                if (p.extension > 0)
                {
                    p.salary = p.extension;
                    p.extension = 0;
                }
        }

        // ------------------------------------------------------------------ I86 free-agent interest

        /// <summary>
        /// How keen a free agent is on your club, 0..100: winning, fans, facilities and a starting spot pull him in.
        /// Under <see cref="InterestNeeded"/> he signs elsewhere.
        /// </summary>
        public static int Interest(FranchiseSaveData f, FrPlayer p)
        {
            var o = FrontOffice.Of(f);
            float score = 45f;
            var last = f.history.FindLast(h => h.team == f.you);
            if (last != null && last.wins + last.losses > 0) score += (last.wins / (float)(last.wins + last.losses) - 0.5f) * 40f;
            if (last != null && last.finish >= 3) score += 10f;
            score += (FrontOffice.Fans(f) - FrontOffice.BaseFans) * 0.3f;
            score += (o.gym + o.film + o.arena + o.lounge) * 2f;
            // A starting role: would he be in your top five?
            var r = Franchise.Roster(f, f.you);
            int better = r.FindAll(x => x.Overall > p.Overall).Count;
            score += better < 5 ? 12f : -6f;
            // Stars are pickier; journeymen take what they can get.
            score -= Math.Max(0, p.Overall - 70) * 1.2f;
            score += Math.Max(0, 60 - p.Overall) * 1f;
            return Math.Max(0, Math.Min(100, (int)Math.Round(score)));
        }

        public const int InterestNeeded = 35;

        // ------------------------------------------------------------------ I87 trade block

        public static bool ToggleBlock(FranchiseSaveData f, int playerId)
        {
            var p = Franchise.Player(f, playerId);
            if (p == null || p.team != f.you || p.created) return false;
            p.onBlock = !p.onBlock;
            if (p.onBlock) FrontOffice.AddMood(p, -3); // he notices
            return true;
        }

        public static List<FrPlayer> Block(FranchiseSaveData f) => Franchise.Roster(f, f.you).FindAll(p => p.onBlock);

        /// <summary>Chance a GM calls in a given week: higher with players on the block.</summary>
        public static float OfferChance(FranchiseSaveData f) => Block(f).Count > 0 ? 0.75f : FranchiseDepth.OfferChance;

        // ------------------------------------------------------------------ I88 optional injuries

        /// <summary>
        /// After each week or round, with injuries on, a starter may pick up a knock and sit 1–3 games (fewer with the
        /// PLAYERS' LOUNGE). The injured go to the end of the depth chart and, once healthy, back to their old spot.
        /// </summary>
        public static void AfterGameInjuries(FranchiseSaveData f, string gameKey)
        {
            if (!Of(f).injuries) return;
            int lounge = FrontOffice.Of(f).lounge;
            for (int t = 0; t < f.teams.Count; t++)
            {
                var roster = f.teams[t].roster;
                // Heal first: one game less for everyone hurt, and the healed go back where they were.
                foreach (var p in Franchise.Roster(f, t))
                {
                    if (p.injured <= 0) continue;
                    p.injured--;
                    if (p.injured > 0) continue;
                    roster.Remove(p.id);
                    roster.Insert(Math.Max(0, Math.Min(roster.Count, p.injSlot)), p.id);
                    p.injSlot = -1;
                    if (t == f.you) FrontOffice.Of(f).note = p.Name + " is back from injury.";
                }
                var r = Franchise.Roster(f, t);
                int healthy = r.FindAll(x => x.injured == 0).Count;
                int n = Math.Min(5, r.Count);
                for (int i = 0; i < n && healthy > 6; i++)
                {
                    var p = r[i];
                    if (p.injured > 0) continue;
                    uint h = StableHash.Of(gameKey + ":inj:" + p.id);
                    float chance = 0.012f + (p.age >= 31 ? 0.008f : 0f) - (t == f.you ? lounge * 0.002f : 0f);
                    if ((h % 10000u) / 10000f >= chance) continue;
                    int games = 1 + (int)(h / 10000u % 3u);
                    if (t == f.you) games = Math.Max(1, games - lounge / 2);
                    p.injured = games;
                    p.injSlot = roster.IndexOf(p.id);
                    healthy--;
                    if (t == f.you) FrontOffice.Of(f).note = p.Name + " is hurt: out " + games + (games == 1 ? " game." : " games.");
                }
                // The injured sit at the end of the depth chart.
                var hurt = roster.FindAll(id => Franchise.Player(f, id)?.injured > 0);
                if (hurt.Count == 0) continue;
                roster.RemoveAll(id => hurt.Contains(id));
                roster.AddRange(hurt);
            }
        }

        public static List<FrPlayer> Injured(FranchiseSaveData f, int team) => Franchise.Roster(f, team).FindAll(p => p.injured > 0);

        // ------------------------------------------------------------------ I89 rename your club

        public const int MaxCity = 16, MaxNickname = 16, MaxAbbr = 3;

        /// <summary>Sets your club's name (filtered, plain text). Empty fields go back to the original. Returns false if a field isn't clean.</summary>
        public static bool Rename(FranchiseSaveData f, string city, string nickname, string abbr)
        {
            string c = TextSafety.Plain(city ?? "", MaxCity), n = TextSafety.Plain(nickname ?? "", MaxNickname);
            string a = TextSafety.Plain((abbr ?? "").ToUpperInvariant(), MaxAbbr);
            if (!TextSafety.IsClean(c) || !TextSafety.IsClean(n) || !TextSafety.IsClean(a)) return false;
            var o = Of(f);
            o.city = c; o.nickname = n; o.abbr = a;
            return true;
        }

        /// <summary>Applied in <see cref="Franchise.Register"/> after the base club's details.</summary>
        public static void ApplyName(FranchiseSaveData f, int team, TeamDef def)
        {
            if (team != f.you || def == null) return;
            var o = Of(f);
            if (!string.IsNullOrEmpty(o.city)) def.city = o.city;
            if (!string.IsNullOrEmpty(o.nickname)) def.nickname = o.nickname;
            if (!string.IsNullOrEmpty(o.abbr)) def.abbreviation = o.abbr;
        }

        // ------------------------------------------------------------------ I90 GM legacy

        public static int LegacyForSeason(FranchiseSaveData f, List<TeamRecord> table)
        {
            var row = table.Find(r => r.TeamId == Franchise.TeamId(f.you));
            int pts = row != null ? row.Wins * 2 : 0;
            var h = f.history.FindLast(x => x.team == f.you && x.year == f.year);
            if (h != null) pts += h.finish >= 3 ? 40 : h.finish == 2 ? 20 : h.finish == 1 ? 10 : 0;
            foreach (var a in f.awards) if (a.year == f.year && a.team == f.you) pts += a.kind == "GM" ? 10 : a.kind == "MVP" ? 8 : 3;
            return pts;
        }

        public static readonly (int at, string name)[] Ranks =
        {
            (0, "INTERN"), (40, "ASSISTANT GM"), (100, "GENERAL MANAGER"), (200, "EXECUTIVE"), (350, "ARCHITECT"), (550, "DYNASTY BUILDER"), (800, "LEGEND GM"),
        };

        public static string Rank(int legacy)
        {
            string r = Ranks[0].name;
            foreach (var x in Ranks) if (legacy >= x.at) r = x.name;
            return r;
        }

        public static int NextRankAt(int legacy)
        {
            foreach (var x in Ranks) if (x.at > legacy) return x.at;
            return -1;
        }

        // ------------------------------------------------------------------ I91 draft grades

        /// <summary>Grades every club's picks from this draft: value of the class against the average class.</summary>
        public static void GradeDraft(FranchiseSaveData f, ContentCatalog c)
        {
            var o = Of(f);
            o.grades.Clear();
            var byTeam = new Dictionary<int, List<FrPlayer>>();
            foreach (var p in f.players)
                if (p.draftYear == f.year && p.draftPick > 0 && p.team >= 0)
                {
                    if (!byTeam.TryGetValue(p.team, out var list)) byTeam[p.team] = list = new List<FrPlayer>();
                    list.Add(p);
                }
            if (byTeam.Count == 0) return;
            var scores = new Dictionary<int, float>();
            float avg = 0f;
            foreach (var kv in byTeam)
            {
                float s = 0f;
                foreach (var p in kv.Value) s += p.Overall * 0.6f + p.potential * 0.4f + Math.Max(0, 12 - p.draftPick) * 0.4f;
                scores[kv.Key] = s;
                avg += s;
            }
            avg /= byTeam.Count;
            foreach (var kv in byTeam)
            {
                float d = scores[kv.Key] - avg;
                string g = d >= 12 ? "A" : d >= 5 ? "B+" : d >= 0 ? "B" : d >= -6 ? "C+" : d >= -14 ? "C" : "D";
                var names = kv.Value.ConvertAll(p => p.Short + " (#" + p.draftPick + ")");
                o.grades.Add(new FrDraftGrade { year = f.year, team = kv.Key, grade = g, line = string.Join(", ", names) });
            }
            o.grades.Sort((a, b) => a.team == f.you ? -1 : b.team == f.you ? 1 : string.CompareOrdinal(a.grade, b.grade));
        }

        // ------------------------------------------------------------------ I92 preview / I93 power rankings

        /// <summary>Clubs from strongest to weakest by roster (the preseason preview and the power rankings' base).</summary>
        public static List<int> ByStrength(FranchiseSaveData f)
        {
            var order = new List<int>();
            for (int t = 0; t < f.teams.Count; t++) order.Add(t);
            order.Sort((a, b) =>
            {
                int c = Franchise.Strength(f, b).CompareTo(Franchise.Strength(f, a));
                return c != 0 ? c : a.CompareTo(b);
            });
            return order;
        }

        /// <summary>Set once per season, on the first look at it (sticks even as rosters change).</summary>
        public static List<int> Preview(FranchiseSaveData f)
        {
            var o = Of(f);
            if (o.previewYear != f.year || o.preview.Count != f.teams.Count)
            {
                o.preview = ByStrength(f);
                o.previewYear = f.year;
            }
            return o.preview;
        }

        /// <summary>
        /// POWER RANKINGS: record first (win percentage), then point differential, then roster strength. With no games
        /// played they're the preview.
        /// </summary>
        public static List<int> PowerRankings(FranchiseSaveData f)
        {
            var table = SeasonEngine.Standings(f.season);
            var order = ByStrength(f);
            float Score(int t)
            {
                var row = table.Find(r => r.TeamId == Franchise.TeamId(t));
                float s = Franchise.Strength(f, t) * 0.5f;
                if (row != null && row.Games > 0) s += row.WinPct * 40f + row.Differential / (float)row.Games * 0.6f;
                return s;
            }
            order.Sort((a, b) =>
            {
                int c = Score(b).CompareTo(Score(a));
                return c != 0 ? c : a.CompareTo(b);
            });
            return order;
        }

        // ------------------------------------------------------------------ I94 sim to the deadline

        /// <summary>Simulates weeks until the trade deadline (or the end of the regular season if it has passed).</summary>
        public static void SimToDeadline(FranchiseSaveData f, ContentCatalog c)
        {
            for (int guard = 0; guard < 64 && f.phase == FranchisePhase.Regular && f.season.currentWeek < Franchise.TradeDeadlineWeek; guard++)
            {
                int before = f.season.currentWeek;
                Franchise.SimNext(f, c);
                if (f.season.currentWeek == before && Franchise.NextGame(f) == null) break;
            }
        }
    }
}
