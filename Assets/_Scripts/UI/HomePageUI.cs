using System;
using TMPro;
using UnityEngine;

public class HomePageUI : UIPage
{
    public static HomePageUI Instance { get; private set; }

    [SerializeField] Transform fixtureContainer;
    [SerializeField] FixtureUI fixturePrefab;
    [SerializeField] TextMeshProUGUI competitionName;

    Competition latestCompetition;
    int latestRoundFocus = -1;
    DateTime latestMyTeamRoundEndDate; // last round involving MyTeam

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

    protected override void OnShow()
    {
        Game.ClearContainer(fixtureContainer);

        Fixture upcomingFixture = TeamManager.Instance.MyTeam.GetUpcomingFixture();
        Competition competition = upcomingFixture.Competition;
        int roundFocus = competition.GetUpcomingRound();

        if (latestCompetition != null && latestRoundFocus >= 0)
        {
            if (CalenderManager.Instance.CurrentDay <= latestMyTeamRoundEndDate.AddDays(1))
            {
                competition = latestCompetition;
                roundFocus = latestRoundFocus;
            }
        }

        competitionName.text = $"{competition.Name}, Round {roundFocus + 1}";

        foreach (var f in competition.Rounds[roundFocus])
        {
            var obj = Instantiate(fixturePrefab, fixtureContainer);
            obj.SetFixtureText(f, false);
        }

        latestCompetition = competition;
        latestRoundFocus = roundFocus;
        latestMyTeamRoundEndDate = GetMyTeamRoundEndDate(competition, roundFocus);
    }

    private DateTime GetMyTeamRoundEndDate(Competition comp, int roundIndex)
    {
        DateTime latest = DateTime.MinValue;
        Team myTeam = TeamManager.Instance.MyTeam;

        foreach (var fixture in comp.Rounds[roundIndex])
        {
            if (fixture.HomeTeam == myTeam || fixture.AwayTeam == myTeam)
            {
                if (fixture.Date > latest)
                    latest = fixture.Date;
            }
        }

        return latest;
    }
}
