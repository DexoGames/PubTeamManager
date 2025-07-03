using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShotHighlight : Highlight
{
    public Player Shooter { get; set; }
    public Player Goalkeep { get; set; }
    public ShotType ShotType { get; set; }
    public ShotOutcome Outcome { get; set; }

    public ShotHighlight(Team team, Player shooter, Player goalkeeper, ShotType shotType, ShotOutcome outcome)
        : base(team)
    {
        Shooter = shooter;
        Goalkeep = goalkeeper;
        ShotType = shotType;
        Outcome = outcome;
    }

    public override string Describe()
    {
        return $"{Team.TeamName}'s {Shooter.Surname} takes a {ShotType.ToString().ToLower()}...";
    }
}
