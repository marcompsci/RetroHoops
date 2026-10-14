using System.Collections.Generic;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using CallerRetroBall.UI;
using CallerRetroBall.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CallerRetroBall.Controls
{
    /// <summary>
    /// On-screen match controls. Left thumb: a floating stick (its base rests in the lower-left until
    /// you touch). Right thumb: an arc of ring buttons in the corner, each with an icon and a label:
    /// SHOOT (hold/release) on the outside, PASS beside it (it reads OOP when a lob is on), DUNK above,
    /// LAYUP on the diagonal and CALL further in. On defence the same buttons turn into BLOCK, SWITCH
    /// and STEAL. Positions and sizes come from <see cref="ControlLayout"/> (Settings ► CUSTOMIZE
    /// CONTROLS). Presses go into an <see cref="InputBuffer"/> so slightly early taps still count.
    /// </summary>
    public sealed class TouchControls : MonoBehaviour
    {
        /// <summary>
        /// Where the controls sit. Full = the whole screen (one player). Bottom / Top = tabletop
        /// 2 Player on one phone lying flat: player 1 holds the bottom edge, player 2 the top edge
        /// (their controls are turned 180° to face them).
        /// </summary>
        public enum Seat { Full = 0, Bottom = 1, Top = 2 }

        public static readonly Color ShootColor = new Color32(0xFF, 0x4F, 0xA3, 255);
        public static readonly Color PassColor = new Color32(0x3B, 0xD5, 0xFF, 255);
        public static readonly Color DunkColor = new Color32(0xFF, 0x8C, 0x42, 255);
        public static readonly Color LayupColor = new Color32(0x7C, 0xE5, 0x77, 255);
        public static readonly Color CallColor = new Color32(0xB8, 0xB4, 0xD8, 255);
        public static readonly Color StealColor = new Color32(0xFF, 0xD1, 0x66, 255);
        public static readonly Color OopColor = new Color32(0xFF, 0xE0, 0x66, 255);

        public VirtualJoystick Joystick { get; private set; }
        public TouchActionButton Shoot { get; private set; }
        public TouchActionButton Pass { get; private set; }
        /// <summary>DUNK on offence, STEAL on defence.</summary>
        public TouchActionButton Dunk { get; private set; }
        public TouchActionButton Layup { get; private set; }
        public TouchActionButton Call { get; private set; }
        /// <summary>The steal button (the DUNK button on defence).</summary>
        public TouchActionButton Defense => Dunk;

        public TouchActionButton this[ControlButton b]
        {
            get
            {
                switch (b)
                {
                    case ControlButton.Shoot: return Shoot;
                    case ControlButton.Pass: return Pass;
                    case ControlButton.Dunk: return Dunk;
                    case ControlButton.Layup: return Layup;
                    default: return Call;
                }
            }
        }

        private InputBuffer _buffer;
        private Canvas _canvas;
        private bool _defense;
        private bool _oop;
        private RectTransform _stickRest;

        public Canvas Canvas => _canvas;

        /// <param name="leftHanded">Mirror the layout: stick on the right, buttons on the left.</param>
        /// <param name="largeButtons">Action buttons 25% bigger (accessibility).</param>
        /// <param name="layout">Saved layout string (Settings ► CUSTOMIZE CONTROLS); empty = the default arc.</param>
        public static TouchControls Create(InputBuffer buffer, bool leftHanded = false, bool largeButtons = false, Seat seat = Seat.Full,
                                           string layout = null)
        {
            var canvas = UiKit.CreateScreenCanvas(seat == Seat.Top ? "TouchControlsCanvasP2" : "TouchControlsCanvas", 10);
            var controls = canvas.gameObject.AddComponent<TouchControls>();
            // 14 Oct 2026: see-through controls (65% by default, Settings ► BUTTON OPACITY) so the court shows underneath.
            var group = canvas.gameObject.AddComponent<CanvasGroup>();
            group.alpha = ControlFeel.OpacityFor(Core.App.Career?.settings);
            controls._canvas = canvas;
            controls._buffer = buffer ?? new InputBuffer();
            controls._leftHanded = leftHanded;
            controls._seat = seat;
            // Half a screen each in tabletop play: slightly smaller buttons so both sets fit.
            controls._scale = (largeButtons ? 1.25f : 1f) * (seat == Seat.Full ? 1f : 0.82f);
            controls._slots = ControlLayout.Parse(seat == Seat.Full ? layout : null);
            var safe = UiKit.SafeArea(canvas.transform);
            if (seat == Seat.Full)
            {
                controls.Build(safe);
            }
            else
            {
                var half = UiKit.NewRect(seat == Seat.Top ? "SeatP2" : "SeatP1", safe);
                half.anchorMin = new Vector2(0f, seat == Seat.Top ? 0.5f : 0f);
                half.anchorMax = new Vector2(1f, seat == Seat.Top ? 1f : 0.5f);
                half.offsetMin = half.offsetMax = Vector2.zero;
                // Turned to face player 2 across the table.
                if (seat == Seat.Top) half.localEulerAngles = new Vector3(0f, 0f, 180f);
                controls.Build(half);
                var tag = UiKit.Label(half, seat == Seat.Top ? "P2" : "P1", 40f, seat == Seat.Top ? Theme.Cyan : Theme.Gold, TextAlignmentOptions.Center, true);
                UiKit.Place(tag.rectTransform, new Vector2(0.5f, 0.06f), new Vector2(160f, 60f));
                tag.raycastTarget = false;
            }
            return controls;
        }

        private bool _leftHanded;
        private float _scale = 1f;
        private Seat _seat;
        private ControlSlot[] _slots;

        private void Build(RectTransform safe)
        {
            // Joystick zone: the lower-left, generous so the thumb never has to aim.
            // 14 Oct 2026: the whole left half under the score bar, so the stick appears wherever the thumb goes.
            var zone = UiKit.NewRect("JoystickZone", safe);
            zone.anchorMin = new Vector2(_leftHanded ? 0.5f : 0f, 0f);
            zone.anchorMax = new Vector2(_leftHanded ? 1f : 0.5f, _seat == Seat.Full ? 0.8f : 0.92f);
            zone.offsetMin = zone.offsetMax = Vector2.zero;
            var zoneImage = zone.gameObject.AddComponent<Image>();
            zoneImage.color = new Color(0f, 0f, 0f, 0f); // invisible but receives touches
            zoneImage.raycastTarget = true;

            // Where the stick rests when nobody's touching it: a faint ring so you know where to put your thumb.
            var rest = UiKit.Panel(zone, new Color(1f, 1f, 1f, 0.22f), Theme.DiscSprite(new Color(0f, 0f, 0f, 0.2f), Theme.Cream), false, "StickRest");
            _stickRest = rest.rectTransform;
            _stickRest.anchorMin = _stickRest.anchorMax = new Vector2(_leftHanded ? 1f : 0f, 0f);
            _stickRest.sizeDelta = new Vector2(300f, 300f);
            _stickRest.anchoredPosition = new Vector2(_leftHanded ? -220f : 220f, 230f);
            rest.raycastTarget = false;

            var baseRing = UiKit.Panel(zone, new Color(1f, 1f, 1f, 0.35f), Theme.DiscSprite(new Color(0f, 0f, 0f, 0.25f), Theme.Cream), false, "StickBase");
            baseRing.rectTransform.anchorMin = baseRing.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            baseRing.rectTransform.sizeDelta = new Vector2(300f, 300f);
            var knob = UiKit.Panel(zone, Color.white, Theme.DiscSprite(Theme.Cream, Theme.Shadow), false, "StickKnob");
            knob.rectTransform.anchorMin = knob.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            knob.rectTransform.sizeDelta = new Vector2(130f, 130f);

            Joystick = zone.gameObject.AddComponent<VirtualJoystick>();
            Joystick.Init(zone, baseRing.rectTransform, knob.rectTransform);
            // Phase 39: Settings ► STICK SIZE and STICK DEAD ZONE.
            var settings = Core.App.Career?.settings;
            Joystick.Radius = ControlFeel.RadiusFor(settings);
            Joystick.DeadZone = ControlFeel.DeadZoneFor(settings);
            Joystick.Moved += active => _stickRest.gameObject.SetActive(!active);
            // The resting ring stays where the thumb last was, so it's easy to find again.
            Joystick.Lifted += at =>
            {
                _stickRest.anchorMin = _stickRest.anchorMax = new Vector2(0.5f, 0.5f);
                _stickRest.anchoredPosition = at;
            };

            var hint = UiKit.Label(_stickRest, "MOVE", 30f, new Color(1f, 1f, 1f, 0.45f), TextAlignmentOptions.Center, true);
            UiKit.Stretch(hint.rectTransform);
            hint.raycastTarget = false;

            // Action buttons, anchored to the lower-right corner of the safe area (lower-left when left-handed).
            Shoot = MakeButton(safe, ControlButton.Shoot, "SHOOT", ShootColor);
            Pass = MakeButton(safe, ControlButton.Pass, "PASS", PassColor);
            Dunk = MakeButton(safe, ControlButton.Dunk, "DUNK", DunkColor);
            Layup = MakeButton(safe, ControlButton.Layup, "LAYUP", LayupColor);
            Call = MakeButton(safe, ControlButton.Call, "CALL", CallColor);
            ApplyLayout(_slots);

            Shoot.Pressed += () => Press(ActionButton.Shoot);
            Pass.Pressed += () => Press(ActionButton.Pass);
            Dunk.Pressed += () => Press(_defense ? ActionButton.Defense : ActionButton.Dunk);
            Layup.Pressed += () => Press(ActionButton.Layup);
            Call.Pressed += () => Press(ActionButton.Call);
        }

        /// <summary>Places every button from a layout (the editor calls this while you drag).</summary>
        public void ApplyLayout(ControlSlot[] slots)
        {
            _slots = slots;
            foreach (var s in slots)
            {
                var b = this[s.Button];
                var rt = (RectTransform)b.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(_leftHanded ? 0f : 1f, 0f);
                rt.sizeDelta = new Vector2(s.Size * _scale, s.Size * _scale);
                rt.anchoredPosition = Corner(s.X * _scale, s.Y * _scale);
                b.SetFontScale(s.Size * _scale / ControlLayout.DefaultSize(s.Button));
            }
        }

        public ControlSlot[] Slots => _slots;
        public float Scale => _scale;
        public bool LeftHanded => _leftHanded;

        private void Press(ActionButton b) => _buffer.Press(b, Time.unscaledTime);

        /// <summary>Offset from the lower-right corner, mirrored to the lower-left for left-handed play.</summary>
        private Vector2 Corner(float x, float y) => _leftHanded ? new Vector2(-x, y) : new Vector2(x, y);

        private static readonly Dictionary<string, Sprite> Faces = new Dictionary<string, Sprite>();
        private static readonly Dictionary<ControlIcon, Sprite> Icons = new Dictionary<ControlIcon, Sprite>();

        public static Sprite Face(Color ring, bool pressed)
        {
            string key = ColorUtility.ToHtmlStringRGB(ring) + (pressed ? "p" : "");
            if (Faces.TryGetValue(key, out var s) && s != null) return s;
            var c32 = (Color32)ring;
            s = TextureFactory.ToSprite(UiSkinGenerator.RingButton(new RgbColor(c32.r, c32.g, c32.b), pressed), "ui.ring." + key);
            Faces[key] = s;
            return s;
        }

        public static Sprite IconSprite(ControlIcon icon)
        {
            if (Icons.TryGetValue(icon, out var s) && s != null) return s;
            s = TextureFactory.ToSprite(ControlIconGenerator.Generate(icon), "ui.icon." + icon);
            Icons[icon] = s;
            return s;
        }

        private static TouchActionButton MakeButton(RectTransform parent, ControlButton which, string text, Color color)
        {
            var image = UiKit.Panel(parent, Color.white, Face(color, false), false, "Btn " + text);
            image.raycastTarget = true;
            image.preserveAspect = true;
            var rt = image.rectTransform;
            rt.pivot = new Vector2(0.5f, 0.5f);

            // Icon in the upper part of the ring, label underneath it.
            var icon = UiKit.Panel(rt, Color.white, IconSprite(ControlIconGenerator.For(which, false)), false, "Icon");
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.rectTransform.anchorMin = new Vector2(0.27f, 0.36f);
            icon.rectTransform.anchorMax = new Vector2(0.73f, 0.82f);
            icon.rectTransform.offsetMin = icon.rectTransform.offsetMax = Vector2.zero;

            var label = UiKit.Label(rt, text, 30f, Theme.Cream, TextAlignmentOptions.Center, true);
            label.rectTransform.anchorMin = new Vector2(0.05f, 0.1f);
            label.rectTransform.anchorMax = new Vector2(0.95f, 0.36f);
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.enableAutoSizing = true;
            label.fontSizeMin = 14f;
            label.fontSizeMax = 30f;
            label.raycastTarget = false;
            UiKit.ApplyTextShadow(label);
            var hint = UiKit.Label(rt, "", 24f, Theme.Cream, TextAlignmentOptions.Center, true, "Hint");
            UiKit.Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(260f, 40f));
            hint.rectTransform.anchoredPosition = new Vector2(0f, 24f);

            var button = image.gameObject.AddComponent<TouchActionButton>();
            button.Init(image, label, hint);
            button.SetIcon(icon);
            button.SetSprites(Face(color, false), Face(color, true));
            return button;
        }

        /// <summary>Switches every button between its offence and defence role (labels, icons, ring colours).</summary>
        public void SetRole(bool defense, bool oop)
        {
            if (defense == _defense && oop == _oop && _roleSet) return;
            _roleSet = true;
            _defense = defense;
            _oop = oop && !defense;
            SetButton(Shoot, ControlButton.Shoot, defense ? "BLOCK" : "SHOOT", ShootColor);
            SetButton(Pass, ControlButton.Pass, defense ? "SWITCH" : (_oop ? "OOP" : "PASS"), _oop ? OopColor : PassColor);
            SetButton(Dunk, ControlButton.Dunk, defense ? "STEAL" : "DUNK", defense ? StealColor : DunkColor);
            SetButton(Layup, ControlButton.Layup, "LAYUP", LayupColor);
            // Nothing to lay up on defence, and no plays to call.
            Layup.gameObject.SetActive(!defense);
            Call.gameObject.SetActive(!defense);
        }

        private bool _roleSet;

        private void SetButton(TouchActionButton b, ControlButton which, string label, Color ring)
        {
            b.SetLabel(label);
            b.SetIconSprite(IconSprite(ControlIconGenerator.For(which, _defense, _oop)));
            b.SetSprites(Face(ring, false), Face(ring, true));
        }

        /// <summary>Labels for a button (e.g. SHOOT/CLEAR/WAIT).</summary>
        public void SetLabels(string shoot, string pass, string dunk)
        {
            Shoot.SetLabel(shoot);
            Pass.SetLabel(pass);
            Dunk.SetLabel(dunk);
        }

        /// <summary>Marks which actions are usable right now (dimmed + a short hint otherwise).</summary>
        public void SetAvailability(bool shoot, bool pass, bool dunk, bool layup, bool call)
        {
            Shoot.Available = shoot;
            Pass.Available = pass;
            Dunk.Available = dunk;
            Layup.Available = layup;
            Call.Available = call;
        }

        public void SetVisible(bool visible)
        {
            if (!visible)
            {
                Joystick.Release();
                Shoot.Release();
                Pass.Release();
                Dunk.Release();
                Layup.Release();
                Call.Release();
            }
            _canvas.enabled = visible;
        }

        /// <summary>Raw stick, screen-up = +y.</summary>
        public Vec2 ScreenStick
        {
            get
            {
                var v = Joystick.Value;
                // Player 2's controls are upside down: their "up" points down the screen.
                return _seat == Seat.Top ? new Vec2(-v.x, -v.y) : v;
            }
        }

        /// <summary>Stick direction in court space for the portrait view (+y away from the hoop = screen-down).</summary>
        public Vec2 CourtMove
        {
            get
            {
                var v = ScreenStick;
                return new Vec2(v.x, -v.y);
            }
        }
    }
}
