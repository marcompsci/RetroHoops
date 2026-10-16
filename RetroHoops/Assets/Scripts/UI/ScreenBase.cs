using CallerRetroBall.Core;
using CallerRetroBall.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// Shared scaffold for menu-style scenes: canvas, generated pixel backdrop,
    /// safe area, optional header with Back, and Escape/back-key handling.
    /// Subclasses build their content in <see cref="Build"/>.
    /// </summary>
    public abstract class ScreenBase : MonoBehaviour
    {
        protected Canvas Canvas { get; private set; }
        protected RectTransform Safe { get; private set; }
        /// <summary>Area below the header (or the full safe area if there is no header).</summary>
        protected RectTransform Body { get; private set; }

        protected virtual string ScreenTitle => null;
        protected virtual string BackdropCourtId => "court.overpass_park";
        protected virtual uint BackdropSeed => 11;
        /// <summary>0..1 darkening over the backdrop so text stays readable.</summary>
        protected virtual float ScrimAlpha => 0.55f;
        /// <summary>Music loop for this screen (0 = menus, 2 = Rise hub).</summary>
        protected virtual int MusicTrack => 0;

        private const float HeaderHeight = 150f;

        protected virtual void Start()
        {
            App.EnsureInitialized();
            Audio.AudioManager.PlayMusic(MusicTrack);
            Canvas = UiKit.CreateScreenCanvas(GetType().Name + "Canvas");
            BuildBackdrop();
            Safe = UiKit.SafeArea(Canvas.transform);

            Body = UiKit.NewRect("Body", Safe);
            UiKit.Stretch(Body);
            if (!string.IsNullOrEmpty(ScreenTitle))
            {
                BuildHeader();
                Body.offsetMax = new Vector2(0f, -HeaderHeight);
            }
            Build();
        }

        protected abstract void Build();

        /// <summary>Default back behaviour returns to the main menu.</summary>
        protected virtual void OnBack()
        {
            // 16 Oct 2026: opened from BALLER CITY (the HQ, your front door, the gym's locker room): back to the city.
            if (App.BackToCity)
            {
                if (SceneFlow.GoTo(SceneNames.City)) App.BackToCity = false;
                return;
            }
            SceneFlow.GoTo(SceneNames.MainMenu);
        }

        protected virtual void Update()
        {
            // Full-screen panels (Franchise, All-Star, Music Player) handle Back themselves.
            if (BackPressedThisFrame() && !PanelOpen()) OnBack();
        }

        private static bool PanelOpen() =>
            FindAnyObjectByType<FranchiseScreen>() != null || FindAnyObjectByType<AllStarScreen>() != null || FindAnyObjectByType<MusicPlayer>() != null
            || FindAnyObjectByType<LegacyScreen>() != null;

        private static bool BackPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            // Controller B / Circle goes back, like Escape.
            return (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame);
#else
            return false;
#endif
        }

        private void BuildBackdrop()
        {
            var court = App.Catalog.Court(BackdropCourtId);
            if (court == null)
            {
                var fill = UiKit.Panel(Canvas.transform, Theme.Ink, name: "Backdrop");
                UiKit.Stretch(fill.rectTransform);
                return;
            }

            var tex = TextureFactory.Backdrop(court, BackdropSeed);
            var raw = UiKit.Picture(Canvas.transform, tex, "Backdrop");
            UiKit.Stretch(raw.rectTransform);
            var fitter = raw.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = tex.width / (float)tex.height;

            if (ScrimAlpha > 0f)
            {
                var scrim = UiKit.Panel(Canvas.transform, new Color(Theme.Ink.r, Theme.Ink.g, Theme.Ink.b, ScrimAlpha), name: "Scrim");
                UiKit.Stretch(scrim.rectTransform);
            }
        }

        private void BuildHeader()
        {
            var header = UiKit.NewRect("Header", Safe);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(0f, HeaderHeight);
            header.anchoredPosition = Vector2.zero;

            var back = UiKit.Button(header, "< BACK", OnBack, ButtonStyle.Ghost, 100f, 40f);
            var backRt = (RectTransform)back.transform;
            backRt.anchorMin = backRt.anchorMax = new Vector2(0f, 0.5f);
            backRt.pivot = new Vector2(0f, 0.5f);
            backRt.sizeDelta = new Vector2(240f, 100f);
            backRt.anchoredPosition = new Vector2(32f, 0f);

            var title = UiKit.ShadowLabel(header, ScreenTitle, 64f, Theme.Cream, Theme.Pink, 6f);
            var titleRt = (RectTransform)title.transform.parent;
            titleRt.anchorMin = new Vector2(0f, 0f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.offsetMin = new Vector2(280f, 0f);
            titleRt.offsetMax = new Vector2(-40f, 0f);
            foreach (var t in titleRt.GetComponentsInChildren<TextMeshProUGUI>()) t.alignment = TextAlignmentOptions.Right;
        }
    }
}
