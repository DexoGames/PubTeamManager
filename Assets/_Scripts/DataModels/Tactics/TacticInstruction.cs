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
    [Tooltip("Fill in the reliance's Eligible Groups to make this a reliance instruction (leave them empty for a " +
             "plain instruction). Toggling a reliance on in the tactics screen prompts you to pick the reliant " +
             "player from the eligible members of your starting XI; that player's chosen stats then sway the team more.")]
    public Reliance reliance;

    /// <summary>
    /// Whether this is a reliance instruction — derived from whether the reliance lists any
    /// <see cref="Reliance.eligibleGroups"/>. No separate flag to keep in sync: an empty groups list is a plain
    /// instruction, any group makes it a reliance.
    /// </summary>
    public bool hasReliance => reliance.eligibleGroups != null && reliance.eligibleGroups.Length > 0;

    [Header("Synergies")]
    [Tooltip("Instructions that clash with this one (handled elsewhere — can't both be active).")]
    public TacticInstruction[] incompatibleInstructions;

    [Tooltip("Instructions that work WELL with this one. When a listed PARTNER instruction is ALSO active, this " +
             "instruction grants the partner's listed tactic-stat perks. One-directional: list the synergy on both " +
             "instructions if you want each to perk the other.")]
    public ComplementaryInstruction[] complementaryInstructions;

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
    /// A synergy: when <see cref="instruction"/> is also active, the owning instruction adds these
    /// <see cref="perks"/> (tactic-stat bonuses) on top of its normal effect.
    /// </summary>
    [Serializable]
    public struct ComplementaryInstruction
    {
        [Tooltip("The partner instruction that must ALSO be active for the perk to apply.")]
        public TacticInstruction instruction;
        [Tooltip("Tactic-stat bonuses granted while the partner is also active (each: a TacticStat + amount).")]
        public List<StatModification> perks;
    }

    /// <summary>
    /// Makes the instruction rely on one chosen player: that player's <see cref="stats"/> count
    /// <see cref="multiplier"/>× more toward the team's effective ability in the groups he plays in — so his
    /// strengths AND weaknesses in those stats are amplified. Which player is chosen at runtime (the picker),
    /// stored as a squad slot so it follows substitutions.
    /// </summary>
    [Serializable]
    public struct Reliance
    {
        [Tooltip("Which of the reliant player's stats count more toward the team's effective ability.")]
        public PlayerStat[] stats;
        [Tooltip("Extra weight on those stats: 1 = the reliant player counts double for them, 2 = triple, …")]
        public float multiplier;
        [Tooltip("Position groups the reliant player must belong to (union of the listed groups). This list is what " +
                 "MAKES the instruction a reliance: leave it empty for a plain instruction. Otherwise it filters the " +
                 "picker, and if he later moves out of all of them the instruction auto-disables. " +
                 "E.g. a crosses reliance: WidePlayers + Attackers (no centre-backs/keepers).")]
        public PlayerGroup[] eligibleGroups;
        [Tooltip("How hard a CHANGE of the reliant player (a sub/swap) dents familiarity. 0 = no effect; 1 = the " +
                 "instruction is fully un-drilled and must be re-learned.")]
        [Range(0f, 1f)] public float familiarityPenalty;
    }
}
