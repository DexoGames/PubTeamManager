using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages interview dialogues with players (hiring, contract renewal, etc.)
/// Uses DialogueUI for display but has its own interview-specific logic.
/// </summary>
public class InterviewManager : MonoBehaviour
{
    [SerializeField] DialogueUI dialogue;
    
    Person interviewee;
    InterviewContext interviewContext;

    public static InterviewManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    /// <summary>
    /// Start an interview with a person using a specific interview type.
    /// </summary>
    public void StartInterview(Person person, InterviewContext.InterviewType type)
    {
        interviewee = person;
        interviewContext = new InterviewContext(person, type);
        dialogue.Setup(interviewContext);
    }

    /// <summary>
    /// Start an interview with custom description and dialogue.
    /// </summary>
    public void StartInterview(Person person, string description, string initialDialogue)
    {
        interviewee = person;
        interviewContext = new InterviewContext(person, description, initialDialogue);
        dialogue.Setup(interviewContext);
    }

    /// <summary>
    /// Update the dialogue based on interview progress.
    /// </summary>
    public void UpdateDialogue(string newDialogue)
    {
        dialogue.UpdateDialogue(newDialogue);
    }

    /// <summary>
    /// Show feedback about the interview (e.g., impression made).
    /// </summary>
    public void ShowFeedback(string feedback)
    {
        dialogue.UpdateExtraInfo(feedback);
    }

    /// <summary>
    /// Get the current interviewee.
    /// </summary>
    public Person GetInterviewee() => interviewee;

    /// <summary>
    /// Get the current interview context.
    /// </summary>
    public InterviewContext GetContext() => interviewContext;
}
