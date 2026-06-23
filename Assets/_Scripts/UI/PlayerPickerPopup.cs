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
public class PlayerPickerPopup : MonoBehaviour
{
    /// <summary>Where the prefab lives, under a <c>Resources</c> folder (no extension). Must be a Canvas (or
    /// hold one) so it renders when instantiated standalone — e.g. Assets/Resources/UI/PlayerPickerPopup.prefab.</summary>
    private const string PrefabResourcePath = "UI/PlayerPickerPopup";

    [SerializeField] private Transform listContainer;     // scroll-view content the rows spawn under
    [SerializeField] private PlayerPickerRow rowPrefab;
    [SerializeField] private TextMeshProUGUI titleText;
    [Tooltip("Optional cancel/backdrop button — closes without picking. Auto-wired on Awake.")]
    [SerializeField] private Button cancelButton;

    private Action<Player> onPicked;
    private Action onCancelled;

    /// <summary>
    /// Instantiates the picker prefab and shows it. <paramref name="describe"/> optionally supplies a secondary
    /// line per candidate (else best positions are shown). Returns false if the prefab couldn't be loaded.
    /// </summary>
    public static bool Show(IEnumerable<Player> candidates, Action<Player> onPicked,
                            string title = "Select a player", Func<Player, string> describe = null,
                            Action onCancelled = null)
    {
        PlayerPickerPopup prefab = Resources.Load<PlayerPickerPopup>(PrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogError($"[PlayerPickerPopup] Prefab not found at Resources/{PrefabResourcePath}.");
            return false;
        }

        PlayerPickerPopup popup = Instantiate(prefab);
        popup.gameObject.SetActive(true);

        // Parent under the root canvas (so it inherits the UI scaling), then force it to render AND receive
        // input ABOVE everything else — page content (e.g. the draggable formation cards) often lives on its
        // own sub-canvas that would otherwise sort on top of the popup and steal its clicks.
        Canvas root = FindObjectOfType<Canvas>();
        if (root != null) root = root.rootCanvas;
        if (root != null)
        {
            popup.transform.SetParent(root.transform, false);
            popup.transform.SetAsLastSibling();
        }

        Canvas popupCanvas = popup.GetComponent<Canvas>();
        if (popupCanvas == null) popupCanvas = popup.gameObject.AddComponent<Canvas>();
        popupCanvas.overrideSorting = true;
        popupCanvas.sortingOrder = short.MaxValue;                 // 32767 — above all page canvases
        if (popup.GetComponent<GraphicRaycaster>() == null)
            popup.gameObject.AddComponent<GraphicRaycaster>();     // its own raycaster so its clicks win

        popup.Populate(candidates, onPicked, title, describe, onCancelled);
        return true;
    }

    private void Awake()
    {
        if (cancelButton != null) cancelButton.onClick.AddListener(Cancel);
    }

    private void Populate(IEnumerable<Player> candidates, Action<Player> onPicked,
                          string title, Func<Player, string> describe, Action onCancelled)
    {
        this.onPicked = onPicked;
        this.onCancelled = onCancelled;

        if (titleText != null) titleText.text = title;

        if (listContainer != null)
        {
            foreach (Transform child in listContainer) Destroy(child.gameObject);

            if (candidates != null && rowPrefab != null)
            {
                foreach (Player p in candidates)
                {
                    Player captured = p;
                    PlayerPickerRow row = Instantiate(rowPrefab, listContainer);
                    string sub = p.GetPosition().HasValue? p.GetPosition().ToString() : "SUB";
                    row.Setup(captured, sub, () => Pick(captured));
                }
            }
        }
    }

    private void Pick(Player p)
    {
        Debug.Log("" + p.FullName);
        Action<Player> cb = onPicked;
        Close();
        cb?.Invoke(p);
    }

    /// <summary>Hook for the cancel/backdrop button — closes without picking.</summary>
    public void Cancel()
    {
        Action cb = onCancelled;
        Close();
        cb?.Invoke();
    }

    private void Close()
    {
        onPicked = null;
        onCancelled = null;
        Destroy(gameObject);
    }
}
