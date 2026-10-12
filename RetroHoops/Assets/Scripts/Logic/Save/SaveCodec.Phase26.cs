using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public static partial class SaveCodec
    {
        private static object EncodeStory(StorySaveData s)
        {
            s = s ?? new StorySaveData();
            var best = new List<object>();
            foreach (var b in s.best ?? new List<int>()) best.Add(b);
            return new Dictionary<string, object> { ["cleared"] = s.cleared, ["best"] = best, ["attempts"] = s.attempts, ["finished"] = s.finished };
        }

        private static StorySaveData DecodeStory(Dictionary<string, object> o)
        {
            var s = new StorySaveData();
            if (o == null) return s;
            s.cleared = Math.Max(0, Math.Min(StoryMode.Chapters, Int(o, "cleared", 0)));
            foreach (var item in Arr(o, "best"))
            {
                if (s.best.Count >= StoryMode.Chapters) break;
                s.best.Add(item is double d && d > 0 ? 1 : 0);
            }
            s.attempts = Math.Max(0, Int(o, "attempts", 0));
            s.finished = Bool(o, "finished", false) && s.cleared >= StoryMode.Chapters;
            return s;
        }

        private static object EncodeLive(LiveSaveData l)
        {
            l = l ?? new LiveSaveData();
            return new Dictionary<string, object>
            {
                ["rating"] = l.rating, ["best"] = l.best, ["wins"] = l.wins, ["losses"] = l.losses, ["games"] = l.games,
                ["team"] = l.teamId ?? "", ["until"] = l.subscribedUntil, ["month"] = l.month,
                ["sm"] = l.seasonMonth, ["sb"] = l.seasonBest, ["sg"] = l.seasonGames,
                ["em"] = l.endedMonth, ["eb"] = l.endedBest, ["eg"] = l.endedGames, ["rm"] = l.rewardedMonth,
                ["pw"] = l.pWins, ["ps"] = l.pScoring, ["pa"] = l.pActivity,
                ["pm"] = l.pMonth, ["pmw"] = l.pMonthWins, ["pms"] = l.pMonthScoring, ["pma"] = l.pMonthActivity,
                ["pd"] = l.pDay, ["pda"] = l.pDayActivity, ["pst"] = l.pStreak, ["pbs"] = l.pBestStreak,
            };
        }

        private static LiveSaveData DecodeLive(Dictionary<string, object> o)
        {
            var l = new LiveSaveData();
            if (o == null) return l;
            l.rating = Math.Max(100, Math.Min(4000, Int(o, "rating", LiveMode.StartRating)));
            l.best = Math.Max(l.rating, Math.Min(4000, Int(o, "best", l.rating)));
            l.wins = Math.Max(0, Int(o, "wins", 0));
            l.losses = Math.Max(0, Int(o, "losses", 0));
            l.games = Math.Max(l.wins + l.losses, Int(o, "games", 0));
            l.teamId = Str(o, "team", "");
            l.subscribedUntil = o.TryGetValue("until", out var u) && u is double d ? d : 0.0;
            l.month = Math.Max(0, Int(o, "month", 0));
            l.seasonMonth = Int(o, "sm", 0); l.seasonBest = Int(o, "sb", 0); l.seasonGames = Int(o, "sg", 0);
            l.endedMonth = Int(o, "em", 0); l.endedBest = Int(o, "eb", 0); l.endedGames = Int(o, "eg", 0); l.rewardedMonth = Int(o, "rm", 0);
            l.pWins = Int(o, "pw", 0); l.pScoring = Int(o, "ps", 0); l.pActivity = Int(o, "pa", 0);
            l.pMonth = Int(o, "pm", 0); l.pMonthWins = Int(o, "pmw", 0); l.pMonthScoring = Int(o, "pms", 0); l.pMonthActivity = Int(o, "pma", 0);
            l.pDay = Int(o, "pd", 0); l.pDayActivity = Int(o, "pda", 0); l.pStreak = Int(o, "pst", 0); l.pBestStreak = Int(o, "pbs", 0);
            l = LiveMode.Sanitize(l);
            LivePoints.Sanitize(l);
            return l;
        }

        private static object EncodeCouch(CouchCupSaveData c)
        {
            c = c ?? new CouchCupSaveData();
            var games = new List<object>();
            foreach (var g in c.games ?? new List<CouchGame>())
                games.Add(new Dictionary<string, object> { ["r"] = g.round, ["a"] = g.a, ["b"] = g.b, ["sa"] = g.scoreA, ["sb"] = g.scoreB, ["p"] = g.played });
            return new Dictionary<string, object>
            {
                ["names"] = Strings(c.names), ["teams"] = Strings(c.teamIds), ["games"] = games, ["champion"] = c.champion, ["cups"] = c.cupsPlayed,
            };
        }

        private static CouchCupSaveData DecodeCouch(Dictionary<string, object> o)
        {
            var c = new CouchCupSaveData();
            if (o == null) return c;
            c.names = StrList(o, "names");
            c.teamIds = StrList(o, "teams");
            c.cupsPlayed = Math.Max(0, Int(o, "cups", 0));
            int n = c.names.Count;
            bool ok = n >= CouchCup.MinPlayers && n <= CouchCup.MaxPlayers && c.teamIds.Count == n;
            foreach (var item in Arr(o, "games"))
            {
                if (!(item is Dictionary<string, object> g)) { ok = false; break; }
                var game = new CouchGame
                {
                    round = Math.Max(0, Int(g, "r", 0)), a = Int(g, "a", 0), b = Int(g, "b", -1),
                    scoreA = Math.Max(0, Int(g, "sa", 0)), scoreB = Math.Max(0, Int(g, "sb", 0)), played = Bool(g, "p", false),
                };
                if (game.a < 0 || game.a >= n || game.b >= n || game.b < -1) ok = false;
                c.games.Add(game);
            }
            c.champion = Int(o, "champion", -1);
            if (c.champion >= n) ok = false;
            if (!ok)
            {
                // A damaged cup is dropped (the count of cups played is kept).
                int cups = c.cupsPlayed;
                c = new CouchCupSaveData { cupsPlayed = cups };
            }
            return c;
        }

        // Phase 34: the Skills Gauntlet.
        private static object EncodeGauntlet(GauntletSaveData g)
        {
            g = g ?? new GauntletSaveData();
            var pts = new List<object>();
            foreach (var p in g.points ?? new List<int>()) pts.Add(p);
            return new Dictionary<string, object>
            {
                ["day"] = g.day, ["points"] = pts, ["todayBest"] = g.todayBest, ["bestDay"] = g.bestDay, ["best"] = g.best, ["runs"] = g.runs,
            };
        }

        private static GauntletSaveData DecodeGauntlet(Dictionary<string, object> o)
        {
            var g = new GauntletSaveData();
            if (o == null) return g;
            g.day = Int(o, "day", -1);
            foreach (var item in Arr(o, "points"))
            {
                if (g.points.Count >= Gauntlet.Stations) break;
                if (item is double d) g.points.Add(Math.Max(0, Math.Min(5000, (int)d)));
            }
            g.todayBest = Math.Max(0, Int(o, "todayBest", 0));
            g.bestDay = Int(o, "bestDay", -1);
            g.best = Math.Max(g.todayBest, Math.Max(0, Int(o, "best", 0)));
            g.runs = Math.Max(0, Int(o, "runs", 0));
            return g;
        }
    }
}
