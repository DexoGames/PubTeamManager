using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PossessionFailHighlight : PossessionHighlight
{

    public PossessionFailHighlight(Team team, Minute minute, Phase.Type phase) : base(team, minute, phase)
    {

    }

    public override string Describe()
    {
        return $"But it results in nothing";
    }
}
