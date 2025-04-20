using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerEventUI : MonoBehaviour
{
    public Event playerEvent;
    [SerializeField] TextMeshProUGUI date, description;
    [SerializeField] Image moraleChange;
    [SerializeField] LinkHandler linkHandler;
    Person person;

    public void Setup(Event _event, Person person)
    {
        this.person = person;
        playerEvent = _event;
        date.text = CalenderManager.Instance.DaysAgo(playerEvent.date);
        description.text = Event.ReadDescription(playerEvent.type.description, playerEvent.affected, playerEvent.customWords);
        moraleChange.color = Person.GetColor(50 + (int)(_event.type.moraleChange * 1.8f));
    }

    public void Link()
    {
        linkHandler.HandleLinkClick(LinkBuilder.BuildRawLink(playerEvent, person));
    }
}