using CallerRetroBall.Audio;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// The Music Player: every track on the Retro Hoops soundtrack with PLAY, a pixel level meter, and
    /// which track plays on the menus and in matches (or shuffle). All music is synthesised in code.
    /// </summary>
    public sealed class MusicPlayer : MonoBehaviour
    {
        private const int Bars = 16;
        private readonly float[] _levels = new float[Bars];
        private readonly float[] _shown = new float[Bars];
        private Image[] _bars;
        private TextMeshProUGUI _now;
        private RectTransform _list;
        private int _selected;

        public static MusicPlayer Open()
        {
            var existing = FindAnyObjectByType<MusicPlayer>();
            if (existing != null) return existing;
            var go = new GameObject("MusicPlayer");
            var p = go.AddComponent<MusicPlayer>();
            p.Build();
            return p;
        }

        private void Build()
        {
            _selected = AudioManager.CurrentTrack;
            var canvas = UiKit.CreateScreenCanvas("MusicCanvas", 45);
            canvas.transform.SetParent(transform, false);
            var scrim = UiKit.Panel(canvas.transform, Theme.Ink, name: "Backdrop");
            UiKit.Stretch(scrim.rectTransform);
            scrim.raycastTarget = true;
            var safe = UiKit.SafeArea(canvas.transform);

            var title = UiKit.ShadowLabel(safe, "MUSIC PLAYER", 60f, Theme.Cream, Theme.Pink, 6f);
            UiKit.Band((RectTransform)title.transform.parent, 0.92f, 0.99f, 24f);

            // Level meter: chunky pixel bars.
            var meter = UiKit.Panel(safe, new Color32(0x0B, 0x0B, 0x16, 255), name: "Meter");
            UiKit.Band(meter.rectTransform, 0.78f, 0.91f, 60f);
            var row = UiKit.Row(meter.transform, 8f, "Bars");
            UiKit.Stretch(row, 16f);
            row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.LowerCenter;
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            row.GetComponent<HorizontalLayoutGroup>().childControlHeight = false;
            _bars = new Image[Bars];
            for (int i = 0; i < Bars; i++)
            {
                var holder = UiKit.NewRect("Bar", row);
                holder.sizeDelta = new Vector2(0f, 180f);
                _bars[i] = UiKit.Panel(holder, Color.Lerp(Theme.Cyan, Theme.Pink, i / (float)Bars), name: "Fill");
                var rt = _bars[i].rectTransform;
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.sizeDelta = new Vector2(0f, 8f);
            }
            _now = UiKit.Label(safe, "", 32f, Theme.Gold, TextAlignmentOptions.Center, true);
            UiKit.Band(_now.rectTransform, 0.72f, 0.78f, 24f);

            var holderList = UiKit.NewRect("List", safe);
            UiKit.Band(holderList, 0.11f, 0.71f, 0f);
            _list = UiKit.ScrollColumn(holderList, 12f, new RectOffset(40, 40, 8, 30));

            var footer = UiKit.Row(safe, 20f, "Footer");
            UiKit.Band(footer, 0.015f, 0.095f, 40f);
            UiKit.Button(footer, "STOP", () =>
            {
                // The phone's beat tape pauses (stopping it would just start the next beat).
                if (AudioManager.PlaylistOwnsMusic) AudioManager.Pause(); else AudioManager.StopMusic();
                Refresh();
            }, ButtonStyle.Ghost, 110f, 40f);
            UiKit.Button(footer, "CLOSE", Close, ButtonStyle.Primary, 110f, 40f);
            Refresh();
        }

        private void Close()
        {
            // Back to the music this screen should be playing.
            if (AudioManager.PlaylistOwnsMusic) AudioManager.Resume();
            else if (!AudioManager.IsPlaying) AudioManager.PlayTrack(AudioManager.CurrentTrack);
            Destroy(gameObject);
        }

        private void Refresh()
        {
            for (int i = _list.childCount - 1; i >= 0; i--) Destroy(_list.GetChild(i).gameObject);
            var s = App.Career.settings;
            int playing = AudioManager.IsPlaying || AudioManager.IsLoading ? AudioManager.CurrentTrack : -1;
            _now.text = playing >= 0 ? Loc.T("NOW PLAYING") + ":  " + Soundtrack.Title(playing).ToUpperInvariant() : Loc.T("STOPPED");

            var names = new string[Soundtrack.Count + 1];
            names[0] = Loc.T("DEFAULT");
            for (int t = 0; t < Soundtrack.Count; t++) names[t + 1] = Soundtrack.Title(t).ToUpperInvariant();
            UiControls.ChoiceRow(_list, "MENU MUSIC", names, s.musicMenu + 1, i => { s.musicMenu = i - 1; App.SaveCareer(); });
            var gameNames = new string[Soundtrack.Count + 2];
            gameNames[0] = Loc.T("SHUFFLE");
            gameNames[1] = Loc.T("DEFAULT");
            for (int t = 0; t < Soundtrack.Count; t++) gameNames[t + 2] = Soundtrack.Title(t).ToUpperInvariant();
            UiControls.ChoiceRow(_list, "MATCH MUSIC", gameNames, s.musicGame + 2, i => { s.musicGame = i - 2; App.SaveCareer(); });

            for (int t = 0; t < Soundtrack.Count; t++)
            {
                int track = t;
                var row = UiKit.Row(_list, 10f, "Track");
                row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
                UiKit.Size(row, 100f);
                string bpm = t >= Soundtrack.ClassicCount ? "  ·  " + Mathf.RoundToInt(Soundtrack.Bpm(t)) + " BPM" : "";
                var label = UiKit.Label(row, (t + 1).ToString("00") + "  <b>" + Soundtrack.Title(t).ToUpperInvariant() + "</b>\n<size=22><color=#8D99AE>"
                                        + Loc.T(Soundtrack.Blurb(t)) + bpm + "</color></size>", 28f, t == playing ? Theme.Gold : Theme.Cream, TextAlignmentOptions.Left);
                label.textWrappingMode = TextWrappingModes.Normal;
                UiKit.Size(label).flexibleWidth = 1f;
                var b = UiKit.Button(row, t == playing ? (AudioManager.IsLoading ? "LOADING" : "PLAYING") : "PLAY", () =>
                {
                    _selected = track;
                    // Picking a track here takes over from the phone's playlist (turn it back on in the MUSIC app).
                    if (AudioManager.PlaylistOwnsMusic) Core.PhoneMusic.Existing?.SetEnabled(false);
                    AudioManager.PlayTrack(track);
                    Refresh();
                }, t == playing ? ButtonStyle.Primary : ButtonStyle.Secondary, 90f, 26f);
                UiKit.Size(b, 90f, 190f);
            }
            UiKit.Size(UiKit.Label(_list, "Every track is written and played by Retro Hoops itself: the chiptune songs and the beat tape. Songs are composed the first time you play them. Playing one here turns off the phone's playlist (turn it back on in the phone's MUSIC app).", 24f, Theme.Muted), 130f);
        }

        private bool _wasLoading;

        private void Update()
        {
            if (_wasLoading && !AudioManager.IsLoading) Refresh();
            _wasLoading = AudioManager.IsLoading;
            AudioManager.Levels(_levels);
            for (int i = 0; i < Bars; i++)
            {
                // Fast attack, slow fall, snapped to 8-pixel steps.
                _shown[i] = _levels[i] > _shown[i] ? _levels[i] : Mathf.Max(0f, _shown[i] - Time.unscaledDeltaTime * 1.6f);
                float h = Mathf.Max(8f, Mathf.Round(_shown[i] * 170f / 8f) * 8f);
                _bars[i].rectTransform.sizeDelta = new Vector2(0f, h);
            }
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame)) Close();
#endif
        }
    }
}
