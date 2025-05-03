using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LeagueManager : MonoBehaviour
{
    List<LeagueTableEntry> standings;

    public static LeagueManager Instance { get; private set; }


    public void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
        }

        standings = new List<LeagueTableEntry>();
    }

    public void InitializeStandings()
    {
        standings.Clear();

        foreach(var team in TeamManager.Instance.GetAllTeams())
        {
            standings.Add(new LeagueTableEntry(team));
        }
    }

    // Update standings based on a fixture
    public void UpdateStandings(Fixture fixture)
    {
        var homeTeamEntry = standings.FirstOrDefault(entry => entry.team == fixture.HomeTeam);
        var awayTeamEntry = standings.FirstOrDefault(entry => entry.team == fixture.AwayTeam);

        if (homeTeamEntry == null || awayTeamEntry == null)
        {
            Debug.LogError("Team not found in league standings.");
            return;
        }

        // Assuming Fixture has properties like HomeGoals, AwayGoals
        int homeGoals = fixture.Result.score.home;
        int awayGoals = fixture.Result.score.away;

        homeTeamEntry.goalsFor += homeGoals;
        homeTeamEntry.goalsAgainst += awayGoals;

        awayTeamEntry.goalsFor += awayGoals;
        awayTeamEntry.goalsAgainst += homeGoals;

        if (homeGoals > awayGoals) // Home win
        {
            homeTeamEntry.points += 3;
        }
        else if (homeGoals < awayGoals) // Away win
        {
            awayTeamEntry.points += 3;
        }
        else // Draw
        {
            homeTeamEntry.points += 1;
            awayTeamEntry.points += 1;
        }

        SortStandings();
    }

    void SortStandings()
    {
        standings = standings.OrderByDescending(entry => entry.points).ThenByDescending(entry => entry.goalDifference).ThenByDescending(entry => entry.goalsFor).ToList();
    }

    public List<LeagueTableEntry> GetStandings()
    {
        if (standings.Count < 1) InitializeStandings();

        return standings;
    }
}