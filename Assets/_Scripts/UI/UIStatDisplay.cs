using UnityEngine;

public class UIStatDisplay : MonoBehaviour
{
    public enum StatType
    {
        Position,
        Surname,
        Rating,
        PersonalityType,
        Age,
        KitNumber,
        Morale,
        Fatigue,
        Intelligence,
        OtherPositions // New type for the bench UI
    }

    public StatType statToDisplay;
}