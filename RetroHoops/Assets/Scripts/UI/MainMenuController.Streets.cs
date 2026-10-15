using System.Collections.Generic;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using CallerRetroBall.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// 15 Oct 2026 (Omari): THE STREETS hub, from the main menu's own button and from PLAY.
    /// Phase 1: PICKUP GAME. Phase 2: the STREET TOUR, your STREETS CREW, and PLAY A FRIEND (LIVE, TWO PHONES, SAME PHONE).
    /// Phase 3: HOMECOURT, STREET GEAR, SIGNATURE SHOWSTOPPER and crew chemistry (MainMenuController.StreetsHome.cs).
    /// </summary>
    public sealed partial class MainMenuController
    {
        private static readonly TourCard StreetsCard = new TourCard("tour.streets", "THE STREETS", "");
        /// <summary>Set while LIVE / TWO PHONES / 2 PLAYER are opened from THE STREETS (their games use street rules).</summary>
        private static bool _streetsStyle;

        private static StreetsSaveData StreetsData()
        {
            var career = App.Career;
            return career.streets ?? (career.streets = new StreetsSaveData());
        }

        private void ShowStreets()
        {
            _streetsStyle = false;
            var s = StreetsData();
            var column = OpenOverlay("THE STREETS", out var footer);

            UiKit.Size(UiKit.Label(column, "3 ON 3 TO 21. TRICKS. COMBOS. SHOWSTOPPERS.", 34f, Theme.Gold, TextAlignmentOptions.Center, true), 60f);
            int level = StreetsTour.RepLevel(s.rep);
            string next = level + 1 < StreetsTour.RepNames.Length ? "  ·  " + (StreetsTour.RepNeeded[level + 1] - s.rep) + " " + Loc.T("to") + " " + Loc.T(StreetsTour.RepNames[level + 1]) : "";
            UiControls.Stat(column, "REP", s.rep + "  " + Loc.T(StreetsTour.RepNames[level]) + next);
            UiControls.Stat(column, "RECORD", s.wins + "-" + s.losses + "  ·  " + Loc.T("STARS") + " " + StreetsTour.TotalStars(s) + "/" + StreetsTour.MaxStars);
            UiControls.Stat(column, "SHOWSTOPPERS", s.showstoppers + "  ·  " + Loc.T("TRICKS") + " " + s.tricks + "  ·  " + Loc.T("BEST BANK") + " " + s.bestBank, 56f);

            Mode(column, "STREET TOUR", Loc.T("Four cities, twelve crews, then THE CROWN. Beat a crew to recruit its leader.") + "  "
                 + s.tourBeaten.Count + "/" + StreetsTour.Stops.Length, ShowStreetsTour, ButtonStyle.Primary);
            Mode(column, "PICKUP GAME", "Any crew, any street court, one game.", ShowStreetsPickup, ButtonStyle.Secondary);
            Mode(column, "STREETS CREW", Loc.T("Pick who runs with you: your recruits or your usual crew.") + "  " + Loc.T("RECRUITS") + " " + s.recruits.Count,
                 ShowStreetsCrew, ButtonStyle.Secondary);

            Section(column, "PLAY A FRIEND");
            Mode(column, "LIVE STREETS", Loc.T("Online against another STREETS player. Counts for your Live rating.") + "  "
                 + s.liveWins + "-" + s.liveLosses, () => { _streetsStyle = true; ShowLive(); }, ButtonStyle.Secondary);
            Mode(column, "TWO PHONES", "Each on your own phone, side by side.", () => { _streetsStyle = true; ShowLinkMenu(); }, ButtonStyle.Secondary);
            Mode(column, "SAME PHONE", "Lay it flat between you (or use controllers).", () => { _streetsStyle = true; ShowVersus(); }, ButtonStyle.Secondary);
            StreetsPhase3Rows(column);

            UiKit.Button(column, "HOW TO PLAY THE STREETS", ShowStreetsHowTo, ButtonStyle.Ghost, 90f, 32f);
            UiKit.Button(footer, "BACK", CloseOverlay, ButtonStyle.Ghost, 130f, 44f);

            // The first time here: how it works.
            if (!Tours.Seen(App.Career, StreetsCard))
            {
                Tours.MarkSeen(App.Career, StreetsCard);
                App.SaveCareer();
                ShowStreetsHowTo();
            }
        }

        /// <summary>Phase 3's rows (HOMECOURT, STREET GEAR, SIGNATURE): MainMenuController.StreetsHome.cs.</summary>
        partial void StreetsPhase3Rows(RectTransform column);

        // ------------------------------------------------------------------ pickup

        private void ShowStreetsPickup()
        {
            var c = App.Catalog;
            var career = App.Career;
            var s = StreetsData();
            var column = OpenOverlay("PICKUP GAME", out var footer);

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
            UiKit.Size(UiKit.Label(column, "Touch: TRICK (where CALL usually is) and SHOWSTOPPER when your meter is full.\nKeyboard: C trick · V showstopper",
                                   24f, Theme.Muted), 80f);

            UiKit.Button(footer, "BACK", ShowStreets, ButtonStyle.Ghost, 130f, 44f);
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
        }

        private static void Anchor(RectTransform rt, float y0, float y1)
        {
            rt.anchorMin = new Vector2(0f, y0);
            rt.anchorMax = new Vector2(1f, y1);
            rt.offsetMin = new Vector2(230f, 0f);
            rt.offsetMax = new Vector2(-24f, 0f);
        }

        // ------------------------------------------------------------------ the tour

        private void ShowStreetsTour()
        {
            var c = App.Catalog;
            var s = StreetsData();
            var column = OpenOverlay("STREET TOUR", out var footer);
            UiKit.Size(UiKit.Label(column, Loc.T("STARS") + " " + StreetsTour.TotalStars(s) + "/" + StreetsTour.MaxStars
                                           + "   " + Loc.T("Win · score a SHOWSTOPPER · win by 7+"), 28f, Theme.Muted, TextAlignmentOptions.Center, true), 56f);

            for (int tier = 0; tier <= StreetsTour.Cities.Length; tier++)
            {
                bool open = StreetsTour.CityOpen(s, tier);
                string city = tier < StreetsTour.Cities.Length ? StreetsTour.Cities[tier] : StreetsTour.FinaleName;
                Section(column, open ? city : city + "  ·  " + Loc.T("LOCKED"));
                foreach (var stop in StreetsTour.InCity(tier))
                {
                    var st = stop;
                    bool beat = StreetsTour.Beaten(s, stop);
                    int stars = StreetsTour.Stars(s, stop);
                    var row = UiKit.Row(column, 10f, "Stop " + stop.Id);
                    row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
                    UiKit.Size(row, 130f);
                    string starText = "<color=#FFD166>" + new string('*', stars) + "</color><color=#3A3A55>" + new string('*', 3 - stars) + "</color>";
                    var label = UiKit.Label(row, open
                        ? "<b>" + stop.Crew + "</b>  " + starText + "\n<size=22><color=#8D99AE>" + stop.Leader + "  ·  " + Loc.T("TO") + " " + stop.Target
                          + "  ·  " + (c.Court(stop.CourtId)?.displayName ?? "") + "\n\"" + stop.Trash + "\"</color></size>"
                        : "<b>???</b>\n<size=22><color=#8D99AE>" + (stop.Finale ? Loc.T("Beat every crew to find THE CROWN.") : Loc.T("Clear the city before to get here.")) + "</color></size>",
                        28f, open ? (beat ? Theme.Cyan : Theme.Cream) : Theme.Muted, TextAlignmentOptions.Left);
                    label.textWrappingMode = TextWrappingModes.Normal;
                    UiKit.Size(label).flexibleWidth = 1f;
                    var b = UiKit.Button(row, !open ? "LOCKED" : beat ? "RUN IT BACK" : "CALL OUT", () =>
                    {
                        App.PendingMatch = StreetsTour.Challenge(App.Catalog, App.Career, st);
                        SceneFlow.GoTo(SceneNames.Game);
                    }, !open ? ButtonStyle.Ghost : beat ? ButtonStyle.Secondary : ButtonStyle.Primary, 110f, 22f);
                    UiKit.Size(b, 110f, 210f);
                    b.interactable = open;
                }
            }
            UiKit.Button(footer, "BACK", ShowStreets, ButtonStyle.Ghost, 130f, 44f);
        }

        // ------------------------------------------------------------------ your crew

        private void ShowStreetsCrew()
        {
            var c = App.Catalog;
            var s = StreetsData();
            var column = OpenOverlay("STREETS CREW", out var footer);
            UiKit.Size(UiKit.Label(column, "You lead it. Beat a tour crew and its leader joins your pool. Put up to two recruits in your crew; an empty spot keeps your usual teammate.",
                                   26f, Theme.Cream), 110f);
            var pool = StreetsTour.Pool(c, s);
            var usual = CrewEngine.TeammateDefs(App.Career, c);
            var options = new List<string> { "" };
            foreach (var p in pool) options.Add(p.id);
            if (s.crew == null) s.crew = new List<string>();
            while (s.crew.Count < 2) s.crew.Add("");

            string Name(string id, int slot)
            {
                if (string.IsNullOrEmpty(id))
                {
                    var u = slot < usual.Count ? usual[slot] : null;
                    return Loc.T("USUAL") + ": " + (u != null ? (u.firstName + " " + u.lastName).Trim().ToUpperInvariant() : "-");
                }
                var p = c.Player(id);
                if (p == null) return "-";
                int toNext = StreetsChem.ToNext(s, id);
                return (p.firstName + " " + p.lastName).Trim().ToUpperInvariant() + "  " + p.attributes.Overall + " OVR"
                       + "\n<size=24><color=#FFD166>" + Loc.T("CHEMISTRY") + " " + Loc.T("LV") + " " + StreetsChem.Level(s, id) + "/" + StreetsChem.MaxLevel
                       + (toNext > 0 ? "</color><color=#8D99AE>  ·  " + toNext + " " + Loc.T("to next") : "") + "</color></size>";
            }

            for (int slot = 0; slot < 2; slot++)
            {
                int k = slot;
                UiKit.Size(UiKit.Label(column, Loc.T("SPOT") + " " + (slot + 1), 32f, Theme.Cyan, TextAlignmentOptions.Left, true), 50f);
                var label = UiKit.Label(column, Name(s.crew[slot], slot), 36f, Theme.Cream, TextAlignmentOptions.Center, true);
                UiKit.Size(label, 96f);
                var b = UiKit.Button(column, "CHANGE", () =>
                {
                    int at = Mathf.Max(0, options.IndexOf(s.crew[k]));
                    for (int step = 1; step <= options.Count; step++)
                    {
                        string next = options[(at + step) % options.Count];
                        if (next == "" || next != s.crew[1 - k])
                        {
                            StreetsTour.SetCrew(s, k, next);
                            break;
                        }
                    }
                    App.SaveCareer();
                    ShowStreetsCrew();
                }, ButtonStyle.Ghost, 90f, 32f);
                b.interactable = pool.Count > 0;
            }
            if (pool.Count == 0)
                UiKit.Size(UiKit.Label(column, "No recruits yet: win on the STREET TOUR.", 28f, Theme.Muted, TextAlignmentOptions.Center), 60f);
            // Phase 3: CREW CHEMISTRY.
            int crewChem = StreetsChem.CrewLevel(s);
            UiControls.Stat(column, "CHEMISTRY", crewChem + "/" + (StreetsChem.MaxLevel * 2) + "  ·  +" + crewChem + "% " + Loc.T("METER"));
            UiKit.Size(UiKit.Label(column, "Every game a recruit plays with you builds chemistry (a win counts double). Chemistry makes your crew sharper and fills your meter faster.",
                                   24f, Theme.Muted), 80f);
            UiKit.Button(footer, "BACK", ShowStreets, ButtonStyle.Ghost, 130f, 44f);
        }

        private static void ShowStreetsHowTo()
        {
            var lines = new List<string>();
            foreach (var l in Streets.HowTo) lines.Add(Loc.T(l));
            UiControls.InfoDialog("THE STREETS", string.Join("\n\n", lines));
        }
    }
}
