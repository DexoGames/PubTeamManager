using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;

public class PlayerDetailsUI : UIPage
{
    public static PlayerDetailsUI Instance { get; private set; }

    [SerializeField] TextMeshProUGUI playerText, teamName, height, personality, bestPosition;
    [SerializeField] Transform statsContainer;
    [SerializeField] StatUI statPrefab;
    [SerializeField] PositionStrengthUI positionStrengths;
    [SerializeField] PlayerEventsUI playerEvents;


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
    }

    protected override void OnShow(Player player)
    {
        base.OnShow();
        Game.ClearContainer(statsContainer);

        playerText.text = player.FullName;
        teamName.text = $"Plays For {LinkBuilder.BuildLink(player.Team)}";
        personality.text = $"{player.Personality.ToString()}";
        height.text = $"{Player.CmToFeet(player.HeightToCm())}";

        bestPosition.text = "";
        foreach(Player.Position pos in player.BestPositions())
        {
            bestPosition.text += Player.LongPosition(pos) + ", ";
        }
        bestPosition.text = bestPosition.text.Remove(bestPosition.text.Length - 2);

        PopulateStats(player);

        positionStrengths.SetPositionStrengths(player, player.Team);
        playerEvents.ShowEvents(player);
    }
    protected override void OnShow(Manager manager)
    {
        base.OnShow();
        Game.ClearContainer(statsContainer);

        playerText.text = manager.FullName;
        teamName.text = $"Manages {LinkBuilder.BuildLink(manager.Team)}";
        personality.text = $"Personality: {manager.Personality.ToString()}";

        bestPosition.text = "";
    }

    void PopulateStats(Player player)
    {
        for(int i = 0; i < Player.SKILL_NO; i++)
        {
            int value = player.RawStats.Skills[i];
            string stat = ((PlayerStat)i).ToString();

            Instantiate(statPrefab, statsContainer).SetText(stat, value);
        }
    }
}