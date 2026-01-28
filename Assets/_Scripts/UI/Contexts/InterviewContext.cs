using UnityEngine;

/// <summary>
/// Dialogue context for player interviews (e.g., hiring, contract discussions).
/// </summary>
public class InterviewContext : IDialogueContext
{
    public Person Person { get; private set; }
    public InterviewType Type { get; private set; }
    
    private string customDescription;
    private string customInitialDialogue;

    public string PersonName => Person is Player player 
        ? LinkBuilder.BuildLink(player) 
        : Person.FullName;

    public string Description => customDescription;
    public string InitialDialogue => customInitialDialogue;

    public Color FaceColor => Person.GetMoraleColor();
    public Sprite FaceSprite => Person.GetMoraleSprite();

    public enum InterviewType
    {
        Hiring,
        ContractRenewal,
        PerformanceReview,
        Exit
    }

    public InterviewContext(Person person, InterviewType type)
    {
        Person = person;
        Type = type;
        SetupDialogueForType();
    }

    public InterviewContext(Person person, string description, string initialDialogue)
    {
        Person = person;
        Type = InterviewType.Hiring; // Default
        customDescription = description;
        customInitialDialogue = initialDialogue;
    }

    private void SetupDialogueForType()
    {
        switch (Type)
        {
            case InterviewType.Hiring:
                customDescription = $"Interview with {Person.FullName}";
                customInitialDialogue = "Hello! I'm excited to be here for this interview.";
                break;
            case InterviewType.ContractRenewal:
                customDescription = $"Contract Discussion with {Person.FullName}";
                customInitialDialogue = "Boss, you wanted to talk about my contract?";
                break;
            case InterviewType.PerformanceReview:
                customDescription = $"Performance Review with {Person.FullName}";
                customInitialDialogue = "So, how have I been doing?";
                break;
            case InterviewType.Exit:
                customDescription = $"Exit Interview with {Person.FullName}";
                customInitialDialogue = "It's been a journey... what did you want to discuss?";
                break;
            default:
                customDescription = $"Meeting with {Person.FullName}";
                customInitialDialogue = "Hey boss, what's up?";
                break;
        }
    }
}
