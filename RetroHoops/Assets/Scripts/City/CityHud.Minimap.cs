using System.Collections.Generic;
using CallerRetroBall.Logic;
using CallerRetroBall.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CallerRetroBall.City
{
    // 28 Oct 2026, I21 MINIMAP: the district from above in the top-right corner, you in the middle, the guide's target
    // and the pins near you. Tap it for the MAP.
    public sealed partial class CityHud
    {
        /// <summary>Tiles across the minimap.</summary>
        public const float MiniTiles = 30f;
        private const float MiniSize = 230f;

        private RectTransform _mini;
        private RawImage _miniMap;
        private Image _miniYou, _miniTarget;
        private readonly List<Image> _miniDots = new List<Image>();

        private void BuildMinimap(RectTransform safe)
        {
            var frame = UiKit.Panel(safe, Theme.Gold, name: "Minimap");
            _mini = frame.rectTransform;
            _mini.anchorMin = _mini.anchorMax = new Vector2(1f, 1f);
            _mini.pivot = new Vector2(1f, 1f);
            _mini.sizeDelta = new Vector2(MiniSize + 8f, MiniSize + 8f);
            _mini.anchoredPosition = new Vector2(-16f, -240f);
            frame.raycastTarget = true;
            var button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;
            button.onClick.AddListener(() => { Audio.AudioManager.Click(); MapRequested?.Invoke(); });

            var back = UiKit.Panel(_mini, new Color32(0x14, 0x14, 0x1F, 255), name: "Back");
            UiKit.Stretch(back.rectTransform, 4f);
            var clip = UiKit.NewRect("Clip", _mini);
            UiKit.Stretch(clip, 4f);
            clip.gameObject.AddComponent<RectMask2D>();
            _miniMap = UiKit.Picture(clip, null, "Map");
            UiKit.Stretch(_miniMap.rectTransform);

            _miniTarget = UiKit.Panel(clip, new Color32(0x9B, 0x5D, 0xE5, 255), Theme.DiscSprite(new Color32(0x9B, 0x5D, 0xE5, 255), Theme.Cream), false, "Target");
            _miniTarget.rectTransform.anchorMin = _miniTarget.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _miniTarget.rectTransform.sizeDelta = new Vector2(24f, 24f);
            _miniYou = UiKit.Panel(clip, Theme.Gold, Theme.DiscSprite(Theme.Gold, new Color32(0x14, 0x14, 0x1F, 255)), false, "You");
            _miniYou.rectTransform.anchorMin = _miniYou.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _miniYou.rectTransform.sizeDelta = new Vector2(22f, 22f);
            var label = UiKit.Label(_mini, "MAP", 20f, Theme.Cream, TMPro.TextAlignmentOptions.BottomRight, true, "Hint");
            UiKit.Stretch(label.rectTransform, 10f);
            UiKit.ApplyTextShadow(label);
            _mini.gameObject.SetActive(false);
        }

        /// <summary>
        /// Updates the minimap: <paramref name="texture"/> is the district (<paramref name="mapW"/> × <paramref name="mapH"/>
        /// tiles), <paramref name="me"/> where you are, <paramref name="target"/> the guide's target here (if any) and
        /// <paramref name="dots"/> pins nearby. <paramref name="visible"/> false hides it.
        /// </summary>
        public void SetMinimap(bool visible, Texture texture, int mapW, int mapH, Vector2 me, Vector2? target, IList<(Vector2 at, Color color)> dots)
        {
            if (_mini == null) return;
            bool on = visible && texture != null && !CardOpen;
            if (_mini.gameObject.activeSelf != on) _mini.gameObject.SetActive(on);
            if (!on) return;
            if (_miniMap.texture != texture) _miniMap.texture = texture;
            // The window: MiniTiles across, centred on you but kept inside the district when it's big enough.
            float half = MiniTiles * 0.5f;
            float cx = mapW <= MiniTiles ? mapW * 0.5f : Mathf.Clamp(me.x, half, mapW - half);
            float cy = mapH <= MiniTiles ? mapH * 0.5f : Mathf.Clamp(me.y, half, mapH - half);
            _miniMap.uvRect = new Rect((cx - half) / mapW, (cy - half) / mapH, MiniTiles / mapW, MiniTiles / mapH);
            float scale = MiniSize / MiniTiles;
            Vector2 ToMini(Vector2 p) => new Vector2((p.x - cx) * scale, (p.y - cy) * scale);
            _miniYou.rectTransform.anchoredPosition = ToMini(me);
            if (target.HasValue)
            {
                var t = ToMini(target.Value);
                float edge = MiniSize * 0.5f - 12f;
                // Off the minimap the target sits on its edge, in its direction.
                if (Mathf.Abs(t.x) > edge || Mathf.Abs(t.y) > edge) t *= edge / Mathf.Max(Mathf.Abs(t.x), Mathf.Abs(t.y));
                _miniTarget.rectTransform.anchoredPosition = t;
                _miniTarget.enabled = true;
            }
            else _miniTarget.enabled = false;
            int n = dots?.Count ?? 0;
            while (_miniDots.Count < n)
            {
                var d = UiKit.Panel(_miniMap.rectTransform.parent, Color.white, Theme.DiscSprite(Color.white, new Color32(0x14, 0x14, 0x1F, 255)), false, "Dot");
                d.rectTransform.anchorMin = d.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                d.rectTransform.sizeDelta = new Vector2(16f, 16f);
                d.transform.SetSiblingIndex(_miniTarget.transform.GetSiblingIndex());
                _miniDots.Add(d);
            }
            for (int i = 0; i < _miniDots.Count; i++)
            {
                bool show = i < n;
                if (show)
                {
                    var p = ToMini(dots[i].at);
                    show = Mathf.Abs(p.x) < MiniSize * 0.5f && Mathf.Abs(p.y) < MiniSize * 0.5f;
                    if (show) { _miniDots[i].rectTransform.anchoredPosition = p; _miniDots[i].color = dots[i].color; }
                }
                _miniDots[i].enabled = show;
            }
        }
    }
}
