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
            };
        }

        // A position that isn't a real number is saved as "none" (MiniJson would write it as 0, a real place).
        private static double Finite(float v) => float.IsNaN(v) || float.IsInfinity(v) ? -1.0 : v;

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
            foreach (var id in StrList(o, "seen"))
                if (CityDistricts.Find(id) != null && !s.seen.Contains(id)) s.seen.Add(id);
            return s;
        }
    }
}
