using System;
using System.Collections.Generic;
using CallerRetroBall.Controls;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using CallerRetroBall.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CallerRetroBall.City
{
    /// <summary>
    /// 16 Oct 2026 (BALLER CITY): the city's touch layer. A floating stick on one half, a big context button
    /// (its label says what it does: ENTER COURT, TALK, LEAVE BUILDING…) and SPRINT on the other, MENU up top.
    /// Left-handed mirrors the sides; Large Buttons makes the buttons 25% bigger. Choice cards for courts, doors and
    /// people slide over the bottom of the screen.
    /// </summary>
    public sealed partial class CityHud : MonoBehaviour
    {
        public static readonly Color ActColor = new Color32(0xFF, 0xD1, 0x66, 255);
        public static readonly Color SprintColor = new Color32(0x3B, 0xD5, 0xFF, 255);

        public VirtualJoystick Stick { get; private set; }
        public TouchActionButton Interact { get; private set; }
        public TouchActionButton Sprint { get; private set; }
        public event Action MenuRequested;
        /// <summary>28 Oct 2026: the MAP button (the zoomable world map).</summary>
        public event Action MapRequested;

        private Canvas _canvas;
        private TextMeshProUGUI _district, _time, _hint, _toast, _prompt;
        private float _toastUntil;
        private GameObject _card;
        private GameObject _controls;
        private string _lastPrompt;

        public bool CardOpen => _card != null;

        public static CityHud Create(SettingsData settings)
        {
            var go = new GameObject("CityHud");
            var hud = go.AddComponent<CityHud>();
            hud.Build(settings);
            return hud;
        }

        private void Build(SettingsData s)
        {
            bool left = s != null && s.leftHanded;
            float big = s != null && s.largeButtons ? 1.25f : 1f;
            float alpha = ControlFeel.OpacityFor(s);
            _canvas = UiKit.CreateScreenCanvas("CityHudCanvas", 10);
            _canvas.transform.SetParent(transform, false);
            var safe = UiKit.SafeArea(_canvas.transform);

            // Top: where you are, the time of day, MENU.
            var bar = UiKit.Panel(safe, new Color(0.04f, 0.04f, 0.09f, 0.72f), name: "TopBar");
            bar.rectTransform.anchorMin = new Vector2(0f, 1f);
            bar.rectTransform.anchorMax = new Vector2(1f, 1f);
            bar.rectTransform.pivot = new Vector2(0.5f, 1f);
            bar.rectTransform.sizeDelta = new Vector2(0f, 130f);
            bar.rectTransform.anchoredPosition = Vector2.zero;
            _district = UiKit.Label(bar.rectTransform, "", 44f, Theme.Gold, TextAlignmentOptions.Left, true, "District");
            _district.rectTransform.anchorMin = new Vector2(0f, 0.42f);
            _district.rectTransform.anchorMax = new Vector2(0.58f, 1f); // 28 Oct 2026: room for MAP
            _district.enableAutoSizing = true;
            _district.fontSizeMin = 24f;
            _district.fontSizeMax = 44f;
            _district.rectTransform.offsetMin = new Vector2(32f, 0f);
            _district.rectTransform.offsetMax = Vector2.zero;
            _time = UiKit.Label(bar.rectTransform, "", 28f, Theme.Cream, TextAlignmentOptions.Left, false, "Time");
            _time.rectTransform.anchorMin = new Vector2(0f, 0.05f);
            _time.rectTransform.anchorMax = new Vector2(0.58f, 0.45f);
            _time.enableAutoSizing = true;
            _time.fontSizeMin = 16f;
            _time.fontSizeMax = 28f;
            _time.rectTransform.offsetMin = new Vector2(32f, 0f);
            _time.rectTransform.offsetMax = Vector2.zero;
            var menu = UiKit.Button(bar.rectTransform, "MENU", () => MenuRequested?.Invoke(), ButtonStyle.Ghost, 96f, 40f);
            var mrt = (RectTransform)menu.transform;
            mrt.anchorMin = mrt.anchorMax = new Vector2(1f, 0.5f);
            mrt.pivot = new Vector2(1f, 0.5f);
            mrt.sizeDelta = new Vector2(220f, 96f);
            mrt.anchoredPosition = new Vector2(-24f, 0f);
            var mapButton = UiKit.Button(bar.rectTransform, "MAP", () => MapRequested?.Invoke(), ButtonStyle.Secondary, 96f, 40f);
            var mbrt = (RectTransform)mapButton.transform;
            mbrt.anchorMin = mbrt.anchorMax = new Vector2(1f, 0.5f);
            mbrt.pivot = new Vector2(1f, 0.5f);
            mbrt.sizeDelta = new Vector2(180f, 96f);
            mbrt.anchoredPosition = new Vector2(-260f, 0f);

            _hint = UiKit.Label(safe, "", 34f, Theme.Cream, TextAlignmentOptions.Center, true, "Hint");
            _hint.rectTransform.anchorMin = new Vector2(0.04f, 1f);
            _hint.rectTransform.anchorMax = new Vector2(0.74f, 1f); // I21: clear of the minimap
            _hint.rectTransform.pivot = new Vector2(0.5f, 1f);
            _hint.rectTransform.sizeDelta = new Vector2(0f, 110f);
            _hint.rectTransform.anchoredPosition = new Vector2(0f, -240f); // 28 Oct 2026: below the objective ticker
            UiKit.ApplyTextShadow(_hint);

            _toast = UiKit.Label(safe, "", 52f, Theme.Gold, TextAlignmentOptions.Center, true, "Toast");
            UiKit.Band(_toast.rectTransform, 0.6f, 0.68f, 40f);
            UiKit.ApplyTextShadow(_toast);

            // Controls.
            var controls = UiKit.NewRect("Controls", safe);
            UiKit.Stretch(controls);
            _controls = controls.gameObject;
            var zone = UiKit.NewRect("StickZone", controls);
            zone.anchorMin = new Vector2(left ? 0.45f : 0f, 0f);
            zone.anchorMax = new Vector2(left ? 1f : 0.55f, 0.72f);
            zone.offsetMin = zone.offsetMax = Vector2.zero;
            var zoneImage = zone.gameObject.AddComponent<Image>();
            zoneImage.color = new Color(0f, 0f, 0f, 0f);
            zoneImage.raycastTarget = true;
            var rest = UiKit.Panel(zone, new Color(1f, 1f, 1f, 0.22f * alpha), Theme.DiscSprite(new Color(0f, 0f, 0f, 0.2f), Theme.Cream), false, "StickRest");
            rest.rectTransform.anchorMin = rest.rectTransform.anchorMax = new Vector2(left ? 1f : 0f, 0f);
            rest.rectTransform.sizeDelta = new Vector2(280f, 280f);
            rest.rectTransform.anchoredPosition = new Vector2(left ? -220f : 220f, 230f);
            var baseRing = UiKit.Panel(zone, new Color(1f, 1f, 1f, 0.35f), Theme.DiscSprite(new Color(0f, 0f, 0f, 0.25f), Theme.Cream), false, "StickBase");
            baseRing.rectTransform.anchorMin = baseRing.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            baseRing.rectTransform.sizeDelta = new Vector2(280f, 280f);
            var knob = UiKit.Panel(zone, Color.white, Theme.DiscSprite(Theme.Cream, Theme.Shadow), false, "StickKnob");
            knob.rectTransform.anchorMin = knob.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            knob.rectTransform.sizeDelta = new Vector2(120f, 120f);
            Stick = zone.gameObject.AddComponent<VirtualJoystick>();
            Stick.Init(zone, baseRing.rectTransform, knob.rectTransform);
            Stick.Radius = ControlFeel.RadiusFor(s);
            Stick.DeadZone = ControlFeel.DeadZoneFor(s);
            Stick.Moved += on => rest.gameObject.SetActive(!on);

            float side = left ? 1f : -1f;
            Interact = MakeButton(controls, "ACTION", ActColor, 270f * big, new Vector2(left ? 0f : 1f, 0f), new Vector2(side * 200f * big, 250f * big), alpha);
            Sprint = MakeButton(controls, "SPRINT", SprintColor, 190f * big, new Vector2(left ? 0f : 1f, 0f), new Vector2(side * 420f * big, 150f * big), alpha);

            // What the action button will do, in words, just above it (readable even under a thumb).
            _prompt = UiKit.Label(controls, "", 34f, Theme.Cream, TextAlignmentOptions.Center, true, "Prompt");
            var prt = _prompt.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(left ? 0f : 1f, 0f);
            prt.sizeDelta = new Vector2(420f, 60f);
            prt.anchoredPosition = new Vector2(side * 200f * big, 250f * big + 175f * big);
            UiKit.ApplyTextShadow(_prompt);
            BuildWorld(safe);
            BuildMinimap(safe); // I21 // 28 Oct 2026 (BALLER WORLD): the guide, the errand clock
        }

        private static TouchActionButton MakeButton(RectTransform parent, string text, Color color, float size, Vector2 anchor, Vector2 pos, float alpha)
        {
            var image = UiKit.Panel(parent, new Color(1f, 1f, 1f, alpha), TouchControls.Face(color, false), false, "Btn " + text);
            image.raycastTarget = true;
            image.preserveAspect = true;
            var rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = pos;
            var label = UiKit.Label(rt, text, 34f, Theme.Cream, TextAlignmentOptions.Center, true);
            label.rectTransform.anchorMin = new Vector2(0.1f, 0.2f);
            label.rectTransform.anchorMax = new Vector2(0.9f, 0.8f);
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.enableAutoSizing = true;
            label.fontSizeMin = 16f;
            label.fontSizeMax = 34f;
            UiKit.ApplyTextShadow(label);
            var hint = UiKit.Label(rt, "", 24f, Theme.Cream, TextAlignmentOptions.Center, true, "Hint");
            UiKit.Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(260f, 40f));
            hint.rectTransform.anchoredPosition = new Vector2(0f, 24f);
            var button = image.gameObject.AddComponent<TouchActionButton>();
            button.Init(image, label, hint);
            button.SetSprites(TouchControls.Face(color, false), TouchControls.Face(color, true));
            return button;
        }

        public void SetPlace(string district, string time)
        {
            _district.text = Loc.T(district);
            _time.text = Loc.T(time);
        }

        public void SetHint(string hint)
        {
            hint = hint == null ? "" : Loc.T(hint);
            if (_hint.text != hint) _hint.text = hint;
        }

        /// <summary>The action button's job right now (null = nothing in reach).</summary>
        public void SetPrompt(string label)
        {
            if (label == _lastPrompt) return;
            _lastPrompt = label;
            Interact.Available = label != null;
            Interact.UnavailableHint = "NOTHING HERE";
            Interact.SetLabel(label == null ? "ACTION" : ShortLabel(label));
            _prompt.text = label == null ? "" : Loc.T(label);
        }

        private static string ShortLabel(string label)
        {
            switch (label)
            {
                case "ENTER COURT": return "PLAY";
                case "LEAVE BUILDING": return "EXIT";
                case "VIEW SCHEDULE": return "INFO";
                default: return label.Length <= 7 ? label : label.Split(' ')[0];
            }
        }

        public void Toast(string text, float seconds = 1.6f)
        {
            _toast.text = Loc.T(text);
            _toastUntil = Time.unscaledTime + seconds;
        }

        public void SetControlsVisible(bool on)
        {
            if (!on)
            {
                Stick.Release();
                Interact.Release();
                Sprint.Release();
            }
            if (_controls.activeSelf != on) _controls.SetActive(on);
        }

        private void Update()
        {
            if (_toast.text.Length > 0 && Time.unscaledTime > _toastUntil) _toast.text = "";
        }

        // ------------------------------------------------------------------ cards

        /// <summary>
        /// A choice card: a title, a line (dialogue or detail) and the choices. Locked choices are shown greyed with
        /// why; tapping one explains instead of starting anything.
        /// </summary>
        public void ShowCard(string title, string line, IList<CityActivity> choices, Action<CityActivity> picked, Action closed)
        {
            CloseCard(false);
            SetControlsVisible(false);
            var root = UiKit.NewRect("Card", _canvas.transform);
            UiKit.Stretch(root);
            _card = root.gameObject;
            var scrim = UiKit.Panel(root, new Color(0.04f, 0.04f, 0.09f, 0.55f), name: "Scrim");
            UiKit.Stretch(scrim.rectTransform);
            scrim.raycastTarget = true;
            var safe = UiKit.SafeArea(root);
            var panel = UiKit.Panel(safe, new Color(0.1f, 0.1f, 0.18f, 0.96f), name: "Panel");
            panel.raycastTarget = true;
            int rows = (choices?.Count ?? 0) + 1;
            float height = Mathf.Min(1500f, 300f + rows * 150f);
            panel.rectTransform.anchorMin = new Vector2(0f, 0f);
            panel.rectTransform.anchorMax = new Vector2(1f, 0f);
            panel.rectTransform.pivot = new Vector2(0.5f, 0f);
            panel.rectTransform.sizeDelta = new Vector2(-48f, height);
            panel.rectTransform.anchoredPosition = new Vector2(0f, 24f);
            var column = UiKit.ScrollColumn(panel.rectTransform, 16f, new RectOffset(40, 40, 32, 32));
            UiKit.Size(UiKit.Label(column, title, 54f, Theme.Gold, TextAlignmentOptions.Left, true), 72f);
            if (!string.IsNullOrEmpty(line))
            {
                var l = UiKit.Label(column, line, 34f, Theme.Cream, TextAlignmentOptions.Left, false);
                UiKit.Size(l, line.Length > 60 ? 140f : 90f);
            }
            if (choices != null)
                foreach (var c in choices)
                {
                    var choice = c;
                    bool locked = choice.Locked != null;
                    var b = UiKit.Button(column, choice.Label, () =>
                    {
                        if (choice.Locked != null) { Toast(choice.Locked, 2.4f); return; }
                        picked?.Invoke(choice);
                    }, locked ? ButtonStyle.Ghost : choice.Primary ? ButtonStyle.Primary : ButtonStyle.Secondary, 120f, 46f);
                    if (locked)
                    {
                        var img = b.GetComponent<Image>();
                        if (img != null) img.color = new Color(1f, 1f, 1f, 0.55f);
                    }
                    string detail = locked ? choice.Locked : choice.Detail;
                    if (!string.IsNullOrEmpty(detail))
                        UiKit.Size(UiKit.Label(column, detail, 28f, locked ? Theme.Muted : Theme.Cream, TextAlignmentOptions.Center, false), 64f);
                }
            UiKit.Button(column, "CLOSE", () => { CloseCard(true); closed?.Invoke(); }, ButtonStyle.Ghost, 104f, 40f);
        }

        /// <summary>One district on the city MAP.</summary>
        public struct MapEntry
        {
            public string Id, Name, Summary;
            public int Col, Row;
            public bool Here, Visited;
        }

        /// <summary>
        /// 17 Oct 2026 (phase 3): the city MAP. Districts as blocks on a grid with the roads between them; tap one you've
        /// been to for fast travel. Districts you haven't walked into yet show as "???".
        /// </summary>
        public void ShowMap(IList<MapEntry> entries, int cols, int rows, string footer, Action<string> travel, Action closed)
        {
            CloseCard(false);
            SetControlsVisible(false);
            var root = UiKit.NewRect("Map", _canvas.transform);
            UiKit.Stretch(root);
            _card = root.gameObject;
            var scrim = UiKit.Panel(root, new Color(0.04f, 0.04f, 0.09f, 0.94f), name: "Scrim");
            UiKit.Stretch(scrim.rectTransform);
            scrim.raycastTarget = true;
            var safe = UiKit.SafeArea(root);
            var title = UiKit.Label(safe, "BALLER CITY MAP", 60f, Theme.Gold, TextAlignmentOptions.Center, true);
            UiKit.Band(title.rectTransform, 0.9f, 0.97f, 40f);
            var grid = UiKit.NewRect("Grid", safe);
            UiKit.Band(grid, 0.22f, 0.88f, 40f);
            // Roads between neighbouring blocks.
            foreach (var a in entries)
                foreach (var b in entries)
                {
                    bool right = b.Row == a.Row && b.Col == a.Col + 1, up = b.Col == a.Col && b.Row == a.Row + 1;
                    if (!right && !up) continue;
                    var road = UiKit.Panel(grid, new Color(0.23f, 0.23f, 0.27f, 1f), name: "Road");
                    var rt = road.rectTransform;
                    float x0 = (a.Col + 0.5f) / cols, y0 = (a.Row + 0.5f) / rows;
                    rt.anchorMin = new Vector2(right ? x0 : x0 - 0.02f, up ? y0 : y0 - 0.015f);
                    rt.anchorMax = new Vector2(right ? (b.Col + 0.5f) / cols : x0 + 0.02f, up ? (b.Row + 0.5f) / rows : y0 + 0.015f);
                    rt.offsetMin = rt.offsetMax = Vector2.zero;
                }
            foreach (var e in entries)
            {
                var entry = e;
                var cell = UiKit.NewRect("Cell " + e.Id, grid);
                cell.anchorMin = new Vector2((float)e.Col / cols, (float)e.Row / rows);
                cell.anchorMax = new Vector2((float)(e.Col + 1) / cols, (float)(e.Row + 1) / rows);
                cell.offsetMin = new Vector2(8f, 8f);
                cell.offsetMax = new Vector2(-8f, -8f);
                string label = e.Visited ? e.Name : "???";
                var b = UiKit.Button(cell, label, () =>
                {
                    if (entry.Here) { Toast("YOU ARE HERE", 1.4f); return; }
                    if (!entry.Visited) { Toast("Walk there first to unlock fast travel.", 2.2f); return; }
                    travel?.Invoke(entry.Id);
                }, e.Here ? ButtonStyle.Primary : e.Visited ? ButtonStyle.Secondary : ButtonStyle.Ghost, 10f, 30f);
                var brt = (RectTransform)b.transform;
                UiKit.Stretch(brt);
                if (e.Visited)
                {
                    var sum = UiKit.Label(brt, (e.Here ? Loc.T("YOU ARE HERE") + "\n" : "") + e.Summary, 20f, Theme.Cream, TextAlignmentOptions.Bottom, false, "Summary");
                    sum.rectTransform.anchorMin = new Vector2(0.04f, 0.04f);
                    sum.rectTransform.anchorMax = new Vector2(0.96f, 0.5f);
                    sum.rectTransform.offsetMin = sum.rectTransform.offsetMax = Vector2.zero;
                    sum.enableAutoSizing = true;
                    sum.fontSizeMin = 12f;
                    sum.fontSizeMax = 20f;
                }
            }
            var foot = UiKit.Label(safe, footer, 30f, Theme.Cream, TextAlignmentOptions.Center, false);
            UiKit.Band(foot.rectTransform, 0.12f, 0.21f, 40f);
            var close = UiKit.Button(safe, "CLOSE", () => { CloseCard(true); closed?.Invoke(); }, ButtonStyle.Ghost, 100f, 40f);
            UiKit.Place((RectTransform)close.transform, new Vector2(0.5f, 0.06f), new Vector2(420f, 110f));
        }

        public void CloseCard(bool showControls = true)
        {
            if (_card != null) Destroy(_card);
            _card = null;
            if (showControls) SetControlsVisible(true);
        }
    }
}
