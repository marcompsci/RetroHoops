using CallerRetroBall.Core;
using CallerRetroBall.Logic;

namespace CallerRetroBall.UI
{
    /// <summary>Phase 37: opens the screen an App Store screenshot launch asked for (see <see cref="StoreShots"/>).</summary>
    public sealed partial class MainMenuController
    {
        /// <summary>A scene to open once the menu's own entrance transition has finished (GoTo is ignored mid-transition).</summary>
        private static string _storeShotScene;

        /// <summary>Called from Update: opens the waiting scene when the screen is free.</summary>
        private static void StoreShotTick()
        {
            if (_storeShotScene == null || SceneFlow.IsTransitioning) return;
            string scene = _storeShotScene;
            _storeShotScene = null;
            SceneFlow.GoTo(scene);
        }

        /// <summary>True the first time the menu builds on a store-screenshot launch: it opened the shot's screen and skips the first-launch dialogs.</summary>
        private bool RunStoreShot()
        {
            if (!StoreShots.Active) return false;
            if (StoreShots.Shown) return false; // back on the menu later: a normal menu
            StoreShots.Shown = true;
            switch (StoreShots.Current.Plan)
            {
                case StoreShotPlan.PlayMenu: ShowPlayMenu(); break;
                case StoreShotPlan.Clutch: ShowClutch(); break;
                case StoreShotPlan.Park: ShowPark(); break;
                case StoreShotPlan.Locker: _storeShotScene = SceneNames.LockerRoom; break;
                case StoreShotPlan.Pro: ShowPro(ShowPlayMenu); break;
                case StoreShotPlan.Game:
                case StoreShotPlan.FullCourt:
                    var request = StoreShots.GameRequest(App.Catalog, StoreShots.Current.Plan == StoreShotPlan.FullCourt);
                    if (request == null) break;
                    App.PendingMatch = request;
                    _storeShotScene = SceneNames.Game;
                    break;
            }
            return true;
        }
    }
}
