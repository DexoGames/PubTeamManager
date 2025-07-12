using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class FormationInteractableUI : FormationUI
{
    PositionUI draggedPosition;

    [SerializeField] BenchManager benchManager;
    [SerializeField] TMP_Dropdown dropdown;

    [SerializeField] RectTransform globalParent;

    public override void SetupInteractable(PositionUI newPos)
    {
        newPos.SetupInteractable();
    }

    public override void SetDropdown(Team team)
    {
        dropdown.ClearOptions();
        dropdown.onValueChanged.RemoveAllListeners();
        dropdown.onValueChanged.AddListener(NewFormation);
        List<string> options = new List<string>();
        foreach (Formation formation in formations)
        {
            options.Add(formation.Name);
        }
        dropdown.AddOptions(options);

        dropdown.value = formations.ToList().IndexOf(team.Formation);
        dropdown.RefreshShownValue();
    }

    public void PositionClicked(PositionUI pos)
    {
        //foreach (PositionUI p in allPositions)
        //{
        //    if (p.IsDraggable()) return;
        //}

        pos.transform.SetAsLastSibling();

        foreach (PositionUI p in allPositions)
        {
            if (p == pos) continue;

            CanvasGroup group = p.fader;
            Player.Position position = p.playerPosition.ID;

            group.alpha = 0.4f - (int)pos.player.RawStats.Positions[position] * 0.1f;
            p.transform.DOScale(Vector3.one * (0.75f + (int)pos.player.RawStats.Positions[position] * 0.1f), 0.25f).SetEase(Ease.InOutQuad);
        }
    }
    public void PositionReleased(PositionUI pos)
    {
        foreach (PositionUI p in allPositions)
        {
            CanvasGroup group = p.fader;
            group.alpha = 0f;
            p.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.InOutQuad);
        }

        PositionUI closest = ClosestPosition(pos.GetRect());
        float distance = DistanceBetweenRects(closest.GetComponent<RectTransform>(), pos.GetComponent<RectTransform>());

        DecideSwap(pos, closest, distance);

    }

    public void DecideSwap(PositionUI pos, PositionUI closest, float distance)
    {
        if (distance < 100)
        {
            SwapPlayerPositions(currentTeam, pos, closest);

            if (pos.GetType() == typeof(PositionUI) && closest.GetType() == typeof(PositionUI))
            {
                Formation.Position oldPos = pos.playerPosition;
                pos.Reassign(closest.playerPosition, pos.player);
                closest.Reassign(oldPos, closest.player);
            }
            else if(pos.GetType() == typeof(PositionUI) || closest.GetType() == typeof(PositionUI))
            {
                BenchPositionUI benchPlayer;
                PositionUI starting;
                if (pos.GetType() == typeof(BenchPositionUI))
                {
                    benchPlayer = (BenchPositionUI)pos;
                    starting = closest;
                }
                else
                {
                    benchPlayer = (BenchPositionUI)closest;
                    starting = pos;
                }

                Player startingPlayer = starting.player;
                Vector2 temp = starting.GetRect().position;
                starting.GetRect().position = benchPlayer.GetRect().position;
                benchPlayer.GetRect().position = temp;
                starting.Reassign(starting.playerPosition, benchPlayer.player);
                benchPlayer.ReassignBench(startingPlayer);
            }
            else
            {
                Vector2 closestPos = ((BenchPositionUI)closest).GetOriginalPos();
                ((BenchPositionUI)closest).SwapTo(((BenchPositionUI)pos).GetOriginalPos());
                ((BenchPositionUI)pos).SwapTo(closestPos);
            }
        }
        else
        {
            if(pos.GetType() == typeof(BenchPositionUI))
            {
                ((BenchPositionUI)pos).ReassignBench(pos.player);
            }
            else
            {
                pos.Reassign(pos.playerPosition, pos.player);
            }
        }
    }

    public PositionUI ClosestPosition(RectTransform requester)
    {
        List<PositionUI> benchAndPositions = new List<PositionUI>();
        benchAndPositions.AddRange(allPositions);
        if(benchManager != null) benchAndPositions.AddRange(benchManager.GetBenchPositions());

        float closestDist = float.MaxValue;
        PositionUI closestPos = benchAndPositions[0];

        foreach (PositionUI pos in benchAndPositions)
        {
            if (pos.GetRect() == requester) continue;

            float distance = DistanceBetweenRects(requester, pos.GetRect());
            if (distance < closestDist)
            {
                closestDist = distance;
                closestPos = pos;
            }
        }

        return closestPos;
    }

    public static float DistanceBetweenRects(RectTransform rt1, RectTransform rt2)
    {
        return Vector2.Distance(rt1.position, rt2.position);
    }

    void SwapPlayerPositions(Team team, PositionUI pos1, PositionUI pos2)
    {
        int index1 = pos1.player.GetTeamIndex();
        int index2 = pos2.player.GetTeamIndex();
        Player temp = pos1.player;
        team.Players[index1] = pos2.player;
        team.Players[index2] = temp;
    }

    public static void OrderPositions(List<PositionUI> positionUIList)
    {
        positionUIList = positionUIList.OrderBy(position => position.id).ToList();
    }

    public RectTransform GetGlobalParent()
    {
        return globalParent;
    }
}