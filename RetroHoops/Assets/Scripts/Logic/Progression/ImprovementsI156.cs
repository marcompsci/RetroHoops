using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// I162 + I163 (20 Nov 2026): what your recent games say about your shooting and your reaching, as one plain line
    /// after the game. The numbers fade (each game's are added after the old ones drop to 70%), so a habit you fixed stops
    /// being mentioned within a few games.
    /// </summary>
    public static class Habits
    {
        /// <summary>Jumpers needed (recent, faded) before the release coach says anything.</summary>
        public const int MinJumpers = 10;
        /// <summary>Share of jumpers let go early (or late) that counts as a habit.</summary>
        public const float HabitShare = 0.4f;
        /// <summary>Recent short reaches that count as a habit.</summary>
        public const int ShortReachHabit = 4;

        public static int Fade(int v) => v * 7 / 10;

        /// <summary>Adds one game's jumpers and reaches to the recent tallies (old ones fade first).</summary>
        public static void AddGame(CareerTotals t, PlayerStatLine line)
        {
            if (t == null || line == null) return;
            t.recentEarly = Fade(t.recentEarly) + line.earlyReleases;
            t.recentLate = Fade(t.recentLate) + line.lateReleases;
            t.recentJumpers = Fade(t.recentJumpers) + Math.Max(line.jumpers, line.earlyReleases + line.lateReleases);
            t.recentShortReaches = Fade(t.recentShortReaches) + line.shortReaches;
        }

        /// <summary>The release coach's note (null = nothing worth saying).</summary>
        public static string ReleaseNote(CareerTotals t)
        {
            if (t == null || t.recentJumpers < MinJumpers) return null;
            float early = t.recentEarly / (float)t.recentJumpers, late = t.recentLate / (float)t.recentJumpers;
            if (early >= HabitShare && early >= late)
                return "SHOOTING NOTE: you've been letting go early. Hold SHOOT a beat longer, until the meter touches the gold line.";
            if (late >= HabitShare)
                return "SHOOTING NOTE: you've been letting go late. Release the moment the meter reaches gold.";
            return null;
        }

        /// <summary>The defence coach's note (null = nothing worth saying).</summary>
        public static string StealNote(CareerTotals t)
        {
            if (t == null || t.recentShortReaches < ShortReachHabit) return null;
            return "DEFENCE NOTE: a lot of reaches from too far away. Get within arm's length first; the ring on STEAL shows when you can reach again.";
        }

        /// <summary>Both notes, for the end of a game.</summary>
        public static List<string> Notes(CareerTotals t)
        {
            var list = new List<string>();
            var r = ReleaseNote(t);
            if (r != null) list.Add(r);
            var s = StealNote(t);
            if (s != null) list.Add(s);
            return list;
        }

        /// <summary>I160: "3/7 STEALS" style line for a box score (empty when there were no reaches).</summary>
        public static string StealLine(PlayerStatLine line) =>
            line == null || line.stealAttempts <= 0 ? "" : line.steals + "/" + line.stealAttempts + " " + Loc.T("STEALS");
    }

    /// <summary>
    /// I166 (20 Nov 2026): LOYALTY COURTS. Reaching a 7-day and then a 30-day login streak (your best ever) each earns one
    /// free pick of any SHOP court, yours for good. It never runs out and needs no purchase.
    /// </summary>
    public static class Loyalty
    {
        public static readonly int[] Milestones = { 7, 30 };

        /// <summary>Free court picks earned so far (from your best login streak).</summary>
        public static int Earned(CareerSaveData d)
        {
            if (d == null) return 0;
            int n = 0;
            foreach (int m in Milestones) if (d.loginBest >= m) n++;
            return n;
        }

        /// <summary>Picks earned but not used yet.</summary>
        public static int Available(CareerSaveData d) => d == null ? 0 : Math.Max(0, Earned(d) - (d.shopEarned?.Count ?? 0));

        /// <summary>Days until the next free pick (0 = none left to earn).</summary>
        public static int DaysToNext(CareerSaveData d)
        {
            if (d == null) return Milestones[0];
            foreach (int m in Milestones) if (d.loginBest < m) return m - Math.Max(d.loginStreak, 0);
            return 0;
        }

        /// <summary>Uses a free pick on a shop court you don't have. Returns false if there's no pick or the court isn't a choice.</summary>
        public static bool Claim(CareerSaveData d, string courtId)
        {
            if (Available(d) <= 0 || !Shop.IsShopCourt(courtId) || Shop.PackOf(courtId) == null || Shop.CourtOpen(d, courtId)) return false;
            d.shopEarned = d.shopEarned ?? new List<string>();
            d.shopEarned.Add(courtId);
            return true;
        }
    }

    /// <summary>
    /// I167 (20 Nov 2026): WEEKEND COURT. On Saturdays and Sundays the Daily Challenge is played on one of the SHOP's
    /// designed courts, so everyone gets a free game on one (a different court each weekend day, in turn).
    /// </summary>
    public static class WeekendCourt
    {
        /// <summary>Day 0 of the Daily Challenge calendar (1 Jan 2000) was a Saturday.</summary>
        public static bool IsWeekend(int dailyDay) => ((dailyDay % 7) + 7) % 7 <= 1;

        /// <summary>The shop court for a weekend day (null on weekdays).</summary>
        public static string CourtFor(int dailyDay)
        {
            if (!IsWeekend(dailyDay)) return null;
            int weekend = (int)Math.Floor(dailyDay / 7.0) * 2 + (((dailyDay % 7) + 7) % 7);
            int i = ((weekend % Shop.Courts.Length) + Shop.Courts.Length) % Shop.Courts.Length;
            return Shop.Courts[i].Id;
        }
    }

    /// <summary>I169 (20 Nov 2026): MY PURCHASES: what the SHOP says is yours, in plain lines.</summary>
    public static class PurchaseHistory
    {
        public static List<string> Lines(CareerSaveData d, bool proOwned)
        {
            var list = new List<string>();
            if (d == null) return list;
            if (proOwned) list.Add(Pro.DisplayName.ToUpperInvariant());
            foreach (var p in Shop.Products)
                if (!p.Consumable && d.shopOwned != null && d.shopOwned.Contains(p.Id)) list.Add(Loc.T(p.Name));
            if (d.shopEarned != null)
                foreach (var id in d.shopEarned)
                {
                    var k = Array.Find(Shop.Courts, x => x.Id == id);
                    if (k != null) list.Add(k.Name.ToUpperInvariant() + "  ·  " + Loc.T("LOYALTY COURT"));
                }
            if (d.shopTips > 0) list.Add(Loc.T("TIPS") + ": " + d.shopTips + "  ·  " + Loc.T("thank you!"));
            return list;
        }
    }

    /// <summary>I174 (20 Nov 2026): a NEW tag on the SHOP button until you've looked at what's new in it.</summary>
    public static class ShopNews
    {
        /// <summary>Bump when the SHOP gets new things (1 = the I141 launch, 2 = loyalty courts and weekend courts).</summary>
        public const int Version = 2;

        public static bool HasNew(CareerSaveData d) => d != null && d.shopSeenVersion < Version;

        public static void MarkSeen(CareerSaveData d)
        {
            if (d != null) d.shopSeenVersion = Version;
        }
    }
}
