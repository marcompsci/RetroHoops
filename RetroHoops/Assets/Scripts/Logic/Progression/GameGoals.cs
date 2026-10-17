using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public enum GoalKind { Points = 0, Assists = 1, Rebounds = 2, Steals = 3, Win = 4, WinBy = 5, Greens = 6, CleanHands = 7 }

    public struct GameGoal
    {
        public GoalKind Kind;
        public int Target;
        public int Reward;
    }

    /// <summary>
    /// 16 Oct 2026 improvement phase I18 GAME GOALS: three small goals for your own game, shown on the tip-off card
    /// ("8 PTS · 2 AST · WIN"), checked on the result screen, paid in play SP (+25 more for all three). The same game
    /// always gets the same goals (seeded), and Full Court targets scale for its longer game and 2s and 3s.
    /// </summary>
    public static class GameGoals
    {
        public const int AllThreeBonus = 25;

        public static bool Applies(GameMode mode) =>
            mode != GameMode.Practice && mode != GameMode.Tutorial && mode != GameMode.Versus && mode != GameMode.Demo && mode != GameMode.Clutch;

        public static List<GameGoal> Pick(uint seed, bool fullCourt)
        {
            float k = fullCourt ? 1.6f : 1f;
            var pool = new List<GameGoal>
            {
                new GameGoal { Kind = GoalKind.Points, Target = (int)Math.Round((6 + seed % 5) * k), Reward = 15 },
                new GameGoal { Kind = GoalKind.Assists, Target = (int)Math.Round((2 + (seed / 5) % 2) * k), Reward = 15 },
                new GameGoal { Kind = GoalKind.Rebounds, Target = (int)Math.Round((2 + (seed / 11) % 3) * k), Reward = 10 },
                new GameGoal { Kind = GoalKind.Steals, Target = (int)Math.Round(1 * k + (seed / 17) % 2), Reward = 15 },
                new GameGoal { Kind = GoalKind.Win, Target = 1, Reward = 10 },
                new GameGoal { Kind = GoalKind.WinBy, Target = (int)Math.Round((4 + (seed / 23) % 3) * k), Reward = 20 },
                new GameGoal { Kind = GoalKind.Greens, Target = 1 + (int)((seed / 29) % 2), Reward = 15 },
                new GameGoal { Kind = GoalKind.CleanHands, Target = 1, Reward = 15 },
            };
            var picked = new List<GameGoal>();
            uint h = seed == 0 ? 1u : seed;
            while (picked.Count < 3 && pool.Count > 0)
            {
                h = h * 1664525u + 1013904223u;
                int i = (int)((h >> 8) % (uint)pool.Count);
                var g = pool[i];
                pool.RemoveAt(i);
                // WIN and WIN BY together would be one goal twice.
                if ((g.Kind == GoalKind.Win && picked.Exists(p => p.Kind == GoalKind.WinBy)) || (g.Kind == GoalKind.WinBy && picked.Exists(p => p.Kind == GoalKind.Win))) continue;
                picked.Add(g);
            }
            return picked;
        }

        public static bool Met(GameGoal g, PlayerStatLine s, bool won, int margin)
        {
            if (s == null) return false;
            switch (g.Kind)
            {
                case GoalKind.Points: return s.points >= g.Target;
                case GoalKind.Assists: return s.assists >= g.Target;
                case GoalKind.Rebounds: return s.rebounds >= g.Target;
                case GoalKind.Steals: return s.steals >= g.Target;
                case GoalKind.Win: return won;
                case GoalKind.WinBy: return won && margin >= g.Target;
                case GoalKind.Greens: return s.greenReleases >= g.Target;
                default: return s.turnovers <= 1; // CLEAN HANDS: one turnover at most
            }
        }

        public static string Text(GameGoal g)
        {
            switch (g.Kind)
            {
                case GoalKind.Points: return g.Target + " " + Loc.T("PTS");
                case GoalKind.Assists: return g.Target + " " + Loc.T("AST");
                case GoalKind.Rebounds: return g.Target + " " + Loc.T("REB");
                case GoalKind.Steals: return g.Target + " " + Loc.T("STL");
                case GoalKind.Win: return Loc.T("WIN");
                case GoalKind.WinBy: return Loc.T("WIN BY") + " " + g.Target;
                case GoalKind.Greens: return g.Target + " " + Loc.T("GREENS");
                default: return Loc.T("1 TURNOVER MAX");
            }
        }

        /// <summary>"GOALS: 8 PTS · 2 AST · WIN".</summary>
        public static string Card(List<GameGoal> goals)
        {
            if (goals == null || goals.Count == 0) return null;
            var t = new List<string>();
            foreach (var g in goals) t.Add(Text(g));
            return Loc.T("GOALS") + ": " + string.Join("  ·  ", t);
        }

        /// <summary>SP earned and the result line ("GOALS 2/3  ✓ 8 PTS  ✓ WIN  ✗ 2 AST  +25 SP").</summary>
        public static int Score(List<GameGoal> goals, PlayerStatLine s, bool won, int margin, out string line)
        {
            line = null;
            if (goals == null || goals.Count == 0) return 0;
            int sp = 0, met = 0;
            var parts = new List<string>();
            foreach (var g in goals)
            {
                bool ok = Met(g, s, won, margin);
                if (ok) { sp += g.Reward; met++; }
                parts.Add((ok ? "+ " : "- ") + Text(g));
            }
            if (met == goals.Count) sp += AllThreeBonus;
            line = Loc.T("GOALS") + " " + met + "/" + goals.Count + "   " + string.Join("   ", parts) + (sp > 0 ? "   +" + sp + " SP" : "");
            return sp;
        }
    }

    /// <summary>
    /// I19 REVENGE GAME: lose, hit REMATCH, and the next game is a revenge game (called on the tip-off card); win it
    /// and it pays a bonus in play SP.
    /// </summary>
    public static class Revenge
    {
        public const int BonusSP = 30;

        public static bool Applies(GameMode mode) => GameGoals.Applies(mode) && mode != GameMode.Daily;
    }

    /// <summary>
    /// I20 CAREER MILESTONES: round numbers across your whole career (games, wins, points, assists, rebounds, steals,
    /// blocks, greens) called on the result screen when a game carries you past one.
    /// </summary>
    public static class CareerMilestones
    {
        public static readonly int[] Steps = { 10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000 };

        /// <summary>The milestones crossed between two snapshots of your totals.</summary>
        public static List<string> Crossed(CareerTotals before, CareerTotals after)
        {
            var list = new List<string>();
            if (before == null || after == null) return list;
            void Check(string name, int a, int b)
            {
                for (int i = Steps.Length - 1; i >= 0; i--)
                    if (a < Steps[i] && b >= Steps[i]) { list.Add(Steps[i] + " " + Loc.T(name)); return; }
            }
            Check("CAREER GAMES", before.games, after.games);
            Check("CAREER WINS", before.wins, after.wins);
            Check("CAREER POINTS", before.points, after.points);
            Check("CAREER ASSISTS", before.assists, after.assists);
            Check("CAREER REBOUNDS", before.rebounds, after.rebounds);
            Check("CAREER STEALS", before.steals, after.steals);
            Check("CAREER BLOCKS", before.blocks, after.blocks);
            Check("CAREER GREENS", before.greens, after.greens);
            return list;
        }

        public static CareerTotals Snapshot(CareerTotals t) => t == null ? new CareerTotals() : new CareerTotals
        {
            games = t.games, wins = t.wins, points = t.points, assists = t.assists, rebounds = t.rebounds,
            steals = t.steals, blocks = t.blocks, greens = t.greens,
        };
    }
}
