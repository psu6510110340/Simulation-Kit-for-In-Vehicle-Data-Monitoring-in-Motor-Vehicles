using UnityEngine;
using UnityEngine.Splines;

[RequireComponent(typeof(Rigidbody))]
public class CarSplineRunner : MonoBehaviour
{
    [Header("Spline")]
    public SplineContainer splineContainer;
    public int splineIndex = 0;
    [Range(0f, 1f)] public float t = 0f;

    [Header("Motion (Base)")]
    public float speedMetersPerSec = 6f;     // base speed
    public bool loop = true;

    [Header("Rotation")]
    public bool rotateAlongSpline = true;
    public Vector3 upAxis = Vector3.up;
    public float rotationLerp = 12f;

    [Header("Collision Slowdown (Realism)")]
    [Tooltip("ชนแล้วจะลดความเร็วเหลือ baseSpeed * factor (ยิ่งน้อยยิ่งหยุดมาก)")]
    [Range(0.05f, 1f)] public float slowFactorOnHit = 0.25f;

    [Tooltip("ความเร็วต่ำสุดหลังชน (กันหยุดสนิทจนดูแปลก)")]
    public float minSpeedAfterHit = 0.3f;

    [Tooltip("อัตราการลดความเร็ว (m/s^2) ตอนโดนชน")]
    public float decelOnHit = 12f;

    [Tooltip("อัตราเร่งกลับไปความเร็วเดิม (m/s^2) หลังชน")]
    public float accelRecover = 3.5f;

    [Tooltip("ถ้ายังชนติดอยู่ ให้จำกัดความเร็วต่ำๆ เพื่อไม่ดันรถหลัก")]
    public float contactCreepSpeed = 0.35f;

    [Tooltip("หลังชนล่าสุดภายในกี่วินาที ถือว่ายัง contact อยู่ (ช่วยตอนชนค้าง)")]
    public float contactHoldSeconds = 0.35f;

    [Header("Extra Realism when hitting Main Car in Gear P")]
    [Tooltip("ถ้ารถที่ชนมี WheelColliderCarController และอยู่เกียร์ P จะใช้ slowFactor นี้แทน")]
    [Range(0.01f, 1f)] public float slowFactorWhenMainInPark = 0.12f;

    [Tooltip("เร่งกลับช้าลงเมื่อชนรถหลักที่อยู่ P (m/s^2)")]
    public float accelRecoverWhenMainInPark = 2.0f;

    [Tooltip("ค่อยๆ ลดความเร็วหนักขึ้นเมื่อชนรถหลักที่อยู่ P (m/s^2)")]
    public float decelOnHitWhenMainInPark = 16f;

    Rigidbody _rb;
    float _splineLength;

    float _baseSpeed;
    float _currentSpeed;

    float _lastContactTime = -999f;
    bool _lastHitWasMainInPark = false;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        _rb.useGravity = false;
    }

    void Start()
    {
        CacheSplineLength();
        _baseSpeed = Mathf.Max(0f, speedMetersPerSec);
        _currentSpeed = _baseSpeed;
        SnapToSpline();
    }

    void CacheSplineLength()
    {
        if (splineContainer == null) return;
        var spline = splineContainer.Splines[splineIndex];
        _splineLength = SplineUtility.CalculateLength(spline, splineContainer.transform.localToWorldMatrix);
        if (_splineLength <= 0.0001f) _splineLength = 1f;
    }

    void SnapToSpline()
    {
        if (splineContainer == null) return;

        Vector3 pos = splineContainer.EvaluatePosition(splineIndex, t);
        _rb.position = pos;

        if (rotateAlongSpline)
        {
            Vector3 tangent = splineContainer.EvaluateTangent(splineIndex, t);
            if (tangent.sqrMagnitude > 0.0001f)
                _rb.rotation = Quaternion.LookRotation(tangent.normalized, upAxis);
        }
    }

    void FixedUpdate()
    {
        if (splineContainer == null) return;

        // sync base speed if user tweaks in inspector at runtime
        _baseSpeed = Mathf.Max(0f, speedMetersPerSec);

        float dt = Time.fixedDeltaTime;

        // ถ้ายัง contact ล่าสุดไม่นาน -> จำกัดความเร็วต่ำๆ กันดันรถหลัก
        bool inContactWindow = (Time.time - _lastContactTime) <= contactHoldSeconds;

        float targetSpeed = _baseSpeed;

        if (inContactWindow)
        {
            targetSpeed = Mathf.Min(targetSpeed, contactCreepSpeed);
        }

        // เร่ง/ลดแบบสมจริง
        float accelUp = _lastHitWasMainInPark ? accelRecoverWhenMainInPark : accelRecover;
        float accelDown = _lastHitWasMainInPark ? decelOnHitWhenMainInPark : decelOnHit;

        if (_currentSpeed < targetSpeed)
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, accelUp * dt);
        else
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, accelDown * dt);

        // move along spline using current speed
        float deltaT = (_currentSpeed * dt) / _splineLength;
        t += deltaT;

        if (t >= 1f)
        {
            if (loop) t = t - 1f;
            else t = 1f;
        }

        Vector3 pos = splineContainer.EvaluatePosition(splineIndex, t);
        _rb.MovePosition(pos);

        if (rotateAlongSpline)
        {
            Vector3 tangent = splineContainer.EvaluateTangent(splineIndex, t);
            if (tangent.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(tangent.normalized, upAxis);
                Quaternion newRot = Quaternion.Slerp(_rb.rotation, targetRot, rotationLerp * dt);
                _rb.MoveRotation(newRot);
            }
        }

        // reset flag gradually when no longer contacting
        if (!inContactWindow) _lastHitWasMainInPark = false;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision == null || collision.collider == null) return;

        Transform otherRoot = collision.collider.transform.root;
        if (otherRoot == transform.root) return;

        // เราสนใจ "ชนกับรถหลัก" (ซึ่งไม่ใช่ tag Car) หรือวัตถุอื่นๆ ที่ไม่ใช่ Car
        if (otherRoot.CompareTag("Car")) return;

        bool mainInPark = false;

        // ถ้าอีกฝั่งมี WheelColliderCarController ให้เช็คว่าอยู่ P ไหม
        var mainCtrl = otherRoot.GetComponentInChildren<WheelColliderCarController>();
        if (mainCtrl != null && mainCtrl.gearModeEnabled && mainCtrl.CurrentGearChar == 'P')
            mainInPark = true;

        ApplyHitSlowdown(mainInPark);

        // mark contact
        _lastContactTime = Time.time;
    }

    void OnCollisionStay(Collision collision)
    {
        if (collision == null || collision.collider == null) return;

        Transform otherRoot = collision.collider.transform.root;
        if (otherRoot == transform.root) return;
        if (otherRoot.CompareTag("Car")) return;

        // ยังชนอยู่ => รีเฟรช contact time เพื่อคุม creep speed ไม่ให้ดันแรง
        _lastContactTime = Time.time;
    }

    void ApplyHitSlowdown(bool mainInPark)
    {
        _lastHitWasMainInPark = mainInPark;

        float factor = mainInPark ? slowFactorWhenMainInPark : slowFactorOnHit;
        float target = Mathf.Max(minSpeedAfterHit, _baseSpeed * factor);

        // ลดทันที (แต่ไม่ให้กระชากเกินไป)
        _currentSpeed = Mathf.Min(_currentSpeed, target);
    }
}
