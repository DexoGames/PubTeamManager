using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Tactic/Instruction")]
public class TacticInstruction : ScriptableObject
{
    public string tacticName;
    [TextArea] public string description;

    public List<StatModification> statModifications = new();
    public List<TeamDependency> dependencies = new();
    public List<TacticDependency> tacticDependencies = new();
    public List<Reliance> reliances = new();

    public TacticInstruction[] incompatibleInstructions;

    public void Apply(TacticStats stats)
    {
        foreach (var statMod in statModifications)
        {
            stats.ModifyStat(statMod.stat, statMod.value);
        }

        foreach (var rely in reliances)
        {
            stats.Reliance(rely.position, rely.stat, rely.strength);
        }

        foreach (var dependency in dependencies)
        {
            stats.AddTeamDependency(dependency);
        }

        foreach (var dependency in tacticDependencies)
        {
            stats.AddTacticDependency(dependency);
        }
    }

    [Serializable]
    public struct StatModification
    {
        public TacticStat stat;
        public int value;
    }

    [Serializable]
    public struct TeamDependency
    {
        public PlayerStat stat;
        public PlayerGroup group;
        public float strength;
    }

    [Serializable]
    public struct TacticDependency
    {
        public TacticStat stat;
        public int minimumValue;
        public bool inverse;
    }

    [Serializable]
    public struct Reliance
    {
        public int position;
        public TacticStat stat;
        public float strength;
    }
}
