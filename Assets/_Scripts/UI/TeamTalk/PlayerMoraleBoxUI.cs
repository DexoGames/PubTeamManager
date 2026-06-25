using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A little squad-member tile for the team-talk screen: shows the player's name and morale, and flashes how they
/// took the talk (a coloured tint + a ±delta). All references are optional, so the prefab can be as minimal as a
/// name + one bar.
/// </summary>
public class PlayerMoraleBoxUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [Tooltip("Filled image (Image type = Filled) showing Mood 0–100.")]
    [SerializeField] private Image moodFill;
    [Tooltip("Optional second bar for Passion 0–100.")]
    [SerializeField] private Image passionFill;
    [Tooltip("Optional numeric mood read-out.")]
    [SerializeField] private TextMeshProUGUI moraleText;
    [Tooltip("Optional ±delta flash after a talk (e.g. \"+8\").")]
    [SerializeField] private TextMeshProUGUI reactionText;
    [Tooltip("Optional background image tinted by the reaction verdict.")]
    [SerializeField] private Image background;

    private Player player;
    private Color baseBackground = Color.white;

    public void Setup(Player p)
    {
        player = p;
        if (background != null) baseBackground = background.color;
        if (nameText != null) nameText.text = p != null ? p.Surname : "";
        if (reactionText != null) reactionText.text = "";
        if (background != null) background.color = baseBackground;
        RefreshMorale();
    }

    /// <summary>Re-reads the player's current morale into the bars/number.</summary>
    public void RefreshMorale()
    {
        if (player == null) return;
        if (moodFill != null)
        {
            moodFill.fillAmount = player.Morale.Mood / 100f;
            moodFill.color = MoraleColour(player.Morale.Mood);
        }
        if (passionFill != null) passionFill.fillAmount = player.Morale.Passion / 100f;
        if (moraleText != null) moraleText.text = player.Morale.Mood.ToString();
    }

    /// <summary>Shows how this player took the talk, then updates the morale display to the new values.</summary>
    public void ShowReaction(PlayerReaction r)
    {
        int total = r.mood + r.passion;
        Color colour = TeamTalkReactions.ReactionColour(r.reaction);
        if (reactionText != null)
        {
            reactionText.text = total > 0 ? $"+{total}" : total.ToString();
            reactionText.color = colour;
        }
        if (background != null) background.color = colour;
        RefreshMorale();
    }

    private static Color MoraleColour(int mood) =>
        mood >= 66 ? new Color(0.35f, 0.75f, 0.40f) :
        mood >= 33 ? new Color(0.85f, 0.75f, 0.35f) :
                     new Color(0.85f, 0.40f, 0.40f);
}
