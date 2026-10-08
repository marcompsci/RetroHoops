using System.Collections;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using CallerRetroBall.UI;
using CallerRetroBall.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CallerRetroBall.Core
{
    /// <summary>
    /// BootScene entry point. Shows the title screen straight away (the same sunset court as the
    /// iOS launch image and the main menu, the RETRO HOOPS logo, and a spinning pixel basketball
    /// as the loader), then initialises services and hands off to the main menu.
    /// </summary>
    public sealed class BootController : MonoBehaviour
    {
        [SerializeField] private float minimumSplashSeconds = 1.4f;
        private const string BackdropCourt = "court.sunset_cage";
        private const uint BackdropSeed = 7;
        private const float SpinFps = 14f;

        private Image _ball;
        private Sprite[] _frames;
        private RectTransform _ballRect;
        private TextMeshProUGUI _loading;
        private float _shownAt;

        private IEnumerator Start()
        {
            float started = Time.realtimeSinceStartup;
            Orientation.Portrait();
            Build();
            // Let the title screen draw before the (blocking) content and save loading.
            yield return null;
            yield return null;
            float initStart = Time.realtimeSinceStartup;
            App.EnsureInitialized();
            // Phase 37: an App Store screenshot launch (tools/App Store Screenshots.command) names its screen here.
            StoreShots.Current = StoreShots.Parse(System.Environment.GetEnvironmentVariable(StoreShots.Variable));
            UiKit.CheckTextMeshProReady();
            // Start listening to the App Store early (renewals and purchases from other devices arrive any time).
            ProStore.EnsureStarted();
            // Startup timing in the device log (Xcode console), for keeping launch fast.
            Debug.Log("[Retro Hoops] Boot: title shown at " + Mathf.RoundToInt(started * 1000f) + " ms after launch, content + save loaded in "
                      + Mathf.RoundToInt((Time.realtimeSinceStartup - initStart) * 1000f) + " ms");

            while (Time.realtimeSinceStartup - started < minimumSplashSeconds) yield return null;
            SceneFlow.GoTo(SceneNames.MainMenu);
        }

        private void Build()
        {
            _shownAt = Time.realtimeSinceStartup;
            var canvas = UiKit.CreateScreenCanvas("BootCanvas");
            var ink = UiKit.Panel(canvas.transform, Theme.Ink, name: "Ink");
            UiKit.Stretch(ink.rectTransform);

            // Sunset court backdrop (same art and seed as the launch image, so launch → title doesn't jump).
            var court = DefaultContent.Create().Court(BackdropCourt);
            if (court != null)
            {
                var tex = TextureFactory.Backdrop(court, BackdropSeed);
                var raw = UiKit.Picture(canvas.transform, tex, "Backdrop");
                UiKit.Stretch(raw.rectTransform);
                var fitter = raw.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = tex.width / (float)tex.height;
            }
            // Soft dark band behind the text at the bottom so it reads over the court.
            var shade = UiKit.Panel(canvas.transform, new Color(Theme.Ink.r, Theme.Ink.g, Theme.Ink.b, 0.45f), name: "Shade");
            shade.rectTransform.anchorMin = new Vector2(0f, 0f);
            shade.rectTransform.anchorMax = new Vector2(1f, 0.3f);
            shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;
            shade.raycastTarget = false;

            var safe = UiKit.SafeArea(canvas.transform);

            // RETRO HOOPS pixel logo.
            var logoTex = TextureFactory.ToTexture(TitleLogoGenerator.Generate(), "ui.title.logo.boot");
            var holder = UiKit.NewRect("TitleLogo", safe);
            UiKit.Band(holder, 0.74f, 0.9f, 40f);
            var logo = UiKit.Picture(holder, logoTex, "Logo");
            UiKit.Stretch(logo.rectTransform);
            var fit = logo.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = logoTex.width / (float)logoTex.height;

            // Spinning ball loader — 44% of screen width, centred over the sun at 55% up.
            var frames = AppIconGenerator.LoaderBall();
            _frames = new Sprite[frames.Length];
            for (int i = 0; i < frames.Length; i++) _frames[i] = TextureFactory.ToSprite(frames[i], "ui.boot.ball." + i);
            // Holder is 44% of the screen width and square; the frames carry 2 px of padding round the ball.
            var ballHolder = UiKit.NewRect("Loader", canvas.transform);
            ballHolder.anchorMin = new Vector2(0.28f, 0.55f);
            ballHolder.anchorMax = new Vector2(0.72f, 0.55f);
            ballHolder.pivot = new Vector2(0.5f, 0.5f);
            ballHolder.offsetMin = ballHolder.offsetMax = Vector2.zero;
            var square = ballHolder.gameObject.AddComponent<AspectRatioFitter>();
            square.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
            square.aspectRatio = 1f;
            var bob = UiKit.NewRect("Bob", ballHolder);
            UiKit.Stretch(bob);
            _ballRect = bob;
            _ball = UiKit.Panel(bob, Color.white, _frames[0], false, "Ball");
            float pad = 2f / (frames[0].Width - 4f);
            _ball.rectTransform.anchorMin = new Vector2(-pad, -pad);
            _ball.rectTransform.anchorMax = new Vector2(1f + pad, 1f + pad);
            _ball.rectTransform.offsetMin = _ball.rectTransform.offsetMax = Vector2.zero;
            _ball.raycastTarget = false;

            _loading = UiKit.Label(safe, "LOADING", 46f, Theme.Cream, TextAlignmentOptions.Center, true, "Loading");
            UiKit.Place(_loading.rectTransform, new Vector2(0.5f, 0.2f), new Vector2(900f, 70f));
            UiKit.ApplyTextShadow(_loading);
            var tagline = UiKit.Label(safe, Theme.Tagline, 34f, Theme.Cyan, TextAlignmentOptions.Center, false, "Tagline");
            UiKit.Place(tagline.rectTransform, new Vector2(0.5f, 0.13f), new Vector2(1000f, 60f));
        }

        private void Update()
        {
            if (_ball == null) return;
            float t = Time.realtimeSinceStartup - _shownAt;
            bool reduce = App.IsInitialized && App.Career?.settings != null && App.Career.settings.reduceMotion;
            int frame = reduce ? 0 : (int)(t * SpinFps) % _frames.Length;
            _ball.sprite = _frames[frame];
            // A gentle bob, snapped to whole pixels of the art so it stays crisp.
            float bob = reduce ? 0f : Mathf.Abs(Mathf.Sin(t * 3.2f)) * 18f;
            _ballRect.offsetMin = _ballRect.offsetMax = new Vector2(0f, Mathf.Round(bob / 6f) * 6f);
            int dots = (int)(t * 3f) % 4;
            _loading.text = "LOADING" + new string('.', dots) + new string(' ', 3 - dots);
        }
    }
}
