using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using CallerRetroBall.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CallerRetroBall.Controls
{
    /// <summary>
    /// Settings ► CUSTOMIZE CONTROLS: the real match buttons over an empty court. Drag a button to move
    /// it, tap one and use − / + to resize it, RESET for the default arc, SAVE to keep it (saved in the
    /// career, so it follows iCloud sync). The same layout is used in portrait and landscape.
    /// </summary>
    public sealed class ControlEditor : MonoBehaviour
    {
        private TouchControls _controls;
        private ControlSlot[] _slots;
        private ControlButton _selected = ControlButton.Shoot;
        private TextMeshProUGUI _status;
        private Image _marker;
        private Canvas _back, _front;

        public static ControlEditor Open()
        {
            var existing = FindAnyObjectByType<ControlEditor>();
            if (existing != null) return existing;
            var go = new GameObject("ControlEditor");
            var e = go.AddComponent<ControlEditor>();
            e.Build();
            return e;
        }

        private int _preset;
        private Button _presetButton;

        private void Build()
        {
            var s = App.Career.settings;
            _preset = ControlLayout.ClampPreset(s.controlPreset);
            _back = UiKit.CreateScreenCanvas("ControlEditorBack", 44);
            _back.transform.SetParent(transform, false);
            var floor = UiKit.Panel(_back.transform, Theme.Asphalt, name: "Floor");
            UiKit.Stretch(floor.rectTransform);
            floor.raycastTarget = true;
            var lines = UiKit.Panel(floor.transform, new Color(1f, 1f, 1f, 0.08f), name: "Line");
            lines.rectTransform.anchorMin = new Vector2(0f, 0.62f);
            lines.rectTransform.anchorMax = new Vector2(1f, 0.625f);
            lines.rectTransform.offsetMin = lines.rectTransform.offsetMax = Vector2.zero;

            _controls = TouchControls.Create(new InputBuffer(), s.leftHanded, s.largeButtons, TouchControls.Seat.Full, s.controlLayout);
            _controls.transform.SetParent(transform, false);
            _controls.Canvas.sortingOrder = 45;
            _controls.SetRole(false, false);
            _slots = (ControlSlot[])_controls.Slots.Clone();
            foreach (ControlButton b in System.Enum.GetValues(typeof(ControlButton)))
            {
                var drag = _controls[b].gameObject.AddComponent<ButtonDragger>();
                drag.Init(this, b);
            }
            _marker = UiKit.Panel(_controls.transform, new Color(1f, 0.82f, 0.4f, 0.55f), TouchControls.Face(new Color(1f, 0.82f, 0.4f), false), false, "Selected");
            _marker.raycastTarget = false;

            _front = UiKit.CreateScreenCanvas("ControlEditorFront", 46);
            _front.transform.SetParent(transform, false);
            var safe = UiKit.SafeArea(_front.transform);
            var title = UiKit.ShadowLabel(safe, "CUSTOMIZE CONTROLS", 56f, Theme.Cream, Theme.Pink, 5f);
            UiKit.Band((RectTransform)title.transform.parent, 0.93f, 0.99f, 24f);
            var help = UiKit.Label(safe, "Drag a button to move it. Tap one, then use - and + to resize it. The stick appears wherever your left thumb lands. " +
                                         "The same layout is used in landscape Full Court.", 28f, Theme.Cream);
            UiKit.Band(help.rectTransform, 0.83f, 0.92f, 48f);
            help.raycastTarget = false;

            var row = UiKit.Row(safe, 16f, "Tools");
            UiKit.Band(row, 0.73f, 0.8f, 48f);
            UiKit.Button(row, "-", () => Resize(0.9f), ButtonStyle.Secondary, 90f, 48f);
            UiKit.Button(row, "+", () => Resize(1.1f), ButtonStyle.Secondary, 90f, 48f);
            UiKit.Button(row, "RESET", ResetLayout, ButtonStyle.Ghost, 90f, 32f);
            // I137: start again from either arc.
            _presetButton = UiKit.Button(row, ControlLayout.PresetNames[1 - _preset], SwapPreset, ButtonStyle.Ghost, 90f, 30f);
            _status = UiKit.Label(safe, "", 28f, Theme.Gold, TextAlignmentOptions.Center, true);
            UiKit.Band(_status.rectTransform, 0.67f, 0.72f, 48f);

            var row2 = UiKit.Row(safe, 16f, "SaveRow");
            UiKit.Band(row2, 0.6f, 0.66f, 120f);
            UiKit.Button(row2, "CANCEL", Close, ButtonStyle.Ghost, 90f, 32f);
            UiKit.Button(row2, "SAVE", Save, ButtonStyle.Primary, 90f, 36f);
            Refresh();
        }

        public void Select(ControlButton b)
        {
            _selected = b;
            Refresh();
        }

        /// <summary>Moves a button by a drag (canvas units, screen direction).</summary>
        public void Move(ControlButton b, Vector2 delta)
        {
            int i = (int)b;
            float k = _controls.Scale;
            var s = _slots[i];
            s.X += (_controls.LeftHanded ? -delta.x : delta.x) / k;
            s.Y += delta.y / k;
            _slots[i] = ControlLayout.Clamp(s);
            _controls.ApplyLayout(_slots);
            Refresh();
        }

        private void Resize(float factor)
        {
            int i = (int)_selected;
            var s = _slots[i];
            s.Size *= factor;
            _slots[i] = ControlLayout.Clamp(s);
            _controls.ApplyLayout(_slots);
            Refresh();
        }

        private void ResetLayout()
        {
            _slots = ControlLayout.Preset(_preset);
            _controls.ApplyLayout(_slots);
            Refresh();
        }

        /// <summary>I137: switches between the SPREAD and CLASSIC arcs (and resets to it).</summary>
        private void SwapPreset()
        {
            _preset = 1 - _preset;
            var label = _presetButton != null ? _presetButton.GetComponentInChildren<TextMeshProUGUI>() : null;
            if (label != null) label.text = Loc.T(ControlLayout.PresetNames[1 - _preset]);
            ResetLayout();
        }

        private void Refresh()
        {
            var bt = (RectTransform)_controls[_selected].transform;
            _marker.rectTransform.SetParent(bt, false);
            _marker.rectTransform.SetAsFirstSibling();
            _marker.rectTransform.anchorMin = Vector2.zero;
            _marker.rectTransform.anchorMax = Vector2.one;
            _marker.rectTransform.offsetMin = new Vector2(-14f, -14f);
            _marker.rectTransform.offsetMax = new Vector2(14f, 14f);
            var overlap = ControlLayout.Overlapping(_slots);
            var close = ControlLayout.TooClose(_slots);
            float pct = _slots[(int)_selected].Size / ControlLayout.DefaultSize(_selected) * 100f;
            _status.text = Loc.T(ControlLayout.PresetNames[_preset]) + "  ·  " + _selected.ToString().ToUpperInvariant() + "  ·  " + Loc.T("SIZE") + " " + Mathf.RoundToInt(pct) + "%"
                           + (overlap.Count > 0 ? "\n<color=#F72585>" + Loc.T("OVERLAPPING") + ": " + string.Join(", ", overlap).ToUpperInvariant() + "</color>"
                              : close.Count > 0 ? "\n<color=#FFD166>" + Loc.T("TOO CLOSE") + ": " + string.Join(", ", close).ToUpperInvariant() + "</color>" : "");
        }

        private void Save()
        {
            App.Career.settings.controlPreset = _preset;
            App.Career.settings.controlLayout = ControlLayout.Serialize(_slots, _preset);
            App.SaveCareer();
            Close();
        }

        private void Close() => Destroy(gameObject);

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Close();
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if (pad != null && pad.buttonEast.wasPressedThisFrame) Close();
#endif
        }
    }

    /// <summary>Lets one match button be dragged in the control editor.</summary>
    public sealed class ButtonDragger : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler
    {
        private ControlEditor _editor;
        private ControlButton _button;
        private Canvas _canvas;

        public void Init(ControlEditor editor, ControlButton button)
        {
            _editor = editor;
            _button = button;
            _canvas = GetComponentInParent<Canvas>();
        }

        public void OnPointerDown(PointerEventData e) => _editor.Select(_button);

        public void OnBeginDrag(PointerEventData e) => _editor.Select(_button);

        public void OnDrag(PointerEventData e)
        {
            float scale = _canvas != null ? _canvas.scaleFactor : 1f;
            _editor.Move(_button, e.delta / Mathf.Max(0.01f, scale));
        }
    }
}
