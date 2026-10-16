using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public static partial class SaveCodec
    {
        // 16 Oct 2026: BALLER CITY (where you are in the city). Old saves have none and start at home.
        private static object EncodeCity(CitySaveData s)
        {
            s = s ?? new CitySaveData();
            return new Dictionary<string, object>
            {
                ["d"] = s.district ?? CityDistricts.FirstCallers,
                ["x"] = Finite(s.x), ["y"] = Finite(s.y), ["f"] = s.facing,
                ["court"] = s.lastCourt ?? "", ["ob"] = s.onboarding, ["v"] = s.visits,
                ["seen"] = Strings(s.seen ?? new List<string>()), ["fps30"] = s.fps30,
                ["md"] = Strings(s.missionsDone ?? new List<string>()), ["cb"] = Strings(s.crewsBeaten ?? new List<string>()),
                ["cw"] = Strings(s.courtsWon ?? new List<string>()), ["hw"] = Strings(s.holidayWins ?? new List<string>()),
                ["tt"] = Strings(s.talkedTo ?? new List<string>()), ["g"] = s.cityGames, ["w"] = s.cityWins,
                ["bp"] = s.bestPoints, ["b3"] = s.bestThrees, ["ba"] = s.bestAssists, ["bs"] = s.bestStops, ["bk"] = s.bestAnkles, ["bm"] = s.bestMargin,
                ["buy"] = s.purchases, ["cut"] = s.haircuts, ["ft"] = s.fastTravels, ["free"] = s.freeCutUsed, ["wx"] = s.weatherOff,
            };
        }

        // A position that isn't a real number is saved as "none" (MiniJson would write it as 0, a real place).
        private static double Finite(float v) => float.IsNaN(v) || float.IsInfinity(v) ? -1.0 : v;

        // 17 Oct 2026: CASHED OUT.
        private static object EncodeStreetCash(StreetCashSaveData s)
        {
            s = s ?? new StreetCashSaveData();
            return new Dictionary<string, object>
            {
                ["cash"] = (double)s.cash, ["earned"] = (double)s.earned, ["g"] = s.games, ["w"] = s.wins, ["st"] = s.streak, ["best"] = s.bestStreak,
                ["big"] = s.bigGames, ["top"] = s.topScorer, ["sp"] = s.bonusSp, ["heat"] = (double)s.heat,
                ["ach"] = Strings(s.achievements ?? new List<string>()),
            };
        }

        private static StreetCashSaveData DecodeStreetCash(Dictionary<string, object> o)
        {
            var s = new StreetCashSaveData();
            if (o == null) return s;
            double cash = Num(o, "cash", 0), earned = Num(o, "earned", 0), heat = Num(o, "heat", 0);
            s.cash = double.IsNaN(cash) || double.IsInfinity(cash) ? 0 : (long)Math.Max(0, Math.Min(1e12, cash));
            s.earned = double.IsNaN(earned) || double.IsInfinity(earned) ? 0 : (long)Math.Max(0, Math.Min(1e12, earned));
            s.earned = Math.Max(s.earned, s.cash);
            s.games = Math.Max(0, Int(o, "g", 0));
            s.wins = Math.Max(0, Math.Min(s.games, Int(o, "w", 0)));
            s.streak = Math.Max(0, Math.Min(s.wins, Int(o, "st", 0)));
            s.bestStreak = Math.Max(s.streak, Math.Min(s.wins, Int(o, "best", 0)));
            s.bigGames = Math.Max(0, Math.Min(s.games, Int(o, "big", 0)));
            s.topScorer = Math.Max(0, Math.Min(s.games, Int(o, "top", 0)));
            s.bonusSp = Math.Max(0, Int(o, "sp", 0));
            s.heat = double.IsNaN(heat) ? 0f : (float)Math.Max(0, Math.Min(1, heat));
            foreach (var id in StrList(o, "ach"))
                if (Array.Exists(StreetCash.Milestones, m => m.Id == id) && !s.achievements.Contains(id)) s.achievements.Add(id);
            return s;
        }

        // 17 Oct 2026 (phases 5–7).
        private static object EncodeBigTime(CityBigTimeSaveData s)
        {
            s = s ?? new CityBigTimeSaveData();
            var p = s.dailyProgress ?? new int[3];
            return new Dictionary<string, object>
            {
                ["ar"] = s.arenaRound, ["ad"] = s.arenaDay, ["add"] = s.arenaDoneDay, ["at"] = s.arenaTitles, ["af"] = s.arenaFinals,
                ["dd"] = s.dailyDay, ["d0"] = p.Length > 0 ? p[0] : 0, ["d1"] = p.Length > 1 ? p[1] : 0, ["d2"] = p.Length > 2 ? p[2] : 0,
                ["dp"] = s.dailyPaid, ["ds"] = s.dailySets, ["cd"] = s.calloutDay, ["cw"] = s.calloutsWon,
            };
        }

        private static CityBigTimeSaveData DecodeBigTime(Dictionary<string, object> o)
        {
            var s = new CityBigTimeSaveData();
            if (o == null) return s;
            s.arenaRound = Math.Max(0, Math.Min(2, Int(o, "ar", 0)));
            s.arenaDay = Int(o, "ad", -1);
            s.arenaDoneDay = Int(o, "add", -1);
            s.arenaTitles = Math.Max(0, Int(o, "at", 0));
            s.arenaFinals = Math.Max(s.arenaTitles, Int(o, "af", 0));
            s.dailyDay = Int(o, "dd", -1);
            s.dailyProgress = new[] { Math.Max(0, Int(o, "d0", 0)), Math.Max(0, Int(o, "d1", 0)), Math.Max(0, Int(o, "d2", 0)) };
            s.dailyPaid = Bool(o, "dp", false);
            s.dailySets = Math.Max(0, Int(o, "ds", 0));
            s.calloutDay = Int(o, "cd", -1);
            s.calloutsWon = Math.Max(0, Int(o, "cw", 0));
            return s;
        }

        private static CitySaveData DecodeCity(Dictionary<string, object> o)
        {
            var s = new CitySaveData();
            if (o == null) return s;
            string d = Str(o, "d", CityDistricts.FirstCallers) ?? CityDistricts.FirstCallers;
            s.district = CityDistricts.Find(d) != null ? d : CityDistricts.FirstCallers;
            double x = Num(o, "x", -1), y = Num(o, "y", -1);
            // A position that isn't a real number, or is off the map, falls back to the district's spawn.
            bool ok = !double.IsNaN(x) && !double.IsNaN(y) && !double.IsInfinity(x) && !double.IsInfinity(y) && x >= 0 && y >= 0 && x < 512 && y < 512;
            s.x = ok ? (float)x : -1f;
            s.y = ok ? (float)y : -1f;
            s.facing = Math.Max(0, Math.Min(3, Int(o, "f", 0)));
            s.lastCourt = Clip(Str(o, "court", "") ?? "");
            s.onboarding = Math.Max(0, Math.Min(CityOnboarding.Steps, Int(o, "ob", 0)));
            s.visits = Math.Max(0, Int(o, "v", 0));
            s.fps30 = Bool(o, "fps30", false);
            // Missions and crews: only known ids, no repeats; counters never negative.
            foreach (var id in StrList(o, "md")) if (CityMissions.Find(id) != null && !s.missionsDone.Contains(id)) s.missionsDone.Add(id);
            foreach (var id in StrList(o, "cb")) if (CityCrews.Find(id) != null && !s.crewsBeaten.Contains(id)) s.crewsBeaten.Add(id);
            foreach (var id in StrList(o, "cw")) if (!string.IsNullOrEmpty(id) && s.courtsWon.Count < 64 && !s.courtsWon.Contains(id)) s.courtsWon.Add(Clip(id));
            foreach (var id in StrList(o, "hw")) if (CityMissions.IsHolidayCourt(id) && !s.holidayWins.Contains(id)) s.holidayWins.Add(id);
            foreach (var id in StrList(o, "tt")) if (!string.IsNullOrEmpty(id) && s.talkedTo.Count < 64 && !s.talkedTo.Contains(id)) s.talkedTo.Add(Clip(id));
            s.cityGames = Math.Max(0, Int(o, "g", 0));
            s.cityWins = Math.Max(0, Math.Min(s.cityGames, Int(o, "w", 0)));
            s.bestPoints = Math.Max(0, Int(o, "bp", 0));
            s.bestThrees = Math.Max(0, Int(o, "b3", 0));
            s.bestAssists = Math.Max(0, Int(o, "ba", 0));
            s.bestStops = Math.Max(0, Int(o, "bs", 0));
            s.bestAnkles = Math.Max(0, Int(o, "bk", 0));
            s.bestMargin = Math.Max(0, Int(o, "bm", 0));
            s.purchases = Math.Max(0, Int(o, "buy", 0));
            s.haircuts = Math.Max(0, Int(o, "cut", 0));
            s.fastTravels = Math.Max(0, Int(o, "ft", 0));
            s.freeCutUsed = Bool(o, "free", false);
            s.weatherOff = Bool(o, "wx", false);
            foreach (var id in StrList(o, "seen"))
                if (CityDistricts.Find(id) != null && !s.seen.Contains(id)) s.seen.Add(id);
            return s;
        }
    }
}
