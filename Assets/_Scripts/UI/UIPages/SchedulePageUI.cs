using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI page showing upcoming schedule entries (matches, training, interviews, rest days).
/// Displays a scrollable list of the next 14 days of activity.
/// </summary>
public class SchedulePageUI : UIPage
{
    public static SchedulePageUI Instance { get; private set; }


    [SerializeField] private int daysPreview;
    [SerializeField] private Transform entryContainer;
    [SerializeField] private GameObject entryPrefab;
    [SerializeField] private TextMeshProUGUI headerText;


    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(this.gameObject);
        else
            Instance = this;
    }

    protected override void OnShow()
    {
        base.OnShow();
        RefreshSchedule();
    }

    public void RefreshSchedule()
    {
        Game.ClearContainer(entryContainer);

        var entries = ScheduleManager.Instance.GetUpcoming(daysPreview);
        DateTime today = CalenderManager.Instance.CurrentDay;

        headerText.text = $"{today:d MMM} – {today.AddDays(daysPreview - 1):d MMM yyyy}";

        foreach (ScheduleEntry entry in entries)
        {
            var card = Instantiate(entryPrefab, entryContainer);
            var view = card.GetComponentInChildren<ScheduleDayUI>();
            string date = entry.Date.Date == today.Date ? $"Today · {entry.Date:d MMM}" : entry.Date.ToString("ddd d");
            view.Show(date, ActivityLabel(entry), HomePageUI.Instance.ActivityIcon(entry.Type));
        }
    }

    static string ActivityLabel(ScheduleEntry entry)
    {
        if (entry.MatchFixture != null)
        {
            Fixture match = entry.MatchFixture;
            bool home = match.HomeTeam == TeamManager.Instance.MyTeam;
            return home ? $"{match.AwayTeam.Name} (H)" : $"{match.HomeTeam.Name} (A)";
        }
        switch (entry.Type)
        {
            case ScheduleEntryType.Interview: return "Interviews";
            case ScheduleEntryType.Training: return "Training";
            case ScheduleEntryType.PubTrip: return "Pub trip";
            case ScheduleEntryType.RestDay: return "";
            default: return entry.Description;
        }
    }
}
