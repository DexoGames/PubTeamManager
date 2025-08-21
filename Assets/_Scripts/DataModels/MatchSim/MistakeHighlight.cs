using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MistakeHighlight : Highlight
{
    public override float Duration => 0.6f;
    public Player Player { get; set; }

    public MistakeHighlight(Team team, Minute minute, Player player) : base(team, minute)
    {
        Player = player;
    }

    public override string Describe()
    {
        return $"{Team.Name}'s {Player.Surname} makes a mistake!";
    }
}
