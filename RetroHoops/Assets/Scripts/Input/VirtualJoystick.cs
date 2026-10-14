using CallerRetroBall.Logic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CallerRetroBall.Controls
{
    /// <summary>
    /// Floating thumbstick: appears wherever the thumb lands inside its zone and tracks one
    /// finger (other touches are ignored, so buttons can be pressed at the same time).
    /// 14 Oct 2026 (Omari): it follows the thumb. Drag past the edge and the whole stick slides after your thumb, so
    /// it's always under it. When you let go, the resting ring stays where your thumb was.
    /// </summary>
    public sealed class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        /// <summary>Knob travel in canvas units.</summary>
        public float Radius = 130f;
        public float DeadZone = 0.12f;

        private RectTransform _zone;
        private RectTransform _base;
        private RectTransform _knob;
        private int _pointerId = int.MinValue;
        private Vector2 _origin;

        /// <summary>Current stick vector (magnitude 0..1). Screen-up is +y.</summary>
        public Vec2 Value { get; private set; }
        public bool IsActive => _pointerId != int.MinValue;
        /// <summary>Raised when the stick appears (true) or goes away (false).</summary>
        public event System.Action<bool> Moved;
        /// <summary>Where the stick was (zone-local) when the thumb lifted.</summary>
        public event System.Action<Vector2> Lifted;
        /// <summary>The stick's centre now (zone-local).</summary>
        public Vector2 Origin => _origin;

        public void Init(RectTransform zone, RectTransform baseRing, RectTransform knob)
        {
            _zone = zone;
            _base = baseRing;
            _knob = knob;
            SetVisible(false);
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (IsActive) return;
            _pointerId = e.pointerId;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_zone, e.position, e.pressEventCamera, out _origin);
            _base.anchoredPosition = _origin;
            _knob.anchoredPosition = _origin;
            SetVisible(true);
            UpdateValue(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId == _pointerId) UpdateValue(e);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != _pointerId) return;
            var at = _origin;
            Release();
            Lifted?.Invoke(at);
        }

        /// <summary>Drops the stick (e.g. when pausing or losing focus).</summary>
        public void Release()
        {
            _pointerId = int.MinValue;
            Value = Vec2.Zero;
            SetVisible(false);
        }

        private void OnDisable() => Release();

        private void UpdateValue(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_zone, e.position, e.pressEventCamera, out var local);
            var drag = new Vec2(local.x - _origin.x, local.y - _origin.y);
            // Follow the thumb: past the edge, the base slides along so the knob stays at the rim under the thumb.
            var follow = JoystickMath.FollowOrigin(new Vec2(_origin.x, _origin.y), new Vec2(local.x, local.y), Radius);
            if (follow.x != _origin.x || follow.y != _origin.y)
            {
                var r = _zone.rect;
                _origin = new Vector2(Mathf.Clamp(follow.x, r.xMin, r.xMax), Mathf.Clamp(follow.y, r.yMin, r.yMax));
                _base.anchoredPosition = _origin;
                drag = new Vec2(local.x - _origin.x, local.y - _origin.y);
            }
            Value = JoystickMath.Evaluate(drag, Radius, DeadZone);
            var knob = JoystickMath.KnobOffset(drag, Radius);
            _knob.anchoredPosition = _origin + new Vector2(knob.x, knob.y);
        }

        private void SetVisible(bool visible)
        {
            if (_base != null) _base.gameObject.SetActive(visible);
            if (_knob != null) _knob.gameObject.SetActive(visible);
            Moved?.Invoke(visible);
        }
    }
}
