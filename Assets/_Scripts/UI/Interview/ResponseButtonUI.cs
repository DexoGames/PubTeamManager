using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One tone button in a <see cref="ResponseManager"/> row. Spawned by the manager, which calls
/// <see cref="Setup"/> to bind it to a response; the prefab's Button OnClick is hooked to <see cref="OnClick"/>.
/// The label and Button are found on the prefab itself, so any layout works.
/// </summary>
public class ResponseButtonUI : MonoBehaviour
{
    [Tooltip("Tint applied to the response that was actually picked once the row locks.")]
    [SerializeField] Color pickedTint = Color.gray;

    Event.Response response;
    ResponseManager manager;
    TextMeshProUGUI text;
    Button button;
    Color baseColour;

    public Event.Response Response => response;

    public void Setup(Event.Response response, ResponseManager manager, string label = null)
    {
        text = GetComponentInChildren<TextMeshProUGUI>();
        button = GetComponent<Button>();

        this.response = response;
        this.manager = manager;

        text.text = label ?? response.ToString();
        baseColour = button.image.color;
        button.interactable = true;
    }

    /// <summary>Hooked to the Button's OnClick in the prefab — don't add a second listener in code.</summary>
    public void OnClick() => manager.ButtonPressed(response);

    /// <summary>Locks the button after the row has been used, tinting the one that was actually picked.</summary>
    public void Lock(Event.Response picked)
    {
        button.interactable = false;
        if (picked == response) button.image.color = pickedTint;
    }

    /// <summary>Enable/disable without changing the tint (the row isn't necessarily spent).</summary>
    public void SetInteractable(bool value)
    {
        button.interactable = value;
        button.image.color = baseColour;
    }
}
