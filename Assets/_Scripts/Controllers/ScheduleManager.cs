using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manages the weekly/fortnightly schedule for the player's team.
/// Determines what happens each day: match, training, interview, or rest.
/// </summary>
public class ScheduleManager : MonoBehaviour
{
    public static ScheduleManager Instance { get; private set; }

    private Dictionary<DateTime, ScheduleEntry> schedule = new Dictionary<DateTime, ScheduleEntry>();
    private const int SCHEDULE_AHEAD_DAYS = 120;
    private const int INTERVIEW_INTERVAL = 14;

    // Fixed anchor for the interview cadence. Set once so that regenerating the rolling window each
    // day keeps interviews on stable dates (anchoring to "today" would make them recede every advance).
    private DateTime interviewEpoch;
    private bool epochSet;

    public event Action OnTrainingDay;
    public event Action OnInterviewDay;
    public event Action<Fixture> OnMatchDay;

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

    /// <summary>
    /// Generates the schedule from a start date. Priority order: matches (fixed) → fortnightly interviews
    /// (fixed cadence, their own day) → pub socials (day after each match) → weekly training → rest. Matches and
    /// interviews sit on fixed dates and never move; pub/training shift forward to the next free day on a clash.
    /// </summary>
    public void GenerateSchedule(DateTime startDate)
    {
        schedule.Clear();

        if (!epochSet)
        {
            interviewEpoch = startDate.Date;
            epochSet = true;
        }

        Team myTeam = TeamManager.Instance.MyTeam;
        var fixtures = FixturesManager.Instance.GetUpcomingFixturesForTeam(myTeam, SCHEDULE_AHEAD_DAYS);

        DateTime start = startDate.Date;
        DateTime end = start.AddDays(SCHEDULE_AHEAD_DAYS);

        // 1. Matches — fixed dates, highest priority.
        var matchDays = new List<DateTime>();
        foreach (var fixture in fixtures)
        {
            DateTime d = fixture.Date.Date;
            schedule[d] = new ScheduleEntry(fixture.Date, ScheduleEntryType.Match,
                $"{fixture.HomeTeam.Name} vs {fixture.AwayTeam.Name}", fixture);
            matchDays.Add(d);
        }

        // 2. Interviews — fortnightly on a FIXED cadence (anchored to the epoch, not 'today'). Placed right after
        //    matches so nothing lower-priority can ever bump them. Each occurrence sits on its cadence date, only
        //    shifting forward past a fixed MATCH (so it always gets its own day). Because the shift depends only on
        //    fixed matches it's identical on every daily regeneration → the date never wanders; and the past-date
        //    skip is keyed on the PLACED day, not the raw cadence day, so a shifted interview never vanishes before
        //    you reach it. Start one cadence before the window so an occurrence shifted into 'today' isn't missed.
        DateTime target = interviewEpoch.AddDays(INTERVIEW_INTERVAL);
        while (target.AddDays(INTERVIEW_INTERVAL) <= start) target = target.AddDays(INTERVIEW_INTERVAL);
        for (; target < end; target = target.AddDays(INTERVIEW_INTERVAL))
        {
            DateTime placed = target.Date;
            while (placed < end && schedule.ContainsKey(placed)) placed = placed.AddDays(1); // only matches occupy days here
            if (placed >= start && placed < end)
                schedule[placed] = new ScheduleEntry(placed, ScheduleEntryType.Interview, "Interview Day");
        }

        // 3. Pub socials — the day after each match day (shifted forward on a clash, incl. past an interview day).
        foreach (var matchDay in matchDays.OrderBy(d => d))
            PlaceActivity(matchDay.AddDays(1), end, ScheduleEntryType.PubTrip, "Pub Trip");

        // 4. Training — once per week, preferring Wednesday, shifted on a clash.
        for (DateTime weekStart = start; weekStart < end; weekStart = weekStart.AddDays(7))
            PlaceActivity(PreferredWeekday(weekStart, DayOfWeek.Wednesday), end, ScheduleEntryType.Training, "Training Day");

        // 5. Everything else is a rest day.
        for (DateTime d = start; d < end; d = d.AddDays(1))
            if (!schedule.ContainsKey(d))
                schedule[d] = new ScheduleEntry(d, ScheduleEntryType.RestDay, "Rest Day");
    }

    /// <summary>
    /// Places an activity on the first free (unscheduled) day at/after <paramref name="preferred"/>,
    /// within the window — so a clash with a higher-priority day shifts it forward instead of dropping it.
    /// </summary>
    private void PlaceActivity(DateTime preferred, DateTime end, ScheduleEntryType type, string description)
    {
        for (DateTime d = preferred.Date; d < end; d = d.AddDays(1))
        {
            if (!schedule.ContainsKey(d))
            {
                schedule[d] = new ScheduleEntry(d, type, description);
                return;
            }
        }
    }

    /// <summary>The first occurrence of <paramref name="day"/> within the week starting at weekStart.</summary>
    private DateTime PreferredWeekday(DateTime weekStart, DayOfWeek day)
    {
        for (int i = 0; i < 7; i++)
        {
            DateTime d = weekStart.AddDays(i);
            if (d.DayOfWeek == day) return d;
        }
        return weekStart;
    }

    /// <summary>
    /// Gets today's schedule entry. Returns RestDay if nothing scheduled.
    /// </summary>
    public ScheduleEntry GetTodaysEntry()
    {
        DateTime today = CalenderManager.Instance.CurrentDay.Date;
        if (schedule.TryGetValue(today, out ScheduleEntry entry))
            return entry;

        return new ScheduleEntry(today, ScheduleEntryType.RestDay, "Rest Day");
    }

    /// <summary>
    /// Returns the date of the next scheduled training day (today onwards), or null if
    /// none is scheduled within the lookahead window.
    /// </summary>
    public DateTime? GetNextTrainingDay()
    {
        DateTime today = CalenderManager.Instance.CurrentDay.Date;
        for (int i = 0; i < SCHEDULE_AHEAD_DAYS; i++)
        {
            DateTime date = today.AddDays(i);
            if (schedule.TryGetValue(date, out ScheduleEntry entry) && entry.Type == ScheduleEntryType.Training)
                return date;
        }
        return null;
    }

    /// <summary>
    /// Gets upcoming schedule entries for the next N days.
    /// </summary>
    public List<ScheduleEntry> GetUpcoming(int days)
    {
        DateTime today = CalenderManager.Instance.CurrentDay.Date;
        var entries = new List<ScheduleEntry>();

        for (int i = 0; i < days; i++)
        {
            DateTime date = today.AddDays(i);
            if (schedule.TryGetValue(date, out ScheduleEntry entry))
                entries.Add(entry);
            else
                entries.Add(new ScheduleEntry(date, ScheduleEntryType.RestDay, "Rest Day"));
        }

        return entries;
    }

    /// <summary>
    /// Called during day advance to trigger the appropriate event for today's activity.
    /// </summary>
    public void ProcessToday()
    {
        var entry = GetTodaysEntry();

        switch (entry.Type)
        {
            case ScheduleEntryType.Match:
                OnMatchDay?.Invoke(entry.MatchFixture);
                break;
            case ScheduleEntryType.Training:
                OnTrainingDay?.Invoke();
                break;
            case ScheduleEntryType.Interview:
                OnInterviewDay?.Invoke();
                break;
        }
    }
}
