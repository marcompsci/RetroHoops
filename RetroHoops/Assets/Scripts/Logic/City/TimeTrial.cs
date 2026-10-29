using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// 28 Oct 2026, improvement I34 DAILY TIME TRIAL: one road a day from where you are (the same all day), with a
    /// par time. Drive it under par, without EASY DRIVE, for a cash prize (play money), once a day.
    /// </summary>
    public static class TimeTrial
    {
        public const float Par = 21f;
        public const long Prize = 15000;

        /// <summary>Today's trial destination from <paramref name="fromRegion"/> (an open region, not this one), or null.</summary>
        public static string Target(CareerSaveData d, string fromRegion, int day)
        {
            var open = new List<string>();
            foreach (var r in WorldRegions.All)
                if (r.Id != fromRegion && WorldRegions.Open(d, r)) open.Add(r.Id);
            if (open.Count == 0) return null;
            uint h = StableHash.Of("trial:" + day + ":" + fromRegion);
            return open[(int)(h % (uint)open.Count)];
        }

        /// <summary>Has today's prize been won already?</summary>
        public static bool Won(CityWorldSaveData w, int day) => w != null && w.trialDay == day;

        /// <summary>
        /// After a drive: pays the prize if it was today's trial, under par, without EASY DRIVE and not won yet today.
        /// Returns what it paid.
        /// </summary>
        public static long Apply(CareerSaveData d, HighwayDrive drive, bool easyDrive, int day)
        {
            var w = d?.world;
            if (w == null || drive == null || !drive.Done || easyDrive || Won(w, day)) return 0;
            if (Target(d, drive.From, day) != drive.To || drive.Time > Par) return 0;
            w.trialDay = day;
            w.trialsWon++;
            if (d.streetCash == null) d.streetCash = new StreetCashSaveData();
            d.streetCash.cash += Prize;
            d.streetCash.earned += Prize;
            return Prize;
        }

        /// <summary>The line for YOUR RIDE.</summary>
        public static string Line(CareerSaveData d, string fromRegion, int day)
        {
            string to = Target(d, fromRegion, day);
            if (to == null) return null;
            var r = WorldRegions.Find(to);
            if (Won(d?.world, day)) return Loc.T("TIME TRIAL") + ": " + Loc.T("won today. New road tomorrow.");
            return Loc.T("TIME TRIAL") + ": " + Loc.T(r.Name) + " " + Loc.T("under") + " " + Par.ToString("0") + " s  ·  " + StreetCash.Money(Prize);
        }
    }

    /// <summary>28 Oct 2026, improvement I35 WORLD STATS: your BALLER WORLD life in numbers.</summary>
    public static class WorldStats
    {
        public static List<(string label, string value)> Rows(CareerSaveData d)
        {
            var w = d?.world ?? new CityWorldSaveData();
            var rows = new List<(string, string)>
            {
                (Loc.T("REGIONS VISITED"), w.regionsSeen.Count + "/" + WorldRegions.All.Length),
                (Loc.T("WALKED"), (w.walkedTotal / 1000f).ToString("0.0") + " km"),
                (Loc.T("DRIVES"), w.drives + "  (" + w.cleanDrives + " " + Loc.T("clean") + ")"),
                (Loc.T("NEAR MISSES"), w.nearMisses.ToString()),
                (Loc.T("FASTEST DRIVE"), w.bestDrive > 0f ? w.bestDrive.ToString("0.0") + " s" : "-"),
                (Loc.T("TIME TRIALS WON"), w.trialsWon.ToString()),
                (Loc.T("SIDE JOBS"), w.sideDone.Count + "/" + CitySideJobs.All.Length),
                (Loc.T("BIG RUN TITLES"), CityBigRun.TotalTitles(w).ToString()),
                (Loc.T("BADGES"), w.badges.Count + "/" + CityAchievements.Badges.Length),
                (Loc.T("GOLD DAYS"), w.achGoldDays + "  ·  " + Loc.T("BEST STREAK") + " " + w.achBestStreak),
                (Loc.T("STORE PIECES"), RegionStore.OwnedCount(d) + "/" + RegionStore.TotalItems),
                (Loc.T("STREET CASH EARNED"), StreetCash.Money(d?.streetCash?.earned ?? 0)),
            };
            foreach (var r in WorldRegions.All)
                rows.Add((Loc.T(r.Name), Loc.T("WINS") + " " + w.RegionWins(r.Id) + "  ·  " + Loc.T("CREWS") + " " + WorldRegions.CrewsBeaten(d?.city, r) + "/" + WorldRegions.Crews(r).Count));
            foreach (var e in w.routeBest)
            {
                var m = e.Split('>', '=');
                if (m.Length != 3) continue;
                rows.Add((Loc.T(WorldRegions.Find(m[0])?.Short ?? m[0]) + " > " + Loc.T(WorldRegions.Find(m[1])?.Short ?? m[1]), m[2].Length > 0 ? m[2].Substring(0, Math.Min(m[2].Length, 5)) + " s" : "-"));
            }
            return rows;
        }
    }
}
