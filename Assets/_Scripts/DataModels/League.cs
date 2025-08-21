using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;

public class League : Competition
{
    public League(string name, List<Team> teams, DateTime startDate) : base(name, 0, teams, startDate)
    {

    }

    List<LeagueTableEntry> standings = new List<LeagueTableEntry>();

    public override void GenerateFixtures()
    {
        Fixtures.Clear();

        // Make a copy of teams list
        List<Team> teams = new List<Team>(Teams);

        // If odd number of teams, add a null (bye week)
        if (teams.Count % 2 != 0)
            teams.Add(null);

        int numTeams = teams.Count;
        int numRounds = (numTeams - 1) * 2; // double round robin
        int matchesPerRound = numTeams / 2;

        Rounds = new List<Fixture>[numRounds];
        DateTime roundDate = startDate;

        // First half
        for (int round = 0; round < numTeams - 1; round++)
        {
            Rounds[round] = new List<Fixture>();

            for (int i = 0; i < matchesPerRound; i++)
            {
                Team home = teams[i];
                Team away = teams[numTeams - 1 - i];

                if (home != null && away != null)
                {
                    DateTime matchDate = FindNextAvailableDate(home, away, roundDate);
                    Fixture fixture = new Fixture(home, away, matchDate, this, round);
                    Fixtures.Add(fixture);
                    Rounds[round].Add(fixture);
                }
            }

            // Rotate teams (except first)
            Team last = teams[numTeams - 1];
            teams.RemoveAt(numTeams - 1);
            teams.Insert(1, last);

            // Jump ahead 14 days (1 week break)
            roundDate = roundDate.AddDays(14);
        }

        // Second half — reverse home/away
        for (int round = 0; round < numTeams - 1; round++)
        {
            int roundIndex = round + (numTeams - 1);
            Rounds[roundIndex] = new List<Fixture>();

            foreach (var firstHalfFixture in Rounds[round])
            {
                Team home = firstHalfFixture.AwayTeam;
                Team away = firstHalfFixture.HomeTeam;

                DateTime matchDate = FindNextAvailableDate(home, away, roundDate);
                Fixture fixture = new Fixture(home, away, matchDate, this, roundIndex);
                Fixtures.Add(fixture);
                Rounds[roundIndex].Add(fixture);
            }

            roundDate = roundDate.AddDays(14);
        }

        base.GenerateFixtures();
    }

    public void InitializeStandings()
    {
        standings.Clear();

        foreach (var team in TeamManager.Instance.GetAllTeams())
        {
            standings.Add(new LeagueTableEntry(team));
        }
    }

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
