using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerEventsUI : UIObject
{
    [SerializeField] PlayerEventUI playerEventPrfab;
    [SerializeField] Transform container;
    public bool allPlayers;
    bool ignoreBasicEvents;

    public override void Setup()
    {
        if (!allPlayers) return;

        ignoreBasicEvents = true;
        ShowEvents(TeamManager.Instance.MyTeam.Players);
    }

    public void ShowEvents(List<Player> players)
    {
        Game.ClearContainer(container);

        List<Event> playerEvents = new List<Event>();
        foreach(Event e in EventsManager.Instance.Events)
        {
            foreach(Player player in players)
            {
                if (e.affected.Contains(player))
                {
                    if (ignoreBasicEvents && e.type.tag == EventType.Tag.Basic) continue;

                    playerEvents.Add(e);

                    PlayerEventUI playerEvent = Instantiate(playerEventPrfab, container);
                    playerEvent.Setup(e, player);
                }
            }
        }
    }
}
