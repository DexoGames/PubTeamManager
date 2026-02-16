using UnityEngine;

/// <summary>
/// Dialogue context for hiring interviews with potential players.
/// </summary>
public class InterviewContext : IDialogueContext
{
    public Person Person { get; private set; }
    public Player Player => Person as Player;

    public string PersonName => Person is Player player 
        ? LinkBuilder.BuildLink(player) 
        : Person.FullName;

    public string Description => $"Interview with {Person.FullName}";
    
    public string InitialDialogue => GetInitialDialogue();

    public Color FaceColor => Person.GetMoraleColor();
    public Sprite FaceSprite => Person.GetMoraleSprite();

    public InterviewContext(Player player)
    {
        Person = player;
    }

    private string GetInitialDialogue()
    {
        if (Player == null) return "Hello!";

        return Player.Personality switch
        {
            Person.PersonalityType.Cocky => "Thanks for having me. You won't regret bringing me in.",
            Person.PersonalityType.Shy => "H-hello... thank you for this opportunity.",
            Person.PersonalityType.Aggressive => "Let's get to it. I'm ready to prove myself.",
            Person.PersonalityType.Kind => "Hi! It's so nice to meet you. Thank you for considering me!",
            Person.PersonalityType.Lazy => "Hey. So, what do you want to know?",
            Person.PersonalityType.Driven => "I appreciate the opportunity. I'm eager to show you what I can bring to this team.",
            Person.PersonalityType.Smart => "Good to meet you. I've done my research on the club - impressive setup.",
            Person.PersonalityType.Silly => "Hey hey! Excited to be here! When do I get my locker?",
            Person.PersonalityType.Cautious => "Thank you for meeting with me. I hope we can find a good fit.",
            Person.PersonalityType.Calm => "Hello. I appreciate you taking the time to speak with me.",
            _ => "Hello! I'm excited to be here for this interview."
        };
    }
}
