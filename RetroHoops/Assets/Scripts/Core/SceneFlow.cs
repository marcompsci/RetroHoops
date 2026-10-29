using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CallerRetroBall.Core
{
    /// <summary>
    /// Persistent scene loader with a pixel screen wipe (an old-console block dissolve; a plain
    /// fade when Reduce Motion is on). While a transition runs, a
    /// full-screen raycast blocker swallows input, so rapid taps on menu buttons
    /// can never start two loads or leave the game half-transitioned.
    /// </summary>
    public sealed class SceneFlow : MonoBehaviour
    {
        private const float FadeSeconds = 0.15f;
        private const int WipeCols = 32, WipeRows = 18;
        private Texture2D _wipe;
        private Color32[] _wipePixels;
        private static readonly Color32 WipeColor = new Color32(0x1A, 0x1A, 0x2E, 255);

        private static SceneFlow _instance;

        private CanvasGroup _fade;
        private bool _busy;
        // Phase 35: the wipe changes look each time, and a tip shows while the screen is covered.
        private int _transitions;
        private int _style;
        private TMPro.TextMeshProUGUI _tip;

        public static bool IsTransitioning => _instance != null && _instance._busy;

        public static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("[SceneFlow]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SceneFlow>();
            _instance.BuildOverlay();
        }

        /// <summary>Loads a scene by name. Ignored if a transition is already running.</summary>
        public static bool GoTo(string sceneName)
        {
            EnsureExists();
            if (_instance._busy) return false;
            _instance.StartCoroutine(_instance.Transition(sceneName));
            return true;
        }

        private void BuildOverlay()
        {
            var canvasGo = new GameObject("FadeCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            canvasGo.AddComponent<GraphicRaycaster>();

            var imageGo = new GameObject("Fade", typeof(RectTransform));
            imageGo.transform.SetParent(canvasGo.transform, false);
            var rt = (RectTransform)imageGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            _wipe = new Texture2D(WipeCols, WipeRows, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            _wipePixels = new Color32[WipeCols * WipeRows];
            var image = imageGo.AddComponent<RawImage>();
            image.texture = _wipe;
            SetWipe(1f);

            _fade = imageGo.AddComponent<CanvasGroup>();
            _fade.alpha = 0f;
            _fade.blocksRaycasts = false;

            try
            {
                _tip = UI.UiKit.Label(imageGo.transform, "", 34f, UI.Theme.Cream, TMPro.TextAlignmentOptions.Center, true, "Tip");
                var trt = _tip.rectTransform;
                trt.anchorMin = new Vector2(0.08f, 0.08f);
                trt.anchorMax = new Vector2(0.92f, 0.22f);
                trt.offsetMin = trt.offsetMax = Vector2.zero;
                _tip.enableAutoSizing = true;
                _tip.fontSizeMin = 20f;
                _tip.fontSizeMax = 34f;
                _tip.gameObject.SetActive(false);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[SceneFlow] No tip label: " + e.Message);
                _tip = null;
            }
        }

        private IEnumerator Transition(string sceneName)
        {
            _busy = true;
            _fade.blocksRaycasts = true;
            _style = _transitions % Logic.ScreenWipe.Styles;
            yield return Fade(0f, 1f);
            if (_tip != null && _transitions > 0)
            {
                _tip.text = "<color=#FFD166>TIP</color>  " + Logic.LoadingTips.Tip(_transitions);
                _tip.gameObject.SetActive(true);
            }
            _transitions++;

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (op == null)
            {
                Debug.LogError("[SceneFlow] Scene '" + sceneName + "' is not in Build Settings. Run 'Retro Hoops ► Run Project Setup'.");
            }
            else
            {
                while (!op.isDone) yield return null;
            }

            if (_tip != null) _tip.gameObject.SetActive(false);
            yield return Fade(1f, 0f);
            _fade.blocksRaycasts = false;
            _busy = false;
        }

        private static bool ReduceMotion => App.Career != null && App.Career.settings.reduceMotion;

        private IEnumerator Fade(float from, float to)
        {
            if (ReduceMotion)
            {
                SetWipe(1f);
                float t = 0f;
                while (t < FadeSeconds)
                {
                    t += Time.unscaledDeltaTime;
                    _fade.alpha = Mathf.Lerp(from, to, UI.UiMotion.Out(t / FadeSeconds));
                    yield return null;
                }
                _fade.alpha = to;
                yield break;
            }
            // Pixel wipe: blocks sweep in (or back out) in a dithered diagonal.
            _fade.alpha = 1f;
            // 28 Oct 2026 (Emil Kowalski audit): the wipe was 0.32 s linear both ways. Covering is movement across the
            // screen (strong ease-in-out, 0.26 s); uncovering answers you (strong ease-out, a touch quicker at 0.24 s).
            float w = 0f;
            bool covering = to > from;
            float seconds = covering ? Logic.ScreenWipe.CoverSeconds : Logic.ScreenWipe.RevealSeconds;
            while (w < seconds)
            {
                w += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(w / seconds);
                SetWipe(Mathf.Lerp(from, to, covering ? UI.UiMotion.InOut(k) : UI.UiMotion.Out(k)));
                yield return null;
            }
            SetWipe(to);
            if (to <= 0f) _fade.alpha = 0f;
        }

        private void SetWipe(float t)
        {
            var clear = new Color32(0, 0, 0, 0);
            for (int y = 0; y < WipeRows; y++)
                for (int x = 0; x < WipeCols; x++)
                    _wipePixels[y * WipeCols + x] = Logic.ScreenWipe.Covered(_style, x, WipeRows - 1 - y, WipeCols, WipeRows, t) ? WipeColor : clear;
            _wipe.SetPixels32(_wipePixels);
            _wipe.Apply(false);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
