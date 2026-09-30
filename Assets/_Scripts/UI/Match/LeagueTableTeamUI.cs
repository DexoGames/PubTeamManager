using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class LeagueTableTeamUI : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI standing, teamName, points;

    public void SetLeagueStandingText(LeagueTableEntry entry, int standing)
    {
        this.standing.text = standing.ToString();
        teamName.text = LinkBuilder.BuildLink(entry.team);
        points.text = entry.points.ToString();
    }

    /// <summary>Marks this row as the player's team. The row has no background graphic, so the labels carry the
    /// colour — at full alpha, since the caller's colour is a translucent background tint.</summary>
    public void Highlight(Color colour)
    {
        colour.a = 1f;
        standing.color = colour;
        teamName.color = colour;
        points.color = colour;
    }
}
