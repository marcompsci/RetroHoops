using System;

namespace CallerRetroBall.Logic
{
    /// <summary>A TRICK in THE STREETS (the TRICK button; the stick picks which one).</summary>
    public enum StreetTrick
    {
        None = 0,
        /// <summary>Stick to the side: behind the back.</summary>
        BehindBack = 1,
        /// <summary>Stick away from the rim: through the legs and back.</summary>
        ThroughLegs = 2,
        /// <summary>Stick at the rim with a defender in the way: off his head and back to you.</summary>
        OffTheHead = 3,
        /// <summary>Stick still: the ball goes round the waist, all show.</summary>
        Halo = 4,
        /// <summary>Stick at the rim, nobody in the way: roll it down the arm and go.</summary>
        RollTheArm = 5,
        /// <summary>Phase 2: driving at the rim from close range: throw it off the backboard and finish your own alley-oop.</summary>
        OffTheGlass = 6,
    }

    /// <summary>
    /// 15 Oct 2026 (Omari): THE STREETS. These are the style rules, an original take on the old street-ball
    /// arcade games:
    /// - <b>Tricks</b> with the ball (the TRICK button; stick skill moves count too) build a combo. The combo gets
    ///   bigger as you chain tricks, and the same trick twice in a possession is worth less.
    /// - <b>Score to bank it.</b> A bucket adds the combo and the shot's own style (dunk, alley-oop, 2) to your team's
    ///   STREET METER. Lose the ball first and the combo is gone.
    /// - Defense earns style straight into the meter: blocks and steals.
    /// - <b>SHOWSTOPPER</b>: with a full meter, the team on offense can call it. The next bucket counts one extra
    ///   point and takes one away from the other team. Keep filling the meter to level 2 before using it and it's
    ///   two each way. Miss, or lose the ball, and it's gone. The shooter gets a boost while it's on.
    /// Everything here is decided by the simulation itself (events in, events out), so two phones playing the
    /// same inputs see the same meters (LIVE and TWO PHONES).
    /// </summary>
    public sealed partial class MatchSimulation
    {
        public const int MeterLevel = 1000;
        public const int MaxBreakerLevel = 2;
        /// <summary>Make-chance boost for the shooter while his team's SHOWSTOPPER is on.</summary>
        public const float BreakerShotBonus = 0.18f;
        public const float TrickCooldown = 0.55f;
        public const float TrickRange = 2.4f;

        /// <summary>The trick name as the MC calls it.</summary>
        public static string TrickName(StreetTrick t)
        {
            switch (t)
            {
                case StreetTrick.BehindBack: return "BEHIND THE BACK";
                case StreetTrick.ThroughLegs: return "THROUGH THE LEGS";
                case StreetTrick.OffTheHead: return "OFF THE HEAD!";
                case StreetTrick.Halo: return "HALO";
                case StreetTrick.RollTheArm: return "ROLL THE ARM";
                case StreetTrick.OffTheGlass: return "OFF THE GLASS!";
                default: return "";
            }
        }

        /// <summary>Base style points for each trick.</summary>
        public static int TrickPoints(StreetTrick t)
        {
            switch (t)
            {
                case StreetTrick.BehindBack: return 70;
                case StreetTrick.ThroughLegs: return 70;
                case StreetTrick.OffTheHead: return 240;
                case StreetTrick.Halo: return 110;
                case StreetTrick.RollTheArm: return 90;
                case StreetTrick.OffTheGlass: return 260;
                default: return 0;
            }
        }

        public const int StyleSkillMove = 45, StyleBeat = 35, StyleAnkles = 160, StyleDunk = 110, StyleAlleyOop = 220,
                         StyleTwo = 60, StyleBlock = 130, StyleSteal = 110, StyleBankKiss = 40;

        /// <summary>Each team's STREET METER (0 .. <see cref="MeterLevel"/> × <see cref="MaxBreakerLevel"/>).</summary>
        public readonly int[] StyleMeter = new int[2];
        /// <summary>The combo waiting to be banked by a bucket, and how many tricks are in it.</summary>
        public readonly int[] ComboPoints = new int[2];
        public readonly int[] ComboCount = new int[2];
        /// <summary>The team with a SHOWSTOPPER on (-1 = none) and its level (1 or 2).</summary>
        public int BreakerTeam { get; private set; } = -1;
        public int BreakerLevel { get; private set; }
        /// <summary>SHOWSTOPPERs scored by each team this game.</summary>
        public readonly int[] BreakersScored = new int[2];

        private readonly int[] _trickRepeats = new int[8];
        private float[] _trickReadyAt;

        public bool StyleOn => Setup.StyleMeter;

        /// <summary>Levels of SHOWSTOPPER a team could call now (0 = meter not full).</summary>
        public int BreakerReady(int team) => !StyleOn || team < 0 || team > 1 ? 0 : Math.Min(MaxBreakerLevel, StyleMeter[team] / MeterLevel);

        /// <summary>True when <paramref name="team"/> may call its SHOWSTOPPER right now.</summary>
        public bool CanCallBreaker(int team) =>
            StyleOn && BreakerTeam < 0 && BreakerReady(team) > 0 && OffenseTeam == team && Phase == MatchPhase.Live
            && Ball.IsHeld && Players[Ball.HolderIndex].Team == team && ChargingIndex < 0;

        /// <summary>Calls the SHOWSTOPPER for <paramref name="team"/> (uses every full level of the meter).</summary>
        public bool CallBreaker(int team)
        {
            if (!CanCallBreaker(team)) return false;
            int level = BreakerReady(team);
            StyleMeter[team] -= level * MeterLevel;
            BreakerTeam = team;
            BreakerLevel = level;
            Events.Add(new MatchEvent(MatchEventType.BreakerOn, Ball.HolderIndex, team, level));
            return true;
        }

        private float BreakerBonusFor(int shooter) =>
            StyleOn && BreakerTeam >= 0 && Players[shooter].Team == BreakerTeam ? BreakerShotBonus : 0f;

        // ------------------------------------------------------------------ tricks

        /// <summary>Which trick the stick asks for (relative to the rim and the nearest defender).</summary>
        public StreetTrick TrickFor(int me, Vec2 stick)
        {
            var p = Players[me];
            var toHoop = Setup.Court.Hoop - p.Position;
            if (stick.SqrMagnitude < 0.09f) return StreetTrick.Halo;
            if (toHoop.SqrMagnitude < 0.0001f) return StreetTrick.BehindBack;
            var dir = stick.Normalized;
            var h = toHoop.Normalized;
            float along = Vec2.Dot(dir, h);
            if (along < -0.5f) return StreetTrick.ThroughLegs;
            if (along > 0.5f)
            {
                int d = SkillTarget(p);
                if (d >= 0 && Vec2.Distance(Players[d].Position, p.Position) < 1.7f)
                {
                    var toD = Players[d].Position - p.Position;
                    if (toD.SqrMagnitude > 0.0001f && Vec2.Dot(toD.Normalized, h) > 0.3f) return StreetTrick.OffTheHead;
                }
                if (CanGlassToss(me)) return StreetTrick.OffTheGlass;
                return StreetTrick.RollTheArm;
            }
            return StreetTrick.BehindBack;
        }

        /// <summary>
        /// Does a trick for the ball handler <paramref name="me"/>: a move on his defender (like the skill moves) plus
        /// style for the combo. Returns false when he can't trick now.
        /// </summary>
        public bool DoTrick(int me, StreetTrick trick)
        {
            if (!StyleOn || trick == StreetTrick.None) return false;
            if (!Ball.IsHeld || Ball.HolderIndex != me || Phase != MatchPhase.Live || ChargingIndex >= 0 || MustInbound) return false;
            EnsureSkillState();
            if (_trickReadyAt == null) _trickReadyAt = new float[Players.Length];
            var p = Players[me];
            if (Time < _trickReadyAt[me] || Time < p.StunnedUntil) return false;
            _trickReadyAt[me] = Time + TrickCooldown;

            int victim = SkillTarget(p);
            if (trick == StreetTrick.OffTheGlass && !CanGlassToss(me)) trick = StreetTrick.RollTheArm;
            StartTrickBall(me, trick, victim);
            switch (trick)
            {
                case StreetTrick.OffTheGlass:
                    GlassToss(me);
                    break;
                case StreetTrick.BehindBack: DoSkillMove(me, SkillMoveKind.Hesi); break;
                case StreetTrick.ThroughLegs: DoSkillMove(me, SkillMoveKind.Jab); break;
                case StreetTrick.RollTheArm: DoSkillMove(me, SkillMoveKind.Spin); break;
                case StreetTrick.OffTheHead:
                    // Off his head and back: he's stunned (a humiliation), and it's a lot of style.
                    if (victim >= 0)
                    {
                        var d = Players[victim];
                        d.StunnedUntil = Math.Max(d.StunnedUntil, Time + 0.9f);
                        d.FakedUntil = Math.Max(d.FakedUntil, Time + 0.9f);
                    }
                    Burst(me, 1.25f, 0.6f);
                    _skillReadyAt[me] = Time + SkillCooldown;
                    break;
                default: // Halo: pure show, no move on the defender.
                    _skillReadyAt[me] = Time + SkillCooldown;
                    break;
            }

            int repeats = _trickRepeats[(int)trick]++;
            int team = p.Team;
            float pts = TrickPoints(trick) * (1f + 0.25f * Math.Min(ComboCount[team], 8)) / (1 << Math.Min(repeats, 3));
            if (trick != StreetTrick.Halo && trick != StreetTrick.OffTheGlass && victim < 0) pts *= 0.6f; // nobody to show up
            int add = Math.Max(5, (int)pts);
            ComboPoints[team] += add;
            ComboCount[team]++;
            Stats[me].tricks++;
            Events.Add(new MatchEvent(MatchEventType.StreetTrick, me, team, (int)trick, add));
            return true;
        }

        /// <summary>
        /// The AI's ball handlers trick too in THE STREETS (so their meter fills and the SHOWSTOPPER can come back
        /// at you). Better handlers and harder difficulties trick more. Never returns true (it isn't a decision).
        /// </summary>
        private bool TryAiTrick(PlayerRuntimeState p, DifficultyDef profile)
        {
            if (!StyleOn || IsHumanControlled(p.Index) || MustClear || MustInbound) return false;
            if (_trickReadyAt == null) _trickReadyAt = new float[Players.Length];
            if (Time < _trickReadyAt[p.Index]) return false;
            int d = SkillTarget(p);
            if (d < 0 || Vec2.Distance(Players[d].Position, p.Position) > TrickRange) return false;
            float quality = profile != null ? Math.Max(0f, Math.Min(1f, profile.decisionQuality)) : 0.5f;
            float chance = (0.05f + 0.1f * quality) * (0.4f + p.Def.attributes.playmaking / 100f);
            if (_rng.NextFloat() >= chance) return false;
            float roll = _rng.NextFloat();
            var trick = roll < 0.3f ? StreetTrick.BehindBack : roll < 0.55f ? StreetTrick.ThroughLegs : roll < 0.75f ? StreetTrick.RollTheArm
                      : roll < 0.9f ? StreetTrick.Halo : StreetTrick.OffTheHead;
            // Near the rim with a finisher: sometimes off the glass to himself.
            if (CanGlassToss(p.Index) && Setup.Court.DistanceToHoop(p.Position) < 4.5f && _rng.NextFloat() < 0.3f) trick = StreetTrick.OffTheGlass;
            DoTrick(p.Index, trick);
            return false;
        }

        /// <summary>An AI team on offense with a full meter calls its SHOWSTOPPER.</summary>
        private void TryAiBreaker()
        {
            if (!StyleOn || BreakerTeam >= 0 || !Ball.IsHeld) return;
            int team = OffenseTeam;
            if (IsHumanTeam(team)) return;
            if (CanCallBreaker(team)) CallBreaker(team);
        }

        private bool IsHumanTeam(int team) =>
            !NoHuman && ((ControlledIndex >= 0 && Players[ControlledIndex].Team == team)
                         || (SecondControlledIndex >= 0 && Players[SecondControlledIndex].Team == team));

        // ------------------------------------------------------------------ the meter

        private void AddMeter(int team, int points)
        {
            if (team < 0 || team > 1 || points <= 0) return;
            int before = StyleMeter[team];
            StyleMeter[team] = Math.Min(MeterLevel * MaxBreakerLevel, before + points);
            if (before / MeterLevel < StyleMeter[team] / MeterLevel)
                Events.Add(new MatchEvent(MatchEventType.BreakerReady, -1, team, StyleMeter[team] / MeterLevel));
        }

        private void LoseCombo(int team)
        {
            if (ComboPoints[team] > 0) Events.Add(new MatchEvent(MatchEventType.ComboLost, -1, team, ComboPoints[team]));
            ComboPoints[team] = 0;
            ComboCount[team] = 0;
        }

        private void EndBreaker(bool scored)
        {
            if (BreakerTeam < 0) return;
            if (!scored) Events.Add(new MatchEvent(MatchEventType.BreakerEnded, -1, BreakerTeam, BreakerLevel));
            BreakerTeam = -1;
            BreakerLevel = 0;
        }

        /// <summary>
        /// Runs the style rules over this step's events (called at the end of every step). Banks combos on buckets,
        /// pays defensive style, and settles the SHOWSTOPPER.
        /// </summary>
        private void ProcessStreetEvents()
        {
            if (!StyleOn) return;
            int n = Events.Count;
            for (int i = 0; i < n; i++)
            {
                var e = Events[i];
                switch (e.Type)
                {
                    case MatchEventType.SkillMove:
                        if (e.Team >= 0)
                        {
                            ComboPoints[e.Team] += StyleSkillMove;
                            ComboCount[e.Team]++;
                        }
                        break;
                    case MatchEventType.DefenderBeaten:
                        if (e.Team >= 0) ComboPoints[e.Team] += StyleBeat;
                        break;
                    case MatchEventType.AnkleBreaker:
                        if (e.Team >= 0) ComboPoints[e.Team] += StyleAnkles;
                        break;
                    case MatchEventType.AlleyOop:
                        if (e.Team >= 0) ComboPoints[e.Team] += StyleAlleyOop;
                        break;
                    case MatchEventType.BankKiss:
                        if (e.PlayerIndex >= 0) ComboPoints[Players[e.PlayerIndex].Team] += StyleBankKiss;
                        break;
                    case MatchEventType.Block:
                        AddMeter(e.Team, StyleBlock);
                        break;
                    case MatchEventType.Steal:
                        AddMeter(e.Team, StyleSteal);
                        break;
                    case MatchEventType.ShotMade:
                    {
                        int team = e.Team;
                        int shotStyle = (Ball.ShotType == ShotType.Dunk ? StyleDunk : 0) + (e.Value >= Setup.Rules.beyondArcPoints ? StyleTwo : 0);
                        int bank = ComboPoints[team] + shotStyle;
                        ComboPoints[team] = 0;
                        ComboCount[team] = 0;
                        if (BreakerTeam == team)
                        {
                            // SHOWSTOPPER: +level for us, -level for them.
                            int level = BreakerLevel;
                            Score[team] += level;
                            Stats[e.PlayerIndex].points += level;
                            Score[1 - team] = Math.Max(0, Score[1 - team] - level);
                            BreakersScored[team]++;
                            Events.Add(new MatchEvent(MatchEventType.BreakerScored, e.PlayerIndex, team, level));
                            EndBreaker(scored: true);
                            _finalPending = Scoring.Evaluate(Score[0], Score[1], GameClock, Setup.Rules) != GameOverReason.None;
                        }
                        else
                        {
                            AddMeter(team, bank);
                            if (bank > 0) Events.Add(new MatchEvent(MatchEventType.StyleBanked, e.PlayerIndex, team, bank));
                        }
                        // The other team's unbanked combo is gone, and a SHOWSTOPPER they had on is spent.
                        LoseCombo(1 - team);
                        if (BreakerTeam == 1 - team) EndBreaker(scored: false);
                        break;
                    }
                    case MatchEventType.ShotMissed:
                        if (BreakerTeam == e.Team) EndBreaker(scored: false);
                        break;
                    case MatchEventType.PossessionChanged:
                    {
                        int lost = 1 - e.Team;
                        LoseCombo(lost);
                        if (BreakerTeam == lost) EndBreaker(scored: false);
                        Array.Clear(_trickRepeats, 0, _trickRepeats.Length);
                        break;
                    }
                    case MatchEventType.CheckBall:
                        Array.Clear(_trickRepeats, 0, _trickRepeats.Length);
                        break;
                }
            }
            if (Phase == MatchPhase.Live) TryAiBreaker();
        }
    
        // ------------------------------------------------------------------ phase 2: off the glass, and the ball in tricks

        public const float GlassMinDistance = 1.8f, GlassMaxDistance = 6.2f;
        private bool _glassToss, _glassHit;
        private float[] _trickBallStart;
        private StreetTrick[] _trickBallKind;
        private int[] _trickBallVictim;

        /// <summary>True while an OFF THE GLASS toss is in the air.</summary>
        public bool GlassTossInFlight => _glassToss && Ball.Phase == BallPhase.Pass;

        /// <summary>Close enough (but not under the rim), on the move, and able to finish above it.</summary>
        public bool CanGlassToss(int me)
        {
            if (!StyleOn || me < 0 || me >= Players.Length) return false;
            var p = Players[me];
            if (MustClear || MustInbound || Setup.TeammatesOnlyPass) return false;
            float d = Setup.Court.DistanceToHoop(p.Position);
            return d >= GlassMinDistance && d <= GlassMaxDistance && p.Def.attributes.finishing >= Setup.Pass.alleyOopMinFinishing;
        }

        /// <summary>Throws it off the backboard: the ball flies to the glass, kisses it, and comes back to the thrower.</summary>
        private void GlassToss(int me)
        {
            var p = Players[me];
            CancelCharge();
            CancelDrive();
            Ball.Phase = BallPhase.Pass;
            Ball.HolderIndex = -1;
            Ball.PasserIndex = me;
            Ball.TargetIndex = me;
            Ball.PassType = PassType.Chest;
            Ball.FlightFrom = p.Position;
            Ball.FlightTo = Setup.Court.Hoop;
            Ball.FlightTime = 0f;
            Ball.Position = p.Position;
            Ball.Height = 1.8f;
            _alleyOop = true;
            _oopToRunner = false;
            _glassToss = true;
            _glassHit = false;
            Events.Add(new MatchEvent(MatchEventType.PassThrown, me, p.Team, (int)PassType.Chest));
        }

        private void StepGlassToss(float dt)
        {
            const float speed = 10f;
            int me = Ball.TargetIndex;
            var p = Players[me];
            Ball.FlightTime += dt;
            float top = RimHeight + 0.9f;
            if (!_glassHit)
            {
                var glass = Setup.Court.Hoop;
                Ball.Position = Vec2.MoveTowards(Ball.Position, glass, speed * dt);
                float total = Math.Max(0.1f, Vec2.Distance(Ball.FlightFrom, glass));
                float progress = Math.Min(1f, Vec2.Distance(Ball.FlightFrom, Ball.Position) / total);
                Ball.Height = 1.8f + (top - 1.8f) * progress;
                if (Vec2.Distance(Ball.Position, glass) <= 0.05f)
                {
                    _glassHit = true;
                    Ball.FlightFrom = Ball.Position;
                    Events.Add(new MatchEvent(MatchEventType.BankKiss, me, p.Team));
                }
            }
            else
            {
                Ball.Position = Vec2.MoveTowards(Ball.Position, p.Position, speed * 0.85f * dt);
                Ball.Height = Math.Max(Setup.Pass.alleyOopCatchHeight, Ball.Height - 1.2f * dt);
                if (Vec2.Distance(Ball.Position, p.Position) <= Setup.Pass.catchRadius)
                {
                    _glassToss = false;
                    _lastCatcher = me;
                    _lastCatchTime = Time;
                    // Still at the rim and on your feet: throw it down in the air. Otherwise you just catch it.
                    if (Time >= p.StunnedUntil && Setup.Court.DistanceToHoop(p.Position) <= Setup.Pass.alleyOopRange + 1.2f)
                    {
                        FinishAlleyOop(me, -1);
                        return;
                    }
                    _alleyOop = false;
                    GiveBall(me, announce: false, fromCheck: false);
                    return;
                }
            }
            if (Ball.FlightTime > 2.5f)
            {
                _glassToss = false;
                _alleyOop = false;
                Ball.Phase = BallPhase.Loose;
                Ball.Velocity = (p.Position - Ball.Position).Normalized * 2f;
                Ball.VerticalVelocity = 0f;
                Events.Add(new MatchEvent(MatchEventType.BallLoose, -1, -1));
            }
        }

        private void StartTrickBall(int me, StreetTrick trick, int victim)
        {
            if (_trickBallStart == null)
            {
                _trickBallStart = new float[Players.Length];
                _trickBallKind = new StreetTrick[Players.Length];
                _trickBallVictim = new int[Players.Length];
                for (int i = 0; i < Players.Length; i++) _trickBallStart[i] = -99f;
            }
            _trickBallStart[me] = Time;
            _trickBallKind[me] = trick;
            _trickBallVictim[me] = victim;
        }

        public const float TrickBallSeconds = 0.5f;

        /// <summary>The trick the holder's ball is doing right now (None when it's a normal dribble).</summary>
        public StreetTrick TrickBallOf(int player) =>
            _trickBallStart != null && player >= 0 && player < Players.Length && Time - _trickBallStart[player] < TrickBallSeconds
                ? _trickBallKind[player] : StreetTrick.None;

        /// <summary>Where the ball is during a trick (around the back, between the legs, off the defender's head...).</summary>
        private bool TrickBall(PlayerRuntimeState holder)
        {
            var kind = TrickBallOf(holder.Index);
            if (kind == StreetTrick.None || kind == StreetTrick.OffTheGlass) return false;
            float t = Math.Min(1f, (Time - _trickBallStart[holder.Index]) / TrickBallSeconds);
            var toHoop = Setup.Court.Hoop - holder.Position;
            var fwd = toHoop.SqrMagnitude > 0.0001f ? toHoop.Normalized : new Vec2(0f, -1f);
            var side = new Vec2(-fwd.y, fwd.x);
            float pi = (float)Math.PI;
            float sin = (float)Math.Sin(pi * t), cos = (float)Math.Cos(pi * t);
            switch (kind)
            {
                case StreetTrick.BehindBack:
                    Ball.Position = holder.Position + side * (0.45f * cos) - fwd * (0.4f * sin);
                    Ball.Height = 0.85f;
                    break;
                case StreetTrick.ThroughLegs:
                    Ball.Position = holder.Position + side * (0.35f * cos) + fwd * (0.05f * sin);
                    Ball.Height = 0.9f - 0.65f * sin;
                    break;
                case StreetTrick.Halo:
                {
                    float a = 2f * pi * t;
                    Ball.Position = holder.Position + side * (0.45f * (float)Math.Cos(a)) + fwd * (0.45f * (float)Math.Sin(a));
                    Ball.Height = 1.05f;
                    break;
                }
                case StreetTrick.OffTheHead:
                {
                    int v = _trickBallVictim[holder.Index];
                    var target = v >= 0 ? Players[v].Position : holder.Position + fwd * 0.9f;
                    Ball.Position = holder.Position + (target - holder.Position) * sin;
                    Ball.Height = 1.1f + 1.3f * sin;
                    break;
                }
                default: // RollTheArm: up the arm, across the shoulders, down the other
                    Ball.Position = holder.Position + side * (0.4f * cos) + fwd * 0.15f;
                    Ball.Height = 1.2f + 0.55f * sin;
                    break;
            }
            Ball.Velocity = holder.Motion.velocity;
            return true;
        }
    }
}
