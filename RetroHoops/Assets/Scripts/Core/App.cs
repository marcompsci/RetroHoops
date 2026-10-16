using CallerRetroBall.Audio;
using CallerRetroBall.Data;
using CallerRetroBall.UI;
using CallerRetroBall.Logic;
using CallerRetroBall.Utilities;
using UnityEngine;

namespace CallerRetroBall.Core
{
    /// <summary>
    /// Tiny service hub. Initialised by BootScene, but every scene controller calls
    /// <see cref="EnsureInitialized"/> too, so pressing Play on any scene in the
    /// Editor still works.
    /// </summary>
    public static class App
    {
        /// <summary>The Player Settings version (set by ProjectSetup / ReleaseTools), shown on menus.</summary>
        public static string Version => Application.version;

        private static string _build;

        /// <summary>The iOS build number written by ReleaseTools at build time (empty in the Editor before a build).</summary>
        public static string BuildNumber
        {
            get
            {
                if (_build == null)
                {
                    var asset = Resources.Load<TextAsset>("build_number");
                    _build = asset != null ? asset.text.Trim() : "";
                }
                return _build;
            }
        }

        /// <summary>"1.0 (build 9)", or just the version when no build number is known.</summary>
        public static string VersionAndBuild => string.IsNullOrEmpty(BuildNumber) ? Version : Version + " (" + Logic.Loc.T("build") + " " + BuildNumber + ")";

        public static ContentDatabase Content { get; private set; }

        /// <summary>The match the GameScene should play next (set by menus).</summary>
        public static MatchRequest PendingMatch { get; set; }

        public static bool IsInitialized => Content != null;

        public static ContentCatalog Catalog => Content?.Catalog;

        /// <summary>The local career (progress, settings). Loaded at boot; saved after games and changes.</summary>
        public static CareerSaveData Career { get; private set; }
        public static LoadStatus CareerLoadStatus { get; private set; }

        public static readonly RewardTuning Rewards = RewardTuning.Default;
        /// <summary>Game Center on iOS builds, a no-op elsewhere. Only used after the player opts in.</summary>
        public static readonly IGameCenterService GameCenter = GameCenterSync.Create();

        /// <summary>Settings ► Game Center toggle: signs in when turned on.</summary>
        public static void SetGameCenter(bool on)
        {
            if (Career == null) return;
            Career.settings.gameCenter = on;
            SaveCareer();
            if (on) GameCenter.Authenticate();
        }

        /// <summary>Pushes achievements and leaderboard scores (no-op unless opted in and signed in).</summary>
        public static void ReportGameCenter() => GameCenterSync.Report(GameCenter, Career);

        /// <summary>Today's local day number for the Daily Challenge.</summary>
        public static int Today => DailyChallenges.DayNumber(System.DateTime.Now);

        /// <summary>What the last Rise game changed (announced once by the Rise hub, then cleared).</summary>
        public static RiseOutcome LastRiseOutcome { get; set; }

        /// <summary>Phase 41: set by the Rise hub (season 4 needs Pro) so the main menu opens the Retro Hoops Pro screen.</summary>
        public static bool OpenProOnMenu { get; set; }

        /// <summary>Set after a First Call Classic game so the main menu reopens the bracket.</summary>
        public static bool OpenClassicOnMenu { get; set; }

        /// <summary>Set after a King of the Court game so the main menu reopens the run.</summary>
        public static bool OpenKingOnMenu { get; set; }

        /// <summary>Set after an Arcade Ladder game so the main menu reopens the ladder.</summary>
        public static bool OpenArcadeOnMenu { get; set; }

        /// <summary>A kit shared through a retrohoops://kit/ link, waiting for the Locker Room's Kit Studio.</summary>
        public static KitData PendingKit { get; set; }

        /// <summary>Opened from a retrohoops://kit/ link (a scanned kit QR code or a tapped link).</summary>
        private static void OnDeepLink(string url)
        {
            if (string.IsNullOrEmpty(url) || !Kits.TryDecode(url, out var kit)) return;
            PendingKit = kit;
            if (Career == null || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == SceneNames.LockerRoom) return;
            UiControls.Dialog("A FRIEND'S KIT", "Someone shared a Retro Hoops kit with you. Open it in the Kit Studio?",
                ("OPEN KIT STUDIO", ButtonStyle.Primary, () => SceneFlow.GoTo(SceneNames.LockerRoom)),
                ("LATER", ButtonStyle.Ghost, null));
        }

        /// <summary>Pass-and-play Shootout in progress (kept between player 1's and player 2's rounds).</summary>
        public static ShootoutDuel PendingDuel { get; set; }

        /// <summary>Set after a Caller Cup game so the main menu reopens the bracket.</summary>
        public static bool OpenCupOnMenu { get; set; }

        /// <summary>After a Franchise game: open the front office again on the menu.</summary>
        public static bool OpenFranchiseOnMenu { get; set; }

        /// <summary>Back from an All-Star event: open the All-Star screen again (menu or Rise hub).</summary>
        public static bool OpenAllStar { get; set; }

        /// <summary>After a Legacy game: open the Legacy screen again on the menu.</summary>
        public static bool OpenLegacyOnMenu { get; set; }

        /// <summary>After a street challenge or a Tournament Builder game: open those again on the menu.</summary>
        public static bool OpenParkOnMenu { get; set; }
        /// <summary>15 Oct 2026: back to THE STREETS after a game there.</summary>
        public static bool OpenStreetsOnMenu { get; set; }
        public static bool OpenCustomCupOnMenu { get; set; }
        /// <summary>Phase 37: after a CLUTCH scenario, open the scenario list again.</summary>
        public static bool OpenClutchOnMenu { get; set; }

        /// <summary>After a Summer Story game: open the story on the menu (and play chapter N's closing scene if it was just cleared).</summary>
        public static bool OpenStoryOnMenu { get; set; }
        public static int StoryOutroPending { get; set; }
        /// <summary>After a Couch Cup game: open the bracket on the menu.</summary>
        public static bool OpenCouchOnMenu { get; set; }
        /// <summary>Phase 34: back to the Skills Gauntlet screen after a station.</summary>
        public static bool OpenGauntletOnMenu { get; set; }
        /// <summary>After a Live game: open LIVE on the menu.</summary>
        public static bool OpenLiveOnMenu { get; set; }
        /// <summary>16 Oct 2026: a screen opened from BALLER CITY (the Rise hub, the Locker Room): Back returns to the city.</summary>
        public static bool BackToCity { get; set; }
        /// <summary>17 Oct 2026: THE STREETS opened from a graffiti wall in BALLER CITY: its BACK returns to the city.</summary>
        public static bool StreetsFromCity { get; set; }

        /// <summary>Simulation steps per second: 120 on 120 Hz screens with High Frame Rate on, else 60.</summary>
        public static int SimulationRate { get; private set; } = 60;

        /// <summary>Applies the frame-rate setting (ProMotion iPhones run at 120 Hz).</summary>
        public static void ApplyFrameRate()
        {
            int hz = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
            bool setting = Career != null && Career.settings.highFrameRate;
            // The simulation rate is picked when a match starts; the screen rate also drops while saving power.
            SimulationRate = PowerPolicy.TargetFrameRate(setting, hz, false);
            Application.targetFrameRate = PowerPolicy.TargetFrameRate(setting, hz, PowerMonitor.SavingPower);
            QualitySettings.vSyncCount = 0;
        }

        /// <summary>
        /// Applies the career to a request: your trained ratings when you play as the First Callers
        /// (Rise and Practice), plus Rise energy (starting stamina) and chemistry.
        /// </summary>
        public static MatchRequest PrepareRequest(MatchRequest request)
        {
            if (request == null || Career == null || Catalog == null) return request;
            if (CustomTeams.IsYours(request.HomeTeamId))
            {
                // Your player (Rook, or the one you created) with training upgrades applied.
                request.HumanPlayer = PlayerCreator.ForMatch(Career, Catalog);
                // Your recruited crew (or the original First Callers).
                request.HumanTeammates = CrewEngine.TeammateDefs(Career, Catalog);
            }
            // 15 Oct 2026 THE STREETS: your STREETS CREW recruits run with you.
            if (request.Mode == GameMode.Streets && CustomTeams.IsYours(request.HomeTeamId) && Career.streets != null)
            {
                var crew = StreetsTour.CrewDefs(Catalog, Career.streets);
                if (request.HumanTeammates == null) request.HumanTeammates = new System.Collections.Generic.List<PlayerDef> { null, null };
                for (int i = 0; i < crew.Count; i++)
                {
                    if (crew[i] == null) continue;
                    while (request.HumanTeammates.Count <= i) request.HumanTeammates.Add(null);
                    request.HumanTeammates[i] = crew[i];
                }
                // Phase 3: crew chemistry, and your HOMECOURT perks in a home game.
                bool home = StreetsHome.IsHome(request);
                request.ChemistryBonus = StreetsChem.Bonus(Career.streets);
                request.StyleBoostA = StreetsChem.Boost(Career.streets) + (home ? StreetsHome.Boost(Career.streets) : 0f);
                request.StartMeterA = home ? StreetsHome.StartMeter(Career.streets) : 0;
            }
            // Secret code ALWAYS HOT (not in practice, the tutorial, 2-player, or the demo).
            if (Secrets.IsOn(Career.secrets, Secrets.AlwaysHeat) && request.Mode != GameMode.Practice && request.Mode != GameMode.Tutorial
                && request.Mode != GameMode.Versus && request.Mode != GameMode.Demo)
                request.StartHeated = true;
            // Phase 36: SPOT SPECIALIST ranks from your career chart (single-player games only).
            request.SpotBonus = Specialist.AppliesTo(request) ? Specialist.SpotBonuses(Career.shotChart) : null;
            // Phase 34: Settings ► DIFFICULTY BY MODE.
            request.DifficultyId = ModeDifficulty.Resolve(Career.settings, request, Catalog);
            // 17 Oct 2026: a BALLER CITY crew always plays at its own level (BALLER or LEGENDARY), whatever the settings say.
            var cityCrew = CityCrews.FromContext(request.ContextId);
            if (cityCrew != null) request.DifficultyId = CityCrews.DifficultyFor(cityCrew);
            // CASHED OUT: after your big scoring games the street CPUs key on you.
            request.KeyOnHuman = StreetCash.KeyOn(Career, request);
            if (request.Mode == GameMode.Rise || request.Mode == GameMode.Rival)
            {
                request.StartingStamina = RiseEngine.StartingStamina(Career.rise);
                request.ChemistryBonus = Career.rise.chemistry / 100f * 0.1f;
                // Phase 40 SMART DIFFICULTY (Settings): a small nudge from your last few Rise results.
                request.AiEdge = Career.settings.smartDifficulty ? Adaptive.EdgeFor(Career.rise.recentMargins) : 0f;
            }
            return request;
        }

        public static void SaveCareer()
        {
            if (Career == null) return;
            if (SaveStore.Save(Career)) CloudSync.NotifySaved();
        }

        /// <summary>True when this launch loaded the career from iCloud (the menu says so once).</summary>
        public static bool CareerFromCloud { get; set; }

        /// <summary>Swaps in another career (loaded from iCloud), rebuilding the content it changes.</summary>
        public static void ReplaceCareer(CareerSaveData data)
        {
            if (data == null) return;
            // Dynasty mode and your custom team change content in memory: start from clean content.
            Content = ContentDatabase.Load();
            Career = data;
            DynastyEngine.Apply(Catalog, Career.dynasty);
            CourtBuilder.Apply(Catalog, Career.courts);
            CustomTeams.Apply(Catalog, Career.customTeam);
            SaveStore.Save(Career);
            ApplySettings();
        }

        /// <summary>Settings ► Reset: wipes the save and starts a fresh career.</summary>
        public static void ResetCareer()
        {
            SaveStore.Delete();
            CloudSync.Erase();
            // Dynasty mode changes ratings and rosters in memory: start from clean content.
            Content = ContentDatabase.Load();
            Career = Logic.Career.New(Catalog);
            CourtBuilder.Apply(Catalog, Career.courts);
            CustomTeams.Apply(Catalog, Career.customTeam);
            SaveCareer();
            ApplySettings();
        }

        /// <summary>The UI scale in use: the chosen one, or bigger when following a larger iPhone text size (Phase 31).</summary>
        public static float EffectiveUiScale(SettingsData s)
        {
            float scale = s.uiScale;
            if (s.followSystemText)
            {
                float system = 1f;
                try { system = UnityEngine.Accessibility.AccessibilitySettings.fontScale; } catch (System.Exception) { }
                scale = Mathf.Max(scale, ScreenReaderText.UiScaleForSystemText(system));
            }
            return scale;
        }

        /// <summary>Pushes settings to the systems that use them.</summary>
        public static void ApplySettings()
        {
            if (Career == null) return;
            UiKit.UiScale = EffectiveUiScale(Career.settings);
            Loc.Language = Loc.Normalize(Career.settings.language);
            AudioManager.ApplySettings();
            ApplyFrameRate();
            CrtOverlay.Apply(Career.settings.crt);
            CrtOverlay.ApplyTint(Secrets.IsOn(Career.secrets, Secrets.PocketGreen));
        }

#if UNITY_EDITOR || DEBUG
        /// <summary>Development only: lots of currency and every cosmetic unlocked.</summary>
        public static void DevUnlockAll()
        {
            Career.signalPoints += 100000;
            Career.fans += 100000;
            foreach (var c in Catalog.Cosmetics)
                if (!Career.ownedCosmetics.Contains(c.id)) Career.ownedCosmetics.Add(c.id);
            Career.gamesSinceUpgrade = 99;
            SaveCareer();
        }
#endif

        public static void EnsureInitialized()
        {
            if (IsInitialized) return;

            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;

            Content = ContentDatabase.Load();
            if (Content.FallbackKinds.Count > 0)
            {
                Debug.LogWarning("[CallerRetroBall] Using built-in default content for: " +
                                 string.Join(", ", Content.FallbackKinds) +
                                 ". Run 'Retro Hoops ► Run Project Setup' to generate editable assets.");
            }

#if UNITY_EDITOR || DEBUG
            var report = ContentValidator.Validate(Content.Catalog);
            if (!report.IsValid) Debug.LogError("[CallerRetroBall] " + report);
            else if (report.Warnings.Count > 0) Debug.LogWarning("[CallerRetroBall] " + report);
#endif

            Career = SaveStore.Load(Content.Catalog, out var status);
            CloudSync.EnsureExists();
            Career = CloudSync.AtBoot(Career, Content.Catalog, out bool fromCloud);
            if (fromCloud)
            {
                CareerFromCloud = true;
                SaveStore.Save(Career);
            }
            DynastyEngine.Apply(Content.Catalog, Career.dynasty);
            CourtBuilder.Apply(Content.Catalog, Career.courts);
            CustomTeams.Apply(Content.Catalog, Career.customTeam);
            CareerLoadStatus = status;
            AudioManager.EnsureExists();
            ApplySettings();
            if (Career.settings.gameCenter) GameCenter.Authenticate();

            SceneFlow.EnsureExists();
            ControllerCursor.EnsureExists();
            PowerMonitor.EnsureExists();
            UI.ScreenReader.EnsureExists(); // VoiceOver for the menus (Phase 31 briefly added a weaker duplicate in Core; removed in Phase 34)

            Application.deepLinkActivated -= OnDeepLink;
            Application.deepLinkActivated += OnDeepLink;
            if (!string.IsNullOrEmpty(Application.absoluteURL)) OnDeepLink(Application.absoluteURL);
        }

        // Supports "Enter Play Mode" without domain reload: static state is reset each play session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Content = null;
            PendingMatch = null;
            Career = null;
            TextureFactory.ClearCache();
            GameCenterSync.ResetSession();
            CareerFromCloud = false;
            PendingDuel = null;
            PendingKit = null;
        }
    }
}
