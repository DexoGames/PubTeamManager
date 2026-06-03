using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

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

    /// <summary>
    /// Wipes the save and restarts from a fresh game. Reloading the active scene
    /// re-runs every manager's Awake/Start, so all singleton state resets cleanly;
    /// with no save present, Start() falls into the fresh SetupGame() branch.
    /// Hook this up to a "New Game" UI button.
    /// </summary>
    public void NewGame()
    {
        SaveManager.Instance?.DeleteSave();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void SetupGame()
    {
        TeamManager.Instance.SpawnTeams();
        FixturesManager.Instance.AddComps();

        // Initialize recruitment pool
        if (RecruitmentManager.Instance != null)
            RecruitmentManager.Instance.RefreshPool();

        // Generate schedule
        if (ScheduleManager.Instance != null)
            ScheduleManager.Instance.GenerateSchedule(CalenderManager.Instance.CurrentDay);

        // Wire up schedule events
        if (ScheduleManager.Instance != null)
        {
            ScheduleManager.Instance.OnTrainingDay += () => TrainingManager.Instance?.ExecuteTraining();
        }

        UIManager.Instance.Setup();

        Debug.Log($"[Game] New game ready — Teams:{TeamManager.Instance.GetAllTeams().Count} Comps:{FixturesManager.Instance.Competitions.Count} Fixtures:{FixturesManager.Instance.GetAllFixtures().Count} | NextIds P:{IdManager.Instance.NextPersonId} T:{IdManager.Instance.NextTeamId} C:{IdManager.Instance.NextCompetitionId} F:{IdManager.Instance.NextFixtureId} S:{IdManager.Instance.NextSeriesId}");
    }

    void Start()
    {
        // Auto-load if a save file exists, otherwise start fresh
        if (SaveManager.Instance != null && SaveManager.Instance.HasSave())
        {
            Debug.Log("[Game] Save file found — loading...");
            SaveManager.Instance.Load();
            UIManager.Instance.Setup();
        }
        else
        {
            SetupGame();
        }

        CalenderManager.Instance.NewDay.AddListener(NewDay);
        CalenderManager.Instance.ConfirmAddedListener();
    }

    void NewDay(DateTime date)
    {
        PlayerMatchSim = null;

        // Process today's schedule
        ScheduleManager.Instance?.ProcessToday();

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

        // Auto-save every 7 days
        if (SaveManager.Instance != null && date.DayOfWeek == DayOfWeek.Monday)
        {
            SaveManager.Instance.AutoSave();
        }

        CalenderManager.Instance.RespondToAdvance();
    }

    public UnityAction PlayerMatchSim;
}
