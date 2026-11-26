using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StatsPageUI : UIPage
{
    public static StatsPageUI Instance { get; private set; }

    [SerializeField] TextMeshProUGUI heading, details;
    [SerializeField] GameObject boards, listPanel, historyButton, newCareerButton, returnButton;
    [SerializeField] TMP_InputField pubSearch;
    [SerializeField] Transform rows;
    [SerializeField] Button rowPrefab;

    Action previous;
    bool HasClub => TeamManager.Instance.HumanTeam != null;

    void Awake()
    {
        Instance = this;
        pubSearch.onValueChanged.AddListener(ShowPubMatches);
    }

    protected override void OnShow()
    {
        ShowStats();
    }

    protected override void OnHide()
    {
        FixturesManager.Instance.ClearViewedSeason();
    }

    void SetView(string title, Action back, bool showList = true)
    {
        heading.text = title;
        previous = back;
        returnButton.SetActive(back != null);
        details.gameObject.SetActive(false);
        pubSearch.gameObject.SetActive(false);
        boards.SetActive(!showList);
        listPanel.SetActive(showList);
        historyButton.SetActive(!showList);
        newCareerButton.SetActive(!showList);
        Game.ClearContainer(rows);
    }

    public void Return()
    {
        previous?.Invoke();
    }

    void ShowStats()
    {
        FixturesManager.Instance.ClearViewedSeason();
        SetView("Season statistics", null, false);
    }

    public void ShowSeasons()
    {
        FixturesManager.Instance.ClearViewedSeason();
        SetView("Seasons", ShowStats);
        foreach (SeasonIndexEntry season in FixturesManager.Instance.Seasons.OrderByDescending(s => s.Year))
            AddRow($"{season.Year}–{(season.Year + 1) % 100:00}", () => ShowCompetitions(season.Year));
    }

    void ShowCompetitions(int year)
    {
        SetView($"{year}–{(year + 1) % 100:00}", ShowSeasons);
        var competitions = year == FixturesManager.Instance.CurrentSeasonYear
            ? FixturesManager.Instance.GetCurrentSeasonCompetitions() : SaveManager.Instance.LoadSeasonForViewing(year);
        foreach (Competition competition in competitions)
            AddRow(competition.Name, () => ShowRounds(competition));
        if (competitions.Count == 0) ShowDetail("Season file unavailable.");
    }

    void ShowRounds(Competition competition)
    {
        SetView(competition.Name, () => ShowCompetitions(competition.SeasonYear));
        if (competition is League league) AddRow("League table", () => ShowTable(league));
        for (int i = 0; i < competition.Rounds.Length; i++)
        {
            int round = i;
            string label = competition is Cup ? CompetitionContextWidget.CupRoundName(competition.Rounds[i].Count) : $"Round {i + 1}";
            AddRow(label, () => ShowFixtures(competition, round));
        }
    }

    void ShowTable(League league)
    {
        SetView(league.Name, () => ShowRounds(league));
        int place = 0;
        foreach (LeagueTableEntry entry in league.GetStandings())
            AddRow($"{++place}.  {entry.team.Name}     {entry.played} played     {entry.points} pts", null);
    }

    void ShowFixtures(Competition competition, int round)
    {
        SetView($"{competition.Name} · Round {round + 1}", () => ShowRounds(competition));
        foreach (Fixture fixture in competition.Rounds[round])
        {
            string score = fixture.BeenPlayed ? fixture.Result.score.ToString() : "v";
            AddRow($"{fixture.Date:d MMM}    {fixture.HomeTeam.Name}  {score}  {fixture.AwayTeam.Name}", () => ShowMatch(fixture));
        }
    }

    void ShowMatch(Fixture fixture)
    {
        SetView($"{fixture.HomeTeam.Name} v {fixture.AwayTeam.Name}", () => ShowFixtures(fixture.Competition, fixture.Round));
        if (!fixture.BeenPlayed)
        {
            ShowDetail($"Kickoff · {fixture.Date:ddd d MMM yyyy}");
            return;
        }

        ShowDetail($"{fixture.Result.score.home} – {fixture.Result.score.away}" +
            (fixture.DecidingTeamId.HasValue ? $" · {fixture.GetWinner().Name} won on penalties" : ""));
        foreach (Match.TeamStats side in new[] { fixture.Result.home, fixture.Result.away })
        {
            AddRow(side.team.Name, null);
            if (side.possession > 0)
                AddRow($"{side.possession:0} actions · {side.wideActions} wide · {side.turnovers} turnovers", null);
            if (side.goals != null)
                foreach (Match.Shot goal in side.goals) AddRow($"{goal.minute}   {goal.shooter?.FullName ?? "Unknown scorer"}", null);
            if (side.lineup != null)
            {
                string names = string.Join(", ", side.lineup.Select(id => PersonManager.Instance.GetPerson(id)?.Surname ?? "Unknown"));
                AddRow("Starting XI · " + names, null);
            }
        }
    }

    public void ChoosePub()
    {
        SetView("Find a pub by name or postcode", HasClub ? ShowStats : null);
        pubSearch.gameObject.SetActive(true);
        pubSearch.SetTextWithoutNotify("");
        ShowPubMatches("");
    }

    void ShowPubMatches(string search)
    {
        Game.ClearContainer(rows);
        string query = search.Trim();
        IEnumerable<Pub> pubs = TeamManager.Instance.GetAllPubs();
        if (query.Length > 0) pubs = pubs.Where(p => p.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
            || p.Postcode.StartsWith(query, StringComparison.OrdinalIgnoreCase));

        var matches = pubs.Take(24).ToList();
        foreach (Pub pub in matches) AddRow($"{pub.Name} · {pub.Postcode}", () => ConfirmClub(pub));
        details.gameObject.SetActive(matches.Count == 0);
        if (matches.Count == 0) details.text = "No matching pubs.";
    }

    void ConfirmClub(Pub pub)
    {
        SetView(pub.Name, ChoosePub);
        ShowDetail($"{pub.Address} · {pub.Postcode}");
        AddRow("Start career", () => GameManager.Instance.StartCareer(pub));
    }

    void AddRow(string label, Action select)
    {
        Button row = Instantiate(rowPrefab, rows);
        row.GetComponentInChildren<TextMeshProUGUI>().text = label;
        ((RectTransform)row.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 54f);
        row.interactable = select != null;
        if (select != null) row.onClick.AddListener(() => select());
    }

    void ShowDetail(string value)
    {
        details.text = value;
        details.gameObject.SetActive(true);
    }
}
