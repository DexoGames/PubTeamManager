using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;

public enum PlayerStat
{
    // Existing numeric values are save/asset data. Do not renumber them.
    Shooting = 0, Passing = 1, Tackling = 2, Technique = 18, Dribbling = 3, Crossing = 4, Heading = 5,
    Positioning = 6, Creativity = 8, Intelligence = 7, Marking = 19, Aggression = 11, Composure = 10, Teamwork = 9,
    Strength = 13, Agility = 15, Pace = 12, Stamina = 16, Jumping = 14, Durability = 17,
    Height = 20
}
public enum PlayerGroup
{
    Goalkeeper, Defenders, Midfielders, Attackers, Outfield, DefenseAndMidfield, MidfieldAndAttack, GoalkeeperAndDefense, WidePlayers
}

[System.Serializable]
public class Player : Person
{
    public const int SKILL_NO = 20;
    private const int LEGACY_SKILL_NO = 18;

    public static readonly PlayerStat[] TechnicalStats =
    {
        PlayerStat.Shooting, PlayerStat.Passing, PlayerStat.Tackling, PlayerStat.Technique,
        PlayerStat.Dribbling, PlayerStat.Crossing, PlayerStat.Heading
    };

    public static readonly PlayerStat[] MentalStats =
    {
        PlayerStat.Positioning, PlayerStat.Creativity, PlayerStat.Intelligence, PlayerStat.Marking,
        PlayerStat.Aggression, PlayerStat.Composure, PlayerStat.Teamwork
    };

    public static readonly PlayerStat[] PhysicalStats =
    {
        PlayerStat.Strength, PlayerStat.Agility, PlayerStat.Pace,
        PlayerStat.Stamina, PlayerStat.Jumping, PlayerStat.Durability
    };

    /// <summary>Canonical presentation order. Height is intentionally separate from the skills.</summary>
    public static readonly PlayerStat[] SkillDisplayOrder =
    {
        PlayerStat.Shooting, PlayerStat.Passing, PlayerStat.Tackling, PlayerStat.Technique,
        PlayerStat.Dribbling, PlayerStat.Crossing, PlayerStat.Heading,
        PlayerStat.Positioning, PlayerStat.Creativity, PlayerStat.Intelligence, PlayerStat.Marking,
        PlayerStat.Aggression, PlayerStat.Composure, PlayerStat.Teamwork,
        PlayerStat.Strength, PlayerStat.Agility, PlayerStat.Pace,
        PlayerStat.Stamina, PlayerStat.Jumping, PlayerStat.Durability
    };

    public Stats RawStats;
    public Stats GetRawStats()
    {
        EnsureStatData();
        return new Stats
        {
            Skills = (int[])RawStats.Skills.Clone(),
            Positions = new Dictionary<Position, PositionStrength>(RawStats.Positions),
            Height = RawStats.Height
        };
    }

    public SelectionHistory Selection = new SelectionHistory();

    [JsonProperty] public int Fatigue { get; private set; }
    [JsonIgnore] public bool SentOff;
    [JsonIgnore] public bool LeftInjured;
    [JsonIgnore] public bool CanPlay => IsAvailable && !SentOff && !LeftInjured;

    public void AccumulateFatigue(int intensity)
    {
        int exertion = 2 + (intensity + 100 - RawStats.Stamina) / 45;
        Fatigue = Mathf.Min(100, Fatigue + exertion);
    }

    public void RecoverOvernight()
    {
        Fatigue = Mathf.Max(0, Fatigue - 8 - RawStats.Stamina / 12);
    }

    // ————————————————————— Transient per-match morale state (not saved) —————————————————————
    /// <summary>True once this player has dropped into a morale SLUMP this match (mood treated as 0 until the end
    /// of the game, unless a team talk snaps them out). Reset each match; never serialized. See <see cref="MoraleEffects"/>.</summary>
    [JsonIgnore] public bool InSlump;

    /// <summary>True only for the single minute the player is BLUNDERING (a complacency/over-eagerness collapse).
    /// Re-rolled every minute by the match engine; never serialized.</summary>
    [JsonIgnore] public bool MoraleBlunder;

    // ————————————————————— Availability (injuries / suspensions / death) —————————————————————

    /// <summary>Current injury, if any (None when fit). Death is permanent and handled via <see cref="IsDeceased"/>.</summary>
    public InjuryType CurrentInjury = InjuryType.None;

    /// <summary>Date the current injury heals (null when fit). Cleared by <see cref="TickInjuryRecovery"/>.</summary>
    public System.DateTime? InjuredUntil;

    /// <summary>Matches still to sit out through suspension (decremented as the team plays).</summary>
    public int MatchesSuspended;

    /// <summary>Yellow cards accrued this season; every <see cref="YELLOW_SUSPENSION_THRESHOLD"/> earns a ban.</summary>
    public int YellowCards;

    /// <summary>True if the player has died (very rare event) — permanently unavailable.</summary>
    public bool IsDeceased;

    /// <summary>Yellow cards that trigger a one-match ban.</summary>
    public const int YELLOW_SUSPENSION_THRESHOLD = 5;

    [JsonIgnore] private System.DateTime Today => CalenderManager.Instance != null ? CalenderManager.Instance.CurrentDay.Date : System.DateTime.MinValue;

    [JsonIgnore]
    public bool IsInjured =>
        !IsDeceased && CurrentInjury != InjuryType.None && CurrentInjury != InjuryType.Death
        && InjuredUntil.HasValue && InjuredUntil.Value.Date >= Today;

    [JsonIgnore] public bool IsSuspended => MatchesSuspended > 0;

    /// <summary>True only if fit, unsuspended and alive — i.e. selectable for a match.</summary>
    [JsonIgnore] public bool IsAvailable => !IsDeceased && !IsInjured && !IsSuspended;

    /// <summary>Applies an injury (taking the longer of any existing one). Death is permanent.</summary>
    public void ApplyInjury(InjuryType type, System.DateTime today)
    {
        if (type == InjuryType.None) return;

        if (type == InjuryType.Death)
        {
            IsDeceased = true;
            CurrentInjury = InjuryType.Death;
            InjuredUntil = null;
            return;
        }

        System.DateTime until = today.Date.AddDays(InjuryDurationDays(type));
        if (!IsInjured || (InjuredUntil.HasValue && until > InjuredUntil.Value))
        {
            CurrentInjury = type;
            InjuredUntil = until;
        }
    }

    /// <summary>Clears an injury once its recovery date has passed.</summary>
    public void TickInjuryRecovery(System.DateTime today)
    {
        if (CurrentInjury == InjuryType.None || CurrentInjury == InjuryType.Death) return;
        if (InjuredUntil.HasValue && InjuredUntil.Value.Date < today.Date)
        {
            CurrentInjury = InjuryType.None;
            InjuredUntil = null;
        }
    }

    /// <summary>Records a yellow; returns true if it tipped the player into a suspension.</summary>
    public bool AddYellowCard()
    {
        YellowCards++;
        if (YellowCards % YELLOW_SUSPENSION_THRESHOLD == 0)
        {
            MatchesSuspended += 1;
            return true;
        }
        return false;
    }

    /// <summary>Straight red — a one-match ban (violent conduct could be extended by the caller).</summary>
    public void ApplyRedCard(int matches = 1) => MatchesSuspended += Mathf.Max(1, matches);

    /// <summary>Counts off one match of a suspension (called when the player's team completes a match).</summary>
    public void ServeOneSuspensionMatch()
    {
        if (MatchesSuspended > 0) MatchesSuspended--;
    }

    /// <summary>Typical lay-off in days for each injury severity.</summary>
    public static int InjuryDurationDays(InjuryType type)
    {
        switch (type)
        {
            case InjuryType.Knock: return UnityEngine.Random.Range(2, 7);
            case InjuryType.Standard: return UnityEngine.Random.Range(10, 26);
            case InjuryType.Hamstring: return UnityEngine.Random.Range(21, 46);
            case InjuryType.ACL: return UnityEngine.Random.Range(180, 301);
            default: return 0;
        }
    }

    /// <summary>Human-readable availability for squad/selection UI.</summary>
    [JsonIgnore]
    public string AvailabilityStatus
    {
        get
        {
            if (IsDeceased) return "Deceased";
            if (IsInjured)
            {
                string when = InjuredUntil.HasValue ? $", back ~{CalenderManager.ShortDateWordsNoYear(InjuredUntil.Value)}" : "";
                return $"Injured ({CurrentInjury}{when})";
            }
            if (IsSuspended) return $"Suspended ({MatchesSuspended})";
            return "Available";
        }
    }

    /// <summary>
    /// Temporary per-skill "Boost" layer from training (0..MAX_BOOST each).
    /// Added on top of raw skills in the stats pipeline; builds with training and
    /// decays when not actively trained. Never modifies the underlying RawStats.
    /// </summary>
    public int[] Boost = new int[SKILL_NO];

    /// <summary>Max amount a stat can be Boosted above its actual value.</summary>
    public const int MAX_BOOST = 15;

    /// <summary>
    /// Per-position progress counters towards the next PositionStrength level,
    /// driven by positional training. Resets to 0 on each level-up.
    /// </summary>
    public Dictionary<Position, int> PositionProgress = new Dictionary<Position, int>();

    public Player(Team team, Formation.Position[] teamPositions, int index)
    {
        GeneratePerson();

        Team = team;

        RawStats.Height = Random.Range(0, 100);
        RawStats.Skills = PlayerGenerator.GenerateCorrelatedSkills();

        RawStats = PlayerGenerator.PersonalityModifier(RawStats, Personality);

        Position bestPosition = (Position)Random.Range(0, Game.GetEnumLength<Position>());
        if (index >= 0 && index < teamPositions.Length)
        {
            if(team != TeamManager.Instance.MyTeam || Random.Range(0, 3) == 0)
            {
                bestPosition = teamPositions[index].ID;
            }
        }
        RawStats.Positions = new Dictionary<Position, PositionStrength>
        {
            { bestPosition, PositionStrength.Natural }
        };

        int length = Game.GetEnumLength<Position>();

        for (int i = 0; i < Game.GetEnumLength<Position>(); i++)
        {
            PositionStrength strength = PositionStrength.None;

            int rand = UnityEngine.Random.Range(0, 10);

            if (rand >= 8) strength = PositionStrength.Good;
            else if (rand >= 6) strength = PositionStrength.Okay;
            else if (rand >= 3) strength = PositionStrength.Poor;

            RawStats.Positions.TryAdd((Position)i, strength);
        }

        // Spilling effect: +1 to similar positions (capped at Good)
        Position[] relatedPositions = PlayerGenerator.GetSimilarPositions(bestPosition);
        foreach (Position relatedPos in relatedPositions)
        {
            if (RawStats.Positions.ContainsKey(relatedPos) && RawStats.Positions[relatedPos] < PositionStrength.Good)
            {
                RawStats.Positions[relatedPos] = (PositionStrength)((int)RawStats.Positions[relatedPos] + 1);
            }
        }
    }

    /// <summary>Parameterless constructor for JSON deserialization.</summary>
    public Player() { }

    [System.Serializable]
    public struct Stats
    {
        public int[] Skills;
        public Dictionary<Position, PositionStrength> Positions;
        public int Height;

        int Clamp(int value) { return Mathf.Clamp(value, 0, 100); }

        public int Shooting { get => Skills[0]; set => Skills[0] = Clamp(value); }
        public int Passing { get => Skills[1]; set => Skills[1] = Clamp(value); }
        public int Tackling { get => Skills[2]; set => Skills[2] = Clamp(value); }
        public int Technique { get => Skills[18]; set => Skills[18] = Clamp(value); }
        public int Dribbling { get => Skills[3]; set => Skills[3] = Clamp(value); }
        public int Crossing { get => Skills[4]; set => Skills[4] = Clamp(value); }
        public int Heading { get => Skills[5]; set => Skills[5] = Clamp(value); }

        public int Positioning { get => Skills[6]; set => Skills[6] = Clamp(value); }
        public int Creativity { get => Skills[8]; set => Skills[8] = Clamp(value ); }
        public int Intelligence { get => Skills[7]; set => Skills[7] = Clamp(value); }
        public int Marking { get => Skills[19]; set => Skills[19] = Clamp(value); }
        public int Aggression { get => Skills[11]; set => Skills[11] = Clamp(value); }
        public int Composure { get => Skills[10]; set => Skills[10] = Clamp(value); }
        public int Teamwork { get => Skills[9]; set => Skills[9] = Clamp(value); }

        public int Strength { get => Skills[13]; set => Skills[13] = Clamp(value); }
        public int Agility { get => Skills[15]; set => Skills[15] = Clamp(value)  ; }
        public int Pace { get => Skills[12]; set => Skills[12] = Clamp(value); }
        public int Stamina { get => Skills[16]; set => Skills[16] = Clamp(value); }
        public int Jumping { get => Skills[14]; set => Skills[14] = Clamp(value); }
        public int Durability { get => Skills[17]; set => Skills[17] = Clamp(value); }

        [JsonIgnore] public int Attacking => (int)Game.WeightedAverage(
            (Shooting, 1f), (Technique, 0.5f), (Composure, 0.45f), (Dribbling, 0.35f),
            (Positioning, 0.25f), (Creativity, 0.2f), (Pace, 0.15f), (Heading, 0.1f));
        [JsonIgnore] public int Midfield => (int)Game.WeightedAverage(
            (Passing, 1f), (Technique, 0.55f), (Intelligence, 0.45f), (Creativity, 0.45f),
            (Teamwork, 0.3f), (Positioning, 0.25f), (Dribbling, 0.15f), (Crossing, 0.15f));
        [JsonIgnore] public int Defending => (int)Game.WeightedAverage(
            (Marking, 1f), (Positioning, 0.75f), (Tackling, 0.65f), (Intelligence, 0.35f),
            (Strength, 0.2f), (Pace, 0.15f), (Heading, 0.1f));
        [JsonIgnore] public int Mental => (int)Game.Average(
            Positioning, Creativity, Intelligence, Marking, Aggression, Composure, Teamwork);
        [JsonIgnore] public int Physical => (int)Game.Average(
            Strength, Agility, Pace, Stamina, Jumping, Durability);
        [JsonIgnore] public int Goalkeeping => (int)Game.WeightedAverage(
            (Positioning, 1f), (Agility, 1f), (Composure, 0.8f), (Jumping, 0.7f),
            (Intelligence, 0.5f), (Height, 0.4f), (Technique, 0.35f));

        public int GetStat(PlayerStat stat)
        {
            return stat == PlayerStat.Height ? Height : Skills[(int)stat];
        }

        public void SetStat(PlayerStat stat, int value)
        {
            if (stat == PlayerStat.Height)
                Height = Clamp(value);
            else
                Skills[(int)stat] = Clamp(value);
        }
    }

    public Stats GetStats(bool ignoreMorale = false)
    {
        Stats newStats = GetBoostedRawStats();

        Position? position = GetPosition();
        if (position.HasValue) newStats = GetStatsFor(position.Value);

        // Morale: a constant drop-off when below ideal (with a slump pinning mood to its worst), plus a one-minute
        // collapse when blundering. Applies to ALL teams — the AI is subject to morale too. See MoraleEffects.
        if (!ignoreMorale)
        {
            newStats = MoraleEffects.ApplyConstant(newStats, Morale, InSlump);
            if (MoraleBlunder) newStats = MoraleEffects.ApplyBlunder(newStats);
        }

        newStats.Pace -= Fatigue / 6;
        newStats.Agility -= Fatigue / 8;
        newStats.Composure -= Fatigue / 12;
        return newStats;
    }

    /// <summary>
    /// Intelligence used for the tactical complexity / squad-IQ checks AND shown in the tactics PositionUI,
    /// so the displayed value always matches what the squad IQ actually uses. This is the player's EFFECTIVE
    /// intelligence (i.e. <see cref="GetStats"/>().Intelligence): it reflects their training boost, current
    /// morale, and the off-position penalty for the slot they're playing in.
    /// </summary>
    public int TacticalIntelligence()
    {
        return GetStats().Intelligence;
    }

    /// <summary>
    /// Raw stats with the temporary training Boost applied on top of each skill
    /// (clamped to 100). This is the entry point for the effective-stats pipeline,
    /// so Boost flows through positional scaling and morale automatically.
    /// </summary>
    private Stats GetBoostedRawStats()
    {
        Stats s = GetRawStats();
        EnsureBoost();
        for (int i = 0; i < SKILL_NO; i++)
            s.Skills[i] = Mathf.Min(100, s.Skills[i] + Boost[i]);
        return s;
    }


    public Stats GetStatsFor(Position position)
    {
        Stats newStats = GetBoostedRawStats();

        PositionStrength strength = newStats.Positions[position];

        for (int i = 0; i < SKILL_NO; i++)
        {
            if (i >= (int)PlayerStat.Pace && i <= (int)PlayerStat.Durability) continue;

            int skill = newStats.Skills[i];
            newStats.Skills[i] = (int)(skill / (1 + (4 - (int)strength) / 3f));
        }

        return newStats;
    }

    /// <summary>Backward-compatible entry point for the CONSTANT morale drop-off (no slump). The full model —
    /// slump + per-minute blunder — lives in <see cref="MoraleEffects"/> and is applied in <see cref="GetStats"/>.</summary>
    public static Stats MoraleModifier(Stats stats, Morale morale) => MoraleEffects.ApplyConstant(stats, morale, false);

    public enum Position
    {
        GK = 0, LB = 1, CB = 2, RB = 3, DM = 4, LM = 5, CM = 6, RM = 7, LW = 8, AM = 9, RW = 10, ST = 11
    }
    public enum PositionStrength
    {
        None, Poor, Okay, Good, Natural
    }
    public static string LongPosition(Position pos)
    {
        switch (pos)
        {
            case Position.GK:
                return "Goalkeeper";
            case Position.CB:
                return "Centre Back";
            case Position.LB:
                return "Left Back";
            case Position.RB:
                return "Right Back";
            case Position.DM:
                return "Defensive Midfielder";
            case Position.CM:
                return "Central Midfielder";
            case Position.LM:
                return "Left Midfielder";
            case Position.RM:
                return "Right Midfielder";
            case Position.AM:
                return "Attacking Midfielder";
            case Position.LW:
                return "Left Winger";
            case Position.RW:
                return "Right Winger";
            case Position.ST:
                return "Striker";
            default:
                return "Null";
        }
    }

    public int GetAverage(Position position)
    {
        int average;

        int defending = GetStatsFor(position).Defending;
        int midfield = GetStatsFor(position).Midfield;
        int attacking = GetStatsFor(position).Attacking;
        int physical = GetStatsFor(position).Physical;
        int goalkeeping = GetStatsFor(position).Goalkeeping;

        if (position == Position.GK)
        {
            average = goalkeeping;
        }
        else if(position == Position.CB)
        {
            average = (int)Game.WeightedAverage( (defending, 2), (physical, 1) );
        }
        else if(position == Position.LB || position == Position.RB)
        {
            average = (int)Game.WeightedAverage( (defending, 2), (midfield, 1) );
        }
        else if(position == Position.DM)
        {
            average = (int)Game.WeightedAverage((defending, 1), (midfield, 1) );
        }
        else if(position == Position.CM || position == Position.LM || position == Position.RM)
        {
            average = (int)Game.WeightedAverage( (midfield, 3), (attacking, 1) );
        }
        else if (position == Position.AM || position == Position.LW || position == Position.RW)
        {
            average = (int)Game.WeightedAverage((midfield, 2), (attacking, 3) );
        }
        else if (position == Position.ST)
        {
            average = (int)Game.WeightedAverage( (attacking, 1) );
        }
        else
        {
            average = (int)Game.WeightedAverage((defending, 1), (midfield, 1), (attacking, 1));
        }

        average = (int)Game.WeightedAverage( (average, 3), (physical, 1) );

        return average;
    }

    public Rating GetRating(Position position)
    {
        int average = Mathf.Clamp(GetAverage(position) + RatingOffset, 0, 100);

        int rating = average / 14;

        return (Rating)rating;
    }

    public int GetTeamIndex()
    {
        return Team != null ? Team.GetPlayerIndex(this) : -1;
    }

    /// <summary>This player's hidden chemistry with another (symmetric, deterministic). For UI/inspection;
    /// the in-match stat effect is applied via the tactic's per-match cache, not here.</summary>
    public ChemistryLevel GetChemistryWith(Player other) => Chemistry.GetLevel(this, other);

    public Position? GetPosition()
    {
        int slot = GetTeamIndex();
        if (slot < 0 || slot >= Team.Formation.Positions.Length) return null;
        return Team.Formation.Positions[slot].ID;
    }

    public bool IsPositionIn(params Position[] validPositions)
    {
        return GetPosition().HasValue && validPositions.Contains(GetPosition().Value);
    }

    public Position BestPosition()
    {
        PositionStrength bestStrength = PositionStrength.None;
        Position bestPos = Position.CM;

        foreach(Position pos in RawStats.Positions.Keys)
        {
            if (RawStats.Positions[pos] >= bestStrength)
            {
                bestStrength = RawStats.Positions[pos];
                bestPos = pos;
            }
        }
        return bestPos;
    }
    public Position[] BestPositions()
    {
        List<Position> positions = new List<Position>();

        positions.Add(BestPosition());

        foreach (Position pos in RawStats.Positions.Keys)
        {
            if (RawStats.Positions[pos] >= PositionStrength.Good)
            {
                if(!positions.Contains(pos)) positions.Add(pos);
            }
        }

        positions.Sort((p1, p2) => GetStats().Positions[p2].CompareTo(GetStats().Positions[p1]));

        return positions.ToArray();
    }

    public string ListBestPositions()
    {
        string text = "";
        foreach (Player.Position pos in BestPositions())
        {
            text += $"{pos.ToString()}, ";
        }
        text = text.Remove(text.Length - 2);

        return text;
    }

    public string AddBestPositions()
    {
        string text = " (";
        text += ListBestPositions();
        text += ")";
        return text;
    }

    public int GetKitNumber()
    {
        if (Team.KitNumbers.TryGetValue(this, out int kitNumber))
        {
            return kitNumber;
        }
        return 0;
    }

    public int HeightToCm()
    {
        return (int)Mathf.Lerp(155, 198, RawStats.Height/100f);
    }

    public static string CmToFeet(int cm)
    {
        float totalInches = cm / 2.54f;
        int feet = (int)(totalInches / 12);
        float inches = totalInches % 12;

        return $"{feet}'{(int)inches}\"";
    }

    /// <summary>
    /// Modifies a raw stat by the given amount (clamped 0-100).
    /// Used by the training system.
    /// </summary>
    public void ModifyStat(PlayerStat stat, float amount)
    {
        EnsureStatData();
        int current = RawStats.GetStat(stat);
        RawStats.SetStat(stat, Mathf.RoundToInt(current + amount));
    }

    // ————————————————————— Training: Boost —————————————————————

    /// <summary>50% chance per session that a positional progress counter increments.</summary>
    public const float POSITIONAL_ROLL_CHANCE = 0.5f;

    /// <summary>
    /// Expands legacy 18-skill saves into the current schema without changing any existing stat's numeric ID.
    /// New skills are inferred deterministically from their closest old skills; new players never use this path.
    /// </summary>
    public void EnsureStatData()
    {
        int[] oldSkills = RawStats.Skills;
        if (oldSkills == null || oldSkills.Length != SKILL_NO)
        {
            var rebuilt = Enumerable.Repeat(40, SKILL_NO).ToArray();
            if (oldSkills != null)
            {
                for (int i = 0; i < Mathf.Min(oldSkills.Length, SKILL_NO); i++)
                    rebuilt[i] = Mathf.Clamp(oldSkills[i], 0, 100);

                if (oldSkills.Length <= LEGACY_SKILL_NO)
                {
                    rebuilt[(int)PlayerStat.Technique] = Mathf.RoundToInt(Game.WeightedAverage(
                        (LegacySkill(oldSkills, PlayerStat.Dribbling), 0.4f),
                        (LegacySkill(oldSkills, PlayerStat.Passing), 0.35f),
                        (LegacySkill(oldSkills, PlayerStat.Shooting), 0.15f),
                        (LegacySkill(oldSkills, PlayerStat.Agility), 0.1f)));
                }

                if (oldSkills.Length <= (int)PlayerStat.Marking)
                {
                    rebuilt[(int)PlayerStat.Marking] = Mathf.RoundToInt(Game.WeightedAverage(
                        (LegacySkill(oldSkills, PlayerStat.Positioning), 0.4f),
                        (LegacySkill(oldSkills, PlayerStat.Tackling), 0.35f),
                        (LegacySkill(oldSkills, PlayerStat.Intelligence), 0.15f),
                        (LegacySkill(oldSkills, PlayerStat.Teamwork), 0.1f)));
                }
            }

            RawStats.Skills = rebuilt;
        }

        if (RawStats.Positions == null)
            RawStats.Positions = new Dictionary<Position, PositionStrength>();
        foreach (Position position in System.Enum.GetValues(typeof(Position)))
            if (!RawStats.Positions.ContainsKey(position)) RawStats.Positions[position] = PositionStrength.None;

        EnsureBoost();
    }

    private static int LegacySkill(int[] skills, PlayerStat stat)
    {
        int index = (int)stat;
        return skills != null && index >= 0 && index < skills.Length ? skills[index] : 40;
    }

    /// <summary>Guards the Boost array against null/old-save length mismatch.</summary>
    public void EnsureBoost()
    {
        if (Boost != null && Boost.Length == SKILL_NO) return;

        int[] rebuilt = new int[SKILL_NO];
        if (Boost != null)
            for (int i = 0; i < Mathf.Min(Boost.Length, SKILL_NO); i++) rebuilt[i] = Boost[i];
        Boost = rebuilt;
    }

    /// <summary>Guards the positional progress dictionary against null (old saves).</summary>
    public void EnsurePositions()
    {
        if (PositionProgress == null) PositionProgress = new Dictionary<Position, int>();
    }

    /// <summary>
    /// Applies one training session's Boost effect: trained stats build (capped at
    /// MAX_BOOST and protected from decay this session), all other skills decay.
    /// Pass an empty/null set (e.g. for tactical/social drills) to decay everything.
    /// </summary>
    public void ApplyBoostSession(IEnumerable<PlayerStat> trainedStats, int build, int decay)
    {
        EnsureBoost();

        HashSet<int> trained = new HashSet<int>();
        if (trainedStats != null)
        {
            foreach (PlayerStat stat in trainedStats)
            {
                int i = (int)stat;
                if (i < 0 || i >= SKILL_NO) continue; // Height has no Boost slot
                Boost[i] = Mathf.Min(MAX_BOOST, Boost[i] + build);
                trained.Add(i);
            }
        }

        for (int i = 0; i < SKILL_NO; i++)
        {
            if (trained.Contains(i)) continue;
            Boost[i] = Mathf.Max(0, Boost[i] - decay);
        }
    }

    /// <summary>Current Boost on a given skill (0 for Height, which has no Boost slot).</summary>
    public int GetBoost(PlayerStat stat)
    {
        int i = (int)stat;
        if (i < 0 || i >= SKILL_NO) return 0;
        EnsureBoost();
        return Boost[i];
    }

    // ————————————————————— Training: Positional —————————————————————

    /// <summary>
    /// One positional-training tick for a target position. With POSITIONAL_ROLL_CHANCE,
    /// the progress counter increments; on reaching the level threshold the position
    /// strength rises one level (permanently) and the counter resets. Caps at Natural.
    /// </summary>
    public void TickPositionalRoll(Position pos)
    {
        EnsurePositions();

        PositionStrength current = RawStats.Positions.TryGetValue(pos, out var s) ? s : PositionStrength.None;
        if (current >= PositionStrength.Natural) return;

        if (UnityEngine.Random.value >= POSITIONAL_ROLL_CHANCE) return;

        int counter = (PositionProgress.TryGetValue(pos, out var c) ? c : 0) + 1;

        if (counter >= ProgressThreshold(current))
        {
            RawStats.Positions[pos] = (PositionStrength)((int)current + 1);
            PositionProgress[pos] = 0;
        }
        else
        {
            PositionProgress[pos] = counter;
        }
    }

    /// <summary>Counter needed to advance from the given strength to the next level.</summary>
    public static int ProgressThreshold(PositionStrength from)
    {
        switch (from)
        {
            case PositionStrength.None: return 2; // None → Poor
            case PositionStrength.Poor: return 3; // Poor → Okay
            case PositionStrength.Okay: return 4; // Okay → Good
            case PositionStrength.Good: return 8; // Good → Natural (the jump)
            default: return int.MaxValue;
        }
    }
}

public static class PlayerExtensions
{
    public static Player.Stats AverageStats(this Player[] players, Tactic tactic = null, PlayerGroup? phaseGroup = null)
        => new SquadStatProfile(players, tactic, phaseGroup).ToAverageStats();

    /// <summary>
    /// Compatibility arithmetic mean for UI and team summaries. Match phases use <see cref="StatProfile(List{Player},Tactic,PlayerGroup?)"/>
    /// so each stat can choose a distribution shape. Both routes share the same complexity, chemistry and reliance
    /// processing in <see cref="SquadStatProfile"/>.
    /// </summary>
    public static Player.Stats AverageStats(this List<Player> players, Tactic tactic = null, PlayerGroup? phaseGroup = null)
        => new SquadStatProfile(players, tactic, phaseGroup).ToAverageStats();

    public static SquadStatProfile StatProfile(this Player[] players, Tactic tactic = null, PlayerGroup? phaseGroup = null)
        => new SquadStatProfile(players, tactic, phaseGroup);

    public static SquadStatProfile StatProfile(this List<Player> players, Tactic tactic = null, PlayerGroup? phaseGroup = null)
        => new SquadStatProfile(players, tactic, phaseGroup);
}
