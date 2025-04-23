using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(menuName = "Tactic Instruction")]
public class LegacyTacticInstruction : LegacyTacticElement
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

    public TacticStat AddInstruction(LegacyTacticStats stats)
    {
        //Action.Invoke(stats);
        return stat;
    }

    public void HoofItLong(LegacyTacticStats stats)
    {
        stats.ModifyStat(TacticStat.Complexity, -5);
        stats.ModifyStats( (TacticStat.Stability, -10), (TacticStat.Control, -10));
        stats.Dependencies(2, PlayerStat.Height, PlayerStat.Strength);
        stats.Dependencies(1, PlayerStat.Crossing, PlayerStat.Jumping, PlayerStat.Aggression);
        stats.Dependencies(-1, PlayerStat.Dribbling, PlayerStat.Agility, PlayerStat.Intelligence);
        stats.TacticDependencies(-1, TacticStat.Control);
    }
    public void DribbleMore(LegacyTacticStats stats)
    {
        stats.ModifyStat(TacticStat.Provoking, 15);
        stats.Dependencies(-1, PlayerStat.Crossing, PlayerStat.Jumping, PlayerStat.Aggression);
        stats.Dependencies(2, PlayerStat.Dribbling, PlayerStat.Agility, PlayerStat.Intelligence);
    }
    public void RiskyPasses(LegacyTacticStats stats)
    {
        stats.ModifyStat(TacticStat.Creativity, 15);
        stats.ModifyStat(TacticStat.Stability, -10);
        stats.Dependencies(-0.5f, PlayerStat.Teamwork);
        stats.Dependencies(2, PlayerStat.Passing, PlayerStat.Intelligence);
        stats.Dependencies(0.5f, PlayerStat.Agility, PlayerStat.Crossing);
    }
    public void AggressiveTackles(LegacyTacticStats stats)
    {
        stats.ModifyStat(TacticStat.Pressure, 5);
        stats.ModifyStat(TacticStat.Fouling, 10);
        stats.ModifyStat(TacticStat.Intensity, 10);
        stats.Dependencies(3, PlayerStat.Aggression, PlayerStat.Tackling);
        stats.Dependencies(-1, PlayerStat.Composure, PlayerStat.Intelligence);
    }
    public void CounterPress(LegacyTacticStats stats)
    {
        stats.ModifyStat(TacticStat.Complexity, 10);
        stats.ModifyStat(TacticStat.Pressure, 15);
        stats.ModifyStat(TacticStat.Fouling, 5);
        stats.ModifyStat(TacticStat.Security, 5);
        stats.ModifyStat(TacticStat.Threat, 5);
        stats.ModifyStat(TacticStat.Intensity, 10);
        stats.Dependencies(3, PlayerStat.Teamwork, PlayerStat.Intelligence);
        stats.Dependencies(1, PlayerStat.Aggression, PlayerStat.Positioning);
        stats.TacticDependencies(2, TacticStat.Pressure, TacticStat.Control);
    }
    public void CounterAttack(LegacyTacticStats stats)
    {
        stats.ModifyStat(TacticStat.Complexity, 10);
        stats.ModifyStat(TacticStat.Pressure, 15);
        stats.ModifyStat(TacticStat.Fouling, 5);
        stats.ModifyStat(TacticStat.Security, 5);
        stats.ModifyStat(TacticStat.Threat, 5);
        stats.ModifyStat(TacticStat.Intensity, 10);
        stats.Dependencies(3, PlayerStat.Pace, PlayerStat.Teamwork);
        stats.Dependencies(1, PlayerStat.Aggression, PlayerStat.Positioning);
    }

    public void FunnelBallTowards(LegacyTacticStats stats, int position)
    {
        stats.Reliance(position, TacticStat.Threat, 2);
    }
    public void FocusAttackAround(LegacyTacticStats stats, int position)
    {
        stats.ModifyStat(TacticStat.Complexity, 5);
        stats.Reliance(position, TacticStat.Creativity, 2);
        stats.Reliance(position, TacticStat.Threat, 0.5f);
        stats.Reliance(position, TacticStat.Stability, 0.5f);
        stats.Reliance(position, TacticStat.Intensity, 0.5f);
    }
    public void FocusBuildUpAround(LegacyTacticStats stats, int position)
    {
        stats.ModifyStat(TacticStat.Complexity, 5);
        stats.Reliance(position, TacticStat.Creativity, 0.5f);
        stats.Reliance(position, TacticStat.Stability, 2);
        stats.Reliance(position, TacticStat.Control, 1);
        stats.Reliance(position, TacticStat.Intensity, 1);
    }
    
}