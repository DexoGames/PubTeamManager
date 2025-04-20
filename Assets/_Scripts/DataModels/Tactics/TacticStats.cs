using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[Serializable]
public enum TacticStat
{
    Complexity, Intensity, Control, Stability, Pressure, Solidity,
    Threat, Creativity, DefensiveWidth, AttackingWidth, Fouling, Provoking
}
public enum PlayerStat
{
    Shooting, Passing, Tackling, Dribbling, Crossing, Heading,
    Positioning, Intelligence, Teamwork, Composure, Aggression, Resilience,
    Pace, Strength, Jumping, Agility, Stamina, Durability
}

[Serializable]
public class TacticStats
{
    private Dictionary<TacticStat, float> stats = new Dictionary<TacticStat, float>();
    private Dictionary<PlayerStat, float> dependencies = new Dictionary<PlayerStat, float>();
    private Dictionary<PlayerStat, float> tacticDependencies = new Dictionary<PlayerStat, float>();
    private Dictionary<Player.Position, (TacticStat, float)> reliances = new Dictionary<Player.Position, (TacticStat, float)>();

    public TacticStats()
    {
        foreach (TacticStat stat in Enum.GetValues(typeof(TacticStat)))
        {
            stats[stat] = 0f;
        }
    }

    public float GetStat(TacticStat stat) => stats[stat];

    public void ModifyStat(TacticStat stat, float value)
    {
        stats[stat] += value;
    }
    public void ModifyStats(params (TacticStat, float)[] statValues)
    {
        foreach((TacticStat, float) sv in statValues)
        {
            stats[sv.Item1] += sv.Item2;
        }
    }

    public void SetStat(TacticStat stat, float value)
    {
        stats[stat] = value;
    }

    public void Reliance(Player.Position position, TacticStat stat, float strength)
    {
        reliances.Add(position, (stat, strength));
    }
    public void Dependency(PlayerStat playerStat, float strength)
    {
        dependencies.Add(playerStat, strength);
    }
    public void Dependencies(float strength, params PlayerStat[] playerStats)
    {
        foreach(PlayerStat stat in playerStats)
        {
            dependencies.Add(stat, strength);
        }
    }
    public void TacticDependencies(float strength, params TacticStat[] tacticStats)
    {
        foreach (PlayerStat stat in tacticStats)
        {
            tacticDependencies.Add(stat, strength);
        }
    }
}
