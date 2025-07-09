using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TacticStatSlider : MonoBehaviour
{
    public TacticStat stat;
    public Color color;

    Slider slider;
    TextMeshProUGUI statName;

    void OnEnable()
    {
        slider = GetComponent<Slider>();
        statName = GetComponentInChildren<TextMeshProUGUI>();

        statName.text = stat.ToString();
        slider.fillRect.GetComponent<Image>().color = color;
        SetValue();
        FindObjectOfType<TacticsPageUI>().OnTacticChange.AddListener(SetValue);
    }

    void SetValue()
    {
        if (TeamManager.Instance == null) return;
        slider.value = TeamManager.Instance.MyTeam.Tactic.GetStat(stat);
    }
}
