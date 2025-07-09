using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.VisualScripting;
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

    public Player(Team team, Formation.Position[] teamPositions)
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
        if (GetTeamIndex() >= 0 && GetTeamIndex() < teamPositions.Length && Random.Range(0, 3) == 0)
        {
            bestPosition = teamPositions[GetTeamIndex()].ID;
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

    [System.Serializable]
    public struct Stats
    {
        public int[] Skills;
        public Dictionary<Position, PositionStrength> Positions;
        public int Height;

        public int Shooting { get => Skills[0]; set => Skills[0] = value; }
        public int Passing { get => Skills[1]; set => Skills[1] = value; }
        public int Tackling { get => Skills[2]; set => Skills[2] = value; }
        public int Dribbling { get => Skills[3]; set => Skills[3] = value; }
        public int Crossing { get => Skills[4]; set => Skills[4] = value; }
        public int Heading { get => Skills[5]; set => Skills[5] = value; }

        public int Positioning { get => Skills[6]; set => Skills[6] = value; }
        public int Intelligence { get => Skills[7]; set => Skills[7] = value; }
        public int Creativity { get => Skills[8]; set => Skills[8] = value; }
        public int Teamwork { get => Skills[9]; set => Skills[9] = value; }
        public int Composure { get => Skills[10]; set => Skills[10] = value; }
        public int Aggression { get => Skills[11]; set => Skills[11] = value; }

        public int Pace { get => Skills[12]; set => Skills[12] = value; }
        public int Strength { get => Skills[13]; set => Skills[13] = value; }
        public int Jumping { get => Skills[14]; set => Skills[14] = value; }
        public int Agility { get => Skills[15]; set => Skills[15] = value; }
        public int Stamina { get => Skills[16]; set => Skills[16] = value; }
        public int Durability { get => Skills[17]; set => Skills[17] = value; }

        public int Attacking => (int)Game.WeightedAverage((Shooting, 1f), (Composure, 0.5f), (Dribbling, 0.2f), (Creativity, 0.2f), (Pace, 0.2f));
        public int Midfield => (int)Game.WeightedAverage((Passing, 1f), (Positioning, 0.5f), (Crossing, 0.2f), (Creativity, 0.2f), (Intelligence, 0.2f));
        public int Defending => (int)Game.WeightedAverage((Positioning, 1f), (Tackling, 0.5f), (Strength, 0.2f), (Pace, 0.2f));
        public int Mental => (int)Game.Average(Intelligence, Teamwork, Composure);
        public int Physical => (int)Game.WeightedAverage((Pace, 1f), (Strength, 0.5f));
        public int Goalkeeping => (int)Game.Average(Jumping, Aggression, Composure, Positioning, Height);
    }

    public Stats GetStats(bool ignoreMorale = false)
    {
        Stats newStats = GetRawStats();

        if (GetTeamIndex() < Team.Formation.Positions.Length)
        {
            newStats = GetStatsFor(Team.Formation.Positions[GetTeamIndex()].ID);
        }

        if(!ignoreMorale) newStats = MoraleModifier(newStats, Morale);

        return newStats;
    }


    public Stats GetStatsFor(Position position)
    {
        Stats newStats = GetRawStats();

        PositionStrength strength = newStats.Positions[position];

        for (int i = 0; i < SKILL_NO; i++)
        {
            if (i >= (int)PlayerStat.Pace && i <= (int)PlayerStat.Durability) continue;

            int skill = newStats.Skills[i];
            newStats.Skills[i] = (int)(skill / (1 + (4 - (int)strength) / 3f));
        }

        return newStats;
    }


    //public Dictionary<PlayerStat, int> GetRawStatsDictionary()
    //{
    //    Dictionary<PlayerStat, int> dict = new Dictionary<PlayerStat, int>();

    //    dict.Add(PlayerStat.Shooting, RawStats.Skills.Shooting);
    //    dict.Add(PlayerStat.Passing, RawStats.Skills.Passing);
    //    dict.Add(PlayerStat.Tackling, RawStats.Skills.Tackling);
    //    dict.Add(PlayerStat.Dribbling, RawStats.Skills.Dribbling);
    //    dict.Add(PlayerStat.Crossing, RawStats.Skills.Crossing);
    //    dict.Add(PlayerStat.Heading, RawStats.Skills.Heading);

    //    dict.Add(PlayerStat.Positioning, RawStats.Skills.Positioning);
    //    dict.Add(PlayerStat.Intelligence, RawStats.Skills.Intelligence);
    //    dict.Add(PlayerStat.Teamwork, RawStats.Skills.Teamwork);
    //    dict.Add(PlayerStat.Composure, RawStats.Skills.Composure);
    //    dict.Add(PlayerStat.Aggression, RawStats.Skills.Aggression);
    //    dict.Add(PlayerStat.Resilience, RawStats.Skills.Resilience);

    //    dict.Add(PlayerStat.Pace, RawStats.Skills.Pace);
    //    dict.Add(PlayerStat.Strength, RawStats.Skills.Strength);
    //    dict.Add(PlayerStat.Jumping, RawStats.Skills.Jumping);
    //    dict.Add(PlayerStat.Agility, RawStats.Skills.Agility);
    //    dict.Add(PlayerStat.Stamina, RawStats.Skills.Stamina);
    //    dict.Add(PlayerStat.Durability, RawStats.Skills.Durability);

    //    dict.Add(PlayerStat.Height, RawStats.Height);

    //    return dict;
    //}


    public static Stats PersonalityModifier(Stats player, PersonalityType personality)
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
                player.Creativity -= bigChange;
                return player;

            case PersonalityType.Cocky:
                player.Teamwork -= bigChange;
                player.Creativity += bigChange;
                return player;

            case PersonalityType.Driven:
                player.Teamwork += bigChange;
                return player;

            case PersonalityType.Kind:
                player.Aggression -= bigChange;
                return player;

            case PersonalityType.Lazy:
                player.Intelligence -= bigChange;
                return player;

            case PersonalityType.Shy:
                player.Teamwork -= bigChange;
                return player;

            case PersonalityType.Silly:
                player.Creativity += bigChange;
                return player;

            case PersonalityType.Smart:
                player.Intelligence += bigChange;
                return player;

            default:
                return player;
        }
    }

    public static Stats MoraleModifier(Stats player, int morale)
    {
        if (morale == 50) return player;

        int bigChange = (int)((morale / 100f - 0.5f) * 30f);
        int smallChange = (int)((morale / 100f - 0.5f) * 16f);

        player.Teamwork += bigChange;
        player.Aggression -= bigChange;
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

public static class PlayerExtensions
{
    public static Player.Stats AverageStats(this Player[] players){
        return AverageStats(players.ToList());
    }

    public static Player.Stats AverageStats(this List<Player> players)
    {
        Player.Stats avgStats = new Player.Stats
        {
            Skills = new int[Player.SKILL_NO],
            Positions = new Dictionary<Player.Position, Player.PositionStrength>(),
            Height = 0
        };

        int count = players.Count;
        if (count == 0) return avgStats;

        var avgSkills = new int[Player.SKILL_NO];
        var totalSkills = new int[Player.SKILL_NO];
        float totalHeight = 0f;

        for (int i = 0; i < count; i++)
        {
            var stats = players[i].GetStats();

            for (int j = 0; j < Player.SKILL_NO; j++)
            {
                totalSkills[j] += stats.Skills[j];
            }

            totalHeight += stats.Height;
        }

        for(int i = 0; i < Player.SKILL_NO; i++)
        {
            avgSkills[i] = totalSkills[i] / count;
        }

        avgStats.Height = Mathf.RoundToInt(totalHeight);
        avgStats.Skills = avgSkills;

        return avgStats;
    }
}