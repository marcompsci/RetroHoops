using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// 16 Oct 2026 improvement phase I13 NEMESIS: the team that has beaten you most in your recent games (at least
    /// <see cref="MinLosses"/> times, and more often than you've beaten them) becomes your NEMESIS. The tip-off card
    /// says so, and beating them pays a bonus (play SP, as always) and clears the grudge.
    /// </summary>
    public static class Nemesis
    {
        public const int MinLosses = 2;
        public const int BonusSP = 50;

        public struct Info
        {
            public string TeamId;
            public int Wins, Losses;
            public bool Exists => !string.IsNullOrEmpty(TeamId);
        }

        public static Info Find(List<MatchHistoryEntry> history)
        {
            var best = new Info();
            if (history == null) return best;
            var tally = new Dictionary<string, (int w, int l)>();
            foreach (var h in history)
            {
                if (h == null || string.IsNullOrEmpty(h.opponentId)) continue;
                tally.TryGetValue(h.opponentId, out var t);
                if (h.Won) t.w++; else if (h.scoreFor < h.scoreAgainst) t.l++;
                tally[h.opponentId] = t;
            }
            foreach (var kv in tally)
            {
                var (w, l) = kv.Value;
                if (l < MinLosses || l <= w) continue;
                if (!best.Exists || l > best.Losses || (l == best.Losses && w < best.Wins) || (l == best.Losses && w == best.Wins && string.CompareOrdinal(kv.Key, best.TeamId) < 0))
                    best = new Info { TeamId = kv.Key, Wins = w, Losses = l };
            }
            return best;
        }

        /// <summary>"NEMESIS: THEY'VE BEATEN YOU 3 TIMES (YOU'VE WON 1)".</summary>
        public static string Line(Info n) =>
            !n.Exists ? null : Loc.T("NEMESIS") + ": " + Loc.T("THEY'VE BEATEN YOU") + " " + n.Losses + "x  (" + Loc.T("YOU'VE WON") + " " + n.Wins + ")";
    }
}
