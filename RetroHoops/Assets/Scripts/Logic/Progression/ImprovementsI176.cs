using System;
using System.Collections.Generic;
using System.Text;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// I182 (20 Nov 2026): when Quick Call is a walkover three games running (won by 8 or more) the post-game offers the
    /// next difficulty up; three heavy losses running offer the one below. It only suggests: you tap to change it.
    /// </summary>
    public static class DifficultyNudge
    {
        public const int Margin = 8;
        public const int Run = 3;

        public static void AddGame(CareerTotals t, MatchSummary s)
        {
            if (t == null || s == null || s.mode != GameMode.QuickCall) return;
            int margin = s.HumanScore - s.OpponentScore;
            t.blowoutWins = margin >= Margin ? t.blowoutWins + 1 : 0;
            t.blowoutLosses = -margin >= Margin ? t.blowoutLosses + 1 : 0;
        }

        /// <summary>The difficulty to suggest (null = none): one step up or down the menu's list from <paramref name="current"/>.</summary>
        public static DifficultyDef Suggest(CareerTotals t, List<DifficultyDef> ladder, string current)
        {
            if (t == null || ladder == null || ladder.Count == 0) return null;
            int at = ladder.FindIndex(d => d.id == current);
            if (at < 0) return null;
            if (t.blowoutWins >= Run && at + 1 < ladder.Count) return ladder[at + 1];
            if (t.blowoutLosses >= Run && at - 1 >= 0) return ladder[at - 1];
            return null;
        }

        /// <summary>After you take (or turn down) a suggestion, the run starts again.</summary>
        public static void Reset(CareerTotals t)
        {
            if (t == null) return;
            t.blowoutWins = 0;
            t.blowoutLosses = 0;
        }
    }

    /// <summary>I183: a finished game as a few plain lines for the share sheet (no rich-text tags).</summary>
    public static class GameShare
    {
        public static string Text(MatchSummary s, string courtName, string keyPlay)
        {
            if (s == null) return "";
            var sb = new StringBuilder();
            string mine = s.humanTeam == 0 ? s.teamAName : s.teamBName, theirs = s.humanTeam == 0 ? s.teamBName : s.teamAName;
            sb.Append(s.HumanWon ? "W" : "L").Append("  ").Append(mine).Append(' ').Append(s.HumanScore)
              .Append(" - ").Append(s.OpponentScore).Append(' ').Append(theirs);
            if (!string.IsNullOrEmpty(courtName)) sb.Append("\n").Append("@ ").Append(courtName);
            var me = s.HumanLine?.stats;
            if (me != null)
            {
                sb.Append("\n").Append(me.points).Append(" PTS  ").Append(me.assists).Append(" AST  ").Append(me.rebounds).Append(" REB");
                if (me.steals > 0) sb.Append("  ").Append(me.steals).Append(" STL");
                if (me.blocks > 0) sb.Append("  ").Append(me.blocks).Append(" BLK");
                if (me.posters > 0) sb.Append("\n").Append(me.posters).Append(me.posters == 1 ? " POSTER" : " POSTERS");
            }
            if (!string.IsNullOrEmpty(keyPlay)) sb.Append("\nPLAY OF THE GAME: ").Append(keyPlay);
            sb.Append("\n\nRETRO HOOPS");
            return Strip(sb.ToString());
        }

        /// <summary>Drops anything between angle brackets (rich-text tags never go out in shared text).</summary>
        public static string Strip(string s)
        {
            var sb = new StringBuilder();
            int depth = 0;
            foreach (char ch in s ?? "")
            {
                if (ch == '<') { depth++; continue; }
                if (ch == '>') { if (depth > 0) depth--; continue; }
                if (depth == 0) sb.Append(ch);
            }
            return sb.ToString();
        }
    }

    /// <summary>I185: CAREER STATS, everything the career counts, as label/value rows.</summary>
    public static class CareerStats
    {
        public static List<(string label, string value)> Rows(CareerSaveData d)
        {
            var rows = new List<(string, string)>();
            if (d == null || d.totals == null) return rows;
            var t = d.totals;
            string Pct(int made, int tries) => tries <= 0 ? "-" : Math.Round(100.0 * made / tries) + "%";
            string Per(int n) => t.games <= 0 ? "-" : (n / (double)t.games).ToString("0.0");
            rows.Add(("GAMES", t.games.ToString()));
            rows.Add(("RECORD", t.wins + "-" + t.losses));
            rows.Add(("WIN %", Pct(t.wins, t.wins + t.losses)));
            rows.Add(("POINTS", t.points + "  (" + Per(t.points) + " " + Loc.T("a game") + ")"));
            rows.Add(("ASSISTS", t.assists.ToString()));
            rows.Add(("REBOUNDS", t.rebounds.ToString()));
            rows.Add(("FIELD GOALS", t.fieldGoalsMade + "/" + t.fieldGoalsAttempted + "  " + Pct(t.fieldGoalsMade, t.fieldGoalsAttempted)));
            rows.Add(("GREEN RELEASES", t.greens.ToString()));
            rows.Add(("BEST GREEN RUN", t.bestGreenRun.ToString()));
            rows.Add(("STEALS", t.steals + (t.stealAttempts > 0 ? "/" + t.stealAttempts + "  " + Pct(t.steals, t.stealAttempts) : "")));
            rows.Add(("BLOCKS", t.blocks.ToString()));
            rows.Add(("CHASE-DOWN BLOCKS", t.chaseDowns.ToString()));
            rows.Add(("POSTERS", t.posters.ToString()));
            rows.Add(("RIM HANGS", t.rimHangs.ToString()));
            rows.Add(("CLUTCH BUCKETS", t.clutchMakes.ToString()));
            rows.Add(("ALLEY-OOPS", t.alleyOops.ToString()));
            rows.Add(("HEAT CHECKS", t.heatUps.ToString()));
            rows.Add(("TITLES", t.championships.ToString()));
            rows.Add(("BEST DAY STREAK", d.loginBest.ToString()));
            return rows;
        }
    }

    /// <summary>I186: SURPRISE ME in Quick Call: one of the courts on offer, picked by the game's seed.</summary>
    public static class SurpriseCourt
    {
        public static string Pick(List<string> courts, uint seed)
        {
            if (courts == null) return null;
            var real = courts.FindAll(x => !string.IsNullOrEmpty(x));
            if (real.Count == 0) return null;
            var rng = new SeededRandom(seed == 0 ? 1u : seed);
            return real[rng.Range(0, real.Count)];
        }
    }

    /// <summary>I192: WEAR IT: puts on everything in a gear pack you own (jersey, shoes, banner, gear).</summary>
    public static class PackWear
    {
        public static int Wear(CareerSaveData d, ContentCatalog c, string packId)
        {
            var pack = Shop.Product(packId);
            if (d == null || c == null || pack == null || pack.Kind != ShopKind.GearPack || !Shop.Owns(d, packId)) return 0;
            int n = 0;
            var done = new HashSet<CosmeticSlot>();
            foreach (var id in pack.Items)
            {
                var item = c.Find(c.Cosmetics, id);
                if (item == null || done.Contains(item.slot)) continue;
                if (Career.Equip(d, item)) { done.Add(item.slot); n++; }
            }
            return n;
        }
    }

    /// <summary>I191: a shop court (owned or a loyalty pick) as your own team's home court.</summary>
    public static class ShopHome
    {
        public static bool Set(CareerSaveData d, string courtId)
        {
            if (d?.customTeam == null || !d.customTeam.created || !Shop.IsShopCourt(courtId) || !Shop.CourtOpen(d, courtId)) return false;
            d.customTeam.homeCourtId = courtId;
            return true;
        }

        public static bool IsHome(CareerSaveData d, string courtId) => d?.customTeam != null && d.customTeam.created && d.customTeam.homeCourtId == courtId;
    }
}
