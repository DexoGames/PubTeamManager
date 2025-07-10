using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "EventType", fileName = "New EventType")]
public class EventType : ScriptableObject
{
    [Serializable]
    public enum Severity
    {
        Dire, Pressing, Unfortunate, Irrelevant, Pleasant, Uplifting, Momentous
    }
    [Serializable]
    public enum Tag
    {
        Basic, Special          // Basic is for wins and losses, special for other (might add more later)
    }

    public Tag tag = Tag.Special;
    public string description;  // Use <1> to discuss the first person affected, <2> for the second etc. or use <all> for everyone affected, <w1> to use custom words
    [TextArea] public string discussion;
    [Range(0f, 1f)] public float odds = 0.2f;
    public int noAffected;
    public int moraleChange;
    public Severity severity = Severity.Irrelevant;
}