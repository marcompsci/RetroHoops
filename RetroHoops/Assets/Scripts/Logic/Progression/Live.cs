using System;

namespace CallerRetroBall.Logic
{
    [Serializable]
    public class LiveSaveData
    {
        public int rating = LiveMode.StartRating;
        public int best = LiveMode.StartRating;
        public int wins, losses, games;
        /// <summary>The team you take into Live games.</summary>
        public string teamId = "";
        /// <summary>Last known subscription end (Unix seconds), from the App Store. Only used to show the state while it refreshes.</summary>
        public double subscribedUntil;
        /// <summary>Phase 33: the month (yyyymm, UTC) of the last Live game, for the monthly leaderboard.</summary>
        public int month;
        /// <summary>Phase 36 LIVE SEASONS: this month (yyyymm), the best rating reached in it, and Live games played in it.</summary>
        public int seasonMonth, seasonBest, seasonGames;
        /// <summary>The last finished season, and the last season already paid out.</summary>
        public int endedMonth, endedBest, endedGames, rewardedMonth;
        /// <summary>12 Oct 2026 LIVE POINTS (see <see cref="LivePoints"/>): all-time wins/scoring/time points, this month's,
        /// the day the daily time cap counts for, today's time points, and the current/best Live win streak.</summary>
        public int pWins, pScoring, pActivity;
        public int pMonth, pMonthWins, pMonthScoring, pMonthActivity;
        public int pDay, pDayActivity;
        public int pStreak, pBestStreak;
    }

    /// <summary>What a finished Live season paid.</summary>
    public sealed class LiveSeasonReward
    {
        public int Month;
        public string Tier;
        public int SignalPoints;
        /// <summary>A cosmetic unlocked by it (null = none, or already owned).</summary>
        public string CosmeticId;
    }

    /// <summary>
    /// Phase 36 LIVE SEASONS: every calendar month (UTC) is a season. Your best Live rating in it decides a reward when
    /// it ends (Signal Points by tier; ALL-STAR or better also unlocks the Live Season Star banner). You need
    /// <see cref="MinGames"/> Live games in the month to qualify. The monthly Game Center board shows the standings.
    /// </summary>
    public static class LiveSeason
    {
        public const int MinGames = 3;
        /// <summary>Signal Points for ROOKIE .. LEGEND.</summary>
        public static readonly int[] TierSp = { 100, 200, 350, 550, 800 };
        public const int StarTier = 3;
        public const string StarBannerId = "cosmetic.live.banner.season_star";

        public static int TierIndex(int rating)
        {
            int t = 0;
            for (int i = 0; i < LiveMode.TierFloor.Length; i++) if (rating >= LiveMode.TierFloor[i]) t = i;
            return t;
        }

        /// <summary>
        /// Brings the season up to date for <paramref name="month"/> (call when the Live screen opens and after each
        /// Live game, with <paramref name="playedGame"/> true then). A new month closes the old season.
        /// </summary>
        public static void Observe(LiveSaveData s, int month, bool playedGame)
        {
            if (s == null || month <= 0) return;
            if (s.seasonMonth != month)
            {
                if (s.seasonMonth > 0 && s.seasonMonth < month)
                {
                    s.endedMonth = s.seasonMonth;
                    s.endedBest = s.seasonBest;
                    s.endedGames = s.seasonGames;
                }
                s.seasonMonth = month;
                s.seasonBest = s.rating;
                s.seasonGames = 0;
            }
            s.seasonBest = Math.Max(s.seasonBest, s.rating);
            if (playedGame) s.seasonGames++;
        }

        /// <summary>Pays the last finished season once (null when there's nothing to pay).</summary>
        public static LiveSeasonReward Settle(CareerSaveData d, ContentCatalog c)
        {
            var s = d?.live;
            if (s == null || s.endedMonth <= 0 || s.rewardedMonth >= s.endedMonth) return null;
            s.rewardedMonth = s.endedMonth;
            if (s.endedGames < MinGames) return null;
            int tier = TierIndex(s.endedBest);
            var r = new LiveSeasonReward { Month = s.endedMonth, Tier = LiveMode.Tiers[tier], SignalPoints = TierSp[tier] };
            d.signalPoints += r.SignalPoints;
            if (tier >= StarTier && c?.Find(c.Cosmetics, StarBannerId) != null && !d.ownedCosmetics.Contains(StarBannerId))
            {
                d.ownedCosmetics.Add(StarBannerId);
                r.CosmeticId = StarBannerId;
            }
            return r;
        }

        /// <summary>"OCT 2026".</summary>
        public static string MonthName(int yyyymm)
        {
            string[] m = { "JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC" };
            int mm = yyyymm % 100;
            return (mm >= 1 && mm <= 12 ? m[mm - 1] : "?") + " " + yyyymm / 100;
        }
    }

    /// <summary>
    /// RETRO HOOPS LIVE: online head-to-head against other players through Game Center matchmaking
    /// (Apple runs the servers; no account of ours). Needs the Retro Hoops Live subscription, an
    /// auto-renewing monthly App Store subscription. Games use the same lockstep simulation as two-phone
    /// play, with a longer input delay for internet latency. Wins and losses move an Elo rating.
    /// </summary>
    public static class LiveMode
    {
        /// <summary>App Store Connect product id (auto-renewable subscription, 1 month).</summary>
        public const string ProductId = "com.phoronomicstudios.retrohoops.live.monthly";
        public const string SubscriptionGroup = "Retro Hoops Live";
        /// <summary>Shown only until the App Store returns the real, localised price.</summary>
        public const string PriceFallback = "$10.99";
        /// <summary>8 steps = 133 ms: covers most internet round trips without stalls.</summary>
        public const int InputDelay = 8;
        /// <summary>AI teammates play at this level in Live games, whatever either player's setting.</summary>
        public const string DifficultyId = DefaultContent.DefaultDifficultyId;
        public const int StartRating = 1000;
        public const int K = 32;
        public const string LeaderboardId = "retrohoops.live.rating";
        /// <summary>
        /// Phase 33: a recurring Game Center leaderboard that starts over every month (set up in App Store Connect as
        /// "recurring, 1 month"): your Live rating, posted only if you've played Live this month.
        /// </summary>
        public const string MonthlyLeaderboardId = "retrohoops.live.monthly";

        public static int MonthKey(DateTime utc) => utc.Year * 100 + utc.Month;

        /// <summary>The monthly board's score: your rating if you played Live this month, otherwise nothing.</summary>
        public static long MonthlyScore(LiveSaveData s, DateTime utcNow) =>
            s != null && s.games > 0 && s.month == MonthKey(utcNow) ? s.rating : 0;

        /// <summary>
        /// The opponent's rating after a game, for a Live rematch: Elo here is zero-sum, so they moved the opposite way
        /// to you (clamped like yours).
        /// </summary>
        public static int OpponentAfter(int theirBefore, int myDelta) => Math.Max(100, theirBefore - myDelta);
        /// <summary>A game counts once it's this far in (quitting earlier is a loss for the quitter only after this).</summary>
        public const float CountsAfterSeconds = 20f;

        public static readonly string[] Tiers = { "ROOKIE", "HOOPER", "STARTER", "ALL-STAR", "LEGEND" };
        public static readonly int[] TierFloor = { 0, 900, 1100, 1300, 1500 };

        public static string Tier(int rating)
        {
            int t = 0;
            for (int i = 0; i < TierFloor.Length; i++) if (rating >= TierFloor[i]) t = i;
            return Tiers[t];
        }

        /// <summary>Chance <paramref name="mine"/> beats <paramref name="theirs"/> (Elo).</summary>
        public static double Expected(int mine, int theirs) => 1.0 / (1.0 + Math.Pow(10.0, (theirs - mine) / 400.0));

        /// <summary>Rating change for a result: beating a stronger player moves you more.</summary>
        public static int Change(int mine, int theirs, bool won) =>
            (int)Math.Round(K * ((won ? 1.0 : 0.0) - Expected(mine, theirs)));

        /// <summary>Records a finished Live game. Returns the rating change.</summary>
        public static int Apply(LiveSaveData s, int theirRating, bool won)
        {
            int delta = Change(s.rating, theirRating, won);
            s.rating = Math.Max(100, s.rating + delta);
            s.best = Math.Max(s.best, s.rating);
            s.games++;
            if (won) s.wins++; else s.losses++;
            return delta;
        }

        /// <summary>
        /// Game Center player group: only players on the same game version (and link version) are matched,
        /// so both sides always run the same simulation.
        /// </summary>
        public static int PlayerGroup(string appVersion) =>
            (int)(StableHash.Of("live:" + LinkProtocol.Version + ":" + (appVersion ?? "")) % 1000000u) + 1;

        /// <summary>Host (seat 0) is the player whose Game Center id sorts first; both phones agree without talking.</summary>
        public static int Seat(string myPlayerId, string theirPlayerId) =>
            string.CompareOrdinal(myPlayerId ?? "", theirPlayerId ?? "") <= 0 ? 0 : 1;

        /// <summary>Cached view of the subscription (the App Store's answer replaces it when it arrives).</summary>
        public static bool CachedActive(LiveSaveData s, double nowUnix) => s != null && s.subscribedUntil > nowUnix;

        /// <summary>
        /// True when the delta between two ratings could have come from a single legitimate Elo game.
        /// The Elo K-factor is 32, so |delta| can't exceed K regardless of the opponent's rating.
        /// A small buffer (+1) absorbs rounding; the 100-floor clamp is handled separately.
        /// </summary>
        public static bool DeltaIsPlausible(int before, int after) =>
            after >= Math.Max(100, before - K) && after <= Math.Min(4000, before + K + 1);

        /// <summary>
        /// Returns a <see cref="LiveSaveData"/> that is self-consistent. Any field that can't be
        /// explained by legitimate play (impossible win/loss count, out-of-range rating, best below
        /// current rating) is corrected to the closest valid value.
        /// </summary>
        public static LiveSaveData Sanitize(LiveSaveData s)
        {
            if (s == null) return new LiveSaveData();
            int rating = Math.Max(100, Math.Min(4000, s.rating));
            int best   = Math.Max(rating, Math.Min(4000, s.best));
            int wins   = Math.Max(0, s.wins);
            int losses = Math.Max(0, s.losses);
            int games  = Math.Max(wins + losses, Math.Max(0, s.games));
            return new LiveSaveData
            {
                rating = rating, best = best,
                wins = wins, losses = losses, games = games,
                teamId = s.teamId ?? "",
                subscribedUntil = Math.Max(0.0, s.subscribedUntil),
                month = Math.Max(0, s.month),
                seasonMonth = Math.Max(0, s.seasonMonth), seasonBest = Math.Max(0, Math.Min(4000, s.seasonBest)), seasonGames = Math.Max(0, s.seasonGames),
                endedMonth = Math.Max(0, s.endedMonth), endedBest = Math.Max(0, Math.Min(4000, s.endedBest)), endedGames = Math.Max(0, s.endedGames),
                rewardedMonth = Math.Max(0, s.rewardedMonth),
                pWins = s.pWins, pScoring = s.pScoring, pActivity = s.pActivity,
                pMonth = Math.Max(0, s.pMonth), pMonthWins = s.pMonthWins, pMonthScoring = s.pMonthScoring, pMonthActivity = s.pMonthActivity,
                pDay = Math.Max(0, s.pDay), pDayActivity = s.pDayActivity, pStreak = s.pStreak, pBestStreak = s.pBestStreak,
            };
        }

        /// <summary>Plain-language subscription terms for the paywall (App Review guideline 3.1.2).</summary>
        public static string Terms(string price) =>
            "Retro Hoops Live is a monthly auto-renewing subscription for " + (string.IsNullOrEmpty(price) ? PriceFallback : price) + " a month. " +
            "Payment is charged to your Apple Account when you confirm. It renews automatically unless you turn off auto-renew at least 24 hours " +
            "before the end of the current period; your account is charged for the next month within 24 hours before it ends. " +
            "Manage or cancel any time in Settings ► your name ► Subscriptions. Everything else in Retro Hoops stays free and works offline.";

        /// <summary>Public pages (the repo is public): the privacy policy and the support page App Store Connect asks for.</summary>
        public const string PrivacyPolicyUrl = "https://github.com/marcompsci/RetroHoops/blob/main/RetroHoops/docs/PRIVACY.md";
        public const string SupportUrl = "https://github.com/marcompsci/RetroHoops/blob/main/RetroHoops/docs/SUPPORT.md";
        public const string TermsOfUseUrl = "https://www.apple.com/legal/internet-services/itunes/dev/stdeula/";
        public const string ManageUrl = "https://apps.apple.com/account/subscriptions";
    }
}
