using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>How a group distribution is reduced to one phase value.</summary>
public enum StatAggregation
{
    Mean,
    Cohesive,
    StarInfluenced,
    WeakLinkInfluenced,
    BestPairInfluenced,
    WeakestPairInfluenced
}

/// <summary>
/// A phase-ready view of a player group. It applies the effective-stat pipeline once, including tactical
/// complexity, chemistry and reliance, then exposes several distribution shapes instead of flattening every
/// stat to the same arithmetic mean.
/// </summary>
public sealed class SquadStatProfile
{
    private const float COHESIVE_MEDIAN_SHARE = 0.4f;
    private const float OUTLIER_SHARE = 0.35f;
    private const float PAIR_SHARE = 0.45f;

    private readonly List<Sample>[] skillSamples = new List<Sample>[Player.SKILL_NO];
    private readonly List<Sample> heightSamples = new List<Sample>();

    private readonly struct Sample
    {
        public readonly float Value;
        public readonly float Weight;

        public Sample(float value, float weight)
        {
            Value = value;
            Weight = weight;
        }
    }

    public SquadStatProfile(IEnumerable<Player> players, Tactic tactic = null, PlayerGroup? phaseGroup = null)
    {
        for (int i = 0; i < skillSamples.Length; i++)
            skillSamples[i] = new List<Sample>();

        if (players == null) return;

        foreach (Player player in players.Where(p => p != null))
        {
            Player.Stats stats = BuildEffectiveStats(player, tactic);
            bool reliant = tactic != null && tactic.IsReliantPlayer(player, phaseGroup);

            for (int i = 0; i < Player.SKILL_NO; i++)
            {
                PlayerStat stat = (PlayerStat)i;
                float weight = reliant ? 1f + tactic.RelianceBonus(player, stat, phaseGroup) : 1f;
                skillSamples[i].Add(new Sample(stats.Skills[i], weight));
            }

            float heightWeight = reliant
                ? 1f + tactic.RelianceBonus(player, PlayerStat.Height, phaseGroup)
                : 1f;
            heightSamples.Add(new Sample(stats.Height, heightWeight));
        }
    }

    public float Get(PlayerStat stat, StatAggregation aggregation = StatAggregation.Cohesive)
    {
        List<Sample> samples = stat == PlayerStat.Height
            ? heightSamples
            : (int)stat >= 0 && (int)stat < Player.SKILL_NO
                ? skillSamples[(int)stat]
                : null;

        if (samples == null || samples.Count == 0) return 0f;

        float mean = WeightedMean(samples);
        if (aggregation == StatAggregation.Mean) return mean;

        float cohesive = Mathf.Lerp(mean, WeightedMedian(samples), COHESIVE_MEDIAN_SHARE);
        switch (aggregation)
        {
            case StatAggregation.StarInfluenced:
                return Mathf.Lerp(cohesive, samples.Max(s => s.Value), OUTLIER_SHARE);
            case StatAggregation.WeakLinkInfluenced:
                return Mathf.Lerp(cohesive, samples.Min(s => s.Value), OUTLIER_SHARE);
            case StatAggregation.BestPairInfluenced:
                return Mathf.Lerp(cohesive, PairAverage(samples, true), PAIR_SHARE);
            case StatAggregation.WeakestPairInfluenced:
                return Mathf.Lerp(cohesive, PairAverage(samples, false), PAIR_SHARE);
            default:
                return cohesive;
        }
    }

    /// <summary>Compatibility view for UI/team composites that still expect one Stats struct.</summary>
    public Player.Stats ToAverageStats()
    {
        var result = new Player.Stats
        {
            Skills = new int[Player.SKILL_NO],
            Positions = new Dictionary<Player.Position, Player.PositionStrength>(),
            Height = Mathf.RoundToInt(Get(PlayerStat.Height, StatAggregation.Mean))
        };

        for (int i = 0; i < Player.SKILL_NO; i++)
            result.Skills[i] = Mathf.RoundToInt(Get((PlayerStat)i, StatAggregation.Mean));

        return result;
    }

    private static Player.Stats BuildEffectiveStats(Player player, Tactic tactic)
    {
        Player.Stats stats = player.GetStats();

        if (tactic != null && tactic.ShouldApplyComplexityPenalty)
        {
            float reduction = tactic.ComplexityPenaltyFraction(stats.Intelligence);
            if (reduction > 0f)
            {
                foreach (PlayerStat stat in Tactic.ComplexityAffectedStats)
                    stats.SetStat(stat, Mathf.RoundToInt(stats.GetStat(stat) * (1f - reduction)));
            }
        }

        if (tactic != null)
        {
            for (int i = 0; i < Player.SKILL_NO; i++)
            {
                int delta = tactic.ChemistryDelta(player, (PlayerStat)i);
                if (delta != 0) stats.Skills[i] = Mathf.Clamp(stats.Skills[i] + delta, 0, 100);
            }
        }

        return stats;
    }

    private static float WeightedMean(List<Sample> samples)
    {
        float total = 0f;
        float weight = 0f;
        foreach (Sample sample in samples)
        {
            total += sample.Value * sample.Weight;
            weight += sample.Weight;
        }
        return weight > 0f ? total / weight : 0f;
    }

    private static float WeightedMedian(List<Sample> samples)
    {
        List<Sample> ordered = samples.OrderBy(s => s.Value).ToList();
        float halfWeight = ordered.Sum(s => s.Weight) * 0.5f;
        float running = 0f;
        for (int i = 0; i < ordered.Count; i++)
        {
            Sample sample = ordered[i];
            running += sample.Weight;
            if (Mathf.Approximately(running, halfWeight) && i + 1 < ordered.Count)
                return (sample.Value + ordered[i + 1].Value) * 0.5f;
            if (running > halfWeight) return sample.Value;
        }
        return ordered[ordered.Count - 1].Value;
    }

    private static float PairAverage(List<Sample> samples, bool best)
    {
        IEnumerable<float> ordered = best
            ? samples.Select(s => s.Value).OrderByDescending(v => v)
            : samples.Select(s => s.Value).OrderBy(v => v);
        float[] pair = ordered.Take(2).ToArray();
        return pair.Length == 0 ? 0f : pair.Average();
    }
}
