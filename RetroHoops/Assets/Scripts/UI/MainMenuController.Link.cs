using System;
using System.Collections.Generic;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CallerRetroBall.UI
{
    /// <summary>2 PLAYER ► TWO PHONES: host a game or join a friend's nearby (no internet needed).</summary>
    public sealed partial class MainMenuController
    {
        private static string LinkName()
        {
            string nick = App.Career?.nickname;
            return (string.IsNullOrEmpty(nick) ? "Retro Hoops" : nick) + " · " + SystemInfo.deviceModel.Replace("iPhone", "iPhone ").Replace("  ", " ").Trim();
        }

        private void ShowLinkMenu()
        {
            var column = OpenOverlay(_streetsStyle ? "STREETS: TWO PHONES" : "TWO PHONES", out var footer);
            FirstVisit(Tours.TwoPhones);
            UiKit.Size(UiKit.Label(column,
                "Play head to head, each on your own iPhone or iPad. Both phones need Retro Hoops (the same version) and to be near each other with Wi-Fi or Bluetooth on. No internet, no account.",
                30f, Theme.Cream), 190f);
            if (!NearbyLink.Supported)
            {
                UiKit.Size(UiKit.Label(column, "Two-phone play works on iPhone, iPad and Mac builds of the game.", 30f, Theme.Muted, TextAlignmentOptions.Center), 100f);
            }
            else
            {
                Mode(column, "HOST A GAME", "Pick both teams, then wait for your friend to join.", ShowLinkHost, ButtonStyle.Primary);
                Mode(column, "JOIN A GAME", "Find your friend's phone nearby and join their game.", ShowLinkJoin, ButtonStyle.Secondary);
                Mode(column, "WATCH A GAME", "Watch two friends' game live on this phone (up to 3 watchers).", ShowLinkWatch, ButtonStyle.Secondary);
                UiKit.Size(UiKit.Label(column, "The first time, iOS asks to find devices on your local network: tap Allow on both phones.", 26f, Theme.Muted), 90f);
            }
            UiKit.Button(footer, "BACK", () => { if (_streetsStyle) ShowStreets(); else ShowVersus(); }, ButtonStyle.Ghost, 130f, 44f);
        }

        private static int _linkHome, _linkAway = 1;

        private void ShowLinkHost()
        {
            var c = App.Catalog;
            var teams = c.TeamsInTier(TeamTier.League);
            if (teams.Count < 2) return;
            _linkHome = Mathf.Clamp(_linkHome, 0, teams.Count - 1);
            _linkAway = Mathf.Clamp(_linkAway, 0, teams.Count - 1);
            if (_linkAway == _linkHome) _linkAway = (_linkHome + 1) % teams.Count;

            var column = OpenOverlay("HOST A GAME", out var footer);
            UiKit.Size(UiKit.Label(column, "YOU (PLAYER 1)", 34f, Theme.Gold, TextAlignmentOptions.Center, true), 50f);
            TextMeshProUGUI homeLabel = null, awayLabel = null;
            homeLabel = UiKit.Label(column, "", 40f, Theme.Cream, TextAlignmentOptions.Center, true);
            UiKit.Size(homeLabel, 60f);
            UiKit.Button(column, "CHANGE TEAM", () => { _linkHome = Next(_linkHome, _linkAway); Refresh(); }, ButtonStyle.Ghost, 90f, 34f);
            UiKit.Size(UiKit.Label(column, "YOUR FRIEND (PLAYER 2)", 34f, Theme.Cyan, TextAlignmentOptions.Center, true), 50f);
            awayLabel = UiKit.Label(column, "", 40f, Theme.Cream, TextAlignmentOptions.Center, true);
            UiKit.Size(awayLabel, 60f);
            UiKit.Button(column, "CHANGE TEAM", () => { _linkAway = Next(_linkAway, _linkHome); Refresh(); }, ButtonStyle.Ghost, 90f, 34f);
            var status = UiKit.Label(column, "", 32f, Theme.Muted, TextAlignmentOptions.Center);
            UiKit.Size(status, 130f);

            Button hostButton = null;
            UiKit.Button(footer, "BACK", () => { NearbyLink.Stop(); ShowLinkMenu(); }, ButtonStyle.Ghost, 130f, 44f);
            hostButton = UiKit.Button(footer, "WAIT FOR FRIEND", () =>
            {
                hostButton.interactable = false;
                var home = teams[_linkHome];
                uint seed = (uint)Environment.TickCount | 1u;
                var setup = LinkSetup.From(c, home.id, teams[_linkAway].id, home.homeCourtId, App.Career.settings.difficultyId, seed, App.Version, LinkName());
                if (_streetsStyle)
                {
                    // THE STREETS on two phones: street rules on a street court.
                    setup.Style = LinkSetup.StreetsStyle;
                    var courts = Streets.CourtList(c);
                    if (courts.Count > 0) setup.CourtId = courts[(int)(seed % (uint)courts.Count)].id;
                }
                var link = NearbyLink.Start(true, LinkName());
                var lobby = new LinkLobby(true, setup, App.Version, LinkProtocol.ContentFingerprint(c), c);
                LinkPoller.Attach(column, link, lobby, status, "Waiting for your friend to join…\nOn their phone: 2 PLAYER ► TWO PHONES ► JOIN A GAME.", 0);
            }, ButtonStyle.Primary, 130f);

            int Next(int current, int other)
            {
                int n = (current + 1) % teams.Count;
                if (n == other) n = (n + 1) % teams.Count;
                return n;
            }

            void Refresh()
            {
                homeLabel.text = teams[_linkHome].FullName.ToUpperInvariant();
                awayLabel.text = teams[_linkAway].FullName.ToUpperInvariant();
            }
            Refresh();
        }

        private void ShowLinkJoin()
        {
            var c = App.Catalog;
            var column = OpenOverlay("JOIN A GAME", out var footer);
            var status = UiKit.Label(column, "Looking for games nearby…", 32f, Theme.Muted, TextAlignmentOptions.Center);
            UiKit.Size(status, 130f);
            var list = UiKit.Column(column, 14f, null, "Hosts");
            UiKit.Size(list, 520f);
            var link = NearbyLink.Start(false, LinkName());
            var lobby = new LinkLobby(false, null, App.Version, LinkProtocol.ContentFingerprint(c), c);
            var poller = LinkPoller.Attach(column, link, lobby, status, "Connected! Getting the game from the host…", 1);
            poller.HostList = list;
            UiKit.Button(footer, "BACK", () => { NearbyLink.Stop(); ShowLinkMenu(); }, ButtonStyle.Ghost, 130f, 44f);
        }

        /// <summary>WATCH A GAME: a third phone joins a hosted two-phone game and shows it live (nothing it does affects the game).</summary>
        private void ShowLinkWatch()
        {
            var c = App.Catalog;
            var column = OpenOverlay("WATCH A GAME", out var footer);
            FirstVisit(Tours.Watch);
            var status = UiKit.Label(column, "Looking for games nearby…", 32f, Theme.Muted, TextAlignmentOptions.Center);
            UiKit.Size(status, 130f);
            var list = UiKit.Column(column, 14f, null, "Hosts");
            UiKit.Size(list, 520f);
            var link = NearbyLink.Start(false, LinkName());
            var watcher = new Spectator(App.Version, LinkProtocol.ContentFingerprint(c), c);
            var poller = LinkPoller.AttachWatcher(column, link, watcher, status);
            poller.HostList = list;
            UiKit.Button(footer, "BACK", () => { NearbyLink.Stop(); ShowLinkMenu(); }, ButtonStyle.Ghost, 130f, 44f);
        }

        /// <summary>COUCH CUP on two phones: this phone hosts the cup's next game; the other phone joins it.</summary>
        private void ShowLinkCupHost()
        {
            var c = App.Catalog;
            var cup = App.Career.couch;
            var next = CouchCup.NextGame(cup);
            if (next == null || !NearbyLink.Supported) { ShowCouchBracket(); return; }
            var column = OpenOverlay("COUCH CUP · TWO PHONES", out var footer);
            UiKit.Size(UiKit.Label(column, CouchCup.RoundName(cup, next.round), 30f, Theme.Gold, TextAlignmentOptions.Center, true), 50f);
            UiKit.Size(UiKit.Label(column, "<color=#FFD166>" + cup.names[next.a].ToUpperInvariant() + "</color> plays on THIS phone\n<color=#4CC9F0>"
                                   + cup.names[next.b].ToUpperInvariant() + "</color> plays on the OTHER phone", 36f, Theme.Cream, TextAlignmentOptions.Center, true), 120f);
            UiKit.Size(UiKit.Label(column, "On the other phone: 2 PLAYER ► TWO PHONES ► JOIN A GAME. The bracket stays on this phone; after each game, both tap NEXT GAME and pass the phones on. Other friends can WATCH A GAME on their own phones.", 26f, Theme.Muted, TextAlignmentOptions.Center), 170f);
            var status = UiKit.Label(column, "", 32f, Theme.Muted, TextAlignmentOptions.Center);
            UiKit.Size(status, 120f);
            Button hostButton = null;
            UiKit.Button(footer, "BACK", () => { NearbyLink.Stop(); ShowCouchBracket(); }, ButtonStyle.Ghost, 130f, 44f);
            hostButton = UiKit.Button(footer, "WAIT FOR PHONE", () =>
            {
                hostButton.interactable = false;
                var setup = CouchCup.LinkSetupFor(cup, c, next, App.Career.settings.difficultyId, (uint)Environment.TickCount | 1u, App.Version, LinkName());
                var link = NearbyLink.Start(true, LinkName());
                var lobby = new LinkLobby(true, setup, App.Version, LinkProtocol.ContentFingerprint(c), c);
                LinkPoller.Attach(column, link, lobby, status, "Waiting for the other phone to join…", 0);
            }, ButtonStyle.Primary, 130f);
        }
    }

    /// <summary>Runs the lobby while a TWO PHONES screen is open; starts the game when both phones are ready.</summary>
    public sealed class LinkPoller : MonoBehaviour
    {
        private NearbyLink _link;
        private LinkLobby _lobby;
        private TextMeshProUGUI _status;
        private string _connectedText;
        private int _seat;
        private bool _started;
        private bool _joining;
        /// <summary>Watching (a third phone): the host's game stream, instead of a lobby.</summary>
        private Spectator _watch;
        private float _nextList;
        private readonly List<string> _shown = new List<string>();
        public RectTransform HostList;

        public static LinkPoller Attach(Transform host, NearbyLink link, LinkLobby lobby, TextMeshProUGUI status, string connectedText, int seat)
        {
            var p = host.gameObject.AddComponent<LinkPoller>();
            p._link = link;
            p._lobby = lobby;
            p._status = status;
            p._connectedText = connectedText;
            p._seat = seat;
            status.text = seat == 0 ? connectedText : status.text;
            return p;
        }

        public static LinkPoller AttachWatcher(Transform host, NearbyLink link, Spectator watch, TextMeshProUGUI status)
        {
            var p = host.gameObject.AddComponent<LinkPoller>();
            p._link = link;
            p._watch = watch;
            p._status = status;
            p._connectedText = "Connected! Getting the game…";
            p._seat = 1;
            return p;
        }

        private void Update()
        {
            if (_started || _link == null) return;
            PowerMonitor.Wake();
            if (_watch != null) { UpdateWatch(); return; }
            var state = _link.State;
            if (_lobby.Status == LobbyStatus.Waiting) _lobby.Update(_link);

            switch (_lobby.Status)
            {
                case LobbyStatus.Started:
                    _started = true;
                    var setup = _lobby.Setup;
                    if (!setup.Register(App.Catalog))
                    {
                        _status.text = "Couldn't set up the teams. Go back and try again.";
                        NearbyLink.Stop();
                        return;
                    }
                    LinkMatch.Transport = _link;
                    LinkMatch.Seat = _seat;
                    LinkMatch.Setup = setup;
                    App.PendingMatch = setup.ToRequest();
                    _status.text = "TIP OFF!";
                    SceneFlow.GoTo(SceneNames.Game);
                    return;
                case LobbyStatus.Rejected:
                case LobbyStatus.Lost:
                    _status.text = "<color=#FF6B6B>" + (_lobby.Error ?? "Lost the connection.") + "</color>";
                    NearbyLink.Stop();
                    _link = null;
                    return;
            }

            if (state == LinkState.Connected)
            {
                _status.text = (_seat == 0 ? "Connected to " + _link.PeerName + ". Starting…" : _connectedText);
                if (HostList != null) HostList.gameObject.SetActive(false);
            }
            else if (state == LinkState.Lost)
            {
                _status.text = "<color=#FF6B6B>Couldn't use the local network. Check Settings ► Privacy ► Local Network ► Retro Hoops is on, then try again.</color>";
            }
            else if (_seat == 1 && HostList != null && Time.unscaledTime >= _nextList)
            {
                _nextList = Time.unscaledTime + 0.5f;
                RefreshHosts();
            }
        }

        private void UpdateWatch()
        {
            var state = _link.State;
            if (state == LinkState.Connected)
            {
                while (_link.TryReceive(out var m)) _watch.Receive(m);
                if (HostList != null) HostList.gameObject.SetActive(false);
                if (_watch.Error != null)
                {
                    _status.text = "<color=#FF6B6B>" + _watch.Error + "</color>";
                    NearbyLink.Stop();
                    _link = null;
                    return;
                }
                if (_watch.Ready)
                {
                    if (!_watch.Setup.Register(App.Catalog))
                    {
                        _status.text = "<color=#FF6B6B>Couldn't set up the teams. Go back and try again.</color>";
                        NearbyLink.Stop();
                        _link = null;
                        return;
                    }
                    _started = true;
                    LinkMatch.Transport = _link;
                    LinkMatch.Seat = 1;
                    LinkMatch.Setup = _watch.Setup;
                    LinkMatch.Watching = _watch;
                    App.PendingMatch = _watch.Setup.ToRequest();
                    _status.text = "WATCHING!";
                    SceneFlow.GoTo(SceneNames.Game);
                    return;
                }
                _status.text = _connectedText;
            }
            else if (state == LinkState.Lost)
            {
                _status.text = "<color=#FF6B6B>The game ended or the connection dropped. Go back and try again.</color>";
            }
            else if (HostList != null && Time.unscaledTime >= _nextList)
            {
                _nextList = Time.unscaledTime + 0.5f;
                RefreshHosts();
            }
        }

        private void RefreshHosts()
        {
            var found = _link.Found();
            bool same = found.Count == _shown.Count;
            for (int i = 0; same && i < found.Count; i++) same = found[i] == _shown[i];
            if (same) return;
            _shown.Clear();
            _shown.AddRange(found);
            foreach (Transform child in HostList) Destroy(child.gameObject);
            _status.text = found.Count == 0 ? (_watch != null ? "Looking for games nearby…\nGames can be watched once both players are in."
                                                              : "Looking for games nearby…\nOn your friend's phone: 2 PLAYER ► TWO PHONES ► HOST A GAME.")
                                            : (_joining ? "Joining…" : _watch != null ? "Tap a game to watch it:" : "Tap your friend's game to join:");
            for (int i = 0; i < found.Count; i++)
            {
                int index = i;
                UiKit.Button(HostList, found[i].ToUpperInvariant(), () =>
                {
                    _joining = true;
                    _status.text = (_watch != null ? "Watching " : "Joining ") + found[index] + "… (the host's phone accepts automatically)";
                    if (_watch != null) _link.Watch(index); else _link.Join(index);
                }, ButtonStyle.Secondary, 110f, 30f);
            }
        }

        private void OnDestroy()
        {
            // Leaving the screen before the game starts hangs up.
            if (!_started && _link != null) NearbyLink.Stop();
        }
    }
}
