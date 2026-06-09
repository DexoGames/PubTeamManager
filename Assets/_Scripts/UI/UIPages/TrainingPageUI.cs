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
    [SerializeField] private Transform playerListContainer;
    [SerializeField] private GameObject playerRowPrefab;
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
    private readonly Dictionary<int, Toggle> playerToggles = new Dictionary<int, Toggle>();
    private Player.Position[] dropdownPositions;
    private bool syncingToggles;

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

        RefreshPlayerRows();
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

        RefreshPlayerRows();   // refresh per-position strength display
        UpdateInfoPanel();
    }

    private void RefreshPlayerRows()
    {
        if (playerListContainer == null || playerRowPrefab == null) return;

        Game.ClearContainer(playerListContainer);
        playerToggles.Clear();

        Team team = TeamManager.Instance?.MyTeam;
        if (team == null) return;

        foreach (var player in team.Players)
        {
            GameObject row = Instantiate(playerRowPrefab, playerListContainer);
            var rowUI = row.GetComponent<PlayerRowUI>();

            // Name + position strength (coloured). Falls back to a child search if no PlayerRowUI.
            var label = rowUI != null && rowUI.nameLabel != null ? rowUI.nameLabel : row.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                Player.PositionStrength strength = player.RawStats.Positions.TryGetValue(selectedPosition, out var s)
                    ? s : Player.PositionStrength.None;
                string hex = ColorUtility.ToHtmlStringRGB(StrengthColor(strength));
                string nameLink = LinkBuilder.BuildLink(player); // clickable → opens player details
                label.text = $"{nameLink}\n<size=75%><color=#{hex}>{strength}</color></size>";
            }

            // Current ability rating (A/B/C…) in the selected position, as a sprite.
            if (rowUI != null && rowUI.ratingImage != null)
            {
                Sprite ratingSprite = UIStatDisplay.GetRatingSprite(player.GetRating(selectedPosition));
                rowUI.ratingImage.sprite = ratingSprite;
                rowUI.ratingImage.enabled = ratingSprite != null;
            }

            var toggle = rowUI != null && rowUI.toggle != null ? rowUI.toggle : row.GetComponentInChildren<Toggle>();
            if (toggle != null)
            {
                int id = player.PersonID;
                toggle.SetIsOnWithoutNotify(selectedPlayerIds.Contains(id));
                toggle.onValueChanged.AddListener(isOn => OnPlayerToggled(id, isOn));
                playerToggles[id] = toggle;
            }
        }
    }

    private void OnPlayerToggled(int playerId, bool isOn)
    {
        if (syncingToggles) return;

        if (isOn)
        {
            if (selectedPlayerIds.Count >= TrainingSession.MAX_POSITIONAL_PLAYERS)
            {
                // Reject the 6th selection — revert the toggle.
                syncingToggles = true;
                if (playerToggles.TryGetValue(playerId, out var t)) t.SetIsOnWithoutNotify(false);
                syncingToggles = false;
                Debug.LogWarning($"[Training UI] Max {TrainingSession.MAX_POSITIONAL_PLAYERS} players for positional training.");
                return;
            }
            if (!selectedPlayerIds.Contains(playerId)) selectedPlayerIds.Add(playerId);
        }
        else
        {
            selectedPlayerIds.Remove(playerId);
        }

        UpdateSelectedCount();
        UpdateInfoPanel();
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
