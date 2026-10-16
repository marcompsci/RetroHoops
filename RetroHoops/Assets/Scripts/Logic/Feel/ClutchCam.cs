using System;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// 16 Oct 2026 improvement phase I7 CLUTCH CAM: a shot that could decide the game hangs in the air in slow motion.
    /// "Could decide" = it would reach the target score, or it's the last few seconds and it would tie the game or put
    /// the shooting team ahead. The outcome is never peeked at (a miss slows down just the same). The simulation runs
    /// the same steps at a slower real-time pace, so nothing about the result changes.
    /// </summary>
    public static class ClutchCam
    {
        /// <summary>Real-time speed while it's on.</summary>
        public const float Scale = 0.45f;
        /// <summary>Game clock (seconds) under which a tying or go-ahead shot counts.</summary>
        public const float ClockWindow = 4f;

        public static bool Active(bool live, bool shotInAir, int shootingTeam, int[] score, int shotPoints,
                                  int targetScore, bool usesClock, float clock)
        {
            if (!live || !shotInAir || score == null || score.Length < 2 || shootingTeam < 0 || shootingTeam > 1 || shotPoints <= 0) return false;
            int mine = score[shootingTeam], theirs = score[1 - shootingTeam];
            if (targetScore > 0 && mine < targetScore && mine + shotPoints >= targetScore) return true;
            if (usesClock && clock <= ClockWindow && mine <= theirs && mine + shotPoints >= theirs) return true;
            return false;
        }
    }
}
