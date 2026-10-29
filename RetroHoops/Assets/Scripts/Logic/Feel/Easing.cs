using System;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// 28 Oct 2026 (Emil Kowalski audit): the app's easing curves as CSS-style cubic beziers, engine-free so they're
    /// tested. UI.UiMotion uses these for every transition.
    /// </summary>
    public static class Easing
    {
        /// <summary>cubic-bezier(0.23, 1, 0.32, 1): strong ease-out for anything entering or exiting.</summary>
        public static float Out(float t) => Bezier(0.23f, 1f, 0.32f, 1f, t);

        /// <summary>cubic-bezier(0.77, 0, 0.175, 1): strong ease-in-out for movement on screen.</summary>
        public static float InOut(float t) => Bezier(0.77f, 0f, 0.175f, 1f, t);

        /// <summary>cubic-bezier(0.32, 0.72, 0, 1): the iOS-like drawer curve for sheets.</summary>
        public static float Drawer(float t) => Bezier(0.32f, 0.72f, 0f, 1f, t);

        /// <summary>A cubic bezier from (0,0) to (1,1) with control points (x1,y1), (x2,y2), at time t (0..1).</summary>
        public static float Bezier(float x1, float y1, float x2, float y2, float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            // Bisection on x(u) = t (x is monotonic for 0 ≤ x1, x2 ≤ 1), then y(u).
            float lo = 0f, hi = 1f, u = t;
            for (int i = 0; i < 28; i++)
            {
                float x = Cubic(x1, x2, u);
                if (Math.Abs(x - t) < 1e-6f) break;
                if (x < t) lo = u; else hi = u;
                u = (lo + hi) * 0.5f;
            }
            return Cubic(y1, y2, u);
        }

        private static float Cubic(float p1, float p2, float u)
        {
            float v = 1f - u;
            return 3f * v * v * u * p1 + 3f * v * u * u * p2 + u * u * u;
        }
    }
}
