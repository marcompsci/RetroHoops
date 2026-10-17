using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// 16 Oct 2026 compliance gate (App Review 1.2): BLOCK and REPORT for LIVE opponents (players Game Center found).
    /// Block is kept on this device only: a blocked player's quick chat is hidden at once, and Retro Hoops won't start a
    /// Live game or a rematch with them again (matchmaking drops them and looks again). Report opens an email to the
    /// developer with the opponent's Game Center name and player id filled in; the player reads it and sends it
    /// themselves, and nothing is sent by the game. There is no moderation server.
    /// </summary>
    public static class LiveSafety
    {
        /// <summary>Most blocked players kept (the oldest drops off).</summary>
        public const int MaxBlocked = 200;
        public const string ReportEmail = "rodlonbell16@gmail.com";

        public static bool IsBlocked(LiveSaveData l, string playerId) =>
            l?.blocked != null && !string.IsNullOrEmpty(playerId) && l.blocked.Contains(playerId);

        /// <summary>Blocks a player. False if there's no id or they were already blocked.</summary>
        public static bool Block(LiveSaveData l, string playerId)
        {
            if (l == null || string.IsNullOrEmpty(playerId)) return false;
            if (l.blocked == null) l.blocked = new List<string>();
            if (l.blocked.Contains(playerId)) return false;
            l.blocked.Add(playerId);
            while (l.blocked.Count > MaxBlocked) l.blocked.RemoveAt(0);
            return true;
        }

        public static void UnblockAll(LiveSaveData l) => l?.blocked?.Clear();

        public static int BlockedCount(LiveSaveData l) => l?.blocked?.Count ?? 0;

        /// <summary>Cleans a saved block list: no blanks or repeats, newest <see cref="MaxBlocked"/> kept.</summary>
        public static List<string> Sanitize(List<string> ids)
        {
            var list = new List<string>();
            if (ids == null) return list;
            foreach (var id in ids)
                if (!string.IsNullOrEmpty(id) && id.Length <= 128 && !list.Contains(id)) list.Add(id);
            while (list.Count > MaxBlocked) list.RemoveAt(0);
            return list;
        }

        /// <summary>The mailto: link REPORT PLAYER opens (subject and body filled in, the player adds what happened).</summary>
        public static string ReportMailto(string opponentName, string opponentId, string appVersion, DateTime utcNow)
        {
            string body = "Retro Hoops LIVE player report\n" +
                          "Player: " + (string.IsNullOrEmpty(opponentName) ? "?" : TextSafety.Plain(opponentName, 40)) + "\n" +
                          "Game Center player id: " + (string.IsNullOrEmpty(opponentId) ? "?" : opponentId) + "\n" +
                          "App version: " + (appVersion ?? "?") + "\n" +
                          "When (UTC): " + utcNow.ToString("yyyy-MM-dd HH:mm") + "\n" +
                          "---\nWhat happened:\n";
            return "mailto:" + ReportEmail + "?subject=" + Uri.EscapeDataString("Retro Hoops LIVE report") + "&body=" + Uri.EscapeDataString(body);
        }
    }
}
