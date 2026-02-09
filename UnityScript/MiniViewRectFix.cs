using UnityEngine;

[ExecuteAlways]
public class MiniViewRectFix : MonoBehaviour
{
    public Vector2 size = new Vector2(264, 264);
    public Vector2 offset = new Vector2(20, -20);

    void OnEnable()
    {
        var rt = GetComponent<RectTransform>();
        if (!rt) return;

        // Top-left anchor
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);

        // Pivot top-left
        rt.pivot = new Vector2(0f, 1f);

        rt.anchoredPosition = offset;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
    }
}
