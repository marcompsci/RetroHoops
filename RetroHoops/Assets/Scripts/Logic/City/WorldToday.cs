using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// 28 Oct 2026, improvement I28 TODAY: a card at the top of PLAY that says what BALLER WORLD has for you today:
    /// the comic's next step, today's achievements and your streak, the Big Runs you can still enter and the side job
    /// you've taken. Nothing until you've walked into the city once.
    /// </summary>
    public static class WorldToday
    {
        public static List<string> Lines(CareerSaveData d, int day)
        {
            var lines = new List<string>();
            var w = d?.world;
            if (w == null || !w.started) return lines;
            var issue = CityStory.Current(w);
            string ticker = CityStory.Ticker(d);
            lines.Add(issue == null ? Loc.T("THE COMIC") + ": " + Loc.T("ALL-WORLD")
                                    : Loc.T("THE COMIC") + " #" + issue.Number + ": " + (ticker ?? Loc.T(issue.Title)));
            int done = w.achDay == day ? CityAchievements.DoneCount(w, day) : 0;
            int streak = CityAchievements.StreakNow(w, day);
            string tier = w.achDay == day && w.achTierPaid > 0 ? "  " + Loc.T(CityAchievements.TierNames[Math.Min(w.achTierPaid, CityAchievements.TierNames.Length) - 1]) : "";
            lines.Add(Loc.T("ACHIEVEMENTS") + " " + done + "/" + CityAchievements.PerDay + tier + "  ·  " + Loc.T("STREAK") + " " + streak);
            int runs = 0;
            foreach (var r in WorldRegions.All)
                if (CityBigRun.Locked(d, r.Id) == null && CityBigRun.NextRound(w, r.Id, day) > 0) runs++;
            var job = CitySideJobs.Active(w);
            var third = new List<string>();
            if (runs > 0) third.Add(Loc.T("BIG RUNS OPEN") + " " + runs);
            if (job != null) third.Add(Loc.T("SIDE JOB") + ": " + Loc.T(job.Title));
            if (third.Count > 0) lines.Add(string.Join("  ·  ", third));
            return lines;
        }
    }
}
