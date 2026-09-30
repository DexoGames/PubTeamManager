using System.Collections.Generic;

/// <summary>
/// Tracks lifetime statistics for a club across all seasons.
/// Attached to each Team instance.
/// </summary>
[System.Serializable]
public class ClubStats
{
    public int LeaguesWon;
    public int CupsWon;
    public int TotalWins;
    public int TotalDraws;
    public int TotalLosses;
    public int TotalGoalsScored;
    public int TotalGoalsConceded;
    public int SeasonsPlayed;
    public List<string> LeagueTitles = new List<string>();
    public List<string> CupTitles = new List<string>();

    public int TotalPlayed => TotalWins + TotalDraws + TotalLosses;
    public int GoalDifference => TotalGoalsScored - TotalGoalsConceded;

    /// <summary>
    /// Records the result of a single match for this club.
    /// </summary>
    public void RecordMatchResult(Fixture fixture, Team thisTeam)
    {
        bool isHome = fixture.HomeTeam == thisTeam;
        int goalsFor = isHome ? fixture.Result.score.home : fixture.Result.score.away;
        int goalsAgainst = isHome ? fixture.Result.score.away : fixture.Result.score.home;

        TotalGoalsScored += goalsFor;
        TotalGoalsConceded += goalsAgainst;

        if (goalsFor > goalsAgainst)
            TotalWins++;
        else if (goalsFor < goalsAgainst)
            TotalLosses++;
        else
            TotalDraws++;
    }

    /// <summary>
    /// Records a league title win.
    /// </summary>
    public void RecordLeagueWin(string leagueName, int season)
    {
        LeaguesWon++;
        LeagueTitles.Add($"{leagueName} ({season})");
    }

    /// <summary>
    /// Records a cup title win.
    /// </summary>
    public void RecordCupWin(string cupName, int season)
    {
        CupsWon++;
        CupTitles.Add($"{cupName} ({season})");
    }
}
