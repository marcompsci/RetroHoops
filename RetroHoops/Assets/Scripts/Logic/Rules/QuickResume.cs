using System;

namespace CallerRetroBall.Logic
{
    /// <summary>A Quick Call game that was interrupted (the app went to the background and may have been closed).</summary>
    [Serializable]
    public class SuspendedGame
    {
        public string homeTeamId, awayTeamId, courtId, difficultyId, houseRules;
        public int scoreA, scoreB;
        public float clock;
        public int day;
    }

    /// <summary>
    /// 16 Oct 2026 improvement phase I11 PICK UP WHERE YOU LEFT OFF. Reviews complain about losing a game to a phone
    /// call or a closed app. When a Quick Call game goes to the background, its teams, court, difficulty, house rules,
    /// score and clock are saved; PLAY then offers RESUME, which tips off again from that score and clock (the box
    /// score starts fresh). Finishing or leaving the game clears it, and it keeps for a week.
    /// </summary>
    public static class QuickResume
    {
        public const int KeepDays = 7;

        public static bool Supports(GameMode mode, bool linked, bool demo) => mode == GameMode.QuickCall && !linked && !demo;

        public static SuspendedGame Capture(MatchRequest r, int scoreA, int scoreB, float clock, string houseRules, int day)
        {
            if (r == null) return null;
            return new SuspendedGame
            {
                homeTeamId = r.HomeTeamId, awayTeamId = r.AwayTeamId, courtId = r.CourtId, difficultyId = r.DifficultyId,
                houseRules = houseRules ?? "", scoreA = Math.Max(0, scoreA), scoreB = Math.Max(0, scoreB), clock = Math.Max(0f, clock), day = day,
            };
        }

        /// <summary>Still worth offering: both teams still exist, it's recent, and nobody had already won.</summary>
        public static bool Valid(SuspendedGame g, ContentCatalog c, int today)
        {
            if (g == null || c == null) return false;
            if (c.Team(g.homeTeamId) == null || c.Team(g.awayTeamId) == null) return false;
            if (today - g.day > KeepDays || g.day > today) return false;
            var rules = HouseRules.Apply(c.Find(c.Rules, DefaultContent.DefaultRulesId), HouseRules.Parse(g.houseRules, c.Find(c.Rules, DefaultContent.DefaultRulesId)));
            return Scoring.Evaluate(g.scoreA, g.scoreB, rules.useGameClock ? Math.Max(0.01f, g.clock) : 1f, rules) == GameOverReason.None;
        }

        public static MatchRequest Request(SuspendedGame g, ContentCatalog c)
        {
            var baseRules = c.Find(c.Rules, DefaultContent.DefaultRulesId);
            var rules = HouseRules.Apply(baseRules, HouseRules.Parse(g.houseRules, baseRules));
            return new MatchRequest
            {
                Mode = GameMode.QuickCall,
                HomeTeamId = g.homeTeamId,
                AwayTeamId = g.awayTeamId,
                CourtId = g.courtId,
                DifficultyId = g.difficultyId,
                RulesOverride = rules,
                StartScoreA = g.scoreA,
                StartScoreB = g.scoreB,
                StartClock = rules.useGameClock ? Math.Max(1f, g.clock) : 0f,
            };
        }

        /// <summary>"RESUME: CRW 14 - 11 VLT  ·  1:12 LEFT".</summary>
        public static string Label(SuspendedGame g, ContentCatalog c)
        {
            var a = c?.Team(g?.homeTeamId);
            var b = c?.Team(g?.awayTeamId);
            if (a == null || b == null) return Loc.T("RESUME");
            int secs = (int)Math.Ceiling(g.clock);
            string clock = secs > 0 ? "  ·  " + (secs / 60) + ":" + (secs % 60).ToString("00") + " " + Loc.T("LEFT") : "";
            return Loc.T("RESUME") + ": " + a.abbreviation + " " + g.scoreA + " - " + g.scoreB + " " + b.abbreviation + clock;
        }
    }
}
