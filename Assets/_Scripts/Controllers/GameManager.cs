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
            ScheduleManager.Instance.OnInterviewDay += () => RecruitmentManager.Instance?.NotifyInterviewDay();
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

    // Fires when the clock advances onto a new day — rebuild the rolling schedule window (so it
    // always looks 120 days ahead and picks up new-season fixtures), then trigger that day's
    // activity effects (training execution, etc.). AI simulation + saving are handled by the
    // advance coroutines.
    void NewDay(DateTime date)
    {
        // If the season just finished, roll into the next one BEFORE (re)building the schedule, so the new
        // season's fixtures exist to schedule. Without this the game sticks in season 1 once fixtures run out.
        FixturesManager.Instance?.CheckSeasonRollover();

        ScheduleManager.Instance?.GenerateSchedule(date);
        ScheduleManager.Instance?.ProcessToday();
        CalenderManager.Instance.RespondToAdvance();
    }

    // ————————————————————— Day / match flow —————————————————————

    /// <summary>True while an advance/play coroutine is running (blocks re-entry; greys the button).</summary>
    public bool IsBusy { get; private set; }

    private const int SIM_MATCHES_PER_FRAME = 6;

    /// <summary>True if the player has an unplayed fixture dated today or earlier (must be played before advancing).</summary>
    public bool HasPendingPlayerMatch() => GetPendingPlayerMatch() != null;

    private Fixture GetPendingPlayerMatch()
    {
        DateTime today = CalenderManager.Instance.CurrentDay.Date;
        Team me = TeamManager.Instance.MyTeam;
        Fixture earliest = null;
        var all = FixturesManager.Instance.GetAllFixtures();
        for (int i = 0; i < all.Count; i++)
        {
            var f = all[i];
            if (f.BeenPlayed || f.Date.Date > today) continue;
            if (f.HomeTeam != me && f.AwayTeam != me) continue;
            if (earliest == null || f.Date < earliest.Date) earliest = f;
        }
        return earliest;
    }

    /// <summary>Button entry point: play today's pending match if there is one, else advance a day.</summary>
    public void AdvanceOrPlay()
    {
        if (IsBusy) return;
        Fixture pending = GetPendingPlayerMatch();
        if (pending != null) StartCoroutine(PlayMatchRoutine(pending));
        else StartCoroutine(AdvanceRoutine());
    }

    // Play the player's match ON its day: resolve that day's other games, then hand off to the
    // interactive sim — which returns to the home page on the SAME day (the player advances separately).
    private IEnumerator PlayMatchRoutine(Fixture match)
    {
        IsBusy = true;
        LoadingOverlay.Instance?.Show();
        yield return null;

        yield return SimulateAiMatches(d => d == match.Date.Date);
        SaveManager.Instance?.AutoSave();
        yield return null;

        LoadingOverlay.Instance?.Hide();
        IsBusy = false;

        UIManager.Instance.ShowMatchSimPage(match); // returns home (same day) + autosaves at full time
    }

    // Advance one day: resolve any due AI matches, advance the clock + save, THEN reveal the home
    // page so the day-strip animation always plays smoothly (the hitch happened under the overlay).
    private IEnumerator AdvanceRoutine()
    {
        IsBusy = true;
        LoadingOverlay.Instance?.Show();
        yield return null;

        // Resolve all due AI matches up to today (the player has none pending in this branch).
        yield return SimulateAiMatches(d => d <= CalenderManager.Instance.CurrentDay.Date);

        CalenderManager.Instance.AdvanceDay(); // increments + fires NewDay (ProcessToday + events)
        SaveManager.Instance?.AutoSave();       // save AFTER sim + advance, BEFORE the animation

        yield return null; // let the save hitch pass so the slide is smooth
        LoadingOverlay.Instance?.Hide();
        IsBusy = false;

        UIManager.Instance.ShowHomePage(); // HomePageUI.OnShow sees +1 day → plays the slide
    }

    // Simulates AI (non-player) fixtures matching the date predicate, a few per frame so the loading
    // spinner keeps moving and a heavy match day doesn't freeze for a noticeable spell.
    private IEnumerator SimulateAiMatches(Func<DateTime, bool> dateMatches)
    {
        Team me = TeamManager.Instance.MyTeam;
        var all = FixturesManager.Instance.GetAllFixtures(); // live list; Count re-read each iteration
        int simmedThisFrame = 0;

        for (int i = 0; i < all.Count; i++)
        {
            Fixture f = all[i];
            if (f.BeenPlayed || f.HomeTeam == me || f.AwayTeam == me) continue;
            if (!dateMatches(f.Date.Date)) continue;

            f.SimulateFixture(); // may append future-dated fixtures (cup next round) — skipped by the predicate

            if (++simmedThisFrame >= SIM_MATCHES_PER_FRAME)
            {
                simmedThisFrame = 0;
                yield return null;
            }
        }
    }
}
