using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(RectTransform))]
public class TacticGridLayout : MonoBehaviour
{
    public enum StartCorner { UpperLeft, UpperRight, LowerLeft, LowerRight }

    [System.Serializable]
    public class TacticEntry
    {
        public TacticInstruction instruction;
        public int size = 1;
    }

    public UnityEvent OnTacticChange;

    public List<TacticEntry> tactics = new List<TacticEntry>();
    public GameObject togglePrefab;

    private readonly List<TacticsToggle> toggles = new List<TacticsToggle>();

    public Vector2 cellSize = new Vector2(100, 100);
    public Vector2 spacing = new Vector2(10, 10);
    public RectOffset padding;
    public StartCorner startCorner = StartCorner.UpperLeft;

    private RectTransform rectTransform;
    private Team team;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    void Start()
    {
        team = TeamManager.Instance.MyTeam;

        OnTacticChange.AddListener(TacticsPageUI.Instance.OnTacticChange.Invoke);
        // Re-sync the toggles when the tactic changes elsewhere (e.g. a formation change or player swap
        // that auto-disables a now-invalid reliance) so a disabled instruction's toggle turns off.
        TacticsPageUI.Instance.OnTacticChange.AddListener(SyncToggleStates);

        GenerateGrid();
    }

    /// <summary>Pushes the current tactic's instruction set onto the toggles (no events fired).</summary>
    void SyncToggleStates()
    {
        foreach (TacticsToggle t in toggles)
        {
            bool active = team.Tactic.Instructions.Contains(t.instruction);
            t.SetSilent(active);
            t.SetReliantPlayer(active && t.instruction.hasReliance
                ? team.Tactic.GetReliancePlayer(t.instruction)
                : null);
        }
        UpdateAllTogglesInteractability();
    }

    /// <summary>Destroys the existing grid and regenerates it from scratch.</summary>
    public void GenerateGrid()
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
        toggles.Clear();

        // Re-validate reliances before building (auto-bind, auto-disable invalid, apply change penalties).
        team.Tactic.RefreshReliances();

        float containerWidth = rectTransform.rect.width;
        float currentX = padding.left;
        float currentY = padding.top;

        if (IsRightAligned())
        {
            currentX = containerWidth - padding.right;
        }

        foreach (var tactic in tactics)
        {
            GameObject obj = Instantiate(togglePrefab, transform);
            TacticsToggle newToggle = obj.GetComponent<TacticsToggle>();
            toggles.Add(newToggle);

            newToggle.Create(tactic.instruction);
            RectTransform rect = obj.GetComponent<RectTransform>();
            ConfigureToggleTransform(rect, tactic, ref currentX, ref currentY, containerWidth);

            newToggle.OnToggleChange.AddListener(() => ToggleClick(newToggle));

            // Set the initial state SILENTLY — don't trigger ToggleClick (or the reliance picker) on build.
            bool active = team.Tactic.Instructions.Contains(newToggle.instruction);
            newToggle.SetSilent(active);
            if (active && newToggle.instruction.hasReliance)
                newToggle.SetReliantPlayer(team.Tactic.GetReliancePlayer(newToggle.instruction));
        }

        UpdateAllTogglesInteractability();
    }

    static PlayerPickerColumnValue DescribeReliance(Player player, PlayerStat[] stats)
    {
        Player.Stats effectiveStats = player.GetStats();
        int average = Mathf.RoundToInt((float)stats.Average(stat => effectiveStats.GetStat(stat)));
        return new PlayerPickerColumnValue($"{average} skill avg.");
    }

    void ToggleClick(TacticsToggle clickedToggle)
    {
        TacticInstruction instr = clickedToggle.instruction;

        if (clickedToggle.toggle.isOn)
        {
            if (!IsInstructionValid(instr))
            {
                clickedToggle.SetSilent(false); // Revert the toggle state (no re-trigger)
                return;
            }

            // A reliance instruction needs a player chosen before it's committed.
            if (instr.hasReliance)
            {
                PlayerStat[] stats = instr.reliance.stats;
                Func<Player, PlayerPickerColumnValue> column = stats?.Length > 0
                    ? player => DescribeReliance(player, stats)
                    : null;

                bool opened = PlayerPickerPopup.Show(
                    team.Tactic.EligibleReliancePlayers(instr),   // only players in the reliance's position groups
                    picked =>
                    {
                        team.Tactic.AddInstruction(instr, picked);
                        clickedToggle.SetReliantPlayer(picked);
                        AfterToggleChange();
                    },
                    $"{instr.tacticName} — rely on which player?",
                    onCancelled: () => clickedToggle.SetSilent(false), extraColumn: column); // didn't pick → revert, don't add
                if (opened) return; // wait for the picker
                // Picker prefab unavailable → fall through and add with an auto-bound player (match start).
            }

            team.Tactic.AddInstruction(instr);
        }
        else
        {
            team.Tactic.RemoveInstruction(instr);
            clickedToggle.SetReliantPlayer(null); // reset label (no-op visual for non-reliance)
        }

        AfterToggleChange();
    }

    void AfterToggleChange()
    {
        UpdateAllTogglesInteractability();
        OnTacticChange.Invoke();
    }

    void UpdateAllTogglesInteractability()
    {
        foreach (TacticsToggle toggle in toggles)
        {
            // Interactable if it's currently selected OR valid to be selected.
            bool isInteractable = toggle.toggle.isOn || IsInstructionValid(toggle.instruction);
            toggle.SetInteractable(isInteractable);
        }
    }

    bool IsInstructionValid(TacticInstruction instruction)
    {
        foreach (TacticInstruction activeInstruction in team.Tactic.Instructions)
        {
            if (activeInstruction.incompatibleInstructions.Contains(instruction))
            {
                return false;
            }
        }
        return true;
    }

    private void ConfigureToggleTransform(RectTransform rect, TacticEntry tactic, ref float currentX, ref float currentY, float containerWidth)
    {
        int size = Mathf.Max(tactic.size, 1);
        float width = size * cellSize.x + (size - 1) * spacing.x;

        bool shouldWrap = IsRightAligned()
            ? (currentX - width < padding.left)
            : (currentX + width > containerWidth - padding.right && currentX > padding.left);

        if (shouldWrap)
        {
            currentY += cellSize.y + spacing.y;
            currentX = IsRightAligned() ? containerWidth - padding.right : padding.left;
        }

        float xPos = IsRightAligned() ? currentX - width : currentX;
        Vector2 anchoredPos = new Vector2(xPos, -currentY);

        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); // top-left anchor
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = new Vector2(width, cellSize.y);

        currentX += IsRightAligned() ? -(width + spacing.x) : (width + spacing.x);
    }

    private bool IsRightAligned() => startCorner == StartCorner.UpperRight || startCorner == StartCorner.LowerRight;
}