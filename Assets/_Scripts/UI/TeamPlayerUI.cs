using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TeamPlayerUI : MonoBehaviour
{
    public TextMeshProUGUI PlayerName, Position;

    public void SetPlayer(Player player)
    {
        PlayerName.text = LinkBuilder.BuildLink(player);
        Position.text = player.Team.Formation.SquadRole(player.Team.GetPlayerIndex(player));
        PlayerName.text += " <size=18>" + player.AddBestPositions() + "</size>";
    }
}
