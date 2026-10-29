using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// 28 Oct 2026, improvement I23 SPANISH STORIES: every line of BALLER WORLD's own text that's shown through
    /// <see cref="Loc.T"/> (the comic's words, goals and covers, side jobs, achievements, badges, regions and districts),
    /// so a test can check each one has its Spanish.
    /// </summary>
    public static class WorldText
    {
        public static List<string> All()
        {
            var set = new List<string>();
            void Add(string s) { if (!string.IsNullOrEmpty(s) && !set.Contains(s)) set.Add(s); }
            foreach (var issue in CityStory.Issues)
            {
                Add(issue.Title);
                Add(issue.Tagline);
                foreach (var g in issue.Goals) Add(g.Text);
                foreach (var p in issue.Intro) { Add(p.Line); Add(p.Caption); }
                foreach (var p in issue.Outro) { Add(p.Line); Add(p.Caption); }
            }
            foreach (var j in CitySideJobs.All) { Add(j.Title); Add(j.Pitch); Add(j.Thanks); }
            foreach (var a in CityAchievements.AllDefs) Add(a.Text);
            foreach (var b in CityAchievements.Badges) { Add(b.Name); Add(b.Detail); }
            foreach (var r in WorldRegions.All) Add(r.Tagline);
            foreach (var r in WorldRegions.All)
                foreach (var d in r.Districts) Add(CityDistricts.Find(d)?.Blurb);
            return set;
        }
    }
}
