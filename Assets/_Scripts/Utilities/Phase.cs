using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Phase
{
    public class Parameters
    {
        public List<(float value, float weight)> AttackingStats { get; set; }
        public List<(float value, float weight)> AttackingTactics { get; set; }
        public List<(float value, float weight)> DefendingStats { get; set; }
        public List<(float value, float weight)> DefendingTactics { get; set; }

        public float TacticExponent { get; set; } = 1f;
        public float AbilityExponent { get; set; } = 0.75f;
        public float RandomnessBonus { get; set; } = 0f;
        public string PhaseName { get; set; } = "Unknown";

        public float SuccessThreshold { get; set; } = 5f;
        public float FailThreshold { get; set; } = -5f;
    }

    public enum Type
    {
        Build, Progress, Probe, Advance, Penetrate, Counter, Break
    }
}