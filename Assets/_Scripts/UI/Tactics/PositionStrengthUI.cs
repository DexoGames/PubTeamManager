using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class PositionStrengthUI : FormationUI
{
    public void SetPositionStrengths(Player player, Team team)
    {
        Team tempTeam = ScriptableObject.CreateInstance<Team>();
        tempTeam.SetTactic(new Tactic(team, team.Manager));
        tempTeam.Tactic.SetFormation(Resources.Load<Formation>("Formations/Other/AllPositions"));
        for(int i  = 0; i < tempTeam.Formation.Positions.Length; i++)
        {
            tempTeam.Players.Add(player);
        }

        //print(tempTeam.Players.Count);

        SetFormations(tempTeam);
    }
}