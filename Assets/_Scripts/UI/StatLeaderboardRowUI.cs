using TMPro;
using UnityEngine;

/// <summary>
/// One row of a stat leaderboard — rank, a clickable player/team name, and the value. Mirrors
/// <see cref="LeagueTableTeamUI"/>. Put a <c>LinkHandler</c> on the name text so the link is clickable.
/// </summary>
public class StatLeaderboardRowUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI rankText, nameText, valueText;

    /// <param name="linkedName">Rich-text link from <see cref="LinkBuilder"/> (player or team).</param>
    public void Set(int rank, string linkedName, int value)
    {
        if (rankText != null) rankText.text = rank.ToString();
        if (nameText != null) nameText.text = linkedName;
        if (valueText != null) valueText.text = value.ToString();
    }
}
