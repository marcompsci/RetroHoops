using System;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using CallerRetroBall.Utilities;
using TMPro;
using UnityEngine;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// TestFlight only (12 Oct 2026): the beta tester's profile, and SEND FEEDBACK, which copies a report for the
    /// tester to paste into TestFlight's own Share Beta Feedback (the game itself sends nothing).
    /// </summary>
    public static class BetaFeedbackScreen
    {
        private static GameObject _root;

        /// <summary>The tester profile form. <paramref name="then"/> runs after SAVE or SKIP.</summary>
        public static void OpenProfile(Action then = null)
        {
            if (_root != null) return;
            var d = App.Career;
            var b = d.beta;
            var canvas = UiKit.CreateScreenCanvas("BetaProfile", 45);
            _root = canvas.gameObject;
            var scrim = UiKit.Panel(canvas.transform, Theme.Scrim, name: "Scrim");
            UiKit.Stretch(scrim.rectTransform);
            scrim.raycastTarget = true;
            var safe = UiKit.SafeArea(canvas.transform);
            var panel = UiKit.Panel(safe, Color.white, Theme.PanelSprite(), true, "Panel");
            UiKit.Band(panel.rectTransform, 0.06f, 0.94f, 40f);
            panel.raycastTarget = true;

            var footer = UiKit.Row(panel.transform, 20f, "Footer");
            footer.anchorMin = new Vector2(0f, 0f);
            footer.anchorMax = new Vector2(1f, 0f);
            footer.pivot = new Vector2(0.5f, 0f);
            footer.sizeDelta = new Vector2(-80f, 130f);
            footer.anchoredPosition = new Vector2(0f, 24f);
            string name = b.name ?? "";
            int games = b.hoopsGames, hoops = b.hoops;
            void Finish(bool save)
            {
                if (save)
                {
                    b.name = (name ?? "").Trim();
                    if (b.name.Length > BetaFeedback.MaxName) b.name = b.name.Substring(0, BetaFeedback.MaxName);
                    b.hoopsGames = games;
                    b.hoops = hoops;
                }
                b.asked = true;
                App.SaveCareer();
                Close();
                then?.Invoke();
            }
            UiKit.Button(footer, "SKIP", () => Finish(false), ButtonStyle.Ghost, 120f);
            UiKit.Button(footer, "SAVE", () => { Haptics.Success(); Finish(true); }, ButtonStyle.Primary, 120f);

            var body = UiKit.NewRect("Body", panel.transform);
            UiKit.Stretch(body);
            body.offsetMin = new Vector2(0f, 170f);
            var col = UiKit.ScrollColumn(body, 14f, new RectOffset(36, 36, 30, 20));
            UiKit.Size(UiKit.ShadowLabel(col, "BETA TESTER", 56f, Theme.Cream, Theme.Pink, 6f).transform.parent.GetComponent<RectTransform>(), 86f);
            UiKit.Size(UiKit.Label(col, "Thanks for testing! Tell us a little about you. It stays on your phone and only goes out when you send feedback.", 30f, Theme.Muted), 120f);
            UiKit.Size(UiKit.Label(col, "NAME OR HANDLE", 36f, Theme.Cyan, TextAlignmentOptions.Left, true), 56f);
            UiControls.TextField(col, name, BetaFeedback.MaxName, v => name = v, "Name or @handle");
            UiControls.ChoiceRow(col, "PLAY BASKETBALL GAMES?", BetaFeedback.HoopsGamesOptions, Math.Max(0, games), i => games = i);
            UiControls.ChoiceRow(col, "HOOP IN REAL LIFE?", BetaFeedback.HoopOptions, Math.Max(0, hoops), i => hoops = i);
            UiKit.Size(UiKit.Label(col, "Use SEND FEEDBACK on the main menu any time something feels off, breaks, or is great.", 28f, Theme.Muted), 90f);
            if (games < 0) games = 0;
            if (hoops < 0) hoops = 0;
        }

        /// <summary>Copies the report and shows how to send it through TestFlight.</summary>
        public static void SendFeedback()
        {
            var d = App.Career;
            GUIUtility.systemCopyBuffer = BetaFeedback.Report(d, App.VersionAndBuild, SystemInfo.deviceModel, SystemInfo.operatingSystem, DateTime.UtcNow);
            d.beta.sent++;
            App.SaveCareer();
            Haptics.Light();
            UiControls.Dialog("SEND FEEDBACK",
                "Your tester info and game stats are copied.\n\n1. Take a screenshot (side button + volume up).\n2. Tap the screenshot, then Share Beta Feedback (or press and hold the Retro Hoops icon ► Send Beta Feedback).\n3. Paste into the comment and tell us what happened.",
                ("GOT IT", ButtonStyle.Primary, null),
                ("EDIT MY PROFILE", ButtonStyle.Ghost, () => OpenProfile()));
        }

        private static void Close()
        {
            if (_root != null) UnityEngine.Object.Destroy(_root);
            _root = null;
        }
    }
}
