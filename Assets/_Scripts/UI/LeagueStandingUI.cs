using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class LeagueStandingUI : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI standing, teamName, points;

    public void SetLeagueStandingText(LeagueTableEntry entry, int standing)
    {
        this.standing.text = standing.ToString();
        teamName.text = LinkBuilder.BuildLink(entry.team);
        points.text = entry.points.ToString();
    }
}
