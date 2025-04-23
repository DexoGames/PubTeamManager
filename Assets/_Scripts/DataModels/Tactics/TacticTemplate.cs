using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Tactic/Template")]
public class TacticTemplate : ScriptableObject
{
    public string templateName;
    [TextArea] public string description;

    public Formation Formation;
    public TacticInstruction[] Instructions;
}
