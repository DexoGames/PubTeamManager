using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives the half-time team talk and converts the result into a squad morale swing. The active flow is two-stage:
/// one squad-wide tone (<see cref="DeliverTalk"/> — Praise/Rage/Encourage/…) followed by up to
/// <see cref="MaxIndividualTalks"/> capped PRIVATE words (<see cref="TalkToPlayer"/>) with players you judge need
/// one. Both stages run through the same discussion reaction path (<see cref="React"/>). The trade-off (core design
/// rule): a mistimed tone flattens the room, and individual talks are scarce — under the deliberate ambiguity of the
/// dressing-room chatter (<see cref="TeamTalkChatter"/>) you'll sometimes spend one on a player who was fine.
///
/// The older minigame path (<see cref="CreateRandom"/>/<see cref="ApplyResult"/>) is kept dormant.
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

    [Header("Individual talks")]
    [Tooltip("How many players you may single out for a private word AFTER the squad tone, each match.")]
    [SerializeField] private int maxIndividualTalks = 2;
    [Tooltip("A personal word is a stronger slump-lifter than the blanket talk — multiplies the recovery chance.")]
    [SerializeField, Range(1f, 2f)] private float individualRecoveryMultiplier = 1.3f;

    /// <summary>The active minigame, if one is in progress.</summary>
    public TeamTalkMinigame Current { get; private set; }

    /// <summary>True once the squad team talk has been used this match (only one allowed).</summary>
    public bool Used { get; private set; }

    /// <summary>How many private words you get after the squad tone (a difficulty dial).</summary>
    public int MaxIndividualTalks => maxIndividualTalks;

    /// <summary>Private words spent this match (reset at kickoff).</summary>
    public int IndividualTalksUsed { get; private set; }

    /// <summary>Private words still available this match.</summary>
    public bool CanTalkIndividually => IndividualTalksUsed < maxIndividualTalks;

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

    /// <summary>Resets the one-per-match flags — call at kickoff.</summary>
    public void ResetForMatch()
    {
        Used = false;
        Current = null;
        IndividualTalksUsed = 0;
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

        Team me = TeamManager.Instance.MyTeam;
        var table = EventsManager.Instance.ReactionTable;
        if (table == null) return reactions;

        foreach (var p in me.Players)
            reactions.Add(React(p, response, severity, 1f));

        Used = true;
        return reactions;
    }

    /// <summary>
    /// A private word with ONE player after the squad tone (up to <see cref="MaxIndividualTalks"/> per match). Same
    /// reaction path as the squad talk, but a personal word is a STRONGER slump-lifter (recovery ×
    /// <see cref="individualRecoveryMultiplier"/>). The trade-off (core design rule): talks are scarce and the tone
    /// can still backfire — under the ambiguity of the chatter you may spend one on a player who was fine.
    /// </summary>
    public PlayerReaction TalkToPlayer(Player player, Event.Response response, EventType.Severity severity)
    {
        PlayerReaction reaction = React(player, response, severity, individualRecoveryMultiplier);
        IndividualTalksUsed++;
        return reaction;
    }

    /// <summary>
    /// The shared per-player reaction path (squad talk and individual talk both run through this): look the personality
    /// reaction up in the discussion <c>ReactionTable</c>, bend it by the scoreline severity, apply the morale swing
    /// via <c>Person.NewMorale</c>, and roll whether a well-received talk snaps them out of a slump.
    /// </summary>
    private PlayerReaction React(Player p, Event.Response response, EventType.Severity severity, float recoveryMultiplier)
    {
        var table = EventsManager.Instance.ReactionTable;
        Event.Reaction reaction = table.TryGetValue((response, p.Personality), out var r) ? r : Event.Reaction.Neutral;

        reaction = Event.ReactionSeverityChange(response, reaction, severity);
        (int mood, int passion) = p.NewMorale(0, reaction, severity); // applies + clamps morale, returns the delta

        bool wasInSlump = p.InSlump;
        if (wasInSlump)
        {
            float chance = Mathf.Clamp01(MoraleEffects.SlumpRecoveryChance(reaction) * recoveryMultiplier);
            if (Random.value < chance) p.InSlump = false;
        }

        return new PlayerReaction
        {
            player = p, reaction = reaction, mood = mood, passion = passion,
            wasInSlump = wasInSlump, recoveredFromSlump = wasInSlump && !p.InSlump
        };
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

        foreach (var p in TeamManager.Instance.MyTeam.Players)
        {
            p.Morale.Mood = Mathf.Clamp(p.Morale.Mood + mood, 0, 100);
            p.Morale.Passion = Mathf.Clamp(p.Morale.Passion + passion, 0, 100);
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
