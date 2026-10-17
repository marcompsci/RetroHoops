using System;

namespace CallerRetroBall.Logic
{
    public enum ShotType { Layup = 0, Dunk = 1, MidRange = 2, Arc = 3 }

    public enum TimingGrade { TooEarly = 0, SlightlyEarly = 1, Green = 2, SlightlyLate = 3, TooLate = 4 }

    /// <summary>The one-line callout shown after a release.</summary>
    public enum ShotFeedback { Green = 0, CleanLook = 1, Contested = 2, TooEarly = 3, TooLate = 4 }

    /// <summary>
    /// Every shot-balance number in one tunable object. Nothing about make chances is
    /// hard-coded anywhere else.
    /// </summary>
    [Serializable]
    public class ShotTuning
    {
        /// <summary>A field-by-field copy (I14).</summary>
        public ShotTuning CloneTuning() => (ShotTuning)MemberwiseClone();

        // Meter
        public float meterFillSeconds = 0.9f;
        public float closeRangeFillSeconds = 0.6f;
        public float greenCenter = 0.85f;
        public float greenHalfWidth = 0.04f;
        /// <summary>Extra green half-width at Shooting 99 (scaled by rating).</summary>
        public float greenWidthFromShooting = 0.03f;
        /// <summary>Width of the "slightly early/late" band beyond each green edge.</summary>
        public float slightBand = 0.1f;
        /// <summary>Holding past a full meter this long auto-releases (TOO LATE).</summary>
        public float overHoldSeconds = 0.25f;
        /// <summary>Let go of SHOOT sooner than this after pressing it and it's a pump fake, not a shot.</summary>
        public float pumpFakeSeconds = 0.18f;

        // Timing bonus
        public float timingGreen = 0.22f;
        public float timingSlight = -0.04f;
        public float timingBad = -0.24f;
        /// <summary>Close shots care less about timing.</summary>
        public float closeRangeTimingScale = 0.5f;

        // Base skill: base + range * normalised rating
        public float layupBase = 0.42f, layupRange = 0.40f;
        public float dunkBase = 0.55f, dunkRange = 0.36f;
        public float midBase = 0.26f, midRange = 0.42f;
        public float arcBase = 0.16f, arcRange = 0.42f;

        // Distance
        public float layupMaxDistance = 1.6f;
        public float dunkMaxDistance = 1.3f;
        public int dunkMinFinishing = 65;
        public float midIdealDistance = 3.5f;
        public float midPenaltyPerMeter = 0.02f;
        public float arcIdealDistance = 7.0f;
        public float arcPenaltyPerMeter = 0.045f;

        // Defense
        public float contestRadius = 1.7f;
        public float maxContestPenalty = 0.26f;
        public float openLookDistance = 2.6f;
        public float openLookBonus = 0.06f;

        // Streaks, fatigue, clutch
        public int hotStreakStart = 2;
        public float hotStreakPerMake = 0.03f;
        public float hotStreakMax = 0.09f;
        /// <summary>HEAT CHECK: makes in a row to heat up; heated players shoot better and move faster.</summary>
        public int heatThreshold = 3;
        public float heatBonus = 0.08f;
        public float heatSpeedScale = 1.06f;
        public float fatigueMaxPenalty = 0.08f;
        public float clutchSwing = 0.08f;

        public float minChance = 0.02f;
        public float maxChance = 0.96f;
        /// <summary>Debug only: green releases always go in.</summary>
        public bool debugGreenAlwaysMakes;

        public static ShotTuning Default => new ShotTuning();
    }

    /// <summary>Inputs to one shot evaluation.</summary>
    public struct ShotContext
    {
        public ShotType Type;
        public float Distance;
        /// <summary>Meter value at release, 0..1+ (above 1 = held past full).</summary>
        public float Meter;
        public AttributeSet Shooter;
        /// <summary>Distance to the nearest defender (float.MaxValue if none).</summary>
        public float NearestDefenderDistance;
        public int NearestDefenderDefense;
        public int HotStreak;
        /// <summary>HEAT CHECK is active for the shooter.</summary>
        public bool Heated;
        /// <summary>1 = fresh, 0 = exhausted.</summary>
        public float Stamina01;
        public bool LateGame;
        /// <summary>Phase 36: flat make-chance bonus (SPOT SPECIALIST ranks, your own player only).</summary>
        public float Bonus;
        /// <summary>14 Oct 2026: a layup from the side that goes off the glass (the board shields it a little from the contest).</summary>
        public bool Bank;
    }

    /// <summary>The full breakdown of a shot, useful for UI, tuning, and tests.</summary>
    public struct ShotEvaluation
    {
        public TimingGrade Grade;
        public ShotFeedback Feedback;
        public float MakeChance;
        public float BaseSkill;
        public float TimingBonus;
        public float OpenLookBonus;
        public float HotStreakBonus;
        public float DistancePenalty;
        public float ContestPenalty;
        public float FatiguePenalty;
        public float ClutchBonus;
        public bool Contested;
        public bool Open;
        public bool GuaranteedMake;
    }

    public static class ShotModel
    {
        public static bool IsCloseRange(ShotType t) => t == ShotType.Layup || t == ShotType.Dunk;

        /// <summary>Picks the shot profile from where and who is shooting.</summary>
        public static ShotType Classify(float distance, ShotZone zone, int finishing, bool attacking, ShotTuning t)
        {
            if (zone == ShotZone.BeyondArc) return ShotType.Arc;
            if (distance <= t.dunkMaxDistance && attacking && finishing >= t.dunkMinFinishing) return ShotType.Dunk;
            if (distance <= t.layupMaxDistance) return ShotType.Layup;
            return ShotType.MidRange;
        }

        public static float FillSeconds(ShotType type, ShotTuning t) =>
            IsCloseRange(type) ? t.closeRangeFillSeconds : t.meterFillSeconds;

        /// <summary>
        /// I14 FORGIVING METER (Settings): a copy of the tuning with a 50% wider green window and wider "slightly"
        /// bands, for people still learning the release. Only your own shots use it; make chances are unchanged.
        /// </summary>
        public static ShotTuning Forgiving(ShotTuning t)
        {
            var f = (ShotTuning)t.CloneTuning();
            f.greenHalfWidth = t.greenHalfWidth * ForgivingGreen;
            f.greenWidthFromShooting = t.greenWidthFromShooting * ForgivingGreen;
            f.slightBand = t.slightBand * 1.3f;
            return f;
        }

        public const float ForgivingGreen = 1.5f;

        public static float GreenHalfWidth(int shootingRating, ShotTuning t) =>
            t.greenHalfWidth + t.greenWidthFromShooting * RatingScale.Normalized(shootingRating);

        public static TimingGrade Grade(float meter, int shootingRating, ShotTuning t)
        {
            if (meter > 1f) return TimingGrade.TooLate;
            float gh = GreenHalfWidth(shootingRating, t);
            float d = meter - t.greenCenter;
            if (Math.Abs(d) <= gh) return TimingGrade.Green;
            if (d < 0f) return d >= -(gh + t.slightBand) ? TimingGrade.SlightlyEarly : TimingGrade.TooEarly;
            return d <= gh + t.slightBand ? TimingGrade.SlightlyLate : TimingGrade.TooLate;
        }

        /// <summary>
        /// makeChance = baseSkill + timing + openLook + hotStreak + clutch − distance − contest − fatigue,
        /// clamped to [minChance, maxChance]. A green release is the best outcome but is not a
        /// guaranteed make (unless the debug flag is set).
        /// </summary>
        public static ShotEvaluation Evaluate(ShotContext c, ShotTuning t)
        {
            var e = new ShotEvaluation();
            var a = c.Shooter;
            bool close = IsCloseRange(c.Type);

            switch (c.Type)
            {
                case ShotType.Layup: e.BaseSkill = t.layupBase + t.layupRange * RatingScale.Normalized(a.finishing); break;
                case ShotType.Dunk: e.BaseSkill = t.dunkBase + t.dunkRange * RatingScale.Normalized(a.finishing); break;
                case ShotType.MidRange: e.BaseSkill = t.midBase + t.midRange * RatingScale.Normalized(a.shooting); break;
                default: e.BaseSkill = t.arcBase + t.arcRange * RatingScale.Normalized(a.shooting); break;
            }

            e.Grade = Grade(c.Meter, a.shooting, t);
            float timing;
            switch (e.Grade)
            {
                case TimingGrade.Green: timing = t.timingGreen; break;
                case TimingGrade.SlightlyEarly:
                case TimingGrade.SlightlyLate: timing = t.timingSlight; break;
                default: timing = t.timingBad; break;
            }
            e.TimingBonus = close ? timing * t.closeRangeTimingScale : timing;

            if (c.Type == ShotType.MidRange)
                e.DistancePenalty = Math.Max(0f, c.Distance - t.midIdealDistance) * t.midPenaltyPerMeter;
            else if (c.Type == ShotType.Arc)
                e.DistancePenalty = Math.Max(0f, c.Distance - t.arcIdealDistance) * t.arcPenaltyPerMeter;

            if (c.NearestDefenderDistance < t.contestRadius)
            {
                float closeness = 1f - c.NearestDefenderDistance / t.contestRadius;
                float defense = 0.5f + 0.5f * RatingScale.Normalized(c.NearestDefenderDefense);
                e.ContestPenalty = t.maxContestPenalty * closeness * defense;
                if (c.Bank && c.Type == ShotType.Layup) e.ContestPenalty *= 1f - MatchSimulation.BankContestShield;
                e.Contested = e.ContestPenalty >= 0.05f;
            }
            if (c.NearestDefenderDistance >= t.openLookDistance)
            {
                e.OpenLookBonus = t.openLookBonus;
                e.Open = true;
            }

            if (c.HotStreak >= t.hotStreakStart)
                e.HotStreakBonus = Math.Min(t.hotStreakMax, (c.HotStreak - t.hotStreakStart + 1) * t.hotStreakPerMake);
            if (c.Heated) e.HotStreakBonus += t.heatBonus;

            float stamina = c.Stamina01 <= 0f ? 0f : (c.Stamina01 >= 1f ? 1f : c.Stamina01);
            e.FatiguePenalty = (1f - stamina) * t.fatigueMaxPenalty;
            if (c.LateGame) e.ClutchBonus = (RatingScale.Normalized(a.clutch) - 0.5f) * t.clutchSwing;

            float chance = e.BaseSkill + e.TimingBonus + e.OpenLookBonus + e.HotStreakBonus + e.ClutchBonus
                           - e.DistancePenalty - e.ContestPenalty - e.FatiguePenalty + c.Bonus;
            e.MakeChance = Math.Max(t.minChance, Math.Min(t.maxChance, chance));
            e.GuaranteedMake = t.debugGreenAlwaysMakes && e.Grade == TimingGrade.Green;

            if (e.Grade == TimingGrade.Green) e.Feedback = ShotFeedback.Green;
            else if (e.Grade == TimingGrade.TooEarly) e.Feedback = ShotFeedback.TooEarly;
            else if (e.Grade == TimingGrade.TooLate) e.Feedback = ShotFeedback.TooLate;
            else e.Feedback = e.Contested ? ShotFeedback.Contested : ShotFeedback.CleanLook;
            return e;
        }

        /// <summary>
        /// Phase 34: the fuller release callout: the read on the look, how the timing was off when it wasn't green, and
        /// the make chance at release ("CLEAN LOOK · SLIGHTLY LATE · 54%").
        /// </summary>
        public static string FeedbackLine(ShotFeedback f, TimingGrade g, float makeChance)
        {
            string line = FeedbackText(f);
            if (g == TimingGrade.SlightlyEarly) line += "  ·  SLIGHTLY EARLY";
            else if (g == TimingGrade.SlightlyLate) line += "  ·  SLIGHTLY LATE";
            int pct = (int)Math.Round(Math.Max(0f, Math.Min(1f, makeChance)) * 100f);
            return line + "  ·  " + pct + "%";
        }

        public static string FeedbackText(ShotFeedback f)
        {
            switch (f)
            {
                case ShotFeedback.Green: return "GREEN";
                case ShotFeedback.CleanLook: return "CLEAN LOOK";
                case ShotFeedback.Contested: return "CONTESTED";
                case ShotFeedback.TooEarly: return "TOO EARLY";
                default: return "TOO LATE";
            }
        }
    }
}
