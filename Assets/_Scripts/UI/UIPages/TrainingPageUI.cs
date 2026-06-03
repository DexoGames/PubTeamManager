using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI page for selecting and previewing training sessions.
/// Player picks from available training types before executing.
/// </summary>
public class TrainingPageUI : UIPage
{
    public static TrainingPageUI Instance { get; private set; }

    [SerializeField] private Transform optionContainer;
    [SerializeField] private GameObject optionButtonPrefab;
    [SerializeField] private TextMeshProUGUI headerText;
    [SerializeField] private TextMeshProUGUI previewText;

    private TrainingSession selectedSession;

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
        selectedSession = null;
        RefreshOptions();
    }

    /// <summary>
    /// Populates the training option buttons from TrainingManager.
    /// </summary>
    private void RefreshOptions()
    {
        Game.ClearContainer(optionContainer);

        if (TrainingManager.Instance == null) return;

        var options = TrainingManager.Instance.GetAvailableTrainingOptions();

        if (headerText != null)
            headerText.text = "Choose Training Session";

        foreach (var session in options)
        {
            if (optionButtonPrefab == null) break;

            GameObject buttonObj = Instantiate(optionButtonPrefab, optionContainer);

            var text = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
                text.text = session.Description;

            var button = buttonObj.GetComponent<Button>();
            if (button != null)
            {
                TrainingSession captured = session;
                button.onClick.AddListener(() => SelectTraining(captured));
            }

            var image = buttonObj.GetComponentInChildren<Image>();
            switch (session.Type){
                case TrainingType.Technical:
                    image.color = Color.blue;
                    break;
                case TrainingType.Mental:
                    image.color = Color.yellow;
                    break;
                case TrainingType.Physical:
                    image.color = Color.cyan;
                    break;
                case TrainingType.Social:
                    image.color = Color.green;
                    break;
                case TrainingType.Tactical:
                    image.color = Color.magenta;
                    break;
            }
        }

        UpdatePreview();
    }

    /// <summary>
    /// Called when a training option button is clicked.
    /// </summary>
    private void SelectTraining(TrainingSession session)
    {
        selectedSession = session;
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (previewText == null) return;

        if (selectedSession == null)
        {
            previewText.text = "Select a training type above.";
            return;
        }

        string description = selectedSession.Description ?? selectedSession.Type.ToString();

        if (selectedSession.Type == TrainingType.Positional)
        {
            description += $"\n{selectedSession.GetEffectivenessDescription  ()}";
        }

        previewText.text = description;
    }

    /// <summary>
    /// Called by the "Confirm" button in the UI. Executes the selected training.
    /// </summary>
    public void ConfirmTraining()
    {
        if (selectedSession == null)
        {
            Debug.LogWarning("[Training UI] No session selected!");
            return;
        }

        if (TrainingManager.Instance != null)
        {
            TrainingManager.Instance.SetTraining(selectedSession);
            TrainingManager.Instance.ExecuteTraining();
        }

        Debug.Log($"[Training UI] Executed: {selectedSession.Description}");

        // Return to home page after training
        UIManager.Instance.ShowHomePage();
    }
}
