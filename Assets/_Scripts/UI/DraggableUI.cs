using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;


public class DraggableUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IBeginDragHandler, IEndDragHandler
{
    [SerializeField] private bool interactable = true;
    private RectTransform rt;
    private Vector2 offset;
    private bool dragging = false;

    [HideInInspector] public UnityEvent OnDragStartAction;
    [HideInInspector] public UnityEvent OnDragAction;
    [HideInInspector] public UnityEvent OnDragEndAction;

    private protected void Awake()
    {
        rt = GetComponent<RectTransform>();
        if (rt == null)
        {
            Debug.LogError("DraggableUI requires a RectTransform component.");
        }

        if(this.GetType() == typeof(DraggableWithLinkUI))
        {
            (this as DraggableWithLinkUI).Setup();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!interactable) return;

        RectTransform parentRectTransform = rt.parent as RectTransform;
        RectTransformUtility.ScreenPointToWorldPointInRectangle(parentRectTransform, eventData.position, eventData.pressEventCamera, out Vector3 localPoint);
        offset = (Vector2)rt.position - new Vector2(localPoint.x, localPoint.y);

        dragging = true;
        OnDragStartAction.Invoke();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!interactable || !dragging) return;

        OnDragAction.Invoke();

        RectTransform parentRectTransform = rt.parent as RectTransform;
        RectTransformUtility.ScreenPointToWorldPointInRectangle(parentRectTransform, eventData.position, eventData.pressEventCamera, out Vector3 localPoint);
        rt.position = new Vector2(localPoint.x, localPoint.y) + offset;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!interactable) return;

        if (dragging)
        {
            dragging = false;
            OnDragEndAction.Invoke();
        }
    }

    public bool IsBeingDragged()
    {
        return dragging;
    }

    public void SetBeingDragged(bool lowwww)
    {
        dragging = lowwww;
    }

    public void SetInteractable(bool isInteractable)
    {
        interactable = isInteractable;
    }

    public bool IsInteractable()
    {
        return interactable;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        
    }
}
