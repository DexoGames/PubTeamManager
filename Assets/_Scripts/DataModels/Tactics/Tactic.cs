using System;
using System.Collections.Generic;
using System.Linq;
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

    public List<TacticInstruction.TeamDependency> Dependencies { get; private set; } = new();
    public List<TacticInstruction.Reliance> Reliances { get; private set; } = new();

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
        Instructions = manager.ManStats.Instructions.ToList();

        if (Team.TeamName != "Man Utd")
        {
            ResetInstructions();
            Instructions.Add(Resources.Load<TacticInstruction>("Tactics/Instructions/HoofItLong"));
            Instructions.Add(Resources.Load<TacticInstruction>("Tactics/Instructions/LowBlock"));
        }

        RecalculateStats();
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

    public void ResetInstructions()
    {
        Instructions.Clear();
    }

    private void ResetStats()
    {
        Complexity = Intensity = Control = Stability = Pressure = Security =
        Threat = Creativity = DefensiveWidth = AttackingWidth = Fouling = Provoking = 50;
    }

    public int GetStat(TacticStat stat) => GetStatRef(stat);

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

        List<TacticInstruction.TacticDependency> tacticDependencies = new();

        foreach (var instruction in Instructions)
        {
            foreach (var mod in instruction.statModifications)
            {
                ModifyStat(mod.stat, mod.value);
                //if (mod.stat == TacticStat.Stability) Debug.Log($"Stability now {Stability}");
            }

            foreach(var tacDep in instruction.tacticDependencies)
            {
                tacticDependencies.Add(tacDep);
            }
        }

        ApplyTacticDependencies(tacticDependencies);

        //Debug.Log("NEW TACTIC STATS");
        //foreach(TacticStat stat in Enum.GetValues(typeof(TacticStat)))
        //{
        //    Debug.Log($"{stat.ToString()}: {GetStat(stat)}");
        //}
    }

    private void ApplyTacticDependencies(List<TacticInstruction.TacticDependency> tacticDependencies)
    {
        Dictionary<(TacticStat stat, bool inverse), TacticInstruction.TacticDependency> dependencyMap = new();

        foreach (var dep in tacticDependencies)
        {
            var key = (dep.stat, dep.inverse);

            if (dependencyMap.TryGetValue(key, out var existing))
            {
                if (dep.inverse)
                {
                    if (dep.minimumValue < existing.minimumValue)
                    {
                        dependencyMap[key] = dep;
                    }
                }
                else
                {
                    if (dep.minimumValue > existing.minimumValue)
                    {
                        dependencyMap[key] = dep;
                    }
                }
            }
            else
            {
                dependencyMap[key] = dep;
            }
        }

        List<TacticInstruction.TacticDependency> sortedList = dependencyMap.Values.OrderBy(d => d.inverse ? 0 : 1).ToList();

        foreach (var dep in sortedList)
        {
            ref int stat = ref GetStatRef(dep.stat);
            if (stat < dep.minimumValue && !dep.inverse)
            {
                //Debug.Log($"minimum for {dep.stat.ToString()} not reached, gone from {stat}");
                stat -= (dep.minimumValue - stat);
                //Debug.Log($"to {stat}");
            }
            if (dep.inverse && stat > dep.minimumValue)
            {
                //Debug.Log($"maximum for {dep.stat.ToString()} exceeded, gone from {stat}");
                stat = stat + (dep.minimumValue - stat);
                //Debug.Log($"to {stat}");
            }
        }
    }

}
