using UnityEngine;

/// <summary>
/// Hidden interpersonal chemistry between two players — most pairs are <see cref="Neutral"/>; a few
/// players just click together or rub each other up the wrong way. Stored as nothing at all: the level is
/// derived deterministically from the pair's PersonIDs (the same trick <see cref="KitColors"/> uses for
/// kits), so it's stable across save/load with zero serialization and symmetric by construction.
///
/// The int values double as the effect direction/scale (see <see cref="Chemistry.StatDelta"/>):
/// −2/−1/0/+1, so a step of ±5 per level gives Frosty −5, In-Sync +5 and Bad Blood −10
/// ("more if it's a more extreme chemistry"). The extra tier sits on the NEGATIVE side on purpose —
/// in keeping with the project's "every edge carries a matching risk" rule, the downside can bite harder
/// than the upside rewards.
/// </summary>
public enum ChemistryLevel
{
    BadBlood = -2, // "very awkward" — rare, the harshest penalty
    Frosty   = -1, // "awkward" — a mild penalty
    Neutral  =  0, // the common case — no effect
    InSync   =  1  // "complementary" — a boost
}

/// <summary>
/// Pure logic for the chemistry system: what level a pair of players has, whether two positions are close
/// enough to "link up", and how big a stat swing a link produces. No state — everything derives on read.
/// </summary>
public static class Chemistry
{
    // ————————————————————— distribution (per unordered player pair) —————————————————————
    // Most links are Neutral; a non-neutral link is the occasional exception, not the rule. Tunable.
    private const float BAD_BLOOD_CHANCE = 0.04f; // very awkward (rarest)
    private const float FROSTY_CHANCE    = 0.11f; // awkward
    private const float IN_SYNC_CHANCE   = 0.11f; // complementary
    // Neutral takes the remaining ~0.74 — "most chemistry links are neutral".

    // ————————————————————— effect —————————————————————
    /// <summary>Stat swing per level: ±5 per step, so Frosty −5, In-Sync +5, Bad Blood −10.</summary>
    private const int STAT_STEP = 5;

    /// <summary>How many of the <see cref="LinkStats"/> a single pairing actually moves — "some, not all".</summary>
    private const int AFFECTED_STAT_COUNT = 3;

    /// <summary>
    /// The "combination" stats chemistry moves — the ones about playing WITH a team-mate. A pairing only
    /// shifts a deterministic subset of these (<see cref="AFFECTED_STAT_COUNT"/>), so links feel varied.
    /// </summary>
    public static readonly PlayerStat[] LinkStats =
    {
        PlayerStat.Passing, PlayerStat.Teamwork, PlayerStat.Positioning,
        PlayerStat.Creativity, PlayerStat.Composure
    };

    // ————————————————————— level —————————————————————

    /// <summary>The deterministic, symmetric chemistry level between two players (Neutral for self/null).</summary>
    public static ChemistryLevel GetLevel(Player a, Player b)
    {
        if (a == null || b == null || a == b) return ChemistryLevel.Neutral;

        int lo = Mathf.Min(a.PersonID, b.PersonID);
        int hi = Mathf.Max(a.PersonID, b.PersonID);
        float roll = Unit(Hash(lo, hi, 0x9E3779B1u));

        if (roll < BAD_BLOOD_CHANCE) return ChemistryLevel.BadBlood;
        if (roll < BAD_BLOOD_CHANCE + FROSTY_CHANCE) return ChemistryLevel.Frosty;
        if (roll < BAD_BLOOD_CHANCE + FROSTY_CHANCE + IN_SYNC_CHANCE) return ChemistryLevel.InSync;
        return ChemistryLevel.Neutral;
    }

    /// <summary>The flat stat change a chemistry level applies to each affected stat (−10…+5).</summary>
    public static int StatDelta(ChemistryLevel level) => (int)level * STAT_STEP;

    /// <summary>
    /// The deterministic subset of <see cref="LinkStats"/> a given pairing shifts (so different links
    /// flavour differently — one pair clicks passing &amp; creativity, another teamwork &amp; positioning).
    /// Symmetric: the same pair always returns the same stats regardless of argument order.
    /// </summary>
    public static PlayerStat[] AffectedStats(Player a, Player b)
    {
        int lo = a != null && b != null ? Mathf.Min(a.PersonID, b.PersonID) : 0;
        int hi = a != null && b != null ? Mathf.Max(a.PersonID, b.PersonID) : 0;

        // Deterministic partial Fisher–Yates over a copy of LinkStats, driven by the pair hash.
        var pool = (PlayerStat[])LinkStats.Clone();
        int take = Mathf.Min(AFFECTED_STAT_COUNT, pool.Length);
        var picked = new PlayerStat[take];

        int remaining = pool.Length;
        for (int i = 0; i < take; i++)
        {
            uint salt = 0xA5A5A5A5u + (uint)i * 0x9E3779B1u;
            int idx = (int)(Hash(lo, hi, salt) % (uint)remaining);
            picked[i] = pool[idx];
            pool[idx] = pool[remaining - 1]; // swap the chosen out of the live range
            remaining--;
        }
        return picked;
    }

    // ————————————————————— positional linking —————————————————————
    //
    // Chemistry only matters when two players line up in similar/adjacent roles — close enough to combine.
    // Same role (CB/CB, ST/ST, CM/CM…) always links; otherwise we use a hand-authored adjacency, kept
    // symmetric by checking both directions. Covers the examples asked for: LB/LM, LB/LW, RW/ST, AM/CM,
    // CB/CB, DM/CM, etc.

    /// <summary>Are these two positions close enough to "link up" for chemistry purposes? (Symmetric.)</summary>
    public static bool ArePositionsLinked(Player.Position a, Player.Position b)
    {
        if (a == b) return true; // same role, side by side
        return Contains(Neighbours(a), b) || Contains(Neighbours(b), a);
    }

    private static bool Contains(Player.Position[] arr, Player.Position p)
    {
        for (int i = 0; i < arr.Length; i++) if (arr[i] == p) return true;
        return false;
    }

    // Static adjacency tables (no per-call allocation). A keeper combines with no-one for chemistry.
    private static readonly Player.Position[] _none = { };
    private static readonly Player.Position[] _lb = { Player.Position.CB, Player.Position.LM, Player.Position.LW, Player.Position.DM };
    private static readonly Player.Position[] _rb = { Player.Position.CB, Player.Position.RM, Player.Position.RW, Player.Position.DM };
    private static readonly Player.Position[] _cb = { Player.Position.LB, Player.Position.RB, Player.Position.DM };
    private static readonly Player.Position[] _dm = { Player.Position.CB, Player.Position.CM, Player.Position.LB, Player.Position.RB };
    private static readonly Player.Position[] _lm = { Player.Position.LB, Player.Position.LW, Player.Position.CM };
    private static readonly Player.Position[] _rm = { Player.Position.RB, Player.Position.RW, Player.Position.CM };
    private static readonly Player.Position[] _cm = { Player.Position.DM, Player.Position.AM, Player.Position.LM, Player.Position.RM };
    private static readonly Player.Position[] _am = { Player.Position.CM, Player.Position.ST, Player.Position.LW, Player.Position.RW };
    private static readonly Player.Position[] _lw = { Player.Position.LM, Player.Position.LB, Player.Position.ST, Player.Position.AM };
    private static readonly Player.Position[] _rw = { Player.Position.RM, Player.Position.RB, Player.Position.ST, Player.Position.AM };
    private static readonly Player.Position[] _st = { Player.Position.AM, Player.Position.LW, Player.Position.RW };

    private static Player.Position[] Neighbours(Player.Position p)
    {
        switch (p)
        {
            case Player.Position.LB: return _lb;
            case Player.Position.RB: return _rb;
            case Player.Position.CB: return _cb;
            case Player.Position.DM: return _dm;
            case Player.Position.LM: return _lm;
            case Player.Position.RM: return _rm;
            case Player.Position.CM: return _cm;
            case Player.Position.AM: return _am;
            case Player.Position.LW: return _lw;
            case Player.Position.RW: return _rw;
            case Player.Position.ST: return _st;
            default: return _none; // GK
        }
    }

    // ————————————————————— display —————————————————————

    /// <summary>Short label for UI/inspection.</summary>
    public static string Label(ChemistryLevel level)
    {
        switch (level)
        {
            case ChemistryLevel.BadBlood: return "Bad Blood";
            case ChemistryLevel.Frosty:   return "Frosty";
            case ChemistryLevel.InSync:   return "In Sync";
            default:                      return "Neutral";
        }
    }

    // ————————————————————— hashing —————————————————————

    /// <summary>Stable FNV-1a-style mix of an unordered id pair + a salt — deterministic across runs/platforms.</summary>
    private static uint Hash(int lo, int hi, uint salt)
    {
        unchecked
        {
            uint h = 2166136261u;
            h = (h ^ (uint)lo) * 16777619u;
            h = (h ^ (uint)hi) * 16777619u;
            h = (h ^ salt) * 16777619u;
            // a couple of extra avalanche steps so nearby ids scatter well
            h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
            return h;
        }
    }

    /// <summary>Hash → a float in [0,1).</summary>
    private static float Unit(uint h) => (h & 0xFFFFFFu) / (float)0x1000000;
}
