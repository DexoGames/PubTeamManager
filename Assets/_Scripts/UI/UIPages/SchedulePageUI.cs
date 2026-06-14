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

        if (ScheduleManager.Instance == null) return;

        var entries = ScheduleManager.Instance.GetUpcoming(daysPreview);
        DateTime today = CalenderManager.Instance.CurrentDay;

        if (headerText != null)
            headerText.text = $"Schedule — {CalenderManager.ShortDateWordsNoYear(today)}";

        foreach (var entry in entries)
        {
            if (entryPrefab == null) break;

            GameObject entryObj = Instantiate(entryPrefab, entryContainer);

            // Try to populate text fields on the prefab
            var texts = entryObj.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length >= 2)
            {
                texts[0].text = CalenderManager.ShortDateWordsNoYear(entry.Date);
                texts[1].text = GetEntryDescription(entry);
            }
            else if (texts.Length >= 1)
            {
                texts[0].text = $"{CalenderManager.ShortDateWordsNoYear(entry.Date)} — {GetEntryDescription(entry)}";
            }

            // Highlight today
            if (entry.Date.Date == today.Date)
            {
                var image = entryObj.GetComponent<Image>();
                if (image != null)
                    image.color = new Color(0.9f, 0.9f, 0.6f, 0.3f);
            }
        }
    }

    private string GetEntryDescription(ScheduleEntry entry)
    {
        switch (entry.Type)
        {
            case ScheduleEntryType.Match:
                return $"Match Day — {entry.Description}";
            case ScheduleEntryType.Training:
                return "Training Session";
            case ScheduleEntryType.Interview:
                return "Interview Day — Recruitment";
            case ScheduleEntryType.PubTrip:
                return "Pub Trip — Team Social";
            case ScheduleEntryType.RestDay:
                return "Rest Day";
            default:
                return entry.Description ?? "—";
        }
    }
}
