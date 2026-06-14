using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// Runtime cup instance — plain C# class (not ScriptableObject).
/// Manages knockout tournament lifecycle: round generation, winner detection,
/// extra time for draws, and cup completion events.
/// </summary>
public class Cup : Competition, ISaveable
{
    [JsonIgnore] private List<Team> autoSecondRoundTeams = new List<Team>();
    [JsonIgnore] private List<List<Fixture>> roundList = new List<List<Fixture>>();

    /// <summary>Parameterless constructor for deserialization.</summary>
    public Cup() { }

    public Cup(string name, List<Team> teams, DateTime startDate) : base(name, 1, teams, startDate) {}

    private void GenerateRound(List<Team> entrants, DateTime roundDate)
    {
        if (entrants == null || entrants.Count <= 1)
        {
            // Only 1 or 0 teams remain — cup is complete
            if (entrants != null && entrants.Count == 1)
            {
                IsComplete = true;
                Team winner = entrants[0];
                Debug.Log($"[Cup] {Name} winner: {winner.Name}!");
                CompetitionEvents.FireCupWon(this, winner);
                CompetitionEvents.FireSeasonComplete(this);
            }
            return;
        }

        int nextPowerOfTwo = 1;
        while (nextPowerOfTwo < entrants.Count) nextPowerOfTwo <<= 1;
        int numByes = nextPowerOfTwo - entrants.Count;

        List<Team> roundTeams = entrants.Skip(numByes).ToList();
        autoSecondRoundTeams = entrants.Take(numByes).ToList();

        List<Fixture> roundFixtures = new List<Fixture>();
        // Cup ties are played midweek (Wednesday), between the weekend league rounds.
        DateTime date = NextDayOfWeek(roundDate, DayOfWeek.Wednesday);

        for (int i = 0; i < roundTeams.Count; i += 2)
        {
            Team home = roundTeams[i];
            Team away = roundTeams[i + 1];
            DateTime matchDate = FindNextAvailableDate(home, away, date);
            Fixture fixture = new Fixture(home, away, matchDate, this, roundList.Count);
            Fixtures.Add(fixture);
            roundFixtures.Add(fixture);
        }

        roundList.Add(roundFixtures);
        Rounds = roundList.ToArray();
    }

    public override void GenerateFixtures()
    {
        GenerateRound(new List<Team>(Teams), startDate);
        base.GenerateFixtures();
    }

    public bool CanGenerateNextRound()
    {
        if (IsComplete) return false;
        if (Rounds == null || Rounds.Length == 0) return false;
        var last = Rounds[Rounds.Length - 1];
        if (last == null || last.Count == 0) return false;
        foreach (var f in last) if (!f.BeenPlayed) return false;
        return true;
    }

    public void TryGenerateNextRound()
    {
        if (!CanGenerateNextRound()) return;

        List<Team> winners = new List<Team>();
        var lastRound = roundList.LastOrDefault();
        if (lastRound == null) return;

        foreach (var fixture in lastRound)
        {
            Team winner = fixture.GetWinner();
            if (winner != null)
            {
                winners.Add(winner);
            }
            else
            {
                // Draw in a cup match — resolve via extra time simulation
                winner = ResolveDrawnCupMatch(fixture);
                winners.Add(winner);
            }
        }

        List<Team> entrants = winners.Union(autoSecondRoundTeams).ToList();
        autoSecondRoundTeams.Clear();

        // Next round ~2 weeks later (GenerateRound snaps it to the next midweek Wednesday).
        DateTime roundDate = roundList.Last().Last().Date.AddDays(14);

        GenerateRound(entrants, roundDate);
    }

    /// <summary>
    /// Returns the cup winner (only valid after cup is complete).
    /// </summary>
    public Team GetWinner()
    {
        if (!IsComplete || roundList.Count == 0) return null;
        var finalRound = roundList.Last();
        if (finalRound.Count != 1) return null;
        return finalRound[0].GetWinner();
    }

    /// <summary>
    /// Resolves a drawn cup match by simulating a deciding contest.
    /// Uses a simple coin-flip weighted by team strength as a penalty shootout proxy.
    /// </summary>
    private Team ResolveDrawnCupMatch(Fixture fixture)
    {
        // Weighted coin flip based on team control stats as penalty proxy
        float homeStrength = fixture.HomeTeam.Control + fixture.HomeTeam.Threat;
        float awayStrength = fixture.AwayTeam.Control + fixture.AwayTeam.Threat;
        float total = homeStrength + awayStrength;

        float rand = UnityEngine.Random.Range(0f, total);
        Team winner = rand < homeStrength ? fixture.HomeTeam : fixture.AwayTeam;

        Debug.Log($"[Cup] {fixture.HomeTeam.Name} vs {fixture.AwayTeam.Name} drawn — {winner.Name} wins on penalties!");
        return winner;
    }

    /// <summary>
    /// ISaveable — rebuilds Rounds from fixtures after deserialization.
    /// </summary>
    public void OnAfterDeserialize()
    {
        BuildRoundsFromFixtures();
    }
}
