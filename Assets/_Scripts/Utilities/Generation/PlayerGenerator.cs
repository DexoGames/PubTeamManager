using System.Linq;
using UnityEngine;

/// <summary>
/// Pure generation math for new <see cref="Player"/>s: correlated skill rolls, position spillover and the
/// personality skew applied at creation. Extracted from <see cref="Player"/> so the model stays a thin
/// wrapper and this math can be tuned/tested headlessly.
/// </summary>
public static class PlayerGenerator
{
    private readonly struct StatRelation
    {
        public readonly PlayerStat A;
        public readonly PlayerStat B;
        public readonly float Weight;

        public StatRelation(PlayerStat a, PlayerStat b, float weight)
        {
            A = a;
            B = b;
            Weight = weight;
        }
    }

    // Relationship weights are also the maximum fraction each neighbour can pull a generated stat toward it.
    // The calculation is simultaneous, so the order of these links cannot cascade through a player profile.
    private static readonly StatRelation[] GenerationRelations =
    {
        new StatRelation(PlayerStat.Technique, PlayerStat.Dribbling, 0.34f),
        new StatRelation(PlayerStat.Technique, PlayerStat.Passing, 0.20f),
        new StatRelation(PlayerStat.Technique, PlayerStat.Shooting, 0.12f),
        new StatRelation(PlayerStat.Passing, PlayerStat.Crossing, 0.16f),
        new StatRelation(PlayerStat.Shooting, PlayerStat.Composure, 0.13f),
        new StatRelation(PlayerStat.Heading, PlayerStat.Jumping, 0.28f),
        new StatRelation(PlayerStat.Heading, PlayerStat.Strength, 0.13f),

        new StatRelation(PlayerStat.Positioning, PlayerStat.Marking, 0.34f),
        new StatRelation(PlayerStat.Marking, PlayerStat.Tackling, 0.26f),
        new StatRelation(PlayerStat.Positioning, PlayerStat.Intelligence, 0.23f),
        new StatRelation(PlayerStat.Creativity, PlayerStat.Technique, 0.22f),
        new StatRelation(PlayerStat.Creativity, PlayerStat.Passing, 0.14f),
        new StatRelation(PlayerStat.Intelligence, PlayerStat.Teamwork, 0.17f),
        new StatRelation(PlayerStat.Composure, PlayerStat.Technique, 0.11f),
        new StatRelation(PlayerStat.Aggression, PlayerStat.Strength, 0.09f),

        new StatRelation(PlayerStat.Pace, PlayerStat.Agility, 0.27f),
        new StatRelation(PlayerStat.Pace, PlayerStat.Stamina, 0.14f),
        new StatRelation(PlayerStat.Stamina, PlayerStat.Durability, 0.32f),
        new StatRelation(PlayerStat.Strength, PlayerStat.Jumping, 0.18f),
        new StatRelation(PlayerStat.Strength, PlayerStat.Durability, 0.13f)
    };

    /// <summary>
    /// Generates varied individuals around a light overall/domain aptitude, then softly pulls closely related
    /// stats together. The independent roll remains the largest input, so specialists and weaknesses still occur.
    /// This only runs at creation; the resulting stats are fully independent afterwards.
    /// </summary>
    public static int[] GenerateCorrelatedSkills()
    {
        float overall = (Random.Range(10f, 90f) + Random.Range(10f, 90f)) * 0.5f;
        float technical = Mathf.Lerp(overall, Random.Range(10f, 90f), 0.55f);
        float mental = Mathf.Lerp(overall, Random.Range(10f, 90f), 0.55f);
        float physical = Mathf.Lerp(overall, Random.Range(10f, 90f), 0.55f);

        var seed = new float[Player.SKILL_NO];
        foreach (PlayerStat stat in Player.SkillDisplayOrder)
        {
            float domain = Player.TechnicalStats.Contains(stat) ? technical
                : Player.MentalStats.Contains(stat) ? mental
                : physical;
            seed[(int)stat] = Mathf.Lerp(domain, Random.Range(10f, 90f), 0.65f);
        }

        var neighbourTotals = new float[Player.SKILL_NO];
        var relationTotals = new float[Player.SKILL_NO];
        foreach (StatRelation relation in GenerationRelations)
        {
            int a = (int)relation.A;
            int b = (int)relation.B;
            neighbourTotals[a] += seed[b] * relation.Weight;
            relationTotals[a] += relation.Weight;
            neighbourTotals[b] += seed[a] * relation.Weight;
            relationTotals[b] += relation.Weight;
        }

        var skills = new int[Player.SKILL_NO];
        for (int i = 0; i < Player.SKILL_NO; i++)
        {
            float value = seed[i];
            if (relationTotals[i] > 0f)
            {
                float neighbourAverage = neighbourTotals[i] / relationTotals[i];
                float pull = Mathf.Min(0.42f, relationTotals[i]);
                value = Mathf.Lerp(value, neighbourAverage, pull);
            }
            skills[i] = Mathf.Clamp(Mathf.RoundToInt(value), 10, 90);
        }

        return skills;
    }

    /// <summary>Adjacent positions that receive the +1 "spilling" bonus around a newly generated player's best position.</summary>
    public static Player.Position[] GetSimilarPositions(Player.Position position)
    {
        switch (position)
        {
            case Player.Position.GK:
                return new Player.Position[] { };

            case Player.Position.LB:
                return new[] { Player.Position.LM, Player.Position.CB };

            case Player.Position.CB:
                return new Player.Position[] { };

            case Player.Position.RB:
                return new[] { Player.Position.RM, Player.Position.CB };

            case Player.Position.DM:
                return new[] { Player.Position.CM, Player.Position.CB };

            case Player.Position.LM:
                return new[] { Player.Position.LB, Player.Position.LW };

            case Player.Position.CM:
                return new[] { Player.Position.DM, Player.Position.AM };

            case Player.Position.RM:
                return new[] { Player.Position.RB, Player.Position.RW };

            case Player.Position.LW:
                return new[] { Player.Position.LM };

            case Player.Position.AM:
                return new[] { Player.Position.CM, Player.Position.ST };

            case Player.Position.RW:
                return new[] { Player.Position.RM };

            case Player.Position.ST:
                return new[] { Player.Position.AM };

            default:
                return new Player.Position[] { };
        }
    }

    /// <summary>Skews a freshly generated stat block toward/away from a few stats based on personality.</summary>
    public static Player.Stats PersonalityModifier(Player.Stats player, Person.PersonalityType personality)
    {
        int bigChange = 40;
        int smallChange = 15;

        switch (personality)
        {
            case Person.PersonalityType.Aggressive:
                player.Aggression = Apply(player.Aggression, bigChange, true);
                player.Composure = Apply(player.Composure, smallChange, false);
                break;

            case Person.PersonalityType.Calm:
                player.Composure = Apply(player.Composure, bigChange, true);
                player.Aggression = Apply(player.Aggression, smallChange, false);
                break;

            case Person.PersonalityType.Cautious:
                player.Creativity = Apply(player.Creativity, bigChange, false);
                player.Intelligence = Apply(player.Intelligence, smallChange, true);
                break;

            case Person.PersonalityType.Cocky:
                player.Teamwork = Apply(player.Teamwork, bigChange, false);
                player.Creativity = Apply(player.Creativity, bigChange, true);
                break;

            case Person.PersonalityType.Driven:
                player.Teamwork = Apply(player.Teamwork, bigChange, true);
                player.Stamina = Apply(player.Stamina, bigChange, true);
                break;

            case Person.PersonalityType.Kind:
                player.Aggression = Apply(player.Aggression, bigChange, false);
                player.Composure = Apply(player.Composure, smallChange, true);
                break;

            case Person.PersonalityType.Lazy:
                player.Intelligence = Apply(player.Intelligence, bigChange, false);
                player.Positioning = Apply(player.Positioning, smallChange, false);
                break;

            case Person.PersonalityType.Shy:
                player.Teamwork = Apply(player.Teamwork, bigChange, false);
                player.Creativity = Apply(player.Creativity, smallChange, false);
                break;

            case Person.PersonalityType.Silly:
                player.Creativity = Apply(player.Creativity, bigChange, true);
                player.Intelligence = Apply(player.Intelligence, smallChange, false);
                break;

            case Person.PersonalityType.Smart:
                player.Intelligence = Apply(player.Intelligence, bigChange, true);
                player.Positioning = Apply(player.Positioning, bigChange, true);
                break;
        }

        return player;

        int Apply(int stat, int baseChange, bool increase)
        {
            float factor = increase ? (100 - stat) / 100f : stat / 100f;
            int scaledChange = (int)Mathf.Round(baseChange * factor);

            stat += increase ? scaledChange : -scaledChange;
            return Mathf.Clamp(stat, 0, 100);
        }
    }
}
