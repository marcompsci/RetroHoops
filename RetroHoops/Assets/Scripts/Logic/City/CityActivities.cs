using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>What a city choice does when picked.</summary>
    public enum CityAction { PlayMatch = 0, OpenRiseHub = 1, OpenLockerRoom = 2, Info = 3, /** 17 Oct 2026 (phase 3): the shops. */ Buy = 4, Equip = 5, Haircut = 6, /** 17 Oct 2026: THE STREETS walls. */ OpenStreets = 7, StreetsBoard = 8, BuyWithCash = 9,
        /** 28 Oct 2026 (BALLER WORLD): drive to a region (ItemId = region id), open the world map, take a side job, read the comic. */ Drive = 10, WorldMap = 11, AcceptJob = 12, Comic = 13 }

    /// <summary>
    /// One thing you can do at a court, a door, a station or with a person: a label, a line of detail, and either a
    /// game (built with the game's existing request builders, so rules, unlocks and rewards are the usual ones) or a
    /// screen to open. Locked choices say why and can't be picked.
    /// </summary>
    public sealed class CityActivity
    {
        public string Label, Detail;
        public CityAction Action;
        public MatchRequest Request;
        /// <summary>Why it can't be picked now (null = it can).</summary>
        public string Locked;
        public bool Primary;
        /// <summary>The shops: a cosmetic id, or a barber choice ("style:2").</summary>
        public string ItemId;
        public int Price;
    }

    /// <summary>
    /// 16 Oct 2026 (Omari): the menus behind the city's courts, doors, stations and people. Every game goes through
    /// the game's existing modes (pickup, the STREET TOUR, HOMECOURT, THE PARK, Rival Challenge, Practice Lab drills,
    /// the Halloween Game) and every one returns to the same spot in the city afterwards.
    /// </summary>
    public static class CityActivities
    {
        /// <summary>The choices at a court.</summary>
        public static List<CityActivity> ForCourt(CityMap map, CityCourt court, CareerSaveData career, ContentCatalog c, int day)
        {
            var list = new List<CityActivity>();
            if (court == null || c == null || career == null) return list;
            string back = CityDistricts.ReturnTag(map.DistrictId, court.Id);
            string diff = career.settings?.difficultyId ?? DefaultContent.DefaultDifficultyId;

            if (court.Use == CityCourtUse.Rise)
            {
                list.Add(new CityActivity { Label = "RISE HUB", Detail = "Your season, your crew, your next game.", Action = CityAction.OpenRiseHub, Primary = true });
                var rival = RivalEngine.Challenge(career, c, diff);
                list.Add(rival != null
                    ? new CityActivity { Label = "RIVAL CHALLENGE", Detail = "The rival crew is here. Win for a bonus.", Request = Tag(rival, back) }
                    : new CityActivity { Label = "RIVAL CHALLENGE", Locked = "From week " + RivalEngine.ChallengeFromWeek + " of a Rise season, once a season." });
                var shoot = MatchRequest.PracticeDefault();
                shoot.Drill = (int)DrillKind.FreeShoot;
                shoot.DifficultyId = diff;
                list.Add(new CityActivity { Label = "SHOOTAROUND", Detail = "Free Shoot for a minute. Your best is saved.", Request = Tag(shoot, back) });
                return list;
            }

            // 17 Oct 2026 (phase 5): the arena floor is the City Championship.
            if (court.Use == CityCourtUse.Arena)
            {
                var arena = CityArena.Choices(career, c, day, back);
                if (CityStory.VelvetOpen(career))
                {
                    var velvet = CityStory.VelvetRequest(c, career);
                    velvet.ReturnTo = back;
                    arena.Insert(0, new CityActivity { Label = Loc.T("THE VELVET INVITATIONAL"), Primary = true, Request = velvet,
                                                       Detail = Loc.T("VELVET SQUAD") + "  ·  " + Loc.T("LEGENDARY") + "  ·  " + Loc.T("The comic's last page.") });
                }
                return arena;
            }

            // 17 Oct 2026: a holiday court plays its Holiday Game (and its crew) instead of pickup.
            if (court.Use == CityCourtUse.Holiday)
            {
                var h = Holidays.All.Find(x => x.CourtId == court.CourtId);
                var mine = Secrets.PlayableTeams(c, career.secrets);
                var theirs = Secrets.OpponentTeams(c, career.secrets);
                if (h != null && mine.Count > 0 && theirs.Count > 0)
                {
                    var opp = theirs[(int)(StableHash.Of(court.Id + ":" + day) % (uint)theirs.Count)];
                    list.Add(new CityActivity { Label = Loc.T("HOLIDAY GAME"), Primary = true,
                                                Detail = h.Name + "  ·  " + Loc.T("vs") + " " + opp.FullName.ToUpperInvariant(),
                                                Request = Tag(Holidays.Request(h.Theme, mine[0].id, opp.id, diff), back) });
                }
                AddCrew(list, court, career, c, back);
                return list;
            }

            // A street court: pickup against today's crew, plus anything the court's legends and tour crews offer.
            var opponents = Streets.Opponents(c);
            if (opponents.Count > 0)
            {
                var opp = opponents[(int)(StableHash.Of(court.Id + ":" + day) % (uint)opponents.Count)];
                var pickup = Streets.Pickup(c, career, opp.id, court.CourtId);
                if (c.Court(court.CourtId) != null) pickup.CourtId = court.CourtId; // played right here, whatever court this is
                list.Add(new CityActivity
                {
                    Label = "PICKUP 3 ON 3", Primary = true,
                    Detail = Loc.T("Today:") + " " + opp.FullName.ToUpperInvariant() + "  ·  " + Loc.T("TO 21, WIN BY 2") + "  ·  " + DifficultyName(c, diff),
                    Request = Tag(pickup, back),
                });
            }

            AddCrew(list, court, career, c, back);

            var s = career.streets;
            if (s != null && StreetsHome.Claimed(s) && s.homeCourt == court.CourtId)
            {
                var defend = StreetsHome.Defend(c, career);
                list.Add(defend != null
                    ? new CityActivity { Label = s.homeTaken ? "TAKE IT BACK" : "DEFEND HOMECOURT", Detail = StreetsHome.Name(s) + ": " + StreetsHome.Challenger(s).Crew + " " + Loc.T("wants it."), Request = Tag(defend, back) }
                    : new CityActivity { Label = "DEFEND HOMECOURT", Locked = "Claim a court in THE STREETS ► HOMECOURT." });
            }

            foreach (var stop in StreetsTour.Stops)
            {
                if (stop.CourtId != court.CourtId || stop.Finale) continue;
                bool open = s != null && StreetsTour.Open(s, stop);
                list.Add(open
                    ? new CityActivity { Label = "TOUR: " + stop.Crew, Detail = stop.Leader + "  ·  " + Loc.T("TO") + " " + stop.Target + "  ·  " + Loc.T("STARS") + " " + StreetsTour.Stars(s, stop) + "/3",
                                         Request = Tag(StreetsTour.Challenge(c, career, stop), back) }
                    : new CityActivity { Label = "TOUR: " + stop.Crew, Locked = "Clear the city before it on the STREET TOUR." });
            }

            foreach (var ch in Street.Challengers)
            {
                if (ch.CourtId != court.CourtId) continue;
                bool open = career.street != null && Street.Unlocked(career.street, ch);
                list.Add(open
                    ? new CityActivity { Label = "THE PARK: " + ch.Nickname, Detail = ch.Name + "  ·  " + (ch.Size == 1 ? "1 ON 1" : ch.Size + " ON " + ch.Size),
                                         Request = Tag(Street.Challenge(c, career, ch), back) }
                    : new CityActivity { Label = "THE PARK: " + ch.Nickname, Locked = "Reach " + Street.RepNames[Math.Min(Street.RepNames.Length - 1, ch.Tier)] + " rep in THE PARK." });
            }
            return list;
        }

        /// <summary>The choices at a door or a station.</summary>
        public static List<CityActivity> ForPortal(CityMap map, CityPortal portal, CareerSaveData career, ContentCatalog c, int day = 0)
        {
            var list = new List<CityActivity>();
            if (portal == null) return list;
            string diff = career?.settings?.difficultyId ?? DefaultContent.DefaultDifficultyId;
            string back = CityDistricts.ReturnTag(map.DistrictId, "gym." + portal.Id);
            if (portal.Target != null && portal.Target.StartsWith("drill:", StringComparison.Ordinal))
            {
                if (!Enum.TryParse(portal.Target.Substring(6), out DrillKind drill)) return list;
                var r = MatchRequest.PracticeDefault();
                r.Drill = (int)drill;
                r.DifficultyId = diff;
                string best = PracticeBest(career, drill);
                list.Add(new CityActivity { Label = "START " + portal.Label, Detail = "Practice Lab." + (best != null ? "  " + Loc.T("BEST") + ": " + best : ""), Request = Tag(r, back), Primary = true });
                return list;
            }
            switch (portal.Target)
            {
                case "hq":
                    list.Add(new CityActivity { Label = "RISE HUB", Detail = "The First Callers' season.", Action = CityAction.OpenRiseHub, Primary = true });
                    break;
                case "home":
                case "lockers":
                    list.Add(new CityActivity { Label = "LOCKER ROOM", Detail = "Your look, your kits, your team.", Action = CityAction.OpenLockerRoom, Primary = true });
                    break;
                case "halloween":
                {
                    var mine = Secrets.PlayableTeams(c, career?.secrets);
                    var theirs = Secrets.OpponentTeams(c, career?.secrets);
                    if (mine.Count > 0 && theirs.Count > 0)
                    {
                        var opp = theirs[(int)(StableHash.Of("halloween:" + DateTime.Now.DayOfYear) % (uint)theirs.Count)];
                        var r = Holidays.Request(HolidayTheme.Halloween, mine[0].id, opp.id, diff);
                        list.Add(new CityActivity { Label = "HALLOWEEN GAME", Detail = Loc.T("Pumpkin Patch Court vs") + " " + opp.FullName.ToUpperInvariant(),
                                                    Request = Tag(r, CityDistricts.ReturnTag(map.DistrictId, "gym.halloween")), Primary = true });
                    }
                    break;
                }
                case "reception":
                    list.Add(new CityActivity { Label = "TODAY AT THE GYM", Detail = "Free Shoot, Passing Targets, Dribble Lane, the Three-Point Drill and Around the World are on the floor. The Halloween court is through the left door.", Action = CityAction.Info });
                    break;
                case "shop":
                    list.AddRange(CityShop.Choices(career, c, day));
                    break;
                case "barber.chair":
                    list.AddRange(CityBarber.Choices(career));
                    break;
                case "arena.desk":
                    // 28 Oct 2026 (BALLER WORLD): the comic's finale, the Velvet Invitational, is booked at the desk.
                    if (career != null && CityStory.VelvetOpen(career))
                    {
                        var velvet = CityStory.VelvetRequest(c, career);
                        velvet.ReturnTo = CityDistricts.ReturnTag(map.DistrictId, "court.arena");
                        list.Add(new CityActivity { Label = Loc.T("THE VELVET INVITATIONAL"), Primary = true, Request = velvet,
                                                    Detail = Loc.T("VELVET SQUAD") + "  ·  " + Loc.T("LEGENDARY") + "  ·  " + Loc.T("The comic's last page.") });
                    }
                    list.AddRange(CityArena.Choices(career, c, day, CityDistricts.ReturnTag(map.DistrictId, "court.arena")));
                    break;
                case "ride":
                    list.AddRange(RideChoices(map, career));
                    break;
                case "bigrun":
                {
                    var region = WorldRegions.RegionOf(map.DistrictId).Id;
                    list.AddRange(CityBigRun.Choices(career, c, region, day, DateTime.Now.DayOfWeek, CityDistricts.ReturnTag(map.DistrictId, NearestSpawn(map, portal.Center))));
                    break;
                }
                case "crib.trophies":
                case "crib.banners":
                case "crib.safe":
                    list.AddRange(CityCrib.Choices(portal, career));
                    break;
                case "streets":
                    list.AddRange(StreetsWall(map, portal, career, c, day));
                    break;
                case "school":
                    list.Add(new CityActivity { Label = "BALLER CITY HIGH", Detail = "Classes are out. The courts out front are open.", Action = CityAction.Info });
                    break;
                case "store":
                case "barber":
                    list.Add(new CityActivity { Label = portal.Label, Locked = "Opening soon." });
                    break;
            }
            return list;
        }

        /// <summary>
        /// 28 Oct 2026 (BALLER WORLD): YOUR RIDE. The world map, and a drive to every region you can reach (under 30
        /// seconds); regions the comic hasn't reached yet say when they open.
        /// </summary>
        public static List<CityActivity> RideChoices(CityMap map, CareerSaveData career)
        {
            var list = new List<CityActivity> { new CityActivity { Label = Loc.T("WORLD MAP"), Detail = Loc.T("Every region, and where the comic goes next."), Action = CityAction.WorldMap } };
            var here = WorldRegions.RegionOf(map.DistrictId);
            foreach (var r in WorldRegions.All)
            {
                if (r.Id == here.Id) continue;
                string locked = WorldRegions.LockedReason(career, r);
                list.Add(locked != null
                    ? new CityActivity { Label = Loc.T("DRIVE TO") + " " + Loc.T(r.Name), Locked = locked }
                    : new CityActivity { Label = Loc.T("DRIVE TO") + " " + Loc.T(r.Name), Detail = Loc.T(r.Tagline), Action = CityAction.Drive, ItemId = r.Id,
                                         Primary = CityStory.Current(career?.world)?.Region == r.Id });
            }
            return list;
        }

        /// <summary>The choices with a person.</summary>
        public static List<CityActivity> ForNpc(CityMap map, CityNpc npc, CareerSaveData career, ContentCatalog c, Vec2 where)
        {
            var list = new List<CityActivity>();
            if (npc == null || c == null || career == null) return list;
            string back = CityDistricts.ReturnTag(map.DistrictId, NearestSpawn(map, where));
            string diff = career.settings?.difficultyId ?? DefaultContent.DefaultDifficultyId;
            switch (npc.Role)
            {
                case CityNpcRole.Rival:
                {
                    var r = RivalEngine.Challenge(career, c, diff);
                    list.Add(r != null
                        ? new CityActivity { Label = "CHALLENGE", Detail = "Rival Challenge: " + npc.Team?.FullName.ToUpperInvariant(), Request = Tag(r, back), Primary = true }
                        : new CityActivity { Label = "CHALLENGE", Locked = "From week " + RivalEngine.ChallengeFromWeek + " of a Rise season, once a season." });
                    break;
                }
                case CityNpcRole.Legend:
                {
                    var ch = Street.Find(npc.Ref);
                    if (ch == null) break;
                    bool open = career.street != null && Street.Unlocked(career.street, ch);
                    list.Add(open
                        ? new CityActivity { Label = "CHALLENGE", Detail = "THE PARK: " + ch.Nickname, Request = Tag(Street.Challenge(c, career, ch), back), Primary = true }
                        : new CityActivity { Label = "CHALLENGE", Locked = "Reach " + Street.RepNames[Math.Min(Street.RepNames.Length - 1, ch.Tier)] + " rep in THE PARK." });
                    break;
                }
                case CityNpcRole.Crew when npc.Callout:
                {
                    // 17 Oct 2026 (phase 6): today's CALL-OUT.
                    var crew = CityCrews.Find(npc.Ref);
                    if (crew == null) break;
                    list.Add(new CityActivity { Label = "ACCEPT CALL-OUT", Primary = true, Request = Tag(CityCallout.Request(c, career, crew), back),
                                                Detail = crew.Name + "  ·  " + Loc.T(crew.TierName) + "  ·  " + Loc.T("Win for an extra") + " " + StreetCash.Money(CityCallout.Bonus) });
                    break;
                }
                case CityNpcRole.Crew:
                {
                    var a = ForCrewMember(map, npc, career, c, back);
                    if (a != null) list.Add(a);
                    break;
                }
                case CityNpcRole.TourLeader:
                {
                    var stop = StreetsTour.Find(npc.Ref);
                    if (stop == null || career.streets == null) break;
                    list.Add(StreetsTour.Open(career.streets, stop)
                        ? new CityActivity { Label = "CALL OUT", Detail = "STREET TOUR: " + stop.Crew, Request = Tag(StreetsTour.Challenge(c, career, stop), back), Primary = true }
                        : new CityActivity { Label = "CALL OUT", Locked = "Clear the city before it on the STREET TOUR." });
                    break;
                }
            }
            return list;
        }

        /// <summary>
        /// 17 Oct 2026 (Omari): a THE STREETS graffiti wall. Run it right here (pickup on the nearest street court),
        /// open THE STREETS (tour, crew, homecourt, gear, live and friends), or see your cash and milestones.
        /// </summary>
        public static List<CityActivity> StreetsWall(CityMap map, CityPortal portal, CareerSaveData career, ContentCatalog c, int day)
        {
            var list = new List<CityActivity>();
            CityCourt nearest = null;
            float best = float.MaxValue;
            foreach (var k in map.Courts)
            {
                if (k.Use != CityCourtUse.Streets) continue;
                float d = Vec2.Distance(new Vec2(k.X + k.W * 0.5f, k.Y + k.H * 0.5f), portal.Center);
                if (d < best) { best = d; nearest = k; }
            }
            if (nearest != null && career != null && c != null)
            {
                var run = ForCourt(map, nearest, career, c, day).Find(a => a.Label == "PICKUP 3 ON 3" && a.Request != null);
                if (run != null)
                    list.Add(new CityActivity { Label = Loc.T("RUN IT AT") + " " + nearest.Name, Detail = run.Detail, Request = run.Request, Primary = true });
            }
            list.Add(new CityActivity { Label = "THE STREETS HQ", Detail = "Street Tour, your crew, Homecourt, gear, Live and games with friends.", Action = CityAction.OpenStreets });
            var cash = career?.streetCash;
            list.Add(new CityActivity
            {
                Label = "CASH & MILESTONES", Action = CityAction.StreetsBoard,
                Detail = cash == null ? null : StreetCash.Money(cash.cash) + "  ·  " + Loc.T("STREAK") + " " + cash.streak + "  ·  " + cash.achievements.Count + "/" + StreetCash.Milestones.Length,
            });
            return list;
        }

        /// <summary>17 Oct 2026: the court's CPU crew (BALLER or LEGENDARY), if it has one.</summary>
        private static void AddCrew(List<CityActivity> list, CityCourt court, CareerSaveData career, ContentCatalog c, string back)
        {
            var crew = CityCrews.AtCourt(court.CourtId);
            if (crew == null) return;
            string label = Loc.T("CREW:") + " " + crew.Name;
            string locked = CityCrews.LockedReason(career.city, crew);
            bool beaten = career.city != null && career.city.crewsBeaten.Contains(crew.Id);
            list.Add(locked != null
                ? new CityActivity { Label = label, Locked = locked }
                : new CityActivity
                {
                    Label = label,
                    Detail = Loc.T(crew.Style) + "  ·  " + Loc.T(crew.TierName) + "  ·  " + Loc.T(crew.StyleBlurb) + (beaten ? "  ·  " + Loc.T("BEATEN") : ""),
                    Request = Tag(CityCrews.Challenge(c, career, crew), back),
                });
        }

        /// <summary>17 Oct 2026: calling out a crew member you meet in the city.</summary>
        public static CityActivity ForCrewMember(CityMap map, CityNpc npc, CareerSaveData career, ContentCatalog c, string back)
        {
            var crew = CityCrews.Find(npc.Ref);
            if (crew == null) return null;
            string locked = CityCrews.LockedReason(career.city, crew);
            return locked != null
                ? new CityActivity { Label = "CHALLENGE", Locked = locked }
                : new CityActivity { Label = "CHALLENGE", Primary = true, Detail = crew.Name + "  ·  " + Loc.T(crew.Style) + "  ·  " + Loc.T(crew.TierName),
                                     Request = Tag(CityCrews.Challenge(c, career, crew), back) };
        }

        private static MatchRequest Tag(MatchRequest r, string back)
        {
            if (r != null) r.ReturnTo = back;
            return r;
        }

        private static string NearestSpawn(CityMap map, Vec2 p)
        {
            string best = map.DefaultSpawn.Id;
            float bestD = float.MaxValue;
            foreach (var s in map.Spawns)
            {
                float d = Vec2.Distance(s.Position, p);
                if (d < bestD) { bestD = d; best = s.Id; }
            }
            return best;
        }

        private static string DifficultyName(ContentCatalog c, string id) => (c.Difficulty(id)?.displayName ?? "").ToUpperInvariant();

        /// <summary>Your personal best for a drill (as the Practice Lab shows it), or null.</summary>
        public static string PracticeBest(CareerSaveData d, DrillKind drill)
        {
            var b = d?.practice;
            if (b == null) return null;
            switch (drill)
            {
                case DrillKind.FreeShoot: return b.freeShootMakes > 0 ? b.freeShootMakes + " " + Loc.T("MAKES") : null;
                case DrillKind.PassingTargets: return b.passingScore > 0 ? b.passingScore.ToString() : null;
                case DrillKind.DribbleLane: return b.dribbleLaneTime > 0f ? b.dribbleLaneTime.ToString("0.0") + " s" : null;
                case DrillKind.ThreePoint: return b.threePointBest > 0 ? b.threePointBest.ToString() : null;
                case DrillKind.AroundTheWorld: return b.aroundWorldTime > 0f ? b.aroundWorldTime.ToString("0.0") + " s" : null;
                default: return null;
            }
        }
    }
}
