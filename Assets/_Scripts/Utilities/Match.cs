using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using static Game;

public class Match
{
    public enum State
    {
        Build, Progress, Probe, Advance, Penetrate, Counter, Break
    }

    public enum ShotType
    {
        Strike, TapIn, Header, Solo, Stylish, Screamer, Penalty, FreeKick, Corner, OwnGoal, Deflection
    }
    public enum ShotOutcome
    {
        Goal, Saved, Post, Miss, BadMiss
    }

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

    public Match(Team home, Team away)
    {
        this.home = home;
        this.away = away;
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
        float possession = CalculateHomePossession(home, away);
        Minute currentMinute = DecideStartMinute(half);

        while (currentMinute.Base <= 45 && currentMinute.Stoppage < Random.Range(1, 3))
        {
            SimulateMinute(currentMinute, possession);
            currentMinute = AdvanceMinute(currentMinute);
        }
    }

    private void SimulateMinute(Minute currentMinute, float possession)
    {
        Team attackingTeam = Random.Range(0f, 1f) < possession ? home : away;
        Team defendingTeam = attackingTeam == home ? away : home;

        
    }

    private float WeightedStat(int stat, float weight)
    {
        return stat * weight;
    }

    private void AttemptShot(ShotType type, Team attackingTeam, Team defendingTeam, Minute currentMinute, float baseXG)
    {
        Player scorer = SelectWeightedRandomPlayer(attackingTeam.StartingPlayers);
        float shootingModidifer = (scorer.GetStats().Shooting-25) * (1-baseXG) / 100f;
        float composureModidifer = (scorer.GetStats().Composure-25) * (baseXG) / 100f;
        float keeperModifier = (defendingTeam.Goalkeeper.GetStats().Goalkeeping-25) * (baseXG) / 200f;
        float odds = baseXG + shootingModidifer + composureModidifer - keeperModifier;
        odds = Mathf.Clamp(odds, 0.015f, 0.985f);

        if (Random.Range(0f, 1f) < odds)
        {
            RecordShot(type, ShotOutcome.Goal, attackingTeam, scorer, baseXG, currentMinute);
        }
        else
        {
            if (Random.Range(0f, 1f) < defendingTeam.Goalkeeper.GetStats().Goalkeeping / 100f)
            {
                // Keeper saves
            }
        }
    }

    void RecordShot(ShotType type, ShotOutcome outcome, Team shootingTeam, Player shooter, float xG, Minute minute)
    {
        Shot shot = new Shot
        {
            type = ShotType.Strike,
            result = ShotOutcome.Goal,
            team = shootingTeam,
            shooter = shooter,
            xG = xG,
            minute = minute,
        };

        if (shootingTeam == home)
        {
            result.home.goals.Add(shot);
        }
        else
        {
            result.away.goals.Add(shot);
        }
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

    float CalculateRandomness()
    {
        float randomRange = 5 + home.Tactic.Stability / 25f + away.Tactic.Stability / 100f*6f;
        return Random.Range(-randomRange, randomRange);
    }

    public void Build(Team attacking, Team defending)
    {
        Player.Stats attackingStats = attacking.Defenders.AverageStats();
        Player.Stats defendingStats = defending.Attackers.AverageStats();

        int buildAbility = (int)WeightedAverage(
            (attackingStats.Passing, 0.5f),
            (attackingStats.Intelligence, 0.5f),
            (attackingStats.Positioning, 0.2f),
            (attackingStats.Composure, 0.2f)
        );

        int buildTactic = (int)WeightedAverage(
            (attacking.Tactic.Control, 0.5f),
            (attacking.Tactic.Stability, 0.3f),
            (attacking.Tactic.Security, 0.15f)
        );

        int pressAbility = (int)WeightedAverage(
            (defendingStats.Positioning, 0.5f),
            (defendingStats.Intelligence, 0.3f),
            (defendingStats.Teamwork, 0.2f),
            (defendingStats.Tackling, 0.1f)
        );

        int pressTactic = (int)WeightedAverage(
            (defending.Tactic.Pressure, 1f),
            (defending.Tactic.Intensity, 0.2f)
        );

        float tacticDiff = buildTactic - pressTactic;
        float abilityDiff = buildAbility - pressAbility;

        float tacticInfluence = 1.0f - Mathf.Clamp01(Mathf.Abs(tacticDiff) / 100f);
        float abilityWeight = tacticInfluence;
        float tacticWeight = 1.0f - tacticInfluence;

        float weightedTactic = tacticWeight * Mathf.Sign(tacticDiff) * Mathf.Pow(Mathf.Abs(tacticDiff), 1f);
        float weightedAbility = abilityWeight * Mathf.Sign(abilityDiff) * Mathf.Pow(Mathf.Abs(abilityDiff), 0.75f);

        float randomness = CalculateRandomness();
        float buildScore = weightedTactic + weightedAbility + randomness;

        float successThreshold = 5f;
        float failThreshold = -5f;

        float overflow = 0f;

        Debug.Log($"buildAbility: {buildAbility}, buildTactic: {buildTactic}");
        Debug.Log($"pressAbility: {pressAbility}, pressTactic: {pressTactic}");
        Debug.Log($"tacticDiff: {tacticDiff}, abilityDiff: {abilityDiff}");
        Debug.Log($"tacticInfluence: {tacticInfluence}, abilityWeight: {abilityWeight}, tacticWeight: {tacticWeight}");
        Debug.Log($"weightedTactic: {weightedTactic}, weightedAbility: {weightedAbility}");
        Debug.Log($"randomness: {randomness}");

        Debug.Log($"BUILD SCORE {home.TeamName} vs {away.TeamName}: {buildScore}");
    }

    public void Progress(Team attacking, Team defending)
    {
        Player.Stats attackingStats = attacking.Midfielders.AverageStats();
        Player.Stats defendingStats = defending.Midfielders.AverageStats();

        int progressAbility = (int)WeightedAverage(
            (attackingStats.Passing, 0.4f),
            (attackingStats.Intelligence, 0.2f),
            (attackingStats.Positioning, 0.2f),
            (attackingStats.Creativity, 0.2f),
            (attackingStats.Dribbling, 0.2f)
        );

        int progressTactic = (int)WeightedAverage(
            (attacking.Tactic.Stability, 0.5f),
            (attacking.Tactic.Control, 0.3f),
            (attacking.Tactic.Security, 0.1f)
        );

        int pressAbility = (int)WeightedAverage(
            (defendingStats.Positioning, 0.5f),
            (defendingStats.Intelligence, 0.4f),
            (defendingStats.Teamwork, 0.2f),
            (defendingStats.Tackling, 0.1f)
        );

        int pressTactic = (int)WeightedAverage(
            (defending.Tactic.Pressure, 1f),
            (defending.Tactic.Intensity, 0.2f),
            (defending.Tactic.Stability, 0.1f)
        );

        float tacticDiff = progressTactic - pressTactic;
        float abilityDiff = progressAbility - pressAbility;

        float tacticInfluence = 1.0f - Mathf.Clamp01(Mathf.Abs(tacticDiff) / 100f);
        float abilityWeight = tacticInfluence;
        float tacticWeight = 1.0f - tacticInfluence;

        float weightedTactic = tacticWeight * Mathf.Sign(tacticDiff) * Mathf.Pow(Mathf.Abs(tacticDiff), 1f);
        float weightedAbility = abilityWeight * Mathf.Sign(abilityDiff) * Mathf.Pow(Mathf.Abs(abilityDiff), 0.75f);

        float randomness = CalculateRandomness();
        float buildScore = weightedTactic + weightedAbility + randomness;

        float successThreshold = 5f;
        float failThreshold = -5f;

        float overflow = 0f;

        Debug.Log($"progressAbility: {progressAbility}, progressTactic: {progressTactic}");
        Debug.Log($"pressAbility: {pressAbility}, pressTactic: {pressTactic}");
        Debug.Log($"tacticDiff: {tacticDiff}, abilityDiff: {abilityDiff}");
        Debug.Log($"tacticInfluence: {tacticInfluence}, abilityWeight: {abilityWeight}, tacticWeight: {tacticWeight}");
        Debug.Log($"weightedTactic: {weightedTactic}, weightedAbility: {weightedAbility}");
        Debug.Log($"randomness: {randomness}");

        Debug.Log($"PROGRESS SCORE {home.TeamName} vs {away.TeamName}: {buildScore}");
    }

    void Counter(Team attacking, Team defending, float overflow)
    {

    }
}
