using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public struct Morale
{
    private int mood;
    private int passion;

    public readonly int IdealMood;
    public readonly int IdealPassion;

    public int Mood
    {
        get => mood;
        set => mood = Clamp(value, 0, 100);
    }

    public int Passion
    {
        get => passion;
        set => passion = Clamp(value, 0, 100);
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    public Morale(int mood, int passion, Person.PersonalityType personality)
    {
        (IdealMood, IdealPassion) = Person.IdealMorale(personality);
        IdealMood += UnityEngine.Random.Range(-5, 6);
        IdealPassion += UnityEngine.Random.Range(-5, 6);
        IdealMood = Mathf.Clamp(IdealMood, 0, 100);
        IdealPassion = Mathf.Clamp(IdealPassion, 0, 100);

        this.mood = 50;
        this.passion = 50;

        Mood = mood;
        Passion = passion;
    }

    public float DistanceToIdeal()
    {
        return Vector2.Distance(new Vector2(Mood, Passion), new Vector2(IdealMood, IdealPassion));
    }
    public Vector2 DisplacementToIdeal()
    {
        return new Vector2(Mood - IdealMood, Passion - IdealPassion);
    }
}


[System.Serializable]
public class Person
{
    public int PersonID;
    public string FirstName, Surname;
    public string FullName => string.Concat(FirstName, " ", Surname);

    public Team Team;

    [Serializable]
    public enum PersonalityType
    {
        Aggressive, Calm, Cautious, Cocky, Driven, Kind, Lazy, Shy, Silly, Smart
    }

    public enum Rating
    {
        F, E, D, C, B, A, S
    }

    public Color GetMoraleColor()
    {
        return GetColor(Morale);
    }

    public static Color GetColor(Morale morale)
    {
        const float BestDistanceThreshold = 3f;
        const float WorstDistanceThreshold = 100f;

        float distance = morale.DistanceToIdeal();

        if (distance <= BestDistanceThreshold)
            return Game.Gradient(MoraleColors(), 1f);

        distance = Mathf.Clamp(distance, BestDistanceThreshold, WorstDistanceThreshold);

        float t = 1f - ((distance - BestDistanceThreshold) /
                       (WorstDistanceThreshold - BestDistanceThreshold));

        return Game.Gradient(MoraleColors(), t);
    }


    public static Color[] MoraleColors()
    {
        return new Color[] { new Color(0.8f, 0.1f, 0f), new Color(1f, 0.5f, 0f), Color.yellow, new Color(0.5f, 0.9f, 0.5f), new Color(0.05f, 0.7f, 0.1f) };
    }

    public Sprite GetMoraleSprite()
    {
        if (Morale.Mood >= 42 && Morale.Mood <= 58 &&
            Morale.Passion >= 42 && Morale.Passion <= 58)
        {
            return Resources.Load<Sprite>("Art/Morale/Neutral");
        }

        if (Morale.Mood >= 50 && Morale.Passion >= 50)
        {
            if(Morale.Passion >=75) return Resources.Load<Sprite>("Art/Morale/Overjoyed");

            return Resources.Load<Sprite>("Art/Morale/Happy");
        }
        else if (Morale.Mood >= 50 && Morale.Passion < 50)
        {
            if(Morale.Mood >= 75) return Resources.Load<Sprite>("Art/Morale/Positive");

            return Resources.Load<Sprite>("Art/Morale/Content");
        }
        else if (Morale.Mood < 50 && Morale.Passion >= 50)
        {
            if(Morale.Passion >= 75 || Morale.Mood <= 10) return Resources.Load<Sprite>("Art/Morale/Angry");

            return Resources.Load<Sprite>("Art/Morale/Annoyed");
        }
        else
        {
            if(Morale.Mood <= 25) return Resources.Load<Sprite>("Art/Morale/Upset");

            return Resources.Load<Sprite>("Art/Morale/Sad");
        }
    }


    public PersonalityType Personality;
    public DateTime DateOfBirth;
    public Morale Morale;

    public int AgeDays() { return (CalenderManager.Instance.CurrentDay - DateOfBirth).Days; }
    public int AgeYears() { return (int)((CalenderManager.Instance.CurrentDay - DateOfBirth).Days / 365.25f); }

    public int RatingOffset { get; private set; }


    public string GenerateFirstName()
    {
        string[] names = new string[] { "James", "Oliver", "Ethan", "Liam", "Alexander", "Noah", "Lucas", "Mason", "Logan", "Samuel", "Daniel", "Michael", "Dexter", "Rory", "Callum", "Gerald", "Javier", "Yash", "Indigo", "Ollie",
            "Ivo", "Alan", "Joe", "Kirk", "Cole", "Ivan", "Son", "Josh", "Walter", "Mike", "Jesse", "Gus", "Hank", "Hikmat", "Alex", "Max", "Lewis", "Charles", "Sergio", "David", "Luke", "Mark", "Jeremy", "Alex", "Brent", "Magnus",
            "James", "Will", "Ben", "Zayn", "Albert", "Jack", "Tom", "Seb", "Sebby", "Zac", "Ed", "Fred", "Freddie", "Brian", "Troy", "Mario", "Luigi", "Karl", "Darwin", "Tim", "John", "Tyler", "Harry", "Victor", "Jayce", "Ange",
            "Cody", "Mal", "Billy", "Cole", "Ivan", "Kale", "Adrian", "Dylan", "Nathdaniel", "Oran", "Nat", "Rono", "Bradley", "Rene", "Aidan", "Adam", "Indy", "Gregory", "Greg", "Erc", "Robert", "Sean", "Hugh", "Russell"};
        var rnd = UnityEngine.Random.Range(0, names.Length);

        return names[rnd];
    }

    public string GenerateSurname()
    {
        string[] names = new string[] { "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez", "Hernandez", "Lopez", "Chapman", "Fitzgerald", "Carlos", "Mehta", "Ronald",
            "Wolf", "Garraway", "Rooney", "McDonald", "McQueen", "Wilson", "Davies", "Robinson", "Watterson", "White", "Clark", "Patel", "Jackson", "Wilcox", "Meldrum", "Fring", "Pinkman", "Sainsbury", "Tesco", "Morrison",
            "Schrader", "Cum", "Black", "John", "Harrison", "Lennon", "McCartney" ,"Starkey", "Preston", "Martin", "Perez", "Hamilton", "Russell", "Norris", "Piastri", "Goldbridge", "Maguire", "Binotto", "Walker", "McCoy",
            "Marriott", "Doyle", "Kite", "Rigby", "Pepper", "Mustard", "Green", "Nicolle", "Hudson", "Dover", "Malik", "Halsey", "Black", "Carpenter", "Simons", "Manifold", "Marsh", "Guetta", "Sheeran", "Perry", "Swift", 
            "Miranda", "Burr", "Schuyler", "Groff", "Jefferson", "Wecht", "Bellingham", "Gallagher", "Bolton", "Montez", "Evans", "Mario", "Mangione", "Marx", "Tralala", "Gusini", "Pork", "Cheese", "Angrave", "Thompson",
            "Wood", "Mainoo", "Shaw", "Trundle", "Lukather", "Jota", "Shears", "Eastman", "Ashdown", "Sterry", "Hart", "Marlene", "Marsden", "Rainsford", "Bending", "Dylan", "Tye", "Flowers", "Ronald", "Hornsey", "Crisp",
            "Withell", "Leung", "Barrie", "Ellwood", "Keating", "Manley", "Gouldstone", "Caldow", "Bolt", "Pise", "Price", "Borman", "Spencer", "Sunderland", "Heaton", "Morgan", "Edge-Morgan", "Maldonado", "Trifinov", "Reeves",
            "Aston", "Cox", "Nolan", "House", "Cuddy", "Cameron", "Chase", "Foreman", "Leonard", "Laurie", "Tritter", "Acaster", "Mitchell", "Mack", "Atkinson", "Howard", "Pegg", "Moore", "Robson", "Stones", "Henderson", "Saka",
            "Pearce", "Butcher", "Keegan"};
        var rnd = UnityEngine.Random.Range(0, names.Length);

        return names[rnd];
    }

    public static DateTime GetRandomDate(DateTime startDate, DateTime endDate)
    {
        int range = (endDate - startDate).Days;

        int randomDays = UnityEngine.Random.Range(0, range);

        DateTime randomDate = startDate.AddDays(randomDays);

        return new DateTime(randomDate.Year, randomDate.Month, randomDate.Day, 0, 0, 0, 0);
    }

    public Person GeneratePerson()
    {
        PersonID = PersonManager.Instance.RegisterPerson(this);
        FirstName = GenerateFirstName();
        Surname = GenerateSurname();

        DateOfBirth = GetRandomDate(new DateTime(1982, 1, 1), new DateTime(2007, 1, 1));

        Array values = Enum.GetValues(typeof(PersonalityType));
        Personality = (PersonalityType)values.GetValue(UnityEngine.Random.Range(0, values.Length));

        RatingOffset = UnityEngine.Random.Range(-10, 11);

        Morale = new Morale(UnityEngine.Random.Range(25, 76), UnityEngine.Random.Range(20, 61), Personality);

        //Debug.Log($"{FullName} is {AgeYears()} years old.");

        return this;
    }

    public (int, int) NewMorale(int originalMoraleChange, Event.Reaction reactionEnum, EventType.Severity severityEnum)
    {
        int reaction = (int)reactionEnum - (int)Event.Reaction.Neutral;
        int severity = (int)severityEnum - (int)EventType.Severity.Irrelevant;

        int moodChange = (int)(reaction * (Mathf.Abs(severity)+1) * 1.5f);

        int passionChange = ( (int)Mathf.Pow(reaction, 2) - 4 ) * (Mathf.Abs(severity)+1);

        Debug.Log($"Mood was {Morale.Mood}, changed by {moodChange}");
        Debug.Log($"Passion was {Morale.Passion}, changed by {passionChange}");

        Morale.Mood += moodChange;
        Morale.Passion += passionChange;

        return (moodChange, passionChange);
    }

    public static (int, int) IdealMorale(PersonalityType personality)
    {
        switch (personality)
        {
            case PersonalityType.Aggressive:
                return (25, 95);
            case PersonalityType.Calm:
                return (90, 5);
            case PersonalityType.Cautious:
                return (75, 25);
            case PersonalityType.Cocky:
                return (95, 80);
            case PersonalityType.Driven:
                return (80, 80);
            case PersonalityType.Kind:
                return (100, 50);
            case PersonalityType.Lazy:
                return (25, 95);
            case PersonalityType.Shy:
                return (100, 70);
            case PersonalityType.Silly:
                return (60, 60);
            case PersonalityType.Smart:
                return (70, 80);
        }

        return (75, 75);
    }

    /// <summary>
    /// Returns how well this personality type gets along with another.
    /// Range: -2 (clash) to +2 (great chemistry)
    /// </summary>
    public static int GetPersonalityCompatibility(PersonalityType self, PersonalityType other)
    {
        // Same personality - depends on the type
        if (self == other)
        {
            return self switch
            {
                PersonalityType.Cocky => -1,      // Two cocky people clash
                PersonalityType.Aggressive => -1, // Two aggressive people clash
                PersonalityType.Kind => 2,        // Kind people get along great
                PersonalityType.Calm => 1,        // Calm people are fine together
                PersonalityType.Driven => 2,      // Driven people motivate each other
                _ => 0                            // Neutral for others
            };
        }

        // Define specific compatibility pairs
        return (self, other) switch
        {
            // Aggressive
            (PersonalityType.Aggressive, PersonalityType.Calm) => 1,      // Calm balances Aggressive
            (PersonalityType.Aggressive, PersonalityType.Kind) => 1,      // Kind calms Aggressive
            (PersonalityType.Aggressive, PersonalityType.Cocky) => -2,    // Both volatile
            (PersonalityType.Aggressive, PersonalityType.Shy) => -1,      // Intimidates Shy

            // Calm
            (PersonalityType.Calm, PersonalityType.Aggressive) => 1,
            (PersonalityType.Calm, PersonalityType.Driven) => 1,          // Good balance
            (PersonalityType.Calm, PersonalityType.Silly) => 0,           // Tolerates Silly
            (PersonalityType.Calm, PersonalityType.Lazy) => -1,           // Frustrated by Lazy

            // Cautious
            (PersonalityType.Cautious, PersonalityType.Smart) => 2,       // Both think ahead
            (PersonalityType.Cautious, PersonalityType.Silly) => -1,      // Silly is unpredictable
            (PersonalityType.Cautious, PersonalityType.Cocky) => -1,      // Cocky is reckless
            (PersonalityType.Cautious, PersonalityType.Driven) => 1,      // Respects dedication

            // Cocky
            (PersonalityType.Cocky, PersonalityType.Aggressive) => -2,
            (PersonalityType.Cocky, PersonalityType.Shy) => -1,           // Dismisses Shy
            (PersonalityType.Cocky, PersonalityType.Kind) => 0,           // Kind tolerates
            (PersonalityType.Cocky, PersonalityType.Driven) => 1,         // Respects ambition

            // Driven
            (PersonalityType.Driven, PersonalityType.Lazy) => -2,         // Opposite values
            (PersonalityType.Driven, PersonalityType.Cocky) => 1,
            (PersonalityType.Driven, PersonalityType.Smart) => 2,         // Great combo
            (PersonalityType.Driven, PersonalityType.Calm) => 1,

            // Kind
            (PersonalityType.Kind, PersonalityType.Shy) => 2,             // Makes Shy comfortable
            (PersonalityType.Kind, PersonalityType.Aggressive) => 1,
            (PersonalityType.Kind, PersonalityType.Silly) => 1,           // Enjoys their humor
            (PersonalityType.Kind, PersonalityType.Lazy) => 0,            // Too nice to judge

            // Lazy
            (PersonalityType.Lazy, PersonalityType.Driven) => -2,
            (PersonalityType.Lazy, PersonalityType.Silly) => 1,           // Both relaxed
            (PersonalityType.Lazy, PersonalityType.Calm) => 1,            // No pressure
            (PersonalityType.Lazy, PersonalityType.Smart) => -1,          // Smart gets frustrated

            // Shy
            (PersonalityType.Shy, PersonalityType.Kind) => 2,
            (PersonalityType.Shy, PersonalityType.Calm) => 1,             // Non-threatening
            (PersonalityType.Shy, PersonalityType.Aggressive) => -1,
            (PersonalityType.Shy, PersonalityType.Cocky) => -1,

            // Silly
            (PersonalityType.Silly, PersonalityType.Kind) => 1,
            (PersonalityType.Silly, PersonalityType.Lazy) => 1,
            (PersonalityType.Silly, PersonalityType.Smart) => 0,          // Smart tolerates
            (PersonalityType.Silly, PersonalityType.Cautious) => -1,

            // Smart
            (PersonalityType.Smart, PersonalityType.Cautious) => 2,
            (PersonalityType.Smart, PersonalityType.Driven) => 2,
            (PersonalityType.Smart, PersonalityType.Lazy) => -1,
            (PersonalityType.Smart, PersonalityType.Silly) => 0,

            // Default - neutral
            _ => 0
        };
    }

    /// <summary>
    /// Get the personality type this person gets along with best.
    /// </summary>
    public PersonalityType GetBestCompatiblePersonality()
    {
        PersonalityType best = PersonalityType.Calm;
        int bestScore = int.MinValue;

        foreach (PersonalityType other in Enum.GetValues(typeof(PersonalityType)))
        {
            int score = GetPersonalityCompatibility(Personality, other);
            if (score > bestScore)
            {
                bestScore = score;
                best = other;
            }
        }
        return best;
    }

    /// <summary>
    /// Get the personality type this person clashes with most.
    /// </summary>
    public PersonalityType GetWorstCompatiblePersonality()
    {
        PersonalityType worst = PersonalityType.Calm;
        int worstScore = int.MaxValue;

        foreach (PersonalityType other in Enum.GetValues(typeof(PersonalityType)))
        {
            int score = GetPersonalityCompatibility(Personality, other);
            if (score < worstScore)
            {
                worstScore = score;
                worst = other;
            }
        }
        return worst;
    }
}