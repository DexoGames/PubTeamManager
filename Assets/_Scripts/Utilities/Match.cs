using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using static Game;

public class Match
{
    public struct Goal
    {
        public Team team;
        public Player scorer;
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
        public List<Goal> goals;
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

        SimulatePlay(attackingTeam, defendingTeam, currentMinute);
    }

    private void SimulatePlay(Team attackingTeam, Team defendingTeam, Minute currentMinute)
    {
        PlayState state = PlayState.Build;
        bool possessionLost = false;

        while (!possessionLost)
        {
            switch (state)
            {
                case PlayState.Build:
                    state = TransitionState(attackingTeam.Control, PlayState.Retain, ref possessionLost);
                    break;

                case PlayState.Retain:
                    state = TransitionState(attackingTeam.Stability, PlayState.Probe, ref possessionLost);
                    break;

                case PlayState.Probe:
                    if (Random.Range(0f, 1f) < attackingTeam.Creativity / 100f)
                        state = PlayState.Advance;
                    else if (Random.Range(0f, 1f) < defendingTeam.Pressure / 100f)
                        possessionLost = true;
                    else
                        AttemptShot(attackingTeam, defendingTeam, currentMinute, 0.1f);
                    break;

                case PlayState.Advance:
                    if (Random.Range(0f, 1f) < attackingTeam.Threat / 100f)
                        state = PlayState.Attack;
                    else if (Random.Range(0f, 1f) < defendingTeam.Pressure / 100f)
                        possessionLost = true;
                    else
                        AttemptShot(attackingTeam, defendingTeam, currentMinute, 0.2f);
                    break;

                case PlayState.Attack:
                    if (Random.Range(0f, 1f) < attackingTeam.Intensity / 100f)
                        state = PlayState.Penetrate;
                    else if (Random.Range(0f, 1f) < defendingTeam.Security / 100f)
                        possessionLost = true;
                    else
                        AttemptShot(attackingTeam, defendingTeam, currentMinute, 0.3f);
                    break;

                case PlayState.Penetrate:
                    AttemptShot(attackingTeam, defendingTeam, currentMinute, 0.5f);
                    possessionLost = true;
                    break;
            }
        }
    }

    private PlayState TransitionState(int stat, PlayState nextState, ref bool possessionLost)
    {
        if (Random.Range(0f, 1f) < stat / 100f)
            return nextState;
        else
        {
            possessionLost = true;
            return PlayState.Build; // Default fallback
        }
    }

    private void AttemptShot(Team attackingTeam, Team defendingTeam, Minute currentMinute, float baseXG)
    {
        Player scorer = SelectWeightedRandomPlayer(attackingTeam.StartingPlayers);
        float xG = baseXG * (scorer.GetStats().Attacking / 100f);

        if (Random.Range(0f, 1f) < xG)
        {
            // Goal scored
            RecordGoal(attackingTeam, scorer, xG, currentMinute);
        }
        else
        {
            // Keeper saves or misses
            if (Random.Range(0f, 1f) < defendingTeam.Goalkeeper.GetStats().Goalkeeping / 100f)
            {
                // Keeper saves
            }
        }
    }

    private void RecordGoal(Team scoringTeam, Player scorer, float xG, Minute minute)
    {
        if (scoringTeam == home)
        {
            result.home.goals.Add(new Goal
            {
                team = home,
                scorer = scorer,
                xG = xG,
                minute = minute
            });
        }
        else
        {
            result.away.goals.Add(new Goal
            {
                team = away,
                scorer = scorer,
                xG = xG,
                minute = minute
            });
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

    private enum PlayState
    {
        Build,
        Retain,
        Probe,
        Advance,
        Attack,
        Penetrate
    }
}
