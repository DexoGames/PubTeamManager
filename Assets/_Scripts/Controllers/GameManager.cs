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
            Debug.Log("[TRACE] GameManager.Awake — DUPLICATE, destroying this GameObject");
            Destroy(this.gameObject);
        }
        else
        {
            Debug.Log("[TRACE] GameManager.Awake — set Instance");
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

    private void OnEnable()
    {
        Debug.Log("[TRACE] GameManager.OnEnable — component is enabled");
    }

    void SetupGame()
    {
        Debug.Log("[TRACE] SetupGame — SpawnTeams");
        TeamManager.Instance.SpawnTeams();
        Debug.Log("[TRACE] SetupGame — AddComps");
        FixturesManager.Instance.AddComps();
        Debug.Log("[TRACE] SetupGame — comps done, continuing");

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

        // Write an initial full save so both core + season files exist from the start.
        SaveManager.Instance?.Save();
    }

    // Set after a failed load so the post-reload run starts fresh instead of re-loading the
    // same bad save (which would loop). Static so it survives the scene reload.
    private static bool startFreshAfterFailedLoad = false;

    void Start()
    {
        Debug.Log("[TRACE] GameManager.Start — begin");

        if (startFreshAfterFailedLoad)
        {
            startFreshAfterFailedLoad = false;
            Debug.Log("[TRACE] GameManager.Start — fresh start after failed load");
            SetupGame();
        }
        // Auto-load if a save file exists, otherwise start fresh
        else if (SaveManager.Instance != null && SaveManager.Instance.HasSave())
        {
            Debug.Log("[TRACE] GameManager.Start — save found, loading");
            if (SaveManager.Instance.Load())
            {
                UIManager.Instance.Setup();
            }
            else
            {
                // Don't show the UI on a half-loaded state — reload the scene for a clean slate
                // and start fresh. The save file is left in place (not deleted).
                Debug.LogError("[Game] Save failed to load (corrupt or incompatible). Starting a fresh game; your save file is left untouched.");
                startFreshAfterFailedLoad = true;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                return;
            }
        }
        else
        {
            Debug.Log("[TRACE] GameManager.Start — no save, SetupGame()");
            SetupGame();
        }

        Debug.Log("[TRACE] GameManager.Start — finished setup branch");

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

        // Auto-save after every day advance (writes core + the current season's fixtures).
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.AutoSave();
        }

        CalenderManager.Instance.RespondToAdvance();
    }

    public UnityAction PlayerMatchSim;
}
