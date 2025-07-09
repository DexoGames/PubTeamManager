using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MissHighlight : ShotHighlight
{
    public override float Duration => 0.8f;

    public MissHighlight(Team team, Minute minute, Player shooter, Player goalkeeper, ShotType shotType, ShotOutcome outcome)
        : base(team, minute, shooter, goalkeeper, shotType, outcome)
    {

    }

    public override string Describe()
    {
        return $"But in the end it's {Outcome.ToString()}!";
    }
}
