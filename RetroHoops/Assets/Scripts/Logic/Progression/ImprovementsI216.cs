using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public enum ScoreFlowChange { None = 0, LeadChange = 1, Tie = 2 }

    /// <summary>
    /// I218–I221 (21 Nov 2026): how a game's score moved. Lead changes, ties, each side's biggest lead (which is also the
    /// biggest hole the other side was in). The simulation feeds it the score after every step.
    /// </summary>
    public sealed class ScoreFlow
    {
        public int LeadChanges { get; private set; }
        public int Ties { get; private set; }
        /// <summary>Biggest lead team 0 / team 1 had at any point.</summary>
        public readonly int[] BiggestLead = new int[2];
        /// <summary>-1 tied, 0 or 1 = who leads now.</summary>
        public int Leader { get; private set; } = -1;
        /// <summary>The last team that held a lead (ties don't reset it), for counting lead changes.</summary>
        private int _lastLeader = -1;
        private int _a = -1, _b = -1;

        public ScoreFlow(int startA = 0, int startB = 0)
        {
            _a = startA;
            _b = startB;
            Leader = startA == startB ? -1 : startA > startB ? 0 : 1;
            _lastLeader = Leader;
            if (Leader >= 0) BiggestLead[Leader] = Math.Abs(startA - startB);
        }

        /// <summary>The score now. Returns what changed (a new leader, or a tie).</summary>
        public ScoreFlowChange Observe(int a, int b)
        {
            if (a == _a && b == _b) return ScoreFlowChange.None;
            _a = a;
            _b = b;
            int leader = a == b ? -1 : a > b ? 0 : 1;
            if (leader >= 0) BiggestLead[leader] = Math.Max(BiggestLead[leader], Math.Abs(a - b));
            var change = ScoreFlowChange.None;
            if (leader != Leader)
            {
                if (leader < 0) { Ties++; change = ScoreFlowChange.Tie; }
                else if (_lastLeader >= 0 && _lastLeader != leader) { LeadChanges++; change = ScoreFlowChange.LeadChange; }
                else change = ScoreFlowChange.LeadChange; // first lead of the game
            }
            Leader = leader;
            if (leader >= 0) _lastLeader = leader;
            return change;
        }
    }

    /// <summary>I228: GAME POINT, one bucket from winning (to-the-target games only).</summary>
    public static class GamePoint
    {
        public static bool At(GameRulesDef r, int score, int other)
        {
            if (r == null || r.targetScore <= 0 || score >= r.targetScore) return false;
            foreach (int pts in new[] { r.insideArcPoints, r.beyondArcPoints })
            {
                if (pts <= 0) continue;
                int after = score + pts;
                if (r.bustRule ? after != r.targetScore : after < r.targetScore) continue;
                if (r.winByTwo && after - other < 2) continue;
                return true;
            }
            return false;
        }
    }

    /// <summary>I216 + I217: you against one team, from your recent games (the last 20 the career keeps).</summary>
    public static class HeadToHead
    {
        public static void Record(CareerSaveData d, string oppId, out int wins, out int losses)
        {
            wins = losses = 0;
            if (d?.history == null || string.IsNullOrEmpty(oppId)) return;
            foreach (var g in d.history)
            {
                if (g.opponentId != oppId || g.scoreFor == g.scoreAgainst) continue;
                if (g.Won) wins++; else losses++;
            }
        }

        /// <summary>"YOUR LAST 20: 3-1 vs THEM", or null when you haven't played them lately.</summary>
        public static string Line(CareerSaveData d, string oppId)
        {
            Record(d, oppId, out int w, out int l);
            if (w + l == 0) return null;
            return Loc.T("RECENT RECORD vs THEM") + ": " + w + "-" + l;
        }

        /// <summary>The tip-off line for a rematch, or null: a revenge game, or a winning run against them.</summary>
        public static string TipLine(CareerSaveData d, string oppId)
        {
            if (d?.history == null || string.IsNullOrEmpty(oppId)) return null;
            int straight = 0;
            bool first = true, lostLast = false;
            foreach (var g in d.history)
            {
                if (g.opponentId != oppId) continue;
                if (first) { lostLast = !g.Won; first = false; if (lostLast) break; }
                if (g.Won) straight++; else break;
            }
            if (first) return null;
            if (lostLast) return Loc.T("REMATCH: THEY GOT YOU LAST TIME");
            return straight >= 3 ? straight + " " + Loc.T("STRAIGHT WINS vs THEM") : null;
        }
    }

    /// <summary>I223 + I224: your form and your week, for the PLAY menu.</summary>
    public static class Form
    {
        public const int Games = 5;

        /// <summary>"FORM  W W L W W" (newest first), or null with fewer than 3 games.</summary>
        public static string Line(CareerSaveData d)
        {
            if (d?.history == null || d.history.Count < 3) return null;
            var parts = new List<string>();
            for (int i = 0; i < Math.Min(Games, d.history.Count); i++) parts.Add(d.history[i].Won ? "W" : "L");
            return Loc.T("FORM") + "  " + string.Join(" ", parts);
        }

        /// <summary>"THIS WEEK: 5 GAMES · 4-1 · 62 PTS" (the last 7 days), or null when you haven't played this week.</summary>
        public static string Week(CareerSaveData d, int today)
        {
            if (d?.history == null) return null;
            int games = 0, wins = 0, pts = 0;
            foreach (var g in d.history)
            {
                if (g.day > today || g.day <= today - 7) continue;
                games++;
                if (g.Won) wins++;
                pts += g.points;
            }
            if (games == 0) return null;
            return Loc.T("THIS WEEK") + ": " + games + " " + Loc.T(games == 1 ? "GAME" : "GAMES") + "  ·  " + wins + "-" + (games - wins) + "  ·  " + pts + " PTS";
        }
    }

    /// <summary>I220–I222, I231–I233: what the post-game calls out about your game.</summary>
    public static class GameCallouts
    {
        public const int ComebackDeficit = 6;

        public static int Tens(PlayerStatLine s)
        {
            if (s == null) return 0;
            int n = 0;
            foreach (int v in new[] { s.points, s.assists, s.rebounds, s.steals, s.blocks }) if (v >= 10) n++;
            return n;
        }

        public static bool Perfect(PlayerStatLine s) => s != null && s.fieldGoalsAttempted >= 5 && s.fieldGoalsMade == s.fieldGoalsAttempted;

        /// <summary>The biggest hole you climbed out of to win (0 = not a comeback win).</summary>
        public static int Comeback(MatchSummary s) =>
            s != null && s.HumanWon && s.humanTrailedBy >= ComebackDeficit ? s.humanTrailedBy : 0;

        public static List<string> For(MatchSummary s)
        {
            var list = new List<string>();
            if (s == null) return list;
            var line = s.HumanLine?.stats;
            int comeback = Comeback(s);
            if (comeback > 0) list.Add(Loc.T("COMEBACK WIN") + ": " + Loc.T("DOWN BY") + " " + comeback);
            int tens = Tens(line);
            if (tens >= 3) list.Add(Loc.T("TRIPLE-DOUBLE!"));
            else if (tens == 2) list.Add(Loc.T("DOUBLE-DOUBLE"));
            if (Perfect(line)) list.Add(Loc.T("PERFECT FROM THE FIELD") + " (" + line.fieldGoalsMade + "/" + line.fieldGoalsAttempted + ")");
            if (s.leadChanges > 0 || s.biggestLeadFor > 0 || s.biggestLeadAgainst > 0)
                list.Add(Loc.T("LEAD CHANGES") + " " + s.leadChanges + "  ·  " + Loc.T("YOUR BIGGEST LEAD") + " " + s.biggestLeadFor
                         + "  ·  " + Loc.T("THEIRS") + " " + s.biggestLeadAgainst);
            return list;
        }

        /// <summary>Copies the flow of a finished game into its summary (the human's side).</summary>
        public static void Fill(MatchSummary s, ScoreFlow f)
        {
            if (s == null || f == null) return;
            int me = s.humanTeam == 1 ? 1 : 0;
            s.leadChanges = f.LeadChanges;
            s.biggestLeadFor = f.BiggestLead[me];
            s.biggestLeadAgainst = f.BiggestLead[1 - me];
            s.humanTrailedBy = f.BiggestLead[1 - me];
        }
    }

    /// <summary>I225–I227: more rows for CAREER STATS (game records, hot and cold spots, your most-played opponent).</summary>
    public static class CareerStatsMore
    {
        public static List<(string label, string value)> Rows(CareerSaveData d, ContentCatalog c)
        {
            var rows = new List<(string, string)>();
            if (d == null) return rows;
            var t = d.totals ?? new CareerTotals();
            rows.Add(("COMEBACK WINS", t.comebacks.ToString()));
            rows.Add(("DOUBLE-DOUBLES", t.doubleDoubles.ToString()));
            rows.Add(("PERFECT SHOOTING GAMES", t.perfectGames.ToString()));
            var r = d.records;
            if (r != null)
            {
                rows.Add(("BEST GAME: POINTS", r.points.ToString()));
                rows.Add(("BEST GAME: ASSISTS", r.assists.ToString()));
                rows.Add(("BEST GAME: REBOUNDS", r.rebounds.ToString()));
                rows.Add(("BEST GAME: STEALS", r.steals.ToString()));
                rows.Add(("BEST GAME: BLOCKS", r.blocks.ToString()));
                rows.Add(("BEST GAME: POSTERS", r.posters.ToString()));
                rows.Add(("BIGGEST WIN", r.biggestWin.ToString()));
                rows.Add(("BIGGEST COMEBACK", r.biggestComeback.ToString()));
                rows.Add(("BEST WIN STREAK", r.bestWinStreak.ToString()));
            }
            if (HotSpot.Best(d.shotChart, out var hot, out float hotPct))
                rows.Add(("HOT SPOT", Loc.T(ShotZones.SpotName(hot)).ToUpperInvariant() + "  " + (int)Math.Round(hotPct * 100f) + "%"));
            if (ColdSpot(d.shotChart, out var cold, out float coldPct) && (!HotSpot.Best(d.shotChart, out hot, out _) || cold != hot))
                rows.Add(("COLD SPOT", Loc.T(ShotZones.SpotName(cold)).ToUpperInvariant() + "  " + (int)Math.Round(coldPct * 100f) + "%"));
            string most = MostPlayed(d, out int games);
            if (most != null)
            {
                HeadToHead.Record(d, most, out int w, out int l);
                rows.Add(("MOST PLAYED (LAST 20)", (c?.Team(most)?.FullName ?? most).ToUpperInvariant() + "  " + w + "-" + l));
            }
            return rows;
        }

        public static bool ColdSpot(ShotChartData chart, out ShotSpot spot, out float pct)
        {
            spot = default;
            pct = 0f;
            if (chart?.spots == null) return false;
            bool found = false;
            for (int i = 0; i < chart.spots.Length; i++)
            {
                var s = chart.spots[i];
                if (s == null || s.attempted < HotSpot.MinAttempts) continue;
                float p = s.made / (float)s.attempted;
                if (!found || p < pct) { found = true; pct = p; spot = (ShotSpot)i; }
            }
            return found;
        }

        public static string MostPlayed(CareerSaveData d, out int games)
        {
            games = 0;
            if (d?.history == null) return null;
            var count = new Dictionary<string, int>();
            string best = null;
            foreach (var g in d.history)
            {
                if (string.IsNullOrEmpty(g.opponentId)) continue;
                count.TryGetValue(g.opponentId, out int n);
                count[g.opponentId] = ++n;
                if (n > games) { games = n; best = g.opponentId; }
            }
            return games >= 2 ? best : null;
        }
    }

    /// <summary>I236: the score situation for the pause menu ("UP 4", "TIED", "DOWN 2").</summary>
    public static class Situation
    {
        public static string Line(int mine, int theirs)
        {
            if (mine == theirs) return Loc.T("TIED") + " " + mine + "-" + theirs;
            return Loc.T(mine > theirs ? "UP BY" : "DOWN BY") + " " + Math.Abs(mine - theirs) + "  (" + mine + "-" + theirs + ")";
        }
    }

    /// <summary>I230: "4 COURTS" / "6 GEAR ITEMS" on a shop card.</summary>
    public static class ShopCounts
    {
        public static string Line(ShopProduct p)
        {
            if (p?.Items == null || p.Items.Length == 0) return null;
            if (p.Kind == ShopKind.CourtPack) return p.Items.Length + " " + Loc.T(p.Items.Length == 1 ? "COURT" : "COURTS");
            if (p.Kind == ShopKind.GearPack) return p.Items.Length + " " + Loc.T("GEAR ITEMS");
            return null;
        }
    }
}
