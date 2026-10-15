using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>Match-flow timing and ball-flight feel.</summary>
    [Serializable]
    public class MatchTuning
    {
        /// <summary>Freeze before live play after a check (ends early when the human moves or acts).</summary>
        public float checkBallSeconds = 0.8f;
        public float deadBallAfterMake = 1.0f;
        public float deadBallAfterTurnover = 0.9f;
        /// <summary>Clutch rating matters when the game clock is at or under this.</summary>
        public float lateGameSeconds = 15f;
        /// <summary>A shot that hits the rim resets the shot clock (3-on-3 style).</summary>
        public bool resetShotClockOnRim = true;
        public float releaseHeight = 2.2f;
        public float dunkReleaseHeight = 2.9f;
        public float shotFlightBase = 0.5f;
        public float shotFlightPerMeter = 0.06f;
        public float dunkFlightSeconds = 0.22f;
        /// <summary>After a made dunk the dunker hangs (and swings) on the rim this long before dropping (can't move).</summary>
        public float rimHangSeconds = 0.7f;
        public float shotArcBase = 1.2f;
        public float shotArcPerMeter = 0.13f;
        public float missSpeedMin = 2.2f;
        public float missSpeedMax = 4.6f;
        public float missUpSpeed = 2.8f;
        public float passHeight = 1.2f;
        public float maxPassSeconds = 2f;
        public float assistWindowSeconds = 4f;
        public float giveAndGoSeconds = 1.2f;

        public static MatchTuning Default => new MatchTuning();
    }

    /// <summary>Everything needed to start a match. Built from a MatchRequest + content.</summary>
    public sealed class MatchSetup
    {
        public TeamDef TeamA;
        public TeamDef TeamB;
        public List<PlayerDef> RosterA = new List<PlayerDef>();
        public List<PlayerDef> RosterB = new List<PlayerDef>();
        public List<ArchetypeDef> ArchetypesA = new List<ArchetypeDef>();
        public List<ArchetypeDef> ArchetypesB = new List<ArchetypeDef>();
        public GameRulesDef Rules = new GameRulesDef();
        public DifficultyDef Difficulty;
        public CourtGeometry Court = CourtGeometry.Default;
        public MovementTuning Movement = MovementTuning.Default;
        public BallTuning Ball = BallTuning.Default;
        public ShotTuning Shot = ShotTuning.Default;
        public PassTuning Pass = PassTuning.Default;
        public MatchTuning Flow = MatchTuning.Default;
        public DefenseTuning Defense = DefenseTuning.Default;
        public int HumanTeam = 0;
        public int StartingOffense = 0;
        public uint Seed = 1;
        /// <summary>Practice: opponents stand still and never take the ball.</summary>
        public bool PassiveOpponents;
        /// <summary>Local 2-player: slot 0 of the other team is also human-controlled (second input).</summary>
        public bool SecondHuman;
        /// <summary>Practice: the human's team keeps the ball after scoring.</summary>
        public bool KeepPossessionAfterScore;
        public float HumanTeamStartingStamina = 1f;
        /// <summary>Practice: AI teammates never shoot or drive; drills hand the ball back.</summary>
        public bool TeammatesOnlyPass;
        public float ChemistryBonus;
        /// <summary>Attract-mode demo: every player is AI (nobody's input is read).</summary>
        public bool Demo;
        /// <summary>Phase 35 coach mode: the human's team is all AI and takes the coach's calls (see <see cref="MatchSimulation.Coaching"/>).</summary>
        public bool Coach;
        /// <summary>Phase 36 SPOT SPECIALIST: make-chance bonus per chart spot for player 1 (null = none).</summary>
        public float[] HumanSpotBonus;
        /// <summary>Secret code: the human's player starts the game heated up.</summary>
        public bool HumanStartsHeated;
        /// <summary>1-on-1: only slot 0 of each team plays; the others sit out.</summary>
        public bool OneOnOne;
        /// <summary>Street rules: sharp crossovers near a defender can break their ankles (they stumble).</summary>
        public bool StreetRules;
        /// <summary>THE STREETS: tricks, combos, the STREET METER and the SHOWSTOPPER (see StreetStyle.cs).</summary>
        public bool StyleMeter;
        /// <summary>Full Court 5-on-5: both hoops, the picture turns when possession changes (see <see cref="FullCourt"/>).</summary>
        public bool FullCourt;
        /// <summary>Players per side: 3 (half court) or 5 (Full Court).</summary>
        public int TeamSize = MatchSimulation.PlayersPerTeam;
        /// <summary>Half-court 3-on-3: the team that scores keeps the ball ("winners' ball").</summary>
        public bool WinnersBall;
        /// <summary>Full Court: two players on each bench (tired AI players sub out at dead balls).</summary>
        /// <summary>Phase 37 CLUTCH: the score and game clock the match starts from (0 clock = the rules' own).</summary>
        public int StartScoreA, StartScoreB;
        /// <summary>THE STREETS phase 3 (HOMECOURT, crew chemistry): team A's STREET METER at tip-off, and its extra meter gain (0.1 = +10%).</summary>
        public int StartMeterA;
        public float StyleBoostA;
        public float StartClock;
        /// <summary>Phase 40 SMART DIFFICULTY: added to the opponents' release accuracy.</summary>
        public float AiEdge;
        public List<PlayerDef> BenchA = new List<PlayerDef>();
        public List<PlayerDef> BenchB = new List<PlayerDef>();
        /// <summary>Finds an archetype by id (bench players coming on).</summary>
        public Func<string, ArchetypeDef> ArchetypeLookup;

        /// <summary>Builds a setup from a request, taking the first three (Full Court: five) players of each roster.</summary>
        public static MatchSetup FromRequest(MatchRequest request, ContentCatalog c)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (c == null) throw new ArgumentNullException(nameof(c));

            bool full = request.Mode == GameMode.FullCourt || request.FullCourt;
            bool drills = request.Mode == GameMode.Practice || request.Mode == GameMode.Tutorial;
            string rulesId = full && (request.RulesId == null || request.RulesId == DefaultContent.DefaultRulesId) ? Logic.FullCourt.RulesId : request.RulesId;
            var setup = new MatchSetup
            {
                TeamA = c.Team(request.HomeTeamId),
                TeamB = c.Team(request.AwayTeamId),
                Rules = c.Find(c.Rules, rulesId) ?? new GameRulesDef(),
                FullCourt = full,
                TeamSize = full ? Logic.FullCourt.TeamSize : (request.TeamSize >= 2 && request.TeamSize <= 4 ? request.TeamSize : MatchSimulation.PlayersPerTeam),
                StreetRules = request.StreetRules || request.Mode == GameMode.Streets || request.StreetsStyle,
                StyleMeter = (request.Mode == GameMode.Streets || request.StreetsStyle) && !full,
                Court = full ? Logic.FullCourt.Geometry() : CourtGeometry.Default,
                // Half-court 3-on-3 is winners' ball; Full Court and 1-on-1 alternate possessions.
                // THE STREETS: the ball changes hands after a bucket.
                WinnersBall = !full && !drills && request.Mode != GameMode.OneOnOne && request.Mode != GameMode.Streets && !request.StreetsStyle,
                ArchetypeLookup = c.ArchetypeById,
                Difficulty = c.Difficulty(request.DifficultyId),
                Seed = request.Seed != 0 ? request.Seed : 1,
                PassiveOpponents = (request.Mode == GameMode.Practice && request.Drill != (int)DrillKind.Lockdown) || request.Mode == GameMode.Tutorial,
                SecondHuman = request.Mode == GameMode.Versus,
                KeepPossessionAfterScore = (request.Mode == GameMode.Practice && request.Drill != (int)DrillKind.Lockdown) || request.Mode == GameMode.Tutorial,
                TeammatesOnlyPass = (request.Mode == GameMode.Practice && request.Drill != (int)DrillKind.Lockdown) || request.Mode == GameMode.Tutorial,
                StartingOffense = request.Mode == GameMode.Practice && request.Drill == (int)DrillKind.Lockdown ? 1 : 0,
                HumanTeamStartingStamina = request.StartingStamina,
                ChemistryBonus = request.ChemistryBonus,
                Demo = request.Mode == GameMode.Demo,
                Coach = request.Coach && request.Mode != GameMode.Versus && request.Mode != GameMode.Demo,
                HumanSpotBonus = request.SpotBonus != null && request.SpotBonus.Length == ShotZones.SpotCount ? (float[])request.SpotBonus.Clone() : null,
                HumanStartsHeated = request.StartHeated,
                OneOnOne = request.Mode == GameMode.OneOnOne,
                StartScoreA = Math.Max(0, request.StartScoreA),
                StartMeterA = Math.Max(0, Math.Min(MatchSimulation.MeterLevel - 1, request.StartMeterA)),
                StyleBoostA = Math.Max(0f, Math.Min(0.5f, request.StyleBoostA)),
                StartScoreB = Math.Max(0, request.StartScoreB),
                StartClock = Math.Max(0f, request.StartClock),
                AiEdge = Math.Max(-Adaptive.MaxEdge, Math.Min(Adaptive.MaxEdge, request.AiEdge)),
            };
            if (request.StartWithBall == 0 || request.StartWithBall == 1) setup.StartingOffense = request.StartWithBall;
            // Practice has no opponent: mirror the player crew so the court still has bodies.
            if (setup.TeamB == null) setup.TeamB = setup.TeamA;
            if (setup.TeamA == null) throw new InvalidOperationException("Match request has no valid home team.");

            FillRoster(setup.RosterA, setup.ArchetypesA, setup.TeamA, c, setup.TeamSize);
            FillRoster(setup.RosterB, setup.ArchetypesB, setup.TeamB, c, setup.TeamSize);
            if (full)
            {
                FillBench(setup.BenchA, setup.RosterA, setup.TeamA, c);
                FillBench(setup.BenchB, setup.RosterB, setup.TeamB, c);
            }
            if (request.HumanTeammates != null)
            {
                for (int i = 0; i < request.HumanTeammates.Count && i + 1 < Math.Min(setup.RosterA.Count, MatchSimulation.PlayersPerTeam); i++)
                {
                    var mate = request.HumanTeammates[i];
                    if (mate == null) continue;
                    setup.RosterA[i + 1] = mate;
                    var archetype = c.ArchetypeById(mate.archetypeId);
                    if (archetype != null && i + 1 < setup.ArchetypesA.Count) setup.ArchetypesA[i + 1] = archetype;
                }
            }
            // Half court: a team's own extra player (if it has one) waits on the bench and comes in for a tired AI player.
            bool halfCourtBench = !full && !drills && request.Mode != GameMode.OneOnOne && request.Mode != GameMode.Tutorial && setup.TeamB != setup.TeamA;
            if (halfCourtBench)
            {
                FillHalfCourtBench(setup.BenchA, setup.RosterA, setup.TeamA, c);
                FillHalfCourtBench(setup.BenchB, setup.RosterB, setup.TeamB, c);
            }
            if (request.HumanPlayer != null)
            {
                setup.RosterA[0] = request.HumanPlayer;
                var archetype = c.ArchetypeById(request.HumanPlayer.archetypeId);
                if (archetype != null && setup.ArchetypesA.Count > 0) setup.ArchetypesA[0] = archetype;
            }
            else if (request.HumanAttributes.HasValue)
            {
                // Copy so the shared content definition is never modified.
                var original = setup.RosterA[0];
                setup.RosterA[0] = new PlayerDef
                {
                    id = original.id, firstName = original.firstName, lastName = original.lastName,
                    jerseyNumber = original.jerseyNumber, archetypeId = original.archetypeId,
                    attributes = request.HumanAttributes.Value, appearance = original.appearance,
                };
            }
            return setup;
        }

        /// <summary>Half court: up to one bench player, only from the team's own roster (no generated reserves).</summary>
        private static void FillHalfCourtBench(List<PlayerDef> bench, List<PlayerDef> roster, TeamDef team, ContentCatalog c)
        {
            if (team == null) return;
            foreach (var id in team.rosterPlayerIds)
            {
                if (bench.Count >= 1) return;
                var p = c.Player(id);
                if (p != null && !roster.Exists(r => r.id == id) && !bench.Exists(b => b.id == id)) bench.Add(p);
            }
        }

        /// <summary>Two bench players: the team's own extras first, then generated reserves.</summary>
        private static void FillBench(List<PlayerDef> bench, List<PlayerDef> roster, TeamDef team, ContentCatalog c)
        {
            bool Used(string id) => roster.Exists(p => p.id == id) || bench.Exists(p => p.id == id);
            foreach (var id in team.rosterPlayerIds)
            {
                if (bench.Count == Logic.FullCourt.BenchSize) return;
                var p = c.Player(id);
                if (p != null && !Used(p.id)) bench.Add(p);
            }
            foreach (var extra in Logic.FullCourt.Reserves(team, c, Logic.FullCourt.TeamSize + Logic.FullCourt.BenchSize))
            {
                if (bench.Count == Logic.FullCourt.BenchSize) return;
                if (!Used(extra.id)) bench.Add(extra);
            }
        }

        private static void FillRoster(List<PlayerDef> roster, List<ArchetypeDef> archetypes, TeamDef team, ContentCatalog c, int size)
        {
            foreach (var id in team.rosterPlayerIds)
            {
                if (roster.Count == size) break;
                var p = c.Player(id);
                if (p == null) continue;
                roster.Add(p);
                archetypes.Add(c.ArchetypeById(p.archetypeId));
            }
            // Full Court: three-player crews (and four-player league teams) fill up with generated reserves.
            if (roster.Count >= MatchSimulation.PlayersPerTeam && roster.Count < size)
                foreach (var extra in Logic.FullCourt.Reserves(team, c, size - roster.Count))
                {
                    roster.Add(extra);
                    archetypes.Add(c.ArchetypeById(extra.archetypeId));
                }
            if (roster.Count < size)
                throw new InvalidOperationException("Team '" + team.id + "' needs " + size + " valid players.");
        }
    }

    /// <summary>Per-player live state (separate from the static PlayerDef).</summary>
    public sealed class PlayerRuntimeState
    {
        public int Index;
        public int Team;
        /// <summary>0..TeamSize-1 within the team; also the defensive matchup (slot guards slot).</summary>
        public int Slot;
        public PlayerDef Def;
        public ArchetypeDef Archetype;
        public MotionState Motion;
        public bool IsHuman;
        public int HotStreak;
        /// <summary>1 = fresh, 0 = gassed. Drains while sprinting, recovers when easing off.</summary>
        public float Stamina = 1f;
        public float StunnedUntil = -99f;
        public float ScreenedUntil = -99f;
        public float JumpStart = -99f;
        /// <summary>Bit on a pump fake: off balance (no contest, no block, can't move) until then.</summary>
        public float FakedUntil = -99f;
        public float StealReadyAt;

        public Vec2 Position => Motion.position;
        public AiTendencies Tendencies => Archetype != null ? Archetype.ai : new AiTendencies(0.5f, 0.5f, 0.5f, 0.5f, 0.3f, 0.5f, 0.5f, 0.3f, 0.5f);
    }

    public enum MatchPhase
    {
        /// <summary>Players reset; ball with the offense at the check spot. Clocks stopped.</summary>
        CheckBall = 0,
        Live = 1,
        /// <summary>Brief pause after a score or turnover. Clocks stopped.</summary>
        DeadBall = 2,
        Final = 3,
    }

    public enum MatchEventType
    {
        PossessionChanged = 0,
        BallPickedUp = 1,
        BallLoose = 2,
        CheckBall = 3,
        ShotStarted = 4,
        ShotReleased = 5,
        ShotMade = 6,
        ShotMissed = 7,
        PassThrown = 8,
        PassCaught = 9,
        Interception = 10,
        ShotClockViolation = 11,
        BallCleared = 12,
        LiveBall = 13,
        GameOver = 14,
        Rebound = 15,
        StealAttempt = 16,
        Steal = 17,
        Jump = 18,
        Block = 19,
        Switch = 20,
        Screen = 21,
        PlayCalled = 22,
        /// <summary>HEAT CHECK: a player hit three in a row (Value = streak).</summary>
        HeatUp = 23,
        /// <summary>A heated-up player cooled off (missed, blocked, or the other team scored).</summary>
        HeatEnded = 24,
        /// <summary>An alley-oop finish (PlayerIndex = finisher, Value = passer).</summary>
        AlleyOop = 25,
        /// <summary>The AI changed its defence (Team = defending team, Value = <see cref="DefenseScheme"/>).</summary>
        SchemeChanged = 26,
        /// <summary>"21" bust rule: went over the target (Team = who busted, Value = new score).</summary>
        Bust = 27,
        /// <summary>Full Court violation (Team = offence that lost the ball, Value = <see cref="ViolationKind"/>).</summary>
        Violation = 28,
        /// <summary>Full Court substitution (PlayerIndex = the slot that changed player).</summary>
        Substitution = 29,
        /// <summary>Street rules: a crossover broke a defender's ankles (PlayerIndex = handler, Value = defender).</summary>
        AnkleBreaker = 30,
        /// <summary>DUNK / LAYUP pressed away from the rim: the player attacks the basket (Value = <see cref="ShotType"/> wanted).</summary>
        DriveStarted = 31,
        /// <summary>A DUNK request that had to be a layup (too far out, or not enough finishing).</summary>
        DunkToLayup = 32,
        /// <summary>A driving player side-stepped a defender in the lane (PlayerIndex = driver, Value = defender).</summary>
        EuroStep = 33,
        /// <summary>A second defender doubles a heated-up scorer (PlayerIndex = the trapper, Value = the ball handler).</summary>
        Trap = 34,
        /// <summary>A quick tap of SHOOT faked the shot (PlayerIndex = shooter, Value = the defender who bit, or -1).</summary>
        PumpFake = 35,
        // 36-38 are kept for the v1.1 branch (Foul, FreeThrow, AndOne).
        /// <summary>A skill move (PlayerIndex = handler, Value = <see cref="SkillMoveKind"/>, Chance = the defender it worked on, -1 = none).</summary>
        SkillMove = 39,
        /// <summary>A skill move beat the defender (PlayerIndex = handler, Value = the defender).</summary>
        DefenderBeaten = 40,
        /// <summary>A bank shot kissed the glass (PlayerIndex = shooter).</summary>
        BankKiss = 41,
        // 15 Oct 2026: THE STREETS (see StreetStyle.cs).
        /// <summary>A trick (PlayerIndex = handler, Value = <see cref="StreetTrick"/>, Chance = style points it added to the combo).</summary>
        StreetTrick = 42,
        /// <summary>A bucket banked the combo into the STREET METER (Value = points banked).</summary>
        StyleBanked = 43,
        /// <summary>A team lost its unbanked combo (Value = points lost).</summary>
        ComboLost = 44,
        /// <summary>A team's meter reached a SHOWSTOPPER level (Value = level now ready).</summary>
        BreakerReady = 45,
        /// <summary>A team called its SHOWSTOPPER (Value = level).</summary>
        BreakerOn = 46,
        /// <summary>A SHOWSTOPPER bucket (Value = level: that many extra points, and that many off the other team).</summary>
        BreakerScored = 47,
        /// <summary>A SHOWSTOPPER was wasted (missed, or the ball was lost).</summary>
        BreakerEnded = 48,
    }

    public struct MatchEvent
    {
        public MatchEventType Type;
        public int PlayerIndex;
        public int Team;
        /// <summary>Points (ShotMade), feedback (ShotReleased), or pass type — depends on Type.</summary>
        public int Value;
        public float Chance;

        public MatchEvent(MatchEventType type, int playerIndex, int team, int value = 0, float chance = 0f)
        {
            Type = type;
            PlayerIndex = playerIndex;
            Team = team;
            Value = value;
            Chance = chance;
        }
    }

    /// <summary>
    /// The deterministic match core: players, ball, possession, shots, passes, scoring,
    /// clocks, and AI. Unity views read this state each frame; nothing here touches the engine.
    /// The human always controls slot 0 of their team (their "caller"); teammates are AI.
    /// </summary>
    public sealed partial class MatchSimulation
    {
        /// <summary>Players per side in the half-court game (Full Court uses <see cref="TeamSize"/> = 5).</summary>
        public const int PlayersPerTeam = 3;

        /// <summary>Players per side in this match.</summary>
        public int TeamSize => Setup.TeamSize;

        public readonly MatchSetup Setup;
        public readonly PlayerRuntimeState[] Players;
        public readonly BallState Ball = new BallState();
        public readonly int[] Score = new int[2];
        public readonly MatchStats Stats;
        /// <summary>Events raised during the last <see cref="Step"/>; views may react to them.</summary>
        public readonly List<MatchEvent> Events = new List<MatchEvent>();

        public MatchPhase Phase { get; private set; }
        public float PhaseTimer { get; private set; }
        public int OffenseTeam { get; private set; }
        public int ControlledIndex { get; private set; }
        public float Time { get; private set; }
        public float GameClock { get; private set; }
        public float ShotClock { get; private set; }
        /// <summary>After a steal or defensive rebound the ball must go beyond the arc before a shot.</summary>
        public bool MustClear { get; private set; }
        /// <summary>Clocks run during live play. Tests may freeze them.</summary>
        public bool ClocksRunning { get; set; } = true;

        /// <summary>Player currently charging the shot meter, or -1.</summary>
        public int ChargingIndex { get; private set; } = -1;
        public float ChargeTime { get; private set; }
        public ShotType ChargeType { get; private set; }
        public float ChargeMeter => ChargingIndex < 0 ? 0f : ChargeTime / ShotModel.FillSeconds(ChargeType, Setup.Shot);
        /// <summary>Meter value of the most recent release (for the meter display).</summary>
        public float LastReleaseMeter { get; private set; }

        /// <summary>Winning team once <see cref="Phase"/> is Final (-1 before).</summary>
        public int Winner { get; private set; } = -1;
        public GameOverReason EndReason { get; private set; }

        private readonly SeededRandom _rng;
        private readonly Vec2[] _scratch;
        private readonly Vec2[] _desired;
        private readonly List<PassCandidate> _candidates = new List<PassCandidate>(PlayersPerTeam);
        private readonly List<Vec2> _defenders = new List<Vec2>(PlayersPerTeam);

        private int _nextOffense;
        private bool _finalPending;
        private float _aiReleaseMeter = -1f;
        private bool _reboundable;
        private int _lastPasser = -1;
        private int _lastCatcher = -1;
        private float _lastCatchTime = -99f;
        private bool _lastShotWasMiss;

        public MatchSimulation(MatchSetup setup)
        {
            Setup = setup ?? throw new ArgumentNullException(nameof(setup));
            _rng = new SeededRandom(setup.Seed);
            Players = new PlayerRuntimeState[setup.TeamSize * 2];
            for (int team = 0; team < 2; team++)
            {
                var roster = team == 0 ? setup.RosterA : setup.RosterB;
                var archetypes = team == 0 ? setup.ArchetypesA : setup.ArchetypesB;
                for (int slot = 0; slot < setup.TeamSize; slot++)
                {
                    int index = team * setup.TeamSize + slot;
                    Players[index] = new PlayerRuntimeState
                    {
                        Index = index,
                        Team = team,
                        Slot = slot,
                        Def = roster[slot],
                        Archetype = slot < archetypes.Count ? archetypes[slot] : null,
                        IsHuman = slot == 0 && !setup.Coach && (team == setup.HumanTeam || setup.SecondHuman),
                        Motion = new MotionState(Vec2.Zero),
                    };
                }
            }
            Stats = new MatchStats(Players.Length);
            _scratch = new Vec2[Players.Length];
            _desired = new Vec2[Players.Length];
            InitAi();
            InitDefense();
            InitSchemes();
            InitBench();
            GameClock = setup.Rules.useGameClock ? (setup.StartClock > 0f ? setup.StartClock : setup.Rules.gameClockSeconds) : 0f;
            Score[0] = Math.Max(0, setup.StartScoreA);
            if (setup.StyleMeter) StyleMeter[0] = setup.StartMeterA;
            Score[1] = Math.Max(0, setup.StartScoreB);
            ShotClock = setup.Rules.shotClockSeconds;
            ControlledIndex = Index(setup.HumanTeam, 0);
            SecondControlledIndex = setup.SecondHuman ? Index(1 - setup.HumanTeam, 0) : -1;
            if (setup.HumanStartsHeated && !setup.Demo && !setup.Coach) Players[ControlledIndex].HotStreak = setup.Shot.heatThreshold;
            CheckBall(setup.StartingOffense);
        }

        public PlayerRuntimeState Controlled => Players[ControlledIndex];
        /// <summary>Player 2's player in local 2-player, otherwise -1.</summary>
        public int SecondControlledIndex { get; private set; } = -1;

        /// <summary>The human-controlled player on <paramref name="team"/>, or -1 if that team is all AI.</summary>
        public int HumanIndexOf(int team) =>
            Setup.Coach ? -1 : team == Setup.HumanTeam ? ControlledIndex : (SecondControlledIndex >= 0 && Players[SecondControlledIndex].Team == team ? SecondControlledIndex : -1);

        /// <summary>True if <paramref name="index"/> is driven by a person (player 1 or player 2).</summary>
        public bool IsHumanControlled(int index) => !NoHuman && (index == ControlledIndex || (index >= 0 && index == SecondControlledIndex));
        public int HolderIndex => Ball.IsHeld ? Ball.HolderIndex : -1;
        public PlayerRuntimeState Holder => Ball.IsHeld ? Players[Ball.HolderIndex] : null;
        public int DefenseTeam => 1 - OffenseTeam;
        public bool IsOver => Phase == MatchPhase.Final;
        public bool HumanHasBall => !NoHuman && Ball.IsHeld && Ball.HolderIndex == ControlledIndex;
        public bool HumanTeamHasBall => !Setup.Coach && Ball.IsHeld && Players[Ball.HolderIndex].Team == Setup.HumanTeam;

        /// <summary>Player index in a half-court 3-on-3 match (tests, drills and the tutorial).</summary>
        public static int IndexOf(int team, int slot) => team * PlayersPerTeam + slot;

        /// <summary>Player index for this match's team size.</summary>
        public int Index(int team, int slot) => team * TeamSize + slot;

        // ------------------------------------------------------------------ full court

        /// <summary>
        /// Full Court: the simulation always runs in the attacking team's frame (its hoop at the top).
        /// When team 1 attacks, that frame is the real court turned 180°.
        /// </summary>
        public bool Flipped => Setup.FullCourt && OffenseTeam == 1;

        /// <summary>
        /// Maps a simulation position to the fixed court the views draw (team 0 attacks the top hoop,
        /// team 1 the bottom one). Identity in the half-court game.
        /// </summary>
        public Vec2 ToWorldCourt(Vec2 p) => Flipped ? new Vec2(-p.x, Setup.Court.depth - p.y) : p;

        /// <summary>Facing as drawn on the fixed court.</summary>
        public Facing8 ToWorldFacing(Facing8 f) => Flipped ? Logic.FullCourt.Turn(f) : f;

        /// <summary>A stick direction (screen/court space) in the simulation frame.</summary>
        private Vec2 FrameMove(Vec2 move) => Flipped ? new Vec2(-move.x, -move.y) : move;

        /// <summary>Turns everything 180° (possession changed on a live ball): what you see doesn't move.</summary>
        private void TurnFrame()
        {
            float len = Setup.Court.depth;
            Vec2 M(Vec2 p) => new Vec2(-p.x, len - p.y);
            foreach (var p in Players)
            {
                var m = p.Motion;
                m.position = M(m.position);
                m.velocity = new Vec2(-m.velocity.x, -m.velocity.y);
                m.facing = Logic.FullCourt.Turn(m.facing);
                p.Motion = m;
            }
            Ball.Position = M(Ball.Position);
            Ball.Velocity = new Vec2(-Ball.Velocity.x, -Ball.Velocity.y);
            Ball.FlightFrom = M(Ball.FlightFrom);
            Ball.FlightTo = M(Ball.FlightTo);
            for (int i = 0; i < _ai.Length; i++) _ai[i].Target = M(_ai[i].Target);
        }

        /// <summary>Full Court restart: the ball is inbounded from the far baseline (or half court at tip-off).</summary>
        private void PlaceFullCourt(int offenseTeam)
        {
            var c = Setup.Court;
            bool tip = Time <= 0f;
            float baseY = tip ? Logic.FullCourt.MidY + 1.5f : c.depth - 1.0f;
            Vec2[] spots = tip
                ? new[]
                {
                    new Vec2(0f, baseY), new Vec2(-4.5f, baseY - 3f), new Vec2(4.5f, baseY - 3f),
                    new Vec2(-3f, baseY - 7f), new Vec2(3f, baseY - 7f),
                }
                : new[]
                {
                    // Inbound: slot 0 on the baseline, a guard stepping up to take it, the rest spread.
                    new Vec2(1.2f, c.depth - 0.3f), new Vec2(-2.5f, c.depth - 3.5f), new Vec2(4.5f, c.depth - 5f),
                    new Vec2(-3f, c.depth - 9f), new Vec2(3f, c.depth - 10f),
                };
            for (int slot = 0; slot < TeamSize; slot++)
            {
                var o = Players[Index(offenseTeam, slot)];
                o.Motion = new MotionState(c.Clamp(spots[slot % spots.Length]), Facing8.N);
            }
            for (int slot = 0; slot < TeamSize; slot++)
            {
                // The defence is back in its own half, between its man and the hoop.
                var man = Players[Index(offenseTeam, slot)].Position;
                float y = Math.Min(man.y - 2f, Logic.FullCourt.MidY - 1f - (slot % 2) * 2.5f);
                var d = Players[Index(1 - offenseTeam, slot)];
                d.Motion = new MotionState(c.Clamp(new Vec2(man.x * 0.7f, y)), Facing8.S);
            }
        }

        /// <summary>Puts the ball in a teammate's hands during live play (tutorial "ASK" step).</summary>
        public void HandBallTo(int playerIndex)
        {
            if (Phase != MatchPhase.Live || playerIndex < 0 || playerIndex >= Players.Length) return;
            if (Ball.IsHeld && Players[Ball.HolderIndex].Team != Players[playerIndex].Team) return;
            CancelCharge();
            GiveBall(playerIndex, announce: true, fromCheck: false);
        }

        /// <summary>
        /// Shooting games: puts the ball straight back in <paramref name="player"/>'s hands where they stand
        /// and goes live (no check-ball reset), so reps flow without walking back to the top.
        /// </summary>
        public void ResumeWithBall(int player)
        {
            if (Phase == MatchPhase.Final || player < 0 || player >= Players.Length || _finalPending) return;
            CancelCharge();
            _alleyOop = false;
            _glassToss = false;
            _reboundable = false;
            _lastShotWasMiss = false;
            OffenseTeam = Players[player].Team;
            GiveBall(player, announce: false, fromCheck: true);
            MustClear = false;
            ShotClock = Setup.Rules.shotClockSeconds;
            if (Phase != MatchPhase.Live) GoLive();
        }

        /// <summary>Restarts play with a check ball for <paramref name="team"/> (tutorial steps, tools).</summary>
        public void RestartWithBall(int team)
        {
            if (Phase == MatchPhase.Final) return;
            CancelCharge();
            MustClear = false;
            CheckBall(team);
        }

        // ------------------------------------------------------------------ phases

        /// <summary>Resets positions for a check-ball: offense spread out, ball to slot 0 at the check spot.</summary>
        public void CheckBall(int offenseTeam)
        {
            OffenseTeam = offenseTeam;
            var court = Setup.Court;
            if (Time > 0f && _bench.Count > 0) MakeSubstitutions();
            if (Setup.FullCourt) PlaceFullCourt(offenseTeam);
            else
            {
                for (int slot = 0; slot < TeamSize; slot++)
                {
                    var o = Players[Index(offenseTeam, slot)];
                    o.Motion = new MotionState(Formation.OffenseSpot(slot, court), Facing8.S);
                }
                for (int slot = 0; slot < TeamSize; slot++)
                {
                    var man = Players[Index(offenseTeam, slot)];
                    var d = Players[Index(1 - offenseTeam, slot)];
                    d.Motion = new MotionState(Formation.GuardSpot(man.Position, slot == 0, court), Facing8.N);
                }
            }
            if (Setup.OneOnOne)
                foreach (var p in Players)
                    if (IsBenched(p.Index)) p.Motion = new MotionState(BenchSpot(p), Facing8.S);
            CancelCharge();
            _alleyOop = false;
            _glassToss = false;
            EndPlay();
            ResetMatchups();
            GiveBall(Index(offenseTeam, 0), announce: false, fromCheck: true);
            MustClear = false;
            _reboundable = false;
            ShotClock = Setup.Rules.shotClockSeconds;
            StartFullCourtPossession(inbound: Time > 0f);
            Phase = MatchPhase.CheckBall;
            PhaseTimer = Setup.Flow.checkBallSeconds;
            ResetAiTimers();
            Events.Add(new MatchEvent(MatchEventType.CheckBall, Ball.HolderIndex, offenseTeam));
        }

        private void GoLive()
        {
            Phase = MatchPhase.Live;
            PhaseTimer = 0f;
            Events.Add(new MatchEvent(MatchEventType.LiveBall, Ball.HolderIndex, OffenseTeam));
        }

        private void GoDead(int nextOffense, float seconds)
        {
            CancelCharge();
            _alleyOop = false;
            _glassToss = false;
            _nextOffense = nextOffense;
            Phase = MatchPhase.DeadBall;
            PhaseTimer = seconds;
        }

        private void EndGame(GameOverReason reason)
        {
            CancelCharge();
            Phase = MatchPhase.Final;
            EndReason = reason;
            int w = Scoring.Winner(Score[0], Score[1]);
            Winner = w;
            Events.Add(new MatchEvent(MatchEventType.GameOver, -1, w));
        }

        // ------------------------------------------------------------------ step

        private PlayerInput _input2;

        /// <summary>Advances the match by <paramref name="dt"/> seconds using the human's input.</summary>
        public void Step(float dt, PlayerInput input) => Step(dt, input, default);

        /// <summary>Two-player step: <paramref name="input2"/> drives player 2 (ignored unless SecondHuman).</summary>
        public void Step(float dt, PlayerInput input, PlayerInput input2)
        {
            StepCore(dt, input, input2);
            ProcessStreetEvents();
        }

        private void StepCore(float dt, PlayerInput input, PlayerInput input2)
        {
            _input2 = SecondControlledIndex >= 0 ? input2 : default;
            Events.Clear();
            if (dt <= 0f) return;
            Time += dt;
            if (_bench.Count > 0) RestBench(dt);

            switch (Phase)
            {
                case MatchPhase.Final:
                    Integrate(dt, freezeAll: true);
                    return;

                case MatchPhase.DeadBall:
                    PhaseTimer -= dt;
                    Integrate(dt, freezeAll: true);
                    StepBallDead(dt);
                    if (PhaseTimer <= 0f)
                    {
                        if (_finalPending) EndGame(EvaluateEnd());
                        else CheckBall(_nextOffense);
                    }
                    return;

                case MatchPhase.CheckBall:
                    PhaseTimer -= dt;
                    bool humanStarts = (HumanHasBall && (input.Move.SqrMagnitude > 0.04f || input.ShootPressed || input.PassPressed))
                        || (SecondControlledIndex >= 0 && Ball.IsHeld && Ball.HolderIndex == SecondControlledIndex
                            && (_input2.Move.SqrMagnitude > 0.04f || _input2.ShootPressed || _input2.PassPressed));
                    if (PhaseTimer <= 0f || humanStarts) GoLive();
                    else
                    {
                        StepHeldBall(dt);
                        return;
                    }
                    break;
            }

            StepLive(dt, input);
        }

        private void StepLive(float dt, PlayerInput input)
        {
            if (!NoHuman) HandleHuman(ControlledIndex, input);
            if (SecondControlledIndex >= 0) HandleHuman(SecondControlledIndex, _input2);
            UpdateDrive();
            UpdatePlay();
            TryRunPendingPlay();
            UpdateAi(dt);
            UpdateCharge(dt, input);

            // Desired movement.
            for (int i = 0; i < Players.Length; i++)
            {
                if (i == ChargingIndex) { _desired[i] = Vec2.Zero; continue; }
                if (i == _driveIndex) { _desired[i] = DriveDesired(Players[i]); continue; }
                // The inbounder stays on the baseline until the pass is away.
                if (MustInbound && Ball.IsHeld && i == Ball.HolderIndex) { _desired[i] = Vec2.Zero; continue; }
                if (i == ControlledIndex && !NoHuman) { _desired[i] = SkillDesired(i, FrameMove(input.Move)); continue; }
                if (i == SecondControlledIndex) { _desired[i] = SkillDesired(i, FrameMove(_input2.Move)); continue; }
                _desired[i] = SkillDesired(i, AiDesired(Players[i]));
            }
            Integrate(dt, freezeAll: false);

            StepBall(dt);
            UpdateFullCourtRules(dt);
            if (Phase != MatchPhase.Live) return;

            if (MustClear && Ball.IsHeld && Players[Ball.HolderIndex].Team == OffenseTeam
                && Setup.Court.ZoneOf(Players[Ball.HolderIndex].Position) == ShotZone.BeyondArc)
            {
                MustClear = false;
                Events.Add(new MatchEvent(MatchEventType.BallCleared, Ball.HolderIndex, OffenseTeam));
            }

            if (Phase == MatchPhase.Live) TickClocks(dt);
        }

        private void Integrate(float dt, bool freezeAll)
        {
            var tuning = Setup.Movement;
            for (int i = 0; i < Players.Length; i++)
            {
                var p = Players[i];
                var desired = freezeAll || Time < p.StunnedUntil ? Vec2.Zero : _desired[i];
                bool dribbling = Ball.IsHeld && Ball.HolderIndex == i;
                float scale = IsHumanControlled(i) || p.Team == Setup.HumanTeam ? 1f : AiProfile(p.Team).movementScale;
                if (Time < p.ScreenedUntil) scale *= Setup.Defense.screenSlow;
                float max = Movement.MaxSpeed(p.Def.attributes.speed, dribbling, tuning, scale) * StaminaSpeedFactor(p) * HeatSpeedFactor(p) * SkillSpeedFactor(i);
                if (Setup.StreetRules && dribbling && !freezeAll) CheckAnkles(i, desired);
                p.Motion = Movement.Step(p.Motion, desired, max, dt, tuning);
                UpdateStamina(p, max, dt, freezeAll);
                _scratch[i] = p.Motion.position;
            }
            Movement.Separate(_scratch, tuning.bodyRadius, Setup.Court);
            for (int i = 0; i < Players.Length; i++) Players[i].Motion.position = _scratch[i];
        }

        // ------------------------------------------------------------------ street: ankle breakers

        public const float AnkleCutAngle = 110f;
        public const float AnkleRange = 1.9f;
        public const float AnkleStumbleSeconds = 0.9f;
        public const float AnkleCooldown = 1.6f;
        private Vec2[] _lastCut;
        private float[] _ankleReadyAt;

        /// <summary>Chance a sharp cut breaks this defender: the handler's handle and burst against the defender's feet.</summary>
        public static float AnkleChance(AttributeSet handler, AttributeSet defender)
        {
            float handle = (handler.playmaking + handler.speed) * 0.5f;
            float feet = (defender.defense + defender.speed) * 0.5f;
            return Math.Max(0.08f, Math.Min(0.55f, 0.25f + (handle - feet) / 120f));
        }

        private void CheckAnkles(int i, Vec2 desired)
        {
            if (Phase != MatchPhase.Live) return;
            if (_lastCut == null) { _lastCut = new Vec2[Players.Length]; _ankleReadyAt = new float[Players.Length]; }
            if (desired.Magnitude < 0.5f) return;
            var prev = _lastCut[i];
            _lastCut[i] = desired;
            if (prev.Magnitude < 0.5f || Time < _ankleReadyAt[i]) return;
            float cos = (prev.x * desired.x + prev.y * desired.y) / (prev.Magnitude * desired.Magnitude);
            if (cos > (float)Math.Cos(AnkleCutAngle * Math.PI / 180.0)) return;
            var p = Players[i];
            PlayerRuntimeState victim = null;
            float best = AnkleRange;
            foreach (var d in Players)
            {
                if (d.Team == p.Team || IsBenched(d.Index) || Time < d.StunnedUntil) continue;
                float dist = Vec2.Distance(d.Position, p.Position);
                if (dist < best) { best = dist; victim = d; }
            }
            if (victim == null) return;
            _ankleReadyAt[i] = Time + AnkleCooldown;
            if (_rng.NextFloat() >= AnkleChance(p.Def.attributes, victim.Def.attributes)) return;
            victim.StunnedUntil = Time + AnkleStumbleSeconds;
            Stats[i].ankleBreakers++;
            Events.Add(new MatchEvent(MatchEventType.AnkleBreaker, i, p.Team, victim.Index));
        }

        private void TickClocks(float dt)
        {
            if (!ClocksRunning) return;
            var rules = Setup.Rules;
            bool shotInAir = Ball.Phase == BallPhase.Shot;

            if (rules.useGameClock && GameClock > 0f) GameClock = Math.Max(0f, GameClock - dt);
            if (!shotInAir && Ball.Phase != BallPhase.Loose) ShotClock = Math.Max(0f, ShotClock - dt);

            if (!shotInAir && ShotClock <= 0f && rules.shotClockSeconds < 99f)
            {
                int holder = Ball.IsHeld ? Ball.HolderIndex : -1;
                if (holder >= 0) Stats[holder].turnovers++;
                Events.Add(new MatchEvent(MatchEventType.ShotClockViolation, holder, OffenseTeam));
                GoDead(DefenseTeam, Setup.Flow.deadBallAfterTurnover);
                return;
            }

            if (rules.useGameClock && GameClock <= 0f && !shotInAir)
            {
                var reason = Scoring.Evaluate(Score[0], Score[1], GameClock, rules);
                if (reason != GameOverReason.None) EndGame(reason);
            }
        }

        private GameOverReason EvaluateEnd()
        {
            var r = Scoring.Evaluate(Score[0], Score[1], GameClock, Setup.Rules);
            return r == GameOverReason.None ? GameOverReason.TargetScore : r;
        }

        // ------------------------------------------------------------------ human

        /// <summary>Applies one person's buttons to their player (offense or defense).</summary>
        private void HandleHuman(int me, PlayerInput input)
        {
            int team = Players[me].Team;
            if (OffenseTeam != team)
            {
                HandleHumanDefense(me, input);
                return;
            }
            if (input.CallPlay != PlayCall.None) StartPlay(team, input.CallPlay, me);
            // THE STREETS: SHOWSTOPPER (anyone on the team can call it) and tricks (the ball handler).
            if (input.BreakerPressed) CallBreaker(team);
            bool hasBall = Ball.IsHeld && Ball.HolderIndex == me;
            if (input.TrickPressed && hasBall && DoTrick(me, TrickFor(me, FrameMove(input.Move)))) return;
            bool teamHasBall = Ball.IsHeld && Players[Ball.HolderIndex].Team == team;
            // Skill moves come from the stick alone (no button this step).
            FeedSkillMoves(me, input, hasBall && ChargingIndex < 0
                && !input.ShootPressed && !input.PassPressed && !input.DunkPressed && !input.LayupPressed);
            if (hasBall && ChargingIndex < 0) LandStepBackShot(me, input);
            if (hasBall && ChargingIndex < 0)
            {
                bool hopping = StepBackShotPending(me); // mid step-back (SHOOT double tap): he lands first
                if (hopping) { /* the jumper starts when he lands (LandStepBackShot) */ }
                else if (input.ShootPressed && CanShoot(me) && TryStepBackShot(me)) CancelDrive();
                else if (input.ShootPressed && CanShoot(me)) { CancelDrive(); BeginCharge(me, -1f); }
                else if ((input.DunkPressed || input.LayupPressed) && CanShoot(me)) StartFinish(me, input.DunkPressed);
                else if (input.PassPressed)
                {
                    CancelDrive();
                    // A teammate running the baseline (or sitting at the rim) turns PASS into an alley-oop lob.
                    int oop = AlleyOopCandidate(me);
                    PassFrom(me, FrameMove(input.Move), oop);
                }
                else if (_driveIndex == me && FrameMove(input.Move).SqrMagnitude > 0.5f
                         && Vec2.Dot(FrameMove(input.Move).Normalized, (Setup.Court.Hoop - Players[me].Position).Normalized) < -0.3f)
                    CancelDrive(); // pulled the stick away: back out of the drive
            }
            else if (input.PassPressed && teamHasBall && !IsHumanControlled(Ball.HolderIndex) && ChargingIndex != Ball.HolderIndex)
            {
                // Call for the ball: the AI teammate with it passes to this person.
                PassFrom(Ball.HolderIndex, Vec2.Zero, me);
            }
        }

        public bool CanShoot(int playerIndex)
        {
            return Phase == MatchPhase.Live && Ball.IsHeld && Ball.HolderIndex == playerIndex
                   && ChargingIndex < 0 && !MustClear && !MustInbound;
        }

        /// <summary>The teammate holding the ball passes it to the human (practice drills, ASK).</summary>
        public bool PassToHuman()
        {
            if (!HumanTeamHasBall || HumanHasBall || ChargingIndex == Ball.HolderIndex) return false;
            return PassFrom(Ball.HolderIndex, Vec2.Zero, ControlledIndex);
        }

        /// <summary>Who a pass from the human would go to right now (for the receiver marker), or -1.</summary>
        public int PreviewPassTarget(Vec2 aim)
        {
            if (!HumanHasBall) return -1;
            int oop = AlleyOopCandidate(ControlledIndex);
            return oop >= 0 ? oop : SelectPassTarget(ControlledIndex, FrameMove(aim));
        }

        // ------------------------------------------------------------------ shooting

        private void BeginCharge(int shooter, float aiReleaseMeter)
        {
            var p = Players[shooter];
            var court = Setup.Court;
            float dist = court.DistanceToHoop(p.Position);
            var zone = court.ZoneOf(p.Position);
            var toHoop = (court.Hoop - p.Position).Normalized;
            bool attacking = Vec2.Dot(p.Motion.velocity, toHoop) > 1.5f;
            ChargeType = ShotModel.Classify(dist, zone, p.Def.attributes.finishing, attacking, Setup.Shot);
            ChargingIndex = shooter;
            ChargeTime = 0f;
            _aiReleaseMeter = aiReleaseMeter;
            p.Motion.velocity = p.Motion.velocity * 0.3f; // plant for the shot
            p.Motion.facing = Movement.FacingOf(toHoop);
            Events.Add(new MatchEvent(MatchEventType.ShotStarted, shooter, p.Team, (int)ChargeType));
        }

        private void UpdateCharge(float dt, PlayerInput input)
        {
            if (ChargingIndex < 0) return;
            if (!Ball.IsHeld || Ball.HolderIndex != ChargingIndex || Phase != MatchPhase.Live)
            {
                CancelCharge();
                return;
            }
            ChargeTime += dt;
            float fill = ShotModel.FillSeconds(ChargeType, Setup.Shot);
            float meter = ChargeTime / fill;
            float overHold = 1f + Setup.Shot.overHoldSeconds / fill;

            bool byPerson = !_autoRelease && ((ChargingIndex == ControlledIndex && !NoHuman) || ChargingIndex == SecondControlledIndex);
            bool release = _autoRelease ? meter >= _aiReleaseMeter
                         : ChargingIndex == ControlledIndex && !NoHuman ? !input.ShootHeld
                         : ChargingIndex == SecondControlledIndex ? !_input2.ShootHeld
                         : meter >= _aiReleaseMeter;
            // A quick tap of SHOOT (let go almost at once) is a pump fake, not a shot.
            if (release && byPerson && ChargeTime < Setup.Shot.pumpFakeSeconds)
            {
                PumpFake(ChargingIndex);
                return;
            }
            if (release || meter >= overHold) ReleaseShot(ChargingIndex, meter);
        }

        private void CancelCharge()
        {
            ChargingIndex = -1;
            ChargeTime = 0f;
            _aiReleaseMeter = -1f;
            _autoRelease = false;
        }

        /// <summary>Builds the context for a shot from <paramref name="shooter"/> at <paramref name="meter"/>.</summary>
        public ShotContext ShotContextFor(int shooter, ShotType type, float meter)
        {
            var p = Players[shooter];
            var court = Setup.Court;
            ContestFor(p, out float nearestDist, out int nearestDefense);
            return new ShotContext
            {
                Type = type,
                Distance = court.DistanceToHoop(p.Position),
                Meter = meter,
                Shooter = p.Def.attributes,
                NearestDefenderDistance = nearestDist,
                NearestDefenderDefense = nearestDefense,
                HotStreak = p.HotStreak,
                Heated = p.HotStreak >= Setup.Shot.heatThreshold,
                Stamina01 = p.Stamina,
                LateGame = Setup.Rules.useGameClock && GameClock <= Setup.Flow.lateGameSeconds,
                Bank = type == ShotType.Layup && IsBankAngle(p.Position, court.Hoop),
                Bonus = (Setup.HumanSpotBonus != null && IsHumanControlled(shooter) && shooter == ControlledIndex
                    ? Setup.HumanSpotBonus[(int)ShotZones.SpotOf(p.Position, court)] : 0f) + BreakerBonusFor(shooter),
            };
        }

        private void NearestOpponent(PlayerRuntimeState p, out float distance, out int defense)
        {
            distance = float.MaxValue;
            defense = 50;
            if (Setup.PassiveOpponents) return;
            for (int i = 0; i < Players.Length; i++)
            {
                var o = Players[i];
                if (o.Team == p.Team || IsBenched(i)) continue;
                float d = Vec2.Distance(o.Position, p.Position);
                if (d < distance)
                {
                    distance = d;
                    defense = o.Def.attributes.defense;
                }
            }
        }

        private void ReleaseShot(int shooter, float meter)
        {
            var p = Players[shooter];
            var court = Setup.Court;
            var flow = Setup.Flow;
            var type = ChargeType;
            int blocker = TryBlock(p, type);
            if (blocker >= 0)
            {
                LastReleaseMeter = meter;
                Events.Add(new MatchEvent(MatchEventType.ShotReleased, shooter, p.Team,
                    (int)ShotModel.Evaluate(ShotContextFor(shooter, type, meter), Setup.Shot).Feedback, 0f));
                ResolveBlock(shooter, blocker);
                return;
            }
            var ctx = ShotContextFor(shooter, type, meter);
            LastReleaseMeter = meter;
            var eval = ShotModel.Evaluate(ctx, Setup.Shot);
            bool makes = eval.GuaranteedMake || _rng.NextFloat() < eval.MakeChance;
            var zone = court.ZoneOf(p.Position);
            int points = Scoring.PointsFor(zone, Setup.Rules);

            var line = Stats[shooter];
            if (eval.Grade == TimingGrade.Green) line.greenReleases++;
            line.fieldGoalsAttempted++;
            if (zone == ShotZone.BeyondArc) line.arcAttempted++;
            line.chart.spots[(int)ShotZones.SpotOf(p.Position, court)].attempted++;

            CancelCharge();
            Ball.Phase = BallPhase.Shot;
            Ball.HolderIndex = -1;
            Ball.ShooterIndex = shooter;
            Ball.ShotWillScore = makes;
            Ball.ShotPoints = points;
            Ball.ShotGrade = eval.Grade;
            Ball.ShotType = type;
            Ball.ShotBank = ctx.Bank;
            Ball.FlightFrom = p.Position;
            Ball.FlightTo = court.Hoop;
            Ball.FlightTime = 0f;
            float dist = ctx.Distance;
            bool dunk = type == ShotType.Dunk;
            Ball.FlightDuration = dunk ? flow.dunkFlightSeconds : flow.shotFlightBase + dist * flow.shotFlightPerMeter;
            Ball.FlightStartHeight = dunk ? flow.dunkReleaseHeight : flow.releaseHeight;
            Ball.FlightEndHeight = RimHeight;
            Ball.FlightArc = dunk ? 0.2f : flow.shotArcBase + dist * flow.shotArcPerMeter;
            Ball.Position = p.Position;
            Ball.Height = Ball.FlightStartHeight;
            _reboundable = false;

            Events.Add(new MatchEvent(MatchEventType.ShotReleased, shooter, p.Team, (int)eval.Feedback, eval.MakeChance));
        }

        public const float RimHeight = 3.05f;

        private void ResolveShot()
        {
            int shooter = Ball.ShooterIndex;
            var p = Players[shooter];
            var line = Stats[shooter];
            var court = Setup.Court;

            if (Ball.ShotWillScore)
            {
                Score[p.Team] += Ball.ShotPoints;
                line.points += Ball.ShotPoints;
                line.fieldGoalsMade++;
                if (Ball.ShotPoints >= Setup.Rules.beyondArcPoints) line.arcMade++;
                var chartSpot = line.chart.spots[(int)ShotZones.SpotOf(Ball.FlightFrom, court)];
                if (chartSpot.made < chartSpot.attempted) chartSpot.made++;
                p.HotStreak++;
                OnMadeForHeat(p);
                AdjustScheme(p.Team, Ball.ShotPoints, Ball.ShotType);
                if (_lastCatcher == shooter && _lastPasser >= 0 && Players[_lastPasser].Team == p.Team
                    && Time - _lastCatchTime <= Setup.Flow.assistWindowSeconds)
                    Stats[_lastPasser].assists++;
                _lastPasser = -1;
                _lastCatcher = -1;

                Events.Add(new MatchEvent(MatchEventType.ShotMade, shooter, p.Team, Ball.ShotPoints));
                // A made dunk: the dunker hangs (and swings) on the rim for a moment before dropping, and can't move meanwhile.
                if (Ball.ShotType == ShotType.Dunk) p.StunnedUntil = Math.Max(p.StunnedUntil, Time + Setup.Flow.rimHangSeconds);
                if (Setup.Rules.bustRule && Score[p.Team] > Setup.Rules.targetScore)
                {
                    Score[p.Team] = Setup.Rules.bustScore;
                    Events.Add(new MatchEvent(MatchEventType.Bust, shooter, p.Team, Score[p.Team]));
                }

                // Ball drops through the net and is dead.
                Ball.Phase = BallPhase.Loose;
                Ball.Position = court.Hoop;
                Ball.Height = RimHeight - 0.4f;
                Ball.Velocity = new Vec2(0f, 0.6f);
                Ball.VerticalVelocity = -1f;

                var end = Scoring.Evaluate(Score[0], Score[1], GameClock, Setup.Rules);
                _finalPending = end != GameOverReason.None;
                // Half-court 3-on-3 is winners' ball (and practice keeps it for more reps); otherwise it changes hands.
                bool keep = Setup.KeepPossessionAfterScore || Setup.Rules.makeItTakeIt || Setup.WinnersBall || Setup.OneOnOne; // 1-on-1: make it, take it
                GoDead(keep ? p.Team : 1 - p.Team, Setup.Flow.deadBallAfterMake);
                return;
            }

            // Miss: carom off the rim, away from the hoop, toward the shooter's side.
            CoolOff(p);
            _lastShotWasMiss = true;
            Events.Add(new MatchEvent(MatchEventType.ShotMissed, shooter, p.Team, (int)Ball.ShotGrade));
            var away = (Ball.FlightFrom - court.Hoop).Normalized;
            if (away.SqrMagnitude < 0.01f) away = Vec2.Up;
            float angle = (_rng.NextFloat() * 2f - 1f) * 1.1f; // ±63°
            float cos = (float)Math.Cos(angle), sin = (float)Math.Sin(angle);
            var dir = new Vec2(away.x * cos - away.y * sin, away.x * sin + away.y * cos);
            if (dir.y < 0.1f) dir = new Vec2(dir.x, 0.1f).Normalized; // never back through the baseline
            float speed = Setup.Flow.missSpeedMin + (Setup.Flow.missSpeedMax - Setup.Flow.missSpeedMin) * _rng.NextFloat();
            Ball.Phase = BallPhase.Loose;
            Ball.Position = court.Hoop;
            Ball.Height = RimHeight;
            Ball.Velocity = dir * speed;
            Ball.VerticalVelocity = Setup.Flow.missUpSpeed;
            _reboundable = true;
            if (Setup.Flow.resetShotClockOnRim) ShotClock = Setup.Rules.shotClockSeconds;
            Events.Add(new MatchEvent(MatchEventType.BallLoose, -1, -1));

            if (Setup.Rules.useGameClock && GameClock <= 0f)
            {
                var end = Scoring.Evaluate(Score[0], Score[1], GameClock, Setup.Rules);
                if (end != GameOverReason.None) EndGame(end);
            }
        }

        // ------------------------------------------------------------------ passing

        private int SelectPassTarget(int passer, Vec2 aim)
        {
            var p = Players[passer];
            _candidates.Clear();
            _defenders.Clear();
            for (int i = 0; i < Players.Length; i++)
            {
                if (i == passer || IsBenched(i)) continue;
                if (Players[i].Team == p.Team) _candidates.Add(new PassCandidate { PlayerIndex = i, Position = Players[i].Position });
                else if (!Setup.PassiveOpponents) _defenders.Add(Players[i].Position);
            }
            int k = PassModel.SelectTarget(p.Position, aim, _candidates, _defenders, Setup.Pass);
            return k < 0 ? -1 : _candidates[k].PlayerIndex;
        }

        /// <summary>Throws a pass. <paramref name="forcedTarget"/> ≥ 0 skips target selection.</summary>
        private bool PassFrom(int passer, Vec2 aim, int forcedTarget)
        {
            if (Phase != MatchPhase.Live || !Ball.IsHeld || Ball.HolderIndex != passer) return false;
            int target = forcedTarget >= 0 ? forcedTarget : SelectPassTarget(passer, aim);
            if (target < 0 || target == passer) return false;

            var from = Players[passer];
            var to = Players[target];
            var t = Setup.Pass;

            // Lane risk and possible interception.
            float minClear = float.MaxValue;
            int threat = -1;
            if (!Setup.PassiveOpponents)
            {
                for (int i = 0; i < Players.Length; i++)
                {
                    if (Players[i].Team == from.Team) continue;
                    float c = PassModel.LaneClearance(from.Position, to.Position, Players[i].Position, out _);
                    if (c < minClear)
                    {
                        minClear = c;
                        threat = i;
                    }
                }
            }
            var type = PassModel.ChooseType(minClear, t);
            int receiver = target;
            var destination = to.Position;
            bool oop = IsAlleyOopTarget(target);
            if (threat >= 0)
            {
                float chance = PassModel.InterceptChance(type, minClear, Players[threat].Def.attributes.defense,
                                                         from.Def.attributes.playmaking, t);
                if (oop) chance *= t.alleyOopInterceptScale; // the lob goes over the top
                if (chance > 0f && _rng.NextFloat() < chance)
                {
                    receiver = threat;
                    PassModel.LaneClearance(from.Position, to.Position, Players[threat].Position, out float along);
                    destination = from.Position + (to.Position - from.Position) * along;
                }
            }

            CancelCharge();
            Ball.Phase = BallPhase.Pass;
            Ball.HolderIndex = -1;
            Ball.PasserIndex = passer;
            Ball.TargetIndex = receiver;
            Ball.PassType = type;
            Ball.FlightFrom = from.Position;
            Ball.FlightTo = destination;
            Ball.FlightTime = 0f;
            Ball.Position = from.Position;
            Ball.Height = Setup.Flow.passHeight;
            _alleyOop = oop && receiver == target;
            _oopToRunner = _alleyOop && IsRunningBaseline(target);
            _lastPasser = passer;
            OnPassForPlays(passer, receiver);
            Events.Add(new MatchEvent(MatchEventType.PassThrown, passer, from.Team, (int)type));
            MustInbound = false; // the inbound pass is away
            OnAiPassed(passer);
            return true;
        }

        // ------------------------------------------------------------------ ball

        private void StepBall(float dt)
        {
            switch (Ball.Phase)
            {
                case BallPhase.Held: StepHeldBall(dt); break;
                case BallPhase.Pass: StepPass(dt); break;
                case BallPhase.Shot: StepShot(dt); break;
                default: StepLoose(dt, allowPickup: true); break;
            }
        }

        private void StepHeldBall(float dt)
        {
            if (!Ball.IsHeld) return;
            var holder = Players[Ball.HolderIndex];
            Ball.DribbleTime += dt;
            Ball.Position = holder.Position + DribbleOffset(holder.Motion.facing);
            Ball.Velocity = holder.Motion.velocity;
            Ball.Height = Ball.HolderIndex == ChargingIndex
                ? 1.6f + 0.6f * Math.Min(1f, ChargeMeter)
                : BallPhysics.DribbleHeight(Ball.DribbleTime, Setup.Ball);
            if (StyleOn && Ball.HolderIndex != ChargingIndex) TrickBall(holder);
        }

        private void StepPass(float dt)
        {
            if (_glassToss)
            {
                StepGlassToss(dt);
                return;
            }
            var t = Setup.Pass;
            float speed = PassModel.Speed(Ball.PassType, t);
            Ball.FlightTime += dt;
            var receiver = Players[Ball.TargetIndex];
            bool intercepting = receiver.Team != Players[Ball.PasserIndex].Team;
            // Passes to teammates lead the receiver; interceptions fly to the lane point.
            var goal = intercepting ? Ball.FlightTo : receiver.Position;
            Ball.Position = Vec2.MoveTowards(Ball.Position, goal, speed * dt);

            float total = Math.Max(0.1f, Vec2.Distance(Ball.FlightFrom, goal));
            float progress = Math.Min(1f, Vec2.Distance(Ball.FlightFrom, Ball.Position) / total);
            Ball.Height = _alleyOop ? LobHeight(progress)
                : Ball.PassType == PassType.Bounce
                ? Setup.Flow.passHeight * Math.Abs(1f - 2f * progress) + 0.05f
                : Setup.Flow.passHeight;

            bool arrived = Vec2.Distance(Ball.Position, goal) <= 0.05f
                           || (!intercepting && Vec2.Distance(Ball.Position, receiver.Position) <= t.catchRadius);
            if (arrived)
            {
                if (intercepting)
                {
                    Stats[Ball.TargetIndex].steals++;
                    Stats[Ball.PasserIndex].turnovers++;
                    Events.Add(new MatchEvent(MatchEventType.Interception, Ball.TargetIndex, receiver.Team));
                }
                else
                {
                    _lastCatcher = Ball.TargetIndex;
                    _lastCatchTime = Time;
                    Events.Add(new MatchEvent(MatchEventType.PassCaught, Ball.TargetIndex, receiver.Team));
                    // The finisher has to still be near the rim when the lob arrives.
                    if (_alleyOop && Setup.Court.DistanceToHoop(receiver.Position) <= (_oopToRunner ? BaselineOopRange : Setup.Pass.alleyOopRange + 1f))
                    {
                        FinishAlleyOop(Ball.TargetIndex, Ball.PasserIndex);
                        return;
                    }
                }
                _alleyOop = false;
            _glassToss = false;
                GiveBall(Ball.TargetIndex, announce: false, fromCheck: false);
                return;
            }

            if (Ball.FlightTime > Setup.Flow.maxPassSeconds)
            {
                // Safety: a pass that never connects becomes a loose ball.
                _alleyOop = false;
            _glassToss = false;
                Ball.Phase = BallPhase.Loose;
                Ball.Velocity = (goal - Ball.Position).Normalized * 2f;
                Ball.VerticalVelocity = 0f;
                Events.Add(new MatchEvent(MatchEventType.BallLoose, -1, -1));
            }
        }

        private void StepShot(float dt)
        {
            float before = Ball.FlightTime / Ball.FlightDuration;
            Ball.FlightTime += dt;
            float u = Math.Min(1f, Ball.FlightTime / Ball.FlightDuration);
            if (Ball.ShotBank)
            {
                // Off the glass: up to the board on the shooter's side, then it drops in (or rims out).
                var spot = BankSpot(Ball.FlightFrom, Ball.FlightTo);
                if (u < BankSplit)
                {
                    float a = u / BankSplit;
                    Ball.Position = Vec2.Lerp(Ball.FlightFrom, spot, a);
                    Ball.Height = Ball.FlightStartHeight + (BankHeight - Ball.FlightStartHeight) * a + 2.4f * Ball.FlightArc * a * (1f - a);
                }
                else
                {
                    float b = (u - BankSplit) / (1f - BankSplit);
                    Ball.Position = Vec2.Lerp(spot, Ball.FlightTo, b);
                    Ball.Height = BankHeight + (Ball.FlightEndHeight - BankHeight) * b;
                }
                if (before < BankSplit && u >= BankSplit) Events.Add(new MatchEvent(MatchEventType.BankKiss, Ball.ShooterIndex, Players[Ball.ShooterIndex].Team));
            }
            else
            {
                Ball.Position = Vec2.Lerp(Ball.FlightFrom, Ball.FlightTo, u);
                Ball.Height = Ball.FlightStartHeight + (Ball.FlightEndHeight - Ball.FlightStartHeight) * u
                              + 4f * Ball.FlightArc * u * (1f - u);
            }
            if (u >= 1f) ResolveShot();
        }

        /// <summary>Team that touched the ball last (for out of bounds): the holder, a shooter or passer, or a blocker.</summary>
        private int _lastTouchTeam;

        /// <summary>Out of bounds: this far past a line (m), so a ball rolling on the line is still in.</summary>
        public const float OutOfBoundsMargin = 0.25f;

        public bool IsOutOfBounds(Vec2 p)
        {
            var c = Setup.Court;
            return p.x < -c.HalfWidth - OutOfBoundsMargin || p.x > c.HalfWidth + OutOfBoundsMargin
                   || p.y < -OutOfBoundsMargin || p.y > c.depth + OutOfBoundsMargin;
        }

        /// <summary>1-on-1: the ball rolled out. The other side gets it (checked up at the top).</summary>
        private void BallOutOfBounds()
        {
            int lost = _lastTouchTeam;
            Ball.Velocity = Vec2.Zero;
            Ball.VerticalVelocity = 0f;
            _reboundable = false;
            Events.Add(new MatchEvent(MatchEventType.Violation, -1, lost, (int)ViolationKind.OutOfBounds));
            GoDead(1 - lost, Setup.Flow.deadBallAfterTurnover);
        }

        private void StepBallDead(float dt)
        {
            if (Ball.Phase == BallPhase.Loose) BallPhysics.StepLoose(Ball, dt, Setup.Court, Setup.Ball);
            else if (Ball.Phase == BallPhase.Held) StepHeldBall(dt);
        }

        private void StepLoose(float dt, bool allowPickup)
        {
            var t = Setup.Ball;
            // 1-on-1 has real out of bounds; other modes keep the arcade walls.
            bool outOfBoundsRule = Setup.OneOnOne && Phase == MatchPhase.Live;
            BallPhysics.StepLoose(Ball, dt, Setup.Court, t, !outOfBoundsRule);
            if (outOfBoundsRule && IsOutOfBounds(Ball.Position))
            {
                BallOutOfBounds();
                return;
            }
            if (!allowPickup) return;

            int best;
            if (_reboundable)
            {
                best = PickRebounder();
            }
            else
            {
                // Nearest eligible player picks it up (ties go to the lower index: deterministic).
                best = -1;
                float bestDist = float.MaxValue;
                for (int i = 0; i < Players.Length; i++)
                {
                    if (Setup.PassiveOpponents && Players[i].Team != Setup.HumanTeam) continue;
                    if (IsBenched(i)) continue;
                    if (!BallPhysics.CanPickUp(Ball, Players[i].Position, t)) continue;
                    float d = Vec2.Distance(Ball.Position, Players[i].Position);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = i;
                    }
                }
            }
            if (best < 0) return;

            if (_reboundable)
            {
                Stats[best].rebounds++;
                Events.Add(new MatchEvent(MatchEventType.Rebound, best, Players[best].Team));
                _reboundable = false;
            }
            GiveBall(best, announce: true, fromCheck: false);
        }

        private void GiveBall(int playerIndex, bool announce, bool fromCheck)
        {
            var p = Players[playerIndex];
            if (fromCheck || p.Team != OffenseTeam)
            {
                _pumpFakes = 0; // a new possession: defenders fall for it again
                ResetSkillRepeats();
            }
            _lastTouchTeam = p.Team;
            Ball.Phase = BallPhase.Held;
            Ball.HolderIndex = playerIndex;
            Ball.Position = p.Position;
            Ball.Velocity = Vec2.Zero;
            Ball.VerticalVelocity = 0f;
            Ball.DribbleTime = 0f;
            Ball.TargetIndex = -1;
            Ball.ShooterIndex = -1;

            if (announce) Events.Add(new MatchEvent(MatchEventType.BallPickedUp, playerIndex, p.Team));
            if (!fromCheck && p.Team != OffenseTeam)
            {
                // Full Court: turn the picture so the new offence attacks "up" (nothing visibly moves).
                if (Setup.FullCourt) TurnFrame();
                OffenseTeam = p.Team;
                ShotClock = Setup.Rules.shotClockSeconds;
                // Change of possession on a live ball: take it back beyond the arc first (half court only;
                // in Full Court the other basket is already the far end).
                MustClear = !Setup.FullCourt && Setup.Court.ZoneOf(p.Position) != ShotZone.BeyondArc;
                StartFullCourtPossession(inbound: false);
                _lastPasser = -1;
                _lastCatcher = -1;
                EndPlay();
                ResetMatchups();
                Events.Add(new MatchEvent(MatchEventType.PossessionChanged, playerIndex, p.Team));
                ResetAiTimers();
            }
            else if (!fromCheck && _lastShotWasMiss)
            {
                // Offensive rebound keeps the possession; no clear needed.
                MustClear = false;
            }
            _lastShotWasMiss = false;
        }

        /// <summary>Knocks the ball loose from its holder (debug / future steals).</summary>
        public void KnockLoose(Vec2 velocity, float upwardSpeed = 2.5f)
        {
            CancelCharge();
            var from = Ball.IsHeld ? Players[Ball.HolderIndex].Position : Ball.Position;
            Ball.Phase = BallPhase.Loose;
            Ball.HolderIndex = -1;
            Ball.Position = from;
            Ball.Velocity = velocity;
            Ball.Height = Math.Max(Ball.Height, 0.8f);
            Ball.VerticalVelocity = upwardSpeed;
            Events.Add(new MatchEvent(MatchEventType.BallLoose, -1, -1));
        }

        /// <summary>Ball sits slightly to the side/front of the dribbler based on facing.</summary>
        public static Vec2 DribbleOffset(Facing8 facing)
        {
            switch (facing)
            {
                case Facing8.N: return new Vec2(0.25f, 0.15f);
                case Facing8.NE: return new Vec2(0.3f, 0.05f);
                case Facing8.E: return new Vec2(0.32f, -0.05f);
                case Facing8.SE: return new Vec2(0.25f, -0.2f);
                case Facing8.S: return new Vec2(0.25f, -0.2f);
                case Facing8.SW: return new Vec2(-0.25f, -0.2f);
                case Facing8.W: return new Vec2(-0.32f, -0.05f);
                default: return new Vec2(-0.3f, 0.05f);
            }
        }
    }
}
