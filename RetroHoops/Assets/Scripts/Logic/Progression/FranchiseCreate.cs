using System;

namespace CallerRetroBall.Logic
{
    /// <summary>What you pick in Franchise ► CREATE A PLAYER.</summary>
    public sealed class CreatedPlayerSpec
    {
        public string First = "", Last = "";
        public int Number = 1;
        /// <summary><see cref="Archetype"/> as an int (style of play).</summary>
        public int Archetype;
        public int Skin, Hair, HairColor, Body = 1, Height = 1;
        /// <summary>Index into <see cref="Franchise.CreateLevels"/>.</summary>
        public int Level;
    }

    /// <summary>A starting level for a created player: how good he is now, his age and ceiling, and what he costs.</summary>
    public sealed class CreateLevel
    {
        public string Name;
        public int Overall, Age, Potential;
        /// <summary>Signs for the league minimum (always allowed, even over the cap).</summary>
        public bool Minimum;
    }

    /// <summary>
    /// Franchise ► CREATE A PLAYER (added 10 Oct 2026, Omari's request): build your own player (name, number, style
    /// of play, look, starting level) and sign him to your club. Up to <see cref="MaxCreated"/> per franchise, any time
    /// except the playoffs, if there's a roster spot. A ROOKIE signs for the minimum (allowed over the cap) and has a
    /// high ceiling; STARTER and STAR are better now but cost market salary and need cap room. Created players are
    /// yours: AI GMs never ask for them in trade offers. Their look, name and number can be changed later; ratings
    /// grow (or fade) like everyone else's.
    /// </summary>
    public static partial class Franchise
    {
        public const int MaxCreated = 3;
        public const int MaxNameLength = 14;

        public static readonly CreateLevel[] CreateLevels =
        {
            new CreateLevel { Name = "ROOKIE", Overall = 64, Age = 20, Potential = 86, Minimum = true },
            new CreateLevel { Name = "STARTER", Overall = 72, Age = 24, Potential = 82 },
            new CreateLevel { Name = "STAR", Overall = 80, Age = 26, Potential = 86 },
        };

        public static int CreatedCount(FranchiseSaveData f) => f == null ? 0 : f.players.FindAll(p => p.created && p.team == f.you).Count;

        /// <summary>The salary a created player at <paramref name="level"/> signs for.</summary>
        public static int CreateSalary(int level)
        {
            var l = CreateLevels[Math.Max(0, Math.Min(CreateLevels.Length - 1, level))];
            return l.Minimum ? MinSalary : MarketSalary(l.Overall);
        }

        /// <summary>Why you can't create a player at <paramref name="level"/> right now, or null if you can.</summary>
        public static string CannotCreate(FranchiseSaveData f, int level = 0)
        {
            if (f == null || !f.active) return "Start a franchise first.";
            if (f.phase == FranchisePhase.Playoffs) return "Not during the playoffs. Come back in the off-season.";
            if (CreatedCount(f) >= MaxCreated) return "You already have three created players on your club.";
            if (Roster(f, f.you).Count >= MaxRoster) return "Your roster is full. Cut someone first.";
            var l = CreateLevels[Math.Max(0, Math.Min(CreateLevels.Length - 1, level))];
            if (!l.Minimum && CreateSalary(level) > CapRoom(f, f.you))
                return "Not enough cap room for that level. A ROOKIE signs for the minimum.";
            return null;
        }

        /// <summary>Ratings for a created player: his style's typical ratings moved to the level's overall.</summary>
        public static AttributeSet CreatedRatings(ContentCatalog c, int archetype, int level)
        {
            var arch = c.Archetypes.Find(a => (int)a.archetype == archetype) ?? c.Archetypes[0];
            var l = CreateLevels[Math.Max(0, Math.Min(CreateLevels.Length - 1, level))];
            var attrs = arch.baseline;
            return attrs.Offset(l.Overall - attrs.Overall);
        }

        /// <summary>Makes the player and signs him to your club (end of the bench). Returns null if <see cref="CannotCreate"/> says no.</summary>
        public static FrPlayer CreatePlayer(FranchiseSaveData f, ContentCatalog c, CreatedPlayerSpec s)
        {
            if (s == null || CannotCreate(f, s.Level) != null) return null;
            int level = Math.Max(0, Math.Min(CreateLevels.Length - 1, s.Level));
            var l = CreateLevels[level];
            var p = new FrPlayer
            {
                id = f.nextId++, created = true, team = f.you, age = l.Age, years = 3,
                attrs = CreatedRatings(c, s.Archetype, level),
            };
            ApplyLook(f, p, s);
            p.archetype = Math.Max(0, Math.Min(11, s.Archetype));
            p.potential = Math.Max(p.Overall, l.Potential);
            p.peak = p.Overall;
            p.salary = CreateSalary(level);
            p.draftYear = f.year;
            f.players.Add(p);
            f.teams[f.you].roster.Add(p.id);
            f.moves.Add(new FrMove { year = f.year, text = "Signed created player " + p.Name + " (" + p.Overall + ", age " + p.age + ")." });
            return p;
        }

        /// <summary>Changes a created player's name, number and look (not his ratings). False if he isn't yours or wasn't created.</summary>
        public static bool EditCreated(FranchiseSaveData f, int playerId, CreatedPlayerSpec s)
        {
            var p = Player(f, playerId);
            if (p == null || !p.created || p.team != f.you || s == null) return false;
            ApplyLook(f, p, s);
            return true;
        }

        /// <summary>A spec filled in from a player (for editing him).</summary>
        public static CreatedPlayerSpec SpecOf(FrPlayer p) => new CreatedPlayerSpec
        {
            First = p.first, Last = p.last, Number = p.number, Archetype = p.archetype,
            Skin = p.skin, Hair = p.hair, HairColor = p.hairColor, Body = p.body, Height = p.height,
        };

        /// <summary>A spec starting from your Locker Room player (look, number, style) and nickname.</summary>
        public static CreatedPlayerSpec SpecFromCareer(CareerSaveData d, ContentCatalog c)
        {
            var me = PlayerCreator.BasePlayer(d, c);
            var arch = c.ArchetypeById(me.archetypeId);
            return new CreatedPlayerSpec
            {
                First = string.IsNullOrEmpty(me.firstName) ? "Rook" : me.firstName, Last = "",
                Number = me.jerseyNumber, Archetype = arch != null ? (int)arch.archetype : 0,
                Skin = me.appearance.skinTone, Hair = me.appearance.hairStyle, HairColor = me.appearance.hairColor,
                Body = (int)me.appearance.body, Height = me.appearance.heightTier,
            };
        }

        /// <summary>Trims a typed name to something safe to show (letters, digits, space, ' . -).</summary>
        public static string CleanName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            var sb = new System.Text.StringBuilder();
            foreach (char ch in name.Trim())
            {
                if (sb.Length >= MaxNameLength) break;
                if (char.IsLetterOrDigit(ch) || ch == ' ' || ch == '\'' || ch == '.' || ch == '-') sb.Append(ch);
            }
            return sb.ToString().Trim();
        }

        private static void ApplyLook(FranchiseSaveData f, FrPlayer p, CreatedPlayerSpec s)
        {
            p.first = CleanName(s.First);
            p.last = CleanName(s.Last);
            if (p.first.Length == 0 && p.last.Length == 0) p.first = "Rookie";
            p.skin = Wrap(s.Skin, PlayerCreator.SkinToneCount);
            p.hair = Wrap(s.Hair, PlayerCreator.HairStyleCount);
            p.hairColor = Wrap(s.HairColor, PlayerCreator.HairColorCount);
            p.body = Wrap(s.Body, PlayerCreator.BodyCount);
            p.height = Wrap(s.Height, PlayerCreator.HeightCount);
            int wanted = Math.Max(0, Math.Min(99, s.Number));
            p.number = p.team == f.you && p.number == wanted ? wanted : FreeNumberExcept(f, f.you, wanted, p.id);
        }

        private static int FreeNumberExcept(FranchiseSaveData f, int team, int wanted, int playerId)
        {
            var used = new System.Collections.Generic.HashSet<int>();
            foreach (var q in Roster(f, team)) if (q.id != playerId) used.Add(q.number);
            int n = Math.Max(0, Math.Min(99, wanted));
            for (int guard = 0; guard < 100 && used.Contains(n); guard++) n = (n + 7) % 100;
            return n;
        }

        private static int Wrap(int v, int n) => n <= 0 ? 0 : ((v % n) + n) % n;
    }
}
