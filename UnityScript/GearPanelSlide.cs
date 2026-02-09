using System.Collections;
using UnityEngine;

public class GearPanelSlide : MonoBehaviour
{
    public RectTransform panel;

    [Header("Auto Setup")]
    public bool useCurrentPositionAsShown = true;

    [Tooltip("ยกตำแหน่งตอนแสดงขึ้น (หน่วย UI)")]
    public float shownYOffset = 20f;     // ✅ ยกขึ้นเพิ่ม

    [Tooltip("เว้นขอบล่างของจอเสมอ (กันติดขอบ)")]
    public float bottomSafeMargin = 16f; // ✅ ระยะเว้นขอบจอ

    [Tooltip("เผื่อซ่อนให้ลึกลงไปอีก")]
    public float extraHidePadding = 120f;

    [Header("Slide")]
    public float duration = 0.25f;
    public bool startHidden = true;

    float shownY;
    float hiddenY;

    Canvas _canvas;
    RectTransform _canvasRT;
    Coroutine _co;

    [ContextMenu("Recalibrate Shown Position")]
    public void RecalibrateShownNow()
    {
        if (!panel) panel = transform as RectTransform;
        shownY = panel.anchoredPosition.y; // ตั้งโชว์เป็นตำแหน่งปัจจุบัน
    }


    void Awake()
    {
        if (!panel) panel = transform as RectTransform;

        _canvas = panel.GetComponentInParent<Canvas>();
        if (!_canvas) _canvas = FindFirstObjectByType<Canvas>();
        if (_canvas) _canvasRT = _canvas.GetComponent<RectTransform>();

        float canvasH = (_canvasRT != null) ? _canvasRT.rect.height : 1080f;
        float panelH = panel.rect.height;

        // ✅ ตำแหน่งโชว์ = ตำแหน่งที่วางใน Scene + offset ที่อยากยกขึ้น
        float baseShown = useCurrentPositionAsShown ? panel.anchoredPosition.y : 0f;
        shownY = baseShown + shownYOffset;

        // ✅ กันไม่ให้ติดขอบล่าง: บังคับให้ "ขอบล่างของ panel" สูงกว่าขอบล่างจอ + margin
        // panel bottom = shownY - panelH/2
        // canvas bottom = -canvasH/2
        float minShownY = (-canvasH * 0.5f) + (panelH * 0.5f) + bottomSafeMargin;
        if (shownY < minShownY) shownY = minShownY;

        // ✅ ซ่อน: เลื่อนลงให้พ้นจอแน่นอน
        hiddenY = shownY - (canvasH * 0.5f + panelH * 0.5f + extraHidePadding);

        // ตั้งตำแหน่งเริ่มต้น
        SetY(startHidden ? hiddenY : shownY);
    }

    public void Show() => SlideTo(shownY);
    public void Hide() => SlideTo(hiddenY);

    public void ShowImmediate() => SetY(shownY);
    public void HideImmediate() => SetY(hiddenY);

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
