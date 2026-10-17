using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// 16 Oct 2026 improvement phase I17 YOUR WEEK: a line at the top of PLAY with the last seven days from your
    /// saved recent games and moments: record, best scoring game, and big moments.
    /// </summary>
    public static class WeekInReview
    {
        public const int Days = 7;

        public static string Line(List<MatchHistoryEntry> history, List<MomentEntry> moments, int today)
        {
            int w = 0, l = 0, best = -1;
            if (history != null)
                foreach (var h in history)
                {
                    if (h == null || today - h.day >= Days || h.day > today) continue;
                    if (h.Won) w++; else if (h.scoreFor < h.scoreAgainst) l++;
                    if (h.points > best) best = h.points;
                }
            if (w + l == 0) return null;
            int big = 0;
            if (moments != null)
                foreach (var m in moments)
                    if (m != null && today - m.day < Days && m.day <= today) big++;
            var parts = new List<string> { Loc.T("YOUR WEEK") + ": " + w + "-" + l };
            if (best > 0) parts.Add(Loc.T("BEST") + " " + best + " " + Loc.T("PTS"));
            if (big > 0) parts.Add(big + " " + Loc.T(big == 1 ? "BIG MOMENT" : "BIG MOMENTS"));
            return string.Join("  ·  ", parts);
        }
    }
}
