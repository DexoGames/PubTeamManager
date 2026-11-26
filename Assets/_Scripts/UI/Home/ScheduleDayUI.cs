using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScheduleDayUI : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI dateLabel;
    [SerializeField] TextMeshProUGUI activityLabel;
    [SerializeField] Image icon;

    public void Show(string date, string activity, Sprite sprite)
    {
        dateLabel.text = date;
        activityLabel.text = activity;
        icon.sprite = sprite;
    }
}
