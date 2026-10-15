using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>Your THE STREETS record.</summary>
    [Serializable]
    public class StreetsSaveData
    {
        public int games, wins, losses;
        /// <summary>SHOWSTOPPERs scored, tricks done, and the biggest style banked in one bucket.</summary>
        public int showstoppers, tricks, bestBank;
        public int streak, bestStreak;
        /// <summary>The last pickup game's choices (so the hub opens where you left it).</summary>
        public string lastOpponent = "", lastCourt = "";
        /// <summary>Phase 2 STREET TOUR: crews beaten, stars ("id=n"), street rep, leaders recruited, and your STREETS CREW (2 spots).</summary>
        public List<string> tourBeaten = new List<string>();
        public List<string> tourStars = new List<string>();
        public int rep;
        public List<string> recruits = new List<string>();
        public List<string> crew = new List<string>();
        /// <summary>Phase 2: LIVE STREETS record (the rating is your LIVE rating).</summary>
        public int liveWins, liveLosses;
        /// <summary>Phase 3 HOMECOURT: the court (id), its name (index into StreetsHome.Names), defenses, the streak, and who took it.</summary>
        public string homeCourt = "";
        public int homeName, homeDefenses, homeStreak, homeBest, homeLosses;
        public bool homeTaken;
        public string homeTaker = "";
        /// <summary>Phase 3: CREW CHEMISTRY ("playerId=points"), STREET GEAR worn ("" = your usual kit), SIGNATURE SHOWSTOPPER (-1 = your dunk package).</summary>
        public List<string> chem = new List<string>();
        public string gear = "";
        public int signature = -1;
    }

    /// <summary>
    /// 15 Oct 2026 (Omari): THE STREETS. Arcade 3-on-3 street ball, an original game inspired by the classic
    /// console street-ball games:
    /// - First to 21 by 1s and 2s, win by two. No clock, no fouls. The ball changes hands after every bucket.
    /// - Tricks, combos, the STREET METER and the SHOWSTOPPER (see <see cref="MatchSimulation"/>, StreetStyle.cs).
    /// - Street courts only: blacktops, cages, piers and rooftops.
    /// Phase 1 is PICKUP: your crew against any street crew on any street court.
    /// </summary>
    public static class Streets
    {
        public const string RulesId = "rules.streets";
        public const int TeamSize = 3;
        public const string PickupContext = "streets:pickup";
        public const string TourPrefix = "streets:tour:";

        /// <summary>The rules for a game to 11, 16 or 21 (all win by two, no clock).</summary>
        public static string RulesFor(int target) => target <= 11 ? RulesId + ".11" : target <= 16 ? RulesId + ".16" : RulesId;

        /// <summary>Every STREETS rules set (21, plus the shorter tour games).</summary>
        public static IEnumerable<GameRulesDef> AllRules()
        {
            yield return Rules();
            var r11 = Rules();
            r11.id = RulesId + ".11";
            r11.targetScore = 11;
            yield return r11;
            var r16 = Rules();
            r16.id = RulesId + ".16";
            r16.targetScore = 16;
            yield return r16;
        }

        /// <summary>The street courts, in the order the hub offers them.</summary>
        public static readonly string[] Courts =
        {
            "court.overpass_park", "court.rooftop_ring", "court.pier_nine", "court.sunset_cage", "court.rain_alley",
            "court.boardwalk_slab", "court.ferry_deck", "court.lantern_market", "court.snowline_park", "court.sundown_yard",
            "court.canyon_rim", "court.static_lot",
        };

        /// <summary>The rules: first to 21, 1s and 2s, win by two, no game clock.</summary>
        public static GameRulesDef Rules() => new GameRulesDef
        {
            id = RulesId,
            targetScore = 21,
            useGameClock = false,
            gameClockSeconds = 0f,
            shotClockSeconds = 16f,
            insideArcPoints = 1,
            beyondArcPoints = 2,
            winByTwo = true,
            checkBallAfterScore = true,
            makeItTakeIt = false,
        };

        /// <summary>Courts from the list that exist in this catalog.</summary>
        public static List<CourtDef> CourtList(ContentCatalog c)
        {
            var list = new List<CourtDef>();
            foreach (var id in Courts)
            {
                var court = c?.Court(id);
                if (court != null) list.Add(court);
            }
            return list;
        }

        /// <summary>Crews you can run against: the Park legends' crews, then the league and circuit teams.</summary>
        public static List<TeamDef> Opponents(ContentCatalog c)
        {
            var list = new List<TeamDef>();
            if (c == null) return list;
            foreach (var ch in Street.Challengers)
            {
                var t = Street.Register(c, ch);
                if (t != null && t.rosterPlayerIds.Count >= TeamSize && !list.Contains(t)) list.Add(t);
            }
            foreach (var t in c.Teams)
                if ((t.tier == TeamTier.League || t.tier == TeamTier.Circuit) && t.rosterPlayerIds.Count >= TeamSize && !list.Contains(t)) list.Add(t);
            return list;
        }

        /// <summary>Your side: your created team when you have one, otherwise your crew (your player leads it).</summary>
        public static string MyTeamId(ContentCatalog c) =>
            CustomTeams.IsYours(CustomTeams.TeamId) && c?.Team(CustomTeams.TeamId) != null ? CustomTeams.TeamId : DefaultContent.PlayerCrewId;

        /// <summary>A pickup game: your crew against <paramref name="opponentId"/> on <paramref name="courtId"/>.</summary>
        public static MatchRequest Pickup(ContentCatalog c, CareerSaveData career, string opponentId, string courtId)
        {
            var opponents = Opponents(c);
            var opp = opponents.Find(t => t.id == opponentId) ?? (opponents.Count > 0 ? opponents[0] : null);
            var courts = CourtList(c);
            var court = courts.Find(k => k.id == courtId) ?? (courts.Count > 0 ? courts[0] : null);
            return new MatchRequest
            {
                Mode = GameMode.Streets,
                HomeTeamId = MyTeamId(c),
                AwayTeamId = opp?.id,
                CourtId = court?.id ?? opp?.homeCourtId,
                RulesId = RulesId,
                TeamSize = TeamSize,
                StreetRules = true,
                DifficultyId = career?.settings?.difficultyId ?? DefaultContent.DefaultDifficultyId,
                ContextId = PickupContext,
                StartWithBall = 0,
            };
        }

        public static bool IsStreets(MatchRequest r) => r != null && r.Mode == GameMode.Streets;

        /// <summary>Records a finished game. Returns the line the result screen shows.</summary>
        public static string ApplyResult(StreetsSaveData s, bool won, int showstoppers, int tricks, int bestBank)
        {
            if (s == null) return "";
            s.games++;
            s.showstoppers += Math.Max(0, showstoppers);
            s.tricks += Math.Max(0, tricks);
            s.bestBank = Math.Max(s.bestBank, Math.Max(0, bestBank));
            if (won)
            {
                s.wins++;
                s.streak++;
                s.bestStreak = Math.Max(s.bestStreak, s.streak);
            }
            else
            {
                s.losses++;
                s.streak = 0;
            }
            return s.wins + "-" + s.losses + "  ·  " + showstoppers + " SHOWSTOPPER" + (showstoppers == 1 ? "" : "S") + "  ·  " + tricks + " TRICK" + (tricks == 1 ? "" : "S");
        }

        /// <summary>How to play, shown from the hub (and the first time you open THE STREETS).</summary>
        public static readonly string[] HowTo =
        {
            "3 ON 3 TO 21: 1s inside, 2s from deep, win by two. No clock, no fouls. The ball changes hands after every bucket.",
            "TRICK: tap TRICK with the ball. The stick picks it: still for a HALO, sideways BEHIND THE BACK, back THROUGH THE LEGS, at the rim ROLL THE ARM, and OFF THE HEAD when he's right in your face.",
            "COMBOS: chain tricks (stick moves count) and the combo grows. The same trick twice is worth less. Score to bank it into your STREET METER. Lose the ball and it's gone.",
            "OFF THE GLASS: driving at the rim from a few steps out, tap TRICK with the stick at the rim. You throw it off the backboard and finish your own alley-oop.",
            "DEFENSE: blocks and steals go straight into your meter.",
            "SHOWSTOPPER: fill the meter, then tap SHOWSTOPPER on offense. Score and you get +1 and they lose 1. Fill it twice before you use it for +2 / -2. Miss and it's wasted.",
            "HOMECOURT: clear SOUTHSIDE and claim a court. Crews come to take it; every defense builds your perks. Lose and you have to take it back.",
        };
    }
}
