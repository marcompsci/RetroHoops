using System;
using System.Collections.Generic;
using System.Text;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// I243 (21 Nov 2026): LOCKDOWN, defensive stops in a row. A stop is one of their possessions that ends without
    /// them scoring (a miss you rebound, a steal, a block, a shot-clock violation). Called at 3 and then every 2.
    /// </summary>
    public sealed class StopTracker
    {
        public const int FirstCall = 3, Every = 2;
        public int Run { get; private set; }
        public int Best { get; private set; }
        private int _next = FirstCall;

        /// <summary>One of their possessions ended. Returns true when the run of stops just reached a call.</summary>
        public bool End(bool theyScored)
        {
            if (theyScored) { Run = 0; _next = FirstCall; return false; }
            Run++;
            Best = Math.Max(Best, Run);
            if (Run < _next) return false;
            _next += Every;
            return true;
        }
    }

    /// <summary>I244 + I245: tonight's shooting, for the post-game.</summary>
    public static class TonightShooting
    {
        /// <summary>"BEST SPOT TONIGHT: LEFT WING 3/4", the spot with the most makes (2+ tries), or null.</summary>
        public static string BestSpot(PlayerStatLine s)
        {
            var spots = s?.chart?.spots;
            if (spots == null) return null;
            int best = -1;
            for (int i = 0; i < spots.Length; i++)
            {
                var r = spots[i];
                if (r == null || r.attempted < 2 || r.made == 0) continue;
                if (best < 0 || r.made > spots[best].made || (r.made == spots[best].made && r.Percentage > spots[best].Percentage)) best = i;
            }
            if (best < 0) return null;
            return Loc.T("BEST SPOT TONIGHT") + ": " + Loc.T(ShotZones.SpotName((ShotSpot)best)).ToUpperInvariant() + " " + spots[best].made + "/" + spots[best].attempted;
        }

        /// <summary>"1.40 POINTS A SHOT" (4+ shots), or null.</summary>
        public static string PerShot(PlayerStatLine s)
        {
            if (s == null || s.fieldGoalsAttempted < 4) return null;
            return (s.points / (double)s.fieldGoalsAttempted).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " " + Loc.T("POINTS A SHOT");
        }
    }

    /// <summary>I246: player of the game, counted across the career.</summary>
    public static class PlayerOfTheGameCount
    {
        public static bool IsHuman(MatchSummary s)
        {
            var me = s?.HumanLine;
            return me != null && s.HumanWon && s.playerOfTheGame == me.playerIndex;
        }

        public static string Line(int count) =>
            count <= 0 ? null : Loc.T("PLAYER OF THE GAME") + "  ·  #" + count + " " + Loc.T("IN YOUR CAREER");
    }

    /// <summary>I247: a tip a day on the PLAY menu (the same one all day, a different one tomorrow).</summary>
    public static class DailyTip
    {
        public static readonly string[] Tips =
        {
            "Release the shot at the top of your jump for a GREEN.",
            "STEAL glows gold when the ball is in reach. Wait for it.",
            "BLOCK glows when a shooter is in range. Jump then, not before.",
            "A pump fake sends a jumping defender flying. Shoot right after.",
            "Corners are the shortest deep shot on the floor.",
            "Chase a shooter down from behind for a chase-down block.",
            "Swing the ball: a quick pass beats a set defence.",
            "Down late? A bucket in the last 5 seconds that ties or wins it is ICE COLD.",
            "Make it a habit: check CAREER STATS for your hot and cold spots.",
            "Settings ► ICONS ONLY hides the button words if you know them by heart.",
            "The MOMENTUM bar shows who's on a run. Answer back before it swings.",
            "Lost to a team? RUN IT BACK on the PLAY menu gets you the rematch.",
            "CUSTOMIZE CONTROLS lets you drag every button where your thumbs want it.",
            "Six straight points brings the crowd up. Keep the run going.",
        };

        public static string For(int day) => Loc.T("TIP OF THE DAY") + ": " + Loc.T(Tips[((day % Tips.Length) + Tips.Length) % Tips.Length]);
    }

    /// <summary>I248: the closest career milestone you haven't reached yet.</summary>
    public static class NextMilestone
    {
        public static string Line(CareerTotals t)
        {
            if (t == null) return null;
            string best = null;
            double bestFrac = double.MaxValue;
            foreach (var track in Milestones.Tracks)
            {
                int now = Milestones.ValueOf(t, track.name);
                foreach (int m in track.marks)
                {
                    if (now >= m) continue;
                    int prev = 0;
                    foreach (int x in track.marks) if (x < m) prev = x;
                    double frac = (m - now) / (double)Math.Max(1, m - prev);
                    if (frac < bestFrac) { bestFrac = frac; best = m + " " + track.name + "  (" + (m - now) + " " + Loc.T("TO GO") + ")"; }
                    break;
                }
            }
            return best == null ? null : Loc.T("NEXT MILESTONE") + ": " + best;
        }
    }

    /// <summary>I249–I253: more of the career from your recent games.</summary>
    public static class RecentGames
    {
        public static List<string> Lines(CareerSaveData d, ContentCatalog c)
        {
            var list = new List<string>();
            if (d?.history == null) return list;
            foreach (var g in d.history)
            {
                var sb = new StringBuilder();
                sb.Append(g.Won ? "W " : "L ").Append(g.scoreFor).Append('-').Append(g.scoreAgainst)
                  .Append("  ").Append(Loc.T("vs")).Append(' ').Append((c?.Team(g.opponentId)?.abbreviation ?? "?").ToUpperInvariant())
                  .Append("  ·  ").Append(g.points).Append(" PTS ").Append(g.assists).Append(" AST ").Append(g.rebounds).Append(" REB");
                if (g.steals > 0) sb.Append(' ').Append(g.steals).Append(" STL");
                if (g.blocks > 0) sb.Append(' ').Append(g.blocks).Append(" BLK");
                string court = string.IsNullOrEmpty(g.courtId) ? null : c?.Court(g.courtId)?.displayName;
                if (court != null) sb.Append("  ·  ").Append(court.ToUpperInvariant());
                list.Add(sb.ToString());
            }
            return list;
        }

        public static List<(string label, string value)> Rows(CareerSaveData d, ContentCatalog c)
        {
            var rows = new List<(string, string)>();
            if (d == null) return rows;
            var t = d.totals ?? new CareerTotals();
            string Per(int n) => t.games <= 0 ? "-" : (n / (double)t.games).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            rows.Add(("ASSISTS A GAME", Per(t.assists)));      // I262
            rows.Add(("REBOUNDS A GAME", Per(t.rebounds)));
            rows.Add(("TURNOVERS A GAME", Per(t.turnovers)));   // I276
            rows.Add(("PLAYER OF THE GAME", t.playerOfGames.ToString())); // I246
            string fav = FavoriteCourt(d, out int favGames);
            if (fav != null) rows.Add(("FAVORITE COURT (LAST 20)", (c?.Court(fav)?.displayName ?? fav).ToUpperInvariant() + "  ·  " + favGames + " " + Loc.T("GAMES")));
            string bestCourt = BestCourt(d, out int bw, out int bl);
            if (bestCourt != null) rows.Add(("BEST COURT (LAST 20)", (c?.Court(bestCourt)?.displayName ?? bestCourt).ToUpperInvariant() + "  " + bw + "-" + bl));
            string trend = Trend(d);
            if (trend != null) rows.Add(("SCORING TREND", trend));
            foreach (var m in ByMode(d)) rows.Add((Loc.T("RECORD") + ": " + m.mode, m.record));
            return rows;
        }

        public static string FavoriteCourt(CareerSaveData d, out int games)
        {
            games = 0;
            var count = new Dictionary<string, int>();
            string best = null;
            foreach (var g in d?.history ?? new List<MatchHistoryEntry>())
            {
                if (string.IsNullOrEmpty(g.courtId)) continue;
                count.TryGetValue(g.courtId, out int n);
                count[g.courtId] = ++n;
                if (n > games) { games = n; best = g.courtId; }
            }
            return games >= 2 ? best : null;
        }

        /// <summary>The court you win most on (2+ games there; ties go to more games played).</summary>
        public static string BestCourt(CareerSaveData d, out int wins, out int losses)
        {
            wins = losses = 0;
            var rec = new Dictionary<string, (int w, int l)>();
            foreach (var g in d?.history ?? new List<MatchHistoryEntry>())
            {
                if (string.IsNullOrEmpty(g.courtId)) continue;
                rec.TryGetValue(g.courtId, out var r);
                rec[g.courtId] = g.Won ? (r.w + 1, r.l) : (r.w, r.l + 1);
            }
            string best = null;
            double bestPct = -1;
            int bestGames = 0;
            foreach (var kv in rec)
            {
                int n = kv.Value.w + kv.Value.l;
                if (n < 2) continue;
                double pct = kv.Value.w / (double)n;
                if (pct > bestPct || (pct == bestPct && n > bestGames)) { bestPct = pct; best = kv.Key; bestGames = n; wins = kv.Value.w; losses = kv.Value.l; }
            }
            return best;
        }

        /// <summary>I251: your last 5 games' scoring against your career average ("12.4 vs 10.1  ·  TRENDING UP").</summary>
        public static string Trend(CareerSaveData d)
        {
            var t = d?.totals;
            if (t == null || t.games < 8 || d.history == null || d.history.Count < 5) return null;
            double recent = 0;
            for (int i = 0; i < 5; i++) recent += d.history[i].points;
            recent /= 5.0;
            double career = t.points / (double)t.games;
            string word = recent >= career + 1.0 ? "TRENDING UP" : recent <= career - 1.0 ? "TRENDING DOWN" : "STEADY";
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            return recent.ToString("0.0", inv) + " " + Loc.T("vs") + " " + career.ToString("0.0", inv) + "  ·  " + Loc.T(word);
        }

        /// <summary>I252: your record in each mode you've played lately (2+ games).</summary>
        public static List<(string mode, string record)> ByMode(CareerSaveData d)
        {
            var list = new List<(string, string)>();
            var rec = new Dictionary<GameMode, (int w, int l)>();
            foreach (var g in d?.history ?? new List<MatchHistoryEntry>())
            {
                rec.TryGetValue(g.mode, out var r);
                rec[g.mode] = g.Won ? (r.w + 1, r.l) : (r.w, r.l + 1);
            }
            foreach (var kv in rec)
                if (kv.Value.w + kv.Value.l >= 2) list.Add((ModeName(kv.Key), kv.Value.w + "-" + kv.Value.l));
            list.Sort((a, b) => string.CompareOrdinal(a.Item1, b.Item1));
            return list;
        }

        public static string ModeName(GameMode m)
        {
            switch (m)
            {
                case GameMode.QuickCall: return "QUICK CALL";
                case GameMode.Rise: return "RISE";
                case GameMode.FullCourt: return "FULL COURT";
                case GameMode.Streets: return "THE STREETS";
                default:
                    var s = m.ToString();
                    var sb = new StringBuilder();
                    for (int i = 0; i < s.Length; i++)
                    {
                        if (i > 0 && char.IsUpper(s[i])) sb.Append(' ');
                        sb.Append(char.ToUpperInvariant(s[i]));
                    }
                    return sb.ToString();
            }
        }
    }

    /// <summary>I254 CALLOUTS: how chatty the game is during play.</summary>
    public static class Callouts
    {
        public const int All = 0, Fewer = 1;
        public static readonly string[] Names = { "ALL", "BIG PLAYS ONLY" };
        public static int Clamp(int v) => v == Fewer ? Fewer : All;
        /// <summary>Minor callouts (runs, lead changes, GAME POINT, DIME, stops) are skipped on BIG PLAYS ONLY.</summary>
        public static bool Show(int setting, bool minor) => !minor || Clamp(setting) == All;
    }

    /// <summary>I255 + I256: Quick Call remembers your matchup, and RANDOM MATCHUP picks one for you.</summary>
    public static class QuickPicks
    {
        public static int IndexOf(List<TeamDef> teams, string id)
        {
            if (teams == null || string.IsNullOrEmpty(id)) return -1;
            return teams.FindIndex(t => t.id == id);
        }

        /// <summary>A team of yours and an opponent (indexes), never the same team.</summary>
        public static void Random(List<TeamDef> mine, List<TeamDef> league, uint seed, out int myIndex, out string opponentId)
        {
            myIndex = 0;
            opponentId = null;
            if (mine == null || mine.Count == 0 || league == null || league.Count == 0) return;
            var rng = new SeededRandom(seed == 0 ? 1u : seed);
            myIndex = rng.Range(0, mine.Count);
            string me = mine[myIndex].id;
            var others = league.FindAll(t => t.id != me);
            if (others.Count > 0) opponentId = others[rng.Range(0, others.Count)].id;
        }
    }

    /// <summary>I258: the SHOP's next suggestion: this week's spotlight if you don't own it, otherwise the first pack you don't.</summary>
    public static class ShopNext
    {
        public static ShopProduct For(CareerSaveData d, int week)
        {
            var spot = Shop.Spotlight(week);
            if (spot != null && !spot.Consumable && !Shop.Owns(d, spot.Id)) return spot;
            foreach (var p in Shop.Products)
                if ((p.Kind == ShopKind.GearPack || p.Kind == ShopKind.CourtPack) && !Shop.Owns(d, p.Id)) return p;
            return null;
        }

        public static string Line(CareerSaveData d, int week)
        {
            var p = For(d, week);
            return p == null ? null : Loc.T("NEXT FOR YOUR COLLECTION") + ": " + Loc.T(p.Name);
        }
    }

    /// <summary>I264: the game's length for the pause menu ("FIRST TO 21  ·  1:12 LEFT").</summary>
    public static class GameLength
    {
        public static string Line(GameRulesDef r, float clock)
        {
            if (r == null) return null;
            var parts = new List<string>();
            if (r.targetScore > 0) parts.Add(Loc.T("FIRST TO") + " " + r.targetScore);
            if (r.useGameClock)
            {
                int s = Math.Max(0, (int)Math.Ceiling(clock));
                parts.Add(s / 60 + ":" + (s % 60).ToString("00") + " " + Loc.T("LEFT"));
            }
            return parts.Count == 0 ? null : string.Join("  ·  ", parts);
        }
    }
}
