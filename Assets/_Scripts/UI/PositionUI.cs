using DG.Tweening;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UIStatDisplay;

public enum DisplayMode
{
    Default,
    Physical,
    Tactical,
    Display,
}

public class PositionUI : MonoBehaviour
{
    [Header("Display Mode Objects")]
    public GameObject defaultDisplay;
    public GameObject physicalDisplay;
    public GameObject tacticalDisplay;
    public GameObject displayDisplay;

    [Header("Configuration")]
    [SerializeField] private DraggableUI draggable;
    [SerializeField] private Color[] StrengthColors;

    public int id;
    public CanvasGroup fader;

    [HideInInspector] public Player player;
    [HideInInspector] public Formation.Position Position;

    protected Dictionary<UIStatDisplay.StatType, List<Component>> uiDisplays;

    protected RectTransform rt;
    protected RectTransform container;
    protected FormationUI manager;

    private void Awake()
    {
        uiDisplays = new Dictionary<UIStatDisplay.StatType, List<Component>>();
        UIStatDisplay[] allDisplays = GetComponentsInChildren<UIStatDisplay>(true);

        foreach (UIStatDisplay display in allDisplays)
        {
            UIStatDisplay.StatType statType = display.statToDisplay;

            if (!uiDisplays.ContainsKey(statType))
            {
                uiDisplays[statType] = new List<Component>();
            }

            if (display.TryGetComponent<TextMeshProUGUI>(out var textComponent))
            {
                uiDisplays[statType].Add(textComponent);
            }
            else if (display.TryGetComponent<Image>(out var imageComponent))
            {
                uiDisplays[statType].Add(imageComponent);
            }
        }
    }

    public virtual void UpdateValues(Formation.Position position, Player player)
    {
        Position = position;
        this.player = player;
        name = "StartingPlayer " + player.FullName;

        UpdateTextStat(UIStatDisplay.StatType.Position, position.ID.ToString(), player.GetTeamIndex() < 11 ? StrengthColors[(int)player.RawStats.Positions[position.ID]] : null);
        UpdateTextStat(UIStatDisplay.StatType.Surname, LinkBuilder.BuildLink(player, player.Surname));
        UpdateTextStat(UIStatDisplay.StatType.Rating, player.GetRating(Position.ID).ToString());
        UpdateTextStat(UIStatDisplay.StatType.KitNumber, player.GetKitNumber().ToString());
        UpdateTextStat(UIStatDisplay.StatType.Age, player.AgeYears().ToString());
        UpdateTextStat(UIStatDisplay.StatType.Fatigue, player.Fatigue.ToString(), Color.Lerp(Color.green, Color.red, player.Fatigue/100f));
        UpdateTextStat(UIStatDisplay.StatType.Intelligence, player.RawStats.Intelligence.ToString(), Color.Lerp(Color.red, Color.green, player.RawStats.Intelligence / 100f));

        UpdateImageColor(UIStatDisplay.StatType.Morale, player.GetMoraleColor());

        string[] list = player.ListBestPositions().Split(' ');
        string newString = "";
        for (int i = 1; i < list.Length; i++)
        {
            if (!string.IsNullOrEmpty(list[i]))
            {
                newString += list[i] + " ";
            }
        }
        newString = newString.Trim();

        UpdateTextStat(StatType.OtherPositions, newString);
    }

    protected void UpdateTextStat(UIStatDisplay.StatType statType, string value, Color? color = null)
    {
        if (uiDisplays.TryGetValue(statType, out var components))
        {
            foreach (var component in components)
            {
                var textComponent = (TextMeshProUGUI)component;
                textComponent.text = value;
                if (color.HasValue)
                {
                    textComponent.color = color.Value;
                }
            }
        }
    }

    protected void UpdateImageColor(UIStatDisplay.StatType statType, Color color)
    {
        if (uiDisplays.TryGetValue(statType, out var components))
        {
            foreach (var component in components)
            {
                ((Image)component).color = color;
            }
        }
    }

    public virtual void Setup(Player player, Formation.Position position, int id, FormationUI formationManager, RectTransform container)
    {
        draggable.SetInteractable(false);
        this.id = id;
        rt = GetComponent<RectTransform>();
        this.container = container;
        manager = formationManager;
        UpdateValues(position, player);
        Move(position);
        SetDisplayMode(DisplayMode.Default);
    }

    public void SetDisplayMode(DisplayMode mode)
    {
        if (defaultDisplay != null) defaultDisplay.SetActive(mode == DisplayMode.Default);
        if (physicalDisplay != null) physicalDisplay.SetActive(mode == DisplayMode.Physical);
        if (tacticalDisplay != null) tacticalDisplay.SetActive(mode == DisplayMode.Tactical);
        if (displayDisplay != null) displayDisplay.SetActive(mode == DisplayMode.Display);
    }

    public void TweenTo(Vector2 pos)
    {
        rt.DOAnchorPos(pos, 0.6f).SetEase(Ease.InOutQuad);
    }

    public virtual void Move(Formation.Position position)
    {
        rt.anchoredPosition = CoordToPos(position.Location, container.rect.size, rt.rect.size);
    }

    public void SetupInteractable()
    {
        draggable.OnDragStartAction.AddListener(OnPointerDown);
        draggable.OnDragEndAction.AddListener(OnPointerUp);
        draggable.SetInteractable(true);
    }

    public void Reassign(Formation.Position position, Player player)
    {
        TweenTo(CoordToPos(position.Location, container.rect.size, rt.rect.size));
        UpdateValues(position, player);
    }

    public RectTransform GetRect()
    {
        return rt;
    }

    public static Vector2 CoordToPos(Vector2Int coord, Vector2 rectSize, Vector2 positionSize)
    {
        return new Vector2(coord.x / 6f * (rectSize.x - positionSize.x), VerticalIncrease(coord.y, rectSize.y - positionSize.y));
    }

    static float VerticalIncrease(int y, float length)
    {
        float min = length / -2;
        float yPos = min;
        float mult = 0;

        float[] multipliers = { 0f, 0.28f, 0.49f, 0.58f, 0.79f, 1f };

        if (y < multipliers.Length && y >= 0)
        {
            mult = multipliers[y];
        }

        return min + length * mult;
    }

    public virtual void OnPointerDown()
    {
        if (DOTween.IsTweening(gameObject))
        {
            draggable.SetBeingDragged(false);
            return;
        }
      ((FormationInteractableUI)manager).PositionClicked(this);
    }

    public virtual void OnPointerUp()
    {
        ((FormationInteractableUI)manager).PositionReleased(this);
    }

    public bool IsDraggable()
    {
        return draggable.IsBeingDragged();
    }

    public DraggableUI GetDraggingUI()
    {
        return draggable;
    }

    private void OnDestroy()
    {
        DOTween.Kill(rt);
    }
}