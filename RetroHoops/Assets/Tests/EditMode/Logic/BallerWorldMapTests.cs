using System;
using System.Collections.Generic;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using NUnit.Framework;

namespace CallerRetroBall.Tests
{
    /// <summary>
    /// 28 Oct 2026 (Omari): the bigger regions (a grid district in each), the zoomable WORLD MAP (layout, taps, DRIVE THERE /
    /// SPAWN THERE) and the cities along THE DRIVE.
    /// </summary>
    public class BallerWorldMapTests
    {
        private static ContentCatalog C => _c ??= DefaultContent.Create();
        private static ContentCatalog _c;

        private static CareerSaveData NewCareer()
        {
            var d = Career.New(C);
            d.world = new CityWorldSaveData();
            d.city = new CitySaveData();
            return d;
        }

        // ------------------------------------------------------------------ the grid districts

        [Test]
        public void EachRegion_HasAGridDistrict_FullOfBuildingsAndStreets_ReachableFromTheRest()
        {
            foreach (var id in new[] { CityDistricts.NyAvenues, CityDistricts.LaBoulevard, CityDistricts.LvNeonMile, CityDistricts.BayCrosstown })
            {
                var m = CityDistricts.Map(id);
                Assert.AreEqual(72, m.Width, id);
                Assert.GreaterOrEqual(m.Buildings.Count, 14, id + " should be lined with buildings");
                Assert.AreEqual(1, m.Courts.Count, id);
                Assert.IsNotNull(C.Court(m.Courts[0].CourtId), id);
                int road = 0;
                for (int y = 0; y < m.Height; y++)
                    for (int x = 0; x < m.Width; x++)
                        if (m.Get(x, y) == CityGround.Road || m.Get(x, y) == CityGround.RoadLine || m.Get(x, y) == CityGround.Rail) road++;
                Assert.Greater(road, 900, id + " has its street grid");
                // Buildings never overlap each other or the roads.
                for (int i = 0; i < m.Buildings.Count; i++)
                {
                    var a = m.Buildings[i];
                    for (int y = a.Y; y < a.Y + a.H; y++)
                        for (int x = a.X; x < a.X + a.W; x++)
                            Assert.AreNotEqual(CityGround.Road, m.Get(x, y), id + " " + a.Id + " is on the road");
                    for (int j = i + 1; j < m.Buildings.Count; j++)
                    {
                        var b = m.Buildings[j];
                        bool overlap = a.X < b.X + b.W && b.X < a.X + a.W && a.Y < b.Y + b.H && b.Y < a.Y + a.H;
                        Assert.IsFalse(overlap, id + ": " + a.Id + " overlaps " + b.Id);
                    }
                }
                // You can walk from the road in to the court.
                var start = m.DefaultSpawn.Position;
                Assert.IsTrue(m.TryGetSpawn(m.Courts[0].Id, out var courtSpawn), id);
                Assert.Greater(CityPath.Find(m, start, courtSpawn.Position, m.Width * m.Height).Count, 0, id + " court unreachable");
            }
        }

        // ------------------------------------------------------------------ the world map

        [Test]
        public void WorldMap_PlatesFitAndDontOverlap_AndEveryDistrictSitsInItsOwnCell()
        {
            var plates = new List<(string, MapRect)>();
            foreach (var r in WorldRegions.All)
            {
                var p = WorldAtlas.Plate(r.Id);
                Assert.GreaterOrEqual(p.X, 0f, r.Id);
                Assert.GreaterOrEqual(p.Y, 0f, r.Id);
                Assert.LessOrEqual(p.X + p.W, WorldAtlas.Width, r.Id);
                Assert.LessOrEqual(p.Y + p.H, WorldAtlas.Height, r.Id);
                foreach (var (other, q) in plates) Assert.IsFalse(p.Overlaps(q), r.Id + " overlaps " + other);
                plates.Add((r.Id, p));

                var cells = new List<MapRect>();
                foreach (var d in r.Districts)
                {
                    var cell = WorldAtlas.CellOf(d);
                    var rect = WorldAtlas.DistrictRect(d);
                    Assert.IsTrue(cell.Contains(rect.X, rect.Y) && cell.Contains(rect.X + rect.W, rect.Y + rect.H), d + " doesn't fit its cell");
                    foreach (var other in cells) Assert.IsFalse(cell.Overlaps(new MapRect(other.X + 1, other.Y + 1, other.W - 2, other.H - 2)), d + " shares a cell");
                    cells.Add(cell);
                    // A tap in the middle of the district picks it.
                    var pick = WorldAtlas.Pick(rect.CenterX, rect.CenterY);
                    Assert.AreEqual(r.Id, pick.Region, d);
                    Assert.AreEqual(d, pick.District, d);
                }
            }
            Assert.IsTrue(WorldAtlas.Pick(5f, 5f).IsNone, "the ocean is nothing");
            // Indoors you're shown at the district outside.
            Assert.AreEqual(WorldAtlas.CellOf(CityDistricts.Downtown).X, WorldAtlas.CellOf(CityDistricts.Arena).X);
        }

        [Test]
        public void WorldMap_DriveToOtherRegions_SpawnAnywhereOpen_LockedStaysLocked()
        {
            var d = NewCareer();
            // In Baller City with nothing else open yet.
            var home = WorldAtlas.Describe(d, CityDistricts.FirstCallers, WorldRegions.BallerCity, CityDistricts.Downtown);
            Assert.IsFalse(home.CanDrive, "you walk or spawn inside your own region");
            Assert.IsTrue(home.CanSpawn);
            var here = WorldAtlas.Describe(d, CityDistricts.FirstCallers, WorldRegions.BallerCity, CityDistricts.FirstCallers);
            Assert.IsTrue(here.Here);
            Assert.IsFalse(here.CanSpawn, "already here");
            var ny = WorldAtlas.Describe(d, CityDistricts.FirstCallers, WorldRegions.NewYork, null);
            Assert.IsNotNull(ny.Blocked, "New York opens in the comic");
            Assert.IsFalse(ny.CanDrive);
            Assert.IsFalse(ny.CanSpawn);
            Assert.IsFalse(WorldAtlas.Spawn(d, WorldRegions.NewYork, CityDistricts.NyAvenues));

            // Open New York: drive or spawn.
            d.world.regionsSeen.Add(WorldRegions.NewYork);
            ny = WorldAtlas.Describe(d, CityDistricts.FirstCallers, WorldRegions.NewYork, CityDistricts.NyAvenues);
            Assert.IsNull(ny.Blocked);
            Assert.IsTrue(ny.CanDrive);
            Assert.IsTrue(ny.CanSpawn);
            Assert.AreEqual("THE AVENUES", ny.Title);
            Assert.IsTrue(WorldAtlas.Spawn(d, WorldRegions.NewYork, CityDistricts.NyAvenues));
            Assert.AreEqual(CityDistricts.NyAvenues, d.city.district);
            Assert.IsTrue(CityDistricts.Map(CityDistricts.NyAvenues).Walkable(new Vec2(d.city.x, d.city.y)));
            // A region's main district puts you at your ride.
            Assert.IsTrue(WorldAtlas.Spawn(d, WorldRegions.NewYork, null));
            Assert.AreEqual(CityDistricts.NyUptown, d.city.district);
            CityDistricts.Map(CityDistricts.NyUptown).TryGetSpawn("ride", out var ride);
            Assert.AreEqual(ride.Position.x, d.city.x, 0.01f);
            // Driving to a district arrives there; to the region, at its ride.
            Assert.AreEqual((CityDistricts.NyAvenues, (string)null), WorldAtlas.DriveArrival(WorldRegions.NewYork, CityDistricts.NyAvenues));
            Assert.AreEqual((CityDistricts.NyUptown, "ride"), WorldAtlas.DriveArrival(WorldRegions.NewYork, null));
            // Interiors can't be spawned into.
            Assert.IsNotNull(WorldAtlas.Blocked(d, WorldRegions.BallerCity, CityDistricts.Arena));
        }

        [Test]
        public void WorldMapArt_DrawsEveryDistrictAndPlate_AndTheCountry()
        {
            var back = WorldMapArt.Backdrop();
            Assert.AreEqual((int)WorldAtlas.Width / WorldMapArt.BackdropScale, back.Width);
            Assert.AreEqual(back.Width * back.Height, back.OpaqueCount(), "no holes in the country");
            foreach (var r in WorldRegions.All)
            {
                var p = WorldAtlas.Plate(r.Id);
                var plate = WorldMapArt.Plate(r.Id, (int)(p.W / WorldAtlas.PxPerTile), (int)(p.H / WorldAtlas.PxPerTile));
                Assert.AreEqual(plate.Width * plate.Height, plate.OpaqueCount(), r.Id);
                foreach (var d in r.Districts)
                {
                    var m = CityDistricts.Map(d);
                    var art = WorldMapArt.District(m);
                    Assert.AreEqual(m.Width * WorldMapArt.TilePx, art.Width, d);
                    Assert.AreEqual(art.Width * art.Height, art.OpaqueCount(), d);
                }
            }
        }

        // ------------------------------------------------------------------ the cities along the drive

        [Test]
        public void TheDrive_LeavesAndArrivesThroughTheCities_AndCrossesTheStateLines()
        {
            var f = DriveScenery.Layout(WorldRegions.LosAngeles, WorldRegions.NewYork, 7);
            float L = HighwayDrive.Length;
            int fromBuildings = 0, toBuildings = 0;
            for (int i = 0; i < f.Count; i++)
            {
                if (i > 0) Assert.GreaterOrEqual(f[i].Y, f[i - 1].Y, "sorted");
                if (f[i].Kind != DriveFeatureKind.Building) continue;
                Assert.IsTrue(f[i].Side == -1 || f[i].Side == 1);
                if (f[i].Region == WorldRegions.LosAngeles) { fromBuildings++; Assert.Less(f[i].Y, L * DriveScenery.CityOut); }
                else { toBuildings++; Assert.AreEqual(WorldRegions.NewYork, f[i].Region); Assert.Greater(f[i].Y, L * DriveScenery.CityIn - 0.01f); }
            }
            Assert.Greater(fromBuildings, 40, "LA lines the road on the way out");
            Assert.Greater(toBuildings, 60, "New York lines the road on the way in");
            // Buildings on one side don't overlap each other.
            foreach (int side in new[] { -1, 1 })
            {
                float end = -1f;
                foreach (var b in f)
                {
                    if (b.Kind != DriveFeatureKind.Building || b.Side != side) continue;
                    if (end > 0f && b.Y < end) Assert.GreaterOrEqual(b.Y, end - 0.01f);
                    end = b.Y + b.H * DriveScenery.TileUnits;
                }
            }
            var lines = DriveScenery.StateLines(WorldRegions.LosAngeles, WorldRegions.NewYork);
            var states = lines.ConvertAll(l => l.state);
            Assert.AreEqual("NEVADA,UTAH,COLORADO,HOME,OHIO,PENNSYLVANIA,NEW YORK", string.Join(",", states));
            foreach (var (p, _) in lines) Assert.IsTrue(p > DriveScenery.CityOut && p < DriveScenery.CityIn, "state lines are out in the country");
            Assert.AreEqual(0, DriveScenery.StateLines(WorldRegions.TheBay, WorldRegions.LosAngeles).Count, "both in California");
            Assert.AreEqual("CALIFORNIA", string.Join(",", DriveScenery.StateLines(WorldRegions.LasVegas, WorldRegions.TheBay).ConvertAll(l => l.state)));
            // The exits name real districts of the city you're arriving in.
            var exits = f.FindAll(x => x.Kind == DriveFeatureKind.Gantry && x.Region == WorldRegions.NewYork);
            Assert.GreaterOrEqual(exits.Count, 3);
            Assert.IsTrue(exits.Exists(x => x.Sub == "THE AVENUES" || x.Sub == "MIDTOWN"));
            Assert.IsTrue(f.Exists(x => x.Kind == DriveFeatureKind.Leaving && x.Sub == "LOS ANGELES"));
            // Every sign fits its text.
            foreach (var x in f)
            {
                if (x.Kind == DriveFeatureKind.Gantry) Assert.Greater(DriveArt.Gantry(x.Text, x.Sub).Width, 0);
                else if (x.Kind != DriveFeatureKind.Building && x.Kind != DriveFeatureKind.CrossStreet) Assert.Greater(DriveArt.Sign(x.Kind, x.Text, x.Sub).Width, 0);
            }
        }
    }
}
