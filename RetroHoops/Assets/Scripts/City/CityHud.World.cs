using System;
using System.Collections.Generic;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using CallerRetroBall.UI;
using CallerRetroBall.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CallerRetroBall.City
{
    /// <summary>One comic panel ready to show: the backdrop, who's talking (drawn from their sprite sheet) and the words.</summary>
    public sealed class ComicPanelView
    {
        public Texture2D Backdrop;
        /// <summary>The speaker's sprite sheet and the front-facing frame in it (UV rect); null = a caption-only panel.</summary>
        public Texture2D Sheet;
        public Rect Frame;
        public string Speaker, Line, Caption, Sfx;
    }

    /// <summary>One region on the WORLD MAP.</summary>
    public struct WorldMapEntry
    {
        public string Id, Name, State, Status, Detail;
        public float X, Y;
        public bool Here, Open, Next;
    }

    // 28 Oct 2026 (Omari, BALLER WORLD): the guide (objective ticker and arrow), the errand clock, the comic pages and
    // the world map.
    public sealed partial class CityHud
    {
        private TextMeshProUGUI _ticker, _route, _timer, _arrowLabel;
        private RectTransform _arrow;
        private Image _arrowImage;
        private bool _arrowShown;
        private readonly List<UnityEngine.Object> _hudOwned = new List<UnityEngine.Object>();

        private void BuildWorld(RectTransform safe)
        {
            // The objective ticker sits under the top bar.
            var band = UiKit.Panel(safe, new Color(0.04f, 0.04f, 0.09f, 0.55f), name: "Ticker");
            band.rectTransform.anchorMin = new Vector2(0f, 1f);
            band.rectTransform.anchorMax = new Vector2(1f, 1f);
            band.rectTransform.pivot = new Vector2(0.5f, 1f);
            band.rectTransform.sizeDelta = new Vector2(0f, 96f);
            band.rectTransform.anchoredPosition = new Vector2(0f, -132f);
            _ticker = UiKit.Label(band.rectTransform, "", 30f, Theme.Gold, TextAlignmentOptions.Left, true, "Objective");
            _ticker.rectTransform.anchorMin = new Vector2(0f, 0.45f);
            _ticker.rectTransform.anchorMax = new Vector2(1f, 1f);
            _ticker.rectTransform.offsetMin = new Vector2(32f, 0f);
            _ticker.rectTransform.offsetMax = new Vector2(-24f, 0f);
            _ticker.enableAutoSizing = true;
            _ticker.fontSizeMin = 18f;
            _ticker.fontSizeMax = 30f;
            _route = UiKit.Label(band.rectTransform, "", 24f, Theme.Cream, TextAlignmentOptions.Left, false, "Route");
            _route.rectTransform.anchorMin = new Vector2(0f, 0f);
            _route.rectTransform.anchorMax = new Vector2(1f, 0.48f);
            _route.rectTransform.offsetMin = new Vector2(32f, 0f);
            _route.rectTransform.offsetMax = new Vector2(-24f, 0f);
            _route.enableAutoSizing = true;
            _route.fontSizeMin = 14f;
            _route.fontSizeMax = 24f;
            band.gameObject.SetActive(false);
            _tickerBand = band.gameObject;

            // The guide arrow (a gold triangle) and its distance.
            var tri = new PixelCanvas(16, 16);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    int half = y / 2;
                    if (Math.Abs(x - 8) <= 7 - half) tri.Set(x, y, Math.Abs(x - 8) >= 6 - half ? new RgbColor(20, 20, 31) : new RgbColor(255, 209, 102));
                }
            var tex = TextureFactory.ToTexture(tri, "city.arrow");
            _hudOwned.Add(tex);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
            _hudOwned.Add(sprite);
            _arrowImage = UiKit.Panel(_canvas.transform, Color.white, sprite, false, "GuideArrow");
            _arrow = _arrowImage.rectTransform;
            _arrow.anchorMin = _arrow.anchorMax = new Vector2(0.5f, 0.5f);
            _arrow.sizeDelta = new Vector2(84f, 84f);
            _arrowLabel = UiKit.Label(_canvas.transform, "", 26f, Theme.Cream, TextAlignmentOptions.Center, true, "GuideDistance");
            _arrowLabel.rectTransform.anchorMin = _arrowLabel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _arrowLabel.rectTransform.sizeDelta = new Vector2(200f, 40f);
            UiKit.ApplyTextShadow(_arrowLabel);
            _arrow.gameObject.SetActive(false);
            _arrowLabel.gameObject.SetActive(false);

            // The errand clock.
            _timer = UiKit.Label(safe, "", 64f, Theme.Gold, TextAlignmentOptions.Center, true, "Timer");
            UiKit.Band(_timer.rectTransform, 0.7f, 0.76f, 40f);
            UiKit.ApplyTextShadow(_timer);
        }

        private GameObject _tickerBand;

        /// <summary>I36: called as each comic page shows (null when the comic closes), for reading it aloud.</summary>
        public Action<ComicPanelView> OnComicPage;

        /// <summary>The objective ticker (null hides it).</summary>
        public void SetTicker(string objective, string route)
        {
            bool on = !string.IsNullOrEmpty(objective);
            if (_tickerBand.activeSelf != on) _tickerBand.SetActive(on);
            if (!on) return;
            if (_ticker.text != objective) _ticker.text = objective;
            route = route ?? "";
            if (_route.text != route) _route.text = route;
        }

        /// <summary>
        /// The guide arrow. <paramref name="screen"/> is the target in screen pixels; off screen (or behind the HUD) the
        /// arrow sits at the edge and points at it.
        /// </summary>
        public void SetGuide(bool show, Vector2 screen, string distance)
        {
            if (!show || CardOpen)
            {
                if (_arrowShown) { _arrow.gameObject.SetActive(false); _arrowLabel.gameObject.SetActive(false); _arrowShown = false; }
                return;
            }
            if (!_arrowShown) { _arrow.gameObject.SetActive(true); _arrowLabel.gameObject.SetActive(true); _arrowShown = true; }
            var rect = (RectTransform)_canvas.transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, null, out var local);
            var size = rect.rect.size;
            float marginX = 70f, marginTop = 300f, marginBottom = 120f;
            float minX = -size.x / 2 + marginX, maxX = size.x / 2 - marginX, minY = -size.y / 2 + marginBottom, maxY = size.y / 2 - marginTop;
            bool inside = local.x > minX && local.x < maxX && local.y > minY && local.y < maxY;
            Vector2 pos;
            float angle;
            if (inside)
            {
                // Over the target, pointing down at it, bobbing.
                pos = local + new Vector2(0f, 90f + (UiMotion.Reduced ? 0f : Mathf.Sin(Time.unscaledTime * 4f) * 6f)); // a gentle bob (none with Reduce Motion)
                angle = 180f;
            }
            else
            {
                var dir = local.normalized;
                float tx = Mathf.Abs(dir.x) > 1e-4f ? (dir.x > 0 ? maxX : minX) / dir.x : float.MaxValue;
                float ty = Mathf.Abs(dir.y) > 1e-4f ? (dir.y > 0 ? maxY : minY) / dir.y : float.MaxValue;
                pos = dir * Mathf.Min(Mathf.Abs(tx), Mathf.Abs(ty));
                angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            }
            _arrow.anchoredPosition = pos;
            _arrow.localRotation = Quaternion.Euler(0f, 0f, angle);
            _arrowLabel.rectTransform.anchoredPosition = pos + new Vector2(0f, inside ? 56f : -64f);
            if (_arrowLabel.text != distance) _arrowLabel.text = distance;
        }

        public void SetTimer(string text)
        {
            text = UiMotion.Tabular(text ?? ""); // the errand clock counts down without jittering
            if (_timer.text != text) _timer.text = text;
        }

        // ------------------------------------------------------------------ the comic

        /// <summary>
        /// Comic pages: one big panel at a time (backdrop, caption box, the speaker drawn from their sprite, a speech
        /// bubble, a sound word). Tap NEXT (or the panel) to turn the page; SKIP closes it.
        /// </summary>
        public void ShowComic(string header, string subtitle, IList<ComicPanelView> panels, Action done)
        {
            CloseCard(false);
            SetControlsVisible(false);
            var root = UiKit.NewRect("Comic", _canvas.transform);
            UiKit.Stretch(root);
            _card = root.gameObject;
            var scrim = UiKit.Panel(root, new Color(0.04f, 0.04f, 0.09f, 0.94f), name: "Scrim");
            UiKit.Stretch(scrim.rectTransform);
            scrim.raycastTarget = true;
            var safe = UiKit.SafeArea(root);
            var title = UiKit.Label(safe, header, 46f, Theme.Gold, TextAlignmentOptions.Center, true, "Header");
            UiKit.Band(title.rectTransform, 0.9f, 0.96f, 40f);
            UiKit.ApplyTextShadow(title);
            var sub = UiKit.Label(safe, subtitle ?? "", 28f, Theme.Cream, TextAlignmentOptions.Center, false, "Subtitle");
            UiKit.Band(sub.rectTransform, 0.86f, 0.9f, 40f);
            var page = UiKit.NewRect("Page", safe);
            UiKit.Band(page, 0.2f, 0.85f, 36f);
            int index = 0;
            UiMotion.Enter(scrim, UiMotion.Modal, Vector2.zero); // the comic opens over a fading scrim
            void Show()
            {
                for (int i = page.childCount - 1; i >= 0; i--) Destroy(page.GetChild(i).gameObject);
                // 28 Oct 2026 (Emil Kowalski audit): a page turn slides the new page in from the right and fades it up
                // (160 ms ease-out) instead of swapping in a single frame.
                UiMotion.Enter(page, UiMotion.Page, new Vector2(28f, 0f));
                var p = panels[index];
                // The panel: a black border round the backdrop (120:72).
                var frame = UiKit.Panel(page, new Color32(0x14, 0x14, 0x1F, 255), name: "Frame");
                frame.rectTransform.anchorMin = new Vector2(0f, 0.38f);
                frame.rectTransform.anchorMax = new Vector2(1f, 1f);
                frame.rectTransform.offsetMin = frame.rectTransform.offsetMax = Vector2.zero;
                var art = UiKit.Picture(frame.rectTransform, p.Backdrop, "Backdrop");
                UiKit.Stretch(art.rectTransform, 10f);
                var fit = art.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fit.aspectRatio = ComicArt.Width / (float)ComicArt.Height;
                if (p.Sheet != null)
                {
                    var who = UiKit.Picture(art.rectTransform, p.Sheet, "Speaker");
                    who.uvRect = p.Frame;
                    who.rectTransform.anchorMin = new Vector2(0.04f, 0f);
                    who.rectTransform.anchorMax = new Vector2(0.4f, 0.96f);
                    who.rectTransform.offsetMin = who.rectTransform.offsetMax = Vector2.zero;
                    var whoFit = who.gameObject.AddComponent<AspectRatioFitter>();
                    whoFit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                    whoFit.aspectRatio = p.Frame.width / Mathf.Max(0.0001f, p.Frame.height) * (p.Sheet.width / (float)Mathf.Max(1, p.Sheet.height));
                }
                if (!string.IsNullOrEmpty(p.Caption))
                {
                    var cap = UiKit.Panel(art.rectTransform, new Color32(0xFF, 0xD1, 0x66, 255), name: "Caption");
                    cap.rectTransform.anchorMin = new Vector2(0.02f, 0.82f);
                    cap.rectTransform.anchorMax = new Vector2(0.78f, 0.98f);
                    cap.rectTransform.offsetMin = cap.rectTransform.offsetMax = Vector2.zero;
                    var ct = UiKit.Label(cap.rectTransform, p.Caption, 30f, new Color32(0x14, 0x14, 0x1F, 255), TextAlignmentOptions.Left, true);
                    UiKit.Stretch(ct.rectTransform, 10f);
                    ct.enableAutoSizing = true;
                    ct.fontSizeMin = 14f;
                    ct.fontSizeMax = 30f;
                }
                if (!string.IsNullOrEmpty(p.Sfx))
                {
                    var sfx = UiKit.Label(art.rectTransform, p.Sfx, 84f, new Color32(0xFF, 0x2E, 0x88, 255), TextAlignmentOptions.Center, true, "Sfx");
                    sfx.rectTransform.anchorMin = new Vector2(0.48f, 0.45f);
                    sfx.rectTransform.anchorMax = new Vector2(1f, 0.8f);
                    sfx.rectTransform.offsetMin = sfx.rectTransform.offsetMax = Vector2.zero;
                    sfx.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 9f);
                    sfx.enableAutoSizing = true;
                    sfx.fontSizeMin = 30f;
                    sfx.fontSizeMax = 84f;
                    sfx.outlineWidth = 0.25f;
                    sfx.outlineColor = new Color32(0xFF, 0xD1, 0x66, 255);
                }
                // The speech bubble under the panel.
                if (!string.IsNullOrEmpty(p.Line))
                {
                    var bubble = UiKit.Panel(page, Color.white, Theme.PanelSprite(), true, "Bubble");
                    bubble.color = new Color32(0xF4, 0xF1, 0xDE, 255);
                    bubble.rectTransform.anchorMin = new Vector2(0f, 0.04f);
                    bubble.rectTransform.anchorMax = new Vector2(1f, 0.35f);
                    bubble.rectTransform.offsetMin = bubble.rectTransform.offsetMax = Vector2.zero;
                    var name = UiKit.Label(bubble.rectTransform, p.Speaker ?? "", 30f, new Color32(0xF7, 0x25, 0x85, 255), TextAlignmentOptions.TopLeft, true, "Name");
                    name.rectTransform.anchorMin = new Vector2(0f, 0.72f);
                    name.rectTransform.anchorMax = new Vector2(1f, 1f);
                    name.rectTransform.offsetMin = new Vector2(28f, 0f);
                    name.rectTransform.offsetMax = new Vector2(-28f, -14f);
                    var line = UiKit.Label(bubble.rectTransform, p.Line, 34f, new Color32(0x14, 0x14, 0x1F, 255), TextAlignmentOptions.TopLeft, false, "Line");
                    line.rectTransform.anchorMin = new Vector2(0f, 0f);
                    line.rectTransform.anchorMax = new Vector2(1f, 0.74f);
                    line.rectTransform.offsetMin = new Vector2(28f, 16f);
                    line.rectTransform.offsetMax = new Vector2(-28f, 0f);
                    line.enableAutoSizing = true;
                    line.fontSizeMin = 18f;
                    line.fontSizeMax = 34f;
                }
                var dots = UiKit.Label(page, (index + 1) + " / " + panels.Count, 26f, Theme.Muted, TextAlignmentOptions.Center, false, "Count");
                dots.rectTransform.anchorMin = new Vector2(0f, -0.06f);
                dots.rectTransform.anchorMax = new Vector2(1f, 0.02f);
                dots.rectTransform.offsetMin = dots.rectTransform.offsetMax = Vector2.zero;
                OnComicPage?.Invoke(p);
            }
            void Finish()
            {
                OnComicPage?.Invoke(null);
                CloseCard(true);
                done?.Invoke();
            }
            var next = UiKit.Button(safe, "NEXT", () =>
            {
                if (index < panels.Count - 1) { index++; Show(); Audio.AudioManager.Play(SfxId.Click, 0.4f); }
                else Finish();
            }, ButtonStyle.Primary, 120f, 44f);
            UiKit.Place((RectTransform)next.transform, new Vector2(0.68f, 0.09f), new Vector2(460f, 130f));
            var skip = UiKit.Button(safe, "SKIP", Finish, ButtonStyle.Ghost, 100f, 36f);
            UiKit.Place((RectTransform)skip.transform, new Vector2(0.24f, 0.09f), new Vector2(340f, 110f));
            if (panels.Count == 0) { Finish(); return; }
            Show();
        }

        // ------------------------------------------------------------------ the world map

        /// <summary>
        /// The WORLD MAP: every region where it sits in the country, the highways out of Baller City, what's done there,
        /// and DRIVE for any region that's open. DISTRICTS opens this region's own map.
        /// </summary>
        public void ShowWorldMap(IList<WorldMapEntry> entries, string footer, Action<string> drive, Action districts, Action closed)
        {
            CloseCard(false);
            SetControlsVisible(false);
            var root = UiKit.NewRect("WorldMap", _canvas.transform);
            UiKit.Stretch(root);
            _card = root.gameObject;
            var scrim = UiKit.Panel(root, new Color32(0x0B, 0x13, 0x2B, 245), name: "Scrim");
            UiKit.Stretch(scrim.rectTransform);
            scrim.raycastTarget = true;
            var safe = UiKit.SafeArea(root);
            var title = UiKit.Label(safe, "BALLER WORLD", 64f, Theme.Gold, TextAlignmentOptions.Center, true);
            UiKit.Band(title.rectTransform, 0.9f, 0.97f, 40f);
            UiKit.ApplyTextShadow(title);
            var land = UiKit.Panel(safe, new Color32(0x2D, 0x6A, 0x4F, 255), name: "Land");
            UiKit.Band(land.rectTransform, 0.3f, 0.88f, 24f);
            var area = land.rectTransform;
            // Highways from home to every region.
            WorldMapEntry home = default;
            foreach (var e in entries) if (e.Id == WorldRegions.BallerCity) home = e;
            foreach (var e in entries)
            {
                if (e.Id == home.Id) continue;
                var road = UiKit.Panel(area, new Color32(0x3A, 0x3A, 0x42, 255), name: "Highway");
                var rt = road.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2((home.X + e.X) / 2f, (home.Y + e.Y) / 2f);
                rt.sizeDelta = new Vector2(Mathf.Max(8f, Vector2.Distance(new Vector2(home.X, home.Y * 1.3f), new Vector2(e.X, e.Y * 1.3f)) * 900f), 10f);
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2((e.Y - home.Y) * 1.3f, e.X - home.X) * Mathf.Rad2Deg);
            }
            foreach (var e in entries)
            {
                var entry = e;
                var cell = UiKit.NewRect("Region " + e.Id, area);
                cell.anchorMin = cell.anchorMax = new Vector2(e.X, e.Y);
                cell.sizeDelta = new Vector2(290f, 190f);
                var b = UiKit.Button(cell, e.Name, () =>
                {
                    if (entry.Here) { districts?.Invoke(); return; }
                    if (!entry.Open) { Toast(entry.Status, 2.4f); return; }
                    drive?.Invoke(entry.Id);
                }, e.Here ? ButtonStyle.Primary : e.Open ? ButtonStyle.Secondary : ButtonStyle.Ghost, 10f, 26f);
                UiKit.Stretch((RectTransform)b.transform);
                var sum = UiKit.Label((RectTransform)b.transform, (e.Next ? Loc.T("NEXT IN THE COMIC") + "\n" : "") + e.Status + "\n" + e.Detail, 18f, Theme.Cream,
                                      TextAlignmentOptions.Bottom, false, "Summary");
                sum.rectTransform.anchorMin = new Vector2(0.04f, 0.04f);
                sum.rectTransform.anchorMax = new Vector2(0.96f, 0.6f);
                sum.rectTransform.offsetMin = sum.rectTransform.offsetMax = Vector2.zero;
                sum.enableAutoSizing = true;
                sum.fontSizeMin = 11f;
                sum.fontSizeMax = 18f;
            }
            var foot = UiKit.Label(safe, footer, 28f, Theme.Cream, TextAlignmentOptions.Center, false);
            UiKit.Band(foot.rectTransform, 0.19f, 0.29f, 40f);
            var dist = UiKit.Button(safe, "DISTRICTS", () => districts?.Invoke(), ButtonStyle.Secondary, 100f, 36f);
            UiKit.Place((RectTransform)dist.transform, new Vector2(0.28f, 0.08f), new Vector2(400f, 110f));
            var close = UiKit.Button(safe, "CLOSE", () => { CloseCard(true); closed?.Invoke(); }, ButtonStyle.Ghost, 100f, 36f);
            UiKit.Place((RectTransform)close.transform, new Vector2(0.72f, 0.08f), new Vector2(400f, 110f));
        }

        /// <summary>28 Oct 2026: the zoomable WORLD MAP as a full-screen card.</summary>
        public void ShowWorldAtlas(WorldMapView.Model model)
        {
            CloseCard(false);
            SetControlsVisible(false);
            var root = UiKit.NewRect("WorldAtlas", _canvas.transform);
            UiKit.Stretch(root);
            _card = root.gameObject;
            WorldMapView.Create(root, model);
            UiMotion.Enter(root, UiMotion.Modal, Vector2.zero, 0.98f);
        }

        private void OnDestroy()
        {
            foreach (var o in _hudOwned) if (o != null) Destroy(o);
        }
    }
}
