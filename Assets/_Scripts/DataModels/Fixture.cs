using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Unity.Mathematics;

[System.Serializable]
public class Fixture
{
    public Team HomeTeam;
    public Team AwayTeam;
    public Match.Result Result;
    public DateTime Date;
    public List<Player> Goalscorers;

    public bool BeenPlayed;

    [System.NonSerialized] public MatchWeek matchWeek;

    public Fixture(Team home, Team away, DateTime date, MatchWeek matchWeek)
    {
        HomeTeam = home;
        AwayTeam = away;
        Result = new Match.Result();
        Date = date;
        this.matchWeek = matchWeek;
    }

    internal void SimulateFixture()
    {
        //Debug.Log($"SIMULATING {HomeTeam.TeamName} VS {AwayTeam.TeamName}");
        Match match = new Match(HomeTeam, AwayTeam);
        Result = match.SimulateMatch();

        //Debug.Log($"{Score.home}-{Score.away}");

        LeagueManager.Instance.UpdateStandings(this);
        BeenPlayed = true;

        if (GetWinner() == TeamManager.Instance.MyTeam) EventsManager.Instance.AddWinEvent(GetWinner(), GetLoser());
        if (GetLoser() == TeamManager.Instance.MyTeam) EventsManager.Instance.AddLoseEvent(GetWinner(), GetLoser());
    }

    public Team GetWinner()
    {
        if (!BeenPlayed || Result.score.home == Result.score.away) return null;

        if (Result.score.home > Result.score.away) return HomeTeam;
        return AwayTeam;
    }
    public Team GetLoser()
    {
        if (!BeenPlayed || Result.score.home == Result.score.away) return null;

        if (Result.score.home > Result.score.away) return AwayTeam;
        return HomeTeam;
    }

    //public static List<Player> DecideGoalscorers(Game.Score score, Team home, Team away)
    //{
    //    foreach(Player p in home.Players.Take(11))

    //    for(int i = 0; i < score.home; i++)
    //    {
            
    //    }
    //}

    public static Game.Score CalculateScore(Team home, Team away)
    {
        Debug.Log($"{home.TeamName} vs {away.TeamName}:");

        float mentalDefecit = 10 + home.AvgMental - away.AvgMental;
        mentalDefecit = SignedSquareRoot(mentalDefecit) / 1.5f;

        float physicalDefecit = 2 + home.AvgPhysical - away.AvgPhysical;
        physicalDefecit = SignedSquareRoot(physicalDefecit) / 1.5f;

        float tacticalDefecit = home.Manager.TacticsMatch(home.Tactic.Formation, away.Tactic.Formation) - away.Manager.TacticsMatch(home.Tactic.Formation, away.Tactic.Formation);
        tacticalDefecit = SignedSquareRoot(tacticalDefecit);

        float homeThreat = 15 + home.Threat - home.Security;
        homeThreat = SignedSquareRoot(homeThreat, 0.65f);

        float awayThreat = 15 + away.Threat - home.Security;
        awayThreat = SignedSquareRoot(awayThreat, 0.65f);

        Debug.Log($"home threat = {homeThreat}");

        float threatDivider = 1.1f;
        float defDivider = 1.5f;
        float avgDefecit = Game.WeightedAverage((mentalDefecit, 1), (physicalDefecit, 1), (tacticalDefecit, 0.5f));
        Game.Score score = new Game.Score(Mathf.RoundToInt((homeThreat / threatDivider) + avgDefecit / defDivider), Mathf.RoundToInt((awayThreat / threatDivider) - avgDefecit / defDivider));
        score.home = Mathf.Max(score.home, 0);
        score.away = Mathf.Max(score.away, 0);

        score.home = Mathf.Max( UnityEngine.Random.Range(0, 2), Mathf.Max(score.home + UnityEngine.Random.Range(-1, 2), 0));
        score.away = Mathf.Max(UnityEngine.Random.Range(0, 2), Mathf.Max(score.away + UnityEngine.Random.Range(-1, 2), 0));

        return score;
    }
    public static float SignedSquareRoot(float input)
    {
        return Mathf.Pow(Mathf.Abs(input), 0.5f) * Mathf.Sign(input);
    }
    public static float SignedSquareRoot(float input, float power)
    {
        return Mathf.Pow(Mathf.Abs(input), power) * Mathf.Sign(input);
    }

    //Match sim logic:
    //    internal IEnumerator AdvancedSimulateFixture()
    //    {
    //        // Initial setup for the match
    //        Tactic currentPossessor = HomeTeam.Tactic;
    //        Tactic opponent;

    //        Game.Score currentScore = new Game.Score();

    //        for (int minute = 1; minute <= 90; minute++)
    //        {
    //            opponent = (currentPossessor == HomeTeam.Tactic) ? AwayTeam.Tactic : HomeTeam.Tactic;

    //            yield return new WaitForSeconds(2.5f);

    //            // Simulate phases of play here
    //            if (PossessionPhase(currentPossessor, opponent, minute))
    //            {
    //                minute++;
    //                MatchSimPageUI.Instance.UpdateTimer(minute);
    //                yield return new WaitForSeconds(1.5f);

    //                if (AdvancementPhase(currentPossessor, minute))
    //                {
    //                    minute++;
    //                    MatchSimPageUI.Instance.UpdateTimer(minute);
    //                    yield return new WaitForSeconds(1.8f);

    //                    if (ChanceCreationPhase(currentPossessor, opponent, minute))
    //                    {
    //                        minute++;
    //                        MatchSimPageUI.Instance.UpdateTimer(minute);
    //                        yield return new WaitForSeconds(1.6f);

    //                        // Clear Chance Phase - Higher probability of scoring
    //                        if (UnityEngine.Random.Range(0.1f, 1) < 0.3f)
    //                        {
    //                            minute++;
    //                            MatchSimPageUI.Instance.UpdateTimer(minute);
    //                            yield return new WaitForSeconds(2.5f);

    //                            if (ClearChancePhase(currentPossessor, opponent, minute))
    //                            {
    //                                MatchSimPageUI.Instance.PrintEvent(minute, $"{currentPossessor.Team.TeamName} scores on a clear chance!");

    //                                currentScore.home += (HomeTeam == currentPossessor.Team) ? 1 : 0;
    //                                currentScore.away += (HomeTeam == currentPossessor.Team) ? 0 : 1;
    //                            }
    //                            else
    //                            {
    //                                MatchSimPageUI.Instance.PrintEvent(minute, $"{currentPossessor.Team.TeamName} misses the clear chance.");
    //                            }
    //                        }
    //                        else if (ScoringAttemptPhase(currentPossessor, opponent, minute))
    //                        {
    //                            yield return new WaitForSeconds(2.2f);
    //                            MatchSimPageUI.Instance.PrintEvent(minute, $"{currentPossessor.Team.TeamName} scores!");

    //                            currentScore.home += (HomeTeam == currentPossessor.Team) ? 1 : 0;
    //                            currentScore.away += (HomeTeam == currentPossessor.Team) ? 0 : 1;
    //                        }
    //                    }
    //                }
    //            }

    //            // Switch possession if no clear chance or scoring attempt succeeds
    //            currentPossessor = opponent;

    //            Score = currentScore;

    //            MatchSimPageUI.Instance.UpdateMatchUI();
    //        }

    //        BeenPlayed = true;
    //    }

    //    bool PossessionPhase(Tactic team, Tactic opponent, int minute)
    //    {
    //        float possessionChance = (7 + team.Control + team.Team.AvgPhysical) - (opponent.Control + opponent.Team.AvgPhysical);
    //        float randomRoll = UnityEngine.Random.Range(0f, 10f); // Random factor
    //        if (randomRoll <= possessionChance)
    //        {
    //            MatchSimPageUI.Instance.PrintEvent(minute, $"{team.Team.TeamName} with some nice build up play.");
    //            return true;
    //        }
    //        else
    //        {
    //            MatchSimPageUI.Instance.PrintEvent(minute, $"{team.Team.TeamName} loses possession.");
    //            return false;
    //        }
    //    }


    //    bool AdvancementPhase(Tactic team, int minute)
    //    {
    //        float advancementChance = team.Threat / 2 + team.Control;
    //        float randomRoll = UnityEngine.Random.Range(0f, 10f); // Random factor
    //        if (randomRoll <= advancementChance)
    //        {
    //            MatchSimPageUI.Instance.PrintEvent(minute, $"{team.Team.TeamName} are advancing into the opposition's half.");
    //            return true;
    //        }
    //        else
    //        {
    //            MatchSimPageUI.Instance.PrintEvent(minute, $"{team.Team.TeamName} with a poor pass. They've lost possession!");
    //            return false;
    //        }
    //    }


    //    bool ChanceCreationPhase(Tactic team, Tactic opponent, int minute)
    //    {
    //        float chanceCreationChance = (team.Threat / 2 + team.Control + 3) - opponent.Security;
    //        float randomRoll = UnityEngine.Random.Range(0f, 10f); // Random factor
    //        if (randomRoll <= chanceCreationChance)
    //        {
    //            MatchSimPageUI.Instance.PrintEvent(minute, $"This is a goal scoring chance for {team.Team.TeamName}");
    //            return true;
    //        }
    //        else
    //        {
    //            MatchSimPageUI.Instance.PrintEvent(minute, $"{team.Team.TeamName} lose the ball!");
    //            return false;
    //        }
    //    }


    //    bool ScoringAttemptPhase(Tactic team, Tactic opponent, int minute)
    //    {
    //        float scoringChance = team.Threat * 0.8f - opponent.Security * 0.2f;
    //        float randomRoll = UnityEngine.Random.Range(0f, 10f); // Random factor
    //        if (randomRoll <= scoringChance)
    //        {
    //            MatchSimPageUI.Instance.PrintEvent(minute, $"GOAL FOR {team.Team.TeamName}!");
    //            return true;
    //        }
    //        else
    //        {
    //            MatchSimPageUI.Instance.PrintEvent(minute, $"The shot by {team.Team.TeamName} is saved!");
    //            return false;
    //        }
    //    }


    //    bool ClearChancePhase(Tactic team, Tactic opponent, int minute)
    //    {
    //        float clearChanceProbability = 0.75f; // High probability for clear chances
    //        bool clearChanceResult = UnityEngine.Random.Range(0.1f, 1) < clearChanceProbability;

    //        if (clearChanceResult)
    //        {
    //            MatchSimPageUI.Instance.PrintEvent(minute, $"GOAL FOR {team.Team.TeamName}!!");
    //        }
    //        else
    //        {
    //            MatchSimPageUI.Instance.PrintEvent(minute, $"What a chance!! {team.Team.TeamName} waste a chance 1 on 1 with the keeper!");
    //        }

    //        return clearChanceResult;
    //    }
}
