using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CallerRetroBall.Controls
{
    /// <summary>
    /// Round action button that reports press and release separately (hold-to-shoot needs
    /// both). When an action isn't available it still responds with a dimmed flash and a
    /// short label, instead of a popup.
    /// </summary>
    public sealed class TouchActionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public event Action Pressed;
        public event Action Released;

        private Image _image;
        private TextMeshProUGUI _label;
        private TextMeshProUGUI _hint;
        private int _pointerId = int.MinValue;
        private float _hintUntil;
        private Color _baseColor = Color.white;
        private Sprite _upSprite, _downSprite;

        public bool IsHeld => _pointerId != int.MinValue;
        public bool Available { get; set; } = true;
        /// <summary>Short label flashed when pressed while unavailable.</summary>
        public string UnavailableHint { get; set; } = "SOON";

        public void Init(Image image, TextMeshProUGUI label, TextMeshProUGUI hint)
        {
            _image = image;
            _label = label;
            _hint = hint;
            _baseColor = image.color;
            if (_hint != null) _hint.text = "";
        }

        /// <summary>Raised and pushed-in faces; the button sinks while held.</summary>
        public void SetSprites(Sprite up, Sprite down)
        {
            _upSprite = up;
            _downSprite = down;
            if (_image != null) _image.sprite = IsHeld && down != null ? down : up;
        }

        private Image _icon;

        /// <summary>The pixel icon drawn above the label.</summary>
        public void SetIcon(Image icon) => _icon = icon;

        public void SetIconSprite(Sprite sprite)
        {
            if (_icon != null && _icon.sprite != sprite) _icon.sprite = sprite;
        }

        /// <summary>Kept for layouts that resize buttons; labels auto-size to the ring.</summary>
        public void SetFontScale(float scale) { }

        public void SetLabel(string text)
        {
            text = Logic.Loc.T(text);
            if (_label != null && _label.text != text) _label.text = text;
        }

        // ------------------------------------------------------------------ I133-I135 (19 Nov 2026)

        private Image _flash, _cooldown;
        private CanvasGroup _group;
        private float _pressedAt = -10f, _releasedAt = -10f;
        private bool _busy;
        private float _alpha = 1f;

        /// <summary>The white flash drawn over the ring right after a press, and the dark sweep of a cooldown.</summary>
        public void SetOverlays(Image flash, Image cooldown)
        {
            _flash = flash;
            _cooldown = cooldown;
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            if (_flash != null) _flash.color = new Color(1f, 1f, 1f, 0f);
            if (_cooldown != null) _cooldown.fillAmount = 0f;
        }

        /// <summary>I135: a player or the ball is under this button (it fades so you can see the play).</summary>
        public bool Busy { get => _busy; set => _busy = value; }

        /// <summary>I134: 0 = ready, 1 = just used (a dark sweep around the ring that empties as it recovers).</summary>
        public void SetCooldown(float remaining01)
        {
            if (_cooldown == null) return;
            float f = Mathf.Clamp01(remaining01);
            if (!Mathf.Approximately(_cooldown.fillAmount, f)) _cooldown.fillAmount = f;
        }

        /// <summary>Shows a short hint above the button (I134: TOO FAR, GET CLOSER...).</summary>
        public void ShowHint(string text, float seconds = 0.8f)
        {
            if (_hint == null || string.IsNullOrEmpty(text)) return;
            _hint.text = Logic.Loc.T(text);
            _hintUntil = Time.unscaledTime + seconds;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (IsHeld) return;
            _pointerId = e.pointerId;
            _pressedAt = Time.unscaledTime;
            // I133: a quick, light tick under the thumb on every press (Settings ► HAPTICS turns it off).
            Core.Haptics.Light();
            if (_downSprite != null) _image.sprite = _downSprite;
            if (!Available)
            {
                _hintUntil = Time.unscaledTime + 0.8f;
                if (_hint != null) _hint.text = Logic.Loc.T(UnavailableHint);
            }
            Pressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != _pointerId) return;
            Release();
        }

        public void Release()
        {
            bool wasHeld = IsHeld;
            _pointerId = int.MinValue;
            if (wasHeld) _releasedAt = Time.unscaledTime;
            else transform.localScale = Vector3.one;
            if (_upSprite != null && _image != null) _image.sprite = _upSprite;
            if (wasHeld) Released?.Invoke();
        }

        private void OnDisable() => Release();

        private void Update()
        {
            var c = _baseColor;
            if (!Available) c = new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f, 0.75f);
            _image.color = c;
            if (_icon != null) _icon.color = Available ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            if (_hint != null && _hint.text.Length > 0 && Time.unscaledTime > _hintUntil) _hint.text = "";
            float now = Time.unscaledTime;
            // I133: sink while held, spring back past full size on release, and a white flash on the press.
            float k = Logic.ControlFeel.PressScale(IsHeld, now - _releasedAt);
            if (Mathf.Abs(transform.localScale.x - k) > 0.001f) transform.localScale = new Vector3(k, k, 1f);
            if (_flash != null)
            {
                float a = Logic.ControlFeel.PressFlash(now - _pressedAt);
                if (Mathf.Abs(_flash.color.a - a) > 0.004f) _flash.color = new Color(1f, 1f, 1f, a);
            }
            // I135: see-through while the play is underneath, solid again under your thumb.
            if (_group != null)
            {
                _alpha = Logic.ControlFeel.FadeStep(_alpha, Logic.ControlFeel.FadeAlpha(_busy, IsHeld), Time.unscaledDeltaTime);
                if (Mathf.Abs(_group.alpha - _alpha) > 0.003f) _group.alpha = _alpha;
            }
        }
    }
}
