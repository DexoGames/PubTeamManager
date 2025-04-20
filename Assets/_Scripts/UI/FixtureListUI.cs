using TMPro;
using UnityEngine;

public class FixtureListUI : UIPage
{
    public static FixtureListUI Instance { get; private set; }

    private int weekFocus = 0;

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
        weekFocus = weekFocus + amount < 0 ? 0 : weekFocus + amount > FixturesManager.Instance.GetMatchWeeks().Count - 1 ? weekFocus : weekFocus + amount;

        SetupFixturesPanel();
    }

    protected override void OnShow()
    {
        weekFocus = Mathf.Max(GameManager.Instance.MatchWeekNum-2, 0);

        SetupFixturesPanel();
    }

    private void SetupFixturesPanel()
    {
        gameWeek.text = $"Game Week {(weekFocus+1).ToString()} Fixtures";

        Game.ClearContainer(fixtureContainer);

        // Set up Results panel
        foreach (Fixture f in FixturesManager.Instance.GetMatchWeek(weekFocus).fixtures)
        {
            var obj = Instantiate(fixturePrefab, fixtureContainer);
            obj.SetFixtureText(f, false);
        }
    }
}