using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class FixtureUI : MonoBehaviour
{
    public TextMeshProUGUI homeTeam, homeScore, awayTeam, awayScore, date;

    public void SetFixtureText(Fixture fixture, bool showNilNil)
    {
        homeTeam.text = LinkBuilder.BuildLink(fixture.HomeTeam);
        awayTeam.text = LinkBuilder.BuildLink(fixture.AwayTeam);
        date.text = CalenderManager.ShortDateWordsNoYear(fixture.Date.Date);

        if (fixture.BeenPlayed || showNilNil)
        {
            Debug.Log($"{fixture.Result}");
            homeScore.text = fixture.Result.score.home.ToString();
            awayScore.text = fixture.Result.score.away.ToString();
        }
        else
        {
            homeScore.text = string.Empty;
            awayScore.text = string.Empty;
        }
    }
}
