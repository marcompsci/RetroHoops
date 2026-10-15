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
            switch (trick)
            {
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
            if (trick != StreetTrick.Halo && victim < 0) pts *= 0.6f; // nobody to show up
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
    }
}
