using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One box in the home-screen day timeline. Dumb view — the DayTimelineWidget computes its
/// data, slot position and size; this just applies them (instantly or via DOTween).
/// </summary>
public class DayBox : MonoBehaviour
{
    [SerializeField] private RectTransform rect;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI dayLabel;
    [SerializeField] private Image icon;
    [Tooltip("Shown only on the lead (today) box — e.g. the club crest / 'HOME' label.")]
    [SerializeField] private GameObject leadExtras;

    public RectTransform Rect => rect != null ? rect : (RectTransform)transform;

    public void SetData(string label, Sprite iconSprite, bool isLead)
    {
        if (dayLabel != null) dayLabel.text = label;
        if (icon != null)
        {
            icon.sprite = iconSprite;
            icon.enabled = iconSprite != null;
        }
        if (leadExtras != null) leadExtras.SetActive(isLead);
    }

    public void SetSlotInstant(Vector2 anchoredPos, Vector2 size)
    {
        Rect.anchoredPosition = anchoredPos;
        Rect.sizeDelta = size;
        if (canvasGroup != null) canvasGroup.alpha = 1f;
    }

    public void AnimateToSlot(Vector2 anchoredPos, Vector2 size, float duration)
    {
        Rect.DOAnchorPos(anchoredPos, duration).SetEase(Ease.OutCubic);
        Rect.DOSizeDelta(size, duration).SetEase(Ease.OutCubic);
        if (canvasGroup != null) canvasGroup.DOFade(1f, duration);
    }

    public void FadeOut(float duration)
    {
        if (canvasGroup != null) canvasGroup.DOFade(0f, duration);
    }

    public void SetVisible(bool visible) => gameObject.SetActive(visible);

    public void KillTweens()
    {
        Rect.DOKill();
        if (canvasGroup != null) canvasGroup.DOKill();
    }
}
