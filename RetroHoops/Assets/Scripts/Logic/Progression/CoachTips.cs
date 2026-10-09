using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>What a coach tip can react to, read from the match each moment.</summary>
    public struct TipSituation
    {
        public bool Live;
        public bool HumanHasBall;
        public bool OnDefense;
        public bool MustClear;
        public bool AlleyOopOpen;
        public int HumanHotStreak;
        public int HeatThreshold;
        public bool LastShotTooEarly;
        public bool LastShotTooLate;
        public bool NearBallHandler;
        public float ShotClock;
        public float MatchTime;
        /// <summary>You have the ball with a defender right on you.</summary>
        public bool TightlyGuarded;
    }

    public sealed class CoachTip
    {
        public string Id;
        public string Text;
        public Func<TipSituation, bool> When;
    }

    /// <summary>
    /// Coach Dee's tips during your first games: each tip shows once per career, at the moment it
    /// helps (on defense, when an alley-oop is open, one make from heating up, ...). They stop after
    /// <see cref="MaxGames"/> games or when switched off.
    /// </summary>
    public static class CoachTips
    {
        public const int MaxGames = 6;
        /// <summary>Minimum seconds between two tips.</summary>
        public const float Gap = 12f;

        public static readonly List<CoachTip> All = new List<CoachTip>
        {
            Tip("tip.meter", "Hold SHOOT, release when the meter hits the gold line.", s => s.Live && s.HumanHasBall && s.MatchTime > 2f),
            Tip("tip.early", "A bit early! Wait for the gold line before you let go.", s => s.LastShotTooEarly),
            Tip("tip.late", "Too late! Let go as soon as the meter reaches gold.", s => s.LastShotTooLate),
            Tip("tip.clear", "After a steal or a board, take it back beyond the arc first.", s => s.MustClear && s.HumanHasBall),
            Tip("tip.defense", "On defense: STEAL near the ball, JUMP when they shoot.", s => s.Live && s.OnDefense && s.NearBallHandler),
            Tip("tip.oop", "Gold arrow? Pass now for the alley-oop!", s => s.Live && s.AlleyOopOpen),
            Tip("tip.heat", "One more make and you're HEATING UP.", s => s.HeatThreshold > 0 && s.HumanHotStreak == s.HeatThreshold - 1),
            Tip("tip.fake", "Defender in your face? Tap SHOOT quickly to pump fake, then shoot.", s => s.Live && s.HumanHasBall && s.TightlyGuarded && s.MatchTime > 8f),
            Tip("tip.shotclock", "Shot clock's low. Get a shot up!", s => s.Live && !s.OnDefense && s.ShotClock > 0f && s.ShotClock < 4f),
        };

        private static CoachTip Tip(string id, string text, Func<TipSituation, bool> when) => new CoachTip { Id = id, Text = text, When = when };

        /// <summary>Tips run for a career's first few games, unless switched off.</summary>
        public static bool Active(CareerSaveData d) => d != null && d.settings.coachTips && d.totals.games < MaxGames;

        /// <summary>
        /// The tip to show now, or null. Marks it seen. <paramref name="lastTipTime"/> spaces tips out.
        /// </summary>
        public static CoachTip Next(CareerSaveData d, TipSituation s, float now, ref float lastTipTime)
        {
            if (!Active(d) || now - lastTipTime < Gap) return null;
            foreach (var tip in All)
            {
                if (d.tipsSeen.Contains(tip.Id) || !tip.When(s)) continue;
                d.tipsSeen.Add(tip.Id);
                lastTipTime = now;
                return tip;
            }
            return null;
        }
    }
}
