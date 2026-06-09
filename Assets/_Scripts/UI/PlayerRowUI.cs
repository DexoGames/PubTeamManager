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
    [Tooltip("The selection toggle for this player.")]
    public Toggle toggle;

    [Tooltip("Name + position-strength label (TextMeshPro).")]
    public TextMeshProUGUI nameLabel;

    [Tooltip("Image that shows the player's current ability rating (A/B/C…) in the selected position.")]
    public Image ratingImage;
}
