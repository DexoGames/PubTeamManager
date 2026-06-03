using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// Base class for all competitions (leagues, cups).
/// Plain C# class (not ScriptableObject) for runtime instances — makes serialization clean.
/// Template data (names, config) comes from ScriptableObject blueprints in the editor.
/// </summary>
public abstract class Competition
{
    /// <summary>Stable per-instance ID (one per season instance). Assigned by FixturesManager.</summary>
    public int Id = -1;

    /// <summary>Links this season instance to its CompetitionSeries lineage.</summary>
    public int SeriesId = -1;

    /// <summary>Which season this instance represents (e.g. the starting calendar year).</summary>
    public int SeasonYear;

    public string Name;
    public int Priority = 0;

    [JsonProperty(ItemConverterType = typeof(TeamRefConverter))]
    public List<Team> Teams = new List<Team>();

    [JsonIgnore] public List<Fixture>[] Rounds { get; protected set; }
    [JsonIgnore] public List<Competition> RelatedCompetitions { get; protected set; } = new List<Competition>();

    public List<Fixture> Fixtures { get; set; } = new List<Fixture>();
    public bool IsComplete { get; set; } = false;

    [JsonIgnore] protected DateTime startDate;

    /// <summary>Parameterless constructor for deserialization.</summary>
    protected Competition() { }

    public Competition(string name, int priority, List<Team> teams, DateTime startDate)
    {
        Name = name;
        Priority = priority;
        Teams = teams;
        this.startDate = startDate;
        GenerateFixtures();
    }

    public virtual void GenerateFixtures()
    {
        FixturesManager.Instance.RegisterFixtures(Fixtures);
    }

    protected bool IsTeamAvailable(Team team, DateTime date)
    {
        return !FixturesManager.Instance.GetAllFixtures().Exists(f =>
            (f.HomeTeam == team || f.AwayTeam == team) &&
            (f.Date.Date == date.Date || f.Date.Date == date.AddDays(1).Date || f.Date.Date == date.AddDays(-1).Date));
    }

    public int GetMostRecentRound()
    {
        if (Rounds == null || Rounds.Length == 0)
            return -1;

        for (int i = Rounds.Length - 1; i >= 0; i--)
        {
            foreach (var fixture in Rounds[i])
            {
                if (!fixture.BeenPlayed)
                {
                    goto NextLoop;
                }
            }
            return i;

        NextLoop:
            continue;
        }
        return 0;
    }

    /// <summary>
    /// Rebuilds the Rounds array from the Fixtures list by grouping on each fixture's Round field.
    /// Called during save restore since Rounds is only populated during GenerateFixtures().
    /// </summary>
    public void BuildRoundsFromFixtures()
    {
        if (Fixtures == null || Fixtures.Count == 0)
        {
            Rounds = new List<Fixture>[0];
            return;
        }

        int maxRound = Fixtures.Max(f => f.Round);
        Rounds = new List<Fixture>[maxRound + 1];

        for (int i = 0; i <= maxRound; i++)
            Rounds[i] = new List<Fixture>();

        foreach (var fixture in Fixtures)
        {
            Rounds[fixture.Round].Add(fixture);
        }
    }

    public int GetUpcomingRound()
    {
        if (Rounds == null || Rounds.Length == 0)
            return -1;

        for (int i = 0; i < Rounds.Length; i++)
        {
            foreach (var fixture in Rounds[i])
            {
                if (!fixture.BeenPlayed)
                {
                    return i;
                }
            }
        }
        return 0;
    }

    protected DateTime FindNextAvailableDate(Team team1, Team team2, DateTime fromDate)
    {
        DateTime date = fromDate.AddDays(UnityEngine.Random.Range(-1, 2));

        while (!IsTeamAvailable(team1, date) || !IsTeamAvailable(team2, date))
        {
            date = date.AddDays(1);
        }

        return date;
    }
}
