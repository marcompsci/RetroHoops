using System.Runtime.InteropServices;

namespace CallerRetroBall.Core
{
    /// <summary>Is this copy from TestFlight? Shows the BETA FEEDBACK button and profile only there.</summary>
    public static class Beta
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int RetroBeta_IsTestFlight();
#endif
        private static int _cached = -1;

        public static bool IsTestFlight
        {
            get
            {
                if (_cached < 0)
                {
#if UNITY_IOS && !UNITY_EDITOR
                    try { _cached = RetroBeta_IsTestFlight() != 0 ? 1 : 0; } catch { _cached = 0; }
#else
                    _cached = 0;
#endif
                }
                return _cached == 1;
            }
        }
    }
}
