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
            Debug.Log("[TRACE] TeamManager.Awake — DUPLICATE, destroying this GameObject");
            Destroy(this.gameObject);
        }
        else
        {
            Debug.Log("[TRACE] TeamManager.Awake — set Instance");
            Instance = this;
        }
    }


    public List<Team> teams;
    public int numberOfTeams = 81; // 1 player team + 80 AI teams
    private List<Pub> allPubs = new List<Pub>();
    List<Team> spawnedTeams = new List<Team>();
    private readonly Dictionary<int, Team> _byId = new Dictionary<int, Team>();
    public Team MyTeam => spawnedTeams[0];

    /// <summary>The human-controlled team, or null if teams haven't spawned yet — null-safe, unlike
    /// <see cref="MyTeam"/>. Used by <see cref="Team.IsCpuControlled"/> to gate human-only penalties.</summary>
    public Team HumanTeam => spawnedTeams != null && spawnedTeams.Count > 0 ? spawnedTeams[0] : null;

    private void OnEnable()
    {
        Debug.Log("[TRACE] TeamManager.OnEnable — component is enabled");
    }

    private void Start()
    {
        Debug.Log("[TRACE] TeamManager.Start — LoadPubsFromCSV");
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
        Pub playerPub = allPubs.FirstOrDefault(p => p.Name.Contains("The Hobbit"));
        
        if (playerPub == null)
        {
            Debug.LogWarning("Could not find The Five Dials pub, using first pub in list");
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

            // Deterministic kit colours seeded from the pub's identity.
            string colorSeed = $"{selectedPubs[i].FasId}|{selectedPubs[i].Name}|{selectedPubs[i].Postcode}";
            spawnedTeam.TeamColor = KitColors.GetHomeColor(colorSeed);
            spawnedTeam.AwayColor = KitColors.GetAwayColor(colorSeed, spawnedTeam.TeamColor);

            spawnedTeams.Add(spawnedTeam);
        }

        for(int i = 0; i < spawnedTeams.Count; i++)
        {
            var team = spawnedTeams[i];
            team.GenerateTeam();
            team.SetTeamId(IdManager.Instance.AllocateTeamId());
            _byId[team.TeamId] = team;
        }

        Debug.Log($"Spawned {spawnedTeams.Count} teams. Player team: {MyTeam.Name}");
    }

    private List<Pub> SelectPubs(Pub playerPub, int totalCount)
    {
        List<Pub> selected = new List<Pub>();
        selected.Add(playerPub); // First team is always the player's pub

        System.Random rng = new System.Random();
        string playerPrefix = playerPub.PostcodePrefix;

        // --- Tier 1: Same postcode prefix (e.g. "TA19") ---
        List<Pub> samePostcodePubs = allPubs
            .Where(p => p != playerPub && p.PostcodePrefix == playerPrefix)
            .OrderBy(x => rng.Next())
            .ToList();

        int needed = totalCount - selected.Count;
        int take = Mathf.Min(needed, samePostcodePubs.Count);
        selected.AddRange(samePostcodePubs.Take(take));

        if (selected.Count >= totalCount) return selected;

        // --- Tier 2: Same local authority (general area) ---
        List<Pub> sameLAPubs = allPubs
            .Where(p => !selected.Contains(p) && p.LocalAuthority == playerPub.LocalAuthority)
            .OrderBy(x => rng.Next())
            .ToList();

        needed = totalCount - selected.Count;
        take = Mathf.Min(needed, sameLAPubs.Count);
        selected.AddRange(sameLAPubs.Take(take));

        if (selected.Count >= totalCount) return selected;

        // --- Tier 3: Closest by distance ---
        List<Pub> remainingPubs = allPubs
            .Where(p => !selected.Contains(p))
            .OrderBy(p => p.DistanceTo(playerPub))
            .ToList();

        needed = totalCount - selected.Count;
        selected.AddRange(remainingPubs.Take(needed));

        return selected;
    }

    public List<Team> GetAllTeams()
    {
        return spawnedTeams;
    }

    public Team GetTeam(int id)
    {
        return _byId.TryGetValue(id, out Team team) ? team : null;
    }

    /// <summary>
    /// Registers a team into the lookup during save restore, before the full team
    /// list is rebuilt. Lets TeamRefConverter resolve references mid-deserialization.
    /// </summary>
    public void RegisterTeamDuringLoad(Team team)
    {
        if (team != null) _byId[team.TeamId] = team;
    }

    /// <summary>Clears the team lookup — called before a save restore.</summary>
    public void ClearLookup()
    {
        _byId.Clear();
    }

    public List<Pub> GetAllPubs()
    {
        return allPubs;
    }

    /// <summary>
    /// Registers already-deserialized teams (from TeamConverter).
    /// Called by SaveManager.RestoreGameState().
    /// </summary>
    public void RestoreTeamsFromState(List<Team> teams, int playerTeamId)
    {
        spawnedTeams.Clear();
        spawnedTeams.AddRange(teams);

        _byId.Clear();
        foreach (var team in spawnedTeams)
            _byId[team.TeamId] = team;

        // Find and set the player's team as index 0
        int playerIdx = spawnedTeams.FindIndex(t => t.TeamId == playerTeamId);
        if (playerIdx > 0)
        {
            var temp = spawnedTeams[0];
            spawnedTeams[0] = spawnedTeams[playerIdx];
            spawnedTeams[playerIdx] = temp;
        }

        Debug.Log($"[Restore] Restored {spawnedTeams.Count} teams. Player team: {(spawnedTeams.Count > 0 ? MyTeam.Name : "none")}");
    }
}
