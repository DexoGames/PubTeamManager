using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Events;
using static Game;

public enum ShotType
{
    Strike, Tap_In, Header, Solo, Stylish, Screamer, Penalty, Free_Kick, Corner, Own_Goal, Deflection
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

        public Result(Team home, Team away)
        {
            this.home = new TeamStats { team = home, goals = new List<Shot>(), fouls = new List<Foul>() };
            this.away = new TeamStats { team = away, goals = new List<Shot>(), fouls = new List<Foul>() };
        }
    }

    public struct TeamStats
    {
        public Team team;
        public List<Shot> goals;
        public List<Foul> fouls;
        public float possession;
    }

    private readonly Team home;
    private readonly Team away;

    public Result result { get; private set; }
    public Minute currentMin { get; set; }

    public Team HomeTeam => home;
    public Team AwayTeam => away;

    public bool trackHighlights;
    public UnityEvent<Highlight> BroadcastHighlight = new UnityEvent<Highlight>();

    public MatchEngine Engine { get; }

    public Match(Team home, Team away, bool trackHighlights = false)
    {
        this.home = home;
        this.away = away;
        result = new Result(home, away);
        this.trackHighlights = trackHighlights;

        Engine = new MatchEngine(this);
    }

    public Result SimulateMatch()
    {
        SimulateHalf(Half.First);
        SimulateHalf(Half.Second);
        return result;
    }

    private void SimulateHalf(Half half)
    {
        currentMin = DecideStartMinute(half);

        while (HalfConditions(half))
        {
            SimulateMinute();
        }
    }

    public void SimulateMinute()
    {
        Engine.SimulateMinute();
    }

    public bool HalfConditions(Half half)
    {
        return currentMin.Base <= DecideEndMinute(half) && currentMin.Stoppage < Random.Range(1, 8);
    }

    public void AddHighlight(Highlight highlight)
    {
        if (!trackHighlights) return;
        BroadcastHighlight.Invoke(highlight);
    }

    public static Minute DecideStartMinute(Half half)
    {
        switch (half)
        {
            case Half.First: return new Minute(1);
            case Half.Second: return new Minute(46);
            case Half.ExtraTime: return new Minute(91);
            default: return new Minute(1);
        }
    }

    public static int DecideEndMinute(Half half)
    {
        switch (half)
        {
            case Half.First: return 45;
            case Half.Second: return 90;
            case Half.ExtraTime: return 120;
            default: return 90;
        }
    }

    public void RecordShot(ShotType type, ShotOutcome outcome, Team shootingTeam, Player shooter, Player goalkeeper, float xG, Minute minute)
    {
        AddHighlight(new ShotHighlight(shootingTeam, minute, shooter, goalkeeper, type, outcome));

        Shot shot = new Shot
        {
            type = type,
            result = outcome,
            team = shootingTeam,
            shooter = shooter,
            xG = xG,
            minute = minute,
        };

        if (outcome == ShotOutcome.Goal)
        {
            AddHighlight(new GoalHighlight(shootingTeam, minute, shooter, goalkeeper, type, outcome));

            if (shootingTeam == HomeTeam)
                result.home.goals.Add(shot);
            else
                result.away.goals.Add(shot);
        }
        else
        {
            AddHighlight(new MissHighlight(shootingTeam, minute, shooter, goalkeeper, type, outcome));
        }
    }
}
