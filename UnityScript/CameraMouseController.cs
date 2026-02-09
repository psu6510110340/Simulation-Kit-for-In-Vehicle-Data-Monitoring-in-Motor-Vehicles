using UnityEngine;

public class CameraMouseController : MonoBehaviour
{
    [Header("Target (optional)")]
    public Transform pivotTarget;        // หมุนรอบวัตถุ (ถ้าใส่)
    public float orbitDistance = 12f;    // ระยะเริ่มต้นมาตรฐาน

    [Header("Default Speeds (System-like)")]
    public float rotateSpeed = 1.0f;     // RMB
    public float panSpeed = 0.01f;       // LMB
    public float zoomSpeed = 10f;        // Scroll

    [Header("Limits")]
    public float minPitch = -75f;
    public float maxPitch = 75f;
    public float minDistance = 5f;
    public float maxDistance = 30f;

    float _yaw;
    float _pitch;
    Vector3 _lastMousePos;

    void Start()
    {
        Vector3 euler = transform.rotation.eulerAngles;
        _yaw = euler.y;
        _pitch = euler.x;

        if (pivotTarget != null)
        {
            orbitDistance = Vector3.Distance(transform.position, pivotTarget.position);
            orbitDistance = Mathf.Clamp(orbitDistance, minDistance, maxDistance);
        }
    }

    void Update()
    {
        // บันทึกตำแหน่งเมาส์ตอนเริ่มลาก
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            _lastMousePos = Input.mousePosition;

        // ---------- RMB : Rotate ----------
        if (Input.GetMouseButton(1))
        {
            Vector3 delta = Input.mousePosition - _lastMousePos;
            _lastMousePos = Input.mousePosition;

            _yaw += delta.x * rotateSpeed * 0.2f;
            _pitch -= delta.y * rotateSpeed * 0.2f;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            UpdateOrbit();
        }

        // ---------- LMB : Pan ----------
        if (Input.GetMouseButton(0))
        {
            Vector3 delta = Input.mousePosition - _lastMousePos;
            _lastMousePos = Input.mousePosition;

            Vector3 move =
                (-transform.right * delta.x
                 - transform.up * delta.y) * panSpeed;

            if (pivotTarget != null)
            {
                pivotTarget.position += move;
                UpdateOrbit();
            }
            else
            {
                transform.position += move;
            }
        }

        // ---------- Scroll : Zoom ----------
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            if (pivotTarget != null)
            {
                orbitDistance -= scroll * zoomSpeed;
                orbitDistance = Mathf.Clamp(orbitDistance, minDistance, maxDistance);
                UpdateOrbit();
            }
            else
            {
                transform.position += transform.forward * scroll * zoomSpeed;
            }
        }
    }

    void UpdateOrbit()
    {
        if (pivotTarget == null) return;
        transform.position = pivotTarget.position - transform.forward * orbitDistance;
    }
}
