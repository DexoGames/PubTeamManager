using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One selectable row in the <see cref="PlayerPickerPopup"/>: a button showing the player's name,
/// an optional secondary line (e.g. position/rating), and an optional rating badge.</summary>
public class PlayerPickerRow : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI nameText;
    [Tooltip("Optional secondary line (the popup passes a per-player description, e.g. position strength/rating).")]
    [SerializeField] private TextMeshProUGUI subText;
    [Tooltip("Optional rating badge (best-position rating sprite).")]
    [SerializeField] private Image ratingImage;

    public void Setup(Player player, string subtitle, Action onClick)
    {
        if (button == null) button = GetComponent<Button>();

        if (nameText != null) nameText.text = player.FullName;
        if (subText != null) subText.text = subtitle ?? "";

        if (ratingImage != null)
        {
            Sprite sprite = UIStatDisplay.GetRatingSprite(player.GetRating(player.BestPosition()));
            ratingImage.sprite = sprite;
            ratingImage.enabled = sprite != null;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick());
        Debug.Log("" + player.FullName);
        Debug.Log(onClick);
    }
}
