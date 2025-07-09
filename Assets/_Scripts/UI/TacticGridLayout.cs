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

    // This list will now be correctly managed. It's private for safety.
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
        // Get the team and perform initial setup.
        // This assumes TeamManager and TacticsPageUI are ready.
        // Consider using a more robust system if initialization order is a problem.
        if (TeamManager.Instance != null)
        {
            team = TeamManager.Instance.MyTeam;
        }
        else
        {
            Debug.LogError("TeamManager instance not found!", this);
            return;
        }

        TacticsPageUI tacticsPage = FindObjectOfType<TacticsPageUI>();
        if (tacticsPage != null)
        {
            OnTacticChange.AddListener(tacticsPage.OnTacticChange.Invoke);
        }

        // Initial generation of the grid.
        GenerateGrid();
    }

    /// <summary>
    /// Destroys the existing grid and regenerates it from scratch.
    /// This is now safe to call at any time.
    /// </summary>
    public void GenerateGrid()
    {
        // 1. Destroy all existing UI children.
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        // 2. CRITICAL FIX: Clear the stale list of toggle references.
        toggles.Clear();

        if (togglePrefab == null)
        {
            Debug.LogError("Toggle Prefab is not assigned in the inspector!", this);
            return;
        }

        // --- Grid layout logic (largely unchanged) ---
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

            // 3. Populate the list with the NEW, valid reference.
            toggles.Add(newToggle);

            // Configure the toggle's visuals and data
            newToggle.Create(tactic.instruction);
            RectTransform rect = obj.GetComponent<RectTransform>();
            ConfigureToggleTransform(rect, tactic, ref currentX, ref currentY, containerWidth);

            // 4. Add the listener to the new toggle instance.
            newToggle.OnToggleChange.AddListener(() => ToggleClick(newToggle));

            // 5. Set its initial state.
            newToggle.Set(team.Tactic.Instructions.Contains(newToggle.instruction));
        }

        // 6. After creating all toggles, update their interactable status.
        UpdateAllTogglesInteractability();
    }

    void ToggleClick(TacticsToggle clickedToggle)
    {
        if (clickedToggle == null) return; // Safety check

        if (clickedToggle.toggle.isOn)
        {
            if (!IsInstructionValid(clickedToggle.instruction))
            {
                clickedToggle.Set(false); // Revert the toggle state
                return;
            }
            team.Tactic.AddInstruction(clickedToggle.instruction);
        }
        else
        {
            team.Tactic.RemoveInstruction(clickedToggle.instruction);
        }

        UpdateAllTogglesInteractability();
        OnTacticChange.Invoke();
    }

    // This helper method updates the UI state for all toggles based on the current tactic.
    void UpdateAllTogglesInteractability()
    {
        foreach (TacticsToggle toggle in toggles)
        {
            // A toggle should be interactable if it is currently selected OR if it is valid to be selected.
            bool isInteractable = toggle.toggle.isOn || IsInstructionValid(toggle.instruction);
            toggle.SetInteractable(isInteractable);
        }
    }

    // Helper to check if an instruction can be added to the current tactic.
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

    // --- Helper methods for positioning ---
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
        Vector2 anchoredPos = new Vector2(xPos, -currentY); // Simplified for top-down layout

        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); // Top-left anchor
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = new Vector2(width, cellSize.y);

        currentX += IsRightAligned() ? -(width + spacing.x) : (width + spacing.x);
    }

    private bool IsRightAligned() => startCorner == StartCorner.UpperRight || startCorner == StartCorner.LowerRight;
}