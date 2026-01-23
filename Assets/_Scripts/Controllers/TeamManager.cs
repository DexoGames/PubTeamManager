using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
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
    public int numberOfTeams = 81; // 1 player team + 80 AI teams
    private List<Pub> allPubs = new List<Pub>();
    List<Team> spawnedTeams = new List<Team>();
    public Team MyTeam => spawnedTeams[0];

    private void Start()
    {
        LoadPubsFromCSV();
    }

    private void LoadPubsFromCSV()
    {
        TextAsset csvFile = Resources.Load<TextAsset>("open_pubs");

        string[] lines = csvFile.text.Split('\n');
        
        // Skip header line
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            string[] values = ParseCSVLine(lines[i]);
            if (values.Length < 9) continue;

            try
            {
                Pub pub = new Pub(
                    fasId: values[0].Trim(),
                    name: values[1].Trim().Trim('"'),
                    address: values[2].Trim().Trim('"'),
                    postcode: values[3].Trim(),
                    easting: float.Parse(values[4].Trim()),
                    northing: float.Parse(values[5].Trim()),
                    latitude: float.Parse(values[6].Trim()),
                    longitude: float.Parse(values[7].Trim()),
                    localAuthority: values[8].Trim()
                );

                allPubs.Add(pub);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to parse line {i}: {e.Message} - Line: {lines[i]}");
            }
        }

        Debug.Log($"Loaded {allPubs.Count} pubs from CSV");
    }

    private string[] ParseCSVLine(string line)
    {
        List<string> result = new List<string>();
        bool inQuotes = false;
        string currentField = "";

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(currentField);
                currentField = "";
            }
            else
            {
                currentField += c;
            }
        }

        // Add the last field
        result.Add(currentField);

        return result.ToArray();
    }

    public void SpawnTeams()
    {
        if (allPubs.Count == 0)
        {
            Debug.LogError("No pubs loaded! Cannot spawn teams.");
            return;
        }

        // Find "The Hobbit" pub in Southampton as the first team
        Pub playerPub = allPubs.FirstOrDefault(p => p.Name.Contains("Hobbit") && p.LocalAuthority.Contains("Southampton"));
        
        if (playerPub == null)
        {
            Debug.LogWarning("Could not find The Hobbit pub, using first pub in list");
            playerPub = allPubs[0];
        }

        List<Pub> selectedPubs = SelectPubs(playerPub, numberOfTeams);

        // Create teams from selected pubs
        for (int i = 0; i < selectedPubs.Count && i < numberOfTeams; i++)
        {
            Team spawnedTeam = null;
            
            // Use first team template if available, otherwise create new instance
            if (teams != null && teams.Count > 0)
            {
                spawnedTeam = Instantiate(teams[0]);
            }
            else
            {
                spawnedTeam = ScriptableObject.CreateInstance<Team>();
            }

            spawnedTeam.Name = selectedPubs[i].Name;
            spawnedTeams.Add(spawnedTeam);
        }

        for(int i = 0; i < spawnedTeams.Count; i++)
        {
            var team = spawnedTeams[i];
            team.GenerateTeam();
            team.SetTeamId(i);
        }

        Debug.Log($"Spawned {spawnedTeams.Count} teams. Player team: {MyTeam.Name}");
    }

    private List<Pub> SelectPubs(Pub playerPub, int totalCount)
    {
        List<Pub> selected = new List<Pub>();
        selected.Add(playerPub); // First team is always the player's pub

        // Get all pubs from the same local authority
        List<Pub> sameLAPubs = allPubs.Where(p => p != playerPub && p.LocalAuthority == playerPub.LocalAuthority).ToList();
        
        // Shuffle for randomness
        System.Random rng = new System.Random();
        sameLAPubs = sameLAPubs.OrderBy(x => rng.Next()).ToList();

        // Add random pubs from same local authority
        int pubsNeeded = totalCount - 1;
        int pubsFromSameLA = Mathf.Min(pubsNeeded, sameLAPubs.Count);
        selected.AddRange(sameLAPubs.Take(pubsFromSameLA));

        // If we need more pubs, get closest ones by distance
        if (selected.Count < totalCount)
        {
            List<Pub> remainingPubs = allPubs.Where(p => !selected.Contains(p)).ToList();
            remainingPubs = remainingPubs.OrderBy(p => p.DistanceTo(playerPub)).ToList();
            
            int additionalNeeded = totalCount - selected.Count;
            selected.AddRange(remainingPubs.Take(additionalNeeded));
        }

        return selected;
    }

    public List<Team> GetAllTeams()
    {
        return spawnedTeams;
    }

    public Team GetTeam(int id)
    {
        return spawnedTeams.FirstOrDefault(x=>x.TeamId == id);
    }

    public List<Pub> GetAllPubs()
    {
        return allPubs;
    }
}
