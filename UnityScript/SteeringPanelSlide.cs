using System.Collections;
using UnityEngine;

public class SteeringPanelSlide : MonoBehaviour
{
    [Header("Target Panel")]
    public RectTransform panel;          // SteeringPanel

    [Header("Positions (anchored Y)")]
    public float shownY = 0f;            // เวลาโชว์
    public float hiddenY = -450f;        // เวลาไม่โชว์ (ปรับตามความสูง panel)

    [Header("Animation")]
    public float duration = 0.25f;
    public bool startHidden = true;

    Coroutine _co;

    void Awake()
    {
        if (!panel) panel = transform as RectTransform;

        if (startHidden)
            SetY(hiddenY);
        else
            SetY(shownY);
    }

    public void Show()
    {
        SlideTo(shownY);
    }

    public void Hide()
    {
        SlideTo(hiddenY);
    }

    void SlideTo(float targetY)
    {
        if (!panel) return;

        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoSlide(targetY));
    }

    IEnumerator CoSlide(float targetY)
    {
        float startY = panel.anchoredPosition.y;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, duration);
            float y = Mathf.Lerp(startY, targetY, EaseOutCubic(t));
            SetY(y);
            yield return null;
        }

        SetY(targetY);
        _co = null;
    }

    void SetY(float y)
    {
        var p = panel.anchoredPosition;
        p.y = y;
        panel.anchoredPosition = p;
    }

    float EaseOutCubic(float x)
    {
        x = Mathf.Clamp01(x);
        return 1f - Mathf.Pow(1f - x, 3f);
    }
}
