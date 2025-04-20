using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    public static Color GetColor(int morale)
    {
        float t = morale / 100f;

        Color[] colorArray = { new Color(0.8f, 0.1f, 0f), new Color(1f, 0.5f, 0f), Color.yellow, new Color(0.5f, 0.9f, 0.5f), new Color(0.05f, 0.7f, 0.1f) };

        return Game.Gradient(colorArray, t);
    }

    public PersonalityType Personality;

    public DateTime DateOfBirth;
    public int Morale => Mathf.Clamp(clampedMorale, 0, 100);
    private int clampedMorale;
    public void ChangeMorale(int i)
    {
        clampedMorale = Mathf.Clamp(clampedMorale + i, 0, 100);
    }
    public void SetMorale(int i)
    {
        clampedMorale = Mathf.Clamp(i, 0, 100);
    }
    public int AgeDays() { return (CalenderManager.Instance.CurrentDay - DateOfBirth).Days; }
    public int AgeYears() { return (int)((CalenderManager.Instance.CurrentDay - DateOfBirth).Days / 365.25f); }

    public int RatingOffset { get; private set; }


    public string GenerateFirstName()
    {
        string[] names = new string[] { "James", "Oliver", "Ethan", "Liam", "Alexander", "Noah", "Lucas", "Mason", "Logan", "Samuel", "Daniel", "Michael", "Dexter", "Rory", "Callum", "Gerald", "Javier", "Yash", "Indigo", "Ollie",
            "Ivo", "Alan", "Joe", "Kirk", "Cole", "Ivan", "Son", "Josh", "Walter", "Mike", "Jesse", "Gus", "Hank", "Hikmat", "Alex", "Max", "Lewis", "Charles", "Sergio", "David", "Luke", "Mark", "Jeremy", "Alex", "Brent", "Magnus",
            "James", "Will", "Ben", "Zayn", "Albert", "Jack", "Tom", "Seb", "Sebby", "Zac", "Ed", "Fred", "Freddie", "Brian", "Troy", "Mario", "Luigi", "Karl", "Darwin", "Tim", "John", "Tyler", "Harry", "Victor", "Jayce", "Ange"};
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
            "Wood", "Mainoo", "Shaw", "Trundle", "Lukather"};
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

        ChangeMorale(UnityEngine.Random.Range(0, 100));

        //Debug.Log($"{FullName} is {AgeYears()} years old.");

        return this;
    }

    public int NewMorale(int originalMoraleChange, Event.Reaction reaction, EventType.Severity severity)
    {
        int newMorale = Morale + (int)(((int)reaction - 3) * (Mathf.Abs((int)severity - 3)));

        Debug.Log($"After talking, morale went from {Morale} to {newMorale}");

        SetMorale(newMorale);

        return newMorale;
    }
}