using System;

namespace CallerRetroBall.Logic
{
    /// <summary>Signal Points and Fans for a finished game. Earned only by playing — nothing is sold.</summary>
    [Serializable]
    public class RewardTuning
    {
        public int winPoints = 120;
        public int lossPoints = 50;
        public int pointsPerPoint = 2;
        public int pointsPerAssist = 3;
        public int pointsPerRebound = 2;
        public int pointsPerStealOrBlock = 4;
        public int maxPerGame = 400;
        public int fansPerWin = 40;
        public int fansPerLoss = 10;
        public int fansPerGreen = 2;
        public int blowoutMargin = 8;
        public int blowoutFans = 10;
        public int playoffWinBonus = 100;
        public int championshipBonus = 500;
        /// <summary>Quick Call pays less than Rise Mode so the career stays the main path.</summary>
        public float quickCallScale = 0.5f;
        /// <summary>First Call Classic title bonus (replaces playoff/championship bonuses in that mode).</summary>
        public int classicTitleBonus = 200;

        public static RewardTuning Default => new RewardTuning();
    }

    [Serializable]
    public struct RewardGrant
    {
        public int signalPoints;
        public int fans;
    }

    public static class Rewards
    {
        public static RewardGrant For(MatchSummary s, RewardTuning t)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (s.mode == GameMode.Practice || s.mode == GameMode.Versus || s.mode == GameMode.Tutorial || s.mode == GameMode.Demo) return default;
            // Rival Challenge: Rise rates (its win bonus is paid by RivalEngine).

            var line = s.HumanLine?.stats ?? new PlayerStatLine();
            bool won = s.HumanWon;
            float sp = (won ? t.winPoints : t.lossPoints)
                       + line.points * t.pointsPerPoint
                       + line.assists * t.pointsPerAssist
                       + line.rebounds * t.pointsPerRebound
                       + (line.steals + line.blocks) * t.pointsPerStealOrBlock;
            bool classic = s.mode == GameMode.Tournament;
            int titleBonus = classic ? t.classicTitleBonus : t.championshipBonus;
            if (won && s.isPlayoff && !classic) sp += t.playoffWinBonus;
            if (won && s.isFinal) sp += titleBonus;
            if (s.mode == GameMode.QuickCall || s.mode == GameMode.Daily || s.mode == GameMode.King || s.mode == GameMode.Arcade || s.mode == GameMode.OneOnOne || s.mode == GameMode.FullCourt || s.mode == GameMode.Franchise || s.mode == GameMode.AllStar || s.mode == GameMode.Legacy || s.mode == GameMode.Street || s.mode == GameMode.Streets || s.mode == GameMode.CustomCup) sp *= t.quickCallScale;

            // Phase 37 CLUTCH: short games from a set score; the stars pay the real reward (Clutch.SpPerNewStar).
            if (s.mode == GameMode.Clutch) sp *= t.quickCallScale * 0.5f;

            int cap = s.isFinal && won ? t.maxPerGame + titleBonus : t.maxPerGame;
            int fans = (won ? t.fansPerWin : t.fansPerLoss) + line.greenReleases * t.fansPerGreen
                       + (won && s.Margin >= t.blowoutMargin && s.mode != GameMode.Clutch ? t.blowoutFans : 0);
            if (s.mode == GameMode.QuickCall || s.mode == GameMode.Daily || s.mode == GameMode.King || s.mode == GameMode.Arcade || s.mode == GameMode.OneOnOne || s.mode == GameMode.FullCourt || s.mode == GameMode.Franchise || s.mode == GameMode.AllStar || s.mode == GameMode.Legacy || s.mode == GameMode.Street || s.mode == GameMode.Streets || s.mode == GameMode.CustomCup || s.mode == GameMode.Clutch) fans = (int)Math.Round(fans * t.quickCallScale);

            return new RewardGrant { signalPoints = Math.Min(cap, (int)Math.Round(sp)), fans = fans };
        }
    }
}
