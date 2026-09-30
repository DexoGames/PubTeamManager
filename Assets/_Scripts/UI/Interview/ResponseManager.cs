using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Spawns a row of <see cref="Event.Response"/> buttons and reports which tone the manager picked. Shared by every
/// screen that asks for a response — the 1-on-1 discussion page and the half-time team talk both drive this same
/// component, so there's one response row to style, tune and maintain.
///
/// Drop it on the container object, assign the button prefab + container, and either hook
/// <see cref="OnResponseChosen"/> in the Inspector or subscribe to it in code.
/// </summary>
public class ResponseManager : MonoBehaviour
{
    [Serializable] public class ResponseEvent : UnityEvent<Event.Response> { }

    [SerializeField] ResponseButtonUI responseButtonPrefab;
    [SerializeField] Transform container;
    [SerializeField] TextMeshProUGUI dialogue;
    [Tooltip("Which responses to offer, in display order. Leave EMPTY to offer all of them in enum order.")]
    [SerializeField] Event.Response[] responses;
    [Tooltip("Lock the whole row once one is picked — one response per discussion / team talk.")]
    [SerializeField] bool lockAfterPick = true;

    public ResponseEvent OnResponseChosen = new();

    readonly List<ResponseButtonUI> responseButtons = new();

    /// <summary>True once a response has been picked since the last <see cref="SpawnButtons"/>.</summary>
    public bool HasPicked { get; private set; }

    /// <summary>(Re)builds the row — call whenever the owning screen opens.</summary>
    public void SpawnButtons()
    {
        dialogue.text = "";
        HasPicked = false;

        foreach (ResponseButtonUI r in responseButtons) Destroy(r.gameObject);
        responseButtons.Clear();

        foreach (Event.Response response in ResponsesToShow())
        {
            ResponseButtonUI button = Instantiate(responseButtonPrefab, container);
            button.Setup(response, this);
            responseButtons.Add(button);
        }
    }

    IEnumerable<Event.Response> ResponsesToShow()
    {
        if (responses.Length > 0) return responses;

        var all = new Event.Response[Game.GetEnumLength<Event.Response>()];
        for (int i = 0; i < all.Length; i++) all[i] = (Event.Response)i;
        return all;
    }

    /// <summary>Called by a <see cref="ResponseButtonUI"/> when it's clicked.</summary>
    public void ButtonPressed(Event.Response response)
    {
        if (HasPicked && lockAfterPick) return;
        HasPicked = true;

        if (lockAfterPick)
            foreach (ResponseButtonUI r in responseButtons) r.Lock(response);

        dialogue.text = ResponseToDialogue(response);
        OnResponseChosen.Invoke(response);
    }

    /// <summary>
    /// Enables/disables the whole row without marking a pick — for a screen that's read-only right now
    /// (e.g. reopening a team talk you've already delivered).
    /// </summary>
    public void SetInteractable(bool value)
    {
        foreach (ResponseButtonUI r in responseButtons) r.SetInteractable(value);
    }

    public static string ResponseToDialogue(Event.Response response)
    {
        switch (response)
        {
            case Event.Response.Praise:
                return "You're great!";
            case Event.Response.Inspire:
                return "Just imagine the possibilites!";
            case Event.Response.Galvanise:
                return "Come on, you can do better!";
            case Event.Response.Challenge:
                return "You're not doing enough.";
            case Event.Response.Deflect:
                return "Not my fault.";
            case Event.Response.Encourage:
                return "You can do this!";
            case Event.Response.Persuade:
                return "I'm right you gotta believe me.";
            case Event.Response.Rage:
                return "YOU'RE USELESS, GET IT TOGETHER";
        }
        return "Okay.";
    }
}
