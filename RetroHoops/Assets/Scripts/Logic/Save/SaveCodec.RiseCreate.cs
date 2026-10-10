using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public static partial class SaveCodec
    {
        // 10 Oct 2026: Rise ► CREATE A TEAMMATE.
        private static List<object> EncodeRiseMade(RiseSaveData r)
        {
            var list = new List<object>();
            if (r?.made == null) return list;
            foreach (var m in r.made)
                list.Add(new Dictionary<string, object>
                {
                    ["id"] = m.id, ["first"] = m.first, ["last"] = m.last, ["num"] = m.number, ["arch"] = m.archetypeId,
                    ["skin"] = m.skin, ["hair"] = m.hair, ["hairColor"] = m.hairColor, ["body"] = m.body, ["height"] = m.height,
                });
            return list;
        }

        private static void DecodeRiseMade(RiseSaveData r, Dictionary<string, object> o)
        {
            r.made = new List<RiseMateData>();
            int maxId = 0;
            foreach (var item in Arr(o, "made"))
            {
                if (!(item is Dictionary<string, object> m) || r.made.Count >= RiseCreate.MaxMade) continue;
                int id = Int(m, "id", 0);
                if (id <= 0 || r.made.Exists(x => x.id == id)) continue;
                r.made.Add(new RiseMateData
                {
                    id = id,
                    first = Franchise.CleanName(Str(m, "first", "")), last = Franchise.CleanName(Str(m, "last", "")),
                    number = Math.Max(0, Math.Min(99, Int(m, "num", 1))), archetypeId = Str(m, "arch", null),
                    skin = Math.Max(0, Int(m, "skin", 0)), hair = Math.Max(0, Int(m, "hair", 0)), hairColor = Math.Max(0, Int(m, "hairColor", 0)),
                    body = Math.Max(0, Int(m, "body", 1)), height = Math.Max(0, Int(m, "height", 1)),
                });
                maxId = Math.Max(maxId, id);
            }
            r.nextMade = Math.Max(maxId + 1, Int(o, "nextMade", 1));
        }
    }
}
