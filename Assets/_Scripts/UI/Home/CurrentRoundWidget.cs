using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Home-screen widget listing the current gameweek/round's fixtures for the player's
/// upcoming competition. Ported from the old HomePageUI: it sticks to the just-played round
/// for a day after it ends, then advances to the next round. A UIObject (auto-refreshes on show).
/// </summary>
public class CurrentRoundWidget : UIObject
{
    [SerializeField] private Transform fixtureContainer;
    [SerializeField] private FixtureUI fixturePrefab;
    [SerializeField] private TextMeshProUGUI competitionName;

    private Competition latestCompetition;
    private int latestRoundFocus = -1;
    private DateTime latestMyTeamRoundEndDate; // last round involving MyTeam

    public override void Setup() => Refresh();

    public void Refresh()
    {
        if (fixtureContainer == null) return;
        Game.ClearContainer(fixtureContainer);

        Team myTeam = TeamManager.Instance != null ? TeamManager.Instance.MyTeam : null;
        if (myTeam == null) return;

        Fixture upcomingFixture = myTeam.GetUpcomingFixture();
        if (upcomingFixture == null)
        {
            if (competitionName != null) competitionName.text = "No upcoming fixtures";
            return;
        }

        Competition competition = upcomingFixture.Competition;
        int roundFocus = competition.GetUpcomingRound();

        // Keep showing the just-finished round for a day after it ends.
        if (latestCompetition != null && latestRoundFocus >= 0)
        {
            if (CalenderManager.Instance.CurrentDay <= latestMyTeamRoundEndDate.AddDays(1))
            {
                competition = latestCompetition;
                roundFocus = latestRoundFocus;
            }
        }

        if (competitionName != null)
            competitionName.text = $"{competition.Name}, Round {roundFocus + 1}";

        if (competition.Rounds != null && roundFocus >= 0 && roundFocus < competition.Rounds.Length)
        {
            foreach (var f in competition.Rounds[roundFocus])
            {
                var obj = Instantiate(fixturePrefab, fixtureContainer);
                obj.SetFixtureText(f, false);
            }
        }

        latestCompetition = competition;
        latestRoundFocus = roundFocus;
        latestMyTeamRoundEndDate = GetMyTeamRoundEndDate(competition, roundFocus);
    }

    private DateTime GetMyTeamRoundEndDate(Competition comp, int roundIndex)
    {
        DateTime latest = DateTime.MinValue;
        Team myTeam = TeamManager.Instance.MyTeam;

        if (comp.Rounds == null || roundIndex < 0 || roundIndex >= comp.Rounds.Length) return latest;

        foreach (var fixture in comp.Rounds[roundIndex])
        {
            if ((fixture.HomeTeam == myTeam || fixture.AwayTeam == myTeam) && fixture.Date > latest)
                latest = fixture.Date;
        }

        return latest;
    }
}
