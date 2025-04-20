using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PositionUI : MonoBehaviour
{
    public int id;
    public TextMeshProUGUI position, surname, rating;
    public Image morale, strength;
    [SerializeField] DraggableUI draggable;
    public CanvasGroup fader;
    [SerializeField] Color[] StrengthColors;
    protected RectTransform rt;
    RectTransform container;
    FormationUI manager;
    [HideInInspector] public Player player;

    [HideInInspector] public Formation.Position Position;

    public virtual void Setup(Player player, Formation.Position position, int id, FormationUI formationManager, RectTransform container)
    {
        draggable.SetInteractable(false);
        this.id = id;
        rt = GetComponent<RectTransform>();
        //rt.anchoredPosition = CoordToPos(position.Location, container.rect.size, rt.rect.size);
        //rt.DOAnchorPos(CoordToPos(position.Location, container.rect.size, rt.rect.size), 0.5f);
        this.container = container;
        manager = formationManager;
        UpdateValues(position, player);

        Move(position);
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

    public virtual void UpdateValues(Formation.Position position, Player player)
    {
        Position = position;
        this.player = player;

        this.position.text = position.ID.ToString();
        //this.position.text = player.Team.Formation.Subposition(player.GetFormationIndex());
        surname.text = LinkBuilder.BuildLink(player, player.Surname);

        this.position.color = StrengthColors[(int)player.RawStats.Positions[position.ID]];
        if (player.GetTeamIndex() < 11)
        {
            Player.PositionStrength posStrength = player.RawStats.Positions[position.ID];

            if(posStrength != Player.PositionStrength.Natural)
            {
                strength.enabled = false;
                strength.color = StrengthColors[(int)posStrength];
            }
            else
            {
                strength.enabled = false;
            }

        }
        else
        {
            strength.enabled = false;
        }
        morale.color = player.GetMoraleColor();
        rating.text = player.GetRating(Position.ID).ToString();
        name = "StartingPlayer " + player.FullName;
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

        if(y < multipliers.Length && y >= 0)
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
        //transform.SetParent(((FormationInteractableUI)manager).GetGlobalParent());
    }

    public virtual void OnPointerUp()
    {
        //transform.SetParent(container);
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