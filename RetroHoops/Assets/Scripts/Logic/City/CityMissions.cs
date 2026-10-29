using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>What a city mission asks for.</summary>
    public enum CityMissionKind
    {
        /// <summary>Walk into a district (Ref = district id).</summary>
        Visit = 0,
        /// <summary>Talk to N different people.</summary>
        Talk = 1,
        /// <summary>Win N games started in the city.</summary>
        Wins = 2,
        /// <summary>Win on N different city courts.</summary>
        Courts = 3,
        /// <summary>Beat a crew (Ref = crew id).</summary>
        Crew = 4,
        /// <summary>Beat N BALLER crews.</summary>
        BallerCrews = 5,
        /// <summary>Beat N LEGENDARY crews.</summary>
        LegendaryCrews = 6,
        /// <summary>Win on N holiday courts.</summary>
        Holiday = 7,
        /// <summary>Score N points in one city game.</summary>
        Points = 8,
        /// <summary>Hit N threes (2-pointers under street rules: shots from beyond the arc) in one city game.</summary>
        Threes = 9,
        /// <summary>N assists in one city game.</summary>
        Assists = 10,
        /// <summary>N steals + blocks in one city game.</summary>
        Stops = 11,
        /// <summary>N ankle breakers in one city game.</summary>
        Ankles = 12,
        /// <summary>Win a city game by N or more.</summary>
        Margin = 13,
        /// <summary>A Practice Lab best (Ref = DrillKind name) of at least N (makes / score).</summary>
        Drill = 14,
        /// <summary>Beat N different crews (any tier).</summary>
        AnyCrews = 15,
        /// <summary>Buy N things at the Corner Store.</summary>
        Shop = 16,
        /// <summary>N haircuts at Fresh Cuts.</summary>
        Haircut = 17,
        /// <summary>Fast travel N times from the city map.</summary>
        Travel = 18,
        /// <summary>Win N City Championships.</summary>
        Titles = 19,
        /// <summary>Finish N sets of daily City Challenges.</summary>
        DailySets = 20,
        /// <summary>Win N call-outs.</summary>
        Callouts = 21,
        /// <summary>Earn N dollars of street cash in all.</summary>
        Earned = 22,
    }

    public sealed class CityMissionDef
    {
        public string Id, Title, Detail, Ref;
        public CityMissionKind Kind;
        public int Target = 1;
        public int Sp, Fans;
        /// <summary>Missions come in chapters: the next chapter opens when this one is done.</summary>
        public int Chapter;
    }

    /// <summary>What a finished city game did to your missions and crews.</summary>
    public sealed class CityGameResult
    {
        public CityCrewDef Crew;
        public bool CrewFirstWin;
        public readonly List<CityMissionDef> Completed = new List<CityMissionDef>();
        public int Sp, Fans;

        public string Note()
        {
            var parts = new List<string>();
            if (Crew != null) parts.Add(CrewFirstWin ? Loc.T("CREW DOWN:") + " " + Crew.Name + " (" + Loc.T(Crew.TierName) + ")" : Crew.Name + " " + Loc.T("BEATEN AGAIN"));
            foreach (var m in Completed) parts.Add(Loc.T("MISSION COMPLETE:") + " " + Loc.T(m.Title));
            if (Sp > 0 || Fans > 0) parts.Add("+" + Sp + " SP  ·  +" + Fans + " " + Loc.T("FANS"));
            return string.Join("\n", parts);
        }
    }

    /// <summary>
    /// 17 Oct 2026 (Omari): BALLER CITY missions. Five chapters that walk you from your first run at the corner court
    /// to beating every LEGENDARY crew: explore, talk, win on new courts, beat crews, light up the holiday courts, and
    /// big single-game lines (points, threes, dimes, stops, ankle breakers). Completing one pays SP and fans straight
    /// to your career (no claiming), and the next chapter opens when a chapter is done. Progress lives in the city save.
    /// </summary>
    public static class CityMissions
    {
        public static readonly CityMissionDef[] All =
        {
            // Chapter 1: NEW IN TOWN.
            M(1, "c1.blacktop", "FIND THE BLACKTOP", "Walk east to the Blacktop District.", CityMissionKind.Visit, 1, 40, 5, CityDistricts.Blacktop),
            M(1, "c1.talk", "MEET THE LOCALS", "Talk to 3 different people in the city.", CityMissionKind.Talk, 3, 40, 5),
            M(1, "c1.firstwin", "FIRST RUN", "Win a game on any city court.", CityMissionKind.Wins, 1, 75, 10),
            // Chapter 2: MAKE A NAME.
            M(2, "c2.heights", "UP THE AVENUE", "Walk north to Hoop Heights.", CityMissionKind.Visit, 1, 50, 5, CityDistricts.HoopHeights),
            M(2, "c2.courts", "COURT HOPPER", "Win on 3 different city courts.", CityMissionKind.Courts, 3, 120, 15),
            M(2, "c2.points", "BUCKET GETTER", "Score 12 points in one city game.", CityMissionKind.Points, 12, 100, 10),
            M(2, "c2.baller", "CALL OUT A BALLER", "Beat any BALLER crew.", CityMissionKind.BallerCrews, 1, 150, 20),
            // Chapter 3: CREW HUNTER.
            M(3, "c3.splash", "SHUT DOWN THE SPLASH", "Beat SPLASH FACTORY at Coin-Op Court.", CityMissionKind.Crew, 1, 160, 20, "splash"),
            M(3, "c3.wreckers", "PROTECT THE RIM", "Beat the RIM WRECKERS at the Skate Bowl.", CityMissionKind.Crew, 1, 160, 20, "wreckers"),
            M(3, "c3.dimes", "DIME TIME", "Get 4 assists in one city game.", CityMissionKind.Assists, 4, 120, 10),
            M(3, "c3.ballers", "BALLER STATUS", "Beat 3 BALLER crews (LEGENDARY crews open).", CityMissionKind.BallerCrews, 3, 250, 30),
            // Chapter 4: HOLIDAY SEASON.
            M(4, "c4.park", "HOLIDAY PARK", "Walk south to Holiday Park.", CityMissionKind.Visit, 1, 50, 5, CityDistricts.HolidayPark),
            M(4, "c4.holiday", "DECK THE COURTS", "Win on 2 holiday courts.", CityMissionKind.Holiday, 2, 200, 25),
            M(4, "c4.ankles", "HOLIDAY ANKLES", "Break 2 ankles in one city game.", CityMissionKind.Ankles, 2, 150, 15),
            M(4, "c4.stops", "STOCKING STUFFER", "Get 4 steals and blocks in one city game.", CityMissionKind.Stops, 4, 150, 15),
            M(4, "c4.drill", "GYM RAT", "Make 15 in Free Shoot at the Baller Gym.", CityMissionKind.Drill, 15, 120, 10, nameof(DrillKind.FreeShoot)),
            // Chapter 5: LEGENDARY.
            M(5, "c5.legend", "LEGEND KILLER", "Beat a LEGENDARY crew.", CityMissionKind.LegendaryCrews, 1, 300, 40),
            M(5, "c5.threes", "FROM DOWNTOWN", "Hit 5 shots from beyond the arc in one city game.", CityMissionKind.Threes, 5, 200, 20),
            M(5, "c5.margin", "NO MERCY", "Win a city game by 8 or more.", CityMissionKind.Margin, 8, 200, 20),
            M(5, "c5.allholiday", "EVERY HOLIDAY", "Win on all 4 holiday courts.", CityMissionKind.Holiday, 4, 350, 40),
            M(5, "c5.legends", "RUN THE CITY", "Beat 6 LEGENDARY crews.", CityMissionKind.LegendaryCrews, 6, 750, 100),
            // Chapter 6: ALL CITY (17 Oct 2026, phase 3).
            M(6, "c6.boardwalk", "BEACH RUN", "Walk east of the Blacktop to the Boardwalk.", CityMissionKind.Visit, 1, 60, 5, CityDistricts.Boardwalk),
            M(6, "c6.downtown", "BRIGHT LIGHTS", "Walk east of Hoop Heights to Downtown.", CityMissionKind.Visit, 1, 60, 5, CityDistricts.Downtown),
            M(6, "c6.school", "BACK TO SCHOOL", "Walk west of Holiday Park to the School Yard.", CityMissionKind.Visit, 1, 60, 5, CityDistricts.SchoolYard),
            M(6, "c6.travel", "TAXI", "Fast travel from the city MAP.", CityMissionKind.Travel, 1, 40, 5),
            M(6, "c6.shop", "FRESH GEAR", "Buy something at the Corner Store.", CityMissionKind.Shop, 1, 60, 10),
            M(6, "c6.cut", "FRESH CUT", "Get a haircut at Fresh Cuts.", CityMissionKind.Haircut, 1, 60, 10),
            M(6, "c6.crews", "CREW COLLECTOR", "Beat 12 different crews.", CityMissionKind.AnyCrews, 12, 400, 50),
            M(6, "c6.wins", "CITY LEGEND", "Win 25 city games.", CityMissionKind.Wins, 25, 400, 50),
            M(6, "c6.legends", "ALL-CITY", "Beat every LEGENDARY crew.", CityMissionKind.LegendaryCrews, -1, 1000, 150),
            // Chapter 7: BIG TIME (17 Oct 2026, phases 5–7).
            M(7, "c7.crib", "HOME SWEET HOME", "Walk into your crib (your front door, First Callers).", CityMissionKind.Visit, 1, 50, 5, CityDistricts.Crib),
            M(7, "c7.arena", "THE BIG ROOM", "Walk into the Baller Arena, Downtown.", CityMissionKind.Visit, 1, 50, 5, CityDistricts.Arena),
            M(7, "c7.callout", "ANSWER THE CALL", "Win a call-out.", CityMissionKind.Callouts, 1, 150, 15),
            M(7, "c7.daily", "DAILY GRIND", "Finish all three City Challenges in a day.", CityMissionKind.DailySets, 1, 200, 20),
            M(7, "c7.title", "CITY CHAMPION", "Win the City Championship at the Baller Arena.", CityMissionKind.Titles, 1, 500, 75),
            M(7, "c7.bank", "QUARTER MILLION", "Earn $250,000 in street cash.", CityMissionKind.Earned, 250000, 400, 50),
            M(7, "c7.dynasty", "DYNASTY", "Win 3 City Championships.", CityMissionKind.Titles, 3, 1000, 150),
        };

        public static readonly string[] ChapterNames = { "", "NEW IN TOWN", "MAKE A NAME", "CREW HUNTER", "HOLIDAY SEASON", "LEGENDARY", "ALL CITY", "BIG TIME" };
        public static int Chapters => ChapterNames.Length - 1;

        private static CityMissionDef M(int chapter, string id, string title, string detail, CityMissionKind kind, int target, int sp, int fans, string reference = null) =>
            new CityMissionDef { Chapter = chapter, Id = id, Title = title, Detail = detail, Kind = kind, Target = target, Sp = sp, Fans = fans, Ref = reference };

        public static CityMissionDef Find(string id) => Array.Find(All, m => m.Id == id);

        /// <summary>Baller City's LEGENDARY crews (28 Oct 2026: the other regions' crews don't move ALL-CITY's goal).</summary>
        public static int LegendaryCount
        {
            get
            {
                int n = 0;
                foreach (var c in CityCrews.All) if (c.Tier == CityCrewTier.Legendary && InBallerCity(c)) n++;
                return n;
            }
        }

        public static bool InBallerCity(CityCrewDef c) => WorldRegions.RegionOf(CityDistricts.CourtHome(c.CourtId)?.district ?? CityDistricts.FirstCallers).Id == WorldRegions.BallerCity;

        public static int TargetOf(CityMissionDef m) => m.Kind == CityMissionKind.LegendaryCrews && m.Target < 0 ? LegendaryCount : m.Target;

        public static bool Done(CitySaveData s, CityMissionDef m) => s?.missionsDone != null && s.missionsDone.Contains(m.Id);

        /// <summary>The chapter you're on: the first one with a mission left (past the last = all done).</summary>
        public static int CurrentChapter(CitySaveData s)
        {
            for (int ch = 1; ch <= Chapters; ch++)
                foreach (var m in All)
                    if (m.Chapter == ch && !Done(s, m)) return ch;
            return Chapters + 1;
        }

        /// <summary>Missions you can work on now (the current chapter's unfinished ones).</summary>
        public static List<CityMissionDef> Active(CitySaveData s)
        {
            int ch = CurrentChapter(s);
            var list = new List<CityMissionDef>();
            foreach (var m in All) if (m.Chapter == ch && !Done(s, m)) list.Add(m);
            return list;
        }

        /// <summary>How far along a mission is: (current, target). Single-game missions show your best line.</summary>
        public static (int now, int target) Progress(CitySaveData s, CareerSaveData career, CityMissionDef m)
        {
            int target = TargetOf(m);
            if (s == null) return (0, target);
            if (Done(s, m)) return (target, target);
            int now;
            switch (m.Kind)
            {
                case CityMissionKind.Visit: now = s.seen.Contains(m.Ref) ? 1 : 0; break;
                case CityMissionKind.Talk: now = s.talkedTo.Count; break;
                case CityMissionKind.Wins: now = s.cityWins; break;
                case CityMissionKind.Courts: now = s.courtsWon.Count; break;
                case CityMissionKind.Crew: now = s.crewsBeaten.Contains(m.Ref) ? 1 : 0; break;
                case CityMissionKind.BallerCrews: now = CityCrews.BallerBeaten(s); break;
                case CityMissionKind.LegendaryCrews: now = LegendaryBeaten(s); break;
                case CityMissionKind.Holiday: now = s.holidayWins.Count; break;
                case CityMissionKind.Drill: now = DrillValue(career, m.Ref); break;
                case CityMissionKind.AnyCrews: now = s.crewsBeaten.Count; break;
                case CityMissionKind.Shop: now = s.purchases; break;
                case CityMissionKind.Haircut: now = s.haircuts; break;
                case CityMissionKind.Travel: now = s.fastTravels; break;
                case CityMissionKind.Titles: now = career?.bigTime?.arenaTitles ?? 0; break;
                case CityMissionKind.DailySets: now = career?.bigTime?.dailySets ?? 0; break;
                case CityMissionKind.Callouts: now = career?.bigTime?.calloutsWon ?? 0; break;
                case CityMissionKind.Earned: now = (int)Math.Min(int.MaxValue, career?.streetCash?.earned ?? 0); break;
                default: now = Best(s, m.Kind); break;
            }
            return (Math.Min(now, target), target);
        }

        public static int LegendaryBeaten(CitySaveData s)
        {
            int n = 0;
            foreach (var id in s.crewsBeaten)
            {
                var c = CityCrews.Find(id);
                if (c != null && c.Tier == CityCrewTier.Legendary && InBallerCity(c)) n++;
            }
            return n;
        }

        private static int DrillValue(CareerSaveData career, string drill)
        {
            var b = career?.practice;
            if (b == null) return 0;
            switch (drill)
            {
                case nameof(DrillKind.FreeShoot): return b.freeShootMakes;
                case nameof(DrillKind.PassingTargets): return b.passingScore;
                case nameof(DrillKind.ThreePoint): return b.threePointBest;
                default: return 0;
            }
        }

        private static int Best(CitySaveData s, CityMissionKind kind)
        {
            switch (kind)
            {
                case CityMissionKind.Points: return s.bestPoints;
                case CityMissionKind.Threes: return s.bestThrees;
                case CityMissionKind.Assists: return s.bestAssists;
                case CityMissionKind.Stops: return s.bestStops;
                case CityMissionKind.Ankles: return s.bestAnkles;
                case CityMissionKind.Margin: return s.bestMargin;
                default: return 0;
            }
        }

        /// <summary>
        /// Marks every mission in the current chapter (and the chapters it opens) that's now met as done, pays it out
        /// to the career, and returns them. Safe to call any time; a mission pays once.
        /// </summary>
        public static List<CityMissionDef> Check(CareerSaveData career)
        {
            var done = new List<CityMissionDef>();
            var s = career?.city;
            if (s == null) return done;
            bool progressed = true;
            while (progressed)
            {
                progressed = false;
                foreach (var m in Active(s))
                {
                    var (now, target) = Progress(s, career, m);
                    if (now < target) continue;
                    s.missionsDone.Add(m.Id);
                    career.signalPoints += m.Sp;
                    career.fans += m.Fans;
                    done.Add(m);
                    progressed = true;
                }
            }
            return done;
        }

        public static bool IsHolidayCourt(string courtId) =>
            courtId == Holidays.ChristmasCourtId || courtId == Holidays.HalloweenCourtId || courtId == Holidays.EasterCourtId || courtId == Holidays.FourthCourtId;

        /// <summary>
        /// A finished game that was started in the city (the request carries a city ReturnTo): wins, courts, crews,
        /// best lines, then any missions that completes. <paramref name="human"/> is your box-score line (may be null).
        /// </summary>
        public static CityGameResult ApplyGame(CareerSaveData career, MatchRequest r, bool won, int margin, PlayerStatLine human)
        {
            var result = new CityGameResult();
            if (career == null || r == null || !CityDistricts.TryParseReturn(r.ReturnTo, out _, out _)) return result;
            if (r.Mode == GameMode.Practice || r.Mode == GameMode.Tutorial) return result;
            var s = career.city ?? (career.city = new CitySaveData());
            s.cityGames++;
            if (human != null)
            {
                s.bestPoints = Math.Max(s.bestPoints, human.points);
                s.bestThrees = Math.Max(s.bestThrees, human.arcMade);
                s.bestAssists = Math.Max(s.bestAssists, human.assists);
                s.bestStops = Math.Max(s.bestStops, human.steals + human.blocks);
                s.bestAnkles = Math.Max(s.bestAnkles, human.ankleBreakers);
            }
            if (won)
            {
                s.cityWins++;
                s.bestMargin = Math.Max(s.bestMargin, margin);
                if (!string.IsNullOrEmpty(r.CourtId) && !s.courtsWon.Contains(r.CourtId)) s.courtsWon.Add(r.CourtId);
                if (IsHolidayCourt(r.CourtId) && !s.holidayWins.Contains(r.CourtId)) s.holidayWins.Add(r.CourtId);
                var crew = CityCrews.FromContext(r.ContextId);
                if (crew != null)
                {
                    result.Crew = crew;
                    result.CrewFirstWin = !s.crewsBeaten.Contains(crew.Id);
                    if (result.CrewFirstWin) s.crewsBeaten.Add(crew.Id);
                }
            }
            int sp0 = career.signalPoints, fans0 = career.fans;
            result.Completed.AddRange(Check(career));
            result.Sp = career.signalPoints - sp0;
            result.Fans = career.fans - fans0;
            return result;
        }

        /// <summary>Talking to someone new counts toward MEET THE LOCALS.</summary>
        public static bool NoteTalk(CitySaveData s, string npcId)
        {
            if (s == null || string.IsNullOrEmpty(npcId) || s.talkedTo.Contains(npcId)) return false;
            if (s.talkedTo.Count < 64) s.talkedTo.Add(npcId);
            return true;
        }
    }
}
