using UnityEngine;
using UnityEngine.Splines;

public class TrainMultiCarFollower : MonoBehaviour
{
    [Header("Spline")]
    public SplineContainer splineContainer;
    public int splineIndex = 0;

    [Header("Train Cars (Front -> Back)")]
    public Transform[] cars;

    [Header("Movement")]
    public float speed = 10f;            // units/sec
    public float carSpacing = 20f;        // ระยะจริงระหว่าง pivot ของแต่ละตู้
    public bool loop = true;

    [Range(0f, 1f)]
    public float tHead = 0f;

    private Quaternion[] lastRots;

    void Start()
    {
        if (cars == null || cars.Length == 0) return;

        lastRots = new Quaternion[cars.Length];
        for (int i = 0; i < cars.Length; i++)
            lastRots[i] = cars[i].rotation;
    }

    void Update()
    {
        if (splineContainer == null || cars == null || cars.Length == 0) return;
        if (splineIndex < 0 || splineIndex >= splineContainer.Splines.Count) return;

        float length = splineContainer.CalculateLength(splineIndex);
        if (length <= 0.0001f) return;

        // เดินหัวรถ
        tHead += (speed / length) * Time.deltaTime;
        tHead = loop ? Mathf.Repeat(tHead, 1f) : Mathf.Clamp01(tHead);

        float dt = carSpacing / length; // แปลงระยะจริงเป็นสัดส่วน t

        for (int i = 0; i < cars.Length; i++)
        {
            float tCar = tHead - dt * i;
            tCar = loop ? Mathf.Repeat(tCar, 1f) : Mathf.Clamp01(tCar);

            Vector3 pos = splineContainer.EvaluatePosition(splineIndex, tCar);
            Vector3 tan = splineContainer.EvaluateTangent(splineIndex, tCar);

            cars[i].position = pos;

            if (tan.sqrMagnitude > 1e-8f)
            {
                Quaternion rot = Quaternion.LookRotation(tan.normalized, Vector3.up);
                cars[i].rotation = rot;
                lastRots[i] = rot;
            }
            else
            {
                cars[i].rotation = lastRots[i];
            }
        }
    }
}
