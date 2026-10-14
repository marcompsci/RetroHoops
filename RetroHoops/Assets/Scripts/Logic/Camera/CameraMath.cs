using System;

namespace CallerRetroBall.Logic
{
    /// <summary>Pure camera maths: smoothing, court clamping, and crisp integer pixel zoom.</summary>
    public static class CameraMath
    {
        /// <summary>Frame-rate independent exponential follow.</summary>
        public static Vec2 Follow(Vec2 current, Vec2 target, float dt, float stiffness)
        {
            if (dt <= 0f) return current;
            float t = 1f - (float)Math.Exp(-stiffness * dt);
            return Vec2.Lerp(current, target, t);
        }

        /// <summary>
        /// Clamps a view centre so the view stays within [min, max] (+margin). If the view is
        /// larger than the area on an axis, it centres on that axis instead.
        /// </summary>
        public static Vec2 ClampView(Vec2 center, float halfViewWidth, float halfViewHeight,
                                     Vec2 areaMin, Vec2 areaMax, float margin = 0f)
        {
            return new Vec2(ClampAxis(center.x, halfViewWidth, areaMin.x - margin, areaMax.x + margin),
                            ClampAxis(center.y, halfViewHeight, areaMin.y - margin, areaMax.y + margin));
        }

        private static float ClampAxis(float c, float half, float min, float max)
        {
            if (half * 2f >= max - min) return (min + max) * 0.5f;
            if (c - half < min) return min + half;
            if (c + half > max) return max - half;
            return c;
        }

        /// <summary>
        /// Largest whole-number scale at which <paramref name="minVisibleUnits"/> world units
        /// (at <paramref name="pixelsPerUnit"/> art pixels each) still fit across the screen.
        /// Keeps pixel art crisp: every art pixel maps to N×N screen pixels.
        /// </summary>
        public static int IntegerZoom(int screenPixels, float minVisibleUnits, float pixelsPerUnit)
        {
            if (screenPixels <= 0 || minVisibleUnits <= 0f || pixelsPerUnit <= 0f) return 1;
            int zoom = (int)Math.Floor(screenPixels / (minVisibleUnits * pixelsPerUnit));
            return Math.Max(1, zoom);
        }

        /// <summary>
        /// Largest integer zoom that shows at least <paramref name="minVisibleWidth"/> across AND
        /// <paramref name="minVisibleHeight"/> top to bottom. Phones are limited by width; wider screens
        /// (iPad, a Mac window) by height, so the court still fits above the thumb zone.
        /// </summary>
        public static int IntegerZoom(int screenWidth, int screenHeight, float minVisibleWidth, float minVisibleHeight, float pixelsPerUnit)
        {
            int byWidth = IntegerZoom(screenWidth, minVisibleWidth, pixelsPerUnit);
            if (minVisibleHeight <= 0f) return byWidth;
            return Math.Min(byWidth, IntegerZoom(screenHeight, minVisibleHeight, pixelsPerUnit));
        }

        /// <summary>
        /// CanvasScaler match for a screen aspect (width / height). Phones taller than 9:16 favour width
        /// so buttons stay big. Anything wider (iPad 3:4, a Mac window) matches height, so the 1080x1920
        /// layout never runs off the top or bottom. (At exactly 9:16 every match gives the same scale,
        /// so there's no jump between the two.)
        /// </summary>
        public static float UiMatch(float aspect) => aspect <= 1080f / 1920f ? 0.35f : 1f;

        /// <summary>
        /// 14 Oct 2026: the integer zoom at which <paramref name="artHeight"/> (m) fills the screen below a HUD strip of
        /// <paramref name="hudShare"/> of its height (rounded down, so the court reaches at least to the bottom).
        /// </summary>
        public static int FillZoom(int screenHeight, float artHeight, float hudShare, float pixelsPerUnit)
        {
            if (screenHeight <= 0 || artHeight <= 0f || pixelsPerUnit <= 0f) return 1;
            float visible = artHeight / Math.Max(0.5f, 1f - hudShare);
            return Math.Max(1, (int)Math.Floor(screenHeight / (visible * pixelsPerUnit)));
        }

        /// <summary>Orthographic size (half height in world units) for a given integer zoom.</summary>
        public static float OrthographicSize(int screenHeightPixels, int zoom, float pixelsPerUnit)
        {
            return screenHeightPixels / (2f * zoom * pixelsPerUnit);
        }
    }
}
