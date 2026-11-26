using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Half-time team-talk screen — a "read the room" puzzle. Every squad member is a morale box in the squad grid,
/// each carrying a SPEECH BUBBLE (from <see cref="TeamTalkChatter"/>) that's a deliberately ambiguous view onto
/// their hidden state. The flow:
///   1. Open → pre-talk chatter: a few players speak (little to react to yet) — your first read.
///   2. Pick ONE squad tone (shared <see cref="ResponseManager"/>) → the whole squad reacts through the discussion
///      reaction path, severity from the scoreline. Boxes flash the ±delta and now MOST players speak (post-talk).
///   3. Individual phase: single out up to <see cref="TeamTalkController.MaxIndividualTalks"/> players for a private
///      word — a stronger slump-lifter, but scarce, so misreading the chatter wastes one (the trade-off).
///
/// Button hooks: the Half-Time Panel's "Team Talk" → <see cref="Open"/>, the screen's Done → <see cref="Close"/>,
/// the individual popup's Cancel → <see cref="CancelIndividual"/>.
/// </summary>
public class TeamTalkUI : MonoBehaviour
{
    [Header("Squad")]
    [SerializeField] private PlayerMoraleBoxUI boxPrefab;
    [SerializeField] private RectTransform boxContainer;

    [Header("Response buttons")]
    [SerializeField] private ResponseManager responseManager;

    [Header("Feedback")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI situationText;
    [SerializeField] private TextMeshProUGUI flavourText;
    [SerializeField] private TextMeshProUGUI overallText;

    [Header("Individual talks")]
    [SerializeField] private GameObject individualPanel;
    [SerializeField] private ResponseManager individualResponseManager;
    [SerializeField] private TextMeshProUGUI individualTargetText;
    [SerializeField] private TextMeshProUGUI talksRemainingText;

    private readonly Dictionary<Player, PlayerMoraleBoxUI> boxes = new();

    private int goalDifference;
    private EventType.Severity severity;
    private Expectation expectation;
    private Player individualTarget;

    bool prepared;

    public void ResetForMatch()
    {
        prepared = false;
    }

    public void Open()
    {
        gameObject.SetActive(true);
        // Take over the whole match screen — the squad grid needs the full width for a 25-man squad.
        MatchSimPageUI.Instance.SetMatchContentVisible(false);

        if (prepared)
        {
            foreach (var pair in boxes) pair.Value.RefreshRole();
            return;
        }
        prepared = true;
        UpdateTalksRemaining();

        goalDifference = MatchSimPageUI.Instance.CurrentGoalDifferenceForMyTeam();
        severity = TeamTalkReactions.SeverityFromScore(goalDifference);
        expectation = TeamTalkChatter.ExpectationFromScore(
            goalDifference, MatchSimPageUI.Instance.ExpectedGoalDifferenceForMyTeam());

        BuildSquadGrid();

        responseManager.OnResponseChosen.RemoveListener(Deliver);
        responseManager.OnResponseChosen.AddListener(Deliver);
        responseManager.SpawnButtons();

        individualResponseManager.OnResponseChosen.RemoveListener(DeliverIndividual);
        individualResponseManager.OnResponseChosen.AddListener(DeliverIndividual);
        individualPanel.SetActive(false);

        situationText.text = goalDifference == 0 ? "Level" : goalDifference > 0 ? $"{goalDifference} ahead" : $"{-goalDifference} behind";
        responseManager.gameObject.SetActive(true);
        flavourText.text = "";
        overallText.text = "";

        // Pre-talk read: a few speak, most stay quiet.
        foreach (var kvp in boxes)
            kvp.Value.ShowBubble(TeamTalkChatter.Line(ContextFor(kvp.Key, ChatterPhase.PreTalk)));

        RefreshState();
    }

    public void Close()
    {
        individualPanel.SetActive(false);
        individualTarget = null;
        gameObject.SetActive(false);
        MatchSimPageUI.Instance.SetMatchContentVisible(true);
    }

    private void BuildSquadGrid()
    {
        Game.ClearContainer(boxContainer);
        boxes.Clear();

        foreach (Player p in TeamManager.Instance.MyTeam.Players)
        {
            if (p.IsDeceased) continue;
            PlayerMoraleBoxUI box = Instantiate(boxPrefab, boxContainer);
            box.Setup(p);
            box.Preview = line => flavourText.text = line;
            boxes[p] = box;
        }
    }

    private void Deliver(Event.Response response)
    {
        if (TeamTalkController.Instance.Used) return;

        flavourText.text = "";

        List<PlayerReaction> reactions = TeamTalkController.Instance.DeliverTalk(response, severity);
        foreach (var r in reactions)
            if (boxes.TryGetValue(r.player, out var box))
            {
                box.ShowReaction(r);
                box.ShowBubble(TeamTalkChatter.Line(ContextFor(r, ChatterPhase.PostTalk, response)),
                               TeamTalkReactions.ReactionColour(r.reaction));
            }

        overallText.text = "";
        RefreshState(); // used is now true → resumes the individual phase
    }

    // ————————————————————— individual talks —————————————————————

    private void EnterIndividualPhase()
    {
        bool available = TeamTalkController.Instance.CanTalkIndividually;
        foreach (var box in boxes.Values) box.SetSelectable(available, OnBoxClicked);
        UpdateTalksRemaining();
    }

    private void OnBoxClicked(Player p)
    {
        if (!TeamTalkController.Instance.CanTalkIndividually) return;

        individualTarget = p;
        individualTargetText.text = $"A word with {p.Surname}";
        individualPanel.SetActive(true);
        individualResponseManager.SpawnButtons();
    }

    public void CancelIndividual()
    {
        individualPanel.SetActive(false);
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
        int remaining = TeamTalkController.Instance.MaxIndividualTalks - TeamTalkController.Instance.IndividualTalksUsed;
        talksRemainingText.text = TeamTalkController.Instance.Used
            ? $"Private words left: {remaining}"
            : "";
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
            responseManager.gameObject.SetActive(false);
            EnterIndividualPhase();
        }

        titleText.text = used ? "Private talks" : "Team talk";
    }
}
