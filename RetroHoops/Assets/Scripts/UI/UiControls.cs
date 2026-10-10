using System;
using CallerRetroBall.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CallerRetroBall.UI
{
    /// <summary>Code-built form controls in the Retro Hoops style: slider, toggle, text field, dialogs.</summary>
    public static class UiControls
    {
        /// <summary>Label + slider row (0..1 by default). Returns the slider.</summary>
        public static Slider SliderRow(Transform parent, string label, float value, Action<float> onChanged,
                                       float min = 0f, float max = 1f)
        {
            var row = UiKit.NewRect("Slider " + label, parent);
            UiKit.Size(row, 110f);
            var text = UiKit.Label(row, label, 36f, Theme.Cream, TextAlignmentOptions.Left, true);
            text.rectTransform.anchorMin = new Vector2(0f, 0f);
            text.rectTransform.anchorMax = new Vector2(0.42f, 1f);
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;

            var sliderRt = UiKit.NewRect("Slider", row);
            sliderRt.anchorMin = new Vector2(0.45f, 0.3f);
            sliderRt.anchorMax = new Vector2(1f, 0.7f);
            sliderRt.offsetMin = sliderRt.offsetMax = Vector2.zero;

            var bg = UiKit.Panel(sliderRt, Theme.InkLight, name: "Background");
            UiKit.Stretch(bg.rectTransform);
            var fillArea = UiKit.NewRect("Fill Area", sliderRt);
            UiKit.Stretch(fillArea);
            var fill = UiKit.Panel(fillArea, Theme.Cyan, name: "Fill");
            UiKit.Stretch(fill.rectTransform);
            var handleArea = UiKit.NewRect("Handle Slide Area", sliderRt);
            UiKit.Stretch(handleArea);
            var handle = UiKit.Panel(handleArea, Color.white, Theme.DiscSprite(Theme.Cream, Theme.Shadow), false, "Handle");
            handle.rectTransform.sizeDelta = new Vector2(60f, 60f);
            handle.raycastTarget = true;

            var slider = sliderRt.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            slider.onValueChanged.AddListener(v => onChanged?.Invoke(v));
            return slider;
        }

        /// <summary>A button that shows ON/OFF and flips its value.</summary>
        public static void ToggleRow(Transform parent, string label, bool value, Action<bool> onChanged)
        {
            var row = UiKit.NewRect("Toggle " + label, parent);
            UiKit.Size(row, 110f);
            var text = UiKit.Label(row, label, 36f, Theme.Cream, TextAlignmentOptions.Left, true);
            text.rectTransform.anchorMin = new Vector2(0f, 0f);
            text.rectTransform.anchorMax = new Vector2(0.6f, 1f);
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;

            bool state = value;
            TextMeshProUGUI buttonLabel = null;
            var button = UiKit.Button(row, state ? "ON" : "OFF", () =>
            {
                state = !state;
                buttonLabel.text = Logic.Loc.T(state ? "ON" : "OFF");
                AudioManager.Click();
                onChanged?.Invoke(state);
            }, ButtonStyle.Secondary, 90f, 36f);
            buttonLabel = button.GetComponentInChildren<TextMeshProUGUI>();
            var brt = (RectTransform)button.transform;
            brt.anchorMin = new Vector2(0.65f, 0.1f);
            brt.anchorMax = new Vector2(1f, 0.9f);
            brt.offsetMin = brt.offsetMax = Vector2.zero;
        }

        /// <summary>Label + choice cycling button (e.g. difficulty).</summary>
        public static void ChoiceRow(Transform parent, string label, string[] options, int index, Action<int> onChanged)
        {
            var row = UiKit.NewRect("Choice " + label, parent);
            UiKit.Size(row, 110f);
            var text = UiKit.Label(row, label, 36f, Theme.Cream, TextAlignmentOptions.Left, true);
            text.rectTransform.anchorMin = new Vector2(0f, 0f);
            text.rectTransform.anchorMax = new Vector2(0.45f, 1f);
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;

            int current = Mathf.Clamp(index, 0, options.Length - 1);
            TextMeshProUGUI buttonLabel = null;
            var button = UiKit.Button(row, options[current], () =>
            {
                current = (current + 1) % options.Length;
                buttonLabel.text = Logic.Loc.T(options[current]);
                AudioManager.Click();
                onChanged?.Invoke(current);
            }, ButtonStyle.Secondary, 90f, 34f);
            buttonLabel = button.GetComponentInChildren<TextMeshProUGUI>();
            var brt = (RectTransform)button.transform;
            brt.anchorMin = new Vector2(0.5f, 0.1f);
            brt.anchorMax = new Vector2(1f, 0.9f);
            brt.offsetMin = brt.offsetMax = Vector2.zero;
        }

        /// <summary>Single-line text field (nickname).</summary>
        public static TMP_InputField TextField(Transform parent, string value, int maxLength, Action<string> onEndEdit) =>
            TextField(parent, value, maxLength, onEndEdit, "Nickname");

        /// <summary>Single-line text field with its own placeholder (shown in the current language).</summary>
        public static TMP_InputField TextField(Transform parent, string value, int maxLength, Action<string> onEndEdit, string placeholderText)
        {
            var box = UiKit.Panel(parent, Color.white, Theme.ButtonSprite(ButtonStyle.Secondary), true, "TextField");
            box.raycastTarget = true;
            UiKit.Size(box, 110f);
            var area = UiKit.NewRect("Text Area", box.transform);
            UiKit.Stretch(area, 24f);
            area.gameObject.AddComponent<RectMask2D>();

            var placeholder = UiKit.Label(area, placeholderText, 40f, Theme.Muted, TextAlignmentOptions.Left, false, "Placeholder");
            UiKit.Stretch(placeholder.rectTransform);
            var text = UiKit.Label(area, "", 40f, Theme.Cream, TextAlignmentOptions.Left, true, "Text");
            UiKit.Stretch(text.rectTransform);

            var field = box.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.characterLimit = maxLength;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.contentType = TMP_InputField.ContentType.Standard;
            field.text = value;
            field.onEndEdit.AddListener(v => onEndEdit?.Invoke(v));
            return field;
        }

        /// <summary>Full-screen modal with a message and up to three buttons. Returns the root to destroy.</summary>
        public static GameObject Dialog(string title, string body, params (string label, ButtonStyle style, Action onClick)[] buttons)
        {
            var canvas = UiKit.CreateScreenCanvas("DialogCanvas", 50);
            var scrim = UiKit.Panel(canvas.transform, Theme.Scrim, name: "Scrim");
            UiKit.Stretch(scrim.rectTransform);
            scrim.raycastTarget = true;
            var panel = UiKit.Panel(canvas.transform, Color.white, Theme.PanelSprite(), true, "Dialog");
            UiKit.Band(panel.rectTransform, 0.3f, 0.72f, 80f);
            panel.gameObject.AddComponent<OverlayPop>();
            var column = UiKit.Column(panel.transform, 22f, new RectOffset(40, 40, 40, 40));
            UiKit.Stretch(column);
            column.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;

            UiKit.Size(UiKit.Label(column, title, 54f, Theme.Gold, TextAlignmentOptions.Center, true), 80f);
            UiKit.Size(UiKit.Label(column, body, 34f, Theme.Cream), 200f);
            var root = canvas.gameObject;
            foreach (var b in buttons)
            {
                var action = b.onClick;
                UiKit.Button(column, b.label, () =>
                {
                    UnityEngine.Object.Destroy(root);
                    action?.Invoke();
                }, b.style, 120f, 44f);
            }
            return root;
        }

        /// <summary>Small stat pill: "LABEL value".</summary>
        public static TextMeshProUGUI Stat(Transform parent, string label, string value, float height = 64f)
        {
            var t = UiKit.Label(parent, "<color=#8D99AE>" + label + "</color>  " + value, 34f, Theme.Cream, TextAlignmentOptions.Center, true);
            UiKit.Size(t, height);
            return t;
        }
    }
}
