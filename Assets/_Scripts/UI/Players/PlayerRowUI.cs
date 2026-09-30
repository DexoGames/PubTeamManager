using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reference holder for a single player row in the positional-training player list.
/// Put this on the playerRowPrefab and wire the fields, so TrainingPageUI can populate
/// the row directly (no fragile GetComponentInChildren lookups, and it can target the
/// rating Image specifically rather than the toggle's own images).
/// </summary>
public class PlayerRowUI : MonoBehaviour
{
    public Toggle toggle;
    public TextMeshProUGUI nameLabel;
    public Image ratingImage;
}
