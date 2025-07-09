using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class DOTweenLayoutAnimator : MonoBehaviour
{
    [Header("Tween Settings")]
    public float tweenDuration = 0.3f;
    public Ease ease = Ease.OutCubic;

    private RectTransform content;

    private void Awake()
    {
        content = GetComponent<RectTransform>();
    }

    public void AnimateLayout()
    {
        // Step 1: Capture initial positions
        Dictionary<RectTransform, Vector2> startPositions = new();

        for (int i = 0; i < content.childCount; i++)
        {
            RectTransform child = content.GetChild(i) as RectTransform;
            if (child != null)
                startPositions[child] = child.anchoredPosition;
        }

        // Step 2: Force layout update
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        // Step 3: Animate from start to new positions
        foreach (var kvp in startPositions)
        {
            RectTransform child = kvp.Key;
            Vector2 startPos = kvp.Value;
            Vector2 endPos = child.anchoredPosition;

            // Immediately set back to start, then tween to new
            child.anchoredPosition = startPos;
            child.DOAnchorPos(endPos, tweenDuration).SetEase(ease);
        }
    }
}
