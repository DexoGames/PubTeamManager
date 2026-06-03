using System;
using System.Collections.Generic;

/// <summary>
/// Static event system for competition outcomes.
/// Any system can subscribe to these events to react to league/cup results.
/// </summary>
public static class CompetitionEvents
{
    /// <summary>Fired when a league season ends and a champion is determined.</summary>
    public static event Action<League, Team> OnLeagueWon;

    /// <summary>Fired when teams are promoted from a league.</summary>
    public static event Action<League, List<Team>> OnPromoted;

    /// <summary>Fired when teams are relegated from a league.</summary>
    public static event Action<League, List<Team>> OnRelegated;

    /// <summary>Fired when a cup final is won.</summary>
    public static event Action<Cup, Team> OnCupWon;

    /// <summary>Fired when any competition finishes its season/tournament.</summary>
    public static event Action<Competition> OnSeasonComplete;

    public static void FireLeagueWon(League league, Team champion)
    {
        OnLeagueWon?.Invoke(league, champion);
    }

    public static void FirePromoted(League league, List<Team> teams)
    {
        OnPromoted?.Invoke(league, teams);
    }

    public static void FireRelegated(League league, List<Team> teams)
    {
        OnRelegated?.Invoke(league, teams);
    }

    public static void FireCupWon(Cup cup, Team winner)
    {
        OnCupWon?.Invoke(cup, winner);
    }

    public static void FireSeasonComplete(Competition competition)
    {
        OnSeasonComplete?.Invoke(competition);
    }
}
