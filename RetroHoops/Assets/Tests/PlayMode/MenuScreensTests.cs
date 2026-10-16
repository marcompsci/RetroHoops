using System.Collections;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CallerRetroBall.Tests
{
    /// <summary>Each menu scene builds without logging errors (Unity fails the test on Debug.LogError/exceptions).</summary>
    public class MenuScreensTests
    {
        private static IEnumerator Load(string scene)
        {
            App.EnsureInitialized();
            SceneManager.LoadScene(scene);
            yield return null;
            yield return null;
            for (int i = 0; i < 10; i++) yield return null;
        }

        [UnityTest] public IEnumerator MainMenu_Builds() { yield return Load(SceneNames.MainMenu); Assert.IsNotNull(App.Career); }
        [UnityTest] public IEnumerator RiseHub_Builds() { yield return Load(SceneNames.Season); Assert.IsNotNull(App.Career.rise); }
        [UnityTest] public IEnumerator LockerRoom_Builds() { yield return Load(SceneNames.LockerRoom); Assert.IsNotNull(App.Career); }
        [UnityTest] public IEnumerator Settings_Builds() { yield return Load(SceneNames.Settings); Assert.IsNotNull(App.Career.settings); }

        // 16 Oct 2026: BALLER CITY builds in every district, and you stand somewhere you can walk.
        [UnityTest]
        public IEnumerator City_BuildsInEveryDistrict()
        {
            foreach (var d in CityDistricts.All)
            {
                App.EnsureInitialized();
                App.Career.city.Arrive(d.Id, null);
                yield return Load(SceneNames.City);
                Assert.IsNotNull(Object.FindAnyObjectByType<CallerRetroBall.City.CityController>(), d.Id);
                Assert.IsTrue(CityDistricts.Map(d.Id).Walkable(new Vec2(App.Career.city.x, App.Career.city.y)), d.Id);
            }
        }

        private static IEnumerator LoadGame(MatchRequest request)
        {
            App.EnsureInitialized();
            App.PendingMatch = request;
            SceneManager.LoadScene(SceneNames.Game);
            yield return null;
            yield return null;
            for (int i = 0; i < 30; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator Tutorial_Starts()
        {
            var r = MatchRequest.PracticeDefault();
            r.Mode = GameMode.Tutorial;
            yield return LoadGame(r);
            Assert.IsNotNull(Object.FindAnyObjectByType<CallerRetroBall.Gameplay.GameSceneController>().Match);
        }

        [UnityTest]
        public IEnumerator Versus_StartsWithTwoHumans()
        {
            var r = MatchRequest.QuickCallDefault(App.Catalog);
            r.Mode = GameMode.Versus;
            yield return LoadGame(r);
            var m = Object.FindAnyObjectByType<CallerRetroBall.Gameplay.GameSceneController>().Match;
            Assert.GreaterOrEqual(m.SecondControlledIndex, 0);
        }

        [UnityTest]
        public IEnumerator Daily_Starts()
        {
            yield return LoadGame(DailyChallenges.For(App.Today, App.Catalog).ToRequest());
            Assert.IsNotNull(Object.FindAnyObjectByType<CallerRetroBall.Gameplay.GameSceneController>().Match);
        }

        [UnityTest]
        public IEnumerator PracticeDrill_StartsWithDrillHud()
        {
            App.EnsureInitialized();
            var request = MatchRequest.PracticeDefault();
            request.Drill = (int)DrillKind.DribbleLane;
            App.PendingMatch = request;
            SceneManager.LoadScene(SceneNames.Game);
            yield return null;
            yield return null;
            Assert.IsNotNull(GameObject.Find("Cone 0"), "Dribble lane should place its cones");
        }
    }
}
