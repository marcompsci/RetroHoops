using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// 16 Oct 2026 improvement phase I10 GAME GRADE: a letter grade for your own game on the result screen (A+ to F),
    /// with the two things that drove it. It rewards what helps a team (efficient scoring, assists, boards, steals,
    /// blocks, winning) and takes off for turnovers and bricks, scaled so a Full Court game (2s and 3s, longer) grades
    /// the same as a half-court one.
    /// </summary>
    public static class GameGrade
    {
        public static readonly string[] Letters = { "F", "D", "C-", "C", "C+", "B-", "B", "B+", "A-", "A", "A+" };
        /// <summary>Score needed for each letter above F.</summary>
        public static readonly float[] Steps = { 3f, 6f, 8f, 10f, 12f, 14f, 16f, 18f, 21f, 25f };

        public static float Score(PlayerStatLine s, bool won, bool fullCourt)
        {
            if (s == null) return 0f;
            float pts = fullCourt ? s.points * 0.55f : s.points;
            int missed = Math.Max(0, s.fieldGoalsAttempted - s.fieldGoalsMade);
            float v = pts + s.assists * 1.5f + s.rebounds * 0.9f + s.steals * 2f + s.blocks * 2f
                      - s.turnovers * 1.5f - missed * 0.6f + (won ? 3f : 0f);
            return Math.Max(0f, v);
        }

        public static string Letter(float score)
        {
            int i = 0;
            while (i < Steps.Length && score >= Steps[i]) i++;
            return Letters[i];
        }

        /// <summary>The two biggest reasons, good or bad ("EFFICIENT SCORING · 3 TURNOVERS").</summary>
        public static string Reasons(PlayerStatLine s, bool won)
        {
            if (s == null) return "";
            var r = new List<(float weight, string text)>();
            float fg = s.fieldGoalsAttempted > 0 ? s.fieldGoalsMade / (float)s.fieldGoalsAttempted : 0f;
            if (s.fieldGoalsAttempted >= 4 && fg >= 0.6f) r.Add((s.points, Loc.T("EFFICIENT SCORING")));
            else if (s.fieldGoalsAttempted >= 6 && fg < 0.35f) r.Add((s.fieldGoalsAttempted - s.fieldGoalsMade, Loc.T("COLD SHOOTING")));
            if (s.assists >= 3) r.Add((s.assists * 1.5f, s.assists + " " + Loc.T("ASSISTS")));
            if (s.rebounds >= 4) r.Add((s.rebounds, s.rebounds + " " + Loc.T("BOARDS")));
            if (s.steals + s.blocks >= 2) r.Add(((s.steals + s.blocks) * 2f, Loc.T("DEFENSE") + " (" + s.steals + " " + Loc.T("STL") + ", " + s.blocks + " " + Loc.T("BLK") + ")"));
            if (s.turnovers >= 3) r.Add((s.turnovers * 1.5f, s.turnovers + " " + Loc.T("TURNOVERS")));
            if (r.Count == 0) r.Add((0f, won ? Loc.T("DID YOUR JOB") : Loc.T("QUIET NIGHT")));
            r.Sort((a, b) => b.weight.CompareTo(a.weight));
            var top = new List<string>();
            for (int i = 0; i < Math.Min(2, r.Count); i++) top.Add(r[i].text);
            return string.Join("  ·  ", top);
        }

        public static string Line(PlayerStatLine s, bool won, bool fullCourt)
        {
            if (s == null) return null;
            return Loc.T("YOUR GRADE") + ": " + Letter(Score(s, won, fullCourt)) + "   " + Reasons(s, won);
        }
    }
}
