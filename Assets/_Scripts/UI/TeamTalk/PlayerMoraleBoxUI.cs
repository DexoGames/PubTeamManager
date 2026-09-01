using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A little squad-member tile for the team-talk screen: shows the player's name and morale, a speech BUBBLE carrying
/// their in-character dressing-room line (from <see cref="TeamTalkChatter"/>), and flashes how they took the talk (a
/// coloured tint + a ±delta). In the individual-talk phase the whole box becomes a Button you click to single a
/// player out. All references are optional, so the prefab can be as minimal as a name + one bar.
/// </summary>
public class PlayerMoraleBoxUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Image morale;

    [Tooltip("Optional numeric mood read-out.")]
    [SerializeField] private TextMeshProUGUI moraleText;
    [Tooltip("Optional ±delta flash after a talk (e.g. \"+8\").")]
    [SerializeField] private TextMeshProUGUI reactionText;
    [Tooltip("Optional background image tinted by the reaction verdict.")]
    [SerializeField] private Image background;

    [Header("Chatter")]
    [Tooltip("Optional: the player's spoken line. Hidden when they say nothing (silence is itself a signal).")]
    [SerializeField] private TextMeshProUGUI bubbleText;
    [Tooltip("Optional: bubble backing image, shown/hidden with the line and tinted by the reaction.")]
    [SerializeField] private Image bubbleBackground;

    [Header("Individual talk")]
    [Tooltip("Optional: makes the box clickable to single this player out during the individual-talk phase.")]
    [SerializeField] private Button selectButton;

    private Player player;
    private Color baseBackground = Color.white;
    private Action<Player> onSelected;

    public Player Player => player;

    public void Setup(Player p)
    {
        player = p;
        if (background != null) baseBackground = background.color;
        if (nameText != null) nameText.text = p != null ? p.Surname : "";
        if (reactionText != null) reactionText.text = "";
        if (background != null) background.color = baseBackground;

        ShowBubble("");

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => onSelected?.Invoke(player));
            selectButton.interactable = false;
        }

        RefreshMorale();
    }

    /// <summary>Re-reads the player's current morale into the bars/number.</summary>
    public void RefreshMorale()
    {
        morale.sprite = player.GetMoraleSprite();
        morale.color = player.GetMoraleColor();
    }

    /// <summary>Shows a spoken line (or hides the bubble when empty). Optionally tints the bubble by reaction.</summary>
    public void ShowBubble(string line, Color? tint = null)
    {
        bool has = !string.IsNullOrEmpty(line);
        if (bubbleText != null)
        {
            bubbleText.text = line;
            bubbleText.gameObject.SetActive(has);
        }
        if (bubbleBackground != null)
        {
            bubbleBackground.gameObject.SetActive(has);
            if (has && tint.HasValue) bubbleBackground.color = tint.Value;
        }
    }

    /// <summary>Shows how this player took the talk (tint + ±delta), then updates the morale display to the new values.</summary>
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

    /// <summary>Enables/disables clicking this box to single the player out, and sets the click handler.</summary>
    public void SetSelectable(bool selectable, Action<Player> callback = null)
    {
        onSelected = callback;
        if (selectButton != null) selectButton.interactable = selectable;
    }

    private static Color MoraleColour(int mood) =>
        mood >= 66 ? new Color(0.35f, 0.75f, 0.40f) :
        mood >= 33 ? new Color(0.85f, 0.75f, 0.35f) :
                     new Color(0.85f, 0.40f, 0.40f);
}
