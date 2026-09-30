using Newtonsoft.Json;
using UnityEngine;

public class LeagueTableEntry
{
    [JsonConverter(typeof(TeamRefConverter))]
    public Team team { get; set; }
    public int points { get; set; }
    public int goalsFor { get; set; }
    public int goalsAgainst { get; set; }
    public int wins { get; set; }
    public int draws { get; set; }
    public int losses { get; set; }
    [JsonIgnore] public int played => wins + draws + losses;
    [JsonIgnore] public int goalDifference => goalsFor - goalsAgainst;

    public LeagueTableEntry() { }

    public LeagueTableEntry(Team team)
    {
        this.team = team;
        points = 0;
        goalsFor = 0;
        goalsAgainst = 0;
        wins = 0;
        draws = 0;
        losses = 0;
    }

}
