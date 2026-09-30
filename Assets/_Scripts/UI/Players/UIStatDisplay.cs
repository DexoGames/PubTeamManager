using UnityEngine;

public class UIStatDisplay : MonoBehaviour
{
    public enum StatType
    {
        Position,
        Surname,
        CurrentRating,
        BestRating,
        PersonalityType,
        Age,
        KitNumber,
        Morale,
        Fatigue,
        Intelligence,
        OtherPositions // New type for the bench UI
    }

    public StatType statToDisplay;

    public static Sprite GetRatingSprite(Player.Rating rating)
    {
        switch (rating)
        {
            case Player.Rating.S:
                return Resources.Load<Sprite>("Art/Ratings/S");
            case Player.Rating.A:
                return Resources.Load<Sprite>("Art/Ratings/A");
            case Player.Rating.B:
                return Resources.Load<Sprite>("Art/Ratings/B");
            case Player.Rating.C:
                return Resources.Load<Sprite>("Art/Ratings/C");
            case Player.Rating.D:
                return Resources.Load<Sprite>("Art/Ratings/D");
            case Person.Rating.E:
                return Resources.Load<Sprite>("Art/Ratings/E");
            case Person.Rating.F:
                return Resources.Load<Sprite>("Art/Ratings/F");
        }
        return null;
    }
}