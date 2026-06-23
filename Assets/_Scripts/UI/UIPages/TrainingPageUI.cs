using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Training page: pick a drill to set as the team's ongoing training regimen.
/// Setting a drill persists it (it repeats every training day) — it does NOT execute
/// immediately. Positional training reveals a sub-panel to choose a position and up to
/// five players. The info panel previews the selected drill's effect and the next session.
/// </summary>
public class TrainingPageUI : UIPage
{
    public static TrainingPageUI Instance { get; private set; }

    [Header("Drill list")]
    [SerializeField] private Transform optionContainer;
    [SerializeField] private GameObject optionButtonPrefab;
    [SerializeField] private TextMeshProUGUI headerText;

    [Header("Info panel")]
    [SerializeField] private TextMeshProUGUI drillNameText;
    [SerializeField] private TextMeshProUGUI typeText;
    [SerializeField] private Image typeBadge;
    [SerializeField] private TextMeshProUGUI affectedStatsText;
    [SerializeField] private TextMeshProUGUI effectText;
    [SerializeField] private TextMeshProUGUI nextSessionText;
    [SerializeField] private TextMeshProUGUI currentlySetText;
    [SerializeField] private Button setTrainingButton;

    [Header("Positional sub-panel")]
    [SerializeField] private GameObject positionalPanel;
    [SerializeField] private TMP_Dropdown positionDropdown;
    [Tooltip("Up to MAX_POSITIONAL_PLAYERS (5) slot widgets; click an empty one to add a player, X to remove.")]
    [SerializeField] private PlayerSlotUI[] playerSlots;
    [SerializeField] private TextMeshProUGUI selectedCountText;

    [Header("Position strength colours (match the Tactics menu's StrengthColors)")]
    [Tooltip("Indexed by PositionStrength: 0=None, 1=Poor, 2=Okay, 3=Good, 4=Natural. " +
             "Set these to the same values used on PositionUI in the tactics menu.")]
    [SerializeField] private Color[] strengthColors = new Color[]
    {
        new Color(0.55f, 0.55f, 0.55f), // None    — grey
        new Color(0.85f, 0.30f, 0.30f), // Poor    — red
        new Color(0.90f, 0.65f, 0.25f), // Okay    — orange
        new Color(0.55f, 0.80f, 0.35f), // Good    — light green
        new Color(0.30f, 0.75f, 0.40f), // Natural — green
    };

    // Selection state (what the player is previewing, not yet set)
    private DrillId? selectedDrill;
    private Player.Position selectedPosition = Player.Position.GK;
    private readonly List<int> selectedPlayerIds = new List<int>();

    // Lookups for highlighting / row management
    private readonly Dictionary<DrillId, Image> drillButtonImages = new Dictionary<DrillId, Image>();
    private readonly Dictionary<DrillId, Color> drillBaseColors = new Dictionary<DrillId, Color>();
    private Player.Position[] dropdownPositions;

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(this.gameObject);
        else
            Instance = this;
    }

    protected override void OnShow()
    {
        base.OnShow();
        RestoreFromCurrentSession();
        RefreshOptions();
        BuildPositionDropdown();
        UpdateInfoPanel();
        UpdatePositionalPanel();
        UpdateCurrentlySet();

        if (setTrainingButton != null)
        {
            setTrainingButton.onClick.RemoveListener(SetSelectedTraining);
            setTrainingButton.onClick.AddListener(SetSelectedTraining);
        }
    }

    /// <summary>Pre-selects the drill that's currently set, so the page reflects ongoing training.</summary>
    private void RestoreFromCurrentSession()
    {
        selectedPlayerIds.Clear();
        var session = TrainingManager.Instance?.CurrentSession;
        if (session == null) { selectedDrill = null; return; }

        selectedDrill = session.Drill;
        if (session.Type == TrainingType.Positional)
        {
            if (session.TargetPosition.HasValue) selectedPosition = session.TargetPosition.Value;
            if (session.SelectedPlayerIds != null) selectedPlayerIds.AddRange(session.SelectedPlayerIds);
        }
    }

    // ————————————————————— drill list —————————————————————

    private void RefreshOptions()
    {
        if (optionContainer == null || optionButtonPrefab == null) return;

        Game.ClearContainer(optionContainer);
        drillButtonImages.Clear();
        drillBaseColors.Clear();

        if (TrainingManager.Instance == null) return;

        if (headerText != null) headerText.text = "Choose Training Session";

        foreach (var drill in TrainingManager.Instance.GetDrills())
        {
            GameObject buttonObj = Instantiate(optionButtonPrefab, optionContainer);

            var text = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.text = drill.Name;

            var button = buttonObj.GetComponent<Button>();
            if (button != null)
            {
                DrillId captured = drill.Id;
                button.onClick.AddListener(() => SelectDrill(captured));
            }

            var image = buttonObj.GetComponentInChildren<Image>();
            if (image != null)
            {
                Color baseColor = TypeColor(drill.Type);
                image.color = baseColor;
                drillButtonImages[drill.Id] = image;
                drillBaseColors[drill.Id] = baseColor;
            }
        }

        ApplyHighlight();
    }

    private void SelectDrill(DrillId drill)
    {
        selectedDrill = drill;
        ApplyHighlight();
        UpdateInfoPanel();
        UpdatePositionalPanel();
    }

    private void ApplyHighlight()
    {
        foreach (var kvp in drillButtonImages)
        {
            Color baseColor = drillBaseColors.TryGetValue(kvp.Key, out var c) ? c : kvp.Value.color;
            bool isSelected = selectedDrill.HasValue && kvp.Key == selectedDrill.Value;
            kvp.Value.color = isSelected ? Color.Lerp(baseColor, Color.white, 0.55f) : baseColor;
        }
    }

    // ————————————————————— info panel —————————————————————

    private void UpdateInfoPanel()
    {
        if (!selectedDrill.HasValue)
        {
            if (drillNameText != null) drillNameText.text = "Select a drill";
            if (typeText != null) typeText.text = "";
            if (affectedStatsText != null) affectedStatsText.text = "";
            if (effectText != null) effectText.text = "Pick a training drill to see what it does.";
            UpdateNextSession();
            return;
        }

        Drill drill = DrillCatalog.Get(selectedDrill.Value);
        if (drill == null) return;

        if (drillNameText != null) drillNameText.text = drill.Name;
        if (typeText != null) typeText.text = drill.Type.ToString();
        if (typeBadge != null) typeBadge.color = TypeColor(drill.Type);

        if (affectedStatsText != null)
        {
            affectedStatsText.text = drill.AffectedStats.Length > 0
                ? "Affects: " + string.Join(", ", drill.AffectedStats) + SquadBoostSuffix(drill.AffectedStats)
                : "";
        }

        if (effectText != null)
        {
            // Build a transient session just to reuse its description logic.
            var preview = BuildSelectedSession();
            effectText.text = drill.Description + "\n\n" + (preview != null ? preview.GetEffectivenessDescription() : "");
        }

        UpdateNextSession();
    }

    /// <summary>Appends the squad-average current Boost for the given stats, e.g. " (squad Boost +3.2)".</summary>
    private string SquadBoostSuffix(PlayerStat[] stats)
    {
        Team team = TeamManager.Instance?.MyTeam;
        if (team == null || team.Players == null || team.Players.Count == 0 || stats.Length == 0) return "";

        float total = 0f;
        foreach (var p in team.Players)
            foreach (var s in stats)
                total += p.GetBoost(s);

        float avg = total / (team.Players.Count * stats.Length);
        return $"   (squad Boost +{avg:F1}/{Player.MAX_BOOST})";
    }

    private void UpdateNextSession()
    {
        if (nextSessionText == null) return;

        DateTime? next = ScheduleManager.Instance?.GetNextTrainingDay();
        nextSessionText.text = next.HasValue
            ? $"Next session: {next.Value:ddd dd MMM}"
            : "Next session: not scheduled";
    }

    private void UpdateCurrentlySet()
    {
        if (currentlySetText == null) return;

        var session = TrainingManager.Instance?.CurrentSession;
        if (session == null) { currentlySetText.text = "Currently training: none"; return; }

        string extra = session.Type == TrainingType.Positional && session.TargetPosition.HasValue
            ? $" ({Player.LongPosition(session.TargetPosition.Value)}, {session.SelectedPlayerIds?.Count ?? 0} players)"
            : "";
        currentlySetText.text = $"Currently training: {session.Name}{extra}";
    }

    // ————————————————————— positional sub-panel —————————————————————

    private void UpdatePositionalPanel()
    {
        bool isPositional = selectedDrill.HasValue && DrillCatalog.Get(selectedDrill.Value)?.Type == TrainingType.Positional;

        if (positionalPanel != null) positionalPanel.SetActive(isPositional);
        if (!isPositional) return;

        SetupSlots();
        UpdateSelectedCount();
    }

    private void BuildPositionDropdown()
    {
        if (positionDropdown == null) return;

        dropdownPositions = (Player.Position[])System.Enum.GetValues(typeof(Player.Position));

        positionDropdown.ClearOptions();
        positionDropdown.AddOptions(dropdownPositions.Select(p => Player.LongPosition(p)).ToList());

        int idx = System.Array.IndexOf(dropdownPositions, selectedPosition);
        positionDropdown.SetValueWithoutNotify(Mathf.Max(0, idx));

        positionDropdown.onValueChanged.RemoveListener(OnPositionDropdownChanged);
        positionDropdown.onValueChanged.AddListener(OnPositionDropdownChanged);
    }

    private void OnPositionDropdownChanged(int index)
    {
        if (dropdownPositions != null && index >= 0 && index < dropdownPositions.Length)
            selectedPosition = dropdownPositions[index];

        // Slot labels are just names (position-independent); the picker's per-player description reads the
        // new position live, so no slot rebuild is needed — just refresh the preview.
        UpdateInfoPanel();
    }

    // ————————————————————— player slots (click to add, X to remove) —————————————————————

    private void SetupSlots()
    {
        if (playerSlots == null) return;

        int max = Mathf.Min(playerSlots.Length, TrainingSession.MAX_POSITIONAL_PLAYERS);
        for (int i = 0; i < playerSlots.Length; i++)
        {
            PlayerSlotUI slot = playerSlots[i];
            if (slot == null) continue;

            // Hide any slots beyond the cap.
            slot.gameObject.SetActive(i < max);
            if (i >= max) continue;

            slot.Setup(CandidatesForPicking, OnSlotsChanged, "Add player to train", SlotDescriber);
            slot.SetPlayerSilent(i < selectedPlayerIds.Count ? selectedPlayerIds[i] : -1);
        }
    }

    /// <summary>The squad, minus any player already sitting in a slot (so you can't pick the same one twice).</summary>
    private IEnumerable<Player> CandidatesForPicking()
    {
        Team team = TeamManager.Instance?.MyTeam;
        if (team == null) return Enumerable.Empty<Player>();

        var taken = new HashSet<int>();
        if (playerSlots != null)
            foreach (var s in playerSlots)
                if (s != null && !s.IsEmpty) taken.Add(s.PlayerId);

        return team.Players.Where(p => !taken.Contains(p.PersonID));
    }

    private void OnSlotsChanged()
    {
        RebuildSelectedFromSlots();
        UpdateSelectedCount();
        UpdateInfoPanel();
    }

    private void RebuildSelectedFromSlots()
    {
        selectedPlayerIds.Clear();
        if (playerSlots == null) return;
        foreach (var s in playerSlots)
            if (s != null && !s.IsEmpty) selectedPlayerIds.Add(s.PlayerId);
    }

    /// <summary>Per-candidate line shown in the picker: their strength + rating at the selected position.</summary>
    private string SlotDescriber(Player p)
    {
        Player.PositionStrength strength = p.RawStats.Positions.TryGetValue(selectedPosition, out var s)
            ? s : Player.PositionStrength.None;
        string hex = ColorUtility.ToHtmlStringRGB(StrengthColor(strength));
        return $"<color=#{hex}>{strength}</color> · {p.GetRating(selectedPosition)} @ {selectedPosition}";
    }

    private void UpdateSelectedCount()
    {
        if (selectedCountText != null)
            selectedCountText.text = $"{selectedPlayerIds.Count}/{TrainingSession.MAX_POSITIONAL_PLAYERS} selected";
    }

    // ————————————————————— set training —————————————————————

    private TrainingSession BuildSelectedSession()
    {
        if (!selectedDrill.HasValue) return null;

        if (DrillCatalog.Get(selectedDrill.Value)?.Type == TrainingType.Positional)
            return TrainingSession.Positional(selectedPosition, selectedPlayerIds);

        return new TrainingSession(selectedDrill.Value);
    }

    /// <summary>Hooked to the "Set Training" button. Persists the regimen; does not execute.</summary>
    public void SetSelectedTraining()
    {
        if (!selectedDrill.HasValue)
        {
            Debug.LogWarning("[Training UI] No drill selected.");
            return;
        }

        if (DrillCatalog.Get(selectedDrill.Value)?.Type == TrainingType.Positional && selectedPlayerIds.Count == 0)
        {
            Debug.LogWarning("[Training UI] Select at least one player for positional training.");
            return;
        }

        TrainingManager.Instance?.SetTraining(BuildSelectedSession());
        SaveManager.Instance?.SaveCore();
        UpdateCurrentlySet();
        Debug.Log($"[Training UI] Set ongoing training: {selectedDrill.Value}");
    }

    // ————————————————————— helpers —————————————————————

    /// <summary>Colour for a position strength, matching the tactics menu (indexed by PositionStrength).</summary>
    private Color StrengthColor(Player.PositionStrength strength)
    {
        int i = (int)strength;
        if (strengthColors != null && i >= 0 && i < strengthColors.Length)
            return strengthColors[i];
        return Color.white;
    }

    private static Color TypeColor(TrainingType type)
    {
        switch (type)
        {
            case TrainingType.Technical:  return new Color(0.30f, 0.55f, 0.95f); // blue
            case TrainingType.Mental:     return new Color(0.95f, 0.80f, 0.25f); // yellow
            case TrainingType.Physical:   return new Color(0.30f, 0.80f, 0.85f); // cyan
            case TrainingType.Social:     return new Color(0.35f, 0.75f, 0.40f); // green
            case TrainingType.Tactical:   return new Color(0.80f, 0.40f, 0.80f); // magenta
            case TrainingType.Positional: return new Color(0.90f, 0.55f, 0.25f); // orange
            default:                      return Color.grey;
        }
    }
}
