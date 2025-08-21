using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class Competition
{
    public string Name;
    public int Priority = 0;
    public List<Team> Teams = new List<Team>();
    public List<Fixture> Fixtures { get; protected set; } = new List<Fixture>();
    public List<Fixture>[] Rounds { get; protected set; }
    public List<Competition> RelatedCompetitions { get; protected set; } = new List<Competition>();

    protected DateTime startDate;

    public Competition(string name, int priority, List<Team> teams, DateTime startDate)
    {
        Name = name;
        Priority = priority;
        Teams = teams;
        this.startDate = startDate;
        GenerateFixtures();
    }

    public virtual void GenerateFixtures()
    {
        FixturesManager.Instance.RegisterFixtures(Fixtures);
    }

    protected bool IsTeamAvailable(Team team, DateTime date)
    {
        return !FixturesManager.Instance.GetAllFixtures().Exists(f =>
            (f.HomeTeam == team || f.AwayTeam == team) &&
            (f.Date.Date == date.Date || f.Date.Date == date.AddDays(1).Date || f.Date.Date == date.AddDays(-1).Date));
    }

    public int GetMostRecentRound()
    {
        if (Rounds == null || Rounds.Length == 0)
            return -1;

        for (int i = Rounds.Length - 1; i >= 0; i--)
        {
            foreach (var fixture in Rounds[i])
            {
                if (!fixture.BeenPlayed)
                {
                    goto NextLoop;
                }
            }
            return i;

        NextLoop:
            continue;
        }
        return 0;
    }
    public int GetUpcomingRound()
    {
        if (Rounds == null || Rounds.Length == 0)
            return -1;

        for (int i = 0; i < Rounds.Length; i++)
        {
            foreach (var fixture in Rounds[i])
            {
                if (!fixture.BeenPlayed)
                {
                    return i;
                }
            }
        }
        return 0;
    }

    protected DateTime FindNextAvailableDate(Team team1, Team team2, DateTime fromDate)
    {
        DateTime date = fromDate.AddDays(UnityEngine.Random.Range(-1, 2));

        while (!IsTeamAvailable(team1, date) || !IsTeamAvailable(team2, date))
        {
            date = date.AddDays(1);
        }

        return date;
    }
}
