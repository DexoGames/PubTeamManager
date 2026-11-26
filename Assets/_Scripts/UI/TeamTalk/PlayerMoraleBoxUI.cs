using System;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A squad-member tile on the team-talk screen: name, morale, the player's dressing-room line (from
/// <see cref="TeamTalkChatter"/>), and a coloured ±delta flash showing how they took the talk. During the
/// individual-talk phase the whole box becomes a button that singles that player out.
/// </summary>
public class PlayerMoraleBoxUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] Image morale;
    [SerializeField] TextMeshProUGUI reactionText;
    [SerializeField] Image background;
    [SerializeField] TextMeshProUGUI bubbleText;
    [SerializeField] Image bubbleBackground;
    [SerializeField] Button selectButton;
    [SerializeField] UITheme theme;

    Player player;
    Action<Player> onSelected;

    public Action<string> Preview;
    string line;
    public void OnPointerEnter(PointerEventData data) { Preview?.Invoke(line); }
    public void OnPointerExit(PointerEventData data) { Preview?.Invoke(""); }
    public void OnSelect(BaseEventData data) { Preview?.Invoke(line); }
    public void OnDeselect(BaseEventData data) { Preview?.Invoke(""); }

    public Player Player => player;

    Color PanelColor => theme.GetColor(UITheme.Role.Panel);

    public void Setup(Player p)
    {
        player = p;
        background.color = PanelColor;
        RefreshRole();
        reactionText.text = "";

        ShowBubble("");

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() => onSelected?.Invoke(player));
        selectButton.interactable = false;

        RefreshMorale();
    }

    public void RefreshRole()
    {
        string role = player.SentOff ? "Sent off" : player.LeftInjured ? "Injured" : player.GetPosition()?.ToString() ?? "Bench";
        nameText.text = $"<size=75%>{role}</size>\n{player.Surname}";
    }

    public void RefreshMorale()
    {
        morale.sprite = player.GetMoraleSprite();
        morale.color = player.GetMoraleColor();
    }

    /// <summary>Speaks a line, or hides the bubble when it's empty — staying silent is itself a signal.</summary>
    public void ShowBubble(string line, Color? tint = null)
    {
        this.line = string.IsNullOrWhiteSpace(line) || line == "…" ? "" : $"{player.Surname} · {line}";
        bubbleBackground.gameObject.SetActive(false);
    }

    public void ShowReaction(PlayerReaction r)
    {
        int total = r.mood + r.passion;
        Color colour = TeamTalkReactions.ReactionColour(r.reaction);

        reactionText.text = total > 0 ? $"+{total}" : total.ToString();
        reactionText.color = colour;
        background.color = Color.Lerp(PanelColor, colour, 0.25f);
        RefreshMorale();
    }

    public void SetSelectable(bool selectable, Action<Player> callback = null)
    {
        onSelected = callback;
        selectButton.interactable = selectable;
    }
}
