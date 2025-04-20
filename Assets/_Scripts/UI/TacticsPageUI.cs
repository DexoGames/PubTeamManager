using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;

public class TacticsPageUI : UIPage
{
    public static TacticsPageUI Instance { get; private set; }

    [SerializeField] FormationInteractableUI formationUI;
    [SerializeField] BenchManager benchManager;
    [SerializeField] TextMeshProUGUI teamName;

    public void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    protected override void OnShow(Team team)
    {
        base.OnShow(team);
        formationUI.SetFormations(team);
        benchManager.ClearContainer();
        benchManager.Setup(formationUI, team);
        teamName.text = LinkBuilder.BuildLink(team) + " Tactics";
    }
}