using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public enum UpgradeCheck { Ok = 0, MaxLevel = 1, AtAttributeCap = 2, NotEnoughPoints = 3, NeedsTraining = 4, Unknown = 5 }

    public enum CosmeticCheck { Ok = 0, AlreadyOwned = 1, NeedsFans = 2, NotEnoughPoints = 3, Unknown = 4, PassOnly = 5 }

    /// <summary>
    /// Pure progression rules on top of <see cref="CareerSaveData"/>: rewards, training upgrades
    /// (with costs, caps, and training time), cosmetics, and the effective ratings of your player.
    /// </summary>
    public static class Career
    {
        public const int MaxRememberedMatches = 30;

        public static CareerSaveData New(ContentCatalog c)
        {
            var data = new CareerSaveData();
            EnsureDefaults(data, c);
            return data;
        }

        /// <summary>Makes sure default cosmetics are owned and every slot has something equipped.</summary>
        public static void EnsureDefaults(CareerSaveData data, ContentCatalog c)
        {
            if (data.ownedCosmetics == null) data.ownedCosmetics = new List<string>();
            foreach (var cos in c.Cosmetics)
            {
                if (cos.unlockedByDefault && !data.ownedCosmetics.Contains(cos.id)) data.ownedCosmetics.Add(cos.id);
                string equipped = data.Equipped(cos.slot);
                bool equippedValid = !string.IsNullOrEmpty(equipped) && data.ownedCosmetics.Contains(equipped) && c.Find(c.Cosmetics, equipped) != null;
                if (!equippedValid && cos.unlockedByDefault) data.SetEquipped(cos.slot, cos.id);
            }
            if (string.IsNullOrWhiteSpace(data.nickname)) data.nickname = "Rook";
            if (data.settings == null) data.settings = new SettingsData();
            if (c.Difficulty(data.settings.difficultyId) == null) data.settings.difficultyId = DefaultContent.DefaultDifficultyId;
            if (data.rise == null) data.rise = new RiseSaveData();
            if (data.totals == null) data.totals = new CareerTotals();
            if (data.practice == null) data.practice = new PracticeBests();
            if (data.upgrades == null) data.upgrades = new List<UpgradeProgress>();
            if (data.appliedMatchIds == null) data.appliedMatchIds = new List<string>();
            if (data.classic == null) data.classic = new ClassicSaveData();
            if (data.daily == null) data.daily = new DailySaveData();
            if (data.records == null) data.records = new CareerRecords();
            if (data.history == null) data.history = new List<MatchHistoryEntry>();
            if (data.seasons == null) data.seasons = new List<SeasonHistoryEntry>();
            if (data.rival == null) data.rival = new RivalSaveData();
            if (data.king == null) data.king = new KingSaveData();
            if (data.king.order == null) data.king.order = new List<string>();
            if (data.secrets == null) data.secrets = new SecretsSaveData();
            if (data.customTeam == null) data.customTeam = new CustomTeamData();
            if (data.kits == null) data.kits = new KitSaveData();
            if (data.cup == null) data.cup = new CupSaveData();
            if (data.dynasty == null) data.dynasty = new DynastySaveData();
            if (data.tipsSeen == null) data.tipsSeen = new List<string>();
            if (data.secrets.codesFound == null) data.secrets.codesFound = new List<string>();
            if (data.secrets.hintsRevealed == null) data.secrets.hintsRevealed = new List<string>();
            if (data.secrets.unlocked == null) data.secrets.unlocked = new List<string>();
            if (data.secrets.arcade == null) data.secrets.arcade = new ArcadeSaveData();
            if (data.settings.crt < 0 || data.settings.crt > 2) data.settings.crt = 1;
            data.settings.language = Loc.Normalize(data.settings.language);
            if (data.badgesSeen == null) data.badgesSeen = new List<string>();
            if (data.storySeen == null) data.storySeen = new List<string>();
            if (data.rise.teammates == null) data.rise.teammates = new List<string>();
            if (data.rise.recruitable == null) data.rise.recruitable = new List<string>();
            if (data.rise.signed == null) data.rise.signed = new List<string>();
            if (data.rise.made == null) data.rise.made = new List<RiseMateData>();
            RiseCreate.Register(data, c);
            // The creator starts from Rook's look; it only replaces Rook once "created" is set.
            if (data.customPlayer == null || (!data.customPlayer.created && string.IsNullOrEmpty(data.customPlayer.archetypeId)))
                data.customPlayer = PlayerCreator.FromRook(c);
            PlayerCreator.Clamp(data.customPlayer, c);
        }

        public static string CleanNickname(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "Rook";
            var chars = new List<char>();
            foreach (char ch in raw.Trim())
            {
                if (char.IsLetterOrDigit(ch) || ch == ' ' || ch == '-' || ch == '_' || ch == '.') chars.Add(ch);
                if (chars.Count == 14) break;
            }
            var s = new string(chars.ToArray()).Trim();
            return s.Length == 0 ? "Rook" : s;
        }

        // ------------------------------------------------------------------ rewards

        /// <summary>
        /// Applies a finished game once. Returns false (and changes nothing) if this match id was
        /// already applied — rewards are never granted twice.
        /// </summary>
        public static bool ApplyMatch(CareerSaveData data, MatchSummary summary, RewardGrant grant)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (summary == null) throw new ArgumentNullException(nameof(summary));
            if (string.IsNullOrEmpty(summary.matchId) || data.appliedMatchIds.Contains(summary.matchId)) return false;

            data.appliedMatchIds.Add(summary.matchId);
            while (data.appliedMatchIds.Count > MaxRememberedMatches) data.appliedMatchIds.RemoveAt(0);

            data.lastNewRecords = new List<string>();
            data.lastSpecialist = new List<string>();
            data.signalPoints += grant.signalPoints;
            data.fans += grant.fans;
            // Practice, tutorial, and local 2-player games don't count toward career stats.
            if (summary.mode == GameMode.Practice || summary.mode == GameMode.Tutorial || summary.mode == GameMode.Versus || summary.mode == GameMode.Demo) return true;

            // Phase 36: a coached Franchise game has no line of yours: it pays, but your career numbers stay yours.
            if (summary.HumanLine == null && summary.mode == GameMode.Franchise) return true;
            // Phase 37: CLUTCH games start from a set score, so they don't set game records (margins, points).
            if (summary.mode != GameMode.Clutch) data.lastNewRecords = Records.Update(data, summary);
            data.gamesSinceUpgrade++;
            var t = data.totals;
            t.games++;
            if (summary.HumanWon) t.wins++;
            else t.losses++;
            var line = summary.HumanLine?.stats;
            if (line != null)
            {
                t.points += line.points;
                t.assists += line.assists;
                t.rebounds += line.rebounds;
                t.steals += line.steals;
                t.blocks += line.blocks;
                t.fieldGoalsMade += line.fieldGoalsMade;
                t.fieldGoalsAttempted += line.fieldGoalsAttempted;
                t.greens += line.greenReleases;
                t.alleyOops += line.alleyOops + line.alleyOopPasses;
                t.heatUps += line.heatUps;
                if (data.shotChart == null) data.shotChart = new ShotChartData();
                var before = ShotCharts.Copy(data.shotChart);
                ShotZones.Merge(data.shotChart, line.chart);
                data.lastSpecialist = Specialist.NewRanks(before, data.shotChart);
            }
            return true;
        }

        // ------------------------------------------------------------------ upgrades

        public static int UpgradeCost(UpgradeDef u, int currentLevel) =>
            (int)Math.Round(u.baseCost * Math.Pow(u.costGrowth, currentLevel));

        public static UpgradeCheck CanBuy(CareerSaveData data, UpgradeDef u, AttributeSet baseRatings)
        {
            if (u == null) return UpgradeCheck.Unknown;
            int level = data.UpgradeLevel(u.id);
            if (level >= u.maxLevel) return UpgradeCheck.MaxLevel;
            int current = Effective(baseRatings, data, u).Get(u.attribute);
            if (current >= u.attributeCap || current >= RatingScale.Max) return UpgradeCheck.AtAttributeCap;
            if (data.signalPoints < UpgradeCost(u, level)) return UpgradeCheck.NotEnoughPoints;
            if (data.gamesSinceUpgrade < u.trainingGames) return UpgradeCheck.NeedsTraining;
            return UpgradeCheck.Ok;
        }

        public static UpgradeCheck Buy(CareerSaveData data, UpgradeDef u, AttributeSet baseRatings)
        {
            var check = CanBuy(data, u, baseRatings);
            if (check != UpgradeCheck.Ok) return check;
            int level = data.UpgradeLevel(u.id);
            data.signalPoints -= UpgradeCost(u, level);
            var entry = data.upgrades.Find(x => x.id == u.id);
            if (entry == null) data.upgrades.Add(new UpgradeProgress { id = u.id, level = 1 });
            else entry.level++;
            data.gamesSinceUpgrade = 0;
            return UpgradeCheck.Ok;
        }

        private static AttributeSet Effective(AttributeSet baseRatings, CareerSaveData data, UpgradeDef only)
        {
            int level = data.UpgradeLevel(only.id);
            int value = Math.Min(Math.Max(baseRatings.Get(only.attribute), 0) + level * only.amountPerLevel, Math.Max(only.attributeCap, baseRatings.Get(only.attribute)));
            return baseRatings.With(only.attribute, RatingScale.Clamp(value));
        }

        /// <summary>Your player's ratings after training upgrades (each capped by its upgrade's cap).</summary>
        public static AttributeSet EffectiveRatings(AttributeSet baseRatings, CareerSaveData data, ContentCatalog c)
        {
            var r = baseRatings;
            foreach (var u in c.Upgrades)
            {
                int level = data.UpgradeLevel(u.id);
                if (level <= 0) continue;
                int start = baseRatings.Get(u.attribute);
                int value = Math.Min(start + level * u.amountPerLevel, Math.Max(u.attributeCap, start));
                r = r.With(u.attribute, RatingScale.Clamp(value));
            }
            return r;
        }

        // ------------------------------------------------------------------ cosmetics

        public static CosmeticCheck CanBuy(CareerSaveData data, CosmeticDef c)
        {
            if (c == null) return CosmeticCheck.Unknown;
            if (data.ownedCosmetics.Contains(c.id)) return CosmeticCheck.AlreadyOwned;
            if (c.passOnly) return CosmeticCheck.PassOnly;
            if (data.fans < c.fansRequired) return CosmeticCheck.NeedsFans;
            if (data.signalPoints < c.cost) return CosmeticCheck.NotEnoughPoints;
            return CosmeticCheck.Ok;
        }

        public static CosmeticCheck Buy(CareerSaveData data, CosmeticDef c)
        {
            var check = CanBuy(data, c);
            if (check != CosmeticCheck.Ok) return check;
            data.signalPoints -= c.cost;
            data.ownedCosmetics.Add(c.id);
            data.SetEquipped(c.slot, c.id);
            return CosmeticCheck.Ok;
        }

        public static bool Equip(CareerSaveData data, CosmeticDef c)
        {
            if (c == null || !data.ownedCosmetics.Contains(c.id)) return false;
            data.SetEquipped(c.slot, c.id);
            return true;
        }

        public const int TutorialReward = 100;

        /// <summary>Marks the tutorial done; the first time grants a small Signal Point reward. Returns the reward.</summary>
        public static int CompleteTutorial(CareerSaveData data)
        {
            if (data == null || data.tutorialDone) return 0;
            data.tutorialDone = true;
            data.signalPoints += TutorialReward;
            return TutorialReward;
        }

        // ------------------------------------------------------------------ practice

        /// <summary>Records drill results; returns true if any personal best improved.</summary>
        public static bool RecordPractice(CareerSaveData data, int makes, int bestStreak, int passingScore, float dribbleTime,
                                          int threePoint = 0, int lockdownStops = 0, bool shootoutWon = false,
                                          float aroundWorldTime = 0f, bool horseWon = false)
        {
            var p = data.practice;
            bool improved = false;
            if (horseWon) { p.horseWins++; improved = true; }
            if (aroundWorldTime > 0f && (p.aroundWorldTime <= 0f || aroundWorldTime < p.aroundWorldTime)) { p.aroundWorldTime = aroundWorldTime; improved = true; }
            if (shootoutWon) { p.shootoutWins++; improved = true; }
            if (threePoint > p.threePointBest) { p.threePointBest = threePoint; improved = true; }
            if (lockdownStops > p.lockdownBest) { p.lockdownBest = lockdownStops; improved = true; }
            if (makes > p.freeShootMakes) { p.freeShootMakes = makes; improved = true; }
            if (bestStreak > p.freeShootStreak) { p.freeShootStreak = bestStreak; improved = true; }
            if (passingScore > p.passingScore) { p.passingScore = passingScore; improved = true; }
            if (dribbleTime > 0f && (p.dribbleLaneTime <= 0f || dribbleTime < p.dribbleLaneTime)) { p.dribbleLaneTime = dribbleTime; improved = true; }
            return improved;
        }
    }
}
