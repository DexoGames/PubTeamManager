using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Highlight
{
    public Team Team { get; set; }

    protected Highlight(Team team)
    {
        Team = team;
    }

    public abstract string Describe();
}