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
    /// Generates the schedule from a start date, filling in match, training, interview, and rest days.
    /// </summary>
    public void GenerateSchedule(DateTime startDate)
    {
        schedule.Clear();

        Team myTeam = TeamManager.Instance.MyTeam;
        var fixtures = FixturesManager.Instance.GetUpcomingFixturesForTeam(myTeam, SCHEDULE_AHEAD_DAYS);

        // Mark match days
        foreach (var fixture in fixtures)
        {
            schedule[fixture.Date.Date] = new ScheduleEntry(fixture.Date, ScheduleEntryType.Match,
                $"{fixture.HomeTeam.Name} vs {fixture.AwayTeam.Name}", fixture);
        }

        // Fill in training and interview days
        int interviewCounter = 0;
        for (int day = 0; day < SCHEDULE_AHEAD_DAYS; day++)
        {
            DateTime date = startDate.AddDays(day).Date;

            if (schedule.ContainsKey(date)) continue; // Already has a match

            DayOfWeek dow = date.DayOfWeek;
            interviewCounter++;

            // Interview every 14 days (fortnightly) on a non-match day
            if (interviewCounter >= 14)
            {
                schedule[date] = new ScheduleEntry(date, ScheduleEntryType.Interview, "Interview Day");
                interviewCounter = 0;
            }
            // Training on Wednesday if no match
            else if (dow == DayOfWeek.Wednesday)
            {
                schedule[date] = new ScheduleEntry(date, ScheduleEntryType.Training, "Training Day");
            }
            else
            {
                schedule[date] = new ScheduleEntry(date, ScheduleEntryType.RestDay, "Rest Day");
            }
        }
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
