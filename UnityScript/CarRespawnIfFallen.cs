using UnityEngine;

public class CarRespawnIfFallen : MonoBehaviour
{
    [Header("Wheels (WheelCollider)")]
    public WheelCollider[] wheels;

    [Header("Respawn Settings")]
    public float maxAirTime = 2.5f;
    public Transform respawnPoint;

    [Header("Safety Offset")]
    public float respawnHeightOffset = 0.5f;

    private float airTimer = 0f;
    private Rigidbody rb;

    private Vector3 startPos;
    private Quaternion startRot;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        startPos = transform.position;
        startRot = transform.rotation;
    }

    void FixedUpdate()
    {
        bool isGrounded = IsAnyWheelGrounded();

        if (!isGrounded)
        {
            airTimer += Time.fixedDeltaTime;
            if (airTimer >= maxAirTime)
            {
                RespawnNow();     // ✅ ใช้ตัวเดียวกัน
                airTimer = 0f;
            }
        }
        else
        {
            airTimer = 0f;
        }
    }

    bool IsAnyWheelGrounded()
    {
        foreach (var wheel in wheels)
        {
            if (wheel != null && wheel.isGrounded)
                return true;
        }
        return false;
    }

    // ✅ ปุ่ม UI เรียกอันนี้ได้
    public void RespawnNow()
    {
        if (!rb) rb = GetComponent<Rigidbody>();

#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
#else
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
#endif

        if (respawnPoint != null)
        {
            transform.position = respawnPoint.position + Vector3.up * respawnHeightOffset;
            transform.rotation = respawnPoint.rotation;
        }
        else
        {
            transform.position = startPos + Vector3.up * respawnHeightOffset;
            transform.rotation = startRot;
        }
    }
}
