using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic.PixelArt
{
    /// <summary>
    /// 28 Oct 2026 (Omari): the WORLD MAP's art. <see cref="District"/> draws a district from above (streets, rooftops,
    /// courts, trees, cars) so a region's plate is a complete little map; <see cref="Backdrop"/> is the country under the
    /// plates: two oceans, deserts, mountains, plains, lakes, a river and the interstates between the cities.
    /// </summary>
    public static class WorldMapArt
    {
        /// <summary>Pixels per city tile in a district's map texture.</summary>
        public const int TilePx = 4;
        /// <summary>The backdrop is drawn at a quarter of the map's size (shown 4× with crisp pixels).</summary>
        public const int BackdropScale = 4;

        private static RgbColor H(string hex) => RgbColor.FromHex(hex);

        // ------------------------------------------------------------------ a district from above

        private static readonly Dictionary<CityGround, string> GroundHex = new Dictionary<CityGround, string>
        {
            [CityGround.Grass] = "#5DAA4A", [CityGround.Sidewalk] = "#B9B7C2", [CityGround.Road] = "#3B3B46", [CityGround.RoadLine] = "#3B3B46",
            [CityGround.Crosswalk] = "#3B3B46", [CityGround.Asphalt] = "#56565F", [CityGround.Hardwood] = "#C98E55", [CityGround.Tile] = "#D9D6E2",
            [CityGround.Water] = "#2D7FD3", [CityGround.Pier] = "#8B6A50", [CityGround.Sand] = "#EAD7A0", [CityGround.Dirt] = "#A88466",
            [CityGround.Rubber] = "#C0473A", [CityGround.Wall] = "#2A2A35", [CityGround.Snow] = "#EEF3FF", [CityGround.Brick] = "#A65A3C",
            [CityGround.Desert] = "#D8B784", [CityGround.Rail] = "#3B3B46",
        };

        /// <summary>Rooftop colours per building style (0–5 Baller City, 6–13 the regions).</summary>
        private static readonly string[] Roofs =
        {
            "#8E3B32", "#6D4C3D", "#D9C7A3", "#8A8D99", "#5B7DB1", "#E2B34E",
            "#7A3E2E", "#6FA8DC", "#E8D5B0", "#C9B79C", "#3C1E5C", "#E07A5F", "#9C6ADE", "#7D6B5A",
        };

        /// <summary>A district from above, <see cref="TilePx"/> pixels a tile.</summary>
        public static PixelCanvas District(CityMap m)
        {
            int T = TilePx;
            var c = new PixelCanvas(m.Width * T, m.Height * T);
            var line = H("#F2C14E");
            var white = H("#F4F1DE");
            for (int ty = 0; ty < m.Height; ty++)
                for (int tx = 0; tx < m.Width; tx++)
                {
                    var g = m.Get(tx, ty);
                    var col = H(GroundHex.TryGetValue(g, out var hex) ? hex : "#777777");
                    uint h = (uint)(tx * 73856093 ^ ty * 19349663);
                    // A little texture so big areas aren't flat.
                    if ((g == CityGround.Grass || g == CityGround.Sand || g == CityGround.Desert || g == CityGround.Dirt) && h % 5 == 0) col = col.Darken(0.08f);
                    c.FillRect(tx * T, ty * T, T, T, col);
                    switch (g)
                    {
                        case CityGround.RoadLine:
                        {
                            bool across = m.Get(tx - 1, ty) == CityGround.RoadLine || m.Get(tx + 1, ty) == CityGround.RoadLine;
                            if (across) { if (tx % 2 == 0) c.FillRect(tx * T, ty * T + T / 2 - 1, T, 1, line); }
                            else if (ty % 2 == 0) c.FillRect(tx * T + T / 2 - 1, ty * T, 1, T, line);
                            break;
                        }
                        case CityGround.Crosswalk:
                            c.FillRect(tx * T, ty * T + 1, T, 1, white);
                            c.FillRect(tx * T, ty * T + 3, T, 1, white);
                            break;
                        case CityGround.Rail:
                            c.FillRect(tx * T + 1, ty * T, 1, T, H("#9AA0A6"));
                            break;
                        case CityGround.Water:
                            if (h % 7 == 0) c.FillRect(tx * T, ty * T + 2, 3, 1, H("#7FB8F0"));
                            break;
                        case CityGround.Pier:
                            c.FillRect(tx * T, ty * T + 3, T, 1, H("#6D5040"));
                            break;
                        case CityGround.Brick:
                            if ((tx + ty) % 2 == 0) c.Set(tx * T + 1, ty * T + 1, H("#8C4A30"));
                            break;
                        case CityGround.Tile:
                            c.FillRect(tx * T, ty * T, T, 1, H("#BFBBCB"));
                            break;
                    }
                }

            // Courts: the floor, the lines and the hoop.
            foreach (var k in m.Courts)
            {
                var floor = H(CourtFloor(k.Look.Floor));
                int x = k.X * T, y = k.Y * T, w = k.W * T, hh = k.H * T;
                c.FillRect(x, y, w, hh, floor);
                var lines = k.Look.Neon ? H("#00F0FF") : white;
                Box(c, x + 1, y + 1, w - 2, hh - 2, lines);
                // The key and the arc at the top.
                int kw = w / 3, kh = hh * 2 / 5;
                Box(c, x + (w - kw) / 2, y + hh - 1 - kh, kw, kh, lines);
                c.FillCircle(x + w / 2f, y + hh - 1 - kh, kw / 2f, new RgbColor(lines.r, lines.g, lines.b, 90));
                c.FillRect(x + w / 2 - 1, y + hh - 4, 3, 2, H("#FF7A1A"));
                if (k.Look.Logo) c.FillCircle(x + w / 2f, y + hh / 3f, 3f, new RgbColor(lines.r, lines.g, lines.b, 120));
            }

            // Buildings: rooftops with an edge, a shadow and roof clutter.
            foreach (var b in m.Buildings)
            {
                var roof = H(Roofs[Math.Max(0, b.Style) % Roofs.Length]);
                int x = b.X * T, y = b.Y * T, w = b.W * T, hh = b.H * T;
                c.FillRect(x + 2, y - 2, w, hh, new RgbColor(0, 0, 0, 70)); // shadow
                c.FillRect(x, y, w, hh, roof.Darken(0.35f));
                c.FillRect(x + 1, y + 1, w - 2, hh - 2, roof);
                c.FillRect(x + 1, y + hh - 3, w - 2, 1, roof.Lighten(0.25f));
                uint s = b.Seed * 2654435761u;
                int units = Math.Max(1, b.W * b.H / 14);
                for (int i = 0; i < units; i++)
                {
                    s = s * 1103515245u + 12345u;
                    int ux = x + 3 + (int)(s >> 8) % Math.Max(1, w - 8);
                    int uy = y + 3 + (int)(s >> 16) % Math.Max(1, hh - 8);
                    c.FillRect(ux, uy, 3, 3, roof.Darken(0.25f));
                    c.Set(ux + 1, uy + 2, roof.Lighten(0.35f));
                }
                if (b.Style == CityDistricts.GlassTower || b.Style == CityDistricts.NeonTower)
                    for (int gx = x + 3; gx < x + w - 3; gx += 4) c.FillRect(gx, y + 3, 1, hh - 6, roof.Lighten(0.2f));
                if (!string.IsNullOrEmpty(b.Sign)) c.FillRect(x + 2, y, w - 4, 2, H(Awnings[(int)(b.Seed % (uint)Awnings.Length)]));
            }

            // Props from above.
            foreach (var p in m.Props) Prop(c, p);
            return c;
        }

        private static readonly string[] Awnings = { "#E63946", "#2A9D8F", "#F4A261", "#4361EE", "#FF2E88", "#FFD166" };

        private static string CourtFloor(int style)
        {
            switch (style)
            {
                case (int)FloorStyle.Hardwood: return "#D4A261";
                case (int)FloorStyle.NeonGrid: return "#1A1033";
                case (int)FloorStyle.Tiles: return "#7FA3C7";
                case (int)FloorStyle.Rubber: return "#B5533C";
                default: return "#4A4A55";
            }
        }

        private static void Box(PixelCanvas c, int x, int y, int w, int h, RgbColor col)
        {
            c.FillRect(x, y, w, 1, col);
            c.FillRect(x, y + h - 1, w, 1, col);
            c.FillRect(x, y, 1, h, col);
            c.FillRect(x + w - 1, y, 1, h, col);
        }

        private static readonly string[] CarHex = { "#D62828", "#1D4ED8", "#F2F2F2", "#E9C46A", "#2EC4B6", "#7209B7", "#FFD60A", "#5A189A", "#0B6E4F" };

        private static void Prop(PixelCanvas c, CityProp p)
        {
            int T = TilePx;
            float cx = p.X * T + p.W * T * 0.5f, cy = p.Y * T + p.H * T * 0.5f;
            switch (p.Kind)
            {
                case CityPropKind.Tree:
                case CityPropKind.PineLit:
                case CityPropKind.Bush:
                    c.FillCircle(cx + 1, cy - 1, 3.2f, new RgbColor(0, 0, 0, 60));
                    c.FillCircle(cx, cy, 3.2f, H("#2F7D32"));
                    c.FillCircle(cx - 0.8f, cy + 0.8f, 1.6f, H("#4CAF50"));
                    break;
                case CityPropKind.Palm:
                    c.Line((int)cx - 3, (int)cy, (int)cx + 3, (int)cy, H("#2E8B57"));
                    c.Line((int)cx, (int)cy - 3, (int)cx, (int)cy + 3, H("#2E8B57"));
                    c.Line((int)cx - 2, (int)cy - 2, (int)cx + 2, (int)cy + 2, H("#3CB371"));
                    c.Line((int)cx - 2, (int)cy + 2, (int)cx + 2, (int)cy - 2, H("#3CB371"));
                    c.Set((int)cx, (int)cy, H("#8B5A2B"));
                    break;
                case CityPropKind.Cactus:
                    c.FillRect((int)cx - 1, (int)cy - 2, 2, 4, H("#3A7D44"));
                    c.Set((int)cx - 2, (int)cy, H("#3A7D44"));
                    c.Set((int)cx + 1, (int)cy + 1, H("#3A7D44"));
                    break;
                case CityPropKind.Lamp:
                case CityPropKind.LanternPost:
                    c.FillRect((int)cx - 1, (int)cy - 1, 2, 2, H("#FFE08A"));
                    break;
                case CityPropKind.Car:
                case CityPropKind.Ride:
                {
                    var body = p.Kind == CityPropKind.Ride ? H("#FFD166") : H(CarHex[Math.Abs(p.Variant) % CarHex.Length]);
                    int x = p.X * T, y = p.Y * T + 1, w = p.W * T, h = p.H * T - 2;
                    c.FillRect(x, y, w, h, body.Darken(0.3f));
                    c.FillRect(x + 1, y + 1, w - 2, h - 2, body);
                    c.FillRect(x + w / 2 - 2, y + 1, 4, h - 2, H("#9AD1FF"));
                    if (p.Kind == CityPropKind.Ride) c.FillRect(x + w / 2 - 1, y + h, 2, 2, H("#FFFFFF"));
                    break;
                }
                case CityPropKind.Fountain:
                    c.FillCircle(cx, cy, p.W * T * 0.5f, H("#B9B7C2"));
                    c.FillCircle(cx, cy, p.W * T * 0.35f, H("#5DADE2"));
                    break;
                case CityPropKind.NeonSign:
                    c.FillRect((int)cx - 1, (int)cy - 1, 3, 2, H("#FF2E88"));
                    break;
                case CityPropKind.Fence:
                    c.FillRect(p.X * T + 1, p.Y * T + 1, 2, 2, H("#7D8597"));
                    break;
                case CityPropKind.Hoop:
                    break;
                case CityPropKind.Graffiti:
                case CityPropKind.Mural:
                    for (int i = 0; i < p.W * T; i++)
                        c.FillRect(p.X * T + i, p.Y * T + 1, 1, 2, H(Awnings[(i / 3) % Awnings.Length]));
                    break;
                case CityPropKind.Poster:
                    c.FillRect(p.X * T, p.Y * T + 1, p.W * T, 2, H("#FFD166"));
                    break;
                case CityPropKind.Bleachers:
                case CityPropKind.Crowd:
                    c.FillRect(p.X * T, p.Y * T, p.W * T, p.H * T, H("#8D99AE"));
                    for (int yy = p.Y * T + 1; yy < (p.Y + p.H) * T; yy += 2) c.FillRect(p.X * T, yy, p.W * T, 1, H("#5C677D"));
                    break;
                case CityPropKind.CableCar:
                case CityPropKind.TacoTruck:
                case CityPropKind.FoodCart:
                    c.FillRect(p.X * T, p.Y * T, p.W * T, p.H * T, H(p.Kind == CityPropKind.CableCar ? "#C0392B" : p.Kind == CityPropKind.TacoTruck ? "#F4A261" : "#E9ECEF"));
                    c.FillRect(p.X * T + 1, p.Y * T + 1, p.W * T - 2, 1, H("#FFFFFF"));
                    break;
                case CityPropKind.BridgeTower:
                case CityPropKind.Crane:
                    c.FillRect(p.X * T, p.Y * T, p.W * T, p.H * T, H(p.Kind == CityPropKind.BridgeTower ? "#C0392B" : "#E9C46A"));
                    c.FillRect(p.X * T + 1, p.Y * T + 1, p.W * T - 2, p.H * T - 2, H(p.Kind == CityPropKind.BridgeTower ? "#E63946" : "#F4D35E"));
                    break;
                case CityPropKind.Pillar:
                    c.FillRect(p.X * T, p.Y * T, T, T, H("#8A8D99"));
                    break;
                case CityPropKind.Umbrella:
                    c.FillCircle(cx, cy, 2.5f, H(p.Variant % 2 == 0 ? "#E63946" : "#2A9D8F"));
                    break;
                case CityPropKind.Lifeguard:
                    c.FillRect(p.X * T, p.Y * T, p.W * T, p.H * T, H("#E63946"));
                    break;
                case CityPropKind.Rock:
                    c.FillCircle(cx, cy, 2.5f, H("#8D7B68"));
                    break;
                case CityPropKind.Billboard:
                    c.FillRect(p.X * T, p.Y * T + 1, p.W * T, 2, H("#4CC9F0"));
                    break;
                default:
                    if (p.Solid && p.W * p.H <= 2) c.FillRect(p.X * T + 1, p.Y * T + 1, p.W * T - 2, p.H * T - 2, H("#6C6F7D"));
                    break;
            }
        }

        // ------------------------------------------------------------------ the rest of the region round its districts

        /// <summary>How the city round a region's districts looks on its plate.</summary>
        private sealed class Fabric
        {
            public string Ground, Park, Lot = "#56565F";
            public string[] Roofs;
            public int Block = 22, ParkEvery = 5, LotEvery = 7;
            public string Water;           // "bottom", "top", "left" or null
            public bool Neon, Palms, Sparse;
        }

        private static Fabric FabricFor(string region)
        {
            switch (region)
            {
                case WorldRegions.NewYork: return new Fabric { Ground = "#B9B7C2", Park = "#4E9A4E", Roofs = new[] { "#7A3E2E", "#8E3B32", "#6FA8DC", "#8A8D99", "#5B5F6E" }, Block = 20, ParkEvery = 9, Water = "left" };
                case WorldRegions.LosAngeles: return new Fabric { Ground = "#CFC6B0", Park = "#7DB55A", Roofs = new[] { "#E8D5B0", "#C9B79C", "#E2B34E", "#D9C7A3", "#E07A5F" }, Block = 24, Water = "bottom", Palms = true };
                case WorldRegions.LasVegas: return new Fabric { Ground = "#D8B784", Park = "#B9A06A", Roofs = new[] { "#3C1E5C", "#E07A5F", "#9C6ADE", "#E8D5B0", "#2B2D42" }, Block = 26, ParkEvery = 4, Neon = true, Sparse = true };
                case WorldRegions.TheBay: return new Fabric { Ground = "#B9B7C2", Park = "#5DAA4A", Roofs = new[] { "#9C6ADE", "#E9C46A", "#7D6B5A", "#6FA8DC", "#E07A5F", "#2A9D8F" }, Block = 20, Water = "top" };
                default: return new Fabric { Ground = "#B9B7C2", Park = "#5DAA4A", Roofs = new[] { "#8E3B32", "#6D4C3D", "#D9C7A3", "#8A8D99", "#5B7DB1", "#E2B34E" }, Block = 22 };
            }
        }

        /// <summary>
        /// The city filling a region's plate (<paramref name="wTiles"/> × <paramref name="hTiles"/>, one pixel a tile, shown
        /// at the map's scale): a street grid with the region's rooftops, parks, lots and its water, so the districts you
        /// can walk sit inside a whole city. The districts' own maps are drawn over it.
        /// </summary>
        public static PixelCanvas Plate(string region, int wTiles, int hTiles)
        {
            var f = FabricFor(region);
            var c = new PixelCanvas(wTiles, hTiles);
            c.Fill(H(f.Ground));
            int B = f.Block;
            uint seed = (uint)region.GetHashCode() * 2654435761u;
            var road = H("#3B3B46");
            var dash = H("#F2C14E");
            var lot = H(f.Lot);
            var park = H(f.Park);
            // Blocks.
            for (int by = 0, row = 0; by < hTiles; by += B, row++)
                for (int bx = 0, col = 0; bx < wTiles; bx += B, col++)
                {
                    seed = seed * 1103515245u + 12345u;
                    int x0 = bx + 3, y0 = by + 3, w = B - 5, h = B - 5;
                    int kind = (int)((seed >> 16) % 100);
                    if (kind < 100 / f.ParkEvery)
                    {
                        c.FillRect(x0, y0, w, h, park);
                        for (int i = 0; i < 5; i++)
                        {
                            seed = seed * 1103515245u + 12345u;
                            int tx = x0 + 1 + (int)((seed >> 8) % (uint)Math.Max(1, w - 2)), ty = y0 + 1 + (int)((seed >> 18) % (uint)Math.Max(1, h - 2));
                            if (region == WorldRegions.LasVegas) c.Set(tx, ty, H("#3A7D44"));
                            else c.FillRect(tx, ty, 2, 2, H(f.Palms ? "#2E8B57" : "#2F7D32"));
                        }
                        continue;
                    }
                    if (kind < 100 / f.ParkEvery + 100 / f.LotEvery)
                    {
                        c.FillRect(x0, y0, w, h, lot);
                        for (int cy = y0 + 1; cy < y0 + h - 1; cy += 3)
                            for (int cx = x0 + 1; cx < x0 + w - 2; cx += 4)
                            {
                                seed = seed * 1103515245u + 12345u;
                                if ((seed >> 20) % 3 == 0) continue;
                                c.FillRect(cx, cy, 2, 1, H(CarHex[(seed >> 8) % (uint)CarHex.Length]));
                            }
                        continue;
                    }
                    // Buildings: rooftops packed along the block, some taller (lighter) than others.
                    for (int yy = y0; yy < y0 + h;)
                    {
                        seed = seed * 1103515245u + 12345u;
                        int bh = Math.Min(y0 + h - yy, 4 + (int)((seed >> 12) % 5));
                        for (int xx = x0; xx < x0 + w;)
                        {
                            seed = seed * 1103515245u + 12345u;
                            int bw = Math.Min(x0 + w - xx, 3 + (int)((seed >> 10) % 5));
                            if (f.Sparse && (seed >> 22) % 4 == 0) { xx += bw + 1; continue; }
                            var roof = H(f.Roofs[(seed >> 16) % (uint)f.Roofs.Length]);
                            if ((seed >> 24) % 3 == 0) roof = roof.Lighten(0.12f);
                            c.FillRect(xx, yy, bw, bh, roof.Darken(0.3f));
                            if (bw > 2 && bh > 2) c.FillRect(xx + 1, yy + 1, bw - 2, bh - 2, roof);
                            if (f.Neon && (seed >> 6) % 3 == 0) c.Set(xx + bw / 2, yy, H((seed & 1) == 0 ? "#FF2E88" : "#00F0FF"));
                            xx += bw + ((seed >> 26) % 4 == 0 ? 1 : 0);
                        }
                        yy += bh + 1;
                    }
                }
            // Streets between the blocks, with sidewalks and a dashed centre line.
            var walk = H(f.Ground).Lighten(0.08f);
            for (int y = 0; y < hTiles; y += B)
            {
                c.FillRect(0, y, wTiles, 3, road);
                c.FillRect(0, y - 1, wTiles, 1, walk);
                c.FillRect(0, y + 3, wTiles, 1, walk);
                for (int x = 0; x < wTiles; x += 4) c.Set(x, y + 1, dash);
            }
            for (int x = 0; x < wTiles; x += B)
            {
                c.FillRect(x, 0, 3, hTiles, road);
                for (int y = 0; y < hTiles; y += 4) if (y % B > 3) c.Set(x + 1, y, dash);
            }
            // The region's water.
            if (f.Water != null)
            {
                var sea = H("#2D7FD3");
                var foam = H("#7FB8F0");
                var sand = H("#EAD7A0");
                for (int x = 0; x < wTiles; x++)
                    for (int y = 0; y < hTiles; y++)
                    {
                        float edge;
                        switch (f.Water)
                        {
                            case "bottom": edge = 18 + (float)Math.Sin(x * 0.08) * 3; if (y < edge) { c.Set(x, y, y > edge - 5 ? sand : sea); if (y < edge - 6 && (x + y * 3) % 17 == 0) c.Set(x, y, foam); } break;
                            case "top": edge = hTiles - 16 - (float)Math.Sin(x * 0.07) * 4; if (y > edge) { c.Set(x, y, sea); if ((x * 3 + y) % 19 == 0) c.Set(x, y, foam); } break;
                            case "left": edge = 12 + (float)Math.Sin(y * 0.09) * 2; if (x < edge) { c.Set(x, y, sea); if ((x + y * 5) % 13 == 0) c.Set(x, y, foam); } break;
                        }
                    }
                if (f.Water == "top")
                {
                    // The bridge across the water.
                    int bx = wTiles / 3, top = hTiles;
                    for (int y = hTiles - 22; y < top; y++) { c.FillRect(bx, y, 3, 1, H("#C0392B")); c.Set(bx + 1, y, road); }
                }
            }
            // A soft edge so the plate sits on the land.
            var rim = H("#14141F");
            for (int x = 0; x < wTiles; x++) { c.Set(x, 0, rim); c.Set(x, hTiles - 1, rim); }
            for (int y = 0; y < hTiles; y++) { c.Set(0, y, rim); c.Set(wTiles - 1, y, rim); }
            return c;
        }

        // ------------------------------------------------------------------ the country under the plates

        /// <summary>
        /// The country, a quarter of <see cref="WorldAtlas.Width"/> × <see cref="WorldAtlas.Height"/>: the Pacific on the
        /// left with its coastline, the Atlantic on the right, desert in the west, mountains, green plains, lakes, a river,
        /// clouds over the sea and the interstates joining the region plates (drawn under them).
        /// </summary>
        public static PixelCanvas Backdrop()
        {
            int S = BackdropScale;
            int W = (int)WorldAtlas.Width / S, Hh = (int)WorldAtlas.Height / S;
            var c = new PixelCanvas(W, Hh);
            var sea = H("#1E5AA8");
            var seaLight = H("#2C73C9");
            for (int y = 0; y < Hh; y++)
            {
                int west = WestCoast(y), east = EastCoast(y);
                for (int x = 0; x < W; x++)
                {
                    uint h = (uint)(x * 73856093 ^ y * 19349663);
                    if (x < west || x > east)
                    {
                        var col = ((x + y * 3) % 23 == 0 || h % 61 == 0) ? seaLight : sea;
                        // Shallows along the coast.
                        if (Math.Abs(x - west) < 3 && x < west || Math.Abs(x - east) < 3 && x > east) col = H("#3B8FD9");
                        c.Set(x, y, col);
                        continue;
                    }
                    c.Set(x, y, Land(x, y, h, W));
                }
            }
            // Beaches along both coasts.
            for (int y = 0; y < Hh; y++)
            {
                int west = WestCoast(y), east = EastCoast(y);
                c.Set(west, y, H("#EAD7A0"));
                c.Set(west + 1, y, H("#E0C98E"));
                if (east < W) { c.Set(east, y, H("#EAD7A0")); c.Set(east - 1, y, H("#E0C98E")); }
            }

            // Mountains: the Sierra (west) and the big range in the middle.
            uint seed = 1234;
            for (int i = 0; i < 70; i++)
            {
                seed = seed * 1103515245u + 12345u;
                int x = 120 + (int)(seed >> 16) % 50, y = 60 + (int)(seed >> 8) % 330;
                if (!OnPlate(x, y, 6)) Mountain(c, x, y, 5 + (int)(seed % 4));
            }
            for (int i = 0; i < 90; i++)
            {
                seed = seed * 1103515245u + 12345u;
                int x = 345 + (int)(seed >> 16) % 45, y = 40 + (int)(seed >> 8) % 380;
                if (!OnPlate(x, y, 6)) Mountain(c, x, y, 6 + (int)(seed % 5));
            }
            // The big lakes up north, and a river down the middle to the gulf.
            foreach (var (lx, ly, rx, ry) in new[] { (610, 360, 26, 9), (650, 340, 18, 12), (585, 330, 12, 16), (690, 368, 14, 6) })
                if (!OnPlate(lx, ly, rx)) c.FillCircleEllipse(lx, ly, rx, ry, H("#2C73C9"));
            for (int y = Hh - 1; y >= 0; y--)
            {
                int rxPos = 520 + (int)Math.Round(Math.Sin(y * 0.03) * 14 + Math.Sin(y * 0.11) * 4);
                if (!OnPlate(rxPos, y, 1)) { c.Set(rxPos, y, H("#3B8FD9")); c.Set(rxPos + 1, y, H("#2C73C9")); }
            }

            // Forest dots in the north-east.
            for (int i = 0; i < 260; i++)
            {
                seed = seed * 1103515245u + 12345u;
                int x = 600 + (int)(seed >> 16) % 170, y = 200 + (int)(seed >> 8) % 240;
                if (x < EastCoast(y) - 2 && !OnPlate(x, y, 3)) { c.FillCircle(x, y, 1.6f, H("#2F6B2F")); c.Set(x, y + 1, H("#4E9A4E")); }
            }
            // Cactus and sage in the desert.
            for (int i = 0; i < 200; i++)
            {
                seed = seed * 1103515245u + 12345u;
                int x = WestCoast(0) + 20 + (int)(seed >> 16) % 300, y = 10 + (int)(seed >> 8) % 300;
                if (x < 340 && x > WestCoast(y) + 6 && !OnPlate(x, y, 3) && Land(x, y, 0, W).g < 200) c.Set(x, y, H("#6B8E4E"));
            }

            // The interstates between the plates.
            foreach (var (a, b) in WorldAtlas.Highways) Highway(c, WorldAtlas.Plate(a), WorldAtlas.Plate(b));

            // Clouds over the oceans, a few boats.
            for (int i = 0; i < 14; i++)
            {
                seed = seed * 1103515245u + 12345u;
                bool westSide = i % 2 == 0;
                int x = westSide ? 4 + (int)(seed >> 16) % 40 : W - 40 + (int)(seed >> 16) % 36;
                int y = 20 + (int)(seed >> 8) % (Hh - 40);
                if (westSide && x > WestCoast(y) - 8 || !westSide && x < EastCoast(y) + 8) continue;
                Cloud(c, x, y);
            }
            foreach (var (bx, by) in new[] { (30, 120), (40, 260), (20, 380), (W - 18, 290), (W - 12, 380) })
                if (bx < WestCoast(by) - 4 || bx > EastCoast(by) + 4) Boat(c, bx, by);
            Compass(c, 26, 40);
            return c;
        }

        private static int WestCoast(int y) => 52 + (int)(Math.Sin(y * 0.021) * 10 + Math.Sin(y * 0.07) * 4) + (y < 90 ? (90 - y) / 3 : 0);

        private static int EastCoast(int y)
        {
            // Straight-ish up the right edge, bending in below New York to leave room for the Atlantic.
            float south = 790f - (float)Math.Sin(y * 0.018) * 8f;
            float north = 742f + (float)Math.Sin(y * 0.05) * 6f;
            float t = Math.Max(0f, Math.Min(1f, (260 - y) / 70f));
            t = t * t * (3f - 2f * t);
            return Math.Min(799, (int)Math.Round(north + (south - north) * (1f - t)));
        }

        private static RgbColor Land(int x, int y, uint h, int W)
        {
            // West to east: golden-green coast, desert, high plains, green plains, deep green forest.
            int coast = WestCoast(y);
            RgbColor col;
            if (x < coast + 40) col = RgbColor.Lerp(H("#9DB36A"), H("#D9BC85"), Math.Max(0f, (x - coast) / 40f));
            else if (x < 300) col = H("#D9BC85");
            else if (x < 420) col = RgbColor.Lerp(H("#D9BC85"), H("#A9BF6C"), (x - 300) / 120f);
            else if (x < 560) col = RgbColor.Lerp(H("#A9BF6C"), H("#7FB35A"), (x - 420) / 140f);
            else col = RgbColor.Lerp(H("#7FB35A"), H("#4F8F44"), Math.Min(1f, (x - 560) / 200f));
            float north = Math.Max(0f, Math.Min(1f, (y - 250) / 90f)) * Math.Max(0f, Math.Min(1f, (260 - x) / 90f));
            if (north > 0f) col = RgbColor.Lerp(col, H("#8FAE5A"), north * 0.5f); // greener up north by the Bay
            if (h % 9 == 0) col = col.Darken(0.06f);
            else if (h % 13 == 0) col = col.Lighten(0.05f);
            return col;
        }

        private static bool OnPlate(int x, int y, int pad)
        {
            int S = BackdropScale;
            foreach (var r in WorldRegions.All)
            {
                var p = WorldAtlas.Plate(r.Id);
                if (x >= p.X / S - pad && x <= (p.X + p.W) / S + pad && y >= p.Y / S - pad && y <= (p.Y + p.H) / S + pad) return true;
            }
            return false;
        }

        private static void Mountain(PixelCanvas c, int x, int y, int size)
        {
            var dark = H("#6D5D4B");
            var light = H("#9C8A72");
            var snow = H("#F4F1DE");
            for (int row = 0; row < size; row++)
            {
                int half = size - row;
                c.FillRect(x - half, y + row, half, 1, light);
                c.FillRect(x, y + row, half, 1, dark);
                if (row >= size - 2) { c.Set(x, y + row, snow); c.Set(x - 1, y + row, snow); }
            }
        }

        private static void Highway(PixelCanvas c, MapRect a, MapRect b)
        {
            int S = BackdropScale;
            // From the nearest edges of the plates, with a bend in the middle.
            float ax = Clamp(b.CenterX, a.X, a.X + a.W) / S, ay = Clamp(b.CenterY, a.Y, a.Y + a.H) / S;
            float bx = Clamp(a.CenterX, b.X, b.X + b.W) / S, by = Clamp(a.CenterY, b.Y, b.Y + b.H) / S;
            float mx = (ax + bx) / 2f, my = (ay + by) / 2f + (Math.Abs(bx - ax) > Math.Abs(by - ay) ? 10f : 0f);
            Road(c, ax, ay, mx, my);
            Road(c, mx, my, bx, by);
        }

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        private static void Road(PixelCanvas c, float x0, float y0, float x1, float y1)
        {
            int n = (int)Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0)) + 1;
            var asphalt = H("#34343E");
            var dash = H("#F2C14E");
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                int x = (int)Math.Round(x0 + (x1 - x0) * t), y = (int)Math.Round(y0 + (y1 - y0) * t);
                c.FillRect(x - 1, y - 1, 3, 3, asphalt);
            }
            for (int i = 0; i <= n; i++)
            {
                if (i % 4 >= 2) continue;
                float t = i / (float)n;
                c.Set((int)Math.Round(x0 + (x1 - x0) * t), (int)Math.Round(y0 + (y1 - y0) * t), dash);
            }
        }

        private static void Cloud(PixelCanvas c, int x, int y)
        {
            var w = new RgbColor(255, 255, 255, 170);
            c.FillCircle(x, y, 3.5f, w);
            c.FillCircle(x + 4, y + 1, 4.5f, w);
            c.FillCircle(x + 8, y, 3f, w);
        }

        private static void Boat(PixelCanvas c, int x, int y)
        {
            c.FillRect(x - 2, y, 5, 1, H("#F4F1DE"));
            c.FillRect(x - 1, y - 1, 3, 1, H("#8B5A2B"));
            c.Set(x, y + 1, H("#E63946"));
            c.FillRect(x - 6, y - 1, 3, 1, new RgbColor(255, 255, 255, 120));
        }

        private static void Compass(PixelCanvas c, int x, int y)
        {
            c.FillCircle(x, y, 9f, new RgbColor(244, 241, 222, 90));
            c.Line(x, y - 9, x, y + 9, H("#F4F1DE"));
            c.Line(x - 9, y, x + 9, y, H("#F4F1DE"));
            c.FillRect(x - 1, y + 5, 3, 4, H("#E63946"));
            PixelFont.DrawCentered(c, "N", x, y + 17, H("#F4F1DE"));
        }

        // ------------------------------------------------------------------ markers

        /// <summary>YOU on the map: a gold ring with a dot.</summary>
        public static PixelCanvas You()
        {
            var c = new PixelCanvas(13, 13);
            c.FillCircle(6, 6, 6f, H("#14141F"));
            c.FillCircle(6, 6, 5f, H("#FFD166"));
            c.FillCircle(6, 6, 2.5f, H("#E63946"));
            return c;
        }
    }

    internal static class PixelCanvasEllipse
    {
        /// <summary>A filled ellipse (radii rx, ry).</summary>
        public static void FillCircleEllipse(this PixelCanvas c, int cx, int cy, int rx, int ry, RgbColor col)
        {
            for (int y = -ry; y <= ry; y++)
                for (int x = -rx; x <= rx; x++)
                    if (x * x / (float)(rx * rx) + y * y / (float)(ry * ry) <= 1f) c.Set(cx + x, cy + y, col);
        }
    }
}
