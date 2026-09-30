using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

public class PlayerDetailsUI : UIPage
{
    public static PlayerDetailsUI Instance { get; private set; }

    [SerializeField] TextMeshProUGUI playerText, teamName, height, personality, bestPosition, statTitle;
    [SerializeField] Transform statsContainer;
    [SerializeField] StatUI statPrefab;
    [SerializeField] PositionStrengthUI positionStrengths;
    [SerializeField] PlayerEventsUI playerEvents;
    [SerializeField] MoraleUI morale;


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
        playerEvents.ShowEvents(new() { player });
        morale.Setup(player);
    }
    protected override void OnShow(Manager manager)
    {
        base.OnShow();
        Game.ClearContainer(statsContainer);

        playerText.text = manager.FullName;
        teamName.text = $"Manages {LinkBuilder.BuildLink(manager.Team)}";
        height.gameObject.SetActive(false);
        statTitle.gameObject.SetActive(false);
        personality.text = $"Personality: {manager.Personality.ToString()}";

        bestPosition.text = "";
    }

    void PopulateStats(Player player)
    {
        Player.Stats stats = player.GetStats();
        foreach (PlayerStat stat in Player.SkillDisplayOrder)
        {
            Instantiate(statPrefab, statsContainer).SetText(stat.ToString(), stats.GetStat(stat));
        }
    }
}
