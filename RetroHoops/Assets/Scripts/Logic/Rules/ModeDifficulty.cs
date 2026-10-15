using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// Phase 34: a difficulty per kind of game (Settings ► DIFFICULTY BY MODE), e.g. Legend in Rise but Rookie in The Park.
    /// "DEFAULT" uses the main DIFFICULTY. Saved as "GROUP=difficulty.id" lines in <see cref="SettingsData.modeDifficulty"/>.
    /// </summary>
    public static class ModeDifficulty
    {
        public static readonly string[] Groups = { "QUICK CALL", "RISE", "FRANCHISE", "THE PARK", "THE STREETS", "EVENTS", "LEGACY" };

        /// <summary>Which group a mode belongs to (null = not adjustable: practice, 2 Player, Live, the demo, the All-Star Game).</summary>
        public static string GroupOf(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.QuickCall: case GameMode.OneOnOne: case GameMode.FullCourt: case GameMode.Daily: return "QUICK CALL";
                case GameMode.Rise: case GameMode.Rival: return "RISE";
                case GameMode.Franchise: return "FRANCHISE";
                case GameMode.Street: return "THE PARK";
                case GameMode.Streets: return "THE STREETS";
                case GameMode.Tournament: case GameMode.King: case GameMode.Arcade: case GameMode.Cup: case GameMode.CustomCup: case GameMode.Clutch: return "EVENTS";
                case GameMode.Legacy: return "LEGACY";
                default: return null;
            }
        }

        /// <summary>The override for a group, or null for DEFAULT.</summary>
        public static string Get(SettingsData s, string group)
        {
            if (s?.modeDifficulty == null || group == null) return null;
            foreach (var line in s.modeDifficulty)
            {
                int eq = line.IndexOf('=');
                if (eq > 0 && line.Substring(0, eq) == group) return line.Substring(eq + 1);
            }
            return null;
        }

        /// <summary>Sets (or with null, clears) a group's override.</summary>
        public static void Set(SettingsData s, string group, string difficultyId)
        {
            if (s == null || group == null) return;
            s.modeDifficulty = s.modeDifficulty ?? new List<string>();
            s.modeDifficulty.RemoveAll(l => l.StartsWith(group + "=", StringComparison.Ordinal));
            if (!string.IsNullOrEmpty(difficultyId)) s.modeDifficulty.Add(group + "=" + difficultyId);
        }

        /// <summary>
        /// The difficulty a request should use: its group's override, but only when the request is using the main
        /// DIFFICULTY (requests with a fixed difficulty, like a friend's two-phone game or a Live game, keep theirs).
        /// </summary>
        public static string Resolve(SettingsData s, MatchRequest r, ContentCatalog c)
        {
            if (s == null || r == null) return r?.DifficultyId;
            if (r.ContextId == "link" || r.DifficultyId != s.difficultyId) return r.DifficultyId;
            string over = Get(s, GroupOf(r.Mode));
            if (string.IsNullOrEmpty(over) || (c != null && c.Difficulties.Find(d => d.id == over) == null)) return r.DifficultyId;
            return over;
        }
    }
}
