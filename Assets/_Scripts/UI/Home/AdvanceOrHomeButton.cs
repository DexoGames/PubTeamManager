using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The top-nav action button. While the home page is active it reads "Next Day" and advances
/// the calendar; on any other page it reads "Home" and returns to the home page (so you must
/// be on the home screen to advance — which also guarantees the day-timeline slide is visible).
///
/// Put this on the button and assign its Button + label; remove any existing inspector OnClick
/// that calls CalenderManager.AdvanceDay directly.
/// </summary>
public class AdvanceOrHomeButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private string advanceText = "Next Day";
    [SerializeField] private string homeText = "Home";

    private void Start()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnClick);
            button.onClick.AddListener(OnClick);
        }
        UpdateLabel();
    }

    private void Update() => UpdateLabel();

    private void UpdateLabel()
    {
        if (label != null)
            label.text = IsHome ? advanceText : homeText;
    }

    private bool IsHome => UIManager.Instance != null && UIManager.Instance.IsHomeActive;

    private void OnClick()
    {
        if (IsHome)
        {
            if (CalenderManager.Instance != null) CalenderManager.Instance.AdvanceDay();
        }
        else
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowHomePage();
        }
    }
}
