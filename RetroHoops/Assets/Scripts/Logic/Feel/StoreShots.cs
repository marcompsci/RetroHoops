using System;

namespace CallerRetroBall.Logic
{
    /// <summary>What a store screenshot shows.</summary>
    public enum StoreShotPlan { Title = 0, PlayMenu = 1, Game = 2, FullCourt = 3, Clutch = 4, Park = 5, Locker = 6, Pro = 7 }

    public sealed class StoreShot
    {
        public string Name;
        public StoreShotPlan Plan;
        /// <summary>Seconds the screenshot script waits after launch before taking the picture.</summary>
        public int Settle;
    }

    /// <summary>
    /// Phase 37: App Store screenshots. "tools/App Store Screenshots.command" launches the Simulator build once per
    /// shot with the environment variable RH_SCREENSHOT=&lt;name&gt; (passed as SIMCTL_CHILD_RH_SCREENSHOT); the game
    /// then opens straight onto that screen with no first-launch dialogs, no attract demo and no DEMO PLAY banner,
    /// and the script saves the screen. Nothing changes when the variable isn't set (it never is on a customer's phone).
    /// </summary>
    public static class StoreShots
    {
        public const string Variable = "RH_SCREENSHOT";

        public static readonly StoreShot[] All =
        {
            new StoreShot { Name = "game", Plan = StoreShotPlan.Game, Settle = 14 },
            new StoreShot { Name = "title", Plan = StoreShotPlan.Title, Settle = 8 },
            new StoreShot { Name = "fullcourt", Plan = StoreShotPlan.FullCourt, Settle = 14 },
            new StoreShot { Name = "play", Plan = StoreShotPlan.PlayMenu, Settle = 9 },
            new StoreShot { Name = "clutch", Plan = StoreShotPlan.Clutch, Settle = 9 },
            new StoreShot { Name = "park", Plan = StoreShotPlan.Park, Settle = 9 },
            new StoreShot { Name = "locker", Plan = StoreShotPlan.Locker, Settle = 10 },
            // 11 Oct 2026: the Retro Hoops Pro screen, for the in-app purchase's App Review screenshot (not a store shot).
            new StoreShot { Name = "pro", Plan = StoreShotPlan.Pro, Settle = 9 },
        };

        /// <summary>The shot this launch is for (null on a normal launch).</summary>
        public static StoreShot Current { get; set; }
        public static bool Active => Current != null;
        /// <summary>Set once the menu has opened the shot's screen, so coming back to the menu later is normal.</summary>
        public static bool Shown { get; set; }

        public static StoreShot Parse(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            string v = value.Trim().ToLowerInvariant();
            return Array.Find(All, s => s.Name == v);
        }

        /// <summary>The game a gameplay shot shows: two league teams, AI on both sides, the same game every time.</summary>
        public static MatchRequest GameRequest(ContentCatalog c, bool fullCourt)
        {
            var league = c?.TeamsInTier(TeamTier.League);
            if (league == null || league.Count < 2) return null;
            var home = league[0];
            var away = league[Math.Min(5, league.Count - 1)];
            return new MatchRequest
            {
                Mode = GameMode.Demo,
                HomeTeamId = home.id,
                AwayTeamId = away.id,
                CourtId = home.homeCourtId,
                RulesId = fullCourt ? FullCourt.RulesId : "rules.demo",
                FullCourt = fullCourt,
                DifficultyId = "difficulty.legend",
                Seed = fullCourt ? 4242u : 2626u,
            };
        }
    }
}
