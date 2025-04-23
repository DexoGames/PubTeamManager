using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class TacticStats
{
    public int Complexity = 50;
    public int Intensity = 50;
    public int Control = 50;
    public int Stability = 50;
    public int Pressure = 50;
    public int Security = 50;
    public int Threat = 50;
    public int Creativity = 50;
    public int DefensiveWidth = 50;
    public int AttackingWidth = 50;
    public int Fouling = 50;
    public int Provoking = 50;

    private List<TacticEffect.TeamDependency> TeamDependencies = new();
    private List<TacticEffect.TacticDependency> TacticDependencies = new();
    private Dictionary<(int, TacticStat), float> Reliances = new();

    public ref int GetStatRef(TacticStat stat)
    {
        switch (stat)
        {
            case TacticStat.Complexity: return ref Complexity;
            case TacticStat.Intensity: return ref Intensity;
            case TacticStat.Control: return ref Control;
            case TacticStat.Stability: return ref Stability;
            case TacticStat.Pressure: return ref Pressure;
            case TacticStat.Security: return ref Security;
            case TacticStat.Threat: return ref Threat;
            case TacticStat.Creativity: return ref Creativity;
            case TacticStat.DefensiveWidth: return ref DefensiveWidth;
            case TacticStat.AttackingWidth: return ref AttackingWidth;
            case TacticStat.Fouling: return ref Fouling;
            case TacticStat.Provoking: return ref Provoking;
            default: throw new ArgumentOutOfRangeException(nameof(stat), stat, null);
        }
    }


    public int GetStat(TacticStat stat) => GetStatRef(stat);

    public void ModifyStat(TacticStat stat, int value)
    {
        ref int s = ref GetStatRef(stat);
        s += value;
    }

    public void ModifyStats(params (TacticStat, int)[] statValues)
    {
        foreach (var (stat, value) in statValues)
        {
            ModifyStat(stat, value);
        }
    }

    public void SetStat(TacticStat stat, int value)
    {
        ref int s = ref GetStatRef(stat);
        s = value;
    }

    public void AddTeamDependency(TacticEffect.TeamDependency dependency)
    {
        TeamDependencies.Add(dependency);
    }

    public void AddTacticDependency(TacticEffect.TacticDependency dependency)
    {
        TacticDependencies.Add(dependency);
    }

    public void Reliance(int position, TacticStat stat, float strength)
    {
        var key = (position, stat);
        if (Reliances.TryGetValue(key, out float existing))
        {
            Reliances[key] = existing + strength;
        }
        else
        {
            Reliances[key] = strength;
        }
    }

    public void TacticDependencyModifier()
    {
        var dependencyMap = new Dictionary<TacticStat, TacticEffect.TacticDependency>();

        foreach (var dependency in TacticDependencies)
        {
            if (dependencyMap.TryGetValue(dependency.stat, out var existing))
            {
                if (dependency.minimumValue > existing.minimumValue)
                {
                    dependencyMap[dependency.stat] = dependency;
                }
            }
            else
            {
                dependencyMap[dependency.stat] = dependency;
            }
        }

        foreach (var dependency in dependencyMap.Values)
        {
            ref int stat = ref GetStatRef(dependency.stat);
            if (stat < dependency.minimumValue)
            {
                stat -= (dependency.minimumValue - stat);
            }
        }
    }

    public Dictionary<TacticStat, int> ToDictionary()
    {
        var dict = new Dictionary<TacticStat, int>();
        foreach (TacticStat stat in Enum.GetValues(typeof(TacticStat)))
        {
            dict[stat] = GetStat(stat);
        }
        return dict;
    }
}
