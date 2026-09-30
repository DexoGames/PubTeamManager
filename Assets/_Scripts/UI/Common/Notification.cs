using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(LinkHandler))]
public class Notification : MonoBehaviour
{
    Event playerEvent;
    Person person;

    [SerializeField] TextMeshProUGUI title, contents;

    public void Setup(Event _event, Person _person)
    {
        playerEvent = _event;
        person = _person;

        title.text = _event.type.severity.ToString().ToUpper();
        contents.text = Event.ReadDescription(_event.type.description, _event.affected, _event.customWords);

        Destroy(gameObject, 3);
    }

    public void Link()
    {
        GetComponent<LinkHandler>().HandleLinkClick(LinkBuilder.BuildRawLink(playerEvent, person));
        Destroy(gameObject);
    }
}