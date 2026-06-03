using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages hiring interviews with potential players.
/// Uses DialogueUI for display and InterviewQuestion system for Q&A logic.
/// </summary>
public class InterviewManager : MonoBehaviour
{
    [SerializeField] DialogueUI dialogue;
    
    Player interviewee;
    InterviewContext interviewContext;
    List<InterviewQuestion> askedQuestions = new List<InterviewQuestion>();
    const int MAX_QUESTIONS = 5;

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
    /// Start a hiring interview with a potential player.
    /// </summary>
    public void StartInterview(Player player)
    {
        interviewee = player;
        askedQuestions.Clear();
        
        interviewContext = new InterviewContext(player);
        dialogue.Setup(interviewContext);
    }

    /// <summary>
    /// Ask about a specific stat (e.g., shooting, passing).
    /// </summary>
    public InterviewAnswer AskAboutStat(PlayerStat stat)
    {
        var question = new InterviewQuestion(InterviewQuestionType.AskAboutStat, stat);
        return AskQuestion(question);
    }

    /// <summary>
    /// Ask a general question.
    /// </summary>
    public InterviewAnswer AskQuestion(InterviewQuestionType questionType)
    {
        var question = new InterviewQuestion(questionType);
        return AskQuestion(question);
    }

    /// <summary>
    /// Ask a specific question and get the answer.
    /// </summary>
    public InterviewAnswer AskQuestion(InterviewQuestion question)
    {
        if (interviewee == null)
        {
            Debug.LogWarning("No interviewee set. Call StartInterview first.");
            return null;
        }

        if (askedQuestions.Count >= MAX_QUESTIONS)
        {
            Debug.Log("[Interview] Maximum questions reached (5). Make your decision!");
            return null;
        }

        askedQuestions.Add(question);
        InterviewAnswer answer = InterviewAnswerGenerator.GenerateAnswer(interviewee, question);
        
        dialogue.UpdateDialogue(answer.ResponseText);
        
        return answer;
    }

    /// <summary>
    /// Returns how many questions remain in this interview.
    /// </summary>
    public int QuestionsRemaining => MAX_QUESTIONS - askedQuestions.Count;

    /// <summary>
    /// Hire the current interviewee — delegates to RecruitmentManager.
    /// </summary>
    public bool HireInterviewee()
    {
        if (interviewee == null) return false;
        bool result = RecruitmentManager.Instance.HirePlayer(interviewee);
        if (result) EndInterview();
        return result;
    }

    /// <summary>
    /// Reject the current interviewee — permanently removed.
    /// </summary>
    public void RejectInterviewee()
    {
        if (interviewee == null) return;
        RecruitmentManager.Instance.RejectPlayer(interviewee);
        EndInterview();
    }

    /// <summary>
    /// Get all available stat questions.
    /// </summary>
    public static List<InterviewQuestion> GetStatQuestions()
    {
        var questions = new List<InterviewQuestion>();
        for (int i = 0; i < Player.SKILL_NO; i++)
        {
            questions.Add(new InterviewQuestion(InterviewQuestionType.AskAboutStat, (PlayerStat)i));
        }
        return questions;
    }

    /// <summary>
    /// Get all general questions.
    /// </summary>
    public static List<InterviewQuestion> GetGeneralQuestions()
    {
        return new List<InterviewQuestion>
        {
            new InterviewQuestion(InterviewQuestionType.BiggestStrength),
            new InterviewQuestion(InterviewQuestionType.BiggestWeakness),
            new InterviewQuestion(InterviewQuestionType.BestPersonalityFit),
            new InterviewQuestion(InterviewQuestionType.WorstPersonalityFit),
            new InterviewQuestion(InterviewQuestionType.PreferredPosition),
            new InterviewQuestion(InterviewQuestionType.CareerGoals)
        };
    }

    /// <summary>
    /// Show feedback/info to the interviewer.
    /// </summary>
    public void ShowFeedback(string feedback)
    {
        dialogue.UpdateExtraInfo(feedback);
    }

    /// <summary>
    /// Get the current interviewee.
    /// </summary>
    public Player GetInterviewee() => interviewee;

    /// <summary>
    /// Get the current interview context.
    /// </summary>
    public InterviewContext GetContext() => interviewContext;

    /// <summary>
    /// Get list of questions asked so far.
    /// </summary>
    public List<InterviewQuestion> GetAskedQuestions() => askedQuestions;

    /// <summary>
    /// End the interview.
    /// </summary>
    public void EndInterview()
    {
        interviewee = null;
        interviewContext = null;
        askedQuestions.Clear();
    }
}
