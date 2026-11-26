using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class FormationDisplayModeControl : UIObject
{
    [SerializeField] TMP_Dropdown dropdown;
    [SerializeField] FormationUI formationUI;
    [SerializeField] GameObject[] otherContainers;

    public override void Setup()
    {
        SetDropdown();
        SetDisplayMode(dropdown.value);
    }

    public void SetDropdown()
    {
        dropdown.ClearOptions();
        dropdown.onValueChanged.RemoveAllListeners();
        dropdown.onValueChanged.AddListener(SetDisplayMode);

        List<string> options = new List<string>();
        foreach (DisplayMode mode in System.Enum.GetValues(typeof(DisplayMode)))
        {
            options.Add(mode.ToString());
        }
        dropdown.AddOptions(options);
    }

    public void SetDisplayMode(int index)
    {
        DisplayMode mode = (DisplayMode)index;
        formationUI.SetDisplayMode(mode);

        foreach (var obj in otherContainers)
        {
            foreach (PositionUI pos in obj.GetComponentsInChildren<PositionUI>())
            {
                pos.SetDisplayMode(mode);
            }
        }
    }

    public int GetCurrentDisplayModeIndex()
    {
        return dropdown.value;
    }
}