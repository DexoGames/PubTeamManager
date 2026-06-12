using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bottom-left home panel that adapts to the player's NEXT fixture:
///   • next game is a league match → a windowed league table centred on the player's position
///   • next game is a cup tie       → the current knockout round's ties (bracket state)
/// Falls back to the main league's final table when there is no upcoming fixture.
///
/// A UIObject, so it refreshes automatically whenever the home page is shown (UIPage.SetupUI).
/// </summary>
public class CompetitionContextWidget : UIObject
{
    [Header("League view")]
    [SerializeField] private GameObject leagueView;
    [SerializeField] private Transform leagueRowContainer;
    [SerializeField] private LeagueTableTeamUI leagueRowPrefab;
    [SerializeField] private TextMeshProUGUI leagueHeader;
    [SerializeField] private int windowSize = 5;

    [Header("Bracket view")]
    [SerializeField] private GameObject bracketView;
    [SerializeField] private Transform bracketRowContainer;
    [SerializeField] private CupTieRowUI tieRowPrefab;
    [SerializeField] private TextMeshProUGUI bracketHeader;

    [Header("Highlight")]
    [SerializeField] private Color myTeamHighlight = new Color(0.95f, 0.85f, 0.35f, 0.5f);

    public override void Setup() => Refresh();

    public void Refresh()
    {
        Team myTeam = TeamManager.Instance != null ? TeamManager.Instance.MyTeam : null;
        if (myTeam == null) return;

        Fixture upcoming = myTeam.GetUpcomingFixture();
        Competition comp = upcoming != null ? upcoming.Competition : null;

        if (comp is Cup cup)
        {
            ShowBracket(cup, upcoming.Round, myTeam);
        }
        else if (comp is League league)
        {
            ShowLeague(league, myTeam);
        }
        else
        {
            // No upcoming fixture (e.g. between seasons) — fall back to the main league table.
            League main = myTeam.GetMainLeague();
            if (main != null) ShowLeague(main, myTeam);
            else SetView(showLeague: false, showBracket: false);
        }
    }

    private void SetView(bool showLeague, bool showBracket)
    {
        if (leagueView != null) leagueView.SetActive(showLeague);
        if (bracketView != null) bracketView.SetActive(showBracket);
    }

    // ————————————————————— league —————————————————————

    private void ShowLeague(League league, Team myTeam)
    {
        SetView(showLeague: true, showBracket: false);
        if (leagueHeader != null) leagueHeader.text = league.Name;
        if (leagueRowContainer == null || leagueRowPrefab == null) return;

        Game.ClearContainer(leagueRowContainer);

        var standings = league.GetStandings();
        if (standings == null || standings.Count == 0) return;

        int myIdx = standings.FindIndex(e => e.team == myTeam);
        if (myIdx < 0) myIdx = 0;

        int maxStart = Mathf.Max(0, standings.Count - windowSize);
        int start = Mathf.Clamp(myIdx - windowSize / 2, 0, maxStart);
        int end = Mathf.Min(standings.Count, start + windowSize);

        for (int i = start; i < end; i++)
        {
            var row = Instantiate(leagueRowPrefab, leagueRowContainer);
            row.SetLeagueStandingText(standings[i], i + 1); // real position (1-based)
            if (standings[i].team == myTeam) Highlight(row.gameObject);
        }
    }

    // ————————————————————— cup bracket —————————————————————

    private void ShowBracket(Cup cup, int roundIndex, Team myTeam)
    {
        SetView(showLeague: false, showBracket: true);
        if (bracketRowContainer == null || tieRowPrefab == null) return;

        Game.ClearContainer(bracketRowContainer);

        if (cup.Rounds == null || cup.Rounds.Length == 0)
        {
            if (bracketHeader != null) bracketHeader.text = cup.Name;
            return;
        }

        roundIndex = Mathf.Clamp(roundIndex, 0, cup.Rounds.Length - 1);
        var ties = cup.Rounds[roundIndex];

        if (bracketHeader != null) bracketHeader.text = $"{cup.Name} — {CupRoundName(ties.Count)}";

        foreach (var tie in ties)
        {
            var row = Instantiate(tieRowPrefab, bracketRowContainer);
            row.Set(tie, myTeam);
        }
    }

    private void Highlight(GameObject row)
    {
        var img = row.GetComponent<Image>();
        if (img != null) img.color = myTeamHighlight;
    }

    /// <summary>Names a knockout round from the number of ties it contains.</summary>
    public static string CupRoundName(int ties)
    {
        switch (ties)
        {
            case 1: return "Final";
            case 2: return "Semi-Final";
            case 4: return "Quarter-Final";
            case 8: return "Round of 16";
            case 16: return "Round of 32";
            case 32: return "Round of 64";
            default: return $"Round ({ties} ties)";
        }
    }
}
