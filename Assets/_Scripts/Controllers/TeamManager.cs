using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TeamManager : MonoBehaviour
{
    public static TeamManager Instance { get; private set; }

    public void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
        }
    }


    public List<Team> teams;
    List<Team> spawnedTeams = new List<Team>();
    public Team MyTeam => spawnedTeams[0];


    public List<Team> GetAllTeams()
    {
        if(spawnedTeams.Count < 1)
        {
            foreach (var team in teams)
            {
                Team spawnedTeam = Instantiate(team);
                spawnedTeams.Add(spawnedTeam);

                spawnedTeam.GenerateTeam();
                spawnedTeam.SetTeamId(spawnedTeams.Count);
            }
        }

        return spawnedTeams;
    }

    public Team GetTeam(int id)
    {
        return spawnedTeams.FirstOrDefault(x=>x.TeamId == id);
    }
}
