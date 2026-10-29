using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>What the guide points at.</summary>
    public enum GuideKind
    {
        /// <summary>Nothing to point at (a goal you do anywhere, like the daily achievements).</summary>
        None = 0,
        /// <summary>A spot in this district.</summary>
        Spot = 1,
        /// <summary>The way out of this district toward the target (a road or a door).</summary>
        Exit = 2,
        /// <summary>Another region: your ride (or MENU ► WORLD MAP to drive).</summary>
        Drive = 3,
        /// <summary>Anyone to talk to (the nearest person).</summary>
        People = 4,
        /// <summary>Any court here (the nearest one you can play on).</summary>
        AnyCourt = 5,
    }

    public struct GuideHint
    {
        public GuideKind Kind;
        /// <summary>The objective in words, for the ticker ("Win a game at THE CAGE").</summary>
        public string Text;
        /// <summary>Where to walk (Spot, Exit, Drive with a ride here).</summary>
        public Vec2 Point;
        public bool HasPoint;
        /// <summary>A second line ("Head to MIDTOWN", "Drive to NEW YORK: your ride or MENU ► WORLD MAP").</summary>
        public string Route;
        /// <summary>The region to drive to (Drive).</summary>
        public string Region;
    }

    /// <summary>
    /// 28 Oct 2026 (Omari, BALLER WORLD phase W2): THE GUIDE. Tracks one thing (the comic's next objective, or the side
    /// job you took) and works out where to go: a spot in this district, the road or door toward the right district,
    /// or your ride when it's in another region. The HUD shows the objective in a ticker and an arrow at the edge of the
    /// screen with the distance.
    /// </summary>
    public static class CityGuide
    {
        /// <summary>Where a court is: its district and the middle of its city court.</summary>
        public static (string district, Vec2 at)? CourtSpot(string courtId)
        {
            var home = CityDistricts.CourtHome(courtId);
            if (home == null) return null;
            var m = CityDistricts.Map(home.Value.district);
            var k = m.Courts.Find(c => c.CourtId == courtId || c.Id == courtId);
            if (k == null) return null;
            return (home.Value.district, new Vec2(k.X + k.W * 0.5f, k.Y + 1.5f));
        }

        /// <summary>A portal by target in a region (the BIG RUN poster, YOUR RIDE…): district and spot.</summary>
        public static (string district, Vec2 at)? PortalIn(WorldRegion r, string target)
        {
            if (r == null) return null;
            foreach (var d in r.Districts)
                foreach (var p in CityDistricts.Map(d).Portals)
                    if (p.Target == target) return (d, p.Center);
            return null;
        }

        /// <summary>The Baller Arena's door (Downtown).</summary>
        public static (string district, Vec2 at)? ArenaDoor()
        {
            foreach (var p in CityDistricts.Map(CityDistricts.Downtown).Portals)
                if (p.Target == CityDistricts.Arena) return (CityDistricts.Downtown, p.Center);
            return null;
        }

        /// <summary>The first unbeaten crew (that you can face) whose court is in the region, preferring this district.</summary>
        public static (string district, Vec2 at)? CrewCourt(CareerSaveData d, WorldRegion r, string here, CityCrewTier? tier = null)
        {
            (string, Vec2)? best = null;
            foreach (var crew in WorldRegions.Crews(r))
            {
                if (tier.HasValue && crew.Tier != tier.Value) continue;
                if (d?.city != null && d.city.crewsBeaten.Contains(crew.Id)) continue;
                if (!CityCrews.Unlocked(d?.city, crew)) continue;
                var spot = CourtSpot(crew.CourtId);
                if (spot == null) continue;
                if (spot.Value.district == here) return spot;
                if (best == null) best = spot;
            }
            return best;
        }

        /// <summary>The comic objective's destination (district, spot), or null for goals done anywhere.</summary>
        public static (string district, Vec2 at)? StoryDestination(CareerSaveData d, ComicGoal g, string here, out GuideKind kind)
        {
            kind = GuideKind.Spot;
            if (g == null) { kind = GuideKind.None; return null; }
            var hereRegion = WorldRegions.RegionOf(here);
            switch (g.Kind)
            {
                case ComicGoalKind.Talk: kind = GuideKind.People; return null;
                case ComicGoalKind.Wins: kind = GuideKind.AnyCourt; return null;
                case ComicGoalKind.WinAt: return CourtSpot(g.Ref);
                case ComicGoalKind.CrewsIn: return CrewCourt(d, WorldRegions.Find(g.Ref), here);
                case ComicGoalKind.Arrive:
                {
                    var r = WorldRegions.Find(g.Ref);
                    return r == null ? null : PortalIn(r, "ride") ?? (r.Main, CityDistricts.Map(r.Main).DefaultSpawn.Position);
                }
                case ComicGoalKind.SideJobs:
                {
                    foreach (var j in CitySideJobs.All)
                        if (j.Region == g.Ref && !CitySideJobs.Done(d?.world, j) && CitySideJobs.Locked(d, j) == null && j.District == here) return (j.District, j.At);
                    foreach (var j in CitySideJobs.All)
                        if (j.Region == g.Ref && !CitySideJobs.Done(d?.world, j) && CitySideJobs.Locked(d, j) == null) return (j.District, j.At);
                    kind = GuideKind.None;
                    return null;
                }
                case ComicGoalKind.BigRun:
                {
                    var r = WorldRegions.Find(g.Ref);
                    if (CityBigRun.Locked(d, g.Ref) != null && WorldRegions.Open(d, r)) return CrewCourt(d, r, here); // a crew win gets you in
                    return PortalIn(r, "bigrun");
                }
                case ComicGoalKind.BallerCrews: return CrewCourt(d, WorldRegions.Find(WorldRegions.BallerCity), here, CityCrewTier.Baller);
                case ComicGoalKind.Titles:
                case ComicGoalKind.Velvet: return ArenaDoor();
                case ComicGoalKind.Drives: return PortalIn(hereRegion, "ride");
                default: kind = GuideKind.None; return null;
            }
        }

        /// <summary>
        /// The first step from <paramref name="from"/> toward <paramref name="to"/> inside one region: the road or door to
        /// take (its spot here and the next district), or null when there's no way on foot.
        /// </summary>
        public static (CityPortal portal, string next)? NextHop(string from, string to)
        {
            if (from == to) return null;
            var prev = new Dictionary<string, (string district, CityPortal portal)>();
            var queue = new Queue<string>();
            queue.Enqueue(from);
            prev[from] = (null, null);
            while (queue.Count > 0)
            {
                string cur = queue.Dequeue();
                if (cur == to) break;
                foreach (var p in CityDistricts.Map(cur).Portals)
                {
                    bool travels = p.Kind == CityPortalKind.District || p.Kind == CityPortalKind.Exit
                                   || (p.Kind == CityPortalKind.Building && CityDistricts.Find(p.Target) != null);
                    if (!travels || prev.ContainsKey(p.Target)) continue;
                    prev[p.Target] = (cur, p);
                    queue.Enqueue(p.Target);
                }
            }
            if (!prev.ContainsKey(to)) return null;
            string step = to;
            while (prev[step].district != from) step = prev[step].district;
            return (prev[step].portal, step);
        }

        /// <summary>What to show for the comic objective (or the tracked side job) from where you are.</summary>
        public static GuideHint Hint(CareerSaveData d, string here)
        {
            var w = d?.world;
            var hint = new GuideHint();
            if (w == null || w.tracking == "off") return hint;
            (string district, Vec2 at)? dest;
            var job = CitySideJobs.Active(w);
            if (job != null && w.tracking != null && w.tracking.StartsWith("side:", StringComparison.Ordinal))
            {
                hint.Text = Loc.T("SIDE JOB") + ": " + CitySideJobs.Goal(job, w);
                dest = CitySideJobs.Destination(job, w);
                hint.Kind = GuideKind.Spot;
            }
            else
            {
                if (CityStory.Finished(w)) return hint;
                var g = CityStory.CurrentGoal(w);
                hint.Text = CityStory.Ticker(d);
                dest = StoryDestination(d, g, here, out hint.Kind);
            }
            if (dest == null) return hint;
            return Route(hint, here, dest.Value.district, dest.Value.at);
        }

        /// <summary>Turns a destination into what to do from here: walk there, take a road or door, or drive.</summary>
        public static GuideHint Route(GuideHint hint, string here, string district, Vec2 at)
        {
            string outside = CityDistricts.OutsideOf(here);
            var hereRegion = WorldRegions.RegionOf(here);
            var thereRegion = WorldRegions.RegionOf(district);
            if (district == here)
            {
                hint.Kind = GuideKind.Spot;
                hint.Point = at;
                hint.HasPoint = true;
                return hint;
            }
            if (hereRegion.Id != thereRegion.Id)
            {
                hint.Kind = GuideKind.Drive;
                hint.Region = thereRegion.Id;
                hint.Route = Loc.T("Drive to") + " " + Loc.T(thereRegion.Name) + ": " + Loc.T("YOUR RIDE or MENU ► WORLD MAP");
                var ride = CityDistricts.Map(here).Portals.Find(p => p.Target == "ride");
                if (ride != null) { hint.Point = ride.Center; hint.HasPoint = true; }
                else if (outside != here)
                {
                    var exit = NextHop(here, outside);
                    if (exit != null) { hint.Point = exit.Value.portal.Center; hint.HasPoint = true; hint.Route = Loc.T("Head outside, then drive to") + " " + Loc.T(thereRegion.Name); }
                }
                return hint;
            }
            var hop = NextHop(here, district);
            if (hop == null) return hint;
            hint.Kind = GuideKind.Exit;
            hint.Point = hop.Value.portal.Center;
            hint.HasPoint = true;
            hint.Route = Loc.T("Head to") + " " + Loc.T(CityDistricts.Find(district)?.Name ?? district);
            return hint;
        }
    }
}
