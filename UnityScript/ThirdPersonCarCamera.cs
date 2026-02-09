using UnityEngine;

public class ThirdPersonCarCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Car (optional แต่แนะนำมาก)")]
    public WheelColliderCarController car;

    [Header("Base Offset (local to target when Follow Target Rotation = true)")]
    public Vector3 baseOffset = new Vector3(0f, 3.5f, -7f);

    [Header("Zoom Out On Acceleration (W only)")]
    public float zoomOutExtra = 2.0f;
    public float zoomSmoothTime = 0.18f;

    [Header("Follow")]
    public float positionSmoothTime = 0.12f;
    public float rotationSmoothSpeed = 8f;

    [Header("Look")]
    public Vector3 lookOffset = new Vector3(0f, 1.2f, 0f);

    [Header("Options")]
    public bool followTargetRotation = true;

    [Header("Zoom rules")]
    public float minMoveSpeedKmhToZoom = 0.3f;

    private Vector3 posVelocity;
    private float zoomVelocity;
    private float currentZoomExtra;

    // ✅ ใช้สำหรับให้ CameraViewSwitcher ดึง “ปลายทางจริง” ของกล้อง 3rd
    public void GetDesiredPose(out Vector3 pos, out Quaternion rot)
    {
        if (target == null)
        {
            pos = transform.position;
            rot = transform.rotation;
            return;
        }

        Vector3 dynamicOffset = baseOffset + new Vector3(0f, 0f, -currentZoomExtra);

        pos = followTargetRotation
            ? target.TransformPoint(dynamicOffset)
            : target.position + dynamicOffset;

        Vector3 lookPoint = target.position + lookOffset;
        rot = Quaternion.LookRotation(lookPoint - pos, Vector3.up);
    }

    // ✅ เรียกตอนกลับไปมุมมองที่สาม เพื่อกัน “ช้าแล้วกระชาก”
    public void SnapInstant()
    {
        GetDesiredPose(out var p, out var r);
        transform.SetPositionAndRotation(p, r);

        posVelocity = Vector3.zero;
        zoomVelocity = 0f;
    }

    void LateUpdate()
    {
        if (target == null) return;
        if (!car) car = FindFirstObjectByType<WheelColliderCarController>();

        bool wHeld = Input.GetKey(KeyCode.W);

        float speedKmh = car ? car.SpeedKmh : 0f;
        bool movingEnough = speedKmh > minMoveSpeedKmhToZoom;

        // ✅ บล็อกซูมด้วยเกียร์ เฉพาะตอน Gear มีผลจริง (gearModeEnabled = true)
        bool blockZoomByGear = false;
        if (car && car.gearModeEnabled)
        {
            char g = car.CurrentGearChar;
            blockZoomByGear = (g == 'P' || g == 'N' || g == 'R');
        }

        float accel01 = (wHeld && movingEnough && !blockZoomByGear) ? 1f : 0f;

        float targetZoomExtra = accel01 * zoomOutExtra;
        currentZoomExtra = Mathf.SmoothDamp(
            currentZoomExtra,
            targetZoomExtra,
            ref zoomVelocity,
            zoomSmoothTime
        );

        Vector3 dynamicOffset = baseOffset + new Vector3(0f, 0f, -currentZoomExtra);

        Vector3 desiredPos = followTargetRotation
            ? target.TransformPoint(dynamicOffset)
            : target.position + dynamicOffset;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPos,
            ref posVelocity,
            positionSmoothTime
        );

        Vector3 lookPoint = target.position + lookOffset;
        Quaternion desiredRot = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRot,
            rotationSmoothSpeed * Time.deltaTime
        );
    }
}
