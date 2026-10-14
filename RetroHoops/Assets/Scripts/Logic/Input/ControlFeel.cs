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
    }
}
