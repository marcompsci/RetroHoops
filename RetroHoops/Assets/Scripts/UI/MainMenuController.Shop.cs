using System;
using System.Collections.Generic;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using CallerRetroBall.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// I147 (20 Nov 2026): the SHOP. Court packs (with a free try of each court every day), gear packs, Court Designer
    /// Plus, ALL-ACCESS and the tip jar, with plain terms under every price and RESTORE PURCHASES. Rules live in
    /// <see cref="Shop"/>; the App Store bridge is <see cref="ShopStore"/>.
    /// </summary>
    public sealed partial class MainMenuController
    {
        private static readonly Dictionary<string, Texture2D> ShopThumbs = new Dictionary<string, Texture2D>();

        private void ShowShop(Action back)
        {
            ShopStore.EnsureStarted();
            ShopStore.Sync();
            back = back ?? ShowPlayMenu;
            var career = App.Career;
            var column = OpenOverlay("SHOP", out var footer);

            Shop.Collection(career, out int have, out int total);
            UiKit.Size(UiKit.Label(column, Loc.T("COLLECTION") + "  " + have + " / " + total, 36f, Theme.Gold, TextAlignmentOptions.Center, true), 56f);
            UiKit.Size(UiKit.Label(column, Loc.T("Everything here changes how the game looks, never your ratings. Nothing is random, and there are no ads."),
                                   26f, Theme.Muted, TextAlignmentOptions.Center), 70f);
            if (Shop.Supporter(career))
                UiKit.Size(UiKit.Label(column, Loc.T("Thanks for supporting the studio!") + "  ·  " + Loc.T("TIPS") + " " + career.shopTips, 28f, Theme.Pink, TextAlignmentOptions.Center, true), 50f);

            // I149: this week's spotlight (a fixed rotation: every pack comes back at the same price, no timer).
            var spot = Shop.Spotlight(Shop.WeekNumber(DateTime.Now));
            UiKit.Size(UiKit.Label(column, Loc.T("THIS WEEK'S SPOTLIGHT") + ":  " + Loc.T(spot.Name), 30f, Theme.Cyan, TextAlignmentOptions.Center, true), 50f);

            var status = UiKit.Label(column, "", 28f, Theme.Muted, TextAlignmentOptions.Center);
            UiKit.Size(status, 60f);

            Section(column, "COURT PACKS");
            foreach (var p in Shop.Products) if (p.Kind == ShopKind.CourtPack) ShopCard(column, p, status, back);
            Section(column, "COURT DESIGNER");
            ShopCard(column, Shop.Product(Shop.DesignerPlus), status, back);
            Section(column, "GEAR PACKS");
            foreach (var p in Shop.Products) if (p.Kind == ShopKind.GearPack) ShopCard(column, p, status, back);
            Section(column, "EVERYTHING");
            ShopCard(column, Shop.Product(Shop.AllAccess), status, back);
            Section(column, "SUPPORT THE STUDIO");
            UiKit.Size(UiKit.Label(column, Loc.T("Retro Hoops is made by one person. A tip helps keep the updates coming. Your first one puts the Studio Supporter banner in your locker."),
                                   26f, Theme.Cream), 100f);
            foreach (var p in Shop.Products) if (p.Kind == ShopKind.Tip) ShopCard(column, p, status, back);

            var links = UiKit.Row(column, 16f, "Links");
            UiKit.Size(links, 90f);
            UiKit.Button(links, "TERMS OF USE", () => Application.OpenURL(LiveMode.TermsOfUseUrl), ButtonStyle.Ghost, 80f, 26f);
            UiKit.Button(links, "PRIVACY POLICY", () => Application.OpenURL(LiveMode.PrivacyPolicyUrl), ButtonStyle.Ghost, 80f, 26f);
            UiKit.Button(column, "RESTORE PURCHASES", () => { ShopStore.Restore(); ProStore.Restore(); status.text = Loc.T("Checking with the App Store…"); }, ButtonStyle.Ghost, 90f, 30f);

            UiKit.Button(footer, "BACK", back, ButtonStyle.Ghost, 130f, 44f);
            var watcher = column.gameObject.AddComponent<ShopWatcher>();
            watcher.Status = status;
            watcher.OnChanged = () => ShowShop(back);
        }

        private void ShopCard(RectTransform column, ShopProduct p, TextMeshProUGUI status, Action back)
        {
            if (p == null) return;
            var career = App.Career;
            bool owned = Shop.Owns(career, p.Id);
            string price = ShopStore.Price(p.Id);
            UiKit.Size(UiKit.Label(column, Loc.T(p.Name), 38f, owned ? Theme.Cyan : Theme.Cream, TextAlignmentOptions.Left, true), 56f);
            UiKit.Size(UiKit.Label(column, Loc.T(p.Blurb), 26f, Theme.Muted), 70f);

            if (p.Kind == ShopKind.CourtPack)
            {
                var row = UiKit.Row(column, 12f, "Courts");
                UiKit.Size(row, 330f);
                foreach (var courtId in p.Items) CourtThumb(row, courtId, owned, back);
            }
            else if (p.Kind == ShopKind.GearPack)
            {
                var row = UiKit.Row(column, 10f, "Gear");
                UiKit.Size(row, 90f);
                foreach (var id in p.Items)
                {
                    var item = App.Catalog.Find(App.Catalog.Cosmetics, id);
                    if (item == null) continue;
                    var sw = UiKit.Panel(row, item.colorA.ToColor(), name: "Swatch");
                    UiKit.Size(sw, 80f, 80f);
                    var inner = UiKit.Panel(sw.transform, item.colorB.ToColor(), name: "Inner");
                    UiKit.Place(inner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(36f, 36f));
                }
            }

            if (owned)
            {
                UiKit.Size(UiKit.Label(column, Loc.T("OWNED") + (p.Kind == ShopKind.GearPack ? "  ·  " + Loc.T("in your LOCKER ROOM") : p.Kind == ShopKind.Designer ? "  ·  " + Loc.T("LOCKER ROOM ► COURT") : p.Kind == ShopKind.CourtPack ? "  ·  " + Loc.T("pick them in QUICK CALL") : ""),
                                       28f, Theme.Cyan, TextAlignmentOptions.Left, true), 46f);
                return;
            }
            var buy = UiKit.Button(column, (p.Consumable ? Loc.T("TIP") : Loc.T("BUY")) + "  " + price, () =>
            {
                ShopStore.Buy(p.Id);
                status.text = Loc.T("Opening the App Store…");
            }, p.Kind == ShopKind.Bundle ? ButtonStyle.Primary : ButtonStyle.Secondary, 90f, 34f);
            UiKit.Size(buy, 90f);
            UiKit.Size(UiKit.Label(column, Shop.Terms(p, price), 20f, Theme.Muted), 80f);
        }

        /// <summary>A court's picture (drawn by the court generator once, then cached), its name, and TRY IT if today's try is free.</summary>
        private void CourtThumb(RectTransform row, string courtId, bool owned, Action back)
        {
            var c = App.Catalog;
            var def = c.Court(courtId);
            if (def == null) return;
            var cell = UiKit.NewRect("Court " + courtId, row);
            UiKit.Size(cell, 320f, 230f);
            var vertical = cell.gameObject.AddComponent<VerticalLayoutGroup>();
            vertical.spacing = 4f;
            vertical.childForceExpandHeight = false;
            vertical.childControlHeight = true;
            vertical.childControlWidth = true;

            if (!ShopThumbs.TryGetValue(courtId, out var tex) || tex == null)
            {
                tex = TextureFactory.ToTexture(CourtGenerator.Generate(def, CourtGeometry.Default, 11), "ui.shop." + courtId);
                ShopThumbs[courtId] = tex;
            }
            var pic = UiKit.Picture(cell, tex, "Thumb");
            UiKit.Size(pic, 200f);
            var fit = pic.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = tex.width / (float)tex.height;
            var name = UiKit.Label(cell, def.displayName.ToUpperInvariant(), 20f, Theme.Cream, TextAlignmentOptions.Center, true);
            name.enableAutoSizing = true;
            name.fontSizeMin = 12f;
            name.fontSizeMax = 20f;
            UiKit.Size(name, 34f);
            int day = CityNpcScheduler.DayNumber(DateTime.Now);
            if (owned)
            {
                UiKit.Size(UiKit.Button(cell, "PLAY", () => StartShopCourt(courtId), ButtonStyle.Ghost, 60f, 22f), 60f);
            }
            else if (Shop.CanTry(App.Career, courtId, day))
            {
                UiKit.Size(UiKit.Button(cell, "TRY IT FREE", () =>
                {
                    if (!Shop.UseTry(App.Career, courtId, day)) return;
                    App.SaveCareer();
                    StartShopCourt(courtId);
                }, ButtonStyle.Ghost, 60f, 22f), 60f);
            }
            else
            {
                UiKit.Size(UiKit.Label(cell, Loc.T("TRIED TODAY"), 18f, Theme.Muted, TextAlignmentOptions.Center), 60f);
            }
        }

        /// <summary>I148: a Quick Call on a shop court with your team.</summary>
        private void StartShopCourt(string courtId)
        {
            var req = Shop.PlayOn(App.Catalog, App.Career, courtId);
            if (req == null) return;
            App.PendingMatch = req;
            SceneFlow.GoTo(SceneNames.Game);
        }
    }

    /// <summary>Watches the App Store while the SHOP is open: shows progress, and redraws when something is bought or restored.</summary>
    public sealed class ShopWatcher : MonoBehaviour
    {
        public TextMeshProUGUI Status;
        public Action OnChanged;
        private ShopBuyState _last = (ShopBuyState)(-1);
        private float _nextSync;
        private bool _done;

        private void Update()
        {
            if (_done) return;
            var s = ShopStore.BuyState;
            if (s != _last)
            {
                _last = s;
                switch (s)
                {
                    case ShopBuyState.Purchasing: Status.text = Loc.T("Waiting for the App Store…"); break;
                    case ShopBuyState.Pending: Status.text = Loc.T("Waiting for approval (Ask to Buy). It unlocks as soon as it's approved."); break;
                    case ShopBuyState.Failed:
                        Status.text = "<color=#FF6B6B>" + (string.IsNullOrEmpty(ShopStore.Error) ? Loc.T("The purchase didn't go through.") : ShopStore.Error) + "</color>";
                        break;
                }
            }
            if (Time.unscaledTime < _nextSync) return;
            _nextSync = Time.unscaledTime + 0.5f;
            if (ShopStore.Sync())
            {
                _done = true;
                Haptics.Success();
                OnChanged?.Invoke();
            }
        }
    }
}
