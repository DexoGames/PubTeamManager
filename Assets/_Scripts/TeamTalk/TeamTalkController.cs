using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives the half-time team talk and converts the result into a squad morale swing. Two paths exist:
/// the tone-based talk (<see cref="DeliverTalk"/>, the active one — Praise/Rage/Encourage/…) and the older
/// minigame path (<see cref="CreateRandom"/>/<see cref="ApplyResult"/>, kept dormant for now).
/// One team talk per match. The trade-off (core design rule): a strong performance fires the squad up,
/// but a flop actually flattens them — attempting a rousing talk and bottling it costs you.
///
/// The UI (a <c>TeamTalkUI</c> built in the editor — see TEAM_TALK_UI.md) asks <see cref="CreateRandom"/>
/// for a game, lets the player solve it, then calls <see cref="ApplyResult"/> with the finished game.
/// </summary>
public class TeamTalkController : MonoBehaviour
{
    public static TeamTalkController Instance { get; private set; }

    [Header("Reward tuning")]
    [Tooltip("Mood swing from a perfect team talk (a flop subtracts).")]
    [SerializeField] private int maxMoodSwing = 10;
    [Tooltip("Passion swing from a perfect team talk (a flop subtracts).")]
    [SerializeField] private int maxPassionSwing = 8;
    [Tooltip("Scores below this are a flop and DENT morale.")]
    [SerializeField, Range(0f, 1f)] private float flopThreshold = 0.4f;

    /// <summary>The active minigame, if one is in progress.</summary>
    public TeamTalkMinigame Current { get; private set; }

    /// <summary>True once a team talk has been used this match (only one allowed).</summary>
    public bool Used { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    /// <summary>Picks a random microgame for the player to attempt.</summary>
    public TeamTalkMinigame CreateRandom()
    {
        Current = Random.value < 0.5f ? (TeamTalkMinigame)new GerrymanderGame() : new BalanceGame();
        return Current;
    }

    /// <summary>Resets the one-per-match flag — call at kickoff.</summary>
    public void ResetForMatch()
    {
        Used = false;
        Current = null;
    }

    /// <summary>
    /// Delivers a team talk to the WHOLE squad using the same reaction system as a 1-on-1 player discussion:
    /// each player's reaction is looked up from <c>EventsManager.ReactionTable[(response, personality)]</c>,
    /// adjusted by <paramref name="severity"/> (derived from the half-time scoreline), and applied via
    /// <c>Person.NewMorale</c>. Marks the talk used and returns the per-player reactions for the UI to visualise.
    /// </summary>
    public List<PlayerReaction> DeliverTalk(Event.Response response, EventType.Severity severity)
    {
        var reactions = new List<PlayerReaction>();

        Team me = TeamManager.Instance != null ? TeamManager.Instance.MyTeam : null;
        if (me == null || me.Players == null || EventsManager.Instance == null) return reactions;

        var table = EventsManager.Instance.ReactionTable;
        if (table == null) return reactions;

        foreach (var p in me.Players)
        {
            if (!table.TryGetValue((response, p.Personality), out Event.Reaction reaction)) continue;

            reaction = Event.ReactionSeverityChange(response, reaction, severity);
            (int mood, int passion) = p.NewMorale(0, reaction, severity); // applies + clamps morale, returns the delta

            reactions.Add(new PlayerReaction { player = p, reaction = reaction, mood = mood, passion = passion });
        }

        Used = true;
        return reactions;
    }

    /// <summary>
    /// Applies the morale swing for a finished game across the squad and marks the talk used.
    /// Returns a short summary line for the UI.
    /// </summary>
    public string ApplyResult(TeamTalkMinigame game)
    {
        if (game == null) return "";

        float score = game.IsComplete ? game.Score : 0f;
        Used = true;
        Current = null;

        // Map score onto a swing: at/above flopThreshold it's positive (up to max); below it goes negative.
        float t = (score - flopThreshold) / Mathf.Max(0.0001f, 1f - flopThreshold);
        t = Mathf.Clamp(t, -1f, 1f);

        int mood = Mathf.RoundToInt(maxMoodSwing * t);
        int passion = Mathf.RoundToInt(maxPassionSwing * t);

        Team me = TeamManager.Instance != null ? TeamManager.Instance.MyTeam : null;
        if (me != null)
        {
            foreach (var p in me.Players)
            {
                p.Morale.Mood = Mathf.Clamp(p.Morale.Mood + mood, 0, 100);
                p.Morale.Passion = Mathf.Clamp(p.Morale.Passion + passion, 0, 100);
            }
        }

        return Summarise(score, mood);
    }

    private string Summarise(float score, int mood)
    {
        string verdict;
        if (score >= 0.85f) verdict = "The lads are buzzing — brilliant team talk!";
        else if (score >= flopThreshold) verdict = "A solid word — the squad looks lifted.";
        else verdict = "That fell flat — the dressing room looks deflated.";

        string delta = mood >= 0 ? $"+{mood}" : mood.ToString();
        return $"{verdict} (Mood {delta})";
    }
}
