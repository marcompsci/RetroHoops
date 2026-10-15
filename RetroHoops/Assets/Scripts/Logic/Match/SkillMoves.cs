using System;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// Offensive skill moves for the person with the ball (14 Oct 2026, Omari's request). They're done with the
    /// movement stick, recognised by <see cref="StickGestures"/>:
    /// - SPIN: a burst around your man. The ball is hard to poke for a moment, and he may be spun past (frozen).
    /// - JAB: a jab step. He may bite and sag off balance (no contest for a moment), then a quick first step.
    /// - HESI (secret): stop and go. He may freeze, then you explode.
    /// - STEP-BACK (secret): you hop back away from the rim, so the next jumper gets more room. He may lose you.
    /// - SHAKE (secret): a wiggle that can break ankles (in any rules, not only street).
    /// Each move works on the closest defender in range. The chance comes from your handle against his defense,
    /// and the same move works less often each time in a possession, like pump fakes. Only people do moves
    /// (the AI doesn't), so AI-vs-AI games and difficulty tuning are unchanged.
    /// Bank layups: a layup from the side (between <see cref="BankMinAngle"/> and <see cref="BankMaxAngle"/> off the
    /// baseline) goes off the glass. The board shields it a little from the contest, and the ball really hits it.
    /// </summary>
    public sealed partial class MatchSimulation
    {
        public const float SkillRange = 2.3f;
        public const float SkillCooldown = 0.7f;
        public const float SpinSeconds = 0.42f;
        public const float StepBackSeconds = 0.22f;
        public const float StepBackSpace = 0.9f;
        public const float StepBackSpaceSeconds = 1.3f;
        public const float CrossoverCooldown = 0.15f;
        /// <summary>Share of beaten crossovers (in street rules) that break ankles.</summary>
        public const float CrossoverAnkleShare = 0.45f;
        /// <summary>SHOOT double tap: after a pump fake, press SHOOT again within this long for a step-back.</summary>
        public const float StepBackTapWindow = 0.35f;
        public const float BankMinAngle = 18f;
        public const float BankMaxAngle = 68f;
        public const float BankMinDistance = 0.6f;
        /// <summary>How much of the contest the glass takes away on a bank layup.</summary>
        public const float BankContestShield = 0.3f;

        private StickGestures[] _gestures;
        private float[] _skillReadyAt, _skillUntil, _burstUntil, _burstFactor, _spaceUntil, _protectUntil;
        private SkillMoveKind[] _skillKind;
        private Vec2[] _skillDir;
        private readonly int[] _skillRepeats = new int[16];

        /// <summary>The move <paramref name="player"/> is doing right now (None when not).</summary>
        public SkillMoveKind SkillMoveOf(int player) =>
            _skillKind != null && player >= 0 && player < Players.Length && Time < _skillUntil[player] ? _skillKind[player] : SkillMoveKind.None;

        /// <summary>Times each move has been used this possession (moves work less often when repeated).</summary>
        public int SkillRepeats(SkillMoveKind kind) => _skillRepeats[(int)kind];

        private void EnsureSkillState()
        {
            if (_gestures != null) return;
            int n = Players.Length;
            _gestures = new StickGestures[n];
            for (int i = 0; i < n; i++) _gestures[i] = new StickGestures();
            _skillReadyAt = new float[n];
            _skillUntil = new float[n];
            _burstUntil = new float[n];
            _burstFactor = new float[n];
            _spaceUntil = new float[n];
            _protectUntil = new float[n];
            _skillKind = new SkillMoveKind[n];
            _skillDir = new Vec2[n];
            for (int i = 0; i < n; i++) { _skillUntil[i] = _burstUntil[i] = _spaceUntil[i] = _protectUntil[i] = -99f; _burstFactor[i] = 1f; }
        }

        /// <summary>A new possession: defenders fall for every move again.</summary>
        private void ResetSkillRepeats()
        {
            Array.Clear(_skillRepeats, 0, _skillRepeats.Length);
            if (_stepShotAt != null)
                for (int i = 0; i < _stepShotAt.Length; i++) { _stepShotAt[i] = -99f; _fakeAt[i] = -99f; }
        }

        /// <summary>Feeds the stick for a person holding the ball (no button pressed this step); runs a move when one lands.</summary>
        private void FeedSkillMoves(int me, PlayerInput input, bool holding)
        {
            EnsureSkillState();
            if (!holding || Phase != MatchPhase.Live || MustInbound)
            {
                _gestures[me].Clear();
                return;
            }
            var p = Players[me];
            var toHoop = Setup.Court.Hoop - p.Position;
            var kind = _gestures[me].Feed(Time, FrameMove(input.Move), toHoop, p.Motion.velocity.Magnitude);
            if (kind == SkillMoveKind.None || Time < _skillReadyAt[me] || Time < p.StunnedUntil) return;
            DoSkillMove(me, kind);
        }

        /// <summary>Runs a move for <paramref name="me"/> (also used by tests and the Practice Lab).</summary>
        public void DoSkillMove(int me, SkillMoveKind kind) => DoSkillMove(me, kind, false);

        /// <param name="force">A step-back from the SHOOT double tap happens even with nobody close.</param>
        private void DoSkillMove(int me, SkillMoveKind kind, bool force)
        {
            EnsureSkillState();
            var p = Players[me];
            var hoop = Setup.Court.Hoop;
            var toHoop = (hoop - p.Position).SqrMagnitude > 0.0001f ? (hoop - p.Position).Normalized : new Vec2(0f, -1f);
            int victim = SkillTarget(p);

            // A step-back (from the stick) and a crossover only make sense with someone on you.
            if ((kind == SkillMoveKind.StepBack && !force || kind == SkillMoveKind.Crossover) && victim < 0) return;

            // A crossover can be followed straight away by the cross back (the SHAKE).
            _skillReadyAt[me] = Time + (kind == SkillMoveKind.Crossover ? CrossoverCooldown : SkillCooldown);
            _skillKind[me] = kind;
            int repeats = _skillRepeats[(int)kind]++;
            bool beat = false;
            if (victim >= 0)
            {
                var d = Players[victim];
                float chance = BeatChance(p, d, kind, repeats);
                if (IsHumanControlled(victim)) chance *= 0.6f; // a person defending reads it better
                beat = _rng.NextFloat() < chance;
            }

            switch (kind)
            {
                case SkillMoveKind.Spin:
                {
                    _skillUntil[me] = Time + SpinSeconds;
                    _protectUntil[me] = Time + SpinSeconds;
                    Burst(me, 1.25f, SpinSeconds + 0.15f);
                    // Spin to the side away from the defender, still going at the rim.
                    var perp = new Vec2(-toHoop.y, toHoop.x);
                    float side = 1f;
                    if (victim >= 0)
                    {
                        var rel = Players[victim].Position - p.Position;
                        side = rel.x * perp.x + rel.y * perp.y > 0f ? -1f : 1f;
                    }
                    _skillDir[me] = (toHoop * 0.75f + perp * (0.66f * side)).Normalized;
                    if (beat) Players[victim].StunnedUntil = Math.Max(Players[victim].StunnedUntil, Time + 0.5f);
                    break;
                }
                case SkillMoveKind.Jab:
                    _skillUntil[me] = Time + 0.3f;
                    Burst(me, 1.3f, 0.6f);
                    if (beat)
                    {
                        var d = Players[victim];
                        d.FakedUntil = Math.Max(d.FakedUntil, Time + 0.55f);
                        d.StunnedUntil = Math.Max(d.StunnedUntil, Time + 0.45f);
                    }
                    break;
                case SkillMoveKind.Hesi:
                    _skillUntil[me] = Time + 0.3f;
                    Burst(me, 1.35f, 0.6f);
                    if (beat) Players[victim].StunnedUntil = Math.Max(Players[victim].StunnedUntil, Time + 0.45f);
                    break;
                case SkillMoveKind.StepBack:
                {
                    _skillUntil[me] = Time + StepBackSeconds;
                    _skillDir[me] = toHoop * -1f;
                    _spaceUntil[me] = Time + StepBackSpaceSeconds;
                    p.Motion.velocity = toHoop * -5.2f;
                    p.Motion.facing = Movement.FacingOf(toHoop);
                    if (beat)
                    {
                        var d = Players[victim];
                        d.FakedUntil = Math.Max(d.FakedUntil, Time + 0.7f);
                        d.StunnedUntil = Math.Max(d.StunnedUntil, Time + 0.3f);
                    }
                    break;
                }
                case SkillMoveKind.Crossover:
                {
                    // Ball across, a hard plant and a burst the other way. Beat him and he's left reaching; in street
                    // rules a big cross can put him on the floor.
                    _skillUntil[me] = Time + 0.3f;
                    Burst(me, 1.3f, 0.5f);
                    if (beat)
                    {
                        var d = Players[victim];
                        bool ankles = (Setup.StreetRules || StyleOn) && Vec2.Distance(d.Position, p.Position) < 1.9f
                                      && _rng.NextFloat() < CrossoverAnkleShare;
                        if (ankles)
                        {
                            d.StunnedUntil = Math.Max(d.StunnedUntil, Time + AnkleStumbleSeconds);
                            Stats[me].ankleBreakers++;
                            Events.Add(new MatchEvent(MatchEventType.AnkleBreaker, me, p.Team, victim));
                            Events.Add(new MatchEvent(MatchEventType.SkillMove, me, p.Team, (int)kind, victim));
                            return;
                        }
                        d.StunnedUntil = Math.Max(d.StunnedUntil, Time + 0.4f);
                    }
                    break;
                }
                default: // Shake
                    _skillUntil[me] = Time + 0.4f;
                    Burst(me, 1.2f, 0.45f);
                    if (beat)
                    {
                        Players[victim].StunnedUntil = Math.Max(Players[victim].StunnedUntil, Time + AnkleStumbleSeconds);
                        Stats[me].ankleBreakers++;
                        Events.Add(new MatchEvent(MatchEventType.AnkleBreaker, me, p.Team, victim));
                    }
                    break;
            }
            Events.Add(new MatchEvent(MatchEventType.SkillMove, me, p.Team, (int)kind, victim));
            if (beat && kind != SkillMoveKind.Shake) Events.Add(new MatchEvent(MatchEventType.DefenderBeaten, me, p.Team, victim));
        }

        // ------------------------------------------------------------------ SHOOT double tap: the step-back jumper

        private float[] _fakeAt, _stepShotAt;

        /// <summary>
        /// 15 Oct 2026 (Omari): tap SHOOT (a pump fake), then press it again quickly: a step-back. Keep holding the
        /// second press and he rises into the jumper as soon as he lands, with room to shoot.
        /// </summary>
        private bool TryStepBackShot(int me)
        {
            if (_fakeAt == null || Time - _fakeAt[me] > StepBackTapWindow || Time < _fakeAt[me]) return false;
            _fakeAt[me] = -99f;
            DoSkillMove(me, SkillMoveKind.StepBack, true);
            if (SkillMoveOf(me) != SkillMoveKind.StepBack) return false;
            _stepShotAt[me] = Time + StepBackSeconds;
            return true;
        }

        private void NoteFake(int shooter)
        {
            EnsureStepShot();
            _fakeAt[shooter] = Time;
        }

        private void EnsureStepShot()
        {
            if (_fakeAt != null) return;
            _fakeAt = new float[Players.Length];
            _stepShotAt = new float[Players.Length];
            for (int i = 0; i < Players.Length; i++) _fakeAt[i] = _stepShotAt[i] = -99f;
        }

        /// <summary>A step-back from the double tap is landing: still holding SHOOT, he goes up for the jumper.</summary>
        private void LandStepBackShot(int me, PlayerInput input)
        {
            if (_stepShotAt == null || _stepShotAt[me] < 0f || Time < _stepShotAt[me]) return;
            _stepShotAt[me] = -99f;
            if (input.ShootHeld && CanShoot(me)) BeginCharge(me, -1f);
        }

        /// <summary>True while a SHOOT double-tap step-back is in the air.</summary>
        public bool StepBackShotPending(int me) => _stepShotAt != null && me >= 0 && me < Players.Length && _stepShotAt[me] >= 0f;

        /// <summary>Seconds between moves for an AI handler.</summary>
        public const float AiSkillCooldown = 2.5f;

        /// <summary>
        /// An AI ball handler guarded closely by a person sometimes puts a move on him: more often on harder
        /// difficulties and for better handlers. Drivers spin and hesi, shooters jab and step back. Only against a
        /// person, so AI-vs-AI games (and their tuning) don't change. The handler still makes his normal decision
        /// afterwards (drive, shoot or pass), so this always returns false.
        /// </summary>
        private bool TryAiSkillMove(PlayerRuntimeState p, DifficultyDef profile, float distToHoop)
        {
            if (Coaching || IsHumanControlled(p.Index) || ChargingIndex >= 0 || MustClear || MustInbound) return false;
            EnsureSkillState();
            if (Time < _skillReadyAt[p.Index]) return false;
            int d = SkillTarget(p);
            if (d < 0 || !IsHumanControlled(d) || Vec2.Distance(Players[d].Position, p.Position) > 1.8f) return false;
            float quality = profile != null ? Math.Max(0f, Math.Min(1f, profile.decisionQuality)) : 0.5f;
            float chance = (0.04f + 0.12f * quality) * (0.3f + p.Def.attributes.playmaking / 100f);
            if (_rng.NextFloat() >= chance) return false;
            var tend = p.Tendencies;
            SkillMoveKind kind;
            float roll = _rng.NextFloat();
            if (distToHoop > 5.5f && tend.shoot >= tend.drive) kind = roll < 0.55f ? SkillMoveKind.StepBack : SkillMoveKind.Jab;
            else kind = roll < 0.35f ? SkillMoveKind.Spin : roll < 0.6f ? SkillMoveKind.Crossover : roll < 0.88f ? SkillMoveKind.Hesi : SkillMoveKind.Shake;
            DoSkillMove(p.Index, kind);
            if (SkillMoveOf(p.Index) == SkillMoveKind.None) return false;
            _skillReadyAt[p.Index] = Time + AiSkillCooldown;
            // After the move, attack: the next decision comes after it's done.
            _ai[p.Index].NextDecision = Time + 0.3f;
            return false;
        }

        private void Burst(int me, float factor, float seconds)
        {
            _burstFactor[me] = factor;
            _burstUntil[me] = Time + seconds;
        }

        /// <summary>The closest defender in <see cref="SkillRange"/> who isn't already down, or -1.</summary>
        private int SkillTarget(PlayerRuntimeState handler)
        {
            if (Setup.PassiveOpponents) return -1;
            int best = -1;
            float bestDist = SkillRange;
            for (int i = 0; i < Players.Length; i++)
            {
                var o = Players[i];
                if (o.Team == handler.Team || IsBenched(i) || Time < o.StunnedUntil) continue;
                float dist = Vec2.Distance(o.Position, handler.Position);
                if (dist <= bestDist) { bestDist = dist; best = i; }
            }
            return best;
        }

        /// <summary>
        /// The chance a move beats the defender. Your handle (playmaking, plus speed for the spin and hesi, shooting
        /// for the jab and step-back) against his defense, cut by 30% for each earlier use of the move this possession.
        /// </summary>
        public static float BeatChance(PlayerRuntimeState handler, PlayerRuntimeState defender, SkillMoveKind kind, int repeats)
        {
            var a = handler.Def.attributes;
            float second;
            switch (kind)
            {
                case SkillMoveKind.Spin:
                case SkillMoveKind.Hesi: second = a.speed; break;
                case SkillMoveKind.Jab:
                case SkillMoveKind.StepBack: second = a.shooting; break;
                case SkillMoveKind.Crossover: second = a.speed * 0.5f + a.playmaking * 0.5f; break;
                default: second = a.playmaking; break;
            }
            float handle = (a.playmaking * 0.65f + second * 0.35f) / 100f;
            float feet = (defender.Def.attributes.defense * 0.7f + defender.Def.attributes.speed * 0.3f) / 100f;
            float chance = 0.42f + (handle - feet) * 0.9f;
            if (kind == SkillMoveKind.Shake) chance -= 0.12f; // breaking ankles is the big prize
            if (kind == SkillMoveKind.Crossover) chance -= 0.04f;
            for (int k = 0; k < repeats; k++) chance *= 0.7f;
            return Math.Max(0.06f, Math.Min(0.8f, chance));
        }

        /// <summary>The stick, unless a move is steering this player (a spin's arc, a step-back hop).</summary>
        private Vec2 SkillDesired(int i, Vec2 stick)
        {
            if (_skillKind == null || Time >= _skillUntil[i]) return stick;
            switch (_skillKind[i])
            {
                case SkillMoveKind.Spin:
                case SkillMoveKind.StepBack: return _skillDir[i];
                default: return stick;
            }
        }

        /// <summary>Top-speed multiplier from a move's burst.</summary>
        private float SkillSpeedFactor(int i) => _burstFactor != null && Time < _burstUntil[i] ? _burstFactor[i] : 1f;

        /// <summary>A spinning handler keeps the ball away from reaching hands.</summary>
        private bool SkillProtected(int i) => _protectUntil != null && Time < _protectUntil[i];

        /// <summary>Extra room (m) on a jumper right after a step-back.</summary>
        private float SkillSpace(int i) => _spaceUntil != null && Time < _spaceUntil[i] ? StepBackSpace : 0f;

        /// <summary>
        /// A layup from the side goes off the glass: between <see cref="BankMinAngle"/> and <see cref="BankMaxAngle"/>
        /// off the baseline, and not right under the rim.
        /// </summary>
        public static bool IsBankAngle(Vec2 shooter, Vec2 hoop)
        {
            var rel = shooter - hoop;
            float dist = rel.Magnitude;
            if (dist < BankMinDistance || rel.y <= 0f) return false;
            double angle = Math.Atan2(rel.y, Math.Abs(rel.x)) * 180.0 / Math.PI;
            return angle >= BankMinAngle && angle <= BankMaxAngle;
        }

        /// <summary>Where a bank shot kisses the glass: on the board, on the shooter's side of the rim.</summary>
        public static Vec2 BankSpot(Vec2 shooter, Vec2 hoop) =>
            new Vec2(hoop.x + (shooter.x >= hoop.x ? 0.28f : -0.28f), hoop.y - 0.3f);

        /// <summary>Height (m) of the kiss off the glass.</summary>
        public const float BankHeight = RimHeight + 0.45f;
        /// <summary>Share of a bank shot's flight spent getting to the glass.</summary>
        public const float BankSplit = 0.78f;
    }
}
