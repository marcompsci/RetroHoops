using System;

namespace CallerRetroBall.Logic
{
    /// <summary>Box score line for one player in one match.</summary>
    [Serializable]
    public class PlayerStatLine
    {
        public int points;
        public int fieldGoalsMade;
        public int fieldGoalsAttempted;
        public int arcMade;
        public int arcAttempted;
        public int assists;
        public int rebounds;
        public int steals;
        public int blocks;
        public int turnovers;
        /// <summary>Releases graded GREEN (made or missed).</summary>
        public int greenReleases;
        /// <summary>Alley-oops finished.</summary>
        public int alleyOops;
        /// <summary>Alley-oop lobs thrown that were finished.</summary>
        public int alleyOopPasses;
        /// <summary>Times this player heated up (HEAT CHECK).</summary>
        public int heatUps;
        /// <summary>Street rules: defenders sent stumbling.</summary>
        public int ankleBreakers;
        /// <summary>THE STREETS: tricks done with the TRICK button.</summary>
        public int tricks;
        /// <summary>Phase 35: where every shot came from (made / attempted per spot).</summary>
        public ShotChartData chart = new ShotChartData();

        public float FieldGoalPercentage => fieldGoalsAttempted == 0 ? 0f : (float)fieldGoalsMade / fieldGoalsAttempted;
    }

    /// <summary>Box score for a match: one line per player index.</summary>
    [Serializable]
    public class MatchStats
    {
        public PlayerStatLine[] players;

        public MatchStats(int playerCount)
        {
            players = new PlayerStatLine[playerCount];
            for (int i = 0; i < playerCount; i++) players[i] = new PlayerStatLine();
        }

        public PlayerStatLine this[int playerIndex] => players[playerIndex];

        public int TeamPoints(int team, int playersPerTeam)
        {
            int sum = 0;
            for (int i = team * playersPerTeam; i < (team + 1) * playersPerTeam; i++) sum += players[i].points;
            return sum;
        }

        /// <summary>Simple game score: points + assists + rebounds + 1.5×(steals+blocks) − turnovers.</summary>
        public int PlayerOfTheGame(int winningTeam, int playersPerTeam)
        {
            int best = winningTeam * playersPerTeam;
            float bestScore = float.MinValue;
            for (int i = winningTeam * playersPerTeam; i < (winningTeam + 1) * playersPerTeam; i++)
            {
                var s = players[i];
                float score = s.points + s.assists + s.rebounds + 1.5f * (s.steals + s.blocks) - s.turnovers;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }
    }
}
