using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A "click to add a player" slot. Empty → clicking opens the <see cref="PlayerPickerPopup"/> with the
/// candidates from the provider; picking fills the slot with the player's name. Filled → the X clears it,
/// and the sub-label keeps the picker's descriptor line visible after the pick.
/// Used for the training positional selection (five slots); reusable for any slot-style player selection.
/// </summary>
public class PlayerSlotUI : MonoBehaviour
{
    [SerializeField] private Button slotButton;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private Button removeButton;
    [SerializeField] private TextMeshProUGUI subLabel;
    [SerializeField] private string emptyText = "+ Add player";

    private int playerId = -1;
    private Func<IEnumerable<Player>> candidateProvider;
    private Func<Player, string> describe;
    private Action onChanged;
    private string title = "Select a player";

    public int PlayerId => playerId;
    public bool IsEmpty => playerId < 0;

    /// <summary>
    /// Wires the slot. <paramref name="candidates"/> returns the players that may be picked (e.g. the squad
    /// minus those already in other slots). <paramref name="changed"/> is called after a pick or clear.
    /// </summary>
    public void Setup(Func<IEnumerable<Player>> candidates, Action changed,
                      string pickerTitle = "Select a player", Func<Player, string> describer = null)
    {
        candidateProvider = candidates;
        onChanged = changed;
        title = pickerTitle;
        describe = describer;

        slotButton.onClick.RemoveListener(OnSlotClicked);
        slotButton.onClick.AddListener(OnSlotClicked);
        removeButton.onClick.RemoveListener(Clear);
        removeButton.onClick.AddListener(Clear);

        Refresh();
    }

    /// <summary>Sets the slot's player WITHOUT firing the change callback (for restoring saved state).</summary>
    public void SetPlayerSilent(int id)
    {
        playerId = id;
        Refresh();
    }

    /// <summary>Hook for the X button — clears the slot and notifies the owner.</summary>
    public void Clear()
    {
        if (IsEmpty) return;
        playerId = -1;
        Refresh();
        onChanged?.Invoke();
    }

    private void OnSlotClicked()
    {
        if (!IsEmpty) return; // a filled slot is cleared via the X, not re-picked
        PlayerPickerPopup.Show(candidateProvider(), OnPicked, title, describe);
    }

    private void OnPicked(Player p)
    {
        playerId = p.PersonID;
        Refresh();
        onChanged?.Invoke();
    }

    /// <summary>Re-reads the slot's display — also call it when the describer's context changes (a new target position).</summary>
    public void Refresh()
    {
        Player p = playerId >= 0 ? PersonManager.Instance.GetPlayer(playerId) : null;
        label.text = p != null ? p.FullName : emptyText;
        removeButton.gameObject.SetActive(p != null);

        // The describer reads live state (e.g. the selected training position), so re-run it on every refresh
        // rather than caching the string at pick time.
        string line = p != null && describe != null ? describe(p) : "";
        subLabel.text = line;
        subLabel.gameObject.SetActive(!string.IsNullOrEmpty(line));
    }
}
