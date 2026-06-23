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
    [Tooltip("Button shown only when the page was opened from half-time, to go back into the match.")]
    [SerializeField] GameObject resumeMatchButton;

    Team team;

    /// <summary>True when the tactics page was opened mid-match (half-time) and should offer a Resume button.</summary>
    bool returnToMatchMode;

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

        if (resumeMatchButton != null) resumeMatchButton.SetActive(returnToMatchMode);
    }

    /// <summary>Persist tactic changes when the player navigates away from the tactics page.</summary>
    protected override void OnHide()
    {
        SaveManager.Instance?.SaveCore();
    }

    /// <summary>Called by MatchSimPageUI before opening this page at half-time, to show the Resume button.</summary>
    public void EnterReturnToMatchMode()
    {
        returnToMatchMode = true;
    }

    /// <summary>Resume-match button hook: go back into the live match with the edited tactic.</summary>
    public void ResumeMatch()
    {
        returnToMatchMode = false;
        if (resumeMatchButton != null) resumeMatchButton.SetActive(false);
        UIManager.Instance.ResumeMatchFromTactics();
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