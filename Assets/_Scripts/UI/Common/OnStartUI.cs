using UnityEngine;
using System.Collections;

[RequireComponent(typeof(CanvasGroup))]
public class OnStartUI : MonoBehaviour
{
    [SerializeField] private float delay = 0.5f;
    private CanvasGroup canvasGroup;
    private Vector3 originalScale;

    private const float FADE_TIME = 0.26f;
    private const float START_SCALE = 0.85f;

    private Coroutine fadeCoroutine;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        originalScale = transform.localScale;
    }

    private void OnEnable()
    {
        ResetUI();
        fadeCoroutine = StartCoroutine(FadeInRoutine());
    }

    public void ResetUI()
    {
        canvasGroup.alpha = 0;
        canvasGroup.interactable = false;
        transform.localScale = originalScale * START_SCALE;

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }
    }

    private IEnumerator FadeInRoutine()
    {
        yield return new WaitForSeconds(delay);

        float elapsed = 0f;
        while (elapsed < FADE_TIME)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / FADE_TIME);

            canvasGroup.alpha = t;
            float scale = Mathf.Lerp(START_SCALE, 1f, t);
            transform.localScale = originalScale * scale;

            if (t >= 0.5f && !canvasGroup.interactable)
                canvasGroup.interactable = true;

            yield return null; // Wait one frame
        }

        SetFinalState();
    }

    public void SetFinalState()
    {
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        transform.localScale = originalScale;
    }

    public void SetDelay(float delay)
    {
        this.delay = delay;
    }
}
