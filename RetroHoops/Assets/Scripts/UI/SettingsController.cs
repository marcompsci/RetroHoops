using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using TMPro;
using UnityEngine;

namespace CallerRetroBall.UI
{
    /// <summary>
    /// SettingsScene: volumes, haptics, screen shake, UI scale, colourblind team contrast,
    /// difficulty, reset save (with confirmation), and credits / licences. Every change is
    /// saved immediately to the local career file.
    /// </summary>
    public sealed class SettingsController : ScreenBase
    {
        protected override string ScreenTitle => "SETTINGS";
        protected override string BackdropCourtId => "court.overpass_park";
        protected override float ScrimAlpha => 0.75f;

        public const string LicenseText =
            "<color=#FFD166>CREDITS & LICENCES</color>\n" +
            "Retro Hoops is an original game. All teams, players, leagues, courts, logos, " +
            "and pixel art are original and generated procedurally inside this project.\n\n" +
            "Text font: Liberation Sans, bundled with Unity TextMeshPro (SIL Open Font License 1.1).\n" +
            "Audio: every sound effect and all nine music tracks are synthesised in code at runtime (no recordings or samples).\n" +
            "Haptics: a small original iOS plugin using Apple's UIKit feedback generators.\n\n" +
            "No ads, analytics, tracking, accounts, or purchases. Progress is stored only on this device. " +
            "Game Center is optional and off unless you turn it on.";

        private static readonly float[] UiScales = { 0.85f, 1f, 1.15f, 1.25f };
        private static readonly string[] UiScaleNames = { "SMALL", "DEFAULT", "LARGE", "LARGEST" };
        private static readonly string[] CrtNames = { "OFF", "SOFT", "STRONG" };

        protected override void Build()
        {
            var column = UiKit.ScrollColumn(Body, 14f, new RectOffset(48, 48, 12, 60));
            var c = App.Catalog;
            var s = App.Career.settings;

            Header(column, "AUDIO");
            UiControls.SliderRow(column, "MUSIC", s.musicVolume, v => { s.musicVolume = v; App.ApplySettings(); });
            UiControls.SliderRow(column, "SFX", s.sfxVolume, v => { s.sfxVolume = v; App.ApplySettings(); });
            UiKit.Button(column, "MUSIC PLAYER", () => MusicPlayer.Open(), ButtonStyle.Secondary, 100f, 36f);
            // 15 Oct 2026 (Omari): your own background music — MY MIX from your Apple Music songs.
            UiKit.Button(column, "MY MUSIC (APPLE MUSIC)", () => PhoneScreen.Open(PhoneScreen.App.Music), ButtonStyle.Secondary, 100f, 36f);

            Header(column, "FEEL");
            UiControls.ToggleRow(column, "HAPTICS", s.haptics, v => { s.haptics = v; Save(); if (v) Haptics.Light(); });
            UiControls.ToggleRow(column, "SCREEN SHAKE", s.screenShake, v => { s.screenShake = v; Save(); });

            Header(column, "DISPLAY");
            UiControls.ChoiceRow(column, "CRT FILTER", CrtNames, Mathf.Clamp(s.crt, 0, 2), i =>
            {
                s.crt = i;
                CrtOverlay.Apply(i);
                Save();
            });
            UiControls.ToggleRow(column, "HIGH FRAME RATE", s.highFrameRate, v => { s.highFrameRate = v; App.ApplyFrameRate(); Save(); });
            UiControls.ToggleRow(column, "TITLE DEMO", s.attractMode, v => { s.attractMode = v; Save(); });
            UiControls.ToggleRow(column, "SHOW FPS", s.showFps, v => { s.showFps = v; Save(); });
            UiControls.ToggleRow(column, "COACH TIPS", s.coachTips, v => { s.coachTips = v; Save(); });
            UiKit.Size(UiKit.Label(column,
                "CRT adds old-TV scanlines. High frame rate runs at 120 Hz on ProMotion iPhones (uses more battery). " +
                "Title demo plays an AI game on the title screen when it's left alone.", 28f, Theme.Muted), 110f);

            Header(column, "SECRETS");
            var sec = App.Career.secrets;
            UiKit.Button(column, "ENTER A CODE", () => SecretCodeScreen.Open(changed =>
            {
                if (changed) SceneFlow.GoTo(SceneNames.Settings); // show newly found toggles
            }), ButtonStyle.Secondary, 110f, 40f);
            foreach (var code in Secrets.All)
            {
                if (!Secrets.Found(sec, code.Id)) continue;
                var id = code.Id;
                if (id == Secrets.BigHeads)
                    UiControls.ToggleRow(column, "BIG HEADS", sec.bigHeads, v => { sec.bigHeads = v; Save(); });
                else if (id == Secrets.RainbowBall)
                    UiControls.ToggleRow(column, "RAINBOW BALL", sec.rainbowBall, v => { sec.rainbowBall = v; Save(); });
                else if (id == Secrets.AlwaysHeat)
                    UiControls.ToggleRow(column, "ALWAYS HOT", sec.alwaysHeat, v => { sec.alwaysHeat = v; Save(); });
                else if (id == Secrets.PocketGreen)
                    UiControls.ToggleRow(column, "POCKET GREEN", sec.pocketGreen, v => { sec.pocketGreen = v; CrtOverlay.ApplyTint(v); Save(); });
                else if (id == Secrets.SkyHigh)
                    UiControls.ToggleRow(column, "SKY HIGH", sec.skyHigh, v => { sec.skyHigh = v; Save(); });
            }
            UiKit.Size(UiKit.Label(column, Loc.T("Codes found:") + " " + sec.codesFound.Count + " / " + Secrets.All.Count, 28f, Theme.Muted), 50f);

            Header(column, "ACCESSIBILITY");
            int scaleIndex = 1;
            for (int i = 0; i < UiScales.Length; i++) if (Mathf.Abs(UiScales[i] - s.uiScale) < 0.01f) scaleIndex = i;
            UiControls.ChoiceRow(column, "UI SCALE", UiScaleNames, scaleIndex, i =>
            {
                s.uiScale = UiScales[i];
                Save();
                SceneFlow.GoTo(SceneNames.Settings); // rebuild canvases at the new scale
            });
            UiControls.ToggleRow(column, "MATCH IPHONE TEXT SIZE", s.followSystemText, v =>
            {
                s.followSystemText = v;
                Save();
                SceneFlow.GoTo(SceneNames.Settings);
            });
            UiKit.Size(UiKit.Label(column, "With Larger Text on in iOS Settings, menus grow to match (up to the biggest UI SCALE). VoiceOver reads the menus' buttons and titles.", 28f, Theme.Muted), 90f);
            UiControls.ToggleRow(column, "TEAM PATTERNS", s.colorblindContrast, v => { s.colorblindContrast = v; Save(); });
            UiKit.Size(UiKit.Label(column, "Team patterns give each side a distinct jersey pattern, not just a colour.", 28f, Theme.Muted), 70f);
            UiControls.ChoiceRow(column, "COLOR FILTER", ColorAccess.FilterNames, (int)ColorAccess.Normalize(s.colorFilter), i => { s.colorFilter = i; Save(); });
            UiKit.Size(UiKit.Label(column, "Color filter changes the shot meter colors and gives the away team its other kit when the two teams would look alike.", 28f, Theme.Muted), 90f);
            UiControls.ToggleRow(column, "CAPTIONS", s.captions, v => { s.captions = v; Save(); });
            UiKit.Size(UiKit.Label(column, "Captions show the announcer's calls on screen. They're also on when Closed Captions is on in iOS Settings. VoiceOver reads the menus.", 28f, Theme.Muted), 90f);
            UiControls.ToggleRow(column, "LEFT-HANDED", s.leftHanded, v => { s.leftHanded = v; Save(); });
            UiControls.ToggleRow(column, "LARGE BUTTONS", s.largeButtons, v => { s.largeButtons = v; Save(); });
            // Phase 39: how the stick and haptics feel.
            UiControls.ChoiceRow(column, "STICK SIZE", ControlFeel.SizeNames, s.stickSize, i => { s.stickSize = i; Save(); });
            UiControls.ChoiceRow(column, "BUTTON OPACITY", ControlFeel.OpacityNames, s.controlsOpacity, i => { s.controlsOpacity = i; Save(); });
            UiControls.ToggleRow(column, "FILL SCREEN WITH COURT", s.fillScreen, v => { s.fillScreen = v; Save(); });
            UiControls.ChoiceRow(column, "STICK DEAD ZONE", ControlFeel.DeadZoneNames, s.stickDeadZone, i => { s.stickDeadZone = i; Save(); });
            UiControls.ChoiceRow(column, "HAPTICS STRENGTH", ControlFeel.HapticNames, s.hapticStrength, i => { s.hapticStrength = i; Save(); Haptics.Medium(); });
            UiControls.ToggleRow(column, "SMART DIFFICULTY (RISE)", s.smartDifficulty, v => { s.smartDifficulty = v; Save(); });
            UiKit.Size(UiKit.Label(column, "A larger stick needs a longer push for full speed. A higher dead zone ignores small thumb drift. Strength changes how firm the buzz is.", 28f, Theme.Muted), 90f);
            UiKit.Button(column, "CUSTOMIZE CONTROLS", () => Controls.ControlEditor.Open(), ButtonStyle.Secondary, 100f, 36f);
            UiControls.ToggleRow(column, "LANDSCAPE: ALL GAMES", s.landscapeAll, v => { s.landscapeAll = v; Save(); });
            UiControls.ToggleRow(column, "COMMENTARY (MIC TALLY)", s.commentary, v => { s.commentary = v; Save(); });
            UiControls.ToggleRow(column, "LIVE STAT LINE", s.liveStatLine, v => { s.liveStatLine = v; Save(); });
            UiControls.ToggleRow(column, "FORGIVING METER", s.forgivingMeter, v => { s.forgivingMeter = v; Save(); });
            UiControls.ToggleRow(column, "PLAY-BY-PLAY VOICE (THE STREETS)", s.playByPlayVoice, v => { s.playByPlayVoice = v; Save(); if (!v) Core.Speech.Stop(); });
            UiKit.Size(UiKit.Label(column, "Full Court always plays sideways. Turn this on to play every mode sideways (2 Player stays upright).", 28f, Theme.Muted), 80f);
            UiControls.ToggleRow(column, "TAP TO SHOOT", s.tapToShoot, v => { s.tapToShoot = v; Save(); });
            UiControls.ToggleRow(column, "REDUCE MOTION", s.reduceMotion, v => { s.reduceMotion = v; Save(); });
            UiKit.Size(UiKit.Label(column,
                "Left-handed puts the stick on the right and buttons on the left. Tap to shoot: tap once to start the meter, tap again to release. " +
                "Reduce motion turns off screen shake, sparks, the score bounce, and crowd bobbing.", 28f, Theme.Muted), 150f);

            Header(column, "LANGUAGE");
            int li = System.Array.IndexOf(Loc.Languages, Loc.Normalize(s.language));
            UiControls.ChoiceRow(column, "LANGUAGE", Loc.LanguageNames, Mathf.Max(0, li), i =>
            {
                s.language = Loc.Languages[i];
                App.ApplySettings();
                Save();
                SceneFlow.GoTo(SceneNames.Settings); // rebuild in the new language
            });

            Header(column, "GAME");
            var diffs = CityCrews.Pickable(c);
            int di = Mathf.Max(0, diffs.FindIndex(d => d.id == s.difficultyId));
            UiControls.ChoiceRow(column, "DIFFICULTY", diffs.ConvertAll(d => d.displayName.ToUpperInvariant()).ToArray(), di, i =>
            {
                s.difficultyId = diffs[i].id;
                Save();
            });
            UiKit.Size(UiKit.Label(column, "Difficulty changes how fast and how well the AI decides. It never boosts their ratings.", 28f, Theme.Muted), 70f);
            // Phase 34: DIFFICULTY BY MODE (DEFAULT = the DIFFICULTY above).
            var names = new System.Collections.Generic.List<string> { "DEFAULT" };
            names.AddRange(diffs.ConvertAll(d => d.displayName.ToUpperInvariant()));
            foreach (var group in ModeDifficulty.Groups)
            {
                string g = group;
                string current = ModeDifficulty.Get(s, g);
                int idx = current == null ? 0 : Mathf.Max(0, diffs.FindIndex(d => d.id == current) + 1);
                UiControls.ChoiceRow(column, g, names.ToArray(), idx, i =>
                {
                    ModeDifficulty.Set(s, g, i == 0 ? null : diffs[i - 1].id);
                    Save();
                });
            }

            UiKit.Button(column, "HOW TO PLAY", MainMenuController.StartTutorial, ButtonStyle.Secondary, 110f, 40f);
            // 14 Oct 2026: the skill-move list (secret moves stay ??? until you pull them off).
            UiKit.Button(column, "SKILL MOVES", () =>
                UiControls.InfoDialog("SKILL MOVES", SkillMoveInfo.ListText(App.Career)),
                ButtonStyle.Secondary, 110f, 40f);

            Header(column, "GAME CENTER");
            UiControls.ToggleRow(column, "SIGN IN", s.gameCenter, v => App.SetGameCenter(v));
            UiKit.Size(UiKit.Label(column, App.GameCenter.IsAvailable
                    ? "Optional. Posts your best marks to leaderboards and unlocks achievements. The game works the same without it."
                    : "Game Center is only available in the iPhone app.",
                28f, Theme.Muted), 90f);
            if (App.GameCenter.IsAvailable)
                UiKit.Button(column, "OPEN GAME CENTER", () =>
                {
                    App.ReportGameCenter();
                    App.GameCenter.ShowDashboard();
                }, ButtonStyle.Secondary, 110f, 40f);

            Header(column, "ICLOUD");
            UiControls.ToggleRow(column, "ICLOUD SYNC", s.icloudSync, v =>
            {
                s.icloudSync = v;
                Save();
            });
            UiKit.Size(UiKit.Label(column, !CloudSync.Supported
                    ? "iCloud sync is only available in the iPhone app."
                    : CloudSync.Available
                        ? "Keeps your career in your own iCloud, so a new iPhone picks up where you left off. Settings stay per device."
                        : "Sign in to iCloud in the iPhone Settings app to sync your career.",
                28f, Theme.Muted), 90f);

            UiKit.Button(column, "RESET SAVE", () =>
                UiControls.Dialog("RESET SAVE?",
                    "This deletes your career, Rise Mode progress, upgrades, and cosmetics on this device and in iCloud. It can't be undone.",
                    ("RESET", ButtonStyle.Primary, () =>
                    {
                        App.ResetCareer();
                        SceneFlow.GoTo(SceneNames.MainMenu);
                    }),
                    ("CANCEL", ButtonStyle.Ghost, null)),
                ButtonStyle.Ghost, 110f, 40f);

            // Phase 40: support and privacy pages (also the URLs given to App Store Connect).
            UiKit.Button(column, "HELP & SUPPORT", () => Application.OpenURL(LiveMode.SupportUrl), ButtonStyle.Secondary, 90f, 32f);
            UiKit.Button(column, "PRIVACY POLICY", () => Application.OpenURL(LiveMode.PrivacyPolicyUrl), ButtonStyle.Ghost, 80f, 26f);

#if UNITY_EDITOR || DEBUG
            UiKit.Button(column, "DEV: UNLOCK ALL", () => App.DevUnlockAll(), ButtonStyle.Ghost, 90f, 30f);
#endif

            var credits = UiKit.Label(column, LicenseText + "\n\n<color=#8D99AE>v" + App.VersionAndBuild + "</color>", 30f, Theme.Cream, TextAlignmentOptions.TopLeft);
            UiKit.Size(credits, 620f);
        }

        private static void Header(Transform parent, string text) =>
            UiKit.Size(UiKit.Label(parent, text, 38f, Theme.Gold, TextAlignmentOptions.Left, true), 64f);

        private static void Save() => App.SaveCareer();
    }
}
