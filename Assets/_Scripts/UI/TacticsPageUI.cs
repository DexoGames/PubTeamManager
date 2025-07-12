using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

public class TacticsPageUI : UIPage
{
    public static TacticsPageUI Instance { get; private set; }

    [SerializeField] FormationInteractableUI formationUI;
    [SerializeField] TabController tabController;
    [SerializeField] BenchManager benchManager;
    [SerializeField] FormationDisplayModeControl displayModeControl;

    Team team;

    public UnityEvent OnTacticChange;

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
        this.team = team;
        formationUI.SetFormations(team);
        benchManager.ClearContainer();
        benchManager.Setup(formationUI, team);
    }

    public void SetSizes(int index)
    {
        RectTransform formationRT = formationUI.GetComponent<RectTransform>();
        RectTransform tabsRT = tabController.GetComponent<RectTransform>();

        if(index == 0)
        {
            formationRT.anchorMax = new Vector2(0.5f, 1);
            tabsRT.anchorMin = new Vector2(0.5f, 0);
        }
        else
        {
            formationRT.anchorMax = new Vector2(0.35f, 1);
            tabsRT.anchorMin = new Vector2(0.35f, 0);
        }

        formationRT.offsetMax = new Vector2(0, 0);
        tabsRT.offsetMin = new Vector2(25, 0);

        formationUI.SetFormations(team);
        displayModeControl.SetDisplayMode(displayModeControl.GetCurrentDisplayModeIndex());
    }
}