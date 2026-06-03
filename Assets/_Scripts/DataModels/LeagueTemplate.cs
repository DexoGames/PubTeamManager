using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject blueprint for league configuration.
/// Placed in Resources/Competitions/Leagues as editor assets.
/// Runtime League instances are created from these templates.
/// </summary>
[CreateAssetMenu(fileName = "New League", menuName = "Competition/League")]
public class LeagueTemplate : ScriptableObject
{
    public string LeagueName;
    public int Priority = 0;
    public int PromotionSpots = 2;
    public int PlayoffSpots = 0;
    public int RelegationSpots = 2;

    /// <summary>Reference to the league template teams get promoted to.</summary>
    public LeagueTemplate PromotionLeagueTemplate;

    /// <summary>Reference to the league template teams get relegated to.</summary>
    public LeagueTemplate RelegationLeagueTemplate;

    /// <summary>Creates a runtime League instance from this template.</summary>
    public League CreateLeague(List<Team> teams, System.DateTime startDate)
    {
        return new League(this, teams, startDate);
    }
}
