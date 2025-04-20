using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public class FormationUI : MonoBehaviour
{
    protected Formation[] formations;

    [SerializeField] protected Team currentTeam;
    [SerializeField] protected PositionUI positionPrefab;
    [SerializeField] protected RectTransform formationContainer; 

    protected List<PositionUI> allPositions = new List<PositionUI>();

    public void SetFormations(Team team)
    {
        currentTeam = team;
        formations = Resources.LoadAll<Formation>("Formations/Usable");

        SetDropdown(team);

        SpawnFormation(team.Formation);
    }

    public virtual void SetDropdown(Team team)
    {
        
    }

    public void NewFormation(int i)
    {
        currentTeam.Tactic.SetFormation(formations[i]);


        for (int p = 0; p < allPositions.Count; p++)
        {
            allPositions[p].Reassign(formations[i].Positions[p], allPositions[p].player);
        }
    }

    public void SpawnFormation(Formation formation)
    {
        foreach(PositionUI p in allPositions)
        {
            if (p == null) continue;
            Destroy(p.gameObject);
        }
        allPositions.Clear();

        for (int i = 0; i < formation.Positions.Length; i++)
        {
            PositionUI newPos = Instantiate(positionPrefab, formationContainer);
            newPos.Setup(currentTeam.Players[i], formation.Positions[i], i, this, formationContainer);
            SetupInteractable(newPos);
            allPositions.Add(newPos);
        }
    }

    public virtual void SetupInteractable(PositionUI newPos)
    {
        
    }
}
