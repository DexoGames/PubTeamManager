using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    /// <summary>True while the home page is the visible page (drives the Next Day / Home button).</summary>
    public bool IsHomeActive { get; private set; }

    public GameObject[] hideForMatchSim;

    private PlayerDetailsUI playerDetailsUI;
    private TeamDetailsUI clubDetailsUI;
    private CompetitionPageUI fixtureListUI;
    private HomePageUI homePageUI;
    private MatchSimPageUI matchSimPageUI;
    private TacticsPageUI tacticsPageUI;
    private DiscussionPageUI discussionPageUI;
    private SchedulePageUI schedulePageUI;
    private TrainingPageUI trainingPageUI;
    private RecruitmentPageUI recruitmentPageUI;
    private StatsPageUI statsPageUI;

    // ————————————————————— navigation history (Back button) —————————————————————
    // Each recorded ShowX pushes a re-show delegate; Back() pops and replays the previous one.
    private readonly List<Action> _backStack = new List<Action>();
    private Action _currentNav;
    private bool _restoringNav;
    private const int MAX_HISTORY = 50;

    /// <summary>True when there's a previous page to return to (drives the Back button's enabled state).</summary>
    public bool CanGoBack => _backStack.Count > 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Debug.Log("INSTANCE");
            Instance = this;
        }
    }

    public void Setup()
    {
        Debug.Log("[TRACE] UIManager.Setup — begin");
        playerDetailsUI = PlayerDetailsUI.Instance;
        clubDetailsUI = TeamDetailsUI.Instance;
        fixtureListUI = CompetitionPageUI.Instance;
        homePageUI = HomePageUI.Instance;
        matchSimPageUI = MatchSimPageUI.Instance;
        tacticsPageUI = TacticsPageUI.Instance;
        discussionPageUI = DiscussionPageUI.Instance;
        schedulePageUI = SchedulePageUI.Instance;
        trainingPageUI = TrainingPageUI.Instance;
        recruitmentPageUI = RecruitmentPageUI.Instance;
        statsPageUI = StatsPageUI.Instance;   // may be null until the page is added to the scene

        //START PAGE IS THE HOME PAGE
        ShowHomePage();
    }

    public void ShowPlayerDetails(int personID)
    {
        PushNav(() => ShowPlayerDetails(personID));
        HideAllUI();

        var player = PersonManager.Instance.GetPlayer(personID);

        playerDetailsUI.Show(player);
    }
    public void ShowManagerDetails(int personID)
    {
        PushNav(() => ShowManagerDetails(personID));
        HideAllUI();

        var manager = PersonManager.Instance.GetManager(personID);

        playerDetailsUI.Show(manager);
    }

    public void ShowTeamDetails(int teamId)
    {
        PushNav(() => ShowTeamDetails(teamId));
        HideAllUI();

        var team = TeamManager.Instance.GetTeam(teamId);

        clubDetailsUI.Show(team);
    }

    public void ShowTactics()
    {
        PushNav(() => ShowTactics());
        HideAllUI();

        tacticsPageUI.Show(TeamManager.Instance.MyTeam);
    }
    public void ShowMyTeam()
    {
        HideAllUI();

        ShowTeamDetails(TeamManager.Instance.MyTeam.TeamId);
    }

    public void ShowDiscussion(int eventIndex, int playerIndex)
    {
        HideAllUI();

        discussionPageUI.Show(EventsManager.Instance.Events[eventIndex], PersonManager.Instance.GetPerson(playerIndex));
    }

    public void ShowFixtureList()
    {
        PushNav(() => ShowFixtureList());
        HideAllUI();
        fixtureListUI.Show();
    }

    public void ShowSchedule()
    {
        PushNav(() => ShowSchedule());
        HideAllUI();
        schedulePageUI.Show();
    }

    public void ShowTraining()
    {
        PushNav(() => ShowTraining());
        HideAllUI();
        trainingPageUI.Show();
    }

    public void ShowRecruitment()
    {
        PushNav(() => ShowRecruitment());
        HideAllUI();
        recruitmentPageUI.Show();
    }

    public void ShowStats()
    {
        PushNav(() => ShowStats());
        HideAllUI();
        statsPageUI?.Show();
    }

    public void ShowHomePage()
    {
        PushNav(() => ShowHomePage());
        Debug.Log($"[TRACE] UIManager.ShowHomePage — homePageUI null? {homePageUI == null}");
        HideAllUI();
        homePageUI.Show();
        IsHomeActive = true;
        Debug.Log("[TRACE] UIManager.ShowHomePage — shown");
    }

    public void ShowMatchSimPage(Fixture fixture)
    {
        HideAllUI();
        matchSimPageUI.Show(fixture);
    }

    /// <summary>Returns from the half-time tactics page back into the live match without restarting it.</summary>
    public void ResumeMatchFromTactics()
    {
        HideAllUI();
        matchSimPageUI.ResumeDisplay();
    }

    private void HideAllUI()
    {
        IsHomeActive = false;
        playerDetailsUI.Hide();
        clubDetailsUI.Hide();
        fixtureListUI.Hide();
        homePageUI.Hide();
        matchSimPageUI.Hide();
        tacticsPageUI.Hide();
        discussionPageUI.Hide();
        schedulePageUI.Hide();
        trainingPageUI.Hide();
        recruitmentPageUI.Hide();
        statsPageUI?.Hide();
    }

    /// <summary>Records the page now being shown so Back() can return to whatever was visible before it.</summary>
    private void PushNav(Action reshow)
    {
        if (_restoringNav) return;            // Back() is driving — don't re-record
        if (_currentNav != null)
        {
            _backStack.Add(_currentNav);
            if (_backStack.Count > MAX_HISTORY) _backStack.RemoveAt(0);
        }
        _currentNav = reshow;
    }

    /// <summary>Return to the previously shown page. No-op during a live match (use the match controls) or with no history.</summary>
    public void Back()
    {
        Tactic tactic = TeamManager.Instance != null && TeamManager.Instance.MyTeam != null
            ? TeamManager.Instance.MyTeam.Tactic : null;
        if (tactic != null && tactic.InMatch) return;

        if (_backStack.Count == 0) return;

        int last = _backStack.Count - 1;
        Action prev = _backStack[last];
        _backStack.RemoveAt(last);

        _restoringNav = true;
        prev();                 // re-shows the previous page (PushNav suppressed via _restoringNav)
        _currentNav = prev;
        _restoringNav = false;
    }

    public void ShowNavigationButtons(bool show)
    {
        foreach (var obj in hideForMatchSim)
        {
            obj.SetActive(show);
        }
    }

    public void Exit()
    {
        Application.Quit();
    }
}
