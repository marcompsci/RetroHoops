using CallerRetroBall.Controls;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using CallerRetroBall.UI;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
// The game's own input struct, not UnityEngine.InputSystem.PlayerInput (the component).
using PlayerInput = CallerRetroBall.Logic.PlayerInput;

namespace CallerRetroBall.Gameplay
{
    /// <summary>
    /// GameScene entry point. Builds the match from the pending <see cref="MatchRequest"/>,
    /// runs the deterministic <see cref="MatchSimulation"/> on a fixed 60 Hz step, and syncs
    /// the views, camera, HUD, and controls from its state.
    ///
    /// Offense: shoot (hold/release meter), pass / ask, CALL plays. Defense: STEAL, JUMP, SWITCH.
    /// Post-game applies rewards exactly once, feeds Rise Mode, and saves the career.
    /// Practice runs a <see cref="PracticeSession"/> drill with its own HUD and end card.
    /// Editor/dev: WASD/arrows move, K shoot/jump (hold), J pass/switch, L steal, C call,
    /// Esc pause, B knocks the ball loose.
    /// </summary>
    public sealed partial class GameSceneController : MonoBehaviour
    {
        /// <summary>Simulation step: 1/60 s, or 1/120 s on 120 Hz screens with high frame rate on.</summary>
        private float FixedStep = 1f / 60f;
        private int MaxStepsPerFrame = 5;
        // Profiler markers (visible in Unity's Profiler and Xcode Instruments' signposts).
        private static readonly Unity.Profiling.ProfilerMarker StepMarker = new Unity.Profiling.ProfilerMarker("Retro Hoops.SimStep");
        private static readonly Unity.Profiling.ProfilerMarker ViewsMarker = new Unity.Profiling.ProfilerMarker("Retro Hoops.SyncViews");

        private MatchRequest _request;
        private MatchSimulation _match;
        private MatchArt _art;
        private PlayerView[] _playerViews;
        private BallView _ballView;
        private ShotMeterView _meter;
        private SpriteRenderer _receiverArrow;
        private CourtCameraRig _cameraRig;
        private TouchControls _controls;
        /// <summary>Tabletop 2 Player: player 2's touch controls at the top of the screen (null otherwise).</summary>
        private TouchControls _controls2;
        /// <summary>Pass-and-play Shootout between two people (null otherwise).</summary>
        private ShootoutDuel _duel;
        private MatchHud _hud;
        private readonly InputBuffer _buffer = new InputBuffer();
        private float _accumulator;
        private bool _paused;
        private bool _finalShown;
        private int _lastShooter = -1;
        private float _shootPoseUntil;
        private int _lastLabelState = -1;
        private PracticeSession _practice;
        // Phase 17: H-O-R-S-E.
        private HorseSession _horse;
        private int _horseSeen;
        private SpriteRenderer _horseMarker;
        // Phase 17: highlight GIFs.
        private bool _capturing;
        private readonly System.Collections.Generic.List<byte[]> _gifFrames = new System.Collections.Generic.List<byte[]>();
        private TutorialSession _tutorial;
        private DailyChallenge _daily;
        private readonly InputBuffer _buffer2 = new InputBuffer();
        private bool Versus => _request != null && _request.Mode == GameMode.Versus;

        // Two-phone play (2 PLAYER ► TWO PHONES): lockstep with the other phone; null otherwise.
        private Lockstep _link;
        private ILinkTransport _wire;
        private PlayerInput _linkCarry;
        private float _lastLinkStep, _nextWaitToast;
        private bool _linkEnded;
        /// <summary>Phase 30: this phone is watching someone else's two-phone game (null otherwise).</summary>
        private Spectator _watch;
        /// <summary>Phase 30, host of a two-phone game: sends the game to phones watching it.</summary>
        private SpectatorFeed _feed;
        /// <summary>The host shares this game with watchers (the feed itself is also kept for SAVE GAME TAPE).</summary>
        private bool _broadcast;
        private bool _tapeSaved;
        /// <summary>Playing a saved tape: 1x, 2x or 4x (tap the court).</summary>
        private int _tapeSpeed = 1;
        private NearbyLink _nearby;
        private int _watchersSeen;
        /// <summary>Phase 30: a Couch Cup game played on two phones (null otherwise).</summary>
        private LinkSetup _cupSetup;
        private bool _cupRecorded;
        /// <summary>Retro Hoops Live: the agreed setup (ratings), or null for two phones nearby.</summary>
        private LinkSetup _liveSetup;
        private string _opponent = "YOUR FRIEND";
        private bool _liveRated;
        /// <summary>Phase 33: this game's rating change (local ratings), for the rematch's starting ratings.</summary>
        private int _liveDelta;
        /// <summary>Set when the Live server accepted this game for rating.</summary>
        private string _serverMatchKey;

        /// <summary>Live: records the result once (a win, a loss, or a forfeit). Returns a line for the post-game screen.</summary>
        private string RateLive(bool won, Backend.Outcome outcome)
        {
            if (_liveSetup == null || _liveRated || App.Career == null) return null;
            _liveRated = true;
            var live = App.Career.live ?? (App.Career.live = new LiveSaveData());
            live.month = LiveMode.MonthKey(System.DateTime.UtcNow); // Phase 33: counts for this month's leaderboard
            LiveSeason.Observe(live, live.month, true); // Phase 36: counts toward this month's Live season
            // 12 Oct 2026 LIVE POINTS: wins + scoring + time played (a quit earns nothing).
            int theirRating = _link.Seat == 0 ? _liveSetup.GuestRating : _liveSetup.HostRating;
            var points = LivePoints.Record(live, System.DateTime.UtcNow, outcome != Backend.Outcome.Quit, won, live.rating, theirRating,
                                           _match.Score[LocalTeam], _match.Time);
            string pointsLine = "\n" + Loc.T("LIVE POINTS") + " " + LivePoints.Total(live) + " (+" + points.Total + ")  ·  " + points.Describe();
            if (_liveSetup.IsStreets && outcome != Backend.Outcome.Quit)
            {
                var st = App.Career.streets ?? (App.Career.streets = new StreetsSaveData());
                if (won) st.liveWins++; else st.liveLosses++;
                pointsLine += "\n" + Loc.T("LIVE STREETS") + " " + st.liveWins + "-" + st.liveLosses;
            }
            if (_serverMatchKey != null)
            {
                // Rated on the Retro Hoops Live server: it changes ratings only when both phones' reports agree.
                var hud = _hud;
                BackendClient.ReportResult(_serverMatchKey, _match.Score[0], _match.Score[1], SimHash.Of(_match), outcome, (state, change, p) =>
                {
                    if (hud == null) return;
                    if (state == "settled") hud.Toast("LIVE RATING " + (p?.Rating ?? live.rating) + " (" + (change >= 0 ? "+" : "") + change + ")", 3f);
                    else if (state == "open") hud.Toast("RESULT SENT: RATING UPDATES WHEN BOTH RESULTS ARE IN", 3f);
                    else if (state == "disputed") hud.Toast("THE TWO RESULTS DIDN'T MATCH: NO RATING CHANGE", 3f);
                });
                App.SaveCareer();
                App.ReportGameCenter();
                return "Result sent to Retro Hoops Live (" + (won ? "win" : "loss") + "). Ratings update on the server." + pointsLine;
            }
            int delta = LiveMode.Apply(live, theirRating, won);
            LiveSeason.Observe(live, live.month, false);
            _liveDelta = delta;
            App.SaveCareer();
            App.ReportGameCenter();
            return "LIVE RATING " + live.rating + " (" + (delta >= 0 ? "+" : "") + delta + ")  ·  " + LiveMode.Tier(live.rating) + "  ·  " + live.wins + "-" + live.losses + pointsLine;
        }
        /// <summary>The team this phone plays: the guest of a two-phone game is team B.</summary>
        private int LocalTeam => _link != null ? _link.Seat : _match.Setup.HumanTeam;
        /// <summary>Full Court on a landscape screen.</summary>
        private bool _landscape;

        /// <summary>A stick / key direction on screen (+y up) as a fixed-court direction.</summary>
        private static Vec2 ScreenToCourt(Vec2 screen) => CourtSpace.ScreenToCourt(screen);
        private bool Demo => _request != null && _request.Mode == GameMode.Demo;

        // Phase 15: arcade feel and secrets.
        private float _hitStopUntil;
        private float _nextFlameAt;
        private bool _rainbowBall;
        private bool _skyHigh;
        private float _demoStartedAt;
        // Phase 18: coach tips.
        private float _lastTipAt = -99f;
        private float _nextTipCheck;
        private bool _tooEarly, _tooLate;
        /// <summary>A controller is being used: hide the touch buttons and show controller hints.</summary>
        private bool _padMode;
        /// <summary>The keyboard was used last (counts as pad mode for hiding the touch controls).</summary>
        private bool _keyMode;

        // Phase 12: accessibility, leaps, crowd.
        private bool _reduceMotion;
        private bool _tapToShoot;
        private TapShoot _tap;
        private bool _shootTapped;
        /// <summary>15 Oct 2026: THE STREETS (meters, combo, SHOWSTOPPER button); null in other modes.</summary>
        private StreetsHud _streetsHud;
        private bool _breakerTapped;
        private int _bestBank;
        private int _leaper = -1;
        private float _leapStart;
        private float _leapDuration;
        // Dunks: where the dunker took off, when the ball meets the rim (0..1 of the leap), and the lift that puts their hands on it.
        private Vec2 _leapFrom;
        private float _slamU;
        /// <summary>Share of the dunk leap spent hanging (and swinging) on the rim after a made dunk (0 = a short hang).</summary>
        private float _hangU;
        private int _liftAtRimPx;
        private Transform _hoopT, _hoop2T;
        private SpriteRenderer _hoopSr, _hoop2Sr;
        /// <summary>When the last basket was made (net swish animation) and whether it went through the second hoop.</summary>
        private float _netAt = -10f;
        private bool _netSecond;
        private Vector3 _hoopBase, _hoop2Base;
        private ShotType _leapType;
        /// <summary>How the current dunk looks (Dunks.cs).</summary>
        private DunkStyle _dunkStyle;
        private struct Fan
        {
            public SpriteRenderer Renderer;
            public Sprite Idle, Cheer;
            public Vector3 Base;
        }
        private Fan[] _fans = new Fan[0];

        // Phase 14: instant replay.
        private ReplayRecorder _recorder;
        private ReplayClip _lastHighlight;
        private ReplayClip _replay;
        /// <summary>Phase 40: clips still to play in the HIGHLIGHTS reel.</summary>
        private readonly System.Collections.Generic.List<ReplayClip> _replayQueue = new System.Collections.Generic.List<ReplayClip>();
        private float _replayT;
        private readonly BallState _replayBall = new BallState();
        private const float ReplaySpeed = 0.5f;
        private bool Replaying => _replay != null;
        private CrowdMood _crowdMood = CrowdMood.Idle;
        private float _crowdMoodUntil;
        private SpriteRenderer[] _cones;
        private SpriteRenderer _targetArrow;
        private PlayCall _pendingCall;
        private bool _resultApplied;
        private float _nextInfoAt;

        // Phase 9 presentation (never read by the simulation).
        private PixelBursts _bursts;
        private int _celebrator = -1;
        private float _celebrateStart;
        private CelebrationKind _celebration;
        private CelebrationKind _myCelebration;
        private DribbleMoveKind _myMove;
        private float _moveStart = -10f;
        private float _moveReadyAt;
        /// <summary>14 Oct 2026: the skill move the controlled player just did (drives the dribble pose).</summary>
        private SkillMoveKind _skillPose;
        private float _skillPoseStart = -10f;
        private Vec2 _lastMoveDir;
        /// <summary>The rim the offence attacks, as drawn (Full Court: whichever end that is right now).</summary>
        private Vector3 HoopWorld => CourtSpace.ToWorldSnapped(_match.Setup.Court.Hoop, CourtSpace.RimHeight);
        private bool FullCourtGame => _match != null && _match.Setup.FullCourt;
        /// <summary>What each slot wears (kept so a substitute can be drawn in the same kit).</summary>
        private KitLook[] _dress;

        private readonly System.Collections.Generic.Dictionary<string, Sprite[,]> _sheets = new System.Collections.Generic.Dictionary<string, Sprite[,]>();

        /// <summary>Full Court substitution: draw the new player in the slot's kit.</summary>
        private void RedrawPlayer(int index)
        {
            if (index < 0 || index >= _playerViews.Length) return;
            var p = _match.Players[index];
            // Phase 36: one sheet per player per slot, reused when he comes back (subs and theater jumps used to build a new one every time).
            string key = index + ":" + p.Def.id;
            if (!_sheets.TryGetValue(key, out var frames))
            {
                frames = _art.PlayerFrames(p.Def, _dress[index]);
                _sheets[key] = frames;
            }
            _playerViews[index].SetFrames(frames);
            if (App.Career != null && Secrets.IsOn(App.Career.secrets, Secrets.BigHeads))
            {
                if (!_headSheets.TryGetValue(key, out var heads))
                {
                    heads = _art.HeadFrames(frames, p.Def.appearance);
                    _headSheets[key] = heads;
                }
                _playerViews[index].EnableBigHead(heads);
            }
        }

        private readonly System.Collections.Generic.Dictionary<string, Sprite[,]> _headSheets = new System.Collections.Generic.Dictionary<string, Sprite[,]>();

        /// <summary>Shown once at tip-off when your Away kit was picked automatically.</summary>
        private string _kitNote;

        public MatchSimulation Match => _match;
        public bool IsPaused => _paused;

        private void Start()
        {
            App.EnsureInitialized();
            var catalog = App.Catalog;
            _request = App.PrepareRequest(App.PendingMatch ?? MatchRequest.QuickCallDefault(catalog));
            App.PendingMatch = null;
            if (_request.Seed == 0) _request.Seed = (uint)System.Environment.TickCount | 1u;
            if (_request.ContextId == "link" && LinkMatch.Watching != null && LinkMatch.Transport != null)
            {
                // Watching (a third phone): the host's game, step for step; nothing here affects it.
                _watch = LinkMatch.Watching;
                _wire = LinkMatch.Transport;
                _opponent = "THE PLAYERS";
                _lastLinkStep = Time.unscaledTime;
            }
            else if (_request.ContextId == "link" && LinkMatch.Active)
            {
                _link = new Lockstep(LinkMatch.Seat, LinkMatch.Setup.Delay);
                _wire = LinkMatch.Transport;
                _liveSetup = LinkMatch.Setup.IsLive ? LinkMatch.Setup : null;
                if (_liveSetup != null && BackendClient.Ready && !string.IsNullOrEmpty(LinkMatch.LocalId) && !string.IsNullOrEmpty(LinkMatch.OpponentId))
                {
                    string host = LinkMatch.Seat == 0 ? LinkMatch.LocalId : LinkMatch.OpponentId;
                    string guest = LinkMatch.Seat == 0 ? LinkMatch.OpponentId : LinkMatch.LocalId;
                    string key = Backend.MatchKey(host, guest, _liveSetup.Seed);
                    BackendClient.StartMatch(key, LinkMatch.OpponentId, LinkMatch.Seat, (rated, why) =>
                    {
                        if (rated) _serverMatchKey = key;
                        else if (_hud != null) _hud.Toast("UNRATED GAME: " + (why ?? "").ToUpperInvariant(), 2.5f);
                    });
                }
                _opponent = _liveSetup == null ? "YOUR FRIEND" : (LinkMatch.Seat == 0 ? _liveSetup.GuestName : _liveSetup.HostName).ToUpperInvariant();
                if (string.IsNullOrEmpty(_opponent)) _opponent = "YOUR OPPONENT";
                if (_liveSetup == null && LinkMatch.Setup.IsCup)
                {
                    _cupSetup = LinkMatch.Setup;
                    string them = LinkMatch.Seat == 0 ? _cupSetup.AwayLabel : _cupSetup.HomeLabel;
                    if (!string.IsNullOrEmpty(them)) _opponent = them.ToUpperInvariant();
                }
                // The host of a game nearby shares it with anyone who wants to watch.
                _nearby = LinkMatch.Transport as NearbyLink;
                // Every two-phone and Live game keeps both inputs (for SAVE GAME TAPE); the host of a game nearby also shares it with watchers.
                _feed = new SpectatorFeed(LinkMatch.Setup);
                _broadcast = _liveSetup == null && LinkMatch.Seat == 0 && _nearby != null;
                _lastLinkStep = Time.unscaledTime;
            }

            // Both phones in a two-phone game step at 60 Hz, whatever their screens do.
            int rate = _link != null || _watch != null ? 60 : App.SimulationRate;
            FixedStep = 1f / rate;
            MaxStepsPerFrame = rate / 12;
            var setup = MatchSetup.FromRequest(_request, catalog);
            _match = new MatchSimulation(setup);
            CourtSpace.Flip = false;
            CourtSpace.FlipLength = setup.Court.depth;
            // Full Court 5-on-5 plays in landscape (baskets left and right).
            _landscape = (setup.FullCourt || (App.Career != null && App.Career.settings.landscapeAll)) && !Demo && !Versus;
            CourtSpace.Landscape = _landscape;
            CourtSpace.LandscapeLength = setup.Court.depth;
            if (_landscape) Orientation.Landscape();
            if (_request.Mode == GameMode.Practice && _request.Drill == (int)DrillKind.Horse)
            {
                bool friend = _request.ContextId == "horse:friend";
                string oppTeam = _request.AwayTeamId ?? catalog.TeamsInTier(TeamTier.League)[0].id;
                _horse = new HorseSession(friend ? HorseOpponent.Friend : HorseOpponent.Cpu, setup.Court, setup.Shot, setup.Rules,
                                          friend ? null : Shootout.PickShooter(catalog, oppTeam), catalog.Difficulty(_request.DifficultyId), _request.Seed);
            }
            else if (_request.Mode == GameMode.Practice && _request.Drill >= 0)
            {
                _practice = new PracticeSession((DrillKind)_request.Drill, _match, _request.Seed);
                if (_practice.Kind == DrillKind.Shootout && IsThreeContest)
                {
                    // All-Star 3-Point Contest: the "CPU" score is the one you need to beat this round.
                    var contest = App.Career.allStar.contest;
                    var rival = contest.round >= 2 ? AllStar.Finalists(contest).Find(e => !e.you) : null;
                    _practice.SetCpu(Mathf.Max(0, AllStar.Target(contest) - 1), rival != null ? rival.name.ToUpperInvariant() : Loc.T("FINAL SPOT"));
                }
                else if (_practice.Kind == DrillKind.Shootout && _request.ContextId == ShootoutDuel.ContextId)
                {
                    // Pass and play: P1 sets the score, P2 tries to beat it (kept across the two rounds).
                    _duel = App.PendingDuel != null && !App.PendingDuel.Finished ? App.PendingDuel : new ShootoutDuel();
                    App.PendingDuel = _duel;
                    _practice.SetCpu(_duel.Round == 1 ? _duel.Points[0] : 0, "P1");
                }
                else if (_practice.Kind == DrillKind.Shootout)
                {
                    // The CPU shoots first (simulated); you get the score to beat.
                    var cpu = Shootout.PickShooter(catalog, _request.AwayTeamId ?? catalog.TeamsInTier(TeamTier.League)[0].id);
                    int cpuScore = Shootout.SimulateCpu(cpu, catalog.Difficulty(_request.DifficultyId), setup.Shot, _request.Seed,
                                                        setup.Court.arcRadius + 0.7f);
                    _practice.SetCpu(cpuScore, cpu != null ? cpu.lastName.ToUpperInvariant() : "CPU");
                }
            }
            if (_request.Mode == GameMode.Tutorial) _tutorial = new TutorialSession();
            if (_request.Mode == GameMode.Daily && _request.ContextId != null && _request.ContextId.StartsWith("daily:", System.StringComparison.Ordinal)
                && int.TryParse(_request.ContextId.Substring(6), out int day))
                _daily = DailyChallenges.For(day, catalog);

            var court = catalog.Court(_request.CourtId) ?? catalog.Court(setup.TeamA.homeCourtId);
            BuildWorld(court, setup);

            var settings = App.Career?.settings;
            _reduceMotion = settings != null && settings.reduceMotion;
            _tapToShoot = settings != null && settings.tapToShoot;
            // 2 Player with no controllers on a touch screen: tabletop mode, each player gets half the screen.
            bool tabletop = Versus && _link == null && TabletopWanted();
            _controls = TouchControls.Create(_buffer, settings != null && settings.leftHanded, settings != null && settings.largeButtons,
                                             tabletop ? TouchControls.Seat.Bottom : TouchControls.Seat.Full, settings?.controlLayout);
            if (tabletop)
            {
                _controls2 = TouchControls.Create(_buffer2, false, settings != null && settings.largeButtons, TouchControls.Seat.Top);
                _controls2.Call.SetLabel("P&R");
            }
            _controls.Shoot.Pressed += () => _shootTapped = true;
            MatchHud.ReduceMotion = _reduceMotion;
            Audio.AudioManager.PlayMusic(1);
            _controls.Dunk.UnavailableHint = "NO BALL";
            _controls.Layup.UnavailableHint = "NO BALL";
            _controls.Call.UnavailableHint = "OFFENSE";
            _hud = MatchHud.Create(setup.TeamA, setup.TeamB);
            _hud.PlayChosen += play => _pendingCall = play;
            if (_match.StyleOn)
            {
                _controls.SetStreet();
                _controls.Call.UnavailableHint = "NO BALL";
                // The guest phone in TWO PHONES / LIVE plays team B.
                int streetsTeam = _link != null && _link.Seat == 1 ? 1 : setup.HumanTeam;
                _streetsHud = StreetsHud.Create(setup.TeamA, setup.TeamB, streetsTeam, settings != null && settings.leftHanded);
                if (_controls2 != null) _controls2.SetStreet();
                _streetsHud.BreakerTapped += () => _breakerTapped = true;
            }
            // Tip-off card with tonight's court and both starting lineups (real games only; quick and skippable).
            if (!Demo && _practice == null && _tutorial == null && _horse == null && _request.Mode != GameMode.Practice && _request.Mode != GameMode.Tutorial)
            {
                var startersA = new System.Collections.Generic.List<string>();
                var startersB = new System.Collections.Generic.List<string>();
                foreach (var pl in _match.Players)
                {
                    if (IsBenchedSlot(pl)) continue;
                    string n = string.IsNullOrEmpty(pl.Def.lastName) ? pl.Def.firstName : pl.Def.lastName;
                    (pl.Team == 0 ? startersA : startersB).Add("#" + pl.Def.jerseyNumber + " " + (n ?? "").ToUpperInvariant());
                }
                _hud.ShowIntro(court != null ? court.displayName : "THE COURT", setup.TeamA, setup.TeamB, startersA, startersB);
            }
            // Mic Tally on the call (Settings ► COMMENTARY), except in practice and the tutorial.
            if ((settings == null || settings.commentary) && _request.Mode != GameMode.Practice && _request.Mode != GameMode.Tutorial)
                _mic = new Commentary(setup.TeamA.nickname, setup.TeamB.nickname, _request.Seed);
                _mic.StartFrom(setup.StartScoreA, setup.StartScoreB);
            _recorder = new ReplayRecorder(_match.Players.Length, ReplayRecorder.DefaultSeconds, rate);
            _rainbowBall = App.Career != null && Secrets.IsOn(App.Career.secrets, Secrets.RainbowBall);
            _skyHigh = App.Career != null && Secrets.IsOn(App.Career.secrets, Secrets.SkyHigh);
            if (Demo)
            {
                SetTouchVisible(false);
                _demoStartedAt = Time.unscaledTime;
            }
            _hud.ReplayRequested += () => StartReplay(_lastHighlight);
            _hud.PlayOfTheGameRequested += () => StartReplay(_recorder.BestPlay);
            // Phase 40: the HIGHLIGHTS reel (best plays in order) and the shareable result card.
            _hud.HighlightsRequested += () =>
            {
                _replayQueue.Clear();
                _replayQueue.AddRange(_recorder.Highlights);
                if (_replayQueue.Count == 0) return;
                var first = _replayQueue[0];
                _replayQueue.RemoveAt(0);
                StartReplay(first);
            };
            _hud.ShareResultRequested += ShareResultCard;
            _hud.ShareRequested += ShareHighlight;
            if (settings != null && settings.leftHanded) _hud.SetCallMenuLeft(true);
            _hud.ContinueRequested += Continue;
            _hud.PauseRequested += () => SetPaused(true);
            _hud.ResumeRequested += () => SetPaused(false);
            _hud.QuitRequested += Quit;
            StartCity();
            _hud.PhotoRequested += () =>
            {
                if (_link != null) { _hud.Toast("NOT IN A TWO-PHONE GAME", 1.2f); return; }
                OpenPhotoMode();
            };
            _hud.RematchRequested += Rematch;
            if (_link != null || (_watch != null && !_watch.IsTape)) _hud.SaveTapeRequested += SaveTape;
            // 14 Oct 2026: the phone from the pause menu, and quick chat with the other player in LIVE / two-phone games.
            _hud.PhoneRequested += () => PhoneScreen.Open();
            if (_link != null && (App.Career == null || App.Career.phone.quickChat)) _hud.EnableQuickChat(SendChat);

            _myCelebration = Flair.CelebrationFor(App.Career?.equippedCelebration);
            _myMove = Flair.DribbleMoveFor(App.Career?.equippedMove);
            Audio.AudioManager.SetAmbience(true);

            StartTheater();
            StartCoach();
            SyncViews(0f, snapCamera: true);
            if (!Demo) Sfx(SfxId.TipOff, 0.7f);
            if (!(Demo && StoreShots.Active)) _hud.Toast(_tutorial != null ? "HOW TO PLAY"
                     : _daily != null ? "DAILY: " + _daily.Describe().ToUpperInvariant()
                     : Versus ? "PLAYER 1  VS  PLAYER 2"
                     : FullCourtGame ? "FULL COURT  5 ON 5"
                     : Demo ? "DEMO PLAY"
                     : _horse != null ? "H-O-R-S-E: " + _horse.Name(0) + " SHOOTS FIRST"
                     : _duel != null ? _duel.Intro
                     : _practice != null && _practice.Kind == DrillKind.Shootout ? "BEAT " + _practice.CpuName + ": " + _practice.CpuScore
                     : _practice != null ? DrillTitle(_practice.Kind)
                     : (_request.Mode == GameMode.Practice ? "PRACTICE LAB" : "CHECK BALL"), 1.8f);
        }

        private void BuildWorld(CourtDef court, MatchSetup setup)
        {
            var career = App.Career;
            var banner = Cosmetic(career?.equippedBanner);
            // Full Court draws two halves back to back; everything else is one half court.
            var artGeometry = setup.FullCourt ? FullCourt.HalfGeometry() : setup.Court;
            _art = MatchArt.Build(court, artGeometry, StableHash.Of(court.id), banner?.colorA, banner?.colorB, staticCrowd: false,
                                  fullCourt: setup.FullCourt);
            var world = new GameObject("World").transform;

            var courtGo = new GameObject("Court");
            courtGo.transform.SetParent(world, false);
            var courtSr = courtGo.AddComponent<SpriteRenderer>();
            courtSr.sprite = _art.Court;
            courtSr.sortingOrder = -10000;
            if (_landscape)
            {
                // The full-court art turned a quarter turn: top basket on the left, bottom basket on the right.
                courtGo.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                courtGo.transform.localPosition = new Vector3(-setup.Court.depth * 0.5f, 0f, 0f);
            }

            var hoopGo = new GameObject("Hoop");
            hoopGo.transform.SetParent(world, false);
            hoopGo.transform.position = CourtSpace.ToWorldSnapped(setup.Court.Hoop, CourtSpace.RimHeight);
            var hoopSr = hoopGo.AddComponent<SpriteRenderer>();
            hoopSr.sprite = _landscape ? _art.HoopSide : _art.Hoop;
            hoopSr.sortingOrder = CourtSpace.SortingOrder(setup.Court.Hoop, -1);
            _hoopT = hoopGo.transform;
            _hoopSr = hoopSr;
            _hoopBase = _hoopT.position;
            if (setup.FullCourt)
            {
                // The other basket at the bottom of the court, backboard toward the bottom baseline.
                var bottomHoop = new Vec2(0f, setup.Court.depth - setup.Court.hoopY);
                var hoop2 = new GameObject("Hoop (bottom)");
                hoop2.transform.SetParent(world, false);
                hoop2.transform.position = CourtSpace.ToWorldSnapped(bottomHoop, CourtSpace.RimHeight);
                var hoop2Sr = hoop2.AddComponent<SpriteRenderer>();
                hoop2Sr.sprite = _landscape ? _art.HoopSide : _art.Hoop;
                if (_landscape) hoop2Sr.flipX = true; // backboard on the right
                else hoop2Sr.flipY = true;
                hoop2Sr.sortingOrder = CourtSpace.SortingOrder(bottomHoop, 1);
                _hoop2T = hoop2.transform;
                _hoop2Sr = hoop2Sr;
                _hoop2Base = _hoop2T.position;
            }

            _playerViews = new PlayerView[_match.Players.Length];
            _dress = new KitLook[_match.Players.Length];
            var ringColor = new Color(1f, 0.82f, 0.4f, 1f);
            bool contrast = career != null && career.settings.colorblindContrast;
            var filter = career != null ? ColorAccess.Normalize(career.settings.colorFilter) : ColorFilter.Off;
            ShotMeterView.UsePalette(ColorAccess.Meter(filter));
            // Away team switches to its alternate kit when the two jerseys would look alike (for this filter).
            var homeJersey = setup.TeamA.primary;
            if (setup.HumanTeam == 0 && setup.TeamA.id == DefaultContent.PlayerCrewId && !setup.TeamA.customKit && Cosmetic(career?.equippedJersey) != null)
                homeJersey = Cosmetic(career?.equippedJersey).colorA;
            // Kit Studio: your team wears your kit (or your Away kit when it would clash with the opponent).
            KitData myKit = null;
            int kitTeam = CustomTeams.IsYours(setup.TeamA.id) ? 0 : (CustomTeams.IsYours(setup.TeamB.id) ? 1 : -1);
            if (career != null && kitTeam >= 0 && !Demo)
            {
                var other = kitTeam == 0 ? setup.TeamB : setup.TeamA;
                myKit = Kits.ForMatch(career.kits, other.primary, filter, out bool toAway);
                if (myKit != null && toAway && !Demo) _kitNote = "AWAY KIT ON: COLORS CLASHED";
                // THE STREETS phase 3: your STREET GEAR (unless it clashes with the other crew).
                var gear = _request.Mode == GameMode.Streets ? StreetsGear.Kit(StreetsGear.Worn(career.streets)) : null;
                if (gear != null)
                {
                    if (!ColorAccess.KitsClash(Kits.Unpack(gear.jersey), other.primary, filter)) { myKit = gear; _kitNote = null; }
                    else _kitNote = "STREET GEAR CLASHED: USUAL KIT ON";
                }
                if (myKit != null && kitTeam == 0) homeJersey = Kits.Unpack(myKit.jersey);
            }
            var awayJersey = setup.TeamB.primary;
            var awayTrim = setup.TeamB.secondary;
            bool awayAlternate = ColorAccess.ResolveAwayKit(homeJersey, ref awayJersey, ref awayTrim, filter);
            var jersey = Cosmetic(career?.equippedJersey);
            var shoes = Cosmetic(career?.equippedShoes);
            for (int i = 0; i < _match.Players.Length; i++)
            {
                var p = _match.Players[i];
                var team = p.Team == 0 ? setup.TeamA : setup.TeamB;
                var primary = team.primary;
                var trim = team.secondary;
                if (p.Team == 1 && awayAlternate)
                {
                    primary = awayJersey;
                    trim = awayTrim;
                }
                // Jersey palettes are for your own crew only.
                if (p.Team == setup.HumanTeam && team.id == DefaultContent.PlayerCrewId && jersey != null && !team.customKit)
                {
                    primary = jersey.colorA;
                    trim = jersey.colorB;
                }
                RgbColor? shoeColor = i == _match.ControlledIndex && shoes != null ? shoes.colorA : (RgbColor?)null;
                var pattern = contrast ? PatternFor(p.Team, setup) : TeamPattern.Solid;
                RgbColor? shorts = null;
                if (team.customKit)
                {
                    // Your created team: its own shorts, shoes, and jersey pattern.
                    shorts = team.shorts;
                    if (shoeColor == null) shoeColor = team.shoes;
                    if (!contrast) pattern = team.pattern;
                }
                // Player 2's ring is cyan so both people can find themselves.
                var ring = i == _match.SecondControlledIndex ? new Color32(0x4C, 0xC9, 0xF0, 255) : (Color32)ringColor;
                KitLook dress;
                if (myKit != null && p.Team == kitTeam)
                {
                    dress = Kits.Look(myKit);
                    if (contrast) dress.Pattern = PatternFor(p.Team, setup); // Team Patterns still wins for readability
                }
                else dress = KitLook.Classic(primary, trim, team.accent, shoeColor, pattern, shorts);
                _dress[i] = dress;
                var frames = _art.PlayerFrames(p.Def, dress);
                _playerViews[i] = PlayerView.Create(world, p, frames, _art, ring);
                if (career != null && Secrets.IsOn(career.secrets, Secrets.BigHeads))
                    _playerViews[i].EnableBigHead(_art.HeadFrames(frames, p.Def.appearance));
            }
            _ballView = BallView.Create(world, _art);
            _meter = ShotMeterView.Create(world, _art);
            _bursts = PixelBursts.Create(world, _art);
            // THE STREETS: rings of energy off the rim on dunks.
            if (_match.StyleOn) _rimPulse = RimPulse.Create(world);
            // The stands' fans are drawn for the portrait view; landscape leaves the stands empty.
            if (!_landscape) BuildCrowd(world, court, setup, artGeometry, banner != null);
            else BuildSidelineCrowd(world, court, setup);
            if (!_landscape && !setup.FullCourt) BuildNearSide(world, court, setup, artGeometry);

            var arrowGo = new GameObject("ReceiverArrow");
            arrowGo.transform.SetParent(world, false);
            _receiverArrow = arrowGo.AddComponent<SpriteRenderer>();
            _receiverArrow.sprite = _art.Arrow;
            _receiverArrow.color = new Color32(0x4C, 0xC9, 0xF0, 255);
            _receiverArrow.sortingOrder = 31500;
            _receiverArrow.enabled = false;

            if (_practice != null) BuildPracticeMarkers(world);
            if (_horse != null)
            {
                var go = new GameObject("HorseSpot");
                go.transform.SetParent(world, false);
                _horseMarker = go.AddComponent<SpriteRenderer>();
                _horseMarker.sprite = _art.Ring;
                _horseMarker.sortingOrder = -9000;
                _horseMarker.enabled = false;
            }

            var cam = Camera.main;
            if (cam == null) cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            _cameraRig = cam.GetComponent<CourtCameraRig>();
            if (_cameraRig == null) _cameraRig = cam.gameObject.AddComponent<CourtCameraRig>();
            // Portrait half court: the near-side art fades into this colour, so there's no seam below it.
            var bg = !_landscape && !setup.FullCourt ? CourtsideGenerator.DeepColor(court) : court.floor.Darken(0.35f);
            _cameraRig.Init(setup.Court, new Color32(bg.r, bg.g, bg.b, 255), setup.FullCourt, _landscape);
        }

        private void Update()
        {
            if (_match == null || _leaving) return;
            PowerMonitor.Gameplay = Replaying || (!_paused && !_finalShown && _photo == null);
            UI.ControllerCursor.Suppressed = !_paused && !_finalShown && !Replaying;
            // Phase 35: the coach's sideline and the tape controls step aside for the end screen, pause, replays and photos.
            bool sideline = !_paused && !_finalShown && !Replaying && _photo == null;
            _coach?.SetVisible(sideline);
            _theaterBar?.SetVisible(sideline);
            if (Replaying)
            {
                StepReplay();
                return;
            }
            if (Demo)
            {
                // Attract mode: any touch, key, or button goes back to the title; so does the final buzzer.
                if ((AnyInputPressed() && Time.unscaledTime - _demoStartedAt > 0.5f) || Time.unscaledTime - _demoStartedAt > DemoSeconds)
                {
                    Quit();
                    return;
                }
            }
            else if (_photo != null && _link == null) return;
            else if (EscapePressed() && !_match.IsOver) SetPaused(!_paused);
            if (_watch != null)
            {
                WatchFrame();
                return;
            }
            if (_link != null)
            {
                // Two phones: the game keeps going while the pause menu is open (the other phone is still playing).
                LinkFrame();
                return;
            }
            if (_paused) return;
            if (Time.unscaledTime < _hitStopUntil)
            {
                // Hit-stop: hold the frame for a beat on the biggest plays.
                SyncViews(0f, snapCamera: false);
                return;
            }

            UpdatePadMode();
            if (CoachHolding())
            {
                SyncViews(0f, snapCamera: false);
                return;
            }
            var input = Coaching ? default : ReadInput();
            var input2 = Versus ? ReadInput2() : default;
            _accumulator += Mathf.Min(Time.unscaledDeltaTime, FixedStep * MaxStepsPerFrame);
            int steps = 0;
            while (_accumulator >= FixedStep && steps < MaxStepsPerFrame)
            {
                StepMarker.Begin();
                _match.Step(FixedStep, input, input2);
                StepMarker.End();
                CourtSpace.Flip = _match.Flipped;
                _recorder.Capture(_match);
                _practice?.Update(_match, FixedStep);
                _horse?.Update(_match);
                if (_tutorial != null)
                {
                    _tutorial.Update(_match);
                    if (_tutorial.JustAdvanced && !_tutorial.Finished)
                    {
                        _hud.Toast("NICE!  NEXT: " + Loc.T(_tutorial.Title), 1.4f);
                        Sfx(SfxId.Click, 0.8f, 1.4f);
                        Haptics.Light();
                        _nextInfoAt = 0f;
                    }
                }
                HandleEvents();
                if (Time.unscaledTime < _hitStopUntil)
                {
                    _accumulator = 0f;
                    break;
                }
                input2.ShootPressed = false;
                input2.PassPressed = false;
                input2.DefensePressed = false;
                input2.CallPlay = PlayCall.None;
                // Edge-triggered presses apply to one step only.
                input.ShootPressed = false;
                input.PassPressed = false;
                input.DefensePressed = false;
                input.CallPlay = PlayCall.None;
                _accumulator -= FixedStep;
                steps++;
            }
            ViewsMarker.Begin();
            SyncViews(steps * FixedStep, snapCamera: false);
            ViewsMarker.End();
            SyncCoach();
            if (_practice == null && _tutorial == null && _horse == null) UpdateCrowdChant();

            if (_tutorial != null && _tutorial.Finished && !_finalShown) ShowTutorialEnd();
            else if (_practice != null && _practice.Finished && !_finalShown) ShowPracticeEnd();
            else if (_horse != null && _horse.Finished && !_finalShown) ShowHorseEnd();
            else if (_match.IsOver && !_finalShown)
            {
                if (Demo) Quit();
                else ShowFinal();
            }
        }

        private const float DemoSeconds = 75f;

        /// <summary>2 Player on a touch screen with no controllers connected: split the screen between the two players.</summary>
        private static bool TabletopWanted()
        {
#if ENABLE_INPUT_SYSTEM
            if (Gamepad.all.Count > 0) return false;
            return Application.isMobilePlatform || UnityEngine.InputSystem.Touchscreen.current != null;
#else
            return Application.isMobilePlatform;
#endif
        }

        private void SetTouchVisible(bool visible)
        {
            if (_watch != null || Coaching) visible = false; // watching or coaching: nothing to steer
            _controls.SetVisible(visible);
            if (_controls2 != null) _controls2.SetVisible(visible);
            _touchShown = visible;
        }

        private bool _touchShown = true;

        private void UpdatePadMode()
        {
#if ENABLE_INPUT_SYSTEM
            bool was = _padMode;
            var pad = Gamepad.current;
            if (pad != null && (pad.leftStick.ReadValue().sqrMagnitude > 0.1f || pad.buttonSouth.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame))
                _padMode = true;
            // A keyboard (Mac, or an iPad keyboard) hides the touch controls too, until the next touch.
            var keys = Keyboard.current;
            if (keys != null && (keys.wKey.wasPressedThisFrame || keys.aKey.wasPressedThisFrame || keys.sKey.wasPressedThisFrame || keys.dKey.wasPressedThisFrame
                                 || keys.jKey.wasPressedThisFrame || keys.kKey.wasPressedThisFrame || keys.lKey.wasPressedThisFrame
                                 || keys.leftArrowKey.wasPressedThisFrame || keys.rightArrowKey.wasPressedThisFrame))
            {
                _padMode = true;
                _keyMode = true;
            }
            var touch = Pointer.current;
            if (touch != null && touch.press.wasPressedThisFrame && Application.isMobilePlatform) _padMode = _keyMode = false;
            if (Gamepad.all.Count == 0 && !_keyMode) _padMode = false;
            if (was != _padMode && !_paused && !_finalShown && !Demo) SetTouchVisible(!_padMode);
#endif
        }

        /// <summary>Any touch, key, or controller button this frame (ends the attract-mode demo).</summary>
        private static bool AnyInputPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var pointer = Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame) return true;
            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame) return true;
            var pad = Gamepad.current;
            if (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame || pad.startButton.wasPressedThisFrame)) return true;
#endif
            return false;
        }

        private PlayerInput ReadInput()
        {
            var move = ScreenToCourt(_controls.ScreenStick);
            bool shootHeld = _controls.Shoot.IsHeld;
#if ENABLE_INPUT_SYSTEM
            // Keyboard / gamepad fallback for the Editor and controllers.
            var kb = Keyboard.current;
            if (kb != null)
            {
                // In 2-player the arrow keys belong to player 2.
                bool arrows = !Versus;
                float x = (kb.dKey.isPressed || arrows && kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || arrows && kb.leftArrowKey.isPressed ? 1f : 0f);
                float y = (kb.wKey.isPressed || arrows && kb.upArrowKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed || arrows && kb.downArrowKey.isPressed ? 1f : 0f);
                if (x != 0f || y != 0f) move = ScreenToCourt(Vec2.ClampMagnitude(new Vec2(x, y), 1f));
                if (kb.uKey.wasPressedThisFrame) _buffer.Press(ActionButton.Dunk, Time.unscaledTime);
                if (kb.iKey.wasPressedThisFrame) _buffer.Press(ActionButton.Layup, Time.unscaledTime);
                if (kb.jKey.wasPressedThisFrame) _buffer.Press(ActionButton.Pass, Time.unscaledTime);
                if (kb.kKey.wasPressedThisFrame) _buffer.Press(ActionButton.Shoot, Time.unscaledTime);
                if (kb.lKey.wasPressedThisFrame) _buffer.Press(ActionButton.Defense, Time.unscaledTime);
                if (kb.cKey.wasPressedThisFrame) _buffer.Press(ActionButton.Call, Time.unscaledTime);
                if (kb.vKey.wasPressedThisFrame) _breakerTapped = true; // THE STREETS: SHOWSTOPPER
                shootHeld |= kb.kKey.isPressed;
                if (kb.kKey.wasPressedThisFrame) _shootTapped = true;
#if UNITY_EDITOR || DEBUG
                if (kb.bKey.wasPressedThisFrame) _match.KnockLoose(new Vec2(Random.Range(-3f, 3f), Random.Range(1f, 3f)));
#endif
            }
            // 2-player: with two controllers player 1 takes the first; with one, it goes to player 2.
            var pad = Versus ? (Gamepad.all.Count >= 2 ? Gamepad.all[0] : null) : Gamepad.current;
            if (pad != null)
            {
                var stick = pad.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.02f) move = ScreenToCourt(new Vec2(stick.x, stick.y));
                if (pad.rightShoulder.wasPressedThisFrame) _buffer.Press(ActionButton.Dunk, Time.unscaledTime);
                if (pad.leftShoulder.wasPressedThisFrame) _buffer.Press(ActionButton.Layup, Time.unscaledTime);
                if (pad.buttonSouth.wasPressedThisFrame) _buffer.Press(ActionButton.Shoot, Time.unscaledTime);
                if (pad.buttonWest.wasPressedThisFrame) _buffer.Press(ActionButton.Pass, Time.unscaledTime);
                if (pad.buttonEast.wasPressedThisFrame) _buffer.Press(ActionButton.Defense, Time.unscaledTime);
                if (pad.buttonNorth.wasPressedThisFrame) _buffer.Press(ActionButton.Call, Time.unscaledTime);
                if (pad.leftTrigger.wasPressedThisFrame) _breakerTapped = true; // THE STREETS: SHOWSTOPPER
                shootHeld |= pad.buttonSouth.isPressed;
                if (pad.buttonSouth.wasPressedThisFrame) _shootTapped = true;
            }
#endif
            if (_tapToShoot)
            {
                // Accessibility: tap to start the meter, tap again to release.
                bool mine = _match.ChargingIndex == _match.ControlledIndex;
                if (_match.OffenseTeam == _match.Setup.HumanTeam && (_match.HumanHasBall || mine))
                    shootHeld = _tap.Update(_shootTapped, mine);
                else
                {
                    _tap = default;
                    shootHeld = false;
                }
            }
            _shootTapped = false;
            float now = Time.unscaledTime;
            var input = new PlayerInput { Move = move, ShootHeld = shootHeld };

            // Buffered presses are consumed only when they can act, so a tap a moment early still counts.
            bool live = _match.Phase == MatchPhase.Live || _match.Phase == MatchPhase.CheckBall;
            bool offense = _match.OffenseTeam == _match.Setup.HumanTeam;
            if (offense)
            {
                if (live && _match.HumanHasBall && !_match.MustClear && _match.ChargingIndex < 0 && _buffer.Consume(ActionButton.Shoot, now))
                    input.ShootPressed = true;
                if (live && (_match.HumanHasBall || _match.HumanTeamHasBall) && _match.ChargingIndex < 0 && _buffer.Consume(ActionButton.Pass, now))
                    input.PassPressed = true;
                bool canFinish = _match.Phase == MatchPhase.Live && _match.HumanHasBall && !_match.MustClear && !_match.MustInbound && _match.ChargingIndex < 0;
                if (canFinish && _buffer.Consume(ActionButton.Dunk, now)) input.DunkPressed = true;
                if (canFinish && _buffer.Consume(ActionButton.Layup, now)) input.LayupPressed = true;
                if (!_match.HumanHasBall) { _buffer.Consume(ActionButton.Dunk, now); _buffer.Consume(ActionButton.Layup, now); }
                _buffer.Consume(ActionButton.Defense, now);
                if (_match.StyleOn)
                {
                    // THE STREETS: CALL is TRICK (the stick picks which one).
                    if (_buffer.Consume(ActionButton.Call, now) && _match.Phase == MatchPhase.Live && _match.HumanHasBall) input.TrickPressed = true;
                }
                else if (_buffer.Consume(ActionButton.Call, now) && CanCall()) _hud.ShowCallMenu(!_hud.CallMenuOpen);
                if (_pendingCall != PlayCall.None)
                {
                    input.CallPlay = _pendingCall;
                    _pendingCall = PlayCall.None;
                }
            }
            else
            {
                bool liveOnly = _match.Phase == MatchPhase.Live;
                if (_hud.CallMenuOpen) _hud.ShowCallMenu(false);
                _pendingCall = PlayCall.None;
                if (liveOnly && _buffer.Consume(ActionButton.Defense, now)) input.DefensePressed = true;
                if (liveOnly && _buffer.Consume(ActionButton.Shoot, now)) input.ShootPressed = true;
                if (liveOnly && _buffer.Consume(ActionButton.Pass, now)) input.PassPressed = true;
                // U / right shoulder steal on defence too.
                if (liveOnly && _buffer.Consume(ActionButton.Dunk, now)) input.DefensePressed = true;
                _buffer.Consume(ActionButton.Layup, now);
                _buffer.Consume(ActionButton.Call, now);
            }
            if (_breakerTapped)
            {
                _breakerTapped = false;
                if (_match.StyleOn) input.BreakerPressed = true;
            }
            return input;
        }

        /// <summary>
        /// Player 2 in local 2-player: arrow keys + numpad (1 shoot, 2 pass, 3 steal, 0 pick-and-roll),
        /// or the second controller (the only one if just one is connected). Touch controls stay with player 1.
        /// </summary>
        private PlayerInput ReadInput2()
        {
            var move = Vec2.Zero;
            bool shootHeld = false;
            float now = Time.unscaledTime;
            bool callPressed = false;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                float x = (kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.leftArrowKey.isPressed ? 1f : 0f);
                float y = (kb.downArrowKey.isPressed ? 1f : 0f) - (kb.upArrowKey.isPressed ? 1f : 0f);
                if (x != 0f || y != 0f) move = Vec2.ClampMagnitude(new Vec2(x, y), 1f);
                if (kb.numpad1Key.wasPressedThisFrame || kb.periodKey.wasPressedThisFrame) _buffer2.Press(ActionButton.Shoot, now);
                if (kb.numpad2Key.wasPressedThisFrame || kb.commaKey.wasPressedThisFrame) _buffer2.Press(ActionButton.Pass, now);
                if (kb.numpad3Key.wasPressedThisFrame || kb.slashKey.wasPressedThisFrame) _buffer2.Press(ActionButton.Defense, now);
                if (kb.numpad0Key.wasPressedThisFrame || kb.mKey.wasPressedThisFrame) callPressed = true;
                shootHeld |= kb.numpad1Key.isPressed || kb.periodKey.isPressed;
            }
            var pad = Gamepad.all.Count >= 2 ? Gamepad.all[1] : (Gamepad.all.Count == 1 ? Gamepad.all[0] : null);
            if (pad != null)
            {
                var stick = pad.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.02f) move = new Vec2(stick.x, -stick.y);
                if (pad.buttonSouth.wasPressedThisFrame) _buffer2.Press(ActionButton.Shoot, now);
                if (pad.buttonWest.wasPressedThisFrame) _buffer2.Press(ActionButton.Pass, now);
                if (pad.buttonEast.wasPressedThisFrame) _buffer2.Press(ActionButton.Defense, now);
                if (pad.buttonNorth.wasPressedThisFrame) callPressed = true;
                shootHeld |= pad.buttonSouth.isPressed;
            }
#endif
            if (_controls2 != null)
            {
                // Tabletop: player 2's half of the screen.
                var touchMove = ScreenToCourt(_controls2.ScreenStick);
                if (touchMove.x != 0f || touchMove.y != 0f) move = touchMove;
                shootHeld |= _controls2.Shoot.IsHeld;
                if (_buffer2.Consume(ActionButton.Call, now)) callPressed = true;
            }
            int me = _match.SecondControlledIndex;
            var input = new PlayerInput { Move = move, ShootHeld = shootHeld };
            if (me < 0) return input;
            int team = _match.Players[me].Team;
            bool live = _match.Phase == MatchPhase.Live || _match.Phase == MatchPhase.CheckBall;
            bool hasBall = _match.Ball.IsHeld && _match.Ball.HolderIndex == me;
            bool teamHasBall = _match.Ball.IsHeld && _match.Players[_match.Ball.HolderIndex].Team == team;
            if (_match.OffenseTeam == team)
            {
                if (live && hasBall && !_match.MustClear && _match.ChargingIndex < 0 && _buffer2.Consume(ActionButton.Shoot, now)) input.ShootPressed = true;
                if (live && teamHasBall && _match.ChargingIndex < 0 && _buffer2.Consume(ActionButton.Pass, now)) input.PassPressed = true;
                bool finish = _match.Phase == MatchPhase.Live && hasBall && !_match.MustClear && !_match.MustInbound && _match.ChargingIndex < 0;
                if (finish && _buffer2.Consume(ActionButton.Dunk, now)) input.DunkPressed = true;
                if (finish && _buffer2.Consume(ActionButton.Layup, now)) input.LayupPressed = true;
                _buffer2.Consume(ActionButton.Defense, now);
                if (_match.StyleOn)
                {
                    // Player 2 has no SHOWSTOPPER button: TRICK calls it when their meter is full.
                    if (callPressed && _match.Phase == MatchPhase.Live)
                    {
                        if (_match.CanCallBreaker(team)) input.BreakerPressed = true;
                        else if (hasBall) input.TrickPressed = true;
                    }
                }
                else if (callPressed && teamHasBall && _match.Phase == MatchPhase.Live && _match.ActivePlay == PlayCall.None) input.CallPlay = PlayCall.PickAndRoll;
            }
            else if (_match.Phase == MatchPhase.Live)
            {
                if (_buffer2.Consume(ActionButton.Defense, now)) input.DefensePressed = true;
                if (_buffer2.Consume(ActionButton.Shoot, now)) input.ShootPressed = true;
                if (_buffer2.Consume(ActionButton.Pass, now)) input.PassPressed = true;
                _buffer2.Consume(ActionButton.Layup, now);
            }
            return input;
        }

        // ------------------------------------------------------------------ two-phone play

        /// <summary>
        /// One frame of a two-phone game: schedule this phone's input at 60 Hz, swap messages, and
        /// simulate every step both inputs have arrived for. A stall (the other phone hasn't sent yet)
        /// just holds the picture; a dropped connection, a player leaving, or a checksum mismatch ends the game.
        /// </summary>
        private void LinkFrame()
        {
            if (_rematching) return; // the next game's messages are for the next scene
            UpdatePadMode();
            if (!_match.IsOver && !_linkEnded)
            {
                var local = _paused ? default : (_link.Seat == 0 ? ReadInput() : ReadGuestInput());
                // Presses wait here until a step is scheduled, so none are lost on frames without one.
                _linkCarry.Move = local.Move;
                _linkCarry.ShootHeld = local.ShootHeld;
                _linkCarry.ShootPressed |= local.ShootPressed;
                _linkCarry.PassPressed |= local.PassPressed;
                _linkCarry.DefensePressed |= local.DefensePressed;
                _linkCarry.DunkPressed |= local.DunkPressed;
                _linkCarry.LayupPressed |= local.LayupPressed;
                _linkCarry.TrickPressed |= local.TrickPressed;
                _linkCarry.BreakerPressed |= local.BreakerPressed;
                if (local.CallPlay != PlayCall.None) _linkCarry.CallPlay = local.CallPlay;

                _accumulator += Mathf.Min(Time.unscaledDeltaTime, FixedStep * MaxStepsPerFrame);
                while (_accumulator >= FixedStep && _link.CanQueueLocal)
                {
                    _link.QueueLocal(_linkCarry);
                    _linkCarry.ShootPressed = _linkCarry.PassPressed = _linkCarry.DefensePressed = _linkCarry.DunkPressed = _linkCarry.LayupPressed = false;
                    _linkCarry.TrickPressed = _linkCarry.BreakerPressed = false;
                    _linkCarry.CallPlay = PlayCall.None;
                    _accumulator -= FixedStep;
                }
                // While waiting on the other phone, don't bank time (that would make a burst later).
                if (_accumulator > FixedStep * MaxStepsPerFrame) _accumulator = FixedStep * MaxStepsPerFrame;
            }

            foreach (var m in _link.TakeOutgoing()) _wire.Send(m);
            // After the buzzer, stop at the first hand-shake message: the rematch lobby reads from there on,
            // so the next game's first inputs are never swallowed by this one.
            while ((!_match.IsOver || _link.Control.Count == 0) && _wire.TryReceive(out var msg)) _link.Receive(msg);
            ShowIncomingChat();

            int steps = 0;
            while (steps < MaxStepsPerFrame * 2 && !_match.IsOver && !_linkEnded && _link.TryStep(out var a, out var b))
            {
                int tick = _link.NextTick - 1;
                StepMarker.Begin();
                _match.Step(FixedStep, a, b);
                StepMarker.End();
                _feed?.Record(a, b);
                _link.AfterStep(tick, SimHash.Of(_match));
                CourtSpace.Flip = _match.Flipped;
                _recorder.Capture(_match);
                HandleEvents();
                steps++;
            }
            _hitStopUntil = 0f; // no hit-stop: both phones keep the same pace
            if (steps > 0)
            {
                _lastLinkStep = Time.unscaledTime;
                foreach (var m in _link.TakeOutgoing()) _wire.Send(m); // checksums due after those steps
            }

            FeedWatchers();

            ViewsMarker.Begin();
            SyncViews(steps * FixedStep, snapCamera: false);
            ViewsMarker.End();

            UpdateLinkRematch();
            if (_linkEnded) return;
            string problem = _link.Desynced ? "OUT OF SYNC"
                           // (inputs already received are played out first, so a friend leaving right at the buzzer still finishes the game)
                           : _link.RemoteLeft && _link.WaitingForRemote ? _opponent + " LEFT"
                           : (_wire.State == LinkState.Lost || _wire.State == LinkState.Idle) && _link.WaitingForRemote ? "CONNECTION LOST" : null;
            if (problem != null && !_match.IsOver)
            {
                EndLink(problem);
                return;
            }
            if (!_match.IsOver && Time.unscaledTime - _lastLinkStep > 0.6f && Time.unscaledTime >= _nextWaitToast)
            {
                _nextWaitToast = Time.unscaledTime + 1f;
                _hud.Toast("WAITING FOR " + _opponent + "…", 1.1f);
            }
            if (_match.IsOver && !_finalShown) ShowFinal();
        }

        /// <summary>Host: sends the game so far to the phones watching (and says who's watching).</summary>
        private void FeedWatchers()
        {
            if (!_broadcast || _feed == null || _nearby == null) return;
            int n = _nearby.WatcherCount;
            if (n > _watchersSeen) _hud.Toast(n == 1 ? "1 FRIEND IS WATCHING" : n + " FRIENDS ARE WATCHING", 2f);
            _watchersSeen = n;
            foreach (var m in _feed.Take(n)) _nearby.SendWatchers(m);
        }

        // ------------------------------------------------------------------ watching on a third phone

        private bool _watchEndShown;

        /// <summary>
        /// One frame of watching: play the host's steps at real speed (fast-forward after joining late),
        /// and follow the players into their next game (a rematch or the next Couch Cup game).
        /// </summary>
        private void WatchFrame()
        {
            if (_rematching) return;
            UpdatePadMode();
            while (_wire.TryReceive(out var msg)) _watch.Receive(msg);
            if (Theater && !_paused && TheaterSeek()) return;
            if (_watch.IsTape && !Theater && !_paused && !_match.IsOver && AnyInputPressed())
            {
                _tapeSpeed = _tapeSpeed >= 4 ? 1 : _tapeSpeed * 2;
                _hud.Toast("TAPE  " + _tapeSpeed + "x", 1f);
            }
            int steps = 0;
            bool catchingUp = !_watch.IsTape && _watch.Behind > 90;
            if (!_match.IsOver)
            {
                _accumulator += Mathf.Min(Time.unscaledDeltaTime, FixedStep * MaxStepsPerFrame);
                int due = Mathf.FloorToInt(_accumulator / FixedStep);
                int n = Theater ? (_paused || _theaterBar.Dragging || _theaterBar.ListOpen ? 0 : Mathf.Min(_watch.Behind, _theater.StepsFor(Time.unscaledDeltaTime, FixedStep, MaxStepsPerFrame * 4)))
                      : _watch.IsTape ? Mathf.Min(_watch.Behind, due * _tapeSpeed) : Spectator.StepsThisFrame(_watch.Behind, due);
                while (steps < n && !_match.IsOver && _watch.TryStep(out var a, out var b))
                {
                    StepMarker.Begin();
                    _match.Step(FixedStep, a, b);
                    StepMarker.End();
                    CourtSpace.Flip = _match.Flipped;
                    _recorder.Capture(_match);
                    HandleEvents();
                    steps++;
                }
                _accumulator = Mathf.Clamp(_accumulator - steps * FixedStep, 0f, FixedStep * MaxStepsPerFrame);
            }
            _hitStopUntil = 0f;
            if (steps > 0) _lastLinkStep = Time.unscaledTime;

            ViewsMarker.Begin();
            SyncViews(steps * FixedStep, snapCamera: catchingUp);
            ViewsMarker.End();
            SyncTheaterBar();

            // The players started another game: watch that one too.
            var next = _watch.Next;
            if (next != null && next.Ready && (_match.IsOver || _watch.Behind == 0))
            {
                if (next.Setup.Register(App.Catalog))
                {
                    _rematching = true;
                    LinkMatch.Setup = next.Setup;
                    LinkMatch.Watching = next;
                    App.PendingMatch = next.Setup.ToRequest();
                    SceneFlow.GoTo(SceneNames.Game);
                    return;
                }
            }

            bool gone = _watch.HostLeft || _wire.State == LinkState.Lost || _wire.State == LinkState.Idle;
            if (!_match.IsOver && !_linkEnded && gone && _watch.Behind == 0)
            {
                _linkEnded = true;
                _finalShown = true;
                _hud.ShowPause(false);
                HangUp();
                _hud.ShowFinal(_watch.IsTape ? "END OF TAPE" : "GAME STOPPED",
                               _watch.IsTape ? "The game stopped here at " + _match.Score[0] + " - " + _match.Score[1] + "."
                                             : "The players' phones disconnected at " + _match.Score[0] + " - " + _match.Score[1] + ".");
                return;
            }
            if (_match.IsOver && !_finalShown)
            {
                _finalShown = true;
                var setup = _match.Setup;
                string home = _watch.Setup.IsCup && !string.IsNullOrEmpty(_watch.Setup.HomeLabel) ? _watch.Setup.HomeLabel : setup.TeamA.FullName;
                string away = _watch.Setup.IsCup && !string.IsNullOrEmpty(_watch.Setup.AwayLabel) ? _watch.Setup.AwayLabel : setup.TeamB.FullName;
                string title = _match.Winner < 0 ? "TIE" : ((_match.Winner == 0 ? home : away).ToUpperInvariant() + " WIN" + (_watch.Setup.IsCup ? "S" : ""));
                // The full post-game: box scores, PLAY OF THE GAME and SHARE HIGHLIGHT (plus SAVE GAME TAPE when watching live).
                _hud.HasPlayOfTheGame = _recorder.BestPlay != null;
                _hud.HighlightCount = _recorder.Highlights.Count;
                _hud.ShowPostGame(title, MatchSummary.From(_match, _request.Mode, "versus"), default, false,
                                  _watch.IsTape ? "End of tape. REMATCH watches it again." : "Stay here: if they play again, you'll watch that game too.",
                                  null, _watch.IsTape);
                Haptics.Success();
            }
            if (_match.IsOver && gone && !_watchEndShown && _watch.Next == null && !_watch.IsTape)
            {
                _watchEndShown = true;
                _hud.Toast("THE PLAYERS HAVE LEFT", 2.5f);
            }
            if (!_match.IsOver && !gone && Time.unscaledTime - _lastLinkStep > 1.2f && Time.unscaledTime >= _nextWaitToast)
            {
                _nextWaitToast = Time.unscaledTime + 1.5f;
                _hud.Toast("WAITING FOR THE PLAYERS…", 1.4f);
            }
        }

        // ------------------------------------------------------------------ two-phone rematch

        private LinkLobby _rematch;
        private ILinkTransport _rematchWire;
        private bool _rematching;

        /// <summary>REMATCH on two phones: the host offers the same game with a new seed; it starts once both have tapped.</summary>
        private void StartLinkRematch()
        {
            if (_rematch != null) return;
            var c = App.Catalog;
            bool host = _link.Seat == 0;
            uint seed = (uint)System.Environment.TickCount | 1u;
            LinkSetup again = null;
            if (host && _cupSetup != null)
            {
                // Couch Cup: the next game in the bracket (or this one again after a tie).
                var cup = App.Career?.couch;
                var g = cup != null ? CouchCup.NextGame(cup) : null;
                if (g == null)
                {
                    _hud.Toast("THE COUCH CUP IS OVER: TAP HOME", 2.5f);
                    return;
                }
                again = CouchCup.LinkSetupFor(cup, c, g, App.Career.settings.difficultyId, seed, App.Version, LinkMatch.Setup.HostName);
            }
            else if (host)
            {
                again = LinkMatch.Setup.Again(seed);
                if (_liveSetup != null && App.Career?.live != null)
                {
                    // Phase 33: a Live rematch starts from the ratings after this game.
                    again.HostRating = Mathf.Max(1, App.Career.live.rating);
                    again.GuestRating = Mathf.Max(1, LiveMode.OpponentAfter(_liveSetup.GuestRating, _liveDelta));
                }
            }
            _rematch = new LinkLobby(host, again, App.Version, LinkProtocol.ContentFingerprint(c), c);
            _rematchWire = new ReplayWire(_wire, _link.Control);
            _hud.Toast(_cupSetup != null ? "WAITING FOR THE OTHER PHONE TO TAP REMATCH…" : "WAITING FOR " + _opponent + " TO TAP REMATCH…", 3f);
        }

        private void UpdateLinkRematch()
        {
            if (_rematch == null || _rematching) return;
            if (_link.RemoteLeft && _rematch.Status == LobbyStatus.Waiting)
            {
                _hud.Toast(_opponent + " LEFT", 2f);
                _rematch = null;
                return;
            }
            _rematch.Update(_rematchWire);
            switch (_rematch.Status)
            {
                case LobbyStatus.Started:
                    var setup = _rematch.Setup;
                    if (!setup.Register(App.Catalog)) { _hud.Toast("COULDN'T START THE REMATCH", 2f); _rematch = null; return; }
                    _rematching = true;
                    LinkMatch.Setup = setup;
                    App.PendingMatch = setup.ToRequest();
                    SceneFlow.GoTo(SceneNames.Game);
                    break;
                case LobbyStatus.Rejected:
                case LobbyStatus.Lost:
                    _hud.Toast(_rematch.Error ?? "NO REMATCH", 2f);
                    _rematch = null;
                    break;
            }
        }

        private void EndLink(string why)
        {
            _linkEnded = true;
            _finalShown = true;
            SetTouchVisible(false);
            _hud.ShowPause(false);
            HangUp();
            string detail = why == "OUT OF SYNC"
                ? "The two games stopped matching (step " + _link.DesyncTick + "). Both phones need the same version. No result counted."
                : "Score " + _match.Score[0] + " - " + _match.Score[1] + ". No result counted.";
            // Live: an opponent who quits after the opening seconds forfeits; a dropped connection or desync doesn't count.
            if (_liveSetup != null && why.EndsWith(" LEFT") && _match.Time >= LiveMode.CountsAfterSeconds)
                detail = "Score " + _match.Score[0] + " - " + _match.Score[1] + ". They forfeit: " + RateLive(true, Backend.Outcome.OpponentLeft);
            _hud.ShowFinal(why, detail);
            Haptics.Heavy();
        }

        /// <summary>Tells the other phone we're leaving (once) and closes the connection.</summary>
        private void HangUp()
        {
            if (_wire == null) return;
            // Leaving a Live game early (after the opening seconds) counts as a loss.
            if (_liveSetup != null && !_linkEnded && !_match.IsOver && _match.Time >= LiveMode.CountsAfterSeconds) RateLive(false, Backend.Outcome.Quit);
            if (_broadcast && _feed != null && _nearby != null)
            {
                // Tell the watchers the game is over for them too.
                _feed.End();
                foreach (var m in _feed.Take(_nearby.WatcherCount)) _nearby.SendWatchers(m);
                _broadcast = false;
            }
            if (_wire.State == LinkState.Connected) _wire.Send(LinkProtocol.Simple(LinkMessage.Bye));
            _wire = new ClosedWire();
            NearbyLink.Stop();
            LiveLink.Stop();
            LinkMatch.Clear();
        }

        /// <summary>Stands in for the connection after hanging up.</summary>
        private sealed class ClosedWire : ILinkTransport
        {
            public LinkState State => LinkState.Idle;
            public string PeerName => "";
            public void Send(byte[] message) { }
            public bool TryReceive(out byte[] message) { message = null; return false; }
        }

        /// <summary>The guest phone's controls (team B, player 2): full-screen touch, keyboard, or a controller.</summary>
        private PlayerInput ReadGuestInput()
        {
            var move = ScreenToCourt(_controls.ScreenStick);
            bool shootHeld = _controls.Shoot.IsHeld;
            float now = Time.unscaledTime;
            bool callPressed = false;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
                float y = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
                if (x != 0f || y != 0f) move = ScreenToCourt(Vec2.ClampMagnitude(new Vec2(x, y), 1f));
                if (kb.uKey.wasPressedThisFrame) _buffer.Press(ActionButton.Dunk, now);
                if (kb.iKey.wasPressedThisFrame) _buffer.Press(ActionButton.Layup, now);
                if (kb.jKey.wasPressedThisFrame) _buffer.Press(ActionButton.Pass, now);
                if (kb.kKey.wasPressedThisFrame) _buffer.Press(ActionButton.Shoot, now);
                if (kb.lKey.wasPressedThisFrame) _buffer.Press(ActionButton.Defense, now);
                if (kb.cKey.wasPressedThisFrame) callPressed = true;
                shootHeld |= kb.kKey.isPressed;
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                var stick = pad.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.02f) move = ScreenToCourt(new Vec2(stick.x, stick.y));
                if (pad.rightShoulder.wasPressedThisFrame) _buffer.Press(ActionButton.Dunk, now);
                if (pad.leftShoulder.wasPressedThisFrame) _buffer.Press(ActionButton.Layup, now);
                if (pad.buttonSouth.wasPressedThisFrame) _buffer.Press(ActionButton.Shoot, now);
                if (pad.buttonWest.wasPressedThisFrame) _buffer.Press(ActionButton.Pass, now);
                if (pad.buttonEast.wasPressedThisFrame) _buffer.Press(ActionButton.Defense, now);
                if (pad.buttonNorth.wasPressedThisFrame) callPressed = true;
                shootHeld |= pad.buttonSouth.isPressed;
            }
#endif
            if (_buffer.Consume(ActionButton.Call, now)) callPressed = true;
            int me = _match.SecondControlledIndex;
            var input = new PlayerInput { Move = move, ShootHeld = shootHeld };
            if (me < 0) return input;
            int team = _match.Players[me].Team;
            bool live = _match.Phase == MatchPhase.Live || _match.Phase == MatchPhase.CheckBall;
            bool hasBall = _match.Ball.IsHeld && _match.Ball.HolderIndex == me;
            bool teamHasBall = _match.Ball.IsHeld && _match.Players[_match.Ball.HolderIndex].Team == team;
            if (_match.OffenseTeam == team)
            {
                if (live && hasBall && !_match.MustClear && _match.ChargingIndex < 0 && _buffer.Consume(ActionButton.Shoot, now)) input.ShootPressed = true;
                if (live && teamHasBall && _match.ChargingIndex < 0 && _buffer.Consume(ActionButton.Pass, now)) input.PassPressed = true;
                bool finish = _match.Phase == MatchPhase.Live && hasBall && !_match.MustClear && !_match.MustInbound && _match.ChargingIndex < 0;
                if (finish && _buffer.Consume(ActionButton.Dunk, now)) input.DunkPressed = true;
                if (finish && _buffer.Consume(ActionButton.Layup, now)) input.LayupPressed = true;
                _buffer.Consume(ActionButton.Defense, now);
                if (_match.StyleOn) { if (callPressed && hasBall && _match.Phase == MatchPhase.Live) input.TrickPressed = true; }
                else if (callPressed && teamHasBall && _match.Phase == MatchPhase.Live && _match.ActivePlay == PlayCall.None) input.CallPlay = PlayCall.PickAndRoll;
            }
            else if (_match.Phase == MatchPhase.Live)
            {
                if (_buffer.Consume(ActionButton.Defense, now)) input.DefensePressed = true;
                if (_buffer.Consume(ActionButton.Shoot, now)) input.ShootPressed = true;
                if (_buffer.Consume(ActionButton.Pass, now)) input.PassPressed = true;
                if (_buffer.Consume(ActionButton.Dunk, now)) input.DefensePressed = true;
                _buffer.Consume(ActionButton.Layup, now);
            }
            if (_breakerTapped)
            {
                _breakerTapped = false;
                if (_match.StyleOn) input.BreakerPressed = true;
            }
            return input;
        }

        /// <summary>Button labels on the guest phone (it plays team B).</summary>
        private void UpdateGuestLabels()
        {
            int me = _match.SecondControlledIndex;
            bool offense = me >= 0 && _match.OffenseTeam == _match.Players[me].Team;
            bool hasBall = me >= 0 && _match.Ball.IsHeld && _match.Ball.HolderIndex == me;
            int state = 128 | (offense ? 1 : 0) | (hasBall ? 2 : 0) | (_match.MustClear ? 4 : 0);
            if (state == _lastLabelState) return;
            _lastLabelState = state;
            _controls.SetRole(!offense, false);
            if (offense) _controls.SetLabels(_match.MustClear && hasBall ? "CLEAR" : "SHOOT", hasBall ? "PASS" : "ASK", "DUNK");
            else _controls.SetLabels("BLOCK", "SWITCH", "STEAL");
            bool canShoot = !offense || (hasBall && !_match.MustClear);
            _controls.SetAvailability(canShoot, true, canShoot, canShoot, offense);
            _controls.Call.SetLabel(_match.StyleOn ? "TRICK" : "P&R");
        }

        private Commentary _mic;

        private bool IsBenchedSlot(PlayerRuntimeState p) => _match.IsBenched(p.Index);
        private float _nextHornAt, _nextClapAt;

        /// <summary>Tight finish (last 20 seconds, within one score): the crowd claps along every few seconds.</summary>
        private void UpdateCrowdChant()
        {
            if (_match == null || _match.IsOver || _match.Phase != MatchPhase.Live || !_match.Setup.Rules.useGameClock) return;
            if (_match.GameClock > 20f || System.Math.Abs(_match.Score[0] - _match.Score[1]) > 3) return;
            if (Time.unscaledTime < _nextClapAt) return;
            _nextClapAt = Time.unscaledTime + 4.5f;
            Sfx(SfxId.ClapChant, 0.5f);
        }

        // ------------------------------------------------------------------ THE STREETS: SHOWSTOPPER cinema

        private float _cineUntil;
        private int _cinePlayer = -1;

        /// <summary>
        /// A SHOWSTOPPER dunk: the camera comes in close on the dunker and the rim, letterbox bars slide in, and the
        /// dunk's name goes up. (Reduce Motion, landscape and two-phone games just get the name.)
        /// </summary>
        private void StartCinema(int player)
        {
            if (_streetsHud != null) _streetsHud.Banner(Dunks.Name(_dunkStyle), Theme.Gold, 1.2f);
            if (_reduceMotion || _landscape || _link != null || Theater) return;
            _cinePlayer = player;
            _cineUntil = Time.unscaledTime + _leapDuration + 0.9f;
            _cameraRig.SetCloseUp(true);
            if (_streetsHud != null) _streetsHud.Cinema(true);
        }

        /// <summary>Where the SHOWSTOPPER camera looks (between the dunker and the rim), or null when it's off.</summary>
        private Vec2? CinemaFollow()
        {
            if (_cinePlayer < 0) return null;
            if (Time.unscaledTime < _cineUntil && !_paused && _match.Phase != MatchPhase.Final)
                return Vec2.Lerp(_match.Players[_cinePlayer].Position, _match.Setup.Court.Hoop, 0.5f);
            EndCinema();
            return null;
        }

        private void EndCinema()
        {
            if (_cinePlayer < 0) return;
            _cinePlayer = -1;
            _cineUntil = 0f;
            _cameraRig.SetCloseUp(false);
            if (_streetsHud != null) _streetsHud.Cinema(false);
        }

        private void HandleEvents()
        {
            if (_mic != null && _match.Events.Count > 0)
            {
                string line = _mic.React(_match, i => { var d = _match.Players[i].Def; return string.IsNullOrEmpty(d.lastName) ? d.firstName : d.lastName; }, _match.Ball.ShotType);
                if (line != null)
                {
                    _hud.Commentary(Commentary.Speaker, line);
                    // THE STREETS: the booth says it out loud.
                    if (_match.StyleOn && !Demo && _watch == null && !Theater) Speech.Say(line, _mic.LastPriority >= 4);
                }
            }
            int human = _match.Setup.HumanTeam;
            float stop = 0f;
            int streetsTeam = _link != null && _link.Seat == 1 ? 1 : human; // THE STREETS: the guest phone is team B
            foreach (var e in _match.Events)
            {
                NoteHighlight(e);
                if (e.Type == MatchEventType.ShotMade || e.Type == MatchEventType.ShotMissed) NoteScouting();
                stop = HitStop.Combine(stop, HitStop.For(e, _match.Ball.ShotType, _reduceMotion));
                switch (e.Type)
                {
                    case MatchEventType.HeatUp:
                        if (!_reduceMotion) _bursts.Spawn(CourtSpace.ToWorldSnapped(_match.Players[e.PlayerIndex].Position, 1.2f), new Color32(0xFF, 0x7E, 0x1F, 255), 24, 4f, 0.6f);
                        Sfx(SfxId.HeatUp, 0.9f);
                        if (e.Team == human || Versus) Haptics.Heavy();
                        break;
                    case MatchEventType.Bust:
                        _hud.Toast("BUST! BACK TO " + e.Value, 1.4f);
                        Sfx(SfxId.Error, 0.7f);
                        break;
                    case MatchEventType.SchemeChanged:
                        // The AI adjusts its defence: tell the person it's now facing something new.
                        if (e.Team != human || Versus) _hud.Toast(Loc.T("DEFENSE:") + " " + Loc.T(MatchSimulation.SchemeName((DefenseScheme)e.Value)), 1.4f);
                        break;
                    case MatchEventType.HeatEnded:
                        if (_match.IsHumanControlled(e.PlayerIndex)) _hud.Toast("COOLED OFF", 0.9f);
                        break;
                    case MatchEventType.AlleyOop:
                        Callout("ALLEY-OOP!", SfxId.Stinger);
                        if (!_reduceMotion) _bursts.Spawn(HoopWorld, new Color32(0x4C, 0xC9, 0xF0, 255), 20, 4.5f, 0.6f);
                        _cameraRig.Shake(0.2f);
                        if (e.Team == human || Versus) Haptics.Heavy();
                        break;
                    case MatchEventType.Trap:
                        if (_match.IsHumanControlled(e.Value)) _hud.Toast("DOUBLE TEAM! FIND THE OPEN MAN", 1.1f);
                        break;
                    case MatchEventType.SkillMove:
                        OnSkillMove(e, human);
                        break;
                    // 15 Oct 2026: THE STREETS.
                    case MatchEventType.StreetTrick:
                        OnStreetTrick(e, streetsTeam);
                        break;
                    case MatchEventType.StyleBanked:
                        if (e.Team == streetsTeam || Versus) _hud.Toast(Loc.T("STYLE BANKED") + "  +" + e.Value, 1.1f);
                        if (e.Team == streetsTeam) _bestBank = Mathf.Max(_bestBank, e.Value);
                        break;
                    case MatchEventType.ComboLost:
                        if (e.Team == streetsTeam && e.Value >= 150) _hud.Toast(Loc.T("COMBO LOST") + "  -" + e.Value, 1f);
                        break;
                    case MatchEventType.BreakerReady:
                        if (_streetsHud != null)
                        {
                            bool mine = e.Team == streetsTeam;
                            _streetsHud.Banner(e.Value >= 2 ? (mine ? "SHOWSTOPPER II READY" : "THEY'RE AT LEVEL II") : (mine ? "SHOWSTOPPER READY" : "THEY CAN BREAK IT"),
                                               mine ? Theme.Gold : Theme.Pink, 1.3f);
                            Sfx(mine ? SfxId.OnFire : SfxId.CrowdOooh, 0.8f);
                            if (mine) Haptics.Heavy();
                        }
                        break;
                    case MatchEventType.BreakerOn:
                        if (_streetsHud != null) _streetsHud.Banner(e.Value >= 2 ? "SHOWSTOPPER II!" : "SHOWSTOPPER!", e.Team == streetsTeam ? Theme.Gold : Theme.Pink, 1.4f);
                        Sfx(SfxId.Stinger, 1f);
                        Audio.AudioManager.Voice("SHOWSTOPPER!");
                        SetCrowd(CrowdMood.Cheer, 1.4f);
                        if (e.Team == streetsTeam) Haptics.Heavy();
                        break;
                    case MatchEventType.BreakerScored:
                        if (_streetsHud != null)
                            _streetsHud.Banner(Loc.T("SHOWSTOPPER") + "\n+" + e.Value + " / -" + e.Value, e.Team == streetsTeam ? Theme.Gold : Theme.Pink, 2f);
                        if (!_reduceMotion)
                        {
                            _bursts.Spawn(HoopWorld, new Color32(0xFF, 0xD1, 0x66, 255), 40, 6f, 0.9f);
                            _bursts.Spawn(HoopWorld, new Color32(0xC7, 0x4D, 0xFF, 255), 30, 5f, 0.8f);
                        }
                        _cameraRig.Shake(0.35f);
                        PulseRim(e.Team, true);
                        Sfx(SfxId.Fanfare, 1f);
                        SetCrowd(e.Team == streetsTeam || Versus ? CrowdMood.Cheer : CrowdMood.Groan, 2.2f);
                        if (e.Team == streetsTeam) Haptics.Success();
                        break;
                    case MatchEventType.BreakerEnded:
                        if (e.Team == streetsTeam) _hud.Toast("SHOWSTOPPER WASTED", 1.2f);
                        else _hud.Toast("STOPPED THE SHOWSTOPPER", 1.2f);
                        Sfx(e.Team == streetsTeam ? SfxId.Error : SfxId.CrowdCheer, 0.7f);
                        break;
                    case MatchEventType.DefenderBeaten:
                        if (e.Team == human || Versus)
                        {
                            Sfx(SfxId.CrowdOooh, 0.6f);
                            if (!_reduceMotion && e.Value >= 0)
                                _bursts.Spawn(CourtSpace.ToWorldSnapped(_match.Players[e.Value].Position, 0.5f), Color.white, 6, 2.2f, 0.35f);
                            if (_match.IsHumanControlled(e.PlayerIndex)) Haptics.Light();
                        }
                        else if (_match.IsHumanControlled(e.Value)) _hud.Toast("HE GOT YOU", 0.8f);
                        break;
                    case MatchEventType.BankKiss:
                        Sfx(SfxId.Backboard, 0.8f);
                        break;
                    case MatchEventType.EuroStep:
                        if (_match.IsHumanControlled(e.PlayerIndex)) _hud.Toast("EURO STEP", 0.7f);
                        break;
                    case MatchEventType.PumpFake:
                        // Ball up for a beat: the arms-up frame flashes, then back to holding it.
                        _lastShooter = e.PlayerIndex;
                        _shootPoseUntil = Time.unscaledTime + 0.15f;
                        if (_match.IsHumanControlled(e.PlayerIndex))
                        {
                            if (e.Value >= 0)
                            {
                                _hud.Toast("PUMP FAKE! HE BIT", 0.9f);
                                Sfx(SfxId.Squeak, 0.7f);
                                Haptics.Light();
                            }
                            else _hud.Toast("PUMP FAKE", 0.6f);
                        }
                        else if (e.Value >= 0 && _match.IsHumanControlled(e.Value)) _hud.Toast("YOU BIT ON THE FAKE", 0.9f);
                        break;
                    case MatchEventType.DunkToLayup:
                        if (_match.IsHumanControlled(e.PlayerIndex)) _hud.Toast("NO DUNK FROM THERE: LAYUP", 0.9f);
                        break;
                    case MatchEventType.PassThrown:
                        if (_match.AlleyOopInFlight) Sfx(SfxId.Lob, 0.7f);
                        break;
                    case MatchEventType.ShotReleased:
                        _lastShooter = e.PlayerIndex;
                        _shootPoseUntil = Time.unscaledTime + 0.35f;
                        if (_match.Ball.Phase == BallPhase.Shot && (_match.Ball.ShotType == ShotType.Dunk || _match.Ball.ShotType == ShotType.Layup))
                        {
                            _leaper = e.PlayerIndex;
                            _leapStart = Time.unscaledTime;
                            _leapType = _match.Ball.ShotType;
                            _leapDuration = Mathf.Max(0.3f, _match.Ball.FlightDuration + 0.15f);
                            if (_leapType == ShotType.Dunk)
                            {
                                // Your dunk package for you; the AI throws down what its finishing allows.
                                _dunkStyle = Mine(e.PlayerIndex) && !Demo
                                    ? Dunks.ForCosmetic(App.Career?.equippedDunk)
                                    : Dunks.ForAi(_match.Players[e.PlayerIndex].Def.attributes.finishing, Random.value);
                                // THE STREETS phase 3: a SHOWSTOPPER dunk is your SIGNATURE (the AI throws its best), and the camera comes in.
                                bool showstopper = _match.StyleOn && _match.BreakerTeam >= 0 && _match.BreakerTeam == _match.Players[e.PlayerIndex].Team;
                                if (showstopper)
                                {
                                    var sig = Mine(e.PlayerIndex) && !Demo ? StreetsSignature.Chosen(App.Career?.streets) : null;
                                    if (sig.HasValue) _dunkStyle = sig.Value;
                                    else if (!Mine(e.PlayerIndex)) _dunkStyle = Dunks.ForAi(_match.Players[e.PlayerIndex].Def.attributes.finishing, 0.999f);
                                }
                                // Rise to the rim with the ball (it meets the rim when the simulation's ball does), hang, drop.
                                float flight = Mathf.Max(0.2f, _match.Ball.FlightDuration);
                                // A made dunk hangs and swings on the rim as long as the simulation holds the dunker there.
                                float hang = _match.Ball.ShotWillScore ? _match.Setup.Flow.rimHangSeconds : 0f;
                                _leapDuration = flight + 0.45f + (Dunks.LiftScale(_dunkStyle) - 1f) * 0.4f + hang;
                                if (showstopper) StartCinema(e.PlayerIndex);
                                _slamU = flight / _leapDuration;
                                _hangU = hang / _leapDuration;
                                _leapFrom = _match.Players[e.PlayerIndex].Position;
                                var hoop = _match.Setup.Court.Hoop;
                                var meet = DunkPath.MeetPoint(_leapFrom, hoop, _landscape ? DunkPath.ApproachSide : DunkPath.Approach);
                                float rimY = CourtSpace.ToWorld(hoop, CourtSpace.RimHeight).y * CourtSpace.PixelsPerUnit;
                                float floorY = CourtSpace.ToWorld(meet).y * CourtSpace.PixelsPerUnit;
                                _liftAtRimPx = DunkPath.LiftToRim(rimY, floorY, CharacterSpriteGenerator.HandReachPx(_match.Players[e.PlayerIndex].Def.appearance));
                            }
                        }
                        if (Mine(e.PlayerIndex))
                        {
                            _tooEarly = e.Value == (int)ShotFeedback.TooEarly;
                            _tooLate = e.Value == (int)ShotFeedback.TooLate;
                            if (e.Value == (int)ShotFeedback.Green) Haptics.Light();
                            _meter.ShowRelease(_match.LastReleaseMeter, _match.Ball.ShotGrade);
                            // Phase 34: the look, the timing and the make chance at release.
                            _hud.Toast(ShotModel.FeedbackLine((ShotFeedback)e.Value, _match.Ball.ShotGrade, e.Chance), 1.1f);
                        }
                        break;
                    case MatchEventType.ShotMade:
                        bool swish = _match.Ball.ShotGrade == TimingGrade.Green;
                        bool dunk = _match.Ball.ShotType == ShotType.Dunk;
                        _netAt = Time.unscaledTime;
                        _netSecond = AttackingSecondHoop();
                        _hud.Toast((dunk ? Dunks.Name(_dunkStyle) + "  +" : _match.Ball.ShotBank ? "OFF THE GLASS  +" : swish ? "SWISH  +" : "+") + e.Value, 1.1f);
                        if (!_reduceMotion) _bursts.Spawn(HoopWorld, swish ? (Color)new Color32(0xFF, 0xD1, 0x66, 255) : Color.white, dunk ? 28 : (swish ? 20 : 12), dunk ? 5f : 4f);
                        if (dunk)
                        {
                            if (_cinePlayer < 0) PulseRim(e.Team, false); // a SHOWSTOPPER dunk gets the big one below
                            _cameraRig.Shake(Dunks.Shake(_dunkStyle) + 0.04f);
                            if (e.Team == human || Versus) Haptics.Heavy();
                        }
                        bool crowdHappy = Versus || e.Team == human;
                        SetCrowd(crowdHappy ? CrowdMood.Cheer : CrowdMood.Groan, crowdHappy ? 1.6f : 1.1f);
                        ArenaOnBasket(e.Team, e.Value, dunk);
                        if (_match.IsHumanControlled(e.PlayerIndex))
                        {
                            int streak = _match.Players[e.PlayerIndex].HotStreak;
                            int heat = _match.Setup.Shot.heatThreshold;
                            if (streak == heat) Callout("HEAT CHECK!", SfxId.OnFire);
                            else if (streak == heat - 1) Callout("HEATING UP", SfxId.Stinger);
                            else if (dunk) { Sfx(SfxId.Stinger, 0.8f); Audio.AudioManager.Voice("SLAM!"); }
                            else if (swish) Sfx(SfxId.Stinger, 0.5f);
                        }
                        _celebrator = e.PlayerIndex;
                        _celebrateStart = Time.unscaledTime;
                        _celebration = Mine(e.PlayerIndex) ? _myCelebration : CelebrationKind.FistPump;
                        Sfx(swish ? SfxId.Swish : SfxId.Rim);
                        Sfx(e.Team == human ? SfxId.CrowdCheer : SfxId.CrowdGroan, 0.6f);
                        if (e.Team == human) Haptics.Success();
                        break;
                    case MatchEventType.ShotMissed:
                        Sfx(SfxId.Rim, 0.8f);
                        break;
                    case MatchEventType.PassCaught:
                        Sfx(SfxId.Bounce, 0.5f, 1.2f);
                        break;
                    case MatchEventType.StealAttempt:
                        Sfx(SfxId.Squeak, 0.6f);
                        break;
                    case MatchEventType.Steal:
                        if (!_reduceMotion) _bursts.Spawn(CourtSpace.ToWorldSnapped(_match.Players[e.PlayerIndex].Position, 1f), new Color32(0x4C, 0xC9, 0xF0, 255), 8, 2.5f, 0.4f);
                        _hud.Toast(e.Team == human ? "STEAL!" : "STRIPPED", 1.1f);
                        Sfx(SfxId.Steal);
                        if (e.Team == human) Haptics.Medium();
                        break;
                    case MatchEventType.Block:
                        if (!_reduceMotion) _bursts.Spawn(CourtSpace.ToWorldSnapped(_match.Players[e.PlayerIndex].Position, 2f), new Color32(0xF4, 0xF1, 0xDE, 255), 14, 3.5f, 0.5f);
                        _hud.Toast(e.Team == human ? "BLOCKED!" : "SENT BACK", 1.1f);
                        Sfx(SfxId.Block);
                        Sfx(SfxId.CrowdOooh, 0.7f);
                        _cameraRig.Shake(0.18f);
                        Haptics.Medium();
                        break;
                    case MatchEventType.Jump:
                        if (Mine(e.PlayerIndex)) Sfx(SfxId.Squeak, 0.5f, 1.3f);
                        break;
                    case MatchEventType.Switch:
                        if (e.Team == human) _hud.Toast("SWITCH", 0.7f);
                        break;
                    case MatchEventType.Screen:
                        if (e.Team == human) _hud.Toast("SCREEN SET", 0.8f);
                        break;
                    case MatchEventType.PlayCalled:
                        if (e.Team == human) _hud.Toast(PlayName((PlayCall)e.Value), 1.0f);
                        break;
                    case MatchEventType.GameOver:
                        Sfx(SfxId.Buzzer, 0.8f);
                        Sfx(SfxId.ArenaHorn, 0.55f);
                        break;
                    case MatchEventType.Interception:
                        _hud.Toast(e.Team == human ? "PICKED OFF!" : "STOLEN", 1.1f);
                        Sfx(SfxId.Steal, 0.8f);
                        if (e.Team == human) Haptics.Light();
                        break;
                    case MatchEventType.Rebound:
                        if (e.Team == human) _hud.Toast("BOARD!", 0.8f);
                        break;
                    case MatchEventType.PossessionChanged:
                        if (e.Team == human && _match.MustClear) _hud.Toast("TAKE IT BACK", 1.2f);
                        break;
                    case MatchEventType.BallCleared:
                        if (e.Team == human) _hud.Toast("CLEARED", 0.7f);
                        break;
                    case MatchEventType.ShotClockViolation:
                        _hud.Toast("SHOT CLOCK", 1.2f);
                        Sfx(SfxId.Whistle, 0.7f);
                        break;
                    case MatchEventType.Violation:
                        // Full Court: backcourt, 8 seconds, or 5-second inbound.
                        _hud.Toast(MatchSimulation.ViolationName((ViolationKind)e.Value), 1.3f);
                        Sfx(SfxId.Whistle, 0.8f);
                        break;
                    case MatchEventType.AnkleBreaker:
                        // Street rules: the defender stumbles.
                        if (!_reduceMotion) _bursts.Spawn(CourtSpace.ToWorldSnapped(_match.Players[e.Value].Position, 0.5f), new Color32(0xFF, 0xD1, 0x66, 255), 12, 3f, 0.5f);
                        if (_streetsHud != null)
                        {
                            // THE STREETS: the big ANKLE BREAKER stamp, and a beat of freeze on him going down.
                            var victim = _match.Players[e.Value].Def;
                            bool mine = e.Team == streetsTeam || Versus;
                            _streetsHud.Stamp("ANKLE BREAKER!", (string.IsNullOrEmpty(victim.lastName) ? victim.firstName : victim.lastName).ToUpperInvariant() + " " + Loc.T("IS DOWN"),
                                              mine ? Theme.Gold : Theme.Pink);
                            if (!_reduceMotion && _link == null) stop = HitStop.Combine(stop, HitStop.Ankles);
                            if (Speech.Enabled) Captions.Show("ANKLE BREAKER!"); // the booth will say it
                            else Audio.AudioManager.Voice("ANKLE BREAKER!");
                        }
                        else _hud.Toast(e.Team == human ? "ANKLES!" : "GOT YOU!", 1.2f);
                        Sfx(SfxId.CrowdCheer, 0.9f);
                        Sfx(SfxId.CrowdOooh, 0.8f);
                        Sfx(SfxId.Squeak, 0.8f, 0.8f);
                        _cameraRig.Shake(0.15f);
                        if (e.Team == human) { Haptics.Medium(); if (_streetsHud == null) Audio.AudioManager.Voice("OOOH!"); }
                        break;
                    case MatchEventType.Substitution:
                        RedrawPlayer(e.PlayerIndex);
                        if (Time.unscaledTime >= _nextHornAt) { Sfx(SfxId.ArenaHorn, 0.3f); _nextHornAt = Time.unscaledTime + 3f; }
                        if (e.Team == human) _hud.Toast("SUB: " + _match.Players[e.PlayerIndex].Def.lastName.ToUpperInvariant() + " IN", 1.1f);
                        break;
                    case MatchEventType.CheckBall:
                        if (!Demo && !Coaching && _watch == null) _hud.Toast(e.Team == human ? (_match.MustInbound && _match.HumanHasBall ? "INBOUND: PASS IT IN" : "YOUR BALL") : "DEFENSE", 0.9f);
                        break;
                }
            }
            if (stop > 0f) _hitStopUntil = Time.unscaledTime + stop;
        }

        private bool _dustUp, _dustDown;
        private RimPulse _rimPulse;

        /// <summary>THE STREETS: the energy pulse off the rim on a dunk (bigger for a SHOWSTOPPER).</summary>
        private void PulseRim(int team, bool big)
        {
            if (_rimPulse == null || _reduceMotion) return;
            int human = _match.Setup.HumanTeam;
            var color = big ? (Color)Theme.Gold : team == human || Versus ? (Color)new Color32(0xFF, 0xD1, 0x66, 255) : new Color32(0x4C, 0xC9, 0xF0, 255);
            _rimPulse.Fire(HoopWorld, color, big);
            if (big) _rimPulse.Fire(HoopWorld, new Color32(0xC7, 0x4D, 0xFF, 255), false);
        }

        /// <summary>THE STREETS: a puff of dust as the dunker leaves the floor, and again as he lands.</summary>
        private void StreetDunkDust(DunkFrame f)
        {
            if (!_match.StyleOn || _reduceMotion) return;
            if (f.Gather) { _dustUp = false; _dustDown = false; return; }
            var dust = new Color32(0xC9, 0xC4, 0xB8, 255);
            if (!_dustUp && f.LiftPx > 0 && !f.Dropping)
            {
                _dustUp = true;
                _bursts.Spawn(CourtSpace.ToWorldSnapped(f.Ground, 0.1f), dust, 8, 1.6f, 0.3f);
                Sfx(SfxId.Squeak, 0.5f, 0.7f);
            }
            if (!_dustDown && f.Landing)
            {
                _dustDown = true;
                _bursts.Spawn(CourtSpace.ToWorldSnapped(f.Ground, 0.1f), dust, 10, 1.8f, 0.35f);
            }
        }

        private void SyncViews(float dt, bool snapCamera)
        {
            CourtSpace.Flip = _match.Flipped;
            float now = Time.unscaledTime;
            if (_kitNote != null && _match.Time > 1.9f)
            {
                _hud.Toast(_kitNote, 1.6f);
                _kitNote = null;
            }
            if (_leaper >= 0 && now - _leapStart >= _leapDuration) _leaper = -1;
            SyncCrowd(now);
            var move = UpdateDribbleMove(now);
            var celebrate = _celebrator >= 0 ? Flair.Celebration(_celebration, now - _celebrateStart) : FlairPose.None;
            if (_celebrator >= 0 && now - _celebrateStart >= Flair.CelebrationSeconds) _celebrator = -1;
            DunkFrame dunk = default;
            bool dunking = _leaper >= 0 && _leapType == ShotType.Dunk;
            if (dunking)
            {
                float u = (now - _leapStart) / _leapDuration;
                dunk = DunkPath.At(_leapFrom, _match.Setup.Court.Hoop, _match.Players[_leaper].Position, u, _slamU, _hangU, _liftAtRimPx,
                                   _landscape ? DunkPath.ApproachSide : DunkPath.Approach, _match.StyleOn ? DunkPath.StreetGather : 0f);
                StreetDunkDust(dunk);
            }
            // The rim is pulled down while someone hangs on it (2 pixels at the slam, then 1).
            if (_reduceMotion) dunk.SwingPx = 0;
            SyncRimDip(dunking && dunk.OnRim ? dunk.RimDipPx : 0);
            SyncNet(now);
            for (int i = 0; i < _playerViews.Length; i++)
            {
                _playerViews[i].ClearOverride();
                bool shooting = i == _match.ChargingIndex || (i == _lastShooter && now < _shootPoseUntil);
                var flair = i == _celebrator ? celebrate : (Mine(i) ? move : FlairPose.None);
                float jump = _match.JumpHeight01(i);
                if (i == _leaper)
                {
                    float leap = Flair.Leap(_leapType, now - _leapStart, _leapDuration);
                    if (_leapType == ShotType.Dunk)
                    {
                        if (dunk.Gather || dunk.Landing)
                        {
                            // THE STREETS: the plant before take-off and the landing: arms down, feet on the floor.
                            flair = FlairPose.None;
                        }
                        else if (dunk.OnRim || dunk.Dropping)
                        {
                            // Hanging and dropping: one steady pose (no flips mid-swing), swinging under the rim.
                            flair = Dunks.HangPose(_dunkStyle, dunk);
                        }
                        else
                        {
                            if (!_reduceMotion) flair = Dunks.Pose(_dunkStyle, Mathf.Min(0.999f, (now - _leapStart) / Mathf.Max(0.01f, _slamU * _leapDuration)));
                            flair.ArmsUp = true; // reaching for the rim
                        }
                        _playerViews[i].SetOverride(dunk.Ground, dunk.LiftPx);
                        leap = 1f;
                    }
                    jump = Mathf.Max(jump, leap);
                }
                if (_skyHigh) jump *= 2f; // SKY HIGH secret: looks only
                _playerViews[i].Sync(dt, _match.IsHumanControlled(i), shooting, jump, flair);
            }

            int holderOrder = _match.Holder != null ? CourtSpace.SortingOrder(_match.Holder.Position) : 0;
            var ballOffset = _match.HumanHasBall ? new Vector2Int(move.BallOffsetX, move.BallLift + move.Lift) : Vector2Int.zero;
            _ballView.Sync(_match.Ball, holderOrder, ballOffset);
            if (dunking && dunk.BallInHands)
            {
                // The ball rides in the dunker's hands up to the rim (windmills and tomahawks swing it around).
                var pose = _reduceMotion ? FlairPose.None : Dunks.Pose(_dunkStyle, Mathf.Min(0.999f, (now - _leapStart) / Mathf.Max(0.01f, _slamU * _leapDuration)));
                int reach = CharacterSpriteGenerator.HandReachPx(_match.Players[_leaper].Def.appearance);
                var hands = dunk.Gather
                    // The gather: the ball brought down to the hip before he goes up.
                    ? CourtSpace.ToWorldSnapped(dunk.Ground) + new Vector3(2f, reach * 0.45f, 0f) / CourtSpace.PixelsPerUnit
                    : CourtSpace.ToWorldSnapped(dunk.Ground)
                      + new Vector3(pose.BallOffsetX, dunk.LiftPx + reach + 2 + System.Math.Max(0, pose.BallLift), 0f) / CourtSpace.PixelsPerUnit;
                _ballView.SyncInHands(hands, CourtSpace.SortingOrder(dunk.Ground, 2));
            }
            if (_rainbowBall) _ballView.Tint(ToColor(ArcadeColors.RainbowAt(now)));
            else _ballView.Tint(_match.Ball.IsHeld && _match.IsHeatedUp(_match.Ball.HolderIndex) ? ToColor(ArcadeColors.FlameAt(now, 1)) : Color.white);
            SpawnFlames(now);

            // Shot meter (human only).
            if (_match.ChargingIndex >= 0 && _match.IsHumanControlled(_match.ChargingIndex))
            {
                var shooter = _match.Players[_match.ChargingIndex];
                _meter.ShowCharging(CourtSpace.ToWorld(shooter.Position), _match.ChargeMeter,
                                    shooter.Def.attributes.shooting, _match.Setup.Shot, _match.ContestLevel(_match.ChargingIndex));
            }
            else
            {
                _meter.Idle();
            }

            // Intended receiver marker.
            int receiver = !Coaching && _watch == null && _match.Phase == MatchPhase.Live && _match.ChargingIndex < 0 ? _match.PreviewPassTarget(ScreenToCourt(_controls.ScreenStick)) : -1;
            _receiverArrow.enabled = receiver >= 0;
            if (receiver >= 0)
            {
                // Gold arrow = this pass will be an alley-oop.
                bool oop = _match.IsAlleyOopTarget(receiver);
                _receiverArrow.color = oop ? (Color)new Color32(0xFF, 0xD1, 0x66, 255) : new Color32(0x4C, 0xC9, 0xF0, 255);
                float bob = oop ? Mathf.Round(Mathf.Sin(now * 10f) * 2f) / CourtSpace.PixelsPerUnit : 0f;
                _receiverArrow.transform.position = CourtSpace.ToWorldSnapped(_match.Players[receiver].Position) + new Vector3(0f, 1.75f + bob, 0f);
            }

            // 2-player and Full Court: follow the ball so nobody is left off screen.
            var follow = TheaterFollow() ?? CinemaFollow() ?? (Versus || FullCourtGame || Coaching ? _match.Ball.Position
                         // Close camera (fill screen): between you and the ball, so the play stays in view.
                         : _cameraRig.Fill ? Vec2.Lerp(_match.Controlled.Position, _match.Ball.Position, 0.5f)
                         : _match.Controlled.Position);
            _cameraRig.Follow(_match.ToWorldCourt(follow), Mathf.Max(dt, Time.unscaledDeltaTime), snapCamera);
            _hud.Sync(_match);
            if (_streetsHud != null) _streetsHud.Sync(_match, _touchShown && !_paused && _match.Phase != MatchPhase.Final);
            // The info line builds a string, so refresh it ~10x a second rather than every frame.
            if (Time.unscaledTime >= _nextInfoAt)
            {
                _nextInfoAt = Time.unscaledTime + 0.1f;
                _hud.SetInfo(InfoLine());
            }
            SyncPracticeMarkers();
            SyncHorse();
            UpdateCoachTips();
            UpdateControlLabels();
        }

        private int _rimDipped;

        /// <summary>Distance from player <paramref name="me"/> to the closest opponent on the floor (for coach tips).</summary>
        private float NearestOpponentDistance(int me)
        {
            float best = float.MaxValue;
            var p = _match.Players[me];
            for (int i = 0; i < _match.Players.Length; i++)
                if (_match.Players[i].Team != p.Team) best = Mathf.Min(best, Vec2.Distance(_match.Players[i].Position, p.Position));
            return best;
        }

        /// <summary>True when the basket under attack is the second (bottom / right) hoop on screen.</summary>
        private bool AttackingSecondHoop()
        {
            if (_hoop2T == null) return false;
            var target = CourtSpace.ToWorld(_match.Setup.Court.Hoop, CourtSpace.RimHeight);
            return (target - _hoop2Base).sqrMagnitude < (target - _hoopBase).sqrMagnitude;
        }

        /// <summary>The net kicks (stretches, then flares) for a moment after a make.</summary>
        private void SyncNet(float now)
        {
            if (_hoopSr == null || _art.HoopFrames == null) return;
            int f = _reduceMotion ? 0 : PropSpriteGenerator.NetFrame(now - _netAt);
            var frames = _landscape ? _art.HoopSideFrames : _art.HoopFrames;
            var a = frames[_netSecond ? 0 : f];
            if (_hoopSr.sprite != a) _hoopSr.sprite = a;
            if (_hoop2Sr != null)
            {
                var b = frames[_netSecond ? f : 0];
                if (_hoop2Sr.sprite != b) _hoop2Sr.sprite = b;
            }
        }

        private void SyncRimDip(int dipPx)
        {
            if (dipPx == _rimDipped || _hoopT == null) return;
            _rimDipped = dipPx;
            var down = new Vector3(0f, -dipPx / CourtSpace.PixelsPerUnit, 0f);
            // The basket being attacked is the one nearest the simulation's hoop on screen.
            var target = CourtSpace.ToWorld(_match.Setup.Court.Hoop, CourtSpace.RimHeight);
            bool second = _hoop2T != null && (target - _hoop2Base).sqrMagnitude < (target - _hoopBase).sqrMagnitude;
            _hoopT.position = _hoopBase + (second ? Vector3.zero : down);
            if (_hoop2T != null) _hoop2T.position = _hoop2Base + (second ? down : Vector3.zero);
        }

        /// <summary>HEAT CHECK: embers rise off heated-up players (and the ball they carry).</summary>
        private void SpawnFlames(float now)
        {
            if (_reduceMotion || Core.PowerMonitor.SavingPower || now < _nextFlameAt) return;
            _nextFlameAt = now + 0.07f;
            for (int i = 0; i < _match.Players.Length; i++)
            {
                if (!_match.IsHeatedUp(i)) continue;
                var at = _match.Ball.IsHeld && _match.Ball.HolderIndex == i
                    ? CourtSpace.ToWorldSnapped(_match.Ball.Position, _match.Ball.Height + 0.2f)
                    : CourtSpace.ToWorldSnapped(_match.Players[i].Position, 0.9f);
                _bursts.Spawn(at, ToColor(ArcadeColors.FlameAt(now, i)), 2, 1.3f, 0.32f);
            }
        }

        private static Color ToColor(RgbColor c) => new Color32(c.r, c.g, c.b, c.a);

        private void UpdateControlLabels()
        {
            if (_link != null && _link.Seat == 1)
            {
                UpdateGuestLabels();
                return;
            }
            bool offense = _match.OffenseTeam == _match.Setup.HumanTeam;
            bool oop = offense && _match.HumanHasBall && _match.Phase == MatchPhase.Live && _match.AlleyOopCandidate(_match.ControlledIndex) >= 0;
            string shoot, pass, dunk;
            bool canShoot, canPass, canDunk;
            if (offense && _match.HumanHasBall)
            {
                shoot = _match.MustClear ? "CLEAR" : (_match.MustInbound ? "WAIT" : "SHOOT");
                pass = _match.MustInbound ? "INBOUND" : (oop ? "OOP" : "PASS");
                dunk = "DUNK";
                canShoot = !_match.MustClear && !_match.MustInbound;
                canDunk = canShoot;
                canPass = true;
                _controls.Shoot.UnavailableHint = _match.MustInbound ? "PASS IT IN" : "TAKE IT BACK";
                _controls.Dunk.UnavailableHint = _controls.Layup.UnavailableHint = _controls.Shoot.UnavailableHint;
            }
            else if (offense)
            {
                shoot = "SHOOT";
                pass = "ASK";
                dunk = "DUNK";
                canShoot = false;
                canDunk = false;
                canPass = _match.HumanTeamHasBall;
                _controls.Shoot.UnavailableHint = "NO BALL";
                _controls.Dunk.UnavailableHint = _controls.Layup.UnavailableHint = "NO BALL";
            }
            else
            {
                shoot = "BLOCK";
                pass = "SWITCH";
                dunk = "STEAL";
                canShoot = true;
                canDunk = true;
                canPass = true;
            }
            _controls.Pass.UnavailableHint = "NO BALL";
            bool canCall = CanCall();

            int state = (offense ? 1 : 0) | (_match.HumanHasBall ? 2 : 0) | (_match.MustClear ? 4 : 0) | (_match.HumanTeamHasBall ? 8 : 0) | (canCall ? 16 : 0)
                        | (oop ? 32 : 0) | (_match.MustInbound ? 64 : 0)
                        | (_controls2 != null && _match.StyleOn && _match.SecondControlledIndex >= 0
                           && _match.CanCallBreaker(_match.Players[_match.SecondControlledIndex].Team) ? 128 : 0);
            if (state == _lastLabelState) return;
            _lastLabelState = state;
            _controls.SetRole(!offense, oop);
            _controls.SetLabels(shoot, pass, dunk);
            _controls.SetAvailability(canShoot, canPass, canDunk, canDunk, canCall);
            if (_controls2 != null)
            {
                bool p2Offense = _match.SecondControlledIndex >= 0 && _match.OffenseTeam == _match.Players[_match.SecondControlledIndex].Team;
                _controls2.SetRole(!p2Offense, false);
                _controls2.Call.SetLabel(!_match.StyleOn ? "P&R" : (state & 128) != 0 ? "SHOW" : "TRICK");
            }
        }

        /// <summary>
        /// Plays your equipped dribble move when you cut sharply with the ball (looks only:
        /// speed and handling come from the simulation, not from this).
        /// </summary>
        /// <summary>A skill move: a toast and a squeak, the dribble pose for your player, and secret moves get found.</summary>
        /// <summary>THE STREETS: a trick's name and points, a little flash, and the crowd.</summary>
        private void OnStreetTrick(MatchEvent e, int human)
        {
            var trick = (StreetTrick)e.Value;
            bool ours = e.Team == human || Versus;
            if (ours) _hud.Toast(Loc.T(MatchSimulation.TrickName(trick)) + "  +" + Mathf.RoundToInt(e.Chance), 0.9f);
            else if (trick == StreetTrick.OffTheHead) _hud.Toast("OFF YOUR HEAD!", 0.9f);
            if (!_reduceMotion && e.PlayerIndex >= 0)
                _bursts.Spawn(CourtSpace.ToWorldSnapped(_match.Players[e.PlayerIndex].Position, 0.8f), new Color32(0xC7, 0x4D, 0xFF, 255),
                              trick == StreetTrick.OffTheHead ? 14 : 6, 2.4f, 0.35f);
            Sfx(trick == StreetTrick.OffTheHead ? SfxId.CrowdOooh : SfxId.Squeak, trick == StreetTrick.OffTheHead ? 0.9f : 0.5f);
            if (_match.IsHumanControlled(e.PlayerIndex)) Haptics.Light();
        }

        private void OnSkillMove(MatchEvent e, int human)
        {
            var kind = (SkillMoveKind)e.Value;
            if (!_match.IsHumanControlled(e.PlayerIndex))
            {
                // An AI handler put a move on you: show what it was, so you can learn to read it.
                int victim = (int)e.Chance;
                if (victim >= 0 && _match.IsHumanControlled(victim))
                {
                    _hud.Toast(SkillToast(kind), 0.6f);
                    Sfx(SfxId.Squeak, 0.6f, 1.1f);
                }
                return;
            }
            Sfx(SfxId.Squeak, 0.75f, kind == SkillMoveKind.Spin ? 1.25f : 1.05f);
            if (e.PlayerIndex == _match.ControlledIndex)
            {
                _skillPose = kind;
                _skillPoseStart = Time.unscaledTime;
            }
            bool counts = !Demo && !Versus && !Theater && e.PlayerIndex == _match.ControlledIndex && App.Career != null;
            if (counts && SkillMoveInfo.Discover(App.Career, kind))
            {
                _hud.Toast(SecretFoundToast(kind), 1.8f);
                Sfx(SfxId.Stinger, 0.6f);
                Haptics.Medium();
                App.SaveCareer();
                return;
            }
            _hud.Toast(SkillToast(kind), 0.7f);
        }

        private static string SkillToast(SkillMoveKind k)
        {
            switch (k)
            {
                case SkillMoveKind.Spin: return "SPIN MOVE";
                case SkillMoveKind.Jab: return "JAB STEP";
                case SkillMoveKind.Hesi: return "HESI";
                case SkillMoveKind.StepBack: return "STEP-BACK";
                case SkillMoveKind.Crossover: return "CROSSOVER";
                default: return "SHAKE";
            }
        }

        private static string SecretFoundToast(SkillMoveKind k)
        {
            switch (k)
            {
                case SkillMoveKind.Hesi: return "SECRET MOVE FOUND: HESI!";
                case SkillMoveKind.StepBack: return "SECRET MOVE FOUND: STEP-BACK!";
                default: return "SECRET MOVE FOUND: SHAKE!";
            }
        }

        private static DribbleMoveKind PoseFor(SkillMoveKind k)
        {
            switch (k)
            {
                case SkillMoveKind.Spin: return DribbleMoveKind.SpinCycle;
                case SkillMoveKind.Jab: return DribbleMoveKind.RockerStep;
                case SkillMoveKind.Hesi: return DribbleMoveKind.HesiHop;
                case SkillMoveKind.StepBack: return DribbleMoveKind.StepBack;
                case SkillMoveKind.Crossover: return DribbleMoveKind.Crossover;
                default: return DribbleMoveKind.DoubleCross;
            }
        }

        private FlairPose UpdateDribbleMove(float now)
        {
            if (_skillPose != SkillMoveKind.None && now - _skillPoseStart < Flair.DribbleMoveSeconds && _match.HumanHasBall)
                return Flair.DribbleMove(PoseFor(_skillPose), now - _skillPoseStart);
            if (!_match.HumanHasBall || _match.ChargingIndex >= 0)
            {
                _lastMoveDir = Vec2.Zero;
                return FlairPose.None;
            }
            var dir = _match.Controlled.Motion.velocity;
            if (now >= _moveReadyAt && Flair.IsSharpCut(_lastMoveDir, dir))
            {
                _moveStart = now;
                _moveReadyAt = now + Flair.DribbleMoveCooldown;
                Sfx(SfxId.Squeak, 0.5f, 1.1f);
            }
            if (dir.SqrMagnitude >= 0.25f) _lastMoveDir = dir;
            return Flair.DribbleMove(_myMove, now - _moveStart);
        }

        private bool CanCall() => _match.StyleOn
            ? _match.Phase == MatchPhase.Live && _match.HumanHasBall
            : _practice == null && _horse == null && _match.OffenseTeam == _match.Setup.HumanTeam && _match.HumanTeamHasBall
            && !_match.MustClear && _match.Phase == MatchPhase.Live && _match.ActivePlay == PlayCall.None;

        private string InfoLine()
        {
            if (_tutorial != null && !_tutorial.Finished)
            {
                string touch = Loc.T(_tutorial.TouchHint);
                string hint = Application.isMobilePlatform ? touch : touch + "  (" + _tutorial.KeyHint + ")";
                return "STEP " + ((int)_tutorial.Step + 1) + "/" + TutorialSession.StepCount + " · " + Loc.T(_tutorial.Title) + " — " + hint.ToUpperInvariant();
            }
            if (Versus && _match.Time < 10f)
                return "P1: WASD · K SHOOT · J PASS · L STEAL · C CALL     P2: ARROWS · NUM1 SHOOT · NUM2 PASS · NUM3 STEAL · NUM0 CALL";
            if (_horse != null)
            {
                string turn = _horse.IsCpuTurn ? _horse.Name(1) + " IS SHOOTING..."
                            : (_horse.Opponent == HorseOpponent.Friend ? _horse.Name(_horse.Shooter) + ": " : "") + (_horse.Matching ? "MATCH THE SHOT (GOLD RING)" : "SET A SHOT");
                return _horse.Name(0) + " " + Pad(_horse.LettersOf(0)) + "  ·  " + _horse.Name(1) + " " + Pad(_horse.LettersOf(1)) + "  ·  " + turn;
            }
            if (_practice != null)
            {
                string time = _practice.TimeLimit > 0f ? Mathf.CeilToInt(_practice.TimeLeft) + "s  ·  " : _practice.Elapsed.ToString("0.0") + "s  ·  ";
                return time + _practice.ResultText().ToUpperInvariant();
            }
            // On a computer, show the keyboard controls for the first seconds of a match.
            if (Demo) return StoreShots.Active ? "" : "DEMO PLAY  ·  TAP OR PRESS ANY BUTTON";
            if (_padMode && !_keyMode && _match.Time < 8f)
                return "STICK MOVE · A SHOOT (HOLD) · X PASS · RB DUNK · LB LAYUP · B STEAL · Y CALL";
            if ((_keyMode || !Application.isMobilePlatform || Core.DeviceInfo.IsMac) && _match.Time < 8f)
                return "WASD MOVE · K SHOOT (HOLD) · J PASS · U DUNK · I LAYUP · L STEAL · C CALL";
            if (_match.ActivePlay != PlayCall.None && _match.ActivePlayTeam == _match.Setup.HumanTeam) return PlayName(_match.ActivePlay);
            if (_match.IsBoxingOut(_match.ControlledIndex)) return "BOX OUT";
            if (_daily != null) return "DAILY: " + _daily.Describe().ToUpperInvariant();
            return "";
        }

        private static string PlayName(PlayCall play)
        {
            switch (play)
            {
                case PlayCall.PickAndRoll: return "PICK & ROLL";
                case PlayCall.GiveAndGo: return "GIVE & GO";
                case PlayCall.ClearOut: return "CLEAR OUT";
                case PlayCall.Backdoor: return "BACKDOOR: HIT THE CUTTER";
                case PlayCall.PostUp: return "POST UP: FEED THE BIG";
                default: return "";
            }
        }

        private static string DrillTitle(DrillKind kind)
        {
            switch (kind)
            {
                case DrillKind.FreeShoot: return "FREE SHOOT";
                case DrillKind.PassingTargets: return "PASSING TARGETS";
                case DrillKind.ThreePoint: return "3-POINT CONTEST";
                case DrillKind.Lockdown: return "LOCKDOWN";
                case DrillKind.Shootout: return "SHOOTOUT";
                case DrillKind.AroundTheWorld: return "AROUND THE WORLD";
                case DrillKind.Horse: return "H-O-R-S-E";
                default: return "DRIBBLE LANE";
            }
        }

        private void ShowFinal()
        {
            _finalShown = true;
            SetTouchVisible(false);
            var s = _match.Setup;
            int human = s.HumanTeam;
            string title = _match.Winner < 0 ? "TIE" : (_match.Winner == human ? "YOU WIN" : "FINAL");

            if (_request.Mode == GameMode.Practice || _request.Mode == GameMode.Tutorial)
            {
                string line = s.TeamA.abbreviation + "  " + _match.Score[0] + "  -  " + _match.Score[1] + "  " + s.TeamB.abbreviation;
                _hud.ShowFinal(title, line);
                return;
            }

            if (Versus)
            {
                // Local 2-player: box score, no rewards (only counted for a badge and an achievement).
                var vs = MatchSummary.From(_match, _request.Mode, "versus");
                bool couch = CouchCup.IsCouch(_request.ContextId) && App.Career?.couch != null && App.Career.couch.Active;
                string couchNote = null, couchTitle = null;
                if (couch && !_resultApplied)
                {
                    var cup = App.Career.couch;
                    var game = CouchCup.NextGame(cup);
                    string nameA = game != null ? cup.names[game.a].ToUpperInvariant() : "PLAYER 1", nameB = game != null ? cup.names[game.b].ToUpperInvariant() : "PLAYER 2";
                    var outcome = CouchCup.Report(cup, _match.Score[0], _match.Score[1]);
                    App.OpenCouchOnMenu = true;
                    couchTitle = outcome == CouchOutcome.TieReplay ? "TIE: PLAY IT AGAIN" : (_match.Score[0] > _match.Score[1] ? nameA : nameB) + " WINS";
                    var after = CouchCup.NextGame(cup);
                    couchNote = outcome == CouchOutcome.Champion ? cup.names[cup.champion].ToUpperInvariant() + " WINS THE COUCH CUP!"
                              : outcome == CouchOutcome.TieReplay ? "Ties don't count in the Couch Cup. HOME, then PLAY to go again."
                              : after != null ? "Next: " + cup.names[after.a] + " vs " + cup.names[after.b] + " (" + CouchCup.RoundName(cup, after.round) + ")" : null;
                    if (outcome == CouchOutcome.Champion) Sfx(SfxId.Fanfare);
                }
                if (App.Career != null && !_resultApplied)
                {
                    _resultApplied = true;
                    App.Career.totals.versusGames++;
                    App.SaveCareer();
                    App.ReportGameCenter();
                }
                _hud.HasPlayOfTheGame = _recorder.BestPlay != null;
                _hud.HighlightCount = _recorder.Highlights.Count;
                string winner = _match.Winner < 0 ? "TIE" : (_match.Winner == 0 ? "PLAYER 1 WINS" : "PLAYER 2 WINS");
                string linkNote = null;
                if (_link != null)
                {
                    winner = _match.Winner < 0 ? "TIE" : (_match.Winner == _link.Seat ? "YOU WIN" : _opponent + " WINS");
                    if (_liveSetup != null) App.OpenLiveOnMenu = true;
                    linkNote = _liveSetup != null ? (_match.Winner >= 0 ? RateLive(_match.Winner == _link.Seat, Backend.Outcome.Final) : "Tie: no rating change.")
                                                    + "\nREMATCH: you both tap it (a new rated game). HOME leaves."
                                                  : "REMATCH: you both tap it. HOME hangs up.";
                    if (_cupSetup != null) linkNote = CupResult(ref winner);
                }
                Haptics.Success();
                if (couchTitle != null) winner = couchTitle;
                // Two-phone and (Phase 33) Live games stay connected for a rematch.
                bool linked = _link != null;
                _hud.ShowPostGame(winner, vs, default, false, linked ? linkNote : couchNote, null, (_link == null && !couch) || linked);
                if (!linked) HangUp();
                return;
            }

            string matchId = string.IsNullOrEmpty(_request.ContextId)
                ? System.Guid.NewGuid().ToString("N")
                : _request.ContextId + "#" + _request.Seed;
            var summary = MatchSummary.From(_match, _request.Mode, matchId);
            summary.day = App.Today;
            summary.isPlayoff = _request.Round >= 1;
            summary.isFinal = _request.Round >= 2;
            // Phase 38: a scenario from the CLUTCH editor is for fun: no SP, fans, career totals, records or weekly progress.
            bool customClutch = _request.Mode == GameMode.Clutch && Logic.Clutch.IsCustomContext(_request.ContextId);
            var grant = customClutch ? default : Rewards.For(summary, App.Rewards);

            string note = null;
            bool rewarded = false;
            if (App.Career != null && !_resultApplied)
            {
                _resultApplied = true;
                rewarded = !customClutch && Career.ApplyMatch(App.Career, summary, grant);
                int recruitsBefore = App.Career.rise.recruitable.Count;
                if (rewarded && _request.Mode == GameMode.Rival)
                {
                    var rivalOutcome = RivalEngine.ApplyResult(App.Career, summary);
                    note = rivalOutcome == RivalOutcome.Won
                        ? "RIVAL BEATEN!  +" + RivalEngine.WinBonus + " SP  +" + RivalEngine.WinFans + " FANS"
                        : "They win this one. They'll be back next season.";
                    if (rivalOutcome == RivalOutcome.Won) title = "RIVAL DOWN";
                }
                if (rewarded && _request.Mode == GameMode.Rise)
                {
                    var outcome = RiseEngine.ApplyResult(App.Career.rise, App.Catalog, summary, App.Career);
                    App.LastRiseOutcome = outcome;
                    note = OutcomeText(outcome);
                    if (outcome == RiseOutcome.Champion) title = "CHAMPIONS";
                }
                if (rewarded && _daily != null)
                {
                    if (DailyChallenges.IsMet(_daily, summary))
                    {
                        int bonus = DailyChallenges.Complete(App.Career.daily, App.Career, _daily.Day);
                        note = bonus > 0
                            ? "DAILY COMPLETE!  +" + bonus + " SP  ·  STREAK " + App.Career.daily.streak
                            : "Daily already done today. Come back tomorrow!";
                        if (bonus > 0) title = "DAILY DONE";
                    }
                    else
                    {
                        note = "Daily goal: " + _daily.Describe() + ". Not this time. Rematch?";
                    }
                }
                if (rewarded && _request.Mode == GameMode.King)
                {
                    var king = KingEngine.ApplyResult(App.Career.king, App.Catalog, summary, App.Career, out int kingBonus);
                    App.OpenKingOnMenu = true;
                    if (king == KingOutcome.Defended)
                    {
                        title = "STILL KING";
                        note = "STREAK " + App.Career.king.streak + "  ·  +" + kingBonus + " SP BONUS";
                    }
                    else if (king == KingOutcome.Dethroned)
                    {
                        title = "DETHRONED";
                        note = "Run over at " + App.Career.king.streak + " (best " + App.Career.king.best + ").";
                    }
                }
                if (rewarded && _request.Mode == GameMode.Arcade)
                {
                    var arcade = ArcadeEngine.ApplyResult(App.Career, summary, out int arcadeBonus);
                    var a = App.Career.secrets.arcade;
                    App.OpenArcadeOnMenu = true;
                    switch (arcade)
                    {
                        case ArcadeOutcome.Advanced:
                            title = "STAGE CLEAR";
                            note = "Next: stage " + (a.rung + 1) + " of " + ArcadeEngine.Rungs + (ArcadeEngine.IsBossRung(a.rung) ? "  ·  THE BOSS" : "");
                            break;
                        case ArcadeOutcome.Continue:
                            title = "CONTINUE?";
                            note = "Continues left: " + a.continues + ". Try the stage again.";
                            Sfx(SfxId.Coin, 0.7f);
                            break;
                        case ArcadeOutcome.GameOver:
                            title = "GAME OVER";
                            note = "Reached stage " + (a.rung + 1) + " of " + ArcadeEngine.Rungs + ".";
                            Audio.AudioManager.Voice("GAME OVER!");
                            break;
                        case ArcadeOutcome.Cleared:
                            title = "LADDER CLEARED";
                            note = "THE GLITCH and its court are unlocked!  +" + arcadeBonus + " SP";
                            Sfx(SfxId.Fanfare);
                            break;
                    }
                }
                if (rewarded && _request.Mode == GameMode.Cup)
                {
                    var cupOutcome = CupEngine.ApplyResult(App.Career.cup, App.Catalog, summary, App.Career, out int cupBonus);
                    App.OpenCupOnMenu = true;
                    var nextCup = CupEngine.NextGame(App.Career.cup);
                    if (cupOutcome == CupOutcome.Champion)
                    {
                        title = "CUP CHAMPIONS";
                        note = "CALLER CUP CHAMPIONS!  +" + cupBonus + " SP";
                        Sfx(SfxId.Fanfare);
                    }
                    else if (cupOutcome == CupOutcome.Advanced && nextCup != null)
                        note = "Next: " + CupEngine.RoundName(nextCup.round);
                    else if (cupOutcome == CupOutcome.Eliminated)
                        note = "Knocked out. Champion: " + (App.Catalog.Team(App.Career.cup.championId)?.FullName ?? "?");
                }
                if (rewarded && _request.Mode == GameMode.Legacy)
                {
                    var lg = App.Career.legacy;
                    var stageBefore = lg.stage;
                    var lgLine = Legacy.RecordGame(lg, App.Catalog, summary.HumanScore, summary.OpponentScore, summary.HumanLine?.stats);
                    App.OpenLegacyOnMenu = true;
                    if (lgLine != null)
                    {
                        note = "GRADE " + lgLine.grade + "  ·  " + Legacy.Level(lg) + " LV  ·  " + lg.skillPoints + " SKILL POINTS";
                        if (lg.stage != stageBefore || (lg.stage == LegacyStage.Pro && lg.season == null))
                        {
                            var h = lg.history.Count > 0 ? lg.history[lg.history.Count - 1] : null;
                            if (h != null)
                            {
                                title = h.result == "CHAMPIONS" ? "CHAMPIONS" : title;
                                note += "\n" + h.label + " OVER: " + h.result + (h.awards.Count > 0 ? "  ·  " + string.Join(", ", h.awards) : "");
                            }
                        }
                    }
                }
                if (rewarded && _request.Mode == GameMode.Streets)
                {
                    // 15 Oct 2026: THE STREETS record.
                    int hTeam = _match.Setup.HumanTeam;
                    int tricks = 0;
                    foreach (var pl in _match.Players) if (pl.Team == hTeam) tricks += _match.Stats[pl.Index].tricks;
                    var st = App.Career.streets ?? (App.Career.streets = new StreetsSaveData());
                    note = Streets.ApplyResult(st, summary.HumanWon, _match.BreakersScored[hTeam], tricks, _bestBank);
                    title = summary.HumanWon ? "YOU OWN THESE STREETS" : "TAKEN TO SCHOOL";
                    App.OpenStreetsOnMenu = true;
                    var stop = StreetsTour.FromContext(_request.ContextId);
                    if (stop != null)
                    {
                        int margin = _match.Score[hTeam] - _match.Score[1 - hTeam];
                        var tr = StreetsTour.Apply(st, App.Career, stop, summary.HumanWon, _match.BreakersScored[hTeam], margin);
                        title = summary.HumanWon ? (tr.TourCleared ? "KING OF THE STREETS" : tr.CityCleared ? stop.City + " IS YOURS" : stop.Crew + " DOWN") : stop.Leader + " WINS";
                        note = Loc.T("STARS") + " " + tr.Stars + "/3  ·  +" + tr.Rep + " " + Loc.T("REP")
                               + (tr.Sp > 0 ? "  ·  +" + tr.Sp + " SP" : "")
                               + (tr.Recruit != null ? "\n" + tr.Recruit + " " + Loc.T("joins your street pool") : "")
                               + (tr.CityCleared && stop.Tier + 1 < StreetsTour.Cities.Length ? "\n" + Loc.T("NEXT CITY OPEN:") + " " + StreetsTour.Cities[stop.Tier + 1]
                                  : tr.CityCleared ? "\n" + Loc.T("THE CROWN is waiting.") : "")
                               + "\n" + note;
                        if (tr.FirstWin) Sfx(SfxId.Fanfare);
                    }
                    // Phase 3: HOMECOURT.
                    if (StreetsHome.IsHome(_request))
                    {
                        var hr = StreetsHome.Apply(st, App.Career, StreetsHome.ChallengerOf(_request.ContextId), summary.HumanWon);
                        string court = StreetsHome.Name(st);
                        title = hr.Retaken ? court + " IS BACK" : hr.Defended ? court + " DEFENDED" : hr.Taken ? court + " TAKEN" : title;
                        note = (hr.Defended ? Loc.T("DEFENSES") + " " + st.homeDefenses + "  ·  " + Loc.T("STREAK") + " " + st.homeStreak + "  ·  " : "")
                               + (hr.Rep > 0 ? "+" + hr.Rep + " " + Loc.T("REP") : "") + (hr.Sp > 0 ? "  ·  +" + hr.Sp + " SP" : "")
                               + (hr.NewPerk != null ? "\n" + Loc.T("NEW PERK:") + " " + Loc.T(hr.NewPerk) : "")
                               + (hr.Taken ? Loc.T("Win TAKE IT BACK to get your perks back.") : "")
                               + "\n" + note;
                        if (hr.NewPerk != null || hr.Retaken) Sfx(SfxId.Fanfare);
                    }
                    // Phase 3: CREW CHEMISTRY (only when your own crew played).
                    if (CustomTeams.IsYours(_request.HomeTeamId) && hTeam == 0)
                    {
                        foreach (var id in StreetsChem.AddGame(st, summary.HumanWon))
                        {
                            var pd = App.Catalog.Player(id);
                            if (pd != null) note += "\n" + Loc.T("CHEMISTRY UP:") + " " + pd.DisplayName.ToUpperInvariant() + " " + Loc.T("LV") + " " + StreetsChem.Level(st, id);
                        }
                    }
                }
                // 17 Oct 2026: BALLER CITY: crews beaten and missions done by a game started in the city.
                if (rewarded && CityDistricts.TryParseReturn(_request.ReturnTo, out _, out _))
                {
                    var cityResult = CityMissions.ApplyGame(App.Career, _request, summary.HumanWon, summary.Margin, summary.HumanLine?.stats);
                    if (cityResult.Crew != null && summary.HumanWon) title = cityResult.Crew.Name + " " + Loc.T("DOWN");
                    else if (!summary.HumanWon && CityCrews.FromContext(_request.ContextId) is CityCrewDef lost) title = lost.Name + " " + Loc.T("WINS");
                    string cityNote = cityResult.Note();
                    if (!string.IsNullOrEmpty(cityNote)) note = string.IsNullOrEmpty(note) ? cityNote : note + "\n" + cityNote;
                    if (cityResult.Completed.Count > 0 || cityResult.CrewFirstWin) Sfx(SfxId.Fanfare);
                }
                var street = Street.FromContext(_request.ContextId);
                if (rewarded && street != null)
                {
                    int ankles = summary.HumanLine?.stats?.ankleBreakers ?? 0;
                    int rep = Street.ApplyResult(App.Career.street, street, summary.HumanWon, ankles);
                    App.OpenParkOnMenu = true;
                    title = summary.HumanWon ? "YOU RUN THE PARK" : street.Nickname + " WINS";
                    note = (rep >= 0 ? "+" : "") + rep + " REP  ·  " + Street.RepNames[Street.RepLevel(App.Career.street.rep)]
                           + (ankles > 0 ? "  ·  " + ankles + " ANKLE BREAKER" + (ankles == 1 ? "" : "S") : "");
                }
                var clutch = _request.Mode == GameMode.Clutch ? Logic.Clutch.FromContext(_request.ContextId, App.Catalog) : null;
                if ((rewarded || customClutch) && clutch != null)
                {
                    var cd = App.Career.clutch ?? (App.Career.clutch = new ClutchSaveData());
                    var outcome = Logic.Clutch.ApplyResult(cd, clutch, summary, App.Career, out int stars, out int newStars);
                    App.OpenClutchOnMenu = true;
                    title = outcome == ClutchOutcome.Won ? (stars == Logic.Clutch.StarsPerScenario ? "ICE COLD" : "CLUTCH") : "NOT THIS TIME";
                    note = ClutchNote(clutch, summary, stars, newStars);
                    // Phase 38: DAILY CLUTCH bonus and run of days; editor scenarios keep no stars.
                    int dailyDay = Logic.Clutch.DailyDayOf(_request.ContextId);
                    int dailyBonus = Logic.Clutch.ApplyDaily(cd, dailyDay, App.Today, outcome == ClutchOutcome.Won, App.Career);
                    if (dailyBonus > 0) note += "\n" + Loc.T("DAILY CLUTCH") + "  +" + dailyBonus + " SP  ·  " + Loc.T("RUN") + " " + cd.dailyStreak;
                    if (clutch.Custom) note += "\n<size=22><color=#8D99AE>" + Loc.T("Custom scenario: stars aren't saved.") + "</color></size>";
                    if (outcome == ClutchOutcome.Won && (newStars > 0 || dailyBonus > 0)) Sfx(SfxId.Fanfare);
                }
                var storyChapter = StoryMode.FromContext(_request.ContextId);
                if (rewarded && storyChapter != null)
                {
                    var st = App.Career.story ?? (App.Career.story = new StorySaveData());
                    var outcome = StoryMode.ApplyResult(st, storyChapter, summary, out int storyReward);
                    App.OpenStoryOnMenu = true;
                    if (outcome == StoryOutcome.Cleared)
                    {
                        App.Career.signalPoints += storyReward;
                        App.StoryOutroPending = storyChapter.Number;
                        title = storyChapter.Number == StoryMode.Chapters ? "SUNBURST CHAMPIONS" : "CHAPTER CLEARED";
                        note = storyChapter.Title + (storyReward > 0 ? "  ·  +" + storyReward + " SP" : "") + "  ·  HOME for the next scene";
                        Sfx(SfxId.Fanfare);
                    }
                    else
                    {
                        title = outcome == StoryOutcome.GoalMissed ? "SO CLOSE" : "NOT THIS TIME";
                        note = "Goal: " + StoryMode.GoalText(storyChapter) + ". REMATCH to try again.";
                    }
                }
                if (rewarded && _request.Mode == GameMode.CustomCup)
                {
                    var cup = App.Career.customCup;
                    var outcome = CustomCup.ApplyResult(cup, App.Catalog, summary.HumanScore, summary.OpponentScore);
                    App.OpenCustomCupOnMenu = true;
                    var nextCup = CustomCup.NextGame(cup);
                    if (outcome == CustomCupOutcome.Champion) { title = cup.name.ToUpperInvariant() + " CHAMPIONS"; Sfx(SfxId.Fanfare); }
                    else if (outcome == CustomCupOutcome.Advanced && nextCup != null) note = "Next: " + CustomCup.RoundName(cup, nextCup.round);
                    else if (outcome == CustomCupOutcome.Eliminated) note = "Knocked out. Champion: " + (App.Catalog.Team(cup.championId)?.FullName ?? "?");
                }
                if (rewarded && _request.Mode == GameMode.AllStar)
                {
                    int bonus = AllStar.ApplyGame(App.Career, summary);
                    title = summary.HumanWon ? "ALL-STAR WIN" : "ALL-STAR GAME";
                    note = (summary.HumanWon ? "Team Sunrise takes it." : "Team Moonlight takes it.") + (bonus > 0 ? "  +" + bonus + " SP" : "");
                }
                if (rewarded && _request.Mode == GameMode.Franchise)
                {
                    var fr = App.Career.franchise;
                    var frGame = Franchise.NextGame(fr);
                    int frRound = frGame != null ? frGame.round : 0;
                    App.OpenFranchiseOnMenu = true;
                    if (Franchise.RecordYourGame(fr, App.Catalog, summary.HumanScore, summary.OpponentScore, summary.TeamTotals(summary.humanTeam).chart))
                    {
                        string champ = Franchise.ChampionId(fr);
                        if (frRound == 2 && champ == Franchise.TeamId(fr.you))
                        {
                            title = "CHAMPIONS";
                            note = "FRANCHISE CHAMPIONS! Year " + (fr.year - 1) + " is yours.";
                        }
                        else if (fr.phase == FranchisePhase.Playoffs && frRound == 0)
                            note = Franchise.NextGame(fr) != null ? "Regular season over: you're in the playoffs!" : "Regular season over. You missed the playoffs.";
                        else if (frRound == 0)
                            note = "Record: " + SeasonEngine.Standings(fr.season).Find(r => r.TeamId == Franchise.TeamId(fr.you))?.Wins + "-"
                                   + SeasonEngine.Standings(fr.season).Find(r => r.TeamId == Franchise.TeamId(fr.you))?.Losses;
                        else if (frRound == 1)
                            note = summary.HumanWon ? "On to the final!" : "Knocked out in the semifinal.";
                        else note = "Runners-up. The off-season starts in the front office.";
                    }
                }
                if (rewarded && _request.Mode == GameMode.Tournament)
                {
                    var outcome = ClassicEngine.ApplyResult(App.Career.classic, App.Catalog, summary);
                    App.OpenClassicOnMenu = true;
                    note = ClassicText(outcome);
                    if (outcome == ClassicOutcome.Champion) title = "CLASSIC CHAMPS";
                }
                // Weekly Challenges and the Hoops Pass (every counted game; the daily adds its own pass XP once a day).
                if (rewarded)
                {
                    var weekly = Weekly.ApplyGame(App.Career, App.Catalog, summary, App.Today, street != null);
                    if (_daily != null && DailyChallenges.CompletedToday(App.Career.daily, _daily.Day))
                        weekly.PassRewards.AddRange(HoopsPass.AddDailyXp(App.Career, App.Catalog, App.Today));
                    string weeklyNote = weekly.Note();
                    if (weeklyNote != null)
                    {
                        note = string.IsNullOrEmpty(note) ? weeklyNote : note + "\n" + weeklyNote;
                        Sfx(SfxId.Coin, 0.8f);
                    }
                }
                if (App.Career.rise.recruitable.Count > recruitsBefore)
                    note = (string.IsNullOrEmpty(note) ? "" : note + "\n") + "New players can join your crew (Rise hub ► YOUR CREW).";
                var hints = Secrets.RevealHints(App.Career);
                if (hints.Count > 0)
                {
                    note = (string.IsNullOrEmpty(note) ? "" : note + "\n") + "SECRET CODE HINT FOUND! (Locker Room ► TROPHY)";
                    Sfx(SfxId.Coin, 0.8f);
                }
                var badges = Badges.TakeNew(App.Career);
                if (badges.Count > 0)
                {
                    note = (string.IsNullOrEmpty(note) ? "" : note + "\n") + "BADGE: " + string.Join(", ", badges.ConvertAll(b => b.Title));
                    Sfx(SfxId.Fanfare, 0.7f);
                }
                var ranks = App.Career.lastSpecialist;
                if (rewarded && ranks != null && ranks.Count > 0)
                {
                    note = (string.IsNullOrEmpty(note) ? "" : note + "\n") + string.Join(", ", ranks);
                    Sfx(SfxId.Fanfare, 0.7f);
                }
                var records = App.Career.lastNewRecords;
                if (rewarded && records != null && records.Count > 0)
                {
                    string line = "NEW RECORD: " + string.Join(", ", records);
                    note = string.IsNullOrEmpty(note) ? line : note + "\n" + line;
                    Sfx(SfxId.Fanfare, 0.8f);
                }
                // 14 Oct 2026: the phone's FEED posts about the game (titles get the CHAMPIONS post).
                if (rewarded)
                    PostToFeed(summary, summary.HumanWon && (title == "CHAMPIONS" || title == "CLASSIC CHAMPS" || title == "SUNBURST CHAMPIONS" || summary.isFinal));
                App.SaveCareer();
                App.ReportGameCenter();
            }
            if (summary.HumanWon) Haptics.Success();
            Sfx(summary.HumanWon ? SfxId.Victory : SfxId.Defeat, 0.8f);
            if (title == "CHAMPIONS" || title == "CLASSIC CHAMPS") Sfx(SfxId.Fanfare);

            // Rise and the Classic continue their run instead of offering a rematch.
            _hud.HasPlayOfTheGame = _recorder.BestPlay != null;
            _hud.HighlightCount = _recorder.Highlights.Count;
            bool run = IsRun;
            _hud.ShowPostGame(title, summary, grant, rewarded, note, run ? "CONTINUE" : null, !run);
        }

        /// <summary>Phase 37: the CLUTCH result line: stars, then which goals were met.</summary>
        private static string ClutchNote(ClutchScenario s, MatchSummary m, int stars, int newStars)
        {
            string Mark(bool ok) => ok ? "<color=#06D6A0>√</color> " : "<color=#8D99AE>×</color> ";
            bool won = m.HumanWon;
            string line = "<color=#FFD166>" + new string('●', stars) + "</color><color=#5C6378>" + new string('○', Logic.Clutch.StarsPerScenario - stars) + "</color>"
                          + (newStars > 0 ? "  +" + newStars * Logic.Clutch.SpPerNewStar + " SP" : "");
            line += "\n" + Mark(won) + Loc.T("Win") + "   " + Mark(won && Logic.Clutch.Met(s, s.Goal, s.GoalValue, m)) + Loc.T(Logic.Clutch.GoalText(s.Goal, s.GoalValue))
                    + "   " + Mark(won && Logic.Clutch.Met(s, s.Bonus, s.BonusValue, m)) + Loc.T(Logic.Clutch.GoalText(s.Bonus, s.BonusValue));
            return line;
        }

        private static string OutcomeText(RiseOutcome o)
        {
            switch (o)
            {
                case RiseOutcome.CircuitWin: return "Crew beaten. The Blacktop Circuit continues.";
                case RiseOutcome.CircuitLoss: return "They held their court. Run it back from the Rise hub.";
                case RiseOutcome.EnteredLeague: return "Circuit cleared! The Caller League wants you.";
                case RiseOutcome.MadePlayoffs: return "PLAYOFFS! The Gold Signal Cup is in reach.";
                case RiseOutcome.MissedPlayoffs: return "Season over: missed the playoffs.";
                case RiseOutcome.AdvancedToFinal: return "On to the final!";
                case RiseOutcome.Eliminated: return "Eliminated. Next season starts from the Rise hub.";
                case RiseOutcome.Champion: return "GOLD SIGNAL CUP CHAMPIONS!";
                default: return null;
            }
        }

        private static string ClassicText(ClassicOutcome o)
        {
            switch (o)
            {
                case ClassicOutcome.WonSemi: return "Semifinal won! The " + DefaultContent.RookieTournamentName + " final is next.";
                case ClassicOutcome.Champion: return DefaultContent.RookieTournamentName.ToUpperInvariant() + " CHAMPIONS!";
                case ClassicOutcome.Eliminated: return "Knocked out. Enter the next Classic from the main menu.";
                default: return null;
            }
        }

        private void ShowTutorialEnd()
        {
            _finalShown = true;
            SetTouchVisible(false);
            int reward = App.Career != null ? Career.CompleteTutorial(App.Career) : 0;
            if (App.Career != null) Badges.TakeNew(App.Career);
            if (App.Career != null)
            {
                App.SaveCareer();
                App.ReportGameCenter();
            }
            Haptics.Success();
            Sfx(SfxId.CrowdCheer, 0.7f);
            _hud.ShowPracticeEnd("TUTORIAL COMPLETE", reward > 0 ? "+" + reward + " SP · YOU'RE READY" : "YOU'RE READY TO PLAY", false);
        }

        /// <summary>Coach Dee's one-time tips during your first games (normal games only).</summary>
        private void UpdateCoachTips()
        {
            if (Time.unscaledTime < _nextTipCheck || App.Career == null) return;
            _nextTipCheck = Time.unscaledTime + 0.25f;
            if (_practice != null || _horse != null || _tutorial != null || Demo || Versus || Coaching || _watch != null || _match.IsOver) return;
            int me = _match.ControlledIndex;
            int handler = _match.HolderIndex;
            bool onDefense = _match.OffenseTeam != _match.Setup.HumanTeam;
            int target = _match.HumanHasBall ? _match.PreviewPassTarget(ScreenToCourt(_controls.ScreenStick)) : -1;
            var s = new TipSituation
            {
                Live = _match.Phase == MatchPhase.Live,
                HumanHasBall = _match.HumanHasBall,
                OnDefense = onDefense,
                MustClear = _match.MustClear,
                AlleyOopOpen = target >= 0 && _match.IsAlleyOopTarget(target),
                HumanHotStreak = _match.Players[me].HotStreak,
                HeatThreshold = _match.Setup.Shot.heatThreshold,
                LastShotTooEarly = _tooEarly,
                LastShotTooLate = _tooLate,
                NearBallHandler = onDefense && handler >= 0 && Vec2.Distance(_match.Players[me].Position, _match.Players[handler].Position) < 2f,
                ShotClock = _match.Setup.Rules.shotClockSeconds < 99f ? _match.ShotClock : 0f,
                MatchTime = _match.Time,
                TightlyGuarded = _match.HumanHasBall && NearestOpponentDistance(me) < 1.3f,
                TeammateHasBall = _match.HumanTeamHasBall && !_match.HumanHasBall,
            };
            _tooEarly = _tooLate = false;
            var tip = CoachTips.Next(App.Career, s, Time.unscaledTime, ref _lastTipAt);
            if (tip == null) return;
            _hud.Toast(Loc.T("COACH:") + " " + Loc.T(tip.Text), 3.2f);
            Sfx(SfxId.Click, 0.6f, 1.3f);
        }

        private static string Pad(string letters) => letters.Length == 0 ? "-" : letters;

        private void SyncHorse()
        {
            if (_horse == null) return;
            if (_horse.Version != _horseSeen)
            {
                _horseSeen = _horse.Version;
                if (!string.IsNullOrEmpty(_horse.LastCall))
                {
                    _hud.Toast(_horse.LastCall, 1.6f);
                    Sfx(_horse.LastCall.Contains("GETS") ? SfxId.Whistle : SfxId.Click, 0.7f);
                }
            }
            bool show = _horse.Matching || _horse.IsCpuTurn;
            _horseMarker.enabled = show;
            if (show)
            {
                var at = _horse.Matching ? _horse.SpotToMatch : _horse.LastCpuSpot;
                _horseMarker.transform.position = CourtSpace.ToWorldSnapped(at);
                _horseMarker.color = new Color32(0xFF, 0xD1, 0x66, 255);
            }
        }

        private void ShowHorseEnd()
        {
            _finalShown = true;
            SetTouchVisible(false);
            bool youWon = _horse.Winner == 0;
            if (App.Career != null && _horse.Opponent == HorseOpponent.Cpu)
            {
                Career.RecordPractice(App.Career, 0, 0, 0, 0f, 0, 0, false, 0f, youWon);
                App.SaveCareer();
                App.ReportGameCenter();
            }
            if (youWon || _horse.Opponent == HorseOpponent.Friend) Sfx(SfxId.Fanfare, 0.8f);
            Audio.AudioManager.Voice("H-O-R-S-E!");
            _hud.ShowPracticeEnd(_horse.Name(_horse.Winner) + " WINS",
                                 _horse.Name(0) + " " + Pad(_horse.LettersOf(0)) + "  ·  " + _horse.Name(1) + " " + Pad(_horse.LettersOf(1)), youWon);
        }

        private void ShowPracticeEnd()
        {
            _finalShown = true;
            SetTouchVisible(false);
            if (_duel != null)
            {
                ShowDuelEnd();
                return;
            }
            if (IsThreeContest)
            {
                ShowThreeContestEnd();
                return;
            }
            if (Gauntlet.TryParse(_request.ContextId, out int gDay, out int gStation))
            {
                ShowGauntletEnd(gDay, gStation);
                return;
            }
            bool best = false;
            if (App.Career != null)
            {
                var p = _practice;
                best = Career.RecordPractice(App.Career,
                    p.Kind == DrillKind.FreeShoot ? p.Makes : 0,
                    p.Kind == DrillKind.FreeShoot ? p.BestStreak : 0,
                    p.Kind == DrillKind.PassingTargets ? p.PassScore : 0,
                    p.Kind == DrillKind.DribbleLane && p.Finished ? p.CourseTime : 0f,
                    p.Kind == DrillKind.ThreePoint ? p.ContestPoints : 0,
                    p.Kind == DrillKind.Lockdown ? p.Stops : 0,
                    p.ShootoutWon,
                    p.Kind == DrillKind.AroundTheWorld && p.Finished ? p.CourseTime : 0f);
                App.SaveCareer();
                App.ReportGameCenter();
            }
            if (best) Haptics.Success();
            Sfx(SfxId.Whistle, 0.7f);
            string endTitle = _practice.Kind == DrillKind.Shootout ? (_practice.ShootoutWon ? "YOU WIN THE SHOOTOUT" : "CPU WINS") : DrillTitle(_practice.Kind);
            if (_practice.ShootoutWon) Sfx(SfxId.Fanfare, 0.8f);
            _hud.ShowPracticeEnd(endTitle, _practice.ResultText().ToUpperInvariant(), best);
        }

        /// <summary>Phase 34: a Skills Gauntlet station: score it, add it to the run, and offer the next one.</summary>
        private void ShowGauntletEnd(int day, int station)
        {
            var p = _practice;
            int partial = p.Kind == DrillKind.AroundTheWorld ? p.WorldSpot : p.NextCone;
            int points = Gauntlet.Points(p.Kind, p.Makes, p.BestStreak, p.PassScore, p.CourseTime, p.Finished && p.CourseTime > 0f,
                                         p.ContestPoints, p.Stops, partial);
            var g = App.Career.gauntlet ?? (App.Career.gauntlet = new GauntletSaveData());
            int bestBefore = g.best;
            bool finished = Gauntlet.Record(g, day, station, points);
            App.SaveCareer();
            Sfx(SfxId.Whistle, 0.7f);
            if (finished)
            {
                int total = Gauntlet.Total(g);
                bool record = total > bestBefore;
                if (record) { Sfx(SfxId.Fanfare, 0.8f); Haptics.Success(); }
                App.ReportGameCenter();
                _gauntletNext = -1;
                _hud.ShowPracticeEnd("GAUNTLET: " + total + " POINTS", Gauntlet.StationName(p.Kind) + " +" + points + "  ·  TODAY'S BEST " + g.todayBest, record, "SEE RESULTS");
                return;
            }
            _gauntletNext = Gauntlet.NextStation(g, day);
            var next = Gauntlet.For(day)[Mathf.Clamp(_gauntletNext, 0, Gauntlet.Stations - 1)];
            _hud.ShowPracticeEnd(Gauntlet.StationName(p.Kind) + " +" + points,
                                 "STATION " + (station + 1) + "/" + Gauntlet.Stations + "  ·  RUN SO FAR " + Gauntlet.Total(g), false,
                                 "NEXT: " + Gauntlet.StationName(next));
        }

        /// <summary>The Gauntlet station REMATCH goes to (-1 = back to the Gauntlet screen).</summary>
        private int _gauntletNext = -1;

        private bool IsThreeContest => _request != null && _request.ContextId != null
                                       && _request.ContextId.StartsWith(AllStar.ThreeContext, System.StringComparison.Ordinal)
                                       && App.Career != null && App.Career.allStar.contest.kind == "three";

        /// <summary>All-Star 3-Point Contest: record the round, then back to the contest.</summary>
        private void ShowThreeContestEnd()
        {
            var contest = App.Career.allStar.contest;
            int round = contest.round;
            AllStar.RecordThrees(contest, App.Catalog, _practice.ContestPoints);
            App.SaveCareer();
            Sfx(SfxId.Whistle, 0.7f);
            var me = AllStar.You(contest);
            string title;
            if (contest.round >= 3) title = contest.youWon ? "3-POINT CHAMPION!" : contest.championName.ToUpperInvariant() + " WINS IT";
            else title = round == 1 && AllStar.InFinal(contest, me) ? "YOU'RE IN THE FINAL" : "ROUND DONE";
            if (contest.youWon) { Sfx(SfxId.Fanfare, 0.8f); Haptics.Success(); }
            App.OpenAllStar = true;
            _hud.ShowPracticeEnd(title, _practice.ContestPoints + " POINTS", false, "CONTINUE");
        }

        /// <summary>Pass-and-play Shootout: hand over to P2 after round one, or show who won.</summary>
        private void ShowDuelEnd()
        {
            _duel.Record(_practice.ContestPoints);
            Sfx(SfxId.Whistle, 0.7f);
            if (!_duel.Finished)
            {
                _hud.ShowPracticeEnd("P1: " + _duel.Points[0] + " POINTS", "PASS THE PHONE TO P2", false, "P2: GO");
                return;
            }
            Sfx(SfxId.Fanfare, 0.8f);
            Haptics.Success();
            if (App.Career != null)
            {
                App.Career.totals.versusGames++;
                App.SaveCareer();
                App.ReportGameCenter();
            }
            _hud.ShowPracticeEnd(_duel.ResultTitle, _duel.ResultLine, false, "NEW DUEL");
        }

        // ------------------------------------------------------------------ practice markers

        private void BuildPracticeMarkers(Transform world)
        {
            // Dribble Lane cones, or the 3-Point Contest's money-ball spots.
            var spots = _practice.ThreePointStyle ? _practice.MoneySpots
                      : _practice.Kind == DrillKind.AroundTheWorld ? _practice.WorldSpots : _practice.Cones;
            _cones = new SpriteRenderer[spots.Count];
            for (int i = 0; i < _cones.Length; i++)
            {
                var go = new GameObject("Cone " + i);
                go.transform.SetParent(world, false);
                go.transform.position = CourtSpace.ToWorldSnapped(spots[i]);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _art.Ring;
                sr.sortingOrder = -9000;
                _cones[i] = sr;
            }
            if (_practice.Kind == DrillKind.PassingTargets)
            {
                var go = new GameObject("TargetArrow");
                go.transform.SetParent(world, false);
                _targetArrow = go.AddComponent<SpriteRenderer>();
                _targetArrow.sprite = _art.Arrow;
                _targetArrow.color = new Color32(0xFF, 0xD1, 0x66, 255);
                _targetArrow.sortingOrder = 31400;
            }
        }

        private void SyncPracticeMarkers()
        {
            if (_practice == null) return;
            if (_cones != null)
            {
                for (int i = 0; i < _cones.Length; i++)
                {
                    if (_practice.Kind == DrillKind.AroundTheWorld)
                    {
                        // Done spots green, the current one gold, the rest faint.
                        _cones[i].color = i < _practice.WorldSpot ? new Color(0.3f, 0.9f, 0.5f, 0.5f)
                                        : i == _practice.WorldSpot ? (Color)new Color32(0xFF, 0xD1, 0x66, 255) : new Color(1f, 1f, 1f, 0.25f);
                        continue;
                    }
                    if (_practice.ThreePointStyle)
                    {
                        // The money-ball spot glows gold; the rest are faint.
                        _cones[i].color = i == _practice.MoneySpot ? (Color)new Color32(0xFF, 0xD1, 0x66, 255) : new Color(1f, 1f, 1f, 0.25f);
                        continue;
                    }
                    bool done = i < _practice.NextCone;
                    bool next = i == _practice.NextCone;
                    _cones[i].color = done ? new Color(0.3f, 0.9f, 0.5f, 0.5f)
                                          : (next ? new Color32(0xFF, 0x8C, 0x42, 255) : new Color(1f, 0.55f, 0.26f, 0.45f));
                }
            }
            if (_targetArrow != null)
            {
                int t = _practice.TargetPlayer;
                _targetArrow.enabled = t >= 0;
                if (t >= 0)
                {
                    float bob = Mathf.Round(Mathf.Sin(Time.unscaledTime * 6f) * 2f) / CourtSpace.PixelsPerUnit;
                    _targetArrow.transform.position = CourtSpace.ToWorldSnapped(_match.Players[t].Position) + new Vector3(0f, 2.1f + bob, 0f);
                }
            }
        }

        // ------------------------------------------------------------------ helpers

        // ------------------------------------------------------------------ replay

        /// <summary>Big plays by a person (dunks, greens, blocks, steals) can be replayed.</summary>
        private ScoutedStyle _scoutShown;

        /// <summary>Phase 40: tell the player once when the defence has worked out how they play.</summary>
        private void NoteScouting()
        {
            if (Demo || _practice != null || _tutorial != null || _horse != null || _match.Setup.Coach) return;
            var style = _match.ScoutOf(_match.ControlledIndex);
            if (style == ScoutedStyle.None || style == _scoutShown) return;
            _scoutShown = style;
            _hud.Toast(Loc.T(style == ScoutedStyle.Shooter ? "SCOUTED: THEY'RE CROWDING YOUR JUMPER" : "SCOUTED: THEY'RE WALLING OFF THE DRIVE"), 1.6f);
        }

        private void NoteHighlight(MatchEvent e)
        {
            if (_practice != null || _horse != null || _tutorial != null || Demo || e.PlayerIndex < 0) return;
            // Alley-oops count if either end (passer or finisher) is a person; the clip is saved when the dunk lands.
            if (e.Type == MatchEventType.AlleyOop && (_match.IsHumanControlled(e.PlayerIndex) || _match.IsHumanControlled(e.Value)))
            {
                _oopFinisher = e.PlayerIndex;
                return;
            }
            if (e.Type == MatchEventType.ShotMade && e.PlayerIndex == _oopFinisher)
            {
                _oopFinisher = -1;
                _recorder.OfferBestPlay("ALLEY-OOP!", 95);
                OfferPhoto("ALLEY-OOP!", 95);
                _lastHighlight = _recorder.Snapshot(3.5f, "ALLEY-OOP!", 95);
                if (_link == null) _hud.OfferReplay(3f);
                return;
            }
            if (e.Type == MatchEventType.ShotMissed || e.Type == MatchEventType.Block) _oopFinisher = -1;
            if (!_match.IsHumanControlled(e.PlayerIndex)) return;
            int score = ReplayRecorder.PlayScore(e.Type, _match.Ball.ShotType, _match.Ball.ShotGrade, e.Type == MatchEventType.ShotMade ? e.Value : 0);
            if (score <= 0) return;
            string label = e.Type == MatchEventType.Block ? "BLOCKED!" : e.Type == MatchEventType.Steal ? "STEAL!"
                         : _match.Ball.ShotType == ShotType.Dunk ? "SLAM!" : "SWISH";
            _recorder.OfferBestPlay(label, score);
            if (score >= 50) OfferPhoto(label, score);
            if (score >= 50)
            {
                _lastHighlight = _recorder.Snapshot(3.5f, label, score);
                if (_link == null) _hud.OfferReplay(3f);
            }
        }

        private int _oopFinisher = -1;

        // ------------------------------------------------------------------ 14 Oct 2026: quick chat

        private float _chatReadyAt;

        private void SendChat(int id)
        {
            string text = QuickChat.Text(id);
            if (text == null || _link == null || _wire == null || _wire.State != LinkState.Connected) return;
            if (Time.unscaledTime < _chatReadyAt) { _hud.Toast("ONE MOMENT", 0.6f); return; }
            _chatReadyAt = Time.unscaledTime + QuickChat.Cooldown;
            _wire.Send(LinkProtocol.Chat(id));
            QuickChatLog.Add("YOU", text, true);
            _hud.ShowChat(Loc.T("YOU") + ": " + Loc.T(text));
            Sfx(SfxId.Click, 0.6f);
        }

        private void ShowIncomingChat()
        {
            _hud.TickChat();
            if (_link == null) return;
            bool on = App.Career == null || App.Career.phone.quickChat;
            while (_link.Chats.Count > 0)
            {
                string text = QuickChat.Text(_link.Chats.Dequeue());
                if (text == null || !on) continue;
                string from = string.IsNullOrEmpty(_wire?.PeerName) ? "THEM" : _wire.PeerName.ToUpperInvariant();
                if (from.Length > 14) from = from.Substring(0, 14);
                QuickChatLog.Add(from, text, false);
                _hud.ShowChat(from + ": " + Loc.T(text));
                Sfx(SfxId.Click, 0.8f, 1.3f);
                Haptics.Light();
            }
        }

        // ------------------------------------------------------------------ 14 Oct 2026: PLAY OF THE GAME photo for the FEED

        private int _photoScore;
        private string _photoLabel;
        private byte[] _photoPng;

        /// <summary>Takes a small still of the screen at your best play so far (end of this frame).</summary>
        private void OfferPhoto(string label, int score)
        {
            if (score <= _photoScore || _link != null || _watch != null || Demo || _capturing) return;
            _photoScore = score;
            _photoLabel = label;
            StartCoroutine(CapturePhoto());
        }

        private System.Collections.IEnumerator CapturePhoto()
        {
            // A beat later, so the ball is at the rim.
            yield return new WaitForSecondsRealtime(0.12f);
            yield return new WaitForEndOfFrame();
            RenderTexture screen = null, small = null;
            Texture2D read = null;
            try
            {
                int w = 360, h = Mathf.Max(1, Mathf.RoundToInt(w * Screen.height / (float)Mathf.Max(1, Screen.width)));
                screen = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
                small = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
                ScreenCapture.CaptureScreenshotIntoRenderTexture(screen);
                // Flip if this platform's capture comes out upside down.
                if (SystemInfo.graphicsUVStartsAtTop) Graphics.Blit(screen, small, new Vector2(1f, -1f), new Vector2(0f, 1f));
                else Graphics.Blit(screen, small);
                // Crop a square around the action: the court sits in the upper middle of the screen.
                int side = Mathf.Min(w, h);
                int y0 = Mathf.Clamp(Mathf.RoundToInt(h * 0.42f) - side / 2, 0, h - side);
                var prev = RenderTexture.active;
                RenderTexture.active = small;
                read = new Texture2D(side, side, TextureFormat.RGB24, false);
                read.ReadPixels(new Rect(0, y0, side, side), 0, 0, false);
                read.Apply(false);
                RenderTexture.active = prev;
                _photoPng = read.EncodeToPNG();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Retro Hoops] Play-of-the-game photo failed: " + e.Message);
            }
            finally
            {
                if (screen != null) Destroy(screen);
                if (small != null) Destroy(small);
                if (read != null) Destroy(read);
            }
        }

        /// <summary>After a game that counts: the FEED's made-up posts about it, with the photo.</summary>
        private void PostToFeed(MatchSummary summary, bool title)
        {
            try
            {
                var d = App.Career;
                if (d == null || _practice != null || _horse != null || _tutorial != null || Demo) return;
                string photo = null;
                if (_photoPng != null)
                {
                    System.IO.Directory.CreateDirectory(FeedPhotos.Folder);
                    photo = "potg-" + StableHash.Of(summary.matchId).ToString("x8") + ".png";
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(FeedPhotos.Folder, photo), _photoPng);
                }
                int chapter = _request.ContextId != null && _request.ContextId.StartsWith(StoryMode.ContextPrefix)
                    && int.TryParse(_request.ContextId.Substring(StoryMode.ContextPrefix.Length), out int n) ? n : 0;
                var posts = SocialFeed.PostsFor(summary, new FeedContext
                {
                    BestPlay = _photoPng != null ? _photoLabel : null,
                    Photo = photo,
                    Fans = d.fans,
                    StoryChapter = chapter,
                    Title = title,
                });
                FeedPhotos.Delete(SocialFeed.Add(d.phone, posts));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Retro Hoops] Feed post failed: " + e.Message);
            }
        }

        private void StartReplay(ReplayClip clip)
        {
            if (clip == null || clip.Frames.Count < 2) return;
            _replay = clip;
            _replayT = 0f;
            SetTouchVisible(false);
            _hud.ShowCallMenu(false);
            _hud.ShowReplayOverlay(true);
        }

        private void StepReplay()
        {
            // Replays are recorded as drawn on the fixed court.
            CourtSpace.Flip = false;
            float dt = Time.unscaledDeltaTime;
            _replayT += dt * ReplaySpeed;
            bool skip = false;
#if ENABLE_INPUT_SYSTEM
            var pointer = UnityEngine.InputSystem.Pointer.current;
            skip = pointer != null && pointer.press.wasPressedThisFrame && _replayT > 0.3f;
            var kb = Keyboard.current;
            skip |= kb != null && (kb.escapeKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame);
            var gp = Gamepad.current;
            skip |= gp != null && _replayT > 0.3f && (gp.buttonSouth.wasPressedThisFrame || gp.buttonEast.wasPressedThisFrame);
#endif
            if (skip || _replayT > _replay.Duration + 0.4f)
            {
                EndReplay();
                return;
            }
            var f = ReplayRecorder.FrameAt(_replay, _replayT);
            for (int i = 0; i < _playerViews.Length && i < f.Players.Length; i++)
                _playerViews[i].SyncReplay(dt * ReplaySpeed, f.Players[i], _match.IsHumanControlled(i));
            _replayBall.Phase = f.BallPhase;
            _replayBall.HolderIndex = f.BallHolder;
            _replayBall.Position = f.BallPosition;
            _replayBall.Height = f.BallHeight;
            int holderOrder = f.BallHolder >= 0 ? CourtSpace.SortingOrder(f.Players[f.BallHolder].Position) : 0;
            _ballView.Sync(_replayBall, holderOrder);
            _meter.Idle();
            _receiverArrow.enabled = false;
            _cameraRig.Follow(f.BallPosition, dt, false);
        }

        /// <summary>Phase 40 SHARE RESULT: the final score card as a picture through the share sheet.</summary>
        private void ShareResultCard()
        {
            try
            {
                var summary = MatchSummary.From(_match, _request.Mode, "share");
                var setup = _match.Setup;
                string title = _match.Winner < 0 ? "TIE" : ((_match.Winner == 0 ? setup.TeamA : setup.TeamB)?.FullName ?? "").ToUpperInvariant() + " WIN";
                var card = Logic.PixelArt.ResultCard.Render(summary, title);
                var tex = Utilities.TextureFactory.ToReadableTexture(card, "share.result");
                byte[] png = tex.EncodeToPNG();
                Destroy(tex);
                string path = System.IO.Path.Combine(Application.temporaryCachePath, "retro-hoops-result.png");
                System.IO.File.WriteAllBytes(path, png);
                Core.Share.File(path, summary.teamAName + " " + summary.scoreA + "-" + summary.scoreB + " " + summary.teamBName + " · Retro Hoops");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Retro Hoops] Couldn't share the result: " + e.Message);
            }
        }

        /// <summary>Plays the play of the game once more while recording it, then saves a GIF and opens the share sheet.</summary>
        private void ShareHighlight()
        {
            if (_capturing || _recorder.BestPlay == null) return;
            _replayQueue.Clear();
            _capturing = true;
            _gifFrames.Clear();
            StartReplay(_recorder.BestPlay);
            StartCoroutine(CaptureHighlight());
        }

        private System.Collections.IEnumerator CaptureHighlight()
        {
            int w = HighlightClip.Width, h = HighlightClip.HeightFor(Screen.width, Screen.height);
            var screen = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
            var small = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
            var read = new Texture2D(w, h, TextureFormat.RGB24, false);
            float nextAt = 0f;
            while (Replaying && _gifFrames.Count < HighlightClip.MaxFrames)
            {
                yield return new WaitForEndOfFrame();
                if (_replayT < nextAt) continue;
                nextAt += 1f / HighlightClip.Fps;
                ScreenCapture.CaptureScreenshotIntoRenderTexture(screen);
                Graphics.Blit(screen, small);
                var prev = RenderTexture.active;
                RenderTexture.active = small;
                read.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
                read.Apply(false);
                RenderTexture.active = prev;
                var raw = read.GetRawTextureData();
                // GIF rows go top to bottom; flip unless the capture already came out top-down.
                var frame = new byte[w * h * 3];
                bool flip = !SystemInfo.graphicsUVStartsAtTop;
                for (int y = 0; y < h; y++)
                    System.Buffer.BlockCopy(raw, (flip ? h - 1 - y : y) * w * 3, frame, y * w * 3, w * 3);
                _gifFrames.Add(frame);
            }
            Destroy(screen);
            Destroy(small);
            Destroy(read);
            if (Replaying) EndReplay();

            if (_gifFrames.Count < 2)
            {
                _capturing = false;
                _hud.Toast("COULDN'T RECORD THE HIGHLIGHT", 1.4f);
                yield break;
            }
            _hud.Toast("SAVING HIGHLIGHT...", 1.2f);
            var frames = new System.Collections.Generic.List<byte[]>(_gifFrames);
            _gifFrames.Clear();
            string folder = System.IO.Path.Combine(Application.persistentDataPath, "Highlights");
            string path = System.IO.Path.Combine(folder, HighlightClip.FileName(System.DateTime.Now));
            // Encode off the main thread (pure C#), then share on the main thread.
            var task = System.Threading.Tasks.Task.Run(() =>
            {
                System.IO.Directory.CreateDirectory(folder);
                System.IO.File.WriteAllBytes(path, GifEncoder.Encode(w, h, frames, 100 / HighlightClip.Fps));
            });
            while (!task.IsCompleted) yield return null;
            _capturing = false;
            if (task.IsFaulted)
            {
                _hud.Toast("COULDN'T SAVE THE HIGHLIGHT", 1.4f);
                Debug.LogWarning("[Retro Hoops] Highlight GIF failed: " + task.Exception?.GetBaseException().Message);
                yield break;
            }
            Haptics.Success();
            Share.File(path, "My play of the game in Retro Hoops");
        }

        private void EndReplay()
        {
            _replay = null;
            if (_replayQueue.Count > 0 && _finalShown)
            {
                // Next play in the HIGHLIGHTS reel.
                var next = _replayQueue[0];
                _replayQueue.RemoveAt(0);
                StartReplay(next);
                if (_replay != null) return;
            }
            _accumulator = 0f;
            _hud.ShowReplayOverlay(false);
            if (!_finalShown && !_paused) SetTouchVisible(!_padMode);
            SyncViews(0f, snapCamera: true);
        }

        /// <summary>Big centre callout with a musical sting (the game's "announcer").</summary>
        private void Callout(string text, SfxId sting)
        {
            _hud.Toast(text, 1.4f);
            Sfx(sting, 0.8f);
            Audio.AudioManager.Voice(text);
        }

        // ------------------------------------------------------------------ crowd

        /// <summary>Animated fans in the stands behind the baseline; they cheer and groan with the game.</summary>
        private void BuildCrowd(Transform world, CourtDef court, MatchSetup setup, CourtGeometry g, bool bannerUp)
        {
            if (court.crowdDensity <= 0f) return;
            int texW = CourtGenerator.TextureWidth(g);
            CourtGenerator.CourtToPixel(g, Vec2.Zero, out float originPx, out float originPy);
            var rng = new SeededRandom(StableHash.Of(court.id + ":crowd"));
            var skins = CharacterSpriteGenerator.SkinTones;
            RgbColor[] shirts =
            {
                setup.TeamA.primary, setup.TeamA.secondary, setup.TeamB.primary,
                court.skyBottom.Darken(0.2f), court.paint, court.lines.Darken(0.3f),
            };
            var sprites = new (Sprite idle, Sprite cheer)[shirts.Length * 4];
            for (int i = 0; i < sprites.Length; i++)
            {
                var shirt = shirts[i % shirts.Length];
                var skin = skins[(i * 7) % skins.Length];
                int look = (i * 5 + i / shirts.Length) % CrowdGenerator.Variants;
                sprites[i] = (_art.CrowdFan(shirt, skin, false, look), _art.CrowdFan(shirt, skin, true, look));
            }

            var fans = new System.Collections.Generic.List<Fan>();
            var parent = new GameObject("Crowd").transform;
            parent.SetParent(world, false);
            const float ppu = CourtSpace.PixelsPerUnit;
            for (int row = 0; row < 3; row++)
            {
                int bottom = CourtGenerator.CrowdRowBottom(g, row);
                for (int x = 2; x < texW - 6; x += 6)
                {
                    if (!rng.Chance(court.crowdDensity)) continue;
                    if (bannerUp && CourtGenerator.BannerCovers(g, texW, x, bottom, CrowdGenerator.Width, CrowdGenerator.Height)) continue;
                    var pick = sprites[rng.Range(0, sprites.Length)];
                    var go = new GameObject("Fan");
                    go.transform.SetParent(parent, false);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = pick.idle;
                    sr.sortingOrder = -9500 - row; // above the court, behind everything that moves
                    var pos = new Vector3((x - originPx) / ppu, (bottom - originPy) / ppu, 0f);
                    go.transform.position = pos;
                    fans.Add(new Fan { Renderer = sr, Idle = pick.idle, Cheer = pick.cheer, Base = pos });
                }
            }
            _fans = fans.ToArray();
        }

        /// <summary>
        /// 13 Oct 2026: portrait half court. Below the court art: an LED board (arenas) or a low wall (outdoors), a
        /// front row of fans seen from behind (they cheer with the rest of the crowd), and steps fading to a dark
        /// colour under the buttons.
        /// </summary>
        private void BuildNearSide(Transform world, CourtDef court, MatchSetup setup, CourtGeometry g)
        {
            const float ppu = CourtSpace.PixelsPerUnit;
            int texW = CourtGenerator.TextureWidth(g);
            CourtGenerator.CourtToPixel(g, Vec2.Zero, out float originPx, out float originPy);
            var canvas = CourtsideGenerator.NearSide(court, texW, setup.TeamA.primary, setup.TeamB.primary);
            int pad = CourtsideGenerator.SidePad;
            // Top edge of the near side = bottom edge of the court art (court pixel row 0).
            var pivot = new Vector2((originPx + pad) / canvas.Width, 1f);
            var go = new GameObject("NearSide");
            go.transform.SetParent(world, false);
            go.transform.localPosition = new Vector3(0f, -originPy / ppu, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _art.NearSide(canvas, pivot);
            sr.sortingOrder = -9990;

            if (court.crowdDensity <= 0f) return;
            var skins = CharacterSpriteGenerator.SkinTones;
            RgbColor[] shirts =
            {
                setup.TeamA.primary, setup.TeamA.secondary, setup.TeamB.primary, setup.TeamB.secondary,
                court.paint, court.lines.Darken(0.3f),
            };
            var sprites = new (Sprite idle, Sprite cheer)[shirts.Length * 2];
            for (int i = 0; i < sprites.Length; i++)
            {
                var shirt = shirts[i % shirts.Length];
                var skin = skins[(i * 5 + 3) % skins.Length];
                int look = i * 3 + i / shirts.Length;
                sprites[i] = (_art.FrontRowFan(shirt, skin, false, look), _art.FrontRowFan(shirt, skin, true, look));
            }
            var fans = new System.Collections.Generic.List<Fan>(_fans);
            var parent = new GameObject("FrontRow").transform;
            parent.SetParent(world, false);
            var rng = new SeededRandom(StableHash.Of(court.id + ":frontrow"));
            float feetY = -originPy / ppu - CourtsideGenerator.FanRowFromTop / ppu;
            foreach (int slot in CourtsideGenerator.FanSlots(texW, StableHash.Of(court.id), court.crowdDensity))
            {
                var pick = sprites[rng.Range(0, sprites.Length)];
                var fgo = new GameObject("Fan");
                fgo.transform.SetParent(parent, false);
                var fsr = fgo.AddComponent<SpriteRenderer>();
                fsr.sprite = pick.idle;
                fsr.sortingOrder = -9980;
                var pos = new Vector3((slot - pad - originPx) / ppu, feetY, 0f);
                fgo.transform.position = pos;
                fans.Add(new Fan { Renderer = fsr, Idle = pick.idle, Cheer = pick.cheer, Base = pos });
            }
            _fans = fans.ToArray();
        }

        /// <summary>
        /// Landscape: bleachers along the far sideline (top of the screen) with cheering fans, since the
        /// baseline stands are side-on in this view.
        /// </summary>
        private void BuildSidelineCrowd(Transform world, CourtDef court, MatchSetup setup)
        {
            const float ppu = CourtSpace.PixelsPerUnit;
            float length = setup.Court.depth + CourtGenerator.BaselineMargin * 2f;
            int widthPx = Mathf.CeilToInt(length * ppu);
            var canvas = CrowdGenerator.SidelineStands(widthPx, court.skyTop, court.floor.Lighten(0.12f), court.paint);
            var standsGo = new GameObject("SidelineStands");
            standsGo.transform.SetParent(world, false);
            var sr = standsGo.AddComponent<SpriteRenderer>();
            sr.sprite = _art.SidelineStands(canvas);
            sr.sortingOrder = -9600;
            var origin = new Vector3(-length * 0.5f, setup.Court.HalfWidth + CourtGenerator.SideMargin, 0f);
            standsGo.transform.position = origin;

            var rng = new SeededRandom(StableHash.Of(court.id + ":sideline"));
            var skins = CharacterSpriteGenerator.SkinTones;
            RgbColor[] shirts = { setup.TeamA.primary, setup.TeamA.secondary, setup.TeamB.primary, setup.TeamB.secondary, court.paint, court.lines.Darken(0.3f) };
            var sprites = new (Sprite idle, Sprite cheer)[shirts.Length * 4];
            for (int i = 0; i < sprites.Length; i++)
            {
                var shirt = shirts[i % shirts.Length];
                var skin = skins[(i * 5) % skins.Length];
                int look = (i * 5 + i / shirts.Length) % CrowdGenerator.Variants;
                sprites[i] = (_art.CrowdFan(shirt, skin, false, look), _art.CrowdFan(shirt, skin, true, look));
            }
            var fans = new System.Collections.Generic.List<Fan>();
            var parent = new GameObject("SidelineCrowd").transform;
            parent.SetParent(world, false);
            float density = Mathf.Clamp01(Mathf.Max(0.35f, court.crowdDensity * 1.3f));
            for (int row = CrowdGenerator.StandRows - 1; row >= 0; row--)
            {
                int floor = CrowdGenerator.StandRowFloor(row);
                for (int x = 2 + (row % 2) * 3; x < widthPx - CrowdGenerator.Width; x += 6)
                {
                    if (!rng.Chance(density)) continue;
                    var pick = sprites[rng.Range(0, sprites.Length)];
                    var go = new GameObject("Fan");
                    go.transform.SetParent(parent, false);
                    var fanSr = go.AddComponent<SpriteRenderer>();
                    fanSr.sprite = pick.idle;
                    fanSr.sortingOrder = -9500 - row; // front row in front of the rows behind it
                    var pos = origin + new Vector3(x / ppu, floor / ppu, 0f);
                    go.transform.position = pos;
                    fans.Add(new Fan { Renderer = fanSr, Idle = pick.idle, Cheer = pick.cheer, Base = pos });
                }
            }
            _fans = fans.ToArray();
        }

        private void SetCrowd(CrowdMood mood, float seconds)
        {
            _crowdMood = mood;
            _crowdMoodUntil = Time.unscaledTime + seconds;
        }

        private void SyncCrowd(float now)
        {
            if (_fans.Length == 0) return;
            if (_crowdMood != CrowdMood.Idle && now > _crowdMoodUntil) _crowdMood = CrowdMood.Idle;
            const float px = 1f / CourtSpace.PixelsPerUnit;
            bool still = _reduceMotion || Core.PowerMonitor.SavingPower;
            for (int i = 0; i < _fans.Length; i++)
            {
                ref var f = ref _fans[i];
                bool armsUp = _crowdMood == CrowdMood.Cheer && (i % 3 != 0);
                int bob = still ? 0 : CrowdGenerator.Bob(_crowdMood, now, i);
                // Phase 35: the wave rolls through on a run; on a close finish everyone's up and bouncing.
                if (!still && _crowdMood != CrowdMood.Groan)
                {
                    int lift = ArenaLift(i, now, out bool waving);
                    bob = Mathf.Max(bob, lift);
                    armsUp |= waving;
                }
                f.Renderer.sprite = armsUp ? f.Cheer : f.Idle;
                f.Renderer.transform.position = f.Base + new Vector3(0f, bob * px, 0f);
            }
            SyncFlashes(now);
            SyncCrowdSound();
        }

        private static CosmeticDef Cosmetic(string id)
        {
            if (string.IsNullOrEmpty(id) || App.Catalog == null) return null;
            return App.Catalog.Find(App.Catalog.Cosmetics, id);
        }

        /// <summary>Colourblind mode: each team wears a distinct jersey pattern, not just a colour.</summary>
        private static TeamPattern PatternFor(int team, MatchSetup s)
        {
            var a = s.TeamA.pattern == TeamPattern.Solid ? TeamPattern.Stripes : s.TeamA.pattern;
            if (team == 0) return a;
            var b = s.TeamB.pattern == TeamPattern.Solid ? TeamPattern.Dots : s.TeamB.pattern;
            if (b == a) b = a == TeamPattern.Dots ? TeamPattern.Checker : TeamPattern.Dots;
            return b;
        }

        private static void Sfx(SfxId id, float volume = 1f, float pitch = 1f) => Audio.AudioManager.Play(id, volume, pitch);

        private void Continue()
        {
            if (_request.Mode == GameMode.AllStar)
            {
                App.OpenAllStar = true;
                SceneFlow.GoTo(AllStarHost());
                return;
            }
            if (ContinueToCity()) return;
            SceneFlow.GoTo(_request.Mode == GameMode.Rise || _request.Mode == GameMode.Rival ? SceneNames.Season : SceneNames.MainMenu);
        }

        /// <summary>The All-Star Weekend lives in the Rise hub while it's open; otherwise the contests are on the main menu.</summary>
        private static string AllStarHost() => App.Career != null && AllStar.WeekendOpen(App.Career) ? SceneNames.Season : SceneNames.MainMenu;

        private PhotoModeView _photo;

        /// <summary>Photo mode from the pause menu: the game stays frozen until DONE brings the pause menu back.</summary>
        private void OpenPhotoMode()
        {
            if (_photo != null || !_paused || Demo) return;
            _hud.ShowPause(false);
            _hud.SetHudVisible(false);
            if (_streetsHud != null) _streetsHud.SetVisible(false);
            SetTouchVisible(false);
            var s = _match.Setup;
            string caption = (s.TeamA?.abbreviation ?? "HOME") + " " + _match.Score[0] + "-" + _match.Score[1] + " " + (s.TeamB?.abbreviation ?? "AWAY")
                             + "  " + System.DateTime.Now.ToString("M/d/yy", System.Globalization.CultureInfo.InvariantCulture);
            _photo = PhotoModeView.Open(Camera.main != null ? Camera.main : _cameraRig.GetComponent<Camera>(), caption, () =>
            {
                _photo = null;
                _hud.SetHudVisible(true);
                if (_streetsHud != null) _streetsHud.SetVisible(true);
                _hud.ShowPause(true);
            });
        }

        private void SetPaused(bool paused)
        {
            if (_paused == paused) return;
            _paused = paused;
            _accumulator = 0f;
            _buffer.Clear();
            SetTouchVisible(!paused && !_padMode);
            _hud.ShowPause(paused);
            _coach?.SetVisible(!paused && !_finalShown);
            _theaterBar?.SetVisible(!paused && !_finalShown);
        }

        /// <summary>Modes that continue a run (no rematch button).</summary>
        private bool IsRun => _request.Mode == GameMode.Rise || _request.Mode == GameMode.Tournament || _request.Mode == GameMode.Rival
                              || _request.Mode == GameMode.King || _request.Mode == GameMode.Arcade || _request.Mode == GameMode.Cup
                              || _request.Mode == GameMode.Franchise || _request.Mode == GameMode.AllStar
                              || _request.Mode == GameMode.Legacy || _request.Mode == GameMode.CustomCup;

        private void Rematch()
        {
            if (_watch != null)
            {
                TapeStore.StartAt = 0;
                if (_watch.IsTape && TapeStore.Playing != null && TapeStore.PrepareToWatch(TapeStore.Playing, out _))
                {
                    _rematching = true;
                    SceneFlow.GoTo(SceneNames.Game);
                    return;
                }
                _hud.Toast("IF THEY PLAY AGAIN, IT SHOWS UP HERE", 2f);
                return;
            }
            if (_link != null)
            {
                if (!_linkEnded && LinkMatch.Setup != null && _wire.State == LinkState.Connected) StartLinkRematch();
                else Quit();
                return;
            }
            if (IsRun) { Continue(); return; }
            if (Gauntlet.TryParse(_request.ContextId, out int gDay, out _))
            {
                if (_gauntletNext > 0 && _gauntletNext < Gauntlet.Stations)
                {
                    App.PendingMatch = Gauntlet.Request(gDay, _gauntletNext);
                    SceneFlow.GoTo(SceneNames.Game);
                }
                else
                {
                    App.OpenGauntletOnMenu = true;
                    SceneFlow.GoTo(SceneNames.MainMenu);
                }
                return;
            }
            if (_request.ContextId != null && _request.ContextId.StartsWith(AllStar.ThreeContext, System.StringComparison.Ordinal))
            {
                App.OpenAllStar = true;
                SceneFlow.GoTo(AllStarHost());
                return;
            }
            _request.Seed = 0;
            App.PendingMatch = _request;
            SceneFlow.GoTo(SceneNames.Game);
        }

        /// <summary>SAVE GAME TAPE: the whole game (setup + both inputs every step) goes to 2 PLAYER ► GAME TAPES.</summary>
        private void SaveTape()
        {
            if (_tapeSaved) { _hud.Toast("ALREADY SAVED", 1.2f); return; }
            LinkSetup setup = _watch != null ? _watch.Setup : _feed?.Setup;
            var a = _watch != null ? _watch.TeamA : _feed?.TeamA;
            var b = _watch != null ? _watch.TeamB : _feed?.TeamB;
            if (setup == null || a == null || a.Count == 0) { _hud.Toast("NOTHING TO SAVE", 1.2f); return; }
            var tape = Tapes.From(setup, a, b, Tapes.TitleFor(setup, _match.Setup.TeamA.FullName, _match.Setup.TeamB.FullName),
                                  System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(), _match.Score[0], _match.Score[1]);
            _tapeSaved = TapeStore.Save(tape);
            _hud.Toast(_tapeSaved ? "TAPE SAVED: 2 PLAYER ► GAME TAPES" : "COULDN'T SAVE THE TAPE", 2f);
        }

        /// <summary>Couch Cup on two phones: the host records the game in its bracket; both say what's next.</summary>
        private string CupResult(ref string winner)
        {
            string nameA = (_cupSetup.HomeLabel ?? "PLAYER 1").ToUpperInvariant(), nameB = (_cupSetup.AwayLabel ?? "PLAYER 2").ToUpperInvariant();
            winner = _match.Winner < 0 ? "TIE: PLAY IT AGAIN" : (_match.Winner == 0 ? nameA : nameB) + " WINS";
            if (_link.Seat != 0) return "The bracket is on the host's phone. Pass the phones on, then you both tap REMATCH for the next game.";
            var cup = App.Career?.couch;
            if (cup == null) return null;
            var outcome = _cupRecorded ? CouchOutcome.NoGame : CouchCup.ReportLink(cup, _cupSetup.Cup, _match.Score[0], _match.Score[1]);
            _cupRecorded = true;
            if (outcome != CouchOutcome.NoGame) App.SaveCareer();
            App.OpenCouchOnMenu = true;
            if (outcome == CouchOutcome.Champion)
            {
                Sfx(SfxId.Fanfare);
                return cup.names[cup.champion].ToUpperInvariant() + " WINS THE COUCH CUP!";
            }
            if (outcome == CouchOutcome.TieReplay) return "Ties don't count in the Couch Cup. You both tap REMATCH to play it again.";
            var after = CouchCup.NextGame(cup);
            return after == null ? null
                 : "Next: " + cup.names[after.a] + " (this phone) vs " + cup.names[after.b] + " (other phone), " + CouchCup.RoundName(cup, after.round)
                   + ". Pass the phones on, then you both tap REMATCH.";
        }

        private void Quit()
        {
            HangUp();
            SceneFlow.GoTo(SceneNames.MainMenu);
        }

        // Backgrounding the app (call, home swipe) always pauses; the player resumes manually.
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && _match != null && !_match.IsOver) SetPaused(true);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && _match != null && !_match.IsOver && !Application.isEditor) SetPaused(true);
        }

        private void OnDestroy()
        {
            PowerMonitor.Gameplay = false;
            Speech.Stop();
            if (!_rematching) HangUp();
            UI.ControllerCursor.Suppressed = false;
            CourtSpace.Flip = false;
            if (_landscape)
            {
                CourtSpace.Landscape = false;
                // Straight into another landscape game (rematch / next Full Court game): stay sideways.
                var next = App.PendingMatch;
                bool nextSideways = next != null && next.Mode != GameMode.Versus && next.Mode != GameMode.Demo
                                    && (next.Mode == GameMode.FullCourt || next.FullCourt || (App.Career != null && App.Career.settings.landscapeAll));
                if (!nextSideways) Orientation.Portrait();
            }
            Audio.AudioManager.SetAmbience(false);
            Audio.AudioManager.PlayMusic(0);
            _art?.Dispose();
        }

        private static bool EscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            return (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame);
#else
            return false;
#endif
        }
    }
}
