using System;
using System.Collections.Generic;
using Newtonsoft.Json;

/// <summary>
/// Root serialization container — replaces the old SaveData + DTOs.
/// All fields serialize directly via Newtonsoft.Json attributes on the models themselves.
/// </summary>
[System.Serializable]
public class GameState
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

    /// <summary>All competitions (current and past seasons) — fixtures owned inline.</summary>
    public List<Competition> Competitions = new List<Competition>();

    /// <summary>Competition lineages linking each season's instance across years.</summary>
    public List<CompetitionSeries> CompetitionSeries = new List<CompetitionSeries>();

    /// <summary>Free agents not on any team.</summary>
    public List<Player> FreeAgents = new List<Player>();

    /// <summary>Active events for the player's team.</summary>
    public List<Event> Events = new List<Event>();
}
