using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;

public enum PlayerStat
{
    Shooting, Passing, Tackling, Dribbling, Crossing, Heading,
    Positioning, Intelligence, Creativity, Teamwork, Composure, Aggression,
    Pace, Strength, Jumping, Agility, Stamina, Durability,
    Height
}
public enum PlayerGroup
{
    Goalkeeper, Defenders, Midfielders, Attackers, Outfield, DefenseAndMidfield, MidfieldAndAttack, GoalkeeperAndDefense, WidePlayers
}

[System.Serializable]
public class Player : Person
{
    public const int SKILL_NO = 18;

    public Stats RawStats;
    public Stats GetRawStats()
    {
        return new Stats
        {
            Skills = (int[])RawStats.Skills.Clone(),
            Positions = new Dictionary<Position, PositionStrength>(RawStats.Positions),
            Height = RawStats.Height
        };
    }

    public int Fatigue { get; private set; }
    public float TacticFamiliarity { get; set; } = 0f;

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
        RawStats.Skills = new int[SKILL_NO];

        for(int i = 0; i < SKILL_NO; i++)
        {
            RawStats.Skills[i] = Random.Range(10, 90);
        }

        RawStats = PersonalityModifier(RawStats, Personality);

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
        Position[] relatedPositions = GetSimilarPositions(bestPosition);
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


    private static Position[] GetSimilarPositions(Position position)
    {
        switch (position)
        {
            case Position.GK:
                return new Position[] { };

            case Position.LB:
                return new Position[] { Position.LM, Position.CB };

            case Position.CB:
                return new Position[] { };

            case Position.RB:
                return new Position[] { Position.RM, Position.CB };

            case Position.DM:
                return new Position[] { Position.CM, Position.CB };

            case Position.LM:
                return new Position[] { Position.LB, Position.LW };

            case Position.CM:
                return new Position[] { Position.DM, Position.AM };

            case Position.RM:
                return new Position[] { Position.RB, Position.RW };

            case Position.LW:
                return new Position[] { Position.LM };

            case Position.AM:
                return new Position[] { Position.CM, Position.ST };

            case Position.RW:
                return new Position[] { Position.RM };

            case Position.ST:
                return new Position[] { Position.AM};

            default:
                return new Position[] { };
        }
    }

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
        public int Dribbling { get => Skills[3]; set => Skills[3] = Clamp(value); }
        public int Crossing { get => Skills[4]; set => Skills[4] = Clamp(value); }
        public int Heading { get => Skills[5]; set => Skills[5] = Clamp(value); }

        public int Positioning { get => Skills[6]; set => Skills[6] = Clamp(value); }
        public int Intelligence { get => Skills[7]; set => Skills[7] = Clamp(value); }
        public int Creativity { get => Skills[8]; set => Skills[8] = Clamp(value ); }
        public int Teamwork { get => Skills[9]; set => Skills[9] = Clamp(value); }
        public int Composure { get => Skills[10]; set => Skills[10] = Clamp(value); }
        public int Aggression { get => Skills[11]; set => Skills[11] = Clamp(value); }

        public int Pace { get => Skills[12]; set => Skills[12] = Clamp(value); }
        public int Strength { get => Skills[13]; set => Skills[13] = Clamp(value); }
        public int Jumping { get => Skills[14]; set => Skills[14] = Clamp(value); }
        public int Agility { get => Skills[15]; set => Skills[15] = Clamp(value)  ; }
        public int Stamina { get => Skills[16]; set => Skills[16] = Clamp(value); }
        public int Durability { get => Skills[17]; set => Skills[17] = Clamp(value); }

        [JsonIgnore] public int Attacking => (int)Game.WeightedAverage((Shooting, 1f), (Composure, 0.5f), (Dribbling, 0.2f), (Creativity, 0.2f), (Pace, 0.2f));
        [JsonIgnore] public int Midfield => (int)Game.WeightedAverage((Passing, 1f), (Positioning, 0.5f), (Crossing, 0.2f), (Creativity, 0.2f), (Intelligence, 0.2f));
        [JsonIgnore] public int Defending => (int)Game.WeightedAverage((Positioning, 1f), (Tackling, 0.5f), (Strength, 0.2f), (Pace, 0.2f));
        [JsonIgnore] public int Mental => (int)Game.Average(Intelligence, Teamwork, Composure);
        [JsonIgnore] public int Physical => (int)Game.WeightedAverage((Pace, 0.7f), (Strength, 0.5f), (Height, 0.3f));
        [JsonIgnore] public int Goalkeeping => (int)Game.Average(Jumping, Aggression, Composure, Positioning, Height);

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

        if (GetTeamIndex() < Team.Formation.Positions.Length)
        {
            newStats = GetStatsFor(Team.Formation.Positions[GetTeamIndex()].ID);
        }

        if(!ignoreMorale) newStats = MoraleModifier(newStats, Morale);

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

    public static Stats PersonalityModifier(Stats player, PersonalityType personality)
    {
        int bigChange = 40;
        int smallChange = 15;

        switch (personality)
        {
            case PersonalityType.Aggressive:
                player.Aggression = Apply(player.Aggression, bigChange, true);
                player.Composure = Apply(player.Composure, smallChange, false);
                break;

            case PersonalityType.Calm:
                player.Composure = Apply(player.Composure, bigChange, true);
                player.Aggression = Apply(player.Aggression, smallChange, false);
                break;

            case PersonalityType.Cautious:
                player.Creativity = Apply(player.Creativity, bigChange, false);
                player.Intelligence = Apply(player.Intelligence, smallChange, true);
                break;

            case PersonalityType.Cocky:
                player.Teamwork = Apply(player.Teamwork, bigChange, false);
                player.Creativity = Apply(player.Creativity, bigChange, true);
                break;

            case PersonalityType.Driven:
                player.Teamwork = Apply(player.Teamwork, bigChange, true);
                player.Stamina = Apply(player.Stamina, bigChange, true);
                break;

            case PersonalityType.Kind:
                player.Aggression = Apply(player.Aggression, bigChange, false);
                player.Composure = Apply(player.Composure, smallChange, true);
                break;

            case PersonalityType.Lazy:
                player.Intelligence = Apply(player.Intelligence, bigChange, false);
                player.Positioning = Apply(player.Positioning, smallChange, false);
                break;

            case PersonalityType.Shy:
                player.Teamwork = Apply(player.Teamwork, bigChange, false);
                player.Creativity = Apply(player.Creativity, smallChange, false);
                break;

            case PersonalityType.Silly:
                player.Creativity = Apply(player.Creativity, bigChange, true);
                player.Intelligence = Apply(player.Intelligence, smallChange, false);
                break;

            case PersonalityType.Smart:
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

    public static Stats MoraleModifier(Stats stats, Morale morale)
    {
        int moodDiff = morale.Mood - morale.IdealMood;
        int passionDiff = morale.Passion - morale.IdealPassion;

        int moodFactor = Mathf.Abs((int) (moodDiff / 5f));
        int passionFactor = Mathf.Abs((int)(passionDiff / 5f));

        if (moodDiff > 0)
        {
            stats.Aggression -= moodFactor;
            stats.Intelligence -= moodFactor;
            stats.Strength -= moodFactor;
        }
        else if (moodDiff < 0)
        {
            stats.Creativity -= moodFactor;
            stats.Teamwork -= moodFactor;
            stats.Durability -= passionFactor;
        }
        if (passionDiff > 0)
        {
            stats.Aggression += passionFactor;
            stats.Composure -= passionFactor;
            stats.Stamina -= passionFactor;
        }
        else if(passionDiff < 0)
        {
            stats.Composure += passionFactor;
            stats.Positioning -= passionFactor;
            stats.Pace -= passionFactor;
        }

        for(int i = 0; i < stats.Skills.Length; i++)
        {
            stats.Skills[i] = Mathf.Clamp(stats.Skills[i] - (int)(morale.DistanceToIdeal()/10f), 0, 100);
        }

        return stats;
    }

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
        return Team.GetPlayerIndex(this);
    }

    public Position? GetPosition()
    {
        if(GetTeamIndex() < Team.Formation.Positions.Length)
        {
            return Team.Formation.Positions[GetTeamIndex()].ID;
        }
        return null;
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
        int current = RawStats.GetStat(stat);
        RawStats.SetStat(stat, Mathf.RoundToInt(current + amount));
    }

    // ————————————————————— Training: Boost —————————————————————

    /// <summary>50% chance per session that a positional progress counter increments.</summary>
    public const float POSITIONAL_ROLL_CHANCE = 0.5f;

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
    public static Player.Stats AverageStats(this Player[] players, Tactic tactic = null){
        return AverageStats(players.ToList(), tactic);
    }

    /// <summary>
    /// Average effective stats across a group of players. When a <paramref name="tactic"/> is supplied
    /// (i.e. during a match) two tactical effects fold in here — the single seam that turns "raw players"
    /// into "players executing this tactic":
    ///   • Complexity / intelligence — when the squad average is too low, below-bar players take a
    ///     proportional cut to their mental stats (~50% at 10 below the bar).
    ///   • Reliance — a reliance instruction's chosen player counts for extra weight on the SPECIFIC stats
    ///     that reliance names, so his strengths AND weaknesses in those stats sway the team more.
    /// Called with no tactic (the default) it is a plain unweighted average, as before, for UI/display.
    /// </summary>
    public static Player.Stats AverageStats(this List<Player> players, Tactic tactic = null)
    {
        Player.Stats avgStats = new Player.Stats
        {
            Skills = new int[Player.SKILL_NO],
            Positions = new Dictionary<Player.Position, Player.PositionStrength>(),
            Height = 0
        };

        int count = players.Count;
        if (count == 0) return avgStats;

        // Per-stat totals + per-stat weights — a reliance can amplify only specific stats of one player.
        var totalSkills = new double[Player.SKILL_NO];
        var statWeight = new double[Player.SKILL_NO];
        double totalHeight = 0, heightWeight = 0;

        for (int i = 0; i < count; i++)
        {
            Player p = players[i];
            var stats = p.GetStats();

            // Complexity penalty — ONLY when the squad's average intelligence has fallen short of the
            // tactic's demand. Below-bar players take a proportional cut to their mental stats.
            if (tactic != null && tactic.ShouldApplyComplexityPenalty)
            {
                float reduction = tactic.ComplexityPenaltyFraction(stats.Intelligence);
                if (reduction > 0f)
                    foreach (PlayerStat st in Tactic.ComplexityAffectedStats)
                        stats.SetStat(st, Mathf.RoundToInt(stats.GetStat(st) * (1f - reduction)));
            }

            // Reliance weighting: only the reliant player, only the named stats, count extra.
            bool reliant = tactic != null && tactic.IsReliantPlayer(p);

            for (int j = 0; j < Player.SKILL_NO; j++)
            {
                float w = reliant ? 1f + tactic.RelianceBonus(p, (PlayerStat)j) : 1f;
                totalSkills[j] += stats.Skills[j] * w;
                statWeight[j] += w;
            }

            float hw = reliant ? 1f + tactic.RelianceBonus(p, PlayerStat.Height) : 1f;
            totalHeight += stats.Height * hw;
            heightWeight += hw;
        }

        var avgSkills = new int[Player.SKILL_NO];
        for (int j = 0; j < Player.SKILL_NO; j++)
            avgSkills[j] = statWeight[j] > 0 ? (int)(totalSkills[j] / statWeight[j]) : 0;

        avgStats.Skills = avgSkills;
        avgStats.Height = heightWeight > 0 ? (int)(totalHeight / heightWeight) : 0;

        return avgStats;
    }
}