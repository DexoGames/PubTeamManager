using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MatchSimPageUI : UIPage
{
    [SerializeField] FixtureUI _fixtureUI;
    [SerializeField] TextMeshProUGUI _timerText;
    [SerializeField] Button _advanceButton, _pauseButton;

    [SerializeField] Transform _eventsContainer;
    [SerializeField] MatchEventUI _matchEventPrefab;

    Queue<Highlight> highlights = new Queue<Highlight>();
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

        SetState(State.PreKickoff);
        SetPauseButton();

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
        _timerText.text = _fixture.BeenPlayed ? "Full Time" : $"{minute}'";
    }

    void SimMatch()
    {
        match = new Match(_fixture.HomeTeam, _fixture.AwayTeam, true);
        match.BroadcastHighlight.AddListener(AddHighlight);
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

            match.SimulateMinute();
            _fixture.Result = match.result;
            UpdateTimer(match.currentMin.Base);

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

            match.SimulateMinute();
            _fixture.Result = match.result;

            while (highlights.Count > 0)
            {
                Highlight h = highlights.Dequeue();
                PrintEvent(h.Team, h.Describe(), match.currentMin.Base);
                yield return new WaitForSeconds(0.05f);
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
        }
        else
        {
            SetState(State.FullTime);

            _fixture.FinaliseResult();
        }
    }

    void SetState(State newState)
    {
        _state = newState;

        SetAdvanceButton();
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
                _currentSimulation = StartCoroutine(FastForwardToHalf(Half.First));
                SetState(State.HalfTime);
                SetPause(false);
                break;

            case State.HalfTime:
                _currentSimulation = StartCoroutine(SimulateHalf(Half.Second));
                SetState(State.SecondHalf);
                break;

            case State.SecondHalf:
                if (_currentSimulation != null) StopCoroutine(_currentSimulation);
                _currentSimulation = StartCoroutine(FastForwardToHalf(Half.Second));
                SetState(State.FullTime);
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
    }
}
