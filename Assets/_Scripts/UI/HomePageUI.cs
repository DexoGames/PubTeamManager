using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HomePageUI : UIPage
{
    public static HomePageUI Instance { get; private set; }

    [SerializeField] Transform fixtureContainer;
    [SerializeField] FixtureUI fixturePrefab;

    [SerializeField] Transform leagueContainer;
    [SerializeField] LeagueStandingUI leaguePrefab;


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

    // Start is called before the first frame update
    protected override void OnShow()
    {
        Game.ClearContainer(fixtureContainer);
        Game.ClearContainer(leagueContainer);

        var matchWeek = GameManager.Instance.MatchWeekNum;
        var prevMatchWeek = GameManager.Instance.PrevMatchWeekNum;



        foreach(var f in FixturesManager.Instance.GetMatchWeeks()[prevMatchWeek].fixtures)
        {
            var obj = Instantiate(fixturePrefab, fixtureContainer);
            obj.SetFixtureText(f, false);
        }


        int leagueIndex = 1;
        var standings = LeagueManager.Instance.GetStandings();
        foreach(LeagueTableEntry entry in standings)
        {
            var obj = Instantiate(leaguePrefab, leagueContainer);
            obj.SetLeagueStandingText(entry, leagueIndex);
            leagueIndex++;
        }
    }
}
