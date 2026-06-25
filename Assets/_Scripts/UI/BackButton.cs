using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A "back" button: returns to the previously shown page (browser-style), using the navigation history
/// tracked by <see cref="UIManager"/>. Just add this component to a UI Button — it auto-hooks the Button's
/// onClick (no manual wiring), and greys itself out when there's nowhere to go back to.
///
/// Notes: live matches are excluded from history (use the match screen's own controls / Resume), and the
/// match/discussion screens aren't recorded as back targets.
/// </summary>
[RequireComponent(typeof(Button))]
public class BackButton : MonoBehaviour
{
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(GoBack);
    }

    private void OnEnable()
    {
        // Pages show/hide by toggling their content root, so this fires whenever the owning page appears —
        // a good moment to reflect whether there's history to go back to.
        RefreshInteractable();
    }

    /// <summary>Go back one page. Also exposed so you can hook it manually if you'd rather not auto-wire.</summary>
    public void GoBack()
    {
        UIManager.Instance?.Back();
        RefreshInteractable();
    }

    private void RefreshInteractable()
    {
        if (button != null)
            button.interactable = UIManager.Instance != null && UIManager.Instance.CanGoBack;
    }
}
