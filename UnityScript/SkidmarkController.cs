using UnityEngine;

public class SkidmarkController : MonoBehaviour
{
    [Header("Refs")]
    public WheelColliderCarController car;   // ลากตัวรถที่มีสคริปต์นี้มาใส่
    public Rigidbody rb;                     // ลาก Rigidbody รถมาใส่ (หรือปล่อยว่างก็ได้)
    public TrailRenderer[] wheelTrails;      // 4 อัน: FL, FR, RL, RR
    public WheelCollider[] wheelColliders;   // 4 อัน: FL, FR, RL, RR

    [Header("When to draw skid")]
    public float minSpeedKmh = 25f;          // วิ่งเร็วกว่าเท่านี้ค่อยมีรอย
    public float slipThreshold = 0.35f;      // slip มากกว่าเท่านี้ถือว่าลื่น/ล็อก
    public KeyCode brakeKey = KeyCode.Space; // ให้ตรงกับรถคุณ

    [Header("Anti z-fight")]
    public float groundOffset = 0.02f;       // ยก trail ขึ้นเล็กน้อยไม่ให้จมพื้น

    void Reset()
    {
        rb = GetComponentInParent<Rigidbody>();
    }

    void LateUpdate()
    {
        if (car == null) return;

        bool braking = Input.GetKey(brakeKey);
        float speedKmh = GetSpeedKmh();

        for (int i = 0; i < wheelTrails.Length; i++)
        {
            var tr = wheelTrails[i];
            var wc = (wheelColliders != null && i < wheelColliders.Length) ? wheelColliders[i] : null;
            if (!tr || !wc) continue;

            bool grounded = wc.GetGroundHit(out WheelHit hit);

            // เงื่อนไขเกิดรอย: เบรก + วิ่งเร็ว + ล้อแตะพื้น + slip เยอะ
            bool skid = false;

            if (braking && speedKmh >= minSpeedKmh && grounded)
            {
                float slip = Mathf.Max(Mathf.Abs(hit.forwardSlip), Mathf.Abs(hit.sidewaysSlip));
                skid = slip >= slipThreshold;
            }

            // เปิด/ปิดการลากรอย
            tr.emitting = skid;

            // วางตำแหน่ง trail ให้อยู่ที่จุดสัมผัสพื้นจริง (จะเนียนมาก)
            if (grounded)
            {
                Vector3 p = hit.point + hit.normal * groundOffset;
                tr.transform.position = p;
                tr.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, hit.normal), hit.normal);
            }
        }
    }

    float GetSpeedKmh()
    {
        if (rb == null)
        {
            rb = car.GetComponent<Rigidbody>();
            if (rb == null) return 0f;
        }

#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity.magnitude * 3.6f;
#else
        return rb.velocity.magnitude * 3.6f;
#endif
    }
}
