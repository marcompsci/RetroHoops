using System;

namespace CallerRetroBall.Logic
{
    public enum AiIntent
    {
        Space = 0,
        Cut = 1,
        Drive = 2,
        Clear = 3,
        Hold = 4,
        Guard = 5,
        Help = 6,
        Chase = 7,
        Crash = 8,
        Stand = 9,
        Screen = 10,
        /// <summary>Running the baseline for an alley-oop (Finishes.cs).</summary>
        BaselineRun = 11,
    }

    /// <summary>Per-player AI memory: what it decided, where it's going, when it thinks next.</summary>
    public sealed class AiState
    {
        public AiIntent Intent;
        public Vec2 Target;
        public float NextDecision;
        public float IntentUntil;
        /// <summary>Phase 34: when this player last made a post move (one at a time).</summary>
        public float LastPostMove = -99f;
    }

    /// <summary>
    /// Readable utility AI. Each player re-decides every reaction-time interval, so a slower
    /// difficulty literally reacts later. Difficulty changes reaction time, decision quality,
    /// shot selection, release timing, and mistakes — never ratings or speed beyond ratings.
    /// </summary>
    public sealed partial class MatchSimulation
    {
        /// <summary>Human-team AI teammates play at a fixed, helpful level.</summary>
        public static readonly DifficultyDef FriendlyAi = new DifficultyDef
        {
            id = "difficulty.friendly", displayName = "Teammate", reactionTime = 0.3f, decisionQuality = 0.75f,
            shotQualityThreshold = 0.42f, errorRate = 0.08f, movementScale = 1f, releaseAccuracy = 0.6f,
        };

        private static readonly DifficultyDef DefaultOpponent = new DifficultyDef
        {
            id = "difficulty.caller", displayName = "Caller", reactionTime = 0.35f, decisionQuality = 0.7f,
            shotQualityThreshold = 0.4f, errorRate = 0.12f, movementScale = 1f, releaseAccuracy = 0.55f,
        };

        private AiState[] _ai;

        public AiState AiStateOf(int playerIndex) => _ai[playerIndex];

        public DifficultyDef AiProfile(int team) =>
            team == Setup.HumanTeam || Setup.SecondHuman ? FriendlyAi : (Setup.Difficulty ?? DefaultOpponent);

        private void InitAi()
        {
            _ai = new AiState[Players.Length];
            for (int i = 0; i < _ai.Length; i++) _ai[i] = new AiState { Intent = AiIntent.Space };
        }

        private void ResetAiTimers()
        {
            for (int i = 0; i < _ai.Length; i++)
            {
                _ai[i].NextDecision = Time; // decide on the next live frame
                _ai[i].IntentUntil = 0f;
                _ai[i].Target = Players[i].Position;
                _ai[i].Intent = AiIntent.Space;
            }
        }

        private void UpdateAi(float dt)
        {
            for (int i = 0; i < Players.Length; i++)
            {
                if (IsHumanControlled(i)) continue;
                var p = Players[i];
                var s = _ai[i];
                if (IsBenched(i))
                {
                    s.Intent = AiIntent.Stand;
                    s.Target = p.Position;
                    continue;
                }

                if (Setup.PassiveOpponents && p.Team != Setup.HumanTeam)
                {
                    s.Intent = AiIntent.Stand;
                    s.Target = p.Position;
                    continue;
                }

                if (Time < s.NextDecision) continue;
                var profile = AiProfile(p.Team);
                s.NextDecision = Time + profile.reactionTime * (0.75f + 0.5f * _rng.NextFloat());
                Decide(p, s, profile);
            }
        }

        /// <summary>Stick-like input for an AI player toward its current target.</summary>
        private Vec2 AiDesired(PlayerRuntimeState p)
        {
            var s = _ai[p.Index];
            if (s.Intent == AiIntent.Stand) return Vec2.Zero;
            var target = s.Intent == AiIntent.Chase ? BallGroundTarget() : s.Target;
            float stop = s.Intent == AiIntent.Guard || s.Intent == AiIntent.Help || s.Intent == AiIntent.Screen ? 0.1f : 0.15f;
            return Movement.ArriveInput(p.Position, target, 1.0f, stop);
        }

        private Vec2 BallGroundTarget()
        {
            if (Ball.Phase == BallPhase.Pass && Ball.TargetIndex >= 0) return Ball.FlightTo;
            return Ball.Position;
        }

        private void OnAiPassed(int passer)
        {
            if (IsHumanControlled(passer)) return;
            var p = Players[passer];
            var s = _ai[passer];
            var court = Setup.Court;
            // Give-and-go: cut toward the rim after passing, then return to spacing.
            if (_rng.NextFloat() < 0.35f + 0.5f * p.Tendencies.cut)
            {
                s.Intent = AiIntent.Cut;
                s.Target = CutSpot(p, court);
                s.IntentUntil = Time + Setup.Flow.giveAndGoSeconds;
                s.NextDecision = s.IntentUntil;
            }
        }

        // ------------------------------------------------------------------ decisions

        private void Decide(PlayerRuntimeState p, AiState s, DifficultyDef profile)
        {
            if (Phase != MatchPhase.Live)
            {
                s.Intent = AiIntent.Stand;
                return;
            }

            switch (Ball.Phase)
            {
                case BallPhase.Loose:
                    DecideLooseBall(p, s);
                    return;
                case BallPhase.Shot:
                    DecideRebound(p, s);
                    return;
                case BallPhase.Pass:
                    if (Ball.TargetIndex == p.Index)
                    {
                        s.Intent = AiIntent.Chase;
                        return;
                    }
                    break;
            }

            if (p.Team == OffenseTeam)
            {
                if (Ball.IsHeld && Ball.HolderIndex == p.Index) DecideHandler(p, s, profile);
                else DecideOffBall(p, s);
            }
            else
            {
                DecideDefense(p, s, profile);
            }
        }

        private void DecideLooseBall(PlayerRuntimeState p, AiState s)
        {
            // The closest player on each team chases; the rest hold their spots.
            int closest = -1;
            float best = float.MaxValue;
            for (int i = 0; i < Players.Length; i++)
            {
                if (Players[i].Team != p.Team) continue;
                float d = Vec2.Distance(Players[i].Position, Ball.Position);
                if (d < best)
                {
                    best = d;
                    closest = i;
                }
            }
            if (closest == p.Index)
            {
                s.Intent = AiIntent.Chase;
                return;
            }
            s.Intent = AiIntent.Space;
            s.Target = p.Team == OffenseTeam ? Formation.OffenseSpot(p.Slot, Setup.Court) : Setup.Court.Hoop + new Vec2(0f, 2.5f);
        }

        private void DecideRebound(PlayerRuntimeState p, AiState s)
        {
            var court = Setup.Court;
            float crash = p.Tendencies.crashBoards;
            if (_rng.NextFloat() < crash)
            {
                var side = (Ball.FlightFrom - court.Hoop).Normalized;
                var jitter = new Vec2((_rng.NextFloat() - 0.5f) * 1.4f, (_rng.NextFloat() - 0.5f) * 0.8f);
                s.Intent = AiIntent.Crash;
                s.Target = court.Clamp(court.Hoop + side * 1.8f + jitter);
            }
            else if (p.Team == OffenseTeam)
            {
                s.Intent = AiIntent.Space;
                s.Target = Formation.OffenseSpot(p.Slot, court);
            }
            else
            {
                // Box out: get between your man and the rim.
                var man = Players[_guarding[p.Index]];
                var toHoop = court.Hoop - man.Position;
                s.Intent = AiIntent.Guard;
                s.Target = toHoop.SqrMagnitude > 0.01f ? court.Clamp(man.Position + toHoop.Normalized * 0.8f) : man.Position;
            }
        }

        private void DecideHandler(PlayerRuntimeState p, AiState s, DifficultyDef profile)
        {
            if (ChargingIndex == p.Index) return;
            var court = Setup.Court;
            if (MustInbound)
            {
                // Full Court inbound: get it in to the most open teammate.
                int inbound = SelectPassTarget(p.Index, Vec2.Zero);
                if (inbound >= 0) PassFrom(p.Index, Vec2.Zero, inbound);
                else
                {
                    s.Intent = AiIntent.Hold;
                    s.Target = p.Position;
                }
                return;
            }
            if (GiveAndGoReturn(p)) return;
            if (Setup.TeammatesOnlyPass && p.Team == Setup.HumanTeam)
            {
                s.Intent = AiIntent.Hold;
                s.Target = p.Position;
                return;
            }

            if (MustClear && court.ZoneOf(p.Position) != ShotZone.BeyondArc)
            {
                s.Intent = AiIntent.Clear;
                var away = p.Position - court.Hoop;
                if (away.SqrMagnitude < 0.01f) away = Vec2.Up;
                s.Target = court.Clamp(court.Hoop + away.Normalized * (court.arcRadius + 1.1f));
                return;
            }

            var tend = p.Tendencies;
            float dist = court.DistanceToHoop(p.Position);
            if (TryAiSkillMove(p, profile, dist)) return;
            if (TryAiTrick(p, profile)) return;
            if ((p.Team != Setup.HumanTeam || Coaching) && ShotClock > 4f && TryPostMove(p, s, dist)) return;
            float urgency = ShotClock < 3f && Setup.Rules.shotClockSeconds < 99f ? 1f : 0f;

            // Shoot: expected chance with a clean release vs. this difficulty's quality bar.
            float uShoot = float.MinValue;
            if (CanShoot(p.Index))
            {
                var type = ShotModel.Classify(dist, court.ZoneOf(p.Position), p.Def.attributes.finishing, dist < 2.5f, Setup.Shot);
                var expected = ShotModel.Evaluate(ShotContextFor(p.Index, type, Setup.Shot.greenCenter), Setup.Shot);
                uShoot = (expected.MakeChance - profile.shotQualityThreshold) * 2f + tend.shoot * 0.25f + urgency;
                // Phase 35: lean on the spots he's hot from this game, shy away from the cold ones.
                uShoot += ShotCharts.AiShootBias(Stats[p.Index].chart, ShotZones.SpotOf(p.Position, court));
            }

            // Pass: best open teammate. Friendly AI looks for the human first ("pass back").
            int target = SelectPassTarget(p.Index, Vec2.Zero);
            float uPass = float.MinValue;
            if (target >= 0)
            {
                float open = OpennessOf(Players[target]);
                uPass = Math.Min(open, 4f) / 4f * 0.6f + tend.pass * 0.25f - 0.2f;
                int humanMate = HumanIndexOf(p.Team);
                if (humanMate >= 0)
                {
                    float humanOpen = OpennessOf(Players[humanMate]);
                    if (humanOpen > 1.8f)
                    {
                        target = humanMate;
                        uPass = Math.Max(uPass, 0.35f + Math.Min(humanOpen, 4f) / 16f);
                    }
                }
                if (NearestOpponentDistance(p) < 0.9f) uPass += 0.3f; // trapped: move it
            }

            // Drive: attack the rim when the lane is open.
            float uDrive = float.MinValue;
            if (dist > 2.2f)
            {
                bool laneOpen = LaneOpenToRim(p);
                uDrive = tend.drive * 0.35f + (laneOpen ? 0.15f : -0.1f);
            }
            float uHold = 0.05f;
            CoachBias(p, dist, ref uShoot, ref uDrive, ref target, ref uPass);

            // AI teams occasionally run a play.
            if ((p.Team != Setup.HumanTeam || Coaching && PendingPlay == PlayCall.None) && _play == PlayCall.None && dist > 4f
                && _rng.NextFloat() < 0.12f * (0.5f + tend.drive) * profile.decisionQuality)
            {
                // Mix it up (no extra random draw, so seeded games keep their shape): mostly pick-and-roll,
                // sometimes a backdoor cut or a post-up for the big.
                int pick = (int)(Time * 10f) % 5;
                var play = pick == 3 ? PlayCall.Backdoor : pick == 4 ? PlayCall.PostUp : PlayCall.PickAndRoll;
                StartPlay(p.Team, play, p.Index);
                s.Intent = AiIntent.Hold;
                s.Target = p.Position;
                return;
            }

            // Pick: best option with probability decisionQuality, otherwise any viable one.
            int choice = Best(uShoot, uPass, uDrive, uHold);
            if (_rng.NextFloat() > profile.decisionQuality) choice = RandomViable(uShoot, uPass, uDrive, uHold);
            if (urgency > 0f && uShoot > float.MinValue) choice = 0;
            if (_rng.NextFloat() < profile.errorRate) choice = 3; // hesitation

            switch (choice)
            {
                case 0:
                    BeginCharge(p.Index, SampleReleaseMeter(p, profile));
                    break;
                case 1:
                    PassFrom(p.Index, Vec2.Zero, target);
                    break;
                case 2:
                    s.Intent = AiIntent.Drive;
                    var toHoop = court.Hoop - p.Position;
                    s.Target = court.Clamp(court.Hoop - toHoop.Normalized * 0.9f + new Vec2(0f, 0.3f));
                    s.IntentUntil = Time + 1.5f;
                    break;
                default:
                    s.Intent = AiIntent.Hold;
                    s.Target = court.Clamp(Formation.OffenseSpot(0, court) + new Vec2((_rng.NextFloat() - 0.5f) * 3f, -1f));
                    break;
            }
        }

        private static int Best(float a, float b, float c, float d)
        {
            int best = 0;
            float v = a;
            if (b > v) { v = b; best = 1; }
            if (c > v) { v = c; best = 2; }
            if (d > v) { best = 3; }
            return best;
        }

        private int RandomViable(float a, float b, float c, float d)
        {
            int count = (a > -0.5f ? 1 : 0) + (b > -0.5f ? 1 : 0) + (c > -0.5f ? 1 : 0) + (d > -0.5f ? 1 : 0);
            if (count == 0) return 3;
            int pick = _rng.Range(0, count);
            if (a > -0.5f && pick-- == 0) return 0;
            if (b > -0.5f && pick-- == 0) return 1;
            if (c > -0.5f && pick-- == 0) return 2;
            return 3;
        }

        /// <summary>
        /// AI release timing: green with probability releaseAccuracy, otherwise early or late by
        /// a random amount. The AI uses the same meter and shot model as the human.
        /// </summary>
        private float SampleReleaseMeter(PlayerRuntimeState p, DifficultyDef profile)
        {
            var t = Setup.Shot;
            float gh = ShotModel.GreenHalfWidth(p.Def.attributes.shooting, t);
            float accuracy = profile.releaseAccuracy + (p.Team == Setup.HumanTeam ? Setup.ChemistryBonus : 0f)
                             // Phase 40 SMART DIFFICULTY (Rise): the other team's shooting eases or sharpens a touch.
                             + (p.Team != Setup.HumanTeam && !Setup.SecondHuman ? Setup.AiEdge : 0f);
            if (_rng.NextFloat() < accuracy)
                return t.greenCenter + (_rng.NextFloat() * 2f - 1f) * gh * 0.8f;
            float side = _rng.NextFloat() < 0.5f ? -1f : 1f;
            float offset = gh + 0.01f + _rng.NextFloat() * 0.25f;
            return Math.Min(1.05f, Math.Max(0.1f, t.greenCenter + side * offset));
        }

        private void DecideOffBall(PlayerRuntimeState p, AiState s)
        {
            if (PlayIntentFor(p, s)) return;
            if (FastBreakSpot(p, s)) return;
            if (BaselineRunIntent(p, s)) return;
            if (Time < s.IntentUntil) return; // finishing a cut
            var court = Setup.Court;
            int defIndex = DefenderOf(p.Index);
            var defender = Players[defIndex >= 0 ? defIndex : Index(DefenseTeam, p.Slot)];
            float separation = Vec2.Distance(defender.Position, p.Position);

            // Backdoor cut when the defender is ball-watching, weighted by cut tendency.
            if (separation > 2.0f && _rng.NextFloat() < p.Tendencies.cut * 0.35f)
            {
                s.Intent = AiIntent.Cut;
                s.Target = CutSpot(p, court);
                s.IntentUntil = Time + 1.3f;
                return;
            }
            s.Intent = AiIntent.Space;
            s.Target = SpacingSpot(p);
        }

        /// <summary>Off-ball players take the spacing spots not occupied by the ball handler.</summary>
        private Vec2 SpacingSpot(PlayerRuntimeState p)
        {
            var court = Setup.Court;
            int handlerSlot = Ball.IsHeld ? Players[Ball.HolderIndex].Slot : 0;
            // Spots 1 and 2 are the wings; the handler's absence frees slot 0's spot (top).
            int spot = p.Slot == 0 ? handlerSlot : p.Slot;
            if (spot == 0) spot = p.Slot == 1 ? 1 : 2;
            var at = Formation.OffenseSpot(spot, court);
            // Phase 35: a shooter who's hot from a spot drifts toward it for the kick-out.
            var hunt = ShotCharts.HuntSpot(Stats[p.Index].chart);
            if (hunt != null) at = court.Clamp(Vec2.Lerp(at, ShotCharts.CourtPoint(hunt.Value, court), HuntPull));
            // Phase 34, drive-and-kick spacing: never stand where the ball handler has driven to. If he's within
            // SpaceFromBall of this spot, take the mirror-image spot on the far side, ready for the kick-out.
            if (Ball.IsHeld && Players[Ball.HolderIndex].Team == p.Team)
            {
                var holder = Players[Ball.HolderIndex].Position;
                var mirror = court.Clamp(new Vec2(-at.x, at.y));
                if (Vec2.Distance(at, holder) < SpaceFromBall && Vec2.Distance(mirror, holder) > Vec2.Distance(at, holder)) at = mirror;
            }
            return at;
        }

        /// <summary>Phase 34: post moves the AI has made this game (for tests and tuning).</summary>
        public int PostMoves { get; private set; }

        /// <summary>Phase 35: how far (0..1) an off-ball shooter moves from his spacing spot toward his hot spot.</summary>
        public const float HuntPull = 0.6f;

        /// <summary>Phase 34: off-ball players keep at least this far (m) from their own ball handler.</summary>
        public const float SpaceFromBall = 2.6f;

        /// <summary>
        /// Phase 34 post move: a strong finisher with the ball close to the rim and a defender on his back drop-steps to
        /// the baseline side of the rim (no extra random draw, so seeded and two-phone games stay in step), then finishes.
        /// </summary>
        private bool TryPostMove(PlayerRuntimeState p, AiState s, float dist)
        {
            if (dist > 2.8f || dist < 1.1f || p.Def.attributes.finishing < 60 || Time - s.LastPostMove < 3f) return false;
            if (NearestOpponentDistance(p) > 1.4f) return false; // nobody on him: just shoot
            var court = Setup.Court;
            float side = p.Position.x >= court.Hoop.x ? 1f : -1f;
            s.LastPostMove = Time;
            PostMoves++;
            s.Intent = AiIntent.Drive;
            s.Target = court.Clamp(court.Hoop + new Vec2(side * 0.9f, 0.35f));
            s.IntentUntil = Time + 0.45f;
            return true;
        }

        private Vec2 CutSpot(PlayerRuntimeState p, CourtGeometry court)
        {
            float side = p.Position.x >= 0f ? 1f : -1f;
            return court.Clamp(new Vec2(side * 1.1f, court.hoopY + 1.1f));
        }

        private void DecideDefense(PlayerRuntimeState p, AiState s, DifficultyDef profile)
        {
            var court = Setup.Court;
            var man = Players[_guarding[p.Index]];
            var holder = Holder;

            if (holder != null && holder.Index == man.Index)
            {
                float dist = Vec2.Distance(p.Position, man.Position);
                // Rise with the shooter (reaction-limited), or gamble for a steal.
                if (ChargingIndex == man.Index && dist < 1.8f && _rng.NextFloat() > profile.errorRate)
                    Jump(p.Index);
                else if (dist < Setup.Defense.stealRange
                         && _rng.NextFloat() < p.Tendencies.gambleForSteals * 0.25f * profile.decisionQuality
                                               * (_scheme[p.Team] == DefenseScheme.Pressure ? 2f : 1f))
                    TrySteal(p.Index);

                // On the ball: tight, and closer still when he's rising up to shoot.
                s.Intent = AiIntent.Guard;
                var toHoop = court.Hoop - man.Position;
                float gap = ChargingIndex == man.Index ? 0.7f : Math.Max(0.6f, SchemeOnBallGap(p.Team) + ScoutGap(p, man, profile));
                // Full Court: contain the ball in the backcourt (give a cushion) unless pressing.
                if (Setup.FullCourt && man.Position.y > FullCourt.MidY && _scheme[p.Team] != DefenseScheme.Pressure) gap = Math.Max(gap, 2.2f);
                s.Target = toHoop.SqrMagnitude > 0.01f ? court.Clamp(man.Position + toHoop.Normalized * gap) : man.Position;
                return;
            }

            if (GetBack(p, s, holder)) return;
            if (ProtectRim(p, s, profile)) return;
            if (DenyBaselineRunner(p, s, man, profile)) return;
            if (TrapHotHand(p, s, holder, profile)) return;

            // Help when the ball handler gets deep; mistakes (errorRate) mean late rotations.
            bool packed = _scheme[p.Team] == DefenseScheme.PackLine || _scheme[p.Team] == DefenseScheme.Zone;
            if (holder != null && !IsBenched(p.Index) && court.DistanceToHoop(holder.Position) < (packed ? 4.5f : 3.5f)
                && _rng.NextFloat() < p.Tendencies.helpDefense * (1f - profile.errorRate) * (_scheme[p.Team] == DefenseScheme.Pressure ? 0.6f : 1f))
            {
                s.Intent = AiIntent.Help;
                var toHoop = court.Hoop - holder.Position;
                s.Target = court.Clamp(holder.Position + toHoop.Normalized * Math.Min(1.0f, toHoop.Magnitude * 0.6f));
                if (ChargingIndex == holder.Index && Vec2.Distance(p.Position, holder.Position) < 1.5f) Jump(p.Index);
                return;
            }

            // Guard the man (positions snapshot at decision time → realistic reaction lag).
            s.Intent = AiIntent.Guard;
            s.Target = BackcourtSag(SchemeGuardSpot(p, man, holder), man, p.Team);
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// Full Court: off-ball defenders get back and wait just inside half court while their man is still
        /// in the backcourt (a pressing team picks up full court instead).
        /// </summary>
        private Vec2 BackcourtSag(Vec2 target, PlayerRuntimeState man, int team)
        {
            if (!Setup.FullCourt || _scheme[team] == DefenseScheme.Pressure || man.Position.y <= FullCourt.MidY) return target;
            var court = Setup.Court;
            var hoop = court.Hoop;
            var d = man.Position - hoop;
            float t = (FullCourt.MidY - 1f - hoop.y) / Math.Max(0.1f, d.y);
            return court.Clamp(hoop + d * Math.Max(0f, Math.Min(1f, t)));
        }

        private float OpennessOf(PlayerRuntimeState p)
        {
            NearestOpponent(p, out float d, out _);
            return d;
        }

        private float NearestOpponentDistance(PlayerRuntimeState p) => OpennessOf(p);

        private bool LaneOpenToRim(PlayerRuntimeState p)
        {
            for (int i = 0; i < Players.Length; i++)
            {
                if (Players[i].Team == p.Team) continue;
                if (PassModel.LaneClearance(p.Position, Setup.Court.Hoop, Players[i].Position, out _) < 1.0f) return false;
            }
            return true;
        }
    }
}
