using UnityEngine;
using UnityEngine.UI;

public class AcceleratorPreviewOnly : MonoBehaviour
{
    [Header("Menu name must match SidePanelSimpleList item")]
    public string acceleratorMenuName = "Accelerator";

    [Header("UI")]
    public RawImage acceleratorRawImage;

    [Header("Preview Camera")]
    public Camera acceleratorPreviewCamera;

    [Header("3D Prefab")]
    public GameObject acceleratorPedalPrefab;
    public Transform spawnPoint;

    [Header("Behavior")]
    public bool hideWhenNotSelected = true;

    GameObject _spawned;

    void Awake()
    {
        // ถ้าติดไว้บน RawImage ตัวเดียว ก็ auto-assign ให้
        if (!acceleratorRawImage) acceleratorRawImage = GetComponent<RawImage>();
    }

    void Start()
    {
        // เริ่มต้นให้ซ่อนก่อน (เหมือน brake)
        SetVisible(false);
    }

    void OnEnable()
    {
        // กันกรณีเปิดวัตถุทีหลัง
        SetVisible(false);
    }

    void Update()
    {
        bool selected = false;

        // ใช้ gate ตัวเดิมที่โปรเจกต์คุณมี
        if (PanelSelectionGate.Instance != null)
            selected = PanelSelectionGate.Instance.IsSelected(acceleratorMenuName);

        if (selected) SetVisible(true);
        else if (hideWhenNotSelected) SetVisible(false);
    }

    void SetVisible(bool visible)
    {
        if (acceleratorRawImage) acceleratorRawImage.enabled = visible;
        if (acceleratorPreviewCamera) acceleratorPreviewCamera.enabled = visible;

        if (visible)
        {
            if (_spawned == null && acceleratorPedalPrefab && spawnPoint)
            {
                _spawned = Instantiate(acceleratorPedalPrefab, spawnPoint.position, spawnPoint.rotation, spawnPoint);
            }
        }
        else
        {
            if (_spawned != null)
            {
                Destroy(_spawned);
                _spawned = null;
            }
        }
    }
}
