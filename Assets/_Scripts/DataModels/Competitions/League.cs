using System.Collections.Generic;
using System;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// Runtime league instance — plain C# class (not ScriptableObject).
/// Created from LeagueTemplate via LeagueTemplate.CreateLeague().
/// Contains full season lifecycle: fixture generation, standings, season completion,
/// promotion/relegation detection.
/// </summary>
public class League : Competition, ISaveable
{
    [JsonIgnore] public LeagueTemplate Template { get; private set; }

    [JsonIgnore] public League PromotionLeague;
    [JsonIgnore] public League RelegationLeague;

    public int PromotionSpots;
    public int PlayoffSpots;
    public int RelegationSpots;

    List<LeagueTableEntry> standings = new List<LeagueTableEntry>();

    /// <summary>Public accessor for serialization of standings.</summary>
    [JsonProperty("Standings")]
    public List<LeagueTableEntry> StandingsData
    {
        get => standings;
        set => standings = value ?? new List<LeagueTableEntry>();
    }

    /// <summary>Parameterless constructor for deserialization.</summary>
    public League() { }

    /// <summary>Constructor used by LeagueTemplate.CreateLeague().</summary>
    public League(LeagueTemplate template, List<Team> teams, DateTime startDate)
    {
        Template = template;
        Name = template.LeagueName;
        Priority = template.Priority;
        PromotionSpots = template.PromotionSpots;
        PlayoffSpots = template.PlayoffSpots;
        RelegationSpots = template.RelegationSpots;

        Initialize(teams, startDate);
    }

    /// <summary>Legacy constructor for compatibility.</summary>
    public League(string name, List<Team> teams, DateTime startDate)
    {
        Name = name;
        Priority = 0;
        Initialize(teams, startDate);
    }

    public void Initialize(List<Team> teams, DateTime startDate)
    {
        this.Teams = teams;
        this.startDate = startDate;

        if (Fixtures == null)
            Fixtures = new List<Fixture>();
        if (Rounds == null)
            Rounds = new List<Fixture>[0];
        if (standings == null)
            standings = new List<LeagueTableEntry>();

        InitializeStandings();
        GenerateFixtures();
    }

    public override void GenerateFixtures()
    {
        if (Fixtures == null)
            Fixtures = new List<Fixture>();

        Fixtures.Clear();

        List<Team> teams = new List<Team>(Teams);

        if (teams.Count % 2 != 0)
            teams.Add(null);

        int numTeams = teams.Count;
        int numRounds = (numTeams - 1) * 2; // double round robin
        int matchesPerRound = numTeams / 2;

        Rounds = new List<Fixture>[numRounds];
        // League games are weekly, on the weekend (Saturday). Rounds advance 7 days each.
        DateTime roundDate = NextDayOfWeek(startDate, DayOfWeek.Saturday);

        // First half
        for (int round = 0; round < numTeams - 1; round++)
        {
            Rounds[round] = new List<Fixture>();
            roundDate = ApplyWinterBreak(roundDate);

            for (int i = 0; i < matchesPerRound; i++)
            {
                Team home = teams[i];
                Team away = teams[numTeams - 1 - i];

                if (home != null && away != null)
                {
                    DateTime matchDate = FindNextAvailableDate(home, away, roundDate);
                    Fixture fixture = new Fixture(home, away, matchDate, this, round);
                    Fixtures.Add(fixture);
                    Rounds[round].Add(fixture);
                }
            }

            // Rotate teams (except first)
            Team last = teams[numTeams - 1];
            teams.RemoveAt(numTeams - 1);
            teams.Insert(1, last);

            roundDate = roundDate.AddDays(7);
        }

        // Second half — reverse home/away, continuing weekly from where the first half ended.
        for (int round = 0; round < numTeams - 1; round++)
        {
            int roundIndex = round + (numTeams - 1);
            Rounds[roundIndex] = new List<Fixture>();
            roundDate = ApplyWinterBreak(roundDate);

            foreach (var firstHalfFixture in Rounds[round])
            {
                Team home = firstHalfFixture.AwayTeam;
                Team away = firstHalfFixture.HomeTeam;

                DateTime matchDate = FindNextAvailableDate(home, away, roundDate);
                Fixture fixture = new Fixture(home, away, matchDate, this, roundIndex);
                Fixtures.Add(fixture);
                Rounds[roundIndex].Add(fixture);
            }

            roundDate = roundDate.AddDays(7);
        }

        base.GenerateFixtures();
    }

    /// <summary>
    /// If a round's Saturday falls in the Christmas/New-Year window (Dec 24 – Jan 1), pushes it to
    /// the first Saturday after the break. Since round dates accumulate, this shifts every following
    /// round too, producing a ~2-week winter break (one skipped weekend, Christmas + New Year off).
    /// </summary>
    private static DateTime ApplyWinterBreak(DateTime saturday)
    {
        int y = saturday.Month == 1 ? saturday.Year - 1 : saturday.Year;
        DateTime breakStart = new DateTime(y, 12, 24);
        DateTime breakEnd = new DateTime(y + 1, 1, 1);

        if (saturday >= breakStart && saturday <= breakEnd)
            return NextDayOfWeek(breakEnd.AddDays(1), DayOfWeek.Saturday); // first Saturday on/after Jan 2

        return saturday;
    }

    public void InitializeStandings()
    {
        standings.Clear();

        foreach (var team in Teams)
        {
            standings.Add(new LeagueTableEntry(team));
        }
    }

    public void UpdateStandings(Fixture fixture)
    {
        var homeTeamEntry = standings.FirstOrDefault(entry => entry.team.TeamId == fixture.HomeTeam.TeamId);
        var awayTeamEntry = standings.FirstOrDefault(entry => entry.team.TeamId == fixture.AwayTeam.TeamId);

        if (homeTeamEntry == null || awayTeamEntry == null)
        {
            Debug.LogError($"Team not found in league standings. Home: {fixture.HomeTeam.Name} (ID:{fixture.HomeTeam.TeamId}), Away: {fixture.AwayTeam.Name} (ID:{fixture.AwayTeam.TeamId})");
            return;
        }

        int homeGoals = fixture.Result.score.home;
        int awayGoals = fixture.Result.score.away;

        homeTeamEntry.goalsFor += homeGoals;
        homeTeamEntry.goalsAgainst += awayGoals;

        awayTeamEntry.goalsFor += awayGoals;
        awayTeamEntry.goalsAgainst += homeGoals;

        if (homeGoals > awayGoals) // Home win
        {
            homeTeamEntry.points += 3;
            homeTeamEntry.wins++;
            awayTeamEntry.losses++;
        }
        else if (homeGoals < awayGoals) // Away win
        {
            awayTeamEntry.points += 3;
            awayTeamEntry.wins++;
            homeTeamEntry.losses++;
        }
        else // Draw
        {
            homeTeamEntry.points += 1;
            awayTeamEntry.points += 1;
            homeTeamEntry.draws++;
            awayTeamEntry.draws++;
        }

        SortStandings();

        // Check if the season is now complete
        CheckSeasonEnd();
    }

    /// <summary>
    /// Checks if all league fixtures have been played, and if so, fires completion events.
    /// </summary>
    public void CheckSeasonEnd()
    {
        if (IsComplete) return;
        if (Fixtures.Any(f => !f.BeenPlayed)) return;

        // All matches played — season is complete
        IsComplete = true;
        SortStandings();

        // Champion
        Team champion = GetChampion();
        if (champion != null)
        {
            Debug.Log($"[League] {Name} champion: {champion.Name}!");
            CompetitionEvents.FireLeagueWon(this, champion);
        }

        // Promotion
        List<Team> promoted = GetPromotedTeams();
        if (promoted.Count > 0)
        {
            Debug.Log($"[League] {Name} promoted: {string.Join(", ", promoted.Select(t => t.Name))}");
            CompetitionEvents.FirePromoted(this, promoted);
        }

        // Relegation
        List<Team> relegated = GetRelegatedTeams();
        if (relegated.Count > 0)
        {
            Debug.Log($"[League] {Name} relegated: {string.Join(", ", relegated.Select(t => t.Name))}");
            CompetitionEvents.FireRelegated(this, relegated);
        }

        CompetitionEvents.FireSeasonComplete(this);
    }

    /// <summary>Returns the league champion (1st in standings).</summary>
    public Team GetChampion()
    {
        return standings.Count > 0 ? standings[0].team : null;
    }

    /// <summary>Returns teams that earned promotion (top N based on PromotionSpots).</summary>
    public List<Team> GetPromotedTeams()
    {
        if (PromotionSpots <= 0) return new List<Team>();
        return standings.Take(PromotionSpots).Select(e => e.team).ToList();
    }

    /// <summary>Returns teams that are relegated (bottom N based on RelegationSpots).</summary>
    public List<Team> GetRelegatedTeams()
    {
        if (RelegationSpots <= 0) return new List<Team>();
        return standings.Skip(standings.Count - RelegationSpots).Select(e => e.team).ToList();
    }

    void SortStandings()
    {
        standings = standings.OrderByDescending(entry => entry.points)
            .ThenByDescending(entry => entry.goalDifference)
            .ThenByDescending(entry => entry.goalsFor)
            .ToList();
    }

    public List<LeagueTableEntry> GetStandings()
    {
        if (standings.Count < 1) InitializeStandings();
        return standings;
    }

    /// <summary>
    /// ISaveable — rebuilds Rounds and resolves Template after deserialization.
    /// </summary>
    public void OnAfterDeserialize()
    {
        BuildRoundsFromFixtures();

        // Resolve LeagueTemplate from Resources by name
        var templates = UnityEngine.Resources.LoadAll<LeagueTemplate>("Competitions/Leagues");
        Template = System.Array.Find(templates, t => t.LeagueName == Name);
    }
}
