using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[RequireComponent(typeof(Toggle))]
public class TacticsToggle : MonoBehaviour
{
    public TacticInstruction instruction { get; private set; }
    public Toggle toggle { get; private set; }
    private TextMeshProUGUI text;

    public readonly UnityEvent OnToggleChange = new UnityEvent();

    [Header("UI Style")]
    public Color selectedColor = Color.white;
    public Color unselectedColor = Color.grey;

    void Awake()
    {
        toggle = GetComponent<Toggle>();
        text = GetComponentInChildren<TextMeshProUGUI>();

        // Subscribe to the event once
        toggle.onValueChanged.AddListener(HandleToggleValueChanged);
    }

    // Called by TacticGridLayout after instantiating.
    public void Create(TacticInstruction newInstruction)
    {
        instruction = newInstruction;
        if (text != null)
        {
            text.text = instruction != null ? instruction.tacticName : "Unnamed";
        }
    }

    // Called when the toggle's value changes (either by user or code).
    private void HandleToggleValueChanged(bool isOn)
    {
        UpdateColor(isOn);
        OnToggleChange.Invoke(); // Notify listeners like TacticGridLayout
    }

    public void Set(bool newState)
    {
        // Setting toggle.isOn will trigger onValueChanged, so no need to call HandleToggleValueChanged manually.
        toggle.isOn = newState;
    }

    private void UpdateColor(bool isOn)
    {
        if (toggle.targetGraphic != null)
        {
            toggle.targetGraphic.color = isOn ? selectedColor : unselectedColor;
        }
    }

    public void SetInteractable(bool isInteractable)
    {
        toggle.interactable = isInteractable;
        float alpha = isInteractable ? 1.0f : 0.5f;
        if (text != null)
        {
            text.alpha = alpha;
        }
    }

    // Good Practice: Clean up listeners when the object is destroyed.
    void OnDestroy()
    {
        OnToggleChange.RemoveAllListeners();
        if (toggle != null)
        {
            toggle.onValueChanged.RemoveAllListeners();
        }
    }
}