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
        if (_event.affected.Count > 0 && _event.affected[0] != person)
        {
            if (_event.affected.Contains(person))
            {
                _event.affected.Remove(person);
                _event.affected.Insert(0, person);
            }
        }

        this.person = person;
        playerEvent = _event;
        date.text = CalenderManager.Instance.DaysAgo(playerEvent.date);
        description.text = Event.ReadDescription(playerEvent.type.description, playerEvent.affected, playerEvent.customWords);
        moraleChange.color = Game.Gradient(Person.MoraleColors(), (50+playerEvent.type.moodChange*2)/100f);
    }

    public void Link()
    {
        linkHandler.HandleLinkClick(LinkBuilder.BuildRawLink(playerEvent, person));
    }
}