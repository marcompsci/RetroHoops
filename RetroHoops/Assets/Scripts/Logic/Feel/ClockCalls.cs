using System;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// 16 Oct 2026 improvement phase I16 CLOCK CALLS: the big moments of the clock get called on screen and by the
    /// crowd: LAST MINUTE, 10 SECONDS in a close game, and SUDDEN DEATH when the horn goes with the score tied (next
    /// basket wins). Called once each as the clock crosses the mark.
    /// </summary>
    public static class ClockCalls
    {
        public const float LastMinute = 60f;
        public const float FinalSeconds = 10f;
        /// <summary>Games shorter than this don't get the LAST MINUTE call (it would be most of the game).</summary>
        public const float MinGameForMinute = 100f;

        /// <param name="prev">Clock last frame.</param>
        /// <param name="gameLength">The game's full clock (seconds).</param>
        public static string For(float prev, float now, float gameLength, int scoreA, int scoreB, bool usesClock, bool suddenDeath, bool live)
        {
            if (!usesClock || !live || prev <= now) return null;
            if (prev > 0f && now <= 0f && suddenDeath && scoreA == scoreB) return "SUDDEN DEATH: NEXT BUCKET WINS";
            if (prev > FinalSeconds && now <= FinalSeconds && Math.Abs(scoreA - scoreB) <= 3) return "10 SECONDS!";
            if (prev > LastMinute && now <= LastMinute && gameLength >= MinGameForMinute) return "LAST MINUTE";
            return null;
        }
    }
}
