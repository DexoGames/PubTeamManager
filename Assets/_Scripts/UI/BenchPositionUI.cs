using UnityEngine;
using static UIStatDisplay;

public class BenchPositionUI : PositionUI
{
    private BenchManager benchManager;
    private Vector2 originalPosition;

    public void BenchSetup(Player player, Formation.Position position, int id, FormationUI form, RectTransform container, BenchManager manager)
    {
        this.benchManager = manager;
        Setup(player, position, id, form, container);
    }

    public override void UpdateValues(Formation.Position position, Player player)
    {
        this.player = player;
        name = "BenchPlayer " + player.FullName;

        UpdateTextStat(StatType.Position, player.BestPosition().ToString());
        UpdateTextStat(StatType.Surname, LinkBuilder.BuildLink(player, player.Surname));
        UpdateTextStat(StatType.Rating, player.GetRating(player.BestPosition()).ToString());
        UpdateImageColor(StatType.Morale, player.GetMoraleColor());

        string[] list = player.ListBestPositions().Split(' ');
        string newString = "";
        for (int i = 1; i < list.Length; i++)
        {
            if (!string.IsNullOrEmpty(list[i]))
            {
                newString += list[i] + " ";
            }
        }
        newString = newString.Trim();

        UpdateTextStat(StatType.OtherPositions, newString);
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