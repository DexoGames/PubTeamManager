using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Complexity stat slider, with squad-IQ feedback. The fill turns red — and an optional warning label
/// appears — when the on-pitch XI's AVERAGE intelligence can't handle the chosen complexity (the same
/// condition as the in-match complexity penalty: CurrentStartingIntelligence &lt; IntelligenceThreshold);
/// green and hidden when the squad can cope.
///
/// Use this INSTEAD of a plain <see cref="TacticStatSlider"/> on the complexity slider only — no other
/// slider pays for any of this. It forces its own <c>stat</c> to Complexity, so that field can be ignored.
/// </summary>
public class ComplexitySlider : TacticStatSlider
{
    [Header("Squad-IQ feedback")]
    [Tooltip("Fill colour when the squad's average intelligence can handle this complexity.")]
    public Color okColor = new Color(0.43f, 0.81f, 0.43f);
    [Tooltip("Fill colour when this complexity is too high for the squad's average intelligence.")]
    public Color tooHighColor = new Color(0.88f, 0.43f, 0.43f);
    [Tooltip("Optional label shown ONLY while the squad IQ is too low. Place it as a SIBLING above the slider.")]
    public GameObject squadIqTooLowWarning;

    protected override void OnEnable()
    {
        stat = TacticStat.Complexity; // this slider always tracks complexity
        base.OnEnable();
    }

    // The fill is coloured dynamically in SetValue, so there's no fixed colour to apply.
    protected override void ApplyColour() { }

    protected override void SetValue()
    {
        base.SetValue();

        Tactic t = CurrentTactic;
        if (t == null) return;

        bool tooHigh = t.CurrentStartingIntelligence() < t.IntelligenceThreshold;

        Image fill = Fill;
        if (fill != null) fill.color = tooHigh ? tooHighColor : okColor;

        if (squadIqTooLowWarning != null)
            squadIqTooLowWarning.SetActive(tooHigh);
    }
}
