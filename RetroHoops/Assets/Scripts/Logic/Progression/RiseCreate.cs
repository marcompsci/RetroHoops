using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// Rise ► CREATE A PLAYER (added 10 Oct 2026, Omari's request):
    /// YOU: build or edit the player you control in Rise (the same player as Locker Room ► CREATE; his name is your nickname).
    /// CREW: build up to <see cref="MaxMade"/> teammates of your own. They're free to put in either crew spot and start at the
    /// same level as the original First Callers (style baseline <see cref="MateRatingOffset"/>). Training upgrades are yours only.
    /// Removing a created teammate puts an original First Caller back in his spot.
    /// </summary>
    public static class RiseCreate
    {
        public const int MaxMade = 3;
        /// <summary>Same as Benny Quarles and Dre Ashby (the original First Callers).</summary>
        public const int MateRatingOffset = -8;
        public const string IdPrefix = "player.rise_made_";

        public static string PlayerId(int id) => IdPrefix + id;
        public static bool IsMade(string playerId) => playerId != null && playerId.StartsWith(IdPrefix, StringComparison.Ordinal);

        public static List<RiseMateData> Made(CareerSaveData d) => d?.rise?.made ?? new List<RiseMateData>();

        public static RiseMateData Find(CareerSaveData d, string playerId)
        {
            foreach (var m in Made(d)) if (PlayerId(m.id) == playerId) return m;
            return null;
        }

        /// <summary>Puts every created teammate in the catalog so matches, the crew screen and box scores can find him.</summary>
        public static void Register(CareerSaveData d, ContentCatalog c)
        {
            if (d?.rise == null || c == null) return;
            if (d.rise.made == null) d.rise.made = new List<RiseMateData>();
            foreach (var m in d.rise.made)
            {
                var arch = c.ArchetypeById(m.archetypeId) ?? c.Archetypes[0];
                m.archetypeId = arch.id;
                string id = PlayerId(m.id);
                var def = c.Player(id);
                if (def == null) c.Players.Add(def = new PlayerDef { id = id });
                def.firstName = m.first;
                def.lastName = m.last;
                def.jerseyNumber = m.number;
                def.archetypeId = arch.id;
                def.attributes = Ratings(c, arch.id);
                def.appearance = new AppearanceDef(Wrap(m.skin, PlayerCreator.SkinToneCount), Wrap(m.hair, PlayerCreator.HairStyleCount),
                                                   Wrap(m.hairColor, PlayerCreator.HairColorCount), (BodyType)Wrap(m.body, PlayerCreator.BodyCount),
                                                   Wrap(m.height, PlayerCreator.HeightCount));
            }
        }

        public static AttributeSet Ratings(ContentCatalog c, string archetypeId)
        {
            var arch = c.ArchetypeById(archetypeId) ?? c.Archetypes[0];
            return arch.baseline.Offset(MateRatingOffset);
        }

        /// <summary>Why you can't create another teammate, or null.</summary>
        public static string CannotCreate(CareerSaveData d)
        {
            if (d?.rise == null) return "Start Rise Mode first.";
            if (Made(d).Count >= MaxMade) return "You already have three created teammates. Edit or remove one.";
            return null;
        }

        /// <summary>Makes a teammate. With <paramref name="slot"/> 0 or 1 he goes straight into that crew spot.</summary>
        public static RiseMateData CreateMate(CareerSaveData d, ContentCatalog c, CreatedPlayerSpec s, int slot = -1)
        {
            if (s == null || CannotCreate(d) != null) return null;
            var m = new RiseMateData { id = Math.Max(1, d.rise.nextMade) };
            d.rise.nextMade = m.id + 1;
            Apply(c, m, s);
            d.rise.made.Add(m);
            Register(d, c);
            if (slot >= 0 && slot < CrewEngine.TeammateSlots) CrewEngine.Recruit(d, c, PlayerId(m.id), slot);
            return m;
        }

        /// <summary>Changes a created teammate's name, number, look and style.</summary>
        public static bool EditMate(CareerSaveData d, ContentCatalog c, string playerId, CreatedPlayerSpec s)
        {
            var m = Find(d, playerId);
            if (m == null || s == null) return false;
            Apply(c, m, s);
            Register(d, c);
            return true;
        }

        /// <summary>Deletes a created teammate. If he was in a crew spot, an original First Caller takes it back.</summary>
        public static bool RemoveMate(CareerSaveData d, ContentCatalog c, string playerId)
        {
            var m = Find(d, playerId);
            if (m == null) return false;
            var team = new List<string>(CrewEngine.Teammates(d, c));
            int slot = team.IndexOf(playerId);
            if (slot >= 0)
            {
                foreach (var original in CrewEngine.DefaultTeammates(c))
                    if (!team.Contains(original)) { team[slot] = original; break; }
                d.rise.teammates = team;
            }
            d.rise.made.Remove(m);
            return true;
        }

        public static CreatedPlayerSpec SpecOf(ContentCatalog c, RiseMateData m) => new CreatedPlayerSpec
        {
            First = m.first, Last = m.last, Number = m.number, Archetype = ArchetypeInt(c, m.archetypeId),
            Skin = m.skin, Hair = m.hair, HairColor = m.hairColor, Body = m.body, Height = m.height,
        };

        /// <summary>A fresh teammate: a random-ish look so he doesn't look like you, the style your crew needs least.</summary>
        public static CreatedPlayerSpec NewMateSpec(CareerSaveData d, ContentCatalog c)
        {
            int n = Made(d).Count + (d?.rise?.nextMade ?? 1);
            return new CreatedPlayerSpec
            {
                First = "", Last = "", Number = 10 + (n * 7) % 80,
                Archetype = ArchetypeInt(c, c.Archetypes[n % c.Archetypes.Count].id),
                Skin = n % PlayerCreator.SkinToneCount, Hair = (n * 3) % PlayerCreator.HairStyleCount, HairColor = 0, Body = 1, Height = (n + 1) % 3,
            };
        }

        /// <summary>Your Rise player as a spec (name = your nickname).</summary>
        public static CreatedPlayerSpec MySpec(CareerSaveData d, ContentCatalog c)
        {
            var s = Franchise.SpecFromCareer(d, c);
            s.First = string.IsNullOrWhiteSpace(d?.nickname) ? "Rook" : d.nickname;
            return s;
        }

        /// <summary>Saves your Rise player (also the Locker Room player). Ratings stay your style baseline + training.</summary>
        public static void ApplyMine(CareerSaveData d, ContentCatalog c, CreatedPlayerSpec s)
        {
            if (d == null || s == null) return;
            var arch = c.Archetypes.Find(a => (int)a.archetype == s.Archetype) ?? c.Archetypes[0];
            d.customPlayer = new CustomPlayerData
            {
                created = true, skinTone = s.Skin, hairStyle = s.Hair, hairColor = s.HairColor, body = s.Body, heightTier = s.Height,
                jerseyNumber = Math.Max(0, Math.Min(PlayerCreator.MaxJersey, s.Number)), archetypeId = arch.id,
            };
            PlayerCreator.Clamp(d.customPlayer, c);
            string name = Franchise.CleanName(s.First);
            if (name.Length > 0) d.nickname = Career.CleanNickname(name);
        }

        private static void Apply(ContentCatalog c, RiseMateData m, CreatedPlayerSpec s)
        {
            m.first = Franchise.CleanName(s.First);
            m.last = Franchise.CleanName(s.Last);
            if (m.first.Length == 0 && m.last.Length == 0) m.first = "Rookie";
            m.number = Math.Max(0, Math.Min(99, s.Number));
            m.archetypeId = (c.Archetypes.Find(a => (int)a.archetype == s.Archetype) ?? c.Archetypes[0]).id;
            m.skin = Wrap(s.Skin, PlayerCreator.SkinToneCount);
            m.hair = Wrap(s.Hair, PlayerCreator.HairStyleCount);
            m.hairColor = Wrap(s.HairColor, PlayerCreator.HairColorCount);
            m.body = Wrap(s.Body, PlayerCreator.BodyCount);
            m.height = Wrap(s.Height, PlayerCreator.HeightCount);
        }

        private static int ArchetypeInt(ContentCatalog c, string archetypeId)
        {
            var a = c.ArchetypeById(archetypeId);
            return a != null ? (int)a.archetype : (int)c.Archetypes[0].archetype;
        }

        private static int Wrap(int v, int n) => n <= 0 ? 0 : ((v % n) + n) % n;
    }
}
