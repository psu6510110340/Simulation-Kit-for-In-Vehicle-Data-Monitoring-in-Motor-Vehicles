using System.Collections;
using UnityEngine;

public class CameraViewSwitcher : MonoBehaviour
{
    [Header("Main Camera (ตัวที่ render จริง และจะขยับ)")]
    public Camera thirdPersonCamera;   // Main Camera

    [Header("In-Car Anchor (จุดอ้างอิงในรถ)")]
    public Camera inCarCamera;         // CameraInCar (เป็นลูกของรถ)

    [Header("Third-person follow script (ถ้ามี)")]
    public ThirdPersonCarCamera thirdFollow;

    [Header("Start View")]
    public bool startInCar = false;

    [Header("Smooth Transition")]
    public float duration = 0.55f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public bool blendFov = true;

    [Header("In-Car Follow")]
    public bool followInCarEveryFrame = true;

    bool _isInCar;
    bool _desiredInCar;
    bool _transitioning;
    Coroutine _routine;

    void Awake()
    {
        if (!thirdPersonCamera) thirdPersonCamera = Camera.main;
    }

    void Start()
    {
        // Anchor ไม่ต้อง render
        if (inCarCamera) inCarCamera.enabled = false;
        DisableAudioListenerIfAny(inCarCamera);

        _isInCar = startInCar;
        _desiredInCar = startInCar;

        ApplyStateImmediate(_isInCar);

        // ถ้าเริ่มในรถ ให้ snap เลย
        if (_isInCar) SnapToInCar();
    }

    void LateUpdate()
    {
        // ✅ อยู่ในรถแล้วให้กล้องหลักตาม anchor ทุกเฟรม
        if (!_transitioning && followInCarEveryFrame && _isInCar && thirdPersonCamera && inCarCamera)
        {
            thirdPersonCamera.transform.SetPositionAndRotation(
                inCarCamera.transform.position,
                inCarCamera.transform.rotation
            );

            // ให้ FOV ตาม anchor
            thirdPersonCamera.fieldOfView = inCarCamera.fieldOfView;
        }
    }

    // เรียกจากปุ่ม UI เท่านั้น
    public void ToggleView()
    {
        _desiredInCar = !_desiredInCar;

        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(SmoothSwitch(_desiredInCar));
    }

    IEnumerator SmoothSwitch(bool toInCar)
    {
        if (!thirdPersonCamera) yield break;
        if (toInCar && !inCarCamera) yield break;

        _transitioning = true;

        // ปิด follow ระหว่างวิ่ง กันมันเขียนทับตำแหน่ง
        if (thirdFollow) thirdFollow.enabled = false;

        Vector3 startPos = thirdPersonCamera.transform.position;
        Quaternion startRot = thirdPersonCamera.transform.rotation;
        float startFov = thirdPersonCamera.fieldOfView;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            float k = ease != null ? ease.Evaluate(u) : u;

            // ✅ อัปเดตปลายทางทุกเฟรม (สำคัญมากถ้ารถกำลังวิ่ง)
            Vector3 endPos;
            Quaternion endRot;
            float endFov;

            if (toInCar)
            {
                endPos = inCarCamera.transform.position;
                endRot = inCarCamera.transform.rotation;
                endFov = inCarCamera.fieldOfView;
            }
            else
            {
                if (thirdFollow)
                {
                    thirdFollow.GetDesiredPose(out endPos, out endRot);
                }
                else
                {
                    endPos = thirdPersonCamera.transform.position;
                    endRot = thirdPersonCamera.transform.rotation;
                }
                endFov = thirdPersonCamera.fieldOfView;
            }

            thirdPersonCamera.transform.position = Vector3.Lerp(startPos, endPos, k);
            thirdPersonCamera.transform.rotation = Quaternion.Slerp(startRot, endRot, k);

            if (blendFov)
                thirdPersonCamera.fieldOfView = Mathf.Lerp(startFov, endFov, k);

            yield return null;
        }

        _isInCar = toInCar;
        ApplyStateImmediate(_isInCar);

        if (_isInCar) SnapToInCar(); // จบแล้ว snap กันคลาด
        _transitioning = false;
    }

    void SnapToInCar()
    {
        if (!thirdPersonCamera || !inCarCamera) return;

        thirdPersonCamera.transform.SetPositionAndRotation(
            inCarCamera.transform.position,
            inCarCamera.transform.rotation
        );
        thirdPersonCamera.fieldOfView = inCarCamera.fieldOfView;
    }

    void ApplyStateImmediate(bool inCar)
    {
        if (thirdPersonCamera) thirdPersonCamera.enabled = true;

        // เปิด thirdFollow เฉพาะตอนอยู่นอกรถ
        if (thirdFollow)
        {
            if (!inCar)
            {
                thirdFollow.SnapInstant(); // กันกระชากตอนกลับ 3rd
                thirdFollow.enabled = true;
            }
            else
            {
                thirdFollow.enabled = false;
            }
        }

        EnableAudioListener(thirdPersonCamera, true);
        DisableAudioListenerIfAny(inCarCamera);
    }

    void EnableAudioListener(Camera cam, bool enabled)
    {
        if (!cam) return;
        var al = cam.GetComponent<AudioListener>();
        if (al) al.enabled = enabled;
    }

    void DisableAudioListenerIfAny(Camera cam)
    {
        if (!cam) return;
        var al = cam.GetComponent<AudioListener>();
        if (al) al.enabled = false;
    }
}
