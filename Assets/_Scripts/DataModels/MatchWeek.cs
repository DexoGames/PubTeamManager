using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MatchWeek
{
    public List<Fixture> fixtures;
    public int weekNumber;

    public MatchWeek(int number)
    {
        weekNumber = number;
        fixtures = new List<Fixture>();
    }

    public Fixture AddFixture(Fixture fixture)
    {
        fixtures.Add(fixture);
        return fixture;
    }

    public void SimulateWeek()
    {
        foreach(Fixture fixture in fixtures)
        {
            //if(fixture == fixtures[0])
            //{
            //    fixtureToWatch = fixture;
            //    continue;
            //}

            fixture.SimulateFixture();

            LeagueManager.Instance.UpdateStandings(fixture);

            fixture.BeenPlayed = true;
        }

        //UIManager.Instance.ShowMatchSimPage(fixtureToWatch);
    }

    public bool FullyPlayed()
    {
        bool allPlayed = true;
        foreach (Fixture f in fixtures)
        {
            if (!f.BeenPlayed)
            {
                allPlayed = false;
            }
        }
        return allPlayed;
    }
}