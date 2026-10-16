using System;
using System.IO;
using System.Linq;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CallerRetroBall.EditorTools
{
    /// <summary>
    /// Release helpers: writes the procedurally drawn app icon and launch image into the
    /// project and assigns them, applies release Player Settings, and builds the Xcode project
    /// for the iOS Simulator or a device. Every build step can also run from the command line:
    ///   Unity -batchmode -quit -projectPath . -executeMethod CallerRetroBall.EditorTools.ReleaseTools.BuildSimulator
    /// </summary>
    public static class ReleaseTools
    {
        public const string ArtFolder = "Assets/Art/AppIcon";
        public const string IconPath = ArtFolder + "/AppIcon1024.png";
        public const string LaunchPath = ArtFolder + "/LaunchImage.png";
        public const string SimulatorOutput = "iOSBuild/Simulator";
        public const string DeviceOutput = "iOSBuild/Device";
        public const string AppStoreOutput = "iOSBuild/AppStore";
        public const string ReleaseVersion = "1.0.0";

        [MenuItem("Retro Hoops/Release/Generate App Icon and Launch Image", priority = 60)]
        public static void GenerateIconAndLaunchMenu()
        {
            string report = GenerateIconAndLaunch();
            EditorUtility.DisplayDialog("Retro Hoops", report, "OK");
        }

        [MenuItem("Retro Hoops/Release/Apply Release Player Settings", priority = 61)]
        public static void ApplyReleaseSettingsMenu()
        {
            EditorUtility.DisplayDialog("Retro Hoops", ApplyReleaseSettings(), "OK");
        }

        [MenuItem("Retro Hoops/Release/Build iOS (Simulator)", priority = 80)]
        public static void BuildSimulator() => Build(iOSSdkVersion.SimulatorSDK, SimulatorOutput);

        [MenuItem("Retro Hoops/Release/Build iOS (Device)", priority = 81)]
        public static void BuildDevice()
        {
            ApplyTeamFromCommandLine();
            EnsureBundleId();
            Build(iOSSdkVersion.DeviceSDK, DeviceOutput);
        }

        /// <summary>
        /// Release build for App Store Connect / TestFlight: raises the build number by one (every upload
        /// needs a new one), Release configuration, writes iOSBuild/AppStore. tools/ship_testflight.sh then
        /// archives and uploads it with xcodebuild. Pass -teamId XXXXXXXXXX on the command line to set the team.
        /// </summary>
        [MenuItem("Retro Hoops/Release/Build iOS (App Store)", priority = 82)]
        public static void BuildAppStore()
        {
            ApplyTeamFromCommandLine();
            EnsureBundleId();
            int build = int.TryParse(PlayerSettings.iOS.buildNumber, out int b) && b >= 1 ? b + 1 : 1;
            PlayerSettings.iOS.buildNumber = build.ToString();
            EditorUserBuildSettings.iOSXcodeBuildConfig = XcodeBuildConfig.Release;
            AssetDatabase.SaveAssets();
            WriteReport("APP STORE BUILD: " + PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS) + " version " + ReleaseVersion + " build " + build +
                        (string.IsNullOrEmpty(PlayerSettings.iOS.appleDeveloperTeamID) ? "" : ", team " + PlayerSettings.iOS.appleDeveloperTeamID));
            Build(iOSSdkVersion.DeviceSDK, AppStoreOutput);
        }

        /// <summary>-teamId XXXXXXXXXX on the Unity command line sets Player Settings ▸ iOS ▸ Signing Team ID.</summary>
        private static void ApplyTeamFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-teamId" && IsTeamId(args[i + 1]))
                    PlayerSettings.iOS.appleDeveloperTeamID = args[i + 1];
        }

        /// <summary>Apple team ids are ten upper-case letters and digits.</summary>
        public static bool IsTeamId(string s) =>
            !string.IsNullOrEmpty(s) && s.Length == 10 && s.All(ch => (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9'));

        // ------------------------------------------------------------------ readiness

        [MenuItem("Retro Hoops/Release/Check iOS Readiness", priority = 59)]
        public static void CheckReadinessMenu() => CheckReadiness(offerFix: true);

        private static void CheckReadiness(bool offerFix)
        {
            var (report, ready) = CheckIosReadiness();
            Debug.Log("[Retro Hoops] iOS readiness:\n" + report);
            WriteReport("iOS readiness (" + (ready ? "READY" : "NOT READY") + ")\n" + report);
            // Fix once (icon, launch image, release settings incl. bundle ID), then show the result with just OK,
            // so things only you can do (installing a module, restarting Unity) don't loop the dialog.
            if (offerFix && EditorUtility.DisplayDialog("Retro Hoops: iOS readiness",
                    report + "\n\nFix what can be fixed automatically (icon, launch image, release settings, bundle ID)?", "Fix", "Close"))
            {
                GenerateIconAndLaunch();
                ApplyReleaseSettings();
                CheckReadiness(offerFix: false);
            }
            else if (!offerFix)
            {
                EditorUtility.DisplayDialog("Retro Hoops: iOS readiness", report, "OK");
            }
        }

        /// <summary>Checks everything an iOS build needs that can be checked from the Editor.</summary>
        public static (string report, bool ready) CheckIosReadiness()
        {
            var lines = new System.Collections.Generic.List<string>();
            bool ready = true;
            void Check(bool ok, string good, string bad, bool blocking = true)
            {
                lines.Add((ok ? "✓ " : (blocking ? "✗ " : "! ")) + (ok ? good : bad));
                if (!ok && blocking) ready = false;
            }

            Check(BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS),
                  "iOS Build Support module installed",
                  "iOS Build Support is not installed (or Unity hasn't been restarted since). Unity Hub ▸ Installs ▸ ⚙ ▸ Add modules ▸ iOS Build Support, then quit and reopen Unity");
            Check(Application.platform == RuntimePlatform.OSXEditor, "Running on a Mac", "iOS builds need a Mac with Xcode");
            Check(Directory.Exists("/Applications/Xcode.app"), "Xcode found in /Applications",
                  "Xcode not found in /Applications. Install it from the Mac App Store", blocking: false);

            var scenes = EditorBuildSettings.scenes.Where(x => x.enabled).Select(x => x.path).ToList();
            Check(scenes.Count >= 6 && scenes[0].EndsWith("BootScene.unity", StringComparison.Ordinal),
                  "Scenes in Build Settings (" + scenes.Count + ", BootScene first)",
                  "Scenes missing from Build Settings. Run Retro Hoops ▸ Run Project Setup");

            string id = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);
            bool placeholder = IsPlaceholderId(id);
            Check(!placeholder, "Bundle ID: " + id,
                  "Bundle ID is a placeholder (" + id + "). Fix sets " + DefaultBundleId + " (or set your own in Player Settings ▸ iOS ▸ Bundle Identifier)",
                  blocking: false);
            Check(!string.IsNullOrEmpty(PlayerSettings.iOS.appleDeveloperTeamID), "Apple Team ID set",
                  "No Apple Team ID yet. Fine for the Simulator; for your iPhone pick your team in Xcode ▸ Signing & Capabilities",
                  blocking: false);
            Check(PlayerSettings.productName == DefaultContent.GameName, "App name: " + PlayerSettings.productName,
                  "App name is \"" + PlayerSettings.productName + "\". Apply Release Player Settings sets it to Retro Hoops");
            var icons = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
            Check(icons != null && icons.Length > 0 && icons[0] != null, "App icon assigned",
                  "No app icon. Use Generate App Icon and Launch Image");
            Check(PlayerSettings.iOS.targetOSVersionString == "15.0" || string.CompareOrdinal(PlayerSettings.iOS.targetOSVersionString, "15.0") >= 0,
                  "Minimum iOS " + PlayerSettings.iOS.targetOSVersionString, "Minimum iOS is below 15.0", blocking: false);

            lines.Add("");
            lines.Add(ready
                ? "Ready. Use Retro Hoops ▸ Release ▸ Build iOS (Simulator), then open iOSBuild/Simulator/Unity-iPhone.xcodeproj in Xcode."
                : "Not ready yet. Fix the ✗ items first (! items are advice).");
            return (string.Join("\n", lines), ready);
        }

        // ------------------------------------------------------------------ icon + launch image

        public static string GenerateIconAndLaunch()
        {
            Directory.CreateDirectory(ArtFolder);
            WritePng(AppIconGenerator.StoreIcon(), IconPath);

            var court = DefaultContent.Create().Court("court.sunset_cage");
            if (court == null) throw new InvalidOperationException("Default content is missing court.sunset_cage.");
            // Same court and seed as the main menu backdrop, scaled 6x to 1080x1920.
            WritePng(AppIconGenerator.Scale(AppIconGenerator.LaunchImage(court, 7), 6), LaunchPath);

            AssetDatabase.Refresh();
            ConfigureImporter(IconPath);
            ConfigureImporter(LaunchPath);

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            var launch = AssetDatabase.LoadAssetAtPath<Texture2D>(LaunchPath);

            // The default icon is used for every iOS size; Xcode gets a single 1024 source.
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

            PlayerSettings.iOS.SetiPhoneLaunchScreenType(iOSLaunchScreenType.ImageAndBackgroundRelative);
            PlayerSettings.iOS.SetLaunchScreenImage(launch, iOSLaunchScreenImageType.iPhonePortraitImage);
            PlayerSettings.iOS.SetiPadLaunchScreenType(iOSLaunchScreenType.ImageAndBackgroundRelative);
            PlayerSettings.iOS.SetLaunchScreenImage(launch, iOSLaunchScreenImageType.iPadImage);
            PlayerSettings.SplashScreen.backgroundColor = new Color32(0x1A, 0x1A, 0x2E, 255);

            AssetDatabase.SaveAssets();
            return "App icon: " + IconPath + " (1024x1024, opaque)\nLaunch image: " + LaunchPath +
                   " (1080x1920)\nBoth assigned in Player Settings. Check them in Player Settings ▸ iOS ▸ Icon and Splash Image.";
        }

        private static void WritePng(PixelCanvas canvas, string path)
        {
            // Readable texture (TextureFactory frees the CPU copy, which EncodeToPNG needs).
            var tex = new Texture2D(canvas.Width, canvas.Height, TextureFormat.RGBA32, false) { name = Path.GetFileNameWithoutExtension(path) };
            var pixels = new Color32[canvas.Pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                var p = canvas.Pixels[i];
                pixels[i] = new Color32(p.r, p.g, p.b, 255);
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            try
            {
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        private static void ConfigureImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        // ------------------------------------------------------------------ player settings

        /// <summary>
        /// The App Store bundle ID, registered to the paid Phoronomic Studios team. (The first one,
        /// com.marcompsci.retroball, belongs to the free personal team, so App Store Connect can't use it.)
        /// </summary>
        public const string DefaultBundleId = "com.phoronomicstudios.retrohoops";
        /// <summary>The paid-team ID used before the rename to Retro Hoops (never uploaded).</summary>
        public const string OldRetroBallBundleId = "com.phoronomicstudios.retroball";
        public const string OldPersonalBundleId = "com.marcompsci.retroball";

        public static bool IsPlaceholderId(string id) =>
            string.IsNullOrEmpty(id) || id == "com.retroball.game" || id == OldPersonalBundleId || id == OldRetroBallBundleId || id == "com.marcompsci.retrohoops" || id.StartsWith("com.Unity", StringComparison.Ordinal)
            || id.StartsWith("com.DefaultCompany", StringComparison.Ordinal);

        /// <summary>
        /// Portrait and both landscapes are allowed in Info.plist (Full Court 5-on-5 turns the screen);
        /// the game itself locks portrait at boot and only goes landscape for Full Court.
        /// </summary>
        public static void EnsureOrientations()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
        }

        /// <summary>Moves the project off a placeholder (or the old personal-team) bundle ID.</summary>
        private static void EnsureBundleId()
        {
            if (IsPlaceholderId(PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS)))
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, DefaultBundleId);
        }

        public static string ApplyReleaseSettings()
        {
            PlayerSettings.productName = DefaultContent.GameName;
            PlayerSettings.bundleVersion = ReleaseVersion;
            if (!int.TryParse(PlayerSettings.iOS.buildNumber, out int build) || build < 1) PlayerSettings.iOS.buildNumber = "1";
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            // iPhone and iPad (portrait, full screen). Apple Silicon Macs run the iPad build as
            // "Designed for iPad"; the camera and UI adapt to the wider screens, keyboard and controllers work.
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.statusBarHidden = true;
            EnsureOrientations();
            // Unity 6 lets every licence turn off the "Made with Unity" splash.
            PlayerSettings.SplashScreen.show = false;
            // Release builds strip the dev-only cheats and checks (they're behind UNITY_EDITOR || DEBUG).
            EditorUserBuildSettings.development = false;
            // Replace a template placeholder bundle ID with a real one (change it if you prefer another).
            string current = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);
            if (IsPlaceholderId(current)) PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, DefaultBundleId);
            AssetDatabase.SaveAssets();

            string id = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);
            return "Version " + PlayerSettings.bundleVersion + " (" + PlayerSettings.iOS.buildNumber + "), iOS 15+, iPhone + iPad (+ Mac as Designed for iPad), portrait (landscape for Full Court), full screen, no splash.\n" +
                   "Bundle ID: " + id + (IsPlaceholderId(id)
                       ? "  ← placeholder: change it to your own (e.g. com.yourname.retroball) before signing." : "");
        }

        // ------------------------------------------------------------------ builds

        /// <summary>
        /// Smaller download: strip unused engine modules and unused .NET library code (Assets/link.xml keeps
        /// all of Retro Hoops' own code), and let IL2CPP share generic code. The match simulation costs about
        /// 10 µs a step, so the size-optimised code generation has no visible cost.
        /// </summary>
        public static void EnsureSizeSettings()
        {
            PlayerSettings.stripEngineCode = true;
            if (PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.iOS) < ManagedStrippingLevel.Low)
                PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.Low);
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.iOS, Il2CppCodeGeneration.OptimizeSize);
        }

        /// <summary>11 Oct 2026: the iOS build number goes into Resources/build_number.txt so Settings can show "v1.0 (build 9)".</summary>
        private static void WriteBuildNumber()
        {
            const string path = "Assets/Resources/build_number.txt";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, PlayerSettings.iOS.buildNumber ?? "");
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        private static void Build(iOSSdkVersion sdk, string output)
        {
            EnsureOrientations();
            EnsureSizeSettings();
            // Phase 34: StoreKit 2 (RetroStore.swift) and the Keychain/GameKit code need iOS 15. Never build below it,
            // even if the project was set up without ProjectSetup / Apply Release Settings.
            if (!System.Version.TryParse(PlayerSettings.iOS.targetOSVersionString, out var minOs) || minOs < new System.Version(15, 0))
            {
                Debug.Log("[ReleaseTools] Minimum iOS raised from " + PlayerSettings.iOS.targetOSVersionString + " to 15.0");
                PlayerSettings.iOS.targetOSVersionString = "15.0";
            }
            ProjectSetup.EnsureScenesForBuild();
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Fail("No scenes in Build Settings. Run 'Retro Hoops ▸ Run Project Setup' first.");
                return;
            }

            var validation = ContentValidator.Validate(DefaultContent.Create());
            if (!validation.IsValid)
            {
                Fail("Content validation failed:\n" + validation);
                return;
            }

            if (!File.Exists(IconPath) || !File.Exists(LaunchPath)) GenerateIconAndLaunch();
            ApplyReleaseSettings();
            WriteBuildNumber();
            PlayerSettings.iOS.sdkVersion = sdk;

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                var errors = report.steps.SelectMany(st => st.messages)
                    .Where(m => m.type == LogType.Error || m.type == LogType.Exception)
                    .Select(m => "  - " + m.content.Split('\n')[0]).Distinct().Take(12).ToArray();
                Fail("iOS build " + summary.result + " with " + summary.totalErrors + " error(s). See the Console." +
                     (errors.Length > 0 ? "\n" + string.Join("\n", errors) : ""));
                return;
            }

            string msg = "Xcode project written to " + Path.GetFullPath(output) + " (" + (summary.totalSize / (1024 * 1024)) + " MB).\n" +
                         "Open Unity-iPhone.xcodeproj, choose your team under Signing & Capabilities, pick " +
                         (sdk == iOSSdkVersion.SimulatorSDK ? "an iPhone simulator" : "your iPhone") + ", and press Run.";
            Debug.Log("[CallerRetroBall] " + msg);
            WriteReport("BUILD OK\n" + msg);
            if (!Application.isBatchMode) EditorUtility.DisplayDialog("Retro Hoops", msg, "OK");
        }

        /// <summary>
        /// Appends a line to Logs/RetroHoops-release.txt in the project, so readiness checks and build
        /// results can be read later (and shared) without copying them out of a dialog.
        /// </summary>
        public static void WriteReport(string text)
        {
            try
            {
                Directory.CreateDirectory("Logs");
                File.AppendAllText(Path.Combine("Logs", "RetroHoops-release.txt"),
                    "=== " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  Unity " + Application.unityVersion + "\n" + text + "\n\n");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[CallerRetroBall] Could not write the release report: " + e.Message);
            }
        }

        private static void Fail(string message)
        {
            Debug.LogError("[CallerRetroBall] " + message);
            WriteReport("FAILED\n" + message);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            else EditorUtility.DisplayDialog("Retro Hoops", message, "OK");
        }
    }
}
