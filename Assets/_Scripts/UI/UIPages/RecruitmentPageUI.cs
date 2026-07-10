using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI page for the recruitment/interview flow.
/// Shows interview session candidates, handles hire/reject cycle.
/// </summary>
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

    private List<Player> candidates;
    private int currentIndex = 0;
    private bool interviewStarted = false;

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
        StartSession();
    }

    private void StartSession()
    {
        // Interviews are only available on a scheduled interview day, once per day.
        if (!RecruitmentManager.Instance.CanInterviewToday)
        {
            headerText.text = "No Interviews Today";
            candidateInfoText.text = RecruitmentManager.Instance.IsInterviewDay
                ? "You've already held your interviews today. Come back on the next interview day."
                : "Interviews are only held on scheduled interview days — check your calendar.";
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

    private void ShowCurrentCandidate()
    {
        if (candidates == null || currentIndex >= candidates.Count)
        {
            candidateInfoText.text = "All candidates reviewed. Session complete.";
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
    }

    public void OnInterviewClicked()
    {
        if (candidates == null || currentIndex >= candidates.Count) return;

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

        // Squad cap: the player must release someone before signing a new player.
        if (RecruitmentManager.Instance.IsSquadFull)
        {
            candidateInfoText.text =
                $"<b>Squad full ({RecruitmentManager.Instance.SquadSize}/{RecruitmentManager.MAX_SQUAD_SIZE}).</b>\n" +
                $"Release a player from your squad before signing {candidates[currentIndex].FullName}.\n" +
                "(Open your squad and release someone, then press Hire again.)";
            return;
        }

        bool success = InterviewManager.Instance.HireInterviewee();
        if (success)
        {
            candidateInfoText.text = $"<b>{candidates[currentIndex].FullName}</b> has been signed!\n\nSession complete — only one hire per session.";
            SetButtonsActive(false);
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
        AdvanceToNextCandidate();
    }

    private void AdvanceToNextCandidate()
    {
        currentIndex++;
        ShowCurrentCandidate();
    }

    /// <summary>Called by an InterviewQuestionButton after a question is asked, to refresh the read-out.</summary>
    public void OnQuestionAsked()
    {
        UpdateQuestionsRemaining();
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
