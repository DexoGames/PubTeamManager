using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manages the free agent pool and recruitment process.
/// Interview day flow: 5 candidates presented one-at-a-time, hire 1 or reject forever.
/// Interviews are only available ON a scheduled interview day, and only one session per such day.
/// </summary>
public class RecruitmentManager : MonoBehaviour
{
    public static RecruitmentManager Instance { get; private set; }

    public List<Player> FreeAgentPool { get; private set; } = new List<Player>();

    /// <summary>Maximum squad size — to hire when full you must release someone first.</summary>
    public const int MAX_SQUAD_SIZE = 25;

    /// <summary>Set to the day the schedule fires an interview day; interviews are only available then.</summary>
    public DateTime InterviewDay { get; private set; } = DateTime.MinValue;

    /// <summary>The day a session was already used (one interview session per interview day).</summary>
    private DateTime sessionUsedDay = DateTime.MinValue;

    private DateTime CurrentDay => CalenderManager.Instance != null ? CalenderManager.Instance.CurrentDay.Date : DateTime.MinValue;

    /// <summary>True when today is the scheduled interview day.</summary>
    public bool IsInterviewDay => InterviewDay.Date == CurrentDay && CurrentDay != DateTime.MinValue;

    /// <summary>True when an interview session can still be started today.</summary>
    public bool CanInterviewToday => IsInterviewDay && sessionUsedDay.Date != CurrentDay;

    public int SquadSize => TeamManager.Instance != null && TeamManager.Instance.MyTeam != null
        ? TeamManager.Instance.MyTeam.Players.Count : 0;

    /// <summary>True when the squad is at the cap — a new hire requires releasing a player first.</summary>
    public bool IsSquadFull => SquadSize >= MAX_SQUAD_SIZE;

    /// <summary>Current interview session candidates (5 per session).</summary>
    public List<Player> CurrentCandidates { get; private set; } = new List<Player>();

    /// <summary>Index of the current candidate being interviewed (0-4).</summary>
    public int CurrentCandidateIndex { get; private set; } = 0;

    /// <summary>Whether a player has been hired this session.</summary>
    public bool HasHiredThisSession { get; private set; } = false;

    /// <summary>Whether the current interview session is active.</summary>
    public bool IsSessionActive { get; private set; } = false;

    private const int POOL_SIZE = 30;
    private const int CANDIDATES_PER_SESSION = 5;

    private void Awake()
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

    /// <summary>
    /// Generates a fresh pool of free agent players.
    /// Called at game start and periodically to refresh.
    /// </summary>
    public void RefreshPool()
    {
        // Keep existing pool members and top up to POOL_SIZE
        int toGenerate = POOL_SIZE - FreeAgentPool.Count;
        for (int i = 0; i < toGenerate; i++)
        {
            var freeAgent = CreateFreeAgent();
            FreeAgentPool.Add(freeAgent);
        }
    }

    private Player CreateFreeAgent()
    {
        // Create a free agent not attached to any team
        // Use a dummy formation positions array
        var dummyPositions = new Formation.Position[0];
        var player = new Player(null, dummyPositions, -1);
        return player;
    }

    /// <summary>
    /// Called by the schedule when today is an interview day — opens up interviewing for the day and
    /// tops up the free-agent pool with fresh faces.
    /// </summary>
    public void NotifyInterviewDay()
    {
        InterviewDay = CurrentDay;
        RefreshPool();
        Debug.Log("[Recruitment] Interview day — candidates available.");
    }

    /// <summary>
    /// Gets exactly 5 candidates for this interview session and starts the session.
    /// </summary>
    public List<Player> StartInterviewSession()
    {
        CurrentCandidates.Clear();
        CurrentCandidateIndex = 0;
        HasHiredThisSession = false;
        IsSessionActive = true;

        // Pick 5 random candidates from the pool
        var shuffled = FreeAgentPool.OrderBy(x => UnityEngine.Random.value).Take(CANDIDATES_PER_SESSION).ToList();
        CurrentCandidates = shuffled;

        // One session per interview day.
        sessionUsedDay = CurrentDay;

        Debug.Log($"[Recruitment] Interview session started with {CurrentCandidates.Count} candidates.");
        return CurrentCandidates;
    }

    /// <summary>
    /// Gets the current candidate being interviewed.
    /// </summary>
    public Player GetCurrentCandidate()
    {
        if (!IsSessionActive || CurrentCandidateIndex >= CurrentCandidates.Count)
            return null;

        return CurrentCandidates[CurrentCandidateIndex];
    }

    /// <summary>
    /// Hires the current candidate — adds them to the player's team.
    /// Ends the interview session (max 1 hire per session).
    /// </summary>
    public bool HirePlayer(Player player)
    {
        if (HasHiredThisSession || player == null) return false;

        // Squad cap: you must release a player before signing a new one.
        if (IsSquadFull)
        {
            Debug.Log($"[Recruitment] Squad full ({SquadSize}/{MAX_SQUAD_SIZE}) — release a player before hiring.");
            return false;
        }

        Team myTeam = TeamManager.Instance.MyTeam;
        player.Team = myTeam;
        myTeam.Players.Add(player);
        FreeAgentPool.Remove(player);
        CurrentCandidates.Remove(player);
        HasHiredThisSession = true;
        IsSessionActive = false;

        // Free agents are already registered at creation; keep their existing ID.
        PersonManager.Instance.RegisterExisting(player);

        Debug.Log($"[Recruitment] Hired {player.FirstName} {player.Surname}!");
        return true;
    }

    /// <summary>
    /// Releases an existing squad player to make room, then hires the new one. Used when the squad is
    /// full and the manager chooses who to drop for the new signing.
    /// </summary>
    public bool HirePlayerReplacing(Player newPlayer, Player toRelease)
    {
        if (toRelease != null) ReleasePlayer(toRelease, returnToPool: false);
        return HirePlayer(newPlayer);
    }

    /// <summary>
    /// Permanently rejects the current candidate — they are removed forever.
    /// Advances to the next candidate, or ends session if all 5 rejected.
    /// </summary>
    public void RejectPlayer(Player player)
    {
        if (player == null) return;

        FreeAgentPool.Remove(player);
        Debug.Log($"[Recruitment] Rejected {player.FirstName} {player.Surname} (permanently removed).");

        CurrentCandidateIndex++;

        if (CurrentCandidateIndex >= CurrentCandidates.Count)
        {
            // All candidates rejected — session ends
            IsSessionActive = false;
            Debug.Log("[Recruitment] All candidates rejected. Session ended.");
        }
    }

    /// <summary>
    /// Releases a player from the team. Optionally returns them to the free agent pool.
    /// </summary>
    public void ReleasePlayer(Player player, bool returnToPool = false)
    {
        Team myTeam = TeamManager.Instance.MyTeam;
        myTeam.Players.Remove(player);

        if (myTeam.KitNumbers.ContainsKey(player))
            myTeam.KitNumbers.Remove(player);

        if (returnToPool)
        {
            player.Team = null;
            FreeAgentPool.Add(player);
        }

        Debug.Log($"[Recruitment] Released {player.FirstName} {player.Surname}.");
    }

    /// <summary>
    /// Ends the current interview session.
    /// </summary>
    public void EndSession()
    {
        IsSessionActive = false;
        CurrentCandidates.Clear();
        CurrentCandidateIndex = 0;
    }

    /// <summary>
    /// Restores already-deserialized free agents.
    /// Called by SaveManager.RestoreGameState().
    /// </summary>
    public void RestoreFreeAgentsFromState(List<Player> freeAgents)
    {
        FreeAgentPool.Clear();

        foreach (var player in freeAgents)
        {
            FreeAgentPool.Add(player);
            PersonManager.Instance.RegisterExisting(player);
        }

        Debug.Log($"[Restore] Restored {FreeAgentPool.Count} free agents");
    }
}
