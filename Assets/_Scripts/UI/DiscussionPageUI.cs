using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DiscussionPageUI : UIPage
{
    [SerializeField] DialogueUI dialogue;
    [SerializeField] ResponseManager responseManager;

    Person person;
    Event thisEvent;

    public static DiscussionPageUI Instance { get; private set; }

    public void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
        }
    }


    protected override void OnShow(Event @event, Person person)
    {
        base.OnShow(@event, person);

        thisEvent = @event;
        this.person = person;

        dialogue.Setup(@event, person);
        responseManager.SpawnButtons();
    }

    public void Response(Event.Response response)
    {
        Event.Reaction reaction = EventsManager.Instance.ReactionTable[(response, person.Personality)];
        reaction = Event.ReactionSeverityChange(response, reaction, thisEvent.type.severity);

        int moraleChange = person.NewMorale(thisEvent.type.moraleChange, reaction, thisEvent.type.severity);
        dialogue.UpdatePerson(ReactionToDialogue(reaction));
        responseManager.MakeDialogue(response);

        EventsManager.Instance.Events.Remove(thisEvent);
    }

    public static string ReactionToDialogue(Event.Reaction reaction)
    {
        switch (reaction)
        {
            case Event.Reaction.Terrible:
                return "Boss, I don't know what to say. Is there something wrong with you?!?";
            case Event.Reaction.Bad:
                return "Boss, what are you on about?!";
            case Event.Reaction.Poor:
                return "Ummm, okay??";
            case Event.Reaction.Neutral:
                return "Sure boss.";
            case Event.Reaction.Good:
                return "Cheers boss.";
            case Event.Reaction.Great:
                return "Thanks boss!";
            case Event.Reaction.Amazing:
                return "Boss, you are absolutely correct!!";
        }
        return "Okay.";
    }
}