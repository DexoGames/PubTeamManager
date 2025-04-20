using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FixturesManager : MonoBehaviour
{
    List<MatchWeek> matchWeeks = new List<MatchWeek>();
    List<Fixture> allFixtures = new List<Fixture>();

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

        //matchWeeks = FixtureGenerator.GenerateFixtures(TeamManager.Instance.GetAllTeams());
    }

    public List<MatchWeek> GetMatchWeeks()
    {
        if(matchWeeks.Count < 1)
        {
            matchWeeks = FixtureGenerator.GenerateFixtures(TeamManager.Instance.GetAllTeams(), CalenderManager.Instance.CurrentDay);
        }

        return matchWeeks;
    }

    public List<Fixture> GetAllFixtures()
    {
        if (allFixtures.Count < 1)
        {
            List<MatchWeek> weeks = GetMatchWeeks();

            foreach(MatchWeek week in weeks)
            {
                foreach(Fixture f in week.fixtures)
                {
                    allFixtures.Add(f);
                }
            }
        }

        return allFixtures;
    }

    public MatchWeek GetMatchWeek(int i)
    {
        if (matchWeeks.Count < 1) GetMatchWeeks();

        i = Mathf.Clamp(i, 0, matchWeeks.Count - 1);

        return matchWeeks[i];
    }
}
