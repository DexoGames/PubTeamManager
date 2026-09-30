using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RecruitmentPageUI : UIPage
{
    public static RecruitmentPageUI Instance { get; private set; }

    [SerializeField] private TextMeshProUGUI headerText;
    [SerializeField] private TextMeshProUGUI candidateInfoText;
    [SerializeField] private TextMeshProUGUI questionsRemainingText;
    [SerializeField] private Button interviewButton;
    [SerializeField] private Button hireButton;
    [SerializeField] private Button rejectButton;
    [SerializeField] private Button skipButton;

    [SerializeField] private Transform questionContainer;
    [SerializeField] private InterviewQuestionButton questionButtonPrefab;
    [Tooltip("Which stats may be asked about, in display order. Leave EMPTY to offer every stat.")]
    [SerializeField] private PlayerStat[] statQuestions;

    private List<Player> candidates;
    private int currentIndex = 0;
    private bool interviewStarted = false;

    private readonly List<InterviewQuestionButton> questionButtons = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(this.gameObject);
        else
            Instance = this;
    }

    protected override void OnShow()
    {
        base.OnShow();
        BuildQuestionButtons();
        StartSession();
    }


    private void BuildQuestionButtons()
    {
        Game.ClearContainer(questionContainer);
        questionButtons.Clear();

        foreach (InterviewQuestion question in QuestionsToOffer())
        {
            InterviewQuestionButton button = Instantiate(questionButtonPrefab, questionContainer);
            button.SetQuestion(question);
            questionButtons.Add(button);
        }

        RefreshQuestionButtons();
    }

    private IEnumerable<InterviewQuestion> QuestionsToOffer()
    {
        var questions = new List<InterviewQuestion>();
        questions.AddRange(InterviewManager.GetGeneralQuestions());
        questions.AddRange(InterviewManager.GetPersonalityQuestions());
        questions.Add(new InterviewQuestion(InterviewQuestionType.CompareToPlayer));

        questions.AddRange(statQuestions.Length > 0
            ? statQuestions.Select(s => new InterviewQuestion(InterviewQuestionType.AskAboutStat, s))
            : InterviewManager.GetStatQuestions());

        return questions;
    }

    private void RefreshQuestionButtons()
    {
        foreach (InterviewQuestionButton button in questionButtons)
            button.RefreshInteractable();
    }

    private void StartSession()
    {
        if (!RecruitmentManager.Instance.CanInterviewToday)
        {
            headerText.text = "No Interviews Today";
            candidateInfoText.text = RecruitmentManager.Instance.IsInterviewDay
                ? "You've already held your interviews today. Come back on the next interview day."
                : "Interviews are only held on scheduled interview days." + NextInterviewDaySuffix();
            questionsRemainingText.text = "";
            SetButtonsActive(false);
            return;
        }

        RecruitmentManager.Instance.StartInterviewSession();
        candidates = RecruitmentManager.Instance.CurrentCandidates;
        currentIndex = 0;
        interviewStarted = false;

        headerText.text = $"Interview Day — {candidates.Count} Candidates";

        ShowCurrentCandidate();
    }

    private string NextInterviewDaySuffix()
    {
        System.DateTime? next = ScheduleManager.Instance.GetNextDay(ScheduleEntryType.Interview);
        return next.HasValue ? $"\n\nNext interview day: {next.Value:ddd dd MMM}" : "\n\nCheck your calendar.";
    }

    private void ShowCurrentCandidate()
    {
        if (currentIndex >= candidates.Count)
        {
            candidateInfoText.text = "All candidates reviewed. Session complete.";
            EndInterview();
            SetButtonsActive(false);
            return;
        }

        Player candidate = candidates[currentIndex];
        interviewStarted = false;

        candidateInfoText.text = $"<b>{candidate.FullName}</b>\n" +
            $"Candidate {currentIndex + 1} of {candidates.Count}\n\n" +
            $"Start an interview to learn more about this player.";
        questionsRemainingText.text = "";

        interviewButton.gameObject.SetActive(true);
        hireButton.gameObject.SetActive(false);
        rejectButton.gameObject.SetActive(false);
        skipButton.gameObject.SetActive(true);

        RefreshQuestionButtons();
    }

    public void OnInterviewClicked()
    {
        Player candidate = candidates[currentIndex];
        InterviewManager.Instance.StartInterview(candidate);
        interviewStarted = true;

        candidateInfoText.text = $"<b>Interviewing: {candidate.FullName}</b>\n\nUse the dialogue panel to ask questions.";
        UpdateQuestionsRemaining();

        interviewButton.gameObject.SetActive(false);
        hireButton.gameObject.SetActive(true);
        rejectButton.gameObject.SetActive(true);
    }

    public void OnHireClicked()
    {
        if (!interviewStarted) return;

        if (RecruitmentManager.Instance.IsSquadFull)
        {
            candidateInfoText.text =
                $"<b>Squad full ({RecruitmentManager.Instance.SquadSize}/{RecruitmentManager.MAX_SQUAD_SIZE}).</b>\n" +
                $"Release a player from your squad before signing {candidates[currentIndex].FullName}.\n" +
                "(Open your squad and release someone, then press Hire again.)";
            return;
        }

        if (InterviewManager.Instance.HireInterviewee())
        {
            candidateInfoText.text = $"<b>{candidates[currentIndex].FullName}</b> has been signed!\n\nSession complete — only one hire per session.";
            interviewStarted = false;
            SetButtonsActive(false);
            RefreshQuestionButtons();
            Debug.Log($"[Recruitment UI] Hired {candidates[currentIndex].FullName}");
        }
    }

    public void OnRejectClicked()
    {
        if (!interviewStarted) return;

        InterviewManager.Instance.RejectInterviewee();
        Debug.Log($"[Recruitment UI] Rejected {candidates[currentIndex].FullName}");
        AdvanceToNextCandidate();
    }

    public void OnSkipClicked()
    {
        EndInterview();
        AdvanceToNextCandidate();
    }

    private void EndInterview()
    {
        interviewStarted = false;
        InterviewManager.Instance.EndInterview();
    }

    private void AdvanceToNextCandidate()
    {
        currentIndex++;
        ShowCurrentCandidate();
    }

    public void OnQuestionAsked()
    {
        UpdateQuestionsRemaining();
        RefreshQuestionButtons();
    }

    private void UpdateQuestionsRemaining()
    {
        int remaining = InterviewManager.Instance.QuestionsRemaining;
        string personality = InterviewManager.Instance.NarrowedPersonalitiesText();
        questionsRemainingText.text = $"Questions remaining: {remaining}\nPersonality: {personality}";
    }

    private void SetButtonsActive(bool active)
    {
        interviewButton.gameObject.SetActive(active);
        hireButton.gameObject.SetActive(active);
        rejectButton.gameObject.SetActive(active);
        skipButton.gameObject.SetActive(active);
    }
}
