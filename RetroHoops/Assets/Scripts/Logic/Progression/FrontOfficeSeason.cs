using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>
    /// I71–I80: more of what players love in the retro sports hits, on top of the I61–I70 front office.
    /// Quick games, a club record book, a rival, the owner's season goal, trade requests, a captain, sell-outs and
    /// an end-of-season review.
    /// </summary>
    public static partial class FrontOffice
    {
        // ------------------------------------------------------------------ I71 game length

        public static readonly string[] LengthNames = { "STANDARD", "QUICK", "LONG" };
        public static readonly float[] LengthSeconds = { 240f, 120f, 360f };

        public static float GameSeconds(FranchiseSaveData f)
        {
            int i = Of(f).length;
            return i >= 0 && i < LengthSeconds.Length ? LengthSeconds[i] : LengthSeconds[0];
        }

        /// <summary>The Full Court rules with your chosen clock (null = the normal four minutes, no override needed).</summary>
        public static GameRulesDef RulesFor(FranchiseSaveData f, ContentCatalog c)
        {
            if (Of(f).length == 0) return null;
            var baseRules = c.Find(c.Rules, FullCourt.RulesId);
            if (baseRules == null) return null;
            var r = baseRules.CloneRules();
            r.gameClockSeconds = GameSeconds(f);
            return r;
        }

        // ------------------------------------------------------------------ I72 club records

        private static void Records(FranchiseSaveData f, bool won, int margin, int yourScore)
        {
            var o = Of(f);
            int streak = won ? Math.Max(1, o.streak + 1) : 0;
            if (streak > o.bestStreak)
            {
                o.bestStreak = streak;
                if (streak >= 3) o.note = "CLUB RECORD: " + streak + " straight wins.";
            }
            if (won && margin > o.biggestWin) o.biggestWin = margin;
            if (yourScore > o.mostPoints)
            {
                if (o.mostPoints > 0) o.note = "CLUB RECORD: " + yourScore + " points in a game.";
                o.mostPoints = yourScore;
            }
        }

        public static List<string> RecordLines(FranchiseSaveData f)
        {
            var o = Of(f);
            var lines = new List<string>
            {
                Loc.T("LONGEST WIN STREAK") + "  " + o.bestStreak,
                Loc.T("BIGGEST WIN") + "  +" + o.biggestWin,
                Loc.T("MOST POINTS IN A GAME") + "  " + o.mostPoints,
                Loc.T("TITLES") + "  " + f.titles,
            };
            int bestWins = 0, bestYear = 0;
            foreach (var h in f.history)
                if (h.team == f.you && h.wins > bestWins) { bestWins = h.wins; bestYear = h.year; }
            if (bestWins > 0) lines.Add(Loc.T("BEST SEASON") + "  " + bestWins + " " + Loc.T("wins") + " (" + Loc.T("YEAR") + " " + bestYear + ")");
            return lines;
        }

        // ------------------------------------------------------------------ I73 rival

        /// <summary>Who knocked you out last time becomes your RIVAL: beat them for extra credits and fans.</summary>
        public static int Rival(FranchiseSaveData f) => Of(f).rival;

        private static void SetRivalFromPlayoffs(FranchiseSaveData f)
        {
            string you = Franchise.TeamId(f.you);
            ScheduledGame last = null;
            foreach (var g in f.season.games)
                if (g.round > 0 && g.played && g.Involves(you) && (last == null || g.round >= last.round)) last = g;
            if (last == null || last.WinnerId == you) return;
            int other = Franchise.IndexOf(last.homeId == you ? last.awayId : last.homeId);
            if (other >= 0 && other != f.you) Of(f).rival = other;
        }

        // ------------------------------------------------------------------ I74 the owner's goal

        public static readonly string[] GoalNames = { "MAKE THE PLAYOFFS", "WIN HALF YOUR GAMES", "REACH THE FINAL", "GET THE FANS TO 70" };

        private static void SeasonGoal(FranchiseSaveData f)
        {
            var o = Of(f);
            if (o.goalYear == f.year && o.goal >= 0) return;
            o.goalYear = f.year;
            o.seasonCredits = 0;
            o.seasonFans = Fans(f);
            o.talked = false;
            // The owner reads the room: a contender is asked for more, a rebuilding club for less.
            float you = Franchise.Strength(f, f.you);
            int better = 0;
            for (int t = 0; t < f.teams.Count; t++) if (t != f.you && Franchise.Strength(f, t) > you) better++;
            if (better <= 1) o.goal = 2;
            else if (better <= 3) o.goal = 0;
            else if (Fans(f) < 60) o.goal = 3;
            else o.goal = 1;
        }

        public static string GoalText(FranchiseSaveData f)
        {
            var o = Of(f);
            return o.goal >= 0 && o.goal < GoalNames.Length ? GoalNames[o.goal] : "";
        }

        public static bool GoalMet(FranchiseSaveData f, int finish)
        {
            var o = Of(f);
            var row = SeasonEngine.Standings(f.season).Find(r => r.TeamId == Franchise.TeamId(f.you));
            int wins = row?.Wins ?? 0, losses = row?.Losses ?? 0;
            switch (o.goal)
            {
                case 0: return finish >= 1;
                case 1: return wins >= losses && wins > 0;
                case 2: return finish >= 2;
                case 3: return Fans(f) >= 70;
                default: return false;
            }
        }

        public const int GoalReward = 10;

        // ------------------------------------------------------------------ I75 trade requests

        /// <summary>A player whose mood has hit WANTS OUT asks to be moved (one at a time).</summary>
        private static void CheckTradeAsk(FranchiseSaveData f)
        {
            var o = Of(f);
            if (o.tradeAsk >= 0)
            {
                var p = Franchise.Player(f, o.tradeAsk);
                if (p == null || p.team != f.you || Morale(p) >= 45)
                {
                    if (p != null && p.team == f.you) o.note = p.Name + " has calmed down. Trade request withdrawn.";
                    o.tradeAsk = -1;
                }
                return;
            }
            foreach (var p in Franchise.Roster(f, f.you))
                if (Morale(p) < 25 && !p.created)
                {
                    o.tradeAsk = p.id;
                    o.note = p.Name + " has asked for a trade.";
                    return;
                }
        }

        private static void TradeAskDrag(FranchiseSaveData f)
        {
            var o = Of(f);
            if (o.tradeAsk < 0) return;
            var asker = Franchise.Player(f, o.tradeAsk);
            if (asker == null || asker.team != f.you) { o.tradeAsk = -1; return; }
            foreach (var p in Franchise.Roster(f, f.you)) if (p.id != asker.id) AddMood(p, -1);
        }

        public const int TalkCost = 3;

        /// <summary>Sit down with him: costs credits, once a season, and lifts his mood enough to stay.</summary>
        public static bool TalkItOut(FranchiseSaveData f)
        {
            var o = Of(f);
            var p = o.tradeAsk >= 0 ? Franchise.Player(f, o.tradeAsk) : null;
            if (p == null || o.talked || o.credits < TalkCost) return false;
            o.credits -= TalkCost;
            o.talked = true;
            AddMood(p, 25);
            if (Morale(p) >= 45) o.tradeAsk = -1;
            o.note = "You talked it out with " + p.Name + ".";
            return true;
        }

        // ------------------------------------------------------------------ I76 captain

        public static bool SetCaptain(FranchiseSaveData f, int playerId)
        {
            var p = Franchise.Player(f, playerId);
            if (p == null || p.team != f.you) return false;
            Of(f).captain = playerId;
            Of(f).note = p.Name + " is your captain.";
            return true;
        }

        public static FrPlayer Captain(FranchiseSaveData f)
        {
            var p = Of(f).captain >= 0 ? Franchise.Player(f, Of(f).captain) : null;
            return p != null && p.team == f.you ? p : null;
        }

        /// <summary>A happy captain on the roster takes the edge off losses for everyone.</summary>
        public static bool CaptainSteadies(FranchiseSaveData f)
        {
            var c = Captain(f);
            return c != null && Morale(c) >= 65;
        }

        // ------------------------------------------------------------------ I77 sell-outs

        public const int SelloutFans = 80;

        public static bool IsSellout(FranchiseSaveData f) => Fans(f) >= SelloutFans;

        /// <summary>A sold-out home crowd is worth a little extra in simulated home games.</summary>
        public static float HomeEdge(FranchiseSaveData f, int homeTeam) => homeTeam == f.you && IsSellout(f) ? 0.75f : 0f;

        // ------------------------------------------------------------------ I79 season review

        private static void SeasonReview(FranchiseSaveData f, int finish)
        {
            var o = Of(f);
            SeasonGoal(f); // a season simmed with no game of yours recorded still gets judged
            var lines = new List<string>();
            bool met = GoalMet(f, finish);
            lines.Add(Loc.T("OWNER'S GOAL") + ": " + Loc.T(GoalText(f)) + "  ·  " + Loc.T(met ? "MET" : "MISSED"));
            if (met)
            {
                o.credits += GoalReward;
                AddFans(f, 5);
            }
            else AddFans(f, -5);
            lines.Add(Loc.T("CREDITS EARNED") + "  " + o.seasonCredits);
            int fansChange = Fans(f) - o.seasonFans;
            lines.Add(Loc.T("FANS") + "  " + o.seasonFans + " → " + Fans(f) + (fansChange >= 0 ? "  (+" + fansChange + ")" : "  (" + fansChange + ")"));
            lines.Add(Loc.T("TEAM MOOD") + "  " + (int)Math.Round(TeamMorale(f, f.you)));
            SetRivalFromPlayoffs(f);
            o.review = lines;
            o.goal = -1;
            o.tradeAsk = -1;
        }
    }
}
