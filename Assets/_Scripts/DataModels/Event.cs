using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public class Event
{
    public EventType type;
    public List<Person> affected;
    public DateTime date;
    public List<string> customWords;

    public Event(EventType type, List<Person> affected, DateTime date, List<string> customWords = null)
    {
        this.type = type;
        this.affected = affected;
        this.date = date;
        if (customWords != null) this.customWords = customWords;
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
