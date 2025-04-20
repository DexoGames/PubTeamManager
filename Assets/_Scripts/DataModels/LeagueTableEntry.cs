using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LeagueTableEntry
{
    public Team team { get; private set; }
    public int points { get; set; }
    public int goalsFor { get; set; }
    public int goalsAgainst { get; set; }
    public int goalDifference => goalsFor - goalsAgainst;

    public LeagueTableEntry(Team team)
    {
        this.team = team;
        points = 0;
        goalsFor = 0;
        goalsAgainst = 0;
    }

}
