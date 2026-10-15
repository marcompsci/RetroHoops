using System.Collections.Generic;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// 15 Oct 2026 (Omari), THE STREETS phase 3 on the hub: HOMECOURT, STREET GEAR and SIGNATURE SHOWSTOPPER.
    /// (CREW CHEMISTRY shows on the STREETS CREW screen.)
    /// </summary>
    public sealed partial class MainMenuController
    {
        partial void StreetsPhase3Rows(RectTransform column)
        {
            var s = StreetsData();
            Section(column, "MAKE IT YOURS");
            string home = !StreetsHome.Unlocked(s) ? Loc.T(StreetsHome.UnlockHint)
                : !StreetsHome.Claimed(s) ? Loc.T("Claim a court and defend it from every crew in the city.")
                : StreetsHome.Name(s) + "  ·  " + Loc.T("DEFENSES") + " " + s.homeDefenses + (s.homeTaken ? "  ·  " + Loc.T("TAKEN!") : "");
            Mode(column, "HOMECOURT", home, ShowStreetsHome, StreetsHome.Unlocked(s) ? ButtonStyle.Primary : ButtonStyle.Ghost);
            var worn = StreetsGear.Worn(s);
            Mode(column, "STREET GEAR", Loc.T("WEARING") + ": " + (worn != null ? worn.Name : Loc.T("YOUR USUAL KIT")) + "  ·  "
                 + StreetsGear.UnlockedCount(s) + "/" + StreetsGear.Sets.Count, ShowStreetsGear, ButtonStyle.Secondary);
            Mode(column, "SIGNATURE SHOWSTOPPER", Loc.T("Your SHOWSTOPPER dunk:") + " " + Loc.T(StreetsSignature.Label(s)), ShowStreetsSignature, ButtonStyle.Secondary);
        }

        // ------------------------------------------------------------------ HOMECOURT

        private void ShowStreetsHome()
        {
            var c = App.Catalog;
            var career = App.Career;
            var s = StreetsData();
            var column = OpenOverlay("HOMECOURT", out var footer);
            UiKit.Button(footer, "BACK", ShowStreets, ButtonStyle.Ghost, 130f, 44f);
            if (!StreetsHome.Unlocked(s))
            {
                UiKit.Size(UiKit.Label(column, StreetsHome.UnlockHint, 34f, Theme.Muted, TextAlignmentOptions.Center, true), 120f);
                PerkList(column, s);
                return;
            }

            var courts = StreetsHome.Choices(c, career);
            if (courts.Count == 0) return;
            int pick = Mathf.Max(0, courts.FindIndex(k => k.id == s.homeCourt));
            bool claimed = StreetsHome.Claimed(s);

            UiKit.Size(UiKit.Label(column, claimed ? StreetsHome.Name(s) : Loc.T("CLAIM YOUR COURT"), 54f, s.homeTaken ? Theme.Pink : Theme.Gold,
                                   TextAlignmentOptions.Center, true), 80f);
            UiControls.ChoiceRow(column, "NAME", StreetsHome.Names, s.homeName, i =>
            {
                s.homeName = i;
                App.SaveCareer();
            });
            UiControls.ChoiceRow(column, "COURT", courts.ConvertAll(k => k.displayName.ToUpperInvariant()).ToArray(), pick, i =>
            {
                pick = i;
                if (StreetsHome.Claimed(s) && StreetsHome.Claim(s, c, career, courts[i].id)) App.SaveCareer();
            });
            UiKit.Size(UiKit.Label(column, "Build your own court in LOCKER ROOM ► COURT and it shows up here.", 24f, Theme.Muted), 44f);

            if (claimed)
            {
                UiControls.Stat(column, "STATUS", s.homeTaken
                    ? Loc.T("TAKEN BY") + " " + (StreetsTour.Find(s.homeTaker)?.Crew ?? "?")
                    : Loc.T("YOURS"));
                UiControls.Stat(column, "DEFENSES", s.homeDefenses + "  ·  " + Loc.T("STREAK") + " " + s.homeStreak + "  ·  " + Loc.T("BEST") + " " + s.homeBest
                                                     + "  ·  " + Loc.T("LOSSES") + " " + s.homeLosses);

                var next = StreetsHome.Challenger(s);
                Section(column, s.homeTaken ? "TAKE IT BACK" : StreetsHome.BossNext(s) ? "BOSS DEFENSE" : "NEXT UP");
                var card = UiKit.Label(column, "<b>" + next.Crew + "</b>  ·  " + next.Leader + "\n<size=24><color=#8D99AE>\"" + next.Trash + "\"</color></size>",
                                       32f, Theme.Cream, TextAlignmentOptions.Center);
                card.textWrappingMode = TextWrappingModes.Normal;
                UiKit.Size(card, 100f);
            }
            PerkList(column, s);

            UiKit.Button(footer, claimed ? (s.homeTaken ? "TAKE IT BACK" : "DEFEND") : "CLAIM IT", () =>
            {
                if (!StreetsHome.Claimed(s))
                {
                    if (StreetsHome.Claim(s, c, career, courts[pick].id))
                    {
                        App.SaveCareer();
                        ShowStreetsHome();
                    }
                    return;
                }
                var request = StreetsHome.Defend(c, career);
                if (request == null) return;
                App.PendingMatch = request;
                SceneFlow.GoTo(SceneNames.Game);
            }, ButtonStyle.Primary, 130f);
        }

        private static void PerkList(RectTransform column, StreetsSaveData s)
        {
            Section(column, "PERKS");
            foreach (var p in StreetsHome.Perks)
            {
                bool on = s.homeDefenses >= p.Defenses;
                var label = UiKit.Label(column, (on ? "<color=#06D6A0>" + Loc.T("ON") + "</color>  " : "<color=#3A3A55>" + p.Defenses + " " + Loc.T("DEFENSES") + "</color>  ")
                                                + "<b>" + Loc.T(p.Name) + "</b>\n<size=22><color=#8D99AE>" + Loc.T(p.Line) + "</color></size>",
                                        28f, on ? Theme.Cream : Theme.Muted, TextAlignmentOptions.Left);
                label.textWrappingMode = TextWrappingModes.Normal;
                UiKit.Size(label, 84f);
            }
            if (s.homeTaken)
                UiKit.Size(UiKit.Label(column, "Perks are off until you take your court back.", 24f, Theme.Pink, TextAlignmentOptions.Center), 44f);
        }

        // ------------------------------------------------------------------ STREET GEAR

        private void ShowStreetsGear()
        {
            var s = StreetsData();
            var column = OpenOverlay("STREET GEAR", out var footer);
            UiKit.Size(UiKit.Label(column, "Your crew wears it in THE STREETS. Beat a tour crew to take their colors.", 26f, Theme.Cream), 80f);
            var worn = StreetsGear.Worn(s);
            GearRow(column, null, worn == null, true, () => { s.gear = ""; });
            foreach (var set in StreetsGear.Sets)
                GearRow(column, set, worn == set, StreetsGear.Unlocked(s, set), () => { s.gear = set.Id; });
            UiKit.Button(footer, "BACK", ShowStreets, ButtonStyle.Ghost, 130f, 44f);
        }

        private void GearRow(RectTransform column, GearSet set, bool worn, bool open, System.Action wear)
        {
            var row = UiKit.Row(column, 14f, "Gear " + (set?.Id ?? "usual"));
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
            UiKit.Size(row, 100f);
            var sw = UiKit.Panel(row, set != null && open ? (Color)(Color32)Swatch(set.Jersey) : new Color32(0x2A, 0x2A, 0x40, 255), null, false, "Swatch");
            UiKit.Size(sw, 90f, 90f);
            if (set != null && open)
            {
                var stripe = UiKit.Panel(sw.transform, (Color32)Swatch(set.Trim), null, false, "Trim");
                stripe.rectTransform.anchorMin = new Vector2(0f, 0.38f);
                stripe.rectTransform.anchorMax = new Vector2(1f, 0.62f);
                stripe.rectTransform.offsetMin = stripe.rectTransform.offsetMax = Vector2.zero;
            }
            string name = set == null ? Loc.T("YOUR USUAL KIT") : set.Name;
            var label = UiKit.Label(row, open ? "<b>" + name + "</b>" + (set == null ? "\n<size=22><color=#8D99AE>" + Loc.T("Your Kit Studio kit or your crew's colors.") + "</color></size>" : "")
                                         : "<b>???</b>\n<size=22><color=#8D99AE>" + Loc.T(set.Hint) + "</color></size>",
                                    28f, open ? (worn ? Theme.Cyan : Theme.Cream) : Theme.Muted, TextAlignmentOptions.Left);
            label.textWrappingMode = TextWrappingModes.Normal;
            UiKit.Size(label).flexibleWidth = 1f;
            var b = UiKit.Button(row, worn ? "WORN" : open ? "WEAR" : "LOCKED", () =>
            {
                wear();
                App.SaveCareer();
                ShowStreetsGear();
            }, worn ? ButtonStyle.Secondary : open ? ButtonStyle.Primary : ButtonStyle.Ghost, 90f, 22f);
            UiKit.Size(b, 90f, 170f);
            b.interactable = open && !worn;
        }

        private static Color32 Swatch(RgbColor c) => new Color32(c.r, c.g, c.b, 255);

        // ------------------------------------------------------------------ SIGNATURE SHOWSTOPPER

        private void ShowStreetsSignature()
        {
            var s = StreetsData();
            var column = OpenOverlay("SIGNATURE", out var footer);
            UiKit.Size(UiKit.Label(column, "Dunk with your SHOWSTOPPER on and you throw down your signature while the camera comes in. More open up as your REP grows.",
                                   26f, Theme.Cream), 110f);
            var chosen = StreetsSignature.Chosen(s);
            SignatureRow(column, s, -1, "YOUR DUNK PACKAGE", Loc.T("The dunk you equipped in the Locker Room."), !chosen.HasValue, true);
            for (int i = 0; i < StreetsSignature.Order.Length; i++)
            {
                var style = StreetsSignature.Order[i];
                bool open = StreetsSignature.Unlocked(s, style);
                SignatureRow(column, s, (int)style, Dunks.Name(style).TrimEnd('!'),
                             open ? "" : Loc.T("Reach") + " " + Loc.T(StreetsTour.RepNames[StreetsSignature.RepLevel[i]]) + " " + Loc.T("rep."),
                             chosen.HasValue && chosen.Value == style, open);
            }
            UiKit.Button(footer, "BACK", ShowStreets, ButtonStyle.Ghost, 130f, 44f);
        }

        private void SignatureRow(RectTransform column, StreetsSaveData s, int value, string name, string line, bool used, bool open)
        {
            var row = UiKit.Row(column, 14f, "Sig " + value);
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
            UiKit.Size(row, 90f);
            var label = UiKit.Label(row, "<b>" + Loc.T(name) + "</b>" + (line.Length > 0 ? "\n<size=22><color=#8D99AE>" + line + "</color></size>" : ""),
                                    30f, !open ? Theme.Muted : used ? Theme.Cyan : Theme.Cream, TextAlignmentOptions.Left);
            label.textWrappingMode = TextWrappingModes.Normal;
            UiKit.Size(label).flexibleWidth = 1f;
            var b = UiKit.Button(row, used ? "IN USE" : open ? "USE" : "LOCKED", () =>
            {
                s.signature = value;
                App.SaveCareer();
                ShowStreetsSignature();
            }, used ? ButtonStyle.Secondary : open ? ButtonStyle.Primary : ButtonStyle.Ghost, 90f, 22f);
            UiKit.Size(b, 90f, 170f);
            b.interactable = open && !used;
        }
    }
}
