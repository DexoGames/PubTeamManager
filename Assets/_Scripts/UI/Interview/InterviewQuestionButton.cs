using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A single interview-question button. Either hand-placed (set its <see cref="questionType"/> in the inspector,
/// and the <see cref="stat"/> if it's an "ask about a stat" question) or spawned by
/// <see cref="RecruitmentPageUI"/> and bound via <see cref="SetQuestion"/>. On click it asks the current
/// interviewee and the answer appears in the DialogueUI. Stat/strength questions reveal ability; the personality
/// questions (HandleCriticism/WorkEthic/BigGameMentality/Leadership) narrow down the hidden personality.
///
/// Wire: put on a Button, assign the label, hook the Button's OnClick to <see cref="Ask"/>.
/// </summary>
public class InterviewQuestionButton : MonoBehaviour
{
    [SerializeField] private InterviewQuestionType questionType;
    [Tooltip("Only used when Question Type is AskAboutStat.")]
    [SerializeField] private PlayerStat stat;
    [SerializeField] private TextMeshProUGUI label;

    private Button button;

    private void Awake() => button = GetComponent<Button>();

    private void OnEnable()
    {
        RefreshLabel();
        RefreshInteractable();
    }

    /// <summary>Binds this button to a question (used when the page spawns its own question row).</summary>
    public void SetQuestion(InterviewQuestion question)
    {
        questionType = question.Type;
        if (question.TargetStat.HasValue) stat = question.TargetStat.Value;

        RefreshLabel();
        RefreshInteractable();
    }

    /// <summary>Greys the button out when clicking it would do nothing — no interview running, or questions spent.</summary>
    public void RefreshInteractable()
    {
        button.interactable = InterviewManager.Instance.GetInterviewee() != null
                           && InterviewManager.Instance.QuestionsRemaining > 0;
    }

    private void RefreshLabel()
    {
        PlayerStat? target = questionType == InterviewQuestionType.AskAboutStat ? stat : (PlayerStat?)null;
        label.text = new InterviewQuestion(questionType, target).QuestionText;
    }

    public void Ask()
    {
        // Comparison opens the player picker first; it consumes the question + refreshes the page once a player
        // is chosen (or costs nothing if cancelled), so we return early here.
        if (questionType == InterviewQuestionType.CompareToPlayer)
        {
            InterviewManager.Instance.AskComparison();
            return;
        }

        if (questionType == InterviewQuestionType.AskAboutStat)
            InterviewManager.Instance.AskAboutStat(stat);
        else
            InterviewManager.Instance.AskQuestion(questionType);

        RecruitmentPageUI.Instance.OnQuestionAsked();
    }
}
