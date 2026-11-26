using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A whole-number slider (0 = Ultra-Defensive … 6 = All-Out Attack) that sets the team's base
/// <see cref="Mentality"/> on the tactics page. This is the persistent, pre-match version of the
/// defensive↔attacking dial; the in-match "More Attacking/Defensive" buttons nudge it temporarily.
///
/// Wire: put on a Slider; assign the value label. Mirrors the TacticStatSlider refresh pattern.
/// </summary>
public class MentalitySelectorUI : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private TextMeshProUGUI label;

    private void OnEnable()
    {
        if (slider == null) slider = GetComponent<Slider>();

        slider.wholeNumbers = true;
        slider.minValue = 0;
        slider.maxValue = Game.GetEnumLength<Mentality>() - 1;

        slider.SetValueWithoutNotify((int)CurrentTactic().BaseMentality);

        slider.onValueChanged.RemoveListener(OnChanged);
        slider.onValueChanged.AddListener(OnChanged);

        UpdateLabel();
    }

    private void OnChanged(float v)
    {
        Tactic t = CurrentTactic();
        t.BaseMentality = (Mentality)Mathf.Clamp((int)v, 0, Game.GetEnumLength<Mentality>() - 1);
        t.RecalculateStats();
        UpdateLabel();

        var page = TacticsPageUI.Instance;
        if (page != null) page.OnTacticChange?.Invoke();
    }

    private void UpdateLabel()
    {
        label.text = MatchSimPageUI.MentalityLabel(CurrentTactic().BaseMentality);
    }

    private static Tactic CurrentTactic() => TeamManager.Instance.MyTeam.Tactic;
}
