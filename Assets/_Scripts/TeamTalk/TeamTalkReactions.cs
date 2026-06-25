using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Half-time team talk built on the SAME reaction system as the 1-on-1 player discussions: the manager picks an
/// <see cref="Event.Response"/> (Praise / Rage / Encourage / …) and every player reacts via
/// <c>EventsManager.ReactionTable</c> keyed on their personality, adjusted by a SEVERITY taken from the half-time
/// scoreline (<see cref="Event.ReactionSeverityChange"/>), then <c>Person.NewMorale</c> applies the swing — the
/// identical path a discussion uses, just looped over the whole squad (see <see cref="TeamTalkController.DeliverTalk"/>).
///
/// This class only holds the score→severity mapping and display helpers (labels, flavour lines, colours, summary).
/// </summary>
public static class TeamTalkReactions
{
    /// <summary>
    /// Maps the player team's half-time goal difference to a dressing-room severity. Winning a tight one is
    /// Pleasant, getting hammered is Dire — so the same response lands differently depending on the situation.
    /// (1-up → Pleasant, 3-down → Pressing, per design; tune the thresholds here.)
    /// </summary>
    public static EventType.Severity SeverityFromScore(int goalDifference) =>
        goalDifference >= 4  ? EventType.Severity.Momentous :
        goalDifference >= 2  ? EventType.Severity.Uplifting :
        goalDifference == 1  ? EventType.Severity.Pleasant :
        goalDifference == 0  ? EventType.Severity.Irrelevant :
        goalDifference == -1 ? EventType.Severity.Unfortunate :
        goalDifference >= -3 ? EventType.Severity.Pressing :
                               EventType.Severity.Dire;

    public static string Label(Event.Response r) => r.ToString();

    public static string Flavour(Event.Response r) => r switch
    {
        Event.Response.Praise    => "\"Brilliant out there — keep doing exactly that.\"",
        Event.Response.Encourage => "\"Keep going, lads. I believe in every one of you.\"",
        Event.Response.Challenge => "\"Is that really your best? Go and prove me wrong.\"",
        Event.Response.Persuade  => "\"Here's what I need from you — trust me on this.\"",
        Event.Response.Inspire   => "\"This is our moment. Let's go and take it.\"",
        Event.Response.Galvanise => "\"Come on! Right here, right now — together!\"",
        Event.Response.Rage      => "\"ABSOLUTELY NOT GOOD ENOUGH. Sort it out — NOW!\"",
        Event.Response.Deflect   => "\"Forget the ref, forget the noise. Just play your game.\"",
        _                        => ""
    };

    /// <summary>Short read on the situation, e.g. "1 up — Pleasant".</summary>
    public static string SituationText(int goalDifference, EventType.Severity severity)
    {
        string score = goalDifference > 0 ? $"{goalDifference} up"
                     : goalDifference < 0 ? $"{-goalDifference} down"
                     : "level";
        return $"{score} — {severity}";
    }

    public static string Summarise(IEnumerable<PlayerReaction> reactions)
    {
        var list = reactions?.ToList();
        if (list == null || list.Count == 0) return "";

        float avg = (float)list.Average(r => r.mood + r.passion);
        string verdict =
            avg >= 8 ? "The dressing room is buzzing!" :
            avg >= 2 ? "The lads look lifted." :
            avg > -2 ? "A muted response — no real change." :
            avg > -8 ? "That went down badly — they look flat." :
                       "You've lost the room. They're deflated.";

        int up = list.Count(r => r.mood + r.passion >= 2);
        int down = list.Count(r => r.mood + r.passion <= -2);
        return $"{verdict}  ({up} lifted, {down} unhappy)";
    }

    /// <summary>Colour for a reaction — used to tint the player box and the ±delta text.</summary>
    public static Color ReactionColour(Event.Reaction reaction) => reaction switch
    {
        Event.Reaction.Terrible => new Color(0.80f, 0.25f, 0.25f),
        Event.Reaction.Bad      => new Color(0.85f, 0.40f, 0.35f),
        Event.Reaction.Poor     => new Color(0.88f, 0.60f, 0.35f),
        Event.Reaction.Neutral  => new Color(0.70f, 0.70f, 0.72f),
        Event.Reaction.Good     => new Color(0.60f, 0.78f, 0.45f),
        Event.Reaction.Great    => new Color(0.40f, 0.78f, 0.40f),
        Event.Reaction.Amazing  => new Color(0.28f, 0.82f, 0.34f),
        _                       => Color.white
    };
}

/// <summary>How one player took the team talk (drives the per-box feedback).</summary>
public struct PlayerReaction
{
    public Player player;
    public Event.Reaction reaction;
    public int mood;
    public int passion;
}
