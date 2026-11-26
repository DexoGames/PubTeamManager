using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SquadStatusWidget : UIObject
{
    [SerializeField] Button[] indicators;
    [SerializeField] TextMeshProUGUI details;
    [SerializeField] UITheme theme;
    [SerializeField] int fatigueThreshold = 70;
    [SerializeField] float lowMoraleDistance = 45f;

    public override void Setup()
    {
        var squad = TeamManager.Instance.MyTeam.Players;
        var groups = new (string Label, List<Player> Players, Func<Player, PlayerPickerColumnValue> Column)[]
        {
            ("Fatigue", squad.FindAll(player => !player.IsDeceased && player.Fatigue >= fatigueThreshold),
                player => new PlayerPickerColumnValue($"{player.Fatigue}%")),
            ("Low morale", squad.FindAll(player => !player.IsDeceased && player.Morale.DistanceToIdeal() >= lowMoraleDistance),
                PlayerPickerColumnValue.FromMorale),
            ("Injured", squad.FindAll(player => player.IsInjured),
                player => new PlayerPickerColumnValue($"Back {player.InjuredUntil:d MMM}")),
            ("Suspended", squad.FindAll(player => player.IsSuspended),
                player => new PlayerPickerColumnValue(player.MatchesSuspended == 1 ? "1 match" : $"{player.MatchesSuspended} matches"))
        };

        details.text = groups.All(group => group.Players.Count == 0) ? "Squad ready" : "";
        for (int i = 0; i < indicators.Length; i++)
        {
            Button button = indicators[i];
            var group = groups[i];
            UITheme.Role countRole = group.Players.Count == 0 ? UITheme.Role.MutedText : UITheme.Role.Brass;
            string colour = ColorUtility.ToHtmlStringRGB(theme.GetColor(countRole));
            button.GetComponentInChildren<TextMeshProUGUI>().text =
                $"<color=#{colour}><b>{group.Players.Count}</b></color>\n<size=72%>{group.Label}</size>";
            button.onClick.RemoveAllListeners();
            button.interactable = group.Players.Count > 0;
            button.onClick.AddListener(() => PlayerPickerPopup.Show(group.Players,
                player => UIManager.Instance.ShowPlayerDetails(player.PersonID), group.Label, extraColumn: group.Column));
        }
    }
}
