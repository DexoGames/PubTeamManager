using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Non-interactable slider that displays the team's tactical Familiarity (0–100), shown alongside the
/// other tactic stat sliders. Mirrors <see cref="TacticStatSlider"/>'s refresh pattern: it updates on
/// show and whenever the tactic changes. (Familiarity itself only climbs after matches/training; opening
/// the tactics page re-reads the current value.)
///
/// Wire: put on a Slider (interactability is forced off in code) with a label child.
/// </summary>
[RequireComponent(typeof(Slider))]
public class FamiliaritySlider : MonoBehaviour
{
    public Color color = new Color(0.4f, 0.7f, 1f);

    private Slider slider;
    private TextMeshProUGUI label;

    private void OnEnable()
    {
        slider = GetComponent<Slider>();
        slider.interactable = false;
        slider.minValue = 0;
        slider.maxValue = 100;

        label = GetComponentInChildren<TextMeshProUGUI>();
        label.text = "Familiarity";

        slider.fillRect.GetComponent<Image>().color = color;

        SetValue();
        TacticsPageUI.Instance.OnTacticChange.AddListener(SetValue);
    }

    private void OnDisable()
    {
        TacticsPageUI.Instance.OnTacticChange.RemoveListener(SetValue);
    }

    private void SetValue()
    {
        slider.value = TeamManager.Instance.MyTeam.Tactic.Familiarity;
    }
}
