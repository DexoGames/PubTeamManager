using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Cup : Competition
{
    private List<Team> autoSecondRoundTeams = new List<Team>();
    private List<List<Fixture>> roundList = new List<List<Fixture>>();

    public Cup(string name, List<Team> teams, DateTime startDate) : base(name, 1, teams, startDate)
    {

    }

    private void GenerateRound(List<Team> entrants, DateTime roundDate)
    {
        if (entrants == null || entrants.Count <= 1)
            return;

        int nextPowerOfTwo = 1;
        while (nextPowerOfTwo < entrants.Count) nextPowerOfTwo <<= 1;
        int numByes = nextPowerOfTwo - entrants.Count;

        List<Team> roundTeams = entrants.Skip(numByes).ToList();
        autoSecondRoundTeams = entrants.Take(numByes).ToList();

        List<Fixture> roundFixtures = new List<Fixture>();
        DateTime date = roundDate;

        int batchSize;
        if (roundTeams.Count >= 16)
            batchSize = 4;
        else if (roundTeams.Count >= 8)
            batchSize = 2;
        else
            batchSize = 1;

        for (int i = 0; i < roundTeams.Count; i += 2)
        {
            Team home = roundTeams[i];
            Team away = roundTeams[i + 1];
            DateTime matchDate = FindNextAvailableDate(home, away, date);
            Fixture fixture = new Fixture(home, away, matchDate, this, roundList.Count);
            Fixtures.Add(fixture);
            roundFixtures.Add(fixture);

            if (((i / 2) + 1) % batchSize == 0)
            {
                date = date.AddDays(7);
            }
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
        if (Rounds == null || Rounds.Length == 0) return false;
        var last = Rounds[Rounds.Length - 1];
        if (last == null || last.Count == 0) return false;
        foreach (var f in last) if (!f.BeenPlayed) return false;
        return true;
    }

    public void TryGenerateNextRound()
    {
        if (!CanGenerateNextRound()) return;

        List<Team> winners = roundList.LastOrDefault()?.Select(f => f.GetWinner()).Where(t => t != null).ToList();

        if (winners == null) throw new ArgumentNullException(nameof(winners));

        List<Team> entrants = winners.Union(autoSecondRoundTeams).ToList();
        autoSecondRoundTeams.Clear();

        DateTime roundDate = startDate;
        roundDate = roundList.Last().Last().Date.AddDays(18);

        GenerateRound(entrants, roundDate);
    }
}
