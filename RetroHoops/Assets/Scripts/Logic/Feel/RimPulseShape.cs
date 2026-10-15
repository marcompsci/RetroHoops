using System;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// 15 Oct 2026 (Omari): THE STREETS rim pulse. When a dunk goes down, rings of energy burst out from the rim:
    /// pixel ellipses (the rim's plane seen at the 3/4 angle) that grow and fade. Pure, so the shapes and timing are
    /// tested; the view (RimPulse) draws them.
    /// </summary>
    public static class RimPulseShape
    {
        /// <summary>Rings are this much flatter than wide (the rim seen from the 3/4 camera).</summary>
        public const float Flatten = 0.55f;
        public const int Frames = 10;
        public const float Life = 0.42f;
        /// <summary>A second (and on a SHOWSTOPPER, third) ring follows this long after the one before.</summary>
        public const float Stagger = 0.08f;

        /// <summary>Radius (art pixels) of frame <paramref name="frame"/> for a pulse that grows to <paramref name="maxRadius"/>.</summary>
        public static int RadiusAt(int frame, int maxRadius)
        {
            frame = Math.Max(0, Math.Min(Frames - 1, frame));
            float u = frame / (float)(Frames - 1);
            // Fast out, slowing: an energy wave, not a balloon.
            float e = 1f - (1f - u) * (1f - u);
            return Math.Max(2, (int)Math.Round(2 + (maxRadius - 2) * e));
        }

        /// <summary>The frame to show <paramref name="t"/> seconds in, or -1 when the ring is gone.</summary>
        public static int FrameAt(float t) => t < 0f || t >= Life ? -1 : Math.Min(Frames - 1, (int)(t / Life * Frames));

        /// <summary>How bright the ring is <paramref name="t"/> seconds in (1 at the burst, 0 at the end).</summary>
        public static float AlphaAt(float t) => t < 0f || t >= Life ? 0f : 1f - (t / Life) * (t / Life);

        /// <summary>
        /// A ring of <paramref name="thickness"/> pixels: an ellipse of radius <paramref name="radius"/> across and
        /// <see cref="Flatten"/> × that up and down. [x, y] in a (2r+1) × (2·ry+1) grid.
        /// </summary>
        public static bool[,] Ring(int radius, int thickness)
        {
            radius = Math.Max(1, radius);
            int ry = Math.Max(1, (int)Math.Round(radius * Flatten));
            int w = radius * 2 + 1, h = ry * 2 + 1;
            var grid = new bool[w, h];
            float t = Math.Max(1, thickness);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - radius) / (float)radius, dy = (y - ry) / (float)ry;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy); // 1 on the ellipse
                    float inner = 1f - t / Math.Max(1f, ry);
                    grid[x, y] = d <= 1.0f + 0.5f / radius && d >= inner;
                }
            }
            return grid;
        }
    }
}
