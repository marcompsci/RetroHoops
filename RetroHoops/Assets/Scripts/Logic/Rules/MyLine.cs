using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// 16 Oct 2026 improvement phase I8 MY LINE: players asked to see their own numbers while they play (shooting
    /// percentage especially). A one-line live box score for the player you control, under the score bar
    /// (Settings ► LIVE STAT LINE).
    /// </summary>
    public static class MyLine
    {
        public static string Text(PlayerStatLine s)
        {
            if (s == null) return "";
            var parts = new List<string>
            {
                s.points + " " + Loc.T("PTS"),
                s.fieldGoalsMade + "/" + s.fieldGoalsAttempted + " " + Loc.T("FG"),
            };
            if (s.arcAttempted > 0) parts.Add(s.arcMade + "/" + s.arcAttempted + " " + Loc.T("DEEP"));
            if (s.assists > 0) parts.Add(s.assists + " " + Loc.T("AST"));
            if (s.rebounds > 0) parts.Add(s.rebounds + " " + Loc.T("REB"));
            if (s.steals > 0) parts.Add(s.steals + " " + Loc.T("STL"));
            if (s.blocks > 0) parts.Add(s.blocks + " " + Loc.T("BLK"));
            return string.Join("  ·  ", parts);
        }
    }
}
