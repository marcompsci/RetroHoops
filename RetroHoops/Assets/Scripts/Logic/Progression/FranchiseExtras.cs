using System;
using System.Collections.Generic;
using System.Text;

namespace CallerRetroBall.Logic
{
    /// <summary>A Franchise trophy (I112): earned once per franchise, paid once in SP.</summary>
    public sealed class FrTrophy
    {
        public int Bit;
        public string Name, Detail;
        public int Sp;
        public Func<FranchiseSaveData, bool> Met;
    }

    /// <summary>
    /// I111–I125: franchise save slots, trophies, a dynasty tracker, scouting reports, game recaps, training camp,
    /// next season's payroll, AUTO OFF-SEASON, team stats, head-to-head records, the assistant GM's tips, award races,
    /// SHARE YOUR SEASON and a season timeline. Every rule is here; Franchise and the screen call it.
    /// </summary>
    public static class FranchiseExtras
    {
        static FrLeagueData L(FranchiseSaveData f) => FranchiseLeague.Of(f);

        // ------------------------------------------------------------------ I111 save slots

        public const int MaxSlots = 3;
        public const int MaxMoves = 150;

        /// <summary>Franchises you can switch to (not counting the active one).</summary>
        public static List<FranchiseSaveData> Parked(CareerSaveData d) => d.franchiseSlots ?? (d.franchiseSlots = new List<FranchiseSaveData>());

        public static int SlotsUsed(CareerSaveData d) => (d.franchise != null && d.franchise.active ? 1 : 0) + Parked(d).FindAll(x => x != null && x.active).Count;

        /// <summary>Puts the active franchise on the shelf so a new one can start. False when all three slots are full.</summary>
        public static bool Park(CareerSaveData d)
        {
            if (d.franchise == null || !d.franchise.active) return true;
            var parked = Parked(d);
            parked.RemoveAll(x => x == null || !x.active);
            if (parked.Count >= MaxSlots - 1) return false;
            parked.Add(d.franchise);
            d.franchise = new FranchiseSaveData();
            return true;
        }

        /// <summary>Switches to a parked franchise; the active one (if any) takes its place on the shelf.</summary>
        public static bool Switch(CareerSaveData d, int index)
        {
            var parked = Parked(d);
            if (index < 0 || index >= parked.Count || parked[index] == null || !parked[index].active) return false;
            var next = parked[index];
            parked.RemoveAt(index);
            if (d.franchise != null && d.franchise.active) parked.Insert(index, d.franchise);
            d.franchise = next;
            return true;
        }

        /// <summary>Deletes a parked franchise for good.</summary>
        public static bool DeleteParked(CareerSaveData d, int index)
        {
            var parked = Parked(d);
            if (index < 0 || index >= parked.Count) return false;
            parked.RemoveAt(index);
            return true;
        }

        public static string SlotLabel(FranchiseSaveData f, ContentCatalog c)
        {
            if (f == null || !f.active) return "";
            var club = c?.Team(f.teams.Count > f.you ? f.teams[f.you].baseId : "");
            string name = !string.IsNullOrEmpty(L(f).nickname) ? (L(f).city + " " + L(f).nickname).Trim() : club?.FullName ?? "?";
            return name.ToUpperInvariant() + "  ·  " + Loc.T("YEAR") + " " + f.year + "  ·  " + f.titles + " " + Loc.T("TITLES");
        }

        // ------------------------------------------------------------------ I112 trophies

        public static readonly FrTrophy[] Trophies =
        {
            T(1, "FIRST WIN", "Win a Franchise game.", 100, f => f.history.Exists(h => h.team == f.you && h.wins > 0) || YourRow(f)?.Wins > 0),
            T(2, "PLAYOFF BOUND", "Make the playoffs.", 150, f => f.history.Exists(h => h.team == f.you && h.finish >= 1)),
            T(4, "CHAMPIONS", "Win a Franchise title.", 300, f => f.titles >= 1),
            T(8, "BACK-TO-BACK", "Win two titles in a row.", 400, f => L(f).bestTitleStreak >= 2),
            T(16, "THREE-PEAT", "Win three titles in a row.", 600, f => L(f).bestTitleStreak >= 3),
            T(32, "WINNING SEASON", "Win 10 or more regular-season games.", 150, f => f.history.Exists(h => h.team == f.you && h.wins >= 10)),
            T(64, "MVP FACTORY", "Have the league MVP on your club.", 250, f => f.awards.Exists(a => a.kind == "MVP" && a.team == f.you)),
            T(128, "FULL HOUSE", "Get the fans to 100.", 200, f => FrontOffice.Fans(f) >= 100),
            T(256, "RAFTERS", "Retire a legend's number.", 250, f => L(f).banners.Count > 0),
            T(512, "STATE OF THE ART", "Max out all four facilities.", 300, f => { var o = FrontOffice.Of(f); return o.gym >= 3 && o.film >= 3 && o.arena >= 3 && o.lounge >= 3; }),
            T(1024, "LEGEND GM", "Reach the LEGEND GM rank.", 600, f => FranchiseLeague.Rank(L(f).legacy) == "LEGEND GM"),
            T(2048, "FIVE-YEAR PLAN", "Finish five seasons.", 200, f => f.history.FindAll(h => h.team == f.you).Count >= 5),
        };

        static FrTrophy T(int bit, string name, string detail, int sp, Func<FranchiseSaveData, bool> met) =>
            new FrTrophy { Bit = bit, Name = name, Detail = detail, Sp = sp, Met = met };

        static TeamRecord YourRow(FranchiseSaveData f) => SeasonEngine.Standings(f.season).Find(r => r.TeamId == Franchise.TeamId(f.you));

        /// <summary>Marks newly met trophies as earned (and in the headlines). Returns how many are new.</summary>
        public static int CheckTrophies(FranchiseSaveData f)
        {
            var l = L(f);
            int fresh = 0;
            foreach (var t in Trophies)
            {
                if ((l.trophies & t.Bit) != 0 || !t.Met(f)) continue;
                l.trophies |= t.Bit;
                fresh++;
                FranchiseLife.Headline(f, Loc.T("TROPHY") + ": " + Loc.T(t.Name));
            }
            return fresh;
        }

        public static bool Earned(FranchiseSaveData f, FrTrophy t) => (L(f).trophies & t.Bit) != 0;
        public static bool Claimed(FranchiseSaveData f, FrTrophy t) => (L(f).trophiesClaimed & t.Bit) != 0;

        /// <summary>Pays an earned trophy's SP into the career, once.</summary>
        public static int Claim(CareerSaveData d, FranchiseSaveData f, FrTrophy t)
        {
            if (d == null || !Earned(f, t) || Claimed(f, t)) return 0;
            L(f).trophiesClaimed |= t.Bit;
            d.signalPoints += t.Sp;
            return t.Sp;
        }

        // ------------------------------------------------------------------ I113 dynasty, I124 timeline (end of season)

        public static void EndOfSeason(FranchiseSaveData f, ContentCatalog c, List<TeamRecord> table, string championId)
        {
            var l = L(f);
            bool won = championId == Franchise.TeamId(f.you);
            l.titleStreak = won ? l.titleStreak + 1 : 0;
            l.bestTitleStreak = Math.Max(l.bestTitleStreak, l.titleStreak);
            if (l.titleStreak == 2) FranchiseLife.Headline(f, Loc.T("BACK-TO-BACK CHAMPIONS") + "!");
            if (l.titleStreak >= 3) FranchiseLife.Headline(f, Loc.T("DYNASTY") + ": " + l.titleStreak + " " + Loc.T("titles in a row") + "!");

            var row = table.Find(r => r.TeamId == Franchise.TeamId(f.you));
            int finish = 0;
            if (won) finish = 3;
            else if (SeasonEngine.FinalGame(f.season)?.Involves(Franchise.TeamId(f.you)) == true) finish = 2;
            else if (f.season.games.Exists(g => g.round == 1 && g.Involves(Franchise.TeamId(f.you)))) finish = 1;
            var sb = new StringBuilder();
            sb.Append(Loc.T("YEAR")).Append(' ').Append(f.year).Append(": ").Append(row?.Wins ?? 0).Append('-').Append(row?.Losses ?? 0)
              .Append("  ").Append(Loc.T(Franchise.FinishName(finish)));
            var mine = f.awards.FindAll(a => a.year == f.year && a.team == f.you && a.kind != "ALL");
            if (mine.Count > 0)
                sb.Append("  ·  ").Append(string.Join(", ", mine.ConvertAll(a => Loc.T(FranchiseLeague.AwardName(a.kind)) + " " + a.name)));
            if (!won && !string.IsNullOrEmpty(championId)) sb.Append("  ·  ").Append(Loc.T("champion")).Append(' ').Append(c?.Team(championId)?.abbreviation ?? "?");
            l.timeline.Add(sb.ToString());
            if (l.timeline.Count > 30) l.timeline.RemoveAt(0);
            // Long franchises stay small enough for iCloud sync: the transactions log keeps its latest 150 lines.
            if (f.moves.Count > MaxMoves) f.moves.RemoveRange(0, f.moves.Count - MaxMoves);
            CheckTrophies(f);
        }

        // ------------------------------------------------------------------ I114 scouting report

        public static List<string> ScoutingReport(FranchiseSaveData f, ContentCatalog c)
        {
            var lines = new List<string>();
            var g = Franchise.NextGame(f);
            if (g == null) return lines;
            string you = Franchise.TeamId(f.you);
            string oppId = g.homeId == you ? g.awayId : g.homeId;
            int opp = Franchise.IndexOf(oppId);
            if (opp < 0) return lines;
            float us = Franchise.Strength(f, f.you), them = Franchise.Strength(f, opp);
            float edge = us + (g.homeId == you ? 1.5f : -1.5f) - them;
            float chance = 1f / (1f + (float)Math.Exp(-edge / 4f));
            lines.Add(Loc.T("ODDS") + ": " + (int)Math.Round(chance * 100f) + "% " + Loc.T("to win") + (opp == FrontOffice.Of(f).rival ? "  ·  " + Loc.T("RIVAL") : ""));
            var r = Franchise.Roster(f, opp);
            FrPlayer star = null;
            foreach (var p in r) if (p.injured == 0 && (star == null || p.Ppg > star.Ppg || (star.gp == 0 && p.Overall > star.Overall))) star = p;
            if (star != null) lines.Add(Loc.T("DANGER MAN") + ": " + star.Name + " (" + star.Overall + (star.gp > 0 ? ", " + star.Ppg.ToString("0.0") + " PPG" : "") + ")");
            var hurt = FranchiseLeague.Injured(f, opp);
            if (hurt.Count > 0) lines.Add(Loc.T("OUT") + ": " + string.Join(", ", hurt.ConvertAll(p => p.Short)));
            var h = HeadToHead(f, opp);
            if (h.wins + h.losses > 0) lines.Add(Loc.T("ALL-TIME vs them") + ": " + h.wins + "-" + h.losses);
            return lines;
        }

        // ------------------------------------------------------------------ I115 recap, I120 head-to-head (after each of your games)

        public static void AfterYourGame(FranchiseSaveData f, ContentCatalog c, int yours, int theirs, int opponent)
        {
            var l = L(f);
            if (opponent >= 0 && opponent < Franchise.Teams)
            {
                while (l.h2hWins.Count < Franchise.Teams) l.h2hWins.Add(0);
                while (l.h2hLosses.Count < Franchise.Teams) l.h2hLosses.Add(0);
                if (yours > theirs) l.h2hWins[opponent]++;
                else l.h2hLosses[opponent]++;
            }
            string club = c?.Team(Franchise.TeamId(opponent))?.abbreviation ?? "?";
            FranchiseLife.Headline(f, (yours > theirs ? Loc.T("WIN") : Loc.T("LOSS")) + " " + yours + "-" + theirs + " vs " + club);
            CheckTrophies(f);
        }

        public static (int wins, int losses) HeadToHead(FranchiseSaveData f, int team)
        {
            var l = L(f);
            int w = team >= 0 && team < l.h2hWins.Count ? l.h2hWins[team] : 0;
            int lo = team >= 0 && team < l.h2hLosses.Count ? l.h2hLosses[team] : 0;
            return (w, lo);
        }

        // ------------------------------------------------------------------ I116 training camp

        /// <summary>Why the camp isn't open now, or null. Once per preseason, players 25 and under.</summary>
        public static string CannotCamp(FranchiseSaveData f, FrPlayer p)
        {
            if (f.phase != FranchisePhase.Preseason) return "Training camp is in the preseason.";
            if (L(f).campYear == f.year) return "Camp is done for this year.";
            if (p == null || p.team != f.you) return "Not on your club.";
            if (p.age > 25) return "Camp is for players 25 and under.";
            return null;
        }

        /// <summary>One young player's camp: +2 to his two weakest ratings (never past 99 or his potential's reach).</summary>
        public static bool Camp(FranchiseSaveData f, int playerId)
        {
            var p = Franchise.Player(f, playerId);
            if (CannotCamp(f, p) != null) return false;
            var types = new List<AttributeType>((AttributeType[])Enum.GetValues(typeof(AttributeType)));
            types.Sort((a, b) => p.attrs.Get(a).CompareTo(p.attrs.Get(b)));
            for (int i = 0; i < 2 && i < types.Count; i++) p.attrs = p.attrs.With(types[i], RatingScale.Clamp(p.attrs.Get(types[i]) + 2));
            p.potential = Math.Max(p.potential, p.Overall);
            L(f).campYear = f.year;
            FrontOffice.AddMood(p, 4);
            FranchiseLife.Headline(f, Loc.T("TRAINING CAMP") + ": " + p.Name + " " + Loc.T("put in the work"));
            return true;
        }

        // ------------------------------------------------------------------ I117 next season's payroll

        /// <summary>What's already owed next season (contracts that run past this one, extensions at their new price).</summary>
        public static int NextSeasonPayroll(FranchiseSaveData f)
        {
            int sum = 0;
            foreach (var p in Franchise.Roster(f, f.you))
            {
                if (p.extension > 0) sum += p.extension;
                else if (p.years >= 1) sum += p.salary;
            }
            return sum;
        }

        // ------------------------------------------------------------------ I118 auto off-season

        /// <summary>
        /// For people who just want to play the games: drafts the best prospect it can see (scouted midpoint plus upside),
        /// re-signs players worth keeping that fit, fills the roster from interested free agents, trims extras and starts
        /// the season. Stops early if something needs you (fired, or a roster it can't make legal). Returns what it did.
        /// </summary>
        public static List<string> AutoOffSeason(FranchiseSaveData f, ContentCatalog c)
        {
            var log = new List<string>();
            if (L(f).fired) { log.Add(Loc.T("Pick your next job first.")); return log; }
            for (int guard = 0; guard < 64 && f.phase == FranchisePhase.Draft; guard++)
            {
                Franchise.DraftUntilYou(f);
                if (f.phase != FranchisePhase.Draft) break;
                if (Franchise.OnTheClock(f) != f.you) continue;
                FrPlayer best = null;
                float bestScore = float.MinValue;
                foreach (var p in Franchise.Prospects(f))
                {
                    Franchise.ScoutedRange(p, out int lo, out int hi);
                    float s = (lo + hi) / 2f + (p.age <= 19 ? 3f : 0f);
                    if (s > bestScore) { bestScore = s; best = p; }
                }
                if (best == null || !Franchise.Pick(f, best.id)) break;
                log.Add(Loc.T("DRAFTED") + ": " + best.Name);
            }
            if (f.phase == FranchisePhase.ReSign)
            {
                var expiring = Franchise.Expiring(f);
                expiring.Sort((a, b) => Franchise.Value(b).CompareTo(Franchise.Value(a)));
                foreach (var p in expiring)
                {
                    int ask = FrontOffice.ReSignSalary(p, Franchise.Demand(p));
                    bool fits = !FranchiseLife.CapOn(f) || Franchise.Payroll(f, f.you) - p.salary + ask <= Franchise.Cap;
                    if (Franchise.Value(p) >= 6f && fits && Franchise.ReSign(f, p.id)) log.Add(Loc.T("RE-SIGNED") + ": " + p.Name);
                }
                Franchise.FinishReSign(f);
            }
            if (f.phase == FranchisePhase.FreeAgency)
            {
                foreach (var p in Franchise.FreeAgents(f))
                {
                    if (f.teams[f.you].roster.Count >= Franchise.MinRoster + 1) break;
                    if (Franchise.CannotSign(f, p) == null && Franchise.SignFreeAgent(f, p.id)) log.Add(Loc.T("SIGNED") + ": " + p.Name);
                }
                Franchise.FinishFreeAgency(f, c);
            }
            if (f.phase == FranchisePhase.Preseason)
            {
                while (Franchise.CannotStart(f) != null && f.teams[f.you].roster.Count > Franchise.MaxRoster)
                {
                    var r = Franchise.Roster(f, f.you);
                    r.Sort((a, b) => Franchise.Value(a).CompareTo(Franchise.Value(b)));
                    var cut = r.Find(x => !x.created) ?? r[0];
                    if (!Franchise.Release(f, cut.id)) break;
                    log.Add(Loc.T("RELEASED") + ": " + cut.Name);
                }
                string why = Franchise.CannotStart(f);
                if (why == null && Franchise.StartSeason(f, c)) log.Add(Loc.T("SEASON") + " " + f.year + " " + Loc.T("is under way."));
                else if (why != null) log.Add(why);
            }
            return log;
        }

        // ------------------------------------------------------------------ I119 team stats

        /// <summary>Points for and against per game, with your league rank in each (1 = best).</summary>
        public static (float pf, float pa, int pfRank, int paRank) TeamStats(FranchiseSaveData f, int team)
        {
            var table = SeasonEngine.Standings(f.season);
            var me = table.Find(r => r.TeamId == Franchise.TeamId(team));
            if (me == null || me.Games == 0) return (0f, 0f, 0, 0);
            float pf = me.PointsFor / (float)me.Games, pa = me.PointsAgainst / (float)me.Games;
            int pfRank = 1, paRank = 1;
            foreach (var r in table)
            {
                if (r == me || r.Games == 0) continue;
                if (r.PointsFor / (float)r.Games > pf) pfRank++;
                if (r.PointsAgainst / (float)r.Games < pa) paRank++;
            }
            return (pf, pa, pfRank, paRank);
        }

        // ------------------------------------------------------------------ I121 the assistant GM

        public static List<string> Tips(FranchiseSaveData f, int max = 3)
        {
            var tips = new List<string>();
            var o = FrontOffice.Of(f);
            var r = Franchise.Roster(f, f.you);
            if (L(f).fired) tips.Add("Take one of the jobs on offer to keep going.");
            if (o.huddle) tips.Add("The post-game huddle is waiting: owner for credits, staff for mood, fans for hype.");
            if (FranchiseLife.CapOn(f) && Franchise.Payroll(f, f.you) > Franchise.Cap) tips.Add("You're over the cap: free agents above the minimum are off the table.");
            int unhappy = r.FindAll(p => FrontOffice.Morale(p) < 35).Count;
            if (unhappy > 0) tips.Add(unhappy + " " + Loc.T("unhappy player(s): start them, win, or use the huddle and the lounge."));
            if (f.phase == FranchisePhase.Regular && Franchise.TradesOpen(f))
            {
                var expiringStarter = r.Find(p => p.years == 0 && r.IndexOf(p) < 5 && FranchiseLeague.CannotExtend(f, p) == null);
                if (expiringStarter != null) tips.Add(Loc.T("Extend") + " " + expiringStarter.Name + " " + Loc.T("before the deadline, while he's happy."));
            }
            for (int i = 0; i < FrontOffice.FacilityNames.Length && tips.Count < max + 1; i++)
            {
                int cost = FrontOffice.UpgradeCost(f, (Facility)i);
                if (cost > 0 && o.credits >= cost) { tips.Add(Loc.T("You can afford the") + " " + Loc.T(FrontOffice.FacilityNames[i]) + " " + Loc.T("upgrade in OFFICE.")); break; }
            }
            if (FrontOffice.Captain(f) == null && r.Count > 0) tips.Add("Name a captain in OFFICE: a happy one takes the sting out of losses.");
            if (f.phase == FranchisePhase.Preseason && L(f).campYear != f.year && r.Exists(p => p.age <= 25)) tips.Add("Send a young player to TRAINING CAMP on the ROSTER tab.");
            if (tips.Count > max) tips.RemoveRange(max, tips.Count - max);
            return tips;
        }

        // ------------------------------------------------------------------ I122 award races

        public static readonly string[] Races = { "MVP", "ROOKIE", "DEFENSE" };

        public static List<FrPlayer> Race(FranchiseSaveData f, string kind, int count = 3)
        {
            var table = SeasonEngine.Standings(f.season);
            var list = f.players.FindAll(p => p.team >= 0 && p.gp > 0 && (kind != "ROOKIE" || p.draftYear == f.year || p.draftYear == f.year - 1 && p.seasons == 0));
            float Score(FrPlayer p)
            {
                if (kind == "DEFENSE") return (FranchiseLeague.PerGame(p, 3) + FranchiseLeague.PerGame(p, 4)) * 4f + p.attrs.defense * 0.1f;
                var rec = table.Find(r => r.TeamId == Franchise.TeamId(p.team));
                return p.Ppg * 2f + p.Overall * 0.5f + (rec?.WinPct ?? 0f) * 20f;
            }
            list.Sort((a, b) =>
            {
                int k = Score(b).CompareTo(Score(a));
                return k != 0 ? k : a.id.CompareTo(b.id);
            });
            if (list.Count > count) list.RemoveRange(count, list.Count - count);
            return list;
        }

        // ------------------------------------------------------------------ I123 share your season

        public static string ShareText(FranchiseSaveData f, ContentCatalog c)
        {
            var sb = new StringBuilder();
            var club = c?.Team(Franchise.TeamId(f.you));
            var row = YourRow(f);
            sb.Append("RETRO HOOPS · FRANCHISE\n");
            sb.Append(club?.FullName?.ToUpperInvariant() ?? "?").Append(" · ").Append(Loc.T("YEAR")).Append(' ').Append(f.year).Append('\n');
            if (row != null && row.Games > 0) sb.Append(Loc.T("RECORD")).Append(' ').Append(row.Wins).Append('-').Append(row.Losses).Append('\n');
            sb.Append(Loc.T("TITLES")).Append(' ').Append(f.titles).Append(" · ").Append(Loc.T(FranchiseLeague.Rank(L(f).legacy))).Append('\n');
            FrPlayer star = null;
            foreach (var p in Franchise.Roster(f, f.you)) if (star == null || p.Overall > star.Overall) star = p;
            if (star != null) sb.Append(Loc.T("FRANCHISE PLAYER")).Append(": ").Append(star.Name).Append(" (").Append(star.Overall).Append(")\n");
            int earned = 0;
            foreach (var t in Trophies) if (Earned(f, t)) earned++;
            sb.Append(Loc.T("TROPHIES")).Append(' ').Append(earned).Append('/').Append(Trophies.Length);
            return sb.ToString();
        }
    }
}
