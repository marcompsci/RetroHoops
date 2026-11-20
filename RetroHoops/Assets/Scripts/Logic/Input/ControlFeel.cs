using System;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// Phase 39 controls feel (Settings ► CONTROLS): how big the touch stick is, how far you push before it moves you,
    /// and how strong the haptics are. Each is a three-step choice so it's easy to pick without fiddling.
    /// </summary>
    public static class ControlFeel
    {
        public static readonly string[] SizeNames = { "SMALL", "NORMAL", "LARGE" };
        public static readonly string[] DeadZoneNames = { "LOW", "NORMAL", "HIGH" };
        public static readonly string[] HapticNames = { "LIGHT", "NORMAL", "STRONG" };

        /// <summary>Stick travel radius in canvas units (the old fixed value was 130).</summary>
        public static readonly float[] StickRadius = { 100f, 130f, 165f };
        /// <summary>Share of the travel ignored at the centre (the old fixed value was 0.12).</summary>
        public static readonly float[] DeadZone = { 0.06f, 0.12f, 0.2f };

        public static int Clamp(int step) => Math.Max(0, Math.Min(2, step));

        /// <summary>14 Oct 2026: touch controls opacity steps; 65% by default so the court shows through.</summary>
        public static readonly string[] OpacityNames = { "40%", "65%", "85%", "100%" };
        public static readonly float[] Opacity = { 0.4f, 0.65f, 0.85f, 1f };
        public const int DefaultOpacity = 1;
        public static int ClampOpacity(int step) => Math.Max(0, Math.Min(Opacity.Length - 1, step));
        public static float OpacityFor(SettingsData s) => Opacity[ClampOpacity(s?.controlsOpacity ?? DefaultOpacity)];

        public static float RadiusFor(SettingsData s) => StickRadius[Clamp(s?.stickSize ?? 1)];
        public static float DeadZoneFor(SettingsData s) => DeadZone[Clamp(s?.stickDeadZone ?? 1)];

        /// <summary>
        /// The impact style to play for a game event of <paramref name="baseStyle"/> (0 light, 1 medium, 2 heavy) at the
        /// chosen strength: LIGHT plays everything one step softer, STRONG one step firmer (never beyond light/heavy).
        /// </summary>
        public static int ImpactStyle(int baseStyle, int strength) => Math.Max(0, Math.Min(2, baseStyle + Clamp(strength) - 1));

        // ------------------------------------------------------------------ I133: how a button feels under the thumb

        /// <summary>A held button sinks to this size.</summary>
        public const float PressedScale = 0.9f;
        /// <summary>On release it springs past full size to this, then settles.</summary>
        public const float ReboundScale = 1.07f;
        public const float ReboundUp = 0.06f, ReboundSettle = 0.16f;

        /// <summary>The button's scale: sunk while held, a quick springy rebound after you let go.</summary>
        public static float PressScale(bool held, float secondsSinceRelease)
        {
            if (held) return PressedScale;
            float t = secondsSinceRelease;
            if (t < 0f || t >= ReboundSettle) return 1f;
            if (t < ReboundUp) return PressedScale + (ReboundScale - PressedScale) * (t / ReboundUp);
            return ReboundScale + (1f - ReboundScale) * ((t - ReboundUp) / (ReboundSettle - ReboundUp));
        }

        /// <summary>The white flash over a button right after it's pressed (alpha), gone in 0.15 s.</summary>
        public static float PressFlash(float secondsSincePress) =>
            secondsSincePress < 0f || secondsSincePress >= 0.15f ? 0f : 0.55f * (1f - secondsSincePress / 0.15f);

        // ------------------------------------------------------------------ I135: see the play under the buttons

        /// <summary>How see-through a button gets while a player or the ball is underneath it.</summary>
        public const float BusyAlpha = 0.3f;

        /// <summary>A button's own fade: clear while the play is under it, solid while your thumb is on it.</summary>
        public static float FadeAlpha(bool busy, bool held) => held ? 1f : busy ? BusyAlpha : 1f;

        /// <summary>Moves <paramref name="current"/> toward <paramref name="target"/> (a quick fade, ~0.12 s end to end).</summary>
        public static float FadeStep(float current, float target, float dt)
        {
            float step = Math.Max(0f, dt) / 0.12f;
            if (current < target) return Math.Min(target, current + step);
            return Math.Max(target, current - step);
        }

        // ------------------------------------------------------------------ I134 / I138: the STEAL button talks back

        /// <summary>The short hint over STEAL after a press (empty = nothing to say).</summary>
        public static string StealHint(StealOutcome o)
        {
            switch (o)
            {
                case StealOutcome.TooFar: return "TOO FAR";
                case StealOutcome.Short: return "GET CLOSER";
                case StealOutcome.Waiting: return "READY...";
                case StealOutcome.Missed: return "MISSED";
                default: return "";
            }
        }
    }
}
