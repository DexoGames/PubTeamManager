using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Shared baseline for a toggle-style option button on the tactics screen — the common plumbing for the
/// instruction toggles (<see cref="TacticsToggle"/>) so any future toggle variants stay consistent. Handles
/// the Unity <see cref="Toggle"/>, the selected/unselected colour, the change event, and label/interactable
/// helpers.
///
/// Subclasses add the meaning: instructions react externally via <see cref="OnToggleChange"/> (TacticGridLayout
/// adds/removes them and runs the reliance picker); a subclass can also override <see cref="OnToggled"/>.
/// </summary>
[RequireComponent(typeof(Toggle))]
public abstract class TacticOptionToggle : MonoBehaviour
{
    [Header("UI Style")]
    public Color selectedColor = Color.white;
    public Color unselectedColor = Color.grey;

    public Toggle toggle { get; private set; }
    protected TextMeshProUGUI text;

    /// <summary>Fires after any toggle change (used by external controllers like TacticGridLayout).</summary>
    public readonly UnityEvent OnToggleChange = new UnityEvent();

    protected virtual void Awake()
    {
        toggle = GetComponent<Toggle>();
        text = GetComponentInChildren<TextMeshProUGUI>();
        toggle.onValueChanged.AddListener(HandleToggleValueChanged);
    }

    private void HandleToggleValueChanged(bool isOn)
    {
        UpdateColor(isOn);
        OnToggled(isOn);          // subclass hook
        OnToggleChange.Invoke();  // external listeners
    }

    /// <summary>Subclass hook: react to a genuine toggle flip (user click or <see cref="Set"/>). Default: nothing.</summary>
    protected virtual void OnToggled(bool isOn) { }

    /// <summary>Sets the toggle state programmatically, firing the change handler (like a click).</summary>
    public void Set(bool newState) => toggle.isOn = newState;

    /// <summary>Sets the toggle state WITHOUT firing the change handler (for syncing the view to data).</summary>
    public void SetSilent(bool newState)
    {
        toggle.SetIsOnWithoutNotify(newState);
        UpdateColor(newState);
    }

    protected void SetLabel(string value)
    {
        if (text != null) text.text = value;
    }

    protected virtual void UpdateColor(bool isOn)
    {
        if (toggle != null && toggle.targetGraphic != null)
            toggle.targetGraphic.color = isOn ? selectedColor : unselectedColor;
    }

    public void SetInteractable(bool isInteractable)
    {
        toggle.interactable = isInteractable;
        if (text != null) text.alpha = isInteractable ? 1f : 0.5f;
    }

    protected virtual void OnDestroy()
    {
        OnToggleChange.RemoveAllListeners();
        if (toggle != null) toggle.onValueChanged.RemoveAllListeners();
    }
}
