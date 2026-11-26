using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Game;

public class MatchEngine
{
    private readonly Match match;

    public MatchEngine(Match match)
    {
        this.match = match;
    }

    // moved to Engine (and everything it calls)
    public void SimulateMinute()
    {
        float possession = CalculateHomePossession(match.HomeTeam, match.AwayTeam);

        Team attackingTeam = Random.Range(0f, 1f) < possession ? match.HomeTeam : match.AwayTeam;
        Team defendingTeam = attackingTeam == match.HomeTeam ? match.AwayTeam : match.HomeTeam;

        if (attackingTeam.OnPitchPlayers.Count > 0 && defendingTeam.OnPitchPlayers.Count > 0)
            PlayPossession(attackingTeam, defendingTeam);

        do
        {
            if (match.currentMin.Stoppage == 0 && match.currentMin.Base % 10 == 0)
            {
                foreach (Player player in match.HomeTeam.OnPitchPlayers) player.AccumulateFatigue(match.HomeTeam.Intensity);
                foreach (Player player in match.AwayTeam.OnPitchPlayers) player.AccumulateFatigue(match.AwayTeam.Intensity);
            }
            match.currentMin = AdvanceMinute(match.currentMin);
        }
        while (Random.Range(0, 4) == 0);
    }

    // ————————————————————— Fouls, cards & injuries —————————————————————

    /// <summary>
    /// Rolls a possible foul by <paramref name="committing"/> on <paramref name="fouled"/>, scaled by the
    /// committing side's Fouling instruction and the fouled side's Provoking. A foul may carry a card
    /// (yellow/red) and/or injure the victim — the in-match source of suspensions and injuries.
    /// </summary>
    bool TryFoul(Team committing, Team fouled, Phase.Type phase)
    {
        float chance = 0.007f + committing.Fouling / 8000f + fouled.Provoking / 12000f;
        if (Random.value > chance) return false;

        var defenders = committing.OnPitchPlayers;
        var attackers = fouled.OnPitchPlayers;
        Player offender = defenders[Random.Range(0, defenders.Count)];
        Player victim = attackers[Random.Range(0, attackers.Count)];
        Card card = DecideCard(committing, offender);
        InjuryType injury = DecideInjury(card, victim);
        match.RecordFoul(card, offender, victim, injury, match.currentMin);
        offender.SentOff = card == Card.Red || card == Card.RedAndSuspension;
        victim.LeftInjured = injury != InjuryType.None;

        if (offender.SentOff || victim.LeftInjured)
        {
            committing.Tactic.RefreshMatchCache();
            fouled.Tactic.RefreshMatchCache();
        }

        bool nearGoal = phase == Phase.Type.Penetrate || phase == Phase.Type.Probe || phase == Phase.Type.Break;
        if (nearGoal && fouled.OnPitchPlayers.Count > 0 && committing.OnPitchPlayers.Count > 0)
        {
            bool penalty = Random.value < 0.16f;
            AttemptShot(phase, fouled, committing, match.currentMin, penalty ? 0.76f : 0.09f,
                        penalty ? ShotType.Penalty : ShotType.Free_Kick);
        }
        return true;
    }

    private Card DecideCard(Team committing, Player offender)
    {
        float aggression = offender.GetStats().Aggression;
        float fouling = committing.Tactic.Fouling;

        float redChance = 0.006f + aggression / 5000f;                 // genuinely rare
        float yellowChance = 0.14f + aggression / 600f + fouling / 900f; // ~14–40%

        float r = Random.value;
        if (r < redChance) return Card.Red;
        if (r < redChance + yellowChance) return Card.Yellow;
        return Card.None;
    }

    private InjuryType DecideInjury(Card card, Player victim)
    {
        float durability = victim.GetStats().Durability;
        float injuryChance = 0.03f + (card != Card.None ? 0.05f : 0f) + (100f - durability) / 2000f;
        if (Random.value > injuryChance) return InjuryType.None;

        // Of the fouls that DO injure: mostly knocks, occasionally worse, death astronomically rare.
        float s = Random.value;
        if (s < 0.62f) return InjuryType.Knock;
        if (s < 0.86f) return InjuryType.Standard;
        if (s < 0.975f) return InjuryType.Hamstring;
        if (s < 0.999f) return InjuryType.ACL;
        return InjuryType.Death;
    }

    void PlayPossession(Team attacking, Team defending)
    {
        float control = WeightedAverage((attacking.Tactic.Control, 1f), (100 - attacking.Intensity, 1f), (100 - attacking.Tactic.Tempo, 1f));
        Phase.Type phase = Random.Range(0f, 100f) < control ? Phase.Type.Build : Phase.Type.Advance;
        float familiarity = attacking.IsCpuControlled ? 1f : attacking.Tactic.Familiarity01;
        if (Random.Range(0f, 10f + attacking.Stability + defending.Stability) <= 10f + (1f - familiarity) * 7f)
        {
            var players = attacking.OnPitchPlayers;
            match.AddHighlight(new MistakeHighlight(attacking, match.currentMin, players[Random.Range(0, players.Count)]));
            (attacking, defending) = (defending, attacking);
            phase = Phase.Type.Counter;
        }

        float momentum = 0f;
        for (int step = 0; step < 12; step++)
        {
            match.AddHighlight(new PossessionHighlight(attacking, match.currentMin, phase));
            if (TryFoul(defending, attacking, phase)) return;

            float result;
            Phase.Type success, turnover;
            float chance = 0f;
            switch (phase)
            {
                case Phase.Type.Build:
                    result = BuildLogic(attacking, defending);
                    success = Phase.Type.Progress;
                    turnover = Phase.Type.Break;
                    break;
                case Phase.Type.Progress:
                    result = ProgressLogic(attacking, defending, momentum);
                    success = Phase.Type.Probe;
                    turnover = Phase.Type.Advance;
                    break;
                case Phase.Type.Advance:
                    result = AdvanceLogic(attacking, defending, momentum);
                    success = Phase.Type.Penetrate;
                    turnover = Phase.Type.Advance;
                    break;
                case Phase.Type.Counter:
                    result = CounterLogic(attacking, defending, momentum);
                    success = Phase.Type.Break;
                    turnover = Phase.Type.Penetrate;
                    break;
                case Phase.Type.Probe:
                    result = ProbeLogic(attacking, defending, momentum);
                    chance = Random.Range(0.2f, 0.8f);
                    success = Phase.Type.Probe;
                    turnover = Phase.Type.Counter;
                    break;
                case Phase.Type.Penetrate:
                    result = PenetrateLogic(attacking, defending, momentum);
                    chance = Random.Range(0.05f, 0.5f);
                    success = Phase.Type.Penetrate;
                    turnover = Phase.Type.Counter;
                    break;
                default:
                    result = BreakLogic(attacking, defending, momentum);
                    chance = Random.Range(0.02f, 0.3f);
                    success = Phase.Type.Break;
                    turnover = Phase.Type.Build;
                    break;
            }

            bool wide = Random.Range(0, 100) < attacking.AttackingWidth;
            Player carrier = SelectCarrier(attacking, phase, wide);
            var skills = carrier.GetStats();
            float execution = wide ? skills.Crossing : skills.Passing;
            if (result != 0f) result += (execution - 50f) / 15f;
            match.RecordAction(attacking, wide, result < 0f);

            momentum = Mathf.Abs(result);
            if (result < 0f)
            {
                (attacking, defending) = (defending, attacking);
                phase = turnover;
                continue;
            }
            if (result == 0f || chance > 0f && WinningComplacency(attacking)) break;
            if (chance > 0f)
            {
                AttemptShot(phase, attacking, defending, match.currentMin, Mathf.Clamp(chance + (execution - 50f) / 1000f, 0.02f, 0.9f), provider: carrier);
                return;
            }
            phase = success;
        }
        match.AddHighlight(new PossessionFailHighlight(attacking, match.currentMin, phase));
    }

    static Player SelectCarrier(Team team, Phase.Type phase, bool wide)
    {
        PlayerGroup group = wide ? PlayerGroup.WidePlayers : PlayerGroup.Midfielders;
        if (phase == Phase.Type.Build && !wide) group = PlayerGroup.Defenders;
        var candidates = team.GetGroup(group).FindAll(p => p.CanPlay);
        if (candidates.Count == 0) candidates = team.OnPitchPlayers;
        return candidates[Random.Range(0, candidates.Count)];
    }

    private bool WinningComplacency(Team attacking)
    {
        int difference = 0;

        if (match.result.score.home > match.result.score.away && attacking == match.HomeTeam)
            difference = match.result.score.home - match.result.score.away;

        if (match.result.score.away > match.result.score.home && attacking == match.AwayTeam)
            difference = match.result.score.away - match.result.score.home;

        float min = 2;
        if (difference <= min) return false;
        if (difference >= Random.Range(5, 14)) return true;

        bool complacent = Random.Range(min, difference) < min + 0.4f;
        return complacent;
    }

    public bool AttemptShot(Phase.Type phase, Team attackingTeam, Team defendingTeam, Minute currentMinute, float baseXG, ShotType? restart = null, Player provider = null)
    {
        Player shooter = SelectWeightedRandomPlayer(attackingTeam.OnPitchPlayers);
        Player keeper = defendingTeam.Goalkeeper;
        if (keeper == null || !keeper.CanPlay) keeper = defendingTeam.OnPitchPlayers[0];

        ShotType shotType = restart ?? SelectShotType(phase, baseXG);

        float playerEffect = CalculatePlayerEffect(shotType, shooter, baseXG) / 1.5f;
        float onTargetChance = Mathf.Clamp(baseXG + playerEffect, 0.01f, 0.99f);

        float rand1 = Random.Range(0f, 1f);
        if (rand1 - 0.25f > onTargetChance)
        {
            match.RecordShot(shotType, ShotOutcome.Miss, attackingTeam, shooter, keeper, baseXG, currentMinute);
            return false;
        }

        float finalGoalChance = AdjustForKeeper(onTargetChance, keeper);
        finalGoalChance = Mathf.Clamp(finalGoalChance, 0.015f, 0.98f);

        if (shotType == ShotType.Deflection)
            finalGoalChance = Mathf.Clamp(baseXG * 4f, 0.01f, 0.9f);
        else if (shotType == ShotType.Own_Goal)
        {
            finalGoalChance = Mathf.Clamp(baseXG * 8f, 0.01f, 0.9f);
            shooter = defendingTeam.OnPitchPlayers[Random.Range(0, defendingTeam.OnPitchPlayers.Count)];
        }


        float rand2 = rand1;
        ShotOutcome outcome = (rand2 < finalGoalChance) ? ShotOutcome.Goal : ShotOutcome.Saved;

        // Credit an assist on most goals (not own goals) to a weighted-random team-mate of the scorer.
        Player assister = (outcome == ShotOutcome.Goal && shotType != ShotType.Own_Goal && shotType != ShotType.Penalty && shotType != ShotType.Free_Kick)
            ? (provider == shooter ? null : provider ?? DecideAssister(attackingTeam, shooter))
            : null;

        match.RecordShot(shotType, outcome, attackingTeam, shooter, keeper, baseXG, currentMinute, assister);

        if (!restart.HasValue && outcome == ShotOutcome.Saved && Random.value < 0.2f)
            return AttemptShot(phase, attackingTeam, defendingTeam, currentMinute, 0.07f, ShotType.Corner);

        return outcome == ShotOutcome.Goal;
    }

    /// <summary>Picks who set up a goal — a team-mate weighted by creative/passing stats, or none (~25% solo).</summary>
    private Player DecideAssister(Team team, Player scorer)
    {
        if (Random.value < 0.25f) return null;

        var candidates = team.OnPitchPlayers.Where(p => p != scorer).ToList();
        if (candidates.Count == 0) return null;

        float total = 0f;
        foreach (var p in candidates) total += AssistWeight(p);
        if (total <= 0f) return candidates[Random.Range(0, candidates.Count)];

        float r = Random.Range(0f, total);
        foreach (var p in candidates)
        {
            r -= AssistWeight(p);
            if (r <= 0f) return p;
        }
        return candidates[candidates.Count - 1];
    }

    private static float AssistWeight(Player p)
    {
        var s = p.GetStats();
        return 1f + WeightedAverage(
            (s.Passing, 0.35f),
            (s.Creativity, 0.25f),
            (s.Technique, 0.20f),
            (s.Crossing, 0.15f),
            (s.Teamwork, 0.05f));
    }

    private ShotType SelectShotType(Phase.Type phase, float baseXG)
    {
        List<(ShotType, float)> options = new();

        options.Add((ShotType.Strike, 1f));
        options.Add((ShotType.Header, 0.4f));

        switch (phase)
        {
            case Phase.Type.Probe:
                if (baseXG >= 0.3f) options.Add((ShotType.Tap_In, 1.5f));
                if (baseXG < 0.05f) options.Add((ShotType.Screamer, 1.0f));
                break;

            case Phase.Type.Break:
                options.Add((ShotType.Solo, 1.0f));
                options.Add((ShotType.Strike, 0.5f));
                options.Add((ShotType.Header, 0.5f));
                break;

            case Phase.Type.Penetrate:
                options.Add((ShotType.Stylish, 1.2f));
                if (baseXG < 0.05f) options.Add((ShotType.Screamer, 0.6f));
                if (baseXG >= 0.3f) options.Add((ShotType.Tap_In, 0.5f));
                break;
        }

        options.Add((ShotType.Deflection, 0.05f));
        options.Add((ShotType.Own_Goal, 0.02f));

        return WeightedRandom(options);
    }

    private float CalculatePlayerEffect(ShotType shotType, Player shooter, float baseXG)
    {
        var stats = shooter.GetStats();
        float relevantSkill = WeightedAverage((stats.Shooting, 0.7f), (stats.Technique, 0.3f));
        float composure = stats.Composure;

        switch (shotType)
        {
            case ShotType.Tap_In:
                relevantSkill = WeightedAverage(
                    (stats.Shooting, 0.4f), (stats.Positioning, 0.35f), (stats.Technique, 0.25f));
                break;
            case ShotType.Corner:
            case ShotType.Header:
                relevantSkill = WeightedAverage(
                    (stats.Heading, 0.38f), (stats.Jumping, 0.25f), (stats.Height, 0.15f),
                    (stats.Strength, 0.12f), (stats.Positioning, 0.10f));
                break;
            case ShotType.Solo:
                relevantSkill = WeightedAverage(
                    (stats.Shooting, 0.3f), (stats.Dribbling, 0.3f), (stats.Technique, 0.2f),
                    (stats.Pace, 0.1f), (stats.Agility, 0.1f));
                break;
            case ShotType.Stylish:
                relevantSkill = WeightedAverage(
                    (stats.Technique, 0.35f), (stats.Shooting, 0.3f),
                    (stats.Creativity, 0.2f), (stats.Agility, 0.15f));
                break;
            case ShotType.Screamer:
                relevantSkill = WeightedAverage(
                    (stats.Shooting, 0.55f), (stats.Technique, 0.3f), (stats.Strength, 0.15f));
                composure = (stats.Shooting + stats.Composure) / 2f; break;
        }

        float skillEffect = ((relevantSkill - 40f) * (1 - baseXG) + (composure - 40f) * baseXG) / 100f;
        skillEffect *= 0.4f + baseXG * 0.6f;
        return skillEffect;
    }

    private float AdjustForKeeper(float shotOnTargetChance, Player keeper)
    {
        float keeperStat = keeper.GetStats().Goalkeeping;

        float keeperEffect = NonLinearKeeperEffect(keeperStat, shotOnTargetChance);
        float finalXG = Mathf.Clamp(shotOnTargetChance + keeperEffect, 0.01f, 0.99f);

        return finalXG;
    }

    private float NonLinearKeeperEffect(float keeperStat, float baseXG)
    {
        float skill = Mathf.Clamp01(keeperStat / 100f);
        float neutral = 0.4f;

        if (Mathf.Approximately(skill, neutral))
            return 0f;

        float xgShift = 0f;

        if (skill > neutral)
        {
            float suppressionStrength = (skill - neutral) / (1f - neutral);
            float weightLow = Mathf.Pow(1f - baseXG, 1.5f);
            float weightHigh = Mathf.Pow(baseXG, 1.3f);

            float suppress = (weightLow * 0.22f + weightHigh * 0.15f);
            xgShift = -suppress * suppressionStrength;
        }
        else
        {
            float weaknessStrength = (neutral - skill) / neutral;
            float weightLow = Mathf.Pow(1f - baseXG, 1.1f);
            float weightHigh = Mathf.Pow(baseXG, 1.0f);

            float boost = (weightLow * 0.25f + weightHigh * 0.08f);
            xgShift = boost * weaknessStrength;
        }

        return xgShift;
    }

    private Minute AdvanceMinute(Minute currentMinute)
    {
        currentMinute.Next();
        return currentMinute;
    }

    // helper used by SimulateMinute
    private static float CalculateHomePossession(Team home, Team away)
    {
        float homeWeight = Mathf.Max(1, home.Control) * home.OnPitchPlayers.Count;
        float awayWeight = Mathf.Max(1, away.Control) * away.OnPitchPlayers.Count;
        return homeWeight / Mathf.Max(1f, homeWeight + awayWeight);
    }

    public float CalculateRandomness()
    {
        float averageStability = (match.HomeTeam.Tactic.Stability + match.AwayTeam.Tactic.Stability) * 0.5f;
        float randomRange = 5f + (100f - averageStability) / 10f;
        return Random.Range(-randomRange, randomRange);
    }

    private static SquadStatProfile Profile(Team team, PlayerGroup players, PlayerGroup? relianceContext = null)
    {
        return team.GetGroup(players).FindAll(p => p.CanPlay).StatProfile(team.Tactic, relianceContext ?? players);
    }

    private static (float value, float weight) PhaseStat(
        SquadStatProfile profile,
        PlayerStat stat,
        float weight,
        StatAggregation aggregation = StatAggregation.Cohesive)
    {
        return (profile.Get(stat, aggregation), weight);
    }

    public float ResolvePhase(Phase.Parameters parameters, Team attacking, Team defending, float overflow = 0f, bool debug = false)
    {
        float attackingAbility = WeightedAverage(parameters.AttackingStats);
        float attackingTactic = WeightedAverage(parameters.AttackingTactics);
        float defendingAbility = WeightedAverage(parameters.DefendingStats);
        float defendingTactic = WeightedAverage(parameters.DefendingTactics);

        float tacticDiff = attackingTactic - defendingTactic;
        float abilityDiff = attackingAbility - defendingAbility;

        float tacticInfluence = 1.0f - Mathf.Clamp01(Mathf.Abs(tacticDiff) / 100f);
        float abilityWeight = tacticInfluence;
        float tacticWeight = 1.0f - tacticInfluence;

        float weightedTactic = tacticWeight * Mathf.Sign(tacticDiff) * Mathf.Pow(Mathf.Abs(tacticDiff), parameters.TacticExponent);
        float weightedAbility = abilityWeight * Mathf.Sign(abilityDiff) * Mathf.Pow(Mathf.Abs(abilityDiff), parameters.AbilityExponent);

        float randomness = CalculateRandomness() + parameters.RandomnessBonus;
        float score = weightedTactic + weightedAbility + randomness + overflow;

        float result = 0;
        if (score > parameters.SuccessThreshold)
            result = Mathf.Pow(score - parameters.SuccessThreshold, 0.75f);
        else if (score < parameters.FailThreshold)
            result = -Mathf.Pow(parameters.FailThreshold - score, 0.75f);

        if (debug)
        {
            Debug.Log($"== {parameters.PhaseName.ToUpper()} PHASE ==");
            Debug.Log($"Attacking Team: {attacking.Name} | Tactic: {attackingTactic:F1}, Ability: {attackingAbility:F1}");
            Debug.Log($"Defending Team: {defending.Name} | Tactic: {defendingTactic:F1}, Ability: {defendingAbility:F1}");
            Debug.Log($"TacticDiff: {tacticDiff}, AbilityDiff: {abilityDiff}");
            Debug.Log($"TacticInfluence: {tacticInfluence:F2} | TacticWeight: {tacticWeight:F2} | AbilityWeight: {abilityWeight:F2}");
            Debug.Log($"WeightedTactic: {weightedTactic:F2}, WeightedAbility: {weightedAbility:F2}");
            Debug.Log($"Randomness: {randomness:F2} | Overflow: {overflow:F2}");
            Debug.Log($"Final Score: {score:F2}");
            Debug.Log($"Success Threshold: {parameters.SuccessThreshold}, Fail Threshold: {parameters.FailThreshold}");
            Debug.Log($"Result: {result:F2}");
        }

        return result;
    }

    public float BuildLogic(Team attacking, Team defending, float overflow = 0f, bool debug = false)
    {
        SquadStatProfile atkStats = Profile(attacking, PlayerGroup.GoalkeeperAndDefense, PlayerGroup.Defenders);
        SquadStatProfile defStats = Profile(defending, PlayerGroup.MidfieldAndAttack);

        var parameters = new Phase.Parameters
        {
            AttackingStats = new()
            {
                PhaseStat(atkStats, PlayerStat.Passing, 0.26f, StatAggregation.StarInfluenced),
                PhaseStat(atkStats, PlayerStat.Technique, 0.20f, StatAggregation.WeakLinkInfluenced),
                PhaseStat(atkStats, PlayerStat.Composure, 0.17f, StatAggregation.WeakLinkInfluenced),
                PhaseStat(atkStats, PlayerStat.Intelligence, 0.14f),
                PhaseStat(atkStats, PlayerStat.Teamwork, 0.10f),
                PhaseStat(atkStats, PlayerStat.Positioning, 0.08f),
                PhaseStat(atkStats, PlayerStat.Agility, 0.05f, StatAggregation.Mean)
            },
            AttackingTactics = new()
            {
                (attacking.Tactic.Control, 0.45f),
                (attacking.Tactic.Stability, 0.35f),
                (attacking.Tactic.Security, 0.20f)
            },
            DefendingStats = new()
            {
                PhaseStat(defStats, PlayerStat.Positioning, 0.20f),
                PhaseStat(defStats, PlayerStat.Marking, 0.18f),
                PhaseStat(defStats, PlayerStat.Intelligence, 0.15f),
                PhaseStat(defStats, PlayerStat.Pace, 0.14f, StatAggregation.StarInfluenced),
                PhaseStat(defStats, PlayerStat.Stamina, 0.10f),
                PhaseStat(defStats, PlayerStat.Aggression, 0.09f, StatAggregation.Mean),
                PhaseStat(defStats, PlayerStat.Tackling, 0.08f, StatAggregation.BestPairInfluenced),
                PhaseStat(defStats, PlayerStat.Teamwork, 0.06f)
            },
            DefendingTactics = new()
            {
                (defending.Tactic.Pressure, 0.55f),
                (defending.Tactic.Intensity, 0.30f),
                (defending.Tactic.Control, 0.15f)
            },
            PhaseName = "Build",
            TacticExponent = 1f,
            AbilityExponent = 0.75f,
            RandomnessBonus = 0f,
            SuccessThreshold = 5f,
            FailThreshold = -5f
        };

        return ResolvePhase(parameters, attacking, defending, overflow, debug);
    }

    public float ProgressLogic(Team attacking, Team defending, float overflow = 0f, bool debug = false)
    {
        SquadStatProfile atkStats = Profile(attacking, PlayerGroup.Midfielders);
        SquadStatProfile defStats = Profile(defending, PlayerGroup.Midfielders);

        var parameters = new Phase.Parameters
        {
            AttackingStats = new()
            {
                PhaseStat(atkStats, PlayerStat.Passing, 0.23f),
                PhaseStat(atkStats, PlayerStat.Technique, 0.18f),
                PhaseStat(atkStats, PlayerStat.Intelligence, 0.14f),
                PhaseStat(atkStats, PlayerStat.Positioning, 0.12f),
                PhaseStat(atkStats, PlayerStat.Teamwork, 0.10f),
                PhaseStat(atkStats, PlayerStat.Dribbling, 0.09f, StatAggregation.StarInfluenced),
                PhaseStat(atkStats, PlayerStat.Creativity, 0.08f, StatAggregation.StarInfluenced),
                PhaseStat(atkStats, PlayerStat.Composure, 0.06f, StatAggregation.WeakLinkInfluenced)
            },
            AttackingTactics = new()
            {
                (attacking.Tactic.Control, 0.40f),
                (attacking.Tactic.Stability, 0.30f),
                (attacking.Tactic.Creativity, 0.20f),
                (attacking.Tactic.Security, 0.10f)
            },
            DefendingStats = new()
            {
                PhaseStat(defStats, PlayerStat.Marking, 0.21f),
                PhaseStat(defStats, PlayerStat.Positioning, 0.19f),
                PhaseStat(defStats, PlayerStat.Intelligence, 0.15f),
                PhaseStat(defStats, PlayerStat.Tackling, 0.14f, StatAggregation.BestPairInfluenced),
                PhaseStat(defStats, PlayerStat.Teamwork, 0.11f),
                PhaseStat(defStats, PlayerStat.Agility, 0.08f),
                PhaseStat(defStats, PlayerStat.Stamina, 0.07f),
                PhaseStat(defStats, PlayerStat.Aggression, 0.05f, StatAggregation.Mean)
            },
            DefendingTactics = new()
            {
                (defending.Tactic.Pressure, 0.35f),
                (defending.Tactic.Stability, 0.30f),
                (defending.Tactic.Intensity, 0.20f),
                (defending.Tactic.Security, 0.15f)
            },
            PhaseName = "Progress",
            TacticExponent = 1f,
            AbilityExponent = 0.75f,
            RandomnessBonus = 0f,
            SuccessThreshold = 5f,
            FailThreshold = -5f
        };

        return ResolvePhase(parameters, attacking, defending, overflow, debug);
    }

    public float ProbeLogic(Team attacking, Team defending, float overflow = 0f, bool debug = false)
    {
        SquadStatProfile atkStats = Profile(attacking, PlayerGroup.MidfieldAndAttack);
        SquadStatProfile defStats = Profile(defending, PlayerGroup.Defenders);

        var parameters = new Phase.Parameters
        {
            AttackingStats = new()
            {
                PhaseStat(atkStats, PlayerStat.Creativity, 0.20f, StatAggregation.StarInfluenced),
                PhaseStat(atkStats, PlayerStat.Intelligence, 0.16f),
                PhaseStat(atkStats, PlayerStat.Technique, 0.15f),
                PhaseStat(atkStats, PlayerStat.Passing, 0.13f),
                PhaseStat(atkStats, PlayerStat.Composure, 0.11f),
                PhaseStat(atkStats, PlayerStat.Positioning, 0.10f, StatAggregation.BestPairInfluenced),
                PhaseStat(atkStats, PlayerStat.Dribbling, 0.08f, StatAggregation.BestPairInfluenced),
                PhaseStat(atkStats, PlayerStat.Crossing, 0.04f, StatAggregation.StarInfluenced),
                PhaseStat(atkStats, PlayerStat.Teamwork, 0.03f)
            },
            AttackingTactics = new()
            {
                (attacking.Tactic.Creativity, 0.40f),
                (attacking.Tactic.Control, 0.30f),
                (attacking.Tactic.Stability, 0.20f),
                (attacking.Tactic.Pressure, 0.10f)
            },
            DefendingStats = new()
            {
                PhaseStat(defStats, PlayerStat.Marking, 0.22f),
                PhaseStat(defStats, PlayerStat.Positioning, 0.21f),
                PhaseStat(defStats, PlayerStat.Intelligence, 0.15f),
                PhaseStat(defStats, PlayerStat.Teamwork, 0.13f),
                PhaseStat(defStats, PlayerStat.Composure, 0.09f),
                PhaseStat(defStats, PlayerStat.Tackling, 0.08f, StatAggregation.BestPairInfluenced),
                PhaseStat(defStats, PlayerStat.Agility, 0.05f),
                PhaseStat(defStats, PlayerStat.Heading, 0.04f),
                PhaseStat(defStats, PlayerStat.Aggression, 0.03f, StatAggregation.Mean)
            },
            DefendingTactics = new()
            {
                (defending.Tactic.Security, 0.45f),
                (Mathf.Max(40, 100 - defending.Tactic.DefensiveWidth), 0.25f),
                (defending.Tactic.Stability, 0.20f),
                (defending.Tactic.Pressure, 0.10f)
            },
            PhaseName = "Probe",
            TacticExponent = 1f,
            AbilityExponent = 0.75f,
            RandomnessBonus = 0f,
            SuccessThreshold = 5f,
            FailThreshold = -5f
        };

        return ResolvePhase(parameters, attacking, defending, overflow, debug);
    }

    public float AdvanceLogic(Team attacking, Team defending, float overflow = 0f, bool debug = false)
    {
        SquadStatProfile atkStats = Profile(attacking, PlayerGroup.MidfieldAndAttack);
        SquadStatProfile defStats = Profile(defending, PlayerGroup.DefenseAndMidfield);

        var parameters = new Phase.Parameters
        {
            AttackingStats = new()
            {
                PhaseStat(atkStats, PlayerStat.Pace, 0.21f, StatAggregation.StarInfluenced),
                PhaseStat(atkStats, PlayerStat.Dribbling, 0.16f, StatAggregation.BestPairInfluenced),
                PhaseStat(atkStats, PlayerStat.Technique, 0.14f),
                PhaseStat(atkStats, PlayerStat.Passing, 0.12f),
                PhaseStat(atkStats, PlayerStat.Creativity, 0.11f, StatAggregation.StarInfluenced),
                PhaseStat(atkStats, PlayerStat.Crossing, 0.09f, StatAggregation.StarInfluenced),
                PhaseStat(atkStats, PlayerStat.Agility, 0.08f, StatAggregation.BestPairInfluenced),
                PhaseStat(atkStats, PlayerStat.Teamwork, 0.05f),
                PhaseStat(atkStats, PlayerStat.Stamina, 0.04f)
            },
            AttackingTactics = new()
            {
                (attacking.Tactic.Creativity, 0.40f),
                (attacking.Tactic.Intensity, 0.30f),
                (attacking.Tactic.Control, 0.20f),
                (attacking.Tactic.Security, 0.10f)
            },
            DefendingStats = new()
            {
                PhaseStat(defStats, PlayerStat.Pace, 0.21f, StatAggregation.WeakLinkInfluenced),
                PhaseStat(defStats, PlayerStat.Positioning, 0.19f),
                PhaseStat(defStats, PlayerStat.Marking, 0.17f),
                PhaseStat(defStats, PlayerStat.Tackling, 0.14f, StatAggregation.BestPairInfluenced),
                PhaseStat(defStats, PlayerStat.Agility, 0.10f, StatAggregation.WeakLinkInfluenced),
                PhaseStat(defStats, PlayerStat.Teamwork, 0.08f),
                PhaseStat(defStats, PlayerStat.Stamina, 0.06f),
                PhaseStat(defStats, PlayerStat.Strength, 0.05f)
            },
            DefendingTactics = new()
            {
                (defending.Tactic.Pressure, 0.40f),
                (defending.Tactic.Security, 0.35f),
                (defending.Tactic.Stability, 0.25f)
            },
            PhaseName = "Advance",
            TacticExponent = 0.95f,
            AbilityExponent = 0.75f,
            RandomnessBonus = 0,
            SuccessThreshold = 5f,
            FailThreshold = -5f
        };

        return ResolvePhase(parameters, attacking, defending, overflow, debug);
    }

    public float PenetrateLogic(Team attacking, Team defending, float overflow = 0f, bool debug = false)
    {
        SquadStatProfile atkStats = Profile(attacking, PlayerGroup.MidfieldAndAttack);
        SquadStatProfile defStats = Profile(defending, PlayerGroup.Defenders);

        var parameters = new Phase.Parameters
        {
            AttackingStats = new()
            {
                PhaseStat(atkStats, PlayerStat.Technique, 0.15f),
                PhaseStat(atkStats, PlayerStat.Dribbling, 0.15f, StatAggregation.BestPairInfluenced),
                PhaseStat(atkStats, PlayerStat.Creativity, 0.14f, StatAggregation.StarInfluenced),
                PhaseStat(atkStats, PlayerStat.Passing, 0.11f),
                PhaseStat(atkStats, PlayerStat.Crossing, 0.10f, StatAggregation.StarInfluenced),
                PhaseStat(atkStats, PlayerStat.Positioning, 0.10f, StatAggregation.BestPairInfluenced),
                PhaseStat(atkStats, PlayerStat.Composure, 0.09f),
                PhaseStat(atkStats, PlayerStat.Pace, 0.07f, StatAggregation.StarInfluenced),
                PhaseStat(atkStats, PlayerStat.Heading, 0.05f, StatAggregation.BestPairInfluenced),
                PhaseStat(atkStats, PlayerStat.Teamwork, 0.04f)
            },
            AttackingTactics = new()
            {
                (attacking.Tactic.Creativity, 0.55f),
                (attacking.Tactic.Intensity, 0.30f),
                (attacking.Tactic.Control, 0.15f)
            },
            DefendingStats = new()
            {
                PhaseStat(defStats, PlayerStat.Marking, 0.19f),
                PhaseStat(defStats, PlayerStat.Positioning, 0.18f),
                PhaseStat(defStats, PlayerStat.Tackling, 0.15f, StatAggregation.BestPairInfluenced),
                PhaseStat(defStats, PlayerStat.Strength, 0.10f),
                PhaseStat(defStats, PlayerStat.Agility, 0.10f),
                PhaseStat(defStats, PlayerStat.Teamwork, 0.09f),
                PhaseStat(defStats, PlayerStat.Composure, 0.06f),
                PhaseStat(defStats, PlayerStat.Jumping, 0.05f, StatAggregation.BestPairInfluenced),
                PhaseStat(defStats, PlayerStat.Heading, 0.05f, StatAggregation.BestPairInfluenced),
                PhaseStat(defStats, PlayerStat.Height, 0.03f, StatAggregation.BestPairInfluenced)
            },
            DefendingTactics = new()
            {
                (defending.Tactic.Security, 0.50f),
                (defending.Tactic.Stability, 0.30f),
                (defending.Tactic.Control, 0.20f)
            },
            PhaseName = "Penetrate",
            TacticExponent = 0.95f,
            AbilityExponent = 0.75f,
            RandomnessBonus = 0,
            SuccessThreshold = 5f,
            FailThreshold = -5f
        };

        return ResolvePhase(parameters, attacking, defending, overflow, debug);
    }

    public float CounterLogic(Team attacking, Team defending, float overflow = 0f, bool debug = false)
    {
        SquadStatProfile atkStats = Profile(attacking, PlayerGroup.DefenseAndMidfield);
        SquadStatProfile defStats = Profile(defending, PlayerGroup.MidfieldAndAttack);

        var parameters = new Phase.Parameters
        {
            AttackingStats = new()
            {
                PhaseStat(atkStats, PlayerStat.Passing, 0.20f, StatAggregation.StarInfluenced),
                PhaseStat(atkStats, PlayerStat.Technique, 0.15f),
                PhaseStat(atkStats, PlayerStat.Intelligence, 0.14f),
                PhaseStat(atkStats, PlayerStat.Creativity, 0.13f, StatAggregation.StarInfluenced),
                PhaseStat(atkStats, PlayerStat.Pace, 0.12f, StatAggregation.StarInfluenced),
                PhaseStat(atkStats, PlayerStat.Dribbling, 0.10f, StatAggregation.BestPairInfluenced),
                PhaseStat(atkStats, PlayerStat.Composure, 0.08f, StatAggregation.WeakLinkInfluenced),
                PhaseStat(atkStats, PlayerStat.Teamwork, 0.05f),
                PhaseStat(atkStats, PlayerStat.Stamina, 0.03f)
            },
            AttackingTactics = new()
            {
                (Mathf.Max(20, 100 - attacking.Tactic.Stability), 0.45f),
                (attacking.Tactic.Intensity, 0.40f),
                (attacking.Tactic.Creativity, 0.15f)
            },
            DefendingStats = new()
            {
                PhaseStat(defStats, PlayerStat.Pace, 0.20f, StatAggregation.WeakLinkInfluenced),
                PhaseStat(defStats, PlayerStat.Marking, 0.18f),
                PhaseStat(defStats, PlayerStat.Positioning, 0.16f),
                PhaseStat(defStats, PlayerStat.Tackling, 0.13f, StatAggregation.BestPairInfluenced),
                PhaseStat(defStats, PlayerStat.Stamina, 0.11f),
                PhaseStat(defStats, PlayerStat.Aggression, 0.08f, StatAggregation.Mean),
                PhaseStat(defStats, PlayerStat.Teamwork, 0.08f),
                PhaseStat(defStats, PlayerStat.Intelligence, 0.06f)
            },
            DefendingTactics = new()
            {
                (defending.Tactic.Security, 0.55f),
                (defending.Tactic.Stability, 0.30f),
                (defending.Tactic.Pressure, 0.15f)
            },
            PhaseName = "Counter",
            TacticExponent = 0.9f,
            AbilityExponent = 0.75f,
            RandomnessBonus = Random.Range(0f, 1.5f),
            SuccessThreshold = 5f,
            FailThreshold = -10f
        };

        return ResolvePhase(parameters, attacking, defending, overflow, debug);
    }

    public float BreakLogic(Team attacking, Team defending, float overflow = 0f, bool debug = false)
    {
        SquadStatProfile atkStats = Profile(attacking, PlayerGroup.Attackers);
        SquadStatProfile defStats = Profile(defending, PlayerGroup.Defenders);

        var parameters = new Phase.Parameters
        {
            AttackingStats = new()
            {
                PhaseStat(atkStats, PlayerStat.Pace, 0.23f, StatAggregation.StarInfluenced),
                PhaseStat(atkStats, PlayerStat.Dribbling, 0.16f, StatAggregation.BestPairInfluenced),
                PhaseStat(atkStats, PlayerStat.Technique, 0.14f),
                PhaseStat(atkStats, PlayerStat.Composure, 0.12f),
                PhaseStat(atkStats, PlayerStat.Passing, 0.10f),
                PhaseStat(atkStats, PlayerStat.Creativity, 0.09f, StatAggregation.StarInfluenced),
                PhaseStat(atkStats, PlayerStat.Positioning, 0.08f, StatAggregation.BestPairInfluenced),
                PhaseStat(atkStats, PlayerStat.Agility, 0.05f, StatAggregation.BestPairInfluenced),
                PhaseStat(atkStats, PlayerStat.Stamina, 0.03f)
            },
            AttackingTactics = new()
            {
                (attacking.Tactic.Creativity, 0.55f),
                (attacking.Tactic.Intensity, 0.45f)
            },
            DefendingStats = new()
            {
                PhaseStat(defStats, PlayerStat.Pace, 0.23f, StatAggregation.WeakLinkInfluenced),
                PhaseStat(defStats, PlayerStat.Positioning, 0.19f),
                PhaseStat(defStats, PlayerStat.Marking, 0.17f, StatAggregation.WeakLinkInfluenced),
                PhaseStat(defStats, PlayerStat.Tackling, 0.13f, StatAggregation.BestPairInfluenced),
                PhaseStat(defStats, PlayerStat.Composure, 0.08f, StatAggregation.WeakLinkInfluenced),
                PhaseStat(defStats, PlayerStat.Strength, 0.07f),
                PhaseStat(defStats, PlayerStat.Agility, 0.06f, StatAggregation.WeakLinkInfluenced),
                PhaseStat(defStats, PlayerStat.Teamwork, 0.04f),
                PhaseStat(defStats, PlayerStat.Heading, 0.03f)
            },
            DefendingTactics = new()
            {
                (defending.Tactic.Security, 0.70f),
                (defending.Tactic.Stability, 0.30f)
            },
            PhaseName = "Break",
            TacticExponent = 0.9f,
            AbilityExponent = 0.75f,
            RandomnessBonus = Random.Range(0f, 1.5f),
            SuccessThreshold = 5f,
            FailThreshold = -5f
        };

        return ResolvePhase(parameters, attacking, defending, overflow, debug);
    }

    // kept public as in original file (utility/debug)
    public void SimulateProbabilites(float xg)
    {
        int count = 0;
        int goals = 0;
        for (int i = 0; i < 10000; i++)
        {
            bool res = AttemptShot(Phase.Type.Penetrate, match.HomeTeam, match.AwayTeam, new Minute(1), xg);
            count++;
            if (res) goals++;
        }
        Debug.Log($"Simulated with xG {xg:F2}: {(float)goals / count:F2}");
    }

    // helper used by AttemptShot
    public static Player SelectWeightedRandomPlayer(List<Player> players)
    {
        if (players == null || players.Count == 0) return null;

        Dictionary<Player.Position, double> positionWeights = new Dictionary<Player.Position, double>
        {
            { Player.Position.ST, 2.0 },
            { Player.Position.LW, 1.8 }, { Player.Position.RW, 1.8 },
            { Player.Position.AM, 1.8 },
            { Player.Position.LM, 1.5 }, { Player.Position.RM, 1.5 },
            { Player.Position.CM, 1.0 },
            { Player.Position.DM, 0.8 },
            { Player.Position.LB, 0.8 }, { Player.Position.RB, 0.8 },
            { Player.Position.CB, 0.8 },
            { Player.Position.GK, 0.1 }
        };

        var weightedPlayers = players.Select(p =>
        {
            int attacking = p.GetStats().Attacking;
            Player.Position pos = p.GetPosition() ?? Player.Position.CM;
            double positionWeight = positionWeights.ContainsKey(pos) ? positionWeights[pos] : 1.0;
            double weight = attacking * positionWeight;
            return new { Player = p, Weight = weight };
        }).Where(p => p.Weight > 0).ToList();

        if (weightedPlayers.Count == 0) return players[Random.Range(0, players.Count)];

        double totalWeight = weightedPlayers.Sum(p => p.Weight);
        double randomValue = UnityEngine.Random.Range(0, 1f) * totalWeight;

        double cumulativeWeight = 0;
        foreach (var entry in weightedPlayers)
        {
            cumulativeWeight += entry.Weight;
            if (randomValue <= cumulativeWeight) return entry.Player;
        }

        return weightedPlayers.Last().Player;
    }
}
