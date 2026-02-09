using UnityEngine;

public class AcceleratorPedalBySpeed : MonoBehaviour
{
    public enum Axis { X, Y, Z }

    [Header("References")]
    public Transform pedalPivot; // ชิ้นที่จะหมุน (Pivot/แป้น)
    public WheelColliderCarController car; // ลากรถที่มี WheelColliderCarController มาวาง

    [Header("Speed -> Pedal")]
    public float speedForFullPressKmh = 60f;   // ความเร็วที่ถือว่า "กดสุด" (ปรับได้)
    public float pressResponseSpeed = 8f;      // ความไวในการไล่ตาม (ยิ่งมากยิ่งไว)

    [Header("Rotation")]
    public Axis rotationAxis = Axis.X;
    public bool invertDirection = false;
    public float pressedAngle = 10f;     // มุมตอนกดสุด
    public float releasedAngle = 0f;    // มุมตอนปล่อย

    [Header("Optional: Small Slide For Realism")]
    public bool enableSlide = true;
    public Vector3 pressedLocalOffset = new Vector3(0f, -0.01f, 0.02f);
    public Vector3 releasedLocalOffset = Vector3.zero;

    [Tooltip("Input = normalized speed (0..1), Output = pedal press (0..1)")]
    public AnimationCurve speedToPressCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    float _current01;     // 0..1
    float _target01;

    Quaternion _initialLocalRot;
    Vector3 _initialLocalPos;

    void Awake()
    {
        if (!pedalPivot) pedalPivot = transform;

        _initialLocalRot = pedalPivot.localRotation;
        _initialLocalPos = pedalPivot.localPosition;

        // กันลืมลาก car: พยายามหาเองจาก Scene
        if (!car) car = FindFirstObjectByType<WheelColliderCarController>();

        Apply01(0f);
    }

    void Update()
    {
        float speedKmh = car ? car.SpeedKmh : 0f;

        float n = (speedForFullPressKmh <= 0.01f) ? 0f : Mathf.Clamp01(speedKmh / speedForFullPressKmh);

        // ใช้ curve ทำให้ช่วงต้นกดชัดขึ้น
        _target01 = Mathf.Clamp01(speedToPressCurve.Evaluate(n));

        _current01 = Mathf.Lerp(_current01, _target01, Time.deltaTime * pressResponseSpeed);
        Apply01(_current01);
    }

    void Apply01(float t01)
    {
        if (!pedalPivot) return;

        float angle = Mathf.Lerp(releasedAngle, pressedAngle, Mathf.Clamp01(t01));
        if (invertDirection) angle = -angle;

        Vector3 axisVec = rotationAxis switch
        {
            Axis.X => Vector3.right,
            Axis.Y => Vector3.up,
            _ => Vector3.forward
        };

        pedalPivot.localRotation = _initialLocalRot * Quaternion.AngleAxis(angle, axisVec);

        if (enableSlide)
        {
            Vector3 offset = Vector3.Lerp(releasedLocalOffset, pressedLocalOffset, Mathf.Clamp01(t01));
            pedalPivot.localPosition = _initialLocalPos + offset;
        }
        else
        {
            pedalPivot.localPosition = _initialLocalPos;
        }
    }
}
