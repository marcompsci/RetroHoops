using System.Collections.Generic;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using CallerRetroBall.Utilities;
using UnityEngine;

namespace CallerRetroBall.City
{
    /// <summary>
    /// 16 Oct 2026 (BALLER CITY): the drawn district. The ground is one baked sprite; buildings and props are sprites
    /// sorted by where they touch the ground, so you walk behind a tree or a building and in front of it. Day and
    /// night swap to lit art (windows, lamps), add a darkness layer and warm glows around the lamps. Everything is
    /// generated once when the scene opens (no per-frame work beyond the light level).
    /// </summary>
    public sealed class CityWorld : MonoBehaviour
    {
        public const float Ppu = CityArtGenerator.Tile;

        /// <summary>Depth: lower on the map is closer to the camera, so drawn later.</summary>
        public static int Order(float groundY) => 20000 - Mathf.RoundToInt(groundY * 100f);

        private readonly List<Object> _owned = new List<Object>();
        private readonly List<(SpriteRenderer r, Sprite day, Sprite night)> _swaps = new List<(SpriteRenderer, Sprite, Sprite)>();
        private readonly List<SpriteRenderer> _glows = new List<SpriteRenderer>();
        private readonly Dictionary<string, (Sprite day, Sprite night)> _propCache = new Dictionary<string, (Sprite, Sprite)>();
        private SpriteRenderer _dark;
        private bool _lit;
        private float _darkness = -1f;

        public CityMap Map { get; private set; }
        public Sprite Shadow { get; private set; }

        public static CityWorld Build(CityMap map, ContentCatalog catalog, uint seed)
        {
            var go = new GameObject("CityWorld");
            var w = go.AddComponent<CityWorld>();
            w.Map = map;
            w.BuildGround(catalog, seed);
            foreach (var b in map.Buildings) w.AddBuilding(b);
            foreach (var p in map.Props) w.AddProp(p);
            w.BuildLights();
            w.Shadow = w.Own(Sprite.Create(w.Own(TextureFactory.ToTexture(ShadowCanvas(), "city.shadow")), new Rect(0, 0, 14, 6), new Vector2(0.5f, 0.5f), Ppu * 2f));
            return w;
        }

        private T Own<T>(T o) where T : Object
        {
            _owned.Add(o);
            return o;
        }

        private void BuildGround(ContentCatalog catalog, uint seed)
        {
            var canvas = CityArtGenerator.Ground(Map, seed, id =>
            {
                var c = catalog?.Court(id);
                return c == null ? ((RgbColor, RgbColor, RgbColor)?)null : (c.floor, c.lines, c.paint);
            });
            var tex = Own(TextureFactory.ToTexture(canvas, "city.ground." + Map.DistrictId));
            var r = NewRenderer("Ground", Own(Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero, Ppu, 0, SpriteMeshType.FullRect)));
            r.sortingOrder = -32000;
        }

        private void AddBuilding(CityBuilding b)
        {
            var day = MakeSprite(CityArtGenerator.Building(b, false), "city.b." + b.Id, Vector2.zero);
            var night = MakeSprite(CityArtGenerator.Building(b, true), "city.b." + b.Id + ".lit", Vector2.zero);
            var r = NewRenderer("Building " + b.Id, day);
            r.transform.localPosition = new Vector3(b.X, b.Y, 0f);
            r.sortingOrder = Order(b.Y);
            _swaps.Add((r, day, night));
        }

        private void AddProp(CityProp p)
        {
            string key = p.Kind + "." + p.Variant + "." + p.W + "x" + p.H;
            if (!_propCache.TryGetValue(key, out var pair))
            {
                var dayCanvas = CityArtGenerator.Prop(p, false, out int px, out int py);
                var nightCanvas = CityArtGenerator.Prop(p, true, out _, out _);
                var pivot = new Vector2(dayCanvas.Width == 0 ? 0f : px / (float)dayCanvas.Width, dayCanvas.Height == 0 ? 0f : py / (float)dayCanvas.Height);
                pair = (MakeSprite(dayCanvas, "city.p." + key, pivot), MakeSprite(nightCanvas, "city.p." + key + ".lit", pivot));
                _propCache[key] = pair;
            }
            var r = NewRenderer("Prop " + p.Kind, pair.day);
            r.transform.localPosition = new Vector3(p.X, p.Y, 0f);
            // Flat things (crosswalk cones aside) sit on the ground; everything else sorts by its base.
            r.sortingOrder = Order(p.Y);
            _swaps.Add((r, pair.day, pair.night));
        }

        private Sprite MakeSprite(PixelCanvas c, string name, Vector2 pivot)
        {
            var tex = Own(TextureFactory.ToTexture(c, name));
            return Own(Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, Ppu, 0, SpriteMeshType.FullRect));
        }

        private SpriteRenderer NewRenderer(string name, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            return r;
        }

        private void BuildLights()
        {
            // Darkness: one tinted quad over the whole district, under the lamp glows and the HUD.
            var px = new PixelCanvas(1, 1);
            px.Pixels[0] = new RgbColor(255, 255, 255, 255);
            var white = MakeSprite(px, "city.white", Vector2.zero);
            _dark = NewRenderer("Night", white);
            _dark.transform.localScale = new Vector3((Map.Width + 2) * Ppu, (Map.Height + 2) * Ppu, 1f);
            _dark.transform.localPosition = new Vector3(-1f, -1f, 0f);
            _dark.color = new Color(0.05f, 0.06f, 0.18f, 0f);
            _dark.sortingOrder = 30000;

            var glow = MakeSprite(CityArtGenerator.Glow(40), "city.glow", new Vector2(0.5f, 0.5f));
            foreach (var p in Map.Props)
            {
                // Streetlights, paper lanterns, arcade screens, Christmas trees and jack-o'-lanterns all light up.
                float size, height;
                Color tint;
                switch (p.Kind)
                {
                    case CityPropKind.Lamp: size = 1f; height = 1.2f; tint = new Color(1f, 0.85f, 0.5f, 0f); break;
                    case CityPropKind.LanternPost: size = 0.7f; height = 1.0f; tint = new Color(1f, 0.55f, 0.3f, 0f); break;
                    case CityPropKind.Arcade: size = 0.45f; height = 0.7f; tint = new Color(0.5f, 0.9f, 1f, 0f); break;
                    case CityPropKind.PineLit: size = 0.8f; height = 1.2f; tint = new Color(1f, 0.9f, 0.6f, 0f); break;
                    case CityPropKind.Pumpkin: size = 0.35f; height = 0.4f; tint = new Color(1f, 0.6f, 0.2f, 0f); break;
                    default: continue;
                }
                var g = NewRenderer("Glow", glow);
                g.transform.localPosition = new Vector3(p.X + 0.5f, p.Y + height, 0f);
                g.transform.localScale = new Vector3(size, size, 1f);
                g.color = tint;
                g.sortingOrder = 30001;
                _glows.Add(g);
            }
            // Lit doorways and the gym's ceiling lights glow a little too.
            foreach (var b in Map.Buildings)
            {
                if (b.DoorX < 0) continue;
                var g = NewRenderer("DoorGlow", glow);
                g.transform.localPosition = new Vector3(b.DoorX + 0.5f, b.Y, 0f);
                g.transform.localScale = new Vector3(0.6f, 0.6f, 1f);
                g.color = new Color(1f, 0.8f, 0.45f, 0f);
                g.sortingOrder = 30001;
                _glows.Add(g);
            }
        }

        /// <summary>Sets the light for the hour (0..24). Only changes renderers when the level actually changes.</summary>
        public void SetHour(float hour)
        {
            float dark = Map.Interior ? 0f : CityClock.Darkness(hour);
            bool lit = !Map.Interior && CityClock.LightsOn(hour);
            if (lit != _lit || _darkness < 0f)
            {
                _lit = lit;
                foreach (var s in _swaps) s.r.sprite = lit ? s.night : s.day;
            }
            if (Mathf.Abs(dark - _darkness) < 0.005f) return;
            _darkness = dark;
            _dark.color = new Color(0.05f, 0.06f, 0.18f, dark * 0.75f);
            float glowA = lit ? Mathf.Clamp01(dark) * 0.45f : 0f;
            foreach (var g in _glows)
            {
                var c = g.color;
                c.a = glowA;
                g.color = c;
            }
        }

        private static PixelCanvas ShadowCanvas()
        {
            var c = new PixelCanvas(14, 6);
            for (int y = 0; y < 6; y++)
                for (int x = 0; x < 14; x++)
                {
                    float dx = (x + 0.5f - 7f) / 7f, dy = (y + 0.5f - 3f) / 3f;
                    if (dx * dx + dy * dy <= 1f) c.Pixels[y * 14 + x] = new RgbColor(255, 255, 255, 255);
                }
            return c;
        }

        private void OnDestroy()
        {
            foreach (var o in _owned) if (o != null) Destroy(o);
            _owned.Clear();
        }
    }
}
