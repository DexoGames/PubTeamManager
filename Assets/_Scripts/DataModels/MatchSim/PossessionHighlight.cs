using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PossessionHighlight : Highlight
{
    public Phase.Type PhaseType { get; set; }

    public PossessionHighlight(Team team, Phase.Type phase)
        : base(team)
    {
        PhaseType = phase;
    }

    public override string Describe()
    {
        return $"{Team.TeamName} are in possession: {PhaseType.ToString().Replace('_', ' ')}.";
    }
}
