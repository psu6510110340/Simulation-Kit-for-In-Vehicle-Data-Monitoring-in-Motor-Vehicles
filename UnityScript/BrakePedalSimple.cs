using UnityEngine;

public class BrakePedalSimple : MonoBehaviour
{
    public enum Axis { X, Y, Z }

    [Header("References")]
    public Transform pedalPivot; // ชิ้นที่จะหมุน (Pivot/แป้น)

    [Header("Input")]
    public KeyCode brakeKey = KeyCode.Space;

    [Header("Rotation")]
    public Axis rotationAxis = Axis.X;     // เลือกแกนหมุนให้ถูก (ลอง X/Y/Z)
    public bool invertDirection = false;   // ถ้ากดแล้ว “เงย” ให้ติ๊กอันนี้
    public float pressedAngle = 12f;       // มุมตอนเหยียบ (หน่วยองศา)
    public float releasedAngle = 0f;       // มุมตอนปล่อย
    public float speed = 12f;              // ความไวการขยับ

    [Header("Optional: Small Slide For Realism")]
    public bool enableSlide = true;
    public Vector3 pressedLocalOffset = new Vector3(0f, -0.01f, 0.02f); // เลื่อนลง+เข้าเล็กน้อย
    public Vector3 releasedLocalOffset = Vector3.zero;

    float _current;
    float _target;

    Quaternion _initialLocalRot;
    Vector3 _initialLocalPos;

    void Awake()
    {
        if (!pedalPivot) pedalPivot = transform;

        _initialLocalRot = pedalPivot.localRotation;
        _initialLocalPos = pedalPivot.localPosition;

        _current = releasedAngle;
        _target = releasedAngle;

        Apply(_current);
    }

    void Update()
    {
        _target = Input.GetKey(brakeKey) ? pressedAngle : releasedAngle;

        // smooth แบบไม่กระโดด
        _current = Mathf.Lerp(_current, _target, Time.deltaTime * speed);

        Apply(_current);
    }

    void Apply(float angle)
    {
        if (!pedalPivot) return;

        float a = invertDirection ? -angle : angle;

        Vector3 axisVec = rotationAxis switch
        {
            Axis.X => Vector3.right,
            Axis.Y => Vector3.up,
            _ => Vector3.forward
        };

        // หมุนจากค่าเริ่มต้น (กันสะสมผิดพลาด)
        pedalPivot.localRotation = _initialLocalRot * Quaternion.AngleAxis(a, axisVec);

        // เลื่อนนิด ๆ ให้เหมือน “กดลง/กดเข้า”
        if (enableSlide)
        {
            float t = Mathf.InverseLerp(releasedAngle, pressedAngle, _target == pressedAngle ? _current : (_current));
            // ป้องกันเคส pressedAngle < releasedAngle
            if (pressedAngle < releasedAngle) t = Mathf.InverseLerp(releasedAngle, pressedAngle, _current);

            Vector3 offset = Vector3.Lerp(releasedLocalOffset, pressedLocalOffset, Mathf.Clamp01(t));
            pedalPivot.localPosition = _initialLocalPos + offset;
        }
        else
        {
            pedalPivot.localPosition = _initialLocalPos;
        }
    }
}
