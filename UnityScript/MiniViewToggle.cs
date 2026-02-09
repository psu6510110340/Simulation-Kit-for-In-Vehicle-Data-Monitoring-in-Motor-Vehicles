using System.Collections;
using UnityEngine;

public class MiniViewToggle : MonoBehaviour
{
    [Header("Targets (UI)")]
    public RectTransform miniViewRoot;     // ✅ เปลี่ยนจาก GameObject เป็น RectTransform
    public MiniViewPointer miniViewPointer;

    [Header("Buttons")]
    public GameObject hideButtonObj;
    public GameObject showButtonObj;

    [Header("Slide Settings")]
    public float slideDuration = 0.25f;    // เวลาเลื่อน
    public float hideExtra = 30f;          // เผื่อให้หลุดจอเพิ่มอีกนิด (กันเหลือขอบ)
    public bool useUnscaledTime = true;    // กัน Time.timeScale = 0 แล้วไม่ขยับ

    private bool isShown = true;

    private Vector2 shownPos;
    private Vector2 hiddenPos;
    private Coroutine slideCo;

    void Start()
    {
        if (miniViewRoot == null)
        {
            Debug.LogError("[MiniViewToggle] miniViewRoot is NULL (assign RectTransform).");
            return;
        }

        // จำตำแหน่ง "ตอนแสดง"
        shownPos = miniViewRoot.anchoredPosition;

        // คำนวณตำแหน่ง "ตอนซ่อน" -> เลื่อนไปทางซ้าย
        float panelWidth = miniViewRoot.rect.width;
        hiddenPos = shownPos + Vector2.left * (panelWidth + hideExtra);

        ApplyImmediate(); // ให้เริ่มต้นตรงกับ isShown
    }

    public void Toggle()
    {
        if (isShown) Hide();
        else Show();
    }

    public void Show()
    {
        isShown = true;
        ApplyAnimated();
    }

    public void Hide()
    {
        isShown = false;
        ApplyAnimated();
    }

    // ---------- Apply ----------
    private void ApplyImmediate()
    {
        // ปุ่ม
        if (hideButtonObj != null) hideButtonObj.SetActive(isShown);
        if (showButtonObj != null) showButtonObj.SetActive(!isShown);

        // เลื่อนแบบทันที
        miniViewRoot.anchoredPosition = isShown ? shownPos : hiddenPos;

        // กัน pointer ค้าง
        ResetPointerIfHidden();
    }

    private void ApplyAnimated()
    {
        // ปุ่ม
        if (hideButtonObj != null) hideButtonObj.SetActive(isShown);
        if (showButtonObj != null) showButtonObj.SetActive(!isShown);

        // เลื่อนแบบนุ่ม
        if (slideCo != null) StopCoroutine(slideCo);
        Vector2 target = isShown ? shownPos : hiddenPos;
        slideCo = StartCoroutine(SlideTo(target));

        ResetPointerIfHidden();
    }

    private IEnumerator SlideTo(Vector2 targetPos)
    {
        Vector2 start = miniViewRoot.anchoredPosition;
        float t = 0f;

        while (t < 1f)
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            t += dt / Mathf.Max(0.0001f, slideDuration);

            // SmoothStep (ลื่น ๆ)
            float s = t * t * (3f - 2f * t);

            miniViewRoot.anchoredPosition = Vector2.LerpUnclamped(start, targetPos, s);
            yield return null;
        }

        miniViewRoot.anchoredPosition = targetPos;
        slideCo = null;
    }

    private void ResetPointerIfHidden()
    {
        if (!isShown && miniViewPointer != null)
        {
            // รีเซ็ตสถานะ pointer กันค้าง
            miniViewPointer.enabled = false;
            miniViewPointer.enabled = true;
        }
    }
}
