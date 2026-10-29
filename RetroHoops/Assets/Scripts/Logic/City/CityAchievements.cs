using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public enum AchKind { Walk = 0, Talk = 1, Wins = 2, Games = 3, Points = 4, Assists = 5, Stops = 6, Crews = 7, Drives = 8, SideJobs = 9, Regions = 10, NearMisses = 11 }

    public sealed class AchDef
    {
        public AchKind Kind;
        public int Target;
        public string Text;
    }

    public sealed class WorldBadgeDef
    {
        public string Id, Name, Detail;
        public int Sp;
        public long Cash;
        public Func<CareerSaveData, bool> Earned;
    }

    /// <summary>What an achievement check did: achievements done today, a tier paid, a streak bonus, badges earned.</summary>
    public sealed class AchEvents
    {
        public readonly List<AchDef> Done = new List<AchDef>();
        /// <summary>The tier just reached (1 bronze, 2 silver, 3 gold; 0 none).</summary>
        public int Tier;
        public long Cash;
        public int Sp;
        public int StreakBonus;
        public readonly List<WorldBadgeDef> Badges = new List<WorldBadgeDef>();
        public bool Any => Done.Count > 0 || Tier > 0 || Badges.Count > 0;
    }

    /// <summary>
    /// 28 Oct 2026 (Omari, BALLER WORLD phase W3): DAILY ACHIEVEMENTS. Six a day, the same for everyone that day and new
    /// tomorrow: walk, talk, win, play, score, dish, get stops, beat crews, drive, finish side jobs, play in different
    /// regions, thread the traffic on the highway. Three done pays BRONZE, five SILVER, all six GOLD (cash and SP each
    /// time). A day with at least bronze keeps your STREAK going, with bonuses at 3, 7, 14 and 30 days. And BADGES for
    /// the big lifetime things (every Big Run, every region, the comic, the drive).
    /// </summary>
    public static class CityAchievements
    {
        public const int PerDay = 6;
        public static readonly int[] TierAt = { 3, 5, 6 };
        public static readonly long[] TierCash = { 10000, 15000, 25000 };
        public static readonly int[] TierSp = { 25, 50, 100 };
        public static readonly string[] TierNames = { "BRONZE", "SILVER", "GOLD" };
        public static readonly int[] StreakSteps = { 3, 7, 14, 30 };
        public static readonly int[] StreakSp = { 100, 250, 500, 1000 };

        private static readonly AchDef[] Pool =
        {
            new AchDef { Kind = AchKind.Walk, Target = 400, Text = "Walk 400 m" },
            new AchDef { Kind = AchKind.Talk, Target = 3, Text = "Talk to 3 people" },
            new AchDef { Kind = AchKind.Wins, Target = 2, Text = "Win 2 city games" },
            new AchDef { Kind = AchKind.Games, Target = 3, Text = "Play 3 city games" },
            new AchDef { Kind = AchKind.Points, Target = 30, Text = "Score 30 points today" },
            new AchDef { Kind = AchKind.Assists, Target = 6, Text = "6 assists today" },
            new AchDef { Kind = AchKind.Stops, Target = 5, Text = "5 steals and blocks today" },
            new AchDef { Kind = AchKind.Crews, Target = 1, Text = "Beat a crew" },
            new AchDef { Kind = AchKind.Drives, Target = 1, Text = "Take a drive" },
            new AchDef { Kind = AchKind.SideJobs, Target = 1, Text = "Finish a side job" },
            new AchDef { Kind = AchKind.Regions, Target = 2, Text = "Play in 2 different regions" },
            new AchDef { Kind = AchKind.NearMisses, Target = 5, Text = "5 near misses on the highway" },
        };

        /// <summary>Every achievement there is (I23: for the Spanish check).</summary>
        public static IReadOnlyList<AchDef> AllDefs => Pool;

        /// <summary>Today's six (the same all day for everyone; a different mix tomorrow).</summary>
        public static List<AchDef> For(int day)
        {
            var pool = new List<AchDef>(Pool);
            var list = new List<AchDef>();
            uint h = StableHash.Of("ach:" + day);
            for (int i = 0; i < PerDay && pool.Count > 0; i++)
            {
                h = h * 1664525u + 1013904223u;
                int k = (int)((h >> 9) % (uint)pool.Count);
                list.Add(pool[k]);
                pool.RemoveAt(k);
            }
            return list;
        }

        /// <summary>A new day clears today's tallies and progress (not the streak).</summary>
        public static void Roll(CityWorldSaveData w, int day)
        {
            if (w == null || w.achDay == day) return;
            w.achDay = day;
            w.achProgress = new int[PerDay];
            w.achTierPaid = 0;
            w.tWalk = w.tTalk = w.tWins = w.tGames = w.tPoints = w.tAssists = w.tStops = w.tCrews = w.tDrives = w.tSide = w.tLandmark = 0;
            w.tRegions = new List<string>();
            w.tNear = 0;
        }

        public static int Value(CityWorldSaveData w, AchKind k)
        {
            switch (k)
            {
                case AchKind.Walk: return w.tWalk;
                case AchKind.Talk: return w.tTalk;
                case AchKind.Wins: return w.tWins;
                case AchKind.Games: return w.tGames;
                case AchKind.Points: return w.tPoints;
                case AchKind.Assists: return w.tAssists;
                case AchKind.Stops: return w.tStops;
                case AchKind.Crews: return w.tCrews;
                case AchKind.Drives: return w.tDrives;
                case AchKind.SideJobs: return w.tSide;
                case AchKind.Regions: return w.tRegions.Count;
                default: return w.tNear;
            }
        }

        public static int DoneCount(CityWorldSaveData w, int day)
        {
            int n = 0;
            var defs = For(day);
            for (int i = 0; i < defs.Count; i++) if (w.achProgress != null && i < w.achProgress.Length && w.achProgress[i] >= defs[i].Target) n++;
            return n;
        }

        // ------------------------------------------------------------------ what counts

        public static void NoteWalk(CityWorldSaveData w, int day, float metres)
        {
            if (w == null || metres <= 0f || float.IsNaN(metres) || float.IsInfinity(metres)) return;
            Roll(w, day);
            w.walkCarry += Math.Min(metres, 50f);
            int whole = (int)w.walkCarry;
            if (whole <= 0) return;
            w.walkCarry -= whole;
            w.tWalk = Math.Min(100000, w.tWalk + whole);
            w.walkedTotal += whole;
        }

        public static void NoteTalk(CityWorldSaveData w, int day)
        {
            if (w == null) return;
            Roll(w, day);
            w.tTalk++;
        }

        /// <summary>A finished game started in the city (any region).</summary>
        public static void NoteGame(CityWorldSaveData w, int day, bool won, PlayerStatLine line, bool crew, string region)
        {
            if (w == null) return;
            Roll(w, day);
            w.tGames++;
            if (won) w.tWins++;
            if (won && crew) w.tCrews++;
            if (line != null)
            {
                w.tPoints += Math.Max(0, line.points);
                w.tAssists += Math.Max(0, line.assists);
                w.tStops += Math.Max(0, line.steals) + Math.Max(0, line.blocks);
            }
            if (!string.IsNullOrEmpty(region) && !w.tRegions.Contains(region)) w.tRegions.Add(region);
            if (won && !string.IsNullOrEmpty(region)) w.AddRegionWin(region);
        }

        public static void NoteDrive(CityWorldSaveData w, int day, int nearMisses)
        {
            if (w == null) return;
            Roll(w, day);
            w.tDrives++;
            w.tNear += Math.Max(0, nearMisses);
        }

        public static void NoteSideJob(CityWorldSaveData w, int day)
        {
            if (w == null) return;
            Roll(w, day);
            w.tSide++;
        }

        // ------------------------------------------------------------------ paying out

        /// <summary>
        /// Updates today's six from the tallies, pays any tier reached (bronze, silver, gold), keeps the streak, and
        /// awards any badges now earned. Safe to call any time (each pays once).
        /// </summary>
        public static AchEvents Update(CareerSaveData d, int day)
        {
            var ev = new AchEvents();
            var w = d?.world;
            if (w == null) return ev;
            Roll(w, day);
            var defs = For(day);
            if (w.achProgress == null || w.achProgress.Length != PerDay) w.achProgress = new int[PerDay];
            for (int i = 0; i < defs.Count; i++)
            {
                bool was = w.achProgress[i] >= defs[i].Target;
                w.achProgress[i] = Math.Min(defs[i].Target, Value(w, defs[i].Kind));
                if (!was && w.achProgress[i] >= defs[i].Target) { ev.Done.Add(defs[i]); w.achTotal++; }
            }
            int done = DoneCount(w, day);
            while (w.achTierPaid < TierAt.Length && done >= TierAt[w.achTierPaid])
            {
                int t = w.achTierPaid;
                w.achTierPaid++;
                ev.Tier = w.achTierPaid;
                ev.Cash += TierCash[t];
                ev.Sp += TierSp[t];
                if (t == 0)
                {
                    // Bronze keeps the streak.
                    w.achStreak = w.achLastDay == day - 1 ? w.achStreak + 1 : w.achLastDay == day ? w.achStreak : 1;
                    w.achLastDay = day;
                    w.achBestStreak = Math.Max(w.achBestStreak, w.achStreak);
                    for (int k = 0; k < StreakSteps.Length; k++)
                        if (w.achStreak == StreakSteps[k] || (k == StreakSteps.Length - 1 && w.achStreak > StreakSteps[k] && w.achStreak % 30 == 0))
                        {
                            ev.StreakBonus = StreakSp[k];
                            ev.Sp += StreakSp[k];
                        }
                }
                if (t == TierAt.Length - 1) w.achGoldDays++;
            }
            d.signalPoints += ev.Sp;
            if (ev.Cash > 0) Pay(d, ev.Cash);
            foreach (var b in CheckBadges(d)) { ev.Badges.Add(b); }
            return ev;
        }

        /// <summary>Your streak as of today (a streak you didn't keep yesterday reads 0 until you hit bronze today).</summary>
        public static int StreakNow(CityWorldSaveData w, int day) => w == null ? 0 : w.achLastDay >= day - 1 ? w.achStreak : 0;

        private static void Pay(CareerSaveData d, long cash)
        {
            if (d.streetCash == null) d.streetCash = new StreetCashSaveData();
            d.streetCash.cash += cash;
            d.streetCash.earned += cash;
        }

        // ------------------------------------------------------------------ badges

        private static WorldBadgeDef B(string id, string name, string detail, int sp, long cash, Func<CareerSaveData, bool> earned) =>
            new WorldBadgeDef { Id = id, Name = name, Detail = detail, Sp = sp, Cash = cash, Earned = earned };

        private static bool King(CareerSaveData d, string region) => CityBigRun.Titles(d.world, region) > 0;

        private static bool AllCrews(CareerSaveData d, string region)
        {
            var r = WorldRegions.Find(region);
            return d.city != null && WorldRegions.CrewsBeaten(d.city, r) >= WorldRegions.Crews(r).Count && WorldRegions.Crews(r).Count > 0;
        }

        public static readonly WorldBadgeDef[] Badges =
        {
            B("first_gold", "FIRST GOLD", "A gold day on the daily achievements.", 100, 10000, d => d.world.achGoldDays >= 1),
            B("golden_week", "GOLDEN WEEK", "7 gold days.", 400, 50000, d => d.world.achGoldDays >= 7),
            B("streak7", "SEVEN STRAIGHT", "A 7-day achievement streak.", 300, 25000, d => d.world.achBestStreak >= 7),
            B("achiever", "ACHIEVER", "100 daily achievements.", 500, 50000, d => d.world.achTotal >= 100),
            B("road_trip", "ROAD TRIP", "Take your first drive.", 50, 5000, d => d.world.drives >= 1),
            B("frequent", "FREQUENT DRIVER", "25 drives.", 250, 25000, d => d.world.drives >= 25),
            B("speed", "SPEED DEMON", "A drive in under 20 seconds.", 200, 20000, d => d.world.bestDrive > 0f && d.world.bestDrive < 20f),
            B("clean", "CLEAN DRIVER", "10 drives without a bump.", 200, 20000, d => d.world.cleanDrives >= 10),
            B("needle", "THREAD THE NEEDLE", "100 near misses on the highway.", 250, 25000, d => d.world.nearMisses >= 100),
            B("world_tour", "WORLD TOUR", "Visit all five regions.", 300, 50000, d => d.world.regionsSeen.Count >= WorldRegions.All.Length),
            B("king_bc", "KING OF BALLER CITY", "Win the Baller City Big Run.", 200, 0, d => King(d, WorldRegions.BallerCity)),
            B("king_ny", "KING OF NEW YORK", "Win the New York Big Run.", 300, 0, d => King(d, WorldRegions.NewYork)),
            B("king_la", "KING OF LA", "Win the Los Angeles Big Run.", 300, 0, d => King(d, WorldRegions.LosAngeles)),
            B("king_lv", "KING OF VEGAS", "Win the Las Vegas Big Run.", 300, 0, d => King(d, WorldRegions.LasVegas)),
            B("king_bay", "KING OF THE BAY", "Win the Bay Big Run.", 300, 0, d => King(d, WorldRegions.TheBay)),
            B("crews_ny", "NEW YORK SWEEP", "Beat every New York crew.", 300, 30000, d => AllCrews(d, WorldRegions.NewYork)),
            B("crews_la", "LA SWEEP", "Beat every Los Angeles crew.", 300, 30000, d => AllCrews(d, WorldRegions.LosAngeles)),
            B("crews_lv", "VEGAS SWEEP", "Beat every Las Vegas crew.", 300, 30000, d => AllCrews(d, WorldRegions.LasVegas)),
            B("crews_bay", "BAY SWEEP", "Beat every Bay crew.", 300, 30000, d => AllCrews(d, WorldRegions.TheBay)),
            B("neighbor", "GOOD NEIGHBOR", "Finish 10 side jobs.", 300, 25000, d => d.world.sideDone.Count >= 10),
            B("every_job", "ODD JOBS", "Finish every side job in the world.", 750, 100000, d => d.world.sideDone.Count >= CitySideJobs.All.Length),
            B("marathon", "MARATHON", "Walk 10 km in all.", 200, 10000, d => d.world.walkedTotal >= 10000),
            B("all_world", "ALL-WORLD", "Finish the comic: beat VELVET SQUAD.", 1000, 0, d => d.world.velvetBeaten),
        };

        public static WorldBadgeDef FindBadge(string id) => Array.Find(Badges, b => b.Id == id);

        /// <summary>Awards every badge now earned (each pays once). Returns the new ones.</summary>
        public static List<WorldBadgeDef> CheckBadges(CareerSaveData d)
        {
            var list = new List<WorldBadgeDef>();
            var w = d?.world;
            if (w == null) return list;
            foreach (var b in Badges)
            {
                if (w.badges.Contains(b.Id)) continue;
                if (!b.Earned(d)) continue;
                w.badges.Add(b.Id);
                d.signalPoints += b.Sp;
                if (b.Cash > 0) Pay(d, b.Cash);
                list.Add(b);
            }
            return list;
        }
    }
}
