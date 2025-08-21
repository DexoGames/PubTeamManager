using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GoalHighlight : ShotHighlight
{
    public override float Duration => 0.8f;


    public Player Assister { get; set; }

    public GoalHighlight(Team team, Minute minute, Player shooter, Player goalkeeper, ShotType shotType, ShotOutcome outcome, Player assister = null)
        : base(team, minute, shooter, goalkeeper, shotType, outcome)
    {
        Assister = assister;
    }

    public override string Describe()
    {
        return $"GOAL for {Team.Name}! {Shooter.Surname} scores with a {ShotType.ToString().ToLower()}!";
    }
}
