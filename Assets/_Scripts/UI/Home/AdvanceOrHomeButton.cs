using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The top-nav action button, with three states:
///   • not on the home page → "Home" (returns to the home page; you must be home to advance)
///   • home + a match pending today → "Play Game" (plays the match on the current day)
///   • home + no match → "Next Day" (advances the calendar)
///
/// Routes play/advance through GameManager (which shows the loading overlay, simulates AI matches,
/// saves, and then animates the day strip). Greys out while the game is busy.
///
/// Put this on the button and assign its Button + label; remove any old inspector OnClick that
/// called CalenderManager.AdvanceDay directly.
/// </summary>
public class AdvanceOrHomeButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private string advanceText = "Next Day";
    [SerializeField] private string playText = "Play Game";
    [SerializeField] private string homeText = "Home";

    private void Start()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnClick);
            button.onClick.AddListener(OnClick);
        }
        UpdateState();
    }

    private void Update() => UpdateState();

    private void UpdateState()
    {
        bool busy = GameManager.Instance != null && GameManager.Instance.IsBusy;
        if (button != null) button.interactable = !busy;

        if (label == null) return;
        if (!IsHome) label.text = homeText;
        else if (HasMatch) label.text = playText;
        else label.text = advanceText;
    }

    private bool IsHome => UIManager.Instance != null && UIManager.Instance.IsHomeActive;
    private bool HasMatch => GameManager.Instance != null && GameManager.Instance.HasPendingPlayerMatch();

    private void OnClick()
    {
        if (!IsHome)
        {
            UIManager.Instance?.ShowHomePage();
            return;
        }
        GameManager.Instance?.AdvanceOrPlay();
    }
}
