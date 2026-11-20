using UnityEngine;

namespace CallerRetroBall.Core
{
    /// <summary>
    /// I165 (20 Nov 2026): when the game comes back to the front, ask the App Store again. An Ask to Buy approval, a
    /// purchase on another device or a refund that happened while the game was in the background lands straight away.
    /// </summary>
    public sealed class StoreResync : MonoBehaviour
    {
        private static StoreResync _instance;
        private float _last;

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("StoreResync");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<StoreResync>();
        }

        private void OnApplicationFocus(bool focus)
        {
            if (focus) Resync();
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused) Resync();
        }

        private void Resync()
        {
            // Focus and un-pause often arrive together: once is enough.
            if (Time.unscaledTime - _last < 2f) return;
            _last = Time.unscaledTime;
            ProStore.Refresh();
            ProStore.Remember();
            ShopStore.Sync();
        }
    }
}
