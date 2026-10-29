using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>A rectangle on the world map (map pixels, y up).</summary>
    public struct MapRect
    {
        public float X, Y, W, H;
        public MapRect(float x, float y, float w, float h) { X = x; Y = y; W = w; H = h; }
        public float CenterX => X + W * 0.5f;
        public float CenterY => Y + H * 0.5f;
        public bool Contains(float x, float y) => x >= X && x <= X + W && y >= Y && y <= Y + H;
        public bool Overlaps(MapRect o) => X < o.X + o.W && o.X < X + W && Y < o.Y + o.H && o.Y < Y + H;
    }

    /// <summary>What the WORLD MAP offers for a place you tapped.</summary>
    public struct MapChoice
    {
        public string Region, District, Title, Subtitle, Detail, Stats;
        /// <summary>Why you can't go (locked); null = you can.</summary>
        public string Blocked;
        public bool Here, InRegion, CanDrive, CanSpawn, Next;
    }

    /// <summary>I26: what a pin on the map marks.</summary>
    public enum MapPinKind { Crew = 0, BigRun = 1, SideJob = 2, Ride = 3, Comic = 4, Store = 5 }

    /// <summary>A pin on the world map: what, where (district and tile) and a short label.</summary>
    public struct MapPin
    {
        public MapPinKind Kind;
        public string District, Label;
        public Vec2 At;
    }

    /// <summary>What a tap on the world map landed on.</summary>
    public struct MapPick
    {
        public string Region, District;
        public bool IsNone => Region == null;
    }

    /// <summary>
    /// 28 Oct 2026 (Omari): the zoomable WORLD MAP. "When players tap on the map it shows them a complete view of the area
    /// of the state they are in; pinch to zoom out to see more of the state, and the more they zoom out they see the full
    /// world. They can move the world around and select the state they'd like to go to: drive there or spawn there."
    /// This is the layout: one big map (<see cref="Width"/> × <see cref="Height"/> map pixels) with each region on its own
    /// plate, its districts drawn true to scale (<see cref="PxPerTile"/>) in a grid of cells where they sit on the region's
    /// district map, the interstates between the plates, and the rules for driving or spawning somewhere.
    /// </summary>
    public static class WorldAtlas
    {
        public const float Width = 3200f, Height = 1800f;
        /// <summary>Map pixels per city tile (every district is drawn at the same scale).</summary>
        public const float PxPerTile = 2f;
        /// <summary>A district cell (the biggest district is 72 tiles = 144 px; the rest is the road between cells).</summary>
        public const float Cell = 160f;
        /// <summary>The plate's border round a region's cells.</summary>
        public const float Margin = 48f;

        /// <summary>Where each region's cell grid starts (bottom-left), laid out like a cartoon of the country.</summary>
        private static readonly Dictionary<string, (float x, float y)> Origins = new Dictionary<string, (float, float)>
        {
            [WorldRegions.TheBay] = (230f, 1180f),
            [WorldRegions.LosAngeles] = (380f, 300f),
            [WorldRegions.LasVegas] = (1000f, 760f),
            [WorldRegions.BallerCity] = (1600f, 640f),
            [WorldRegions.NewYork] = (2650f, 1060f),
        };

        /// <summary>The interstates (pairs of regions joined by a highway on the map).</summary>
        public static readonly (string a, string b)[] Highways =
        {
            (WorldRegions.TheBay, WorldRegions.LosAngeles),
            (WorldRegions.TheBay, WorldRegions.LasVegas),
            (WorldRegions.LosAngeles, WorldRegions.LasVegas),
            (WorldRegions.LasVegas, WorldRegions.BallerCity),
            (WorldRegions.BallerCity, WorldRegions.NewYork),
        };

        private static (int cols, int rows) GridSize(string region)
        {
            int cols = 0, rows = 0;
            foreach (var s in CityAtlas.SpotsFor(region))
            {
                cols = Math.Max(cols, s.col + 1);
                rows = Math.Max(rows, s.row + 1);
            }
            return (Math.Max(1, cols), Math.Max(1, rows));
        }

        /// <summary>The region's cells (no margin).</summary>
        public static MapRect Cells(string region)
        {
            var o = Origins.TryGetValue(region, out var v) ? v : (0f, 0f);
            var (cols, rows) = GridSize(region);
            return new MapRect(o.Item1, o.Item2, cols * Cell, rows * Cell);
        }

        /// <summary>The region's plate (its cells and the border round them).</summary>
        public static MapRect Plate(string region)
        {
            var c = Cells(region);
            return new MapRect(c.X - Margin, c.Y - Margin, c.W + Margin * 2f, c.H + Margin * 2f);
        }

        /// <summary>A district's cell on the map.</summary>
        public static MapRect CellOf(string district)
        {
            string outside = CityAtlas.Outside(district);
            var r = WorldRegions.RegionOf(outside);
            var cells = Cells(r.Id);
            foreach (var s in CityAtlas.SpotsFor(r.Id))
                if (s.id == outside) return new MapRect(cells.X + s.col * Cell, cells.Y + s.row * Cell, Cell, Cell);
            return new MapRect(cells.X, cells.Y, Cell, Cell);
        }

        /// <summary>Where a district's map is drawn inside its cell (true scale, centred).</summary>
        public static MapRect DistrictRect(string district)
        {
            var cell = CellOf(district);
            var m = CityDistricts.Map(CityAtlas.Outside(district));
            float w = m.Width * PxPerTile, h = m.Height * PxPerTile;
            return new MapRect(cell.CenterX - w * 0.5f, cell.CenterY - h * 0.5f, w, h);
        }

        /// <summary>A spot in a district (tiles) on the world map.</summary>
        public static (float x, float y) ToMap(string district, Vec2 at)
        {
            var r = DistrictRect(district);
            return (r.X + at.x * PxPerTile, r.Y + at.y * PxPerTile);
        }

        /// <summary>The district (and region) under a point on the map, or the region whose plate it's on, or nothing.</summary>
        public static MapPick Pick(float x, float y)
        {
            foreach (var r in WorldRegions.All)
            {
                if (!Plate(r.Id).Contains(x, y)) continue;
                foreach (var s in CityAtlas.SpotsFor(r.Id))
                {
                    var d = DistrictRect(s.id);
                    // A little slack round the district so a tap on its edge counts.
                    if (new MapRect(d.X - 6f, d.Y - 6f, d.W + 12f, d.H + 12f).Contains(x, y)) return new MapPick { Region = r.Id, District = s.id };
                }
                return new MapPick { Region = r.Id };
            }
            return new MapPick();
        }

        /// <summary>The rectangle to frame when the map opens on a region (its plate, a little padded).</summary>
        public static MapRect Frame(string region)
        {
            var p = Plate(region);
            return new MapRect(p.X - 40f, p.Y - 60f, p.W + 80f, p.H + 160f);
        }

        /// <summary>The roads between neighbouring districts of a region (cell centre to cell centre).</summary>
        public static List<(string a, string b)> Links(string region)
        {
            var list = new List<(string, string)>();
            foreach (var s in CityAtlas.SpotsFor(region))
            {
                var m = CityDistricts.Map(s.id);
                foreach (var p in m.Portals)
                {
                    if (p.Kind != CityPortalKind.District) continue;
                    if (string.CompareOrdinal(s.id, p.Target) >= 0) continue; // each road once
                    if (WorldRegions.RegionOf(p.Target).Id != region) continue;
                    list.Add((s.id, p.Target));
                }
            }
            return list;
        }

        // ------------------------------------------------------------------ I24 COURT PASSPORT

        private static Dictionary<string, string> _courtRegions;

        /// <summary>The BALLER WORLD region a catalog court is in (null for Baller City's and every other court).</summary>
        public static string CourtRegion(string courtId)
        {
            if (courtId == null) return null;
            if (_courtRegions == null)
            {
                var map = new Dictionary<string, string>();
                foreach (var r in WorldRegions.All)
                {
                    if (r.Id == WorldRegions.BallerCity) continue;
                    foreach (var d in r.Districts)
                        foreach (var k in CityDistricts.Map(d).Courts) map[k.CourtId] = r.Id;
                }
                _courtRegions = map;
            }
            return _courtRegions.TryGetValue(courtId, out var region) ? region : null;
        }

        /// <summary>Quick Play lists a BALLER WORLD court once you've been to its region.</summary>
        public static bool CourtStamped(CareerSaveData d, string courtId)
        {
            string region = CourtRegion(courtId);
            return region == null || (d?.world != null && d.world.regionsSeen.Contains(region));
        }

        /// <summary>The court's name for a list, with its region ("THE CAGE  ·  NYC").</summary>
        public static string CourtLabel(string courtId, string displayName)
        {
            var r = WorldRegions.Find(CourtRegion(courtId));
            return (displayName ?? courtId).ToUpperInvariant() + (r == null ? "" : "  ·  " + r.Short);
        }

        // ------------------------------------------------------------------ I26 pins

        /// <summary>
        /// The pins on the map: courts with a crew you haven't beaten, the Big Run posters, side jobs you can take,
        /// your ride in each region, the region stores and where the comic wants you next. Only regions that are open.
        /// </summary>
        public static List<MapPin> Pins(CareerSaveData d, string here)
        {
            var list = new List<MapPin>();
            var w = d?.world ?? new CityWorldSaveData();
            foreach (var r in WorldRegions.All)
            {
                if (!WorldRegions.Open(d, r)) continue;
                foreach (var id in r.Districts)
                {
                    var m = CityDistricts.Map(id);
                    foreach (var p in m.Portals)
                    {
                        if (p.Kind != CityPortalKind.Station && p.Kind != CityPortalKind.Building) continue;
                        var at = new Vec2(p.X + p.W * 0.5f, p.Y + p.H * 0.5f);
                        if (p.Target == "ride") list.Add(new MapPin { Kind = MapPinKind.Ride, District = id, At = at, Label = "YOUR RIDE" });
                        else if (p.Target == "bigrun") list.Add(new MapPin { Kind = MapPinKind.BigRun, District = id, At = at, Label = "BIG RUN" });
                        else if (p.Target != null && p.Target.StartsWith("store.", StringComparison.Ordinal))
                            list.Add(new MapPin { Kind = MapPinKind.Store, District = id, At = at, Label = p.Label });
                    }
                }
            }
            foreach (var crew in CityCrews.All)
            {
                if (d?.city != null && d.city.crewsBeaten.Contains(crew.Id)) continue;
                var spot = CityGuide.CourtSpot(crew.CourtId);
                if (spot == null || !WorldRegions.Open(d, WorldRegions.RegionOf(spot.Value.district))) continue;
                list.Add(new MapPin { Kind = MapPinKind.Crew, District = spot.Value.district, At = spot.Value.at, Label = crew.Name });
            }
            foreach (var j in CitySideJobs.All)
            {
                if (CitySideJobs.Done(w, j) || CitySideJobs.Locked(d, j) != null) continue;
                list.Add(new MapPin { Kind = MapPinKind.SideJob, District = j.District, At = j.At, Label = j.Title });
            }
            var goal = CityStory.CurrentGoal(w);
            if (goal != null)
            {
                var dest = CityGuide.StoryDestination(d, goal, here, out _);
                if (dest != null) list.Add(new MapPin { Kind = MapPinKind.Comic, District = dest.Value.district, At = dest.Value.at, Label = "THE COMIC" });
            }
            return list;
        }

        /// <summary>Is this kind of pin shown (the WORLD SETTINGS filter)?</summary>
        public static bool PinShown(CityWorldSaveData w, MapPinKind k) => w == null || (w.mapPins & (1 << (int)k)) != 0;

        // ------------------------------------------------------------------ going somewhere

        /// <summary>Why you can't go to a district or region from here (null = you can).</summary>
        public static string Blocked(CareerSaveData d, string region, string district)
        {
            var r = WorldRegions.Find(region);
            if (r == null) return "Nothing there.";
            string locked = WorldRegions.LockedReason(d, r);
            if (locked != null) return locked;
            if (district != null)
            {
                var def = CityDistricts.Find(district);
                if (def == null || def.Interior || WorldRegions.RegionOf(district).Id != r.Id) return "Nothing there.";
            }
            return null;
        }

        /// <summary>Can you DRIVE there from <paramref name="here"/>? Only to another region (inside one, you walk or spawn).</summary>
        public static bool CanDrive(CareerSaveData d, string here, string region) =>
            Blocked(d, region, null) == null && WorldRegions.RegionOf(here).Id != region;

        /// <summary>
        /// SPAWN THERE: puts you straight into a district (any outdoor district of a region that's open), at the
        /// region's ride when it's the main district, otherwise at its front door. False if it's locked.
        /// </summary>
        public static bool Spawn(CareerSaveData d, string region, string district)
        {
            if (Blocked(d, region, district) != null) return false;
            var r = WorldRegions.Find(region);
            string target = district ?? r.Main;
            var map = CityDistricts.Map(target);
            string spawn = map.TryGetSpawn("ride", out _) ? "ride" : null;
            if (d.city == null) d.city = new CitySaveData();
            d.city.Arrive(target, spawn);
            if (!d.city.seen.Contains(target)) d.city.seen.Add(target);
            return true;
        }

        /// <summary>
        /// What the sheet says when you tap somewhere on the map, and which of DRIVE THERE / SPAWN THERE it offers.
        /// <paramref name="here"/> is the district you're standing in (indoors counts as outside).
        /// </summary>
        public static MapChoice Describe(CareerSaveData d, string here, string region, string district)
        {
            var r = WorldRegions.Find(region);
            var choice = new MapChoice { Region = region, District = district };
            if (r == null) return choice;
            string hereOutside = CityAtlas.Outside(here);
            var hereRegion = WorldRegions.RegionOf(hereOutside);
            var def = district == null ? null : CityDistricts.Find(district);
            choice.Title = def != null ? def.Name : r.Name;
            choice.Subtitle = def != null ? r.Name + "  ·  " + r.State : r.State;
            choice.Detail = def != null ? def.Blurb : r.Tagline;
            int crews = WorldRegions.Crews(r).Count, beaten = WorldRegions.CrewsBeaten(d?.city, r);
            choice.Stats = Loc.T("CREWS") + " " + beaten + "/" + crews + "  ·  " + Loc.T("BIG RUN TITLES") + " " + CityBigRun.Titles(d?.world ?? new CityWorldSaveData(), r.Id);
            choice.Blocked = Blocked(d, region, district);
            string target = district ?? r.Main;
            choice.Here = target == hereOutside || (district == null && hereRegion.Id == r.Id);
            choice.InRegion = hereRegion.Id == r.Id;
            choice.CanDrive = choice.Blocked == null && !choice.InRegion;
            choice.CanSpawn = choice.Blocked == null && target != hereOutside;
            choice.Next = CityStory.Current(d?.world ?? new CityWorldSaveData())?.Region == r.Id && !choice.InRegion;
            // I25: your best time on this road.
            float best = d?.world == null || choice.InRegion ? 0f : d.world.RouteBest(hereRegion.Id, r.Id);
            if (best > 0f) choice.Stats += "  ·  " + Loc.T("BEST DRIVE") + " " + best.ToString("0.0") + " s";
            return choice;
        }

        /// <summary>Where a drive to a region (or one of its districts) leaves you.</summary>
        public static (string district, string spawn) DriveArrival(string region, string district)
        {
            var r = WorldRegions.Find(region);
            if (r == null) return (CityDistricts.FirstCallers, null);
            if (district == null || district == r.Main) return HighwayDrive.Arrival(region);
            return (district, null);
        }
    }
}
