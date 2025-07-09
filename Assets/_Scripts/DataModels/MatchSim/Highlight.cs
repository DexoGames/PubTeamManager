using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Highlight
{
    public Team Team { get; set; }
    public Minute Minute { get; set; }
    public abstract float Duration { get; }

    protected Highlight(Team team, Minute minute)
    {
        Team = team;
        Minute = minute;
    }

    public abstract string Describe();
}