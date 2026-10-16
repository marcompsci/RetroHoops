using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CallerRetroBall.Core;
using CallerRetroBall.Data;
using CallerRetroBall.Gameplay;
using CallerRetroBall.Logic;
using CallerRetroBall.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CallerRetroBall.EditorTools
{
    /// <summary>
    /// One-click (and first-open automatic) project setup. Creates the six scenes,
    /// registers them in Build Settings, applies iOS portrait Player Settings,
    /// imports TextMeshPro essentials, and writes default content to editable
    /// ScriptableObject assets. Safe to run repeatedly: existing scenes and assets
    /// are kept unless you choose an explicit overwrite command.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        public const string ScenesFolder = "Assets/Scenes";
        private const string AutoRunKey = "CallerRetroBall.ProjectSetup.AutoRanThisSession";
        private const string PlaceholderBundleId = "com.retroball.game";
        /// <summary>Placeholder used before the rename; replaced with the new one on the next setup run.</summary>
        private const string OldPlaceholderBundleId = "com.callerretroball.game";

        static ProjectSetup()
        {
            // First open of a fresh clone: build scenes automatically once the editor settles.
            if (File.Exists(ScenePath(SceneNames.Boot))) return;
            if (SessionState.GetBool(AutoRunKey, false)) return;
            SessionState.SetBool(AutoRunKey, true);
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
                Debug.Log("[CallerRetroBall] First open detected — running project setup.");
                Run(interactive: false);
            };
        }

        public static string ScenePath(string sceneName) => ScenesFolder + "/" + sceneName + ".unity";

        [MenuItem("Retro Hoops/Run Project Setup", priority = 0)]
        public static void RunFromMenu() => Run(interactive: true);

        [MenuItem("Retro Hoops/Rebuild Scenes (overwrite)", priority = 20)]
        public static void RebuildScenes()
        {
            if (!EditorUtility.DisplayDialog("Rebuild scenes?",
                    "This overwrites all seven scenes in Assets/Scenes with fresh generated versions.", "Rebuild", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateScenes(overwrite: true);
            ConfigureBuildSettings();
            EditorSceneManager.OpenScene(ScenePath(SceneNames.Boot));
        }

        [MenuItem("Retro Hoops/Regenerate Content Assets (overwrite)", priority = 21)]
        public static void RegenerateContent()
        {
            if (!EditorUtility.DisplayDialog("Regenerate content?",
                    "This overwrites every asset in Assets/Resources/Data with the built-in defaults. Your Inspector tuning will be lost.",
                    "Overwrite", "Cancel"))
                return;
            int written = GenerateContentAssets(overwrite: true);
            Debug.Log("[CallerRetroBall] Regenerated " + written + " content assets.");
        }

        [MenuItem("Retro Hoops/Validate Content", priority = 40)]
        public static void ValidateContentMenu()
        {
            var report = ContentValidator.Validate(ContentDatabase.Load().Catalog);
            if (report.IsValid) Debug.Log("[CallerRetroBall] " + report);
            else Debug.LogError("[CallerRetroBall] " + report);
        }

        public static void Run(bool interactive)
        {
            if (interactive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var log = new List<string>();
            EnsureFolders();
            log.Add(ImportTextMeshProEssentials());
            log.Add(ConfigurePlayerSettings());
            log.Add("Content assets created: " + GenerateContentAssets(overwrite: false));
            log.Add("Scenes created: " + CreateScenes(overwrite: false));
            ConfigureBuildSettings();
            log.Add("Build Settings: " + string.Join(", ", SceneNames.All));
            log.Add(CheckRenderPipeline());

            var report = ContentValidator.Validate(ContentDatabase.Load().Catalog);
            log.Add(report.ToString());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(ScenePath(SceneNames.Boot));

            Debug.Log("[CallerRetroBall] Project setup finished:\n  • " + string.Join("\n  • ", log));
            if (interactive)
                EditorUtility.DisplayDialog("Retro Hoops", "Project setup finished. See the Console for details.\n\nPress Play in BootScene to start.", "OK");
        }

        // ------------------------------------------------------------------ settings rebuild

        /// <summary>
        /// Batch mode (tools/Restore Project Settings.command): when the ProjectSettings folder has been lost, Unity
        /// recreates it with defaults on launch; this puts back everything Retro Hoops depends on. It's the same
        /// settings Project Setup and the release builds apply, plus what the earlier builds show they used:
        /// - the new Input System only (the old compile defines had ENABLE_INPUT_SYSTEM and no legacy input);
        /// - the bundle ID com.phoronomicstudios.retrohoops and team X6LZQ3FS36 (-teamId on the command line);
        /// - version 1.0.0 build 2 (the last archive), iOS 15, iPhone + iPad, the six scenes in Build Settings;
        /// - the built-in renderer (no pipeline asset was ever assigned).
        /// </summary>
        public static void RestoreSettingsBatch()
        {
            var log = new List<string>();
            EnsureFolders();
            log.Add(ConfigurePlayerSettings());
            log.Add(ReleaseTools.ApplyReleaseSettings());
            ReleaseTools.EnsureSizeSettings();
            var ios = UnityEditor.Build.NamedBuildTarget.iOS;
            if (PlayerSettings.GetApplicationIdentifier(ios) != ReleaseTools.DefaultBundleId)
                PlayerSettings.SetApplicationIdentifier(ios, ReleaseTools.DefaultBundleId);
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-teamId" && ReleaseTools.IsTeamId(args[i + 1])) PlayerSettings.iOS.appleDeveloperTeamID = args[i + 1];
            if (!int.TryParse(PlayerSettings.iOS.buildNumber, out int build) || build < 2) PlayerSettings.iOS.buildNumber = "2";
            PlayerSettings.SetScriptingBackend(ios, ScriptingImplementation.IL2CPP);
            log.Add(SetInputSystemOnly());
            ConfigureBuildSettings();
            log.Add(CheckRenderPipeline());
            AssetDatabase.SaveAssets();
            Debug.Log("[CallerRetroBall] Project settings restored:\n  • " + string.Join("\n  • ", log) +
                      "\n  • Bundle ID " + PlayerSettings.GetApplicationIdentifier(ios) + ", team " + PlayerSettings.iOS.appleDeveloperTeamID +
                      ", build " + PlayerSettings.iOS.buildNumber);
        }

        /// <summary>
        /// Batch mode: the whole first-open setup (TextMeshPro essentials, content assets, the six scenes, Build Settings),
        /// for a project whose generated assets are missing (tools/Restore Project Settings.command runs it when
        /// Assets/Scenes/BootScene.unity isn't there). Keeps anything that already exists.
        /// </summary>
        public static void SetupBatch()
        {
            Debug.Log("[CallerRetroBall] " + ImportTmpEssentialsNow());
            Run(interactive: false);
            Debug.Log("[CallerRetroBall] Setup batch done. Boot scene present: " + File.Exists(ScenePath(SceneNames.Boot)));
        }

        /// <summary>Player Settings ▸ Active Input Handling = Input System Package (New).</summary>
        private static string SetInputSystemOnly()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return "Input handling: couldn't open ProjectSettings.asset (set it in Player Settings)";
            var so = new SerializedObject(assets[0]);
            var prop = so.FindProperty("activeInputHandler");
            if (prop == null) return "Input handling: setting not found (set it in Player Settings)";
            prop.intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
            return "Input handling: Input System Package (New)";
        }

        // ------------------------------------------------------------------ folders

        private static void EnsureFolders()
        {
            string[] folders =
            {
                "Assets/Art/Generated", "Assets/Art/Placeholder", "Assets/Audio/Placeholder", "Assets/Fonts", "Assets/Materials",
                "Assets/Prefabs/Basketball", "Assets/Prefabs/Characters", "Assets/Prefabs/Courts", "Assets/Prefabs/UI",
                "Assets/Resources/Data", ScenesFolder, "Assets/Settings",
            };
            foreach (var f in folders) EnsureFolder(f);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ------------------------------------------------------------------ TextMeshPro

        /// <summary>
        /// Phase 36: imports TMP Essential Resources straight from the uGUI package's .unitypackage (synchronous in batch
        /// mode, unlike the importer window), so a rebuilt project has fonts before the first build.
        /// </summary>
        public static string ImportTmpEssentialsNow()
        {
            if (Resources.Load("TMP Settings") != null) return "TextMeshPro essentials: already present";
            foreach (var name in new[] { "com.unity.ugui", "com.unity.textmeshpro" })
            {
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/" + name);
                if (info == null || string.IsNullOrEmpty(info.resolvedPath)) continue;
                string pkg = Path.Combine(info.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
                if (!File.Exists(pkg)) continue;
                UnityEditor.AssetPackage.Package.Import(pkg, false);
                AssetDatabase.Refresh();
                return "TextMeshPro essentials: imported from " + name;
            }
            return "TextMeshPro essentials: package file not found (Window ▸ TextMeshPro ▸ Import TMP Essential Resources)";
        }

        private static string ImportTextMeshProEssentials()
        {
            if (Resources.Load("TMP Settings") != null) return "TextMeshPro essentials: already present";

            // TMP_PackageResourceImporter lives in the TMP editor assembly; its location has
            // moved between versions, so find it by name rather than hard-referencing it.
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("TMPro.TMP_PackageResourceImporter", false))
                .FirstOrDefault(t => t != null);
            var method = type?.GetMethod("ImportResources", new[] { typeof(bool), typeof(bool), typeof(bool) });
            if (method == null)
            {
                Debug.LogWarning("[CallerRetroBall] Could not auto-import TMP Essentials. Use Window ▸ TextMeshPro ▸ Import TMP Essential Resources.");
                return "TextMeshPro essentials: MANUAL STEP REQUIRED (Window ▸ TextMeshPro ▸ Import TMP Essential Resources)";
            }
            method.Invoke(null, new object[] { true, false, false });
            return "TextMeshPro essentials: import started";
        }

        // ------------------------------------------------------------------ player settings

        private static string ConfigurePlayerSettings()
        {
            PlayerSettings.productName = DefaultContent.GameName;
            if (string.IsNullOrEmpty(PlayerSettings.companyName) || PlayerSettings.companyName == "DefaultCompany")
                PlayerSettings.companyName = "Retro Hoops Dev";
            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "0.1" ||
                PlayerSettings.bundleVersion == "1.0" || PlayerSettings.bundleVersion == "0.1.0")
                PlayerSettings.bundleVersion = ReleaseTools.ReleaseVersion;

            // Only replace Unity's default identifier; never clobber one the developer set.
            var ios = UnityEditor.Build.NamedBuildTarget.iOS;
            var android = UnityEditor.Build.NamedBuildTarget.Android;
            string currentId = PlayerSettings.GetApplicationIdentifier(ios);
            if (string.IsNullOrEmpty(currentId) || currentId.StartsWith("com.DefaultCompany", StringComparison.Ordinal) ||
                currentId.StartsWith("com.Company", StringComparison.Ordinal) || currentId == OldPlaceholderBundleId ||
                currentId.StartsWith("com.Unity-Technologies", StringComparison.Ordinal) || currentId.StartsWith("com.UnityTechnologies", StringComparison.Ordinal))
            {
                PlayerSettings.SetApplicationIdentifier(ios, PlaceholderBundleId);
                PlayerSettings.SetApplicationIdentifier(android, PlaceholderBundleId);
            }

            // Portrait everywhere, landscape for Full Court (the game locks the one it wants at runtime).
            ReleaseTools.EnsureOrientations();
            PlayerSettings.statusBarHidden = true;

            // iOS
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            if (string.IsNullOrEmpty(PlayerSettings.iOS.buildNumber) || PlayerSettings.iOS.buildNumber == "0")
                PlayerSettings.iOS.buildNumber = "1";

            return "Player Settings: portrait, iOS 15+, id " + PlayerSettings.GetApplicationIdentifier(ios) +
                   (PlayerSettings.GetApplicationIdentifier(ios) == PlaceholderBundleId ? " (PLACEHOLDER — change before App Store submission)" : "");
        }

        // ------------------------------------------------------------------ content assets

        public static int GenerateContentAssets(bool overwrite)
        {
            var c = DefaultContent.Create();
            int n = 0;
            n += WriteKind<ArchetypeData, ArchetypeDef>(ContentPaths.Archetypes, c.Archetypes, overwrite);
            n += WriteKind<PlayerData, PlayerDef>(ContentPaths.Players, c.Players, overwrite);
            n += WriteKind<TeamData, TeamDef>(ContentPaths.Teams, c.Teams, overwrite);
            n += WriteKind<CourtData, CourtDef>(ContentPaths.Courts, c.Courts, overwrite);
            n += WriteKind<GameRulesData, GameRulesDef>(ContentPaths.Rules, c.Rules, overwrite);
            n += WriteKind<DifficultyData, DifficultyDef>(ContentPaths.Difficulties, c.Difficulties, overwrite);
            n += WriteKind<UpgradeData, UpgradeDef>(ContentPaths.Upgrades, c.Upgrades, overwrite);
            n += WriteKind<CosmeticData, CosmeticDef>(ContentPaths.Cosmetics, c.Cosmetics, overwrite);
            n += WriteKind<SeasonConfigData, SeasonConfigDef>(ContentPaths.Seasons, c.Seasons, overwrite);
            AssetDatabase.SaveAssets();
            return n;
        }

        private static int WriteKind<TAsset, TDef>(string resourcesPath, List<TDef> defs, bool overwrite)
            where TAsset : DefinitionAsset<TDef>
            where TDef : class, IHasId
        {
            string folder = "Assets/Resources/" + resourcesPath;
            EnsureFolder(folder);
            int written = 0;
            foreach (var def in defs)
            {
                string path = folder + "/" + def.Id + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<TAsset>(path);
                if (existing != null)
                {
                    if (!overwrite) continue;
                    existing.SetDefinition(def);
                    EditorUtility.SetDirty(existing);
                }
                else
                {
                    var asset = ScriptableObject.CreateInstance<TAsset>();
                    asset.SetDefinition(def);
                    AssetDatabase.CreateAsset(asset, path);
                }
                written++;
            }
            return written;
        }

        // ------------------------------------------------------------------ scenes

        private static readonly Dictionary<string, Type> SceneControllers = new Dictionary<string, Type>
        {
            { SceneNames.Boot, typeof(BootController) },
            { SceneNames.MainMenu, typeof(MainMenuController) },
            { SceneNames.Game, typeof(GameSceneController) },
            { SceneNames.Season, typeof(SeasonScreenController) },
            { SceneNames.LockerRoom, typeof(LockerRoomController) },
            { SceneNames.Settings, typeof(SettingsController) },
            { SceneNames.City, typeof(City.CityController) },
        };

        public static int CreateScenes(bool overwrite)
        {
            EnsureFolder(ScenesFolder);
            int created = 0;
            foreach (var sceneName in SceneNames.All)
            {
                string path = ScenePath(sceneName);
                if (File.Exists(path) && !overwrite) continue;

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
                var cam = cameraGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 5f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color32(0x1A, 0x1A, 0x2E, 255);
                cam.transform.position = new Vector3(0f, 0f, -10f);
                cameraGo.AddComponent<AudioListener>();

                var root = new GameObject(sceneName.Replace("Scene", "") + "Root");
                root.AddComponent(SceneControllers[sceneName]);

                EditorSceneManager.SaveScene(scene, path);
                created++;
            }
            return created;
        }

        /// <summary>
        /// 16 Oct 2026 (BALLER CITY): release builds create any scene that's missing (CityScene on a project set up
        /// before it existed) and put every scene in Build Settings. Existing scenes are never touched.
        /// </summary>
        public static void EnsureScenesForBuild()
        {
            bool missing = SceneNames.All.Any(n => !File.Exists(ScenePath(n)));
            if (missing)
            {
                string open = EditorSceneManager.GetActiveScene().path;
                int made = CreateScenes(overwrite: false);
                Debug.Log("[CallerRetroBall] Created " + made + " missing scene(s) for the build.");
                if (!string.IsNullOrEmpty(open) && File.Exists(open)) EditorSceneManager.OpenScene(open);
            }
            var listed = EditorBuildSettings.scenes.Where(x => x.enabled).Select(x => x.path).ToArray();
            if (!SceneNames.All.Select(ScenePath).SequenceEqual(listed)) ConfigureBuildSettings();
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = SceneNames.All
                .Select(n => new EditorBuildSettingsScene(ScenePath(n), true))
                .ToArray();
        }

        // ------------------------------------------------------------------ render pipeline

        private static string CheckRenderPipeline()
        {
            if (GraphicsSettings.defaultRenderPipeline != null)
                return "Render pipeline: " + GraphicsSettings.defaultRenderPipeline.name;

            Debug.LogWarning("[CallerRetroBall] No render pipeline asset assigned. The game runs, but to use URP 2D: " +
                             "Assets ▸ Create ▸ Rendering ▸ URP Asset (with 2D Renderer), save it in Assets/Settings, " +
                             "then assign it in Project Settings ▸ Graphics and Project Settings ▸ Quality.");
            return "Render pipeline: NOT ASSIGNED (manual 2-step URP 2D setup, see README)";
        }
    }
}
