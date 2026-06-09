using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Stable identifier for each training drill. Persisted (a TrainingSession stores a
/// DrillId, not a Drill instance), so values must never be renumbered — only appended.
/// </summary>
public enum DrillId
{
    // Technical
    Shooting,
    Passing,
    Defending,
    Dribbling,
    Crossing,
    Heading,
    // Mental
    Positioning,
    TacticalAwareness,
    CreativePlay,
    TeamBonding,
    Composure,
    // Physical
    Fitness,
    Strength,
    // Special
    TacticDrills,   // tactic familiarity
    TeamSocial,     // morale
    Positional      // position strength
}

/// <summary>
/// Static definition of a training drill: its identity, category, the stats it Boosts
/// (empty for special drills), and a description for the UI. Content only — the player's
/// chosen drill + parameters live in <see cref="TrainingSession"/>.
/// </summary>
public class Drill
{
    public DrillId Id;
    public string Name;
    public TrainingType Type;
    public PlayerStat[] AffectedStats; // empty for Tactical / Social / Positional
    public string Description;

    public Drill(DrillId id, string name, TrainingType type, PlayerStat[] affectedStats, string description)
    {
        Id = id;
        Name = name;
        Type = type;
        AffectedStats = affectedStats ?? new PlayerStat[0];
        Description = description;
    }

    /// <summary>True if this drill builds the temporary stat Boost.</summary>
    public bool IsBoostDrill =>
        Type == TrainingType.Technical || Type == TrainingType.Mental || Type == TrainingType.Physical;
}

/// <summary>
/// The catalog of all training drills. Single source of truth — UI and execution both
/// read from here. Add new drills by appending to <see cref="DrillId"/> and this list.
/// </summary>
public static class DrillCatalog
{
    public static readonly List<Drill> All = new List<Drill>
    {
        // ————— Technical —————
        new Drill(DrillId.Shooting,  "Shooting Practice",   TrainingType.Technical,
            new[] { PlayerStat.Shooting },  "Sharpens finishing. Boosts Shooting."),
        new Drill(DrillId.Passing,   "Passing Drills",      TrainingType.Technical,
            new[] { PlayerStat.Passing },   "Tightens distribution. Boosts Passing."),
        new Drill(DrillId.Defending, "Defensive Training",  TrainingType.Technical,
            new[] { PlayerStat.Tackling },  "Drills tackling and challenges. Boosts Tackling."),
        new Drill(DrillId.Dribbling, "Dribbling Drills",    TrainingType.Technical,
            new[] { PlayerStat.Dribbling }, "Improves close control. Boosts Dribbling."),
        new Drill(DrillId.Crossing,  "Crossing Practice",   TrainingType.Technical,
            new[] { PlayerStat.Crossing },  "Works wide deliveries. Boosts Crossing."),
        new Drill(DrillId.Heading,   "Heading Drills",      TrainingType.Technical,
            new[] { PlayerStat.Heading },   "Aerial work at both ends. Boosts Heading."),

        // ————— Mental —————
        new Drill(DrillId.Positioning,       "Positioning Drills",  TrainingType.Mental,
            new[] { PlayerStat.Positioning },  "Shape and movement off the ball. Boosts Positioning."),
        new Drill(DrillId.TacticalAwareness, "Tactical Awareness",  TrainingType.Mental,
            new[] { PlayerStat.Intelligence }, "Reading the game. Boosts Intelligence."),
        new Drill(DrillId.CreativePlay,      "Creative Play",       TrainingType.Mental,
            new[] { PlayerStat.Creativity },   "Encourages flair and invention. Boosts Creativity."),
        new Drill(DrillId.TeamBonding,       "Team Bonding Drills", TrainingType.Mental,
            new[] { PlayerStat.Teamwork },     "Builds understanding. Boosts Teamwork."),
        new Drill(DrillId.Composure,         "Composure Training",  TrainingType.Mental,
            new[] { PlayerStat.Composure },    "Calm under pressure. Boosts Composure."),

        // ————— Physical —————
        new Drill(DrillId.Fitness,  "Fitness Training",        TrainingType.Physical,
            new[] { PlayerStat.Pace, PlayerStat.Stamina },   "Aerobic work. Boosts Pace and Stamina."),
        new Drill(DrillId.Strength, "Strength & Conditioning", TrainingType.Physical,
            new[] { PlayerStat.Strength, PlayerStat.Jumping }, "Gym and power work. Boosts Strength and Jumping."),

        // ————— Special —————
        new Drill(DrillId.TacticDrills, "Tactic Drills",         TrainingType.Tactical,
            new PlayerStat[0], "Rehearses the team's tactic. Raises tactic familiarity (no stat Boost)."),
        new Drill(DrillId.TeamSocial,   "Team Social Activity",  TrainingType.Social,
            new PlayerStat[0], "A day off the pitch. Lifts morale (no stat Boost)."),
        new Drill(DrillId.Positional,   "Positional Training",   TrainingType.Positional,
            new PlayerStat[0], "Retrains up to 5 players in a chosen position, permanently improving their strength there."),
    };

    public static Drill Get(DrillId id) => All.FirstOrDefault(d => d.Id == id);
}
