using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Game;

[System.Serializable]
[CreateAssetMenu(fileName = "New Team", menuName = "Team")]
public class Team : ScriptableObject
{
    public string Name;
    public int YearFounded;
    public Color TeamColor;

    int teamId;

    public List<Player> Players = new List<Player>();
    public List<Player> Defenders => Players.FindAll(player => player.IsPositionIn(Player.Position.CB, Player.Position.LB, Player.Position.RB, Player.Position.DM));
    public List<Player> Midfielders => Players.FindAll(player => player.IsPositionIn(Player.Position.CM, Player.Position.DM, Player.Position.AM, Player.Position.LM, Player.Position.RM));
    public List<Player> Attackers => Players.FindAll(player => player.IsPositionIn(Player.Position.ST, Player.Position.LW, Player.Position.RW, Player.Position.AM));
    public Player Goalkeeper => Players.Find(player => player.IsPositionIn(Player.Position.GK));
    public List<Player> WidePlayers => Players.FindAll(player => player.IsPositionIn(Player.Position.LB, Player.Position.RB, Player.Position.LM, Player.Position.RM, Player.Position.LW, Player.Position.RW));
    public List<Player> StartingPlayers => Players.GetRange(0, 11);
    public List<Player> Substitutes => Players.GetRange(11, Players.Count - 11);

    public Dictionary<Player, int> KitNumbers;

    public Manager Manager { get; private set; }
    public Tactic Tactic { get; private set; }
    public void SetTactic(Tactic newTactic)
    {
        Tactic = newTactic;
    }

    public int Security => (int)Average(AvgDefending, Tactic.Security);
    public int Threat => (int)Average(AvgAttacking, Tactic.Threat);
    public int Control => (int)Average(AvgControl, Tactic.Control);
    public int Creativity => (int)Average(AvgAttacking, Tactic.Creativity);
    public int Intensity => Tactic.Intensity;
    public int Stability => Tactic.Stability;
    public int Pressure => Tactic.Pressure;
    public int DefensiveWidth => Tactic.DefensiveWidth;
    public int AttackingWidth => Tactic.AttackingWidth;
    public int Fouling => Tactic.Fouling;
    public int Provoking => Tactic.Provoking;



    public void SetTeamId(int id) { teamId = id; }
    public int TeamId => teamId;
    public int AvgAttacking => (int)WeightedAverage(((float)Players.Average(x => x.GetStats().Attacking), 0.5f), ((float)Attackers.Average(x => x.GetStats().Attacking), 3));
    public int AvgDefending => (int)WeightedAverage(((float)Players.Average(x => x.GetStats().Defending), 0.5f), ((float)Defenders.Average(x => x.GetStats().Defending), 3), (Goalkeeper.GetStats().Goalkeeping, 2));
    public int AvgMental => (int)Players.Average(x => x.RawStats.Mental);
    public int AvgPhysical => (int)Players.Average(x => x.RawStats.Physical);
    public int AvgControl => (int)Players.Average(x => Average(x.GetStats().Intelligence, x.GetStats().Teamwork, x.GetStats().Passing));
    public Formation Formation => Tactic.Formation;

    public void GenerateTeam()
    {
        Manager = new Manager(this);
        Tactic = new Tactic(this, Manager);
        KitNumbers = new Dictionary<Player, int>();

        for (int i = 0; i < 21; i++)
        {
            var newPlayer = new Player(this, Tactic.Formation.Positions, i);
            Players.Add(newPlayer);
            KitNumbers.Add(newPlayer, i + 1);
        }
    }

    public int GetPlayerIndex(Player player)
    {
        int index = -1;

        if (Players.Contains(player))
        {
            index = Players.IndexOf(player);
        }

        return index;
    }

    public List<Player> OrderPlayers()
    {
        List<Player> ordered = Players.GetRange(0, 11);
        List<Player> subs = Players.GetRange(11, Players.Count - 11);
        ordered = ordered.OrderBy(player => Tactic.Formation.Positions[player.GetTeamIndex()].ID).ToList();

        ordered.AddRange(subs);

        return ordered;
    }

    public Player.Stats AverageStartingStats()
    {
        return StartingPlayers.AverageStats();
    }

    public List<Player> GetGroup(PlayerGroup group)
    {
        switch (group)
        {
            case PlayerGroup.Goalkeeper:
                return Goalkeeper != null ? new List<Player> { Goalkeeper } : new List<Player>();

            case PlayerGroup.Defenders:
                return Defenders;

            case PlayerGroup.Midfielders:
                return Midfielders;

            case PlayerGroup.Attackers:
                return Attackers;

            case PlayerGroup.Outfield:
                return StartingPlayers.FindAll(p => p != Goalkeeper);

            case PlayerGroup.DefenseAndMidfield:
                return Defenders.Union(Midfielders).ToList();

            case PlayerGroup.MidfieldAndAttack:
                return Midfielders.Union(Attackers).ToList();

            case PlayerGroup.GoalkeeperAndDefense:
                var result = new List<Player>();
                if (Goalkeeper != null) result.Add(Goalkeeper);
                result.AddRange(Defenders);
                return result;

            case PlayerGroup.WidePlayers:
                return WidePlayers;

            default:
                return new List<Player>();
        }
    }

    public List<Competition> GetAllCompetitions()
    {
        return FixturesManager.Instance.Competitions.Where(c => c.Teams.Contains(this)).ToList();
    }
    public League GetMainLeague()
    {
        return (League)GetAllCompetitions().OrderBy(c => c.Priority).FirstOrDefault();
    }
    public Fixture GetUpcomingFixture()
    {
        return FixturesManager.Instance.GetUpcomingFixturesForTeam(this).FirstOrDefault();
    }
}