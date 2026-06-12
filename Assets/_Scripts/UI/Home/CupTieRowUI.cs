using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One knockout tie in the home-screen bracket view: "Home [score] Away", highlighting the
/// player's tie. Shows "vs" until the tie has been played.
/// </summary>
public class CupTieRowUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI homeText;
    [SerializeField] private TextMeshProUGUI awayText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [Tooltip("Optional row background, tinted when this is the player's tie.")]
    [SerializeField] private Image background;

    public void Set(Fixture tie, Team myTeam)
    {
        if (homeText != null) homeText.text = tie.HomeTeam != null ? LinkBuilder.BuildLink(tie.HomeTeam) : "—";
        if (awayText != null) awayText.text = tie.AwayTeam != null ? LinkBuilder.BuildLink(tie.AwayTeam) : "—";

        if (scoreText != null)
            scoreText.text = tie.BeenPlayed ? $"{tie.Result.score.home} - {tie.Result.score.away}" : "vs";

        bool involvesMe = myTeam != null && (tie.HomeTeam == myTeam || tie.AwayTeam == myTeam);
        if (background != null && involvesMe)
            background.color = new Color(0.95f, 0.85f, 0.35f, 0.5f);
    }
}
