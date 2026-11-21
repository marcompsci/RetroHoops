using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// Career records (best single-game marks, win streaks), match history (last
    /// <see cref="HistoryLength"/> games), and per-season Rise history. Updated once per counted game.
    /// </summary>
    public static class Records
    {
        public const int HistoryLength = 20;

        /// <summary>Applies a finished game. Returns the names of records it broke (empty if none).</summary>
        public static List<string> Update(CareerSaveData d, MatchSummary s)
        {
            var broken = new List<string>();
            if (d == null || s == null) return broken;
            if (d.records == null) d.records = new CareerRecords();
            var r = d.records;
            var line = s.HumanLine?.stats ?? new PlayerStatLine();

            // Full Court scores 2s and 3s, so its points don't compare with the half-court record.
            if (s.mode != GameMode.FullCourt && !s.fullCourt) Check(ref r.points, line.points, "POINTS", broken);
            Check(ref r.assists, line.assists, "ASSISTS", broken);
            Check(ref r.rebounds, line.rebounds, "REBOUNDS", broken);
            Check(ref r.steals, line.steals, "STEALS", broken);
            Check(ref r.blocks, line.blocks, "BLOCKS", broken);
            Check(ref r.greens, line.greenReleases, "GREENS", broken);
            Check(ref r.posters, line.posters, "POSTERS", broken); // I198
            Check(ref r.chaseDowns, line.chaseDowns, "CHASE-DOWN BLOCKS", broken);
            Check(ref r.clutchMakes, line.clutchMakes, "CLUTCH BUCKETS", broken);
            Check(ref r.greenRun, line.bestGreenRun, "GREEN RUN", broken);
            Check(ref r.biggestComeback, GameCallouts.Comeback(s), "BIGGEST COMEBACK", broken); // I221
            if (s.HumanWon) Check(ref r.biggestWin, s.Margin, "BIGGEST WIN", broken);

            r.winStreak = s.HumanWon ? r.winStreak + 1 : 0;
            if (r.winStreak > r.bestWinStreak)
            {
                // Only announce a streak record once it's meaningful.
                if (r.winStreak >= 3) broken.Add("WIN STREAK");
                r.bestWinStreak = r.winStreak;
            }

            if (d.history == null) d.history = new List<MatchHistoryEntry>();
            d.history.Insert(0, new MatchHistoryEntry
            {
                day = s.day,
                mode = s.mode,
                opponentId = s.humanTeam == 0 ? s.teamBId : s.teamAId,
                scoreFor = s.HumanScore,
                scoreAgainst = s.OpponentScore,
                points = line.points,
                assists = line.assists,
                rebounds = line.rebounds,
                teamId = s.humanTeam == 0 ? s.teamAId : s.teamBId, // I237
            });
            while (d.history.Count > HistoryLength) d.history.RemoveAt(d.history.Count - 1);

            if (s.mode == GameMode.Rise && d.rise != null)
            {
                int season = d.rise.stage == RiseStage.Circuit || d.rise.season == null ? 0 : d.rise.season.seasonNumber;
                var e = Season(d, season);
                e.games++;
                if (s.HumanWon) e.wins++; else e.losses++;
                e.points += line.points;
                e.assists += line.assists;
                e.rebounds += line.rebounds;
            }
            return broken;
        }

        /// <summary>Records how a Rise season ended (champion, eliminated, missed the playoffs).</summary>
        public static void SetSeasonResult(CareerSaveData d, int season, string result)
        {
            if (d == null) return;
            Season(d, season).result = result;
        }

        public static SeasonHistoryEntry Season(CareerSaveData d, int season)
        {
            if (d.seasons == null) d.seasons = new List<SeasonHistoryEntry>();
            foreach (var e in d.seasons) if (e.season == season) return e;
            var created = new SeasonHistoryEntry { season = season };
            d.seasons.Add(created);
            d.seasons.Sort((a, b) => a.season.CompareTo(b.season));
            return created;
        }

        private static void Check(ref int best, int value, string name, List<string> broken)
        {
            if (value <= best) return;
            // A first game sets every mark; only announce once there was a previous best.
            if (best > 0) broken.Add(name);
            best = value;
        }
    }

    /// <summary>Your player: Rook by default, or the one you built in the Locker Room.</summary>
    public static class PlayerCreator
    {
        /// <summary>Same starting level as Rook: archetype baseline minus 10, then trained with upgrades.</summary>
        public const int RatingOffset = -10;
        public const int MaxJersey = 99;

        public static int SkinToneCount => PixelArt.CharacterSpriteGenerator.SkinTones.Length;
        public static int HairColorCount => PixelArt.CharacterSpriteGenerator.HairColors.Length;
        public static int HairStyleCount => PixelArt.CharacterSpriteGenerator.HairStyleCount;
        public const int BodyCount = 3;
        public const int HeightCount = 3;

        /// <summary>Starts the editor from Rook's look so nothing changes until the player edits it.</summary>
        public static CustomPlayerData FromRook(ContentCatalog c)
        {
            var rook = c.Player(DefaultContent.RookPlayerId);
            var look = rook != null ? rook.appearance : new AppearanceDef(2, 0, 0, BodyType.Standard, 1);
            return new CustomPlayerData
            {
                created = false,
                skinTone = look.skinTone,
                hairStyle = look.hairStyle,
                hairColor = look.hairColor,
                body = (int)look.body,
                heightTier = look.heightTier,
                jerseyNumber = rook != null ? rook.jerseyNumber : 1,
                archetypeId = rook?.archetypeId,
            };
        }

        /// <summary>Keeps every field in range (bad save data can't break sprites or ratings).</summary>
        public static void Clamp(CustomPlayerData p, ContentCatalog c)
        {
            p.skinTone = Wrap(p.skinTone, SkinToneCount);
            p.hairStyle = Wrap(p.hairStyle, HairStyleCount);
            p.hairColor = Wrap(p.hairColor, HairColorCount);
            p.body = Wrap(p.body, BodyCount);
            p.heightTier = Wrap(p.heightTier, HeightCount);
            p.jerseyNumber = Math.Max(0, Math.Min(MaxJersey, p.jerseyNumber));
            if (c.ArchetypeById(p.archetypeId) == null)
                p.archetypeId = c.Player(DefaultContent.RookPlayerId)?.archetypeId ?? c.Archetypes[0].id;
        }

        /// <summary>Your player's definition before training upgrades (a copy; content is never modified).</summary>
        public static PlayerDef BasePlayer(CareerSaveData d, ContentCatalog c)
        {
            var rook = c.Player(DefaultContent.RookPlayerId);
            string name = string.IsNullOrEmpty(d?.nickname) ? "Rook" : d.nickname;
            if (d == null || d.customPlayer == null || !d.customPlayer.created)
            {
                return new PlayerDef
                {
                    id = DefaultContent.RookPlayerId, firstName = name, lastName = "",
                    jerseyNumber = rook.jerseyNumber, archetypeId = rook.archetypeId,
                    attributes = rook.attributes, appearance = rook.appearance,
                };
            }
            var p = d.customPlayer;
            Clamp(p, c);
            var archetype = c.ArchetypeById(p.archetypeId);
            return new PlayerDef
            {
                id = DefaultContent.RookPlayerId,
                firstName = name,
                lastName = "",
                jerseyNumber = p.jerseyNumber,
                archetypeId = archetype.id,
                attributes = archetype.baseline.Offset(RatingOffset),
                appearance = new AppearanceDef(p.skinTone, p.hairStyle, p.hairColor, (BodyType)p.body, p.heightTier),
            };
        }

        public static AttributeSet BaseRatings(CareerSaveData d, ContentCatalog c) => BasePlayer(d, c).attributes;

        /// <summary>The player as they take the court: base player with training upgrades applied.</summary>
        public static PlayerDef ForMatch(CareerSaveData d, ContentCatalog c)
        {
            var p = BasePlayer(d, c);
            if (d != null) p.attributes = Career.EffectiveRatings(p.attributes, d, c);
            return p;
        }

        private static int Wrap(int v, int count) => count <= 0 ? 0 : ((v % count) + count) % count;
    }
}
