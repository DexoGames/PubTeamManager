using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Game;

[System.Serializable]
[CreateAssetMenu(fileName = "New Team", menuName = "Team")]
public class Team : ScriptableObject
{
    public string TeamName;
    public int YearFounded;
    public int StadiumCapacity;

    int teamId;

    public List<Player> Players = new List<Player>();
    public List<Player> Defenders => Players.FindAll(player => player.IsPositionIn(Player.Position.CB, Player.Position.LB, Player.Position.RB, Player.Position.DM));
    public List<Player> Midfielders => Players.FindAll(player => player.IsPositionIn(Player.Position.CM, Player.Position.DM, Player.Position.AM, Player.Position.LM, Player.Position.RM));
    public List<Player> Attackers => Players.FindAll(player => player.IsPositionIn(Player.Position.ST, Player.Position.LW, Player.Position.RW, Player.Position.AM));
    public Player Goalkeeper => Players.Find(player => player.IsPositionIn(Player.Position.GK));
    public List<Player> WidePlayers => Players.FindAll(player => player.IsPositionIn(Player.Position.LB, Player.Position.RB, Player.Position.LM, Player.Position.RM, Player.Position.LW, Player.Position.RW));


    public Manager Manager { get; private set; }
    public Tactic Tactic { get; private set; }
    public void SetTactic(Tactic newTactic)
    {
        Tactic = newTactic;
    }

    public void SetTeamId(int id) { teamId = id; }
    public int TeamId => teamId;
    public int AvgAttacking => (int)WeightedAverage(((float)Players.Average(x => x.GetStats().Attacking), 0.5f), ((float)Attackers.Average(x => x.GetStats().Attacking), 3));
    public int AvgDefending => (int)WeightedAverage(((float)Players.Average(x => x.GetStats().Defending), 0.5f), ((float)Defenders.Average(x => x.GetStats().Defending), 3), (Goalkeeper.GetStats().Goalkeeping, 2));
    public int AvgMental => (int)Players.Average(x => x.RawStats.Mental);
    public int AvgPhysical => (int)Players.Average(x => x.RawStats.Physical);
    public int AvgControl => (int)Players.Average(x => Average(x.GetStats().Skills.Intelligence, x.GetStats().Skills.Teamwork, x.GetStats().Skills.Passing));
    public Formation Formation => Tactic.Formation;

    public void GenerateTeam()
    {
        Manager = new Manager(this);
        Tactic = new Tactic(this, Manager);

        for (int i = 0; i < 21; i++)
        {
            var newPlayer = new Player(this, Tactic.Formation.Positions, i);
            Players.Add(newPlayer);
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
}