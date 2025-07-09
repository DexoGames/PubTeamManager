using System;
using TMPro;
using UnityEngine;

public class TeamDetailsUI : UIPage
{
    public static TeamDetailsUI Instance { get; private set; }

    [SerializeField] private TextMeshProUGUI _teamTitleText, _stadiumCapacityText, _formationText, _managerText;
    //[SerializeField] private TextMeshProUGUI _statAttacking, _statDefending, _statMental, _statPhysical;
    [SerializeField] private Transform _teamContainer;
    [SerializeField] private TeamPlayerUI _teamPlayerPrefab;
    [SerializeField] private Transform _teamStatsContainer;
    [SerializeField] private StatUI _teamStatsPrefab;
    [SerializeField] private FormationUI formation;

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
        Game.ClearContainer(_teamContainer);

        _teamTitleText.text = team.TeamName;

        _managerText.text = $"Manager: {LinkBuilder.BuildLink(team.Manager)}";

        _formationText.text = $"{team.Formation.Name}\n{team.Manager.ManStats.Template.templateName}";

        PopulateTeamUI(team);
        ShowAverageStats(team);

        formation.SetFormations(team);
    }

    private void ShowAverageStats(Team team)
    {
        Game.ClearContainer(_teamStatsContainer);

        Instantiate(_teamStatsPrefab, _teamStatsContainer).SetText("Attacking", team.Threat);
        Instantiate(_teamStatsPrefab, _teamStatsContainer).SetText("Defending", team.Security);
        Instantiate(_teamStatsPrefab, _teamStatsContainer).SetText("Mental", team.AvgMental);
        Instantiate(_teamStatsPrefab, _teamStatsContainer).SetText("Physical", team.AvgPhysical);
    }

    private void PopulateTeamUI(Team team)
    {
        foreach (Player p in team.OrderPlayers())
        {
            var newPlayerUI = Instantiate(_teamPlayerPrefab, _teamContainer);
            newPlayerUI.GetComponent<TeamPlayerUI>().SetPlayer(p);
        }
    }
}
