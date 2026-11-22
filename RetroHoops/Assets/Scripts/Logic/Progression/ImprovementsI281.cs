using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>I281–I283 (21 Nov 2026): HOT HAND, makes in a row (the simulation keeps the run on each player's line).</summary>
    public static class HotHand
    {
        public const int Call = 3;

        /// <summary>True right after the make that reached the call (3, then 5, 7…).</summary>
        public static bool Called(PlayerStatLine s) => s != null && s.makeRun >= Call && (s.makeRun - Call) % 2 == 0;

        public static string Line(int run) => Loc.T("HOT HAND") + "  ·  " + run + " " + Loc.T("IN A ROW");
    }

    /// <summary>I284 + I285 + I286: shooting by range (paint, mid-range, deep).</summary>
    public static class ShotRanges
    {
        public static void Split(ShotChartData chart, out int pm, out int pa, out int mm, out int ma, out int dm, out int da)
        {
            pm = pa = mm = ma = dm = da = 0;
            if (chart?.spots == null) return;
            for (int i = 0; i < chart.spots.Length; i++)
            {
                var r = chart.spots[i];
                if (r == null) continue;
                var spot = (ShotSpot)i;
                if (spot == ShotSpot.Paint) { pm += r.made; pa += r.attempted; }
                else if (spot == ShotSpot.FoulLine || spot == ShotSpot.ElbowLeft || spot == ShotSpot.ElbowRight) { mm += r.made; ma += r.attempted; }
                else { dm += r.made; da += r.attempted; }
            }
        }

        /// <summary>"INSIDE 4/5 · MID 2/4 · DEEP 1/3" (ranges you haven't shot from are left out), or null.</summary>
        public static string Line(ShotChartData chart)
        {
            Split(chart, out int pm, out int pa, out int mm, out int ma, out int dm, out int da);
            var parts = new List<string>();
            if (pa > 0) parts.Add(Loc.T("INSIDE") + " " + pm + "/" + pa);
            if (ma > 0) parts.Add(Loc.T("MID") + " " + mm + "/" + ma);
            if (da > 0) parts.Add(Loc.T("DEEP") + " " + dm + "/" + da);
            return parts.Count == 0 ? null : string.Join("  ·  ", parts);
        }

        /// <summary>I286: where the other team did their damage tonight ("THEY SCORED MOST FROM: INSIDE (6)").</summary>
        public static string TheirDamage(MatchSummary s)
        {
            if (s?.lines == null) return null;
            int p = 0, m = 0, d = 0;
            foreach (var l in s.lines)
            {
                if (l.stats == null || l.team == s.humanTeam) continue;
                Split(l.stats.chart, out int pm, out _, out int mm, out _, out int dm, out _);
                p += pm; m += mm; d += dm;
            }
            if (p + m + d < 3) return null;
            string where = p >= m && p >= d ? "INSIDE" : m >= d ? "MID" : "DEEP";
            int n = Math.Max(p, Math.Max(m, d));
            return Loc.T("THEY SCORED MOST FROM") + ": " + Loc.T(where) + " (" + n + " " + Loc.T("BUCKETS") + ")";
        }
    }

    /// <summary>I287 + I288 + I293 + I296: lines for the tip-off and the post-game from your recent games.</summary>
    public static class StreakLines
    {
        /// <summary>Tip-off: "ON A 4-GAME WIN STREAK" (3+).</summary>
        public static string WinStreak(CareerSaveData d) =>
            d?.records != null && d.records.winStreak >= 3 ? Loc.T("ON A WIN STREAK") + ": " + d.records.winStreak : null;

        /// <summary>Post-game: a win after 2+ losses in a row.</summary>
        public static bool BounceBack(CareerSaveData d)
        {
            var h = d?.history;
            if (h == null || h.Count < 3 || !h[0].Won) return false;
            return !h[1].Won && !h[2].Won;
        }

        public static bool CloseOne(MatchSummary s) => s != null && s.HumanWon && s.Margin <= 2;

        /// <summary>Tip-off: your first game on this court lately.</summary>
        public static bool NewCourt(CareerSaveData d, string courtId)
        {
            if (string.IsNullOrEmpty(courtId) || d?.history == null || d.history.Count == 0) return false;
            foreach (var g in d.history) if (g.courtId == courtId) return false;
            return true;
        }

        /// <summary>I297: different courts in your last 20 games.</summary>
        public static int CourtsPlayed(CareerSaveData d)
        {
            var set = new HashSet<string>();
            foreach (var g in d?.history ?? new List<MatchHistoryEntry>()) if (!string.IsNullOrEmpty(g.courtId)) set.Add(g.courtId);
            return set.Count;
        }
    }

    /// <summary>
    /// I290 + I291: a DAILY GOAL and a WEEKLY GOAL. Shown on the PLAY menu and checked against your games; they're for
    /// fun, with no reward (nothing to grind, nothing to buy).
    /// </summary>
    public static class Goals
    {
        public enum Kind { Points = 0, Assists = 1, Rebounds = 2, Win = 3, WinBy5 = 4 }

        private static readonly (Kind kind, int target, string text)[] Daily =
        {
            (Kind.Points, 15, "SCORE 15 IN A GAME"),
            (Kind.Assists, 5, "DISH 5 ASSISTS IN A GAME"),
            (Kind.Rebounds, 6, "GRAB 6 REBOUNDS IN A GAME"),
            (Kind.Win, 1, "WIN A GAME"),
            (Kind.WinBy5, 5, "WIN A GAME BY 5 OR MORE"),
            (Kind.Points, 20, "SCORE 20 IN A GAME"),
        };

        public static (Kind kind, int target, string text) DailyFor(int day) => Daily[((day % Daily.Length) + Daily.Length) % Daily.Length];

        public static bool Meets(MatchHistoryEntry g, Kind kind, int target)
        {
            if (g == null) return false;
            switch (kind)
            {
                case Kind.Points: return g.points >= target;
                case Kind.Assists: return g.assists >= target;
                case Kind.Rebounds: return g.rebounds >= target;
                case Kind.Win: return g.Won;
                default: return g.Won && g.scoreFor - g.scoreAgainst >= target;
            }
        }

        public static bool DailyDone(CareerSaveData d, int day)
        {
            var goal = DailyFor(day);
            foreach (var g in d?.history ?? new List<MatchHistoryEntry>())
                if (g.day == day && Meets(g, goal.kind, goal.target)) return true;
            return false;
        }

        /// <summary>True when the game just played (history[0]) finished today's goal for the first time.</summary>
        public static bool DailyJustDone(CareerSaveData d, int day)
        {
            var h = d?.history;
            if (h == null || h.Count == 0 || h[0].day != day) return false;
            var goal = DailyFor(day);
            if (!Meets(h[0], goal.kind, goal.target)) return false;
            for (int i = 1; i < h.Count; i++) if (h[i].day == day && Meets(h[i], goal.kind, goal.target)) return false;
            return true;
        }

        public static string DailyLine(CareerSaveData d, int day) =>
            Loc.T("TODAY'S GOAL") + ": " + Loc.T(DailyFor(day).text) + (DailyDone(d, day) ? "  ·  " + Loc.T("DONE!") : "");

        public const int WeeklyWins = 5;

        /// <summary>Wins in the 7-day week the day falls in (weeks start on day 0 of the game's calendar, so every week lines up).</summary>
        public static int WeekWins(CareerSaveData d, int day)
        {
            int start = day - (((day % 7) + 7) % 7);
            int wins = 0;
            foreach (var g in d?.history ?? new List<MatchHistoryEntry>()) if (g.day >= start && g.day <= day && g.Won) wins++;
            return wins;
        }

        public static string WeeklyLine(CareerSaveData d, int day)
        {
            int w = Math.Min(WeeklyWins, WeekWins(d, day));
            return Loc.T("WEEKLY GOAL") + ": " + Loc.T("WIN 5 GAMES") + "  (" + w + "/" + WeeklyWins + ")" + (w >= WeeklyWins ? "  ·  " + Loc.T("DONE!") : "");
        }
    }

    /// <summary>I294 + I295: RANDOM OUTFIT in the Locker (from what you own), and PUT IT BACK.</summary>
    public static class RandomLook
    {
        public static readonly CosmeticSlot[] Slots = { CosmeticSlot.JerseyPalette, CosmeticSlot.Shoes, CosmeticSlot.Gear, CosmeticSlot.CourtBanner };

        /// <summary>What's on now, slot by slot (to put it back).</summary>
        public static string[] Snapshot(CareerSaveData d)
        {
            var s = new string[Slots.Length];
            for (int i = 0; i < Slots.Length; i++) s[i] = d?.Equipped(Slots[i]);
            return s;
        }

        public static void Restore(CareerSaveData d, string[] look)
        {
            if (d == null || look == null || look.Length != Slots.Length) return;
            for (int i = 0; i < Slots.Length; i++) d.SetEquipped(Slots[i], look[i]);
        }

        /// <summary>Puts on a random owned item in each slot that has two or more. Returns false when there's nothing to shuffle.</summary>
        public static bool Shuffle(CareerSaveData d, ContentCatalog c, uint seed)
        {
            if (d?.ownedCosmetics == null || c == null) return false;
            var rng = new SeededRandom(seed == 0 ? 1u : seed);
            bool any = false;
            foreach (var slot in Slots)
            {
                var owned = c.Cosmetics.FindAll(x => x.slot == slot && d.ownedCosmetics.Contains(x.id));
                if (owned.Count < 2) continue;
                any = true;
                d.SetEquipped(slot, owned[rng.Range(0, owned.Count)].id);
            }
            return any;
        }
    }
}
