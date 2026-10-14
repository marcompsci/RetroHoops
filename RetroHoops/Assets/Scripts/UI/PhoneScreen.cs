using System.Collections.Generic;
using System.IO;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// 14 Oct 2026 (Omari's request): your player's phone. Open it from the main menu, the story screen or the pause
    /// menu. It has three apps:
    /// - MUSIC: the Apple Music playlist (credits, a + to add each song in Apple Music, shuffle that reshuffles
    ///   every loop).
    /// - TEXTS: your agent Dee Marlowe (SUMMER STORY) and the quick chat from LIVE and two-phone games.
    /// - FEED: made-up posts about your games, with PLAY OF THE GAME photos.
    /// </summary>
    public sealed class PhoneScreen : MonoBehaviour
    {
        public enum App { Music = 0, Texts = 1, Feed = 2 }

        private static PhoneScreen _open;
        private App _app;
        private RectTransform _content;
        private readonly TextMeshProUGUI[] _tabLabels = new TextMeshProUGUI[3];
        private readonly List<Texture2D> _photos = new List<Texture2D>();
        private bool _dirty;
        private float _rebuildAt;

        public static bool IsOpen => _open != null;

        /// <summary>The phone was closed (menus refresh their PHONE badge).</summary>
        public static event System.Action Closed;

        /// <summary>Unread texts plus new FEED posts (the badge on the PHONE buttons).</summary>
        public static int Badge()
        {
            var d = Core.App.Career;
            if (d == null) return 0;
            return AgentTexts.Unread(d.phone, d.story) + SocialFeed.Unseen(d.phone);
        }

        /// <summary>"PHONE" or "PHONE (3)".</summary>
        public static string ButtonLabel()
        {
            int n = Badge();
            return n > 0 ? "PHONE (" + n + ")" : "PHONE";
        }

        public static PhoneScreen Open(App app = App.Music)
        {
            if (_open != null) { _open.Show(app); return _open; }
            var go = new GameObject("Phone");
            _open = go.AddComponent<PhoneScreen>();
            _open.Build();
            _open.Show(app);
            return _open;
        }

        public static void CloseIfOpen()
        {
            if (_open != null) _open.Close();
        }

        private void OnDestroy()
        {
            if (_open == this) _open = null;
            if (PhoneMusic.Existing != null) PhoneMusic.Existing.Changed -= OnMusicChanged;
            foreach (var t in _photos) if (t != null) Destroy(t);
        }

        private void PhoneMusicChanged(bool on)
        {
            var m = PhoneMusic.Instance;
            if (on) m.Changed += OnMusicChanged;
            else m.Changed -= OnMusicChanged;
        }

        private void OnMusicChanged()
        {
            if (_app != App.Music) return;
            _dirty = true;
        }

        private void Update()
        {
            // Rebuild the MUSIC app when the song changes (at most twice a second).
            if (_dirty && Time.unscaledTime >= _rebuildAt)
            {
                _dirty = false;
                _rebuildAt = Time.unscaledTime + 0.5f;
                if (_app == App.Music) Rebuild();
            }
        }

        private void Build()
        {
            var canvas = UiKit.CreateScreenCanvas("PhoneCanvas", 70);
            canvas.transform.SetParent(transform, false);
            var scrim = UiKit.Panel(canvas.transform, Theme.Scrim, name: "Scrim");
            UiKit.Stretch(scrim.rectTransform);
            scrim.raycastTarget = true;
            var safe = UiKit.SafeArea(canvas.transform);

            // The phone: a dark slab with a bezel.
            var bezel = UiKit.Panel(safe, new Color32(0x05, 0x05, 0x0A, 255), name: "Bezel");
            UiKit.Band(bezel.rectTransform, 0.025f, 0.975f, 40f);
            bezel.raycastTarget = true;
            var screen = UiKit.Panel(bezel.transform, new Color32(0x12, 0x12, 0x22, 255), Theme.PanelSprite(), true, "Screen");
            UiKit.Stretch(screen.rectTransform, 14f);

            // Status bar: the phone's name and a close button.
            var top = UiKit.NewRect("Top", screen.transform);
            top.anchorMin = new Vector2(0f, 1f);
            top.anchorMax = new Vector2(1f, 1f);
            top.pivot = new Vector2(0.5f, 1f);
            top.sizeDelta = new Vector2(-40f, 110f);
            top.anchoredPosition = new Vector2(0f, -16f);
            var title = UiKit.Label(top, "RETRO PHONE", 44f, Theme.Cream, TextAlignmentOptions.Left, true);
            UiKit.Stretch(title.rectTransform);
            title.rectTransform.offsetMin = new Vector2(20f, 0f);
            var close = UiKit.Button(top, "CLOSE", Close, ButtonStyle.Ghost, 90f, 32f);
            var crt = (RectTransform)close.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(1f, 0.5f);
            crt.pivot = new Vector2(1f, 0.5f);
            crt.sizeDelta = new Vector2(220f, 90f);
            crt.anchoredPosition = new Vector2(-10f, 0f);

            // App tabs.
            var tabs = UiKit.Row(screen.transform, 12f, "Apps");
            tabs.anchorMin = new Vector2(0f, 1f);
            tabs.anchorMax = new Vector2(1f, 1f);
            tabs.pivot = new Vector2(0.5f, 1f);
            tabs.sizeDelta = new Vector2(-48f, 96f);
            tabs.anchoredPosition = new Vector2(0f, -136f);
            string[] names = { "MUSIC", "TEXTS", "FEED" };
            for (int i = 0; i < 3; i++)
            {
                var a = (App)i;
                var b = UiKit.Button(tabs, names[i], () => Show(a), ButtonStyle.Secondary, 90f, 32f);
                _tabLabels[i] = b.GetComponentInChildren<TextMeshProUGUI>();
            }

            var body = UiKit.NewRect("Body", screen.transform);
            body.anchorMin = Vector2.zero;
            body.anchorMax = Vector2.one;
            body.offsetMin = new Vector2(20f, 20f);
            body.offsetMax = new Vector2(-20f, -250f);
            _content = UiKit.ScrollColumn(body, 18f, new RectOffset(16, 16, 12, 40));
            PhoneMusicChanged(true);
        }

        private void Close()
        {
            Core.App.SaveCareer();
            Destroy(gameObject);
            Closed?.Invoke();
        }

        private void Show(App app)
        {
            _app = app;
            var d = Core.App.Career;
            if (d != null)
            {
                if (app == App.Texts) AgentTexts.MarkRead(d.phone, d.story);
                if (app == App.Feed) SocialFeed.MarkSeen(d.phone);
            }
            Rebuild();
        }

        private void RefreshTabs()
        {
            var d = Core.App.Career;
            int texts = d != null ? AgentTexts.Unread(d.phone, d.story) : 0;
            int feed = d != null ? SocialFeed.Unseen(d.phone) : 0;
            string[] names = { "MUSIC", texts > 0 ? "TEXTS (" + texts + ")" : "TEXTS", feed > 0 ? "FEED (" + feed + ")" : "FEED" };
            for (int i = 0; i < 3; i++)
            {
                if (_tabLabels[i] == null) continue;
                _tabLabels[i].text = Loc.T(names[i]);
                _tabLabels[i].color = (int)_app == i ? Theme.Gold : Theme.ButtonText;
            }
        }

        private void Rebuild()
        {
            if (_content == null) return;
            for (int i = _content.childCount - 1; i >= 0; i--) Destroy(_content.GetChild(i).gameObject);
            foreach (var t in _photos) if (t != null) Destroy(t);
            _photos.Clear();
            RefreshTabs();
            switch (_app)
            {
                case App.Music: BuildMusic(); break;
                case App.Texts: BuildTexts(); break;
                default: BuildFeed(); break;
            }
        }

        // ------------------------------------------------------------------ helpers

        private TextMeshProUGUI Line(string text, float size, Color color, float height, TextAlignmentOptions align = TextAlignmentOptions.Left, bool bold = false)
        {
            var t = UiKit.Label(_content, text, size, color, align, bold);
            t.textWrappingMode = TextWrappingModes.Normal;
            UiKit.Size(t, height);
            return t;
        }

        private RectTransform Card(Color color, float height)
        {
            var p = UiKit.Panel(_content, color, Theme.PanelSprite(), true, "Card");
            UiKit.Size(p, height);
            return p.rectTransform;
        }

        private static TextMeshProUGUI CardText(RectTransform card, string text, float size, Color color, Vector2 min, Vector2 max,
                                                TextAlignmentOptions align = TextAlignmentOptions.Left, bool bold = false)
        {
            var t = UiKit.Label(card, text, size, color, align, bold);
            t.rectTransform.anchorMin = min;
            t.rectTransform.anchorMax = max;
            t.rectTransform.offsetMin = new Vector2(24f, 0f);
            t.rectTransform.offsetMax = new Vector2(-24f, 0f);
            t.textWrappingMode = TextWrappingModes.Normal;
            t.enableAutoSizing = true;
            t.fontSizeMax = size;
            t.fontSizeMin = Mathf.Max(16f, size * 0.6f);
            return t;
        }

        private static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin = default, Vector2 offMax = default)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
        }

        // ------------------------------------------------------------------ MUSIC

        private void BuildMusic()
        {
            var m = PhoneMusic.Instance;
            var d = Core.App.Career;
            bool on = d == null || d.phone.musicOn;
            Line("PLAYLIST", 40f, Theme.Pink, 56f, TextAlignmentOptions.Left, true);

            switch (m.State)
            {
                case PhoneMusic.Status.Unsupported:
                    Line("Apple Music plays here in the iPhone and iPad app.", 28f, Theme.Muted, 80f);
                    break;
                case PhoneMusic.Status.NotConnected:
                    Line("Play the Retro Hoops playlist from Apple Music while you play. Full songs need an Apple Music subscription.", 28f, Theme.Cream, 120f);
                    UiKit.Button(_content, "CONNECT APPLE MUSIC", m.Connect, ButtonStyle.Primary, 110f, 40f);
                    break;
                case PhoneMusic.Status.Denied:
                    Line("Apple Music access is off. Turn it on in the iPhone Settings app ► Retro Hoops.", 28f, Theme.Muted, 100f);
                    break;
                case PhoneMusic.Status.NoSubscription:
                    Line("Full songs need an Apple Music subscription. The game's own soundtrack plays meanwhile. Tap + to open any song in Apple Music.", 28f, Theme.Muted, 130f);
                    break;
                case PhoneMusic.Status.Loading:
                    Line("Finding the songs on Apple Music...", 28f, Theme.Muted, 60f);
                    break;
                case PhoneMusic.Status.Failed:
                    Line("Couldn't reach Apple Music. Check the internet connection.", 28f, Theme.Muted, 80f);
                    UiKit.Button(_content, "TRY AGAIN", m.Retry, ButtonStyle.Secondary, 100f, 36f);
                    break;
                case PhoneMusic.Status.NoCleanSongs:
                    Line("None of these songs have a clean version on Apple Music right now, so they're left out.", 28f, Theme.Muted, 100f);
                    break;
                default:
                    NowPlaying(m, on);
                    break;
            }

            // The songs: credits and a + each. In the playlist, only the clean versions found on Apple Music.
            Line("SONGS", 34f, Theme.Gold, 54f, TextAlignmentOptions.Left, true);
            if (m.State == PhoneMusic.Status.Ready)
            {
                foreach (var s in m.Songs) SongRow(PhonePlaylist.Find(s.key), s, m);
                int left = PhonePlaylist.Tracks.Length - m.Songs.Count;
                if (left > 0) Line(left + " more left out (no clean version on Apple Music).", 24f, Theme.Muted, 50f);
            }
            else if (m.State != PhoneMusic.Status.NoCleanSongs)
            {
                foreach (var t in PhonePlaylist.Tracks) SongRow(t, null, m);
            }
            Line("Songs play from Apple Music. Retro Hoops doesn't own or include them. Credits as listed on Apple Music.", 22f, Theme.Muted, 80f);
        }

        private void NowPlaying(PhoneMusic m, bool on)
        {
            var card = Card(new Color32(0x1E, 0x1E, 0x36, 255), 330f);
            var entry = m.Songs.Find(s => s.key == m.NowPlayingKey);
            var track = entry != null ? PhonePlaylist.Find(entry.key) : null;
            if (entry?.art != null)
            {
                var art = UiKit.Picture(card, entry.art, "Artwork");
                Anchor(art.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
                art.rectTransform.pivot = new Vector2(0f, 0.5f);
                art.rectTransform.sizeDelta = new Vector2(230f, 230f);
                art.rectTransform.anchoredPosition = new Vector2(28f, 20f);
            }
            float left = entry?.art != null ? 0.33f : 0f;
            string title = track != null ? track.DisplayTitle : on ? "Starting the playlist..." : "Playlist is off";
            string credits = track != null ? track.Credits : "";
            CardText(card, title, 34f, Theme.Cream, new Vector2(left, 0.66f), new Vector2(1f, 0.92f), TextAlignmentOptions.Left, true);
            CardText(card, credits, 26f, Theme.Muted, new Vector2(left, 0.46f), new Vector2(1f, 0.66f));
            if (entry != null && !string.IsNullOrEmpty(entry.album))
                CardText(card, entry.album, 22f, Theme.Muted, new Vector2(left, 0.32f), new Vector2(1f, 0.46f));

            var controls = UiKit.Row(card, 12f, "Controls");
            Anchor(controls, new Vector2(left, 0.04f), new Vector2(1f, 0.3f), new Vector2(20f, 0f), new Vector2(-20f, 0f));
            UiKit.Button(controls, "BACK", m.Previous, ButtonStyle.Ghost, 80f, 26f);
            UiKit.Button(controls, m.IsPlaying ? "PAUSE" : "PLAY", m.TogglePause, ButtonStyle.Primary, 80f, 28f);
            UiKit.Button(controls, "NEXT", m.Next, ButtonStyle.Ghost, 80f, 26f);

            var d = Core.App.Career;
            Line("SHUFFLE  ·  LOOP " + ((d?.phone.cycle ?? 0) + 1) + "  ·  New order every loop", 24f, Theme.Cyan, 44f);
            UiControls.ToggleRow(_content, "PLAY THE PLAYLIST", on, v => PhoneMusic.Instance.SetEnabled(v));
        }

        private void SongRow(PlaylistTrack t, PhoneMusic.Entry entry, PhoneMusic m)
        {
            if (t == null) return;
            bool current = entry != null && entry.key == m.NowPlayingKey;
            var card = Card(current ? new Color32(0x2A, 0x2A, 0x4A, 255) : new Color32(0x1A, 0x1A, 0x2E, 255), 130f);
            if (entry != null)
            {
                // Tap a song to play it now.
                var b = card.gameObject.AddComponent<Button>();
                var img = card.GetComponent<Image>();
                img.raycastTarget = true;
                b.targetGraphic = img;
                string key = t.Key;
                b.onClick.AddListener(() => { Audio.AudioManager.Click(); PhoneMusic.Instance.PlaySong(key); });
            }
            CardText(card, (current ? "► " : "") + t.DisplayTitle, 30f, current ? Theme.Gold : Theme.Cream, new Vector2(0f, 0.5f), new Vector2(0.8f, 0.95f), TextAlignmentOptions.Left, true);
            CardText(card, t.Credits, 24f, Theme.Muted, new Vector2(0f, 0.08f), new Vector2(0.8f, 0.5f));
            // + : open the song in Apple Music to add it to your library.
            string k = t.Key;
            var plus = UiKit.Button(card, "+", () => PhoneMusic.OpenInAppleMusic(k), ButtonStyle.Secondary, 90f, 48f);
            var prt = (RectTransform)plus.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(1f, 0.5f);
            prt.pivot = new Vector2(1f, 0.5f);
            prt.sizeDelta = new Vector2(110f, 100f);
            prt.anchoredPosition = new Vector2(-16f, 0f);
        }

        // ------------------------------------------------------------------ TEXTS

        private void BuildTexts()
        {
            var d = Core.App.Career;
            Line(AgentTexts.Name, 38f, (Color)new Color32(0x06, 0xD6, 0xA0, 255), 54f, TextAlignmentOptions.Left, true);
            Line(AgentTexts.Role + "  ·  SUMMER STORY", 24f, Theme.Muted, 40f);
            if (d != null)
            {
                var arrived = AgentTexts.Arrived(d.story);
                for (int i = 0; i < arrived.Count; i++)
                {
                    var t = arrived[i];
                    Bubble(t.Text, false);
                    int reply = AgentTexts.ReplyOf(d.phone, i);
                    if (reply >= 0)
                    {
                        Bubble(t.Replies[reply], true);
                        Bubble(t.FollowUps[reply], false);
                    }
                    else
                    {
                        var row = UiKit.Row(_content, 12f, "Replies");
                        UiKit.Size(row, 96f);
                        int index = i;
                        for (int r = 0; r < 2; r++)
                        {
                            int choice = r;
                            UiKit.Button(row, t.Replies[r], () =>
                            {
                                if (AgentTexts.Reply(d.phone, d.story, index, choice)) { Core.App.SaveCareer(); Haptics.Light(); }
                                Rebuild();
                            }, ButtonStyle.Ghost, 90f, 24f);
                        }
                    }
                }
                if (d.story.cleared < StoryMode.Chapters)
                    Line("Dee texts before every SUMMER STORY chapter and after you clear it. PLAY ► SUMMER STORY.", 22f, Theme.Muted, 70f);
            }

            Line("LIVE CHAT", 38f, Theme.Cyan, 70f, TextAlignmentOptions.Left, true);
            if (QuickChatLog.Lines.Count == 0)
                Line("In LIVE and two-phone games, tap CHAT next to the pause button to send a quick message. Messages from this session show here.", 24f, Theme.Muted, 110f);
            foreach (var l in QuickChatLog.Lines) Bubble((l.mine ? "" : l.from + ":  ") + l.text, l.mine);
            if (d != null) UiControls.ToggleRow(_content, "QUICK CHAT", d.phone.quickChat, v => { d.phone.quickChat = v; Core.App.SaveCareer(); });
        }

        private void Bubble(string text, bool mine)
        {
            var row = UiKit.NewRect("Bubble", _content);
            int lines = 1 + Mathf.Max(0, text.Length - 1) / 30;
            float h = 40f + lines * 36f;
            UiKit.Size(row.gameObject.AddComponent<LayoutElement>(), h);
            var bubble = UiKit.Panel(row, mine ? (Color)new Color32(0x20, 0x38, 0xEC, 255) : new Color32(0x2A, 0x2A, 0x45, 255), Theme.PanelSprite(), true, "Fill");
            Anchor(bubble.rectTransform, new Vector2(mine ? 0.22f : 0f, 0f), new Vector2(mine ? 1f : 0.78f, 1f));
            var t = UiKit.Label(bubble.transform, text, 28f, Theme.Cream, mine ? TextAlignmentOptions.Right : TextAlignmentOptions.Left);
            UiKit.Stretch(t.rectTransform, 18f);
            t.textWrappingMode = TextWrappingModes.Normal;
            t.enableAutoSizing = true;
            t.fontSizeMax = 28f;
            t.fontSizeMin = 18f;
        }

        // ------------------------------------------------------------------ FEED

        private void BuildFeed()
        {
            var d = Core.App.Career;
            var feed = d?.phone.feed;
            if (feed == null || feed.Count == 0)
            {
                Line("Nothing yet. Play a game and the city starts talking.", 28f, Theme.Muted, 90f);
                return;
            }
            foreach (var p in feed) Post(p);
            Line("Made-up posts from the Retro Hoops world. Nothing here is real or leaves your phone.", 22f, Theme.Muted, 70f);
        }

        private void Post(FeedPost p)
        {
            var photo = LoadPhoto(p.photo);
            float photoH = photo != null ? 640f * photo.height / Mathf.Max(1f, photo.width) : 0f;
            photoH = Mathf.Min(photoH, 720f);
            float textH = 60f + (1 + p.text.Length / 34) * 36f;
            float commentsH = p.comments.Count * 38f;
            var card = Card(new Color32(0x1A, 0x1A, 0x2E, 255), 110f + textH + photoH + commentsH + 60f);
            var col = UiKit.Column(card, 8f, new RectOffset(24, 24, 20, 20), "Post");
            UiKit.Stretch(col);

            Color accent = ColorUtility.TryParseHtmlString(p.color, out var c) ? c : Theme.Cyan;
            var head = UiKit.Label(col, "<color=#" + ColorUtility.ToHtmlStringRGB(accent) + ">" + p.account + "</color>  <color=#8D99AE>" + p.handle + "</color>", 28f, Theme.Cream, TextAlignmentOptions.Left, true);
            UiKit.Size(head, 44f);
            if (!string.IsNullOrEmpty(p.tag))
                UiKit.Size(UiKit.Label(col, p.tag, 24f, Theme.Gold, TextAlignmentOptions.Left, true), 34f);
            var body = UiKit.Label(col, p.text, 28f, Theme.Cream, TextAlignmentOptions.Left);
            body.textWrappingMode = TextWrappingModes.Normal;
            UiKit.Size(body, textH);
            if (photo != null)
            {
                var pic = UiKit.Picture(col, photo, "Photo");
                var le = pic.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = le.minHeight = photoH;
                var fit = pic.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fit.aspectRatio = photo.width / (float)Mathf.Max(1, photo.height);
            }
            UiKit.Size(UiKit.Label(col, Count(p.likes) + " LIKES  ·  " + Count(p.reposts) + " REPOSTS", 22f, Theme.Pink, TextAlignmentOptions.Left, true), 34f);
            foreach (var cm in p.comments)
                UiKit.Size(UiKit.Label(col, cm, 22f, Theme.Muted, TextAlignmentOptions.Left), 34f);
        }

        private static string Count(int n) => n >= 10000 ? (n / 1000) + "K" : n >= 1000 ? (n / 100 / 10f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "K" : n.ToString();

        private Texture2D LoadPhoto(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            try
            {
                string path = Path.Combine(FeedPhotos.Folder, name);
                if (!File.Exists(path)) return null;
                var tex = new Texture2D(2, 2, TextureFormat.RGB24, false) { filterMode = FilterMode.Point };
                if (!tex.LoadImage(File.ReadAllBytes(path))) { Destroy(tex); return null; }
                _photos.Add(tex);
                return tex;
            }
            catch { return null; }
        }
    }

    /// <summary>Quick chat messages from this session (the TEXTS app's LIVE CHAT; not saved).</summary>
    public static class QuickChatLog
    {
        public struct Entry { public string from, text; public bool mine; }
        public static readonly List<Entry> Lines = new List<Entry>();

        public static void Add(string from, string text, bool mine)
        {
            Lines.Add(new Entry { from = from ?? "", text = text ?? "", mine = mine });
            while (Lines.Count > 30) Lines.RemoveAt(0);
        }
    }

    /// <summary>Where PLAY OF THE GAME photos are kept (the app's own storage).</summary>
    public static class FeedPhotos
    {
        public static string Folder => Path.Combine(Application.persistentDataPath, "Feed");

        public static void Delete(IEnumerable<string> names)
        {
            foreach (var n in names)
            {
                try
                {
                    string path = Path.Combine(Folder, n);
                    if (File.Exists(path)) File.Delete(path);
                }
                catch { }
            }
        }
    }
}
