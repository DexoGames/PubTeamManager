using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class CalenderManager : MonoBehaviour
{
    public static CalenderManager Instance { get; private set; }

    int advanceListeners;
    int advanceResponses;

    public void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
        }

        CurrentDay = new DateTime(2024, 8, 1);
        dateText.text = CurrentDay.Date.ToShortDateString();
    }


    public DateTime CurrentDay { get; private set; }

    [SerializeField] TextMeshProUGUI dateText;

    public UnityEvent<DateTime> NewDay;

    public static string ShortDate(DateTime dateTime)
    {
        string shortened = dateTime.Date.ToShortDateString();
        return shortened.Remove(6, 2);
    }
    public static string ShortDateNoYear(DateTime dateTime)
    {
        string shortened = dateTime.Date.ToShortDateString();
        return shortened.Remove(5, 5);
    }
    public static string ShortDateWordsNoYear(DateTime dateTime)
    {
        string daySuffix = GetDaySuffix(dateTime.Day);
        return $"{dateTime.Day}{daySuffix} {dateTime.ToString("MMM")}";
    }

    private static string GetDaySuffix(int day)
    {
        if (day >= 11 && day <= 13) return "th";

        int ending = day % 10;

        return ending switch
        {
            1 => "st",
            2 => "nd",
            3 => "rd",
            _ => "th"
        };
    }

    public string DaysAgo(DateTime date)
    {
        int days = (CurrentDay - date).Days;
        if(days == 0)
        {
            return "Today";
        }
        else if(days == 1)
        {
            return "Yesterday";
        }
        else if(days > 0 && days < 10)
        {
            return days + " days ago";
        }
        else
        {
            return ShortDateWordsNoYear(date);
        }
    }

    public void AdvanceDay()
    {
        if (advanceResponses > 0) return;

        CurrentDay = CurrentDay.AddDays(1);
        dateText.text = CurrentDay.Date.ToShortDateString();
        advanceResponses = advanceListeners;
        NewDay.Invoke(CurrentDay);

        if(GameManager.Instance.PlayerMatchSim != null)
        {
            GameManager.Instance.PlayerMatchSim.Invoke();
            GameManager.Instance.PlayerMatchSim = null;
        }
        else
        {
            UIManager.Instance.ShowHomePage();
        }
    }

    public UnityAction ShowNextPage;

    public void ConfirmAddedListener()
    {
        advanceListeners++;
    }
    public void ConfirmRemoveListener()
    {
        advanceListeners--;
    }


    public void RespondToAdvance()
    {
        advanceResponses--;
    }
}
