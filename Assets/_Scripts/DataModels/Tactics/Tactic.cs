using System.Collections.Generic;
using UnityEngine;
using static Game;

public enum TacticStat
{
    Complexity, Intensity, Control, Stability, Pressure, Security,
    Threat, Creativity, DefensiveWidth, AttackingWidth, Fouling, Provoking
}

public class Tactic
{
    public Team Team { get; private set; }
    public Manager Manager { get; private set; }
    public Formation Formation { get; private set; }
    public List<TacticInstruction> Instructions { get; private set; } = new();

    // Stat fields
    public int Complexity;
    public int Intensity;
    public int Control;
    public int Stability;
    public int Pressure;
    public int Security;
    public int Threat;
    public int Creativity;
    public int DefensiveWidth;
    public int AttackingWidth;
    public int Fouling;
    public int Provoking;

    public Tactic(Team team, Manager manager)
    {
        Manager = manager;
        Team = team;
        Formation = manager.ManStats.Formation;
        ResetStats();
    }

    public void SetFormation(Formation newFormation)
    {
        Formation = newFormation;
    }

    public void AddInstruction(TacticInstruction instruction)
    {
        Instructions.Add(instruction);
        RecalculateStats();
    }

    private void ResetStats()
    {
        Complexity = Intensity = Control = Stability = Pressure = Security =
        Threat = Creativity = DefensiveWidth = AttackingWidth = Fouling = Provoking = 50;
    }

    private ref int GetStatRef(TacticStat stat)
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
            default: throw new System.ArgumentOutOfRangeException(nameof(stat), stat, null);
        }
    }

    private void ModifyStat(TacticStat stat, int value)
    {
        ref int s = ref GetStatRef(stat);
        s += value;
    }

    private void RecalculateStats()
    {
        ResetStats();
        foreach (var instruction in Instructions)
        {
            foreach (var mod in instruction.effect.statModifications)
            {
                ModifyStat(mod.stat, mod.value);
            }
        }
        Debug.Log("NEW TACTIC STATS");
        Debug.Log($"Intensity: {Intensity}");
        Debug.Log($"Control: {Control}");
        Debug.Log($"Threat: {Threat}");
    }

    public int GetStat(TacticStat stat) => GetStatRef(stat);
}
