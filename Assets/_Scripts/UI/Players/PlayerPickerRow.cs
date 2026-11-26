using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerPickerRow : MonoBehaviour
{
    [SerializeField] Button button;
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] TextMeshProUGUI subText;
    [SerializeField] Image ratingImage;
    [SerializeField] RectTransform extraColumn;
    [SerializeField] TextMeshProUGUI extraText;
    [SerializeField] Image extraIcon;

    public void Setup(Player player, string subtitle, Action onClick, PlayerPickerColumnValue? extra = null)
    {
        nameText.text = player.FullName;
        subText.text = subtitle;

        Sprite rating = UIStatDisplay.GetRatingSprite(player.GetRating(player.BestPosition()));
        ratingImage.sprite = rating;
        ratingImage.enabled = rating != null;

        SetExtraColumn(extra);

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick());
    }

    void SetExtraColumn(PlayerPickerColumnValue? extra)
    {
        extraColumn.gameObject.SetActive(extra.HasValue);
        if (extra.HasValue)
        {
            PlayerPickerColumnValue value = extra.Value;
            extraText.text = value.Text;
            extraText.gameObject.SetActive(!string.IsNullOrEmpty(value.Text));
            extraIcon.sprite = value.Icon;
            extraIcon.color = value.IconColor;
            extraIcon.gameObject.SetActive(value.Icon != null);
        }

        RectTransform nextColumn = extra.HasValue ? extraColumn : ratingImage.rectTransform;
        float rightEdge = nextColumn.offsetMin.x - 12;
        SetTextRightEdge(nameText.rectTransform, rightEdge);
        SetTextRightEdge(subText.rectTransform, rightEdge);
    }

    static void SetTextRightEdge(RectTransform text, float rightEdge)
    {
        text.offsetMax = new Vector2(rightEdge, text.offsetMax.y);
    }
}
