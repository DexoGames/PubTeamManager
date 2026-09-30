using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LeagueTable : UIObject
{
    [SerializeField] LeagueTableTeamUI leaguePrefab;
    [SerializeField] RectTransform leagueContainer;

    public override void Setup()
    {
        Game.ClearContainer(leagueContainer);

        int leagueIndex = 1;
        var standings = TeamManager.Instance.MyTeam.GetMainLeague().GetStandings();
        foreach (LeagueTableEntry entry in standings)
        {
            var obj = Instantiate(leaguePrefab, leagueContainer);
            obj.SetLeagueStandingText(entry, leagueIndex);
            leagueIndex++;
        }
    }
}
