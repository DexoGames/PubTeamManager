using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays one <see cref="TacticStat"/> on a slider, refreshing on show and whenever the tactic changes.
/// Deliberately generic and lightweight — stat-specific behaviour (e.g. the complexity squad-IQ feedback)
/// lives in a subclass such as <see cref="ComplexitySlider"/>, so a plain stat slider carries none of it.
/// </summary>
public class TacticStatSlider : MonoBehaviour
{
    public TacticStat stat;
    public Color color;

    protected Slider slider;
    private TextMeshProUGUI statName;

    protected virtual void OnEnable()
    {
        slider = GetComponent<Slider>();
        statName = GetComponentInChildren<TextMeshProUGUI>();
        if (statName != null) statName.text = stat.ToString();

        ApplyColour();
        SetValue();

        var page = FindObjectOfType<TacticsPageUI>();
        if (page != null) page.OnTacticChange.AddListener(SetValue);
    }

    protected virtual void OnDisable()
    {
        var page = FindObjectOfType<TacticsPageUI>();
        if (page != null) page.OnTacticChange.RemoveListener(SetValue);
    }

    /// <summary>Applies the fixed fill colour. Overridden by subclasses that colour the fill dynamically.</summary>
    protected virtual void ApplyColour()
    {
        Image fill = Fill;
        if (fill != null) fill.color = color;
    }

    /// <summary>Pushes the current tactic's value onto the slider. Runs on show and on every tactic change.</summary>
    protected virtual void SetValue()
    {
        Tactic t = CurrentTactic;
        if (t == null || slider == null) return;
        slider.value = t.GetStat(stat);
    }

    /// <summary>The slider's fill Image (null-safe).</summary>
    protected Image Fill => slider != null && slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;

    /// <summary>The player's live tactic, or null if the game isn't ready.</summary>
    protected static Tactic CurrentTactic =>
        TeamManager.Instance != null && TeamManager.Instance.MyTeam != null
            ? TeamManager.Instance.MyTeam.Tactic
            : null;
}
