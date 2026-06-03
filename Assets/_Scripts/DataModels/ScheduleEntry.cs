using System;

public enum ScheduleEntryType
{
    Match,
    Training,
    Interview,
    RestDay
}

/// <summary>
/// Represents a single day's activity in the schedule.
/// </summary>
[System.Serializable]
public class ScheduleEntry
{
    public DateTime Date;
    public ScheduleEntryType Type;
    public string Description;

    /// <summary>
    /// Reference to the associated fixture (only for Match days). 
    /// Null for other entry types.
    /// </summary>
    [System.NonSerialized]
    public Fixture MatchFixture;

    public ScheduleEntry() { }

    public ScheduleEntry(DateTime date, ScheduleEntryType type, string description, Fixture fixture = null)
    {
        Date = date;
        Type = type;
        Description = description;
        MatchFixture = fixture;
    }
}
