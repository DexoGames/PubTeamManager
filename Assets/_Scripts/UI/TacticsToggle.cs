using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TacticsToggle : Toggle
{
    // Start is called before the first frame update
    new void Start()
    {
        base.Start();
        this.onValueChanged.AddListener(SetOnOff);
    }

    public void Set(bool newState)
    {
        this.isOn = newState;
    }

    void SetOnOff(bool state)
    {
        if (state)
        {
            image.color = Color.white;
        }
        else
        {
            image.color = Color.gray;
        }
    }
}
