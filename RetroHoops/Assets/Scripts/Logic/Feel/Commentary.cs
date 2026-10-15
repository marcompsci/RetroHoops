using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// MIC TALLY, the voice of the blacktop: short text commentary on the big moments (runs, lead
    /// changes, dunks, alley-oops, blocks, broken ankles, game point, buzzer beaters). At most one
    /// line every few seconds, never the same line twice in a row, and quiet during routine play.
    /// Settings ► COMMENTARY turns it off. All lines are original.
    /// </summary>
    public sealed class Commentary
    {
        public const string Speaker = "MIC";
        /// <summary>Seconds of game time between lines (big moments can cut in after half of it).</summary>
        public const float Gap = 5f;

        /// <summary>How big the last line was (4+ is a big call: the play-by-play voice speeds up for it).</summary>
        public int LastPriority => _lastPriority;

        private readonly SeededRandom _rng;
        private readonly string[] _teams;
        private float _lastAt = -99f;
        private int _lastPriority;
        private string _last = "";
        private int _runTeam = -1, _runPoints;
        private int _leader = -1;
        private bool _gamePointCalled;
        private readonly Dictionary<string, int> _used = new Dictionary<string, int>();

        /// <summary>Phase 37: a game that starts from a set score (CLUTCH): the team ahead is the one a lead change would overtake.</summary>
        public void StartFrom(int scoreA, int scoreB) => _leader = scoreA == scoreB ? -1 : (scoreA > scoreB ? 0 : 1);

        public Commentary(string teamA, string teamB, uint seed)
        {
            _teams = new[] { Cap(teamA), Cap(teamB) };
            _rng = new SeededRandom(seed == 0 ? 1u : seed);
        }

        private static string Cap(string s) => string.IsNullOrEmpty(s) ? "THEM" : s.ToUpperInvariant();

        /// <summary>
        /// Reacts to one simulation step's events. Returns a line to show, or null.
        /// <paramref name="names"/> gives a player's display name by index; <paramref name="shot"/> is the
        /// type of the shot that just went in (for ShotMade).
        /// </summary>
        public string React(MatchSimulation m, Func<int, string> names, ShotType shot)
        {
            string best = null;
            int priority = 0;
            void Offer(int p, string line)
            {
                if (line != null && p > priority) { priority = p; best = line; }
            }

            foreach (var e in m.Events)
            {
                string who = e.PlayerIndex >= 0 && names != null ? Cap(names(e.PlayerIndex)) : "";
                string team = e.Team >= 0 && e.Team < 2 ? _teams[e.Team] : "";
                switch (e.Type)
                {
                    case MatchEventType.ShotMade:
                        int scorer = e.Team;
                        if (scorer == _runTeam) _runPoints += Math.Max(1, e.Value);
                        else { _runTeam = scorer; _runPoints = Math.Max(1, e.Value); }
                        if (m.Ball.ShotBank) Offer(2, Pick("glass", who + " OFF THE GLASS!", "KISSED IT OFF THE BOARD! " + who + "!", "GLASS! " + who + " BANKS IT IN!"));
                        if (shot == ShotType.Dunk && m.StyleOn)
                            Offer(4, Pick("dunk.st", "OH MY GOODNESS! " + who + " THROWS IT DOWN!", "ARE YOU SERIOUS?! " + who + " JUST RATTLED THE WHOLE BACKBOARD!",
                                          "RIM RATTLER! " + who + " WITH AUTHORITY!", "HE'S GOT HOPS, FOLKS! " + who + " FROM THE SKY!", "BOOM! " + who + " HAMMERS IT HOME!"));
                        else if (shot == ShotType.Dunk) Offer(3, Pick("dunk", who + " THROWS IT DOWN!", "OH, " + who + " WITH THE HAMMER!", "SOMEBODY CHECK THE RIM, " + who + " JUST BENT IT!", who + " FLUSHES IT!"));
                        else if (e.Value >= 2 && !m.Setup.FullCourt || e.Value >= 3) Offer(2, Pick("deep", who + " FROM DOWNTOWN!", "BANG! " + who + " FROM DEEP!", who + " LETS IT FLY... GOT IT!"));
                        if (_runPoints >= RunSize(m)) Offer(2, Pick("run", team + " ON A " + _runPoints + "-0 RUN!", "SOMEBODY CALL A TIMEOUT, " + team + " ARE ROLLING!", team + " CAN'T MISS RIGHT NOW!"));
                        int lead = m.Score[0] == m.Score[1] ? -1 : (m.Score[0] > m.Score[1] ? 0 : 1);
                        if (lead >= 0 && _leader >= 0 && lead != _leader) Offer(3, Pick("lead", "LEAD CHANGE! " + _teams[lead] + " IN FRONT!", "AND " + _teams[lead] + " TAKE THE LEAD!"));
                        else if (lead < 0 && m.Score[0] > 0) Offer(2, Pick("tie", "ALL TIED UP AT " + m.Score[0] + "!", "DEAD EVEN! " + m.Score[0] + " APIECE!"));
                        if (lead >= 0) _leader = lead;
                        int target = m.Setup.Rules != null ? m.Setup.Rules.targetScore : 0;
                        if (!_gamePointCalled && !m.Setup.FullCourt && target > 0 && Math.Max(m.Score[0], m.Score[1]) >= target - 2 && Math.Max(m.Score[0], m.Score[1]) < target)
                        {
                            _gamePointCalled = true;
                            Offer(3, Pick("gp", "GAME POINT COMING UP!", "ONE MORE BUCKET AND IT'S OVER!"));
                        }
                        break;
                    case MatchEventType.AlleyOop:
                        Offer(4, Pick("oop", "ALLEY-OOP! " + who + " FROM THE SKY!", "LOB CITY! " + who + " FINISHES!", "OH THE OOP! " + who + "!"));
                        break;
                    case MatchEventType.AnkleBreaker:
                        if (m.StyleOn)
                        {
                            // THE STREETS: the booth loses it (college-broadcast style, all original lines).
                            string down = e.Value >= 0 && e.Value < m.Players.Length && names != null ? Cap(names(e.Value)) : "HIM";
                            Offer(6, Pick("ankles.st",
                                "OH MY! ANKLE BREAKER! " + who + " SENT " + down + " TO THE FLOOR!",
                                "ARE YOU KIDDING ME?! " + who + " TOOK " + down + "'S ANKLES RIGHT OFF!",
                                "CROSSOVER... AND DOWN GOES " + down + "! WE MAY NEED A TRAINER OUT HERE!",
                                "OH NO! " + down + " IS ON THE BLACKTOP AND " + who + " IS JUST WALKING AWAY!",
                                "THAT IS DISRESPECTFUL, FOLKS! " + who + " PUT " + down + " ON THE GROUND!",
                                "SOMEBODY BRING A CHAIR FOR " + down + "! " + who + " BROKE HIM DOWN!"));
                        }
                        else Offer(5, Pick("ankles", "OH! SOMEBODY'S DOWN! " + who + " BROKE SOME ANKLES!", "SOMEBODY GET A CHAIR! " + who + " WITH THE CROSSOVER!", who + " LEFT THEM ON THE FLOOR!"));
                        break;
                    case MatchEventType.SkillMove:
                        // THE STREETS: the booth calls the big moves too.
                        if (!m.StyleOn) break;
                        if (e.Value == (int)SkillMoveKind.Crossover)
                            Offer(2, Pick("cross.st", "WHAT A CROSSOVER BY " + who + "!", "LEFT, RIGHT, GOODBYE! " + who + "!", who + " CROSSES HIM UP!"));
                        else if (e.Value == (int)SkillMoveKind.StepBack)
                            Offer(2, Pick("stepback.st", "STEP-BACK BY " + who + "... LOOK AT ALL THAT ROOM!", who + " CREATES SPACE!", "OH, THE STEP-BACK! " + who + "!"));
                        break;
                    case MatchEventType.Block:
                        Offer(3, Pick("block", "REJECTED! " + who + " SAYS NO!", "GET THAT OUTTA HERE! " + who + "!", who + " SENDS IT BACK!"));
                        break;
                    case MatchEventType.Steal:
                        Offer(1, Pick("steal", "PICKED! " + who + " WITH THE STEAL!", who + " READ THAT ALL THE WAY!"));
                        break;
                    case MatchEventType.HeatUp:
                        Offer(3, Pick("heat", who + " IS HEATING UP!", "THREE STRAIGHT! " + who + " IS ON FIRE!"));
                        break;
                    case MatchEventType.Trap:
                        string star = e.Value >= 0 && names != null ? Cap(names(e.Value)) : "";
                        Offer(2, Pick("trap", "THEY'RE SENDING TWO AT " + star + "!", "DOUBLE TEAM ON " + star + "!", "TOO HOT TO GUARD ONE ON ONE: HERE COMES THE TRAP!"));
                        break;
                    case MatchEventType.DefenderBeaten:
                        Offer(2, Pick("beat", "WHAT A MOVE BY " + who + "!", who + " LEAVES HIM STANDING!", "TOO QUICK! " + who + " IS PAST HIM!"));
                        break;
                    case MatchEventType.EuroStep:
                        Offer(2, Pick("euro", who + " WITH THE EURO STEP!", "SIDE STEP, " + who + "! BEAUTIFUL!"));
                        break;
                    // 15 Oct 2026: THE STREETS.
                    case MatchEventType.StreetTrick:
                        if (e.Value == (int)StreetTrick.OffTheHead) Offer(5, Pick("head", "OFF HIS HEAD! " + who + " IS DISRESPECTFUL!", "BOUNCED IT OFF HIS DOME! " + who + "!", "SOMEBODY CHECK ON HIM! " + who + " USED HIS HEAD!"));
                        else if (e.Value == (int)StreetTrick.OffTheGlass) Offer(4, Pick("selfoop", who + " OFF THE GLASS TO HIMSELF!", "HE DOESN'T NEED A PASSER! " + who + "!"));
                        else if (e.Chance >= 150f) Offer(2, Pick("trick", who + " IS PUTTING ON A SHOW!", "LOOK AT THE HANDLE ON " + who + "!", who + " WITH THE WHOLE BAG!"));
                        break;
                    case MatchEventType.BreakerOn:
                        Offer(5, Pick("breakeron", team + " CALLED THE SHOWSTOPPER!", "HERE IT COMES! SHOWSTOPPER FOR " + team + "!", "EVERYBODY STAND UP! " + team + " WANT THE SHOWSTOPPER!"));
                        break;
                    case MatchEventType.BreakerScored:
                        Offer(6, Pick("breaker", "SHOWSTOPPER! " + team + " TAKE " + e.Value + " OFF THE BOARD!", "THAT'S A SHOWSTOPPER! THE GAME JUST FLIPPED!", "OH MY! " + team + " BROKE IT WIDE OPEN!"));
                        break;
                    case MatchEventType.BreakerEnded:
                        Offer(4, Pick("breakerend", "SHOWSTOPPER STOPPED!", "THEY WASTED THE SHOWSTOPPER!"));
                        break;
                    case MatchEventType.GameOver:
                        bool buzzer = m.Setup.Rules != null && m.Setup.Rules.useGameClock && m.GameClock <= 0.5f;
                        int w = m.Winner;
                        if (w >= 0)
                            Offer(6, buzzer && Math.Abs(m.Score[0] - m.Score[1]) <= 2
                                ? Pick("buzzer", "AT THE BUZZER! " + _teams[w] + " WIN IT!", "WITH NO TIME LEFT! " + _teams[w] + "!")
                                : Pick("final", "THAT'S THE GAME! " + _teams[w] + " WIN!", "IT'S OVER! " + _teams[w] + " TAKE IT!"));
                        break;
                }
            }

            if (best == null) return null;
            float since = m.Time - _lastAt;
            bool cutIn = priority > _lastPriority && since >= Gap * 0.5f;
            if (since < Gap && !cutIn && priority < 6) return null;
            if (best == _last) return null;
            _lastAt = m.Time;
            _lastPriority = priority;
            _last = best;
            return best;
        }

        private static int RunSize(MatchSimulation m) => m.Setup.FullCourt ? 8 : 4;

        /// <summary>A line from the set, avoiding the one used last time for this kind of moment.</summary>
        private string Pick(string kind, params string[] lines)
        {
            if (lines.Length == 0) return null;
            int i = _rng.Range(0, lines.Length);
            if (lines.Length > 1 && _used.TryGetValue(kind, out int prev) && prev == i) i = (i + 1) % lines.Length;
            _used[kind] = i;
            return lines[i];
        }
    }
}
