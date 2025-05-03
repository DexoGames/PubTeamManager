using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum Card
{
    None, Yellow, Red, RedAndSuspension
}
public enum InjuryType
{
    None, Knock, Standard, Hamstring, ACL, Death
}

public enum Half
{
    First, Second, ExtraTime
}

public struct Minute : IComparable<Minute>, IEquatable<Minute>
{
    public int Base { get; private set;}
    public int Stoppage { get; private set;}

    public Minute(int baseMinute, int stoppageTime = 0)
    {
        Base = baseMinute;
        Stoppage = stoppageTime;
    }

    public void Next()
    {
        if(Base == 45 || Base == 90 || Base == 120)
        {
            Stoppage++;
        }
        else{
            Base++;
        }
        
    }

    public int TotalMinutes => Base + Stoppage;

    public bool IsInStoppageTime => Stoppage > 0;

    public override string ToString()
    {
        return IsInStoppageTime ? $"{Base}'+{Stoppage}" : $"{Base}'";
    }

    public int CompareTo(Minute other)
    {
        int baseComparison = Base.CompareTo(other.Base);
        if (baseComparison != 0)
            return baseComparison;

        return Stoppage.CompareTo(other.Stoppage);
    }


    public bool Equals(Minute other)
    {
        return Base == other.Base && Stoppage == other.Stoppage;
    }

    public override bool Equals(object obj)
    {
        return obj is Minute other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Base, Stoppage);
    }

    public static bool operator ==(Minute left, Minute right) => left.Equals(right);
    public static bool operator !=(Minute left, Minute right) => !(left == right);
    public static bool operator <(Minute left, Minute right) => left.CompareTo(right) < 0;
    public static bool operator >(Minute left, Minute right) => left.CompareTo(right) > 0;
}

public class Game : MonoBehaviour
{
    [System.Serializable]
    public struct Score
    {
        public int home;
        public int away;

        public Score(int h, int a)
        {
            home = h;
            away = a;
        }
    }

    public static void ClearContainer(Transform container)
    {
        foreach (Transform t in container)
        {
            Destroy(t.gameObject);
        }
    }

    public static float Average(params float[] numbers)
    {
        if (numbers.Length == 0)
            return 0f; // Return 0 if no numbers are provided

        float sum = 0f;

        foreach (float number in numbers)
        {
            sum += number;
        }

        return sum / numbers.Length;
    }

    public static float WeightedAverage(params (float number, float weight)[] numberWeightPairs)
    {
        if (numberWeightPairs.Length == 0)
            return 0f; // Return 0 if no pairs are provided

        float weightedSum = 0f;
        float totalWeight = 0f;

        foreach (var pair in numberWeightPairs)
        {
            weightedSum += pair.number * pair.weight;
            totalWeight += pair.weight;
        }

        if (totalWeight == 0f)
            return 0f; // Avoid division by zero, return 0 if all weights are zero

        return weightedSum / totalWeight;
    }

    public static T RandomEnumValue<T>() where T : Enum
    {
        Array values = Enum.GetValues(typeof(T));
        return (T)values.GetValue(UnityEngine.Random.Range(0, values.Length));
    }
    public static T GetEnumValue<T>(int i) where T : Enum
    {
        Array values = Enum.GetValues(typeof(T));
        return (T)values.GetValue(i);
    }
    public static int GetEnumLength<T>() where T : Enum
    {
        Array values = Enum.GetValues(typeof(T));
        return values.Length;
    }

    public static void Shuffle<T>(T[] array)
    {
        System.Random rng = new System.Random(); // Random instance created inside the method
        int n = array.Length;
        for (int i = n - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }
    }

    public static Color Gradient(Color[] colorArray, float t)
    {
        if (colorArray == null || colorArray.Length == 0)
            return Color.white; // Default fallback color

        if (colorArray.Length == 1)
            return colorArray[0];

        // Scale t to the range of the color array
        float scaledT = Mathf.Clamp01(t) * (colorArray.Length - 1);
        int indexA = Mathf.FloorToInt(scaledT);
        int indexB = Mathf.Clamp(indexA + 1, 0, colorArray.Length - 1);
        float localT = scaledT - indexA;

        return Color.Lerp(colorArray[indexA], colorArray[indexB], localT);
    }
}