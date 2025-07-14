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

    public override void Move(Formation.Position position) { }

    protected override void UpdatePosition(Formation.Position position)
    {
        var bestPosition = player.BestPosition();

        UpdateTextStat(StatType.Position, bestPosition.ToString(), null);
    }

    protected override void UpdateCurrentRating(Player player)
    {
        var rating = player.GetRating(player.BestPosition());

        UpdateImage(StatType.CurrentRating, null, GetRatingSprite(rating));
    }

    protected override void UpdateOtherPositions(Player player, bool includeBest = false)
    {
        base.UpdateOtherPositions(player, false);
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
        originalPosition = rt.anchoredPosition;
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