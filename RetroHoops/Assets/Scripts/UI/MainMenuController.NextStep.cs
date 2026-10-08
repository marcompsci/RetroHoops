using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using TMPro;
using UnityEngine;

namespace CallerRetroBall.UI
{
    /// <summary>Phase 39: the NEXT suggestion under the title, and the one-tap first game.</summary>
    public sealed partial class MainMenuController
    {
        private void BuildNextStep()
        {
            var next = Onboarding.Next(App.Career, App.Today);
            if (next == null) return;
            var b = UiKit.Button(Body, Loc.T("NEXT") + " ►  " + Loc.T(next.Title), () => RunNext(next.Action), ButtonStyle.Ghost, 60f, 26f);
            var rt = (RectTransform)b.transform;
            UiKit.Band(rt, 0.712f, 0.748f, 140f);
            var label = b.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.color = Theme.Gold;
        }

        private void RunNext(NextAction a)
        {
            switch (a)
            {
                case NextAction.Tutorial: StartTutorial(); break;
                case NextAction.QuickCall: ShowQuickCall(); break;
                case NextAction.Rise: SceneFlow.GoTo(SceneNames.Season); break;
                case NextAction.Clutch: ShowClutch(); break;
                case NextAction.DailyClutch:
                    var request = Clutch.DailyRequest(App.Catalog, App.Today, App.Career.settings.difficultyId);
                    if (request == null) { ShowClutch(); break; }
                    App.PendingMatch = request;
                    SceneFlow.GoTo(SceneNames.Game);
                    break;
                case NextAction.Daily: ShowDaily(); break;
                case NextAction.Weekly: ShowWeekly(); break;
                case NextAction.Park: ShowPark(); break;
                default:
                    if (Pro.FranchiseLocked(ProStore.Owned)) ShowPro(null); // FRANCHISE comes with Retro Hoops Pro
                    else FranchiseScreen.Open();
                    break;
            }
        }

        /// <summary>JUST PLAY on the first launch: a Quick Call on Rookie.</summary>
        private static void StartFirstGame()
        {
            var request = Onboarding.FirstGame(App.Catalog);
            if (request == null) return;
            App.PendingMatch = request;
            SceneFlow.GoTo(SceneNames.Game);
        }
    }
}
