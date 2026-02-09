using UnityEngine;

public class WheelVisualAuto : MonoBehaviour
{
    [Header("Find wheel meshes by name (optional)")]
    public string frontLeftKey = "Wheel_Front_Left";
    public string frontRightKey = "Wheel_Front_Right";
    public string rearLeftKey = "Wheel_Rear_Left";
    public string rearRightKey = "Wheel_Rear_Right";

    [Header("Manual references (recommended)")]
    public Transform frontLeftMesh;
    public Transform frontRightMesh;
    public Transform rearLeftMesh;
    public Transform rearRightMesh;

    [Header("Optional front pivots (manual)")]
    public Transform frontLeftPivot;
    public Transform frontRightPivot;

    [Header("Wheel Visual")]
    public float wheelRadius = 0.33f;
    public Vector3 localSpinAxis = Vector3.right;
    public bool invertSpin = false;

    [Header("Steering Visual")]
    public Vector3 localSteerAxis = Vector3.up;
    public float maxSteerAngle = 30f;

    [Header("Speed sampling")]
    public bool deriveSpeedFromTransform = true;
    public float speedSmoothing = 12f;

    [Header("Auto steer (safe, no hierarchy edits)")]
    public bool autoSteerFromMotion = true;
    public float autoSteerSensitivity = 1.0f;

    [Header("Setup")]
    public bool findMeshesOnEnable = true;     // ✅ หา ref เฉย ๆ (ไม่ย้าย parent)
    public bool logDebug = false;

    Quaternion _flPivotBase, _frPivotBase;

    Vector3 _prevPos;
    Vector3 _velWorld;
    float _signedSpeed;
    float _steerAngle;
    float _spinAngle;

    void OnEnable()
    {
        _prevPos = transform.position;

        // ✅ ปลอดภัย: แค่หา reference ถ้ายังไม่ได้ลากใส่
        if (findMeshesOnEnable) FindMeshesIfMissing();

        // cache pivot base rotations (ถ้ามี)
        if (frontLeftPivot) _flPivotBase = frontLeftPivot.localRotation;
        if (frontRightPivot) _frPivotBase = frontRightPivot.localRotation;
    }

    // ❌ ไม่ทำอะไรใน OnValidate เพื่อกันเพี้ยน
    // void OnValidate() {}

    void FindMeshesIfMissing()
    {
        if (!frontLeftMesh) frontLeftMesh = FindChildContains(frontLeftKey);
        if (!frontRightMesh) frontRightMesh = FindChildContains(frontRightKey);
        if (!rearLeftMesh) rearLeftMesh = FindChildContains(rearLeftKey);
        if (!rearRightMesh) rearRightMesh = FindChildContains(rearRightKey);

        if (logDebug)
        {
            Debug.Log($"[WheelVisualAuto] FindMeshes | " +
                      $"FL={(frontLeftMesh ? frontLeftMesh.name : "null")} " +
                      $"FR={(frontRightMesh ? frontRightMesh.name : "null")} " +
                      $"RL={(rearLeftMesh ? rearLeftMesh.name : "null")} " +
                      $"RR={(rearRightMesh ? rearRightMesh.name : "null")}", this);
        }
    }

    Transform FindChildContains(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;

        var all = GetComponentsInChildren<Transform>(true);
        foreach (var t in all)
        {
            if (t == transform) continue;
            if (t.name.Contains(key)) return t;
        }
        return null;
    }

    void Update()
    {
        // ต้องมีล้อครบก่อน
        if (!frontLeftMesh || !frontRightMesh || !rearLeftMesh || !rearRightMesh) return;

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);

        // speed from transform delta (รองรับ spline)
        if (deriveSpeedFromTransform)
        {
            Vector3 curPos = transform.position;
            _velWorld = (curPos - _prevPos) / dt;
            _prevPos = curPos;

            float signed = Vector3.Dot(transform.forward, _velWorld);
            _signedSpeed = Mathf.Lerp(_signedSpeed, signed, 1f - Mathf.Exp(-speedSmoothing * dt));
        }

        // auto steer from motion (ไม่มีการแก้ hierarchy)
        if (autoSteerFromMotion)
        {
            Vector3 v = _velWorld;
            if (v.sqrMagnitude > 0.0001f)
            {
                Vector3 dir = v.normalized;
                float side = Vector3.Dot(transform.right, dir);
                float target = Mathf.Clamp(side * maxSteerAngle * autoSteerSensitivity, -maxSteerAngle, maxSteerAngle);
                _steerAngle = Mathf.Lerp(_steerAngle, target, 1f - Mathf.Exp(-speedSmoothing * dt));
            }
        }

        ApplyWheelVisuals(_signedSpeed, _steerAngle, dt);
    }

    public void SetSteerAngle(float steerAngleDeg)
    {
        _steerAngle = Mathf.Clamp(steerAngleDeg, -maxSteerAngle, maxSteerAngle);
        autoSteerFromMotion = false;
    }

    public void SetSignedSpeed(float signedSpeedMS)
    {
        _signedSpeed = signedSpeedMS;
        deriveSpeedFromTransform = false;
    }

    void ApplyWheelVisuals(float signedSpeedMS, float steerAngleDeg, float dt)
    {
        float r = Mathf.Max(0.001f, wheelRadius);
        float omegaDegPerSec = (signedSpeedMS / r) * Mathf.Rad2Deg;
        float spinSign = invertSpin ? -1f : 1f;

        _spinAngle += (omegaDegPerSec * spinSign) * dt;
        Quaternion spinRot = Quaternion.AngleAxis(_spinAngle, localSpinAxis.normalized);

        // หมุนล้อทั้ง 4 (หมุนเฉพาะล้อ ไม่ไปยุ่ง parent)
        frontLeftMesh.localRotation = spinRot;
        frontRightMesh.localRotation = spinRot;
        rearLeftMesh.localRotation = spinRot;
        rearRightMesh.localRotation = spinRot;

        // เลี้ยวล้อหน้า (ถ้าคุณทำ pivot เอง/ลากใส่เอง)
        if (frontLeftPivot) frontLeftPivot.localRotation = _flPivotBase * Quaternion.AngleAxis(steerAngleDeg, localSteerAxis.normalized);
        if (frontRightPivot) frontRightPivot.localRotation = _frPivotBase * Quaternion.AngleAxis(steerAngleDeg, localSteerAxis.normalized);
    }

    // ---------- Optional helper: create pivots once (manual trigger) ----------
    [ContextMenu("Create Front Pivots (one-time, safe)")]
    public void CreateFrontPivotsOnce()
    {
        FindMeshesIfMissing();

        if (frontLeftMesh) frontLeftPivot = CreatePivotFor(frontLeftMesh, "FrontLeft");
        if (frontRightMesh) frontRightPivot = CreatePivotFor(frontRightMesh, "FrontRight");

        if (frontLeftPivot) _flPivotBase = frontLeftPivot.localRotation;
        if (frontRightPivot) _frPivotBase = frontRightPivot.localRotation;
    }

    Transform CreatePivotFor(Transform wheelMesh, string key)
    {
        // ถ้าพ่อแม่เป็น pivot อยู่แล้ว ใช้เลย
        if (wheelMesh.parent != null && wheelMesh.parent.name == $"Pivot_{key}")
            return wheelMesh.parent;

        var oldParent = wheelMesh.parent;

        var pivotGO = new GameObject($"Pivot_{key}");
        var pivot = pivotGO.transform;

        // วาง pivot ตรงตำแหน่งล้อ (world)
        pivot.position = wheelMesh.position;
        pivot.rotation = wheelMesh.rotation;
        pivot.localScale = Vector3.one;

        // parent pivot ใต้ oldParent โดยคง world
        if (oldParent) pivot.SetParent(oldParent, true);

        // ย้ายล้อเข้า pivot โดยคง world (✅ ไม่ reset scale/pos)
        wheelMesh.SetParent(pivot, true);

        return pivot;
    }
}
