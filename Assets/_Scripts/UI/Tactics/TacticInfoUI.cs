using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>
/// Read-out of the tactic's hidden trade-offs for the tactics page: how drilled the side is
/// (Familiarity), how demanding the setup is (Complexity → the Intelligence bar), and how many of the
/// current XI fall below that bar (and so will lose decision-making in matches). Refreshes whenever the
/// tactic changes, like <see cref="TacticStatSlider"/>.
///
/// Wire: put on a GameObject with a TMP text; assign it.
/// </summary>
public class TacticInfoUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI text;

    private void OnEnable()
    {
        Refresh();
        TacticsPageUI.Instance.OnTacticChange.AddListener(Refresh);
    }

    private void OnDisable()
    {
        TacticsPageUI.Instance.OnTacticChange.RemoveListener(Refresh);
    }

    public void Refresh()
    {
        Team team = TeamManager.Instance.MyTeam;
        Tactic t = team.Tactic;

        int needed = t.IntelligenceThreshold;
        int avg = Mathf.RoundToInt(t.CurrentStartingIntelligence());
        bool covered = avg >= needed;

        int below = 0;
        foreach (var p in team.StartingPlayers)
            if (p.TacticalIntelligence() < needed) below++;

        string famColour = t.Familiarity >= 60 ? "#6fcf6f" : (t.Familiarity >= 35 ? "#e0c060" : "#e06f6f");
        string iqColour = covered ? "#6fcf6f" : "#e06f6f";

        // The squad only gets penalised if the AVERAGE falls short — a smart XI covers for a dim player.
        string iqLine = covered
            ? $"Squad IQ <color={iqColour}>{avg}</color> ≥ {needed} — the side can handle this complexity"
            : $"Squad IQ <color={iqColour}>{avg}</color> &lt; {needed} — {below} player(s) will be penalised";

        text.text =
            $"Familiarity: <color={famColour}>{Mathf.RoundToInt(t.Familiarity)}%</color>\n" +
            $"Mentality: {MatchSimPageUI.MentalityLabel(t.EffectiveMentality)}\n" +
            $"Complexity: {t.Complexity} (needs avg Intelligence {needed})\n" +
            $"{iqLine}\n" +
            $"Reliances: {t.Instructions.Count(i => i.hasReliance)}";
    }
}
