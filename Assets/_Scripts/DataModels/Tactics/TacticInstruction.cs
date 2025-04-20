using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(menuName = "Tactic Instruction")]
public class TacticInstruction : TacticElement
{
    public string Name { get; private set; }
    public bool IsActive { get; private set; }


    TacticStat stat;

    //public TacticInstruction(string name, Action<TacticStats> effect)
    //{
    //    Name = name;
    //    this.effect = effect;
    //    IsActive = false;
    //}

    public void Toggle()
    {
        IsActive = !IsActive;
    }

    public TacticStat AddInstruction(TacticStats stats)
    {
        //Action.Invoke(stats);
        return stat;
    }

    public void HoofItLong(TacticStats stats)
    {
        stats.ModifyStat(TacticStat.Complexity, -5);
        stats.ModifyStats( (TacticStat.Stability, -10), (TacticStat.Control, -10));
        stats.Dependencies(1, PlayerStat.Crossing, PlayerStat.Jumping, PlayerStat.Strength, PlayerStat.Aggression);
        stats.Dependencies(-1, PlayerStat.Dribbling, PlayerStat.Agility, PlayerStat.Intelligence);
    }
    public void DribbleMore(TacticStats stats)
    {
        stats.ModifyStat(TacticStat.Complexity, 5);
        stats.ModifyStats((TacticStat.Provoking, 15));
        stats.Dependencies(-1, PlayerStat.Crossing, PlayerStat.Jumping, PlayerStat.Aggression);
        stats.Dependencies(1, PlayerStat.Dribbling, PlayerStat.Agility, PlayerStat.Intelligence);
    }
}