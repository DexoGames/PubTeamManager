using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the player's team training. Holds the ongoing training session (which persists
/// across saves and repeats every training day) and executes it when the schedule fires a
/// training day. Drill content comes from <see cref="DrillCatalog"/>.
/// </summary>
public class TrainingManager : MonoBehaviour
{
    public static TrainingManager Instance { get; private set; }

    /// <summary>Default drill used if the player never sets one.</summary>
    private const DrillId DEFAULT_DRILL = DrillId.Fitness;

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

    /// <summary>All available drills (content from the catalog).</summary>
    public IReadOnlyList<Drill> GetDrills() => DrillCatalog.All;

    /// <summary>
    /// Sets the ongoing training session. Persists until changed and repeats every
    /// training day — it is NOT executed here (execution happens on training days).
    /// </summary>
    public void SetTraining(TrainingSession session)
    {
        CurrentSession = session;
        if (session != null)
            Debug.Log($"[Training] Set ongoing training: {session.Name}");
    }

    /// <summary>Convenience: set a simple (non-positional) drill as the ongoing session.</summary>
    public void SetDrill(DrillId drill) => SetTraining(new TrainingSession(drill));

    /// <summary>
    /// Executes the current training session on the player's team.
    /// Called on training days via ScheduleManager.OnTrainingDay.
    /// </summary>
    public void ExecuteTraining()
    {
        if (CurrentSession == null)
        {
            Debug.Log($"[Training] No session set — defaulting to {DEFAULT_DRILL}.");
            CurrentSession = new TrainingSession(DEFAULT_DRILL);
        }

        Team myTeam = TeamManager.Instance.MyTeam;
        CurrentSession.Execute(myTeam);
        Debug.Log($"[Training] Executed '{CurrentSession.Name}' ({CurrentSession.Type}).");
    }
}
