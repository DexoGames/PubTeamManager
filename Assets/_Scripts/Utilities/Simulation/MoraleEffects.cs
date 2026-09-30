using UnityEngine;

/// <summary>
/// Pure logic for how a player's <see cref="Morale"/> (Mood + Passion vs their personality ideal) affects their
/// in-match stats. Two layers:
///
///  • CONSTANT (every frame their stats are read) — only when they're BELOW their ideal:
///      – Passion short of ideal → a flat "flat/under-motivated" drop-off (the stronger one).
///      – Mood short of ideal → a weaker "unhappy" drop-off. If the player is in a SLUMP, mood is treated as 0
///        (worst case), so the unhappy drop hits at full force until they snap out of it.
///    Being ABOVE the ideal carries NO constant penalty — instead it feeds the per-minute blunder roll below.
///
///  • PER-MINUTE (rolled once a minute for every player on the pitch — <see cref="StepMatchMinute"/>):
///      – SLUMP entry: an unhappy player (mood below ideal) can drop into a slump that lasts the rest of the match
///        (a team talk can snap them out — see TeamTalkController). Riskier the further below ideal; half as likely
///        in the second half.
///      – BLUNDER: a one-minute collapse from complacency / over-eagerness. Always a small base chance, rising the
///        further ABOVE ideal the player is (too happy / too pumped). When it fires, the player's stats crater for
///        that single minute (see <see cref="ApplyBlunder"/>).
///
/// In keeping with the project's trade-off rule, the manager's only levers here are selection and the half-time
/// team talk — so morale is a genuine risk to manage, not a free dial.
/// </summary>
public static class MoraleEffects
{
    // ————————————————————— constant drop-off tuning —————————————————————
    // Drop per stat = ceil(shortfall / divisor). Ceil so even a small shortfall costs ≥1, and so the reference
    // points land exactly on the passion curve: 10→4, 30→10, 60→20.
    /// <summary>Passion-shortfall divisor: stat loss = ceil(shortfall / this). Smaller = harsher (the stronger drop-off).</summary>
    public const float PASSION_DIVISOR = 3f;
    /// <summary>Mood-shortfall divisor: larger than passion's, so the unhappy drop-off is weaker.</summary>
    public const float MOOD_DIVISOR = 5f;

    // ————————————————————— slump tuning —————————————————————
    // A slump roll happens once per SimulateMinute, ~35×/half, so per-minute chances compound over a half:
    //   P(slump by half-time) = 1 − (1 − chance/min)^35.
    // Calibrated to the reference point "mood 20 below ideal ⇒ ≈15% chance to slump in the first half":
    //   0.00023/pt × 20 = 0.0046/min → 1 − 0.9954^35 ≈ 0.149.  (Second half is halved.)
    /// <summary>Per-minute slump chance added per point of mood below ideal.</summary>
    public const float SLUMP_RISK_PER_POINT = 0.00023f;
    /// <summary>Cap on the per-minute slump chance, however unhappy the player is.</summary>
    public const float SLUMP_MAX_PER_MIN = 0.02f;
    /// <summary>Second-half slumps are this fraction as likely (they've settled / less time left to spiral).</summary>
    public const float SLUMP_SECOND_HALF_FACTOR = 0.5f;

    // ————————————————————— blunder tuning —————————————————————
    /// <summary>Baseline per-minute blunder chance for any player (even at/below ideal) — a sprinkle of chaos.</summary>
    public const float BLUNDER_BASE = 0.01f;
    /// <summary>Per-minute blunder chance when way above ideal (complacent / over-eager).</summary>
    public const float BLUNDER_MAX = 0.20f;
    /// <summary>Combined over-ideal points (mood-excess + passion-excess) at which the chance reaches the max.</summary>
    public const int BLUNDER_EXCESS_FOR_MAX = 60;
    /// <summary>How hard stats crater during a blundered minute (0.4 = a 60% collapse).</summary>
    public const float BLUNDER_STAT_FACTOR = 0.4f;

    // ————————————————————— affected stats —————————————————————
    // Two coherent groups (Height and Durability are never touched — they aren't "form"):
    //   • PASSION drives the PHYSICAL / intensity engine — running, physicality, aerial and defensive effort.
    //   • MOOD drives the TECHNICAL / mental / confidence game — on-the-ball execution and decision-making.
    // A player low on both therefore loses ground almost everywhere, but via the two different divisors.

    /// <summary>Stats that fall when PASSION is below ideal (physical / intensity / effort).</summary>
    public static readonly PlayerStat[] PassionStats =
    {
        PlayerStat.Pace, PlayerStat.Stamina, PlayerStat.Strength, PlayerStat.Jumping,
        PlayerStat.Agility, PlayerStat.Tackling, PlayerStat.Heading, PlayerStat.Aggression
    };

    /// <summary>Stats that fall when MOOD is below ideal (technical / mental / confidence).</summary>
    public static readonly PlayerStat[] MoodStats =
    {
        PlayerStat.Passing, PlayerStat.Shooting, PlayerStat.Technique, PlayerStat.Dribbling, PlayerStat.Crossing,
        PlayerStat.Positioning, PlayerStat.Creativity, PlayerStat.Intelligence, PlayerStat.Marking,
        PlayerStat.Composure, PlayerStat.Teamwork
    };

    // ————————————————————— constant effect —————————————————————

    /// <summary>
    /// Applies the constant morale drop-off to a copy of the stats and returns it. <paramref name="inSlump"/>
    /// forces the mood component to its worst (mood = 0). At or above ideal in a dimension → no effect from it.
    /// </summary>
    public static Player.Stats ApplyConstant(Player.Stats stats, Morale morale, bool inSlump)
    {
        // PASSION below ideal — flat / under-motivated: the stronger drop-off across the physical stats.
        int passionShort = morale.IdealPassion - morale.Passion;
        if (passionShort > 0)
        {
            int f = Mathf.CeilToInt(passionShort / PASSION_DIVISOR);
            foreach (var s in PassionStats) stats.SetStat(s, stats.GetStat(s) - f);
        }

        // MOOD below ideal — unhappy: a weaker drop-off across the technical/mental stats. A slump pins mood at 0.
        int effectiveMood = inSlump ? 0 : morale.Mood;
        int moodShort = morale.IdealMood - effectiveMood;
        if (moodShort > 0)
        {
            int f = Mathf.CeilToInt(moodShort / MOOD_DIVISOR);
            foreach (var s in MoodStats) stats.SetStat(s, stats.GetStat(s) - f);
        }

        return stats;
    }

    /// <summary>Craters every skill for a single blundered minute (a momentary loss of the plot).</summary>
    public static Player.Stats ApplyBlunder(Player.Stats stats)
    {
        for (int i = 0; i < Player.SKILL_NO; i++)
            stats.Skills[i] = Mathf.Clamp((int)(stats.Skills[i] * BLUNDER_STAT_FACTOR), 0, 100);
        return stats;
    }

    // ————————————————————— per-minute rolls —————————————————————

    /// <summary>Chance THIS minute that a fully-happy player still blunders, rising the further above ideal.</summary>
    public static float BlunderChance(Morale morale)
    {
        int excess = Mathf.Max(0, morale.Mood - morale.IdealMood)
                   + Mathf.Max(0, morale.Passion - morale.IdealPassion);
        float t = Mathf.Clamp01(excess / (float)BLUNDER_EXCESS_FOR_MAX);
        return Mathf.Lerp(BLUNDER_BASE, BLUNDER_MAX, t);
    }

    /// <summary>Per-minute chance an unhappy player drops into a slump (0 if at/above their mood ideal).</summary>
    public static float SlumpChancePerMinute(Morale morale)
    {
        int shortfall = morale.IdealMood - morale.Mood;
        if (shortfall <= 0) return 0f;
        return Mathf.Min(SLUMP_MAX_PER_MIN, shortfall * SLUMP_RISK_PER_POINT);
    }

    /// <summary>
    /// One minute's morale rolls for a single on-pitch player: re-rolls their blunder flag for this minute and,
    /// if they aren't already slumping, rolls whether they drop into one (rarer in the second half).
    /// </summary>
    public static void StepMatchMinute(Player player, bool secondHalf)
    {
        if (player == null) return;

        player.MoraleBlunder = Random.value < BlunderChance(player.Morale);

        if (!player.InSlump)
        {
            float chance = SlumpChancePerMinute(player.Morale);
            if (secondHalf) chance *= SLUMP_SECOND_HALF_FACTOR;
            if (chance > 0f && Random.value < chance) player.InSlump = true;
        }
    }

    // ————————————————————— team-talk recovery —————————————————————

    /// <summary>How likely a half-time team talk is to lift a slumping player out of it, by how well they took it.
    /// A flat or negative reaction won't help; a great one very likely will.</summary>
    public static float SlumpRecoveryChance(Event.Reaction reaction)
    {
        switch (reaction)
        {
            case Event.Reaction.Amazing: return 0.90f;
            case Event.Reaction.Great:   return 0.70f;
            case Event.Reaction.Good:    return 0.45f;
            case Event.Reaction.Neutral: return 0.15f;
            default:                     return 0f; // Poor / Bad / Terrible — won't snap them out
        }
    }
}
