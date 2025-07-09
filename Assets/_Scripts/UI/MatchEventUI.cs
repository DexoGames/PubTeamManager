using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MatchEventUI : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _text;
    [SerializeField] TextMeshProUGUI _minute;
    [SerializeField] Image backgorund;

    Team team;

    internal void SetText(Team team, string str, int minute)
    {
        this.team = team;

        _text.text = str;
        _text.color = GetContrastingTextColor(team.TeamColor);
        _minute.text = minute.ToString();
        _minute.color = GetContrastingTextColor(team.TeamColor);

        backgorund.color = team.TeamColor;
    }

    public static Color GetContrastingTextColor(Color background)
    {
        float luminance = 0.2126f * background.r + 0.7152f * background.g + 0.0722f * background.b;

        return luminance > 0.5f ? Color.black : Color.white;
    }
}
