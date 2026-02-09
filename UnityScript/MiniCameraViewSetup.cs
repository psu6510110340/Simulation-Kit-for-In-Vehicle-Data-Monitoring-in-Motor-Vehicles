using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Camera))]
public class MiniCameraViewSetup : MonoBehaviour
{
    [Header("UI Target (RawImage)")]
    public RawImage targetRawImage;

    [Header("Auto Match UI Size")]
    public bool matchToRawImageRect = true;
    public int fallbackWidth = 512;
    public int fallbackHeight = 288;

    [Range(0.25f, 2f)] public float resolutionScale = 1f;

    private Camera cam;
    private RenderTexture rt;

    void Awake() => cam = GetComponent<Camera>();

    void OnEnable()
    {
        CreateRT();
        Apply();
    }

    void OnDisable() => ReleaseRT();

    void LateUpdate()
    {
        // ถ้า UI เปลี่ยนขนาด runtime ให้ RT ตาม (ไม่ต้องก็ได้ แต่ช่วยกันเพี้ยน)
        if (matchToRawImageRect) CreateRT();
    }

    void CreateRT()
    {
        int w = fallbackWidth;
        int h = fallbackHeight;

        if (matchToRawImageRect && targetRawImage != null)
        {
            RectTransform rtUI = targetRawImage.rectTransform;
            w = Mathf.Max(64, Mathf.RoundToInt(rtUI.rect.width));
            h = Mathf.Max(64, Mathf.RoundToInt(rtUI.rect.height));
        }

        w = Mathf.Max(64, Mathf.RoundToInt(w * resolutionScale));
        h = Mathf.Max(64, Mathf.RoundToInt(h * resolutionScale));

        if (rt != null && rt.width == w && rt.height == h) return;

        ReleaseRT();
        rt = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32);
        rt.name = "MiniCam_RT";
        rt.Create();
    }

    void Apply()
    {
        cam.targetTexture = rt;
        if (targetRawImage != null) targetRawImage.texture = rt;
    }

    void ReleaseRT()
    {
        if (cam != null) cam.targetTexture = null;
        if (rt != null)
        {
            rt.Release();
            Destroy(rt);
            rt = null;
        }
    }
}
