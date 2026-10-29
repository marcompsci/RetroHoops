using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public static partial class SaveCodec
    {
        // 28 Oct 2026 (BALLER WORLD): the comic, the guide, achievements, side jobs, Big Runs and the drive. Old saves
        // have none and start at issue #1; every value is checked (unknown ids dropped, numbers clamped).
        private static object EncodeWorld(CityWorldSaveData w)
        {
            w = w ?? new CityWorldSaveData();
            var ints = new List<object>();
            foreach (var n in w.issuesOpened ?? new List<int>()) ints.Add(n);
            var prog = new List<object>();
            foreach (var n in w.achProgress ?? new int[CityAchievements.PerDay]) prog.Add(n);
            var found = new List<object>();
            foreach (var n in w.sideFound ?? new List<int>()) found.Add(n);
            return new Dictionary<string, object>
            {
                ["is"] = w.issue, ["st"] = w.step, ["sb"] = (double)w.stepBase, ["io"] = ints, ["go"] = w.started, ["vb"] = w.velvetBeaten, ["po"] = w.pendingOutro,
                ["tr"] = w.tracking ?? "",
                ["ad"] = w.achDay, ["ap"] = prog, ["at"] = w.achTierPaid, ["as"] = w.achStreak, ["ab"] = w.achBestStreak, ["al"] = w.achLastDay,
                ["ag"] = w.achGoldDays, ["an"] = w.achTotal, ["bd"] = Strings(w.badges ?? new List<string>()),
                ["tw"] = w.tWalk, ["tt"] = w.tTalk, ["twn"] = w.tWins, ["tg"] = w.tGames, ["tp"] = w.tPoints, ["ta"] = w.tAssists, ["ts"] = w.tStops,
                ["tc"] = w.tCrews, ["td"] = w.tDrives, ["tsj"] = w.tSide, ["tl"] = w.tLandmark, ["tn"] = w.tNear, ["tr2"] = Strings(w.tRegions ?? new List<string>()),
                ["wc"] = (double)w.walkCarry,
                ["sd"] = Strings(w.sideDone ?? new List<string>()), ["sa"] = w.sideActive ?? "", ["ss"] = w.sideStage, ["sf"] = found,
                ["runs"] = Strings(w.runs ?? new List<string>()),
                ["rs"] = Strings(w.regionsSeen ?? new List<string>()), ["dr"] = w.drives, ["cd"] = w.cleanDrives, ["nm"] = w.nearMisses,
                ["bdt"] = (double)w.bestDrive, ["rw"] = Strings(w.regionWins ?? new List<string>()), ["wt"] = (double)w.walkedTotal,
            };
        }

        private static CityWorldSaveData DecodeWorld(Dictionary<string, object> o)
        {
            var w = new CityWorldSaveData();
            if (o == null) return w;
            w.issue = Math.Max(1, Math.Min(CityStory.Count + 1, Int(o, "is", 1)));
            var issue = CityStory.Issue(w.issue);
            w.step = Math.Max(0, Math.Min(issue == null ? 0 : issue.Goals.Length - 1, Int(o, "st", 0)));
            double sb = Num(o, "sb", 0);
            w.stepBase = double.IsNaN(sb) || double.IsInfinity(sb) ? 0 : (long)Math.Max(0, Math.Min(1e12, sb));
            foreach (var n in IntList(o, "io")) if (n >= 1 && n <= CityStory.Count && !w.issuesOpened.Contains(n)) w.issuesOpened.Add(n);
            w.started = Bool(o, "go", false);
            w.velvetBeaten = Bool(o, "vb", false);
            int po = Int(o, "po", 0);
            w.pendingOutro = po >= 1 && po <= CityStory.Count ? po : 0;
            string tr = Str(o, "tr", "") ?? "";
            w.tracking = tr == "off" || tr == "story" || (tr.StartsWith("side:", StringComparison.Ordinal) && CitySideJobs.Find(tr.Substring(5)) != null) ? tr : "";

            w.achDay = Int(o, "ad", -1);
            var prog = IntList(o, "ap");
            w.achProgress = new int[CityAchievements.PerDay];
            for (int i = 0; i < w.achProgress.Length && i < prog.Count; i++) w.achProgress[i] = Math.Max(0, Math.Min(1000000, prog[i]));
            w.achTierPaid = Math.Max(0, Math.Min(CityAchievements.TierAt.Length, Int(o, "at", 0)));
            w.achStreak = Math.Max(0, Int(o, "as", 0));
            w.achBestStreak = Math.Max(w.achStreak, Int(o, "ab", 0));
            w.achLastDay = Int(o, "al", -1);
            w.achGoldDays = Math.Max(0, Int(o, "ag", 0));
            w.achTotal = Math.Max(0, Int(o, "an", 0));
            foreach (var id in StrList(o, "bd")) if (CityAchievements.FindBadge(id) != null && !w.badges.Contains(id)) w.badges.Add(id);
            int T(string k) => Math.Max(0, Math.Min(10000000, Int(o, k, 0)));
            w.tWalk = T("tw"); w.tTalk = T("tt"); w.tWins = T("twn"); w.tGames = T("tg"); w.tPoints = T("tp"); w.tAssists = T("ta"); w.tStops = T("ts");
            w.tCrews = T("tc"); w.tDrives = T("td"); w.tSide = T("tsj"); w.tLandmark = T("tl"); w.tNear = T("tn");
            foreach (var r in StrList(o, "tr2")) if (WorldRegions.Find(r) != null && !w.tRegions.Contains(r)) w.tRegions.Add(r);
            double wc = Num(o, "wc", 0);
            w.walkCarry = double.IsNaN(wc) || wc < 0 || wc > 1 ? 0f : (float)wc;

            foreach (var id in StrList(o, "sd")) if (CitySideJobs.Find(id) != null && !w.sideDone.Contains(id)) w.sideDone.Add(id);
            string sa = Str(o, "sa", "") ?? "";
            w.sideActive = CitySideJobs.Find(sa) != null && !w.sideDone.Contains(sa) ? sa : "";
            w.sideStage = w.sideActive.Length == 0 ? 0 : Math.Max(0, Math.Min(1, Int(o, "ss", 0)));
            var job = CitySideJobs.Find(w.sideActive);
            if (job != null)
                foreach (var n in IntList(o, "sf")) if (n >= 0 && n < job.Spots.Length && !w.sideFound.Contains(n)) w.sideFound.Add(n);

            foreach (var e in StrList(o, "runs"))
            {
                var p = (e ?? "").Split('|');
                if (p.Length == 5 && CityBigRun.For(p[0]) != null && !w.runs.Exists(x => x.StartsWith(p[0] + "|", StringComparison.Ordinal))) w.runs.Add(e);
            }
            foreach (var r in StrList(o, "rs")) if (WorldRegions.Find(r) != null && !w.regionsSeen.Contains(r)) w.regionsSeen.Add(r);
            w.drives = Math.Max(0, Int(o, "dr", 0));
            w.cleanDrives = Math.Max(0, Math.Min(w.drives, Int(o, "cd", 0)));
            w.nearMisses = Math.Max(0, Int(o, "nm", 0));
            double bd = Num(o, "bdt", 0);
            w.bestDrive = double.IsNaN(bd) || bd < 0 || bd > 600 ? 0f : (float)bd;
            foreach (var e in StrList(o, "rw"))
            {
                int eq = (e ?? "").IndexOf('=');
                if (eq > 0 && WorldRegions.Find(e.Substring(0, eq)) != null && int.TryParse(e.Substring(eq + 1), out int n) && n >= 0
                    && !w.regionWins.Exists(x => x.StartsWith(e.Substring(0, eq) + "=", StringComparison.Ordinal)))
                    w.regionWins.Add(e);
            }
            double wt = Num(o, "wt", 0);
            w.walkedTotal = double.IsNaN(wt) || wt < 0 ? 0 : (long)Math.Min(1e12, wt);
            return w;
        }
    }
}
