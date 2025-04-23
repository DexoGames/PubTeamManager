using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Tactic/Instruction")]
public class TacticInstruction : ScriptableObject
{
    public string instructionName;
    [TextArea] public string description;

    public TacticEffect effect;
    public TacticInstruction[] incompatibleInstructions;
    public Player reliance;

    public void Apply(TacticStats stats)
    {
        effect.Apply(stats);
    }
}