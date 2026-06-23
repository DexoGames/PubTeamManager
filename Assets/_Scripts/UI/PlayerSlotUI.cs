using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A "click to add a player" slot. Empty → clicking opens the <see cref="PlayerPickerPopup"/> with the
/// candidates from the provider; picking fills the slot with the player's name. Filled → an X (remove
/// button) clears it. Used for the training positional selection (five slots); reusable for any slot-style
/// player selection.
/// </summary>
public class PlayerSlotUI : MonoBehaviour
{
    [Tooltip("The whole slot — opens the picker when the slot is empty.")]
    [SerializeField] private Button slotButton;
    [SerializeField] private TextMeshProUGUI label;
    [Tooltip("The X — clears a filled slot. Auto-hidden while the slot is empty.")]
    [SerializeField] private Button removeButton;
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

        if (slotButton == null) slotButton = GetComponent<Button>();
        if (slotButton != null)
        {
            slotButton.onClick.RemoveListener(OnSlotClicked);
            slotButton.onClick.AddListener(OnSlotClicked);
        }
        if (removeButton != null)
        {
            removeButton.onClick.RemoveListener(Clear);
            removeButton.onClick.AddListener(Clear);
        }

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
        PlayerPickerPopup.Show(candidateProvider?.Invoke(), OnPicked, title, describe);
    }

    private void OnPicked(Player p)
    {
        playerId = p.PersonID;
        Refresh();
        onChanged?.Invoke();
    }

    private void Refresh()
    {
        Player p = playerId >= 0 && PersonManager.Instance != null ? PersonManager.Instance.GetPlayer(playerId) : null;
        if (label != null) label.text = p != null ? p.FullName : emptyText;
        if (removeButton != null) removeButton.gameObject.SetActive(p != null);
    }
}
