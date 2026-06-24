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
        HideAllUI();

        var player = PersonManager.Instance.GetPlayer(personID);

        playerDetailsUI.Show(player);
    }
    public void ShowManagerDetails(int personID)
    {
        HideAllUI();

        var manager = PersonManager.Instance.GetManager(personID);

        playerDetailsUI.Show(manager);
    }

    public void ShowTeamDetails(int teamId)
    {
        HideAllUI();

        var team = TeamManager.Instance.GetTeam(teamId);

        clubDetailsUI.Show(team);
    }

    public void ShowTactics()
    {
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
        HideAllUI();
        fixtureListUI.Show();
    }

    public void ShowSchedule()
    {
        HideAllUI();
        schedulePageUI.Show();
    }

    public void ShowTraining()
    {
        HideAllUI();
        trainingPageUI.Show();
    }

    public void ShowRecruitment()
    {
        HideAllUI();
        recruitmentPageUI.Show();
    }

    public void ShowStats()
    {
        HideAllUI();
        statsPageUI?.Show();
    }

    public void ShowHomePage()
    {
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
