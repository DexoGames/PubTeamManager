using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using static Game;

public enum ShotType
{
    Strike, TapIn, Header, Solo, Stylish, Screamer, Penalty, FreeKick, Corner, OwnGoal, Deflection
}
public enum ShotOutcome
{
    Goal, Saved, Post, Miss, BadMiss
}

public class Match
{
    public struct Shot
    {
        public ShotType type;
        public ShotOutcome result;
        public Team team;
        public Player shooter;
        public Player assister;
        public float xG;
        public Minute minute;
    }
    public struct Foul
    {
        public Card card;
        public Player offender;
        public Player victim;
        public Minute minute;
        public InjuryType injuryType;

        public Foul(Card c, Player o, Player v, InjuryType i, Minute m)
        {
            card = c;
            offender = o;
            victim = v;
            minute = m;
            injuryType = i;
        }
    }
    public struct Result
    {
        public Score score => new Score(home.goals.Count, away.goals.Count);
        public TeamStats home;
        public TeamStats away;
    }
    public struct TeamStats
    {
        public Team team;
        public List<Shot> goals;
        public List<Foul> fouls;
        public float possession;
    }

    Team home;
    Team away;
    Result result;
    Minute currentMin;

    float temp;

    public Match(Team home, Team away)
    {
        this.home = home;
        this.away = away;

        InitializeMatch();
    }

    public Result SimulateMatch()
    {
        InitializeMatch();

        SimulateHalf(Half.First);
        SimulateHalf(Half.Second);

        return result;
    }

    private void InitializeMatch()
    {
        result = new Result
        {
            home = new TeamStats { team = home, goals = new(), fouls = new() },
            away = new TeamStats { team = away, goals = new(), fouls = new() }
        };
    }

    private void SimulateHalf(Half half)
    {
        currentMin = DecideStartMinute(half);

        while (currentMin.Base <= 45 && currentMin.Stoppage < Random.Range(1, 8))
        {
            if (Random.Range(0, 5) != 0) continue;

            SimulateMinute();
            currentMin = AdvanceMinute(currentMin);
        }
    }

    public void SimulateMinute()
    {
        float possession = CalculateHomePossession(home, away);

        Team attackingTeam = Random.Range(0f, 1f) < possession ? home : away;
        Team defendingTeam = attackingTeam == home ? away : home;
        Debug.Log($"--- {attackingTeam} Has Possesion ---");

        StartingPhase(attackingTeam, defendingTeam);
    }

    void StartingPhase(Team attacking, Team defending)
    {
        if (Random.Range(0, 10f + home.Tactic.Stability + away.Tactic.Stability) <= 10)
        {
            Debug.Log("OMG THERE WAS A MISTAKE!!!");
            int i = Random.Range(0, 3);
            switch (i)
            {
                case 0:
                    Counter(defending, attacking, 0);
                    return;
                case 1:
                    Penetrate(defending, attacking, 0);
                    return;
                case 2:
                    Break(defending, attacking, 0);
                    return;
            }
        }

        float controlScore = WeightedAverage((attacking.Tactic.Control, 1f), (100 - attacking.Tactic.Intensity, 1f));
        float rand = Random.Range(0, 100f);

        if (rand < controlScore)
        {
            Build(attacking, defending);
        }
        else
        {
            Advance(attacking, defending, 0);
        }
    }

    void Build(Team attacking, Team defending)
    {
        Debug.Log($"Build for {attacking.TeamName}");
        float result = BuildLogic(attacking, defending);
        if(result > 0)
        {
            Progress(attacking, defending, result);
        }
        else if (result < 0)
        {
            Break(defending, attacking, -result);
        }
        else
        {
            if (Random.Range(0, 2) == 0) Build(attacking, defending);
        }
    }

    void Progress(Team attacking, Team defending, float overflow)
    {
        Debug.Log($"Progress for {attacking.TeamName}, overflow: {overflow}");
        float result = ProgressLogic(attacking, defending, overflow);
        if (result > 0)
        {
            Probe(attacking, defending, result);
        }
        else if (result < 0)
        {
            Advance(defending, attacking, -result);
        }
        else
        {
            if (Random.Range(0, 2) == 0) Build(attacking, defending);
        }
    }
    void Probe(Team attacking, Team defending, float overflow)
    {
        Debug.Log($"Probe for {attacking.TeamName}, overflow: {overflow}");

        float result = ProbeLogic(attacking, defending, overflow);
        if (result > 0)
        {
            if (WinningComplacency(attacking)) return;
            AttemptShot(Phase.Type.Probe, attacking, defending, currentMin, Random.Range(0.2f, 0.8f));
        }
        else if (result < 0)
        {
            Counter(defending, attacking, -result);
        }
        else
        {
            if (Random.Range(0, 6) != 0) Progress(attacking, defending, 0);
        }
    }

    void Advance(Team attacking, Team defending, float overflow)
    {
        Debug.Log($"Advance for {attacking.TeamName}, overflow: {overflow}");

        float result = AdvanceLogic(attacking, defending, overflow);
        if (result > 0)
        {
            Penetrate(attacking, defending, result);
        }
        else if (result < 0)
        {
            Advance(defending, attacking, -result);
        }
        else
        {
            if (Random.Range(0, 4) == 0) Advance(attacking, defending, 0);
        }
    }

    void Penetrate(Team attacking, Team defending, float overflow)
    {
        Debug.Log($"Penetrate for {attacking.TeamName}, overflow: {overflow}");

        float result = PenetrateLogic(attacking, defending, overflow);
        if (result > 0)
        {
            if (WinningComplacency(attacking)) return;
            AttemptShot(Phase.Type.Penetrate, attacking, defending, currentMin, Random.Range(0.05f, 0.5f));
        }
        else if (result < 0)
        {
            Counter(defending, attacking, -result);
        }
        else
        {
            if (Random.Range(0, 4) == 0) Penetrate(attacking, defending, 0);
        }
    }

    void Counter(Team attacking, Team defending, float overflow)
    {
        Debug.Log($"Counter for {attacking.TeamName}, overflow: {overflow}");

        float result = CounterLogic(attacking, defending, overflow);
        if (result > 0)
        {
            Break(attacking, defending, result);
        }
        else if (result < 0)
        {
            Penetrate(defending, attacking, -result);
        }
        else
        {
            if (Random.Range(0, 2) == 0) Advance(attacking, defending, 0);
        }
    }

    void Break(Team attacking, Team defending, float overflow)
    {
        Debug.Log($"Break for {attacking.TeamName}, overflow: {overflow}");

        float result = BreakLogic(attacking, defending, overflow);
        if (result > 0)
        {
            if (WinningComplacency(attacking)) return;
            AttemptShot(Phase.Type.Break, attacking, defending, currentMin, Random.Range(0.02f, 0.3f));
        }
        else if (result < 0)
        {
            Build(defending, attacking);
        }
        else
        {
            if (Random.Range(0, 3) == 0) Advance(attacking, defending, 0);
        }
    }

    bool WinningComplacency(Team attacking)
    {
        int difference = 0;

        if(result.score.home > result.score.away && attacking == home)
        {
            difference = result.score.home - result.score.away;
        }
        if(result.score.away > result.score.home && attacking == away)
        {
            difference = result.score.away - result.score.home;
        }

        float min = 3;
        if (difference < min) return false;

        float exp = 1.5f;

        return Random.Range(0f, Mathf.Pow(difference, exp)) <= Mathf.Pow(min-1, exp);
    }

    public bool AttemptShot(Phase.Type phase, Team attackingTeam, Team defendingTeam, Minute currentMinute, float baseXG)
    {
        Player shooter = SelectWeightedRandomPlayer(attackingTeam.StartingPlayers);
        Player keeper = defendingTeam.Goalkeeper;

        ShotType shotType = SelectShotType(phase, baseXG);

        float playerEffect = CalculatePlayerEffect(shotType, shooter, baseXG) / 1.5f;
        float onTargetChance = Mathf.Clamp(baseXG + playerEffect, 0.01f, 0.99f);
        temp = onTargetChance;

        float rand1 = Random.Range(0f, 1f);
        if (rand1 > onTargetChance)
        {
            RecordShot(shotType, ShotOutcome.Miss, attackingTeam, shooter, baseXG, currentMinute);
            return false;
        }

        float finalGoalChance = AdjustForKeeper(onTargetChance, keeper);
        finalGoalChance = Mathf.Clamp(finalGoalChance, 0.015f, 0.98f);

        // Deflection/Own Goal override logic
        if (shotType == ShotType.Deflection)
            finalGoalChance = Mathf.Clamp(baseXG * 2f, 0.01f, 0.99f);
        else if (shotType == ShotType.OwnGoal)
            finalGoalChance = Mathf.Clamp(baseXG * 3f, 0.01f, 0.99f);

        temp = finalGoalChance;

        float rand2 = rand1;
        ShotOutcome outcome = (rand2 < finalGoalChance) ? ShotOutcome.Goal : ShotOutcome.Saved;

        RecordShot(shotType, outcome, attackingTeam, shooter, baseXG, currentMinute);

        return outcome == ShotOutcome.Goal;
    }


    private ShotType SelectShotType(Phase.Type phase, float baseXG)
    {
        List<(ShotType, float)> options = new();

        // Always available
        options.Add((ShotType.Strike, 1f));
        options.Add((ShotType.Header, 0.4f));

        // Add based on phase
        switch (phase)
        {
            case Phase.Type.Probe:
                if (baseXG >= 0.5f) options.Add((ShotType.TapIn, 1.5f));
                if (baseXG < 0.05f) options.Add((ShotType.Screamer, 1.0f));
                break;

            case Phase.Type.Break:
                options.Add((ShotType.Solo, 1.0f));
                options.Add((ShotType.Strike, 1.2f));
                options.Add((ShotType.Header, 1.0f));
                break;

            case Phase.Type.Penetrate:
                options.Add((ShotType.Stylish, 1.2f));
                break;
        }

        options.Add((ShotType.Deflection, 0.05f));
        options.Add((ShotType.OwnGoal, 0.02f));

        return WeightedRandom(options);
    }

    private float CalculatePlayerEffect(ShotType shotType, Player shooter, float baseXG)
    {
        var stats = shooter.GetStats();
        float relevantSkill = stats.Shooting;
        float composure = stats.Composure;

        switch (shotType)
        {
            case ShotType.TapIn:
                relevantSkill = stats.Positioning;
                break;
            case ShotType.Header:
                relevantSkill = (stats.Heading + stats.Jumping) / 2f;
                break;
            case ShotType.Solo:
                relevantSkill = (stats.Shooting + stats.Dribbling) / 2f;
                break;
            case ShotType.Stylish:
                relevantSkill = (stats.Shooting + stats.Creativity) / 2f;
                break;
            case ShotType.Screamer:
                relevantSkill = stats.Shooting;
                composure = (stats.Shooting + stats.Composure) / 2f;
                break;
        }

        //relevantSkill = 38; // For testing
        //composure = 38; // For testing

        float skillEffect = ((relevantSkill - 40f) * (1 - baseXG) + (composure - 40f) * baseXG) / 100f;
        skillEffect *= 0.4f + baseXG*0.6f;
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
        //keeperStat = 38; // For testing

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






    void RecordShot(ShotType type, ShotOutcome outcome, Team shootingTeam, Player shooter, float xG, Minute minute)
    {
        Shot shot = new Shot
        {
            type = type,
            result = outcome,
            team = shootingTeam,
            shooter = shooter,
            xG = xG,
            minute = minute,
        };

        if(outcome == ShotOutcome.Goal)
        {
            if (shootingTeam == home)
            {
                result.home.goals.Add(shot);
            }
            else
            {
                result.away.goals.Add(shot);
            }
        }

        Debug.LogWarning($"{shootingTeam.TeamName} - {outcome}! {shooter.Surname} ({type}) with xG: {xG:F2} and chance: {temp:F2}.");
    }

    private Minute AdvanceMinute(Minute currentMinute)
    {
        currentMinute.Next();
        return currentMinute;
    }

    public static Player SelectWeightedRandomPlayer(List<Player> players)
    {
        if (players == null || players.Count == 0)
            return null; // Handle empty lists

        // Define position multipliers
        Dictionary<Player.Position, double> positionWeights = new Dictionary<Player.Position, double>
        {
            { Player.Position.ST, 2.0 },  // Strikers are more likely
            { Player.Position.LW, 1.8 }, { Player.Position.RW, 1.8 },
            { Player.Position.AM, 1.8 },
            { Player.Position.LM, 1.5 }, { Player.Position.RM, 1.5 },
            { Player.Position.CM, 1.0 },
            { Player.Position.DM, 0.8 },
            { Player.Position.LB, 0.8 }, { Player.Position.RB, 0.8 },
            { Player.Position.CB, 0.8 },
            { Player.Position.GK, 0.1 }
        };

        // Calculate weights
        var weightedPlayers = players.Select(p =>
        {
            int attacking = p.GetStats().Attacking;
            Player.Position pos = p.GetPosition() ?? Player.Position.CM; // Default to CM if null

            double positionWeight = positionWeights.ContainsKey(pos) ? positionWeights[pos] : 1.0;
            double weight = attacking * positionWeight;

            return new { Player = p, Weight = weight };
        }).Where(p => p.Weight > 0).ToList();

        if (weightedPlayers.Count == 0)
            return null;

        // Perform weighted random selection
        double totalWeight = weightedPlayers.Sum(p => p.Weight);
        double randomValue = UnityEngine.Random.Range(0, 1f) * totalWeight;

        double cumulativeWeight = 0;
        foreach (var entry in weightedPlayers)
        {
            cumulativeWeight += entry.Weight;
            if (randomValue <= cumulativeWeight)
                return entry.Player;
        }

        return weightedPlayers.Last().Player;
    }

    static Minute DecideStartMinute(Half half)
    {
        switch (half)
        {
            case Half.First:
                return new Minute(1);
            case Half.Second:
                return new Minute(46);
            case Half.ExtraTime:
                return new Minute(91);
            default:
                return new Minute(1);
        }
    }

    static float CalculateHomePossession(Team home, Team away)
    {
        return home.Control / (float)(home.Control + away.Control);
    }

    public float CalculateRandomness()
    {
        float randomRange = 5 + (100-home.Tactic.Stability) / 25f + (100-away.Tactic.Stability) / 100f * 6f;
        return Random.Range(-randomRange, randomRange);
    }


    public float ResolvePhase(Phase.Parameters parameters, Team attacking, Team defending,
                                float overflow = 0f, bool debug = false)
    {
        int attackingAbility = (int)WeightedAverage(parameters.AttackingStats);
        int attackingTactic = (int)WeightedAverage(parameters.AttackingTactics);
        int defendingAbility = (int)WeightedAverage(parameters.DefendingStats);
        int defendingTactic = (int)WeightedAverage(parameters.DefendingTactics);

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
        {
            result = Mathf.Pow(score - parameters.SuccessThreshold, 0.75f);
        }
        else if (score < parameters.FailThreshold)
        {
            result = -Mathf.Pow(Mathf.Abs(score + parameters.FailThreshold), 0.75f);
        }

        if (debug)
        {
            Debug.Log($"== {parameters.PhaseName.ToUpper()} PHASE ==");
            Debug.Log($"Attacking Team: {attacking.TeamName} | Tactic: {attackingTactic}, Ability: {attackingAbility}");
            Debug.Log($"Defending Team: {defending.TeamName} | Tactic: {defendingTactic}, Ability: {defendingAbility}");
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
        var atkStats = attacking.Defenders.AverageStats();
        var defStats = defending.Attackers.AverageStats();

        var parameters = new Phase.Parameters
        {
            AttackingStats = new()
           {
               (atkStats.Passing, 0.5f),
               (atkStats.Intelligence, 0.5f),
               (atkStats.Positioning, 0.2f),
               (atkStats.Composure, 0.2f)
           },
            AttackingTactics = new()
           {
               (attacking.Tactic.Control, 0.5f),
               (attacking.Tactic.Stability, 0.3f),
               (attacking.Tactic.Security, 0.15f)
           },
            DefendingStats = new()
           {
               (defStats.Positioning, 0.5f),
               (defStats.Intelligence, 0.3f),
               (defStats.Teamwork, 0.2f),
               (defStats.Aggression, 0.1f),
               (defStats.Tackling, 0.1f)
           },
            DefendingTactics = new()
           {
               (defending.Tactic.Pressure, 1f),
               (defending.Tactic.Intensity, 0.2f)
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
        var atkStats = attacking.Midfielders.AverageStats();
        var defStats = defending.Midfielders.AverageStats();

        var parameters = new Phase.Parameters
        {
            AttackingStats = new()
           {
               (atkStats.Passing, 0.4f),
               (atkStats.Intelligence, 0.2f),
               (atkStats.Positioning, 0.2f),
               (atkStats.Creativity, 0.2f),
               (atkStats.Dribbling, 0.2f)
           },
            AttackingTactics = new()
           {
               (attacking.Tactic.Stability, 0.5f),
               (attacking.Tactic.Control, 0.3f),
               (attacking.Tactic.Security, 0.1f)
           },
            DefendingStats = new()
           {
               (defStats.Positioning, 0.5f),
               (defStats.Intelligence, 0.4f),
               (defStats.Teamwork, 0.2f),
               (defStats.Aggression, 0.1f),
               (defStats.Tackling, 0.1f)
           },
            DefendingTactics = new()
           {
               (defending.Tactic.Pressure, 1f),
               (defending.Tactic.Intensity, 0.2f),
               (defending.Tactic.Stability, 0.1f)
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
        var atkStats = attacking.Attackers.Union(attacking.Midfielders).ToArray().AverageStats();
        var defStats = defending.Defenders.AverageStats();

        var parameters = new Phase.Parameters
        {
            AttackingStats = new()
           {
               (atkStats.Intelligence, 0.4f),
               (atkStats.Passing, 0.3f),
               (atkStats.Composure, 0.3f),
               (atkStats.Positioning, 0.3f),
               (atkStats.Creativity, 0.2f),
               (atkStats.Dribbling, 0.2f),
               (atkStats.Crossing, 0.1f)
           },
            AttackingTactics = new()
           {
               (attacking.Tactic.Creativity, 0.5f),
               (attacking.Tactic.Control, 0.3f),
               (attacking.Tactic.Stability, 0.3f),
               (attacking.Tactic.Pressure, 0.2f)
           },
            DefendingStats = new()
           {
               (defStats.Positioning, 0.5f),
               (defStats.Teamwork, 0.4f),
               (defStats.Composure, 0.2f),
               (defStats.Aggression, 0.2f),
               (defStats.Tackling, 0.2f)
           },
            DefendingTactics = new()
           {
               (defending.Tactic.Security, 0.5f),
               (Mathf.Max(40, 100 - defending.Tactic.DefensiveWidth), 0.3f),
               (defending.Tactic.Pressure, 0.2f)
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
        var atkStats = attacking.Midfielders.AverageStats();
        var defStats = defending.Midfielders.AverageStats();

        var parameters = new Phase.Parameters
        {
            AttackingStats = new()
           {
               (atkStats.Creativity, 0.5f),
               (atkStats.Passing, 0.3f),
               (atkStats.Crossing, 0.2f),
               (atkStats.Dribbling, 0.2f),
               (atkStats.Teamwork, 0.2f),
               (atkStats.Pace, 0.2f)
           },
            AttackingTactics = new()
           {
               (attacking.Tactic.Creativity, 0.5f),
               (attacking.Tactic.Intensity, 0.3f),
               (attacking.Tactic.Security, 0.1f)
           },
            DefendingStats = new()
           {
               (defStats.Positioning, 0.5f),
               (defStats.Tackling, 0.3f),
               (defStats.Teamwork, 0.3f),
               (defStats.Aggression, 0.2f)
           },
            DefendingTactics = new()
           {
               (defending.Tactic.Pressure, 0.5f),
               (defending.Tactic.Security, 0.3f),
               (defending.Tactic.Stability, 0.1f)
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
        var atkStats = attacking.Attackers.AverageStats();
        var defStats = defending.Defenders.AverageStats();

        var parameters = new Phase.Parameters
        {
            AttackingStats = new()
           {
               (atkStats.Creativity, 0.5f),
               (atkStats.Crossing, 0.3f),
               (atkStats.Dribbling, 0.3f),
               (atkStats.Passing, 0.2f),
               (atkStats.Teamwork, 0.2f),
               (atkStats.Pace, 0.1f),
           },
            AttackingTactics = new()
           {
               (attacking.Tactic.Creativity, 1f),
               (attacking.Tactic.Intensity, 0.4f),
               (attacking.Tactic.Security, 0.1f)
           },
            DefendingStats = new()
           {
               (defStats.Positioning, 0.5f),
               (defStats.Tackling, 0.4f),
               (defStats.Teamwork, 0.4f),
               (defStats.Aggression, 0.2f),
               (defStats.Strength, 0.2f)
           },
            DefendingTactics = new()
           {
               (defending.Tactic.Security, 0.5f),
               (defending.Tactic.Control, 0.2f),
               (defending.Tactic.Stability, 0.2f)
           },
            PhaseName = "Strike",
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
        var atkStats = attacking.Midfielders.Union(attacking.Defenders).ToArray().AverageStats();
        var defStats = defending.Midfielders.Union(defending.Attackers).ToArray().AverageStats();

        var parameters = new Phase.Parameters
        {
            AttackingStats = new()
           {
               (atkStats.Pace, 0.6f),
               (atkStats.Creativity, 0.3f),
               (atkStats.Crossing, 0.2f),
               (atkStats.Dribbling, 0.2f),
               (atkStats.Passing, 0.2f)
           },
            AttackingTactics = new()
           {
               (Mathf.Max(20, 100 - attacking.Tactic.Stability), 0.5f),
               (attacking.Tactic.Intensity, 0.5f)
           },
            DefendingStats = new()
           {
               (defStats.Pace, 0.6f),
               (defStats.Tackling, 0.3f),
               (defStats.Positioning, 0.2f),
               (defStats.Aggression, 0.1f)
           },
            DefendingTactics = new()
           {
               (defending.Tactic.Security, 1f),
               (defending.Tactic.Stability, 0.3f),
               (defending.Tactic.Pressure, 0.1f)
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
        var atkStats = attacking.Attackers.AverageStats();
        var defStats = defending.Defenders.AverageStats();

        var parameters = new Phase.Parameters
        {
            AttackingStats = new()
           {
               (atkStats.Creativity, 0.4f),
               (atkStats.Pace, 0.4f),
               (atkStats.Crossing, 0.2f),
               (atkStats.Dribbling, 0.2f),
               (atkStats.Passing, 0.1f)
           },
            AttackingTactics = new()
           {
               (attacking.Tactic.Creativity, 1f),
               (attacking.Tactic.Intensity, 0.5f)
           },
            DefendingStats = new()
           {
               (defStats.Pace, 0.4f),
               (defStats.Tackling, 0.3f),
               (defStats.Positioning, 0.2f)
           },
            DefendingTactics = new()
           {
               (defending.Tactic.Security, 1f),
               (defending.Tactic.Stability, 0.2f)
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


    public void SimulateProbabilites(float xg)
    {
        int count = 0;
        int goals = 0;
        for(int i = 0; i < 10000; i++)
        {
            bool result = AttemptShot(Phase.Type.Penetrate, home, away, new Minute(1), xg);
            count++;
            if (result) goals++;
        }
        Debug.Log($"Simulated with xG {xg:F2}: {(float)goals / count:F2}");
    }
}
