using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>A Full Court player on the bench: who, how rested, and their stats so far.</summary>
    public sealed class BenchPlayer
    {
        public int Team;
        public PlayerDef Def;
        public ArchetypeDef Archetype;
        public float Stamina = 1f;
        public PlayerStatLine Line = new PlayerStatLine();
        /// <summary>Has been on the court at some point (shows in the box score).</summary>
        public bool Played;
    }

    public enum ViolationKind
    {
        /// <summary>Took the ball back over half court after crossing it.</summary>
        Backcourt = 0,
        /// <summary>Didn't get the ball over half court within eight seconds.</summary>
        EightSeconds = 1,
        /// <summary>Didn't inbound the ball within five seconds.</summary>
        FiveSecondInbound = 2,
        /// <summary>1-on-1: the ball went out of bounds (Team = the side that touched it last and lost it).</summary>
        OutOfBounds = 3,
    }

    /// <summary>
    /// Full Court rules and flow on top of the shared match core: the inbound pass after a basket,
    /// backcourt and eight-second violations, fast-break and get-back AI, and substitutions from a
    /// two-player bench when someone's tired. Everything here is off in the half-court game.
    /// </summary>
    public sealed partial class MatchSimulation
    {
        public const float EightSecondLimit = 8f;
        public const float InboundLimit = 5f;
        /// <summary>A tired AI player (stamina below this) comes out at the next dead ball if the bench is fresher.</summary>
        public const float SubBelowStamina = 0.62f;
        private const float BenchRecoverPerSecond = 0.05f;

        private bool _crossedHalf;
        private float _backcourtTime;
        private float _inboundTime;
        private readonly List<BenchPlayer> _bench = new List<BenchPlayer>();

        /// <summary>The ball has to be passed in from the baseline before anyone can dribble or shoot.</summary>
        public bool MustInbound { get; private set; }
        /// <summary>The offence has brought the ball over half court this possession.</summary>
        public bool CrossedHalf => _crossedHalf;
        /// <summary>Seconds the offence has spent in the backcourt this possession (eight-second count).</summary>
        public float BackcourtTime => _backcourtTime;
        public IReadOnlyList<BenchPlayer> Bench => _bench;

        private void InitBench()
        {
            _bench.Clear();
            for (int team = 0; team < 2; team++)
            {
                var list = team == 0 ? Setup.BenchA : Setup.BenchB;
                if (list == null) continue;
                foreach (var def in list)
                    if (def != null) _bench.Add(new BenchPlayer { Team = team, Def = def, Archetype = Setup.ArchetypeLookup?.Invoke(def.archetypeId) });
            }
        }

        /// <summary>New possession (check ball or a live change of possession): reset the Full Court counts.</summary>
        private void StartFullCourtPossession(bool inbound)
        {
            _crossedHalf = false;
            _backcourtTime = 0f;
            _inboundTime = 0f;
            MustInbound = Setup.FullCourt && inbound;
        }

        /// <summary>Called every live step after the ball moves.</summary>
        private void UpdateFullCourtRules(float dt)
        {
            if (!Setup.FullCourt || Phase != MatchPhase.Live) return;
            float mid = FullCourt.MidY;

            if (MustInbound)
            {
                _inboundTime += dt;
                if (Ball.IsHeld && _inboundTime > InboundLimit)
                {
                    Violation(ViolationKind.FiveSecondInbound);
                    return;
                }
            }

            bool offenseHas = (Ball.IsHeld && Players[Ball.HolderIndex].Team == OffenseTeam)
                              || (Ball.Phase == BallPhase.Pass && Ball.PasserIndex >= 0 && Players[Ball.PasserIndex].Team == OffenseTeam);
            if (Ball.IsHeld && Players[Ball.HolderIndex].Team == OffenseTeam)
            {
                float y = Players[Ball.HolderIndex].Position.y;
                if (!_crossedHalf && y < mid - 0.2f) _crossedHalf = true;
                else if (_crossedHalf && y > mid + 0.4f)
                {
                    Violation(ViolationKind.Backcourt);
                    return;
                }
            }
            if (!_crossedHalf && offenseHas && !MustInbound)
            {
                _backcourtTime += dt;
                if (_backcourtTime > EightSecondLimit) Violation(ViolationKind.EightSeconds);
            }
        }

        private void Violation(ViolationKind kind)
        {
            int holder = Ball.IsHeld ? Ball.HolderIndex : -1;
            if (holder >= 0) Stats[holder].turnovers++;
            Events.Add(new MatchEvent(MatchEventType.Violation, holder, OffenseTeam, (int)kind));
            GoDead(DefenseTeam, Setup.Flow.deadBallAfterTurnover);
        }

        public static string ViolationName(ViolationKind kind)
        {
            switch (kind)
            {
                case ViolationKind.Backcourt: return "BACKCOURT!";
                case ViolationKind.EightSeconds: return "8 SECONDS!";
                case ViolationKind.OutOfBounds: return "OUT OF BOUNDS!";
                default: return "5 SECONDS!";
            }
        }

        // ------------------------------------------------------------------ substitutions

        private void RestBench(float dt)
        {
            foreach (var b in _bench) b.Stamina = Math.Min(1f, b.Stamina + BenchRecoverPerSecond * dt);
        }

        /// <summary>At a dead ball: tired AI players come out for fresher bench players.</summary>
        private void MakeSubstitutions()
        {
            if (_bench.Count == 0) return;
            ApplyCoachSubs();
            foreach (var p in Players)
            {
                if (p.IsHuman || IsHumanControlled(p.Index) || p.Stamina >= SubBelowStamina) continue;
                if (Coaching && p.Team == Setup.HumanTeam && !CoachAutoSubs) continue;
                BenchPlayer best = null;
                foreach (var b in _bench)
                    if (b.Team == p.Team && b.Stamina > p.Stamina + 0.25f && (best == null || b.Stamina > best.Stamina)) best = b;
                if (best == null) continue;
                Swap(p, best);
            }
        }

        private void Swap(PlayerRuntimeState p, BenchPlayer b)
        {
            var outDef = p.Def;
            var outArch = p.Archetype;
            float outStamina = p.Stamina;
            var outLine = Stats.players[p.Index];
            p.Def = b.Def;
            p.Archetype = b.Archetype ?? p.Archetype;
            p.Stamina = b.Stamina;
            p.HotStreak = 0;
            Stats.players[p.Index] = b.Line;
            b.Def = outDef;
            b.Archetype = outArch;
            b.Stamina = outStamina;
            b.Line = outLine;
            b.Played = true;
            Events.Add(new MatchEvent(MatchEventType.Substitution, p.Index, p.Team));
        }

        // ------------------------------------------------------------------ transition AI

        /// <summary>Full Court, offence still in the backcourt: runners fill the lanes ahead of the ball.</summary>
        private bool FastBreakSpot(PlayerRuntimeState p, AiState s)
        {
            if (!Setup.FullCourt || !Ball.IsHeld || Players[Ball.HolderIndex].Team != p.Team) return false;
            var holder = Players[Ball.HolderIndex];
            if (holder.Position.y <= FullCourt.MidY || MustInbound && p.Index == holder.Index) return false;
            var c = Setup.Court;
            Vec2 spot;
            switch (p.Slot)
            {
                case 1: spot = new Vec2(-(c.HalfWidth - 2f), c.hoopY + 3.5f); break;  // left lane
                case 2: spot = new Vec2(c.HalfWidth - 2f, c.hoopY + 3.5f); break;     // right lane
                case 3: spot = new Vec2(-2.5f, FullCourt.MidY - 2f); break;           // trailer
                case 4: spot = new Vec2(2.5f, FullCourt.MidY - 2f); break;
                default: spot = new Vec2(0f, c.hoopY + 6f); break;
            }
            s.Intent = AiIntent.Space;
            s.Target = c.Clamp(spot);
            return true;
        }

        /// <summary>Full Court defence: anyone behind the ball sprints back to protect the rim first.</summary>
        private bool GetBack(PlayerRuntimeState p, AiState s, PlayerRuntimeState holder)
        {
            if (!Setup.FullCourt || holder == null || holder.Team == p.Team) return false;
            if (p.Position.y <= holder.Position.y + 1f) return false;
            var c = Setup.Court;
            float side = p.Slot % 2 == 0 ? -1.4f : 1.4f;
            s.Intent = AiIntent.Guard;
            s.Target = c.Clamp(new Vec2(side, c.hoopY + 2.2f + (p.Slot >= 3 ? 2.5f : 0f)));
            return true;
        }
    }
}
