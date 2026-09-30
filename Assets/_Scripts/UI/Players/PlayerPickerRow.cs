using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One selectable row in the <see cref="PlayerPickerPopup"/>: the player's name, a secondary line
/// (e.g. position strength/rating) and a rating badge.</summary>
public class PlayerPickerRow : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI subText;
    [SerializeField] private Image ratingImage;

    public void Setup(Player player, string subtitle, Action onClick)
    {
        nameText.text = player.FullName;
        subText.text = subtitle;

        Sprite sprite = UIStatDisplay.GetRatingSprite(player.GetRating(player.BestPosition()));
        ratingImage.sprite = sprite;
        ratingImage.enabled = sprite != null;   // no badge sprite for some ratings

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick());
    }
}
