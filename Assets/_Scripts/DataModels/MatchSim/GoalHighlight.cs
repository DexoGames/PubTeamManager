using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GoalHighlight : ShotHighlight
{
    public Player Assister { get; set; }

    public GoalHighlight(Team team, Player shooter, Player goalkeeper, ShotType shotType, ShotOutcome outcome, Player assister = null)
        : base(team, shooter, goalkeeper, shotType, outcome)
    {
        Assister = assister;
    }

    public override string Describe()
    {
        return $"GOAL for {Team.TeamName}! {Shooter.Surname} scores with a {ShotType.ToString().ToLower()}!";
    }
}
