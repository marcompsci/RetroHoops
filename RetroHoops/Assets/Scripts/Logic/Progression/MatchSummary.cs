using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>One box-score row with display info, detached from the live simulation.</summary>
    [Serializable]
    public class SummaryLine
    {
        public int playerIndex;
        public int team;
        public string playerId;
        public string name;
        public int jerseyNumber;
        public bool isHuman;
        public PlayerStatLine stats;
    }

    /// <summary>
    /// Everything the post-game screen and progression need from a finished match. Built once
    /// from a <see cref="MatchSimulation"/>; its <see cref="matchId"/> guards against granting
    /// rewards twice.
    /// </summary>
    [Serializable]
    public class MatchSummary
    {
        public string matchId;
        /// <summary>Local calendar day the game was played (for match history).</summary>
        public int day;
        public GameMode mode;
        public string teamAId;
        public string teamBId;
        public string teamAName;
        public string teamBName;
        public int scoreA;
        public int scoreB;
        public int humanTeam;
        /// <summary>0 = team A, 1 = team B, -1 = tie.</summary>
        public int winner;
        public GameOverReason reason;
        public int playerOfTheGame;
        public List<SummaryLine> lines = new List<SummaryLine>();
        public bool isPlayoff;
        public bool isFinal;
        /// <summary>Played Full Court 5-on-5 (scores 2s and 3s).</summary>
        public bool fullCourt;
        /// <summary>I218–I220: lead changes, each side's biggest lead, and the biggest hole the human's team was in.</summary>
        public int leadChanges, biggestLeadFor, biggestLeadAgainst, humanTrailedBy;
        /// <summary>I249: the court it was played on.</summary>
        public string courtId;

        public bool HumanWon => winner == humanTeam;
        public int HumanScore => humanTeam == 0 ? scoreA : scoreB;
        public int OpponentScore => humanTeam == 0 ? scoreB : scoreA;
        public int Margin => HumanScore - OpponentScore;

        public SummaryLine HumanLine
        {
            get
            {
                foreach (var l in lines) if (l.isHuman) return l;
                return null;
            }
        }

        /// <summary>The whole team's box score added up (for the post-game comparison).</summary>
        public PlayerStatLine TeamTotals(int team)
        {
            var t = new PlayerStatLine();
            foreach (var l in lines)
            {
                if (l.team != team || l.stats == null) continue;
                var x = l.stats;
                t.points += x.points; t.fieldGoalsMade += x.fieldGoalsMade; t.fieldGoalsAttempted += x.fieldGoalsAttempted;
                t.arcMade += x.arcMade; t.arcAttempted += x.arcAttempted; t.assists += x.assists; t.rebounds += x.rebounds;
                t.steals += x.steals; t.blocks += x.blocks; t.turnovers += x.turnovers; t.greenReleases += x.greenReleases;
                t.alleyOops += x.alleyOops; t.alleyOopPasses += x.alleyOopPasses; t.heatUps += x.heatUps; t.ankleBreakers += x.ankleBreakers;
                ShotZones.Merge(t.chart, x.chart);
            }
            return t;
        }

        /// <summary>Rows for the post-game TEAM STATS comparison: label, team A value, team B value, and text for each.</summary>
        public System.Collections.Generic.List<(string label, float a, float b, string textA, string textB)> Comparison()
        {
            var A = TeamTotals(0);
            var B = TeamTotals(1);
            string Pct(PlayerStatLine x) => x.fieldGoalsAttempted == 0 ? "-" : (int)Math.Round(x.FieldGoalPercentage * 100f) + "%";
            return new System.Collections.Generic.List<(string, float, float, string, string)>
            {
                ("FG%", A.FieldGoalPercentage, B.FieldGoalPercentage, Pct(A), Pct(B)),
                ("DEEP", A.arcMade, B.arcMade, A.arcMade + "/" + A.arcAttempted, B.arcMade + "/" + B.arcAttempted),
                ("ASSISTS", A.assists, B.assists, A.assists.ToString(), B.assists.ToString()),
                ("REBOUNDS", A.rebounds, B.rebounds, A.rebounds.ToString(), B.rebounds.ToString()),
                ("STEALS", A.steals, B.steals, A.steals.ToString(), B.steals.ToString()),
                ("BLOCKS", A.blocks, B.blocks, A.blocks.ToString(), B.blocks.ToString()),
                ("TURNOVERS", A.turnovers, B.turnovers, A.turnovers.ToString(), B.turnovers.ToString()),
            };
        }

        public SummaryLine Line(int playerIndex)
        {
            foreach (var l in lines) if (l.playerIndex == playerIndex) return l;
            return null;
        }

        public static MatchSummary From(MatchSimulation m, GameMode mode, string matchId)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            var s = new MatchSummary
            {
                matchId = matchId,
                mode = mode,
                teamAId = m.Setup.TeamA.id,
                teamBId = m.Setup.TeamB.id,
                teamAName = m.Setup.TeamA.FullName,
                teamBName = m.Setup.TeamB.FullName,
                scoreA = m.Score[0],
                scoreB = m.Score[1],
                humanTeam = m.Setup.HumanTeam,
                winner = Scoring.Winner(m.Score[0], m.Score[1]),
                reason = m.EndReason,
                fullCourt = m.Setup.FullCourt,
            };
            GameCallouts.Fill(s, m.Flow); // I218
            s.courtId = m.Setup.CourtId;   // I249
            int potgTeam = s.winner < 0 ? s.humanTeam : s.winner;
            s.playerOfTheGame = m.Stats.PlayerOfTheGame(potgTeam, m.TeamSize);
            foreach (var p in m.Players)
            {
                s.lines.Add(new SummaryLine
                {
                    playerIndex = p.Index,
                    team = p.Team,
                    playerId = p.Def.id,
                    name = p.Def.DisplayName,
                    jerseyNumber = p.Def.jerseyNumber,
                    isHuman = p.IsHuman,
                    stats = m.Stats[p.Index],
                });
            }
            // Full Court: bench players who came on get their own lines.
            int benchIndex = 100;
            foreach (var b in m.Bench)
            {
                if (!b.Played) continue;
                s.lines.Add(new SummaryLine
                {
                    playerIndex = benchIndex++,
                    team = b.Team,
                    playerId = b.Def.id,
                    name = b.Def.DisplayName,
                    jerseyNumber = b.Def.jerseyNumber,
                    isHuman = false,
                    stats = b.Line,
                });
            }
            return s;
        }
    }
}
