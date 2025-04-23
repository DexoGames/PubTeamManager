using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public enum PlayerStat
{
    Shooting, Passing, Tackling, Dribbling, Crossing, Heading,
    Positioning, Intelligence, Teamwork, Composure, Aggression, Resilience,
    Pace, Strength, Jumping, Agility, Stamina, Durability,
    Height
}
public enum PlayerGroup
{
    Goalkeeper, Defenders, Midfielders, Attackers, Outfield, DefenseAndMidfield, MidfieldAndAttack, GoalkeeperAndDefense
}

[System.Serializable]
public class Player : Person
{
    public Stats RawStats;

    int teamIndex;

    public Player(Team team, Formation.Position[] teamPositions, int teamIndex)
    {
        GeneratePerson();

        Team = team;
        this.teamIndex = teamIndex;

        RawStats.Height = Random.Range(0, 100);

        foreach (FieldInfo field in typeof(Skills).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (field.FieldType == typeof(int))
            {
                field.SetValueDirect(__makeref(RawStats.Skills), UnityEngine.Random.Range(16, 85));
            }
        }

        RawStats.Skills = PersonalityModifier(RawStats.Skills, Personality);

        Position bestPosition = (Position)Random.Range(0, Game.GetEnumLength<Position>());
        if (teamIndex < teamPositions.Length && Random.Range(0, 3) == 0)
        {
            bestPosition = teamPositions[teamIndex].ID;
        }
        RawStats.Positions = new Dictionary<Position, PositionStrength>();
        RawStats.Positions.Add(bestPosition, PositionStrength.Natural);

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
    }

    public Stats GetStats()
    {
        Stats newStats = RawStats;

        if (GetTeamIndex() < Team.Formation.Positions.Length) newStats = GetStatsFor(Team.Formation.Positions[GetTeamIndex()].ID);

        newStats.Skills = MoraleModifier(newStats.Skills, Morale);

        return newStats;
    }

    public Stats GetStatsFor(Position position)
    {
        Stats newStats = RawStats;

        foreach (FieldInfo field in typeof(Skills).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (field.FieldType == typeof(int))
            {
                int skill = (int)field.GetValue(newStats.Skills);
                PositionStrength strength = newStats.Positions[position];
                field.SetValueDirect(__makeref(newStats.Skills), (int)(skill / (1+(4 - (float)strength)/3f)) );
            }
        }

        return newStats;
    }

    [System.Serializable]
    public struct Skills
    {
        public int Shooting, Passing, Tackling, Dribbling, Crossing, Heading;
        public int Positioning, Intelligence, Teamwork, Composure, Aggression, Resilience;
        public int Pace, Strength, Jumping, Agility, Stamina, Durability;
    }

    public Dictionary<PlayerStat, int> GetRawStatsDictionary()
    {
        Dictionary<PlayerStat, int> dict = new Dictionary<PlayerStat, int>();

        dict.Add(PlayerStat.Shooting, RawStats.Skills.Shooting);
        dict.Add(PlayerStat.Passing, RawStats.Skills.Passing);
        dict.Add(PlayerStat.Tackling, RawStats.Skills.Tackling);
        dict.Add(PlayerStat.Dribbling, RawStats.Skills.Dribbling);
        dict.Add(PlayerStat.Crossing, RawStats.Skills.Crossing);
        dict.Add(PlayerStat.Heading, RawStats.Skills.Heading);

        dict.Add(PlayerStat.Positioning, RawStats.Skills.Positioning);
        dict.Add(PlayerStat.Intelligence, RawStats.Skills.Intelligence);
        dict.Add(PlayerStat.Teamwork, RawStats.Skills.Teamwork);
        dict.Add(PlayerStat.Composure, RawStats.Skills.Composure);
        dict.Add(PlayerStat.Aggression, RawStats.Skills.Aggression);
        dict.Add(PlayerStat.Resilience, RawStats.Skills.Resilience);

        dict.Add(PlayerStat.Pace, RawStats.Skills.Pace);
        dict.Add(PlayerStat.Strength, RawStats.Skills.Strength);
        dict.Add(PlayerStat.Jumping, RawStats.Skills.Jumping);
        dict.Add(PlayerStat.Agility, RawStats.Skills.Agility);
        dict.Add(PlayerStat.Stamina, RawStats.Skills.Stamina);
        dict.Add(PlayerStat.Durability, RawStats.Skills.Durability);

        dict.Add(PlayerStat.Height, RawStats.Height);

        return dict;
    }


    public static Skills PersonalityModifier(Skills player, PersonalityType personality)
    {
        int bigChange = 15;
        //int smallChange = 10;

        switch (personality)
        {
            case PersonalityType.Aggressive:
                player.Aggression += bigChange;
                return player;

            case PersonalityType.Calm:
                player.Composure += bigChange;
                return player;

            case PersonalityType.Cautious:
                player.Composure -= bigChange;
                return player;

            case PersonalityType.Cocky:
                player.Teamwork -= bigChange;
                return player;

            case PersonalityType.Driven:
                player.Resilience += bigChange;
                return player;

            case PersonalityType.Kind:
                player.Aggression -= bigChange;
                return player;

            case PersonalityType.Lazy:
                player.Intelligence -= bigChange;
                return player;

            case PersonalityType.Shy:
                player.Resilience -= bigChange;
                return player;

            case PersonalityType.Silly:
                player.Teamwork += bigChange;
                return player;

            case PersonalityType.Smart:
                player.Intelligence += bigChange;
                return player;

            default:
                return player;
        }
    }

    public static Skills MoraleModifier(Skills player, int morale)
    {
        if (morale == 50) return player;

        int bigChange = (int)((morale / 100f - 0.5f) * 30f);
        int smallChange = (int)((morale / 100f - 0.5f) * 16f);

        player.Teamwork += bigChange;
        player.Aggression -= bigChange;
        player.Resilience -= smallChange;
        player.Composure += smallChange;
        player.Strength -= smallChange;
        player.Dribbling += smallChange;

        return player;
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

    [System.Serializable]
    public struct Stats
    {
        public Skills Skills;
        public Dictionary<Position, PositionStrength> Positions;
        public int Height;

        public int Attacking => (int)Game.Average(Skills.Shooting, Skills.Shooting, Skills.Dribbling, Skills.Composure);
        public int Midfield => (int)Game.Average(Skills.Passing, Skills.Intelligence, Skills.Positioning, Skills.Crossing, Skills.Dribbling);
        public int Defending => (int)Game.Average(Skills.Tackling, Skills.Tackling, Skills.Positioning, Skills.Positioning, Skills.Strength, Skills.Passing);
        public int Mental => (int)Game.Average(Skills.Intelligence, Skills.Teamwork, Skills.Resilience, Skills.Composure);
        public int Physical => (int)Game.Average(Skills.Pace, Skills.Strength, Skills.Stamina, Skills.Durability);
        public int Goalkeeping => (int)Game.Average(Skills.Jumping, Skills.Aggression, Skills.Composure, Skills.Positioning);
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
}