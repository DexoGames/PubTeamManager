using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manages training sessions for the player's team.
/// Provides available training options and executes the selected session.
/// </summary>
public class TrainingManager : MonoBehaviour
{
    public static TrainingManager Instance { get; private set; }

    public TrainingSession CurrentSession { get; private set; }

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
    /// Sets the training session for this week.
    /// </summary>
    public void SetTraining(TrainingSession session)
    {
        CurrentSession = session;
    }

    /// <summary>
    /// Executes the current training session on the player's team.
    /// Called on training days by the schedule system.
    /// </summary>
    public void ExecuteTraining()
    {
        if (CurrentSession == null)
        {
            Debug.Log("[Training] No training session set — using default stat boost.");
            CurrentSession = GetAvailableTrainingOptions()[0];
        }

        Team myTeam = TeamManager.Instance.MyTeam;
        CurrentSession.Execute(myTeam);
        Debug.Log($"[Training] Executed {CurrentSession.Type} training: {CurrentSession.Description}");
    }

    /// <summary>
    /// Returns a list of available training session options.
    /// </summary>
    public List<TrainingSession> GetAvailableTrainingOptions()
    {
        var options = new List<TrainingSession>();

        // Stat boost options — one per stat category
        options.Add(new TrainingSession(TrainingType.Technical, "Shooting Practice") { TargetStat = PlayerStat.Shooting });
        options.Add(new TrainingSession(TrainingType.Technical, "Passing Drills") { TargetStat = PlayerStat.Passing });
        options.Add(new TrainingSession(TrainingType.Technical, "Defensive Training") { TargetStat = PlayerStat.Tackling });
        options.Add(new TrainingSession(TrainingType.Technical, "Dribbling Drills") { TargetStat = PlayerStat.Dribbling });
        options.Add(new TrainingSession(TrainingType.Technical, "Crossing Practice") { TargetStat = PlayerStat.Crossing });
        options.Add(new TrainingSession(TrainingType.Technical, "Heading Drills") { TargetStat = PlayerStat.Heading });
        options.Add(new TrainingSession(TrainingType.Mental, "Positioning Drills") { TargetStat = PlayerStat.Positioning });
        options.Add(new TrainingSession(TrainingType.Mental, "Tactical Awareness") { TargetStat = PlayerStat.Intelligence });
        options.Add(new TrainingSession(TrainingType.Mental, "Creative Play") { TargetStat = PlayerStat.Creativity });
        options.Add(new TrainingSession(TrainingType.Mental, "Team Bonding Drills") { TargetStat = PlayerStat.Teamwork });
        options.Add(new TrainingSession(TrainingType.Mental, "Composure Training") { TargetStat = PlayerStat.Composure });
        options.Add(new TrainingSession(TrainingType.Physical, "Fitness Training") { TargetStat = PlayerStat.Pace });
        options.Add(new TrainingSession(TrainingType.Physical, "Strength & Conditioning") { TargetStat = PlayerStat.Strength });

        // Tactic familiarity
        options.Add(new TrainingSession(TrainingType.Tactical, "Tactic Drills"));

        // Social / morale
        options.Add(new TrainingSession(TrainingType.Social, "Team Social Activity"));

        // Positional — this is set up via UI, so we add a template
        options.Add(new TrainingSession(TrainingType.Positional, "Positional Training"));

        return options;
    }
}
