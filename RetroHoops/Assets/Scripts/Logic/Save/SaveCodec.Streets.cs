using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public static partial class SaveCodec
    {
        // 15 Oct 2026: THE STREETS record.
        private static object EncodeStreets(StreetsSaveData s)
        {
            s = s ?? new StreetsSaveData();
            return new Dictionary<string, object>
            {
                ["g"] = s.games, ["w"] = s.wins, ["l"] = s.losses, ["ss"] = s.showstoppers, ["tr"] = s.tricks, ["bank"] = s.bestBank,
                ["streak"] = s.streak, ["best"] = s.bestStreak, ["opp"] = s.lastOpponent ?? "", ["court"] = s.lastCourt ?? "",
                ["beaten"] = Strings(s.tourBeaten ?? new List<string>()), ["stars"] = Strings(s.tourStars ?? new List<string>()), ["rep"] = s.rep,
                ["recruits"] = Strings(s.recruits ?? new List<string>()), ["crew"] = Strings(s.crew ?? new List<string>()),
                ["lw"] = s.liveWins, ["ll"] = s.liveLosses,
            };
        }

        private static StreetsSaveData DecodeStreets(Dictionary<string, object> o)
        {
            var s = new StreetsSaveData();
            if (o == null) return s;
            s.games = Math.Max(0, Int(o, "g", 0));
            s.wins = Math.Max(0, Int(o, "w", 0));
            s.losses = Math.Max(0, Int(o, "l", 0));
            s.showstoppers = Math.Max(0, Int(o, "ss", 0));
            s.tricks = Math.Max(0, Int(o, "tr", 0));
            s.bestBank = Math.Max(0, Int(o, "bank", 0));
            s.streak = Math.Max(0, Int(o, "streak", 0));
            s.bestStreak = Math.Max(s.streak, Int(o, "best", 0));
            s.lastOpponent = Clip(Str(o, "opp", "") ?? "");
            s.lastCourt = Clip(Str(o, "court", "") ?? "");
            // Phase 2: the tour. Only crews and leaders that exist are kept.
            foreach (var id in StrList(o, "beaten"))
                if (StreetsTour.Find(id) != null && !s.tourBeaten.Contains(id)) s.tourBeaten.Add(id);
            foreach (var line in StrList(o, "stars"))
            {
                int eq = line.IndexOf('=');
                if (eq > 0 && StreetsTour.Find(line.Substring(0, eq)) != null && int.TryParse(line.Substring(eq + 1), out int n) && n >= 0 && n <= 3)
                    s.tourStars.Add(line);
            }
            s.rep = Math.Max(0, Int(o, "rep", 0));
            foreach (var id in StrList(o, "recruits"))
                if (Array.Exists(StreetsTour.Stops, t => t.LeaderId == id) && !s.recruits.Contains(id)) s.recruits.Add(id);
            foreach (var id in StrList(o, "crew"))
                if (s.crew.Count < 2) s.crew.Add(s.recruits.Contains(id) ? id : "");
            s.liveWins = Math.Max(0, Int(o, "lw", 0));
            s.liveLosses = Math.Max(0, Int(o, "ll", 0));
            return s;
        }
    }
}
