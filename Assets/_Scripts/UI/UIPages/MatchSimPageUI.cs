using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MatchSimPageUI : UIPage
{
    public const float FAST_FORWARD_TIME = 0.02f;
    /// <summary>Alpha for the mentality label while a touchline shout is queued but not yet live (see
    /// <see cref="Tactic.PendingMentalityShift"/>) — a subtle "not in effect yet" cue.</summary>
    const float PENDING_MENTALITY_ALPHA = 0.5f;

    [SerializeField] FixtureUI _fixtureUI;
    [SerializeField] TextMeshProUGUI _timerText;
    [SerializeField] Button _advanceButton, _pauseButton;

    [Header("In-match tactics")]
    [SerializeField] TextMeshProUGUI _mentalityText;
    [Tooltip("Shown only at half-time: holds the Team Talk + Edit Tactics buttons.")]
    [SerializeField] GameObject _halfTimePanel;
    [Tooltip("The team-talk overlay. It manages its own visibility, so the match screen closes it whenever " +
             "half-time ends or we navigate away — otherwise it would linger over the next page.")]
    [SerializeField] TeamTalkUI _teamTalkPanel;

    [SerializeField] Transform _eventsContainer;
    [SerializeField] MatchEventUI _matchEventPrefab;

    Slider mentalitySlider;

    Queue<Highlight> highlights = new Queue<Highlight>();
    /// <summary>Match-screen objects switched off while the team-talk overlay is up (see <see cref="SetMatchContentVisible"/>).</summary>
    readonly List<GameObject> _hiddenForTeamTalk = new List<GameObject>();
    bool isProcessing = false;
    bool _isPaused = false;
    bool _isFastForwarding = false;

    Fixture _fixture;
    Match match;
    int _currentMinute;

    public static MatchSimPageUI Instance { get; private set; }

    enum State
    {
        PreKickoff,
        FirstHalf,
        HalfTime,
        SecondHalf,
        FullTime
    }

    State _state = State.PreKickoff;
    Coroutine _currentSimulation;

    private void Awake()
    {
        mentalitySlider = _mentalityText.GetComponentInParent<Slider>(true);

        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    protected override void OnShow(Fixture fixture)
    {
        UIManager.Instance.ShowNavigationButtons(false);
        base.OnShow(fixture);
        Game.ClearContainer(_eventsContainer);
        _fixture = fixture;
        match = null;
        highlights.Clear();
        _isPaused = false;

        SetState(State.PreKickoff);
        SetPauseButton();
        UpdateMentalityUI();
        _teamTalkPanel.Close();

        _fixtureUI.SetFixtureText(_fixture, true);
        UpdateTimer(0);
    }

    public void UpdateMatchUI()
    {
        _fixtureUI.SetFixtureText(_fixture, true);
        UpdateTimer(match.currentMin.Base);
    }

    public void UpdateTimer(int minute)
    {
        _timerText.text = _state == State.FullTime ? "Full time" : _state == State.HalfTime ? "Half-time" : $"{minute}'";
    }

    void SimMatch()
    {
        // Don't field injured/suspended players.
        _fixture.HomeTeam.EnsureAvailableLineup();
        _fixture.AwayTeam.EnsureAvailableLineup();

        match = new Match(_fixture.HomeTeam, _fixture.AwayTeam, true);
        match.BroadcastHighlight.AddListener(AddHighlight);

        // Put the player's tactic into live-match mode (resets any in-match mentality nudge).
        TeamManager.Instance.MyTeam.Tactic?.BeginMatch();
        UpdateMentalityUI();

        // Allow one half-time team talk this match.
        TeamTalkController.Instance?.ResetForMatch();
        _teamTalkPanel.ResetForMatch();
    }

    // ————————————————————— In-match tactical changes —————————————————————

    public void MoreAttacking() => ShiftMentality(+1);
    public void MoreDefensive() => ShiftMentality(-1);

    void ShiftMentality(int delta)
    {
        Team myTeam = TeamManager.Instance.MyTeam;
        Tactic t = myTeam.Tactic;

        if (_state == State.PreKickoff || _state == State.FullTime) return;
        t.ShiftMentalityInMatch(delta);
        if (_isPaused) t.ApplyPendingMentality();
        UpdateMentalityUI();
    }

    /// <summary>Promotes any queued touchline shout to live and, only if it actually changed something,
    /// announces it as a match event tagged with <paramref name="minute"/> — the minute that just finished
    /// playing (not <c>match.currentMin</c>, which has already advanced past it by this point).</summary>
    void ApplyPendingMentalityAndAnnounce(int minute)
    {
        Team myTeam = TeamManager.Instance.MyTeam;
        Tactic t = myTeam.Tactic;

        if (t.ApplyPendingMentality() && !_isPaused && (_state == State.FirstHalf || _state == State.SecondHalf))
            PrintEvent(myTeam, $"Touchline shout — switching to a {MentalityLabel(t.EffectiveMentality)} approach.", minute);

        UpdateMentalityUI();
    }

    /// <summary>Refreshes the mentality label, showing the queued shout (fading it translucent while it's
    /// still waiting on the current minute to end — see <see cref="Tactic.PendingMentalityShift"/>).</summary>
    void UpdateMentalityUI()
    {
        Tactic t = TeamManager.Instance.MyTeam.Tactic;
        mentalitySlider.SetValueWithoutNotify(t.PendingMentalityIndex);
        mentalitySlider.interactable = _state != State.PreKickoff && _state != State.FullTime;
        _mentalityText.text = MentalityLabel(t.PendingMentality);

        Color c = _mentalityText.color;
        c.a = t.PendingMentality == t.EffectiveMentality ? 1f : PENDING_MENTALITY_ALPHA;
        _mentalityText.color = c;
    }

    public static string MentalityLabel(Mentality m)
    {
        switch (m)
        {
            case Mentality.UltraDefensive: return "Ultra-Defensive";
            case Mentality.Defensive: return "Defensive";
            case Mentality.Cautious: return "Cautious";
            case Mentality.Balanced: return "Balanced";
            case Mentality.Positive: return "Positive";
            case Mentality.Attacking: return "Attacking";
            case Mentality.UltraAttacking: return "All-Out Attack";
            default: return m.ToString();
        }
    }

    /// <summary>Set the live mentality to a specific level (0–6).</summary>
    public void SetMentalityInMatch(int targetIndex)
    {
        ShiftMentality(targetIndex - TeamManager.Instance.MyTeam.Tactic.PendingMentalityIndex);
    }

    /// <summary>Slider hook: a UI Slider passes a float, so bind its OnValueChanged here (rounds to a level).</summary>
    public void SetMentalityInMatch(float targetIndex) => SetMentalityInMatch(Mathf.RoundToInt(targetIndex));

    /// <summary>Slider hook: reads the slider's own value (rounds to a level) — bind directly to OnValueChanged.</summary>
    public void SetMentalitySliderInMatch(Slider self) => SetMentalityInMatch(Mathf.RoundToInt(self.value));

    /// <summary>
    /// Current goal difference from the PLAYER's team perspective (+ = winning), e.g. for the half-time team-talk
    /// severity. 0 if there's no live match.
    /// </summary>
    public int CurrentGoalDifferenceForMyTeam()
    {
        if (match == null || _fixture == null) return 0;
        Team me = TeamManager.Instance.MyTeam;
        int home = match.result.score.home;
        int away = match.result.score.away;
        if (_fixture.AwayTeam == me) return away - home;
        return home - away;
    }

    /// <summary>
    /// Pre-match EXPECTED goal difference from the player's view, derived from the two starting XIs' relative
    /// strength (rating gap → goals). Feeds the half-time team-talk "over/under-performing" read: a 0-0 is great
    /// against a far stronger side and dismal against a far weaker one. Ability-based, so it isn't polluted by
    /// in-match morale/slumps. 0 if there's no live match.
    /// </summary>
    public float ExpectedGoalDifferenceForMyTeam()
    {
        if (match == null || _fixture == null) return 0f;
        Team me = TeamManager.Instance.MyTeam;
        Team opponent = _fixture.HomeTeam == me ? _fixture.AwayTeam : _fixture.HomeTeam;
        return (StartingStrength(me) - StartingStrength(opponent)) / TeamTalkChatter.RATING_POINTS_PER_GOAL;
    }

    /// <summary>Average rating of a team's starting XI, each player rated in the slot they're lined up in.</summary>
    static float StartingStrength(Team team)
    {
        if (team == null || team.Players.Count < 11 || team.Formation == null) return 0f;

        int slots = Mathf.Min(11, team.Formation.Positions.Length);
        float sum = 0f;
        for (int i = 0; i < slots; i++)
            sum += team.Players[i].GetAverage(team.Formation.Positions[i].ID);
        return slots > 0 ? sum / slots : 0f;
    }

    // ————————————————————— Half-time: full tactics access —————————————————————

    /// <summary>
    /// Half-time only: open the full tactics page to make structural changes (formation, instructions,
    /// dependencies). Use during open play is impossible — only shouts/mentality are available then.
    /// </summary>
    public void OpenHalfTimeTactics()
    {
        if (_state != State.HalfTime) return;
        _teamTalkPanel.Close();
        TacticsPageUI.Instance?.EnterReturnToMatchMode();
        UIManager.Instance.ShowTactics();
    }

    /// <summary>
    /// Hides (or restores) everything on the match screen except the team-talk overlay, so the talk gets the whole
    /// screen. Restoring re-activates exactly what was showing before — so e.g. the half-time panel comes back but
    /// a panel that was already hidden stays hidden, without this needing to know the state machine.
    /// </summary>
    public void SetMatchContentVisible(bool visible)
    {
        if (visible)
        {
            foreach (GameObject go in _hiddenForTeamTalk) go.SetActive(true);
            _hiddenForTeamTalk.Clear();
            return;
        }

        if (_hiddenForTeamTalk.Count > 0) return; // already hidden — don't stack

        foreach (Transform child in Elements)
        {
            if (child == _teamTalkPanel.transform || !child.gameObject.activeSelf) continue;
            child.gameObject.SetActive(false);
            _hiddenForTeamTalk.Add(child.gameObject);
        }
    }

    /// <summary>Re-shows the match screen after half-time tactics editing, WITHOUT restarting the match.</summary>
    public void ResumeDisplay()
    {
        UIManager.Instance.ShowNavigationButtons(false);
        TeamManager.Instance.MyTeam.Tactic.RefreshMatchCache();
        DisplayUI();
        UpdateMatchUI();
        UpdateMentalityUI();
        SetState(_state); // refresh the half-time panel + advance button for the preserved state
    }

    void AddHighlight(Highlight highlight)
    {
        highlights.Enqueue(highlight);

        if (!isProcessing)
        {
            isProcessing = true;
            StartCoroutine(ProcessHighlights());
        }
    }

    IEnumerator ProcessHighlights()
    {
        while (highlights.Count > 0)
        {
            if (_isPaused)
            {
                yield return new WaitUntil(() => !_isPaused);
            }

            Highlight highlight = highlights.Dequeue();
            PrintEvent(highlight.Team, highlight.Describe(), highlight.Minute.Base);
            yield return new WaitForSeconds(highlight.Duration);
        }

        isProcessing = false;
    }

    IEnumerator SimulateHalf(Half half)
    {
        match.currentMin = Match.DecideStartMinute(half);

        while (match.HalfConditions(half))
        {
            yield return new WaitUntil(() => !_isPaused);

            // MatchEngine.SimulateMinute() advances match.currentMin past this minute internally, so grab
            // the minute that's actually about to be played BEFORE simulating (for the mentality announcement).
            int minuteJustPlayed = match.currentMin.Base;
            match.SimulateMinute();
            _fixture.Result = match.result;
            UpdateTimer(match.currentMin.Base);

            // The minute has now fully played out — any queued touchline shout goes live from here.
            ApplyPendingMentalityAndAnnounce(minuteJustPlayed);

            yield return new WaitUntil(() => highlights.Count == 0);
            UpdateMatchUI();
            yield return new WaitForSeconds(1.2f);
        }

        EndHalf(half);
    }

    IEnumerator FastForwardToHalf(Half half)
    {
        _isFastForwarding = true;

        while (match.HalfConditions(half))
        {
            yield return new WaitUntil(() => !_isPaused);

            int minuteJustPlayed = match.currentMin.Base;
            match.SimulateMinute();
            _fixture.Result = match.result;

            // The minute has now fully played out — any queued touchline shout goes live from here.
            ApplyPendingMentalityAndAnnounce(minuteJustPlayed);

            while (highlights.Count > 0)
            {
                Highlight h = highlights.Dequeue();
                PrintEvent(h.Team, h.Describe(), h.Minute.Base);
                yield return new WaitForSeconds(FAST_FORWARD_TIME);
            }

            UpdateMatchUI();
            yield return null;
        }

        EndHalf(half);
        _isFastForwarding = false;
    }

    void EndHalf(Half half)
    {
        if (half == Half.First)
        {
            SetState(State.HalfTime);
            // The CPU opponent's manager sorts their dressing room out — clear their slumps. The player fixes
            // their own via the half-time team talk.
            match.HalfTimeRecovery();
            // Half-time: structural tactic changes (formation/instructions/dependencies) are now allowed.
            TeamManager.Instance.MyTeam.Tactic.IsHalfTime = true;
            // Flush any shout still queued from the closing minute — no more minutes ticking until kickoff.
            ApplyPendingMentalityAndAnnounce(match.currentMin.Base);
        }
        else
        {
            SetState(State.FullTime);

            // End live-match mode: clears the temporary mentality nudge + half-time window. Familiarity is
            // advanced for both teams inside FinaliseResult (the match counts as a "session").
            TeamManager.Instance.MyTeam.Tactic.EndMatch();

            _fixture.FinaliseResult();

            // Clear transient slump/blunder flags so they don't leak into post-match (UI) stat reads.
            match.ClearPlayerMatchMorale();

            // The player's match result changed a fixture — persist core + current season.
            SaveManager.Instance.AutoSave();
        }
    }

    void SetState(State newState)
    {
        _state = newState;

        SetAdvanceButton();
        UpdateHalfTimePanel();
        UpdateMentalityUI();
        if (match != null) UpdateTimer(match.currentMin.Base);
    }

    void UpdateHalfTimePanel()
    {
        _halfTimePanel.SetActive(_state == State.HalfTime);
        _pauseButton.interactable = _state == State.FirstHalf || _state == State.SecondHalf;
    }

    void SetAdvanceButton()
    {
        TextMeshProUGUI advance = _advanceButton.GetComponentInChildren<TextMeshProUGUI>();

        switch (_state)
        {
            case State.PreKickoff:
                advance.text = "Begin Game";
                break;
            case State.FirstHalf:
                advance.text = "Sim To Half Time";
                break;
            case State.HalfTime:
                advance.text = "Begin Second Half";
                break;
            case State.SecondHalf:
                advance.text = "Sim To Full Time";
                break;
            case State.FullTime:
                advance.text = "Return Home";
                break;
        }
    }

    void SetPauseButton()
    {
        TextMeshProUGUI pause = _pauseButton.GetComponentInChildren<TextMeshProUGUI>();
        pause.text = _isPaused ? "Resume" : "Pause";
    }

    public void Advance()
    {
        if (_isFastForwarding)
            return;

        SetPause(false);

        switch (_state)
        {
            case State.PreKickoff:
                SimMatch();
                _currentSimulation = StartCoroutine(SimulateHalf(Half.First));
                SetState(State.FirstHalf);
                break;

            case State.FirstHalf:
                if (_currentSimulation != null) StopCoroutine(_currentSimulation);
                // Stay in FirstHalf (don't flip to HalfTime yet) — the fast-forward coroutine itself calls
                // EndHalf() once it actually reaches half time, which is what should reveal the half-time
                // panel (tactics/team-talk buttons). Flipping state here made them appear immediately,
                // before the sim had actually caught up.
                _currentSimulation = StartCoroutine(FastForwardToHalf(Half.First));
                SetPause(false);
                break;

            case State.HalfTime:
                // Leaving half-time closes the structural-change window.
                TeamManager.Instance.MyTeam.Tactic.IsHalfTime = false;
                _teamTalkPanel.Close();
                _currentSimulation = StartCoroutine(SimulateHalf(Half.Second));
                SetState(State.SecondHalf);
                break;

            case State.SecondHalf:
                if (_currentSimulation != null) StopCoroutine(_currentSimulation);
                // As above: EndHalf() (called by the coroutine once full time is actually reached) is what
                // should flip the state, not this click.
                _currentSimulation = StartCoroutine(FastForwardToHalf(Half.Second));
                break;

            case State.FullTime:
                UIManager.Instance.ShowNavigationButtons(true);
                UIManager.Instance.ShowHomePage();
                break;
        }
    }

    public void Pause()
    {
        SetPause(!_isPaused);
    }
    void SetPause(bool pause)
    {
        _isPaused = pause;
        SetPauseButton();
    }

    public void PrintEvent(Team team, string text, int minute)
    {
        MatchEventUI matchEvent = Instantiate(_matchEventPrefab, _eventsContainer);
        matchEvent.SetText(team, text, minute);

        if (_isFastForwarding)
        {
            matchEvent.InstantShow();
        }
    }
}
