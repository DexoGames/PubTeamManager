/// <summary>
/// An instruction toggle on the tactics screen. Just the instruction-specific bits — all the toggle plumbing
/// (Toggle, colours, OnToggleChange, Set/SetInteractable) lives in <see cref="TacticOptionToggle"/>.
/// TacticGridLayout listens to <see cref="TacticOptionToggle.OnToggleChange"/> to add/remove the instruction.
/// </summary>
public class TacticsToggle : TacticOptionToggle
{
    public TacticInstruction instruction { get; private set; }

    // Called by TacticGridLayout after instantiating.
    public void Create(TacticInstruction newInstruction)
    {
        instruction = newInstruction;
        SetLabel(instruction != null ? instruction.tacticName : "Unnamed");
    }

    /// <summary>For reliance instructions: show the chosen player after the name ("Name: Surname"). Null → just the name.</summary>
    public void SetReliantPlayer(Player p)
    {
        if (instruction == null) { SetLabel("Unnamed"); return; }
        SetLabel(p != null ? $"{instruction.tacticName}: {p.Surname}" : instruction.tacticName);
    }
}
