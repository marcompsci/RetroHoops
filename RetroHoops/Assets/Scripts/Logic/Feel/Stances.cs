using System;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// 16 Oct 2026 graphics phase G2: which action frame a player shows when nothing flashier is going on. A passer
    /// holds the chest-pass frame for a beat; a defender near the ball handler crouches into a stance and shuffles
    /// (stance and slide frames alternating) while moving at a defender's pace; anyone sprinting runs normally.
    /// Drawing only: the simulation never reads it.
    /// </summary>
    public static class Stances
    {
        /// <summary>How close (metres) to the ball handler a defender gets down in a stance.</summary>
        public const float GuardRange = 2.4f;
        /// <summary>Faster than this (m/s) a defender is running, not sliding.</summary>
        public const float SlideSpeed = 3.0f;
        /// <summary>Below this (m/s) the defender is set, not shuffling.</summary>
        public const float SetSpeed = 0.25f;
        /// <summary>How long the chest-pass frame holds after the ball leaves the hands.</summary>
        public const float PassPoseSeconds = 0.24f;
        /// <summary>Shuffle steps per second (stance and slide frames alternating).</summary>
        public const float ShuffleFps = 7f;

        public static PoseFrame For(bool live, bool onDefense, float distToHandler, float speed, bool passing, float animTime)
        {
            if (passing) return PoseFrame.Pass;
            if (!live || !onDefense || distToHandler > GuardRange || speed > SlideSpeed) return PoseFrame.None;
            if (speed < SetSpeed) return PoseFrame.Defense;
            return (int)(Math.Max(0f, animTime) * ShuffleFps) % 2 == 0 ? PoseFrame.Slide : PoseFrame.Defense;
        }
    }
}
