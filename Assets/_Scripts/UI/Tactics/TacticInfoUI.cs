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

        string iq = covered ? $"<color=#9CD498>{avg}</color>" : $"<color=#FFA080>{avg} · {below} at risk</color>";
        text.text = $"{MatchSimPageUI.MentalityLabel(t.EffectiveMentality)}     Squad IQ {iq} / {needed}     Reliances {t.Instructions.Count(i => i.hasReliance)}";
    }
}
