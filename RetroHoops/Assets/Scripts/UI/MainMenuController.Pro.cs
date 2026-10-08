using System;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using TMPro;
using UnityEngine;

namespace CallerRetroBall.UI
{
    /// <summary>Phase 41: PLAY ► RETRO HOOPS PRO, the game's one purchase (a one-time unlock, not a subscription).</summary>
    public sealed partial class MainMenuController
    {
        private void ShowPro(Action back)
        {
            ProStore.EnsureStarted();
            ProStore.Remember();
            var column = OpenOverlay("RETRO HOOPS PRO", out var footer);
            back = back ?? ShowPlayMenu;

            if (ProStore.Owned)
            {
                UiKit.Size(UiKit.Label(column, Loc.T("PRO UNLOCKED"), 48f, Theme.Gold, TextAlignmentOptions.Center, true), 80f);
                UiKit.Size(UiKit.Label(column, Loc.T("Thanks for supporting Retro Hoops! Rise seasons 4 and up are open, and every Hoops Pass item is in your locker."), 30f, Theme.Cream), 150f);
                UiKit.Button(column, "RESTORE PURCHASES", () => ProStore.Restore(), ButtonStyle.Ghost, 90f, 30f);
                UiKit.Button(footer, "BACK", back, ButtonStyle.Ghost, 130f, 44f);
                return;
            }

            UiKit.Size(UiKit.Label(column, "• " + string.Join("\n• ", Array.ConvertAll(Pro.Perks, Loc.T)), 30f, Theme.Cream), 300f);
            var price = UiKit.Label(column, ProStore.Price + "  ·  " + Loc.T("ONE TIME"), 48f, Theme.Gold, TextAlignmentOptions.Center, true);
            UiKit.Size(price, 80f);
            var status = UiKit.Label(column, "", 28f, Theme.Muted, TextAlignmentOptions.Center);
            UiKit.Size(status, 90f);
            var terms = UiKit.Label(column, Pro.Terms(ProStore.Price), 22f, Theme.Muted);
            UiKit.Size(terms, 200f);
            var links = UiKit.Row(column, 16f, "Links");
            UiKit.Size(links, 90f);
            UiKit.Button(links, "TERMS OF USE", () => Application.OpenURL(LiveMode.TermsOfUseUrl), ButtonStyle.Ghost, 80f, 26f);
            UiKit.Button(links, "PRIVACY POLICY", () => Application.OpenURL(LiveMode.PrivacyPolicyUrl), ButtonStyle.Ghost, 80f, 26f);
            UiKit.Button(column, "RESTORE PURCHASES", () => { ProStore.Restore(); status.text = Loc.T("Checking with the App Store…"); }, ButtonStyle.Ghost, 90f, 30f);

            UiKit.Button(footer, "BACK", back, ButtonStyle.Ghost, 130f, 44f);
            UiKit.Button(footer, "UNLOCK PRO", () => { ProStore.Buy(); status.text = Loc.T("Opening the App Store…"); }, ButtonStyle.Primary, 130f);

            var watcher = column.gameObject.AddComponent<ProWatcher>();
            watcher.Price = price;
            watcher.Terms = terms;
            watcher.Status = status;
            watcher.OnOwned = () =>
            {
                Haptics.Success();
                ShowPro(back);
            };
        }
    }
}
