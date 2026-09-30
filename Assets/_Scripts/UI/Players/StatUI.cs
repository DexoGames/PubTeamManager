using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class StatUI : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI statName;
    [SerializeField] TextMeshProUGUI statValue;

    [SerializeField] Color[] colors;

    internal void SetText(string name, int value)
    {
        statName.text = name;
        statValue.text = value.ToString();

        statValue.color = colors[Mathf.Clamp(value / 20, 0, colors.Length-1)];
    }
}
