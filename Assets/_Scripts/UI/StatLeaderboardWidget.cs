using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>
/// A reusable "top N" stat list, rendered like the league table (ranked rows with clickable links). One widget
/// covers every use:
///   • Main screen — Teams mode, MyMainLeague scope, with a dropdown to switch the stat (team with most shots, …).
///   • My Team page — Players mode + Restrict To Shown Team (top scorers/assisters in that squad).
///   • Stats page — drop several, each fixed to a stat (top scorers, most assists, team saves, …).
/// It's a <see cref="UIObject"/>, so the owning <see cref="UIPage"/> calls <see cref="Setup"/> automatically on show.
/// </summary>
public class StatLeaderboardWidget : UIObject
{
    public enum EntityMode { Players, Teams }
    public enum FixtureScope { MyMainLeague, ShownTeamCompetitions, AllCurrentSeason }

    [Header("What to rank")]
    [SerializeField] private EntityMode mode = EntityMode.Players;
    [SerializeField] private FixtureScope scope = FixtureScope.MyMainLeague;
    [Tooltip("Players mode only: rank just the players of the team currently shown on the club page (My Team page).")]
    [SerializeField] private bool restrictToShownTeam = false;
    [SerializeField] private int topN = 10;

    [Header("Stat selection")]
    [Tooltip("Stats offered. With a dropdown assigned the user can switch between them; otherwise the first is shown.")]
    [SerializeField] private List<StatCategory> categories = new List<StatCategory> { StatCategory.Goals };
    [Tooltip("Optional — lets the player change which stat the list ranks by.")]
    [SerializeField] private TMP_Dropdown categoryDropdown;
    [Tooltip("Optional — shows e.g. \"Top Goals\". Updated when the stat changes.")]
    [SerializeField] private TextMeshProUGUI titleText;

    [Header("List")]
    [SerializeField] private StatLeaderboardRowUI rowPrefab;
    [SerializeField] private RectTransform container;

    private bool dropdownWired;

    public override void Setup()
    {
        if (categories == null || categories.Count == 0)
            categories = new List<StatCategory> { StatCategory.Goals };

        if (categoryDropdown != null && !dropdownWired)
        {
            categoryDropdown.ClearOptions();
            categoryDropdown.AddOptions(categories.Select(StatLeaderboards.Label).ToList());
            categoryDropdown.onValueChanged.AddListener(_ => Render());
            dropdownWired = true;
        }

        Render();
    }

    private void Render()
    {
        if (container == null || rowPrefab == null) return;
        Game.ClearContainer(container);

        StatCategory cat = CurrentCategory();
        if (titleText != null)
            titleText.text = (mode == EntityMode.Teams ? "Team " : "") + StatLeaderboards.Label(cat);

        var fixtures = ResolveFixtures(out Team teamFilter);

        int rank = 1;
        if (mode == EntityMode.Teams)
        {
            foreach (var r in StatLeaderboards.TopTeams(fixtures, cat, topN))
                Instantiate(rowPrefab, container).Set(rank++, LinkBuilder.BuildLink(r.team), r.value);
        }
        else
        {
            foreach (var r in StatLeaderboards.TopPlayers(fixtures, cat, topN, teamFilter))
                Instantiate(rowPrefab, container).Set(rank++, LinkBuilder.BuildLink(r.player), r.value);
        }
    }

    private StatCategory CurrentCategory()
    {
        if (categoryDropdown != null && categories.Count > 0)
            return categories[Mathf.Clamp(categoryDropdown.value, 0, categories.Count - 1)];
        return categories[0];
    }

    /// <summary>Resolves which fixtures to aggregate, and (for player mode) an optional single-team filter.</summary>
    private IEnumerable<Fixture> ResolveFixtures(out Team teamFilter)
    {
        teamFilter = null;

        if (mode == EntityMode.Players && restrictToShownTeam)
        {
            Team t = ShownOrMyTeam();
            teamFilter = t;
            return t != null ? t.GetAllCompetitions().SelectMany(c => c.Fixtures) : Enumerable.Empty<Fixture>();
        }

        switch (scope)
        {
            case FixtureScope.ShownTeamCompetitions:
                Team shown = ShownOrMyTeam();
                return shown != null ? shown.GetAllCompetitions().SelectMany(c => c.Fixtures) : Enumerable.Empty<Fixture>();

            case FixtureScope.AllCurrentSeason:
                return FixturesManager.Instance != null
                    ? FixturesManager.Instance.GetCurrentSeasonCompetitions().SelectMany(c => c.Fixtures)
                    : Enumerable.Empty<Fixture>();

            case FixtureScope.MyMainLeague:
            default:
                League league = TeamManager.Instance != null && TeamManager.Instance.MyTeam != null
                    ? TeamManager.Instance.MyTeam.GetMainLeague()
                    : null;
                return league != null ? league.Fixtures : Enumerable.Empty<Fixture>();
        }
    }

    private static Team ShownOrMyTeam()
    {
        if (TeamDetailsUI.Instance != null && TeamDetailsUI.Instance.CurrentTeam != null)
            return TeamDetailsUI.Instance.CurrentTeam;
        return TeamManager.Instance != null ? TeamManager.Instance.MyTeam : null;
    }
}
