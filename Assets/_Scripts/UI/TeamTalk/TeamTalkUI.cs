using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Half-time team-talk screen. Lays out every squad member as a little morale box along a shallow arch (a flat
/// "∩": across the top, dipping down the left/right sides), then lets the manager pick ONE response — the same
/// <see cref="Event.Response"/> options as a 1-on-1 player discussion (Praise / Rage / Encourage / …). The choice is
/// delivered to the WHOLE squad through the shared discussion reaction system, with the SEVERITY taken from the
/// half-time scoreline (winning → Pleasant, getting hammered → Dire). Each player reacts by personality, the boxes
/// flash how it landed, and an overall verdict sums it up. One talk per match.
///
/// Open it from the Half-Time Panel's "Team Talk" button (hook the button to <see cref="Open"/>).
/// </summary>
public class TeamTalkUI : MonoBehaviour
{
    [Header("Squad arch")]
    [SerializeField] private PlayerMoraleBoxUI boxPrefab;
    [Tooltip("Holds the player boxes. Anchor it to the TOP-CENTRE of the screen; boxes are placed relative to it.")]
    [SerializeField] private RectTransform boxContainer;
    [Tooltip("Total horizontal spread of the arch, in px (≈ usable screen width).")]
    [SerializeField] private float arcWidth = 1600f;
    [Tooltip("How far the ends dip below the centre, in px. Small = very shallow ∩.")]
    [SerializeField] private float arcDepth = 130f;
    [Tooltip("Gap from the container's top edge down to the centre (highest) box.")]
    [SerializeField] private float topMargin = 40f;

    [Header("Response buttons (built from this list)")]
    [SerializeField] private Button responseButtonPrefab;
    [SerializeField] private RectTransform responseButtonContainer;
    [SerializeField] private Event.Response[] responses =
    {
        Event.Response.Praise, Event.Response.Encourage, Event.Response.Inspire, Event.Response.Galvanise,
        Event.Response.Persuade, Event.Response.Challenge, Event.Response.Rage, Event.Response.Deflect
    };

    [Header("Feedback")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI situationText; // "Half-time — 1 up — Pleasant"
    [SerializeField] private TextMeshProUGUI flavourText;   // the line the manager "said"
    [SerializeField] private TextMeshProUGUI overallText;   // overall verdict

    private readonly Dictionary<Player, PlayerMoraleBoxUI> boxes = new();
    private readonly List<Button> responseButtons = new();
    private bool responseButtonsBuilt;

    private int goalDifference;
    private EventType.Severity severity;

    /// <summary>Show the panel, (re)build the squad arch, and read the half-time scoreline. Hook the Team Talk button here.</summary>
    public void Open()
    {
        gameObject.SetActive(true);

        goalDifference = MatchSimPageUI.Instance != null ? MatchSimPageUI.Instance.CurrentGoalDifferenceForMyTeam() : 0;
        severity = TeamTalkReactions.SeverityFromScore(goalDifference);

        BuildSquadArch();
        BuildResponseButtons();

        if (situationText != null) situationText.text = "Half-time — " + TeamTalkReactions.SituationText(goalDifference, severity);
        if (flavourText != null) flavourText.text = "";
        if (overallText != null) overallText.text = "";
        RefreshState();
    }

    /// <summary>Hide the panel (hook a Close/Done button here). Returns to the half-time panel underneath.</summary>
    public void Close() => gameObject.SetActive(false);

    private void BuildSquadArch()
    {
        if (boxContainer == null || boxPrefab == null) return;

        Game.ClearContainer(boxContainer);
        boxes.Clear();

        Team me = TeamManager.Instance != null ? TeamManager.Instance.MyTeam : null;
        if (me == null || me.Players == null) return;

        int n = me.Players.Count;
        for (int i = 0; i < n; i++)
        {
            Player p = me.Players[i];
            PlayerMoraleBoxUI box = Instantiate(boxPrefab, boxContainer);
            box.Setup(p);

            // Place along a shallow ∩: even spacing left→right, height peaks in the middle and dips at the ends.
            var rt = (RectTransform)box.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);

            float u = n > 1 ? (i / (float)(n - 1)) * 2f - 1f : 0f; // -1 (far left) … +1 (far right)
            float x = u * (arcWidth * 0.5f);
            float y = -topMargin - arcDepth * (u * u);             // centre highest, ends lowest
            rt.anchoredPosition = new Vector2(x, y);

            boxes[p] = box;
        }
    }

    private void BuildResponseButtons()
    {
        if (responseButtonsBuilt || responseButtonPrefab == null || responseButtonContainer == null) return;
        responseButtonsBuilt = true;

        foreach (var response in responses)
        {
            Event.Response captured = response; // avoid the closure-capture trap
            Button btn = Instantiate(responseButtonPrefab, responseButtonContainer);
            var label = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = TeamTalkReactions.Label(captured);
            btn.onClick.AddListener(() => Deliver(captured));
            responseButtons.Add(btn);
        }
    }

    private void Deliver(Event.Response response)
    {
        if (TeamTalkController.Instance == null || TeamTalkController.Instance.Used) return;

        if (flavourText != null) flavourText.text = TeamTalkReactions.Flavour(response);

        List<PlayerReaction> reactions = TeamTalkController.Instance.DeliverTalk(response, severity);
        foreach (var r in reactions)
            if (boxes.TryGetValue(r.player, out var box)) box.ShowReaction(r);

        if (overallText != null) overallText.text = TeamTalkReactions.Summarise(reactions);
        RefreshState();
    }

    private void RefreshState()
    {
        bool used = TeamTalkController.Instance != null && TeamTalkController.Instance.Used;

        foreach (var b in responseButtons)
            if (b != null) b.interactable = !used;

        if (titleText != null) titleText.text = used ? "Team Talk — delivered" : "Half-Time Team Talk";
        if (used && overallText != null && string.IsNullOrEmpty(overallText.text))
            overallText.text = "You've already had your say this match.";
    }
}
