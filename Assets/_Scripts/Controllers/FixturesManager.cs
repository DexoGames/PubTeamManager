using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FixturesManager : MonoBehaviour
{
    List<Fixture> allFixtures = new List<Fixture>();

    Dictionary<Team, List<Fixture>> fixturesByTeam = new Dictionary<Team, List<Fixture>>();
    private readonly Dictionary<int, Fixture> _fixturesById = new Dictionary<int, Fixture>();

    public List<Competition> Competitions { get; private set; } = new List<Competition>();
    private readonly Dictionary<int, Competition> _competitionsById = new Dictionary<int, Competition>();

    public List<CompetitionSeries> CompetitionSeries { get; private set; } = new List<CompetitionSeries>();
    private readonly Dictionary<int, CompetitionSeries> _seriesById = new Dictionary<int, CompetitionSeries>();

    public static FixturesManager Instance { get; private set; }

    public void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
        }

        // Subscribe to competition events for title recording
        CompetitionEvents.OnLeagueWon += HandleLeagueWon;
        CompetitionEvents.OnCupWon += HandleCupWon;
    }

    private void OnDestroy()
    {
        CompetitionEvents.OnLeagueWon -= HandleLeagueWon;
        CompetitionEvents.OnCupWon -= HandleCupWon;
    }

    private void HandleLeagueWon(League league, Team champion)
    {
        int season = CalenderManager.Instance.CurrentDay.Year;
        champion.Stats.RecordLeagueWin(league.Name, season);
    }

    private void HandleCupWon(Cup cup, Team winner)
    {
        int season = CalenderManager.Instance.CurrentDay.Year;
        winner.Stats.RecordCupWin(cup.Name, season);
    }

    public void AddComps()
    {
        var teams = TeamManager.Instance.GetAllTeams();
        DateTime startDate = CalenderManager.Instance.CurrentDay;
        int seasonYear = startDate.Year;

        LeagueTemplate[] leagueTemplates = Resources.LoadAll<LeagueTemplate>("Competitions/Leagues");

        // Sort templates by name to ensure consistent ordering (1, 2, 3, 4)
        System.Array.Sort(leagueTemplates, (a, b) => string.Compare(a.name, b.name));

        League div3 = leagueTemplates[3].CreateLeague(teams.GetRange(0, 20), startDate);
        League div2 = leagueTemplates[2].CreateLeague(teams.GetRange(20, 20), startDate);
        League div1 = leagueTemplates[1].CreateLeague(teams.GetRange(40, 20), startDate);
        League premier = leagueTemplates[0].CreateLeague(teams.GetRange(60, 20), startDate);

        // Link promotion/relegation between leagues
        premier.RelegationLeague = div1;
        div1.PromotionLeague = premier;
        div1.RelegationLeague = div2;
        div2.PromotionLeague = div1;
        div2.RelegationLeague = div3;
        div3.PromotionLeague = div2;

        Cup cup = new Cup("Papa Johns Cup", teams, startDate.AddDays(10));

        RegisterCompetition(premier, GetOrCreateSeries(premier.Name, premier.Name), seasonYear);
        RegisterCompetition(div1, GetOrCreateSeries(div1.Name, div1.Name), seasonYear);
        RegisterCompetition(div2, GetOrCreateSeries(div2.Name, div2.Name), seasonYear);
        RegisterCompetition(div3, GetOrCreateSeries(div3.Name, div3.Name), seasonYear);
        RegisterCompetition(cup, GetOrCreateSeries(cup.Name, cup.Name), seasonYear);

        foreach (var c in Competitions)
            Debug.Log($"[Comp] '{c.Name}' Id:{c.Id} Series:{c.SeriesId} Season:{c.SeasonYear} Fixtures:{c.Fixtures.Count}");
    }

    /// <summary>
    /// Assigns an ID + series + season to a freshly-created competition instance and indexes it.
    /// </summary>
    private void RegisterCompetition(Competition comp, CompetitionSeries series, int seasonYear)
    {
        if (comp == null) return;
        if (comp.Id < 0) comp.Id = IdManager.Instance.AllocateCompetitionId();
        comp.SeriesId = series.Id;
        comp.SeasonYear = seasonYear;

        if (!_competitionsById.ContainsKey(comp.Id))
        {
            Competitions.Add(comp);
            _competitionsById[comp.Id] = comp;
        }
        series.AddSeason(comp);
    }

    /// <summary>
    /// Finds the lineage for a competition (anchored by templateName), creating it if absent.
    /// </summary>
    public CompetitionSeries GetOrCreateSeries(string templateName, string displayName)
    {
        var existing = CompetitionSeries.FirstOrDefault(s => s.TemplateName == templateName);
        if (existing != null) return existing;

        var series = new CompetitionSeries(IdManager.Instance.AllocateSeriesId(), displayName, templateName);
        CompetitionSeries.Add(series);
        _seriesById[series.Id] = series;
        return series;
    }

    public Competition GetCompetition(int id) =>
        _competitionsById.TryGetValue(id, out var c) ? c : null;

    public Fixture GetFixture(int id) =>
        _fixturesById.TryGetValue(id, out var f) ? f : null;

    public CompetitionSeries GetSeries(int id) =>
        _seriesById.TryGetValue(id, out var s) ? s : null;

    /// <summary>Season instances of a series, in chronological order.</summary>
    public List<Competition> GetSeasonsOfSeries(int seriesId) =>
        GetSeries(seriesId)?.Seasons ?? new List<Competition>();

    /// <summary>The active (not-yet-complete) competitions — the current season.</summary>
    public List<Competition> GetActiveCompetitions() =>
        Competitions.Where(c => !c.IsComplete).ToList();

    /// <summary>
    /// Rolls over to a new season. The finished instances are marked complete and RETAINED
    /// (for history); brand-new Competition instances are created for the new season, linked
    /// to the same CompetitionSeries lineage. Promotion/relegation is applied into fresh
    /// rosters without mutating the archived instances.
    /// </summary>
    public void StartNewSeason()
    {
        // The current (most recent) instance of each series.
        var currentLeagues = CompetitionSeries
            .Select(s => GetCompetition(s.SeasonCompetitionIds.LastOrDefault()) as League)
            .Where(l => l != null)
            .ToList();

        // Mark finished, and snapshot rosters so we don't mutate the archived instances.
        var newRosters = new Dictionary<League, List<Team>>();
        foreach (var league in currentLeagues)
        {
            league.IsComplete = true;
            newRosters[league] = new List<Team>(league.Teams);
        }

        // Apply promotion/relegation movements into the snapshot rosters.
        foreach (var league in currentLeagues)
        {
            if (league.PromotionLeague is League promo && newRosters.ContainsKey(promo))
            {
                foreach (var team in league.GetPromotedTeams())
                {
                    newRosters[league].Remove(team);
                    newRosters[promo].Add(team);
                    Debug.Log($"[Season] {team.Name} promoted to {promo.Name}");
                }
            }
            if (league.RelegationLeague is League releg && newRosters.ContainsKey(releg))
            {
                foreach (var team in league.GetRelegatedTeams())
                {
                    newRosters[league].Remove(team);
                    newRosters[releg].Add(team);
                    Debug.Log($"[Season] {team.Name} relegated to {releg.Name}");
                }
            }
        }

        DateTime newStartDate = CalenderManager.Instance.CurrentDay;
        int seasonYear = newStartDate.Year;

        // Create new league instances with the updated rosters, linked to the same series.
        var newLeaguesByName = new Dictionary<string, League>();
        foreach (var oldLeague in currentLeagues)
        {
            List<Team> roster = newRosters[oldLeague];
            League newLeague = oldLeague.Template != null
                ? oldLeague.Template.CreateLeague(roster, newStartDate)
                : new League(oldLeague.Name, roster, newStartDate);

            RegisterCompetition(newLeague, GetSeries(oldLeague.SeriesId), seasonYear);
            newLeaguesByName[newLeague.Name] = newLeague;
        }

        // Re-establish promotion/relegation links among the new instances.
        foreach (var oldLeague in currentLeagues)
        {
            var newLeague = newLeaguesByName[oldLeague.Name];
            if (oldLeague.PromotionLeague != null && newLeaguesByName.TryGetValue(oldLeague.PromotionLeague.Name, out var np))
                newLeague.PromotionLeague = np;
            if (oldLeague.RelegationLeague != null && newLeaguesByName.TryGetValue(oldLeague.RelegationLeague.Name, out var nr))
                newLeague.RelegationLeague = nr;
        }

        // New cup with all teams, linked to the cup series.
        var allTeams = TeamManager.Instance.GetAllTeams();
        var currentCup = CompetitionSeries
            .Select(s => GetCompetition(s.SeasonCompetitionIds.LastOrDefault()) as Cup)
            .FirstOrDefault(c => c != null);
        if (currentCup != null) currentCup.IsComplete = true;
        string cupName = currentCup?.Name ?? "Papa Johns Cup";
        var cupSeries = currentCup != null ? GetSeries(currentCup.SeriesId) : GetOrCreateSeries(cupName, cupName);
        Cup newCup = new Cup(cupName, allTeams, newStartDate.AddDays(10));
        RegisterCompetition(newCup, cupSeries, seasonYear);

        // Increment seasons played for all teams
        foreach (var team in allTeams)
        {
            team.Stats.SeasonsPlayed++;
        }

        foreach (var newLeague in newLeaguesByName.Values)
            Debug.Log($"[Season] New '{newLeague.Name}' Id:{newLeague.Id} Series:{newLeague.SeriesId} Season:{seasonYear} — lineage now {GetSeasonsOfSeries(newLeague.SeriesId).Count} seasons");
    }

    public void RegisterFixtures(List<Fixture> fixtures)
    {
        Debug.Log($"Registering {fixtures.Count} fixtures");

        foreach (Fixture fixture in fixtures)
        {
            if (fixture.Id >= 0 && _fixturesById.ContainsKey(fixture.Id)) continue;
            if (allFixtures.Contains(fixture)) continue;

            allFixtures.Add(fixture);
            if (fixture.Id >= 0) _fixturesById[fixture.Id] = fixture;

            if (!fixturesByTeam.ContainsKey(fixture.HomeTeam))
                fixturesByTeam[fixture.HomeTeam] = new List<Fixture>();
            fixturesByTeam[fixture.HomeTeam].Add(fixture);

            if (!fixturesByTeam.ContainsKey(fixture.AwayTeam))
                fixturesByTeam[fixture.AwayTeam] = new List<Fixture>();
            fixturesByTeam[fixture.AwayTeam].Add(fixture);
        }
    }

    /// <summary>
    /// Clears all competition/fixture/series state — called at the start of a save restore.
    /// </summary>
    public void ClearCompetitionState()
    {
        allFixtures.Clear();
        fixturesByTeam.Clear();
        _fixturesById.Clear();
        Competitions.Clear();
        _competitionsById.Clear();
        CompetitionSeries.Clear();
        _seriesById.Clear();
    }

    /// <summary>
    /// Indexes a restored competition instance, preserving its existing Id/SeriesId/SeasonYear.
    /// </summary>
    public void IndexRestoredCompetition(Competition comp)
    {
        if (comp == null || comp.Id < 0) return;
        if (!_competitionsById.ContainsKey(comp.Id))
        {
            Competitions.Add(comp);
            _competitionsById[comp.Id] = comp;
        }
    }

    /// <summary>Restores the series lineages from saved state.</summary>
    public void RestoreSeries(List<CompetitionSeries> series)
    {
        CompetitionSeries.Clear();
        _seriesById.Clear();
        if (series == null) return;
        foreach (var s in series)
        {
            CompetitionSeries.Add(s);
            _seriesById[s.Id] = s;
        }
    }

    public List<Fixture> GetAllFixtures()
    {
        return allFixtures;
    }

    public List<Fixture> GetAllUpcomingFixtures(int daysAhead = 1000)
    {
        Debug.Log("Getting all upcoming fixtures");

        DateTime today = CalenderManager.Instance.CurrentDay;
        DateTime maxDate = today.AddDays(daysAhead);

        return allFixtures
            .Where(f => !f.BeenPlayed && f.Date >= today && f.Date <= maxDate)
            .OrderBy(f => f.Date)
            .ToList();
    }

    public List<Fixture> GetUpcomingFixturesForTeam(Team team, int daysAhead = 1000)
    {
        if (!fixturesByTeam.ContainsKey(team))
        {
            Debug.LogWarning($"No fixtures found for team: {team.Name}");
            return new List<Fixture>();
        }

        DateTime today = CalenderManager.Instance.CurrentDay;
        DateTime maxDate = today.AddDays(daysAhead);

        return fixturesByTeam[team]
            .Where(f => !f.BeenPlayed && f.Date >= today && f.Date <= maxDate)
            .OrderBy(f => f.Date)
            .ToList();
    }

    /// <summary>
    /// Gets the current state of all competitions.
    /// </summary>
    public List<Competition> GetCompetitionState() => Competitions;

    private void Update()
    {
        // Reserved for future debugging/testing
    }
}
