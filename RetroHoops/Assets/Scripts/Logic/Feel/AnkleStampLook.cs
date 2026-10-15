using System;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// 15 Oct 2026 (Omari): the THE STREETS ANKLE BREAKER stamp's animation, as a pure function of time (tested):
    /// the band wipes in, the words slam down with an overshoot and shake, the victim's line slides in, then it all
    /// whips off upward and fades. With Reduce Motion it simply shows and fades.
    /// </summary>
    public struct AnkleStampLook
    {
        public float BandWidth, WordScale, ShakeX, ShakeY, SubX, ExitY, Alpha;

        public const float BandIn = 0.12f, SlamStart = 0.06f, SlamEnd = 0.18f, Settle = 0.26f, ShakeEnd = 0.5f,
                           SubStart = 0.2f, SubEnd = 0.4f, ExitShare = 0.2f;

        public static AnkleStampLook At(float t, float total, bool reduceMotion)
        {
            var k = new AnkleStampLook { BandWidth = 1f, WordScale = 1f, Alpha = 1f };
            float exitStart = total * (1f - ExitShare);
            if (t < 0f || t >= total) { k.Alpha = 0f; return k; }
            if (reduceMotion)
            {
                if (t > exitStart) k.Alpha = 1f - (t - exitStart) / (total - exitStart);
                return k;
            }
            // The band wipes across.
            k.BandWidth = t >= BandIn ? 1f : 1f - (1f - t / BandIn) * (1f - t / BandIn);
            // The slam: huge, then down past its size, then settled.
            if (t < SlamStart) k.WordScale = 0f;
            else if (t < SlamEnd) k.WordScale = Lerp(2.6f, 0.92f, (t - SlamStart) / (SlamEnd - SlamStart));
            else if (t < Settle) k.WordScale = Lerp(0.92f, 1f, (t - SlamEnd) / (Settle - SlamEnd));
            // The shake after the impact, dying out.
            if (t >= SlamEnd && t < ShakeEnd)
            {
                float d = 1f - (t - SlamEnd) / (ShakeEnd - SlamEnd);
                k.ShakeX = (float)Math.Round(Math.Sin(t * 95.0) * 12.0 * d);
                k.ShakeY = (float)Math.Round(Math.Cos(t * 77.0) * 6.0 * d);
            }
            // The victim's line slides in from the left.
            k.SubX = t < SubStart ? -900f : t < SubEnd ? Lerp(-900f, 0f, Ease((t - SubStart) / (SubEnd - SubStart))) : 0f;
            // Whipped off upward.
            if (t > exitStart)
            {
                float u = (t - exitStart) / (total - exitStart);
                k.ExitY = 420f * u * u;
                k.Alpha = 1f - u;
            }
            return k;
        }

        private static float Lerp(float a, float b, float u) => a + (b - a) * Math.Max(0f, Math.Min(1f, u));
        private static float Ease(float u) => 1f - (1f - u) * (1f - u) * (1f - u);
    }
}
