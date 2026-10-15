using System.Collections.Generic;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using CallerRetroBall.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CallerRetroBall.UI
{
    /// <summary>15 Oct 2026 (Omari): THE STREETS hub, from the main menu's own button and from PLAY.</summary>
    public sealed partial class MainMenuController
    {
        private static readonly TourCard StreetsCard = new TourCard("tour.streets", "THE STREETS", "");

        private void ShowStreets()
        {
            var c = App.Catalog;
            var career = App.Career;
            var s = career.streets ?? (career.streets = new StreetsSaveData());
            var column = OpenOverlay("THE STREETS", out var footer);

            UiKit.Size(UiKit.Label(column, "3 ON 3 TO 21. TRICKS. COMBOS. SHOWSTOPPERS.", 34f, Theme.Gold, TextAlignmentOptions.Center, true), 60f);
            UiKit.Size(UiKit.Label(column, "No clock, no fouls, no mercy. Show them up with the ball, bank your style with a bucket, then break the game open.",
                                   26f, Theme.Cream), 80f);
            UiControls.Stat(column, "RECORD", s.wins + "-" + s.losses + (s.streak > 1 ? "  ·  " + Loc.T("WON") + " " + s.streak + " " + Loc.T("IN A ROW") : ""));
            UiControls.Stat(column, "SHOWSTOPPERS", s.showstoppers + "  ·  " + Loc.T("TRICKS") + " " + s.tricks + "  ·  " + Loc.T("BEST BANK") + " " + s.bestBank);

            // Pickup game: the crew you run against and where.
            UiKit.Size(UiKit.Label(column, "PICKUP GAME", 36f, Theme.Cyan, TextAlignmentOptions.Left, true), 56f);
            var opponents = Streets.Opponents(c);
            var courts = Streets.CourtList(c);
            int opp = Mathf.Max(0, opponents.FindIndex(t => t.id == s.lastOpponent));
            int court = Mathf.Max(0, courts.FindIndex(k => k.id == s.lastCourt));

            var card = UiKit.Panel(column, new Color32(0x1A, 0x1A, 0x2E, 255), Theme.PanelSprite(), true, "Crew");
            UiKit.Size(card, 230f);
            var logo = UiKit.Picture(card.transform, null, "Logo");
            logo.rectTransform.anchorMin = logo.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            logo.rectTransform.pivot = new Vector2(0f, 0.5f);
            logo.rectTransform.sizeDelta = new Vector2(170f, 170f);
            logo.rectTransform.anchoredPosition = new Vector2(30f, 0f);
            var oppName = UiKit.Label(card.transform, "", 40f, Theme.Cream, TextAlignmentOptions.Left, true);
            Anchor(oppName.rectTransform, 0.66f, 0.95f);
            var oppLine = UiKit.Label(card.transform, "", 24f, Theme.Muted, TextAlignmentOptions.Left, false);
            Anchor(oppLine.rectTransform, 0.08f, 0.64f);
            oppLine.textWrappingMode = TextWrappingModes.Normal;

            void Show()
            {
                if (opponents.Count == 0) return;
                var t = opponents[opp];
                if (logo.texture != null) Destroy(logo.texture);
                logo.texture = TextureFactory.TeamLogo(t);
                oppName.text = t.FullName.ToUpperInvariant();
                oppLine.text = Loc.T("CREW") + " " + ArcadeEngine.TeamOverall(c, t) + " OVR"
                               + (string.IsNullOrEmpty(t.motto) ? "" : "\n\"" + t.motto + "\"");
            }
            Show();
            UiKit.Button(column, "CHANGE CREW", () =>
            {
                if (opponents.Count == 0) return;
                opp = (opp + 1) % opponents.Count;
                Show();
            }, ButtonStyle.Ghost, 100f, 36f);

            if (courts.Count > 0)
                UiControls.ChoiceRow(column, "COURT", courts.ConvertAll(k => k.displayName.ToUpperInvariant()).ToArray(), court, i => court = i);

            var difficulties = c.Difficulties;
            int diff = Mathf.Max(0, difficulties.FindIndex(d => d.id == career.settings.difficultyId));
            UiControls.ChoiceRow(column, "DIFFICULTY", difficulties.ConvertAll(d => d.displayName.ToUpperInvariant()).ToArray(), diff, i => diff = i);

            UiKit.Button(column, "HOW TO PLAY THE STREETS", ShowStreetsHowTo, ButtonStyle.Secondary, 100f, 34f);
            UiKit.Size(UiKit.Label(column, "Touch: TRICK (where CALL usually is) and SHOWSTOPPER when your meter is full.\nKeyboard: C trick · V showstopper",
                                   24f, Theme.Muted), 80f);

            UiKit.Button(footer, "BACK", CloseOverlay, ButtonStyle.Ghost, 130f, 44f);
            UiKit.Button(footer, "TIP OFF", () =>
            {
                if (opponents.Count == 0) return;
                s.lastOpponent = opponents[opp].id;
                s.lastCourt = courts.Count > 0 ? courts[court].id : "";
                career.settings.difficultyId = difficulties[diff].id;
                App.SaveCareer();
                var request = Streets.Pickup(c, career, s.lastOpponent, s.lastCourt);
                request.DifficultyId = difficulties[diff].id;
                App.PendingMatch = request;
                SceneFlow.GoTo(SceneNames.Game);
            }, ButtonStyle.Primary, 130f);

            // The first time here: how it works.
            if (!Tours.Seen(career, StreetsCard))
            {
                Tours.MarkSeen(career, StreetsCard);
                App.SaveCareer();
                ShowStreetsHowTo();
            }
        }

        private static void Anchor(RectTransform rt, float y0, float y1)
        {
            rt.anchorMin = new Vector2(0f, y0);
            rt.anchorMax = new Vector2(1f, y1);
            rt.offsetMin = new Vector2(230f, 0f);
            rt.offsetMax = new Vector2(-24f, 0f);
        }

        private static void ShowStreetsHowTo()
        {
            var lines = new List<string>();
            foreach (var l in Streets.HowTo) lines.Add(Loc.T(l));
            UiControls.InfoDialog("THE STREETS", string.Join("\n\n", lines));
        }
    }
}
