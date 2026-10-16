using System.Collections.Generic;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using UnityEngine;

namespace CallerRetroBall.Audio
{
    /// <summary>
    /// Plays the procedurally generated sounds (see <see cref="AudioSynth"/>). Two volume buses —
    /// Music and SFX — driven by Settings. SFX use a small pool of sources (no per-play allocation).
    /// No third-party audio is bundled.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        private const int SfxVoices = 6;

        private static AudioManager _instance;

        private readonly Dictionary<SfxId, AudioClip> _clips = new Dictionary<SfxId, AudioClip>();
        private AudioSource _music;
        private AudioSource _ambience;
        private readonly AudioClip[] _tracks = new AudioClip[Soundtrack.Count];
        private int _track = -1;
        private bool _ambienceOn;
        private AudioSource[] _sfx;
        private int _next;
        private float _musicVolume = 0.6f;
        private float _sfxVolume = 0.9f;
        // Phase 35 mix: the announcer has a voice of his own (never cut off by a bounce), the music and crowd dip
        // under him, the crowd follows the game's mood, and repeats of one sound in a moment are softened.
        private AudioSource _voice;
        private float _duck = 1f, _crowdDuck = 1f, _crowd = 1f, _crowdTarget = 1f;
        private readonly Dictionary<SfxId, float> _lastPlayed = new Dictionary<SfxId, float>();
        private readonly Dictionary<SfxId, int> _repeats = new Dictionary<SfxId, int>();

        public static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("[AudioManager]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<AudioManager>();
            _instance.Build();
        }

        private void Build()
        {
            foreach (SfxId id in System.Enum.GetValues(typeof(SfxId)))
                _clips[id] = MakeClip("sfx." + id, AudioSynth.Sfx(id));

            _music = gameObject.AddComponent<AudioSource>();
            _tracks[0] = MakeClip("music.track0", AudioSynth.MusicLoop(0));
            _music.clip = _tracks[0];
            _track = 0;
            _music.loop = true;
            _music.playOnAwake = false;

            _ambience = gameObject.AddComponent<AudioSource>();
            _ambience.clip = MakeClip("ambience.crowd", AudioSynth.CrowdAmbience());
            _ambience.loop = true;
            _ambience.playOnAwake = false;

            _voice = gameObject.AddComponent<AudioSource>();
            _voice.playOnAwake = false;

            _sfx = new AudioSource[SfxVoices];
            for (int i = 0; i < SfxVoices; i++)
            {
                _sfx[i] = gameObject.AddComponent<AudioSource>();
                _sfx[i].playOnAwake = false;
            }
            ApplySettings();
            _music.Play();
        }

        private static AudioClip MakeClip(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, AudioSynth.SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>Re-reads Music/SFX volumes from the career settings.</summary>
        public static void ApplySettings()
        {
            if (_instance == null) return;
            var s = App.Career?.settings;
            _instance._musicVolume = s != null ? s.musicVolume : 0.6f;
            _instance._sfxVolume = s != null ? s.sfxVolume : 0.9f;
            _instance.ApplyLevels();
        }

        /// <summary>
        /// Switches the music for a screen: 0 = menus, 1 = matches, 2 = Rise hub. Menus and matches play
        /// the tracks picked in the Music Player (matches can shuffle the whole soundtrack).
        /// </summary>
        public static void PlayMusic(int role)
        {
            if (_instance == null) return;
            _lastRole = role;
            // 15 Oct 2026: the phone's beat tape is playing: it keeps going from screen to screen.
            if (PlaylistOwnsMusic) return;
            var s = App.Career?.settings;
            int track = ((role % AudioSynth.MusicTrackCount) + AudioSynth.MusicTrackCount) % AudioSynth.MusicTrackCount;
            if (role == 0 && s != null && s.musicMenu >= 0) track = s.musicMenu;
            if (role == 1 && s != null && s.musicGame >= 0) track = s.musicGame;
            if (role == 1 && s != null && s.musicGame == MusicShuffle) track = Random.Range(0, Soundtrack.Count);
            PlayTrack(track);
        }

        private static int _lastRole;

        // ------------------------------------------------------------------ 15 Oct 2026: the phone's beat tape

        /// <summary>The phone's RETRO HOOPS playlist (the beat tape) is in charge of the music: screens don't switch tracks.</summary>
        public static bool PlaylistOwnsMusic { get; private set; }

        private bool _once, _paused;

        /// <summary>Plays a track once (no loop) for the phone's playlist, which then picks the next one.</summary>
        public static void PlayOnce(int track)
        {
            if (_instance == null) return;
            PlaylistOwnsMusic = true;
            _instance._once = true;
            _instance._paused = false;
            if (_instance._track == track && _instance._music.isPlaying) _instance._music.Stop();
            _instance._track = -1;
            PlayTrack(track);
        }

        /// <summary>The playlist let go of the music: back to the screen's own soundtrack.</summary>
        public static void ReleasePlaylist()
        {
            if (_instance == null || !PlaylistOwnsMusic) return;
            PlaylistOwnsMusic = false;
            _instance._once = false;
            _instance._paused = false;
            _instance._music.loop = true;
            _instance._track = -1;
            PlayMusic(_lastRole);
        }

        public static void Pause()
        {
            if (_instance == null) return;
            _instance._paused = true;
            _instance._music.Pause();
        }

        public static void Resume()
        {
            if (_instance == null) return;
            _instance._paused = false;
            if (_instance._music.clip != null) _instance._music.UnPause();
            if (!_instance._music.isPlaying && _instance._music.clip != null && _instance._pending == null) _instance._music.Play();
        }

        public static bool IsPaused => _instance != null && _instance._paused;

        /// <summary>Seconds into the track playing now.</summary>
        public static float Position => _instance != null && _instance._music.clip != null ? _instance._music.time : 0f;

        /// <summary>Restarts the track playing now from the top.</summary>
        public static void Restart()
        {
            if (_instance == null || _instance._music.clip == null) return;
            _instance._music.time = 0f;
            if (!_instance._paused) _instance._music.Play();
        }

        /// <summary>A playlist track played to its end (the playlist moves on).</summary>
        public static bool TrackFinished =>
            _instance != null && _instance._once && !_instance._paused && _instance._pending == null && _instance._music.clip != null
            && !_instance._music.isPlaying && _instance._track >= 0 && _instance._tracks[_instance._track] == _instance._music.clip;

        /// <summary>Matches shuffle the whole soundtrack.</summary>
        public const int MusicShuffle = -2;

        /// <summary>The track playing now (0..Soundtrack.Count-1).</summary>
        public static int CurrentTrack => _instance != null ? _instance._track : 0;

        public static bool IsPlaying => _instance != null && _instance._music.isPlaying;

        /// <summary>A composed song is still being written.</summary>
        public static bool IsLoading => _instance != null && _instance._pending != null;

        /// <summary>Plays one soundtrack track now (the Music Player). Composed songs are written the first
        /// time they're played (about a second) and only the current one is kept in memory.</summary>
        public static void PlayTrack(int track)
        {
            if (_instance == null) return;
            if (track < 0 || track >= Soundtrack.Count) track = 0;
            if (_instance._track == track && _instance._music.isPlaying) return;
            if (_instance._tracks[track] == null && track >= Soundtrack.ClassicCount)
            {
                // Composed songs are written on a worker thread so loading a screen never hitches;
                // the clip starts as soon as its samples are ready (Update picks it up).
                _instance._track = track;
                _instance._pendingTrack = track;
                int t = track;
                _instance._pending = System.Threading.Tasks.Task.Run(() => Soundtrack.Render(t));
                return;
            }
            if (_instance._tracks[track] == null)
                _instance._tracks[track] = MakeClip("music.track" + track, Soundtrack.Render(track));
            StartClip(track);
        }

        private System.Threading.Tasks.Task<float[]> _pending;
        private int _pendingTrack = -1;

        private void Update()
        {
            Mix();
            if (_pending == null || !_pending.IsCompleted) return;
            var task = _pending;
            int track = _pendingTrack;
            _pending = null;
            _pendingTrack = -1;
            if (task.IsFaulted || task.Result == null) return;
            if (_tracks[track] == null) _tracks[track] = MakeClip("music.track" + track, task.Result);
            // Only start it if nothing else was asked for while it was being written.
            if (_track == track) StartClip(track);
        }

        private static void StartClip(int track)
        {
            _instance._track = track;
            _instance._music.clip = _instance._tracks[track];
            _instance._music.loop = !_instance._once;
            _instance._music.Play();
            if (_instance._paused) _instance._music.Pause();
            // Keep only the composed song that's playing (each is several MB); the three loops stay cached.
            for (int i = Soundtrack.ClassicCount; i < _instance._tracks.Length; i++)
                if (i != track && _instance._tracks[i] != null)
                {
                    Destroy(_instance._tracks[i]);
                    _instance._tracks[i] = null;
                }
        }

        /// <summary>14 Oct 2026: Apple Music (the phone's playlist) is playing, so the soundtrack is silent.</summary>
        public static bool ExternalMusic { get; set; }

        public static void StopMusic()
        {
            if (_instance != null) _instance._music.Stop();
        }

        /// <summary>Loudness of the music right now in <paramref name="bands"/> slices (0..1), for the player's meter.</summary>
        public static void Levels(float[] bands)
        {
            if (_instance == null || bands == null || bands.Length == 0) return;
            var src = _instance._music;
            if (!src.isPlaying || src.clip == null) { System.Array.Clear(bands, 0, bands.Length); return; }
            var data = _levelBuffer;
            src.GetSpectrumData(data, 0, FFTWindow.BlackmanHarris);
            // Log-spaced bands from bass to treble.
            int n = bands.Length;
            for (int b = 0; b < n; b++)
            {
                int from = (int)(Mathf.Pow(data.Length, b / (float)n)), to = Mathf.Max(from + 1, (int)(Mathf.Pow(data.Length, (b + 1) / (float)n)));
                float sum = 0f;
                for (int i = from; i < to && i < data.Length; i++) sum += data[i];
                bands[b] = Mathf.Clamp01(Mathf.Sqrt(sum) * 2.2f);
            }
        }

        private static readonly float[] _levelBuffer = new float[512];

        /// <summary>Crowd murmur under a match (follows the SFX volume). Music ducks while it plays.</summary>
        public static void SetAmbience(bool on)
        {
            if (_instance == null || _instance._ambienceOn == on) return;
            _instance._ambienceOn = on;
            if (on) _instance._ambience.Play();
            else _instance._ambience.Stop();
            _instance._crowdTarget = 1f;
            _instance.ApplyLevels();
        }

        /// <summary>Phase 35: how loud the crowd murmur should sit (see <see cref="AudioMix.CrowdLevel"/>); it eases there.</summary>
        public static void SetCrowdLevel(float level)
        {
            if (_instance != null) _instance._crowdTarget = Mathf.Clamp(level, 0.3f, 2.5f);
        }

        /// <summary>Phase 35: eases the duck and the crowd level each frame.</summary>
        private void Mix()
        {
            float dt = Time.unscaledDeltaTime;
            bool speaking = _voice != null && _voice.isPlaying;
            _duck = AudioMix.Duck(_duck, speaking, dt);
            _crowdDuck = AudioMix.Duck(_crowdDuck, speaking, dt, AudioMix.CrowdDuckDepth);
            _crowd = AudioMix.Approach(_crowd, _crowdTarget, dt, 2.5f, 0.8f);
            ApplyLevels();
        }

        private void ApplyLevels()
        {
            if (_music == null) return;
            // The phone's Apple Music playlist is playing: the game's own soundtrack steps aside.
            _music.volume = ExternalMusic ? 0f : _musicVolume * (_ambienceOn ? 0.3f : 0.5f) * _duck;
            _ambience.volume = Mathf.Min(1f, _sfxVolume * 0.22f * _crowd * _crowdDuck);
        }

        public static void Play(SfxId id, float volume = 1f, float pitch = 1f)
        {
            if (_instance == null || _instance._sfxVolume <= 0f) return;
            // The same sound again in the same moment (a scramble of bounces, a flurry of cheers) is played softer.
            float now = Time.unscaledTime;
            int repeats = _instance._lastPlayed.TryGetValue(id, out float last) && now - last < AudioMix.RepeatWindow
                ? (_instance._repeats.TryGetValue(id, out int r) ? r + 1 : 1) : 0;
            _instance._lastPlayed[id] = now;
            _instance._repeats[id] = repeats;
            var src = _instance._sfx[_instance._next];
            _instance._next = (_instance._next + 1) % SfxVoices;
            src.pitch = pitch;
            src.PlayOneShot(_instance._clips[id], volume * _instance._sfxVolume * AudioMix.RepeatGain(repeats));
        }

        public static void Click() => Play(SfxId.Click, 0.6f);

        private readonly System.Collections.Generic.Dictionary<string, AudioClip> _voices = new System.Collections.Generic.Dictionary<string, AudioClip>();

        /// <summary>The announcer "says" a callout as chiptune voice blips (synthesised once per phrase, then cached).</summary>
        public static void Voice(string phrase, float volume = 0.9f)
        {
            // Captions show even with the sound off.
            UI.Captions.Show(phrase);
            if (_instance == null || _instance._sfxVolume <= 0f || string.IsNullOrEmpty(phrase)) return;
            if (!_instance._voices.TryGetValue(phrase, out var clip))
            {
                if (_instance._voices.Count > 48) _instance._voices.Clear();
                clip = MakeClip("voice." + phrase, AudioSynth.Voice(phrase));
                _instance._voices[phrase] = clip;
            }
            // His own source: a new call replaces the last one instead of piling up, and nothing else cuts him off.
            var src = _instance._voice;
            src.Stop();
            src.pitch = 1f;
            src.clip = clip;
            src.volume = Mathf.Clamp01(volume * _instance._sfxVolume);
            src.Play();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
