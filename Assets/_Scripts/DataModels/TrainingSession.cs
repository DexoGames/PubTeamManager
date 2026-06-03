using System.Collections.Generic;
using UnityEngine;

public enum TrainingType
{
    Technical,
    Mental,
    Physical,
    Tactical,
    Social,
    Positional
}

/// <summary>
/// Represents a training session that can be executed on a team.
/// Different types have different effects on players.
/// </summary>
[System.Serializable]
public class TrainingSession
{
    public TrainingType Type;
    public PlayerStat? TargetStat;                  // for StatBoost
    public Player.Position? TargetPosition;          // for Positional
    public List<Player> SelectedPlayers;              // for Positional
    public string Description;

    /// <summary>Base effect magnitude for stat changes.</summary>
    private const float BASE_STAT_BOOST = 2f;
    private const float BASE_FAMILIARITY_GAIN = 8f;
    private const float BASE_POSITIONAL_GAIN = 5f;
    private const float POSITIONAL_DIMINISHING_EXPONENT = 0.35f;

    public TrainingSession() { }

    public TrainingSession(TrainingType type, string description)
    {
        Type = type;
        Description = description;
    }

    /// <summary>
    /// Executes the training session, applying effects to the team's players.
    /// </summary>
    public void Execute(Team team)
    {
        switch (Type)
        {
            case TrainingType.Technical:
                ExecuteStatBoost(team);
                break;
            case TrainingType.Mental:
                ExecuteStatBoost(team);
                break;
            case TrainingType.Physical:
                ExecuteStatBoost(team);
                break;
            case TrainingType.Tactical:
                ExecuteTacticFamiliarity(team);
                break;
            case TrainingType.Social:
                ExecuteSocial(team);
                break;
            case TrainingType.Positional:
                ExecutePositional();
                break;
        }
    }

    private void ExecuteStatBoost(Team team)
    {
        if (!TargetStat.HasValue) return;

        foreach (var player in team.StartingPlayers)
        {
            float boost = Random.Range(0.5f, BASE_STAT_BOOST);
            player.ModifyStat(TargetStat.Value, boost);
        }
    }

    private void ExecuteTacticFamiliarity(Team team)
    {
        foreach (var player in team.StartingPlayers)
        {
            // Intelligence scales the gain: 50 = average (1x), 80 = 1.6x, 20 = 0.4x
            float intelligenceMultiplier = player.GetStats().Intelligence / 50f;
            float gain = BASE_FAMILIARITY_GAIN * intelligenceMultiplier;
            player.TacticFamiliarity = Mathf.Clamp(player.TacticFamiliarity + gain, 0f, 100f);
        }
    }

    private void ExecuteSocial(Team team)
    {
        foreach (var player in team.StartingPlayers)
        {
            // Social training pushes morale towards a healthy baseline
            float currentMorale = player.Morale.Mood;
            float targetMorale = 65f;
            float shift = (targetMorale - currentMorale) * 0.15f + Random.Range(-2f, 5f);
            player.Morale.Mood = Mathf.Clamp(player.Morale.Mood + (int)shift, 0, 100);
            player.Morale.Passion = Mathf.Clamp(player.Morale.Passion + Random.Range(0, 4), 0, 100);
        }
    }

    private void ExecutePositional()
    {
        if (!TargetPosition.HasValue || SelectedPlayers == null || SelectedPlayers.Count == 0) return;

        int count = SelectedPlayers.Count;

        // Non-linear diminishing returns: 1/(count^0.35)
        // 1 player = 1.0x, 2 players = 0.78x, 3 = 0.68x, 5 = 0.57x, 8 = 0.48x
        float effectMultiplier = 1f / Mathf.Pow(count, POSITIONAL_DIMINISHING_EXPONENT);

        foreach (var player in SelectedPlayers)
        {
            float gain = BASE_POSITIONAL_GAIN * effectMultiplier * Random.Range(0.7f, 1.3f);
            player.ImprovePositionalStrength(TargetPosition.Value, gain);
        }
    }

    /// <summary>
    /// Gets a description of the training's expected effectiveness.
    /// </summary>
    public string GetEffectivenessDescription()
    {
        if (Type != TrainingType.Positional || SelectedPlayers == null) return "";

        int count = SelectedPlayers.Count;
        float multiplier = 1f / Mathf.Pow(count, POSITIONAL_DIMINISHING_EXPONENT);
        return $"{count} player{(count > 1 ? "s" : "")} selected — {multiplier * 100f:F0}% effectiveness per player";
    }
}
