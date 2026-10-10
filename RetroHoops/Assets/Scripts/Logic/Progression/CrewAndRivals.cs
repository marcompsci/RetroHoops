using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public enum RecruitCheck { Ok = 0, AlreadyOnCrew = 1, Locked = 2, NotEnoughPoints = 3, Unknown = 4 }

    /// <summary>
    /// Rise Mode roster building. Beating a street crew makes its three players recruitable; beating
    /// a league team makes its bench player recruitable (the fourth player, who never plays for them,
    /// so a recruit is never also on the other side). Signing costs Signal Points once; signed
    /// players and the original First Callers swap in and out of your two teammate spots for free.
    /// </summary>
    public static class CrewEngine
    {
        public const int TeammateSlots = 2;

        public static List<string> DefaultTeammates(ContentCatalog c)
        {
            var crew = c.Team(DefaultContent.PlayerCrewId);
            var list = new List<string>();
            if (crew != null)
                foreach (var id in crew.rosterPlayerIds)
                    if (id != DefaultContent.RookPlayerId && list.Count < TeammateSlots) list.Add(id);
            return list;
        }

        /// <summary>Called when the crew beats <paramref name="teamId"/> in Rise Mode.</summary>
        public static List<string> UnlockFrom(RiseSaveData r, ContentCatalog c, string teamId)
        {
            var added = new List<string>();
            var team = c.Team(teamId);
            if (r == null || team == null) return added;
            if (team.tier == TeamTier.Circuit)
            {
                foreach (var id in team.rosterPlayerIds) Add(r, id, added);
            }
            else if (team.tier == TeamTier.League && team.rosterPlayerIds.Count > MatchSimulation.PlayersPerTeam)
            {
                for (int i = MatchSimulation.PlayersPerTeam; i < team.rosterPlayerIds.Count; i++) Add(r, team.rosterPlayerIds[i], added);
            }
            return added;
        }

        private static void Add(RiseSaveData r, string id, List<string> added)
        {
            if (r.recruitable.Contains(id)) return;
            r.recruitable.Add(id);
            added.Add(id);
        }

        /// <summary>Signing fee: rises with overall rating, rounded to 10.</summary>
        public static int Cost(PlayerDef p)
        {
            if (p == null) return 0;
            int raw = 60 + Math.Max(0, p.attributes.Overall - 45) * 18;
            return (int)(Math.Round(raw / 10.0) * 10);
        }

        public static List<string> Teammates(CareerSaveData d, ContentCatalog c)
        {
            var r = d?.rise;
            RiseCreate.Register(d, c); // created teammates (Rise ► CREATE A TEAMMATE) must be in the catalog first
            if (r == null || r.teammates == null || r.teammates.Count != TeammateSlots) return DefaultTeammates(c);
            foreach (var id in r.teammates) if (c.Player(id) == null) return DefaultTeammates(c);
            return r.teammates;
        }

        public static bool IsFree(CareerSaveData d, ContentCatalog c, string playerId) =>
            DefaultTeammates(c).Contains(playerId) || (d.rise.signed != null && d.rise.signed.Contains(playerId))
            || RiseCreate.Find(d, playerId) != null;

        public static RecruitCheck CanRecruit(CareerSaveData d, ContentCatalog c, string playerId)
        {
            var p = c.Player(playerId);
            if (d == null || p == null) return RecruitCheck.Unknown;
            if (Teammates(d, c).Contains(playerId)) return RecruitCheck.AlreadyOnCrew;
            if (IsFree(d, c, playerId)) return RecruitCheck.Ok;
            if (!d.rise.recruitable.Contains(playerId)) return RecruitCheck.Locked;
            if (d.signalPoints < Cost(p)) return RecruitCheck.NotEnoughPoints;
            return RecruitCheck.Ok;
        }

        /// <summary>Puts <paramref name="playerId"/> in teammate <paramref name="slot"/> (paying the fee the first time).</summary>
        public static RecruitCheck Recruit(CareerSaveData d, ContentCatalog c, string playerId, int slot)
        {
            var check = CanRecruit(d, c, playerId);
            if (check != RecruitCheck.Ok) return check;
            if (slot < 0 || slot >= TeammateSlots) return RecruitCheck.Unknown;
            if (!IsFree(d, c, playerId))
            {
                d.signalPoints -= Cost(c.Player(playerId));
                d.rise.signed.Add(playerId);
            }
            var team = new List<string>(Teammates(d, c));
            team[slot] = playerId;
            d.rise.teammates = team;
            return RecruitCheck.Ok;
        }

        /// <summary>Everyone you could put on the floor: originals, signed players, and unlocked recruits.</summary>
        public static List<string> Pool(CareerSaveData d, ContentCatalog c)
        {
            RiseCreate.Register(d, c);
            var pool = new List<string>(DefaultTeammates(c));
            foreach (var m in RiseCreate.Made(d)) pool.Add(RiseCreate.PlayerId(m.id));
            foreach (var id in d.rise.signed) if (!pool.Contains(id)) pool.Add(id);
            foreach (var id in d.rise.recruitable) if (!pool.Contains(id)) pool.Add(id);
            pool.RemoveAll(id => c.Player(id) == null);
            return pool;
        }

        public static List<PlayerDef> TeammateDefs(CareerSaveData d, ContentCatalog c) =>
            Teammates(d, c).ConvertAll(id => c.Player(id));
    }

    [Serializable]
    public class RivalSaveData
    {
        public int wins;
        public int losses;
        /// <summary>Wins over the Sundown Syndicate (also counted in <see cref="wins"/>).</summary>
        public int sundownWins;
        /// <summary>Wins over the Midnight Tide (also counted in <see cref="wins"/>).</summary>
        public int tideWins;
        /// <summary>Wins over the Paper Cranes (also counted in <see cref="wins"/>).</summary>
        public int cranesWins;
        /// <summary>Wins over the Cassette Club (also counted in <see cref="wins"/>).</summary>
        public int cassetteWins;
        /// <summary>Wins over the Lighthouse Keepers (also counted in <see cref="wins"/>; Season 6).</summary>
        public int keeperWins;
        /// <summary>Wins over the Comet Couriers (also counted in <see cref="wins"/>; Season 7).</summary>
        public int courierWins;
        /// <summary>Wins over the Night Lanterns (also counted in <see cref="wins"/>; Season 8).</summary>
        public int lanternWins;
        /// <summary>Wins over the Roller Royals (also counted in <see cref="wins"/>; Season 9).</summary>
        public int royalWins;
        /// <summary>Wins over the Wash House (also counted in <see cref="wins"/>; Season 10).</summary>
        public int washWins;
        /// <summary>Wins over the Sky Kites (also counted in <see cref="wins"/>; Season 11).</summary>
        public int kiteWins;
        /// <summary>Wins over the Putt Club (also counted in <see cref="wins"/>; Season 12).</summary>
        public int puttWins;
        /// <summary>Rise season of the last Rival Challenge played (0 = none yet).</summary>
        public int lastSeason;
    }

    public enum RivalOutcome { None = 0, Won = 1, Lost = 2 }

    /// <summary>
    /// The four rival crews take turns, one per Rise season (see <see cref="RivalFor"/>). Once a Rise season, from week 5 of the regular season, a Rival
    /// Challenge appears in the hub. It doesn't count in the standings; winning pays a bonus.
    /// </summary>
    public static class RivalEngine
    {
        public const int ChallengeFromWeek = 5;
        public const int WinBonus = 150;
        public const int WinFans = 50;

        public static bool ChallengeAvailable(CareerSaveData d)
        {
            var r = d?.rise;
            if (r == null || r.season == null || r.stage != RiseStage.Season) return false;
            if (r.season.currentWeek < ChallengeFromWeek) return false;
            return d.rival.lastSeason != r.season.seasonNumber;
        }

        public static MatchRequest Challenge(CareerSaveData d, ContentCatalog c, string difficultyId)
        {
            if (!ChallengeAvailable(d)) return null;
            var rival = c.Team(RivalFor(d.rise.season.seasonNumber));
            if (rival == null) return null;
            return new MatchRequest
            {
                Mode = GameMode.Rival,
                HomeTeamId = DefaultContent.PlayerCrewId,
                AwayTeamId = rival.id,
                CourtId = rival.homeCourtId,
                DifficultyId = difficultyId,
                ContextId = "rival:s" + d.rise.season.seasonNumber,
            };
        }

        /// <summary>
        /// Which rival crew a Rise season brings. The six rivals take turns: Neon Static (seasons 1, 7, 13...),
        /// the Sundown Syndicate (2, 8, ...), the Midnight Tide (3, 9, ...), the Paper Cranes (4, 10, ...), the Cassette Club (5, 11, ...)
        /// the Lighthouse Keepers (6, 14, ...), the Comet Couriers (7, 15, ...) the Night Lanterns (8, 18, ...), the Roller Royals (9, 20, ...), the Wash House (10, 22, ...), the Sky Kites (11, 23, ...) and the Putt Club (12, 24, ...); twelve since Phase 40.
        /// </summary>
        public static string RivalFor(int seasonNumber)
        {
            if (seasonNumber < 1) return DefaultContent.RivalCrewId;
            switch ((seasonNumber - 1) % 12)
            {
                case 11: return DefaultContent.Rival12CrewId;
                case 10: return DefaultContent.Rival11CrewId;
                case 9: return DefaultContent.Rival10CrewId;
                case 8: return DefaultContent.Rival9CrewId;
                case 7: return DefaultContent.Rival8CrewId;
                case 6: return DefaultContent.Rival7CrewId;
                case 5: return DefaultContent.Rival6CrewId;
                case 1: return DefaultContent.Rival2CrewId;
                case 2: return DefaultContent.Rival3CrewId;
                case 3: return DefaultContent.Rival4CrewId;
                case 4: return DefaultContent.Rival5CrewId;
                default: return DefaultContent.RivalCrewId;
            }
        }

        /// <summary>Wins over Neon Static only (the other rivals have their own counters).</summary>
        public static int StaticWins(CareerSaveData d) =>
            d == null ? 0 : d.rival.wins - d.rival.sundownWins - d.rival.tideWins - d.rival.cranesWins - d.rival.cassetteWins - d.rival.keeperWins - d.rival.courierWins - d.rival.lanternWins - d.rival.royalWins - d.rival.washWins - d.rival.kiteWins - d.rival.puttWins;

        /// <summary>Beaten the first five rival crews at least once (the Game Center achievement; see <see cref="BeatAllSix"/>).</summary>
        public static bool BeatEveryRival(CareerSaveData d) =>
            d != null && d.rival != null && StaticWins(d) >= 1 && d.rival.sundownWins >= 1 && d.rival.tideWins >= 1 && d.rival.cranesWins >= 1 && d.rival.cassetteWins >= 1;

        /// <summary>Beaten all six rival crews at least once (Season 6).</summary>
        public static bool BeatAllSix(CareerSaveData d) => BeatEveryRival(d) && d.rival.keeperWins >= 1;

        /// <summary>Beaten all seven rival crews at least once (Season 7).</summary>
        public static bool BeatAllSeven(CareerSaveData d) => BeatAllSix(d) && d.rival.courierWins >= 1;

        /// <summary>Beaten all eight rival crews at least once (Season 8).</summary>
        public static bool BeatAllEight(CareerSaveData d) => BeatAllSeven(d) && d.rival.lanternWins >= 1;

        /// <summary>Beaten all nine rival crews at least once (Season 9).</summary>
        public static bool BeatAllNine(CareerSaveData d) => BeatAllEight(d) && d.rival.royalWins >= 1;

        /// <summary>Beaten all ten rival crews at least once (Season 10).</summary>
        public static bool BeatAllTen(CareerSaveData d) => BeatAllNine(d) && d.rival.washWins >= 1;

        /// <summary>Beaten all eleven rival crews at least once (Season 11).</summary>
        public static bool BeatAllEleven(CareerSaveData d) => BeatAllTen(d) && d.rival.kiteWins >= 1;

        /// <summary>Beaten all twelve rival crews at least once (Season 12).</summary>
        public static bool BeatAllTwelve(CareerSaveData d) => BeatAllEleven(d) && d.rival.puttWins >= 1;

        public static RivalOutcome ApplyResult(CareerSaveData d, MatchSummary s)
        {
            if (d == null || s == null || s.mode != GameMode.Rival) return RivalOutcome.None;
            d.rival.lastSeason = d.rise.season != null ? d.rise.season.seasonNumber : d.rival.lastSeason + 1;
            bool sundown = s.teamAId == DefaultContent.Rival2CrewId || s.teamBId == DefaultContent.Rival2CrewId;
            bool tide = s.teamAId == DefaultContent.Rival3CrewId || s.teamBId == DefaultContent.Rival3CrewId;
            bool cranes = s.teamAId == DefaultContent.Rival4CrewId || s.teamBId == DefaultContent.Rival4CrewId;
            bool cassette = s.teamAId == DefaultContent.Rival5CrewId || s.teamBId == DefaultContent.Rival5CrewId;
            bool keepers = s.teamAId == DefaultContent.Rival6CrewId || s.teamBId == DefaultContent.Rival6CrewId;
            bool couriers = s.teamAId == DefaultContent.Rival7CrewId || s.teamBId == DefaultContent.Rival7CrewId;
            bool lanterns = s.teamAId == DefaultContent.Rival8CrewId || s.teamBId == DefaultContent.Rival8CrewId;
            bool royals = s.teamAId == DefaultContent.Rival9CrewId || s.teamBId == DefaultContent.Rival9CrewId;
            bool wash = s.teamAId == DefaultContent.Rival10CrewId || s.teamBId == DefaultContent.Rival10CrewId;
            bool kites = s.teamAId == DefaultContent.Rival11CrewId || s.teamBId == DefaultContent.Rival11CrewId;
            bool putt = s.teamAId == DefaultContent.Rival12CrewId || s.teamBId == DefaultContent.Rival12CrewId;
            if (s.HumanWon)
            {
                if (sundown) d.rival.sundownWins++;
                if (tide) d.rival.tideWins++;
                if (cranes) d.rival.cranesWins++;
                if (cassette) d.rival.cassetteWins++;
                if (keepers) d.rival.keeperWins++;
                if (couriers) d.rival.courierWins++;
                if (lanterns) d.rival.lanternWins++;
                if (royals) d.rival.royalWins++;
                if (wash) d.rival.washWins++;
                if (kites) d.rival.kiteWins++;
                if (putt) d.rival.puttWins++;
                d.rival.wins++;
                d.signalPoints += WinBonus;
                d.fans += WinFans;
                return RivalOutcome.Won;
            }
            d.rival.losses++;
            return RivalOutcome.Lost;
        }
    }

    public sealed class BadgeDef
    {
        public string Id;
        public string Title;
        public string Description;
        public Func<CareerSaveData, bool> Earned;
    }

    /// <summary>In-game badges (no Game Center needed). New ones are announced once after a game.</summary>
    public static class Badges
    {
        public static readonly List<BadgeDef> All = new List<BadgeDef>
        {
            B("badge.first_win", "FIRST W", "Win a game.", d => d.totals.wins >= 1),
            B("badge.green_light", "GREEN LIGHT", "Hit a GREEN release.", d => d.totals.greens >= 1),
            B("badge.sharpshooter", "SHARPSHOOTER", "5 GREEN releases in one game.", d => d.records.greens >= 5),
            B("badge.bucket_getter", "BUCKET GETTER", "Score 12 points in one game.", d => d.records.points >= 12),
            B("badge.floor_general", "FLOOR GENERAL", "5 assists in one game.", d => d.records.assists >= 5),
            B("badge.lockdown", "LOCKDOWN", "3 steals in one game.", d => d.records.steals >= 3),
            B("badge.rim_protector", "RIM PROTECTOR", "2 blocks in one game.", d => d.records.blocks >= 2),
            B("badge.on_a_run", "ON A RUN", "Win 5 games in a row.", d => d.records.bestWinStreak >= 5),
            B("badge.circuit", "OFF THE BLACKTOP", "Clear The Blacktop Circuit.", d => d.rise.stage != RiseStage.Circuit || d.rise.seasonsPlayed > 0),
            B("badge.gold_signal", "GOLD SIGNAL", "Win The Gold Signal Cup.", d => d.totals.championships >= 1),
            B("badge.first_call", "FIRST CALL", "Win the First Call Classic.", d => d.classic.titles >= 1),
            B("badge.static_killer", "STATIC KILLER", "Beat Neon Static.", d => d.rival.wins >= 1),
            B("badge.recruiter", "RECRUITER", "Sign a player to your crew.", d => d.rise.signed.Count >= 1),
            B("badge.every_day", "EVERY DAY", "7-day Daily Challenge streak.", d => d.daily.bestStreak >= 7),
            B("badge.self_made", "SELF MADE", "Create your own player.", d => d.customPlayer != null && d.customPlayer.created),
            B("badge.ready", "READY TO CALL", "Finish How to Play.", d => d.tutorialDone),
            B("badge.sundown", "SUNDOWN SETTLED", "Beat the Sundown Syndicate.", d => d.rival.sundownWins >= 1),
            B("badge.two_time", "TWO-TIME", "Win The Gold Signal Cup twice.", d => d.totals.championships >= 2),
            B("badge.sky_hookup", "SKY HOOKUP", "10 alley-oops (thrown or finished).", d => d.totals.alleyOops >= 10),
            B("badge.heat_check", "HEAT CHECK", "Heat up 10 times.", d => d.totals.heatUps >= 10),
            B("badge.your_colors", "YOUR COLORS", "Create your own team.", d => d.customTeam != null && d.customTeam.created),
            B("badge.caller_cup", "CUP RUN", "Win the Caller Cup.", d => d.cup != null && d.cup.titles >= 1),
            B("badge.shootout", "SHOOTOUT STAR", "Win a Shootout.", d => d.practice.shootoutWins >= 1),
            B("badge.tide", "TIDE TURNER", "Beat the Midnight Tide.", d => d.rival.tideWins >= 1),
            B("badge.three_peat", "THREE-PEAT", "Win The Gold Signal Cup three times.", d => d.totals.championships >= 3),
            B("badge.dynasty", "DYNASTY", "Play five Rise seasons.", d => d.rise.seasonsPlayed >= 5),
            B("badge.cranes", "GROUNDED", "Beat the Paper Cranes.", d => d.rival.cranesWins >= 1),
            B("badge.every_rival", "NO RIVALS LEFT", "Beat all five rival crews.", RivalEngine.BeatEveryRival),
            B("badge.cassette", "REWOUND", "Beat the Cassette Club.", d => d.rival.cassetteWins >= 1),
            B("badge.keepers", "LIGHTS OUT", "Beat the Lighthouse Keepers.", d => d.rival.keeperWins >= 1),
            B("badge.all_six", "FULL ROTATION", "Beat all six rival crews.", RivalEngine.BeatAllSix),
            B("badge.couriers", "SIGNED FOR", "Beat the Comet Couriers.", d => d.rival.courierWins >= 1),
            B("badge.all_seven", "SEVEN FOR SEVEN", "Beat all seven rival crews.", RivalEngine.BeatAllSeven),
            B("badge.lanterns", "LIGHTS OUT", "Beat the Night Lanterns.", d => d.rival.lanternWins >= 1),
            B("badge.specialist", "SPECIALIST", "Reach GOLD as a SPOT SPECIALIST anywhere.", d => AnyGold(d)),
            B("badge.clutch", "CLUTCH GENE", "Win every CLUTCH scenario.", d => Clutch.AllWon(d.clutch)),
            B("badge.ice_veins", "ICE IN THE VEINS", "Earn every CLUTCH star.", d => Clutch.Perfect(d.clutch)),
            B("badge.all_eight", "EIGHT FOR EIGHT", "Beat all eight rival crews.", RivalEngine.BeatAllEight),
            B("badge.royals", "LAST SKATE", "Beat the Roller Royals.", d => d.rival.royalWins >= 1),
            B("badge.all_nine", "NINE FOR NINE", "Beat all nine rival crews.", RivalEngine.BeatAllNine),
            B("badge.wash", "SPIN CYCLE", "Beat the Wash House.", d => d.rival.washWins >= 1),
            B("badge.all_ten", "PERFECT TEN", "Beat all ten rival crews.", RivalEngine.BeatAllTen),
            B("badge.kites", "GROUNDED KITES", "Beat the Sky Kites.", d => d.rival.kiteWins >= 1),
            B("badge.all_eleven", "ELEVEN STRAIGHT", "Beat all eleven rival crews.", RivalEngine.BeatAllEleven),
            B("badge.putt", "IN THE CUP", "Beat the Putt Club.", d => d.rival.puttWins >= 1),
            B("badge.all_twelve", "FULL DOZEN", "Beat all twelve rival crews.", RivalEngine.BeatAllTwelve),
            B("badge.couch", "COUCH RIVALS", "Play a 2 Player game.", d => d.totals.versusGames >= 1),
            B("badge.four_rings", "FOUR CUPS", "Win The Gold Signal Cup four times.", d => d.totals.championships >= 4),
            B("badge.ladder", "NO CONTINUES NEEDED", "Clear the Arcade Ladder.", d => d.secrets != null && d.secrets.arcade.clears > 0),
            B("badge.codes", "CODE BREAKER", "Find every secret code.", d => d.secrets != null && Secrets.All.TrueForAll(x => d.secrets.codesFound.Contains(x.Id))),
        };

        /// <summary>Phase 36: GOLD as a SPOT SPECIALIST in any area.</summary>
        private static bool AnyGold(CareerSaveData d)
        {
            if (d?.shotChart == null) return false;
            for (int g = 0; g < Specialist.Groups; g++)
                if (Specialist.TierOf(d.shotChart, (SpotGroup)g) == SpecialistTier.Gold) return true;
            return false;
        }

        private static BadgeDef B(string id, string title, string description, Func<CareerSaveData, bool> earned) =>
            new BadgeDef { Id = id, Title = title, Description = description, Earned = earned };

        public static bool IsEarned(BadgeDef b, CareerSaveData d)
        {
            try { return d != null && b.Earned(d); }
            catch (NullReferenceException) { return false; }
        }

        /// <summary>Badges earned but not yet announced; marks them announced.</summary>
        public static List<BadgeDef> TakeNew(CareerSaveData d)
        {
            var list = new List<BadgeDef>();
            if (d == null) return list;
            foreach (var b in All)
                if (IsEarned(b, d) && !d.badgesSeen.Contains(b.Id))
                {
                    d.badgesSeen.Add(b.Id);
                    list.Add(b);
                }
            return list;
        }

        public static int EarnedCount(CareerSaveData d)
        {
            int n = 0;
            foreach (var b in All) if (IsEarned(b, d)) n++;
            return n;
        }
    }

    public enum StorySpeaker { Coach = 0, Rival = 1, You = 2, /** Kaia Sol of the Sundown Syndicate (chapter 2). */ Rival2 = 3,
        /** Mara Quill of the Midnight Tide (chapter 3). */ Rival3 = 4, /** Juno Vale of the Paper Cranes (chapter 4). */ Rival4 = 5,
        /** Echo Rivera of the Cassette Club (chapter 5). */ Rival5 = 6,
        /** Summer Story (original cast): Nova Quinn, Big Sal, Mic Tally, Kojo Stride. */ Nova = 7, Sal = 8, Mic = 9, Kojo = 10,
        /** Wren Marsh of the Lighthouse Keepers (chapter 6). */ Rival6 = 11,
        /** Remy Okoro of the Comet Couriers (chapter 7). */ Rival7 = 12,
        /** Juno Akande of the Night Lanterns (chapter 8). */ Rival8 = 13,
        /** Skye Varo of the Roller Royals (chapter 9). */ Rival9 = 14,
        /** Opal Whitaker of the Wash House (chapter 10). */ Rival10 = 15,
        /** Rin Takahashi of the Sky Kites (chapter 11). */ Rival11 = 16,
        /** Bex Quarrie of the Putt Club (chapter 12). */ Rival12 = 17 }

    public struct StoryLine
    {
        public StorySpeaker Speaker;
        public string Text;
        public StoryLine(StorySpeaker speaker, string text) { Speaker = speaker; Text = text; }
    }

    public sealed class StoryBeat
    {
        public string Id;
        public List<StoryLine> Lines = new List<StoryLine>();
    }

    /// <summary>
    /// Short story scenes between Rise Mode stages, with Coach Dee (your crew's coach) and Vex of
    /// Neon Static. All dialogue is original. Each scene plays once, in order, when its moment arrives.
    /// </summary>
    public static class Story
    {
        public const string CoachName = "COACH DEE";
        public const string RivalName = "VEX";
        public const string Rival2Name = "KAIA";
        public const string Rival3Name = "MARA";
        public const string Rival4Name = "JUNO";
        public const string Rival5Name = "ECHO";
        public const string Rival6Name = "WREN";
        public const string Rival7Name = "REMY";
        public const string Rival8Name = "JUNO";
        public const string Rival9Name = "SKYE";
        public const string Rival10Name = "OPAL";
        public const string Rival11Name = "RIN";
        public const string Rival12Name = "BEX";

        public const string Intro = "story.intro";
        public const string CircuitCleared = "story.circuit_cleared";
        public const string RivalIntro = "story.rival_intro";
        public const string RivalBeaten = "story.rival_beaten";
        public const string RivalLost = "story.rival_lost";
        public const string Playoffs = "story.playoffs";
        public const string Champions = "story.champions";
        /// <summary>First launch: Coach Dee says hello (played from the main menu, not the Rise hub).</summary>
        public const string Welcome = "story.welcome";
        // Chapter 2 (Rise Season 2 and later).
        public const string Season2 = "story.s2_open";
        public const string Rival2Intro = "story.rival2_intro";
        public const string Rival2Beaten = "story.rival2_beaten";
        public const string TwoTime = "story.two_time";
        // Chapter 3 (Rise Season 3 and every third season).
        public const string Rival3Intro = "story.rival3_intro";
        public const string Rival3Beaten = "story.rival3_beaten";
        public const string ThreePeat = "story.three_peat";
        // Chapter 4 (Rise Season 4 and every fourth season).
        public const string Rival4Intro = "story.rival4_intro";
        public const string Rival4Beaten = "story.rival4_beaten";
        public const string FourCups = "story.four_cups";
        // Chapter 5 (Rise Season 5 and every fifth season).
        public const string Rival5Intro = "story.rival5_intro";
        public const string Rival5Beaten = "story.rival5_beaten";
        // Chapter 6 (Rise Season 6 and every sixth season).
        public const string Rival6Intro = "story.rival6_intro";
        public const string Rival6Beaten = "story.rival6_beaten";
        // Chapter 7 (Rise Season 7 and every seventh season).
        public const string Rival7Intro = "story.rival7_intro";
        public const string Rival7Beaten = "story.rival7_beaten";
        // Chapter 8 (Rise Season 8 and every eighth season).
        public const string Rival8Intro = "story.rival8_intro";
        public const string Rival8Beaten = "story.rival8_beaten";
        // Chapter 9 (Rise Season 9 and every ninth season).
        public const string Rival9Intro = "story.rival9_intro";
        public const string Rival9Beaten = "story.rival9_beaten";
        // Chapter 10 (Rise Season 10 and every tenth season).
        public const string Rival10Intro = "story.rival10_intro";
        public const string Rival10Beaten = "story.rival10_beaten";
        // Chapter 11 (Rise Season 11 and every eleventh season).
        public const string Rival11Intro = "story.rival11_intro";
        public const string Rival11Beaten = "story.rival11_beaten";
        // Chapter 12 (Rise Season 12 and every twelfth season).
        public const string Rival12Intro = "story.rival12_intro";
        public const string Rival12Beaten = "story.rival12_beaten";

        public static StoryBeat Beat(string id, string nickname) => Beat(id, nickname, Loc.Language);

        public static StoryBeat Beat(string id, string nickname, string language)
        {
            string me = string.IsNullOrEmpty(nickname) ? "Rook" : nickname;
            var b = new StoryBeat { Id = id };
            void C(string t) => b.Lines.Add(new StoryLine(StorySpeaker.Coach, t));
            void V(string t) => b.Lines.Add(new StoryLine(StorySpeaker.Rival, t));
            void Y(string t) => b.Lines.Add(new StoryLine(StorySpeaker.You, t));
            void K(string t) => b.Lines.Add(new StoryLine(StorySpeaker.Rival2, t));
            void M(string t) => b.Lines.Add(new StoryLine(StorySpeaker.Rival3, t));
            void J(string t) => b.Lines.Add(new StoryLine(StorySpeaker.Rival4, t));
            void E(string t) => b.Lines.Add(new StoryLine(StorySpeaker.Rival5, t));
            void W(string t) => b.Lines.Add(new StoryLine(StorySpeaker.Rival6, t));
            void R(string t) => b.Lines.Add(new StoryLine(StorySpeaker.Rival7, t));
            void N(string t) => b.Lines.Add(new StoryLine(StorySpeaker.Rival8, t));
            void X(string t) => b.Lines.Add(new StoryLine(StorySpeaker.Rival9, t));
            void O(string t) => b.Lines.Add(new StoryLine(StorySpeaker.Rival10, t));
            void I(string t) => b.Lines.Add(new StoryLine(StorySpeaker.Rival11, t));
            void Q(string t) => b.Lines.Add(new StoryLine(StorySpeaker.Rival12, t));
            if (language == Loc.Spanish)
            {
                switch (id)
                {
                    case Rival12Intro:
                        Q("Aquí cada tiro se mide antes de soltarlo, " + me + ". Como un putt al hoyo 18.");
                        Q("El Putt Club no corre. Esperamos el tiro perfecto y no fallamos.");
                        Y("Pues hoy os meto prisa.");
                        C("Bex no tira hasta que lo tiene claro. Presiona pronto en la posesión y oblígala a decidir.");
                        break;
                    case Rival12Beaten:
                        Q("...Ni un tiro limpio en toda la noche. Bien jugado.");
                        C("Doce equipos rivales, " + me + ". Una docena entera. Esta ciudad ya es tuya.");
                        break;
                    case Rival11Intro:
                        I("Arriba en el acantilado sopla siempre, " + me + ". Nosotros tiramos con el viento a favor.");
                        I("Los Sky Kites tiran desde cualquier sitio. Si nos dejas espacio, el balón no baja.");
                        Y("Pues hoy cortamos el hilo.");
                        C("Rin tira desde muy lejos. Sal a defender hasta la línea y no le dejes soltar el brazo.");
                        break;
                    case Rival11Beaten:
                        I("...Se calmó el viento. Primera vez en todo el verano.");
                        C("Once equipos rivales, " + me + ". Ya no les ganas: les enseñas.");
                        break;
                    case Rival10Intro:
                        O("Nosotros jugamos cuando la ciudad duerme, " + me + ". Entre lavadora y secadora.");
                        O("En el Wash House el balón no para nunca. Pase, corte, pase. Te mareas antes del descanso.");
                        Y("Pues hoy alguien va a tener que doblar la ropa.");
                        C("Opal no se queda con el balón ni un segundo. Defiende las líneas de pase y no te pierdas en los cortes.");
                        break;
                    case Rival10Beaten:
                        O("...Se acabó el programa. Esta noche lo has dejado todo limpio.");
                        C("Diez equipos rivales, " + me + ". Diez. No queda una cancha en esta ciudad que no sepa tu nombre.");
                        break;
                    case Rival9Intro:
                        X("La pista cierra a las once, " + me + ". El último patinaje es nuestro.");
                        X("Los Roller Royals presionan los cuatro minutos. Si botas, te lo quitamos.");
                        Y("Pues hoy no patino. Hoy paso.");
                        C("Skye presiona en toda la cancha. Pasa rápido, no botes de lado y busca a quien queda solo.");
                        break;
                    case Rival9Beaten:
                        X("...Encendieron las luces de la pista. Se acabó la noche.");
                        C("Nueve equipos rivales, " + me + ". Ya no queda nadie en esta ciudad que no te haya visto jugar.");
                        break;
                    case Rival8Intro:
                        N("El mercado cierra a medianoche, " + me + ". Nosotros abrimos a esa hora.");
                        N("Los Night Lanterns no corren detrás de nadie. Cerramos la zona y lanzamos por encima.");
                        Y("Pues hoy apago las luces del mercado.");
                        C("Juno juega en zona y busca el pase bombeado. Tira desde las esquinas y castiga el hueco.");
                        break;
                    case Rival8Beaten:
                        N("...Se apagaron todas. No recuerdo la última vez.");
                        C("Ocho equipos rivales, " + me + ". Esta ciudad ya no tiene un rincón donde no te conozcan.");
                        break;
                    case Rival7Intro:
                        R("Firma aquí, " + me + ". Te traemos una derrota a domicilio.");
                        R("Los Comet Couriers no se paran. Salimos antes de que te des la vuelta.");
                        Y("Pues hoy no hay entrega.");
                        C("Remy corre la contra en cada balón. Vuelve en defensa antes de celebrar.");
                        break;
                    case Rival7Beaten:
                        R("...Paquete devuelto. No pasa nunca.");
                        C("Siete equipos rivales, " + me + ". Ya no queda nadie en esta ciudad que no te haya visto ganar.");
                        break;
                    case Rival6Intro:
                        W("Desde la torre se ve toda la ciudad, " + me + ". Te vimos venir hace seis temporadas.");
                        W("Los Lighthouse Keepers no persiguen a nadie. Esperamos, y la luz nos dice adónde vas.");
                        Y("Pues apagad la luz. Voy a jugar a oscuras.");
                        C("Wren aprieta en todo el campo. Saca rápido el balón de la presión y busca al tirador.");
                        break;
                    case Rival6Beaten:
                        W("...La luz no te alcanzaba. Eras más rápido que el haz.");
                        C("Seis rivales. Ya no queda ninguna torre en esta ciudad que no sepa tu nombre, " + me + ".");
                        break;
                    case Rival5Intro:
                        E("Tenemos todos tus partidos grabados, " + me + ". Cara A, cara B.");
                        E("El Cassette Club no improvisa. Rebobinamos y lo volvemos a poner hasta que sale perfecto.");
                        Y("Entonces voy a cambiar la canción.");
                        C("Echo te deja tirar de fuera y cierra la zona. Ataca el aro y pásala rápido.");
                        break;
                    case Rival5Beaten:
                        E("...Vale. Esa cinta la vamos a escuchar muchas veces.");
                        C("Cinco equipos rivales, cinco historias. Todas terminan contigo, " + me + ".");
                        break;
                    case Rival4Intro:
                        J("Nos fijamos en ti hace tres temporadas, " + me + ". Tomamos notas de cada partido.");
                        J("Los Paper Cranes no tienen estrellas. Tenemos un plan, doblado mil veces.");
                        Y("Los planes se rompen cuando alguien los empuja.");
                        C("Juno lee la cancha antes que nadie. Cambia el ritmo y no repitas la misma jugada.");
                        break;
                    case Rival4Beaten:
                        J("...Bien. Haremos un plan nuevo.");
                        C("Ya conoces a todos los rivales de la ciudad. Y todos te conocen a ti.");
                        break;
                    case FourCups:
                        C("Cuatro copas. Ya no hablan de los First Callers. Hablan de ti, " + me + ".");
                        Y("Entonces hablemos menos y juguemos más.");
                        break;
                    case Rival3Intro:
                        M("Llegamos en el último ferry. Nadie nos vio venir.");
                        M("El Midnight Tide no corre. Esperamos a que te canses y luego subimos, como la marea.");
                        Y("Entonces no me voy a cansar.");
                        C("Mara juega en los dos lados de la cancha. Cuida el balón y corre cuando puedas.");
                        break;
                    case Rival3Beaten:
                        M("...La marea vuelve, " + me + ". Siempre vuelve.");
                        C("Que vuelva. Nosotros estaremos aquí.");
                        break;
                    case ThreePeat:
                        C("Tres copas. Ya hablan de los First Callers como una dinastía.");
                        Y("Y apenas estamos empezando.");
                        break;
                    case Welcome:
                        C("¡Eh, tú! Sí, tú. ¿Juegas?");
                        C("Soy la entrenadora Dee. Armo un equipo que anuncia sus tiros antes de lanzarlos: los First Callers.");
                        Y("¿Y qué necesito para entrar?");
                        C("Dos minutos de práctica. Te enseño a tirar, pasar y defender. Luego, a la cancha.");
                        break;
                    case Season2:
                        C("Temporada dos. Ya no somos los nuevos, " + me + ". Ahora todos nos tienen estudiados.");
                        C("Y corre la voz de un equipo nuevo en el muelle del ferrocarril. Juegan al atardecer y no pierden.");
                        Y("Que nos estudien. Nosotros seguimos anunciando el tiro.");
                        break;
                    case Rival2Intro:
                        K("¿Los First Callers? Pensé que serían más altos.");
                        K("El Sundown Syndicate no grita. Llegamos, ganamos, y nos vamos antes de que se apaguen las luces.");
                        Y("Entonces no te vayas temprano.");
                        C("Kaia lee el juego como un libro. Muévete sin balón y no le des tiempo.");
                        break;
                    case Rival2Beaten:
                        K("...Bien jugado. De verdad.");
                        K("Pero el sol sale mañana otra vez, " + me + ". Y nosotros también.");
                        C("Eso es respeto. Te lo ganaste.");
                        break;
                    case TwoTime:
                        C("Dos copas. Ya no es suerte, es un legado.");
                        Y("¿Y ahora qué, entrenadora?");
                        C("Ahora defendemos el trono. Cada temporada. Contra todos.");
                        break;
                    case Intro:
                        C("Así que tú eres " + me + ". Dicen que anuncias tu tiro antes de lanzarlo.");
                        C("Cinco equipos callejeros mandan en el Blacktop Circuit. Gánales a los cinco y la Caller League tendrá que abrirnos la puerta.");
                        Y("Entonces, a tocar puertas.");
                        C("Ese es el espíritu. Primero, los Cage Regulars. No les gustan las visitas.");
                        break;
                    case CircuitCleared:
                        C("Cinco de cinco. Todo el asfalto habla de los First Callers.");
                        C("La Caller League es otra cosa. Diez partidos y los cuatro mejores van a playoffs.");
                        Y("¿Y la Gold Signal Cup?");
                        C("Gana dos partidos de playoffs y es nuestra. Paso a paso.");
                        break;
                    case RivalIntro:
                        V("Así que estos son los famosos First Callers. Qué tiernos.");
                        V("Neon Static mandaba en esta ciudad antes de que aprendieras a botar. Ven al Static Lot y demuéstralo.");
                        Y("Dime la hora.");
                        C("No hagas caso, " + me + ". Aunque... callarlos se sentiría bien.");
                        break;
                    case RivalBeaten:
                        V("...Noche de suerte. No te acostumbres.");
                        C("¿Suerte? Escuchaste a la gente. Eso fue todo tuyo.");
                        break;
                    case RivalLost:
                        V("Static gana. Static siempre gana.");
                        C("Olvídalo. Volverán la próxima temporada, y nosotros también.");
                        break;
                    case Playoffs:
                        C("Playoffs. Todo lo que construimos se decide en dos partidos.");
                        Y("Entonces anunciamos el tiro.");
                        break;
                    case Champions:
                        C("Campeones de la Gold Signal Cup. De la jaula a lo más alto de la liga.");
                        V("Disfrútalo mientras dure, " + me + ". La próxima temporada es nuestra.");
                        Y("Aquí los esperamos.");
                        break;
                    default:
                        return null;
                }
                return b;
            }
            switch (id)
            {
                case Rival12Intro:
                    Q("Every shot here gets measured before it's let go, " + me + ". Like a putt on the eighteenth.");
                    Q("The Putt Club doesn't run. We wait for the perfect look, and we don't miss it.");
                    Y("Then tonight I'm rushing you.");
                    C("Bex won't shoot until it's clean. Pressure early in the possession and make her decide.");
                    break;
                case Rival12Beaten:
                    Q("...Not one clean look all night. Well played.");
                    C("Twelve rival crews, " + me + ". A full dozen. This city's yours now.");
                    break;
                case Rival11Intro:
                    I("It's always windy up on the bluff, " + me + ". We shoot with it at our backs.");
                    I("The Sky Kites shoot from anywhere. Give us room and the ball never comes down.");
                    Y("Then tonight we cut the string.");
                    C("Rin shoots from way out. Close out to the line and don't let him get his arm up.");
                    break;
                case Rival11Beaten:
                    I("...The wind dropped. First time all summer.");
                    C("Eleven rival crews, " + me + ". You're not beating them any more, you're teaching them.");
                    break;
                case Rival10Intro:
                    O("We play while the city sleeps, " + me + ". Between the wash and the dry.");
                    O("At the Wash House the ball never stops. Pass, cut, pass. You'll be dizzy by halftime.");
                    Y("Then somebody's folding laundry tonight.");
                    C("Opal never holds the ball for a second. Guard the passing lanes and don't get lost on the cuts.");
                    break;
                case Rival10Beaten:
                    O("...Cycle's done. You cleaned us out tonight.");
                    C("Ten rival crews, " + me + ". Ten. There isn't a court in this city that doesn't know your name.");
                    break;
                case Rival9Intro:
                    X("The rink closes at eleven, " + me + ". The last skate is ours.");
                    X("The Roller Royals press for all four minutes. Put it on the floor and we take it.");
                    Y("Then tonight I'm not skating. I'm passing.");
                    C("Skye presses the whole floor. Move it fast, don't dribble sideways, and find whoever's left open.");
                    break;
                case Rival9Beaten:
                    X("...They turned the rink lights on. Night's over.");
                    C("Nine rival crews, " + me + ". There's nobody left in this city who hasn't seen you play.");
                    break;
                case Rival8Intro:
                    N("The market closes at midnight, " + me + ". That's when we open.");
                    N("The Night Lanterns don't chase anybody. We close the zone and throw it over the top.");
                    Y("Then tonight I'm turning the lights off.");
                    C("Juno plays a zone and looks for the lob. Shoot from the corners and punish the gaps.");
                    break;
                case Rival8Beaten:
                    N("...They all went out. I can't remember the last time.");
                    C("Eight rival crews, " + me + ". There's no corner of this city that doesn't know you now.");
                    break;
                case Rival7Intro:
                    R("Sign here, " + me + ". We're delivering you a loss, door to door.");
                    R("The Comet Couriers never stop. We're gone up the floor before you turn around.");
                    Y("Then there's no delivery today.");
                    C("Remy runs the break off every rebound. Get back on defense before you celebrate.");
                    break;
                case Rival7Beaten:
                    R("...Returned to sender. That never happens.");
                    C("Seven rival crews, " + me + ". There's nobody left in this city who hasn't seen you win.");
                    break;
                case Rival6Intro:
                    W("From the tower you can see the whole city, " + me + ". We saw you coming six seasons ago.");
                    W("The Lighthouse Keepers don't chase anybody. We wait, and the light tells us where you're going.");
                    Y("Then switch it off. I'll play in the dark.");
                    C("Wren presses the whole floor. Get the ball out of the trap fast and find the shooter.");
                    break;
                case Rival6Beaten:
                    W("...The light couldn't keep up. You were quicker than the beam.");
                    C("Six rivals. There's no tower left in this city that doesn't know your name, " + me + ".");
                    break;
                case Rival5Intro:
                    E("We've got every one of your games on tape, " + me + ". Side A, side B.");
                    E("The Cassette Club doesn't improvise. We rewind and play it again until it's perfect.");
                    Y("Then I'll change the song.");
                    C("Echo lets you shoot from outside and packs the paint. Attack the rim and move the ball fast.");
                    break;
                case Rival5Beaten:
                    E("...Okay. We'll be replaying that tape for a long time.");
                    C("Five rival crews, five stories. Every one of them ends with you, " + me + ".");
                    break;
                case Rival4Intro:
                    J("We started watching you three seasons ago, " + me + ". We took notes on every game.");
                    J("The Paper Cranes don't have stars. We have a plan, folded a thousand times.");
                    Y("Plans tear when somebody pushes on them.");
                    C("Juno reads the floor before anyone else. Change your pace and don't run the same play twice.");
                    break;
                case Rival4Beaten:
                    J("...Fine. We'll fold a new plan.");
                    C("You've met every rival in this city now. And every one of them knows your name.");
                    break;
                case FourCups:
                    C("Four cups. They're not talking about the First Callers anymore. They're talking about you, " + me + ".");
                    Y("Then let's talk less and play more.");
                    break;
                case Rival3Intro:
                    M("We came in on the last ferry. Nobody saw us coming.");
                    M("The Midnight Tide doesn't rush. We wait for you to tire, then we rise. Like the tide.");
                    Y("Then I won't get tired.");
                    C("Mara plays both ends of the floor. Protect the ball and run when you can.");
                    break;
                case Rival3Beaten:
                    M("...The tide comes back, " + me + ". It always comes back.");
                    C("Let it. We'll be right here.");
                    break;
                case ThreePeat:
                    C("Three cups. People are calling the First Callers a dynasty now.");
                    Y("And we're just getting started.");
                    break;
                case Welcome:
                    C("Hey, you! Yeah, you. You hoop?");
                    C("I'm Coach Dee. I'm putting together a crew that calls its shots before it takes them: the First Callers.");
                    Y("So what does it take to get in?");
                    C("Two minutes of practice. I'll show you how to shoot, pass, and defend. Then we hit the courts.");
                    break;
                case Season2:
                    C("Season two. We're not the new kids anymore, " + me + ". Everybody's got film on us now.");
                    C("And there's word of a new crew down at the rail yard. They play at sundown and they don't lose.");
                    Y("Let them study. We'll keep calling our shots.");
                    break;
                case Rival2Intro:
                    K("The First Callers? Thought you'd be taller.");
                    K("The Sundown Syndicate doesn't shout. We show up, we win, and we're gone before the lights come on.");
                    Y("Then don't leave early.");
                    C("Kaia reads the floor like a book. Move without the ball and don't give her time.");
                    break;
                case Rival2Beaten:
                    K("...Good game. I mean it.");
                    K("But the sun comes up again tomorrow, " + me + ". So do we.");
                    C("That's respect. You earned it.");
                    break;
                case TwoTime:
                    C("Two cups. That's not luck anymore. That's a legacy.");
                    Y("So what now, Coach?");
                    C("Now we defend the throne. Every season. Against everybody.");
                    break;
                case Intro:
                    C("So you're " + me + ". Heard you call your shot before you take it.");
                    C("Five street crews run the Blacktop Circuit. Beat all five and the Caller League has to let us in.");
                    Y("Then let's start knocking.");
                    C("That's the spirit. Cage Regulars first. They don't like visitors.");
                    break;
                case CircuitCleared:
                    C("Five for five. The whole blacktop is talking about the First Callers.");
                    C("The Caller League is a different animal. Ten games, top four make the playoffs.");
                    Y("And the Gold Signal Cup?");
                    C("Win two playoff games and it's ours. One step at a time.");
                    break;
                case RivalIntro:
                    V("So these are the famous First Callers. Cute.");
                    V("Neon Static ran this city before you learned to dribble. Come to the Static Lot and prove it.");
                    Y("Name the time.");
                    C("Ignore the noise, " + me + ". But... it would feel good to shut them up.");
                    break;
                case RivalBeaten:
                    V("...Lucky night. Don't get used to it.");
                    C("Lucky? You heard that crowd. That was all you.");
                    break;
                case RivalLost:
                    V("Static wins. Static always wins.");
                    C("Shake it off. They'll be back next season, and so will we.");
                    break;
                case Playoffs:
                    C("Playoffs. Everything we've built comes down to two games.");
                    Y("Then we call our shot.");
                    break;
                case Champions:
                    C("Gold Signal Cup champions. From the cage to the top of the league.");
                    V("Enjoy it while it lasts, " + me + ". Next season, it's ours.");
                    Y("We'll be waiting.");
                    break;
                default:
                    return null;
            }
            return b;
        }

        /// <summary>The next scene to play in the Rise hub, or null.</summary>
        public static string Pending(CareerSaveData d)
        {
            if (d == null) return null;
            bool Seen(string id) => d.storySeen.Contains(id);
            var r = d.rise;
            if (!Seen(Intro)) return Intro;
            if (r.stage != RiseStage.Circuit && !Seen(CircuitCleared)) return CircuitCleared;
            int season = r.season != null ? r.season.seasonNumber : 0;
            bool sundownSeason = RivalEngine.RivalFor(season) == DefaultContent.Rival2CrewId;
            bool tideSeason = RivalEngine.RivalFor(season) == DefaultContent.Rival3CrewId;
            bool cranesSeason = RivalEngine.RivalFor(season) == DefaultContent.Rival4CrewId;
            bool cassetteSeason = RivalEngine.RivalFor(season) == DefaultContent.Rival5CrewId;
            bool keeperSeason = RivalEngine.RivalFor(season) == DefaultContent.Rival6CrewId;
            bool courierSeason = RivalEngine.RivalFor(season) == DefaultContent.Rival7CrewId;
            bool lanternSeason = RivalEngine.RivalFor(season) == DefaultContent.Rival8CrewId;
            bool royalSeason = RivalEngine.RivalFor(season) == DefaultContent.Rival9CrewId;
            bool washSeason = RivalEngine.RivalFor(season) == DefaultContent.Rival10CrewId;
            bool kiteSeason = RivalEngine.RivalFor(season) == DefaultContent.Rival11CrewId;
            bool puttSeason = RivalEngine.RivalFor(season) == DefaultContent.Rival12CrewId;
            if (season >= 2 && r.stage == RiseStage.Season && !Seen(Season2)) return Season2;
            if (RivalEngine.ChallengeAvailable(d) && puttSeason && !Seen(Rival12Intro)) return Rival12Intro;
            if (d.rival.puttWins >= 1 && !Seen(Rival12Beaten)) return Rival12Beaten;
            if (RivalEngine.ChallengeAvailable(d) && kiteSeason && !Seen(Rival11Intro)) return Rival11Intro;
            if (d.rival.kiteWins >= 1 && !Seen(Rival11Beaten)) return Rival11Beaten;
            if (RivalEngine.ChallengeAvailable(d) && washSeason && !Seen(Rival10Intro)) return Rival10Intro;
            if (d.rival.washWins >= 1 && !Seen(Rival10Beaten)) return Rival10Beaten;
            if (RivalEngine.ChallengeAvailable(d) && royalSeason && !Seen(Rival9Intro)) return Rival9Intro;
            if (d.rival.royalWins >= 1 && !Seen(Rival9Beaten)) return Rival9Beaten;
            if (RivalEngine.ChallengeAvailable(d) && lanternSeason && !Seen(Rival8Intro)) return Rival8Intro;
            if (d.rival.lanternWins >= 1 && !Seen(Rival8Beaten)) return Rival8Beaten;
            if (RivalEngine.ChallengeAvailable(d) && courierSeason && !Seen(Rival7Intro)) return Rival7Intro;
            if (d.rival.courierWins >= 1 && !Seen(Rival7Beaten)) return Rival7Beaten;
            if (RivalEngine.ChallengeAvailable(d) && keeperSeason && !Seen(Rival6Intro)) return Rival6Intro;
            if (d.rival.keeperWins >= 1 && !Seen(Rival6Beaten)) return Rival6Beaten;
            if (RivalEngine.ChallengeAvailable(d) && cassetteSeason && !Seen(Rival5Intro)) return Rival5Intro;
            if (d.rival.cassetteWins >= 1 && !Seen(Rival5Beaten)) return Rival5Beaten;
            if (RivalEngine.ChallengeAvailable(d) && cranesSeason && !Seen(Rival4Intro)) return Rival4Intro;
            if (d.rival.cranesWins >= 1 && !Seen(Rival4Beaten)) return Rival4Beaten;
            if (RivalEngine.ChallengeAvailable(d) && tideSeason && !Seen(Rival3Intro)) return Rival3Intro;
            if (d.rival.tideWins >= 1 && !Seen(Rival3Beaten)) return Rival3Beaten;
            if (RivalEngine.ChallengeAvailable(d) && !sundownSeason && !tideSeason && !cranesSeason && !cassetteSeason && !keeperSeason && !courierSeason && !lanternSeason && !royalSeason && !washSeason && !kiteSeason && !puttSeason && !Seen(RivalIntro)) return RivalIntro;
            if (RivalEngine.ChallengeAvailable(d) && sundownSeason && !Seen(Rival2Intro)) return Rival2Intro;
            if (d.rival.sundownWins >= 1 && !Seen(Rival2Beaten)) return Rival2Beaten;
            if (RivalEngine.StaticWins(d) >= 1 && !Seen(RivalBeaten)) return RivalBeaten;
            if (d.rival.losses >= 1 && d.rival.wins == 0 && !sundownSeason && !tideSeason && !cranesSeason && !cassetteSeason && !keeperSeason && !courierSeason && !lanternSeason && !royalSeason && !washSeason && !kiteSeason && !puttSeason && !Seen(RivalLost)) return RivalLost;
            if (r.stage == RiseStage.Playoffs && !Seen(Playoffs)) return Playoffs;
            if (d.totals.championships >= 1 && !Seen(Champions)) return Champions;
            if (d.totals.championships >= 2 && !Seen(TwoTime)) return TwoTime;
            if (d.totals.championships >= 3 && !Seen(ThreePeat)) return ThreePeat;
            if (d.totals.championships >= 4 && !Seen(FourCups)) return FourCups;
            return null;
        }

        public static void MarkSeen(CareerSaveData d, string id)
        {
            if (d != null && !string.IsNullOrEmpty(id) && !d.storySeen.Contains(id)) d.storySeen.Add(id);
        }

        public static readonly string[] AllIds =
        {
            Intro, CircuitCleared, RivalIntro, RivalBeaten, RivalLost, Playoffs, Champions,
            Season2, Rival2Intro, Rival2Beaten, TwoTime, Welcome, Rival3Intro, Rival3Beaten, ThreePeat,
            Rival4Intro, Rival4Beaten, FourCups, Rival5Intro, Rival5Beaten, Rival6Intro, Rival6Beaten, Rival7Intro, Rival7Beaten, Rival8Intro, Rival8Beaten,
            Rival9Intro, Rival9Beaten, Rival10Intro, Rival10Beaten,
            Rival11Intro, Rival11Beaten, Rival12Intro, Rival12Beaten,
        };
    }
}
