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
            return s;
        }
    }
}
