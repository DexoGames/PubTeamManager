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

    [Header("Reliance (optional) — this instruction leans on ONE chosen player")]
    [Tooltip("Tick to make this a reliance instruction. Toggling it on in the tactics screen prompts you to " +
             "pick the reliant player from your starting XI; that player's chosen stats then sway the team more.")]
    public bool hasReliance;
    public Reliance reliance;

    public TacticInstruction[] incompatibleInstructions;

    public void Apply(TacticStats stats)
    {
        foreach (var statMod in statModifications)
            stats.ModifyStat(statMod.stat, statMod.value);

        foreach (var dependency in dependencies)
            stats.AddTeamDependency(dependency);

        foreach (var dependency in tacticDependencies)
            stats.AddTacticDependency(dependency);
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

    /// <summary>
    /// Makes the instruction rely on one chosen player: that player's <see cref="stats"/> count
    /// <see cref="multiplier"/>× more toward the team's effective ability in the groups he plays in — so his
    /// strengths AND weaknesses in those stats are amplified. Which player is chosen at runtime (the picker).
    /// </summary>
    [Serializable]
    public struct Reliance
    {
        [Tooltip("Which of the reliant player's stats count more toward the team's effective ability.")]
        public PlayerStat[] stats;
        [Tooltip("Extra weight on those stats: 1 = the reliant player counts double for them, 2 = triple, …")]
        public float multiplier;
    }
}
