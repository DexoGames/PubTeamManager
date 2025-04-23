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

    Fixture _fixture;
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

        UpdateTimer(_currentMinute);
    }

    public void UpdateTimer(int minute)
    {
        _timerText.text = _fixture.BeenPlayed ? "Full Time" : $"{minute}'";
    }

    void SimMatch()
    {
        //StartCoroutine(_fixture.AdvancedSimulateFixture());

        UpdateMatchUI();
    }

    IEnumerator SimulateHalf(int startingMinute)
    {
        for (int i = 0; i < 45; i++)
        {
            _currentMinute = startingMinute + i;

            PrintEvent(_currentMinute, $"Match is at minute {_currentMinute}");

            UpdateMatchUI();

            yield return new WaitForSeconds(0.2f);
        }

        Debug.Log("AYYYY");
        _fixture.SimulateFixture();

        Debug.Log(_fixture.Score.home);

        UpdateMatchUI();
    }

    public void PrintEvent(int minute, string text)
    {
        Instantiate(_matchEventPrefab, _eventsContainer).SetText(text);
    }
}