using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public abstract class LegacyTacticElement : ScriptableObject
{
    public int ID;
    public List<string> dependencies;
    public UnityEvent<LegacyTacticStats> Action;

    public enum Dependency
    {
        StrongNegative=-3, Negative, WeakNeagtive, Neutral, WeakPositive, Positive, StrongPositive
    }
}