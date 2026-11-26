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

    [SerializeField] UITheme theme;

    [Header("Fallback Colors")]
    public Color normalColor = Color.white;
    public Color selectedColor = Color.green;

    int currentIndex;

    [HideInInspector]
    public IntEvent OnSwitch;

    public override void Setup()
    {
        OnSwitch.RemoveAllListeners();
        OnSwitch.AddListener(TacticsPageUI.Instance.SetSizes);

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
        Color normal = theme != null ? theme.GetColor(UITheme.Role.Oak) : normalColor;
        Color selected = theme != null ? theme.GetColor(UITheme.Role.Felt) : selectedColor;

        for (int i = 0; i < tabs.Count; i++)
        {
            bool isActive = i == index;
            tabs[i].tabPanel.SetActive(isActive);

            if (tabs[i].tabButton.TryGetComponent(out Image image)) image.color = isActive ? selected : normal;
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