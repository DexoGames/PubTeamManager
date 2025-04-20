using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Linq;
using TMPro;
using UnityEngine;

public class BenchPositionUI : PositionUI
{
    BenchManager benchManager;
    Vector2 originalPosition;
    [SerializeField] TextMeshProUGUI otherPositions;

    public void BenchSetup(Player player, Formation.Position position, int id, FormationUI form, RectTransform container, BenchManager manager)
    {
        this.benchManager = manager;
        Setup(player, position, id, form, container);
    }

    public override void UpdateValues(Formation.Position position, Player player)
    {
        this.player = player;
        this.position.text = player.BestPosition().ToString();
        string[] list = player.ListBestPositions().Split(' ');

        string newString = "";
        for (int i = 1; i < list.Length; i++)
        {
            newString += list[i] + "  ";
            newString = newString.Substring(0, newString.Length - 2);
        }
        this.otherPositions.text = newString;

        //this.position.text = player.Team.Formation.Subposition(player.GetFormationIndex());
        morale.color = player.GetMoraleColor();
        rating.text = player.GetRating(player.BestPosition()).ToString();
        surname.text = LinkBuilder.BuildLink(player, player.Surname);
        name = "BenchPlayer " + player.FullName;
    }

    public override void Move(Formation.Position position)
    {
        
    }

    public void ReassignBench(Player player)
    {
        TweenTo(originalPosition);
        UpdateValues(new Formation.Position(), player);
    }
    public void SwapTo(Vector2 pos)
    {
        TweenTo(pos);
        UpdateValues(new Formation.Position(), player);
    }

    public override void OnPointerDown()
    {
        originalPosition = GetRect().anchoredPosition;
        benchManager.OnPointerDown(this);
    }

    public void BasePointerDown()
    {
        base.OnPointerDown();
    }

    public override void OnPointerUp()
    {
        benchManager.OnPointerUp(this);
    }

    public void BasePointerUp()
    {
        base.OnPointerUp();
    }

    public Vector2 GetOriginalPos()
    {
        return originalPosition;
    }
    public void SetOriginalPos()
    {
        originalPosition = rt.anchoredPosition;
    }
}
