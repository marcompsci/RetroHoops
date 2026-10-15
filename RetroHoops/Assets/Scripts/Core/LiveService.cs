#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using CallerRetroBall.Logic;

namespace CallerRetroBall.Core
{
    public enum StoreState { Loading = 0, NotOwned = 1, Owned = 2, Purchasing = 3, Failed = 4, Pending = 5 }

    /// <summary>
    /// Retro Hoops Pro, the one-time unlock (Plugins/iOS/RetroStore.swift, StoreKit 2). The App Store is the only judge
    /// of whether it's owned; the career keeps the last answer (<see cref="CareerSaveData.proOwned"/>) so the menus are
    /// right straight away. In the Editor a test switch stands in for the App Store. (Before Phase 41 this was the
    /// Retro Hoops Live subscription; LIVE is free now.)
    /// </summary>
    public static class ProStore
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void RetroStore_Start(string productId);
        [DllImport("__Internal")] private static extern int RetroStore_State();
        [DllImport("__Internal")] private static extern string RetroStore_Price();
        [DllImport("__Internal")] private static extern string RetroStore_Error();
        [DllImport("__Internal")] private static extern void RetroStore_Buy();
        [DllImport("__Internal")] private static extern void RetroStore_Restore();
        [DllImport("__Internal")] private static extern void RetroStore_Refresh();
#else
        /// <summary>Editor only: pretend Pro is owned.</summary>
        public static bool EditorOwned;
#endif
        private static bool _started;

        public static void EnsureStarted()
        {
            if (_started) return;
            _started = true;
#if UNITY_IOS && !UNITY_EDITOR
            RetroStore_Start(Pro.ProductId);
#endif
        }

        public static StoreState State
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return (StoreState)RetroStore_State();
#else
                return EditorOwned ? StoreState.Owned : StoreState.NotOwned;
#endif
            }
        }

        /// <summary>The localised price from the App Store ("$4.99", "£4.99"…), or the fallback while it loads.</summary>
        public static string Price
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                string p = RetroStore_Price();
                return string.IsNullOrEmpty(p) ? Pro.PriceFallback : p;
#else
                return Pro.PriceFallback;
#endif
            }
        }

        public static string Error
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return RetroStore_Error() ?? "";
#else
                return "";
#endif
            }
        }

        /// <summary>Owned: the App Store's answer, or the career's cached answer while it's still loading or offline.</summary>
        public static bool Owned
        {
            get
            {
                var s = State;
                if (s == StoreState.Owned) return true;
                if (s == StoreState.NotOwned) return false;
                return App.Career != null && App.Career.proOwned;
            }
        }

        /// <summary>Copies the App Store's settled answer into the career (and grants the pass items the first time).</summary>
        public static void Remember()
        {
            var d = App.Career;
            if (d == null) return;
            var s = State;
            if (s != StoreState.Owned && s != StoreState.NotOwned) return;
            if (Pro.Apply(d, App.Catalog, s == StoreState.Owned)) App.SaveCareer();
        }

        public static void Buy()
        {
#if UNITY_IOS && !UNITY_EDITOR
            RetroStore_Buy();
#else
            EditorOwned = true;
#endif
        }

        public static void Restore()
        {
#if UNITY_IOS && !UNITY_EDITOR
            RetroStore_Restore();
#endif
        }

        public static void Refresh()
        {
#if UNITY_IOS && !UNITY_EDITOR
            RetroStore_Refresh();
#endif
        }
    }

    /// <summary>
    /// A Live match connection (Plugins/iOS/RetroLive.mm): Game Center finds an opponent on the same game
    /// version and relays the messages. Implements the same wire as two-phone play.
    /// </summary>
    public sealed class LiveLink : ILinkTransport
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void RetroLive_Find(int group);
        [DllImport("__Internal")] private static extern void RetroLive_Stop();
        [DllImport("__Internal")] private static extern int RetroLive_State();
        [DllImport("__Internal")] private static extern string RetroLive_Error();
        [DllImport("__Internal")] private static extern string RetroLive_OpponentName();
        [DllImport("__Internal")] private static extern string RetroLive_OpponentId();
        [DllImport("__Internal")] private static extern string RetroLive_LocalId();
        [DllImport("__Internal")] private static extern string RetroLive_LocalName();
        [DllImport("__Internal")] private static extern int RetroLive_Send(byte[] data, int length);
        [DllImport("__Internal")] private static extern int RetroLive_NextLength();
        [DllImport("__Internal")] private static extern int RetroLive_Receive(byte[] buffer, int capacity);
        [DllImport("__Internal")] private static extern void RetroLive_FetchIdentity();
        [DllImport("__Internal")] private static extern int RetroLive_IdentityState();
        [DllImport("__Internal")] private static extern string RetroLive_IdentityKeyUrl();
        [DllImport("__Internal")] private static extern string RetroLive_IdentitySignature();
        [DllImport("__Internal")] private static extern string RetroLive_IdentitySalt();
        [DllImport("__Internal")] private static extern double RetroLive_IdentityTimestamp();
        [DllImport("__Internal")] private static extern void RetroLive_Invite(int group);
        [DllImport("__Internal")] private static extern int RetroLive_ListenForInvites();
        [DllImport("__Internal")] private static extern int RetroLive_TakeInvite();
        public static bool Supported => true;
#else
        public static bool Supported => false;
#endif
        public static LiveLink Current { get; private set; }

        /// <summary>Asks Game Center for an opponent on this game version.</summary>
        public static LiveLink Find(string appVersion, bool streets = false)
        {
            Stop();
            Current = new LiveLink();
#if UNITY_IOS && !UNITY_EDITOR
            RetroLive_Find(LiveMode.PlayerGroup(appVersion, streets));
#endif
            return Current;
        }

        /// <summary>Phase 33: INVITE A FRIEND — Apple's Game Center invite screen, for a Live game with a friend.</summary>
        public static LiveLink Invite(string appVersion, bool streets = false)
        {
            Stop();
            Current = new LiveLink();
#if UNITY_IOS && !UNITY_EDITOR
            RetroLive_Invite(LiveMode.PlayerGroup(appVersion, streets));
#endif
            return Current;
        }

        private static bool _listening;

        /// <summary>Lets friends' Game Center invites reach the game (once Game Center has signed in).</summary>
        public static void ListenForInvites()
        {
            if (_listening) return;
#if UNITY_IOS && !UNITY_EDITOR
            _listening = RetroLive_ListenForInvites() != 0; // 0 = not signed in yet: try again next time
#else
            _listening = true;
#endif
        }

        /// <summary>True once when a friend's invite was accepted outside the game; it is already connecting (use <see cref="AdoptInvite"/>).</summary>
        public static bool TakeInvite()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return RetroLive_TakeInvite() != 0;
#else
            return false;
#endif
        }

        /// <summary>The connection for an accepted invite (Game Center is already connecting it).</summary>
        public static LiveLink AdoptInvite()
        {
            Current = new LiveLink();
            return Current;
        }

        public static void Stop()
        {
            if (Current == null) return;
#if UNITY_IOS && !UNITY_EDITOR
            RetroLive_Stop();
#endif
            Current = null;
        }

        public LinkState State
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return (LinkState)RetroLive_State();
#else
                return LinkState.Idle;
#endif
            }
        }

        public string PeerName
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return RetroLive_OpponentName() ?? "";
#else
                return "";
#endif
            }
        }

        public string Error
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return RetroLive_Error() ?? "";
#else
                return "Live needs an iPhone, iPad or Mac build.";
#endif
            }
        }

        /// <summary>0 = host (the player whose Game Center id sorts first), 1 = guest.</summary>
        public int Seat
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return LiveMode.Seat(RetroLive_LocalId(), RetroLive_OpponentId());
#else
                return 0;
#endif
            }
        }

        /// <summary>This player's Game Center teamPlayerID (the id the Live server knows them by).</summary>
        public static string LocalId
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return RetroLive_LocalId() ?? "";
#else
                return "";
#endif
            }
        }

        /// <summary>The opponent's teamPlayerID (connected games only).</summary>
        public string OpponentId
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return RetroLive_OpponentId() ?? "";
#else
                return "";
#endif
            }
        }

        public struct IdentityProof
        {
            public string KeyUrl, Signature, Salt;
            public double Timestamp;
        }

        /// <summary>Asks Game Center for a signed proof of who this player is (for the Live server). 0 none, 1 working, 2 ready, 3 failed.</summary>
        public static void FetchIdentity()
        {
#if UNITY_IOS && !UNITY_EDITOR
            RetroLive_FetchIdentity();
#endif
        }

        public static int IdentityState
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return RetroLive_IdentityState();
#else
                return 3;
#endif
            }
        }

        public static IdentityProof Identity
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return new IdentityProof { KeyUrl = RetroLive_IdentityKeyUrl(), Signature = RetroLive_IdentitySignature(), Salt = RetroLive_IdentitySalt(), Timestamp = RetroLive_IdentityTimestamp() };
#else
                return default;
#endif
            }
        }

        public static string LocalName
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return RetroLive_LocalName() ?? "";
#else
                return "";
#endif
            }
        }

        public void Send(byte[] message)
        {
            if (message == null || message.Length == 0) return;
#if UNITY_IOS && !UNITY_EDITOR
            RetroLive_Send(message, message.Length);
#endif
        }

        public bool TryReceive(out byte[] message)
        {
            message = null;
#if UNITY_IOS && !UNITY_EDITOR
            int n = RetroLive_NextLength();
            if (n < 0) return false;
            message = new byte[n];
            if (n == 0) { RetroLive_Receive(message, 0); return true; }
            return RetroLive_Receive(message, n) == n;
#else
            return false;
#endif
        }
    }
}
