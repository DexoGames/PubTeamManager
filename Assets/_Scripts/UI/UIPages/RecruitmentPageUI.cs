using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RecruitmentPageUI : UIPage
{
    public static RecruitmentPageUI Instance { get; private set; }

    [SerializeField] TextMeshProUGUI headerText, candidateInfoText, questionsRemainingText;
    [SerializeField] Button interviewButton, hireButton, rejectButton, skipButton;
    [SerializeField] TMP_Dropdown questionMenu;
    [SerializeField] TextMeshProUGUI interviewLabel;
    [SerializeField] PlayerStat[] statQuestions;

    readonly List<InterviewQuestion> questions = new List<InterviewQuestion>();

    RecruitmentManager Recruitment => RecruitmentManager.Instance;
    InterviewManager Interview => InterviewManager.Instance;

    void Awake()
    {
        Instance = this;
    }

    protected override void OnShow()
    {
        Refresh();
    }

    void Refresh(string result = null)
    {
        Player candidate = Recruitment.GetCurrentCandidate();
        bool interviewing = candidate != null && Interview.GetInterviewee() == candidate;
        bool canStart = candidate != null || Recruitment.CanInterviewToday;

        headerText.text = candidate == null ? "Recruitment" : $"Candidate {Recruitment.ReviewedBeforeLoad + Recruitment.CurrentCandidateIndex + 1} of {Recruitment.ReviewedBeforeLoad + Recruitment.CurrentCandidates.Count}";
        candidateInfoText.text = result ?? (candidate != null ? candidate.FullName : SessionStatus());
        candidateInfoText.gameObject.SetActive(!interviewing);
        Interview.DialoguePanel.SetActive(interviewing);

        questionsRemainingText.gameObject.SetActive(interviewing);
        questionMenu.gameObject.SetActive(interviewing && Interview.QuestionsRemaining > 0);
        if (interviewing) RefreshQuestions();

        interviewButton.gameObject.SetActive(canStart && (!interviewing || Interview.QuestionsRemaining > 0));
        interviewLabel.text = interviewing ? "Ask" : "Interview";
        hireButton.gameObject.SetActive(interviewing);
        rejectButton.gameObject.SetActive(interviewing);
        skipButton.gameObject.SetActive(candidate != null && !interviewing);
    }

    string SessionStatus()
    {
        if (Recruitment.CanInterviewToday) return "Five candidates available · One signing";
        if (Recruitment.IsInterviewDay) return "Interviews finished for today.";

        var date = ScheduleManager.Instance.GetNextDay(ScheduleEntryType.Interview);
        return date.HasValue ? $"Next interviews · {date.Value:ddd d MMM}" : "No interviews scheduled.";
    }

    void RefreshQuestions()
    {
        questions.Clear();
        questions.AddRange(InterviewManager.GetGeneralQuestions());
        questions.AddRange(InterviewManager.GetPersonalityQuestions());
        questions.Add(new InterviewQuestion(InterviewQuestionType.CompareToPlayer));
        questions.AddRange(statQuestions.Length == 0 ? InterviewManager.GetStatQuestions()
            : statQuestions.Select(stat => new InterviewQuestion(InterviewQuestionType.AskAboutStat, stat)));
        questions.RemoveAll(Interview.HasAsked);

        questionMenu.ClearOptions();
        questionMenu.AddOptions(questions.Select(q => q.QuestionText).ToList());
        questionMenu.SetValueWithoutNotify(0);
        string clue = Interview.NarrowedPersonalities.Count < 10 ? "\n" + string.Join(" / ", Interview.NarrowedPersonalities) : "";
        questionsRemainingText.text = $"{Interview.QuestionsRemaining} questions left{clue}";
    }

    public void OnInterviewClicked()
    {
        Player candidate = Recruitment.GetCurrentCandidate();
        if (candidate == null)
        {
            if (!Recruitment.CanInterviewToday) return;
            Recruitment.StartInterviewSession();
            candidate = Recruitment.GetCurrentCandidate();
            if (candidate == null) { Refresh(); return; }
        }

        if (Interview.GetInterviewee() != candidate)
        {
            Interview.StartInterview(candidate);
        }
        else
        {
            InterviewQuestion question = questions[questionMenu.value];
            if (question.Type == InterviewQuestionType.CompareToPlayer)
            {
                Interview.AskComparison();
                return;
            }
            Interview.AskQuestion(question);
        }

        OnQuestionAsked();
    }

    public void OnQuestionAsked()
    {
        Refresh();
        SaveManager.Instance.SaveCore();
    }

    public void OnHireClicked()
    {
        Player candidate = Recruitment.GetCurrentCandidate();
        if (candidate == null || candidate != Interview.GetInterviewee()) return;
        if (Recruitment.IsSquadFull)
        {
            PlayerPickerPopup.Show(TeamManager.Instance.MyTeam.Players, outgoing => Sign(candidate, outgoing),
                $"Release a player to sign {candidate.Surname}", player => player.AvailabilityStatus, extraColumn: PlayerPickerColumnValue.FromMorale);
            return;
        }
        Sign(candidate, null);
    }

    void Sign(Player candidate, Player outgoing)
    {
        if (candidate != Interview.GetInterviewee() || !Interview.HireInterviewee(outgoing)) return;
        Refresh($"{candidate.FullName} signed.");
        SaveManager.Instance.SaveCore();
    }

    public void OnRejectClicked()
    {
        Interview.RejectInterviewee();
        OnQuestionAsked();
    }

    public void OnSkipClicked()
    {
        Interview.EndInterview();
        Recruitment.SkipCandidate();
        OnQuestionAsked();
    }
}
