using UnityEngine;

/// <summary>
/// Dialogue context for event-based discussions with players.
/// </summary>
public class DiscussionContext : IDialogueContext
{
    public Person Person { get; private set; }
    public Event Event { get; private set; }

    public string PersonName => LinkBuilder.BuildLink((Player)Person);
    
    public string Description => Event.ReadDescription(
        Event.type.description, 
        Event.affected, 
        Event.customWords
    );

    public string InitialDialogue
    {
        get
        {
            if (Event.type.discussion.Length <= 1)
            {
                return "Hey boss, did you want something from me?";
            }
            return Event.ReadDescription(Event.type.discussion, Event.affected, Event.customWords);
        }
    }

    public Color FaceColor => Person.GetMoraleColor();
    public Sprite FaceSprite => Person.GetMoraleSprite();

    public DiscussionContext(Event @event, Person person)
    {
        Event = @event;
        Person = person;
    }
}
