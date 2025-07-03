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
        Fixture myFixture = null;
        for (int i  = 0; i < allFixtures.Count; i++)
        {
            if (!allFixtures[i].BeenPlayed && allFixtures[i].Date < CalenderManager.Instance.CurrentDay)
            {
                if (myFixture == null && (allFixtures[i].HomeTeam == TeamManager.Instance.MyTeam || allFixtures[i].AwayTeam == TeamManager.Instance.MyTeam))
                {
                    myFixture = allFixtures[i];
                }
                else
                {
                    allFixtures[i].SimulateFixture();
                }
            }
        }

        if (myFixture != null)
        {
            UIManager.Instance.ShowMatchSimPage(myFixture);
        }
        else
        {
            UpdateMatchWeek();
        }

        CalenderManager.Instance.RespondToAdvance();
    }

    void UpdateMatchWeek()
    {
        MatchWeek week = FixturesManager.Instance.GetMatchWeek(matchWeekNum-1);

        UIManager.Instance.ShowHomePage();

        if (week.FullyPlayed())
        {
            matchWeekNum = Mathf.Min(matchWeekNum + 1, FixturesManager.Instance.GetMatchWeeks().Count);
        }
    }
}
