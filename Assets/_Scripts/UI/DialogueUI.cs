using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialogueUI : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI description, personName, contents;
    [SerializeField] Image face;

    Person person;
    Event dialogueEvent;

    public void Setup(Event _event, Person person)
    {
        this.person = person;
        dialogueEvent = _event;

        face.color = person.GetMoraleColor();
        description.text = Event.ReadDescription(dialogueEvent.type.description, dialogueEvent.affected, dialogueEvent.customWords);
        if (_event.type.discussion.Length <= 1)
        {
            contents.text = "Hey boss, did you want something from me?";
        }
        else
        {
            contents.text = Event.ReadDescription(dialogueEvent.type.discussion, dialogueEvent.affected, dialogueEvent.customWords);
        }
        personName.text = LinkBuilder.BuildLink((Player)person);
    }

    public void UpdatePerson(string response)
    {
        contents.text = response;
        face.color = person.GetMoraleColor();
    }
}