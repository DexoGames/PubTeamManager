using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Half-time team-talk screen — a "read the room" puzzle. Every squad member is a little morale box along a shallow
/// arch, each carrying a SPEECH BUBBLE (from <see cref="TeamTalkChatter"/>) that's a deliberately ambiguous view onto
/// their hidden state. The flow:
///   1. Open → pre-talk chatter: a few players speak (little to react to yet) — your first read.
///   2. Pick ONE squad tone (shared <see cref="ResponseManager"/>) → the whole squad reacts through the discussion
///      reaction path, severity from the scoreline. Boxes flash the ±delta and now MOST players speak (post-talk).
///   3. Individual phase: single out up to <see cref="TeamTalkController.MaxIndividualTalks"/> players for a private
///      word — a stronger slump-lifter, but scarce, so misreading the chatter wastes one (the trade-off).
///
/// Open it from the Half-Time Panel's "Team Talk" button (hook the button to <see cref="Open"/>).
/// </summary>
public class TeamTalkUI : MonoBehaviour
{
    [Header("Squad arch")]
    [SerializeField] private PlayerMoraleBoxUI boxPrefab;
    [SerializeField] private RectTransform boxContainer;
    [SerializeField] private float arcWidth = 1600f;
    [SerializeField] private float arcDepth = 130f;
    [SerializeField] private float topMargin = 40f;

    [Header("Response buttons")]
    [SerializeField] private ResponseManager responseManager;

    [Header("Feedback")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI situationText; // "Half-time — 1 up — Pleasant · about as expected"
    [SerializeField] private TextMeshProUGUI flavourText;   // the line the manager "said"
    [SerializeField] private TextMeshProUGUI overallText;   // overall verdict

    [Header("Individual talks (optional — wire to enable the private-word phase)")]
    [SerializeField] private GameObject individualPanel;
    [Tooltip("A SECOND ResponseManager, inside the individual popup, for the private-word tone.")]
    [SerializeField] private ResponseManager individualResponseManager;
    [SerializeField] private TextMeshProUGUI individualTargetText;
    [SerializeField] private TextMeshProUGUI talksRemainingText;

    private readonly Dictionary<Player, PlayerMoraleBoxUI> boxes = new();

    private int goalDifference;
    private EventType.Severity severity;
    private Expectation expectation;
    private Event.Response squadTone;
    private Player individualTarget;

    private bool HasIndividualPhase => individualPanel != null && individualResponseManager != null;

    /// <summary>Show the panel, (re)build the squad arch, read the scoreline, and voice the pre-talk chatter.</summary>
    public void Open()
    {
        gameObject.SetActive(true);
        // Take over the whole match screen — the arch needs the full width for a 20+ man squad.
        MatchSimPageUI.Instance.SetMatchContentVisible(false);

        goalDifference = MatchSimPageUI.Instance.CurrentGoalDifferenceForMyTeam();
        severity = TeamTalkReactions.SeverityFromScore(goalDifference);
        expectation = TeamTalkChatter.ExpectationFromScore(
            goalDifference, MatchSimPageUI.Instance.ExpectedGoalDifferenceForMyTeam());

        BuildSquadArch();

        responseManager.OnResponseChosen.RemoveListener(Deliver);
        responseManager.OnResponseChosen.AddListener(Deliver);
        responseManager.SpawnButtons();

        if (HasIndividualPhase)
        {
            individualResponseManager.OnResponseChosen.RemoveListener(DeliverIndividual);
            individualResponseManager.OnResponseChosen.AddListener(DeliverIndividual);
            individualPanel.SetActive(false);
        }

        situationText.text = "Half-time — " + TeamTalkReactions.SituationText(goalDifference, severity)
                           + " · " + TeamTalkChatter.ExpectationText(expectation);
        flavourText.text = "";
        overallText.text = "";

        // Pre-talk read: a few speak, most stay quiet.
        foreach (var kvp in boxes)
            kvp.Value.ShowBubble(TeamTalkChatter.Line(ContextFor(kvp.Key, ChatterPhase.PreTalk)));

        RefreshState();
    }

    /// <summary>Hide the panel (hook a Close/Done button here) and give the match screen back.</summary>
    public void Close()
    {
        if (individualPanel != null) individualPanel.SetActive(false);
        individualTarget = null;
        gameObject.SetActive(false);
        MatchSimPageUI.Instance.SetMatchContentVisible(true);
    }

    private void BuildSquadArch()
    {
        Game.ClearContainer(boxContainer);
        boxes.Clear();

        Team me = TeamManager.Instance.MyTeam;
        int n = me.Players.Count;
        for (int i = 0; i < n; i++)
        {
            Player p = me.Players[i];
            PlayerMoraleBoxUI box = Instantiate(boxPrefab, boxContainer);
            box.Setup(p);

            // // Place along a shallow ∩: even spacing left→right, height peaks in the middle and dips at the ends.
            // var rt = (RectTransform)box.transform;
            // rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            // rt.pivot = new Vector2(0.5f, 1f);

            // float u = n > 1 ? (i / (float)(n - 1)) * 2f - 1f : 0f; // -1 (far left) … +1 (far right)
            // float x = u * (arcWidth * 0.5f);
            // float y = -topMargin - arcDepth * (u * u);             // centre highest, ends lowest
            // rt.anchoredPosition = new Vector2(x, y);

            boxes[p] = box;
        }
    }

    private void Deliver(Event.Response response)
    {
        if (TeamTalkController.Instance.Used) return;

        squadTone = response;
        flavourText.text = TeamTalkReactions.Flavour(response);

        List<PlayerReaction> reactions = TeamTalkController.Instance.DeliverTalk(response, severity);
        foreach (var r in reactions)
            if (boxes.TryGetValue(r.player, out var box))
            {
                box.ShowReaction(r);
                box.ShowBubble(TeamTalkChatter.Line(ContextFor(r, ChatterPhase.PostTalk, response)),
                               TeamTalkReactions.ReactionColour(r.reaction));
            }

        overallText.text = TeamTalkReactions.Summarise(reactions);
        RefreshState(); // used is now true → resumes the individual phase
    }

    // ————————————————————— individual talks —————————————————————

    private void EnterIndividualPhase()
    {
        if (!HasIndividualPhase) return;
        bool available = TeamTalkController.Instance.CanTalkIndividually;
        foreach (var box in boxes.Values) box.SetSelectable(available, OnBoxClicked);
        UpdateTalksRemaining();
    }

    private void OnBoxClicked(Player p)
    {
        if (!TeamTalkController.Instance.CanTalkIndividually) return;

        individualTarget = p;
        if (individualTargetText != null) individualTargetText.text = $"A word with {p.Surname}";
        individualPanel.SetActive(true);
        individualResponseManager.SpawnButtons();
    }

    /// <summary>Back out of an individual talk without spending one (hook a Cancel button on the popup here).</summary>
    public void CancelIndividual()
    {
        if (individualPanel != null) individualPanel.SetActive(false);
        individualTarget = null;
    }

    private void DeliverIndividual(Event.Response tone)
    {
        if (individualTarget == null || !TeamTalkController.Instance.CanTalkIndividually) return;

        PlayerReaction r = TeamTalkController.Instance.TalkToPlayer(individualTarget, tone, severity);
        if (boxes.TryGetValue(individualTarget, out var box))
        {
            box.ShowReaction(r);
            box.ShowBubble(TeamTalkChatter.Line(ContextFor(r, ChatterPhase.Individual, tone)),
                           TeamTalkReactions.ReactionColour(r.reaction));
        }

        individualPanel.SetActive(false);
        individualTarget = null;

        // Out of talks → the boxes stop responding to clicks.
        if (!TeamTalkController.Instance.CanTalkIndividually)
            foreach (var b in boxes.Values) b.SetSelectable(false);

        UpdateTalksRemaining();
    }

    private void UpdateTalksRemaining()
    {
        if (talksRemainingText == null) return;
        int remaining = TeamTalkController.Instance.MaxIndividualTalks - TeamTalkController.Instance.IndividualTalksUsed;
        talksRemainingText.text = TeamTalkController.Instance.Used
            ? $"Private words left: {remaining}"
            : "Deliver a team talk first";
    }

    // ————————————————————— shared context builders —————————————————————

    private TalkContext ContextFor(Player p, ChatterPhase phase) => new TalkContext
    {
        personality = p.Personality,
        phase = phase,
        goalDifference = goalDifference,
        expectation = expectation,
        morale = p.Morale,
        inRut = p.InSlump
    };

    private TalkContext ContextFor(PlayerReaction r, ChatterPhase phase, Event.Response tone) => new TalkContext
    {
        personality = r.player.Personality,
        phase = phase,
        goalDifference = goalDifference,
        expectation = expectation,
        morale = r.player.Morale,
        inRut = r.wasInSlump && !r.recoveredFromSlump,
        tone = tone,
        reaction = r.reaction,
        recovered = r.recoveredFromSlump
    };

    private void RefreshState()
    {
        bool used = TeamTalkController.Instance.Used;

        // Reopening a talk you've already given rebuilds a fresh row, so re-lock it — and resume the individual phase.
        if (used)
        {
            responseManager.SetInteractable(false);
            EnterIndividualPhase();
        }

        titleText.text = used ? "Team Talk — delivered" : "Half-Time Team Talk";
        if (used && string.IsNullOrEmpty(overallText.text))
            overallText.text = "You've had your say — now single out who needs a word.";
    }
}
