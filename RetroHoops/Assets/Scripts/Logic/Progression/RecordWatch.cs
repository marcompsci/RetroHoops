using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// 16 Oct 2026 improvement phase I12 RECORD WATCH: the moment you pass one of your one-game records (points,
    /// assists, boards, steals, blocks, greens) the game tells you, live, once per record per game. Records under
    /// <see cref="MinRecord"/> stay quiet so a new player isn't told about every bucket. The saved records themselves
    /// still update after the game, as before.
    /// </summary>
    public static class RecordWatch
    {
        public const int MinRecord = 3;

        /// <summary>Records broken since the last call (adds them to <paramref name="announced"/>).</summary>
        public static List<string> Check(CareerRecords r, PlayerStatLine s, HashSet<string> announced, bool fullCourt)
        {
            var list = new List<string>();
            if (r == null || s == null || announced == null) return list;
            void Watch(string key, int now, int record)
            {
                if (record < MinRecord || now <= record || announced.Contains(key)) return;
                announced.Add(key);
                list.Add(Loc.T("NEW CAREER HIGH") + ": " + now + " " + Loc.T(key));
            }
            if (!fullCourt) Watch("PTS", s.points, r.points); // Full Court's 2s and 3s don't compare with the half-court record
            Watch("AST", s.assists, r.assists);
            Watch("REB", s.rebounds, r.rebounds);
            Watch("STL", s.steals, r.steals);
            Watch("BLK", s.blocks, r.blocks);
            Watch("GREENS", s.greenReleases, r.greens);
            return list;
        }

        public static bool Applies(GameMode mode) =>
            mode != GameMode.Practice && mode != GameMode.Tutorial && mode != GameMode.Versus && mode != GameMode.Demo && mode != GameMode.Clutch;
    }
}
