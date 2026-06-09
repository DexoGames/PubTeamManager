using System;
using System.Collections.Generic;
using Newtonsoft.Json;

/// <summary>
/// Root of the cross-season "core" save file (core.json). Holds everything that persists
/// across seasons: date, ID counters, teams (with players + ClubStats), free agents, events,
/// training, the competition-series lineage, and an index of which seasons exist on disk.
///
/// Per-season competitions/fixtures live in separate season files (see SeasonSaveState),
/// so the core file stays roughly constant in size as years pass.
/// </summary>
[System.Serializable]
public class CoreSaveState
{
    public DateTime CurrentDay;
    public int PlayerTeamId;

    /// <summary>Per-type ID allocator counters — re-seed IdManager on load.</summary>
    public int NextPersonId;
    public int NextTeamId;
    public int NextCompetitionId;
    public int NextFixtureId;
    public int NextSeriesId;

    /// <summary>All teams — serialized inline, with Players and Manager owned by each Team.</summary>
    [JsonProperty(ItemConverterType = typeof(TeamConverter))]
    public List<Team> Teams = new List<Team>();

    /// <summary>Free agents not on any team.</summary>
    public List<Player> FreeAgents = new List<Player>();

    /// <summary>Active events for the player's team.</summary>
    public List<Event> Events = new List<Event>();

    /// <summary>The player's ongoing training session (repeats every training day).</summary>
    public TrainingSession CurrentTraining;

    /// <summary>Competition lineages linking each season's instance across years.</summary>
    public List<CompetitionSeries> CompetitionSeries = new List<CompetitionSeries>();

    /// <summary>The season currently in play (its competitions live in season_&lt;year&gt;.json).</summary>
    public int CurrentSeasonYear;

    /// <summary>Index of every season that has a file on disk (current + archived).</summary>
    public List<SeasonIndexEntry> Seasons = new List<SeasonIndexEntry>();
}

/// <summary>
/// One season file (season_&lt;Year&gt;.json): the competitions (leagues + cup) for that
/// season, with their fixtures and standings serialized inline. Written once when a season
/// completes (archived) and rewritten each save only while it is the current season.
/// </summary>
[System.Serializable]
public class SeasonSaveState
{
    public int Year;
    public List<Competition> Competitions = new List<Competition>();
}

/// <summary>Lightweight index entry letting the core file know which season files exist
/// and which competition IDs belong to each year (for lazy history loading).</summary>
[System.Serializable]
public class SeasonIndexEntry
{
    public int Year;
    public List<int> CompetitionIds = new List<int>();

    public SeasonIndexEntry() { }

    public SeasonIndexEntry(int year, List<int> competitionIds)
    {
        Year = year;
        CompetitionIds = competitionIds ?? new List<int>();
    }
}
