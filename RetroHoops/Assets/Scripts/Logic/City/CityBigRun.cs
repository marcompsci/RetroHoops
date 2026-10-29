using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public sealed class BigRunDef
    {
        public string Region, Name;
        /// <summary>The three crews, in order (the last is the final).</summary>
        public string[] Rounds;
        /// <summary>What each round pays (street cash), before the weekend double.</summary>
        public long[] Purse;
    }

    /// <summary>One region's Big Run state today.</summary>
    public struct BigRunState
    {
        public int Day, Round, DoneDay, Titles;
    }

    /// <summary>
    /// 28 Oct 2026 (Omari, BALLER WORLD phase W5): the BIG RUN, every region's big street game. Three crews in a row on
    /// their own courts, cash every round from the purse ($10K–$25K early, up to $250K for the final), double on
    /// weekends. Win the final and you're KING of that city (a title, a badge, the comic's next page). One run a day:
    /// a loss ends it until tomorrow. You get in once you've beaten a crew in that region. The poster is in the region's
    /// main district (Baller City's is on the Blacktop pier).
    /// </summary>
    public static class CityBigRun
    {
        public const int RoundsCount = 3;
        public const string Tag = "bigrun";

        public static readonly BigRunDef[] All =
        {
            new BigRunDef { Region = WorldRegions.BallerCity, Name = "THE BALLER CITY BIG RUN", Rounds = new[] { "glass", "tide", "nightshift" }, Purse = new long[] { 10000, 25000, 100000 } },
            new BigRunDef { Region = WorldRegions.NewYork, Name = "THE NEW YORK BIG RUN", Rounds = new[] { "stoop", "skyline", "borough" }, Purse = new long[] { 15000, 30000, 150000 } },
            new BigRunDef { Region = WorldRegions.LosAngeles, Name = "THE LOS ANGELES BIG RUN", Rounds = new[] { "riptide", "lowslow", "goldenhour" }, Purse = new long[] { 20000, 40000, 175000 } },
            new BigRunDef { Region = WorldRegions.LasVegas, Name = "THE LAS VEGAS BIG RUN", Rounds = new[] { "neonnights", "dustdevils", "headliners" }, Purse = new long[] { 20000, 45000, 200000 } },
            new BigRunDef { Region = WorldRegions.TheBay, Name = "THE BAY BIG RUN", Rounds = new[] { "cablecars", "hype", "townbiz" }, Purse = new long[] { 25000, 50000, 250000 } },
        };

        public static BigRunDef For(string region) => Array.Find(All, b => b.Region == region);

        public static bool Weekend(DayOfWeek d) => d == DayOfWeek.Saturday || d == DayOfWeek.Sunday;

        public static long Pay(BigRunDef run, int round, DayOfWeek today) =>
            run == null || round < 1 || round > RoundsCount ? 0 : run.Purse[round - 1] * (Weekend(today) ? 2 : 1);

        public static long TotalPurse(BigRunDef run, DayOfWeek today)
        {
            long t = 0;
            for (int r = 1; r <= RoundsCount; r++) t += Pay(run, r, today);
            return t;
        }

        // ------------------------------------------------------------------ the save ("region|day|round|doneDay|titles")

        public static BigRunState State(CityWorldSaveData w, string region)
        {
            var s = new BigRunState { Day = -1, Round = 0, DoneDay = -1, Titles = 0 };
            if (w?.runs == null) return s;
            foreach (var e in w.runs)
            {
                var p = e.Split('|');
                if (p.Length != 5 || p[0] != region) continue;
                int.TryParse(p[1], out s.Day);
                int.TryParse(p[2], out s.Round);
                int.TryParse(p[3], out s.DoneDay);
                int.TryParse(p[4], out s.Titles);
                s.Round = Math.Max(0, Math.Min(RoundsCount, s.Round));
                s.Titles = Math.Max(0, s.Titles);
            }
            return s;
        }

        private static void Save(CityWorldSaveData w, string region, BigRunState s)
        {
            w.runs.RemoveAll(e => e.StartsWith(region + "|", StringComparison.Ordinal));
            w.runs.Add(region + "|" + s.Day + "|" + s.Round + "|" + s.DoneDay + "|" + s.Titles);
        }

        public static int Titles(CityWorldSaveData w, string region) => State(w, region).Titles;

        public static int TotalTitles(CityWorldSaveData w)
        {
            int n = 0;
            foreach (var b in All) n += Titles(w, b.Region);
            return n;
        }

        /// <summary>The round you'd play now (1–3), or 0 if today's run is over.</summary>
        public static int NextRound(CityWorldSaveData w, string region, int day)
        {
            var s = State(w, region);
            if (s.DoneDay == day) return 0;
            return s.Day == day && s.Round >= 1 ? s.Round : 1;
        }

        /// <summary>Why you can't enter yet (null = you can): the road must be open and you need a crew win in the region.</summary>
        public static string Locked(CareerSaveData d, string region)
        {
            var r = WorldRegions.Find(region);
            if (r == null) return Loc.T("No Big Run here.");
            if (!WorldRegions.Open(d, r)) return WorldRegions.LockedReason(d, r);
            if (WorldRegions.CrewsBeaten(d?.city, r) < 1) return Loc.T("Beat a crew in") + " " + Loc.T(r.Name) + " " + Loc.T("to get in.");
            return null;
        }

        public static CityCrewDef CrewFor(BigRunDef run, int round) => run == null || round < 1 || round > RoundsCount ? null : CityCrews.Find(run.Rounds[round - 1]);

        public static string Context(CityCrewDef crew, string region, int round) => crew.ContextId + "#" + Tag + ":" + region + ":" + round;

        /// <summary>The Big Run round of a game (region, round), or null.</summary>
        public static (string region, int round)? Of(MatchRequest r)
        {
            string tag = CityCrews.TagOf(r?.ContextId);
            if (tag == null || !tag.StartsWith(Tag + ":", StringComparison.Ordinal)) return null;
            var p = tag.Split(':');
            if (p.Length != 3 || For(p[1]) == null || !int.TryParse(p[2], out int round) || round < 1 || round > RoundsCount) return null;
            return (p[1], round);
        }

        public static MatchRequest Request(ContentCatalog c, CareerSaveData d, string region, int day)
        {
            var run = For(region);
            int round = NextRound(d?.world, region, day);
            var crew = CrewFor(run, round);
            if (crew == null || c == null || d == null) return null;
            var w = d.world ?? (d.world = new CityWorldSaveData());
            var s = State(w, region);
            if (s.Day != day) { s.Day = day; s.Round = 1; Save(w, region, s); }
            var r = CityCrews.Challenge(c, d, crew);
            r.ContextId = Context(crew, region, round);
            return r;
        }

        /// <summary>A finished Big Run game: pays the round, moves on, crowns you, or ends today's run. Returns the result line.</summary>
        public static string ApplyGame(CareerSaveData d, MatchRequest r, bool won, int day, DayOfWeek today, out long paid)
        {
            paid = 0;
            var of = Of(r);
            if (d == null || of == null) return null;
            var (region, round) = of.Value;
            var run = For(region);
            var w = d.world ?? (d.world = new CityWorldSaveData());
            var s = State(w, region);
            if (s.Day != day) { s.Day = day; s.Round = round; }
            if (!won)
            {
                s.DoneDay = day;
                s.Round = 0;
                Save(w, region, s);
                return Loc.T(run.Name) + ": " + Loc.T("OUT IN ROUND") + " " + round + ". " + Loc.T("Come back tomorrow.");
            }
            paid = Pay(run, round, today);
            if (d.streetCash == null) d.streetCash = new StreetCashSaveData();
            d.streetCash.cash += paid;
            d.streetCash.earned += paid;
            string line;
            if (round >= RoundsCount)
            {
                s.Titles++;
                s.DoneDay = day;
                s.Round = 0;
                line = Loc.T(KingTitle(region)) + "!  " + Loc.T(run.Name) + "  +" + StreetCash.Money(paid);
            }
            else
            {
                s.Round = round + 1;
                line = Loc.T(run.Name) + ": " + Loc.T("ROUND") + " " + round + " " + Loc.T("WON") + "  +" + StreetCash.Money(paid) + "  ·  " + Loc.T("NEXT") + ": " + CrewFor(run, round + 1).Name;
            }
            Save(w, region, s);
            return line;
        }

        public static string KingTitle(string region)
        {
            switch (region)
            {
                case WorldRegions.NewYork: return "KING OF NEW YORK";
                case WorldRegions.LosAngeles: return "KING OF LA";
                case WorldRegions.LasVegas: return "KING OF VEGAS";
                case WorldRegions.TheBay: return "KING OF THE BAY";
                default: return "KING OF BALLER CITY";
            }
        }

        /// <summary>The poster's card: the bracket, the purse, and the next game (or why not).</summary>
        public static List<CityActivity> Choices(CareerSaveData d, ContentCatalog c, string region, int day, DayOfWeek today, string back)
        {
            var list = new List<CityActivity>();
            var run = For(region);
            if (run == null) return list;
            string locked = Locked(d, region);
            int next = NextRound(d?.world, region, day);
            var crew = CrewFor(run, next);
            if (locked != null)
                list.Add(new CityActivity { Label = Loc.T("ENTER THE BIG RUN"), Locked = locked });
            else if (crew == null)
                list.Add(new CityActivity { Label = Loc.T("ENTER THE BIG RUN"), Locked = Loc.T("Today's run is over. A new bracket tomorrow.") });
            else
            {
                var r = Request(c, d, region, day);
                if (r != null) r.ReturnTo = back;
                list.Add(new CityActivity
                {
                    Label = next == 1 ? Loc.T("ENTER THE BIG RUN") : next == RoundsCount ? Loc.T("PLAY THE FINAL") : Loc.T("PLAY ROUND") + " " + next,
                    Detail = Loc.T("vs") + " " + crew.Name + "  ·  " + Loc.T(crew.TierName) + "  ·  " + Loc.T("PAYS") + " " + StreetCash.Money(Pay(run, next, today)),
                    Request = r, Primary = true,
                });
            }
            for (int k = 1; k <= RoundsCount; k++)
            {
                var rc = CrewFor(run, k);
                list.Add(new CityActivity
                {
                    Label = (k == RoundsCount ? Loc.T("FINAL") : Loc.T("ROUND") + " " + k) + ": " + rc?.Name,
                    Detail = StreetCash.Money(Pay(run, k, today)) + (rc != null ? "  ·  " + Loc.T(rc.Style) + "  ·  " + Loc.T(rc.TierName) : ""),
                    Action = CityAction.Info,
                });
            }
            return list;
        }

        public static string Poster(BigRunDef run, CityWorldSaveData w, DayOfWeek today) =>
            Loc.T("Three crews. Cash every round. Lose and you're out until tomorrow.") + "\n"
            + Loc.T("PURSE") + " " + StreetCash.Money(TotalPurse(run, today)) + (Weekend(today) ? "  ·  " + Loc.T("WEEKEND: DOUBLE PURSE!") : "")
            + "  ·  " + Loc.T("TITLES") + " " + Titles(w, run.Region);
    }
}
