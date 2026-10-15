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
                ["hc"] = s.homeCourt ?? "", ["hn"] = s.homeName, ["hd"] = s.homeDefenses, ["hs"] = s.homeStreak, ["hb"] = s.homeBest,
                ["hl"] = s.homeLosses, ["ht"] = s.homeTaken, ["htk"] = s.homeTaker ?? "",
                ["chem"] = Strings(s.chem ?? new List<string>()), ["gear"] = s.gear ?? "", ["sig"] = s.signature,
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
            // Phase 3: HOMECOURT, chemistry, gear and signature. (A court id that's gone is checked when the game loads it.)
            s.homeCourt = Clip(Str(o, "hc", "") ?? "");
            s.homeName = Math.Max(0, Math.Min(StreetsHome.Names.Length - 1, Int(o, "hn", 0)));
            s.homeDefenses = Math.Max(0, Int(o, "hd", 0));
            s.homeStreak = Math.Max(0, Int(o, "hs", 0));
            s.homeBest = Math.Max(s.homeStreak, Int(o, "hb", 0));
            s.homeLosses = Math.Max(0, Int(o, "hl", 0));
            s.homeTaker = Clip(Str(o, "htk", "") ?? "");
            if (StreetsTour.Find(s.homeTaker) == null) s.homeTaker = "";
            s.homeTaken = Bool(o, "ht", false) && s.homeTaker != "";
            foreach (var line in StrList(o, "chem"))
            {
                int eq = line.LastIndexOf('=');
                if (eq > 0 && s.recruits.Contains(line.Substring(0, eq)) && int.TryParse(line.Substring(eq + 1), out int n) && n >= 0
                    && !s.chem.Exists(l => l.StartsWith(line.Substring(0, eq + 1), StringComparison.Ordinal)))
                    s.chem.Add(line);
            }
            s.gear = Clip(Str(o, "gear", "") ?? "");
            if (StreetsGear.Find(s.gear) == null) s.gear = "";
            s.signature = Int(o, "sig", -1);
            if (s.signature < -1 || s.signature >= Dunks.Names.Length) s.signature = -1;
            return s;
        }
    }
}
