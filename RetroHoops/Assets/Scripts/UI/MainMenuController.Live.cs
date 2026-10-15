using System;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CallerRetroBall.UI
{
    /// <summary>PLAY ► LIVE: free online matchmaking through Game Center (Phase 41: no subscription).</summary>
    public sealed partial class MainMenuController
    {
        private void ShowLive()
        {
            FirstVisit(Tours.Live);
            var c = App.Catalog;
            var career = App.Career;
            var live = career.live ?? (career.live = new LiveSaveData());
            var teams = c.TeamsInTier(TeamTier.League);
            int teamIndex = Mathf.Max(0, teams.FindIndex(t => t.id == live.teamId));

            // Phase 36 LIVE SEASONS: close last month's season and pay it once.
            int monthNow = LiveMode.MonthKey(System.DateTime.UtcNow);
            LiveSeason.Observe(live, monthNow, false);
            var paid = LiveSeason.Settle(career, c);
            if (paid != null) App.SaveCareer();

            bool streets = _streetsStyle;
            var column = OpenOverlay(streets ? "LIVE STREETS" : "LIVE", out var footer);
            if (streets)
                UiKit.Size(UiKit.Label(column, "THE STREETS online: 3 on 3 to 21 with tricks and SHOWSTOPPERS. You only meet other LIVE STREETS players. It counts for your Live rating.",
                                       28f, Theme.Gold), 100f);
            if (paid != null)
                UiKit.Size(UiKit.Label(column, "SEASON " + LiveSeason.MonthName(paid.Month) + " FINISHED " + paid.Tier + ": +" + paid.SignalPoints + " SP"
                                               + (paid.CosmeticId != null ? "  ·  LIVE SEASON STAR BANNER" : ""), 30f, Theme.Cyan, TextAlignmentOptions.Center, true), 60f);
            UiKit.Size(UiKit.Label(column, "RATING " + live.rating + "  ·  " + LiveMode.Tier(live.rating), 44f, Theme.Gold, TextAlignmentOptions.Center, true), 70f);
            UiKit.Size(UiKit.Label(column, "RECORD " + live.wins + "-" + live.losses + "  ·  BEST " + live.best, 30f, Theme.Cream, TextAlignmentOptions.Center, true), 50f);
            UiKit.Size(UiKit.Label(column, "Play someone online, head to head, one game. Win to climb; leaving a game early counts as a loss.", 28f, Theme.Muted), 90f);

            UiKit.Size(UiKit.Label(column, "YOUR TEAM", 30f, Theme.Gold, TextAlignmentOptions.Center, true), 50f);
            TextMeshProUGUI teamLabel = null;
            teamLabel = UiKit.Label(column, teams[teamIndex].FullName.ToUpperInvariant(), 40f, Theme.Cream, TextAlignmentOptions.Center, true);
            UiKit.Size(teamLabel, 60f);
            UiKit.Button(column, "CHANGE TEAM", () =>
            {
                teamIndex = (teamIndex + 1) % teams.Count;
                live.teamId = teams[teamIndex].id;
                teamLabel.text = teams[teamIndex].FullName.ToUpperInvariant();
                App.SaveCareer();
            }, ButtonStyle.Ghost, 90f, 34f);

            var status = UiKit.Label(column, "", 30f, Theme.Muted, TextAlignmentOptions.Center);
            UiKit.Size(status, 130f);

            bool signedIn = App.GameCenter.IsAvailable && App.GameCenter.IsSignedIn;
            if (!LiveLink.Supported) status.text = "Live games work on the iPhone, iPad and Mac versions of the game.";
            else if (!signedIn)
            {
                status.text = "Live uses Game Center to find opponents. Sign in first.";
                UiKit.Button(column, "SIGN IN TO GAME CENTER", () => { App.SetGameCenter(true); status.text = "Signing in… then tap FIND A GAME."; }, ButtonStyle.Secondary, 100f, 32f);
            }
            UiKit.Size(UiKit.Label(column, "SEASON " + LiveSeason.MonthName(monthNow) + ": BEST " + live.seasonBest + " (" + LiveMode.Tier(live.seasonBest) + ")  ·  "
                                           + live.seasonGames + "/" + LiveSeason.MinGames + " GAMES TO QUALIFY", 26f, Theme.Muted, TextAlignmentOptions.Center, true), 44f);
            if (signedIn)
                UiKit.Button(column, "FRIENDS THIS MONTH", () => App.GameCenter.ShowLeaderboard(LiveMode.MonthlyLeaderboardId, true), ButtonStyle.Secondary, 90f, 30f);
            LivePointsBlock(column, live, signedIn);
            if (BackendConfig.Enabled)
                UiKit.Button(column, "DELETE MY LIVE DATA", () => UiControls.Dialog("DELETE LIVE DATA",
                    "Erase your Live rating and record from the Retro Hoops server?",
                    ("DELETE", ButtonStyle.Primary, () => BackendClient.DeleteMyData(ok =>
                    {
                        if (ok) { live.rating = live.best = LiveMode.StartRating; live.wins = live.losses = live.games = 0; App.SaveCareer(); }
                        status.text = ok ? "Deleted." : "<color=#FF6B6B>Couldn't reach the server. Try again later.</color>";
                    })),
                    ("CANCEL", ButtonStyle.Ghost, null)), ButtonStyle.Ghost, 80f, 28f);

            Button find = null, invite = null;
            UiKit.Button(footer, "BACK", () => { LiveLink.Stop(); if (streets) ShowStreets(); else ShowPlayMenu(); }, ButtonStyle.Ghost, 130f, 44f);
            // Phase 33: FIND A GAME (anyone) or INVITE A FRIEND (Game Center friends), or an invite you accepted outside the game.
            void Go(int how)
            {
                if (!LiveLink.Supported) return;
                live.teamId = teams[teamIndex].id;
                App.SaveCareer();
                if (find != null) find.interactable = false;
                if (invite != null) invite.interactable = false;
                var team = teams[teamIndex];
                string me = LiveLink.LocalName;
                if (string.IsNullOrEmpty(me)) me = "Player"; // only the Game Center name is shown to opponents (no typed names online)
                var offer = new LiveOffer { AppVersion = App.Version, Name = me, Rating = live.rating, TeamData = LinkTeams.Write(c, team), CourtId = team.homeCourtId };
                if (streets)
                {
                    offer.Style = LinkSetup.StreetsStyle;
                    var courts = Streets.CourtList(c);
                    if (courts.Count > 0) offer.CourtId = courts[(int)((uint)System.Environment.TickCount % (uint)courts.Count)].id;
                }
                var link = how == 0 ? LiveLink.Find(App.Version, streets) : how == 1 ? LiveLink.Invite(App.Version, streets) : LiveLink.AdoptInvite();
                if (how > 0) status.text = how == 1 ? "Pick a friend in Game Center's invite screen…" : "Joining your friend's game…";
                LivePoller.Attach(column, link, offer, status, () => { if (find != null) find.interactable = true; if (invite != null) invite.interactable = true; });
            }
            invite = UiKit.Button(footer, "INVITE A FRIEND", () => Go(1), ButtonStyle.Secondary, 130f, 32f);
            find = UiKit.Button(footer, "FIND A GAME", () => Go(0), ButtonStyle.Primary, 130f);
            if (_joinInvite)
            {
                _joinInvite = false;
                Go(2);
            }

            // With the Retro Hoops Live server switched on, sign in to it first: it checks the subscription with
            // Apple and keeps the official ratings. FIND A GAME waits until it says yes.
            if (BackendConfig.Enabled && LiveLink.Supported && signedIn)
            {
                BackendClient.Connect();
                var watch = column.gameObject.AddComponent<BackendWatcher>();
                watch.Status = status;
                watch.Find = find;
                watch.OnReady = () => ShowLive();
            }
        }

        /// <summary>12 Oct 2026 LIVE POINTS: this month's points and division, the breakdown, and the two points boards.</summary>
        private void LivePointsBlock(RectTransform column, LiveSaveData live, bool signedIn)
        {
            LivePoints.Roll(live, System.DateTime.UtcNow);
            int month = LivePoints.MonthTotal(live);
            int next = LivePoints.ToNextDivision(month);
            UiKit.Size(UiKit.Label(column, Loc.T("LIVE POINTS") + " " + month + "  ·  " + Loc.T(LivePoints.Division(month)), 40f, Theme.Gold, TextAlignmentOptions.Center, true), 64f);
            UiKit.Size(UiKit.Label(column, Loc.T("WINS") + " " + live.pMonthWins + "  ·  " + Loc.T("SCORING") + " " + live.pMonthScoring + "  ·  " + Loc.T("TIME") + " " + live.pMonthActivity
                                           + (next > 0 ? "\n" + next + " " + Loc.T("to") + " " + Loc.T(LivePoints.Divisions[LivePoints.DivisionIndex(month) + 1]) : "")
                                           + "\n" + Loc.T("ALL-TIME") + " " + LivePoints.Total(live) + "  ·  " + Loc.T("BEST STREAK") + " " + live.pBestStreak
                                           + "  ·  " + Loc.T("TIME TODAY") + " " + live.pDayActivity + "/" + LivePoints.DailyActivityCap,
                                   26f, Theme.Cream, TextAlignmentOptions.Center), 120f);
            var row = UiKit.Row(column, 16f, "Points boards");
            UiKit.Size(row, 90f);
            UiKit.Button(row, "HOW POINTS WORK", () => UiControls.Dialog("LIVE POINTS",
                "Every Live game you finish earns points:\n• WIN: 100, plus up to 50 for beating a higher-rated player and 10 for each win in a row after the first (up to 50)\n"
                + "• SCORING: 1 per point your team scores (up to 50 a game)\n• TIME: 2 per minute played (up to 60 a day)\nQuitting earns nothing. Monthly points set your division and reset on the 1st.",
                ("OK", ButtonStyle.Primary, null)), ButtonStyle.Ghost, 90f, 26f);
            if (signedIn)
            {
                UiKit.Button(row, "POINTS THIS MONTH", () => App.GameCenter.ShowLeaderboard(LivePoints.MonthlyLeaderboardId, false), ButtonStyle.Secondary, 90f, 26f);
                UiKit.Button(row, "ALL-TIME", () => App.GameCenter.ShowLeaderboard(LivePoints.LeaderboardId, false), ButtonStyle.Secondary, 90f, 26f);
            }
        }

        /// <summary>Phase 33: set when a friend's invite was accepted outside the game; the next LIVE screen joins it.</summary>
        private static bool _joinInvite;

        /// <summary>Called from the main menu's Update: listen for invites, and open LIVE when one is accepted.</summary>
        private void PollLiveInvites()
        {
            if (!LiveLink.Supported || App.GameCenter == null || !App.GameCenter.IsSignedIn) return;
            LiveLink.ListenForInvites();
            if (!LiveLink.TakeInvite()) return;
            _joinInvite = true;
            ShowLive();
        }
    }

    /// <summary>Shows the Live server sign-in on the LIVE screen and holds FIND A GAME until it's done.</summary>
    public sealed class BackendWatcher : MonoBehaviour
    {
        public TextMeshProUGUI Status;
        public Button Find;
        public Action OnReady;
        private BackendState _last = (BackendState)(-1);
        private bool _wasReady;

        private void Start() => _wasReady = BackendClient.Ready;

        private void Update()
        {
            var s = BackendClient.State;
            if (s == _last) return;
            _last = s;
            if (GetComponent<LivePoller>() != null) return; // matchmaking owns the status line
            switch (s)
            {
                case BackendState.Connecting:
                    Status.text = "Signing in to Retro Hoops Live…";
                    Find.interactable = false;
                    break;
                case BackendState.Ready:
                    if (!Pro.LiveIsFree && !BackendClient.SubscriptionActive)
                    {
                        Status.text = "<color=#FF6B6B>The Live server couldn't confirm your subscription with Apple. Try RESTORE PURCHASES, then come back.</color>";
                        Find.interactable = false;
                    }
                    else
                    {
                        Find.interactable = true;
                        if (!_wasReady) OnReady?.Invoke(); // redraw with the server's rating
                    }
                    break;
                case BackendState.Failed:
                    Status.text = "<color=#FF6B6B>" + BackendClient.Error + "</color>";
                    Find.interactable = false;
                    break;
            }
        }
    }

    /// <summary>Keeps the Retro Hoops Pro screen in step with the App Store (price, purchase progress) and moves on once owned.</summary>
    public sealed class ProWatcher : MonoBehaviour
    {
        public TextMeshProUGUI Price, Terms, Status;
        public Action OnOwned;
        private string _lastPrice;
        private StoreState _last = (StoreState)(-1);
        private bool _done;

        private void Update()
        {
            if (_done) return;
            string p = ProStore.Price;
            if (p != _lastPrice)
            {
                _lastPrice = p;
                Price.text = p + "  ·  " + Loc.T("ONE TIME");
                Terms.text = Pro.Terms(p);
            }
            var s = ProStore.State;
            if (s == _last) return;
            _last = s;
            switch (s)
            {
                case StoreState.Owned:
                    _done = true;
                    ProStore.Remember();
                    OnOwned?.Invoke();
                    break;
                case StoreState.Purchasing: Status.text = Loc.T("Waiting for the App Store…"); break;
                case StoreState.Pending: Status.text = Loc.T("Waiting for approval (Ask to Buy). Pro unlocks as soon as it's approved."); break;
                case StoreState.Failed: Status.text = "<color=#FF6B6B>" + (string.IsNullOrEmpty(ProStore.Error) ? Loc.T("The purchase didn't go through.") : ProStore.Error) + "</color>"; break;
                case StoreState.NotOwned:
                    ProStore.Remember();
                    if (Status.text.StartsWith(Loc.T("Checking"), StringComparison.Ordinal)) Status.text = Loc.T("No Retro Hoops Pro purchase found on this Apple Account.");
                    break;
            }
        }
    }

    /// <summary>Runs Live matchmaking and the hand-shake while the LIVE screen is open; starts the game when ready.</summary>
    public sealed class LivePoller : MonoBehaviour
    {
        private LiveLink _link;
        private LiveOffer _offer;
        private LinkLobby _lobby;
        private TextMeshProUGUI _status;
        private Action _onFail;
        private bool _started;
        private float _since;

        public static LivePoller Attach(Transform host, LiveLink link, LiveOffer offer, TextMeshProUGUI status, Action onFail)
        {
            var old = host.GetComponent<LivePoller>();
            if (old != null) Destroy(old);
            var p = host.gameObject.AddComponent<LivePoller>();
            p._link = link;
            p._offer = offer;
            p._status = status;
            p._onFail = onFail;
            p._since = Time.unscaledTime;
            status.text = "Finding an opponent on Game Center…";
            return p;
        }

        private void Update()
        {
            if (_started || _link == null) return;
            PowerMonitor.Wake();
            var state = _link.State;
            if (state == LinkState.Lost)
            {
                Fail(string.IsNullOrEmpty(_link.Error) ? "Matchmaking stopped. Try again." : _link.Error);
                return;
            }
            if (state == LinkState.Searching || state == LinkState.Idle)
            {
                int secs = (int)(Time.unscaledTime - _since);
                _status.text = "Finding an opponent on Game Center… " + secs + "s\n<size=22>BACK to stop looking.</size>";
                return;
            }
            // Connected: the hand-shake picks who hosts and swaps teams.
            if (_lobby == null)
            {
                int seat = _link.Seat;
                _lobby = new LinkLobby(seat == 0, _offer, (uint)Environment.TickCount | 1u, LinkProtocol.ContentFingerprint(App.Catalog), App.Catalog);
                _status.text = "Found " + _link.PeerName + "! Getting ready…";
            }
            _lobby.Update(_link);
            switch (_lobby.Status)
            {
                case LobbyStatus.Started:
                    _started = true;
                    var setup = _lobby.Setup;
                    if (!setup.Register(App.Catalog))
                    {
                        Fail("Couldn't set up the teams.");
                        return;
                    }
                    LinkMatch.Transport = _link;
                    LinkMatch.LocalId = LiveLink.LocalId;
                    LinkMatch.OpponentId = _link.OpponentId;
                    LinkMatch.Seat = _lobby.IsHost ? 0 : 1;
                    LinkMatch.Setup = setup;
                    App.PendingMatch = setup.ToRequest();
                    _status.text = "TIP OFF vs " + (_lobby.IsHost ? setup.GuestName : setup.HostName) + "!";
                    SceneFlow.GoTo(SceneNames.Game);
                    break;
                case LobbyStatus.Rejected:
                case LobbyStatus.Lost:
                    Fail(_lobby.Error ?? "Lost the connection.");
                    break;
            }
        }

        private void Fail(string why)
        {
            _status.text = "<color=#FF6B6B>" + why + "</color>";
            LiveLink.Stop();
            _link = null;
            _onFail?.Invoke();
        }

        private void OnDestroy()
        {
            if (!_started && _link != null) LiveLink.Stop();
        }
    }
}
