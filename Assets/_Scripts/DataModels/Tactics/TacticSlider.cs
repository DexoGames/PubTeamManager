using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Tactic Slider")]
public class TacticSlider : LegacyTacticElement
{
    int _value;
    public int Value
    {
        get => _value;
        set => _value = Mathf.Clamp(value, -2, 2);
    }
}
