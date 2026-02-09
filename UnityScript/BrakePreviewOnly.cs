using UnityEngine;
using UnityEngine.UI;

public class BrakePreviewOnly : MonoBehaviour
{
    [Header("Menu name must match SidePanelSimpleList item")]
    public string brakeMenuName = "Break"; // <- สำคัญ: ตอนนี้ของคุณคือ "Break"

    [Header("UI")]
    public RawImage brakeRawImage;

    [Header("Preview Camera")]
    public Camera brakePreviewCamera;

    [Header("3D Prefab")]
    public GameObject brakePedalPrefab;
    public Transform spawnPoint; // ใช้ SteeringSpawn ตัวเดิมได้ (ตำแหน่งเดียวกับพวงมาลัย)

    GameObject _instance;
    bool _isShown;

    void Awake()
    {
        SetVisible(false);
    }

    void Update()
    {
        // อ่านสถานะจากระบบเดิมของคุณ (ไม่ต้องแก้ SidePanelSimpleList)
        bool shouldShow = false;

        if (PanelSelectionGate.Instance != null)
        {
            // สมมติว่าคุณมีเมธอด IsSelected(name)
            // ถ้าไม่มี ให้บอกผมชื่อเมธอดใน PanelSelectionGate ของคุณ แล้วผมจะปรับให้ตรง
            shouldShow = PanelSelectionGate.Instance.IsSelected(brakeMenuName);
        }

        if (shouldShow != _isShown)
        {
            SetVisible(shouldShow);
        }
    }

    void SetVisible(bool on)
    {
        _isShown = on;

        if (on)
        {
            if (_instance == null)
                Spawn();
        }

        if (brakeRawImage) brakeRawImage.enabled = on;
        if (brakePreviewCamera) brakePreviewCamera.enabled = on;

        if (!on && _instance != null)
        {
            Destroy(_instance);
            _instance = null;
        }
    }

    void Spawn()
    {
        if (!brakePedalPrefab || !spawnPoint)
        {
            Debug.LogError("BrakePreviewOnly: brakePedalPrefab/spawnPoint not assigned");
            return;
        }

        _instance = Instantiate(brakePedalPrefab, spawnPoint.position, spawnPoint.rotation, spawnPoint);
        _instance.transform.localPosition = Vector3.zero;
        _instance.transform.localRotation = Quaternion.identity;
    }
}
