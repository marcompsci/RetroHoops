#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using CallerRetroBall.Logic;

namespace CallerRetroBall.Core
{
    /// <summary>
    /// 15 Oct 2026 (Omari): the play-by-play voice for THE STREETS. iOS speaks the commentary with the built-in
    /// speech voices (Assets/Plugins/iOS/RetroSpeech.mm), fast and up-tempo like a college broadcast; other
    /// platforms show the text only. Off with Settings ► COMMENTARY or ► PLAY-BY-PLAY VOICE, or the sound off.
    /// </summary>
    public static class Speech
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void RetroSpeech_Say(string text, string language, float rate, float pitch, float volume);
        [DllImport("__Internal")] private static extern void RetroSpeech_Stop();
#endif

        public static bool Enabled
        {
            get
            {
                var s = App.Career?.settings;
                return s != null && s.commentary && s.playByPlayVoice && s.sfxVolume > 0f;
            }
        }

        /// <summary>
        /// I36: reads a line aloud whatever the commentary settings (the comic's READ ALOUD option); still silent when
        /// the sound effects volume is at zero. A new line cuts off the last one.
        /// </summary>
        public static void Read(string line)
        {
            var s = App.Career?.settings;
            if (s == null || s.sfxVolume <= 0f || string.IsNullOrEmpty(line)) return;
            string lang = Loc.Language == Loc.Spanish ? "es-MX" : "en-US";
            float volume = UnityEngine.Mathf.Clamp01(s.sfxVolume * 1.1f);
#if UNITY_IOS && !UNITY_EDITOR
            RetroSpeech_Say(Announcer.ForSpeech(line), lang, Announcer.CallRate, Announcer.CallPitch, volume);
#endif
        }

        /// <summary>Says a commentary line (a new line cuts off the last one).</summary>
        public static void Say(string line, bool hype)
        {
            if (!Enabled || string.IsNullOrEmpty(line)) return;
            string translated = Loc.T(line);
            string spoken = Announcer.ForSpeech(translated);
            // A line with no Spanish yet stays in the English voice (a Spanish voice reading English sounds wrong).
            string lang = Loc.Language == Loc.Spanish && translated != line ? "es-MX" : "en-US";
            float volume = UnityEngine.Mathf.Clamp01(App.Career.settings.sfxVolume * 1.1f);
#if UNITY_IOS && !UNITY_EDITOR
            RetroSpeech_Say(spoken, lang, hype ? Announcer.HypeRate : Announcer.CallRate, hype ? Announcer.HypePitch : Announcer.CallPitch, volume);
#endif
        }

        public static void Stop()
        {
#if UNITY_IOS && !UNITY_EDITOR
            RetroSpeech_Stop();
#endif
        }
    }
}
