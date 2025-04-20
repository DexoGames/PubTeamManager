using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ResponseManager : MonoBehaviour
{
    [SerializeField] DiscussionPageUI discussionPage;
    [SerializeField] ResponseButtonUI responseButtonPrefab;
    [SerializeField] Transform container;
    [SerializeField] TextMeshProUGUI dialogue;

    List<ResponseButtonUI> responseButtons = new List<ResponseButtonUI>();

    public void SpawnButtons()
    {
        dialogue.text = "";

        foreach(ResponseButtonUI r in responseButtons)
        {
            Destroy(r.gameObject);
        }
        responseButtons.Clear();

        for(int i = 0; i < Game.GetEnumLength<Event.Response>(); i++)
        {
            ResponseButtonUI response = Instantiate(responseButtonPrefab, container);
            response.Setup((Event.Response)i, this);
            responseButtons.Add(response);
        }
    }

    public void ButtonPressed(Event.Response response)
    {
        foreach(ResponseButtonUI r in responseButtons)
        {
            r.MakeUninteractable(response);
        }
        discussionPage.Response(response);
    }

    public void MakeDialogue(Event.Response response)
    {
        dialogue.text = ResponseToDialogue(response);
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
