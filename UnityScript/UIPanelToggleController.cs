using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class UIPanelToggleController : MonoBehaviour
{
    [Header("Panels")]
    public RectTransform sidePanel;
    public RectTransform bottomCenterPanel;

    [Header("Animation")]
    public float animationDuration = 0.3f;

    [Header("Hide Distances")]
    public float sidePanelHideOffset = 400f;      // เลื่อนออกขวา
    public float bottomPanelHideOffset = 200f;    // เลื่อนลงล่าง

    private bool isHidden = false;

    Vector2 sidePanelShownPos;
    Vector2 bottomPanelShownPos;

    void Start()
    {
        // เก็บตำแหน่งเริ่มต้น (ตำแหน่งแสดง)
        sidePanelShownPos = sidePanel.anchoredPosition;
        bottomPanelShownPos = bottomCenterPanel.anchoredPosition;
    }

    public void TogglePanels()
    {
        StopAllCoroutines();

        if (!isHidden)
        {
            // ซ่อน
            StartCoroutine(MovePanel(sidePanel,
                sidePanelShownPos,
                sidePanelShownPos + new Vector2(sidePanelHideOffset, 0)));

            StartCoroutine(MovePanel(bottomCenterPanel,
                bottomPanelShownPos,
                bottomPanelShownPos + new Vector2(0, -bottomPanelHideOffset)));
        }
        else
        {
            // แสดง
            StartCoroutine(MovePanel(sidePanel,
                sidePanel.anchoredPosition,
                sidePanelShownPos));

            StartCoroutine(MovePanel(bottomCenterPanel,
                bottomCenterPanel.anchoredPosition,
                bottomPanelShownPos));
        }

        isHidden = !isHidden;
    }

    IEnumerator MovePanel(RectTransform panel, Vector2 from, Vector2 to)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / animationDuration;
            panel.anchoredPosition = Vector2.Lerp(from, to, Mathf.SmoothStep(0, 1, t));
            yield return null;
        }
        panel.anchoredPosition = to;
    }
}
