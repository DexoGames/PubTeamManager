using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PossessionHighlight : Highlight
{
    public Phase.Type PhaseType { get; set; }
    public override float Duration => 0.5f;

    public PossessionHighlight(Team team, Minute minute, Phase.Type phase)
        : base(team, minute)
    {
        PhaseType = phase;
    }

    public override string Describe()
    {
        return $"{Team.Name} are in possession: {PhaseType.ToString().Replace('_', ' ')}.";
    }
}
