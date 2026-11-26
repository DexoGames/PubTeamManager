using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reusable modal for picking ONE player from a supplied list. Call the static <see cref="Show"/> — it
/// instantiates the popup prefab on demand (from Resources, see <see cref="PrefabResourcePath"/>), shows it,
/// invokes your callback with the chosen player (or the cancel callback if dismissed), then destroys itself.
/// No pre-placed scene object needed. Shared by reliance instructions and the training player slots.
/// </summary>
[RequireComponent(typeof(Canvas), typeof(GraphicRaycaster))]
public class PlayerPickerPopup : MonoBehaviour
{
    /// <summary>Where the prefab lives, under a <c>Resources</c> folder (no extension). Must be a Canvas (or
    /// hold one) so it renders when instantiated standalone — e.g. Assets/Resources/UI/PlayerPickerPopup.prefab.</summary>
    const string PrefabResourcePath = "UI/PlayerPickerPopUp";

    [SerializeField] Transform listContainer;
    [SerializeField] PlayerPickerRow rowPrefab;
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] Button cancelButton;

    Action<Player> onPicked;
    Action onCancelled;

    /// <summary>
    /// Instantiates the picker prefab and shows it. <paramref name="describe"/> optionally supplies a secondary
    /// line per candidate. <paramref name="extraColumn"/> supplies a separate text or icon cell for each row.
    /// </summary>
    public static bool Show(IEnumerable<Player> candidates, Action<Player> onPicked,
                            string title = "Select a player", Func<Player, string> describe = null,
                            Action onCancelled = null, Func<Player, PlayerPickerColumnValue> extraColumn = null)
    {
        PlayerPickerPopup prefab = Resources.Load<PlayerPickerPopup>(PrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogError($"[PlayerPickerPopup] Prefab not found at Resources/{PrefabResourcePath}.");
            return false;
        }

        Canvas root = HomePageUI.Instance.GetComponentInParent<Canvas>().rootCanvas;
        PlayerPickerPopup popup = Instantiate(prefab, root.transform, false);
        popup.gameObject.SetActive(true);

        popup.Populate(candidates, onPicked, title, describe, onCancelled, extraColumn);
        return true;
    }

    void Awake()
    {
        cancelButton.onClick.AddListener(Cancel);
    }

    void Populate(IEnumerable<Player> candidates, Action<Player> onPicked,
                  string title, Func<Player, string> describe, Action onCancelled,
                  Func<Player, PlayerPickerColumnValue> extraColumn)
    {
        this.onPicked = onPicked;
        this.onCancelled = onCancelled;

        titleText.text = title;

        foreach (Transform child in listContainer) Destroy(child.gameObject);

        foreach (Player player in candidates)
        {
            PlayerPickerRow row = Instantiate(rowPrefab, listContainer);
            string subtitle = describe != null ? describe(player) : player.GetPosition()?.ToString() ?? "SUB";
            row.Setup(player, subtitle, () => Pick(player), extraColumn?.Invoke(player));
        }
    }

    void Pick(Player player)
    {
        Action<Player> callback = onPicked;
        Close();
        callback?.Invoke(player);
    }

    /// <summary>Hook for the cancel/backdrop button — closes without picking.</summary>
    public void Cancel()
    {
        Action callback = onCancelled;
        Close();
        callback?.Invoke();
    }

    void Close()
    {
        onPicked = null;
        onCancelled = null;
        Destroy(gameObject);
    }
}
