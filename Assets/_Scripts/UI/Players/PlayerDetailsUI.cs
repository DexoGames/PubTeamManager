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
        teamName.text = player.Team != null ? LinkBuilder.BuildLink(player.Team) : "Free agent";
        height.gameObject.SetActive(true);
        statTitle.gameObject.SetActive(true);
        positionStrengths.gameObject.SetActive(player.Team != null);
        morale.gameObject.SetActive(true);
        personality.text = $"{player.Personality.ToString()}";
        height.text = $"{Player.CmToFeet(player.HeightToCm())}";

        bestPosition.text = "";
        foreach(Player.Position pos in player.BestPositions())
        {
            bestPosition.text += Player.LongPosition(pos) + ", ";
        }
        bestPosition.text = bestPosition.text.Remove(bestPosition.text.Length - 2);

        if (!player.IsAvailable) bestPosition.text += $" · {player.AvailabilityStatus}";
        PopulateStats(player);
        ShowChemistry(player);

        if (player.Team != null) positionStrengths.SetPositionStrengths(player, player.Team);
        playerEvents.ShowEvents(new() { player });
        morale.Setup(player);
    }
    protected override void OnShow(Manager manager)
    {
        base.OnShow();
        Game.ClearContainer(statsContainer);

        playerText.text = manager.FullName;
        teamName.text = $"Manages {LinkBuilder.BuildLink(manager.Team)}";
        positionStrengths.gameObject.SetActive(false);
        morale.gameObject.SetActive(false);
        playerEvents.ShowEvents(new List<Player>());
        height.gameObject.SetActive(false);
        statTitle.gameObject.SetActive(false);
        personality.text = $"Personality: {manager.Personality.ToString()}";

        bestPosition.text = "";
    }

    void ShowChemistry(Player player)
    {
        var position = player.GetPosition();
        if (!position.HasValue) return;

        var links = new List<string>();
        foreach (Player teammate in player.Team.StartingPlayers)
        {
            var other = teammate.GetPosition();
            if (player == teammate || !other.HasValue || !Chemistry.ArePositionsLinked(position.Value, other.Value)) continue;
            ChemistryLevel level = Chemistry.GetLevel(player, teammate);
            if (level != ChemistryLevel.Neutral) links.Add($"{teammate.Surname} ({Chemistry.Label(level)})");
        }
        if (links.Count > 0) bestPosition.text += "\nChemistry · " + string.Join(", ", links);
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
