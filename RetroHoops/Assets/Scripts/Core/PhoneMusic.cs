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
        [DllImport("__Internal")] private static extern void RetroMusic_Search(string term);
        [DllImport("__Internal")] private static extern void RetroMusic_Lookup(string ids);
        [DllImport("__Internal")] private static extern void RetroMusic_PickFromLibrary();
        [DllImport("__Internal")] private static extern int RetroMusic_RequestState();
        [DllImport("__Internal")] private static extern string RetroMusic_RequestResult();
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
        private static void RetroMusic_Search(string term) { }
        private static void RetroMusic_Lookup(string ids) { }
        private static void RetroMusic_PickFromLibrary() { }
        private static int RetroMusic_RequestState() => 0;
        private static string RetroMusic_RequestResult() => "[]";
        /// <summary>Apple Music only works in the iPhone / iPad app.</summary>
        public static bool Supported => false;
#endif

        public enum Status { Unsupported, NotConnected, Denied, NoSubscription, Loading, NoCleanSongs, Failed, Ready, MixEmpty }

        /// <summary>15 Oct 2026: what a MY MIX request is for.</summary>
        public enum Request { None, Search, Pick, Register, PasteLink }

        /// <summary>A playlist song found on Apple Music (clean).</summary>
        [Serializable]
        public class Entry
        {
            public string key, id, url, artwork, title, artist, album;
            public double seconds;
            /// <summary>Apple Music lists this version as explicit (there was no clean one).</summary>
            public bool @explicit;
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
        /// <summary>Every playlist song found on Apple Music (clean, or explicit when there's no clean one).</summary>
        public readonly List<Entry> Found = new List<Entry>();
        /// <summary>MY MIX songs Apple Music has found this session (keyed "mix:id"), ready to play.</summary>
        public readonly List<Entry> MixFound = new List<Entry>();
        /// <summary>The last in-game Apple Music search's results.</summary>
        public readonly List<Entry> SearchResults = new List<Entry>();
        /// <summary>The MY MIX request running now (None = idle).</summary>
        public Request Busy { get; private set; }
        /// <summary>A short note for the MUSIC app after a request ("3 SONGS ADDED", "NO SONG LINK FOUND"...).</summary>
        public string Note { get; private set; }
        private readonly HashSet<string> _registerFailed = new HashSet<string>();

        public bool MixMode => Data != null && Data.musicSource == 1;

        /// <summary>Songs left out because they're explicit and EXPLICIT SONGS is off.</summary>
        public int HiddenExplicit { get; private set; }
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

        /// <summary>
        /// 15 Oct 2026: which found songs can play. An explicit song plays only when EXPLICIT SONGS is on (on by
        /// default; iPhone Screen Time limits still apply on top).
        /// </summary>
        private void ApplyFilter()
        {
            bool allow = Data != null && Data.explicitSongs;
            Songs.Clear();
            HiddenExplicit = 0;
            if (MixMode)
            {
                // MY MIX: the player's songs, in the order they added them, once Apple Music has found them.
                if (Data.myMix == null) Data.myMix = new List<MixSong>();
                foreach (var m in Data.myMix)
                {
                    var f = MixFound.Find(x => x.id == m.id);
                    if (f == null) continue;
                    if (f.@explicit && !allow) { HiddenExplicit++; continue; }
                    Songs.Add(f);
                }
                return;
            }
            foreach (var f in Found)
            {
                if (f.@explicit && !allow) { HiddenExplicit++; continue; }
                Songs.Add(f);
            }
        }

        /// <summary>Settings / MUSIC app: allow explicit songs or not. Restarts the playlist with the new list.</summary>
        public void SetExplicit(bool allow)
        {
            if (Data == null) return;
            Data.explicitSongs = allow;
            App.SaveCareer();
            ApplyFilter();
            StopForChange();
            Changed?.Invoke();
        }

        private void StopForChange()
        {
            if (_started)
            {
                RetroMusic_Command(4);
                _started = false;
                IsPlaying = false;
                AudioManager.ExternalMusic = false;
                _lastIndexPlayed = -1;
            }
            if (State == Status.Ready || State == Status.NoCleanSongs || State == Status.MixEmpty) State = ListState();
        }

        private Status ListState() =>
            Songs.Count > 0 ? Status.Ready : MixMode ? Status.MixEmpty : _resolveFailed ? Status.Failed : Status.NoCleanSongs;

        // ------------------------------------------------------------------ 15 Oct 2026: MY MIX

        /// <summary>0 = the Retro Hoops playlist, 1 = MY MIX. Switching starts the new list.</summary>
        public void SetSource(int source)
        {
            if (Data == null || Data.musicSource == source) return;
            Data.musicSource = source;
            App.SaveCareer();
            Note = null;
            ApplyFilter();
            StopForChange();
            State = ListState();
            Changed?.Invoke();
        }

        /// <summary>SEARCH: looks for songs on Apple Music (results in <see cref="SearchResults"/>).</summary>
        public void Search(string term)
        {
            term = (term ?? "").Trim();
            if (term.Length < 2 || Busy != Request.None || !Supported) return;
            Busy = Request.Search;
            Note = null;
            RetroMusic_Search(term.Length > 80 ? term.Substring(0, 80) : term);
            Changed?.Invoke();
        }

        /// <summary>ADD FROM MY MUSIC: the iPhone's music picker; the songs picked join MY MIX.</summary>
        public void PickFromLibrary()
        {
            if (Busy != Request.None || !Supported) return;
            Busy = Request.Pick;
            Note = null;
            RetroMusic_PickFromLibrary();
            Changed?.Invoke();
        }

        /// <summary>PASTE LINK: adds the song from an Apple Music link on the clipboard (Share ▸ Copy Link in Apple Music).</summary>
        public void PasteLink()
        {
            if (Busy != Request.None || !Supported) return;
            string id = MyMix.SongIdFromLink(GUIUtility.systemCopyBuffer);
            if (id == null)
            {
                Note = "NO SONG LINK FOUND. IN APPLE MUSIC: SHARE > COPY LINK, THEN COME BACK";
                Changed?.Invoke();
                return;
            }
            Busy = Request.PasteLink;
            Note = null;
            RetroMusic_Lookup(id);
            Changed?.Invoke();
        }

        /// <summary>Adds a search result (or any found song) to MY MIX.</summary>
        public bool AddToMix(Entry e)
        {
            if (e == null || Data == null) return false;
            bool added = MyMix.Add(Data, new MixSong { id = e.id, title = e.title ?? "", artist = e.artist ?? "", album = e.album ?? "", url = e.url ?? "", @explicit = e.@explicit });
            if (!added) return false;
            Register(e);
            App.SaveCareer();
            ApplyFilter();
            if (MixMode && State == Status.MixEmpty) State = ListState();
            Changed?.Invoke();
            return true;
        }

        public void RemoveFromMix(string id)
        {
            if (Data == null || !MyMix.Remove(Data, id)) return;
            App.SaveCareer();
            bool wasPlaying = MixMode && _started;
            ApplyFilter();
            if (wasPlaying) StopForChange();
            else if (MixMode) State = ListState();
            Changed?.Invoke();
        }

        private void Register(Entry e)
        {
            if (e == null || MixFound.Exists(x => x.id == e.id)) return;
            e.key = MyMix.Key(e.id);
            MixFound.Add(e);
            StartCoroutine(LoadArt(e));
        }

        private void PollRequest()
        {
            if (Busy == Request.None) return;
            int rs = RetroMusic_RequestState();
            if (rs == 1) return; // still working (or the picker is still open)
            var kind = Busy;
            Busy = Request.None;
            var items = new List<Entry>();
            if (rs == 2)
            {
                try { items = JsonUtility.FromJson<EntryList>("{\"items\":" + (RetroMusic_RequestResult() ?? "[]") + "}")?.items ?? items; }
                catch (Exception ex) { Debug.LogWarning("[Retro Hoops] Apple Music request: " + ex.Message); }
            }
            foreach (var it in items) it.key = MyMix.Key(it.id);
            switch (kind)
            {
                case Request.Search:
                    SearchResults.Clear();
                    SearchResults.AddRange(items);
                    foreach (var it in items) StartCoroutine(LoadArt(it));
                    Note = rs == 3 ? "COULDN'T REACH APPLE MUSIC" : items.Count == 0 ? "NO SONGS FOUND" : null;
                    break;
                case Request.Pick:
                case Request.PasteLink:
                    int added = 0;
                    foreach (var it in items) if (AddToMix(it)) added++;
                    Note = rs == 3 ? "COULDN'T REACH APPLE MUSIC"
                         : added > 0 ? (added == 1 ? "1 SONG ADDED TO MY MIX" : added + " SONGS ADDED TO MY MIX")
                         : items.Count > 0 ? "ALREADY IN MY MIX"
                         : kind == Request.Pick ? null : "THAT SONG ISN'T ON APPLE MUSIC";
                    break;
                default: // Register (saved MY MIX songs)
                    foreach (var it in items) Register(it);
                    if (rs == 3 && Data?.myMix != null) foreach (var m in Data.myMix) if (!MixFound.Exists(x => x.id == m.id)) _registerFailed.Add(m.id);
                    break;
            }
            ApplyFilter();
            if (State == Status.MixEmpty || State == Status.Ready) State = ListState();
            Changed?.Invoke();
        }

        /// <summary>Finds saved MY MIX songs on Apple Music so they can play (once per session).</summary>
        private void RegisterSavedMix()
        {
            if (Busy != Request.None || Data?.myMix == null) return;
            var ids = new List<string>();
            foreach (var m in Data.myMix)
                if (!MixFound.Exists(x => x.id == m.id) && !_registerFailed.Contains(m.id)) ids.Add(m.id);
            if (ids.Count == 0) return;
            foreach (var id in ids) _registerFailed.Add(id); // tried once; a later add or retry can find them
            Busy = Request.Register;
            RetroMusic_Lookup(string.Join(",", ids));
        }

        /// <summary>Plays this song now; the rest of the loop follows in a fresh shuffle.</summary>
        public void PlaySong(string key)
        {
            int index = Songs.FindIndex(s => s.key == key);
            if (index >= 0) StartLoop(index);
        }

        /// <summary>The + button: opens the song in Apple Music (to add it to your library).</summary>
        public static void OpenInAppleMusic(string key)
        {
            var entry = _instance == null ? null : _instance.Found.Find(s => s.key == key) ?? _instance.MixFound.Find(s => s.key == key)
                        ?? _instance.SearchResults.Find(s => s.key == key);
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
            else if (!_resolving && Found.Count == 0 && RetroMusic_ResolveState() == 0) Resolve();
            else if (_resolving) PollResolve();
            else State = ListState();

            // MY MIX requests (search, picker, pasted links) and finding saved songs.
            if (auth == 3)
            {
                PollRequest();
                if (MixMode && RetroMusic_CanPlay() == 1) RegisterSavedMix();
            }
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
                Found.Clear();
                foreach (var t in PhonePlaylist.Tracks)
                {
                    var hit = parsed?.items?.Find(x => x.key == t.Key);
                    if (hit != null) Found.Add(hit);
                }
                ApplyFilter();
                foreach (var f in Found) StartCoroutine(LoadArt(f));
            }
            _resolveFailed = rs == 3;
            ApplyFilter();
            State = ListState();
            Changed?.Invoke();
        }

        /// <summary>Looks the songs up again (after a failure, e.g. no internet).</summary>
        public void Retry()
        {
            if (!Supported || _resolving) return;
            Songs.Clear();
            Found.Clear();
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
