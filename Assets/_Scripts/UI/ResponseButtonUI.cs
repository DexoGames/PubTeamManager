using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One tone button in a <see cref="ResponseManager"/> row. Spawned by the manager, which calls
/// <see cref="Setup"/> to bind it to a response; the prefab's Button OnClick is hooked to
/// <see cref="OnClick"/>.
///
/// The two references are found automatically if left empty, so the prefab works with any layout.
/// </summary>
public class ResponseButtonUI : MonoBehaviour
{
    [Tooltip("Optional: the response label. Falls back to the first TMP found in the children.")]
    [SerializeField] TextMeshProUGUI text;
    [Tooltip("Optional: the button. Falls back to the Button on this object.")]
    [SerializeField] Button button;
    [Tooltip("Tint applied to the response that was actually picked once the row locks.")]
    [SerializeField] Color pickedTint = Color.gray;

    Event.Response response;
    ResponseManager manager;
    Color baseColour;

    public Event.Response Response => response;

    public void Setup(Event.Response response, ResponseManager manager)
    {
        if (text == null) text = GetComponentInChildren<TextMeshProUGUI>();
        if (button == null) button = GetComponent<Button>();

        this.response = response;
        this.manager = manager;

        if (text != null) text.text = response.ToString();
        if (button.image != null) baseColour = button.image.color;
        button.interactable = true;
    }

    /// <summary>Hooked to the Button's OnClick in the prefab — don't add a second listener in code.</summary>
    public void OnClick() => manager.ButtonPressed(response);

    /// <summary>Locks the button after the row has been used, tinting the one that was actually picked.</summary>
    public void Lock(Event.Response picked)
    {
        button.interactable = false;
        if (picked == response && button.image != null) button.image.color = pickedTint;
    }

    /// <summary>Enable/disable without changing the tint (the row isn't necessarily spent).</summary>
    public void SetInteractable(bool value)
    {
        button.interactable = value;
        if (button.image != null) button.image.color = baseColour;
    }
}
