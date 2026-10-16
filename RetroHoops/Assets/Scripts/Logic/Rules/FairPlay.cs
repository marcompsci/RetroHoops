using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// 16 Oct 2026 improvement phase I2 FAIR PLAY. The most common complaint in App Store basketball reviews is a CPU
    /// that feels rigged ("open shots miss, theirs all go in"). Every shot here already has a make chance decided at
    /// release by the same model for both sides; this tally adds those chances up so the result screen can show what
    /// each team was expected to make against what it did make. Nothing in the game reads it back.
    /// </summary>
    public sealed class FairPlayTally
    {
        public readonly int[] Attempts = new int[2];
        public readonly int[] Makes = new int[2];
        public readonly float[] Expected = new float[2];

        public void Apply(MatchEvent e)
        {
            if (e.Team < 0 || e.Team > 1) return;
            if (e.Type == MatchEventType.ShotReleased)
            {
                Attempts[e.Team]++;
                Expected[e.Team] += Math.Max(0f, Math.Min(1f, e.Chance));
            }
            else if (e.Type == MatchEventType.ShotMade) Makes[e.Team]++;
        }

        /// <summary>Makes over (+) or under (−) what the odds said.</summary>
        public float Luck(int team) => Makes[team] - Expected[team];

        /// <summary>Within this many makes of the odds counts as "about what the odds said".</summary>
        public const float Even = 1.5f;

        /// <summary>
        /// The result-screen line: "FAIR PLAY · YOU 9/14 (ODDS SAID 8.2) · CPU 7/15 (ODDS SAID 7.4)" and a verdict on the
        /// other side's shooting. Null when nobody shot.
        /// </summary>
        public string Line(int you, string youName, string themName)
        {
            if (you < 0 || you > 1 || Attempts[0] + Attempts[1] == 0) return null;
            int them = 1 - you;
            string Side(int t, string name) => name + " " + Makes[t] + "/" + Attempts[t] + " (" + Loc.T("ODDS SAID") + " " + Expected[t].ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + ")";
            float l = Luck(them);
            string verdict = Attempts[them] == 0 ? Loc.T("THEY NEVER GOT A SHOT UP")
                : Math.Abs(l) <= Even ? Loc.T("THEY SHOT ABOUT WHAT THE ODDS SAID")
                : l > 0 ? Loc.T("THEY GOT HOT: ABOVE THE ODDS")
                : Loc.T("THEY WENT COLD: BELOW THE ODDS");
            return Loc.T("FAIR PLAY") + "  ·  " + Side(you, youName) + "  ·  " + Side(them, themName) + "\n" + verdict;
        }
    }

    /// <summary>
    /// I2 HOUSE RULES for Quick Call: game to 11 / 15 / 21, the game clock off or 2 / 3 / 4 minutes, 1s-and-2s or
    /// 2s-and-3s scoring, and win by two. Kept in Settings as a short code ("21|2|0|0") so the next game remembers.
    /// </summary>
    public static class HouseRules
    {
        public static readonly int[] Targets = { 11, 15, 21 };
        /// <summary>Clock choices in minutes (0 = no clock: first to the target wins).</summary>
        public static readonly int[] ClockMinutes = { 0, 2, 3, 4 };
        public static readonly string[] TargetNames = { "11", "15", "21" };
        public static readonly string[] ClockNames = { "OFF", "2:00", "3:00", "4:00" };
        public static readonly string[] ScoringNames = { "1s & 2s", "2s & 3s" };
        public static readonly string[] OnOff = { "OFF", "ON" };

        public struct Choice
        {
            public int Target, Clock, Scoring, WinByTwo; // indexes into the arrays above (WinByTwo 0/1)
        }

        /// <summary>The choices matching a rules set (the default game: 21, 2:00, 1s & 2s, win by one).</summary>
        public static Choice From(GameRulesDef r)
        {
            var c = new Choice();
            if (r == null) r = new GameRulesDef();
            c.Target = Nearest(Targets, r.targetScore);
            c.Clock = !r.useGameClock ? 0 : Nearest(ClockMinutes, (int)Math.Round(r.gameClockSeconds / 60f), 1);
            c.Scoring = r.insideArcPoints >= 2 ? 1 : 0;
            c.WinByTwo = r.winByTwo ? 1 : 0;
            return c;
        }

        private static int Nearest(int[] options, int value, int from = 0)
        {
            int best = from;
            for (int i = from; i < options.Length; i++)
                if (Math.Abs(options[i] - value) < Math.Abs(options[best] - value)) best = i;
            return best;
        }

        public static string Pack(Choice c) => Clamp(c.Target, Targets.Length) + "|" + Clamp(c.Clock, ClockMinutes.Length) + "|" + Clamp(c.Scoring, 2) + "|" + Clamp(c.WinByTwo, 2);

        /// <summary>Reads a saved code; anything missing or broken falls back to the default rules' choice.</summary>
        public static Choice Parse(string code, GameRulesDef defaults)
        {
            var c = From(defaults);
            if (string.IsNullOrEmpty(code)) return c;
            var parts = code.Split('|');
            if (parts.Length != 4) return c;
            var v = new int[4];
            for (int i = 0; i < 4; i++)
                if (!int.TryParse(parts[i], out v[i])) return c;
            c.Target = Clamp(v[0], Targets.Length);
            c.Clock = Clamp(v[1], ClockMinutes.Length);
            c.Scoring = Clamp(v[2], 2);
            c.WinByTwo = Clamp(v[3], 2);
            return c;
        }

        private static int Clamp(int v, int n) => v < 0 ? 0 : (v >= n ? n - 1 : v);

        /// <summary>A copy of the base rules with the house choices applied (the base is never changed).</summary>
        public static GameRulesDef Apply(GameRulesDef baseRules, Choice c)
        {
            var b = baseRules ?? new GameRulesDef();
            var r = new GameRulesDef
            {
                id = "rules.house",
                targetScore = Targets[Clamp(c.Target, Targets.Length)],
                useGameClock = ClockMinutes[Clamp(c.Clock, ClockMinutes.Length)] > 0,
                gameClockSeconds = Math.Max(60, ClockMinutes[Clamp(c.Clock, ClockMinutes.Length)] * 60),
                shotClockSeconds = b.shotClockSeconds,
                insideArcPoints = c.Scoring == 1 ? 2 : 1,
                beyondArcPoints = c.Scoring == 1 ? 3 : 2,
                winByTwo = c.WinByTwo == 1,
                checkBallAfterScore = b.checkBallAfterScore,
                suddenDeathOnTie = b.suddenDeathOnTie,
                bustRule = false,
                bustScore = b.bustScore,
                makeItTakeIt = b.makeItTakeIt,
            };
            return r;
        }

        /// <summary>One line for the Quick Call screen: "GAME TO 21 · 2:00 CLOCK · 1s & 2s".</summary>
        public static string Summary(Choice c)
        {
            var r = Apply(null, c);
            string clock = r.useGameClock ? ClockNames[Clamp(c.Clock, ClockMinutes.Length)] + " " + Loc.T("CLOCK") : Loc.T("NO CLOCK");
            return Loc.T("GAME TO") + " " + r.targetScore + "  ·  " + clock + "  ·  " + ScoringNames[Clamp(c.Scoring, 2)] + (r.winByTwo ? "  ·  " + Loc.T("WIN BY 2") : "");
        }
    }
}
