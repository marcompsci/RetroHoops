#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using System.Collections.Generic;
using CallerRetroBall.Logic;

namespace CallerRetroBall.Core
{
    /// <summary>Where a SHOP purchase is up to.</summary>
    public enum ShopBuyState { Idle = 0, Purchasing = 1, Pending = 2, Failed = 3, Done = 4 }

    /// <summary>
    /// I142 (20 Nov 2026): the SHOP's App Store bridge (Plugins/iOS/RetroShop.swift, StoreKit 2). Many products at once:
    /// the one-time unlocks (court packs, gear packs, Court Designer Plus, ALL-ACCESS) and the consumable tips. The App
    /// Store is the only judge of what's owned; <see cref="Sync"/> copies its settled answer into the career through
    /// <see cref="Shop.Apply"/>. Retro Hoops Pro keeps its own bridge (<see cref="ProStore"/>). In the Editor a test
    /// set stands in for the App Store.
    /// </summary>
    public static class ShopStore
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void RetroShop_Start(string idsCsv);
        [DllImport("__Internal")] private static extern int RetroShop_Ready();
        [DllImport("__Internal")] private static extern string RetroShop_OwnedCsv();
        [DllImport("__Internal")] private static extern string RetroShop_Price(string id);
        [DllImport("__Internal")] private static extern void RetroShop_Buy(string id);
        [DllImport("__Internal")] private static extern int RetroShop_BuyState();
        [DllImport("__Internal")] private static extern string RetroShop_Error();
        [DllImport("__Internal")] private static extern string RetroShop_TakeTipsCsv();
        [DllImport("__Internal")] private static extern void RetroShop_Restore();
#else
        /// <summary>Editor only: what the pretend App Store says is owned.</summary>
        public static readonly HashSet<string> EditorOwned = new HashSet<string>();
        private static readonly List<string> EditorTips = new List<string>();
        private static ShopBuyState _editorState;
#endif
        private static bool _started;

        public static void EnsureStarted()
        {
            if (_started) return;
            _started = true;
            StoreResync.Ensure(); // I165
#if UNITY_IOS && !UNITY_EDITOR
            RetroShop_Start(string.Join(",", Shop.ProductIds()));
#endif
        }

        /// <summary>True once the App Store has answered what's owned (until then the career's cached answer stands).</summary>
        public static bool Ready
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return RetroShop_Ready() != 0;
#else
                return true;
#endif
            }
        }

        /// <summary>The localised price ("$1.99", "1,99 €"…) or the shop's fallback while it loads.</summary>
        public static string Price(string productId)
        {
            var p = Shop.Product(productId);
#if UNITY_IOS && !UNITY_EDITOR
            string s = RetroShop_Price(productId);
            if (!string.IsNullOrEmpty(s)) return s;
#endif
            return p != null ? p.PriceFallback : "";
        }

        /// <summary>I194: true once the App Store has sent at least one price (false offline, or before the products exist).</summary>
        public static bool PricesLoaded
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                foreach (var id in Shop.ProductIds()) if (!string.IsNullOrEmpty(RetroShop_Price(id))) return true;
                return false;
#else
                return true;
#endif
            }
        }

        public static ShopBuyState BuyState
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return (ShopBuyState)RetroShop_BuyState();
#else
                return _editorState;
#endif
            }
        }

        public static string Error
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return RetroShop_Error() ?? "";
#else
                return "";
#endif
            }
        }

        public static void Buy(string productId)
        {
            EnsureStarted();
#if UNITY_IOS && !UNITY_EDITOR
            RetroShop_Buy(productId);
#else
            var p = Shop.Product(productId);
            if (p == null) return;
            if (p.Consumable) EditorTips.Add(productId);
            else EditorOwned.Add(productId);
            _editorState = ShopBuyState.Done;
#endif
        }

        public static void Restore()
        {
            EnsureStarted();
#if UNITY_IOS && !UNITY_EDITOR
            RetroShop_Restore();
#endif
        }

        /// <summary>
        /// Copies the App Store's answer into the career: owned unlocks (gear goes in the locker, refunded gear comes
        /// out) and any tips that just went through. Returns true if the career changed (and saves it).
        /// </summary>
        public static bool Sync()
        {
            var d = App.Career;
            if (d == null) return false;
            EnsureStarted();
            bool changed = false;
            foreach (var tip in TakeTips()) changed |= Shop.RecordTip(d, tip);
            if (Ready) changed |= Shop.Apply(d, App.Catalog, Owned());
            if (changed)
            {
                ProStore.Remember(); // ALL-ACCESS includes Pro
                App.SaveCareer();
            }
            return changed;
        }

        private static List<string> Owned()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return Split(RetroShop_OwnedCsv());
#else
            return new List<string>(EditorOwned);
#endif
        }

        private static List<string> TakeTips()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return Split(RetroShop_TakeTipsCsv());
#else
            var list = new List<string>(EditorTips);
            EditorTips.Clear();
            return list;
#endif
        }

        private static List<string> Split(string csv)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(csv)) return list;
            foreach (var s in csv.Split(',')) if (s.Length > 0) list.Add(s);
            return list;
        }
    }
}
