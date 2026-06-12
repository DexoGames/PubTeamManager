using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Home-screen squad-status widget: flags players who are fatigued or low on morale (with
/// clickable name links), plus placeholders for injuries/suspensions (no system yet).
/// Renders into a single rich-text TMP block — attach a LinkHandler to that text so the
/// player links are clickable. A UIObject (auto-refreshes on show).
/// </summary>
public class SquadStatusWidget : UIObject
{
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private int fatigueThreshold = 70;
    [SerializeField] private float lowMoraleDistance = 45f;
    [SerializeField] private int maxNamesPerLine = 4;

    public override void Setup() => Refresh();

    public void Refresh()
    {
        if (statusText == null) return;

        Team team = TeamManager.Instance != null ? TeamManager.Instance.MyTeam : null;
        if (team == null || team.Players == null) { statusText.text = ""; return; }

        var fatigued = team.Players.Where(p => p.Fatigue >= fatigueThreshold).ToList();
        var lowMorale = team.Players.Where(p => p.Morale.DistanceToIdeal() >= lowMoraleDistance).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("<b>Squad Status</b>");
        sb.AppendLine(Line("Fatigued", fatigued));
        sb.AppendLine(Line("Low morale", lowMorale));
        sb.Append("Injured: 0    Suspended: 0"); // hooks for future systems

        statusText.text = sb.ToString();
    }

    private string Line(string label, List<Player> players)
    {
        if (players.Count == 0) return $"{label}: none";

        string names = string.Join(", ", players.Take(maxNamesPerLine).Select(p => LinkBuilder.BuildLink(p)));
        string more = players.Count > maxNamesPerLine ? $" +{players.Count - maxNamesPerLine}" : "";
        return $"{label} ({players.Count}): {names}{more}";
    }
}
