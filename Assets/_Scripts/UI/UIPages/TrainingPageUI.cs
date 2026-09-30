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

        setTrainingButton.onClick.RemoveListener(SetSelectedTraining);
        setTrainingButton.onClick.AddListener(SetSelectedTraining);
    }

    /// <summary>Pre-selects the drill that's currently set, so the page reflects ongoing training.</summary>
    private void RestoreFromCurrentSession()
    {
        selectedPlayerIds.Clear();
        var session = TrainingManager.Instance.CurrentSession;
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
        Game.ClearContainer(optionContainer);
        drillButtonImages.Clear();
        drillBaseColors.Clear();

        headerText.text = "Choose Training Session";

        foreach (var drill in TrainingManager.Instance.GetDrills())
        {
            GameObject buttonObj = Instantiate(optionButtonPrefab, optionContainer);

            buttonObj.GetComponentInChildren<TextMeshProUGUI>().text = drill.Name;

            DrillId captured = drill.Id;
            buttonObj.GetComponent<Button>().onClick.AddListener(() => SelectDrill(captured));

            Color baseColor = TypeColor(drill.Type);
            Image image = buttonObj.GetComponentInChildren<Image>();
            image.color = baseColor;
            drillButtonImages[drill.Id] = image;
            drillBaseColors[drill.Id] = baseColor;
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
            drillNameText.text = "Select a drill";
            typeText.text = "";
            affectedStatsText.text = "";
            effectText.text = "Pick a training drill to see what it does.";
            UpdateNextSession();
            return;
        }

        Drill drill = DrillCatalog.Get(selectedDrill.Value);
        if (drill == null) return;

        drillNameText.text = drill.Name;
        typeText.text = drill.Type.ToString();
        typeBadge.color = TypeColor(drill.Type);

        affectedStatsText.text = drill.AffectedStats.Length > 0
            ? "Affects: " + string.Join(", ", drill.AffectedStats) + SquadBoostSuffix(drill.AffectedStats)
            : "";

        // Build a transient session just to reuse its description logic.
        var preview = BuildSelectedSession();
        effectText.text = drill.Description + "\n\n" + preview.GetEffectivenessDescription();

        UpdateNextSession();
    }

    /// <summary>Appends the squad-average current Boost for the given stats, e.g. " (squad Boost +3.2)".</summary>
    private string SquadBoostSuffix(PlayerStat[] stats)
    {
        Team team = TeamManager.Instance.MyTeam;
        if (team.Players.Count == 0 || stats.Length == 0) return "";

        float total = 0f;
        foreach (var p in team.Players)
            foreach (var s in stats)
                total += p.GetBoost(s);

        float avg = total / (team.Players.Count * stats.Length);
        return $"   (squad Boost +{avg:F1}/{Player.MAX_BOOST})";
    }

    private void UpdateNextSession()
    {
        DateTime? next = ScheduleManager.Instance.GetNextTrainingDay();
        nextSessionText.text = next.HasValue
            ? $"Next session: {next.Value:ddd dd MMM}"
            : "Next session: not scheduled";
    }

    private void UpdateCurrentlySet()
    {
        var session = TrainingManager.Instance.CurrentSession;
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

        positionalPanel.SetActive(isPositional);
        if (!isPositional) return;

        SetupSlots();
        UpdateSelectedCount();
    }

    private void BuildPositionDropdown()
    {
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
        selectedPosition = dropdownPositions[index];

        // The slots' sub-labels describe each player AT the selected position, so they have to re-read.
        RefreshSlotLabels();
        UpdateInfoPanel();
    }

    // ————————————————————— player slots (click to add, X to remove) —————————————————————

    private void RefreshSlotLabels()
    {
        foreach (PlayerSlotUI slot in playerSlots) slot.Refresh();
    }

    private void SetupSlots()
    {
        int max = Mathf.Min(playerSlots.Length, TrainingSession.MAX_POSITIONAL_PLAYERS);
        for (int i = 0; i < playerSlots.Length; i++)
        {
            PlayerSlotUI slot = playerSlots[i];

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
        Team team = TeamManager.Instance.MyTeam;

        var taken = new HashSet<int>();
        foreach (var s in playerSlots)
            if (!s.IsEmpty) taken.Add(s.PlayerId);

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
        foreach (var s in playerSlots)
            if (!s.IsEmpty) selectedPlayerIds.Add(s.PlayerId);
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

        TrainingManager.Instance.SetTraining(BuildSelectedSession());
        SaveManager.Instance.SaveCore();
        UpdateCurrentlySet();
        Debug.Log($"[Training UI] Set ongoing training: {selectedDrill.Value}");
    }

    // ————————————————————— helpers —————————————————————

    private Color StrengthColor(Player.PositionStrength strength) => strengthColors[(int)strength];

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
