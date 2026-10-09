using System;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// Pump fakes (added 9 Oct 2026, Omari's request): tap SHOOT and let go at once (under
    /// <see cref="ShotTuning.pumpFakeSeconds"/>) and the shooter shows the ball instead of shooting. The closest
    /// defender may bite: he leaves his feet and is off balance for <see cref="DefenseTuning.fakeBiteRecovery"/>, so
    /// he can't contest or block and can't move, and you can shoot over him or drive past. Good defenders bite less,
    /// smarter AI bites less, and each extra fake in the same possession works half as often.
    /// A person defending decides for himself (he only bites if he really jumps).
    /// </summary>
    public sealed partial class MatchSimulation
    {
        private int _pumpFakes;

        /// <summary>Pump fakes so far this possession.</summary>
        public int PumpFakesThisPossession => _pumpFakes;

        /// <summary>The defender who bit on the last pump fake is still off balance.</summary>
        public bool IsFaked(int player) => player >= 0 && player < Players.Length && Time < Players[player].FakedUntil;

        private void PumpFake(int shooter)
        {
            var p = Players[shooter];
            CancelCharge();
            p.Motion.facing = Movement.FacingOf((Setup.Court.Hoop - p.Position).Normalized);
            _pumpFakes++;

            int bit = -1;
            int defender = FakeTarget(p);
            if (defender >= 0)
            {
                var d = Players[defender];
                var t = Setup.Defense;
                bool alreadyUp = IsJumping(defender); // jumped at the fake on his own: he's bitten
                bool bites = alreadyUp;
                if (!bites && !IsHumanControlled(defender))
                {
                    float chance = BiteChance(d, p);
                    bites = _rng.NextFloat() < chance;
                }
                if (bites)
                {
                    bit = defender;
                    d.JumpStart = Time; // leaves his feet
                    d.FakedUntil = Time + t.fakeBiteRecovery;
                    d.StunnedUntil = Math.Max(d.StunnedUntil, Time + t.fakeBiteRecovery);
                }
            }
            Events.Add(new MatchEvent(MatchEventType.PumpFake, shooter, p.Team, bit));
        }

        /// <summary>The defender a fake from <paramref name="shooter"/> works on: the closest one in range, or -1.</summary>
        private int FakeTarget(PlayerRuntimeState shooter)
        {
            if (Setup.PassiveOpponents) return -1;
            int best = -1;
            float bestDist = Setup.Defense.fakeBiteRange;
            for (int i = 0; i < Players.Length; i++)
            {
                var o = Players[i];
                if (o.Team == shooter.Team || IsBenched(i)) continue;
                if (Time < o.StunnedUntil && !IsJumping(i)) continue; // already flat-footed
                float dist = Vec2.Distance(o.Position, shooter.Position);
                if (dist <= bestDist)
                {
                    bestDist = dist;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>
        /// How likely an AI defender is to bite: high for weak defenders, lower for good ones and smart AI,
        /// higher against a dangerous shooter, and halved for every earlier fake this possession.
        /// </summary>
        public float BiteChance(PlayerRuntimeState defender, PlayerRuntimeState shooter)
        {
            var t = Setup.Defense;
            float chance = t.fakeBiteBase - t.fakeBiteFromDefense * RatingScale.Normalized(defender.Def.attributes.defense)
                           + 0.15f * (RatingScale.Normalized(shooter.Def.attributes.shooting) - 0.5f);
            var profile = AiProfile(defender.Team);
            if (profile != null) chance *= 1.15f - 0.5f * Math.Max(0f, Math.Min(1f, profile.decisionQuality));
            for (int k = 1; k < _pumpFakes; k++) chance *= t.fakeRepeatFactor;
            return Math.Max(0.05f, Math.Min(0.9f, chance));
        }
    }
}
