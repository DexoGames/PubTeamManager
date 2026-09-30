using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;

public class Event
{
    /// <summary>EventType SO — serialized by name, resolved from Resources.</summary>
    [JsonIgnore] public EventType type;

    /// <summary>Name of the EventType ScriptableObject for serialization.</summary>
    [JsonProperty] public string EventTypeName
    {
        get => type != null ? type.name : "";
        set => _eventTypeName = value;
    }
    private string _eventTypeName;

    /// <summary>Affected persons — serialized as PersonIDs.</summary>
    [JsonIgnore] public List<Person> affected = new List<Person>();

    /// <summary>PersonIDs for serialization.</summary>
    [JsonProperty] public List<int> AffectedPersonIds
    {
        get => affected?.Select(p => p.PersonID).ToList() ?? new List<int>();
        set => _affectedIds = value;
    }
    private List<int> _affectedIds;

    public DateTime date;
    public List<string> customWords;

    public Event() { }

    public Event(EventType type, List<Person> affected, DateTime date, List<string> customWords = null)
    {
        this.type = type;
        this.affected = affected;
        this.date = date;
        if (customWords != null) this.customWords = customWords;
    }

    /// <summary>
    /// Called after deserialization to resolve EventType and Person references.
    /// </summary>
    public void OnAfterDeserialize()
    {
        // Resolve EventType from Resources
        if (!string.IsNullOrEmpty(_eventTypeName) && type == null)
        {
            var allEvents = Resources.LoadAll<EventType>("Events");
            type = Array.Find(allEvents, e => e.name == _eventTypeName);
            if (type == null)
                Debug.LogWarning($"[Event] Could not find EventType '{_eventTypeName}'");
        }

        // Resolve affected persons from IDs
        if (_affectedIds != null && (affected == null || affected.Count == 0))
        {
            affected = new List<Person>();
            foreach (int id in _affectedIds)
            {
                Person p = PersonManager.Instance.GetPerson(id);
                if (p != null) affected.Add(p);
            }
        }
    }

    public static string ReadDescription(string desc, List<Person> affected, List<string> customWords)
    {
        // Replace <all> first
        desc = Regex.Replace(desc, @"<all>", match =>
        {
            return affected.Count > 0 ? string.Join(", ", affected.ConvertAll(p => p.FullName)) : match.Value;
        });

        // Replace numbered tags like <1>, <2>, etc.
        desc = Regex.Replace(desc, @"<(\d+)>", match =>
        {
            int index = int.Parse(match.Groups[1].Value) - 1;
            return (index >= 0 && index < affected.Count) ? affected[index].FullName : match.Value;
        });

        // Replace custom tags like <w1>, <w2>, etc.
        desc = Regex.Replace(desc, @"<w(\d+)>", match =>
        {
            int index = int.Parse(match.Groups[1].Value) - 1;
            return (index >= 0 && index < customWords.Count) ? customWords[index] : match.Value;
        });

        return desc;
    }

    public enum Response
    {
        Praise, Encourage, Challenge, Persuade, Inspire, Galvanise, Rage, Deflect
    }

    public enum Reaction
    {
        Terrible, Bad, Poor, Neutral, Good, Great, Amazing
    }

    public static Reaction ReactionSeverityChange(Response response, Reaction reactionEnum, EventType.Severity severityEnum)
    {
        int severity = (int)severityEnum - (int)EventType.Severity.Irrelevant;
        int reaction = (int)reactionEnum;

        bool Check(Response r) { return response == r; }

        if(Mathf.Abs(severity) <= 1)
        {
            if (Check(Response.Rage) || Check(Response.Challenge) || Check(Response.Inspire))
            {
                reaction = Mathf.Abs(severity)+1;
            }
            else
            {
                reaction = reaction - 1 * (int)Mathf.Sign(reaction);
            }
        }
        else
        {
            if (Check(Response.Deflect))
            {
                reaction -= 1;
            }

            if (severity < 0)
            {
                if (Check(Response.Praise))
                {
                    reaction = reaction + severity + 1;
                }
            }
            if (severity > 0)
            {
                if (Check(Response.Challenge) || Check(Response.Persuade))
                {
                    reaction = -2;
                }
            }
        }

        reaction = Mathf.Clamp(reaction, 0, Game.GetEnumLength<Reaction>());

        return (Reaction)reaction;
    }
}
