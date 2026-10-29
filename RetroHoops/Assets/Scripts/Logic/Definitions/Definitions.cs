using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    // ------------------------------------------------------------------
    // Static content definitions. These are plain [Serializable] classes so
    // they can live inside Unity ScriptableObjects AND be unit-tested outside
    // Unity. Runtime state (scores, save data) lives in separate models.
    // ------------------------------------------------------------------

    /// <summary>Anything with a stable, unique string id.</summary>
    public interface IHasId
    {
        string Id { get; }
    }

    public enum Archetype
    {
        FloorGeneral = 0,
        DeepShooter = 1,
        RimRunner = 2,
        LockdownWing = 3,
        GlassCleaner = 4,
        TwoWaySpark = 5,
        PostAnchor = 6,
        QuickCutter = 7,
        Playmaker = 8,
        ShotCreator = 9,
        HustleGuard = 10,
        StretchForward = 11,
    }

    /// <summary>
    /// AI tendencies, each 0..1. Used to weight utility-AI decisions; they never
    /// change ratings, so difficulty and archetype stay "fair".
    /// </summary>
    [Serializable]
    public struct AiTendencies
    {
        public float shoot;
        public float drive;
        public float pass;
        public float cut;
        public float screen;
        public float crashBoards;
        public float helpDefense;
        public float gambleForSteals;
        /// <summary>Preferred shot range, 0 = at the rim, 1 = deep beyond the arc.</summary>
        public float preferredRange;

        public AiTendencies(float shoot, float drive, float pass, float cut, float screen,
                            float crashBoards, float helpDefense, float gambleForSteals, float preferredRange)
        {
            this.shoot = shoot;
            this.drive = drive;
            this.pass = pass;
            this.cut = cut;
            this.screen = screen;
            this.crashBoards = crashBoards;
            this.helpDefense = helpDefense;
            this.gambleForSteals = gambleForSteals;
            this.preferredRange = preferredRange;
        }
    }

    [Serializable]
    public class ArchetypeDef : IHasId
    {
        public string id;
        public Archetype archetype;
        public string displayName;
        public string description;
        public List<AttributeType> strengths = new List<AttributeType>();
        public List<AttributeType> weaknesses = new List<AttributeType>();
        /// <summary>Typical ratings for a mid-level player of this archetype.</summary>
        public AttributeSet baseline;
        public AiTendencies ai;

        public string Id => id;
    }

    public enum BodyType { Slim = 0, Standard = 1, Broad = 2 }

    /// <summary>Procedural sprite parameters; indices into generated palettes.</summary>
    [Serializable]
    public struct AppearanceDef
    {
        public int skinTone;
        public int hairStyle;
        public int hairColor;
        public BodyType body;
        /// <summary>0 = short, 1 = average, 2 = tall. Affects sprite height only.</summary>
        public int heightTier;

        public AppearanceDef(int skinTone, int hairStyle, int hairColor, BodyType body, int heightTier)
        {
            this.skinTone = skinTone;
            this.hairStyle = hairStyle;
            this.hairColor = hairColor;
            this.body = body;
            this.heightTier = heightTier;
        }
    }

    [Serializable]
    public class PlayerDef : IHasId
    {
        public string id;
        public string firstName;
        public string lastName;
        public int jerseyNumber;
        public string archetypeId;
        public AttributeSet attributes;
        public AppearanceDef appearance;

        public string Id => id;
        public string DisplayName => string.IsNullOrEmpty(lastName) ? firstName : firstName + " " + lastName;
    }

    public enum TeamTier
    {
        /// <summary>Street crews in The Blacktop Circuit.</summary>
        Circuit = 0,
        /// <summary>Teams in The Caller League.</summary>
        League = 1,
        /// <summary>The player's own crew in Rise Mode.</summary>
        PlayerCrew = 2,
        /// <summary>The rival crew (Rise Mode Rival Challenges only).</summary>
        Rival = 3,
        /// <summary>Hidden teams: the arcade boss and code-unlocked crews (Quick Call once unlocked).</summary>
        Secret = 4,
        /// <summary>Your created team (Locker Room ► TEAM).</summary>
        Custom = 5,
        /// <summary>Special-event sides: Franchise clubs (team.fr.N) and the All-Star Game teams. Never in menus.</summary>
        Franchise = 6,
    }

    public enum LogoShape { Circle = 0, Shield = 1, Diamond = 2, Hexagon = 3, Badge = 4 }

    public enum LogoMotif { Bolt = 0, Wave = 1, Paw = 2, Tree = 3, Comet = 4, Dune = 5, Owl = 6, Crown = 7, Ball = 8, Signal = 9, Crane = 10, /** Season 6. */ Lighthouse = 11, /** Season 8. */ Lantern = 12, /** Season 9. */ Skate = 13, /** Season 10. */ Bubbles = 14, /** Season 11. */ Kite = 15, /** Season 12. */ Flag = 16 }

    /// <summary>Secondary pattern used on jerseys in colourblind contrast mode.</summary>
    public enum TeamPattern { Solid = 0, Stripes = 1, Dots = 2, Chevrons = 3, Checker = 4, Diagonal = 5, Rings = 6, Cross = 7 }

    [Serializable]
    public class TeamDef : IHasId
    {
        public string id;
        public string city;
        public string nickname;
        public string abbreviation;
        public TeamTier tier;
        public RgbColor primary;
        public RgbColor secondary;
        public RgbColor accent;
        public LogoShape logoShape;
        public LogoMotif logoMotif;
        public TeamPattern pattern;
        public string homeCourtId;
        public string motto;
        public bool unlockedByDefault;
        public List<string> rosterPlayerIds = new List<string>();
        /// <summary>Custom kit (your created team): jersey pattern always drawn, own shorts and shoe colours.</summary>
        public bool customKit;
        /// <summary>Favourite defence when the AI plays this team.</summary>
        public DefenseScheme scheme;
        public RgbColor shorts;
        public RgbColor shoes;

        public string Id => id;
        public string FullName => string.IsNullOrEmpty(city) ? nickname : city + " " + nickname;
    }

    public enum CourtCircuit { Blacktop = 0, League = 1, Practice = 2, /** Hidden courts unlocked by secrets: neon grid floor. */ Secret = 3,
        /** Seasonal courts for the Holiday Games (always open). */ Holiday = 4,
        /** Courts you built in the Court Builder. */ Custom = 5 }

    /// <summary>How a court's floor is drawn (−1 on a court = pick from its circuit).</summary>
    public enum FloorStyle { Asphalt = 0, Hardwood = 1, NeonGrid = 2, Tiles = 3, Rubber = 4 }

    /// <summary>What's behind the baseline.</summary>
    public enum StandsStyle { Crowd = 0, Fence = 1, Brick = 2 }

    /// <summary>Holiday decorations a court is drawn with.</summary>
    public enum HolidayTheme { None = 0, Christmas = 1, Halloween = 2, Easter = 3, FourthOfJuly = 4 }

    [Serializable]
    public class CourtDef : IHasId
    {
        public string id;
        public string displayName;
        public CourtCircuit circuit;
        public string description;
        public RgbColor floor;
        public RgbColor lines;
        public RgbColor paint;
        public RgbColor skyTop;
        public RgbColor skyBottom;
        /// <summary>Crowd density 0..1 for silhouette generation.</summary>
        public float crowdDensity;
        /// <summary>Holiday decorations (Holiday courts only).</summary>
        public HolidayTheme theme;
        /// <summary>Floor look; −1 = from the circuit (league hardwood, secret neon, street asphalt).</summary>
        public int floorStyle = -1;
        public StandsStyle stands;
        /// <summary>Centre-court logo motif (−1 = none), its shape and colour.</summary>
        public int logoMotif = -1;
        public LogoShape logoShape;
        public RgbColor logoColor;

        public string Id => id;
    }

    [Serializable]
    public class GameRulesDef : IHasId
    {
        public string id = "rules.default";
        public int targetScore = 21;
        public bool useGameClock = true;
        public float gameClockSeconds = 120f;
        public float shotClockSeconds = 14f;
        public int insideArcPoints = 1;
        public int beyondArcPoints = 2;
        public bool winByTwo = false;
        public bool checkBallAfterScore = true;
        /// <summary>Tie at the horn → next basket wins.</summary>
        public bool suddenDeathOnTie = true;
        /// <summary>"21": go over the target and you drop back to <see cref="bustScore"/>.</summary>
        public bool bustRule;
        public int bustScore = 13;
        /// <summary>Make it, take it: the scorer keeps the ball.</summary>
        public bool makeItTakeIt;

        public string Id => id;
    }

    [Serializable]
    public class DifficultyDef : IHasId
    {
        public string id;
        public string displayName;
        public int sortOrder;
        /// <summary>Delay before AI reacts to a new situation, seconds.</summary>
        public float reactionTime;
        /// <summary>0..1: probability AI picks the best-scored option rather than a random viable one.</summary>
        public float decisionQuality;
        /// <summary>Minimum expected make chance before the AI takes a shot, 0..1.</summary>
        public float shotQualityThreshold;
        /// <summary>0..1: chance of a deliberate mistake (late rotation, lazy pass).</summary>
        public float errorRate;
        /// <summary>Multiplier on AI movement; capped at 1.0 so AI never outruns its ratings.</summary>
        public float movementScale = 1f;
        /// <summary>Timing accuracy of AI shot releases, 0..1 (1 = always in the green window).</summary>
        public float releaseAccuracy;

        public string Id => id;
    }

    [Serializable]
    public class UpgradeDef : IHasId
    {
        public string id;
        public string displayName;
        public string description;
        public AttributeType attribute;
        public int amountPerLevel = 1;
        public int maxLevel = 5;
        public int baseCost = 100;
        /// <summary>Cost multiplier per level already purchased (e.g. 1.5).</summary>
        public float costGrowth = 1.5f;
        /// <summary>Completed games required between purchases ("training time").</summary>
        public int trainingGames = 1;
        /// <summary>Upgrades never push the attribute beyond this.</summary>
        public int attributeCap = 90;

        public string Id => id;
    }

    public enum CosmeticSlot { JerseyPalette = 0, Shoes = 1, CourtBanner = 2, Celebration = 3, DribbleMove = 4, DunkPackage = 5, /** I3 (16 Oct 2026): sleeves, ink, goggles. */ Gear = 6 }

    [Serializable]
    public class CosmeticDef : IHasId
    {
        public string id;
        public string displayName;
        public CosmeticSlot slot;
        public int cost;
        /// <summary>Fans required before it can be bought (0 = none).</summary>
        public int fansRequired;
        public bool unlockedByDefault;
        /// <summary>Only unlocked on the Hoops Pass track (never sold in the shop).</summary>
        public bool passOnly;
        public RgbColor colorA;
        public RgbColor colorB;
        /// <summary>I30: sold only in this region's store, for street cash (null = the usual shops).</summary>
        public string store;

        public string Id => id;
    }

    [Serializable]
    public class SeasonConfigDef : IHasId
    {
        public string id = "season.caller_league_mvp";
        public string displayName = "Caller League Season";
        public int regularSeasonGames = 10;
        /// <summary>2 or 4.</summary>
        public int playoffTeams = 4;
        public List<string> teamIds = new List<string>();
        public string championshipName = "The Gold Signal Cup";
        public int signalPointsPerWin = 120;
        public int signalPointsPerLoss = 50;
        public int fansPerWin = 40;
        public int fansPerLoss = 10;
        public int championshipBonusPoints = 500;

        public string Id => id;
    }

    /// <summary>All static content in one bag. Built from ScriptableObjects or <see cref="DefaultContent"/>.</summary>
    public class ContentCatalog
    {
        public List<ArchetypeDef> Archetypes = new List<ArchetypeDef>();
        public List<PlayerDef> Players = new List<PlayerDef>();
        public List<TeamDef> Teams = new List<TeamDef>();
        public List<CourtDef> Courts = new List<CourtDef>();
        public List<GameRulesDef> Rules = new List<GameRulesDef>();
        public List<DifficultyDef> Difficulties = new List<DifficultyDef>();
        public List<UpgradeDef> Upgrades = new List<UpgradeDef>();
        public List<CosmeticDef> Cosmetics = new List<CosmeticDef>();
        public List<SeasonConfigDef> Seasons = new List<SeasonConfigDef>();
        /// <summary>Runtime only: the First Callers' original look while your custom team dresses them.</summary>
        [NonSerialized] public TeamDef CrewOriginalLook;

        public T Find<T>(IEnumerable<T> list, string id) where T : class, IHasId
        {
            foreach (var item in list)
                if (item != null && item.Id == id) return item;
            return null;
        }

        public TeamDef Team(string id) => Find(Teams, id);
        public PlayerDef Player(string id) => Find(Players, id);
        public CourtDef Court(string id) => Find(Courts, id);
        public ArchetypeDef ArchetypeById(string id) => Find(Archetypes, id);
        public DifficultyDef Difficulty(string id) => Find(Difficulties, id);

        public List<TeamDef> TeamsInTier(TeamTier tier)
        {
            var result = new List<TeamDef>();
            foreach (var t in Teams) if (t.tier == tier) result.Add(t);
            return result;
        }
    }
}
