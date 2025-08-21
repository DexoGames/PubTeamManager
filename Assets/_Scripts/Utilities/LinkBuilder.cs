using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class LinkBuilder
{
    public static string BuildLink(Team team)
    {
        return $"<link=\"team/{team.TeamId}\"><u>{team.Name}</u></link>";
    }
    public static string BuildLink(Player player)
    {
        return $"<link=\"player/{player.PersonID}\"><u>{player.FullName}</u></link>";
    }
    public static string BuildLink(Player player, string customName)
    {
        return $"<link=\"player/{player.PersonID}\"><u>{customName}</u></link>";
    }
    public static string BuildLink(Manager manager)
    {
        return $"<link=\"manager/{manager.PersonID}\"><u>{manager.FullName}</u></link>";
    }
    public static string BuildRawLink(Event @event, Person person)
    {
        int index = EventsManager.Instance.Events.IndexOf(@event);
        return $"event/{index}-{person.PersonID}";
    }
    public static string BuildTacticsLink(Team team)
    {
        return $"<link=\"tactics/{team.TeamId}\"><u>{team.Name}</u></link>";
    }
}
