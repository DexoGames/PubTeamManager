using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

public enum TrainingType
{
    Technical,
    Mental,
    Physical,
    Tactical,
    Social,
    Positional
}

/// <summary>
/// The player's chosen, ongoing training instruction. Lightweight and serializable:
/// it stores a <see cref="DrillId"/> (resolved against <see cref="DrillCatalog"/>) plus,
/// for positional training, a target position and the IDs of the selected players.
/// Player references are stored as IDs and resolved via PersonManager so the session can
/// be embedded directly in the save (GameState.CurrentTraining).
///
/// Executed by TrainingManager on scheduled training days; it persists until changed,
/// so the same session repeats every training day.
/// </summary>
[System.Serializable]
public class TrainingSession
{
    public DrillId Drill;
    public Player.Position? TargetPosition;            // positional only
    public List<int> SelectedPlayerIds = new List<int>(); // positional only

    // ————————————————————— tunables —————————————————————
    public const int MAX_BOOST = Player.MAX_BOOST; // cap echoed for clarity
    public const int BUILD_PER_SESSION = 1;        // Boost gained on trained stats
    public const int DECAY_PER_SESSION = 1;        // Boost lost on untrained stats
    public const int MAX_POSITIONAL_PLAYERS = 5;

    private const float BASE_FAMILIARITY_GAIN = 8f;

    // ————————————————————— resolved views —————————————————————
    [JsonIgnore] public Drill Definition => DrillCatalog.Get(this.Drill);
    [JsonIgnore] public TrainingType Type => Definition != null ? Definition.Type : TrainingType.Technical;
    [JsonIgnore] public string Name => Definition != null ? Definition.Name : Drill.ToString();
    [JsonIgnore] public string Description => Definition != null ? Definition.Description : "";
    [JsonIgnore] public PlayerStat[] AffectedStats => Definition != null ? Definition.AffectedStats : new PlayerStat[0];

    [JsonIgnore]
    public List<Player> SelectedPlayers =>
        SelectedPlayerIds == null || PersonManager.Instance == null
            ? new List<Player>()
            : SelectedPlayerIds.Select(id => PersonManager.Instance.GetPlayer(id))
                               .Where(p => p != null)
                               .ToList();

    public TrainingSession() { }

    public TrainingSession(DrillId drill)
    {
        Drill = drill;
    }

    public static TrainingSession Positional(Player.Position position, IEnumerable<int> playerIds)
    {
        return new TrainingSession(DrillId.Positional)
        {
            TargetPosition = position,
            SelectedPlayerIds = (playerIds ?? Enumerable.Empty<int>())
                                .Take(MAX_POSITIONAL_PLAYERS).ToList()
        };
    }

    /// <summary>
    /// Executes the training session on the whole squad. Boost drills build their
    /// affected stats and decay the rest; special drills run their own effect and
    /// decay all Boost (the opportunity cost of not running a Boost drill).
    /// </summary>
    public void Execute(Team team)
    {
        if (team == null) return;

        switch (Type)
        {
            case TrainingType.Technical:
            case TrainingType.Mental:
            case TrainingType.Physical:
                ExecuteBoost(team);
                break;
            case TrainingType.Tactical:
                ExecuteTacticFamiliarity(team);
                DecayAll(team);
                break;
            case TrainingType.Social:
                ExecuteSocial(team);
                DecayAll(team);
                break;
            case TrainingType.Positional:
                ExecutePositional();
                DecayAll(team);
                break;
        }

        // Any training session counts as time spent drilling the current tactic → builds familiarity.
        team.Tactic?.AdvanceFamiliarity();
    }

    private void ExecuteBoost(Team team)
    {
        PlayerStat[] stats = AffectedStats;
        foreach (var player in team.Players)
            player.ApplyBoostSession(stats, BUILD_PER_SESSION, DECAY_PER_SESSION);
    }

    private void DecayAll(Team team)
    {
        foreach (var player in team.Players)
            player.ApplyBoostSession(null, 0, DECAY_PER_SESSION);
    }

    private void ExecuteTacticFamiliarity(Team team)
    {
        // Per-player tactical sharpness. Team-wide tactic Familiarity is advanced for ALL training types
        // in Execute() (a session is a session), so it isn't bumped again here.
        foreach (var player in team.Players)
        {
            float intelligenceMultiplier = player.GetStats().Intelligence / 50f;
            float gain = BASE_FAMILIARITY_GAIN * intelligenceMultiplier;
            player.TacticFamiliarity = Mathf.Clamp(player.TacticFamiliarity + gain, 0f, 100f);
        }
    }

    private void ExecuteSocial(Team team)
    {
        foreach (var player in team.Players)
        {
            float currentMorale = player.Morale.Mood;
            float targetMorale = 65f;
            float shift = (targetMorale - currentMorale) * 0.15f + Random.Range(-2f, 5f);
            player.Morale.Mood = Mathf.Clamp(player.Morale.Mood + (int)shift, 0, 100);
            player.Morale.Passion = Mathf.Clamp(player.Morale.Passion + Random.Range(0, 4), 0, 100);
        }
    }

    private void ExecutePositional()
    {
        if (!TargetPosition.HasValue) return;

        foreach (var player in SelectedPlayers.Take(MAX_POSITIONAL_PLAYERS))
            player.TickPositionalRoll(TargetPosition.Value);
    }

    /// <summary>Human-readable summary of what the session does, for the UI preview.</summary>
    public string GetEffectivenessDescription()
    {
        if (Type == TrainingType.Positional)
        {
            int count = SelectedPlayerIds?.Count ?? 0;
            string pos = TargetPosition.HasValue ? Player.LongPosition(TargetPosition.Value) : "(no position)";
            return $"Training {count}/{MAX_POSITIONAL_PLAYERS} player{(count == 1 ? "" : "s")} as {pos}.";
        }

        if (Definition != null && Definition.IsBoostDrill && AffectedStats.Length > 0)
        {
            string stats = string.Join(", ", AffectedStats);
            return $"+{BUILD_PER_SESSION} Boost per session to {stats} (max +{MAX_BOOST}). Other Boost fades.";
        }

        return Description;
    }
}
