using System;

namespace CallerRetroBall.Logic
{
    /// <summary>Phase 40: what the other team has worked out about how a person plays this game.</summary>
    public enum ScoutedStyle { None = 0, Shooter = 1, Driver = 2 }

    // Phase 40 SMARTER AI: the defence scouts the person it's guarding during the game. After a handful of shots it
    // knows whether you live behind the arc (it plays you tighter) or attack the rim (it gives you a cushion and walls
    // off the drive). It reads only the box score, so it draws no random numbers and both phones in a two-phone game
    // see the same thing.
    public sealed partial class MatchSimulation
    {
        /// <summary>Shots before the defence decides what kind of player you are.</summary>
        public const int ScoutShots = 5;
        public const float ShooterShare = 0.55f, DriverShare = 0.25f;
        /// <summary>How much tighter (shooter) or looser (driver) the on-ball defender plays, in metres at full smarts.</summary>
        public const float ScoutTighten = 0.3f, ScoutLoosen = 0.4f;

        /// <summary>The style the defence has read for <paramref name="index"/> (only people are scouted).</summary>
        public ScoutedStyle ScoutOf(int index)
        {
            if (index < 0 || index >= Players.Length || !IsHumanControlled(index)) return ScoutedStyle.None;
            var line = Stats.players[index];
            if (line == null || line.fieldGoalsAttempted < ScoutShots) return ScoutedStyle.None;
            float arc = line.arcAttempted / (float)line.fieldGoalsAttempted;
            if (arc >= ShooterShare) return ScoutedStyle.Shooter;
            if (arc <= DriverShare) return ScoutedStyle.Driver;
            return ScoutedStyle.None;
        }

        /// <summary>On-ball gap change for an AI defender guarding <paramref name="man"/> (smarter teams adjust more).</summary>
        /// <summary>17 Oct 2026 CASHED OUT: how much closer the on-ball defender plays a keyed-on human (metres).</summary>
        private float KeyOnGap(PlayerRuntimeState man) => Setup.KeyOnHuman > 0f && man.IsHuman && !Setup.SecondHuman ? 0.35f * Setup.KeyOnHuman : 0f;

        private float ScoutGap(PlayerRuntimeState defender, PlayerRuntimeState man, DifficultyDef profile)
        {
            if (defender.Team == Setup.HumanTeam && !Setup.Coach) return 0f; // your own AI teammates don't scout you
            float smarts = profile != null ? Math.Max(0f, Math.Min(1f, profile.decisionQuality)) : 0.5f;
            switch (ScoutOf(man.Index))
            {
                case ScoutedStyle.Shooter: return -ScoutTighten * smarts;
                case ScoutedStyle.Driver: return ScoutLoosen * smarts;
                default: return 0f;
            }
        }
    }
}
