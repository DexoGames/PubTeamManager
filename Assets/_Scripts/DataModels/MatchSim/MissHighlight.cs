using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MissHighlight : ShotHighlight
{
    public MissHighlight(Team team, Player shooter, Player goalkeeper, ShotType shotType, ShotOutcome outcome)
        : base(team, shooter, goalkeeper, shotType, outcome)
    {

    }

    public override string Describe()
    {
        return $"But in the end it's {Outcome.ToString()}!";
    }
}
