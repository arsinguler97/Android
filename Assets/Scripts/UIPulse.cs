using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class UIPulse : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float minAlpha = 0.25f;
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 1f;
    [SerializeField] private float speed = 2f;

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Update()
    {
        float t = (Mathf.Sin(Time.unscaledTime * speed) + 1f) * 0.5f;
        canvasGroup.alpha = Mathf.Lerp(minAlpha, maxAlpha, t);
    }
}
