using System;

public enum ClubConcern
{
    None, PlayingTime, Workload, Tactics
}

public class SelectionHistory
{
    public int MissedStarts;
    public int DifficultStarts;
    public string LastSetup;
    public string DiscussedSetup;
    public DateTime NextDiscussion;
    public ClubConcern Promise;
    public int PromisedComplexity;

    public void Record(bool started, bool available, string setup, bool tooComplex)
    {
        MissedStarts = available && !started ? MissedStarts + 1 : 0;
        DifficultStarts = started && tooComplex ? (setup == LastSetup ? DifficultStarts + 1 : 1) : 0;
        if (LastSetup != setup) DiscussedSetup = null;
        LastSetup = setup;
    }

    public ClubConcern Concern(bool started, int fatigue)
    {
        if (DifficultStarts >= 2 && LastSetup != DiscussedSetup) return ClubConcern.Tactics;
        if (started && fatigue >= 65) return ClubConcern.Workload;
        return MissedStarts >= 3 ? ClubConcern.PlayingTime : ClubConcern.None;
    }

    public bool PromiseKept(bool started, int complexity)
    {
        switch (Promise)
        {
            case ClubConcern.PlayingTime: return started;
            case ClubConcern.Workload: return !started;
            case ClubConcern.Tactics: return complexity < PromisedComplexity;
            default: return false;
        }
    }
}
