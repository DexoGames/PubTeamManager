using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShotHighlight : Highlight
{
    public override float Duration => 0.6f;

    public Player Shooter { get; set; }
    public Player Goalkeep { get; set; }
    public ShotType ShotType { get; set; }
    public ShotOutcome Outcome { get; set; }

    public ShotHighlight(Team team, Minute minute, Player shooter, Player goalkeeper, ShotType shotType, ShotOutcome outcome)
        : base(team, minute)
    {
        Shooter = shooter;
        Goalkeep = goalkeeper;
        ShotType = shotType;
        Outcome = outcome;
    }

    public override string Describe()
    {
        return $"{Shooter.Surname} takes a {ShotType.ToString().ToLower()}...";
    }
}
