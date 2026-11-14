using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>Your club's front office in Franchise (I61–I70): credits, fans, facilities and what's waiting for you.</summary>
    [Serializable]
    public class FrontOfficeData
    {
        /// <summary>I61: FRONT OFFICE CREDITS, earned every game and spent on facilities.</summary>
        public int credits;
        /// <summary>I65: fan rating is 50 + this (0..100).</summary>
        public int fanDelta;
        /// <summary>I62: facility levels, 0..<see cref="FrontOffice.MaxLevel"/> (GYM, FILM ROOM, ARENA, PLAYERS' LOUNGE).</summary>
        public int gym, film, arena, lounge;
        /// <summary>I61: the post-game HUDDLE is waiting for your pick.</summary>
        public bool huddle;
        /// <summary>I64: a PRESS ROOM question waiting (−1 = none), and the player it's about.</summary>
        public int press = -1;
        public int pressPlayer = -1;
        /// <summary>I66: a LOCKER ROOM event waiting (−1 = none), the player it's about, and the week last checked.</summary>
        public int locker = -1;
        public int lockerPlayer = -1;
        public int lockerWeek = -1;
        /// <summary>Your games this franchise (played, coached or simulated), and the current streak (+ wins / − losses).</summary>
        public int games;
        public int streak;
        /// <summary>The last thing the front office did, for the HOME screen.</summary>
        public string note = "";

        // ---- I71–I80
        /// <summary>I71: game length for your Franchise games (0 = STANDARD 4 min, 1 = QUICK 2 min, 2 = LONG 6 min).</summary>
        public int length;
        /// <summary>I72 CLUB RECORDS: best win streak, biggest win, most points in a game.</summary>
        public int bestStreak, biggestWin, mostPoints;
        /// <summary>I73: the RIVAL club (team index, −1 = none yet).</summary>
        public int rival = -1;
        /// <summary>I74: the owner's goal this season (−1 = not set), the year it's for, and whether it's met.</summary>
        public int goal = -1;
        public int goalYear;
        /// <summary>I75: a player who has asked to be traded (−1 = none), and whether you've talked to him this season.</summary>
        public int tradeAsk = -1;
        public bool talked;
        /// <summary>I76: the team captain (player id, −1 = none).</summary>
        public int captain = -1;
        /// <summary>I79: credits and fans this season, and last season's front-office review.</summary>
        public int seasonCredits;
        public int seasonFans = FrontOffice.BaseFans;
        public List<string> review = new List<string>();
        /// <summary>I103: whether last season's owner's goal was met.</summary>
        public bool lastGoalMet = true;
    }

    public enum Facility { Gym = 0, Film = 1, Arena = 2, Lounge = 3 }

    public enum HuddleChoice { Owner = 0, Staff = 1, Fans = 2 }

    /// <summary>
    /// I61–I70, from what players love in the retro football and soccer hits: a light front office between games.
    /// After every game you pick who to thank (owner = credits, staff = morale, fans = hype), the press asks you
    /// a question now and then, the locker room throws up a decision, and credits build GYM / FILM ROOM / ARENA /
    /// PLAYERS' LOUNGE. Players have moods; a happy room plays a touch better and signs for less, an unhappy one
    /// costs you. Each title turns up the league's heat. All original; nothing here is real money.
    /// </summary>
    public static partial class FrontOffice
    {
        public const int BaseMorale = 70;
        public const int BaseFans = 50;
        public const int MaxLevel = 3;

        public static readonly string[] FacilityNames = { "GYM", "FILM ROOM", "ARENA", "PLAYERS' LOUNGE" };
        public static readonly string[] FacilityBlurbs =
        {
            "Young players grow faster every off-season.",
            "More scouting points each draft, and a sharper game plan.",
            "Home wins earn more credits, and the fans warm up quicker.",
            "Losses and the bench sting less; moods settle faster.",
        };

        public static FrontOfficeData Of(FranchiseSaveData f) => f.office ?? (f.office = new FrontOfficeData());

        // ------------------------------------------------------------------ morale (I63)

        public static int Morale(FrPlayer p) => Clamp(BaseMorale + p.mood, 0, 100);

        public static string MoodName(int morale) =>
            morale >= 85 ? "FIRED UP" : morale >= 65 ? "HAPPY" : morale >= 45 ? "OKAY" : morale >= 25 ? "UNHAPPY" : "WANTS OUT";

        public static void AddMood(FrPlayer p, int by) => p.mood = Clamp(BaseMorale + p.mood + by, 0, 100) - BaseMorale;

        /// <summary>Your rotation's average morale (top seven).</summary>
        public static float TeamMorale(FranchiseSaveData f, int team)
        {
            var r = Franchise.Roster(f, team);
            int n = Math.Min(7, r.Count);
            if (n == 0) return BaseMorale;
            float sum = 0f;
            for (int i = 0; i < n; i++) sum += Morale(r[i]);
            return sum / n;
        }

        /// <summary>I68: a happy room shoots a touch better in the games you play (0 to +3%); an unhappy one a touch worse.</summary>
        public static float ChemistryFor(FranchiseSaveData f)
        {
            float m = TeamMorale(f, f.you);
            if (m >= BaseMorale) return Math.Min(0.03f, (m - BaseMorale) / 30f * 0.03f);
            return Math.Max(-0.02f, (m - BaseMorale) / 40f * 0.02f);
        }

        // ------------------------------------------------------------------ fans and facilities (I62, I65)

        public static int Fans(FranchiseSaveData f) => Clamp(BaseFans + Of(f).fanDelta, 0, 100);

        public static void AddFans(FranchiseSaveData f, int by) => Of(f).fanDelta = Clamp(BaseFans + Of(f).fanDelta + by, 0, 100) - BaseFans;

        public static int Level(FranchiseSaveData f, Facility which)
        {
            var o = Of(f);
            switch (which)
            {
                case Facility.Gym: return o.gym;
                case Facility.Film: return o.film;
                case Facility.Arena: return o.arena;
                default: return o.lounge;
            }
        }

        /// <summary>Credits for the next level (0 when maxed).</summary>
        public static int UpgradeCost(FranchiseSaveData f, Facility which)
        {
            int lv = Level(f, which);
            return lv >= MaxLevel ? 0 : 6 * (lv + 1);
        }

        public static bool Upgrade(FranchiseSaveData f, Facility which)
        {
            var o = Of(f);
            int cost = UpgradeCost(f, which);
            if (cost <= 0 || o.credits < cost) return false;
            o.credits -= cost;
            switch (which)
            {
                case Facility.Gym: o.gym++; break;
                case Facility.Film: o.film++; break;
                case Facility.Arena: o.arena++; break;
                default: o.lounge++; break;
            }
            o.note = FacilityNames[(int)which] + " upgraded to level " + Level(f, which) + ".";
            return true;
        }

        /// <summary>I62 GYM: extra off-season growth for your young players who haven't hit their potential.</summary>
        public static int GymGrowth(FranchiseSaveData f, FrPlayer p)
        {
            int lv = Of(f).gym;
            if (lv <= 0 || p.age > 25 || p.Overall >= p.potential) return 0;
            return Math.Min(p.potential - p.Overall, (lv + 1) / 2);
        }

        public static int FilmScoutPoints(FranchiseSaveData f) => Of(f).film;

        /// <summary>
        /// Sim strength on top of the ratings: your film room and your room's mood, and for everyone else the league's
        /// heat (I67 RISING CHALLENGE: each of your titles makes the other clubs a little better prepared, up to four).
        /// </summary>
        public static float StrengthEdge(FranchiseSaveData f, int team)
        {
            if (f == null) return 0f;
            if (team == f.you) return Of(f).film * 0.3f + (TeamMorale(f, team) - BaseMorale) / 30f;
            return Heat(f) * 0.5f;
        }

        public static int Heat(FranchiseSaveData f) => Math.Min(4, f.titles);

        // ------------------------------------------------------------------ after each of your games (I61, I63, I64, I65)

        public static void AfterYourGame(FranchiseSaveData f, bool won, int margin, bool home, int yourScore = 0, int opponent = -1)
        {
            var o = Of(f);
            SeasonGoal(f); // I74: the owner sets this season's goal on your first game
            bool rival = opponent >= 0 && opponent == o.rival;
            Records(f, won, margin, yourScore);
            o.games++;
            o.streak = won ? Math.Max(1, o.streak + 1) : Math.Min(-1, o.streak - 1);
            // Credits: one for showing up, two more for a win, the ARENA pays on packed home wins.
            int credits = 1 + (won ? 2 : 0) + (won && home && Fans(f) >= 60 ? o.arena : 0)
                          + (home && IsSellout(f) ? 1 : 0) + (won && rival ? 2 : 0);
            o.credits += credits;
            o.seasonCredits += credits;
            AddFans(f, (won ? 1 : -1) + (won && home && o.arena > 0 ? 1 : 0) + (won && margin >= 10 ? 1 : 0) + (rival ? (won ? 2 : -1) : 0));
            // Moods: winning helps everyone; starters like starting; the deep bench gets restless. The lounge softens it.
            var r = Franchise.Roster(f, f.you);
            for (int i = 0; i < r.Count; i++)
            {
                int d = won ? 2 : -2;
                if (i < 5) d += 1;
                else if (i >= 7) d -= 2;
                if (d < 0) d = Math.Min(0, d + o.lounge / 2 + (o.lounge >= MaxLevel ? 1 : 0) + (CaptainSteadies(f) ? 1 : 0));
                AddMood(r[i], d);
                // Moods drift back towards normal a little every game (faster with a lounge).
                if (r[i].mood > 0) r[i].mood = Math.Max(0, r[i].mood - 1);
                else if (r[i].mood < 0) r[i].mood = Math.Min(0, r[i].mood + 1 + o.lounge / 2);
            }
            TradeAskDrag(f); // I75: a player who wants out wears on the room
            o.huddle = true; // a new game: the old huddle (if you skipped it) is gone, this one is waiting
            // I64: the press want you every third game, and after any three-game streak.
            if (o.press < 0 && (o.games % 3 == 0 || Math.Abs(o.streak) == 3))
            {
                var rng = new SeededRandom(StableHash.Of("fr:press:" + f.seed + ":" + f.year + ":" + o.games));
                o.press = PickPress(o.streak, rng);
                o.pressPlayer = r.Count > 0 ? r[Math.Min(r.Count - 1, rng.Range(0, Math.Min(5, r.Count)))].id : -1;
            }
        }

        // ------------------------------------------------------------------ the huddle (I61)

        public static readonly string[] HuddleNames = { "THANK THE OWNER", "PRAISE THE STAFF", "SALUTE THE FANS" };
        public static readonly string[] HuddleBlurbs = { "+3 credits", "+5 mood for the whole team", "+4 fan rating" };

        public static bool Huddle(FranchiseSaveData f, HuddleChoice choice)
        {
            var o = Of(f);
            if (!o.huddle) return false;
            o.huddle = false;
            switch (choice)
            {
                case HuddleChoice.Owner:
                    o.credits += 3;
                    o.note = "The owner liked that: +3 credits.";
                    break;
                case HuddleChoice.Staff:
                    foreach (var p in Franchise.Roster(f, f.you)) AddMood(p, 5);
                    o.note = "The staff and players felt it: +5 mood.";
                    break;
                default:
                    AddFans(f, 4);
                    o.note = "The crowd loved it: +4 fans.";
                    break;
            }
            return true;
        }

        // ------------------------------------------------------------------ the press room (I64)

        public sealed class Question
        {
            public string text;
            public string[] answers;
            /// <summary>Per answer: credits, fans, team mood, the named player's mood.</summary>
            public int[,] effects;
            /// <summary>Asked after a win streak (+1), a losing streak (−1) or any time (0).</summary>
            public int mood;
        }

        /// <summary>{P} is the player the question is about.</summary>
        public static readonly Question[] Questions =
        {
            new Question { mood = 1, text = "Three straight wins. Is this team for real?", answers = new[] { "WE'RE JUST GETTING STARTED", "ONE GAME AT A TIME", "ASK ME IN THE PLAYOFFS" },
                effects = new[,] { { 0, 4, 2, 0 }, { 1, 1, 1, 0 }, { 2, -1, 0, 0 } } },
            new Question { mood = -1, text = "Three losses in a row. What's going wrong?", answers = new[] { "THAT'S ON ME", "THE PLAYERS HAVE TO BE BETTER", "WE'LL BE FINE" },
                effects = new[,] { { 0, 2, 4, 0 }, { 2, 0, -5, 0 }, { 0, -2, 1, 0 } } },
            new Question { mood = 0, text = "{P} has been quiet lately. Still your guy?", answers = new[] { "HE'S OUR GUY", "EVERYONE HAS TO EARN IT", "NO COMMENT" },
                effects = new[,] { { 0, 1, 0, 8 }, { 1, 0, 1, -6 }, { 0, -1, 0, 0 } } },
            new Question { mood = 0, text = "Fans want more shots for {P}. Will he get them?", answers = new[] { "YOU'LL SEE MORE OF HIM", "WE PLAY THE RIGHT WAY", "THE FANS AREN'T COACHING" },
                effects = new[,] { { 0, 3, -1, 6 }, { 1, 0, 2, -2 }, { 1, -5, 1, 0 } } },
            new Question { mood = 0, text = "Is this the year you go for the title?", answers = new[] { "TITLE OR BUST", "WE'RE BUILDING", "WE'RE HAPPY WHERE WE ARE" },
                effects = new[,] { { 0, 4, 1, 0 }, { 2, -1, 0, 0 }, { 1, -3, 1, 0 } } },
            new Question { mood = 0, text = "Your rival's coach says you're overrated. Response?", answers = new[] { "SCOREBOARD", "WE RESPECT EVERYONE", "SEE YOU ON THE COURT" },
                effects = new[,] { { 0, 4, 2, 0 }, { 2, 0, 0, 0 }, { 0, 2, 3, 0 } } },
            new Question { mood = 0, text = "{P} wants a bigger role. Will he start?", answers = new[] { "HE'S EARNED A LOOK", "HE KNOWS WHAT TO WORK ON", "THAT STAYS IN THE LOCKER ROOM" },
                effects = new[,] { { 0, 1, -1, 7 }, { 1, 0, 1, -4 }, { 0, -1, 1, 1 } } },
            new Question { mood = 0, text = "Any message for the fans tonight?", answers = new[] { "THANK YOU, KEEP IT LOUD", "WE OWE YOU A WIN", "LET'S FILL THE BUILDING" },
                effects = new[,] { { 0, 3, 0, 0 }, { 0, 2, 1, 0 }, { 2, 1, 0, 0 } } },
        };

        private static int PickPress(int streak, SeededRandom rng)
        {
            if (streak >= 3) return 0;
            if (streak <= -3) return 1;
            return 2 + rng.Range(0, Questions.Length - 2);
        }

        public static string PressText(FranchiseSaveData f)
        {
            var o = Of(f);
            if (o.press < 0 || o.press >= Questions.Length) return "";
            return Loc.T(Questions[o.press].text).Replace("{P}", Franchise.Player(f, o.pressPlayer)?.Name ?? "Your star");
        }

        public static bool AnswerPress(FranchiseSaveData f, int answer)
        {
            var o = Of(f);
            if (o.press < 0 || o.press >= Questions.Length || answer < 0 || answer > 2) return false;
            var q = Questions[o.press];
            Apply(f, q.effects[answer, 0], q.effects[answer, 1], q.effects[answer, 2], Franchise.Player(f, o.pressPlayer), q.effects[answer, 3]);
            o.note = "PRESS ROOM: \"" + q.answers[answer] + "\"  " + Effects(q.effects[answer, 0], q.effects[answer, 1], q.effects[answer, 2], q.effects[answer, 3]);
            o.press = -1;
            o.pressPlayer = -1;
            return true;
        }

        public static void SkipPress(FranchiseSaveData f)
        {
            var o = Of(f);
            if (o.press < 0) return;
            AddFans(f, -1); // no comment is a comment
            o.note = "You skipped the press. -1 fans.";
            o.press = -1;
            o.pressPlayer = -1;
        }

        // ------------------------------------------------------------------ the locker room (I66)

        public sealed class LockerEvent
        {
            public string text;
            public string[] choices;
            /// <summary>Per choice: credits, fans, team mood, the named player's mood.</summary>
            public int[,] effects;
        }

        public static readonly LockerEvent[] Events =
        {
            new LockerEvent { text = "{P} was late to practice again.", choices = new[] { "SIT HIM A QUARTER", "LET IT SLIDE" },
                effects = new[,] { { 0, 0, 2, -6 }, { 0, 0, -3, 3 } } },
            new LockerEvent { text = "{P} wants to run a free kids' clinic at the park.", choices = new[] { "WE'LL PAY FOR IT", "NOT DURING THE SEASON" },
                effects = new[,] { { -2, 5, 1, 4 }, { 0, -1, 0, -3 } } },
            new LockerEvent { text = "The players want a team dinner after the road trip.", choices = new[] { "DINNER'S ON US", "SAVE THE MONEY" },
                effects = new[,] { { -2, 0, 5, 0 }, { 1, 0, -2, 0 } } },
            new LockerEvent { text = "{P} is unhappy with his minutes.", choices = new[] { "PROMISE MORE MINUTES", "HE HAS TO EARN THEM" },
                effects = new[,] { { 0, 0, -1, 8 }, { 0, 0, 1, -5 } } },
            new LockerEvent { text = "A local station wants {P} for a halftime feature.", choices = new[] { "SAY YES", "KEEP HIM FOCUSED" },
                effects = new[,] { { 2, 3, 0, 3 }, { 0, 0, 1, -1 } } },
            new LockerEvent { text = "Two players got into it at practice.", choices = new[] { "CLEAR THE AIR TOGETHER", "EXTRA SPRINTS FOR BOTH" },
                effects = new[,] { { 0, 0, 3, 0 }, { 0, 0, -1, -2 } } },
        };

        /// <summary>Called after each regular-season week: about one week in four, the locker room needs a decision.</summary>
        public static void AfterWeek(FranchiseSaveData f)
        {
            var o = Of(f);
            if (f.phase != FranchisePhase.Regular) return;
            CheckTradeAsk(f);
            int week = f.season.currentWeek;
            if (o.lockerWeek == week || o.locker >= 0) return;
            o.lockerWeek = week;
            var rng = new SeededRandom(StableHash.Of("fr:locker:" + f.seed + ":" + f.year + ":" + week));
            if (!rng.Chance(0.25f)) return;
            var r = Franchise.Roster(f, f.you);
            if (r.Count == 0) return;
            // The unhappiest player is the likeliest story.
            FrPlayer who = r[rng.Range(0, r.Count)];
            foreach (var p in r) if (Morale(p) < 35 && Morale(p) < Morale(who)) who = p;
            o.locker = Morale(who) < 45 ? 3 : rng.Range(0, Events.Length);
            o.lockerPlayer = who.id;
        }

        public static string LockerText(FranchiseSaveData f)
        {
            var o = Of(f);
            if (o.locker < 0 || o.locker >= Events.Length) return "";
            return Loc.T(Events[o.locker].text).Replace("{P}", Franchise.Player(f, o.lockerPlayer)?.Name ?? "A player");
        }

        public static bool DecideLocker(FranchiseSaveData f, int choice)
        {
            var o = Of(f);
            if (o.locker < 0 || o.locker >= Events.Length || choice < 0 || choice > 1) return false;
            var e = Events[o.locker];
            if (e.effects[choice, 0] < 0 && o.credits < -e.effects[choice, 0]) return false; // can't pay for it
            Apply(f, e.effects[choice, 0], e.effects[choice, 1], e.effects[choice, 2], Franchise.Player(f, o.lockerPlayer), e.effects[choice, 3]);
            o.note = "LOCKER ROOM: " + e.choices[choice] + "  " + Effects(e.effects[choice, 0], e.effects[choice, 1], e.effects[choice, 2], e.effects[choice, 3]);
            o.locker = -1;
            o.lockerPlayer = -1;
            return true;
        }

        // ------------------------------------------------------------------ contracts and the season (I63, I65, I67)

        /// <summary>I63: a happy player gives you a hometown discount; an unhappy one wants paying to stay.</summary>
        public static int ReSignSalary(FrPlayer p, int demand)
        {
            int m = Morale(p);
            if (m >= 85) return Math.Max(Franchise.MinSalary, demand * 9 / 10);
            if (m < 40) return demand * 6 / 5;
            return demand;
        }

        /// <summary>End of season: a title, the playoffs or missing them moves the fans; moods reset most of the way.</summary>
        public static void AfterSeason(FranchiseSaveData f, int finish)
        {
            var o = Of(f);
            SeasonReview(f, finish); // I74 goal, I79 review (before the reset below)
            AddFans(f, finish >= 3 ? 10 : finish >= 1 ? 4 : -4);
            o.credits += finish >= 3 ? 6 : finish >= 1 ? 3 : 1;
            foreach (var p in f.players) p.mood /= 2;
            o.streak = 0;
            o.huddle = false;
            o.press = -1;
            o.locker = -1;
        }

        // ------------------------------------------------------------------ helpers

        private static void Apply(FranchiseSaveData f, int credits, int fans, int team, FrPlayer player, int playerMood)
        {
            var o = Of(f);
            o.credits = Math.Max(0, o.credits + credits);
            AddFans(f, fans);
            if (team != 0) foreach (var p in Franchise.Roster(f, f.you)) AddMood(p, team);
            if (player != null && playerMood != 0) AddMood(player, playerMood);
        }

        public static string Effects(int credits, int fans, int team, int player)
        {
            var parts = new List<string>();
            if (credits != 0) parts.Add((credits > 0 ? "+" : "") + credits + " " + Loc.T("credits"));
            if (fans != 0) parts.Add((fans > 0 ? "+" : "") + fans + " " + Loc.T("fans"));
            if (team != 0) parts.Add((team > 0 ? "+" : "") + team + " " + Loc.T("team mood"));
            if (player != 0) parts.Add((player > 0 ? "+" : "") + player + " " + Loc.T("his mood"));
            return string.Join(", ", parts);
        }

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
