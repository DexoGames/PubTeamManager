using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MatchSimPageUI : UIPage
{
    [SerializeField] FixtureUI _fixtureUI;
    [SerializeField] TextMeshProUGUI _timerText;

    [SerializeField] Transform _eventsContainer;
    [SerializeField] MatchEventUI _matchEventPrefab;

    Queue<Highlight> highlights = new Queue<Highlight>();
    bool isProcessing = false;

    Fixture _fixture;
    Match match;
    int _currentMinute;

    public static MatchSimPageUI Instance { get; private set; }
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
        base.OnShow(fixture);
        Game.ClearContainer(_eventsContainer);
        _fixture = fixture;

        SimMatch();
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

        StartCoroutine(SimulateHalf(Half.First));
        StartCoroutine(SimulateHalf(Half.Second));
    }

    void AddHighlight(Highlight highlight)
    {
        highlights.Enqueue(highlight);

        if (!isProcessing)
        {
            StartCoroutine(ProcessHighlights());
        }
    }

    IEnumerator ProcessHighlights()
    {
        isProcessing = true;

        while (highlights.Count > 0)
        {
            Highlight highlight = highlights.Dequeue();
            PrintEvent(highlight.Describe());
            yield return new WaitForSeconds(0.8f);
        }

        isProcessing = false;
    }

    IEnumerator SimulateHalf(Half half)
    {
        while (match.HalfConditions(half))
        {
            match.SimulateMinute();
            _fixture.Result = match.result;

            yield return new WaitUntil(() => highlights.Count == 0);
            UpdateMatchUI();
            yield return new WaitForSeconds(1.6f);
        }
    }

    public void PrintEvent(string text)
    {
        Instantiate(_matchEventPrefab, _eventsContainer).SetText(text);
    }
}