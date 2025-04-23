using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[Serializable]
public class LegacyTacticStats
{
    private Dictionary<TacticStat, float> stats = new Dictionary<TacticStat, float>();
    private Dictionary<PlayerStat, float> dependencies = new Dictionary<PlayerStat, float>();
    private Dictionary<PlayerStat, float> tacticDependencies = new Dictionary<PlayerStat, float>();
    private Dictionary<(int, TacticStat), float> reliances = new Dictionary<(int, TacticStat), float>();

    public LegacyTacticStats()
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
            ModifyStat(sv.Item1, sv.Item2);
        }
    }

    public void SetStat(TacticStat stat, float value)
    {
        stats[stat] = value;
    }

    public void Reliance(int position, TacticStat stat, float strength)
    {
        reliances[(position, stat)] = strength + reliances.GetValueOrDefault((position, stat), 0);
    }

    public void Dependency(PlayerStat playerStat, float strength)
    {
        dependencies[playerStat] = strength + dependencies.GetValueOrDefault(playerStat, 0);
    }
    public void Dependencies(float strength, params PlayerStat[] playerStats)
    {
        foreach(PlayerStat stat in playerStats)
        {
            dependencies[stat] = strength + dependencies.GetValueOrDefault(stat, 0);
        }
    }

    public void TacticDependencies(float strength, params TacticStat[] tacticStats)
    {
        foreach (PlayerStat stat in tacticStats)
        {
            tacticDependencies[stat] = strength + tacticDependencies.GetValueOrDefault(stat, 0);
        }
    }
}
