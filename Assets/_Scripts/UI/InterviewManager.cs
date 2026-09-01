using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>Personalities still consistent with the clue answers given so far (narrows as you probe).</summary>
    public List<Person.PersonalityType> NarrowedPersonalities { get; private set; } = new List<Person.PersonalityType>();

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

    public void StartInterview(Player player)
    {
        interviewee = player;
        askedQuestions.Clear();

        // Start with every personality possible; clue questions narrow this down.
        NarrowedPersonalities = new List<Person.PersonalityType>(
            (Person.PersonalityType[])Enum.GetValues(typeof(Person.PersonalityType)));

        interviewContext = new InterviewContext(player);
        dialogue.Setup(interviewContext);
    }

    public InterviewAnswer AskAboutStat(PlayerStat stat)
    {
        var question = new InterviewQuestion(InterviewQuestionType.AskAboutStat, stat);
        return AskQuestion(question);
    }

    public InterviewAnswer AskQuestion(InterviewQuestionType questionType)
    {
        var question = new InterviewQuestion(questionType);
        return AskQuestion(question);
    }

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

        // Personality-probing questions narrow the candidate set (intersection of clue groups).
        if (answer != null && answer.PossiblePersonalities != null)
        {
            NarrowedPersonalities = NarrowedPersonalities
                .Where(p => Array.IndexOf(answer.PossiblePersonalities, p) >= 0)
                .ToList();
        }

        dialogue.UpdateDialogue(answer.ResponseText);

        return answer;
    }

    /// <summary>
    /// Comparison question: opens the shared player picker (any squad member); once one is chosen it generates the
    /// comparison answer, narrows the personality, and consumes a question. Cancelling the picker costs nothing.
    /// </summary>
    public void AskComparison()
    {
        if (interviewee == null)
        {
            Debug.LogWarning("No interviewee set. Call StartInterview first.");
            return;
        }
        if (askedQuestions.Count >= MAX_QUESTIONS)
        {
            Debug.Log("[Interview] Maximum questions reached (5). Make your decision!");
            return;
        }

        Team myTeam = TeamManager.Instance.MyTeam;
        if (myTeam.Players.Count == 0) return;

        PlayerPickerPopup.Show(
            myTeam.Players,
            picked =>
            {
                if (picked == null) return;

                askedQuestions.Add(new InterviewQuestion(InterviewQuestionType.CompareToPlayer));
                InterviewAnswer answer = InterviewAnswerGenerator.GenerateComparisonAnswer(interviewee, picked);

                if (answer.PossiblePersonalities != null)
                    NarrowedPersonalities = NarrowedPersonalities
                        .Where(p => Array.IndexOf(answer.PossiblePersonalities, p) >= 0)
                        .ToList();

                dialogue.UpdateDialogue(answer.ResponseText);
                RecruitmentPageUI.Instance?.OnQuestionAsked();
            },
            $"Compare {interviewee.FullName} to…");
    }

    /// <summary>Human-readable summary of how far the personality has been narrowed down.</summary>
    public string NarrowedPersonalitiesText()
    {
        if (NarrowedPersonalities == null || NarrowedPersonalities.Count == 0) return "Unknown";
        if (NarrowedPersonalities.Count >= Game.GetEnumLength<Person.PersonalityType>())
            return "Unknown — ask some personality questions";
        return string.Join(" / ", NarrowedPersonalities);
    }

    public int QuestionsRemaining => MAX_QUESTIONS - askedQuestions.Count;

    /// <summary>Hire the current interviewee — delegates to RecruitmentManager.</summary>
    public bool HireInterviewee()
    {
        if (interviewee == null) return false;
        bool result = RecruitmentManager.Instance.HirePlayer(interviewee);
        if (result) EndInterview();
        return result;
    }

    /// <summary>Reject the current interviewee — permanently removed.</summary>
    public void RejectInterviewee()
    {
        if (interviewee == null) return;
        RecruitmentManager.Instance.RejectPlayer(interviewee);
        EndInterview();
    }

    public static List<InterviewQuestion> GetStatQuestions()
    {
        var questions = new List<InterviewQuestion>();
        foreach (PlayerStat stat in Player.SkillDisplayOrder)
            questions.Add(new InterviewQuestion(InterviewQuestionType.AskAboutStat, stat));
        return questions;
    }

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
    /// Personality-probing questions whose answers narrow down the hidden personality type.
    /// </summary>
    public static List<InterviewQuestion> GetPersonalityQuestions()
    {
        return new List<InterviewQuestion>
        {
            new InterviewQuestion(InterviewQuestionType.HandleCriticism),
            new InterviewQuestion(InterviewQuestionType.WorkEthic),
            new InterviewQuestion(InterviewQuestionType.BigGameMentality),
            new InterviewQuestion(InterviewQuestionType.Leadership)
        };
    }

    public void ShowFeedback(string feedback)
    {
        dialogue.UpdateExtraInfo(feedback);
    }

    public Player GetInterviewee() => interviewee;

    public InterviewContext GetContext() => interviewContext;

    public List<InterviewQuestion> GetAskedQuestions() => askedQuestions;

    public void EndInterview()
    {
        interviewee = null;
        interviewContext = null;
        askedQuestions.Clear();
    }
}
