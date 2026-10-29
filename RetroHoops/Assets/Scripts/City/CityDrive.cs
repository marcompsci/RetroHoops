using System;
using System.Collections.Generic;
using CallerRetroBall.Controls;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using CallerRetroBall.UI;
using CallerRetroBall.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CallerRetroBall.City
{
    /// <summary>
    /// 28 Oct 2026 (Omari, BALLER WORLD W6): THE DRIVE, drawn. A top-down highway scrolling up the portrait screen:
    /// your gold ride, traffic to thread, the roadside turning from where you left to where you're going (the red
    /// bridge into the Bay), WELCOME TO at the end. Hold ◄ / ► to change lanes and BOOST to fly; keys and pads work too.
    /// The rules (speed, traffic, bumps, near misses, the under-30-seconds promise) are <see cref="HighwayDrive"/>.
    /// </summary>
    public sealed class CityDrive : MonoBehaviour
    {
        // View units: 1 lane = 1.6 units; road and cars use pixels-per-unit to match.
        private const float LaneUnits = 1.6f;
        private const float RoadPpu = DriveArt.Lane / LaneUnits;
        private const float CarPpu = 9f;
        private const float SideStep = 3f;

        private HighwayDrive _drive;
        private Camera _camera;
        private Action<HighwayDrive> _done;
        private bool _reduceMotion, _finished;
        private float _finishedAt, _shake;
        private readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();
        private readonly List<SpriteRenderer> _road = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _cars = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _side = new List<SpriteRenderer>();
        private SpriteRenderer _me, _groundL, _groundR, _welcome;
        private Sprite _roadSprite, _bridgeSprite;
        private readonly Dictionary<int, Sprite> _sideSprites = new Dictionary<int, Sprite>();
        private TextMeshProUGUI _route, _time, _speed, _misses, _banner;
        private RectTransform _progress, _boostFill;
        private TouchActionButton _left, _right, _boost;
        private Canvas _canvas;
        private int _lastMisses, _lastBumps;
        private float _bannerUntil;
        private bool _bannerShown;
        // 28 Oct 2026: the cities along the road (buildings, cross streets, exit signs, state lines, mile markers).
        private const float BuildingPpu = 16f / DriveScenery.TileUnits;
        private List<DriveFeature> _scenery;
        private readonly Dictionary<string, Sprite> _sceneSprites = new Dictionary<string, Sprite>();
        private int _nextLine;
        // I25 / I27.
        private bool _easy;
        private float _routeBest;
        // I39: the drive at night (dimmer country, headlights) and into the fog (a veil over the last stretch).
        private bool _night, _fogAhead;
        private SpriteRenderer _headlights, _fogVeil;

        public static CityDrive Begin(Camera camera, string from, string to, SettingsData settings, Action<HighwayDrive> done, bool easyDrive = false, float routeBest = 0f)
        {
            var go = new GameObject("CityDrive");
            var d = go.AddComponent<CityDrive>();
            d._camera = camera;
            d._done = done;
            d._reduceMotion = settings != null && settings.reduceMotion;
            d._drive = new HighwayDrive(from, to, (uint)Environment.TickCount | 1u);
            d._easy = easyDrive;
            d._routeBest = routeBest;
            d.Build(settings);
            return d;
        }

        private Sprite MakeSprite(PixelCanvas c, string name, float ppu, Vector2 pivot)
        {
            var tex = TextureFactory.ToTexture(c, name);
            _owned.Add(tex);
            var s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, ppu, 0, SpriteMeshType.FullRect);
            _owned.Add(s);
            return s;
        }

        private SpriteRenderer Renderer(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            return r;
        }

        private void Build(SettingsData settings)
        {
            _roadSprite = MakeSprite(DriveArt.Road(false), "drive.road", RoadPpu, new Vector2(0.5f, 0f));
            _bridgeSprite = MakeSprite(DriveArt.Road(true), "drive.bridge", RoadPpu, new Vector2(0.5f, 0f));
            var white = new PixelCanvas(1, 1);
            white.Pixels[0] = new RgbColor(255, 255, 255);
            var px = MakeSprite(white, "drive.white", 1f, new Vector2(0.5f, 0.5f));
            _groundL = Renderer("GroundL", px, -5000);
            _groundR = Renderer("GroundR", px, -5000);
            for (int i = 0; i < 14; i++) _road.Add(Renderer("Road", _roadSprite, -4500));
            for (int i = 0; i < 24; i++) _side.Add(Renderer("Roadside", null, -40));
            var carSprites = new Sprite[8];
            for (int v = 0; v < carSprites.Length; v++) carSprites[v] = MakeSprite(DriveArt.Car(v), "drive.car" + v, CarPpu, new Vector2(0.5f, 0.5f));
            foreach (var c in _drive.Traffic) _cars.Add(Renderer("Traffic", carSprites[c.Variant % carSprites.Length], 10));
            _me = Renderer("You", MakeSprite(DriveArt.Car(-1), "drive.me", CarPpu, new Vector2(0.5f, 0.5f)), 20);
            _welcome = Renderer("Welcome", MakeSprite(DriveArt.Welcome(WorldRegions.Find(_drive.To)), "drive.welcome", RoadPpu, new Vector2(0.5f, 0f)), 30);
            _welcome.transform.localPosition = new Vector3(0f, HighwayDrive.Length - 6f, 0f);
            BuildScenery(px);
            int hourNow = DateTime.Now.Hour;
            _night = hourNow >= 19 || hourNow < 6;
            var dest = WorldRegions.Find(_drive.To);
            _fogAhead = dest != null && CityWeather.For(CityNpcScheduler.DayNumber(DateTime.Now), DateTime.Now.Month, dest.Main, false,
                                                         App.Career?.city?.weatherOff ?? false) == CityWeatherKind.Fog;
            if (_night)
            {
                _headlights = Renderer("Headlights", px, 15);
                _headlights.color = new Color(1f, 0.95f, 0.7f, 0.22f);
                _headlights.transform.localScale = new Vector3(1.2f, 4.5f, 1f);
                foreach (var r in _road) r.color = new Color(0.62f, 0.62f, 0.75f, 1f);
                foreach (var r in _side) r.color = new Color(0.5f, 0.5f, 0.65f, 1f);
            }
            if (_fogAhead)
            {
                _fogVeil = Renderer("Fog", px, 28);
                _fogVeil.color = new Color(0.85f, 0.88f, 0.92f, 0f);
                _fogVeil.transform.localScale = new Vector3(30f, 60f, 1f);
            }
            BuildHud(settings);
        }

        /// <summary>The cities and signs along the road (all placed once; Unity skips what's off screen).</summary>
        private void BuildScenery(Sprite px)
        {
            _scenery = DriveScenery.Layout(_drive.From, _drive.To, (uint)Environment.TickCount | 1u);
            int hour = DateTime.Now.Hour;
            bool night = hour >= 19 || hour < 6;
            float roadHalf = DriveArt.RoadWidth / RoadPpu * 0.5f;
            foreach (var f in _scenery)
            {
                switch (f.Kind)
                {
                    case DriveFeatureKind.Building:
                    {
                        string key = "b." + f.Style + "." + f.W + "." + f.H + "." + f.Seed + (night ? ".n" : "");
                        if (!_sceneSprites.TryGetValue(key, out var sprite))
                        {
                            var b = new CityBuilding { Id = "drive", W = f.W, H = f.H, Style = f.Style, DoorX = f.W / 2, Seed = 11 + f.Seed * 37 };
                            _sceneSprites[key] = sprite = MakeSprite(CityArtGenerator.Building(b, night), "drive." + key, BuildingPpu, Vector2.zero);
                        }
                        var r = Renderer("Building", sprite, -1000 - Mathf.RoundToInt(f.Y * 4f));
                        float w = f.W * DriveScenery.TileUnits;
                        r.transform.localPosition = new Vector3(f.Side < 0 ? -(roadHalf + 0.25f) - w : roadHalf + 0.25f, f.Y, 0f);
                        break;
                    }
                    case DriveFeatureKind.CrossStreet:
                    {
                        var street = Renderer("CrossStreet", px, -4400);
                        street.color = new Color32(0x3B, 0x3B, 0x46, 255);
                        street.transform.localPosition = new Vector3(f.Side * (roadHalf + 4.25f), f.Y + 0.9f, 0f);
                        street.transform.localScale = new Vector3(8f, 1.8f, 1f);
                        var line = Renderer("Crosswalk", px, -4390);
                        line.color = new Color32(0xF4, 0xF1, 0xDE, 255);
                        line.transform.localPosition = new Vector3(f.Side * (roadHalf + 0.6f), f.Y + 0.9f, 0f);
                        line.transform.localScale = new Vector3(0.5f, 1.6f, 1f);
                        break;
                    }
                    case DriveFeatureKind.Gantry:
                    {
                        var g = Renderer("Gantry", MakeSprite(DriveArt.Gantry(f.Text, f.Sub), "drive.gantry", RoadPpu, new Vector2(0.5f, 0f)), 25);
                        g.transform.localPosition = new Vector3(0f, f.Y, 0f);
                        break;
                    }
                    default:
                    {
                        var sign = Renderer("Sign", MakeSprite(DriveArt.Sign(f.Kind, f.Text, f.Sub), "drive.sign", RoadPpu, new Vector2(0.5f, 0f)), -900);
                        sign.transform.localPosition = new Vector3(f.Side * (roadHalf + 1.1f), f.Y, 0f);
                        break;
                    }
                }
            }
        }

        private Sprite SideSprite(WorldBiome b, int variant)
        {
            int key = (int)b * 16 + variant;
            if (!_sideSprites.TryGetValue(key, out var s))
                _sideSprites[key] = s = MakeSprite(DriveArt.Roadside(b, variant), "drive.side." + key, 10f, new Vector2(0.5f, 0.5f));
            return s;
        }

        private void BuildHud(SettingsData s)
        {
            float alpha = ControlFeel.OpacityFor(s);
            bool big = s != null && s.largeButtons;
            _canvas = UiKit.CreateScreenCanvas("DriveHud", 20);
            _canvas.transform.SetParent(transform, false);
            var safe = UiKit.SafeArea(_canvas.transform);
            var bar = UiKit.Panel(safe, new Color(0.04f, 0.04f, 0.09f, 0.8f), name: "Top");
            bar.rectTransform.anchorMin = new Vector2(0f, 1f);
            bar.rectTransform.anchorMax = new Vector2(1f, 1f);
            bar.rectTransform.pivot = new Vector2(0.5f, 1f);
            bar.rectTransform.sizeDelta = new Vector2(0f, 200f);
            var from = WorldRegions.Find(_drive.From);
            var to = WorldRegions.Find(_drive.To);
            _route = UiKit.Label(bar.rectTransform, Loc.T(from?.Name ?? "") + "  ►  " + Loc.T(to?.Name ?? ""), 40f, Theme.Gold, TextAlignmentOptions.Center, true);
            _route.rectTransform.anchorMin = new Vector2(0f, 0.62f);
            _route.rectTransform.anchorMax = new Vector2(1f, 1f);
            _route.rectTransform.offsetMin = _route.rectTransform.offsetMax = Vector2.zero;
            var track = UiKit.Panel(bar.rectTransform, new Color32(0x3A, 0x3A, 0x42, 255), name: "Track");
            track.rectTransform.anchorMin = new Vector2(0.06f, 0.44f);
            track.rectTransform.anchorMax = new Vector2(0.94f, 0.56f);
            track.rectTransform.offsetMin = track.rectTransform.offsetMax = Vector2.zero;
            var fill = UiKit.Panel(track.rectTransform, Theme.Gold, name: "Done");
            _progress = fill.rectTransform;
            _progress.anchorMin = Vector2.zero;
            _progress.anchorMax = new Vector2(0f, 1f);
            _progress.offsetMin = _progress.offsetMax = Vector2.zero;
            _time = Stat(bar.rectTransform, 0f, "0.0 s");
            _speed = Stat(bar.rectTransform, 1f / 3f, "0 MPH");
            _misses = Stat(bar.rectTransform, 2f / 3f, Loc.T("NEAR MISSES") + " 0");

            _banner = UiKit.Label(safe, "", 60f, Theme.Gold, TextAlignmentOptions.Center, true, "Banner");
            UiKit.Band(_banner.rectTransform, 0.62f, 0.7f, 40f);
            UiKit.ApplyTextShadow(_banner);

            float size = big ? 300f : 240f;
            _left = Hold(safe, "◄", new Vector2(0f, 0f), new Vector2(size * 0.62f, size * 0.62f), size, alpha);
            _right = Hold(safe, "►", new Vector2(0f, 0f), new Vector2(size * 1.72f, size * 0.62f), size, alpha);
            _boost = Hold(safe, "BOOST", new Vector2(1f, 0f), new Vector2(-size * 0.7f, size * 0.72f), size * 1.1f, alpha);
            var meter = UiKit.Panel(safe, new Color32(0x3A, 0x3A, 0x42, 255), name: "BoostMeter");
            meter.rectTransform.anchorMin = meter.rectTransform.anchorMax = new Vector2(1f, 0f);
            meter.rectTransform.sizeDelta = new Vector2(size, 22f);
            meter.rectTransform.anchoredPosition = new Vector2(-size * 0.7f, size * 1.42f);
            var mf = UiKit.Panel(meter.rectTransform, Theme.Cyan, name: "Fill");
            _boostFill = mf.rectTransform;
            _boostFill.anchorMin = Vector2.zero;
            _boostFill.anchorMax = Vector2.one;
            _boostFill.offsetMin = _boostFill.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI Stat(RectTransform parent, float x, string text)
        {
            var l = UiKit.Label(parent, text, 32f, Theme.Cream, TextAlignmentOptions.Center, true);
            l.rectTransform.anchorMin = new Vector2(x, 0.04f);
            l.rectTransform.anchorMax = new Vector2(x + 1f / 3f, 0.38f);
            l.rectTransform.offsetMin = l.rectTransform.offsetMax = Vector2.zero;
            return l;
        }

        private static TouchActionButton Hold(RectTransform parent, string text, Vector2 anchor, Vector2 pos, float size, float alpha)
        {
            var color = text == "BOOST" ? (Color)new Color32(0x3B, 0xD5, 0xFF, 255) : (Color)new Color32(0xFF, 0xD1, 0x66, 255);
            var image = UiKit.Panel(parent, new Color(1f, 1f, 1f, alpha), TouchControls.Face(color, false), false, "Btn " + text);
            image.raycastTarget = true;
            image.preserveAspect = true;
            var rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = pos;
            var label = UiKit.Label(rt, text, 40f, Theme.Cream, TextAlignmentOptions.Center, true);
            UiKit.Stretch(label.rectTransform, size * 0.12f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 18f;
            label.fontSizeMax = 56f;
            var hint = UiKit.Label(rt, "", 20f, Theme.Cream, TextAlignmentOptions.Center, true, "Hint");
            var b = image.gameObject.AddComponent<TouchActionButton>();
            b.Init(image, label, hint);
            b.SetSprites(TouchControls.Face(color, false), TouchControls.Face(color, true));
            return b;
        }

        // ------------------------------------------------------------------ frame

        private void Update()
        {
            if (_drive == null) return;
            float dt = Time.deltaTime;
            if (_finished)
            {
                if (Time.unscaledTime > _finishedAt) { var done = _done; _done = null; done?.Invoke(_drive); }
                return;
            }
            float steer = (_left.IsHeld ? -1f : 0f) + (_right.IsHeld ? 1f : 0f) + KeySteer();
            // I27 EASY DRIVE: when you're not steering, the ride steers round the traffic.
            if (_easy && Mathf.Abs(steer) < 0.1f) steer = _drive.AutoSteer();
            _drive.Step(dt, Mathf.Clamp(steer, -1f, 1f), _boost.IsHeld || KeyBoost());
            if (_drive.Bumps > _lastBumps)
            {
                _lastBumps = _drive.Bumps;
                _shake = _reduceMotion ? 0f : 0.35f;
                Audio.AudioManager.Play(SfxId.Backboard, 0.8f, 0.7f);
                Haptics.Medium();
                Banner(Loc.T("BUMP!"), 0.7f);
            }
            if (_drive.NearMisses > _lastMisses)
            {
                _lastMisses = _drive.NearMisses;
                Audio.AudioManager.Play(SfxId.Swish, 0.6f, 1.3f);
                Haptics.Light();
                Banner(Loc.T("NEAR MISS!"), 0.6f);
            }
            // Crossing a state line.
            while (_scenery != null && _nextLine < _scenery.Count && _scenery[_nextLine].Y <= _drive.Distance)
            {
                var f = _scenery[_nextLine++];
                if (f.Kind == DriveFeatureKind.StateLine) Banner(Loc.T("WELCOME TO") + "\n" + f.Sub, 1.6f);
                else if (f.Kind == DriveFeatureKind.Leaving) Banner(Loc.T("NOW LEAVING") + "\n" + Loc.T(f.Sub), 1.4f);
            }
            if (_drive.Done)
            {
                _finished = true;
                _finishedAt = Time.unscaledTime + 1.6f;
                Audio.AudioManager.Play(SfxId.Fanfare, 0.7f);
                Haptics.Success();
                bool record = _routeBest <= 0f || _drive.Time < _routeBest;
                Banner(Loc.T("ARRIVED") + "  " + _drive.Time.ToString("0.0") + " s" + (record ? "\n" + Loc.T("NEW ROUTE RECORD!") : _drive.Bumps == 0 ? "\n" + Loc.T("CLEAN DRIVE!") : ""), 3f);
            }
            if (_bannerShown && Time.unscaledTime > _bannerUntil)
            {
                _bannerShown = false;
                UiTween.Run(_banner.gameObject, UiMotion.ToastOut, UiMotion.Out, null, new UiTween.State { Alpha = 0f, Offset = new Vector2(0f, 10f), Scale = 1f },
                            () => { if (!_bannerShown) _banner.text = ""; });
            }
            // Fixed-width digits so the clock and the speed don't shake as they count.
            _time.text = UiMotion.Tabular(_drive.Time.ToString("0.0")) + " s" + (_routeBest > 0f ? "  ·  " + Loc.T("BEST") + " " + _routeBest.ToString("0.0") : "") + (_easy ? "  ·  " + Loc.T("EASY") : "");
 _speed.text = UiMotion.Tabular(_drive.Mph.ToString().PadLeft(3)) + " MPH" + (_drive.Boosting ? "  " + Loc.T("BOOST") : "");
            _misses.text = Loc.T("NEAR MISSES") + " " + _drive.NearMisses;
            _progress.anchorMax = new Vector2(_drive.Progress, 1f);
            _boostFill.anchorMax = new Vector2(_drive.Boost, 1f);
        }

        private void Banner(string text, float seconds)
        {
            // 28 Oct 2026 (Emil Kowalski audit): banners rise and fade in, then drift out (they snapped on and off).
            if (!_bannerShown || _banner.text != text) UiMotion.Enter(_banner, UiMotion.Toast, new Vector2(0f, -14f), 0.96f);
            _bannerShown = true;
            _banner.text = text;
            _bannerUntil = Time.unscaledTime + seconds;
        }

        private void LateUpdate()
        {
            if (_drive == null || _camera == null) return;
            float aspect = Mathf.Max(0.3f, _camera.aspect);
            float halfW = 5.2f, halfH = halfW / aspect;
            _camera.orthographicSize = halfH;
            float camY = _drive.Distance + halfH * 0.55f;
            float shakeX = 0f;
            if (_shake > 0f) { _shake -= Time.deltaTime; shakeX = Mathf.Sin(Time.unscaledTime * 60f) * 0.12f; }
            _camera.transform.position = new Vector3(shakeX, camY, -10f);
            _camera.backgroundColor = new Color32(0x14, 0x14, 0x1F, 255);

            // The ground on each side, tinted for where you are on the trip.
            var biome = HighwayDrive.BiomeAt(_drive.From, _drive.To, _drive.Progress);
            var g = DriveArt.Ground(biome);
            var ground = new Color32(g.r, g.g, g.b, 255);
            // In the cities the road runs between sidewalks.
            if (DriveScenery.CityAt(_drive.From, _drive.To, Mathf.Clamp01(camY / HighwayDrive.Length)) != null) ground = new Color32(0x8D, 0x8D, 0x99, 255);
            if (_night) ground = Color.Lerp(ground, new Color32(0x0B, 0x0B, 0x1A, 255), 0.55f); // I39
            if (_headlights != null) _headlights.transform.localPosition = new Vector3((_drive.X - 1f) * LaneUnits, _drive.Distance + 3.2f, 0f);
            if (_fogVeil != null)
            {
                float t = Mathf.Clamp01((_drive.Progress - DriveScenery.CityIn + 0.08f) / 0.12f);
                _fogVeil.color = new Color(0.85f, 0.88f, 0.92f, 0.38f * t);
                _fogVeil.transform.localPosition = new Vector3(0f, camY, 0f);
            }
            float roadHalf = DriveArt.RoadWidth / RoadPpu * 0.5f;
            foreach (var (r, side) in new[] { (_groundL, -1f), (_groundR, 1f) })
            {
                r.color = Color.Lerp(r.color, ground, _reduceMotion ? 1f : Mathf.Clamp01(Time.deltaTime * 3f));
                r.transform.localPosition = new Vector3(side * (roadHalf + 6f), camY, 0f);
                r.transform.localScale = new Vector3(12f, halfH * 2f + 4f, 1f);
            }

            // The road: tiles recycled up the screen (the bridge's deck at the end of a drive to the Bay).
            float tileH = DriveArt.RoadTile / RoadPpu;
            float start = Mathf.Floor((camY - halfH - tileH) / tileH) * tileH;
            for (int i = 0; i < _road.Count; i++)
            {
                float y = start + i * tileH;
                _road[i].transform.localPosition = new Vector3(0f, y, 0f);
                _road[i].sprite = HighwayDrive.OnBridge(_drive.To, y / HighwayDrive.Length) ? _bridgeSprite : _roadSprite;
            }

            // The roadside: things every few units on both sides, from the country at that point of the trip.
            float sideStart = Mathf.Floor((camY - halfH - SideStep) / SideStep) * SideStep;
            for (int i = 0; i < _side.Count; i++)
            {
                int slot = Mathf.RoundToInt(sideStart / SideStep) + i / 2;
                float y = slot * SideStep;
                int sideSign = i % 2 == 0 ? -1 : 1;
                uint h = StableHash.Of(slot + ":" + sideSign);
                var b = HighwayDrive.BiomeAt(_drive.From, _drive.To, Mathf.Clamp01(y / HighwayDrive.Length));
                var r = _side[i];
                r.enabled = DriveScenery.CityAt(_drive.From, _drive.To, Mathf.Clamp01(y / HighwayDrive.Length)) == null;
                r.sprite = SideSprite(b, (int)(h % 4));
                r.transform.localPosition = new Vector3(sideSign * (roadHalf + 1.4f + (h / 4 % 3) * 0.8f), y, 0f);
            }

            // Cars.
            for (int i = 0; i < _cars.Count; i++)
            {
                var c = _drive.Traffic[i];
                bool near = Mathf.Abs(c.Y - camY) < halfH + 3f;
                _cars[i].enabled = near;
                if (near) _cars[i].transform.localPosition = new Vector3((c.Lane - 1) * LaneUnits, c.Y, 0f);
            }
            float tilt = Mathf.Clamp((_left.IsHeld ? 1f : 0f) - (_right.IsHeld ? 1f : 0f) - KeySteer(), -1f, 1f) * (_reduceMotion ? 0f : 8f);
            _me.transform.localPosition = new Vector3((_drive.X - 1f) * LaneUnits, _drive.Distance, 0f);
            _me.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
            _me.color = _drive.Stun > 0f && Mathf.Repeat(Time.unscaledTime * 12f, 1f) < 0.5f ? new Color(1f, 1f, 1f, 0.5f) : Color.white;
        }

        private static float KeySteer()
        {
#if ENABLE_INPUT_SYSTEM
            float x = 0f;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                float v = pad.leftStick.ReadValue().x;
                if (Mathf.Abs(v) > 0.2f) x += v;
            }
            return x;
#else
            return 0f;
#endif
        }

        private static bool KeyBoost()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            return (kb != null && (kb.spaceKey.isPressed || kb.leftShiftKey.isPressed)) || (pad != null && (pad.buttonSouth.isPressed || pad.rightTrigger.isPressed));
#else
            return false;
#endif
        }

        private void OnDestroy()
        {
            foreach (var o in _owned) if (o != null) Destroy(o);
        }
    }
}
