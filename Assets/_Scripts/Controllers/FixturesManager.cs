using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FixturesManager : MonoBehaviour
{
    List<Fixture> allFixtures = new List<Fixture>();

    Dictionary<Team, List<Fixture>> fixturesByTeam = new Dictionary<Team, List<Fixture>>();
    public List<Competition> Competitions { get; private set; } = new List<Competition>();

    public static FixturesManager Instance { get; private set; }

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
    }

    public void AddComps()
    {
        League league = new("Scunthorpe Suburbs Regional", TeamManager.Instance.GetAllTeams(), CalenderManager.Instance.CurrentDay.AddDays(7));
        Cup cup = new("Scunthorpe Papa Johns Cup", TeamManager.Instance.GetAllTeams(), CalenderManager.Instance.CurrentDay.AddDays(10));

        Competitions.Add(league);
        Competitions.Add(cup);
    }

    public void RegisterFixtures(List<Fixture> fixtures)
    {
        Debug.Log($"Registering {fixtures.Count} fixtures");

        foreach (Fixture fixture in fixtures)
        {
            if (allFixtures.Contains(fixture)) continue;
            allFixtures.Add(fixture);

            if (!fixturesByTeam.ContainsKey(fixture.HomeTeam))
                fixturesByTeam[fixture.HomeTeam] = new List<Fixture>();
            fixturesByTeam[fixture.HomeTeam].Add(fixture);

            if (!fixturesByTeam.ContainsKey(fixture.AwayTeam))
                fixturesByTeam[fixture.AwayTeam] = new List<Fixture>();
            fixturesByTeam[fixture.AwayTeam].Add(fixture);
        }
    }

    public List<Fixture> GetAllFixtures()
    {
        return allFixtures;
    }

    public List<Fixture> GetAllUpcomingFixtures(int daysAhead = 1000)
    {
        Debug.Log("Getting all upcoming fixtures"); 

        DateTime today = CalenderManager.Instance.CurrentDay;
        DateTime maxDate = today.AddDays(daysAhead);

        return allFixtures
            .Where(f => !f.BeenPlayed && f.Date >= today && f.Date <= maxDate)
            .OrderBy(f => f.Date)
            .ToList();
    }

    public List<Fixture> GetUpcomingFixturesForTeam(Team team, int daysAhead = 1000)
    {
        if (!fixturesByTeam.ContainsKey(team))
        {
            Debug.LogWarning($"No fixtures found for team: {team.Name}");
            return new List<Fixture>();
        }

        DateTime today = CalenderManager.Instance.CurrentDay;
        DateTime maxDate = today.AddDays(daysAhead);

        return fixturesByTeam[team]
            .Where(f => !f.BeenPlayed && f.Date >= today && f.Date <= maxDate)
            .OrderBy(f => f.Date)
            .ToList();
    }

    private void Update()
    {
        // You can reimplement testing logic here using GetRound instead of MatchWeek
    }
}
