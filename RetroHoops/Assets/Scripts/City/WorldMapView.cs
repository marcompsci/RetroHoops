using System;
using System.Collections.Generic;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using CallerRetroBall.UI;
using CallerRetroBall.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CallerRetroBall.City
{
    /// <summary>
    /// 28 Oct 2026 (Omari): the zoomable WORLD MAP. Opens on the region you're in (every district drawn from above in a
    /// whole city), pinch or scroll to zoom out to the full country, drag to move around, tap a region or district for
    /// DRIVE THERE or SPAWN THERE. The layout and the rules are in Logic (WorldAtlas); the art in WorldMapArt.
    /// </summary>
    public sealed class WorldMapView : MonoBehaviour
    {
        /// <summary>What the map shows and what tapping does.</summary>
        public sealed class Model
        {
            public string Here;                 // the district you're in
            public Vec2 HereAt;                 // where you are in it (tiles)
            public string Focus;                // region to open on (null = the whole world)
            public Func<string, string, MapChoice> Describe;
            public Func<string, bool> IsOpen;
            public Action<string, string> Drive, Spawn;
            public Action Closed;
            public string Footer;
        }

        // Textures are built once and kept for the session (the map opens instantly the second time).
        private static Texture2D _backdrop;
        private static readonly Dictionary<string, Texture2D> Plates = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Texture2D> Districts = new Dictionary<string, Texture2D>();
        private static Sprite _youSprite;

        private Model _model;
        private RectTransform _viewport, _content, _overlay, _sheet;
        private float _zoom = 1f, _minZoom = 0.2f, _maxZoom = 8f;
        private Vector2 _pan;
        private readonly Dictionary<int, Vector2> _pointers = new Dictionary<int, Vector2>();
        private bool _multi;
        private Vector2 _tapStart;
        private float _tapTime;
        private float _moved;

        private readonly List<(string id, RawImage image)> _pendingDistricts = new List<(string, RawImage)>();
        private readonly List<(string id, RawImage image)> _pendingPlates = new List<(string, RawImage)>();
        private readonly Dictionary<string, Image> _frames = new Dictionary<string, Image>();
        private readonly List<(TextMeshProUGUI label, Vector2 at, bool district)> _labels = new List<(TextMeshProUGUI, Vector2, bool)>();
        private Image _you;
        private Vector2 _youAt;
        private string _selected;
        private TextMeshProUGUI _hintLabel;

        /// <summary>Builds the map under <paramref name="parent"/> (a full-screen card on the HUD canvas).</summary>
        public static WorldMapView Create(RectTransform parent, Model model)
        {
            var go = UiKit.NewRect("WorldMapView", parent).gameObject;
            var view = go.AddComponent<WorldMapView>();
            view._model = model;
            view.Build((RectTransform)go.transform);
            return view;
        }

        // ------------------------------------------------------------------ building

        private void Build(RectTransform root)
        {
            UiKit.Stretch(root);
            var sea = UiKit.Panel(root, new Color32(0x1E, 0x5A, 0xA8, 255), name: "Sea");
            UiKit.Stretch(sea.rectTransform);

            // The map itself: a viewport that takes the gestures, the content (map pixels) and an overlay for labels.
            _viewport = UiKit.NewRect("Viewport", root);
            UiKit.Stretch(_viewport);
            _viewport.pivot = Vector2.zero;
            var catcher = _viewport.gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            catcher.raycastTarget = true;
            _viewport.gameObject.AddComponent<RectMask2D>();
            // Gestures arrive at this component through the viewport's raycast target.
            var relay = _viewport.gameObject.AddComponent<WorldMapGestures>();
            relay.Target = this;

            _content = UiKit.NewRect("Content", _viewport);
            _content.anchorMin = _content.anchorMax = Vector2.zero;
            _content.pivot = Vector2.zero;
            _content.sizeDelta = new Vector2(WorldAtlas.Width, WorldAtlas.Height);

            if (_backdrop == null) _backdrop = TextureFactory.ToTexture(WorldMapArt.Backdrop(), "map.backdrop");
            var back = UiKit.Picture(_content, _backdrop, "Backdrop");
            Place(back.rectTransform, new MapRect(0f, 0f, WorldAtlas.Width, WorldAtlas.Height));

            foreach (var r in WorldRegions.All)
            {
                bool open = _model.IsOpen == null || _model.IsOpen(r.Id);
                var plateRect = WorldAtlas.Plate(r.Id);
                var shadow = UiKit.Panel(_content, new Color(0f, 0f, 0f, 0.35f), name: "Shadow " + r.Id);
                Place(shadow.rectTransform, new MapRect(plateRect.X + 10f, plateRect.Y - 10f, plateRect.W, plateRect.H));
                var rim = UiKit.Panel(_content, Hex(r.Primary), name: "Rim " + r.Id);
                Place(rim.rectTransform, new MapRect(plateRect.X - 6f, plateRect.Y - 6f, plateRect.W + 12f, plateRect.H + 12f));
                var plate = UiKit.Picture(_content, Plates.TryGetValue(r.Id, out var pt) && pt != null ? pt : null, "Plate " + r.Id);
                Place(plate.rectTransform, plateRect);
                plate.color = open ? Color.white : new Color(0.45f, 0.45f, 0.5f, 1f);
                if (plate.texture == null) { plate.color = new Color(0.2f, 0.2f, 0.26f, 1f); _pendingPlates.Add((r.Id, plate)); }

                foreach (var s in CityAtlas.SpotsFor(r.Id))
                {
                    var rect = WorldAtlas.DistrictRect(s.id);
                    var frame = UiKit.Panel(_content, Hex(r.Primary), name: "Frame " + s.id);
                    Place(frame.rectTransform, new MapRect(rect.X - 4f, rect.Y - 4f, rect.W + 8f, rect.H + 8f));
                    _frames[s.id] = frame;
                    var img = UiKit.Picture(_content, Districts.TryGetValue(s.id, out var dt) && dt != null ? dt : null, "District " + s.id);
                    Place(img.rectTransform, rect);
                    img.color = open ? Color.white : new Color(0.45f, 0.45f, 0.5f, 1f);
                    if (img.texture == null) { img.color = new Color(0.3f, 0.3f, 0.36f, 1f); _pendingDistricts.Add((s.id, img)); }
                }
            }

            // The overlay: labels and YOU stay the same size at every zoom.
            _overlay = UiKit.NewRect("Overlay", _viewport);
            UiKit.Stretch(_overlay);
            _overlay.pivot = Vector2.zero;
            foreach (var r in WorldRegions.All)
            {
                var p = WorldAtlas.Plate(r.Id);
                bool open = _model.IsOpen == null || _model.IsOpen(r.Id);
                string text = Loc.T(r.Name) + (open ? "" : "  " + Loc.T("LOCKED"));
                AddLabel(text, new Vector2(p.CenterX, p.Y + p.H + 34f), 34f, open ? Hex(r.Primary) : Theme.Cream, false);
                foreach (var s in CityAtlas.SpotsFor(r.Id))
                {
                    var d = WorldAtlas.DistrictRect(s.id);
                    AddLabel(CityDistricts.Find(s.id)?.Name ?? s.id, new Vector2(d.CenterX, d.Y + d.H + 10f), 22f, Theme.Cream, true);
                }
            }
            if (_youSprite == null)
            {
                var tex = TextureFactory.ToTexture(WorldMapArt.You(), "map.you");
                _youSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
            _you = UiKit.Panel(_overlay, Color.white, _youSprite, false, "You");
            _you.rectTransform.anchorMin = _you.rectTransform.anchorMax = Vector2.zero;
            _you.rectTransform.sizeDelta = new Vector2(52f, 52f);
            var (yx, yy) = WorldAtlas.ToMap(CityAtlas.Outside(_model.Here), CityAtlas.Outside(_model.Here) == _model.Here ? _model.HereAt : CityDistricts.Map(CityAtlas.Outside(_model.Here)).DefaultSpawn.Position);
            _youAt = new Vector2(yx, yy);

            // The chrome: title, zoom buttons, footer and close.
            var safe = UiKit.SafeArea(root);
            var bar = UiKit.Panel(safe, new Color(0.04f, 0.04f, 0.09f, 0.82f), name: "TitleBar");
            bar.rectTransform.anchorMin = new Vector2(0f, 1f);
            bar.rectTransform.anchorMax = new Vector2(1f, 1f);
            bar.rectTransform.pivot = new Vector2(0.5f, 1f);
            bar.rectTransform.sizeDelta = new Vector2(0f, 150f);
            bar.raycastTarget = true; // taps on the bars don't fall through to the map
            var title = UiKit.Label(bar.rectTransform, "BALLER WORLD MAP", 50f, Theme.Gold, TextAlignmentOptions.Center, true, "Title");
            title.rectTransform.anchorMin = new Vector2(0f, 0.45f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;
            UiKit.ApplyTextShadow(title);
            _hintLabel = UiKit.Label(bar.rectTransform, Loc.T("Pinch to zoom, drag to move, tap a place to go."), 24f, Theme.Cream, TextAlignmentOptions.Center, false, "Hint");
            _hintLabel.rectTransform.anchorMin = new Vector2(0f, 0.05f);
            _hintLabel.rectTransform.anchorMax = new Vector2(1f, 0.45f);
            _hintLabel.rectTransform.offsetMin = _hintLabel.rectTransform.offsetMax = Vector2.zero;

            var side = UiKit.NewRect("ZoomButtons", safe);
            side.anchorMin = side.anchorMax = new Vector2(1f, 0.62f);
            side.pivot = new Vector2(1f, 0.5f);
            side.sizeDelta = new Vector2(150f, 520f);
            side.anchoredPosition = new Vector2(-20f, 0f);
            AddSideButton(side, "+", 0, () => ZoomAround(1.6f, ViewCenter()));
            AddSideButton(side, "-", 1, () => ZoomAround(1f / 1.6f, ViewCenter()));
            AddSideButton(side, "ME", 2, () => FrameRect(WorldAtlas.Frame(WorldRegions.RegionOf(_model.Here).Id), centerOn: _youAt));
            AddSideButton(side, "ALL", 3, () => FrameRect(new MapRect(0f, 0f, WorldAtlas.Width, WorldAtlas.Height)));

            var foot = UiKit.Panel(safe, new Color(0.04f, 0.04f, 0.09f, 0.82f), name: "Footer");
            foot.rectTransform.anchorMin = new Vector2(0f, 0f);
            foot.rectTransform.anchorMax = new Vector2(1f, 0f);
            foot.rectTransform.pivot = new Vector2(0.5f, 0f);
            foot.rectTransform.sizeDelta = new Vector2(0f, 210f);
            foot.raycastTarget = true;
            var footText = UiKit.Label(foot.rectTransform, _model.Footer ?? "", 24f, Theme.Cream, TextAlignmentOptions.Center, false, "Stats");
            footText.rectTransform.anchorMin = new Vector2(0f, 0.6f);
            footText.rectTransform.anchorMax = new Vector2(1f, 1f);
            footText.rectTransform.offsetMin = new Vector2(24f, 0f);
            footText.rectTransform.offsetMax = new Vector2(-24f, 0f);
            footText.enableAutoSizing = true;
            footText.fontSizeMin = 14f;
            footText.fontSizeMax = 24f;
            var close = UiKit.Button(foot.rectTransform, "CLOSE", Close, ButtonStyle.Ghost, 100f, 36f);
            UiKit.Place((RectTransform)close.transform, new Vector2(0.5f, 0.3f), new Vector2(420f, 100f));
        }

        private void AddSideButton(RectTransform parent, string text, int index, Action onClick)
        {
            var b = UiKit.Button(parent, text, onClick, ButtonStyle.Secondary, 110f, 44f);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(140f, 110f);
            rt.anchoredPosition = new Vector2(0f, -index * 128f);
        }

        private void AddLabel(string text, Vector2 at, float size, Color color, bool district)
        {
            var l = UiKit.Label(_overlay, text, size, color, TextAlignmentOptions.Center, true, "Label");
            l.rectTransform.anchorMin = l.rectTransform.anchorMax = Vector2.zero;
            l.rectTransform.pivot = new Vector2(0.5f, 0f);
            l.rectTransform.sizeDelta = new Vector2(420f, size * 1.6f);
            l.textWrappingMode = TextWrappingModes.NoWrap;
            UiKit.ApplyTextShadow(l);
            _labels.Add((l, at, district));
        }

        private static void Place(RectTransform rt, MapRect r)
        {
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(r.X, r.Y);
            rt.sizeDelta = new Vector2(r.W, r.H);
        }

        private static Color Hex(string hex)
        {
            var c = RgbColor.FromHex(hex);
            return new Color32(c.r, c.g, c.b, 255);
        }

        private void Start()
        {
            // Open on the region you're in (or the whole world).
            Canvas.ForceUpdateCanvases();
            var size = _viewport.rect.size;
            _minZoom = Mathf.Min(size.x / WorldAtlas.Width, size.y / WorldAtlas.Height) * 0.95f;
            _maxZoom = Mathf.Max(_minZoom * 4f, size.x / 110f);
            if (_model.Focus != null) FrameRect(WorldAtlas.Frame(_model.Focus), centerOn: _model.Focus == WorldRegions.RegionOf(_model.Here).Id ? _youAt : (Vector2?)null);
            else FrameRect(new MapRect(0f, 0f, WorldAtlas.Width, WorldAtlas.Height));
        }

        // ------------------------------------------------------------------ every frame

        private void Update()
        {
            // Build a texture or two a frame so the map opens at once and fills in.
            if (_pendingPlates.Count > 0)
            {
                var (id, img) = _pendingPlates[0];
                _pendingPlates.RemoveAt(0);
                if (!Plates.TryGetValue(id, out var tex) || tex == null)
                {
                    var p = WorldAtlas.Plate(id);
                    Plates[id] = tex = TextureFactory.ToTexture(WorldMapArt.Plate(id, (int)(p.W / WorldAtlas.PxPerTile), (int)(p.H / WorldAtlas.PxPerTile)), "map.plate." + id);
                }
                if (img != null)
                {
                    img.texture = tex;
                    img.color = _model.IsOpen == null || _model.IsOpen(id) ? Color.white : new Color(0.45f, 0.45f, 0.5f, 1f);
                }
            }
            else if (_pendingDistricts.Count > 0)
            {
                // Nearest to the middle of the screen first.
                var mid = ScreenToMap(ViewCenter());
                int best = 0;
                float bestD = float.MaxValue;
                for (int i = 0; i < _pendingDistricts.Count; i++)
                {
                    var r = WorldAtlas.DistrictRect(_pendingDistricts[i].id);
                    float dd = (new Vector2(r.CenterX, r.CenterY) - mid).sqrMagnitude;
                    if (dd < bestD) { bestD = dd; best = i; }
                }
                var (id, img) = _pendingDistricts[best];
                _pendingDistricts.RemoveAt(best);
                if (!Districts.TryGetValue(id, out var tex) || tex == null)
                    Districts[id] = tex = TextureFactory.ToTexture(WorldMapArt.District(CityDistricts.Map(id)), "map.district." + id);
                if (img != null)
                {
                    img.texture = tex;
                    img.color = _model.IsOpen == null || _model.IsOpen(WorldRegions.RegionOf(id).Id) ? Color.white : new Color(0.45f, 0.45f, 0.5f, 1f);
                }
            }

            // Keys and pads (Mac, iPad keyboards).
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame) { if (_sheet != null) CloseSheet(); else Close(); }
                Vector2 move = Vector2.zero;
                if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) move.x += 1f;
                if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) move.x -= 1f;
                if (kb.upArrowKey.isPressed || kb.wKey.isPressed) move.y -= 1f;
                if (kb.downArrowKey.isPressed || kb.sKey.isPressed) move.y += 1f;
                if (move != Vector2.zero) PanBy(move * 900f * Time.unscaledDeltaTime);
                if (kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame) ZoomAround(1.6f, ViewCenter());
                if (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame) ZoomAround(1f / 1.6f, ViewCenter());
            }
#endif
        }

        private void LateUpdate()
        {
            _content.localScale = new Vector3(_zoom, _zoom, 1f);
            _content.anchoredPosition = _pan;
            // Region names everywhere; district names once they're big enough to read.
            bool districts = _zoom > 0.75f;
            foreach (var (label, at, district) in _labels)
            {
                bool show = district ? districts : true;
                label.enabled = show;
                if (show) label.rectTransform.anchoredPosition = _pan + at * _zoom;
            }
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.12f;
            _you.rectTransform.anchoredPosition = _pan + _youAt * _zoom;
            _you.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
            string hereOutside = CityAtlas.Outside(_model.Here);
            foreach (var kv in _frames)
            {
                var r = WorldRegions.RegionOf(kv.Key);
                Color baseColor = Hex(r.Primary);
                if (kv.Key == _selected) kv.Value.color = Color.white;
                else if (kv.Key == hereOutside) kv.Value.color = Color.Lerp(baseColor, Color.white, 0.5f + Mathf.Sin(Time.unscaledTime * 4f) * 0.5f);
                else kv.Value.color = baseColor;
            }
        }

        // ------------------------------------------------------------------ zoom and pan

        private Vector2 ViewCenter() => _viewport.rect.size * 0.5f;

        private Vector2 ScreenToMap(Vector2 local) => (local - _pan) / _zoom;

        private bool ToLocal(Vector2 screen, Camera cam, out Vector2 local) =>
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, screen, cam, out local);

        private void ZoomAround(float factor, Vector2 local)
        {
            float z = Mathf.Clamp(_zoom * factor, _minZoom, _maxZoom);
            var mapPt = ScreenToMap(local);
            _zoom = z;
            _pan = local - mapPt * _zoom;
            ClampPan();
        }

        private void PanBy(Vector2 delta)
        {
            _pan += delta;
            ClampPan();
        }

        private void FrameRect(MapRect r, Vector2? centerOn = null)
        {
            var size = _viewport.rect.size;
            // Leave room for the title bar and the footer.
            float usableH = Mathf.Max(200f, size.y - 380f);
            _zoom = Mathf.Clamp(Mathf.Min(size.x / r.W, usableH / r.H), _minZoom, _maxZoom);
            var c = centerOn ?? new Vector2(r.CenterX, r.CenterY);
            _pan = new Vector2(size.x * 0.5f, 210f + usableH * 0.5f) - c * _zoom;
            ClampPan();
        }

        private void ClampPan()
        {
            var size = _viewport.rect.size;
            float w = WorldAtlas.Width * _zoom, h = WorldAtlas.Height * _zoom;
            const float slack = 160f;
            _pan.x = w + slack * 2f < size.x ? (size.x - w) * 0.5f : Mathf.Clamp(_pan.x, size.x - w - slack, slack);
            _pan.y = h + slack * 2f < size.y ? (size.y - h) * 0.5f : Mathf.Clamp(_pan.y, size.y - h - slack - 150f, slack + 210f);
        }

        // ------------------------------------------------------------------ gestures

        public void OnPointerDown(PointerEventData e)
        {
            if (!ToLocal(e.position, e.pressEventCamera, out var local)) return;
            _pointers[e.pointerId] = local;
            if (_pointers.Count == 1)
            {
                _multi = false;
                _tapStart = local;
                _tapTime = Time.unscaledTime;
                _moved = 0f;
            }
            else _multi = true;
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_pointers.TryGetValue(e.pointerId, out var prev)) return;
            if (!ToLocal(e.position, e.pressEventCamera, out var local)) return;
            _pointers[e.pointerId] = local;
            _moved += (local - prev).magnitude;
            if (_pointers.Count == 1)
            {
                PanBy(local - prev);
                return;
            }
            // Pinch: the two first fingers; zoom by how far apart they've moved, pan by how their middle moved.
            Vector2 other = Vector2.zero;
            foreach (var kv in _pointers) if (kv.Key != e.pointerId) { other = kv.Value; break; }
            float before = Vector2.Distance(prev, other), after = Vector2.Distance(local, other);
            var midBefore = (prev + other) * 0.5f;
            var midAfter = (local + other) * 0.5f;
            if (before > 10f) ZoomAround(after / before, midBefore);
            PanBy(midAfter - midBefore);
        }

        public void OnPointerUp(PointerEventData e)
        {
            bool tap = _pointers.Count == 1 && !_multi && _moved < 24f && Time.unscaledTime - _tapTime < 0.5f;
            _pointers.Remove(e.pointerId);
            if (tap && ToLocal(e.position, e.pressEventCamera, out var local)) Tap(local);
        }

        public void OnScroll(PointerEventData e)
        {
            if (!ToLocal(e.position, e.enterEventCamera, out var local)) return;
            float s = e.scrollDelta.y;
            if (Mathf.Abs(s) < 0.01f) return;
            ZoomAround(Mathf.Pow(1.15f, Mathf.Clamp(s, -3f, 3f)), local);
        }

        // ------------------------------------------------------------------ choosing somewhere

        private void Tap(Vector2 local)
        {
            var at = ScreenToMap(local);
            var pick = WorldAtlas.Pick(at.x, at.y);
            if (pick.IsNone) { CloseSheet(); return; }
            // Zoomed right out, a tap picks the whole state, not one of its little districts.
            string district = _zoom < 0.55f ? null : pick.District;
            ShowSheet(_model.Describe(pick.Region, district));
            Audio.AudioManager.Click();
        }

        private void ShowSheet(MapChoice c)
        {
            CloseSheet();
            _selected = c.District;
            var root = (RectTransform)transform;
            _sheet = UiKit.NewRect("Sheet", root);
            _sheet.anchorMin = new Vector2(0f, 0f);
            _sheet.anchorMax = new Vector2(1f, 0f);
            _sheet.pivot = new Vector2(0.5f, 0f);
            _sheet.sizeDelta = new Vector2(0f, 560f);
            var bg = UiKit.Panel(_sheet, new Color(0.05f, 0.05f, 0.1f, 0.96f), name: "SheetBg");
            UiKit.Stretch(bg.rectTransform);
            bg.raycastTarget = true;
            var region = WorldRegions.Find(c.Region);
            var stripe = UiKit.Panel(_sheet, region != null ? Hex(region.Primary) : Theme.Gold, name: "Stripe");
            stripe.rectTransform.anchorMin = new Vector2(0f, 1f);
            stripe.rectTransform.anchorMax = new Vector2(1f, 1f);
            stripe.rectTransform.pivot = new Vector2(0.5f, 1f);
            stripe.rectTransform.sizeDelta = new Vector2(0f, 10f);

            var title = UiKit.Label(_sheet, c.Title ?? "", 52f, Theme.Gold, TextAlignmentOptions.Center, true, "Title");
            UiKit.Band(title.rectTransform, 0.8f, 0.96f, 30f);
            UiKit.ApplyTextShadow(title);
            string sub = (c.Here ? Loc.T("YOU ARE HERE") + "  ·  " : "") + (c.Next ? Loc.T("NEXT IN THE COMIC") + "  ·  " : "") + (c.Subtitle ?? "");
            var subtitle = UiKit.Label(_sheet, sub, 26f, Theme.Cream, TextAlignmentOptions.Center, true, "Subtitle");
            UiKit.Band(subtitle.rectTransform, 0.71f, 0.8f, 30f);
            var detail = UiKit.Label(_sheet, Loc.T(c.Detail ?? "") + "\n" + (c.Blocked ?? c.Stats ?? ""), 26f, c.Blocked != null ? new Color(1f, 0.6f, 0.5f) : Theme.Cream,
                                     TextAlignmentOptions.Center, false, "Detail");
            UiKit.Band(detail.rectTransform, 0.44f, 0.71f, 40f);
            detail.enableAutoSizing = true;
            detail.fontSizeMin = 16f;
            detail.fontSizeMax = 26f;

            var row = UiKit.NewRect("Buttons", _sheet);
            UiKit.Band(row, 0.06f, 0.4f, 24f);
            var buttons = new List<(string, ButtonStyle, Action)>();
            if (c.CanDrive) buttons.Add(("DRIVE THERE", ButtonStyle.Primary, () => { var d = _model.Drive; Close(false); d?.Invoke(c.Region, c.District); }));
            if (c.CanSpawn) buttons.Add(("SPAWN THERE", c.CanDrive ? ButtonStyle.Secondary : ButtonStyle.Primary, () => { var s = _model.Spawn; Close(false); s?.Invoke(c.Region, c.District); }));
            buttons.Add(("BACK", ButtonStyle.Ghost, CloseSheet));
            for (int i = 0; i < buttons.Count; i++)
            {
                var (label, style, action) = buttons[i];
                var b = UiKit.Button(row, label, action, style, 120f, 38f);
                var rt = (RectTransform)b.transform;
                float w = 1f / buttons.Count;
                rt.anchorMin = new Vector2(i * w, 0f);
                rt.anchorMax = new Vector2((i + 1) * w, 1f);
                rt.offsetMin = new Vector2(10f, 10f);
                rt.offsetMax = new Vector2(-10f, -10f);
            }
        }

        private void CloseSheet()
        {
            if (_sheet != null) Destroy(_sheet.gameObject);
            _sheet = null;
            _selected = null;
        }

        private void Close() => Close(true);

        private void Close(bool notify)
        {
            var closed = _model.Closed;
            _model.Closed = null;
            if (notify) closed?.Invoke();
        }
    }
}
