using CallerRetroBall.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// Buttons press in a little and come straight back (off with Reduce Motion).
    /// 28 Oct 2026 (Emil Kowalski audit): the press is a subtle 0.96, not 0.92, and the release no longer overshoots:
    /// a bounce on every tap reads as a gimmick on something you press hundreds of times. A stiff, critically damped
    /// spring settles in about 120 ms and can be interrupted mid-press without a jump.
    /// </summary>
    public sealed class ButtonPop : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private const float Pressed = 0.96f;
        private const float Stiffness = 1100f;
        private static readonly float Damping = 2f * Mathf.Sqrt(Stiffness); // critically damped: no overshoot

        private float _target = 1f;
        private float _scale = 1f;
        private float _velocity;

        public void OnPointerDown(PointerEventData e) => _target = UiMotion.Reduced ? 1f : Pressed;
        public void OnPointerUp(PointerEventData e) => _target = 1f;
        public void OnPointerExit(PointerEventData e) => _target = 1f;

        private void OnDisable()
        {
            _target = _scale = 1f;
            _velocity = 0f;
            transform.localScale = Vector3.one;
        }

        private void Update()
        {
            if (Mathf.Approximately(_scale, _target) && Mathf.Abs(_velocity) < 0.001f) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            // Two half-steps keep the stiff spring stable at 30 fps.
            for (int i = 0; i < 2; i++)
            {
                float h = dt * 0.5f;
                float accel = (_target - _scale) * Stiffness - _velocity * Damping;
                _velocity += accel * h;
                _scale += _velocity * h;
            }
            if (Mathf.Abs(_scale - _target) < 0.0005f && Mathf.Abs(_velocity) < 0.01f) { _scale = _target; _velocity = 0f; }
            transform.localScale = new Vector3(_scale, _scale, 1f);
        }
    }

    /// <summary>
    /// Modal panels (menus, dialogs) fade in from 97% with the strong ease-out, and their scrim fades in under them.
    /// 28 Oct 2026 (Emil Kowalski audit): was a 90% scale-only pop with the scrim snapping on; menus open tens of times
    /// a session, so the entrance is now quieter (97%, 200 ms) and centred, as a modal should be. Reduce Motion: fade only.
    /// </summary>
    public sealed class OverlayPop : MonoBehaviour
    {
        private void OnEnable()
        {
            UiMotion.Enter(this, UiMotion.Modal, Vector2.zero, 0.97f);
            // The scrim behind the panel (a sibling or a sibling of a parent named "Scrim") fades in with it.
            var scrim = FindScrim();
            if (scrim != null) UiMotion.Enter(scrim, UiMotion.Modal * 0.8f, Vector2.zero, 1f);
            enabled = false;
        }

        private Transform FindScrim()
        {
            for (var t = transform.parent; t != null; t = t.parent)
            {
                var s = t.Find("Scrim");
                if (s != null) return s;
                if (t.GetComponent<Canvas>() != null) break;
            }
            return null;
        }
    }
}
