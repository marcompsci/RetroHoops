using System;
using CallerRetroBall.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// Code-built uGUI + TextMeshPro helpers. Screens are constructed at runtime so the
    /// project has no fragile hand-authored prefabs, and every screen shares one look.
    /// Reference layout is 1080 x 1920 portrait.
    /// </summary>
    public static class UiKit
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1080, 1920);

        /// <summary>Accessibility UI scale (0.85–1.25). Applied when canvases are created.</summary>
        public static float UiScale { get; set; } = 1f;

        private const float ClickCooldown = 0.2f;
        private static float _lastClick = -10f;

        // ------------------------------------------------------------------ canvas

        public static Canvas CreateScreenCanvas(string name, int sortingOrder = 0)
        {
            EnsureEventSystem();
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            canvas.pixelPerfect = false;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            // Tall phones favour width (big buttons); iPad / Mac aspects match height so nothing runs off screen.
            // Landscape (Full Court) lays out on 1920 x 1080 instead. AspectMatch keeps this right as the screen turns.
            AspectMatch.Apply(scaler);
            scaler.referencePixelsPerUnit = 100f;
            go.AddComponent<AspectMatch>();

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>Full-screen child that is inset to the device safe area (notch, home indicator).</summary>
        public static RectTransform SafeArea(Transform canvas)
        {
            var rt = NewRect("SafeArea", canvas);
            Stretch(rt);
            rt.gameObject.AddComponent<SafeAreaFitter>();
            return rt;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            var module = go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (module.actionsAsset == null) module.AssignDefaultActions();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        /// <summary>Logs a clear error if TMP Essential Resources haven't been imported (text would be invisible).</summary>
        public static bool CheckTextMeshProReady()
        {
            bool ok;
            try
            {
                ok = TMP_Settings.instance != null && TMP_Settings.defaultFontAsset != null;
            }
            catch (Exception)
            {
                ok = false;
            }
            if (!ok)
            {
                Debug.LogError("[CallerRetroBall] TextMeshPro Essential Resources are missing, so text will not render. " +
                               "Run 'Retro Hoops ► Run Project Setup' or 'Window ► TextMeshPro ► Import TMP Essential Resources'.");
            }
            return ok;
        }

        // ------------------------------------------------------------------ primitives

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image Panel(Transform parent, Color color, Sprite sprite = null, bool sliced = false, string name = "Panel")
        {
            var rt = NewRect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            if (sprite != null && sliced)
            {
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 1f / Theme.PixelSize;
            }
            image.raycastTarget = false;
            return image;
        }

        public static TextMeshProUGUI Label(Transform parent, string text, float size, Color color,
                                           TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = false,
                                           string name = "Label")
        {
            var rt = NewRect(name, parent);
            var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = Logic.Loc.T(text);
            label.fontSize = size;
            label.color = color;
            label.alignment = align;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            label.characterSpacing = 4f;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>A label with a hard pixel drop-shadow (a second label offset down-right).</summary>
        public static TextMeshProUGUI ShadowLabel(Transform parent, string text, float size, Color color, Color shadow,
                                                  float offset = 8f, bool bold = true)
        {
            var holder = NewRect("ShadowLabel", parent);
            var back = Label(holder, text, size, shadow, TextAlignmentOptions.Center, bold, "Shadow");
            Stretch(back.rectTransform);
            back.rectTransform.anchoredPosition = new Vector2(offset, -offset);
            var front = Label(holder, text, size, color, TextAlignmentOptions.Center, bold, "Text");
            Stretch(front.rectTransform);
            return front;
        }

        public static RawImage Picture(Transform parent, Texture texture, string name = "Picture")
        {
            var rt = NewRect(name, parent);
            var raw = rt.gameObject.AddComponent<RawImage>();
            raw.texture = texture;
            raw.raycastTarget = false;
            return raw;
        }

        /// <summary>
        /// Chunky pixel button with a global click cooldown and no clicks during scene
        /// transitions (prevents double-loads from rapid taps).
        /// </summary>
        public static UnityEngine.UI.Button Button(Transform parent, string text, Action onClick, ButtonStyle style = ButtonStyle.Primary,
                                    float height = 150f, float fontSize = 56f)
        {
            var image = Panel(parent, Color.white, Theme.ButtonSprite(style), true, "Button: " + text);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
            image.gameObject.AddComponent<ButtonPop>();
            button.targetGraphic = image;
            // Console-style press: swap to the pushed-in face instead of tinting.
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                pressedSprite = Theme.ButtonPressedSprite(style),
                disabledSprite = Theme.ButtonDisabledSprite(style),
            };

            var label = Label(image.transform, text, fontSize, Theme.ButtonText, TextAlignmentOptions.Center, true);
            Stretch(label.rectTransform, 12f);
            // Keep text clear of the bottom lip so it sits on the face.
            label.rectTransform.offsetMin = new Vector2(12f, 20f);
            // 11 Oct 2026: long labels shrink to fit instead of spilling over the button (UNLOCK PRO on the Pro screen)
            // or breaking mid-word (the Locker Room tabs on iPhone). Labels that fit keep their size.
            label.enableAutoSizing = true;
            label.fontSizeMax = fontSize;
            label.fontSizeMin = Mathf.Max(14f, fontSize * 0.45f);
            if (label.text.IndexOf(' ') < 0) label.textWrappingMode = TextWrappingModes.NoWrap;
            ApplyTextShadow(label);

            Size(image, height);

            button.onClick.AddListener(() =>
            {
                if (SceneFlow.IsTransitioning) return;
                if (Time.unscaledTime - _lastClick < ClickCooldown) return;
                _lastClick = Time.unscaledTime;
                Audio.AudioManager.Click();
                onClick?.Invoke();
            });
            return button;
        }

        private static Material _shadowMaterial;

        /// <summary>
        /// Hard dark drop shadow behind button text (TMP underlay) for readability on bright faces.
        /// One shared material for every button.
        /// </summary>
        public static void ApplyTextShadow(TextMeshProUGUI label)
        {
            if (label == null || label.fontSharedMaterial == null) return;
            if (_shadowMaterial == null)
            {
                _shadowMaterial = new Material(label.fontSharedMaterial) { name = "Button Text Shadow" };
                _shadowMaterial.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                _shadowMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.04f, 0.04f, 0.08f, 1f));
                _shadowMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.7f);
                _shadowMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.7f);
                _shadowMaterial.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
            }
            label.fontSharedMaterial = _shadowMaterial;
        }

        // ------------------------------------------------------------------ layout

        public static RectTransform Column(Transform parent, float spacing, RectOffset padding = null, string name = "Column")
        {
            var rt = NewRect(name, parent);
            var layout = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset(0, 0, 0, 0);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return rt;
        }

        /// <summary>
        /// Vertically scrolling column filling <paramref name="parent"/>. Add children to the returned
        /// content rect; it grows to fit them.
        /// </summary>
        public static RectTransform ScrollColumn(Transform parent, float spacing, RectOffset padding = null)
        {
            var root = NewRect("Scroll", parent);
            Stretch(root);
            var viewport = NewRect("Viewport", root);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f); // lets drags anywhere scroll

            var content = Column(viewport, spacing, padding, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;
            return content;
        }

        public static RectTransform Row(Transform parent, float spacing, string name = "Row")
        {
            var rt = NewRect(name, parent);
            var layout = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return rt;
        }

        /// <summary>Sets preferred height (and optionally width) for layout groups.</summary>
        public static LayoutElement Size(Component c, float height = -1f, float width = -1f)
        {
            var le = c.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (height >= 0f) { le.preferredHeight = height; le.minHeight = height; }
            if (width >= 0f) { le.preferredWidth = width; le.minWidth = width; }
            return le;
        }

        public static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>Anchors a fixed-size rect at a normalised point of its parent.</summary>
        public static void Place(RectTransform rt, Vector2 anchor, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
        }

        /// <summary>Stretches horizontally between normalised vertical anchors (yMin..yMax) with side padding.</summary>
        public static void Band(RectTransform rt, float yMin, float yMax, float sidePadding = 48f)
        {
            rt.anchorMin = new Vector2(0f, yMin);
            rt.anchorMax = new Vector2(1f, yMax);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(sidePadding, 0f);
            rt.offsetMax = new Vector2(-sidePadding, 0f);
        }
    }
}
