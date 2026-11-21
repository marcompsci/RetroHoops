using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// I196 (21 Nov 2026): MOMENTUM, who's on a run. Every bucket pushes the bar toward the team that scored, and the push
    /// fades (half of it is gone after <see cref="HalfLife"/> seconds of game time), so a 6-0 run swings it hard and a quiet
    /// spell brings it back to the middle.
    /// </summary>
    public sealed class Momentum
    {
        public const float HalfLife = 20f;
        /// <summary>Points (faded) that put the bar about three quarters of the way over.</summary>
        public const float Scale = 6f;

        private readonly List<(float time, int signedPoints)> _buckets = new List<(float, int)>();

        /// <summary>A bucket by <paramref name="team"/> (0 pushes the bar to team A's side, 1 to team B's).</summary>
        public void Add(int team, int points, float time)
        {
            if (points <= 0 || team < 0 || team > 1) return;
            _buckets.Add((time, team == 0 ? points : -points));
            if (_buckets.Count > 40) _buckets.RemoveAt(0);
        }

        /// <summary>-1 (all team B) .. 1 (all team A) at <paramref name="time"/>.</summary>
        public float Value(float time)
        {
            double sum = 0;
            foreach (var b in _buckets)
            {
                float age = Math.Max(0f, time - b.time);
                sum += b.signedPoints * Math.Pow(0.5, age / HalfLife);
            }
            return (float)Math.Tanh(sum / Scale);
        }
    }

    /// <summary>I204: unanswered runs ("8-0 RUN"). The crowd swells at 6 straight points and again every 4 after.</summary>
    public sealed class RunTracker
    {
        public const int FirstCall = 6, Every = 4;
        public int Team { get; private set; } = -1;
        public int Points { get; private set; }
        private int _nextCall = FirstCall;

        /// <summary>A bucket; returns true when the run just reached a call (6, 10, 14...).</summary>
        public bool Add(int team, int points)
        {
            if (points <= 0) return false;
            if (team != Team) { Team = team; Points = 0; _nextCall = FirstCall; }
            Points += points;
            if (Points < _nextCall) return false;
            while (_nextCall <= Points) _nextCall += Every;
            return true;
        }

        public string Call => Points + "-0 " + Loc.T("RUN");
    }

    /// <summary>I197: your HOT SPOT, the spot on the floor where your career shooting is best (with enough shots to trust).</summary>
    public static class HotSpot
    {
        public const int MinAttempts = 8;

        public static bool Best(ShotChartData chart, out ShotSpot spot, out float pct)
        {
            spot = default;
            pct = 0f;
            if (chart?.spots == null) return false;
            bool found = false;
            for (int i = 0; i < chart.spots.Length; i++)
            {
                var r = chart.spots[i];
                if (r == null || r.attempted < MinAttempts) continue;
                float p = r.made / (float)r.attempted;
                if (!found || p > pct) { found = true; pct = p; spot = (ShotSpot)i; }
            }
            return found;
        }

        /// <summary>The tip-off line ("YOUR HOT SPOT: LEFT CORNER  ·  52%"), or null.</summary>
        public static string Line(ShotChartData chart) =>
            Best(chart, out var spot, out float pct)
                ? Loc.T("YOUR HOT SPOT") + ": " + Loc.T(ShotZones.SpotName(spot)).ToUpperInvariant() + "  ·  " + (int)Math.Round(pct * 100f) + "%"
                : null;
    }

    /// <summary>I199: CAREER MILESTONES, round numbers passed in the game just played (100 points, 50 steals...).</summary>
    public static class Milestones
    {
        public static readonly (string name, int[] marks)[] Tracks =
        {
            ("GAMES", new[] { 10, 25, 50, 100, 250, 500, 1000 }),
            ("WINS", new[] { 10, 25, 50, 100, 250, 500 }),
            ("POINTS", new[] { 100, 250, 500, 1000, 2500, 5000, 10000 }),
            ("STEALS", new[] { 25, 50, 100, 250, 500 }),
            ("BLOCKS", new[] { 25, 50, 100, 250, 500 }),
            ("GREENS", new[] { 50, 100, 250, 500, 1000 }),
            ("POSTERS", new[] { 5, 10, 25, 50, 100 }),
            ("CHASE-DOWN BLOCKS", new[] { 5, 10, 25, 50 }),
            ("CLUTCH BUCKETS", new[] { 5, 10, 25, 50 }),
            ("COMEBACK WINS", new[] { 1, 5, 10, 25 }),   // I234
            ("DOUBLE-DOUBLES", new[] { 1, 10, 25, 50 }),
        };

        public static int ValueOf(CareerTotals t, string name)
        {
            if (t == null) return 0;
            switch (name)
            {
                case "GAMES": return t.games;
                case "WINS": return t.wins;
                case "POINTS": return t.points;
                case "STEALS": return t.steals;
                case "BLOCKS": return t.blocks;
                case "GREENS": return t.greens;
                case "POSTERS": return t.posters;
                case "CHASE-DOWN BLOCKS": return t.chaseDowns;
                case "CLUTCH BUCKETS": return t.clutchMakes;
                case "COMEBACK WINS": return t.comebacks;
                case "DOUBLE-DOUBLES": return t.doubleDoubles;
                default: return 0;
            }
        }

        /// <summary>Values of every track now (taken before a game, compared after it).</summary>
        public static int[] Snapshot(CareerTotals t)
        {
            var v = new int[Tracks.Length];
            for (int i = 0; i < Tracks.Length; i++) v[i] = ValueOf(t, Tracks[i].name);
            return v;
        }

        /// <summary>The marks passed between <paramref name="before"/> and now, e.g. "100 STEALS".</summary>
        public static List<string> Crossed(int[] before, CareerTotals after)
        {
            var list = new List<string>();
            if (before == null || before.Length != Tracks.Length || after == null) return list;
            for (int i = 0; i < Tracks.Length; i++)
            {
                int now = ValueOf(after, Tracks[i].name);
                foreach (int m in Tracks[i].marks)
                    if (before[i] < m && now >= m) list.Add(m + " " + Tracks[i].name);
            }
            return list;
        }
    }

    /// <summary>I200: the login run on the main menu: today's day and what tomorrow pays.</summary>
    public static class LoginStrip
    {
        public static string Line(CareerSaveData d)
        {
            if (d == null || d.loginStreak <= 0) return null;
            int tomorrow = Onboarding.LoginReward(d.loginStreak + 1);
            return Loc.T("DAY") + " " + d.loginStreak + " " + Loc.T("IN A ROW") + "  ·  " + Loc.T("TOMORROW") + " +" + tomorrow + " SP";
        }
    }

    /// <summary>I205 + I206: your last game, and the run you're on, for the PLAY menu.</summary>
    public static class LastGame
    {
        public static string Line(CareerSaveData d, ContentCatalog c)
        {
            if (d?.history == null || d.history.Count == 0) return null;
            var g = d.history[0];
            string opp = c?.Team(g.opponentId)?.FullName ?? "?";
            return Loc.T("LAST GAME") + ":  " + (g.Won ? "W " : "L ") + g.scoreFor + "-" + g.scoreAgainst + "  " + Loc.T("vs") + " " + opp.ToUpperInvariant()
                   + "  ·  " + g.points + " PTS";
        }

        /// <summary>RUN IT BACK: a Quick Call against the same team (null when the last game wasn't a Quick Call one or the team is gone).</summary>
        public static MatchRequest RunItBack(CareerSaveData d, ContentCatalog c)
        {
            if (d?.history == null || d.history.Count == 0 || c == null) return null;
            var g = d.history[0];
            if (g.mode != GameMode.QuickCall || c.Team(g.opponentId) == null) return null;
            var mine = Secrets.PlayableTeams(c, d.secrets);
            if (mine.Count == 0) return null;
            var home = (!string.IsNullOrEmpty(g.teamId) && g.teamId != g.opponentId ? mine.Find(t => t.id == g.teamId) : null)
                       ?? mine.Find(t => t.id != g.opponentId); // I237: the team you played as
            if (home == null) return null;
            return new MatchRequest
            {
                Mode = GameMode.QuickCall, HomeTeamId = home.id, AwayTeamId = g.opponentId, CourtId = home.homeCourtId,
                DifficultyId = d.settings.difficultyId,
            };
        }

        public static string StreakLine(CareerSaveData d)
        {
            var r = d?.records;
            if (r == null || r.winStreak < 2) return null;
            return Loc.T("WIN STREAK") + ": " + r.winStreak + (r.winStreak >= r.bestWinStreak ? "  ·  " + Loc.T("YOUR BEST!") : "  ·  " + Loc.T("BEST") + " " + r.bestWinStreak);
        }
    }

    /// <summary>I207 + I208: gear packs in the locker.</summary>
    public static class PackSets
    {
        /// <summary>The gear pack you're wearing all of (every slot it covers), or null.</summary>
        public static ShopProduct FullSet(CareerSaveData d)
        {
            if (d == null) return null;
            foreach (var p in Shop.Products)
            {
                if (p.Kind != ShopKind.GearPack) continue;
                var slots = new Dictionary<CosmeticSlot, bool>();
                foreach (var id in p.Items)
                {
                    var g = Array.Find(Shop.Gear, x => x.id == id);
                    if (g.id == null) continue;
                    bool worn = d.Equipped(g.slot) == id;
                    slots[g.slot] = (slots.TryGetValue(g.slot, out bool already) && already) || worn;
                }
                bool all = slots.Count > 0;
                foreach (var kv in slots) all &= kv.Value;
                if (all) return p;
            }
            return null;
        }

        /// <summary>"COURTS 3/12  ·  GEAR 5/20" for the SHOP.</summary>
        public static string Progress(CareerSaveData d)
        {
            int courts = 0, gear = 0;
            foreach (var k in Shop.Courts) if (Shop.CourtOpen(d, k.Id)) courts++;
            foreach (var g in Shop.Gear) if (d?.ownedCosmetics != null && d.ownedCosmetics.Contains(g.id)) gear++;
            return Loc.T("COURTS") + " " + courts + "/" + Shop.Courts.Length + "  ·  " + Loc.T("GEAR") + " " + gear + "/" + Shop.Gear.Length;
        }
    }

    /// <summary>I213: your points against your own average (before this game), for the post-game.</summary>
    public static class VsAverage
    {
        /// <summary>Modes whose games go into your career totals (so the average means something).</summary>
        public static bool Counts(GameMode m) =>
            m != GameMode.Practice && m != GameMode.Tutorial && m != GameMode.Versus && m != GameMode.Demo && m != GameMode.Franchise;

        /// <summary>The line, or null when there aren't 3 earlier games to compare with or it's within a point.</summary>
        public static string Line(CareerTotals after, PlayerStatLine game)
        {
            if (after == null || game == null || after.games < 4) return null;
            double avg = (after.points - game.points) / (double)(after.games - 1);
            double diff = game.points - avg;
            if (Math.Abs(diff) < 1.0) return null;
            int n = (int)Math.Round(Math.Abs(diff));
            return n + " " + Loc.T(diff > 0 ? "ABOVE YOUR AVERAGE" : "BELOW YOUR AVERAGE") + " (" + avg.ToString("0.0") + ")";
        }
    }
}
