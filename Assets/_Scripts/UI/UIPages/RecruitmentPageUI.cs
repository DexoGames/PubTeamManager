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

    /// <summary>
    /// Begins a new interview session — gets 5 candidates from RecruitmentManager.
    /// </summary>
    private void StartSession()
    {
        if (RecruitmentManager.Instance == null) return;

        // Interviews are only available on a scheduled interview day, once per day.
        if (!RecruitmentManager.Instance.CanInterviewToday)
        {
            if (headerText != null) headerText.text = "No Interviews Today";
            if (candidateInfoText != null)
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

        if (headerText != null)
            headerText.text = $"Interview Day — {candidates.Count} Candidates";

        ShowCurrentCandidate();
    }

    /// <summary>
    /// Displays the current candidate's info.
    /// </summary>
    private void ShowCurrentCandidate()
    {
        if (candidates == null || currentIndex >= candidates.Count)
        {
            // All candidates reviewed
            if (candidateInfoText != null)
                candidateInfoText.text = "All candidates reviewed. Session complete.";

            SetButtonsActive(false);
            return;
        }

        Player candidate = candidates[currentIndex];
        interviewStarted = false;

        if (candidateInfoText != null)
        {
            candidateInfoText.text = $"<b>{candidate.FullName}</b>\n" +
                $"Candidate {currentIndex + 1} of {candidates.Count}\n\n" +
                $"Start an interview to learn more about this player.";
        }

        if (questionsRemainingText != null)
            questionsRemainingText.text = "";

        // Show interview button, hide hire/reject until interview starts
        if (interviewButton != null) interviewButton.gameObject.SetActive(true);
        if (hireButton != null) hireButton.gameObject.SetActive(false);
        if (rejectButton != null) rejectButton.gameObject.SetActive(false);
        if (skipButton != null) skipButton.gameObject.SetActive(true);
    }

    /// <summary>
    /// Called by the "Start Interview" button.
    /// </summary>
    public void OnInterviewClicked()
    {
        if (candidates == null || currentIndex >= candidates.Count) return;

        Player candidate = candidates[currentIndex];
        InterviewManager.Instance.StartInterview(candidate);
        interviewStarted = true;

        if (candidateInfoText != null)
            candidateInfoText.text = $"<b>Interviewing: {candidate.FullName}</b>\n\nUse the dialogue panel to ask questions.";

        UpdateQuestionsRemaining();

        // Show hire/reject, hide interview button
        if (interviewButton != null) interviewButton.gameObject.SetActive(false);
        if (hireButton != null) hireButton.gameObject.SetActive(true);
        if (rejectButton != null) rejectButton.gameObject.SetActive(true);
    }

    /// <summary>
    /// Called by the "Hire" button.
    /// </summary>
    public void OnHireClicked()
    {
        if (!interviewStarted) return;

        // Squad cap: the player must release someone before signing a new player.
        if (RecruitmentManager.Instance.IsSquadFull)
        {
            if (candidateInfoText != null)
                candidateInfoText.text =
                    $"<b>Squad full ({RecruitmentManager.Instance.SquadSize}/{RecruitmentManager.MAX_SQUAD_SIZE}).</b>\n" +
                    $"Release a player from your squad before signing {candidates[currentIndex].FullName}.\n" +
                    "(Open your squad and release someone, then press Hire again.)";
            return;
        }

        bool success = InterviewManager.Instance.HireInterviewee();
        if (success)
        {
            if (candidateInfoText != null)
                candidateInfoText.text = $"<b>{candidates[currentIndex].FullName}</b> has been signed!\n\nSession complete — only one hire per session.";

            SetButtonsActive(false);

            Debug.Log($"[Recruitment UI] Hired {candidates[currentIndex].FullName}");
        }
    }

    /// <summary>
    /// Called by the "Reject" button.
    /// </summary>
    public void OnRejectClicked()
    {
        if (!interviewStarted) return;

        InterviewManager.Instance.RejectInterviewee();
        Debug.Log($"[Recruitment UI] Rejected {candidates[currentIndex].FullName}");
        AdvanceToNextCandidate();
    }

    /// <summary>
    /// Called by the "Skip" button (skip without interview).
    /// </summary>
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
        if (questionsRemainingText == null) return;

        int remaining = InterviewManager.Instance.QuestionsRemaining;
        string personality = InterviewManager.Instance.NarrowedPersonalitiesText();
        questionsRemainingText.text = $"Questions remaining: {remaining}\nPersonality: {personality}";
    }

    private void SetButtonsActive(bool active)
    {
        if (interviewButton != null) interviewButton.gameObject.SetActive(active);
        if (hireButton != null) hireButton.gameObject.SetActive(active);
        if (rejectButton != null) rejectButton.gameObject.SetActive(active);
        if (skipButton != null) skipButton.gameObject.SetActive(active);
    }
}
