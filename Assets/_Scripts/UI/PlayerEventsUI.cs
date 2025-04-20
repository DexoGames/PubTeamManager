using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerEventsUI : MonoBehaviour
{
    [SerializeField] PlayerEventUI playerEventPrfab;
    [SerializeField] Transform container;

    public void ShowEvents(Player player)
    {
        Game.ClearContainer(container);

        List<Event> playerEvents = new List<Event>();
        foreach(Event e in EventsManager.Instance.Events)
        {
            if (e.affected.Contains(player))
            {
                playerEvents.Add(e);
            }
        }

        foreach(Event e in playerEvents)
        {
            PlayerEventUI playerEvent = Instantiate(playerEventPrfab, container);
            playerEvent.Setup(e, player);
        }
    }
}
