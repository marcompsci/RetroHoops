namespace CallerRetroBall.Core
{
    /// <summary>Scene names, in build-settings order. Keep in sync with ProjectSetup.</summary>
    public static class SceneNames
    {
        public const string Boot = "BootScene";
        public const string MainMenu = "MainMenuScene";
        public const string Game = "GameScene";
        public const string Season = "SeasonScene";
        public const string LockerRoom = "LockerRoomScene";
        public const string Settings = "SettingsScene";
        /// <summary>16 Oct 2026: BALLER CITY.</summary>
        public const string City = "CityScene";

        public static readonly string[] All = { Boot, MainMenu, Game, Season, LockerRoom, Settings, City };
    }
}
