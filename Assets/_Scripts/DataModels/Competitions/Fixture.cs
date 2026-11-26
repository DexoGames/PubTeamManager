using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using System.Linq;
using Unity.Mathematics;

[System.Serializable]
public class Fixture : ISaveable
{
    /// <summary>Stable unique ID — lets any past fixture be addressed/queried for history.</summary>
    public int Id = -1;

    [JsonIgnore]
    public Competition Competition;
    public int Round;
    [JsonConverter(typeof(TeamRefConverter))]
    public Team HomeTeam;
    [JsonConverter(typeof(TeamRefConverter))]
    public Team AwayTeam;
    public Match.Result Result;
    public DateTime Date;

    public bool BeenPlayed;
    public int? DecidingTeamId;

    /// <summary>Parameterless constructor for deserialization.</summary>
    public Fixture() { }



    public Fixture(Team home, Team away, DateTime date, Competition competition, int round)
    {
        Id = IdManager.Instance.AllocateFixtureId();
        HomeTeam = home;
        AwayTeam = away;
        Result = new Match.Result(home, away);
        Date = date;
        Competition = competition;
        Round = round;
    }

    internal void SimulateFixture()
    {
        //Debug.Log($"SIMULATING {HomeTeam.TeamName} VS {AwayTeam.TeamName}");

        // Don't field injured/suspended players in the simulated XI.
        HomeTeam.EnsureAvailableLineup();
        AwayTeam.EnsureAvailableLineup();

        Match match = new Match(HomeTeam, AwayTeam);
        Result = match.SimulateMatch();

        //Debug.Log($"{Score.home}-{Score.away}");

        FinaliseResult();
    }

    public void FinaliseResult()
    {
        if (BeenPlayed) return;
        BeenPlayed = true;

        if (Competition is Cup || Competition is League playoff && playoff.IsPlayoff(this))
        {
            if (Result.score.home == Result.score.away) DecidingTeamId = Cup.ResolveDrawnCupMatch(this).TeamId;
        }

        // Record club stats for both teams
        HomeTeam.Stats.RecordMatchResult(this, HomeTeam);
        AwayTeam.Stats.RecordMatchResult(this, AwayTeam);

        if (GetWinner() == TeamManager.Instance.MyTeam) EventsManager.Instance.AddWinEvent(GetWinner(), GetLoser());
        if (GetLoser() == TeamManager.Instance.MyTeam) EventsManager.Instance.AddLoseEvent(GetWinner(), GetLoser());

        if (Competition is Cup cup)
        {
            cup.TryGenerateNextRound();
        }
        else if (Competition is League league)
        {
            league.UpdateStandings(this);
        }

        Team club = TeamManager.Instance.MyTeam;
        if (InvolvesTeam(club.TeamId))
            EventsManager.Instance.RecordSelection(club, club == HomeTeam ? Result.home : Result.away, Date);

        // Turn this match's fouls into bookings/suspensions/injuries for the player's team.
        InjuryManager.Instance?.ProcessMatchConsequences(this);

        // Playing settles both sides further into their current setup (familiarity muscle memory).
        HomeTeam.Tactic?.AdvanceFamiliarity();
        AwayTeam.Tactic?.AdvanceFamiliarity();
    }

    /// <summary>True if the given team (by ID) played in this fixture.</summary>
    public bool InvolvesTeam(int teamId)
    {
        return (HomeTeam != null && HomeTeam.TeamId == teamId)
            || (AwayTeam != null && AwayTeam.TeamId == teamId);
    }

    /// <summary>
    /// Strips this fixture's result down for long-term archival: drops fouls and possession,
    /// keeps the score, goal scorers and both starting XIs. Used for completed-season matches
    /// that don't involve the player's team.
    /// </summary>
    public Fixture CopyForArchive()
    {
        var copy = (Fixture)MemberwiseClone();
        copy.SlimForArchive();
        return copy;
    }

    public void SlimForArchive()
    {
        var r = Result;
        r.home.fouls = new List<Match.Foul>();
        r.away.fouls = new List<Match.Foul>();
        r.home.shots = new List<Match.Shot>();   // keep goals; drop the full shot log on archived matches
        r.away.shots = new List<Match.Shot>();
        r.home.possession = 0f;
        r.away.possession = 0f;
        Result = r;
    }

    public Team GetWinner()
    {
        if (!BeenPlayed) return null;
        if (Result.score.home == Result.score.away) return DecidingTeamId.HasValue ? (DecidingTeamId == HomeTeam.TeamId ? HomeTeam : AwayTeam) : null;

        if (Result.score.home > Result.score.away) return HomeTeam;
        return AwayTeam;
    }
    public Team GetLoser()
    {
        if (!BeenPlayed) return null;
        if (Result.score.home == Result.score.away) return DecidingTeamId.HasValue ? (DecidingTeamId == HomeTeam.TeamId ? AwayTeam : HomeTeam) : null;

        if (Result.score.home > Result.score.away) return AwayTeam;
        return HomeTeam;
    }

    /// <summary>
    /// ISaveable — no extra work needed since Result is fully serialized.
    /// </summary>
    public void OnAfterDeserialize() { }
}
