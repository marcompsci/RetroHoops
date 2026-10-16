using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// 16 Oct 2026 improvement phase I4 TALE OF THE TAPE (players ask for story moments and rivalries, not just a
    /// score): the tip-off card adds your recent series with tonight's opponent, how the last meeting went, their
    /// danger player and one KEY TO THE GAME read from their roster. Drawn from your saved recent games only.
    /// </summary>
    public static class TaleOfTheTape
    {
        public struct Series
        {
            public int Wins, Losses;
            /// <summary>The most recent meeting (null if none in your recent games).</summary>
            public MatchHistoryEntry Last;
            public int Games => Wins + Losses;
        }

        /// <summary>Your record against a team in your saved recent games (newest first, as kept).</summary>
        public static Series Against(List<MatchHistoryEntry> history, string opponentId)
        {
            var s = new Series();
            if (history == null || string.IsNullOrEmpty(opponentId)) return s;
            foreach (var h in history)
            {
                if (h == null || h.opponentId != opponentId) continue;
                if (s.Last == null) s.Last = h;
                if (h.Won) s.Wins++;
                else if (h.scoreFor < h.scoreAgainst) s.Losses++;
            }
            return s;
        }

        /// <summary>"RECENT SERIES 3-1  ·  LAST TIME: W 21-18" / "FIRST MEETING".</summary>
        public static string SeriesLine(Series s)
        {
            if (s.Last == null) return Loc.T("FIRST MEETING");
            string last = (s.Last.Won ? "W " : "L ") + s.Last.scoreFor + "-" + s.Last.scoreAgainst;
            string run = s.Wins > s.Losses ? Loc.T("YOU LEAD") : s.Wins < s.Losses ? Loc.T("THEY LEAD") : Loc.T("TIED");
            return Loc.T("RECENT SERIES") + " " + s.Wins + "-" + s.Losses + " (" + run + ")  ·  " + Loc.T("LAST TIME") + ": " + last;
        }

        /// <summary>Their best player by overall rating (the one to stop).</summary>
        public static PlayerDef DangerMan(IList<PlayerDef> roster)
        {
            PlayerDef best = null;
            int bestScore = int.MinValue;
            if (roster == null) return null;
            foreach (var p in roster)
            {
                if (p == null) continue;
                var a = p.attributes;
                int score = a.finishing + a.shooting + a.playmaking + a.defense + a.rebounding + a.speed + a.clutch;
                if (score > bestScore) { bestScore = score; best = p; }
            }
            return best;
        }

        /// <summary>One read on how to beat them, from what their roster does best on average.</summary>
        public static string Key(IList<PlayerDef> roster)
        {
            if (roster == null || roster.Count == 0) return null;
            float shoot = 0, finish = 0, play = 0, def = 0, reb = 0, speed = 0;
            foreach (var p in roster)
            {
                if (p == null) continue;
                var a = p.attributes;
                shoot += a.shooting; finish += a.finishing; play += a.playmaking; def += a.defense; reb += a.rebounding; speed += a.speed;
            }
            var keys = new (float v, string text)[]
            {
                (shoot, "KEY: CLOSE OUT ON THEIR SHOOTERS"),
                (finish, "KEY: WALL OFF THE PAINT"),
                (play, "KEY: JUMP THE PASSING LANES"),
                (def, "KEY: MOVE THE BALL, THEY LOCK UP HANDLERS"),
                (reb, "KEY: BOX OUT, THEY CRASH THE GLASS"),
                (speed, "KEY: GET BACK, THEY RUN"),
            };
            int best = 0;
            for (int i = 1; i < keys.Length; i++) if (keys[i].v > keys[best].v) best = i;
            return Loc.T(keys[best].text);
        }

        /// <summary>The full card lines (series, danger man, key), or null when there's no opponent to read.</summary>
        public static string Card(List<MatchHistoryEntry> history, string opponentId, IList<PlayerDef> roster)
        {
            if (string.IsNullOrEmpty(opponentId)) return null;
            var lines = new List<string> { SeriesLine(Against(history, opponentId)) };
            var danger = DangerMan(roster);
            if (danger != null)
            {
                string n = string.IsNullOrEmpty(danger.lastName) ? danger.firstName : danger.lastName;
                lines.Add(Loc.T("WATCH") + ": #" + danger.jerseyNumber + " " + (n ?? "").ToUpperInvariant());
            }
            string key = Key(roster);
            if (key != null) lines.Add(key);
            return string.Join("\n", lines);
        }
    }
}
