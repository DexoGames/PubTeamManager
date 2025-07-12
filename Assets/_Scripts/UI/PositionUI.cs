using DG.Tweening;
using System.Collections.Generic;
using System.Text;
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
    [HideInInspector] public Formation.Position playerPosition;

    protected Dictionary<StatType, List<Component>> uiDisplays;

    protected RectTransform rt;
    protected RectTransform container;
    protected FormationUI manager;

    private void Awake()
    {
        uiDisplays = new Dictionary<StatType, List<Component>>();
        UIStatDisplay[] allDisplays = GetComponentsInChildren<UIStatDisplay>(true);

        foreach (UIStatDisplay display in allDisplays)
        {
            StatType statType = display.statToDisplay;

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

    public void UpdateValues(Formation.Position position, Player player)
    {
        playerPosition = position;
        this.player = player;
        name = "StartingPlayer " + player.FullName;

        UpdatePosition(position);
        UpdateSurname(player);
        UpdateBestRating(player);
        UpdateCurrentRating(player);
        UpdateKitNumber(player);
        UpdateAge(player);
        UpdateFatigue(player);
        UpdateIntelligence(player);
        UpdateMorale(player);
        UpdateOtherPositions(player);
    }

    protected virtual void UpdatePosition(Formation.Position position)
    {
        var positionId = position.ID;
        int strengthIndex = (int)player.RawStats.Positions[positionId];
        Color color = StrengthColors[strengthIndex];

        UpdateTextStat(StatType.Position, positionId.ToString(), color);
    }

    protected virtual void UpdateSurname(Player player)
    {
        string surnameLink = LinkBuilder.BuildLink(player, player.Surname);

        UpdateTextStat(StatType.Surname, surnameLink);
    }

    protected virtual void UpdateBestRating(Player player)
    {
        var rating = player.GetRating(player.BestPosition());

        UpdateImage(StatType.BestRating, null, GetRatingSprite(rating));
    }

    protected virtual void UpdateCurrentRating(Player player)
    {
        var rating = player.GetRating(playerPosition.ID);

        UpdateImage(StatType.CurrentRating, null, GetRatingSprite(rating));
    }

    protected virtual void UpdateKitNumber(Player player)
    {
        int kitNumber = player.GetKitNumber();

        UpdateTextStat(StatType.KitNumber, kitNumber.ToString());
    }

    protected virtual void UpdateAge(Player player)
    {
        int age = player.AgeYears();

        UpdateTextStat(StatType.Age, age.ToString());
    }

    protected virtual void UpdateFatigue(Player player)
    {
        float fatigueValue = player.Fatigue;
        Color fatigueColor = Color.Lerp(Color.green, Color.red, fatigueValue / 100f);

        UpdateTextStat(StatType.Fatigue, fatigueValue.ToString(), fatigueColor);
    }

    protected virtual void UpdateIntelligence(Player player)
    {
        float intelligenceValue = player.RawStats.Intelligence;
        Color intelligenceColor = Color.Lerp(Color.red, Color.green, intelligenceValue / 100f);

        UpdateTextStat(StatType.Intelligence, intelligenceValue.ToString(), intelligenceColor);
    }

    protected virtual void UpdateMorale(Player player)
    {
        Color moraleColor = player.GetMoraleColor();

        UpdateImage(StatType.Morale, moraleColor);
    }

    protected virtual void UpdateOtherPositions(Player player, bool includeBest = true)
    {
        string[] bestPositions = player.ListBestPositions().Split(' ');
        StringBuilder formatted = new StringBuilder();

        for (int i = includeBest? 0:1; i < bestPositions.Length; i++)
        {
            string pos = bestPositions[i];

            if (!string.IsNullOrEmpty(pos))
            {
                formatted.Append(pos);
                formatted.Append(' ');
            }
        }

        string trimmed = formatted.ToString().Trim();
        Color? optionalColor = null;

        if(trimmed.Length <= 1)
        {
            trimmed = "No others";
            optionalColor = Color.grey;
        }

        UpdateTextStat(StatType.OtherPositions, trimmed, optionalColor);
    }



    protected void UpdateTextStat(StatType statType, string value, Color? color = null)
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

    protected void UpdateImage(StatType statType, Color? color = null, Sprite sprite = null)
    {
        if (uiDisplays.TryGetValue(statType, out var components))
        {
            foreach (var component in components)
            {
                if(color.HasValue) ((Image)component).color = color.Value;
                if(sprite != null) ((Image)component).sprite = sprite;
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
