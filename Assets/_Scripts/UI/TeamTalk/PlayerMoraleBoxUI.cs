using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A squad-member tile on the team-talk screen: name, morale, the player's dressing-room line (from
/// <see cref="TeamTalkChatter"/>), and a coloured ±delta flash showing how they took the talk. During the
/// individual-talk phase the whole box becomes a button that singles that player out.
/// </summary>
public class PlayerMoraleBoxUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Image morale;
    [SerializeField] private TextMeshProUGUI reactionText;
    [SerializeField] private Image background;
    [SerializeField] private TextMeshProUGUI bubbleText;
    [SerializeField] private Image bubbleBackground;
    [SerializeField] private Button selectButton;

    private Player player;
    private Action<Player> onSelected;

    public Player Player => player;

    public void Setup(Player p)
    {
        player = p;
        nameText.text = LinkBuilder.BuildLink(p);
        reactionText.text = "";

        ShowBubble("");

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() => onSelected?.Invoke(player));
        selectButton.interactable = false;

        RefreshMorale();
    }

    public void RefreshMorale()
    {
        morale.sprite = player.GetMoraleSprite();
        morale.color = player.GetMoraleColor();
    }

    /// <summary>Speaks a line, or hides the bubble when it's empty — staying silent is itself a signal.</summary>
    public void ShowBubble(string line, Color? tint = null)
    {
        bool speaking = !string.IsNullOrEmpty(line);
        bubbleText.text = line;
        bubbleText.gameObject.SetActive(speaking);
        bubbleBackground.gameObject.SetActive(speaking);
        if (speaking && tint.HasValue) bubbleBackground.color = tint.Value;
    }

    public void ShowReaction(PlayerReaction r)
    {
        int total = r.mood + r.passion;
        Color colour = TeamTalkReactions.ReactionColour(r.reaction);

        reactionText.text = total > 0 ? $"+{total}" : total.ToString();
        reactionText.color = colour;
        background.color = colour;
        RefreshMorale();
    }

    public void SetSelectable(bool selectable, Action<Player> callback = null)
    {
        onSelected = callback;
        selectButton.interactable = selectable;
    }
}
