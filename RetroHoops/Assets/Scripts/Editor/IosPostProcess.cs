#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace CallerRetroBall.EditorTools
{
    /// <summary>
    /// After an iOS build, sets the Info.plist keys App Store Connect would otherwise ask about
    /// (no non-exempt encryption, full screen, hidden status bar, Sports Games category), links
    /// GameKit and MultipeerConnectivity (two-phone play, with the local-network keys), and adds the Game Center and iCloud (key-value storage) capabilities.
    /// </summary>
    public static class IosPostProcess
    {
        [PostProcessBuild(100)]
        public static void OnPostProcessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            string plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            var root = plist.root;
            root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            root.SetBoolean("UIRequiresFullScreen", true);
            root.SetBoolean("UIStatusBarHidden", true);
            root.SetBoolean("UIViewControllerBasedStatusBarAppearance", false);
            // Lets ProMotion iPhones run the game at 120 Hz (Settings ▸ HIGH FRAME RATE).
            root.SetBoolean("CADisableMinimumFrameDurationOnPhone", true);
            root.SetString("LSApplicationCategoryType", "public.app-category.sports-games");
            // The share sheet's "Save Image" writes highlight GIFs to Photos (add-only access).
            root.SetString("NSPhotoLibraryAddUsageDescription", "Save your Retro Hoops highlights to Photos.");
            // The in-game phone's MY MIX plays songs the player picks from their own Apple Music (MusicKit). 16 Oct 2026
            // compliance gate: the RETRO HOOPS playlist is the game's own offline beats now, so the reason says MY MIX.
            root.SetString("NSAppleMusicUsageDescription", "Play songs you pick from your Apple Music in the game's MY MIX while you play.");
            // Bluetooth / MFi controllers (Xbox, PlayStation, Switch Pro and similar extended gamepads).
            root.SetBoolean("GCSupportsControllerUserInteraction", true);
            // retrohoops://kit/... (and older retroball://kit/...) links (kit QR codes) open the app straight into the Kit Studio.
            var urlTypes = root["CFBundleURLTypes"] as PlistElementArray ?? root.CreateArray("CFBundleURLTypes");
            bool hasScheme = false;
            foreach (var t in urlTypes.values)
                if (t is PlistElementDict d && d["CFBundleURLSchemes"] is PlistElementArray schemes)
                    foreach (var sch in schemes.values)
                        if (sch is PlistElementString str && str.value == "retrohoops") hasScheme = true;
            if (!hasScheme)
            {
                var type = urlTypes.AddDict();
                type.SetString("CFBundleURLName", "com.phoronomicstudios.retrohoops.kit");
                var schemes = type.CreateArray("CFBundleURLSchemes");
                schemes.AddString("retrohoops");
                schemes.AddString("retroball"); // links shared before the rename
            }
            // Two-phone play (2 PLAYER ▸ TWO PHONES): nearby discovery over Wi-Fi / Bluetooth, no internet.
            root.SetString("NSLocalNetworkUsageDescription", "Find a friend's iPhone or iPad nearby to play Retro Hoops head to head.");
            var bonjour = root["NSBonjourServices"] as PlistElementArray ?? root.CreateArray("NSBonjourServices");
            foreach (var service in new[] { "_retrohoops._tcp", "_retrohoops._udp" })
            {
                bool present = false;
                foreach (var v in bonjour.values) if (v is PlistElementString str && str.value == service) present = true;
                if (!present) bonjour.AddString(service);
            }
            // App Transport Security stays fully on (every connection is HTTPS). 16 Oct 2026 compliance gate: if anything
            // ever adds an "allow arbitrary loads" exception, take it back out rather than ship it.
            if (root["NSAppTransportSecurity"] is PlistElementDict ats)
            {
                foreach (var key in new[] { "NSAllowsArbitraryLoads", "NSAllowsArbitraryLoadsInWebContent", "NSAllowsArbitraryLoadsForMedia", "NSAllowsLocalNetworking" })
                    if (ats[key] != null) ats.values.Remove(key);
                if (ats.values.Count == 0) root.values.Remove("NSAppTransportSecurity");
            }
            if (root["GCSupportedGameControllers"] == null)
                root.CreateArray("GCSupportedGameControllers").AddDict().SetString("ProfileName", "ExtendedGamepad");
            plist.WriteToFile(plistPath);

            // Game Center: link GameKit and add the capability (sign-in stays opt-in inside the game).
            string projPath = PBXProject.GetPBXProjectPath(path);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);
            proj.AddFrameworkToProject(proj.GetUnityFrameworkTargetGuid(), "GameKit.framework", false);
            proj.AddFrameworkToProject(proj.GetUnityFrameworkTargetGuid(), "MultipeerConnectivity.framework", false);
            // StoreKit 2 (RetroStore.swift) for the Retro Hoops Pro unlock (Phase 41), GameKit matchmaking for Live (RetroLive.mm).
            string fw = proj.GetUnityFrameworkTargetGuid();
            proj.AddFrameworkToProject(fw, "StoreKit.framework", false);
            proj.AddFrameworkToProject(fw, "Security.framework", false); // RetroKeychain.mm (the save file's key)
            if (string.IsNullOrEmpty(proj.GetBuildPropertyForAnyConfig(fw, "SWIFT_VERSION"))) proj.SetBuildProperty(fw, "SWIFT_VERSION", "5.0");
            proj.SetBuildProperty(fw, "CLANG_ENABLE_MODULES", "YES");
            // Swift in UnityFramework: allow Unity's own non-modular headers inside the framework module.
            proj.SetBuildProperty(fw, "CLANG_ALLOW_NON_MODULAR_INCLUDES_IN_FRAMEWORK_MODULES", "YES");
            // Phase 34: compile our Objective-C++ plugins with ARC (Unity's project doesn't turn it on). They were
            // written to be correct either way; ARC stops the small leaks they'd have under manual retain/release.
            foreach (var plugin in new[] { "RetroLink.mm", "RetroLive.mm", "RetroKeychain.mm", "RetroCloud.mm", "RetroPower.mm", "RetroShare.mm", "CallerGameCenter.mm", "CallerHaptics.mm" })
            {
                string guid = proj.FindFileGuidByProjectPath("Libraries/Plugins/iOS/" + plugin);
                if (string.IsNullOrEmpty(guid)) continue;
                var flags = proj.GetCompileFlagsForFile(fw, guid) ?? new System.Collections.Generic.List<string>();
                if (!flags.Contains("-fobjc-arc")) flags.Add("-fobjc-arc");
                proj.SetCompileFlagsForFile(fw, guid, flags);
            }
            proj.WriteToFile(projPath);
            var caps = new ProjectCapabilityManager(projPath, "Unity-iPhone/RetroHoops.entitlements", null, proj.GetUnityMainTargetGuid());
            caps.AddGameCenter();
            caps.AddInAppPurchase();
            // iCloud key-value storage for save sync (Settings ▸ ICLOUD SYNC). No documents, no CloudKit.
            caps.AddiCloud(true, false, false, false, new string[0]);
            caps.WriteToFile();
        }
    }
}
#endif
