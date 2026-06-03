using System.Linq;
using TMPro;
using UnityEngine;

public class CompetitionPageUI : UIPage
{
    public static CompetitionPageUI Instance { get; private set; }

    Competition competition;
    private int roundFocus = 0;

    [SerializeField] private TextMeshProUGUI gameWeek;

    [SerializeField] private Transform fixtureContainer;
    [SerializeField] private FixtureUI fixturePrefab;

    // Unity Message
    private void Awake()
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

    public void ChangeWeekFocus(int amount)
    {
        roundFocus = roundFocus + amount;
        roundFocus = Mathf.Clamp(roundFocus, 0, competition.Rounds.Length-1);

        SetupFixturesPanel();
    }

    protected override void OnShow(Competition competition)
    {
        this.competition = competition;
        roundFocus = Mathf.Max(competition.GetMostRecentRound(), 0);

        SetupFixturesPanel();
    }

    private void SetupFixturesPanel()
    {
        gameWeek.text = $"Game Week {(roundFocus+1).ToString()} Fixtures";

        Game.ClearContainer(fixtureContainer);

        foreach (Fixture f in competition.Rounds[roundFocus])
        {
            var obj = Instantiate(fixturePrefab, fixtureContainer);
            obj.SetFixtureText(f, false);
        }
    }
}