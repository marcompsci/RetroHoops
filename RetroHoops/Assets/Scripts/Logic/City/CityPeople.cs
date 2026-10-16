using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public enum CityNpcRole { Teammate = 0, Rival = 1, Legend = 2, TourLeader = 3 }

    /// <summary>Someone who lives in Baller City: a real player from the game (your crew, the rival crew, legends).</summary>
    public sealed class CityNpc
    {
        public string Id;
        public PlayerDef Player;
        public TeamDef Team;
        public CityNpcRole Role;
        /// <summary>Where they go (district/hangout pairs: "blacktop/court.sunset_cage.side").</summary>
        public string[] Haunts;
        /// <summary>Their Park legend or tour stop id (for challenges).</summary>
        public string Ref;
        public string Name => Player == null ? "" : (string.IsNullOrEmpty(Player.lastName) ? Player.firstName : Player.firstName + " " + Player.lastName);
    }

    /// <summary>Where someone is in a time block: a district and a hangout there, or nowhere (home, away).</summary>
    public struct CityPlace
    {
        public string District, Hangout;
        public bool Away => string.IsNullOrEmpty(District);
        public override string ToString() => Away ? "away" : District + "/" + Hangout;
    }

    /// <summary>
    /// 16 Oct 2026 (Omari): who's out in Baller City, and where. A deterministic daily schedule (the same on every
    /// launch that day, offline): each person has a few places they go, and for each time block of the day the
    /// schedule picks exactly one (so nobody is in two places at once). Late at night most people are home.
    /// Only the people in the district you're in are walked around; everyone else is just this lookup.
    /// </summary>
    public static class CityNpcScheduler
    {
        private static readonly string[] TeammateHaunts =
        {
            "first_callers/hq.front", "first_callers/court.first_callers", "first_callers/court.first_callers.side", "first_callers/gym.lot",
            "baller_gym/gym.floor", "baller_gym/gym.lobby", "baller_gym/gym.bleachers", "blacktop/court.sunset_cage.side", "first_callers/park.se",
        };

        private static readonly string[] RivalHaunts =
        {
            "blacktop/court.sunset_cage", "blacktop/court.overpass_park", "blacktop/court.static_lot.side", "blacktop/pier.rail", "blacktop/court.pier_nine.side",
        };

        /// <summary>Everyone who can be in the city now: your teammates, this Rise season's rival crew, Park legends, tour leaders whose city is open.</summary>
        public static List<CityNpc> Cast(CareerSaveData d, ContentCatalog c)
        {
            var list = new List<CityNpc>();
            if (c == null) return list;
            if (d != null)
                foreach (var p in CrewEngine.TeammateDefs(d, c))
                    if (p != null)
                        list.Add(new CityNpc { Id = "mate." + p.id, Player = p, Team = c.Team(DefaultContent.PlayerCrewId), Role = CityNpcRole.Teammate, Haunts = TeammateHaunts });

            int season = d?.rise?.season?.seasonNumber ?? 0;
            var rival = c.Team(RivalEngine.RivalFor(Math.Max(1, season)));
            if (rival != null)
                for (int i = 0; i < rival.rosterPlayerIds.Count && i < 3; i++)
                {
                    var p = c.Player(rival.rosterPlayerIds[i]);
                    if (p != null) list.Add(new CityNpc { Id = "rival." + p.id, Player = p, Team = rival, Role = CityNpcRole.Rival, Haunts = RivalHaunts });
                }

            foreach (var ch in Street.Challengers)
            {
                string spot = CourtSpot(ch.CourtId);
                if (spot == null) continue;
                var team = Street.Register(c, ch);
                var p = team.rosterPlayerIds.Count > 0 ? c.Player(team.rosterPlayerIds[0]) : null;
                if (p != null)
                    list.Add(new CityNpc { Id = "legend." + ch.Id, Player = p, Team = team, Role = CityNpcRole.Legend, Ref = ch.Id,
                                           Haunts = new[] { spot, spot + ".side", "blacktop/pier.rail" } });
            }

            var streets = d?.streets;
            foreach (var stop in StreetsTour.Stops)
            {
                if (stop.Finale) continue;
                string spot = CourtSpot(stop.CourtId);
                if (spot == null || streets == null || !StreetsTour.Open(streets, stop)) continue;
                var team = StreetsTour.Register(c, stop);
                var p = c.Player(stop.LeaderId);
                if (p != null)
                    list.Add(new CityNpc { Id = "tour." + stop.Id, Player = p, Team = team, Role = CityNpcRole.TourLeader, Ref = stop.Id,
                                           Haunts = new[] { spot, spot + ".side" } });
            }
            return list;
        }

        /// <summary>"district/court" for a court that's in the city, or null.</summary>
        public static string CourtSpot(string courtId)
        {
            var home = CityDistricts.CourtHome(courtId);
            if (home == null) return null;
            var map = CityDistricts.Map(home.Value.district);
            var court = map.Courts.Find(k => k.CourtId == courtId || k.Id == courtId);
            return court == null ? null : home.Value.district + "/" + court.Id;
        }

        /// <summary>Days since 2000-01-01 (the schedule's day number).</summary>
        public static int DayNumber(DateTime local) => (int)(local.Date - new DateTime(2000, 1, 1)).TotalDays;

        /// <summary>Where <paramref name="npc"/> is on day <paramref name="day"/> in time block <paramref name="time"/>.</summary>
        public static CityPlace Place(CityNpc npc, int day, CityTimeOfDay time)
        {
            if (npc?.Haunts == null || npc.Haunts.Length == 0) return default;
            uint h = StableHash.Of(npc.Id + ":" + day + ":" + (int)time);
            // Nights: three in four are home.
            if (time == CityTimeOfDay.Night && h % 4 != 0) return default;
            // Now and then someone's just not around.
            if (h % 7 == 3) return default;
            var pick = npc.Haunts[(int)((h / 7) % (uint)npc.Haunts.Length)];
            int slash = pick.IndexOf('/');
            return new CityPlace { District = pick.Substring(0, slash), Hangout = pick.Substring(slash + 1) };
        }

        /// <summary>The people in <paramref name="district"/> right now, with where they hang out.</summary>
        public static List<(CityNpc npc, Vec2 spot)> InDistrict(List<CityNpc> cast, string district, int day, CityTimeOfDay time)
        {
            var list = new List<(CityNpc, Vec2)>();
            var map = CityDistricts.Map(district);
            foreach (var n in cast)
            {
                var place = Place(n, day, time);
                if (place.Away || place.District != district) continue;
                if (!map.Hangouts.TryGetValue(place.Hangout, out var spot)) continue;
                list.Add((n, spot));
            }
            return list;
        }
    }

    public enum CityNpcActivity { Idle = 0, Walk = 1, Shoot = 2, Cheer = 3, Talk = 4 }

    /// <summary>
    /// How one NPC moves around its hangout: it picks a nearby reachable spot (a short grid path, never a search
    /// of the whole map), walks there, then idles, shoots around on a court or cheers by one, and picks again. It
    /// stops and faces you while you talk to it, and steps aside from people it's bumping into.
    /// </summary>
    public sealed class CityNpcBrain
    {
        public CityBody Body;
        public Vec2 Anchor;
        public float Radius = 3.5f;
        public CityNpcActivity Activity { get; private set; }
        private readonly SeededRandom _rng;
        private List<Vec2> _path = new List<Vec2>();
        private int _pathIndex;
        private float _waitUntil;
        private readonly bool _onCourt;
        private float _stuckFor;

        public CityNpcBrain(Vec2 anchor, uint seed, bool onCourt, CityMap map)
        {
            _rng = new SeededRandom(seed == 0 ? 1u : seed);
            Anchor = anchor;
            _onCourt = onCourt;
            Body.Position = CityMovement.Settle(map, anchor + new Vec2(_rng.NextFloat() - 0.5f, _rng.NextFloat() - 0.5f));
        }

        public bool Walking => Activity == CityNpcActivity.Walk;

        public void Step(CityMap map, float time, float dt, Vec2 player, bool talking, IList<Vec2> others = null)
        {
            if (talking)
            {
                Activity = CityNpcActivity.Talk;
                Body.Velocity = Vec2.Zero;
                Body.Facing = CityMovement.FacingOf(player - Body.Position);
                _path.Clear();
                _waitUntil = time + 1.5f;
                return;
            }
            if (Activity == CityNpcActivity.Talk) Activity = CityNpcActivity.Idle;

            if (_pathIndex < _path.Count)
            {
                Activity = CityNpcActivity.Walk;
                var target = _path[_pathIndex];
                var to = target - Body.Position;
                if (to.Magnitude < 0.15f) { _pathIndex++; return; }
                var stick = to.Normalized * 0.45f + Separation(others) * 0.6f;
                var before = Body.Position;
                CityMovement.Step(ref Body, map, stick, false, dt);
                _stuckFor = Vec2.Distance(before, Body.Position) < 0.002f ? _stuckFor + dt : 0f;
                if (_stuckFor > 1f) { _path.Clear(); _pathIndex = 0; _stuckFor = 0f; }
                return;
            }

            // Slow to a stop.
            CityMovement.Step(ref Body, map, Separation(others) * 0.3f, false, dt);
            if (time < _waitUntil) return;
            float roll = _rng.NextFloat();
            if (_onCourt && roll < 0.35f) { Activity = CityNpcActivity.Shoot; _waitUntil = time + 1.2f + _rng.NextFloat(); return; }
            if (!_onCourt && roll < 0.2f) { Activity = CityNpcActivity.Cheer; _waitUntil = time + 1f + _rng.NextFloat(); return; }
            if (roll < 0.55f)
            {
                Activity = CityNpcActivity.Idle;
                _waitUntil = time + 1.5f + _rng.NextFloat() * 2.5f;
                return;
            }
            var goal = Anchor + new Vec2((_rng.NextFloat() * 2f - 1f) * Radius, (_rng.NextFloat() * 2f - 1f) * Radius);
            _path = CityPath.Find(map, Body.Position, goal, 300);
            _pathIndex = 0;
            if (_path.Count == 0) { Activity = CityNpcActivity.Idle; _waitUntil = time + 1f; }
        }

        /// <summary>A push away from anyone too close (soft, so people don't overlap).</summary>
        private Vec2 Separation(IList<Vec2> others)
        {
            if (others == null) return Vec2.Zero;
            var push = Vec2.Zero;
            foreach (var o in others)
            {
                var d = Body.Position - o;
                float m = d.Magnitude;
                if (m < 0.001f || m > 0.8f) continue;
                push += d / m * (0.8f - m);
            }
            return push;
        }
    }

    /// <summary>Short grid paths (four directions, a capped search) between two points.</summary>
    public static class CityPath
    {
        public static List<Vec2> Find(CityMap map, Vec2 from, Vec2 to, int maxNodes)
        {
            var result = new List<Vec2>();
            int sx = (int)Math.Floor(from.x), sy = (int)Math.Floor(from.y);
            int gx = (int)Math.Floor(to.x), gy = (int)Math.Floor(to.y);
            if (map.Blocked(gx, gy) || map.Blocked(sx, sy)) return result;
            if (sx == gx && sy == gy) { result.Add(new Vec2(gx + 0.5f, gy + 0.5f)); return result; }
            var came = new Dictionary<int, int>();
            var queue = new Queue<int>();
            int start = sy * map.Width + sx, goal = gy * map.Width + gx;
            came[start] = -1;
            queue.Enqueue(start);
            int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
            bool found = false;
            while (queue.Count > 0 && came.Count < maxNodes)
            {
                int cur = queue.Dequeue();
                if (cur == goal) { found = true; break; }
                int cx = cur % map.Width, cy = cur / map.Width;
                for (int k = 0; k < 4; k++)
                {
                    int nx = cx + dx[k], ny = cy + dy[k];
                    if (map.Blocked(nx, ny)) continue;
                    int id = ny * map.Width + nx;
                    if (came.ContainsKey(id)) continue;
                    came[id] = cur;
                    queue.Enqueue(id);
                }
            }
            if (!found && !came.ContainsKey(goal)) return result;
            for (int at = goal; at != -1 && at != start; at = came[at])
                result.Add(new Vec2(at % map.Width + 0.5f, at / map.Width + 0.5f));
            result.Reverse();
            return result;
        }
    }

    /// <summary>What city people say: their role, your history with them, and the season.</summary>
    public static class CityDialogue
    {
        /// <summary>The First Callers' record this Rise season ("4-2"), or null before a season.</summary>
        public static string Record(CareerSaveData d)
        {
            var s = d?.rise?.season;
            if (s?.games == null) return null;
            int w = 0, l = 0;
            foreach (var g in s.games)
            {
                if (!g.played || !g.Involves(DefaultContent.PlayerCrewId)) continue;
                if (g.WinnerId == DefaultContent.PlayerCrewId) w++; else l++;
            }
            return w + l == 0 ? null : w + "-" + l;
        }

        public static string Line(CityNpc npc, CareerSaveData d, int day)
        {
            if (npc == null) return "";
            uint h = StableHash.Of(npc.Id + ":talk:" + day);
            switch (npc.Role)
            {
                case CityNpcRole.Teammate:
                {
                    string record = Record(d);
                    string[] lines =
                    {
                        "Run it back at the HQ when you're ready. We got games to win.",
                        record != null ? "We're " + record + " this season. Let's keep stacking." : "New season soon. You ready to lead us?",
                        "Gym's open. I'm getting shots up before the next one.",
                        "Saw you on the blacktop. Save some of that for our games.",
                    };
                    return lines[h % (uint)lines.Length];
                }
                case CityNpcRole.Rival:
                {
                    bool can = d != null && RivalEngine.ChallengeAvailable(d);
                    return can ? "Rival Challenge is on. You, me, your crew. Let's go."
                               : "We'll see you when it counts. From week " + RivalEngine.ChallengeFromWeek + " of a Rise season.";
                }
                case CityNpcRole.Legend:
                {
                    var ch = Street.Find(npc.Ref);
                    return ch != null ? "\"" + ch.Trash + "\"" : "Who's next?";
                }
                default:
                {
                    var stop = StreetsTour.Find(npc.Ref);
                    return stop != null ? "\"" + stop.Trash + "\"" : "This court is ours.";
                }
            }
        }
    }
}
