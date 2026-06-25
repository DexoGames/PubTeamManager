using TMPro;
using UnityEngine;

/// <summary>
/// A single interview-question button. Set its <see cref="questionType"/> in the inspector (and the
/// <see cref="stat"/> if it's an "ask about a stat" question); on click it asks the current interviewee
/// and the answer appears in the DialogueUI. Stat/strength questions reveal ability; the personality
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

    private void OnEnable()
    {
        if (label == null) return;
        PlayerStat? target = questionType == InterviewQuestionType.AskAboutStat ? stat : (PlayerStat?)null;
        label.text = new InterviewQuestion(questionType, target).QuestionText;
    }

    public void Ask()
    {
        if (InterviewManager.Instance == null) return;

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

        if (RecruitmentPageUI.Instance != null)
            RecruitmentPageUI.Instance.OnQuestionAsked();
    }
}
