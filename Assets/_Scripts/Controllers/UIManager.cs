using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    public GameObject[] hideForMatchSim;

    private PlayerDetailsUI playerDetailsUI;
    private TeamDetailsUI clubDetailsUI;
    private FixtureListUI fixtureListUI;
    private HomePageUI homePageUI;
    private MatchSimPageUI matchSimPageUI;
    private TacticsPageUI tacticsPageUI;
    private DiscussionPageUI discussionPageUI;

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

    private void Start()
    {
        playerDetailsUI = PlayerDetailsUI.Instance;
        clubDetailsUI = TeamDetailsUI.Instance;
        fixtureListUI = FixtureListUI.Instance;
        homePageUI = HomePageUI.Instance;
        matchSimPageUI = MatchSimPageUI.Instance;
        tacticsPageUI = TacticsPageUI.Instance;
        discussionPageUI = DiscussionPageUI.Instance;

        //Start by showing home page
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

    public void ShowHomePage()
    {
        HideAllUI();
        homePageUI.Show();
    }

    public void ShowMatchSimPage(Fixture fixture)
    {
        HideAllUI();
        matchSimPageUI.Show(fixture);
    }

    private void HideAllUI()
    {
        // Hide all UI panels
        playerDetailsUI.Hide();
        clubDetailsUI.Hide();
        fixtureListUI.Hide();
        homePageUI.Hide();
        matchSimPageUI.Hide();
        tacticsPageUI.Hide();
        discussionPageUI.Hide();
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
