using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName = "Formation", fileName = "New Formation")]
public class Formation : ScriptableObject
{
    public string Name;
    public Position[] Positions;
    public FormStats Stats;
    public Formation[] Inferiors;

    [Serializable]
    public struct FormStats
    {
        public int Complexity, Intensity, Control, Threat, Security, Tempo;
    }

    [Serializable]
    public struct Position
    {
        public Player.Position ID;
        public Vector2Int Location;
    }

    public string SquadRole(int i)
    {
        if(i < Positions.Length)
        {
            return Subposition(i);
        }
        return "SUB";
    }

    public string Subposition(int i)
    {
        string pos = Positions[i].ID.ToString();

        if (pos == "GK") return pos;

        string addon = "";
        if (Positions[i].Location.x == 0)
        {
            //addon = "C";
        }
        else if (Positions[i].Location.x < 0)
        {
            addon = "L";
        }
        else if (Positions[i].Location.x > 0)
        {
            addon = "R";
        }

        if (addon == pos[0].ToString()) addon = "";

        return addon + pos;
    }
}