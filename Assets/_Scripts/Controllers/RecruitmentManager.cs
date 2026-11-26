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
    public List<Player> FormerPlayers { get; } = new List<Player>();

    DateTime sessionUsedDay = DateTime.MinValue;

    [Header("Debug")]
    [Tooltip("TESTING ONLY — makes EVERY day an interview day with no once-per-day limit, so you can keep " +
             "interviewing without waiting for the fortnightly schedule. Toggle live in the Inspector; leave off for normal play.")]
    [SerializeField] private bool debugEndlessInterviews = false;

    /// <summary>TESTING: every day is an interview day with unlimited sessions and a topped-up candidate pool.</summary>
    public bool DebugEndlessInterviews { get => debugEndlessInterviews; set => debugEndlessInterviews = value; }

    private DateTime CurrentDay => CalenderManager.Instance != null ? CalenderManager.Instance.CurrentDay.Date : DateTime.MinValue;

    /// <summary>True when today is the scheduled interview day (always true with <see cref="DebugEndlessInterviews"/>).</summary>
    public bool IsInterviewDay => debugEndlessInterviews || (InterviewDay.Date == CurrentDay && CurrentDay != DateTime.MinValue);

    /// <summary>True when an interview session can still be started today (no daily limit in debug mode).</summary>
    public bool CanInterviewToday => debugEndlessInterviews || (IsInterviewDay && sessionUsedDay.Date != CurrentDay);

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
        if (IsSessionActive && sessionUsedDay == CurrentDay) return CurrentCandidates;
        if (!CanInterviewToday) return CurrentCandidates;
        InterviewManager.Instance.EndInterview();

        if (debugEndlessInterviews) RefreshPool(); // keep fresh candidates flowing while testing

        CurrentCandidates.Clear();
        CurrentCandidateIndex = 0;
        ReviewedBeforeLoad = 0;
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
        if (!IsSessionActive || sessionUsedDay != CurrentDay || CurrentCandidateIndex >= CurrentCandidates.Count)
            return null;

        return CurrentCandidates[CurrentCandidateIndex];
    }

    /// <summary>
    /// Hires the current candidate — adds them to the player's team.
    /// Ends the interview session (max 1 hire per session).
    /// </summary>
    public bool HirePlayer(Player player)
    {
        return HirePlayerReplacing(player, null);
    }

    public bool HirePlayerReplacing(Player candidate, Player outgoing)
    {
        Team team = TeamManager.Instance.MyTeam;
        if (candidate == null || candidate != GetCurrentCandidate() || !FreeAgentPool.Contains(candidate)) return false;
        if (IsSquadFull && outgoing == null) return false;
        if (outgoing != null && !team.Players.Contains(outgoing)) return false;

        int shirt = 1;
        int index = team.Players.Count;
        if (outgoing != null)
        {
            index = team.Players.IndexOf(outgoing);
            shirt = outgoing.GetKitNumber();
            team.KitNumbers.Remove(outgoing);
            team.Players.RemoveAt(index);
            outgoing.Team = null;
            FormerPlayers.Add(outgoing);
            TrainingManager.Instance.CurrentSession?.SelectedPlayerIds.Remove(outgoing.PersonID);
        }

        while (team.KitNumbers.ContainsValue(shirt)) shirt++;
        team.Players.Insert(index, candidate);
        team.KitNumbers[candidate] = shirt;
        candidate.Team = team;
        FreeAgentPool.Remove(candidate);
        team.Tactic.RefreshReliances();
        HasHiredThisSession = true;
        EndSession();
        return true;
    }

    public void RejectPlayer(Player player)
    {
        if (player != GetCurrentCandidate() || player == null) return;

        FreeAgentPool.Remove(player);
        SkipCandidate();
    }

    public void SkipCandidate()
    {
        CurrentCandidateIndex++;
        if (CurrentCandidateIndex >= CurrentCandidates.Count) EndSession();
    }

    public RecruitmentState CaptureState()
    {
        var state = new RecruitmentState { Day = sessionUsedDay, Signed = HasHiredThisSession };
        if (GetCurrentCandidate() != null)
        {
            state.Reviewed = ReviewedBeforeLoad + CurrentCandidateIndex;
            state.Candidates = CurrentCandidates.Skip(CurrentCandidateIndex).Select(p => p.PersonID).ToList();
            InterviewManager.Instance.CaptureInterview(state);
        }
        return state;
    }

    public int ReviewedBeforeLoad { get; private set; }

    public void RestoreSession(RecruitmentState state)
    {
        if (state == null) return;
        sessionUsedDay = state.Day;
        HasHiredThisSession = state.Signed;
        if (sessionUsedDay != CurrentDay || state.Candidates.Count == 0) return;

        CurrentCandidates = state.Candidates.Select(PersonManager.Instance.GetPlayer).Where(FreeAgentPool.Contains).ToList();
        CurrentCandidateIndex = 0;
        ReviewedBeforeLoad = state.Reviewed;
        IsSessionActive = CurrentCandidates.Count > 0;
        if (state.InConversation && IsSessionActive) InterviewManager.Instance.RestoreInterview(GetCurrentCandidate(), state);
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
            player.EnsureStatData();
            FreeAgentPool.Add(player);
            PersonManager.Instance.RegisterExisting(player);
        }

        Debug.Log($"[Restore] Restored {FreeAgentPool.Count} free agents");
    }
}
