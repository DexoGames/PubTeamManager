using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.Events;

[System.Serializable]
public class IntEvent : UnityEvent<int> { }

public class TabController : UIObject
{
    [System.Serializable]
    public class Tab
    {
        public Button tabButton;
        public GameObject tabPanel;
    }

    [Header("Tabs List")]
    public List<Tab> tabs;

    [Header("Optional Highlight")]
    public Color normalColor = Color.white;
    public Color selectedColor = Color.green;

    private int currentIndex = 0;

    [HideInInspector]
    public IntEvent OnSwitch;

    public override void Setup()
    {
        OnSwitch.RemoveAllListeners();
        OnSwitch.AddListener(FindObjectOfType<TacticsPageUI>().SetSizes);

        for (int i = 0; i < tabs.Count; i++)
        {
            int index = i;
            tabs[i].tabButton.onClick.RemoveAllListeners();
            tabs[i].tabButton.onClick.AddListener(() => SwitchTab(index));
        }
        SwitchTab(currentIndex);
    }

    public void SwitchTab(int index)
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            bool isActive = i == index;
            tabs[i].tabPanel.SetActive(isActive);

            if (tabs[i].tabButton.TryGetComponent(out Image img))
            {
                img.color = isActive ? selectedColor : normalColor;
            }
        }

        currentIndex = index;
        OnSwitch?.Invoke(currentIndex);

        foreach(UIObject obj in GetComponentsInChildren<UIObject>())
        {
            if(obj != this)
                obj.Setup();
        }
    }
    public int GetCurrentIndex()
    {
        return currentIndex;
    }
}