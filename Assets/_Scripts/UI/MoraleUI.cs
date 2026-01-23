using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MoraleUI : MonoBehaviour
{
    Person person;
    [SerializeField] Image face;
    [SerializeField] TextMeshProUGUI description;

    public void Setup(Person p)
    {
        person = p;
        UpdateFace();
        UpdateText();
    }

    void UpdateFace()
    {
        face.color = person.GetMoraleColor();
        face.sprite = person.GetMoraleSprite();
    }

    void UpdateText()
    {
        description.text = DecideText(person);
    }

    public static string DecideText(Person person)
    {
        Vector2 disp = person.Morale.DisplacementToIdeal();
        Debug.Log("" + disp.x + " " + disp.y);
        
        if(person.Morale.DistanceToIdeal() < 10) return "Thriving";

        if(disp.x > 0 && disp.y > 0) return "Overexcited";
        if(disp.x > 0 && disp.y < 0) return "Too Relaxed";
        if(disp.x < 0 && disp.y > 0) return "Too Angry";
        if(disp.x < 0 && disp.y < 0) return "Too Sad";

        return "Uhhhhh";
    }
}