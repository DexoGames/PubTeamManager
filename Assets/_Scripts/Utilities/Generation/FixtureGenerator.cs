using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class FixtureGenerator
{
    //public static List<MatchWeek> GenerateFixtures(League league, List<Team> teams, DateTime startDate)
    //{
    //    int numberOfTeams = teams.Count;
    //    List<MatchWeek> matchWeeks = new List<MatchWeek>();
    //    int totalRounds = (numberOfTeams % 2 == 0) ? numberOfTeams - 1 : numberOfTeams;
    //    int matchesPerRound = numberOfTeams / 2;

    //    for (int round = 0; round < totalRounds * 2; round++) // Double round-robin
    //    {
    //        MatchWeek matchWeek = new MatchWeek(round + 1);
    //        for (int match = 0; match < matchesPerRound; match++)
    //        {
    //            int homeIndex, awayIndex;

    //            if (round < totalRounds) // First half of the season
    //            {
    //                homeIndex = (round + match) % numberOfTeams;
    //                awayIndex = (numberOfTeams - 1 - match + round) % numberOfTeams;
    //            }
    //            else // Second half of the season (reverse fixtures)
    //            {
    //                awayIndex = (round + match) % numberOfTeams;
    //                homeIndex = (numberOfTeams - 1 - match + round) % numberOfTeams;
    //            }

    //            // If the number of teams is odd, one team will have a bye each round
    //            if (homeIndex == awayIndex)
    //            {
    //                continue;
    //            }

    //            Team homeTeam = teams[homeIndex];
    //            Team awayTeam = teams[awayIndex];

    //            Fixture newFixture = matchWeek.AddFixture(new Fixture(homeTeam, awayTeam, startDate.AddDays((6-(int)startDate.Date.DayOfWeek) + round * 7 + UnityEngine.Random.Range(0, 2)), matchWeek));
    //            //Debug.Log($"{homeTeam.TeamName} vs {awayTeam.TeamName} on {newFixture.Date.Date.ToShortDateString()}");
    //        }

    //        // Add matchweek only if it contains any fixtures (to handle bye rounds)
    //        if (matchWeek.fixtures.Count > 0)
    //        {
    //            matchWeeks.Add(matchWeek);
    //        }
    //    }

    //    return matchWeeks;
    //}
}
