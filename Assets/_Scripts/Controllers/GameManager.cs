using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; set; }

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

    void SetupGame()
    {
        TeamManager.Instance.SpawnTeams();
        FixturesManager.Instance.AddComps();

        UIManager.Instance.Setup();
    }

    void Start()
    {
        SetupGame();

        CalenderManager.Instance.NewDay.AddListener(NewDay);
        CalenderManager.Instance.ConfirmAddedListener();
    }

    void NewDay(DateTime date)
    {
        PlayerMatchSim = null;

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
            PlayerMatchSim = () => UIManager.Instance.ShowMatchSimPage(myFixture);
        }

        CalenderManager.Instance.RespondToAdvance();
    }

    public UnityAction PlayerMatchSim;
}
