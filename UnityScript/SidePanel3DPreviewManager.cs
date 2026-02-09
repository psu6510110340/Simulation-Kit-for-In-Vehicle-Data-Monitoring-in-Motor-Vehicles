using UnityEngine;
using UnityEngine.UI;

public class SidePanel3DPreviewManager : MonoBehaviour
{
    [Header("UI")]
    public RawImage previewRawImage;          // SteeringPreviewRawImage
    public RenderTexture previewTexture;      // RT_SteeringPreview

    [Header("Slide Panel")]
    public SteeringPanelSlide steeringPanelSlide; // SteeringPanel ที่มี SteeringPanelSlide

    [Header("Preview Camera")]
    public Camera previewCamera;

    [Header("3D Prefab / Instance")]
    public GameObject steeringWheelPrefab;    // Prefab SteeringWheel
    public Transform spawnPoint;              // SteeringSpawn (แนะนำให้เป็นลูกของ PreviewCamera)

    [Header("Steering Controller (optional)")]
    public SteeringWheelController steeringController; // ตัวที่รับ input A/D + Send

    [Header("Options")]
    public bool spawnOnAwake = false;         // จะสร้าง clone ตั้งแต่เริ่มหรือไม่
    public bool keepSpawnRotation = true;     // ใช้ rotation ของ spawnPoint เป็นฐาน
    public bool forceShowOnStart = false;     // debug: ให้โชว์ steering ตอนเริ่ม

    [Header("Car Controller (Steering Source Switch)")]
    public WheelColliderCarController carController;

    GameObject _steeringInstance;

    void Awake()
    {
        // ผูก RT ให้ UI/Camera
        if (previewRawImage && previewTexture)
            previewRawImage.texture = previewTexture;

        if (previewCamera && previewTexture)
            previewCamera.targetTexture = previewTexture;

        // หา controller อัตโนมัติถ้าไม่ใส่
        if (!steeringController)
            steeringController = FindAnyObjectByType<SteeringWheelController>();

        // ซ่อนเริ่มต้น + ปิด input
        HideAll();

        // ถ้าต้องการสร้างไว้ก่อน
        if (spawnOnAwake)
            EnsureSteeringInstance();

        // debug
        if (forceShowOnStart)
            ShowByName("Steering");

        if (!carController)
            carController = FindAnyObjectByType<WheelColliderCarController>();
    }

    /// <summary>
    /// เรียกจากปุ่มเมนู Side Panel
    /// </summary>
    public void ShowByName(string itemName)
    {
        if (itemName == "Steering")
        {
            ShowSteeringWheel();
        }
        else
        {
            HideAll();
        }
    }

    void ShowSteeringWheel()
    {
        EnsureSteeringInstance();

        if (previewRawImage) previewRawImage.enabled = true;
        if (previewCamera) previewCamera.enabled = true;

        // ✅ สไลด์ขึ้น
        if (steeringPanelSlide) steeringPanelSlide.Show();

        // ✅ เปิดให้ควบคุมได้เฉพาะตอนโชว์
        if (steeringController) steeringController.SetInputEnabled(true);

        if (carController)
        {
            carController.useSteeringWheel = true;
            carController.steeringWheelController = steeringController; // ชี้ตัวเดียวกัน
        }
    }

    public void HideAll()
    {
        if (previewRawImage) previewRawImage.enabled = false;
        if (previewCamera) previewCamera.enabled = false;

        // ✅ สไลด์ลง
        if (steeringPanelSlide) steeringPanelSlide.Hide();

        // ✅ ปิดการควบคุมเมื่อซ่อน
        if (steeringController) steeringController.SetInputEnabled(false);

        if (carController)
        {
            carController.useSteeringWheel = false;
        }
    }

    void EnsureSteeringInstance()
    {
        if (_steeringInstance) return;

        if (!steeringWheelPrefab || !spawnPoint)
        {
            Debug.LogError("PreviewManager: steeringWheelPrefab/spawnPoint not assigned");
            return;
        }

        // Instantiate ใต้ spawnPoint
        _steeringInstance = Instantiate(
            steeringWheelPrefab,
            spawnPoint.position,
            spawnPoint.rotation,
            spawnPoint
        );

        _steeringInstance.transform.localPosition = Vector3.zero;

        // ✅ ไม่ทับ rotation ของ spawnPoint (local identity = เท่ากับ spawnPoint.rotation)
        if (keepSpawnRotation)
            _steeringInstance.transform.localRotation = Quaternion.identity;
        else
            _steeringInstance.transform.rotation = steeringWheelPrefab.transform.rotation;

        // ให้ controller ไปคุม clone ที่กล้องถ่าย
        if (!steeringController)
            steeringController = FindFirstObjectByType<SteeringWheelController>();

        if (steeringController)
        {
            steeringController.SetSteeringWheel(_steeringInstance.transform);

            // เริ่มต้นให้ “ปิด input” ไว้ก่อน (เพราะยังซ่อนอยู่)
            steeringController.SetInputEnabled(false);
        }
        else
        {
            Debug.LogWarning("PreviewManager: SteeringWheelController not found, preview wheel will not rotate by input.");
        }
    }

    // เผื่ออยากรีเซ็ต
    public void DestroySteeringInstance()
    {
        if (_steeringInstance)
        {
            Destroy(_steeringInstance);
            _steeringInstance = null;
        }
    }

    [ContextMenu("DEBUG: Show Steering")]
    void DebugShowSteering() => ShowByName("Steering");

    [ContextMenu("DEBUG: Hide All")]
    void DebugHideAll() => HideAll();
}
