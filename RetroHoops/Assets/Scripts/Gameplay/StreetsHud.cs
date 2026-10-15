using System;
using CallerRetroBall.Logic;
using CallerRetroBall.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CallerRetroBall.Gameplay
{
    /// <summary>
    /// 15 Oct 2026 (Omari): THE STREETS on screen.
    /// - Both teams' STREET METERs sit under the score bar. Each one fills, then lights a level (I, then II) and glows.
    /// - Your combo shows over the buttons while you trick: "COMBO x3  +245".
    /// - The SHOWSTOPPER button appears (and pulses) when your meter is full and you're on offense.
    /// - A big banner flashes for SHOWSTOPPER moments.
    /// </summary>
    public sealed class StreetsHud : MonoBehaviour
    {
        public event Action BreakerTapped;

        private RectTransform _fillA, _fillB, _meters;
        private Image _fillImgA, _fillImgB;
        private TextMeshProUGUI _levelA, _levelB, _combo, _banner;
        private GameObject _breaker;
        private TextMeshProUGUI _breakerLabel;
        private float _bannerUntil, _bannerStart;
        private int _lastA = -1, _lastB = -1, _lastCombo = -1, _lastCount = -1;
        private Color _colorA, _colorB;
        private int _human;
        private bool _landscape, _layoutSet, _left;

        public static StreetsHud Create(TeamDef a, TeamDef b, int humanTeam, bool leftHanded)
        {
            var canvas = UiKit.CreateScreenCanvas("StreetsHudCanvas", 21);
            var hud = canvas.gameObject.AddComponent<StreetsHud>();
            hud._human = humanTeam;
            hud._colorA = ToColor(a.primary);
            hud._colorB = ToColor(b.primary);
            hud._left = leftHanded;
            hud.Build(UiKit.SafeArea(canvas.transform), a, b);
            return hud;
        }

        private static Color ToColor(RgbColor c) => new Color32(c.r, c.g, c.b, 255);

        private void Build(RectTransform safe, TeamDef a, TeamDef b)
        {
            _meters = UiKit.NewRect("Meters", safe);
            _meters.anchorMin = new Vector2(0f, 1f);
            _meters.anchorMax = new Vector2(1f, 1f);
            _meters.pivot = new Vector2(0.5f, 1f);
            _meters.sizeDelta = new Vector2(-48f, 54f);
            _meters.anchoredPosition = new Vector2(0f, -172f);

            Meter(_meters, 0f, 0.47f, a.abbreviation, _colorA, out _fillA, out _fillImgA, out _levelA, false);
            Meter(_meters, 0.53f, 1f, b.abbreviation, _colorB, out _fillB, out _fillImgB, out _levelB, true);
            var mid = UiKit.Label(_meters, "STREET", 22f, Theme.Muted, TextAlignmentOptions.Center, true);
            UiKit.Stretch(mid.rectTransform);
            mid.rectTransform.anchorMin = new Vector2(0.45f, 0f);
            mid.rectTransform.anchorMax = new Vector2(0.55f, 1f);
            mid.raycastTarget = false;

            // The combo, over the action buttons.
            // (Left-handed: the buttons are on the left, so these go there too.)
            float side = _left ? 0f : 1f, dx = _left ? 40f : -40f;
            _combo = UiKit.Label(safe, "", 40f, Theme.Gold, _left ? TextAlignmentOptions.Left : TextAlignmentOptions.Right, true, "Combo");
            _combo.rectTransform.anchorMin = _combo.rectTransform.anchorMax = new Vector2(side, 0f);
            _combo.rectTransform.pivot = new Vector2(side, 0f);
            _combo.rectTransform.sizeDelta = new Vector2(700f, 60f);
            _combo.rectTransform.anchoredPosition = new Vector2(dx, 760f);
            _combo.raycastTarget = false;
            UiKit.ApplyTextShadow(_combo);

            // SHOWSTOPPER: a big gold button over the action buttons.
            var b2 = UiKit.Button(safe, "SHOWSTOPPER", () => BreakerTapped?.Invoke(), ButtonStyle.Primary, 130f, 44f);
            var brt = (RectTransform)b2.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(side, 0f);
            brt.pivot = new Vector2(side, 0f);
            brt.sizeDelta = new Vector2(520f, 130f);
            brt.anchoredPosition = new Vector2(dx, 830f);
            _breaker = b2.gameObject;
            _breakerLabel = b2.GetComponentInChildren<TextMeshProUGUI>();
            _breaker.SetActive(false);

            // The banner.
            _banner = UiKit.Label(safe, "", 96f, Theme.Gold, TextAlignmentOptions.Center, true, "Banner");
            _banner.rectTransform.anchorMin = _banner.rectTransform.anchorMax = new Vector2(0.5f, 0.62f);
            _banner.rectTransform.sizeDelta = new Vector2(1600f, 220f);
            _banner.textWrappingMode = TextWrappingModes.Normal;
            _banner.raycastTarget = false;
            UiKit.ApplyTextShadow(_banner);
        }

        private static void Meter(RectTransform parent, float x0, float x1, string abbr, Color color, out RectTransform fill, out Image fillImg,
                                  out TextMeshProUGUI level, bool rightToLeft)
        {
            var back = UiKit.Panel(parent, new Color32(0x10, 0x10, 0x1C, 220), null, false, "Meter " + abbr);
            back.rectTransform.anchorMin = new Vector2(x0, 0.15f);
            back.rectTransform.anchorMax = new Vector2(x1, 0.85f);
            back.rectTransform.offsetMin = back.rectTransform.offsetMax = Vector2.zero;
            fillImg = UiKit.Panel(back.transform, color, null, false, "Fill");
            fill = fillImg.rectTransform;
            fill.pivot = new Vector2(rightToLeft ? 1f : 0f, 0.5f);
            fill.anchorMin = new Vector2(rightToLeft ? 1f : 0f, 0f);
            fill.anchorMax = new Vector2(rightToLeft ? 1f : 0f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            // Half-way mark: level I of II.
            var tick = UiKit.Panel(back.transform, new Color(1f, 1f, 1f, 0.5f), null, false, "Tick");
            tick.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            tick.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            tick.rectTransform.sizeDelta = new Vector2(4f, 0f);
            level = UiKit.Label(back.transform, abbr, 24f, Theme.Cream, rightToLeft ? TextAlignmentOptions.Right : TextAlignmentOptions.Left, true);
            UiKit.Stretch(level.rectTransform);
            level.rectTransform.offsetMin = new Vector2(12f, 0f);
            level.rectTransform.offsetMax = new Vector2(-12f, 0f);
            level.raycastTarget = false;
        }

        private void Layout()
        {
            bool landscape = Screen.width > Screen.height;
            if (_layoutSet && landscape == _landscape) return;
            _layoutSet = true;
            _landscape = landscape;
            // Landscape: the score bar is a centered 1100-wide strip, so the meters sit under it at the same width.
            if (landscape)
            {
                _meters.anchorMin = _meters.anchorMax = new Vector2(0.5f, 1f);
                _meters.sizeDelta = new Vector2(1100f, 50f);
                _meters.anchoredPosition = new Vector2(0f, -150f);
            }
            else
            {
                _meters.anchorMin = new Vector2(0f, 1f);
                _meters.anchorMax = new Vector2(1f, 1f);
                _meters.sizeDelta = new Vector2(-48f, 54f);
                _meters.anchoredPosition = new Vector2(0f, -172f);
            }
        }

        /// <summary>Updates the meters, the combo and the SHOWSTOPPER button from the match.</summary>
        public void Sync(MatchSimulation m, bool showControls)
        {
            Layout();
            float now = Time.unscaledTime;
            if (m.StyleMeter[0] != _lastA)
            {
                _lastA = m.StyleMeter[0];
                SetFill(_fillA, _levelA, m.Setup.TeamA.abbreviation, _lastA);
            }
            if (m.StyleMeter[1] != _lastB)
            {
                _lastB = m.StyleMeter[1];
                SetFill(_fillB, _levelB, m.Setup.TeamB.abbreviation, _lastB);
            }
            // A full meter glows.
            float glow = 0.75f + 0.25f * Mathf.Sin(now * 8f);
            _fillImgA.color = m.BreakerReady(0) > 0 ? Color.Lerp(_colorA, Color.white, 0.35f * glow) : _colorA;
            _fillImgB.color = m.BreakerReady(1) > 0 ? Color.Lerp(_colorB, Color.white, 0.35f * glow) : _colorB;

            int combo = m.ComboPoints[_human], count = m.ComboCount[_human];
            if (combo != _lastCombo || count != _lastCount)
            {
                _lastCombo = combo;
                _lastCount = count;
                _combo.text = combo > 0 ? Loc.T("COMBO") + " x" + Math.Max(1, count) + "  +" + combo : "";
            }

            bool canBreak = showControls && m.CanCallBreaker(_human);
            if (_breaker.activeSelf != canBreak) _breaker.SetActive(canBreak);
            if (canBreak)
            {
                int level = m.BreakerReady(_human);
                string text = level >= 2 ? Loc.T("SHOWSTOPPER") + " II" : Loc.T("SHOWSTOPPER");
                if (_breakerLabel.text != text) _breakerLabel.text = text;
                float s = 1f + 0.05f * Mathf.Sin(now * 7f);
                _breaker.transform.localScale = new Vector3(s, s, 1f);
            }

            if (_banner.text.Length > 0)
            {
                if (now > _bannerUntil) _banner.text = "";
                else
                {
                    float t = Mathf.Clamp01((now - _bannerStart) / 0.25f);
                    float k = MatchHud.ReduceMotion ? 1f : Mathf.Lerp(1.6f, 1f, t);
                    _banner.transform.localScale = new Vector3(k, k, 1f);
                }
            }
        }

        private static void SetFill(RectTransform fill, TextMeshProUGUI label, string abbr, int meter)
        {
            float f = Mathf.Clamp01(meter / (float)(MatchSimulation.MeterLevel * MatchSimulation.MaxBreakerLevel));
            fill.anchorMax = new Vector2(fill.pivot.x > 0.5f ? 1f : f, 1f);
            fill.anchorMin = new Vector2(fill.pivot.x > 0.5f ? 1f - f : 0f, 0f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            int level = Math.Min(MatchSimulation.MaxBreakerLevel, meter / MatchSimulation.MeterLevel);
            label.text = level == 0 ? abbr : abbr + "  " + (level >= 2 ? "II" : "I");
        }

        /// <summary>The big SHOWSTOPPER banner.</summary>
        public void Banner(string text, Color color, float seconds = 1.6f)
        {
            _banner.text = Loc.T(text);
            _banner.color = color;
            _bannerStart = Time.unscaledTime;
            _bannerUntil = _bannerStart + seconds;
        }

        public void SetVisible(bool visible) => gameObject.SetActive(visible);

        // ------------------------------------------------------------------ SHOWSTOPPER cinema bars

        private RectTransform _barTop, _barBottom;
        private bool _cinema;
        private float _cinemaAt;

        /// <summary>Letterbox bars for a SHOWSTOPPER dunk (they slide in and out).</summary>
        public void Cinema(bool on)
        {
            if (_barTop == null)
            {
                var canvas = (RectTransform)transform;
                _barTop = Bar(canvas, 1f);
                _barBottom = Bar(canvas, 0f);
                _barTop.SetAsFirstSibling();
                _barBottom.SetAsFirstSibling();
            }
            if (on == _cinema) return;
            _cinema = on;
            _cinemaAt = Time.unscaledTime;
        }

        private static RectTransform Bar(RectTransform canvas, float y)
        {
            var img = UiKit.Panel(canvas, Color.black, null, false, y > 0.5f ? "CinemaTop" : "CinemaBottom");
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0f, y);
            rt.anchorMax = new Vector2(1f, y);
            rt.pivot = new Vector2(0.5f, y);
            rt.sizeDelta = new Vector2(0f, 0f);
            img.raycastTarget = false;
            return rt;
        }

        private void LateUpdate()
        {
            if (_barTop == null) return;
            float t = Mathf.Clamp01((Time.unscaledTime - _cinemaAt) / 0.25f);
            float h = (_cinema ? t : 1f - t) * Screen.height * 0.09f / Mathf.Max(0.01f, ((RectTransform)transform).lossyScale.y);
            _barTop.sizeDelta = new Vector2(0f, h);
            _barBottom.sizeDelta = new Vector2(0f, h);
        }
    }
}
