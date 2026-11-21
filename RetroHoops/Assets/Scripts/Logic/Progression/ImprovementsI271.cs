using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>I271 (21 Nov 2026): the SCOUTING REPORT at tip-off: their best player and what he does best.</summary>
    public static class Scouting
    {
        public static int Rating(PlayerDef p)
        {
            if (p == null) return 0;
            var a = p.attributes;
            return a.finishing + a.shooting + a.playmaking + a.defense + a.rebounding + a.speed;
        }

        public static string Strength(PlayerDef p)
        {
            if (p == null) return null;
            var a = p.attributes;
            var best = ("FINISHER", a.finishing);
            foreach (var x in new[] { ("SHOOTER", a.shooting), ("PLAYMAKER", a.playmaking), ("STOPPER", a.defense), ("REBOUNDER", a.rebounding), ("BURNER", a.speed) })
                if (x.Item2 > best.Item2) best = x;
            return best.Item1;
        }

        /// <summary>"WATCH OUT: #7 NOVA REYES · SHOOTER", for the best player on <paramref name="team"/> in this game.</summary>
        public static string Line(MatchSimulation m, int team)
        {
            if (m == null || team < 0 || team > 1) return null;
            PlayerDef best = null;
            foreach (var p in m.Players)
                if (p.Team == team && !p.IsHuman && (best == null || Rating(p.Def) > Rating(best))) best = p.Def;
            if (best == null) return null;
            return Loc.T("WATCH OUT") + ": #" + best.jerseyNumber + " " + best.DisplayName.ToUpperInvariant() + "  ·  " + Loc.T(Strength(best));
        }
    }

    /// <summary>I272: who led the game in points, assists and rebounds (both teams).</summary>
    public static class StatLeaders
    {
        public static string Line(MatchSummary s)
        {
            if (s?.lines == null || s.lines.Count == 0) return null;
            var parts = new List<string>();
            foreach (var (label, get) in new (string, Func<PlayerStatLine, int>)[] { ("PTS", x => x.points), ("AST", x => x.assists), ("REB", x => x.rebounds) })
            {
                SummaryLine best = null;
                foreach (var l in s.lines)
                    if (l.stats != null && (best == null || get(l.stats) > get(best.stats))) best = l;
                if (best == null || get(best.stats) <= 0) continue;
                string who = best.isHuman ? Loc.T("YOU") : (best.name ?? "?").ToUpperInvariant();
                parts.Add(label + ": " + who + " " + get(best.stats));
            }
            return parts.Count == 0 ? null : Loc.T("LEADERS") + "  " + string.Join("  ·  ", parts);
        }
    }

    /// <summary>I273: your NEMESIS (I13's rule) named on the PLAY menu, so you can go and settle it.</summary>
    public static class NemesisMenu
    {
        public static string Line(CareerSaveData d, ContentCatalog c)
        {
            var n = Nemesis.Find(d?.history);
            if (!n.Exists) return null;
            return Loc.T("YOUR NEMESIS") + ": " + (c?.Team(n.TeamId)?.FullName ?? n.TeamId).ToUpperInvariant() + "  " + n.Wins + "-" + n.Losses
                   + "  ·  +" + Nemesis.BonusSP + " SP " + Loc.T("FOR BEATING THEM");
        }
    }

    /// <summary>I274: the day's first win (the history is newest first, so the game just played is entry 0).</summary>
    public static class FirstWin
    {
        public static bool Today(CareerSaveData d)
        {
            if (d?.history == null || d.history.Count == 0 || !d.history[0].Won) return false;
            int day = d.history[0].day;
            for (int i = 1; i < d.history.Count; i++)
                if (d.history[i].day == day && d.history[i].Won) return false;
            return true;
        }
    }

    /// <summary>I275 + I278: a word from the bench after the game: ball security, and a drill for what went wrong.</summary>
    public static class BenchNotes
    {
        public const int TurnoverWarn = 4;

        public static string Turnovers(PlayerStatLine s) =>
            s != null && s.turnovers >= TurnoverWarn ? Loc.T("CARE WITH THE BALL") + ": " + s.turnovers + " " + Loc.T("TURNOVERS") : null;

        /// <summary>The Practice Lab drill that fits the game (null when it went fine).</summary>
        public static DrillKind? Drill(PlayerStatLine s)
        {
            if (s == null) return null;
            if (s.fieldGoalsAttempted >= 6 && s.fieldGoalsMade / (double)s.fieldGoalsAttempted < 0.35) return DrillKind.FreeShoot;
            if (s.turnovers >= TurnoverWarn) return DrillKind.DribbleLane;
            if (s.stealAttempts >= 5 && s.steals == 0) return DrillKind.Lockdown;
            return null;
        }

        public static string DrillLine(PlayerStatLine s)
        {
            var d = Drill(s);
            if (d == null) return null;
            string name = d == DrillKind.FreeShoot ? "FREE SHOOT" : d == DrillKind.DribbleLane ? "DRIBBLE LANE" : "LOCKDOWN";
            return Loc.T("TRY") + ": " + Loc.T("PRACTICE LAB") + " ► " + Loc.T(name);
        }
    }

    /// <summary>I277: the header of RECENT GAMES ("LAST 20: 13-7 · 11.2 PTS A GAME").</summary>
    public static class RecentSummary
    {
        public static string Line(CareerSaveData d)
        {
            if (d?.history == null || d.history.Count == 0) return null;
            int w = 0, pts = 0;
            foreach (var g in d.history) { if (g.Won) w++; pts += g.points; }
            int n = d.history.Count;
            return Loc.T("LAST") + " " + n + ": " + w + "-" + (n - w) + "  ·  "
                   + (pts / (double)n).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " " + Loc.T("PTS A GAME");
        }
    }
}
