using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public enum LoadStatus
    {
        /// <summary>Loaded normally.</summary>
        Ok = 0,
        /// <summary>No save existed; a fresh career was created.</summary>
        New = 1,
        /// <summary>An older save version was upgraded.</summary>
        Migrated = 2,
        /// <summary>The file was unreadable; a fresh career was created (the bad file should be kept as a backup).</summary>
        Recovered = 3,
    }

    /// <summary>
    /// Versioned JSON encoding for <see cref="CareerSaveData"/>. Reading is forgiving: missing
    /// or wrongly-typed fields fall back to defaults instead of failing, and anything unreadable
    /// produces a fresh career with <see cref="LoadStatus.Recovered"/>.
    /// </summary>
    public static partial class SaveCodec
    {
        public static string Encode(CareerSaveData d)
        {
            var o = new Dictionary<string, object>
            {
                ["version"] = CareerSaveData.CurrentVersion,
                ["nickname"] = d.nickname,
                ["signalPoints"] = d.signalPoints,
                ["fans"] = d.fans,
                ["gamesSinceUpgrade"] = d.gamesSinceUpgrade,
                ["upgrades"] = List(d.upgrades, u => new Dictionary<string, object> { ["id"] = u.id, ["level"] = u.level }),
                ["ownedCosmetics"] = Strings(d.ownedCosmetics),
                ["equipped"] = new Dictionary<string, object>
                {
                    ["jersey"] = d.equippedJersey, ["shoes"] = d.equippedShoes, ["banner"] = d.equippedBanner,
                    ["celebration"] = d.equippedCelebration, ["move"] = d.equippedMove, ["dunk"] = d.equippedDunk,
                },
                ["totals"] = new Dictionary<string, object>
                {
                    ["games"] = d.totals.games, ["wins"] = d.totals.wins, ["losses"] = d.totals.losses,
                    ["points"] = d.totals.points, ["assists"] = d.totals.assists, ["rebounds"] = d.totals.rebounds,
                    ["steals"] = d.totals.steals, ["blocks"] = d.totals.blocks, ["fgm"] = d.totals.fieldGoalsMade,
                    ["fga"] = d.totals.fieldGoalsAttempted, ["greens"] = d.totals.greens, ["championships"] = d.totals.championships,
                    ["oops"] = d.totals.alleyOops, ["heatUps"] = d.totals.heatUps, ["versus"] = d.totals.versusGames,
                },
                ["practice"] = new Dictionary<string, object>
                {
                    ["freeShootMakes"] = d.practice.freeShootMakes, ["freeShootStreak"] = d.practice.freeShootStreak,
                    ["passingScore"] = d.practice.passingScore, ["dribbleLaneTime"] = (double)d.practice.dribbleLaneTime,
                    ["threePointBest"] = d.practice.threePointBest, ["lockdownBest"] = d.practice.lockdownBest,
                    ["shootoutWins"] = d.practice.shootoutWins,
                    ["aroundWorld"] = (double)d.practice.aroundWorldTime, ["horseWins"] = d.practice.horseWins,
                },
                ["settings"] = new Dictionary<string, object>
                {
                    ["musicVolume"] = (double)d.settings.musicVolume, ["sfxVolume"] = (double)d.settings.sfxVolume,
                    ["haptics"] = d.settings.haptics, ["screenShake"] = d.settings.screenShake,
                    ["uiScale"] = (double)d.settings.uiScale, ["colorblindContrast"] = d.settings.colorblindContrast,
                    ["difficultyId"] = d.settings.difficultyId,
                    ["gameCenter"] = d.settings.gameCenter,
                    ["leftHanded"] = d.settings.leftHanded,
                    ["largeButtons"] = d.settings.largeButtons,
                    ["controlLayout"] = d.settings.controlLayout ?? "",
                    ["landscapeAll"] = d.settings.landscapeAll,
                    ["commentary"] = d.settings.commentary,
                    ["tapToShoot"] = d.settings.tapToShoot,
                    ["reduceMotion"] = d.settings.reduceMotion,
                },
                ["rise"] = EncodeRise(d.rise),
                ["classic"] = EncodeClassic(d.classic),
                ["cup"] = EncodeCup(d.cup),
                ["dynasty"] = EncodeDynasty(d.dynasty ?? new DynastySaveData()),
                ["daily"] = new Dictionary<string, object>
                {
                    ["lastCompletedDay"] = (d.daily ?? new DailySaveData()).lastCompletedDay,
                    ["streak"] = (d.daily ?? new DailySaveData()).streak,
                    ["bestStreak"] = (d.daily ?? new DailySaveData()).bestStreak,
                    ["completed"] = (d.daily ?? new DailySaveData()).completed,
                },
                ["tutorialDone"] = d.tutorialDone,
                ["king"] = new Dictionary<string, object>
                {
                    ["order"] = Strings((d.king ?? new KingSaveData()).order),
                    ["index"] = (d.king ?? new KingSaveData()).index,
                    ["streak"] = (d.king ?? new KingSaveData()).streak,
                    ["best"] = (d.king ?? new KingSaveData()).best,
                    ["active"] = (d.king ?? new KingSaveData()).active,
                    ["runs"] = (d.king ?? new KingSaveData()).runs,
                    ["home"] = (d.king ?? new KingSaveData()).homeTeamId,
                },
                ["language"] = d.settings.language,
                ["display"] = new Dictionary<string, object>
                {
                    ["crt"] = d.settings.crt,
                    ["highFrameRate"] = d.settings.highFrameRate,
                    ["attractMode"] = d.settings.attractMode,
                    ["showFps"] = d.settings.showFps,
                    ["coachTips"] = d.settings.coachTips,
                    ["colorFilter"] = d.settings.colorFilter,
                    ["captions"] = d.settings.captions,
                    ["icloud"] = d.settings.icloudSync,
                    ["systemText"] = d.settings.followSystemText,
                    ["modeDifficulty"] = Strings(d.settings.modeDifficulty),
                    ["musicMenu"] = d.settings.musicMenu,
                    ["musicGame"] = d.settings.musicGame,
                },
                ["secrets"] = EncodeSecrets(d.secrets ?? new SecretsSaveData()),
                ["customTeam"] = EncodeTeam(d.customTeam ?? new CustomTeamData()),
                ["kits"] = EncodeKits(d.kits ?? new KitSaveData()),
                ["franchise"] = EncodeFranchise(d.franchise),
                ["allStar"] = EncodeAllStar(d.allStar),
                ["courts"] = EncodeCourts(d.courts),
                ["legacy"] = EncodeLegacy(d.legacy),
                ["street"] = EncodeStreet(d.street),
                ["clutch"] = EncodeClutch(d.clutch),
                ["login"] = EncodeLogin(d),
                ["feel"] = EncodeFeel(d.settings),
                ["pro"] = EncodePro(d),
                ["beta"] = EncodeBeta(d.beta),
                ["phone"] = EncodePhone(d.phone),
                ["customCup"] = EncodeCustomCup(d.customCup),
                ["weekly"] = EncodeWeekly(d.weekly),
                ["pass"] = EncodePass(d.pass),
                ["story"] = EncodeStory(d.story),
                ["couch"] = EncodeCouch(d.couch),
                ["gauntlet"] = EncodeGauntlet(d.gauntlet),
                ["live"] = EncodeLive(d.live),
                ["flagged"] = d.saveFlagged,
                ["photos"] = d.photosTaken,
                ["shots"] = ShotCharts.Encode(d.shotChart),
                ["rival"] = new Dictionary<string, object>
                {
                    ["wins"] = (d.rival ?? new RivalSaveData()).wins,
                    ["sundownWins"] = (d.rival ?? new RivalSaveData()).sundownWins,
                    ["tideWins"] = (d.rival ?? new RivalSaveData()).tideWins,
                    ["cranesWins"] = (d.rival ?? new RivalSaveData()).cranesWins,
                    ["cassetteWins"] = (d.rival ?? new RivalSaveData()).cassetteWins,
                    ["keeperWins"] = (d.rival ?? new RivalSaveData()).keeperWins,
                    ["courierWins"] = (d.rival ?? new RivalSaveData()).courierWins,
                    ["lanternWins"] = (d.rival ?? new RivalSaveData()).lanternWins,
                    ["royalWins"] = (d.rival ?? new RivalSaveData()).royalWins,
                    ["washWins"] = (d.rival ?? new RivalSaveData()).washWins,
                    ["kiteWins"] = (d.rival ?? new RivalSaveData()).kiteWins,
                    ["puttWins"] = (d.rival ?? new RivalSaveData()).puttWins,
                    ["losses"] = (d.rival ?? new RivalSaveData()).losses,
                    ["lastSeason"] = (d.rival ?? new RivalSaveData()).lastSeason,
                },
                ["badgesSeen"] = Strings(d.badgesSeen ?? new List<string>()),
                ["storySeen"] = Strings(d.storySeen ?? new List<string>()),
                ["tipsSeen"] = Strings(d.tipsSeen ?? new List<string>()),
                ["movesFound"] = Strings(d.movesFound ?? new List<string>()),
                ["customPlayer"] = EncodeCustom(d.customPlayer ?? new CustomPlayerData()),
                ["records"] = EncodeRecords(d.records ?? new CareerRecords()),
                ["history"] = List(d.history ?? new List<MatchHistoryEntry>(), h => new Dictionary<string, object>
                {
                    ["day"] = h.day, ["mode"] = (int)h.mode, ["opp"] = h.opponentId, ["for"] = h.scoreFor,
                    ["against"] = h.scoreAgainst, ["pts"] = h.points, ["ast"] = h.assists, ["reb"] = h.rebounds,
                }),
                ["seasons"] = List(d.seasons ?? new List<SeasonHistoryEntry>(), e => new Dictionary<string, object>
                {
                    ["season"] = e.season, ["games"] = e.games, ["wins"] = e.wins, ["losses"] = e.losses,
                    ["pts"] = e.points, ["ast"] = e.assists, ["reb"] = e.rebounds, ["result"] = e.result,
                }),
                ["appliedMatchIds"] = Strings(d.appliedMatchIds),
            };
            return MiniJson.Write(o);
        }

        private static Dictionary<string, object> EncodeSecrets(SecretsSaveData s)
        {
            var a = s.arcade ?? new ArcadeSaveData();
            return new Dictionary<string, object>
            {
                ["codes"] = Strings(s.codesFound ?? new List<string>()),
                ["hints"] = Strings(s.hintsRevealed ?? new List<string>()),
                ["unlocked"] = Strings(s.unlocked ?? new List<string>()),
                ["bigHeads"] = s.bigHeads,
                ["rainbowBall"] = s.rainbowBall,
                ["alwaysHeat"] = s.alwaysHeat,
                ["pocketGreen"] = s.pocketGreen,
                ["skyHigh"] = s.skyHigh,
                ["arcade"] = new Dictionary<string, object>
                {
                    ["active"] = a.active, ["rung"] = a.rung, ["continues"] = a.continues, ["home"] = a.homeTeamId,
                    ["clears"] = a.clears, ["bestRung"] = a.bestRung, ["runs"] = a.runs,
                },
            };
        }

        private static Dictionary<string, object> EncodeTeam(CustomTeamData t) => new Dictionary<string, object>
        {
            ["created"] = t.created, ["city"] = t.city, ["name"] = t.nickname, ["abbr"] = t.abbreviation,
            ["primary"] = t.primary, ["secondary"] = t.secondary, ["accent"] = t.accent, ["shorts"] = t.shorts, ["shoes"] = t.shoes,
            ["pattern"] = t.pattern, ["shape"] = t.logoShape, ["motif"] = t.logoMotif, ["court"] = t.homeCourtId, ["rise"] = t.useInRise,
        };

        // Kits are stored as their share codes (compact, versioned, checksummed) plus a name.
        private static Dictionary<string, object> EncodeKits(KitSaveData k)
        {
            var slots = new List<object>();
            if (k.slots != null)
                foreach (var kit in k.slots)
                    if (kit != null) slots.Add(new Dictionary<string, object> { ["name"] = kit.name, ["code"] = Kits.Encode(kit.Clone()) });
            return new Dictionary<string, object> { ["designed"] = k.designed, ["wear"] = k.wear, ["autoAway"] = k.autoAway, ["slots"] = slots };
        }

        private static KitSaveData DecodeKits(Dictionary<string, object> o)
        {
            var k = new KitSaveData();
            if (o == null) return k;
            k.designed = Bool(o, "designed", false);
            k.wear = Math.Max(0, Math.Min(Kits.SlotCount - 1, Int(o, "wear", 0)));
            k.autoAway = Bool(o, "autoAway", true);
            foreach (var item in Arr(o, "slots"))
            {
                if (!(item is Dictionary<string, object> slot)) continue;
                if (!Kits.TryDecode(Str(slot, "code", null), out var kit)) continue;
                kit.name = Str(slot, "name", Kits.SlotNames[Math.Min(k.slots.Count, Kits.SlotCount - 1)]);
                Kits.Clamp(kit);
                k.slots.Add(kit);
            }
            // A save with missing or unreadable kits goes back to the old look rather than half a set.
            if (k.slots.Count != Kits.SlotCount)
            {
                k.slots.Clear();
                k.designed = false;
            }
            return k;
        }

        private static CustomTeamData DecodeTeam(Dictionary<string, object> o)
        {
            var d = new CustomTeamData();
            if (o == null) return d;
            d.created = Bool(o, "created", false);
            d.city = Str(o, "city", "");
            d.nickname = Str(o, "name", d.nickname);
            d.abbreviation = Str(o, "abbr", d.abbreviation);
            d.primary = Int(o, "primary", d.primary);
            d.secondary = Int(o, "secondary", d.secondary);
            d.accent = Int(o, "accent", d.accent);
            d.shorts = Int(o, "shorts", d.shorts);
            d.shoes = Int(o, "shoes", d.shoes);
            d.pattern = Int(o, "pattern", 0);
            d.logoShape = Int(o, "shape", 0);
            d.logoMotif = Int(o, "motif", d.logoMotif);
            d.homeCourtId = Str(o, "court", d.homeCourtId);
            d.useInRise = Bool(o, "rise", false);
            return d;
        }

        private static SecretsSaveData DecodeSecrets(Dictionary<string, object> o)
        {
            var a = Obj(o, "arcade");
            return new SecretsSaveData
            {
                codesFound = StrList(o, "codes"),
                hintsRevealed = StrList(o, "hints"),
                unlocked = StrList(o, "unlocked"),
                bigHeads = Bool(o, "bigHeads", false),
                rainbowBall = Bool(o, "rainbowBall", false),
                alwaysHeat = Bool(o, "alwaysHeat", false),
                pocketGreen = Bool(o, "pocketGreen", false),
                skyHigh = Bool(o, "skyHigh", false),
                arcade = new ArcadeSaveData
                {
                    active = Bool(a, "active", false),
                    rung = Math.Max(0, Math.Min(ArcadeEngine.Rungs - 1, Int(a, "rung", 0))),
                    continues = Math.Max(0, Math.Min(ArcadeEngine.Continues, Int(a, "continues", 0))),
                    homeTeamId = Str(a, "home", null),
                    clears = Math.Max(0, Int(a, "clears", 0)),
                    bestRung = Math.Max(0, Math.Min(ArcadeEngine.Rungs, Int(a, "bestRung", 0))),
                    runs = Math.Max(0, Int(a, "runs", 0)),
                },
            };
        }

        private static Dictionary<string, object> EncodeGame(ScheduledGame g) => new Dictionary<string, object>
        {
            ["week"] = g.week, ["round"] = g.round, ["home"] = g.homeId, ["away"] = g.awayId,
            ["played"] = g.played, ["homeScore"] = g.homeScore, ["awayScore"] = g.awayScore,
        };

        private static ScheduledGame DecodeGame(Dictionary<string, object> g) => new ScheduledGame
        {
            week = Int(g, "week", 0), round = Int(g, "round", 0),
            homeId = Str(g, "home", null), awayId = Str(g, "away", null),
            played = Bool(g, "played", false), homeScore = Int(g, "homeScore", 0), awayScore = Int(g, "awayScore", 0),
        };

        private static object EncodeClassic(ClassicSaveData t)
        {
            t = t ?? new ClassicSaveData();
            return new Dictionary<string, object>
            {
                ["seeds"] = Strings(t.seeds),
                ["games"] = List(t.games, EncodeGame),
                ["edition"] = t.edition,
                ["championId"] = t.championId,
                ["finished"] = t.finished,
                ["titles"] = t.titles,
            };
        }

        private static ClassicSaveData DecodeClassic(Dictionary<string, object> o)
        {
            var t = new ClassicSaveData();
            if (o == null) return t;
            t.seeds = StrList(o, "seeds");
            foreach (var item in Arr(o, "games"))
                if (item is Dictionary<string, object> g) t.games.Add(DecodeGame(g));
            t.edition = Math.Max(0, Int(o, "edition", 0));
            t.championId = Str(o, "championId", null);
            t.finished = Bool(o, "finished", false);
            t.titles = Math.Max(0, Int(o, "titles", 0));
            // A bracket that isn't four teams is unusable: drop it (titles are kept).
            if (t.seeds.Count != 4) { t.seeds.Clear(); t.games.Clear(); t.finished = false; }
            return t;
        }

        private static object EncodeDynasty(DynastySaveData y) => new Dictionary<string, object>
        {
            ["year"] = y.year,
            ["last"] = y.lastSeasonProcessed,
            ["players"] = List(y.players, p => new Dictionary<string, object>
            {
                ["id"] = p.id, ["age"] = p.age, ["delta"] = p.delta, ["peak"] = p.peak, ["seasons"] = p.seasons, ["retired"] = p.retired,
            }),
            ["rookies"] = List(y.rookies, r => new Dictionary<string, object>
            {
                ["id"] = r.id, ["first"] = r.first, ["last"] = r.last, ["number"] = r.number, ["arch"] = r.archetype,
                ["skin"] = r.skin, ["hair"] = r.hair, ["hairColor"] = r.hairColor, ["body"] = r.body, ["height"] = r.height, ["offset"] = r.offset,
            }),
            ["swaps"] = List(y.swaps, s => new Dictionary<string, object> { ["team"] = s.teamId, ["old"] = s.oldId, ["new"] = s.newId }),
            ["hall"] = List(y.hall, h => new Dictionary<string, object>
            {
                ["name"] = h.name, ["team"] = h.team, ["peak"] = h.peak, ["seasons"] = h.seasons, ["year"] = h.year,
            }),
            ["champions"] = List(y.champions, ch => new Dictionary<string, object> { ["season"] = ch.season, ["team"] = ch.teamId }),
            ["draft"] = Strings(y.draftPool),
        };

        private static DynastySaveData DecodeDynasty(Dictionary<string, object> o)
        {
            var y = new DynastySaveData();
            if (o == null) return y;
            y.year = Math.Max(0, Int(o, "year", 0));
            y.lastSeasonProcessed = Math.Max(0, Int(o, "last", 0));
            foreach (var item in Arr(o, "players"))
                if (item is Dictionary<string, object> p && Str(p, "id", null) != null)
                    y.players.Add(new DynastyPlayer
                    {
                        id = Str(p, "id", null), age = Math.Max(16, Math.Min(50, Int(p, "age", 25))), delta = Math.Max(-12, Math.Min(10, Int(p, "delta", 0))),
                        peak = Int(p, "peak", 0), seasons = Math.Max(0, Int(p, "seasons", 0)), retired = Bool(p, "retired", false),
                    });
            foreach (var item in Arr(o, "rookies"))
                if (item is Dictionary<string, object> r && Str(r, "id", null) != null)
                    y.rookies.Add(new RookieData
                    {
                        id = Str(r, "id", null), first = Str(r, "first", "Rookie"), last = Str(r, "last", ""), number = Math.Max(0, Math.Min(99, Int(r, "number", 0))),
                        archetype = Math.Max(0, Math.Min(11, Int(r, "arch", 0))), skin = Int(r, "skin", 0), hair = Int(r, "hair", 0),
                        hairColor = Int(r, "hairColor", 0), body = Math.Max(0, Math.Min(2, Int(r, "body", 1))), height = Math.Max(0, Math.Min(2, Int(r, "height", 1))),
                        offset = Math.Max(-10, Math.Min(5, Int(r, "offset", 0))),
                    });
            foreach (var item in Arr(o, "swaps"))
                if (item is Dictionary<string, object> s)
                    y.swaps.Add(new RosterSwap { teamId = Str(s, "team", null), oldId = Str(s, "old", null), newId = Str(s, "new", null) });
            foreach (var item in Arr(o, "hall"))
                if (item is Dictionary<string, object> h)
                    y.hall.Add(new HallEntry { name = Str(h, "name", "?"), team = Str(h, "team", ""), peak = Int(h, "peak", 0), seasons = Int(h, "seasons", 0), year = Int(h, "year", 0) });
            foreach (var item in Arr(o, "champions"))
                if (item is Dictionary<string, object> ch)
                    y.champions.Add(new ChampionEntry { season = Int(ch, "season", 0), teamId = Str(ch, "team", null) });
            y.draftPool = StrList(o, "draft");
            return y;
        }

        private static object EncodeCup(CupSaveData t)
        {
            t = t ?? new CupSaveData();
            return new Dictionary<string, object>
            {
                ["bracket"] = Strings(t.bracket), ["games"] = List(t.games, EncodeGame), ["home"] = t.homeTeamId,
                ["edition"] = t.edition, ["championId"] = t.championId, ["finished"] = t.finished, ["titles"] = t.titles,
            };
        }

        private static CupSaveData DecodeCup(Dictionary<string, object> o)
        {
            var t = new CupSaveData();
            if (o == null) return t;
            t.bracket = StrList(o, "bracket");
            foreach (var item in Arr(o, "games"))
                if (item is Dictionary<string, object> g) t.games.Add(DecodeGame(g));
            t.homeTeamId = Str(o, "home", null);
            t.edition = Math.Max(0, Int(o, "edition", 0));
            t.championId = Str(o, "championId", null);
            t.finished = Bool(o, "finished", false);
            t.titles = Math.Max(0, Int(o, "titles", 0));
            if (t.bracket.Count != CupEngine.Teams) { t.bracket.Clear(); t.games.Clear(); t.finished = false; }
            return t;
        }

        private static object EncodeCustom(CustomPlayerData p) => new Dictionary<string, object>
        {
            ["created"] = p.created, ["skin"] = p.skinTone, ["hairStyle"] = p.hairStyle, ["hairColor"] = p.hairColor,
            ["body"] = p.body, ["height"] = p.heightTier, ["jersey"] = p.jerseyNumber, ["archetype"] = p.archetypeId,
        };

        private static object EncodeRecords(CareerRecords r) => new Dictionary<string, object>
        {
            ["points"] = r.points, ["assists"] = r.assists, ["rebounds"] = r.rebounds, ["steals"] = r.steals,
            ["blocks"] = r.blocks, ["greens"] = r.greens, ["biggestWin"] = r.biggestWin,
            ["winStreak"] = r.winStreak, ["bestWinStreak"] = r.bestWinStreak,
        };

        private static object EncodeRise(RiseSaveData r)
        {
            return new Dictionary<string, object>
            {
                ["stage"] = (int)r.stage,
                ["circuitBeaten"] = Strings(r.circuitBeaten),
                ["energy"] = r.energy,
                ["chemistry"] = r.chemistry,
                ["seenEvents"] = Strings(r.seenEvents),
                ["pendingEventId"] = r.pendingEventId,
                ["seasonsPlayed"] = r.seasonsPlayed,
                ["teammates"] = Strings(r.teammates ?? new List<string>()),
                ["margins"] = Ints(r.recentMargins),
                ["recruitable"] = Strings(r.recruitable ?? new List<string>()),
                ["signed"] = Strings(r.signed ?? new List<string>()),
                ["made"] = EncodeRiseMade(r),
                ["nextMade"] = r.nextMade,
                ["season"] = r.season == null ? null : new Dictionary<string, object>
                {
                    ["seasonNumber"] = r.season.seasonNumber,
                    ["seed"] = (double)r.season.seed,
                    ["teamIds"] = Strings(r.season.teamIds),
                    ["currentWeek"] = r.season.currentWeek,
                    ["weeks"] = r.season.weeks,
                    ["championId"] = r.season.championId,
                    ["games"] = List(r.season.games, g => new Dictionary<string, object>
                    {
                        ["week"] = g.week, ["round"] = g.round, ["home"] = g.homeId, ["away"] = g.awayId,
                        ["played"] = g.played, ["homeScore"] = g.homeScore, ["awayScore"] = g.awayScore,
                    }),
                },
            };
        }

        /// <summary>Decodes a save. Never throws; check <paramref name="status"/>.</summary>
        public static CareerSaveData Decode(string json, ContentCatalog c, out LoadStatus status)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                status = LoadStatus.New;
                return Career.New(c);
            }
            try
            {
                if (!(MiniJson.Read(json) is Dictionary<string, object> o)) throw new MiniJson.JsonException("Root is not an object.");
                int version = Int(o, "version", 0);
                if (version <= 0) throw new MiniJson.JsonException("Missing version.");

                var d = new CareerSaveData
                {
                    nickname = Career.CleanNickname(Str(o, "nickname", "Rook")),
                    signalPoints = Math.Max(0, Int(o, "signalPoints", 0)),
                    fans = Math.Max(0, Int(o, "fans", 0)),
                    gamesSinceUpgrade = Math.Max(0, Int(o, "gamesSinceUpgrade", 99)),
                    ownedCosmetics = StrList(o, "ownedCosmetics"),
                    appliedMatchIds = StrList(o, "appliedMatchIds"),
                };
                foreach (var item in Arr(o, "upgrades"))
                {
                    if (!(item is Dictionary<string, object> u)) continue;
                    string id = Str(u, "id", null);
                    if (id != null) d.upgrades.Add(new UpgradeProgress { id = id, level = Math.Max(0, Int(u, "level", 0)) });
                }
                var eq = Obj(o, "equipped");
                d.equippedJersey = Str(eq, "jersey", null);
                d.equippedShoes = Str(eq, "shoes", null);
                d.equippedBanner = Str(eq, "banner", null);
                d.equippedCelebration = Str(eq, "celebration", null);
                d.equippedMove = Str(eq, "move", null);
                d.equippedDunk = Str(eq, "dunk", null);

                var t = Obj(o, "totals");
                d.totals = new CareerTotals
                {
                    games = Int(t, "games", 0), wins = Int(t, "wins", 0), losses = Int(t, "losses", 0),
                    points = Int(t, "points", 0), assists = Int(t, "assists", 0), rebounds = Int(t, "rebounds", 0),
                    steals = Int(t, "steals", 0), blocks = Int(t, "blocks", 0), fieldGoalsMade = Int(t, "fgm", 0),
                    fieldGoalsAttempted = Int(t, "fga", 0), greens = Int(t, "greens", 0), championships = Int(t, "championships", 0),
                    alleyOops = Math.Max(0, Int(t, "oops", 0)), heatUps = Math.Max(0, Int(t, "heatUps", 0)),
                    versusGames = Math.Max(0, Int(t, "versus", 0)),
                };
                var pr = Obj(o, "practice");
                d.practice = new PracticeBests
                {
                    freeShootMakes = Int(pr, "freeShootMakes", 0), freeShootStreak = Int(pr, "freeShootStreak", 0),
                    passingScore = Int(pr, "passingScore", 0), dribbleLaneTime = (float)Num(pr, "dribbleLaneTime", 0),
                    threePointBest = Math.Max(0, Int(pr, "threePointBest", 0)), lockdownBest = Math.Max(0, Int(pr, "lockdownBest", 0)),
                    shootoutWins = Math.Max(0, Int(pr, "shootoutWins", 0)),
                    aroundWorldTime = Math.Max(0f, (float)Num(pr, "aroundWorld", 0)), horseWins = Math.Max(0, Int(pr, "horseWins", 0)),
                };
                var st = Obj(o, "settings");
                d.settings = new SettingsData
                {
                    musicVolume = Clamp01((float)Num(st, "musicVolume", 0.6)),
                    sfxVolume = Clamp01((float)Num(st, "sfxVolume", 0.9)),
                    haptics = Bool(st, "haptics", true),
                    screenShake = Bool(st, "screenShake", true),
                    uiScale = Math.Max(0.85f, Math.Min(1.25f, (float)Num(st, "uiScale", 1))),
                    colorblindContrast = Bool(st, "colorblindContrast", false),
                    difficultyId = Str(st, "difficultyId", DefaultContent.DefaultDifficultyId),
                    gameCenter = Bool(st, "gameCenter", false),
                    leftHanded = Bool(st, "leftHanded", false),
                    largeButtons = Bool(st, "largeButtons", false),
                    controlLayout = ControlLayout.Serialize(ControlLayout.Parse(Str(st, "controlLayout", ""))),
                    landscapeAll = Bool(st, "landscapeAll", false),
                    commentary = Bool(st, "commentary", true),
                    tapToShoot = Bool(st, "tapToShoot", false),
                    reduceMotion = Bool(st, "reduceMotion", false),
                };
                d.rise = DecodeRise(Obj(o, "rise"));
                d.classic = DecodeClassic(Obj(o, "classic"));
                d.cup = DecodeCup(Obj(o, "cup"));
                d.dynasty = DecodeDynasty(Obj(o, "dynasty"));
                var dy = Obj(o, "daily");
                d.daily = new DailySaveData
                {
                    lastCompletedDay = Int(dy, "lastCompletedDay", -1),
                    streak = Math.Max(0, Int(dy, "streak", 0)),
                    bestStreak = Math.Max(0, Int(dy, "bestStreak", 0)),
                    completed = Math.Max(0, Int(dy, "completed", 0)),
                };
                d.tutorialDone = Bool(o, "tutorialDone", false);
                var kg = Obj(o, "king");
                d.king = new KingSaveData
                {
                    order = StrList(kg, "order"),
                    index = Math.Max(0, Int(kg, "index", 0)),
                    streak = Math.Max(0, Int(kg, "streak", 0)),
                    best = Math.Max(0, Int(kg, "best", 0)),
                    active = Bool(kg, "active", false),
                    runs = Math.Max(0, Int(kg, "runs", 0)),
                    homeTeamId = Str(kg, "home", null),
                };
                d.settings.language = Loc.Normalize(Str(o, "language", Loc.English));
                var disp = Obj(o, "display");
                d.settings.crt = Math.Max(0, Math.Min(2, Int(disp, "crt", 1)));
                d.settings.highFrameRate = Bool(disp, "highFrameRate", true);
                d.settings.attractMode = Bool(disp, "attractMode", true);
                d.settings.showFps = Bool(disp, "showFps", false);
                d.settings.coachTips = Bool(disp, "coachTips", true);
                d.settings.colorFilter = (int)ColorAccess.Normalize(Int(disp, "colorFilter", 0));
                d.settings.captions = Bool(disp, "captions", false);
                d.settings.icloudSync = Bool(disp, "icloud", true);
                d.settings.followSystemText = Bool(disp, "systemText", true);
                d.settings.modeDifficulty = StrList(disp, "modeDifficulty");
                d.settings.musicMenu = Math.Max(-1, Math.Min(Soundtrack.Count - 1, Int(disp, "musicMenu", -1)));
                d.settings.musicGame = Math.Max(-2, Math.Min(Soundtrack.Count - 1, Int(disp, "musicGame", -1)));
                d.tipsSeen = StrList(o, "tipsSeen");
                d.movesFound = StrList(o, "movesFound");
                d.secrets = DecodeSecrets(Obj(o, "secrets"));
                d.customTeam = DecodeTeam(Obj(o, "customTeam"));
                d.kits = DecodeKits(Obj(o, "kits"));
                d.franchise = DecodeFranchise(Obj(o, "franchise"));
                d.allStar = DecodeAllStar(Obj(o, "allStar"));
                d.courts = DecodeCourts(Arr(o, "courts"));
                d.legacy = DecodeLegacy(Obj(o, "legacy"));
                d.street = DecodeStreet(Obj(o, "street"));
                d.clutch = DecodeClutch(Obj(o, "clutch"));
                DecodeLogin(d, Obj(o, "login"));
                DecodeFeel(d.settings, Obj(o, "feel"));
                DecodePro(d, Obj(o, "pro"));
                d.beta = DecodeBeta(Obj(o, "beta"));
                d.phone = DecodePhone(Obj(o, "phone"));
                d.customCup = DecodeCustomCup(Obj(o, "customCup"));
                d.weekly = DecodeWeekly(Obj(o, "weekly"));
                d.pass = DecodePass(Obj(o, "pass"));
                d.story = DecodeStory(Obj(o, "story"));
                d.couch = DecodeCouch(Obj(o, "couch"));
                d.gauntlet = DecodeGauntlet(Obj(o, "gauntlet"));
                d.live = DecodeLive(Obj(o, "live"));
                d.saveFlagged = Bool(o, "flagged", false);
                d.photosTaken = Math.Max(0, Int(o, "photos", 0));
                d.shotChart = ShotCharts.Decode(Str(o, "shots", ""));
                CourtBuilder.Apply(c, d.courts);
                CustomTeams.Clamp(d.customTeam, c);
                var rv = Obj(o, "rival");
                d.rival = new RivalSaveData
                {
                    wins = Math.Max(0, Int(rv, "wins", 0)),
                    sundownWins = Math.Max(0, Int(rv, "sundownWins", 0)),
                    tideWins = Math.Max(0, Int(rv, "tideWins", 0)),
                    cranesWins = Math.Max(0, Int(rv, "cranesWins", 0)),
                    cassetteWins = Math.Max(0, Int(rv, "cassetteWins", 0)),
                    keeperWins = Math.Max(0, Int(rv, "keeperWins", 0)),
                    courierWins = Math.Max(0, Int(rv, "courierWins", 0)),
                    lanternWins = Math.Max(0, Int(rv, "lanternWins", 0)),
                    royalWins = Math.Max(0, Int(rv, "royalWins", 0)),
                    washWins = Math.Max(0, Int(rv, "washWins", 0)),
                    kiteWins = Math.Max(0, Int(rv, "kiteWins", 0)),
                    puttWins = Math.Max(0, Int(rv, "puttWins", 0)),
                    losses = Math.Max(0, Int(rv, "losses", 0)),
                    lastSeason = Math.Max(0, Int(rv, "lastSeason", 0)),
                };
                d.badgesSeen = StrList(o, "badgesSeen");
                d.storySeen = StrList(o, "storySeen");
                var cp = Obj(o, "customPlayer");
                d.customPlayer = new CustomPlayerData
                {
                    created = Bool(cp, "created", false),
                    skinTone = Int(cp, "skin", 0), hairStyle = Int(cp, "hairStyle", 0), hairColor = Int(cp, "hairColor", 0),
                    body = Int(cp, "body", 1), heightTier = Int(cp, "height", 1), jerseyNumber = Int(cp, "jersey", 1),
                    archetypeId = Str(cp, "archetype", null),
                };
                if (cp == null) d.customPlayer = null; // older save: EnsureDefaults starts it from Rook's look
                else PlayerCreator.Clamp(d.customPlayer, c);
                var rc = Obj(o, "records");
                d.records = new CareerRecords
                {
                    points = Int(rc, "points", 0), assists = Int(rc, "assists", 0), rebounds = Int(rc, "rebounds", 0),
                    steals = Int(rc, "steals", 0), blocks = Int(rc, "blocks", 0), greens = Int(rc, "greens", 0),
                    biggestWin = Int(rc, "biggestWin", 0), winStreak = Int(rc, "winStreak", 0), bestWinStreak = Int(rc, "bestWinStreak", 0),
                };
                foreach (var item in Arr(o, "history"))
                {
                    if (!(item is Dictionary<string, object> h)) continue;
                    int mode = Int(h, "mode", 0);
                    d.history.Add(new MatchHistoryEntry
                    {
                        day = Int(h, "day", 0), mode = mode >= 0 && mode <= (int)GameMode.CustomCup ? (GameMode)mode : GameMode.QuickCall,
                        opponentId = Str(h, "opp", null), scoreFor = Int(h, "for", 0), scoreAgainst = Int(h, "against", 0),
                        points = Int(h, "pts", 0), assists = Int(h, "ast", 0), rebounds = Int(h, "reb", 0),
                    });
                    if (d.history.Count >= Records.HistoryLength) break;
                }
                foreach (var item in Arr(o, "seasons"))
                {
                    if (!(item is Dictionary<string, object> e)) continue;
                    d.seasons.Add(new SeasonHistoryEntry
                    {
                        season = Int(e, "season", 0), games = Int(e, "games", 0), wins = Int(e, "wins", 0), losses = Int(e, "losses", 0),
                        points = Int(e, "pts", 0), assists = Int(e, "ast", 0), rebounds = Int(e, "reb", 0), result = Str(e, "result", null),
                    });
                }

                Career.EnsureDefaults(d, c);
                status = version < CareerSaveData.CurrentVersion ? LoadStatus.Migrated : LoadStatus.Ok;
                d.version = CareerSaveData.CurrentVersion;
                return d;
            }
            catch (Exception)
            {
                status = LoadStatus.Recovered;
                return Career.New(c);
            }
        }

        private static RiseSaveData DecodeRise(Dictionary<string, object> o)
        {
            var r = new RiseSaveData();
            if (o == null) return r;
            int stage = Int(o, "stage", 0);
            r.stage = stage >= 0 && stage <= (int)RiseStage.Complete ? (RiseStage)stage : RiseStage.Circuit;
            r.circuitBeaten = StrList(o, "circuitBeaten");
            r.energy = Math.Max(RiseEngine.MinEnergy, Math.Min(100, Int(o, "energy", 100)));
            r.chemistry = Math.Max(0, Math.Min(100, Int(o, "chemistry", 50)));
            r.seenEvents = StrList(o, "seenEvents");
            r.pendingEventId = Str(o, "pendingEventId", null);
            r.recentMargins = Adaptive.Clean(IntList(o, "margins"));
            r.seasonsPlayed = Math.Max(0, Int(o, "seasonsPlayed", 0));
            r.teammates = StrList(o, "teammates");
            r.recruitable = StrList(o, "recruitable");
            r.signed = StrList(o, "signed");
            DecodeRiseMade(r, o);

            var s = Obj(o, "season");
            if (s != null)
            {
                r.season = new SeasonSaveData
                {
                    seasonNumber = Int(s, "seasonNumber", 1),
                    seed = (uint)Math.Max(0, Num(s, "seed", 1)),
                    teamIds = StrList(s, "teamIds"),
                    currentWeek = Int(s, "currentWeek", 0),
                    weeks = Int(s, "weeks", 10),
                    championId = Str(s, "championId", null),
                };
                foreach (var item in Arr(s, "games"))
                {
                    if (!(item is Dictionary<string, object> g)) continue;
                    r.season.games.Add(new ScheduledGame
                    {
                        week = Int(g, "week", 0), round = Int(g, "round", 0),
                        homeId = Str(g, "home", null), awayId = Str(g, "away", null),
                        played = Bool(g, "played", false), homeScore = Int(g, "homeScore", 0), awayScore = Int(g, "awayScore", 0),
                    });
                }
            }
            // A season stage without a season is inconsistent: fall back to the circuit.
            if ((r.stage == RiseStage.Season || r.stage == RiseStage.Playoffs) && r.season == null) r.stage = RiseStage.Circuit;
            return r;
        }

        // ------------------------------------------------------------------ helpers

        private static List<object> List<T>(List<T> items, Func<T, object> map)
        {
            var list = new List<object>();
            if (items != null) foreach (var i in items) list.Add(map(i));
            return list;
        }

        private static List<object> Strings(List<string> items)
        {
            var list = new List<object>();
            if (items != null) foreach (var s in items) list.Add(s);
            return list;
        }

        private static Dictionary<string, object> Obj(Dictionary<string, object> o, string key) =>
            o != null && o.TryGetValue(key, out var v) ? v as Dictionary<string, object> : null;

        private static List<object> Arr(Dictionary<string, object> o, string key) =>
            o != null && o.TryGetValue(key, out var v) && v is List<object> l ? l : new List<object>();

        private static List<string> StrList(Dictionary<string, object> o, string key)
        {
            var list = new List<string>();
            foreach (var v in Arr(o, key)) if (v is string s) list.Add(s);
            return list;
        }

        private static string Str(Dictionary<string, object> o, string key, string fallback) =>
            o != null && o.TryGetValue(key, out var v) && v is string s ? s : fallback;

        private static double Num(Dictionary<string, object> o, string key, double fallback) =>
            o != null && o.TryGetValue(key, out var v) && v is double d ? d : fallback;

        private static int Int(Dictionary<string, object> o, string key, int fallback)
        {
            double d = Num(o, key, fallback);
            if (d > int.MaxValue) return int.MaxValue;
            if (d < int.MinValue) return int.MinValue;
            return (int)Math.Round(d);
        }

        private static bool Bool(Dictionary<string, object> o, string key, bool fallback) =>
            o != null && o.TryGetValue(key, out var v) && v is bool b ? b : fallback;

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
