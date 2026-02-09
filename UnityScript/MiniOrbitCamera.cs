using UnityEngine;

[RequireComponent(typeof(Camera))]
public class MiniOrbitCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("UI Gate (คุมได้เฉพาะบน MiniView)")]
    public MiniViewPointer miniViewPointer;
    public bool requireLeftMouseHold = true;

    [Header("Orbit Speed (ปรับให้ไว/หมุนได้เยอะขึ้น)")]
    public float yawSpeed = 300f;      // เดิม 120 -> เพิ่มให้ไว
    public float pitchSpeed = 220f;    // เดิม 80 -> เพิ่มให้ไว
    public float minPitch = -15f;
    public float maxPitch = 55f;
    public bool invertY = false;

    [Header("Distance / Zoom")]
    public float distance = 8f;              // ระยะเริ่มต้น
    public float minDistance = 6.5f;         // ซูมเข้าได้แค่นี้
    public float maxDistance = 10.0f;        // ซูมออกได้แค่นี้
    public float zoomSpeed = 6.0f;           // ความไวล้อเมาส์
    public float zoomSmoothTime = 0.12f;     // ความนุ่มของซูม

    [Header("Height")]
    public float height = 2.5f;

    [Header("Follow / Smooth")]
    public float followSmooth = 16f;          // เพิ่มความหนึบ
    public Vector3 lookOffset = new Vector3(0f, 1.2f, 0f);

    private float yaw;
    private float pitch = 18f;

    private float currentDistance;
    private float distanceVel;

    void Start()
    {
        if (target != null)
            yaw = target.eulerAngles.y + 150f; // ✅ หันมาจากด้านหน้าซ้าย

        currentDistance = Mathf.Clamp(distance, minDistance, maxDistance);
    }

    void LateUpdate()
    {
        if (target == null) return;

        bool pointerOverMini = (miniViewPointer != null && miniViewPointer.IsPointerOver);

        // คุมได้เฉพาะใน mini view
        bool allowControl = pointerOverMini;

        // หมุนต้องคลิกซ้ายค้าง (ถ้าเปิดไว้)
        bool allowRotate = allowControl && (!requireLeftMouseHold || Input.GetMouseButton(0));

        // --------- Rotate ----------
        if (allowRotate)
        {
            float mx = Input.GetAxis("Mouse X");
            float my = Input.GetAxis("Mouse Y");

            // คูณเพิ่มนิดให้ “ขยับได้มากกว่าเดิม” ต่อการลากหนึ่งครั้ง
            yaw += mx * yawSpeed * Time.deltaTime;

            float yDir = invertY ? 1f : -1f;
            pitch += my * pitchSpeed * Time.deltaTime * yDir;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        // --------- Zoom (mouse wheel) ----------
        if (allowControl)
        {
            float scroll = Input.mouseScrollDelta.y; // ล้อเมาส์
            if (Mathf.Abs(scroll) > 0.0001f)
            {
                float targetDist = currentDistance - scroll * zoomSpeed;
                currentDistance = Mathf.Clamp(targetDist, minDistance, maxDistance);
            }
        }

        // ทำซูมให้ “นุ่ม” (ไม่กระชาก)
        float smoothDist = Mathf.SmoothDamp(GetCurrentDistance(), currentDistance, ref distanceVel, zoomSmoothTime);
        SetCurrentDistance(smoothDist);

        // --------- Orbit Position ----------
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 offset = rot * new Vector3(0f, 0f, -GetCurrentDistance());
        Vector3 desiredPos = target.position + new Vector3(0f, height, 0f) + offset;

        // Smooth follow (แบบ exponential)
        transform.position = Vector3.Lerp(transform.position, desiredPos, 1f - Mathf.Exp(-followSmooth * Time.deltaTime));

        // Look at
        Vector3 lookPoint = target.position + lookOffset;
        transform.rotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
    }

    // ------- distance smoothing helper -------
    private float _smoothedDistance;
    private float GetCurrentDistance() => (_smoothedDistance <= 0f) ? currentDistance : _smoothedDistance;
    private void SetCurrentDistance(float d) => _smoothedDistance = Mathf.Clamp(d, minDistance, maxDistance);
}
