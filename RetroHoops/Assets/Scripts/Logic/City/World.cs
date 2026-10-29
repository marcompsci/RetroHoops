using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>How the highway looks on the way in and out of a region (THE DRIVE's scenery).</summary>
    public enum WorldBiome { Suburbs = 0, Metro = 1, Coast = 2, Desert = 3, Bay = 4, Plains = 5 }

    /// <summary>One region of BALLER WORLD: a city with its own look, its districts, crews and Big Run.</summary>
    public sealed class WorldRegion
    {
        public string Id, Name, Short, Tagline, State;
        /// <summary>Its districts (the first is where your ride parks and where you arrive).</summary>
        public string[] Districts;
        /// <summary>Spot on the world map (x right 0..1, y up 0..1, roughly where it is in the country).</summary>
        public float MapX, MapY;
        public WorldBiome Biome;
        /// <summary>The issue of the comic that opens the road here (0 = open from the start).</summary>
        public int OpensAtIssue;
        public string Primary, Secondary;

        public string Main => Districts[0];
    }

    /// <summary>
    /// 28 Oct 2026 (Omari, BALLER WORLD phase W6): the regions. BALLER CITY is home; NEW YORK, LOS ANGELES, LAS VEGAS
    /// and THE BAY each have their own streets, buildings, courts, crews, side jobs and Big Run. You drive between them
    /// (THE DRIVE, under 30 seconds) and walk between the districts inside each one.
    /// </summary>
    public static class WorldRegions
    {
        public const string BallerCity = "baller_city";
        public const string NewYork = "new_york";
        public const string LosAngeles = "los_angeles";
        public const string LasVegas = "las_vegas";
        public const string TheBay = "the_bay";

        public static readonly WorldRegion[] All =
        {
            new WorldRegion
            {
                Id = BallerCity, Name = "BALLER CITY", Short = "BC", State = "HOME", Tagline = "Where it started. Your crib, your crew, the Baller Arena.",
                Districts = new[] { CityDistricts.FirstCallers, CityDistricts.Blacktop, CityDistricts.HoopHeights, CityDistricts.HolidayPark,
                                    CityDistricts.Boardwalk, CityDistricts.Downtown, CityDistricts.SchoolYard },
                MapX = 0.55f, MapY = 0.52f, Biome = WorldBiome.Suburbs, OpensAtIssue = 0, Primary = "#FFD166", Secondary = "#2E6FD8",
            },
            new WorldRegion
            {
                Id = NewYork, Name = "NEW YORK", Short = "NYC", State = "NEW YORK", Tagline = "Brownstones, the Cage and rooftops. Concrete that remembers every bucket.",
                Districts = new[] { CityDistricts.NyUptown, CityDistricts.NyMidtown },
                MapX = 0.88f, MapY = 0.68f, Biome = WorldBiome.Metro, OpensAtIssue = 3, Primary = "#F2C14E", Secondary = "#1B1B1E",
            },
            new WorldRegion
            {
                Id = LosAngeles, Name = "LOS ANGELES", Short = "LA", State = "CALIFORNIA", Tagline = "Palms, the beach courts and the freeway. Golden hour every hour.",
                Districts = new[] { CityDistricts.LaBeach, CityDistricts.LaEastside },
                MapX = 0.1f, MapY = 0.32f, Biome = WorldBiome.Coast, OpensAtIssue = 5, Primary = "#FFB703", Secondary = "#8338EC",
            },
            new WorldRegion
            {
                Id = LasVegas, Name = "LAS VEGAS", Short = "LV", State = "NEVADA", Tagline = "Neon towers, the desert lot and the biggest stage in the West.",
                Districts = new[] { CityDistricts.LvStrip, CityDistricts.LvOldTown },
                MapX = 0.24f, MapY = 0.42f, Biome = WorldBiome.Desert, OpensAtIssue = 7, Primary = "#FF2E88", Secondary = "#00F0FF",
            },
            new WorldRegion
            {
                Id = TheBay, Name = "THE BAY", Short = "BAY", State = "CALIFORNIA", Tagline = "Hills, fog, cable cars and the Town. The bridge brings you in.",
                Districts = new[] { CityDistricts.BayHills, CityDistricts.BayTown },
                MapX = 0.07f, MapY = 0.6f, Biome = WorldBiome.Bay, OpensAtIssue = 9, Primary = "#E63946", Secondary = "#1D3557",
            },
        };

        public static WorldRegion Find(string id) => Array.Find(All, r => r.Id == id);

        /// <summary>The region a district (or a building in it) belongs to. Indoors counts as the district outside.</summary>
        public static WorldRegion RegionOf(string district)
        {
            foreach (var r in All)
                if (Array.IndexOf(r.Districts, district) >= 0) return r;
            foreach (var r in All)
                if (Array.IndexOf(r.Districts, CityDistricts.OutsideOf(district)) >= 0) return r;
            return All[0];
        }

        /// <summary>Can you drive here yet? Home always; the rest open as the comic reaches them.</summary>
        public static bool Open(CareerSaveData d, WorldRegion r)
        {
            if (r == null) return false;
            if (r.OpensAtIssue <= 0) return true;
            var w = d?.world;
            return w != null && (w.regionsSeen.Contains(r.Id) || CityStory.IssueReached(w) >= r.OpensAtIssue);
        }

        public static string LockedReason(CareerSaveData d, WorldRegion r) =>
            Open(d, r) ? null : Loc.T("Opens in the comic, issue") + " #" + r.OpensAtIssue + ".";

        /// <summary>The region's crews (every crew whose court is in one of its districts).</summary>
        public static List<CityCrewDef> Crews(WorldRegion r)
        {
            var list = new List<CityCrewDef>();
            if (r == null) return list;
            foreach (var crew in CityCrews.All)
            {
                var home = CityDistricts.CourtHome(crew.CourtId);
                if (home != null && Array.IndexOf(r.Districts, home.Value.district) >= 0) list.Add(crew);
            }
            return list;
        }

        public static int CrewsBeaten(CitySaveData s, WorldRegion r)
        {
            int n = 0;
            if (s == null) return 0;
            foreach (var c in Crews(r)) if (s.crewsBeaten.Contains(c.Id)) n++;
            return n;
        }

        /// <summary>Marks a region as visited (first walk-in). True the first time.</summary>
        public static bool NoteArrive(CityWorldSaveData w, string district)
        {
            if (w == null) return false;
            var r = RegionOf(district);
            if (w.regionsSeen.Contains(r.Id)) return false;
            w.regionsSeen.Add(r.Id);
            return true;
        }
    }

    /// <summary>28 Oct 2026 (BALLER WORLD, phases W1–W10): what the save keeps for the comic, the guide, achievements, side jobs, Big Runs and the drive.</summary>
    [Serializable]
    public class CityWorldSaveData
    {
        // W1 the comic.
        /// <summary>The issue you're on (1-based; past the last = the comic is finished).</summary>
        public int issue = 1;
        /// <summary>The objective you're on in this issue, and the counter it started from (for "win 3 more" goals).</summary>
        public int step;
        public long stepBase;
        /// <summary>Issues whose opening pages you've read (so the cover shows once).</summary>
        public List<int> issuesOpened = new List<int>();
        /// <summary>The comic has started counting (set on the first visit, so old saves' totals don't finish goals by themselves).</summary>
        public bool started;
        /// <summary>The finale: VELVET SQUAD beaten.</summary>
        public bool velvetBeaten;
        /// <summary>An issue finished away from the city (in a game): its closing pages show when you're back (0 = none).</summary>
        public int pendingOutro;

        // W2 the guide: "" or "story" tracks the comic; "side:<id>" a side job; "off" hides the arrow.
        public string tracking = "";

        // W3 daily achievements.
        public int achDay = -1;
        public int[] achProgress = new int[CityAchievements.PerDay];
        /// <summary>Tiers already paid today (0 none, 1 bronze, 2 silver, 3 gold).</summary>
        public int achTierPaid;
        public int achStreak, achBestStreak, achLastDay = -1, achGoldDays, achTotal;
        /// <summary>Lifetime BADGES earned (ids).</summary>
        public List<string> badges = new List<string>();
        /// <summary>Today's tallies (reset with achDay): metres walked, talks, wins, games, points, assists, stops, crews, drives, side jobs, landmark games, regions played.</summary>
        public int tWalk, tTalk, tWins, tGames, tPoints, tAssists, tStops, tCrews, tDrives, tSide, tLandmark;
        public List<string> tRegions = new List<string>();
        /// <summary>Today's near misses on the highway, and the part of a metre walked that hasn't counted yet.</summary>
        public int tNear;
        public float walkCarry;

        // W4 side jobs.
        public List<string> sideDone = new List<string>();
        /// <summary>The side job you've taken ("" = none), its stage (errands: 0 go to the pickup, 1 deliver) and the found bits (collect jobs).</summary>
        public string sideActive = "";
        public int sideStage;
        public List<int> sideFound = new List<int>();

        // W5 Big Runs: one entry per region "region|day|round|doneDay|titles".
        public List<string> runs = new List<string>();

        // W6 the world.
        public List<string> regionsSeen = new List<string>();
        public int drives, cleanDrives, nearMisses;
        /// <summary>Fastest drive (seconds; 0 = none yet).</summary>
        public float bestDrive;
        /// <summary>Wins in each region ("region=count").</summary>
        public List<string> regionWins = new List<string>();
        /// <summary>Lifetime counts for badges.</summary>
        public long walkedTotal;

        public int RegionWins(string region)
        {
            foreach (var e in regionWins)
            {
                int eq = e.IndexOf('=');
                if (eq > 0 && e.Substring(0, eq) == region && int.TryParse(e.Substring(eq + 1), out int n)) return n;
            }
            return 0;
        }

        public void AddRegionWin(string region)
        {
            int n = RegionWins(region) + 1;
            regionWins.RemoveAll(e => e.StartsWith(region + "=", StringComparison.Ordinal));
            regionWins.Add(region + "=" + n);
        }

        public int TotalRegionWins
        {
            get
            {
                int n = 0;
                foreach (var r in WorldRegions.All) n += RegionWins(r.Id);
                return n;
            }
        }
    }
}
