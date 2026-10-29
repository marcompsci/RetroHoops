using System;
using CallerRetroBall.Core;
using UnityEngine;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// 28 Oct 2026 (Omari: "audit my UI against Emil Kowalski's design engineering skill"): the app's motion vocabulary,
    /// one place for every curve and duration so the whole game moves the same way.
    /// - Entering and exiting use a strong ease-out (starts fast, so it answers the tap at once); never ease-in.
    /// - Things moving on screen (map zoom, the screen wipe) use a strong ease-in-out.
    /// - Sheets that rise from the bottom use the iOS drawer curve.
    /// - UI stays under 300 ms; exits are quicker than entrances.
    /// - Nothing appears from scale 0: entrances start at 0.96–0.97 with opacity 0.
    /// - Reduce Motion keeps the fades (they help you follow what changed) and drops the movement.
    /// </summary>
    public static class UiMotion
    {
        // Durations (seconds).
        public const float Press = 0.12f;
        public const float Toast = 0.18f, ToastOut = 0.14f;
        public const float Modal = 0.2f, ModalOut = 0.14f;
        public const float Sheet = 0.26f, SheetOut = 0.18f;
        public const float Page = 0.16f;
        public const float Move = 0.3f;

        /// <summary>cubic-bezier(0.23, 1, 0.32, 1): the strong ease-out for anything entering or exiting.</summary>
        public static float Out(float t) => Logic.Easing.Out(t);

        /// <summary>cubic-bezier(0.77, 0, 0.175, 1): the strong ease-in-out for things moving on screen.</summary>
        public static float InOut(float t) => Logic.Easing.InOut(t);

        /// <summary>cubic-bezier(0.32, 0.72, 0, 1): the iOS-like drawer curve for sheets.</summary>
        public static float Drawer(float t) => Logic.Easing.Drawer(t);

        public static bool Reduced => App.Career != null && App.Career.settings != null && App.Career.settings.reduceMotion;

        /// <summary>
        /// Numbers that change every frame (timers, speed) in fixed-width digits so they don't jitter side to side
        /// (TextMesh Pro's mspace: the web's tabular-nums).
        /// </summary>
        public static string Tabular(string text) => string.IsNullOrEmpty(text) ? text : "<mspace=0.58em>" + text + "</mspace>";

        // ------------------------------------------------------------------ helpers

        /// <summary>Fades and slides a UI element in (from <paramref name="offset"/> and <paramref name="scale"/>).</summary>
        public static UiTween Enter(Component target, float seconds, Vector2 offset, float scale = 1f, Func<float, float> ease = null) =>
            UiTween.Run(target.gameObject, seconds, ease ?? Out, from: new UiTween.State { Alpha = 0f, Offset = offset, Scale = scale },
                        to: new UiTween.State { Alpha = 1f, Offset = Vector2.zero, Scale = 1f }, done: null);

        /// <summary>Fades and slides a UI element out, then destroys it (exits are quicker than entrances).</summary>
        public static UiTween ExitAndDestroy(GameObject target, float seconds, Vector2 offset, float scale = 1f, Func<float, float> ease = null)
        {
            if (target == null) return null;
            // On its way out it no longer takes taps.
            var group = target.GetComponent<CanvasGroup>() ?? target.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            return UiTween.Run(target, seconds, ease ?? Out, from: null, to: new UiTween.State { Alpha = 0f, Offset = offset, Scale = scale },
                               done: () => { if (target != null) UnityEngine.Object.Destroy(target); });
        }
    }

    /// <summary>
    /// One tween on a UI element: opacity (a CanvasGroup), a position offset and a scale, on unscaled time. Starting a
    /// new tween on the same element retargets it from wherever it is now (interruptible, like a CSS transition).
    /// Reduce Motion keeps the fade and drops the movement.
    /// </summary>
    public sealed class UiTween : MonoBehaviour
    {
        public struct State
        {
            public float Alpha, Scale;
            public Vector2 Offset;
        }

        private CanvasGroup _group;
        private RectTransform _rect;
        private Vector2 _home;
        private bool _homeSet;
        private State _from, _to, _now;
        private float _start, _seconds;
        private Func<float, float> _ease;
        private Action _done;

        public static UiTween Run(GameObject go, float seconds, Func<float, float> ease, State? from, State to, Action done)
        {
            var t = go.GetComponent<UiTween>() ?? go.AddComponent<UiTween>();
            t.Begin(seconds, ease, from, to, done);
            return t;
        }

        private void Begin(float seconds, Func<float, float> ease, State? from, State to, Action done)
        {
            if (_group == null) _group = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            if (_rect == null) _rect = transform as RectTransform;
            if (!_homeSet && _rect != null) { _home = _rect.anchoredPosition; _homeSet = true; }
            if (!enabled || _seconds <= 0f) _now = new State { Alpha = _group.alpha, Scale = transform.localScale.x, Offset = _rect != null ? _rect.anchoredPosition - _home : Vector2.zero };
            _from = from ?? _now;
            _to = to;
            if (UiMotion.Reduced)
            {
                // Fades only: no slide, no scale.
                _from.Offset = _to.Offset = Vector2.zero;
                _from.Scale = _to.Scale = 1f;
            }
            _start = Time.unscaledTime;
            _seconds = Mathf.Max(0.01f, seconds);
            _ease = ease ?? UiMotion.Out;
            _done = done;
            enabled = true;
            Apply(0f);
        }

        private void Update()
        {
            float t = Mathf.Clamp01((Time.unscaledTime - _start) / _seconds);
            Apply(_ease(t));
            if (t < 1f) return;
            enabled = false;
            _seconds = 0f;
            var done = _done;
            _done = null;
            done?.Invoke();
        }

        private void Apply(float e)
        {
            _now.Alpha = Mathf.LerpUnclamped(_from.Alpha, _to.Alpha, e);
            _now.Scale = Mathf.LerpUnclamped(_from.Scale, _to.Scale, e);
            _now.Offset = Vector2.LerpUnclamped(_from.Offset, _to.Offset, e);
            _group.alpha = Mathf.Clamp01(_now.Alpha);
            transform.localScale = new Vector3(_now.Scale, _now.Scale, 1f);
            if (_rect != null) _rect.anchoredPosition = _home + _now.Offset;
        }
    }
}
