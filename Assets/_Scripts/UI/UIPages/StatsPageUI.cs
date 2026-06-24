using UnityEngine;

/// <summary>
/// A dedicated stats hub. It carries no logic of its own — drop as many <see cref="StatLeaderboardWidget"/>s as
/// you like under its Elements (top scorers, assists, team shots, saves, cards, …) and the base
/// <see cref="UIPage"/> calls each widget's Setup() automatically when the page is shown.
/// Shown via <c>UIManager.Instance.ShowStats()</c>.
/// </summary>
public class StatsPageUI : UIPage
{
    public static StatsPageUI Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }
}
