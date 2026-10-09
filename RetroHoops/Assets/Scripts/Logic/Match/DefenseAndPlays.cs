using System;

namespace CallerRetroBall.Logic
{
    /// <summary>Defense, rebounding, stamina, and screen tuning.</summary>
    [Serializable]
    public class DefenseTuning
    {
        // Steals
        public float stealRange = 1.3f;
        public float stealCooldown = 1.0f;
        public float stealBase = 0.22f;
        public float stealFromDefense = 0.35f;
        public float stealVsHandling = 0.25f;
        /// <summary>A failed reach leaves the defender flat-footed this long.</summary>
        public float failedStealRecovery = 0.5f;

        // Jumps, contests, blocks
        public float jumpSeconds = 0.45f;
        public float jumpCooldown = 0.35f;
        /// <summary>A jumping defender contests as if this much closer (distance multiplier).</summary>
        public float jumpContestFactor = 0.55f;
        public float blockRange = 1.1f;
        public float blockBase = 0.06f;
        public float blockFromDefense = 0.26f;
        public float blockPerHeightTier = 0.05f;
        public float blockCloseRangeBonus = 0.12f;
        public float blockMaxChance = 0.6f;

        // Pump fakes
        /// <summary>Only a defender this close to the shooter can bite on a pump fake.</summary>
        public float fakeBiteRange = 2.0f;
        /// <summary>Bite chance for a 0-defense defender (minus <see cref="fakeBiteFromDefense"/> for a 99).</summary>
        public float fakeBiteBase = 0.85f;
        public float fakeBiteFromDefense = 0.45f;
        /// <summary>A defender who bit is off balance this long: no contest, no block, can't move.</summary>
        public float fakeBiteRecovery = 0.75f;
        /// <summary>Each extra fake in the same possession multiplies the bite chance by this (they wise up).</summary>
        public float fakeRepeatFactor = 0.5f;

        // Rebounding
        public float boxOutRadius = 1.1f;
        public float boxOutBonus = 0.3f;
        public float reboundReach = 0.4f;

        // Screens
        public float screenRadius = 0.9f;
        public float screenSlow = 0.45f;

        // Schemes
        /// <summary>Same kind of basket this many times in a row makes a smart AI change its defence.</summary>
        public int schemeAdjustAfter = 2;
        /// <summary>Only AI at or above this decision quality adjusts (Caller and Legend).</summary>
        public float schemeAdjustMinQuality = 0.6f;
        public float screenSeconds = 0.7f;

        // Stamina (0..1)
        public float staminaDrainPerSecond = 0.06f;
        public float staminaRecoverPerSecond = 0.05f;
        public float staminaDeadBallRecover = 0.15f;
        /// <summary>Top speed lost at zero stamina.</summary>
        public float staminaSpeedPenalty = 0.12f;
        /// <summary>AI good enough to double-team a heated-up scorer (Caller and Legend).</summary>
        public float trapMinQuality = 0.6f;
        /// <summary>Only near the basket: within this distance of the rim.</summary>
        public float trapRange = 8f;

        public static DefenseTuning Default => new DefenseTuning();
    }

    public enum PlayCall
    {
        None = 0, PickAndRoll = 1, GiveAndGo = 2, ClearOut = 3,
        /// <summary>A wing jogs out high, then cuts behind their defender to the rim (pass it to them on the cut).</summary>
        Backdoor = 4,
        /// <summary>Your best big seals on the low block on the ball side; everyone else clears to the corners.</summary>
        PostUp = 5,
    }

    public sealed partial class MatchSimulation
    {
        private int[] _guarding;
        private PlayCall _play = PlayCall.None;
        private int _playTeam = -1;
        private float _playUntil;
        private int _screener = -1;
        private bool _screenRolling;
        private bool _screenSet;
        private float _screenSetAt;
        private int _giveAndGoPartner = -1;
        private float _playStart;
        private bool _backdoorCut;
        private int _lastTrapHolder = -1;
        private float _lastTrapAt = -99f;
        /// <summary>Backdoor cutter or post-up big, -1 when none.</summary>
        private int _playMate = -1;

        /// <summary>Seconds a backdoor cutter fakes high before cutting.</summary>
        public const float BackdoorFakeSeconds = 0.9f;

        public PlayCall ActivePlay => _play;
        public int PlayMate => _playMate;
        public int ActivePlayTeam => _playTeam;
        public int Screener => _screener;

        /// <summary>Offense player index that <paramref name="defenderIndex"/> is assigned to guard.</summary>
        public int GuardingOf(int defenderIndex) => _guarding[defenderIndex];

        private void InitDefense()
        {
            _guarding = new int[Players.Length];
            ResetMatchups();
            foreach (var p in Players) p.Stamina = p.Team == Setup.HumanTeam ? Math.Max(0f, Math.Min(1f, Setup.HumanTeamStartingStamina)) : 1f;
        }

        private void ResetMatchups()
        {
            // Everyone guards the same slot on the other team.
            for (int i = 0; i < Players.Length; i++)
                _guarding[i] = Index(1 - Players[i].Team, Players[i].Slot);
        }

        // ------------------------------------------------------------------ human defense

        private void HandleHumanDefense(int me, PlayerInput input)
        {
            if (input.DefensePressed) TrySteal(me);
            if (input.ShootPressed) Jump(me);
            if (input.PassPressed) SwitchOntoBall(me);
        }

        /// <summary>Reach for the ball. Needs to be close; a miss leaves you briefly beaten.</summary>
        public bool TrySteal(int defender)
        {
            var d = Players[defender];
            var t = Setup.Defense;
            if (Phase != MatchPhase.Live || !Ball.IsHeld || Time < d.StealReadyAt || Time < d.StunnedUntil) return false;
            var handler = Players[Ball.HolderIndex];
            if (handler.Team == d.Team || Setup.PassiveOpponents && d.Team != Setup.HumanTeam) return false;
            d.StealReadyAt = Time + t.stealCooldown;

            float dist = Vec2.Distance(d.Position, Ball.Position);
            Events.Add(new MatchEvent(MatchEventType.StealAttempt, defender, d.Team));
            if (dist > t.stealRange)
            {
                d.StunnedUntil = Time + t.failedStealRecovery * 0.5f; // whiff at air
                return false;
            }

            float closeness = 1f - dist / t.stealRange;
            float chance = (t.stealBase + t.stealFromDefense * RatingScale.Normalized(d.Def.attributes.defense)
                            - t.stealVsHandling * RatingScale.Normalized(handler.Def.attributes.playmaking))
                           * (0.5f + 0.5f * closeness);
            if (ChargingIndex == handler.Index) chance *= 0.5f; // ball is up, harder to poke
            chance = Math.Max(0.03f, Math.Min(0.6f, chance));

            if (_rng.NextFloat() < chance)
            {
                Stats[defender].steals++;
                Stats[handler.Index].turnovers++;
                CancelCharge();
                Events.Add(new MatchEvent(MatchEventType.Steal, defender, d.Team));
                GiveBall(defender, announce: false, fromCheck: false);
                return true;
            }

            d.StunnedUntil = Time + t.failedStealRecovery;
            return false;
        }

        /// <summary>Jump to contest (and maybe block) a shot.</summary>
        public bool Jump(int player)
        {
            var p = Players[player];
            var t = Setup.Defense;
            if (Phase != MatchPhase.Live || Time < p.JumpStart + t.jumpSeconds + t.jumpCooldown || Time < p.StunnedUntil) return false;
            p.JumpStart = Time;
            Events.Add(new MatchEvent(MatchEventType.Jump, player, p.Team));
            return true;
        }

        public bool IsJumping(int player) => Time - Players[player].JumpStart <= Setup.Defense.jumpSeconds;

        /// <summary>Jump height 0..1 for the view (parabolic).</summary>
        public float JumpHeight01(int player)
        {
            float u = (Time - Players[player].JumpStart) / Setup.Defense.jumpSeconds;
            if (u < 0f || u > 1f) return 0f;
            return 4f * u * (1f - u);
        }

        /// <summary>Human swaps assignments with the teammate guarding the ball.</summary>
        public bool SwitchOntoBall(int defender)
        {
            if (!Ball.IsHeld) return false;
            int handler = Ball.HolderIndex;
            var d = Players[defender];
            if (Players[handler].Team == d.Team || _guarding[defender] == handler) return false;
            for (int i = 0; i < Players.Length; i++)
            {
                if (Players[i].Team != d.Team || i == defender || _guarding[i] != handler) continue;
                _guarding[i] = _guarding[defender];
                _guarding[defender] = handler;
                Events.Add(new MatchEvent(MatchEventType.Switch, defender, d.Team));
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ blocks & contests

        /// <summary>
        /// Phase 38: how contested a shot from <paramref name="shooterIndex"/> would be right now, 0 (open) to 1 (a top
        /// defender in your face), on the same scale as the shot model's contest penalty. Read-only (no random draws),
        /// so the shot meter can show it while you charge.
        /// </summary>
        public float ContestLevel(int shooterIndex)
        {
            if (shooterIndex < 0 || shooterIndex >= Players.Length) return 0f;
            ContestFor(Players[shooterIndex], out float distance, out int defense);
            var t = Setup.Shot;
            if (t == null || t.contestRadius <= 0f || distance >= t.contestRadius) return 0f;
            float closeness = 1f - distance / t.contestRadius;
            float d = 0.5f + 0.5f * RatingScale.Normalized(defense);
            return Math.Max(0f, Math.Min(1f, closeness * d));
        }

        /// <summary>Best contesting defender for a shot (jumping defenders count as closer).</summary>
        private void ContestFor(PlayerRuntimeState shooter, out float effectiveDistance, out int defense)
        {
            effectiveDistance = float.MaxValue;
            defense = 50;
            if (Setup.PassiveOpponents) return;
            for (int i = 0; i < Players.Length; i++)
            {
                var o = Players[i];
                if (o.Team == shooter.Team) continue;
                if (Time < o.FakedUntil) continue; // bit on a pump fake: off balance, no contest
                float d = Vec2.Distance(o.Position, shooter.Position);
                if (IsJumping(i)) d *= Setup.Defense.jumpContestFactor;
                if (d < effectiveDistance)
                {
                    effectiveDistance = d;
                    defense = o.Def.attributes.defense;
                }
            }
        }

        /// <summary>Checks airborne defenders at release. Returns the blocker or -1.</summary>
        private int TryBlock(PlayerRuntimeState shooter, ShotType type)
        {
            if (Setup.PassiveOpponents) return -1;
            var t = Setup.Defense;
            for (int i = 0; i < Players.Length; i++)
            {
                var d = Players[i];
                if (d.Team == shooter.Team || !IsJumping(i) || Time < d.FakedUntil) continue;
                float dist = Vec2.Distance(d.Position, shooter.Position);
                if (dist > t.blockRange) continue;
                float chance = t.blockBase + t.blockFromDefense * RatingScale.Normalized(d.Def.attributes.defense)
                               + t.blockPerHeightTier * (d.Def.appearance.heightTier - shooter.Def.appearance.heightTier)
                               + (ShotModel.IsCloseRange(type) ? t.blockCloseRangeBonus : 0f);
                chance *= 1f - dist / t.blockRange * 0.5f;
                chance = Math.Max(0f, Math.Min(t.blockMaxChance, chance));
                if (_rng.NextFloat() < chance) return i;
            }
            return -1;
        }

        private void ResolveBlock(int shooter, int blocker)
        {
            var s = Players[shooter];
            var b = Players[blocker];
            Stats[blocker].blocks++;
            Stats[shooter].fieldGoalsAttempted++;
            if (Setup.Court.ZoneOf(s.Position) == ShotZone.BeyondArc) Stats[shooter].arcAttempted++;
            Stats[shooter].chart.spots[(int)ShotZones.SpotOf(s.Position, Setup.Court)].attempted++;
            CoolOff(s);
            CancelCharge();
            var away = (s.Position - b.Position).Normalized;
            if (away.SqrMagnitude < 0.01f) away = Vec2.Up;
            Ball.Phase = BallPhase.Loose;
            Ball.HolderIndex = -1;
            Ball.Position = s.Position;
            Ball.Height = 2.3f;
            Ball.Velocity = away * (2.5f + _rng.NextFloat() * 2f);
            Ball.VerticalVelocity = 1.5f;
            _reboundable = true;
            _lastShotWasMiss = true;
            Events.Add(new MatchEvent(MatchEventType.Block, blocker, b.Team));
            Events.Add(new MatchEvent(MatchEventType.BallLoose, -1, -1));
        }

        // ------------------------------------------------------------------ rebounding

        /// <summary>True when this player is sealing an opponent away from the rim on a rebound.</summary>
        public bool IsBoxingOut(int player)
        {
            bool reboundSituation = Ball.Phase == BallPhase.Shot || (Ball.Phase == BallPhase.Loose && _reboundable);
            if (!reboundSituation) return false;
            var p = Players[player];
            var hoop = Setup.Court.Hoop;
            float myDist = Vec2.Distance(p.Position, hoop);
            if (myDist > 5f) return false;
            for (int i = 0; i < Players.Length; i++)
            {
                var o = Players[i];
                if (o.Team == p.Team) continue;
                if (Vec2.Distance(o.Position, p.Position) <= Setup.Defense.boxOutRadius && Vec2.Distance(o.Position, hoop) > myDist)
                    return true;
            }
            return false;
        }

        /// <summary>Who wins a contested rebound: reach, rebounding rating, box-out, and a little luck.</summary>
        private int PickRebounder()
        {
            var t = Setup.Ball;
            var d = Setup.Defense;
            if (Ball.Height > t.pickupMaxHeight) return -1;
            int best = -1;
            float bestScore = float.MinValue;
            for (int i = 0; i < Players.Length; i++)
            {
                if (Setup.PassiveOpponents && Players[i].Team != Setup.HumanTeam) continue;
                if (IsBenched(i)) continue;
                float dist = Vec2.Distance(Ball.Position, Players[i].Position);
                if (dist > t.pickupRadius + d.reboundReach) continue;
                float score = RatingScale.Normalized(Players[i].Def.attributes.rebounding) * 0.6f - dist * 0.8f
                              + (IsBoxingOut(i) ? d.boxOutBonus : 0f) + _rng.NextFloat() * 0.2f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            // Must actually be in reach unless boxing out gives the edge.
            if (best >= 0 && Vec2.Distance(Ball.Position, Players[best].Position) > t.pickupRadius && !IsBoxingOut(best)) return -1;
            return best;
        }

        // ------------------------------------------------------------------ stamina

        private void UpdateStamina(PlayerRuntimeState p, float maxSpeed, float dt, bool deadBall)
        {
            var t = Setup.Defense;
            if (deadBall)
            {
                p.Stamina = Math.Min(1f, p.Stamina + t.staminaDeadBallRecover * dt);
                return;
            }
            float ratio = maxSpeed > 0f ? p.Motion.velocity.Magnitude / maxSpeed : 0f;
            if (ratio > 0.7f)
            {
                // Better stamina ratings drain slower.
                float drain = t.staminaDrainPerSecond * (1.2f - 0.6f * RatingScale.Normalized(p.Def.attributes.stamina));
                p.Stamina = Math.Max(0f, p.Stamina - drain * dt);
            }
            else
            {
                p.Stamina = Math.Min(1f, p.Stamina + t.staminaRecoverPerSecond * dt);
            }
        }

        private float StaminaSpeedFactor(PlayerRuntimeState p) => 1f - Setup.Defense.staminaSpeedPenalty * (1f - p.Stamina);

        // ------------------------------------------------------------------ plays

        /// <summary>Calls a play for the human's team (CALL button). Offense only.</summary>
        public bool CallPlay(PlayCall play) => StartPlay(Setup.HumanTeam, play, ControlledIndex);

        private bool StartPlay(int team, PlayCall play, int ballHandler)
        {
            if (play == PlayCall.None || Phase != MatchPhase.Live || OffenseTeam != team) return false;
            _play = play;
            _playTeam = team;
            _screener = -1;
            _screenRolling = false;
            _screenSet = false;
            _giveAndGoPartner = -1;
            _playStart = Time;
            _playMate = -1;
            _backdoorCut = false;
            switch (play)
            {
                case PlayCall.PickAndRoll:
                    _screener = BestScreener(team, ballHandler);
                    _playUntil = Time + 3.5f;
                    break;
                case PlayCall.Backdoor:
                    _playMate = BestBy(team, ballHandler, p => p.Tendencies.cut + p.Def.attributes.speed / 200f);
                    _playUntil = Time + 4.5f;
                    break;
                case PlayCall.PostUp:
                    _playMate = BestBy(team, ballHandler, p => p.Def.attributes.finishing + p.Def.attributes.rebounding);
                    _playUntil = Time + 5f;
                    break;
                case PlayCall.GiveAndGo:
                    _playUntil = Time + 5f;
                    break;
                default:
                    _playUntil = Time + 5f;
                    break;
            }
            for (int i = 0; i < Players.Length; i++)
                if (Players[i].Team == team) _ai[i].NextDecision = Time; // react now
            Events.Add(new MatchEvent(MatchEventType.PlayCalled, ballHandler, team, (int)play));
            return true;
        }

        private int BestBy(int team, int exclude, Func<PlayerRuntimeState, float> score)
        {
            int best = -1;
            float bestScore = float.MinValue;
            for (int slot = 0; slot < TeamSize; slot++)
            {
                int i = Index(team, slot);
                if (i == exclude || (Ball.IsHeld && i == Ball.HolderIndex) || IsBenched(i)) continue;
                float s = score(Players[i]);
                if (s > bestScore) { bestScore = s; best = i; }
            }
            return best;
        }

        private int BestScreener(int team, int exclude)
        {
            int best = -1;
            float bestScore = float.MinValue;
            for (int slot = 0; slot < TeamSize; slot++)
            {
                int i = Index(team, slot);
                if (i == exclude || (Ball.IsHeld && i == Ball.HolderIndex)) continue;
                float s = Players[i].Tendencies.screen;
                if (s > bestScore)
                {
                    bestScore = s;
                    best = i;
                }
            }
            return best;
        }

        private void UpdatePlay()
        {
            if (_play == PlayCall.None) return;
            if (Time > _playUntil || OffenseTeam != _playTeam || Phase != MatchPhase.Live)
            {
                EndPlay();
                return;
            }

            // Backdoor: the moment the fake is over, the cutter goes (don't wait for their next decision).
            if (_play == PlayCall.Backdoor && !_backdoorCut && _playMate >= 0 && Time - _playStart >= BackdoorFakeSeconds)
            {
                _backdoorCut = true;
                _ai[_playMate].NextDecision = Time;
            }

            if (_play == PlayCall.PickAndRoll && _screener >= 0 && !_screenRolling && Ball.IsHeld)
            {
                var screener = Players[_screener];
                var handler = Players[Ball.HolderIndex];
                if (!_screenSet)
                {
                    // The screen counts once the screener has planted at the spot.
                    if (Vec2.Distance(screener.Position, _ai[_screener].Target) < 0.4f && _ai[_screener].Intent == AiIntent.Screen)
                    {
                        _screenSet = true;
                        _screenSetAt = Time;
                        _playUntil = Math.Max(_playUntil, Time + 2.5f);
                    }
                    return;
                }
                // Defenders who run into the screener get held up.
                for (int i = 0; i < Players.Length; i++)
                {
                    var d = Players[i];
                    if (d.Team == screener.Team) continue;
                    if (Vec2.Distance(d.Position, screener.Position) <= Setup.Defense.screenRadius && d.ScreenedUntil < Time)
                    {
                        d.ScreenedUntil = Time + Setup.Defense.screenSeconds;
                        Events.Add(new MatchEvent(MatchEventType.Screen, _screener, screener.Team));
                    }
                }
                // Once the handler comes off the screen (or it has been held long enough), roll to the rim.
                float held = Time - _screenSetAt;
                if ((held > 0.4f && Vec2.Distance(handler.Position, screener.Position) < 1.3f) || held > 1.8f)
                {
                    _screenRolling = true;
                    _ai[_screener].NextDecision = Time;
                }
            }
        }

        private void EndPlay()
        {
            _play = PlayCall.None;
            _playTeam = -1;
            _screener = -1;
            _screenRolling = false;
            _screenSet = false;
            _giveAndGoPartner = -1;
            _playMate = -1;
        }

        /// <summary>AI off-ball behaviour while a play is running; returns false when not involved.</summary>
        private bool PlayIntentFor(PlayerRuntimeState p, AiState s)
        {
            if (_play == PlayCall.None || p.Team != _playTeam) return false;
            var court = Setup.Court;
            switch (_play)
            {
                case PlayCall.PickAndRoll:
                    if (p.Index != _screener || !Ball.IsHeld) return false;
                    var handler = Players[Ball.HolderIndex];
                    if (_screenRolling)
                    {
                        s.Intent = AiIntent.Cut;
                        s.Target = CutSpot(p, court);
                        return true;
                    }
                    // Set the screen on the handler's defender, on the side toward the middle.
                    int defender = DefenderOf(handler.Index);
                    var anchor = defender >= 0 ? Players[defender].Position : handler.Position + new Vec2(0f, -1f);
                    float side = handler.Position.x > 0f ? -1f : 1f;
                    s.Intent = AiIntent.Screen;
                    s.Target = court.Clamp(anchor + new Vec2(side * 0.55f, 0.35f));
                    return true;

                case PlayCall.Backdoor:
                    if (Ball.IsHeld && p.Index == Ball.HolderIndex) return false;
                    if (p.Index == _playMate)
                    {
                        float wing = p.Position.x >= 0f ? 1f : -1f;
                        if (Time - _playStart < BackdoorFakeSeconds)
                        {
                            // Sell it: jog out high on the wing, pulling the defender away from the rim.
                            s.Intent = AiIntent.Space;
                            s.Target = court.Clamp(new Vec2(wing * (court.CornerLineX - 0.4f), court.hoopY + 5.6f));
                        }
                        else
                        {
                            s.Intent = AiIntent.Cut;
                            s.Target = court.Clamp(new Vec2(wing * 0.5f, court.hoopY + 0.9f));
                        }
                        return true;
                    }
                    // Everyone else stays wide so the lane is empty.
                    s.Intent = AiIntent.Space;
                    s.Target = new Vec2((p.Position.x >= 0f ? 1f : -1f) * (court.CornerLineX + 0.3f), 1.2f);
                    return true;

                case PlayCall.PostUp:
                    if (Ball.IsHeld && p.Index == Ball.HolderIndex) return false;
                    float ballSide = Ball.Position.x >= 0f ? 1f : -1f;
                    if (p.Index == _playMate)
                    {
                        // Seal on the low block on the ball side.
                        s.Intent = AiIntent.Hold;
                        s.Target = court.Clamp(new Vec2(ballSide * 1.7f, court.hoopY + 1.3f));
                        return true;
                    }
                    s.Intent = AiIntent.Space;
                    s.Target = new Vec2(-ballSide * (court.CornerLineX + 0.3f), 1.0f);
                    return true;

                case PlayCall.ClearOut:
                    if (Ball.IsHeld && p.Index == Ball.HolderIndex) return false;
                    s.Intent = AiIntent.Space;
                    float x = p.Slot == 1 ? -1f : (p.Slot == 2 ? 1f : (p.Position.x >= 0f ? 1f : -1f));
                    s.Target = new Vec2(x * (court.CornerLineX + 0.3f), 1.0f);
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Smart AI sends a second defender at a heated-up scorer near the basket (the nearest off-ball
        /// defender doubles from the middle). Somebody is open: find them.
        /// </summary>
        private bool TrapHotHand(PlayerRuntimeState p, AiState s, PlayerRuntimeState holder, DifficultyDef profile)
        {
            if (holder == null || holder.Team == p.Team || IsBenched(p.Index) || !IsHeatedUp(holder.Index)) return false;
            if (profile == null || profile.decisionQuality < Setup.Defense.trapMinQuality) return false;
            var court = Setup.Court;
            if (court.DistanceToHoop(holder.Position) > Setup.Defense.trapRange) return false;
            int onBall = DefenderOf(holder.Index);
            if (onBall == p.Index) return false;
            int nearest = -1;
            float best = float.MaxValue;
            for (int i = 0; i < Players.Length; i++)
            {
                if (Players[i].Team != p.Team || i == onBall || IsBenched(i)) continue;
                float d = Vec2.Distance(Players[i].Position, holder.Position);
                if (d < best) { best = d; nearest = i; }
            }
            if (nearest != p.Index) return false;
            // Double from the middle of the floor, a step off the ball.
            float side = holder.Position.x > 0f ? -1f : 1f;
            s.Intent = AiIntent.Help;
            s.Target = court.Clamp(holder.Position + new Vec2(side * 0.9f, -0.2f));
            if (holder.Index != _lastTrapHolder || Time - _lastTrapAt > 4f)
                Events.Add(new MatchEvent(MatchEventType.Trap, p.Index, p.Team, holder.Index));
            _lastTrapHolder = holder.Index;
            _lastTrapAt = Time;
            return true;
        }

        private int DefenderOf(int offensePlayer)
        {
            for (int i = 0; i < Players.Length; i++)
                if (Players[i].Team != Players[offensePlayer].Team && _guarding[i] == offensePlayer) return i;
            return -1;
        }

        /// <summary>Give-and-go: after the human passes during the play, the receiver hits them back on the cut.</summary>
        private bool GiveAndGoReturn(PlayerRuntimeState handler)
        {
            int humanIndex = HumanIndexOf(_playTeam);
            if (_play != PlayCall.GiveAndGo || handler.Team != _playTeam || humanIndex < 0 || handler.Index == humanIndex) return false;
            if (_giveAndGoPartner != handler.Index) return false;
            var human = Players[humanIndex];
            float humanToHoop = Setup.Court.DistanceToHoop(human.Position);
            if (humanToHoop < 3.5f || OpennessOf(human) > 1.8f)
            {
                PassFrom(handler.Index, Vec2.Zero, humanIndex);
                EndPlay();
                return true;
            }
            return false;
        }

        private void OnPassForPlays(int passer, int receiver)
        {
            if (_play == PlayCall.GiveAndGo && passer == HumanIndexOf(_playTeam) && Players[receiver].Team == _playTeam)
                _giveAndGoPartner = receiver;
        }
    }
}
