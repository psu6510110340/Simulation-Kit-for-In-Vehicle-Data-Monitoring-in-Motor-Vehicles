using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

public class SplineWalker : MonoBehaviour
{
    [Header("Spline")]
    [SerializeField] private SplineContainer spline;
    public float speed = 1.5f;
    public bool loop = true;

    [Header("Orientation")]
    public bool faceForward = true;
    public Vector3 upAxis = Vector3.up;

    [Header("Offset")]
    public float heightOffset = 0f;
    public Vector3 localOffset = Vector3.zero;

    [Header("Animation (Optional)")]
    public Animator animator;
    public string speedParam = "Speed";
    public float animSpeedValue = 1f;

    [Range(0f, 1f)] public float t;   // เปิดให้ตั้งค่าเริ่มต้นได้
    float splineLength = 1f;

    public void SetSpline(SplineContainer newSpline, bool recalc = true)
    {
        spline = newSpline;
        if (recalc) RecalcLength();
    }

    public void SetNormalizedT(float newT)
    {
        t = Mathf.Repeat(newT, 1f);
    }

    void OnEnable() => RecalcLength();
    void Start() => RecalcLength();

    void Update()
    {
        if (spline == null || spline.Spline == null) return;
        if (splineLength <= 0.0001f) RecalcLength();

        float dt = Time.deltaTime;
        float tDelta = (speed / Mathf.Max(0.0001f, splineLength)) * dt;
        t += tDelta;

        if (loop) t = Mathf.Repeat(t, 1f);
        else t = Mathf.Clamp01(t);

        float3 pLocal = SplineUtility.EvaluatePosition(spline.Spline, t);
        float3 tanLocal = SplineUtility.EvaluateTangent(spline.Spline, t);

        Vector3 posWorld = spline.transform.TransformPoint((Vector3)pLocal);
        Vector3 tanWorld = spline.transform.TransformDirection((Vector3)tanLocal);

        posWorld += upAxis.normalized * heightOffset;
        transform.position = posWorld;

        if (faceForward && tanWorld.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(tanWorld.normalized, upAxis.normalized);
        }

        if (localOffset != Vector3.zero)
        {
            transform.position += transform.right * localOffset.x
                                + transform.up * localOffset.y
                                + transform.forward * localOffset.z;
        }

        if (animator != null && !string.IsNullOrEmpty(speedParam))
            animator.SetFloat(speedParam, animSpeedValue);
    }

    [ContextMenu("Recalc Spline Length")]
    public void RecalcLength()
    {
        if (spline == null || spline.Spline == null)
        {
            splineLength = 1f;
            return;
        }
        float4x4 m = spline.transform.localToWorldMatrix;
        splineLength = SplineUtility.CalculateLength(spline.Spline, m);
        if (splineLength <= 0.0001f) splineLength = 1f;
    }
}
