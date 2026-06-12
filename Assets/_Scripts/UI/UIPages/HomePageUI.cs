using System;
using UnityEngine;

/// <summary>
/// Home dashboard coordinator. Its only direct job is the day-timeline strip: on show it
/// detects whether the day advanced by exactly one since it was last shown (the player must be
/// on the home page to advance, and AdvanceDay re-shows the home page) and animates the slide,
/// otherwise it snaps. Every other panel is a UIObject under this page, so it refreshes
/// automatically via UIPage.SetupUI() — no per-widget wiring needed here.
/// </summary>
public class HomePageUI : UIPage
{
    public static HomePageUI Instance { get; private set; }

    [SerializeField] private DayTimelineWidget dayTimeline;

    private DateTime lastShownDay;
    private bool hasShownBefore;

    public void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(this.gameObject);
        else
            Instance = this;
    }

    protected override void OnShow()
    {
        if (dayTimeline != null && CalenderManager.Instance != null)
        {
            DateTime today = CalenderManager.Instance.CurrentDay.Date;
            bool advancedOneDay = hasShownBefore && today == lastShownDay.AddDays(1).Date;

            if (advancedOneDay) dayTimeline.AdvanceAnimated();
            else dayTimeline.SnapToToday();

            lastShownDay = today;
            hasShownBefore = true;
        }

        // CompetitionContextWidget, CurrentRoundWidget, SquadStatusWidget and the events inbox
        // are UIObjects — UIPage.SetupUI() refreshes them right after this returns.
    }
}
