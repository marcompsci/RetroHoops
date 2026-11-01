using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>What a batch of AI-vs-AI games looked like (I49 BALANCE PROBE).</summary>
    public sealed class BalanceReport
    {
        public int Games, Stalls, HomeWins;
        public int Shots, RimShots, ArcShots, Makes;
        public int OffensiveRebounds, DefensiveRebounds;
        public int Steals, Turnovers, Possessions;
        public int DriveFinishes, KickOuts, CloseOuts, PostMoves, Plays;
        /// <summary>I58 BALANCE PROBE v2.</summary>
        public int Assists, AiFakes, ScorerFeeds, Putbacks, ScreenCalls;
        public int TotalMargin;

        public float HomeWinShare => Games > 0 ? HomeWins / (float)Games : 0f;
        public float OffensiveReboundShare => OffensiveRebounds + DefensiveRebounds > 0 ? OffensiveRebounds / (float)(OffensiveRebounds + DefensiveRebounds) : 0f;
        public float RimShotsPerGame => Games > 0 ? RimShots / (float)Games : 0f;
        public float ArcShare => Shots > 0 ? ArcShots / (float)Shots : 0f;
        public float FieldGoalShare => Shots > 0 ? Makes / (float)Shots : 0f;
        public float TurnoverShare => Possessions > 0 ? Turnovers / (float)Possessions : 0f;
        public float AverageMargin => Games > 0 ? TotalMargin / (float)Games : 0f;

        public override string ToString() =>
            $"{Games} games: home {HomeWinShare:0.00}, FG {FieldGoalShare:0.00}, arc share {ArcShare:0.00}, rim shots {RimShotsPerGame:0.0}/game, "
            + $"OREB {OffensiveReboundShare:0.00}, TOV {TurnoverShare:0.00}, margin {AverageMargin:0.0}, stalls {Stalls}";
    }

    /// <summary>
    /// I49 BALANCE PROBE: plays seeded AI-vs-AI Demo games headless and tallies the numbers that tell you whether the
    /// game plays like basketball — fair home/away, shots at the rim, real defensive rebounding, some turnovers.
    /// Deterministic for the same inputs, so the tests can pin it.
    /// </summary>
    public static class BalanceProbe
    {
        public const float StepSeconds = 1f / 60f;
        /// <summary>A game that hasn't ended after this long is counted as a stall.</summary>
        public const float MaxGameSeconds = 900f;

        public static BalanceReport Run(ContentCatalog content, string difficultyId, int games, uint seedBase = 5)
        {
            var report = new BalanceReport();
            var league = content.TeamsInTier(TeamTier.League);
            if (league.Count < 2 || games <= 0) return report;
            var pick = new SeededRandom(seedBase * 7u + 3u);
            for (int g = 0; g < games; g++)
            {
                var home = league[pick.Range(0, league.Count)];
                var away = league[pick.Range(0, league.Count)];
                if (away == home) away = league[(league.IndexOf(home) + 1) % league.Count];
                var request = new MatchRequest
                {
                    Mode = GameMode.Demo, HomeTeamId = home.id, AwayTeamId = away.id, CourtId = home.homeCourtId,
                    DifficultyId = difficultyId, Seed = (uint)(g * 31) + seedBase,
                };
                PlayOne(new MatchSimulation(MatchSetup.FromRequest(request, content)), report);
            }
            return report;
        }

        private static void PlayOne(MatchSimulation m, BalanceReport r)
        {
            int lastShotTeam = -1, lastOffense = -1;
            float t = 0f;
            while (!m.IsOver && t < MaxGameSeconds)
            {
                m.Step(StepSeconds, default);
                t += StepSeconds;
                foreach (var e in m.Events)
                {
                    switch (e.Type)
                    {
                        case MatchEventType.ShotStarted:
                            if (e.Value == (int)ShotType.Layup || e.Value == (int)ShotType.Dunk) r.RimShots++;
                            break;
                        case MatchEventType.ShotReleased:
                            lastShotTeam = e.Team;
                            break;
                        case MatchEventType.Rebound:
                            if (e.Team == lastShotTeam) r.OffensiveRebounds++;
                            else r.DefensiveRebounds++;
                            break;
                        case MatchEventType.PlayCalled:
                            r.Plays++;
                            break;
                    }
                }
                if (m.OffenseTeam != lastOffense)
                {
                    if (lastOffense >= 0) r.Possessions++;
                    lastOffense = m.OffenseTeam;
                }
            }
            r.Games++;
            if (!m.IsOver) r.Stalls++;
            if (m.Score[0] > m.Score[1]) r.HomeWins++;
            r.TotalMargin += Math.Abs(m.Score[0] - m.Score[1]);
            foreach (var p in m.Stats.players)
            {
                r.Shots += p.fieldGoalsAttempted;
                r.Makes += p.fieldGoalsMade;
                r.ArcShots += p.arcAttempted;
                r.Steals += p.steals;
                r.Turnovers += p.turnovers;
                r.Assists += p.assists;
            }
            r.DriveFinishes += m.DriveFinishes;
            r.KickOuts += m.KickOuts;
            r.CloseOuts += m.CloseOuts;
            r.PostMoves += m.PostMoves;
            r.AiFakes += m.AiFakes;
            r.ScorerFeeds += m.ScorerFeeds;
            r.Putbacks += m.Putbacks;
            r.ScreenCalls += m.ScreenCalls;
        }
    }
}
