using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Game;

public class Tactic
{
    public Team Team { get; private set; }
    public Manager Manager { get; private set; }
    public Formation Formation { get; private set; }
    public List<TacticInstruction> Instructions { get; private set; }
    public List<TacticSlider> Sliders { get; private set; }
    public TacticStats Stats { get; private set; }

    public int Control => (int)Average(Team.AvgControl, Formation.Stats.Control);
    public int Threat => (int)WeightedAverage((Team.AvgAttacking, 2), (Formation.Stats.Threat, 1));
    public int Security => (int)WeightedAverage((Team.AvgDefending, 2), (Formation.Stats.Security, 1));

    public Tactic(Team team, Manager manager)
    {
        Manager = manager;
        Team = team;
        Formation = manager.ManStats.Formation;
        Instructions = new List<TacticInstruction>();
        Sliders = new List<TacticSlider>();
    }

    public void SetFormation(Formation newFormation)
    {
        Formation = newFormation;
    }
    public void AddInstruction(TacticInstruction instruction)
    {
        Instructions.Add(instruction);
    }

    public void ToggleInstruction(TacticInstruction instruction)
    {
        if (instruction != null)
        {
            instruction.Toggle();
        }
    }

}