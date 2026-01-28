using UnityEngine;

/// <summary>
/// Interface for providing dialogue context to DialogueUI.
/// Implement this for different dialogue scenarios (discussions, interviews, etc.)
/// </summary>
public interface IDialogueContext
{
    Person Person { get; }
    string PersonName { get; }
    string Description { get; }
    string InitialDialogue { get; }
    Color FaceColor { get; }
    Sprite FaceSprite { get; }
}
