using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public abstract class UIPage : MonoBehaviour
{
    public RectTransform Elements => transform.GetChild(0).GetComponent<RectTransform>();

    public void Show()
    {
        DisplayUI();
        OnShow();
    }
    public void Show(Player player)
    {
        DisplayUI();
        OnShow(player);
    }
    public void Show(Manager manager)
    {
        DisplayUI();
        OnShow(manager);
    }
    public void Show(Team team)
    {
        DisplayUI();
        OnShow(team);
    }
    public void Show(Fixture fixture)
    {
        DisplayUI();
        OnShow(fixture);
    }
    public void Show(Event @event, Person person)
    {
        DisplayUI();
        OnShow(@event, person);
    }

    protected virtual void OnShow() { }
    protected virtual void OnShow(Player player) { }
    protected virtual void OnShow(Manager manager) { }
    protected virtual void OnShow(Team team) { }
    protected virtual void OnShow(Fixture fixture) { }
    protected virtual void OnShow(Event @event, Person person) { }


    public void DisplayUI()
    {
        Elements.gameObject.SetActive(true);
    }

    public void Hide()
    {
        Elements.gameObject.SetActive(false);
    }

}