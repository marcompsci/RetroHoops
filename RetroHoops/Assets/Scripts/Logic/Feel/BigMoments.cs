using System;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// 16 Oct 2026 improvement phase I1 BIG MOMENTS (from what players ask for in App Store basketball reviews: teammates
    /// mobbing the game-winner, a crowd that reacts and chants). Drawing and sound only: the simulation never reads it.
    /// </summary>
    public static class GameWinnerMob
    {
        /// <summary>How long the final whistle waits while the team celebrates before the result screen.</summary>
        public const float Seconds = 2.4f;
        /// <summary>How long teammates take to reach the shooter.</summary>
        public const float ArriveSeconds = 0.55f;
        /// <summary>How close to the end the winning basket must fall to count as the game-winner (seconds).</summary>
        public const float Window = 0.75f;
        /// <summary>Game clock (seconds) at release for a buzzer-beater.</summary>
        public const float BuzzerClock = 1.5f;
        /// <summary>Ring the mob forms round the shooter (metres).</summary>
        public const float Radius = 0.8f;

        /// <summary>Was the game decided by the last basket (made just before the game ended, by the winners)?</summary>
        public static bool IsGameWinner(int winner, int madeTeam, float madeAt, float overAt) =>
            winner >= 0 && madeTeam == winner && overAt - madeAt >= -0.01f && overAt - madeAt <= Window;

        /// <summary>A buzzer-beater: the clock was nearly out when the winning shot left the hand.</summary>
        public static bool IsBuzzerBeater(bool usesClock, float clockAtRelease) => usesClock && clockAtRelease <= BuzzerClock;

        public static string Title(bool buzzer) => buzzer ? "BUZZER BEATER!" : "GAME WINNER!";

        /// <summary>
        /// Where a teammate is <paramref name="t"/> seconds into the celebration: running from where he was to his spot
        /// in a ring round the shooter, then jumping up and down with his arms up.
        /// </summary>
        /// <param name="slot">This teammate's place in the ring (0..count-1).</param>
        public static void Pose(Vec2 from, Vec2 shooter, int slot, int count, float t, out Vec2 ground, out int liftPx, out bool armsUp)
        {
            count = Math.Max(1, count);
            double angle = (slot + 0.5) / count * Math.PI * 2.0 + 0.6;
            var spot = new Vec2(shooter.x + (float)Math.Cos(angle) * Radius, shooter.y + (float)Math.Sin(angle) * Radius * 0.7f);
            float u = ArriveSeconds <= 0f ? 1f : Math.Max(0f, Math.Min(1f, t / ArriveSeconds));
            float ease = 1f - (1f - u) * (1f - u);
            ground = new Vec2(from.x + (spot.x - from.x) * ease, from.y + (spot.y - from.y) * ease);
            if (u < 1f)
            {
                liftPx = 0;
                armsUp = false;
                return;
            }
            float hop = (float)Math.Abs(Math.Sin((t - ArriveSeconds) * 9.0 + slot * 1.3));
            liftPx = (int)Math.Round(hop * 5f);
            armsUp = true;
        }
    }

    public enum CrowdChant { None = 0, Defense = 1, Mvp = 2, LetsGo = 3 }

    /// <summary>
    /// The home crowd (your side) chants when the game calls for it: DE-FENSE when the other team is about to win or
    /// it's tight late, MVP for a heated-up scorer of yours with the ball, LET'S GO when you're down by four or more.
    /// One chant at a time, with a rest between.
    /// </summary>
    public static class CrowdChants
    {
        public const float Every = 9f;
        public const float Lasts = 2.4f;
        public const int TrailBy = 4;

        /// <param name="home">The crowd's team (yours).</param>
        /// <param name="pointsToWin">Points each team still needs (0 or less = not a points game).</param>
        /// <param name="clock">Game clock left (seconds), or a negative number for untimed games.</param>
        public static CrowdChant For(bool live, int home, int offense, int[] score, int[] pointsToWin, float clock, bool homeHeatedWithBall)
        {
            if (!live || score == null || score.Length < 2 || home < 0 || home > 1) return CrowdChant.None;
            int away = 1 - home;
            bool defending = offense == away;
            bool awayAtGamePoint = pointsToWin != null && pointsToWin.Length > away && pointsToWin[away] > 0 && pointsToWin[away] <= 3;
            bool tightLate = clock >= 0f && clock <= 30f && Math.Abs(score[0] - score[1]) <= 3;
            if (defending && (awayAtGamePoint || tightLate)) return CrowdChant.Defense;
            if (!defending && homeHeatedWithBall) return CrowdChant.Mvp;
            if (score[away] - score[home] >= TrailBy) return CrowdChant.LetsGo;
            return CrowdChant.None;
        }

        public static string Text(CrowdChant c, string teamName)
        {
            switch (c)
            {
                case CrowdChant.Defense: return "DE-FENSE!  DE-FENSE!";
                case CrowdChant.Mvp: return "M-V-P!  M-V-P!";
                case CrowdChant.LetsGo: return "LET'S GO " + (string.IsNullOrEmpty(teamName) ? "HOME" : teamName.ToUpperInvariant()) + "!";
                default: return "";
            }
        }
    }
}
