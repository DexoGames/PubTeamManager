using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; set; }

    int matchWeekNum;
    public int MatchWeekNum => matchWeekNum;
    public int PrevMatchWeekNum => matchWeekNum == 0 ? 0 : matchWeekNum - 1;

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

    void Start()
    {
        CalenderManager.Instance.NewDay.AddListener(NewDay);
        CalenderManager.Instance.ConfirmAddedListener();
    }

    void NewDay(DateTime date)
    {
        List<Fixture> allFixtures = FixturesManager.Instance.GetAllFixtures();
        for (int i  = 0; i < allFixtures.Count; i++)
        {
            if (!allFixtures[i].BeenPlayed)
            {
                if (allFixtures[i].Date < CalenderManager.Instance.CurrentDay)
                {
                    allFixtures[i].SimulateFixture();
                }
            }
        }

        UpdateMatchWeek();

        CalenderManager.Instance.RespondToAdvance();
    }

    void UpdateMatchWeek()
    {
        //Debug.Log("MATCH WEEK NUM " + matchWeekNum);
        MatchWeek week = FixturesManager.Instance.GetMatchWeek(matchWeekNum-1);

        UIManager.Instance.ShowHomePage();

        if (week.FullyPlayed())
        {
            matchWeekNum = Mathf.Min(matchWeekNum + 1, FixturesManager.Instance.GetMatchWeeks().Count);
        }
    }

    public void SimulateMatchWeek()
    {
        MatchWeek matchWeek = FixturesManager.Instance.GetMatchWeek(matchWeekNum);
        int matchWeekLength = FixturesManager.Instance.GetMatchWeeks().Count;

        if (matchWeekNum < matchWeekLength)
        {
            matchWeek.SimulateWeek();

            matchWeekNum = Mathf.Min(matchWeekNum+1, matchWeekLength);
        }

        UIManager.Instance.ShowHomePage();

        //UIManager.Instance.ShowMatchSimPage(matchWeek.fixtures[0]);
    } // DEPRECATED
}
