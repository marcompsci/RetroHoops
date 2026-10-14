using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using CallerRetroBall.Audio;
using CallerRetroBall.Logic;
using UnityEngine;
using UnityEngine.Networking;

namespace CallerRetroBall.Core
{
    /// <summary>
    /// 14 Oct 2026: the phone's MUSIC app. It plays <see cref="PhonePlaylist"/> in the background through Apple Music
    /// (RetroMusic.swift / MusicKit) on shuffle, and deals a new order every time the playlist loops. Clean
    /// versions only. While it plays, the game's own soundtrack is silent (<see cref="AudioManager.ExternalMusic"/>).
    /// It lives across scenes, so the songs keep going from the menus into games.
    /// </summary>
    public sealed class PhoneMusic : MonoBehaviour
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void RetroMusic_Refresh();
        [DllImport("__Internal")] private static extern void RetroMusic_RequestAccess();
        [DllImport("__Internal")] private static extern int RetroMusic_Auth();
        [DllImport("__Internal")] private static extern int RetroMusic_CanPlay();
        [DllImport("__Internal")] private static extern void RetroMusic_Resolve(string request);
        [DllImport("__Internal")] private static extern int RetroMusic_ResolveState();
        [DllImport("__Internal")] private static extern string RetroMusic_Resolved();
        [DllImport("__Internal")] private static extern void RetroMusic_Play(string ids, int start);
        [DllImport("__Internal")] private static extern void RetroMusic_Command(int command);
        [DllImport("__Internal")] private static extern string RetroMusic_NowPlaying();
        public static bool Supported => true;
#else
        private static void RetroMusic_Refresh() { }
        private static void RetroMusic_RequestAccess() { }
        private static int RetroMusic_Auth() => 0;
        private static int RetroMusic_CanPlay() => 0;
        private static void RetroMusic_Resolve(string request) { }
        private static int RetroMusic_ResolveState() => 0;
        private static string RetroMusic_Resolved() => "[]";
        private static void RetroMusic_Play(string ids, int start) { }
        private static void RetroMusic_Command(int command) { }
        private static string RetroMusic_NowPlaying() => "";
        /// <summary>Apple Music only works in the iPhone / iPad app.</summary>
        public static bool Supported => false;
#endif

        public enum Status { Unsupported, NotConnected, Denied, NoSubscription, Loading, NoCleanSongs, Failed, Ready }

        /// <summary>A playlist song found on Apple Music (clean).</summary>
        [Serializable]
        public class Entry
        {
            public string key, id, url, artwork, title, artist, album;
            public double seconds;
            [NonSerialized] public Texture2D art;
        }

        [Serializable] private class EntryList { public List<Entry> items = new List<Entry>(); }

        private static PhoneMusic _instance;

        public static PhoneMusic Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("PhoneMusic");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<PhoneMusic>();
                }
                return _instance;
            }
        }

        /// <summary>The running instance, or null (doesn't create one).</summary>
        public static PhoneMusic Existing => _instance;

        /// <summary>Starts it (call once from the main menu). Doesn't ask for Apple Music access by itself.</summary>
        public static void Wake() { _ = Instance; }

        /// <summary>The clean songs found on Apple Music, in playlist order.</summary>
        public readonly List<Entry> Songs = new List<Entry>();
        public Status State { get; private set; } = Status.NotConnected;
        public string NowPlayingKey { get; private set; }
        public bool IsPlaying { get; private set; }
        public double Position { get; private set; }
        /// <summary>The current loop's order (indexes into <see cref="Songs"/>).</summary>
        public int[] Order { get; private set; } = new int[0];
        public event Action Changed;

        private bool _resolving, _started, _userPaused, _resolveFailed, _sawPlaying;
        private float _nextPoll, _silentSince = -1f;
        private int _lastIndexPlayed = -1;

        private PhoneData Data => App.Career?.phone;

        private void Start()
        {
            if (!Supported) { State = Status.Unsupported; return; }
            RetroMusic_Refresh();
        }

        /// <summary>Asks for Apple Music access (the system prompt), then starts.</summary>
        public void Connect()
        {
            if (!Supported) return;
            RetroMusic_RequestAccess();
            State = Status.Loading;
            Changed?.Invoke();
        }

        /// <summary>Switches the playlist on or off (Settings ▸ PHONE MUSIC and the MUSIC app).</summary>
        public void SetEnabled(bool on)
        {
            if (Data == null) return;
            Data.musicOn = on;
            App.SaveCareer();
            if (!on)
            {
                RetroMusic_Command(4);
                IsPlaying = false;
                _started = false;
                AudioManager.ExternalMusic = false;
            }
            Changed?.Invoke();
        }

        public void TogglePause()
        {
            if (!_started) { StartLoop(-1); return; }
            _userPaused = IsPlaying;
            RetroMusic_Command(IsPlaying ? 0 : 1);
        }

        public void Next() { if (_started) RetroMusic_Command(2); }
        public void Previous() { if (_started) RetroMusic_Command(3); }

        /// <summary>Plays this song now; the rest of the loop follows in a fresh shuffle.</summary>
        public void PlaySong(string key)
        {
            int index = Songs.FindIndex(s => s.key == key);
            if (index >= 0) StartLoop(index);
        }

        /// <summary>The + button: opens the song in Apple Music (to add it to your library).</summary>
        public static void OpenInAppleMusic(string key)
        {
            var entry = _instance != null ? _instance.Songs.Find(s => s.key == key) : null;
            var track = PhonePlaylist.Find(key);
            string url = entry != null && !string.IsNullOrEmpty(entry.url) ? entry.url : track != null ? PhonePlaylist.SearchUrl(track) : null;
            if (!string.IsNullOrEmpty(url)) Application.OpenURL(url);
        }

        private void Update()
        {
            if (!Supported || Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + 0.5f;
            var before = State;
            int auth = RetroMusic_Auth();
            if (auth == 1 || auth == 2) State = Status.Denied;
            else if (auth == 0) State = State == Status.Loading ? Status.Loading : Status.NotConnected;
            else if (RetroMusic_CanPlay() != 1) State = Status.NoSubscription;
            else if (!_resolving && Songs.Count == 0 && RetroMusic_ResolveState() == 0) Resolve();
            else if (_resolving) PollResolve();
            else State = Songs.Count > 0 ? Status.Ready : _resolveFailed ? Status.Failed : Status.NoCleanSongs;

            if (State == Status.Ready) PollPlayback();
            else AudioManager.ExternalMusic = false;
            if (before != State) Changed?.Invoke();
        }

        private void Resolve()
        {
            _resolving = true;
            State = Status.Loading;
            RetroMusic_Resolve(PhonePlaylist.ResolveRequest());
        }

        private void PollResolve()
        {
            int rs = RetroMusic_ResolveState();
            if (rs == 1) { State = Status.Loading; return; }
            _resolving = false;
            Songs.Clear();
            if (rs == 2)
            {
                EntryList parsed = null;
                try { parsed = JsonUtility.FromJson<EntryList>("{\"items\":" + (RetroMusic_Resolved() ?? "[]") + "}"); }
                catch (Exception e) { Debug.LogWarning("[Retro Hoops] Apple Music list: " + e.Message); }
                // Keep the playlist's own order.
                foreach (var t in PhonePlaylist.Tracks)
                {
                    var hit = parsed?.items?.Find(x => x.key == t.Key);
                    if (hit != null) Songs.Add(hit);
                }
                foreach (var s in Songs) StartCoroutine(LoadArt(s));
            }
            _resolveFailed = rs == 3;
            State = Songs.Count > 0 ? Status.Ready : _resolveFailed ? Status.Failed : Status.NoCleanSongs;
            Changed?.Invoke();
        }

        /// <summary>Looks the songs up again (after a failure, e.g. no internet).</summary>
        public void Retry()
        {
            if (!Supported || _resolving) return;
            Songs.Clear();
            _resolveFailed = false;
            Resolve();
            Changed?.Invoke();
        }

        private IEnumerator LoadArt(Entry e)
        {
            if (string.IsNullOrEmpty(e.artwork)) yield break;
            using (var req = UnityWebRequestTexture.GetTexture(e.artwork))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) yield break;
                e.art = DownloadHandlerTexture.GetContent(req);
                if (e.art != null) e.art.filterMode = FilterMode.Bilinear;
                Changed?.Invoke();
            }
        }

        /// <summary>Deals the next loop's shuffle and starts it (at <paramref name="firstIndex"/> if given).</summary>
        private void StartLoop(int firstIndex)
        {
            var data = Data;
            if (data == null || Songs.Count == 0) return;
            if (data.shuffleSeed == 0) data.shuffleSeed = (uint)UnityEngine.Random.Range(1, int.MaxValue);
            var order = PhonePlaylist.ShuffleOrder(Songs.Count, data.shuffleSeed, data.cycle, _lastIndexPlayed);
            if (firstIndex >= 0)
            {
                int at = Array.IndexOf(order, firstIndex);
                if (at > 0) (order[0], order[at]) = (order[at], order[0]);
            }
            Order = order;
            var ids = new string[order.Length];
            for (int i = 0; i < order.Length; i++) ids[i] = Songs[order[i]].id;
            RetroMusic_Play(string.Join(",", ids), 0);
            _started = true;
            _userPaused = false;
            _sawPlaying = false;
            _silentSince = -1f;
            App.SaveCareer();
            Changed?.Invoke();
        }

        private void PollPlayback()
        {
            var data = Data;
            if (data == null) return;
            if (!data.musicOn) { AudioManager.ExternalMusic = false; return; }
            if (!_started) { StartLoop(-1); return; }

            var parts = (RetroMusic_NowPlaying() ?? "").Split('\t');
            string id = parts.Length > 0 ? parts[0] : "";
            bool playing = parts.Length > 1 && parts[1] == "1";
            Position = parts.Length > 2 && double.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var pos) ? pos : 0;
            var entry = Songs.Find(s => s.id == id);
            string key = entry?.key;
            if (key != NowPlayingKey || playing != IsPlaying)
            {
                NowPlayingKey = key ?? NowPlayingKey;
                IsPlaying = playing;
                Changed?.Invoke();
            }
            if (entry != null) _lastIndexPlayed = Songs.IndexOf(entry);
            AudioManager.ExternalMusic = playing;
            if (playing) _sawPlaying = true;

            // The loop ran out (the last song finished and nothing's playing): reshuffle and go again.
            if (!playing && !_userPaused && _sawPlaying)
            {
                if (_silentSince < 0f) _silentSince = Time.unscaledTime;
                bool lastWasEnd = Order.Length > 0 && _lastIndexPlayed == Order[Order.Length - 1];
                if (Time.unscaledTime - _silentSince > 1.5f && (lastWasEnd || string.IsNullOrEmpty(id)))
                {
                    data.cycle++;
                    StartLoop(-1);
                }
            }
            else _silentSince = -1f;
        }
    }
}
