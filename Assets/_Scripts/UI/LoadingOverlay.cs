using UnityEngine;

/// <summary>
/// Full-screen loading overlay shown while the game simulates a day and saves, so the
/// day-advance (daybox) animation always plays cleanly afterwards regardless of any hitch.
/// Build the visual in the scene (a full-screen, input-blocking panel with a spinner image)
/// and wire `root` + `spinner`; if unwired, Show/Hide are safe no-ops.
/// </summary>
public class LoadingOverlay : MonoBehaviour
{
    public static LoadingOverlay Instance { get; private set; }

    [Tooltip("The overlay panel root (should block raycasts). Toggled on/off.")]
    [SerializeField] private GameObject root;
    [Tooltip("A graphic rotated each frame to read as a spinner.")]
    [SerializeField] private RectTransform spinner;
    [SerializeField] private float rotateSpeed = 220f;

    private bool visible;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (root != null) root.SetActive(false);
    }

    public void Show()
    {
        visible = true;
        if (root != null) root.SetActive(true);
    }

    public void Hide()
    {
        visible = false;
        if (root != null) root.SetActive(false);
    }

    private void Update()
    {
        // Unscaled so it keeps moving even if the game is paused / time-scaled.
        if (visible && spinner != null)
            spinner.Rotate(0f, 0f, -rotateSpeed * Time.unscaledDeltaTime);
    }
}
