using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public enum DriveFeatureKind { Building = 0, CrossStreet = 1, Gantry = 2, StateLine = 3, Leaving = 4, MileMarker = 5 }

    /// <summary>One thing beside (or over) the highway on THE DRIVE.</summary>
    public struct DriveFeature
    {
        public DriveFeatureKind Kind;
        /// <summary>Distance along the road (drive units, 0..<see cref="HighwayDrive.Length"/>).</summary>
        public float Y;
        /// <summary>-1 left of the road, +1 right, 0 over it.</summary>
        public int Side;
        /// <summary>Buildings: the style and footprint (tiles) and a seed for the windows.</summary>
        public int Style, W, H;
        public uint Seed;
        /// <summary>Signs: the words.</summary>
        public string Text, Sub;
        public string Region;
    }

    /// <summary>
    /// 28 Oct 2026 (Omari): "make them bigger and complete looking when driving through each state". THE DRIVE now
    /// leaves through the city you're in and arrives through the city you're going to: both sides of the highway lined
    /// with that region's own buildings (brownstones and glass in New York, stucco and bungalows in LA, neon towers and
    /// motels in Vegas, Victorians and warehouses in the Bay), cross streets, green exit signs over the road naming its
    /// districts, NOW LEAVING, the state lines you cross on the way and the mile markers. This is the layout; CityDrive
    /// draws it.
    /// </summary>
    public static class DriveScenery
    {
        /// <summary>The city you leave lines the road until here, and the one you're going to from here on.</summary>
        public const float CityOut = 0.26f, CityIn = 0.64f;
        /// <summary>Drive units per city tile for the buildings (a 16-pixel tile drawn at 47 pixels a unit).</summary>
        public const float TileUnits = 0.34f;

        /// <summary>The city lining the road at <paramref name="progress"/> (null = open country).</summary>
        public static string CityAt(string from, string to, float progress) =>
            progress < CityOut ? from : progress > CityIn ? to : null;

        /// <summary>A region's buildings for the roadside: style, width and depth (tiles).</summary>
        public static (int style, int w, int h)[] Buildings(string region)
        {
            switch (region)
            {
                case WorldRegions.NewYork:
                    return new[] { (CityDistricts.Brownstone, 6, 7), (CityDistricts.GlassTower, 8, 11), (CityDistricts.Brownstone, 6, 7), (1, 7, 8), (CityDistricts.GlassTower, 7, 10), (0, 6, 8) };
                case WorldRegions.LosAngeles:
                    return new[] { (CityDistricts.Mission, 7, 7), (CityDistricts.Bungalow, 6, 6), (5, 7, 7), (CityDistricts.Mission, 6, 7), (2, 7, 8) };
                case WorldRegions.LasVegas:
                    return new[] { (CityDistricts.NeonTower, 8, 11), (CityDistricts.Motel, 9, 5), (CityDistricts.NeonTower, 7, 10), (CityDistricts.Mission, 6, 6) };
                case WorldRegions.TheBay:
                    return new[] { (CityDistricts.Victorian, 5, 8), (CityDistricts.Victorian, 5, 8), (CityDistricts.Warehouse, 8, 7), (CityDistricts.GlassTower, 7, 10), (CityDistricts.Victorian, 5, 8) };
                default:
                    return new[] { (0, 7, 8), (3, 7, 9), (2, 6, 7), (4, 6, 7), (1, 7, 8), (5, 6, 7) };
            }
        }

        /// <summary>The states across the country, west to east (where each starts on the map, 0..1).</summary>
        private static readonly (float x, string state)[] States =
        {
            (0f, "CALIFORNIA"), (0.17f, "NEVADA"), (0.31f, "UTAH"), (0.41f, "COLORADO"), (0.5f, "HOME"),
            (0.62f, "OHIO"), (0.74f, "PENNSYLVANIA"), (0.84f, "NEW YORK"),
        };

        /// <summary>The state at a point on the map (Baller City's is HOME).</summary>
        public static string StateAt(float mapX)
        {
            string s = States[0].state;
            foreach (var e in States) if (mapX >= e.x) s = e.state;
            return s;
        }

        /// <summary>The state lines crossed between two regions, in order, with where on the drive each one is (0..1).</summary>
        public static List<(float progress, string state)> StateLines(string from, string to)
        {
            var list = new List<(float, string)>();
            var a = WorldRegions.Find(from);
            var b = WorldRegions.Find(to);
            if (a == null || b == null) return list;
            float x0 = a.MapX, x1 = b.MapX;
            if (Math.Abs(x1 - x0) < 0.0001f) return list;
            bool east = x1 > x0;
            string current = StateAt(x0);
            // Walk the borders between the two, spread over the open-country stretch of the drive.
            var crossings = new List<(float t, string state)>();
            foreach (var e in States)
            {
                float edge = e.x;
                if (edge <= Math.Min(x0, x1) || edge > Math.Max(x0, x1)) continue;
                float t = (edge - x0) / (x1 - x0);
                // Going west you enter the state on the west side of the border.
                string entering = east ? e.state : StateBefore(edge);
                crossings.Add((t, entering));
            }
            crossings.Sort((p, q) => p.t.CompareTo(q.t));
            foreach (var (t, state) in crossings)
            {
                if (state == current) continue;
                current = state;
                float progress = CityOut + 0.02f + t * (CityIn - CityOut - 0.04f);
                list.Add((progress, state));
            }
            // The last one is the state you're arriving in (the WELCOME sign covers it at the end); keep it as a line too.
            return list;
        }

        private static string StateBefore(float edge)
        {
            string s = States[0].state;
            foreach (var e in States) if (e.x < edge) s = e.state;
            return s;
        }

        /// <summary>Everything along the road for a drive, sorted by distance.</summary>
        public static List<DriveFeature> Layout(string from, string to, uint seed)
        {
            var list = new List<DriveFeature>();
            float L = HighwayDrive.Length;
            var a = WorldRegions.Find(from) ?? WorldRegions.All[0];
            var b = WorldRegions.Find(to) ?? WorldRegions.All[0];
            uint s = seed == 0 ? 1u : seed;

            Strip(list, a.Id, 0f, L * CityOut, ref s);
            Strip(list, b.Id, L * CityIn, L - 4f, ref s);

            // Exit signs over the road: the districts of the city you're leaving, then the one you're going to.
            int exit = 1 + (int)(s % 20);
            for (int i = 1; i < a.Districts.Length && i <= 2; i++)
                list.Add(new DriveFeature { Kind = DriveFeatureKind.Gantry, Y = L * CityOut * (0.35f + 0.3f * (i - 1)), Text = "EXIT " + exit++, Sub = DistrictName(a.Districts[i]), Region = a.Id });
            list.Add(new DriveFeature { Kind = DriveFeatureKind.Leaving, Y = L * CityOut + 6f, Side = 1, Text = "NOW LEAVING", Sub = a.Name, Region = a.Id });
            exit += 30 + (int)(s % 50);
            list.Add(new DriveFeature { Kind = DriveFeatureKind.Gantry, Y = L * (CityIn - 0.04f), Text = b.Name, Sub = "NEXT " + b.Districts.Length + " EXITS", Region = b.Id });
            for (int i = 0; i < b.Districts.Length && i < 3; i++)
                list.Add(new DriveFeature { Kind = DriveFeatureKind.Gantry, Y = L * (CityIn + 0.08f + 0.09f * i), Text = "EXIT " + exit++, Sub = DistrictName(b.Districts[(i + 1) % b.Districts.Length]), Region = b.Id });

            // State lines on the way.
            foreach (var (p, state) in StateLines(from, to))
                list.Add(new DriveFeature { Kind = DriveFeatureKind.StateLine, Y = L * p, Side = -1, Text = "WELCOME TO", Sub = state == "HOME" ? "THE HOME STATE" : state });

            // Mile markers through the open country.
            int mile = 1;
            for (float y = L * CityOut + 20f; y < L * CityIn - 10f; y += 34f)
                list.Add(new DriveFeature { Kind = DriveFeatureKind.MileMarker, Y = y, Side = 1, Text = "MILE " + (mile++ * 12 + (int)(seed % 7)) });

            list.Sort((p, q) => p.Y.CompareTo(q.Y));
            return list;
        }

        private static string DistrictName(string id) => CityDistricts.Find(id)?.Name ?? id.ToUpperInvariant();

        /// <summary>Both sides of the road from y0 to y1: the region's buildings shoulder to shoulder, a cross street every few.</summary>
        private static void Strip(List<DriveFeature> list, string region, float y0, float y1, ref uint s)
        {
            var kinds = Buildings(region);
            foreach (int side in new[] { -1, 1 })
            {
                float y = y0 + (side > 0 ? 1.2f : 0f);
                int sinceStreet = 0;
                while (y < y1)
                {
                    s = s * 1103515245u + 12345u;
                    if (sinceStreet >= 4 && (s >> 16) % 3 == 0)
                    {
                        list.Add(new DriveFeature { Kind = DriveFeatureKind.CrossStreet, Y = y, Side = side, Region = region });
                        y += 2.2f;
                        sinceStreet = 0;
                        continue;
                    }
                    var (style, w, h) = kinds[(s >> 8) % (uint)kinds.Length];
                    float depth = h * TileUnits;
                    if (y + depth > y1) break;
                    list.Add(new DriveFeature { Kind = DriveFeatureKind.Building, Y = y, Side = side, Style = style, W = w, H = h, Seed = (s >> 4) % 3, Region = region });
                    y += depth + ((s >> 20) % 4 == 0 ? 0.4f : 0f);
                    sinceStreet++;
                }
            }
        }
    }
}
