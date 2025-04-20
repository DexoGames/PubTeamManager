using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BenchManager : MonoBehaviour
{
    FormationInteractableUI formation;
    List<PositionUI> allBenchPositions = new List<PositionUI>();
    [SerializeField] BenchPositionUI benchPositionPrefab;
    BenchPositionUI draggedPLayer;
    [SerializeField] GridLayoutGroup grid;
    [SerializeField] RectTransform container;
    Team team;

    public void Setup(FormationInteractableUI _formation, Team team)
    {
        formation = _formation;
        this.team = team;
        draggedPLayer = null;

        PopulateTeamUI(team.Players);
    }

    public void OnPointerUp(BenchPositionUI sender)
    {
        if(sender == draggedPLayer)
        {
            draggedPLayer = null;
            sender.BasePointerUp();
            SetGrid(true);
        }
    }

    public void OnPointerDown(BenchPositionUI sender)
    {
        if(draggedPLayer == null)
        {
            draggedPLayer = sender;

            foreach(BenchPositionUI bench in allBenchPositions)
            {
                if (bench != sender) bench.SetOriginalPos(); 
            }

            SetGrid(false);

            sender.BasePointerDown();
        }
    }

    public void PopulateTeamUI(List<Player> players)
    {
        allBenchPositions.Clear();
        foreach (Player p in players.GetRange(11, players.Count - 11))
        {
            BenchPositionUI newPlayerUI = Instantiate(benchPositionPrefab, container);
            newPlayerUI.BenchSetup(p, new Formation.Position { Location = Vector2Int.down }, p.GetTeamIndex(), formation, container, this);
            newPlayerUI.SetupInteractable();
            allBenchPositions.Add(newPlayerUI);
        }
    }

    public void ClearContainer()
    {
        Game.ClearContainer(container);
    }

    public void SetGrid(bool b)
    {
        if (!b)
        {
            grid.enabled = false;
            return;
        }

        allBenchPositions.Sort((b1, b2) => b1.player.GetTeamIndex().CompareTo(b2.player.GetTeamIndex()));

        for(int i = allBenchPositions.Count-1; i >= 0; i--)
        {
            allBenchPositions[i].transform.SetAsFirstSibling();
        }
        grid.enabled = true;
    }

    public List<PositionUI> GetBenchPositions()
    {
        return allBenchPositions;
    }
}
