using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class WheelColliderCarController : MonoBehaviour
{
    [System.Serializable]
    public class Axle
    {
        public WheelCollider leftCollider;
        public WheelCollider rightCollider;

        public Transform leftMesh;
        public Transform rightMesh;

        public bool steering; // front
        public bool motor;    // driven
    }

    [Header("Axles (Front/Rear)")]
    public Axle frontAxle;
    public Axle rearAxle;

    [Header("Driving")]
    public float maxMotorTorque = 700f;
    public float maxSteerAngle = 30f;
    public float maxBrakeTorque = 3000f;

    [Header("Input Tuning")]
    [Range(0f, 0.3f)] public float throttleDeadzone = 0.08f;

    [Header("Coast / Realistic Drag (ไม่ล็อกล้อ)")]
    public float coastDrag = 0.8f;
    public float steerCoastExtraDrag = 0.6f;
    public float aeroDrag = 0.02f;

    [Header("Speed Limit (Forward)")]
    public float maxSpeedKmh = 140f;
    public float speedLimiterStrength = 6f;

    [Header("Reverse Limit (NEW)")]
    [Tooltip("ความเร็วถอยสูงสุด (เหมือนรถจริง)")]
    public float reverseMaxSpeedKmh = 18f;

    [Tooltip("สเกลแรงบิดสูงสุดตอนถอย (0.3 = ถอยอืดๆ, 1 = แรงเท่าข้างหน้า)")]
    [Range(0.1f, 1f)]
    public float reverseMaxTorqueScale = 0.45f;

    [Tooltip("ถอยใกล้ถึงเพดานแล้วค่อยๆ ลดแรงบิด (ยิ่งมาก ยิ่งนุ่ม)")]
    [Range(1f, 12f)]
    public float reverseTorqueFalloff = 6f;

    [Header("Steering Assist (Anti-Flip)")]
    public float steerAtHighSpeedFactor = 0.35f;
    public float steerFadeSpeedKmh = 60f;

    [Header("Stability")]
    public Vector3 centerOfMassOffset = new Vector3(0f, -0.6f, 0f);
    public float antiRoll = 6000f;

    [Header("Brake Realism (ABS)")]
    public bool enableABS = true;
    public float absSlipThreshold = 0.45f;
    [Range(0f, 1f)] public float absMinBrakeScale = 0.25f;

    [Header("Read Only (Inspector)")]
    [SerializeField] private float speedKmh;
    [SerializeField] private float forwardSpeedKmh;   // + เดินหน้า, - ถอย
    [SerializeField] private bool isReversing;

    [Header("Steering Source")]
    public bool useSteeringWheel = false;   // ✅ เปิด/ปิดโหมดพวงมาลัย
    public SteeringWheelController steeringWheelController;

    [Header("Gear Mode (NEW)")]
    public bool gearModeEnabled = false;
    public char CurrentGearChar = 'P';
    public float gearCreepThrottle = 0f;   // ใช้ตอน D/B
    public float gearTorqueScale = 1f;     // ใช้ลดแรงใน B

    [Header("Auto Speed Hold (Gear)")]
    public bool enableAutoHoldSpeed = true;
    public float autoSpeedD_Kmh = 12f;  // ความเร็วคงที่เกียร์ D
    public float autoSpeedB_Kmh = 7f;   // ความเร็วคงที่เกียร์ B (หน่วงกว่า)
    public float autoSpeedR_Kmh = 8f;   // ความเร็วคงที่เกียร์ R (ถอย)
    [Range(0.2f, 3f)] public float autoHoldGain = 1.2f;
    public float autoHoldEpsilonKmh = 0.3f;

    [Header("Boost (press W to accelerate)")]
    public float boostSpeedD_Kmh = 12f;  // เพิ่มจากฐานเมื่อกด W (D)
    public float boostSpeedB_Kmh = 6f;   // เพิ่มจากฐานเมื่อกด W (B หน่วงกว่า)

    [Header("Manual Accel (Hold W)")]
    [Range(0.1f, 1f)] public float bThrottleScaleWhenW = 0.75f; // B เร่งช้ากว่า D (ลดคันเร่ง)

    [Header("Parking Lock (P)")]
    [Tooltip("แรงเบรกที่ใช้ล็อกล้อเมื่ออยู่เกียร์ P (ยิ่งมาก ยิ่งล็อกแน่น แต่ถ้าสูงเกินอาจสั่น)")]
    public float parkBrakeTorque = 15000f;

    [Tooltip("ถ้าเปิด จะเพิ่มแรงหน่วงของ Rigidbody เล็กน้อยตอนอยู่ P เพื่อลดอาการสั่นจากการชน")]
    public bool addExtraDampingInPark = true;
    public float parkExtraLinearDamping = 2.0f;
    public float parkExtraAngularDamping = 1.0f;

    [Header("Hold Speed when NOT pressing W (D/B)")]
    public bool holdSpeedWhenNoW = true;
    public float minHoldSpeedKmh = 1.0f;     // ต่ำกว่านี้ถือว่า "หยุด" ไม่ต้อง hold
    [Range(0.2f, 3f)] public float holdGain = 1.2f;
    public float holdEpsilonKmh = 0.25f;
    [Range(0f, 0.5f)] public float holdMinThrottle = 0.12f; // ช่วยให้ไปถึงเป้า ไม่โดน deadzone

    [Header("External Speed Ramp (Adaptive)")]
    public bool useAdaptiveExternalRamp = true;
    public float externalRampMin = 25f;    // เดิมของคุณ ~25
    public float externalRampMax = 180f;   // เร็วสุดเวลาสั่งเป้าสูง
    public float externalRampAt255 = 180f; // เป้าหมาย 255 -> ramp ประมาณนี้

    float _latchedHoldKmh = 0f;
    bool _hasLatchedHold = false;
    char _prevGear = '\0';

    // ให้ UI เรียกดูความเร็ว
    public float SpeedKmh => speedKmh;

    // ===============================
    // External Speed Command (from UI)
    // ===============================
    [Header("External Speed Command (UI)")]
    public bool externalSpeedCommandEnabled = false;

    [Tooltip("เป้าหมายความเร็วที่สั่งจาก UI (km/h)")]
    public float externalTargetSpeedKmh = 0f;

    [Tooltip("ความเร็วในการไต่ไปหาเป้าหมาย (km/h ต่อวินาที)")]
    public float externalRampKmhPerSec = 25f;

    [Tooltip("ถ้าเปิด เมื่อกดเบรก (Space) จะยกเลิกคำสั่งความเร็วทันที")]
    public bool cancelExternalOnBrake = true;

    float _externalCurrentTargetKmh = 0f;

    public void SetExternalTargetSpeed(float targetKmh, float rampKmhPerSec = 25f)
    {
        externalSpeedCommandEnabled = true;
        externalTargetSpeedKmh = Mathf.Max(0f, targetKmh);
        externalRampKmhPerSec = Mathf.Max(0.1f, rampKmhPerSec);

        // เริ่มไต่จากความเร็วปัจจุบัน (เดินหน้าเท่านั้น)
        _externalCurrentTargetKmh = Mathf.Max(0f, forwardSpeedKmh);
    }

    public void ClearExternalTargetSpeed()
    {
        externalSpeedCommandEnabled = false;
        externalTargetSpeedKmh = 0f;
    }

    [SerializeField] private float _latchedSteer01 = 0f;
    public float steeringWheelMaxDeg = 450f;
    public float steerEpsilon = 0.0005f;

    public bool IsReversing => isReversing; // ✅ ให้สคริปไฟท้ายเรียกเช็คได้

    private Rigidbody rb;

    private float _baseLinearDamping;
    private float _baseAngularDamping;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        rb.automaticCenterOfMass = false;
        rb.centerOfMass += centerOfMassOffset;

#if UNITY_6000_0_OR_NEWER
        _baseLinearDamping = rb.linearDamping;
        _baseAngularDamping = rb.angularDamping;
#else
    _baseLinearDamping = rb.drag;
    _baseAngularDamping = rb.angularDrag;
#endif
    }

    void Update()
    {
        speedKmh = GetSpeedMS() * 3.6f;

        // ความเร็วตามแกนรถ (สำคัญต่อการรู้ว่ากำลังถอยจริงไหม)
        float forwardMS = Vector3.Dot(GetVelocity(), transform.forward);
        forwardSpeedKmh = forwardMS * 3.6f;

        // ถอยจริง = ความเร็วแกนหน้าเป็นลบ (กำลังเคลื่อนถอย)
        isReversing = forwardSpeedKmh < -0.5f; // threshold กันสั่น
    }

    public void SetGearMode(bool enabled, char gearChar, float creepThrottle, float torqueScale)
    {
        gearModeEnabled = enabled;
        CurrentGearChar = gearChar;
        gearCreepThrottle = creepThrottle;
        gearTorqueScale = Mathf.Clamp(torqueScale, 0.1f, 1f);
    }

    void FixedUpdate()
    {
        float rawThrottle = 0f;
        bool braking = Input.GetKey(KeyCode.Space);

        bool w = Input.GetKey(KeyCode.W);
        bool s = Input.GetKey(KeyCode.S);

        float fwdKmh = forwardSpeedKmh; // + เดินหน้า, - ถอย

        float HoldForward(float targetKmh)
        {
            float cur = Mathf.Max(0f, fwdKmh);
            float err = targetKmh - cur;

            if (err <= holdEpsilonKmh) return 0f;

            float ratio = targetKmh <= 0.1f ? 1f : Mathf.Clamp01(err / targetKmh);
            float t = Mathf.Clamp01(ratio * holdGain);

            // ✅ กันคันเร่งเล็กเกินจนโดน deadzone แล้วไม่ถึงเป้า
            if (t > 0f) t = Mathf.Max(t, holdMinThrottle);

            return t;
        }

        float HoldReverse(float targetKmh)
        {
            float cur = Mathf.Max(0f, -fwdKmh);
            float err = targetKmh - cur;
            if (err <= autoHoldEpsilonKmh) return 0f;
            float ratio = targetKmh <= 0.1f ? 1f : Mathf.Clamp01(err / targetKmh);
            return -Mathf.Clamp01(ratio * autoHoldGain);
        }

        if (gearModeEnabled && CurrentGearChar != _prevGear)
        {
            _prevGear = CurrentGearChar;
            _hasLatchedHold = false;
        }

        if (!gearModeEnabled)
        {
            // ✅ NEW: Allow UI external speed command even when gear mode is OFF
            if (externalSpeedCommandEnabled)
            {
                if (braking)
                {
                    rawThrottle = 0f;

                    if (cancelExternalOnBrake)
                        ClearExternalTargetSpeed();
                }
                else
                {
                    float maxAllowed = Mathf.Max(0f, maxSpeedKmh);
                    float desired = Mathf.Clamp(externalTargetSpeedKmh, 0f, maxAllowed);

                    float rampNow = externalRampKmhPerSec;

                    if (useAdaptiveExternalRamp)
                    {
                        // ยิ่ง target สูง -> ramp สูง (คุมด้วย clamp)
                        float t01 = Mathf.InverseLerp(0f, 255f, externalTargetSpeedKmh);
                        float adaptive = Mathf.Lerp(externalRampMin, externalRampAt255, t01);
                        rampNow = Mathf.Clamp(adaptive, externalRampMin, externalRampMax);
                    }

                    _externalCurrentTargetKmh = Mathf.MoveTowards(
                        _externalCurrentTargetKmh,
                        desired,
                        rampNow * Time.fixedDeltaTime
                    );

                    rawThrottle = HoldForward(_externalCurrentTargetKmh);
                }
            }
            else
            {
                // ✅ Original keyboard control
                if (w) rawThrottle += 1f;
                if (s) rawThrottle -= 1f;
            }
        }
        else
        {
            // P/N: ห้ามขยับ
            if (CurrentGearChar == 'P' || CurrentGearChar == 'N')
            {
                rawThrottle = 0f;
            }
            // R: ถอยอัตโนมัติแบบ "คงที่" + เบรกได้
            else if (CurrentGearChar == 'R')
            {
                if (braking) rawThrottle = 0f;
                else rawThrottle = enableAutoHoldSpeed ? HoldReverse(autoSpeedR_Kmh) : -0.35f;
                // ถ้าไม่ใช้ hold speed ให้ถอยช้าๆ แบบคงที่ด้วย -0.35f
            }
            else if (CurrentGearChar == 'D' || CurrentGearChar == 'B')
            {
                if (braking)
                {
                    rawThrottle = 0f;
                    _hasLatchedHold = false;

                    if (cancelExternalOnBrake && externalSpeedCommandEnabled)
                        ClearExternalTargetSpeed();
                }
                else
                {
                    // ✅ 1) ถ้ามีคำสั่งความเร็วจาก UI ให้ใช้คำสั่งนี้ก่อน (override W/autoHold)
                    if (externalSpeedCommandEnabled)
                    {
                        // ไต่ค่าเป้าหมายแบบค่อยๆ เพิ่ม
                        float maxAllowed = Mathf.Max(0f, maxSpeedKmh); // ยังโดน speed limiter ของรถอยู่
                        float desired = Mathf.Clamp(externalTargetSpeedKmh, 0f, maxAllowed);

                        _externalCurrentTargetKmh = Mathf.MoveTowards(
                            _externalCurrentTargetKmh,
                            desired,
                            externalRampKmhPerSec * Time.fixedDeltaTime
                        );

                        rawThrottle = HoldForward(_externalCurrentTargetKmh);
                        _hasLatchedHold = false;
                    }
                    else
                    {
                        // ✅ 2) โหมดเดิมของคุณ
                        bool wHeld = Input.GetKey(KeyCode.W);

                        if (wHeld)
                        {
                            rawThrottle = (CurrentGearChar == 'B') ? (1f * bThrottleScaleWhenW) : 1f;
                            _hasLatchedHold = false;
                        }
                        else
                        {
                            if (enableAutoHoldSpeed)
                            {
                                float baseTarget = (CurrentGearChar == 'B') ? autoSpeedB_Kmh : autoSpeedD_Kmh;
                                rawThrottle = HoldForward(baseTarget);
                            }
                            else
                            {
                                rawThrottle = gearCreepThrottle;
                            }
                        }
                    }
                }
            }
        }

        float steer = 0f;
        if (useSteeringWheel && steeringWheelController != null)
        {
            float wheelAngle = steeringWheelController.CurrentAngle;
            float steer01 = Mathf.Clamp(wheelAngle / Mathf.Max(1f, steeringWheelMaxDeg), -1f, 1f);

            if (Mathf.Abs(steer01 - _latchedSteer01) > steerEpsilon)
                _latchedSteer01 = steer01;

            steer = _latchedSteer01;
        }
        else
        {
            if (Input.GetKey(KeyCode.A)) steer -= 1f;
            if (Input.GetKey(KeyCode.D)) steer += 1f;
            _latchedSteer01 = 0f;
        }

        float throttle = Mathf.Abs(rawThrottle) < throttleDeadzone ? 0f : rawThrottle;

        float speedMS = GetSpeedMS();
        float speedKmhNow = speedMS * 3.6f;

        // 1) เลี้ยวลดลงตามความเร็ว (กันคว่ำ)
        float t = Mathf.Clamp01(speedKmhNow / Mathf.Max(1f, steerFadeSpeedKmh));
        float steerLimit = Mathf.Lerp(maxSteerAngle, maxSteerAngle * steerAtHighSpeedFactor, t);
        float steerAngle = steer * steerLimit;

        // 2) คำนวณแรงบิด (NEW: จำกัดตอนถอย)
        float motorTorque = ComputeMotorTorque(throttle) * (gearModeEnabled ? gearTorqueScale : 1f);

        // 3) เบรก (Space)
        float baseBrakeTorque = braking ? maxBrakeTorque : 0f;

        // 3.1) เกียร์ P: ล็อกล้อด้วยแรงเบรกสูง (เหมือน Parking Pawl/Handbrake)
        // - ให้รถ "ขยับได้นิดหน่อย" ตามแรงชนได้ แต่จะไม่ไหลต่อและล้อไม่ค่อยหมุน
        bool inPark = gearModeEnabled && CurrentGearChar == 'P';
        float brakeTorque = inPark ? Mathf.Max(baseBrakeTorque, parkBrakeTorque) : baseBrakeTorque;

        ApplyAxle(frontAxle, motorTorque, steerAngle, brakeTorque);
        ApplyAxle(rearAxle, motorTorque, steerAngle, brakeTorque);


        // 4) Coast drag
        if (!braking && throttle == 0f && speedMS > 0.05f)
        {
            Vector3 v = GetVelocity();
            Vector3 dir = v.normalized;

            float dragAccel = coastDrag + Mathf.Abs(steer) * steerCoastExtraDrag + (aeroDrag * speedMS * speedMS);
            rb.AddForce(-dir * dragAccel, ForceMode.Acceleration);
        }

        if (addExtraDampingInPark && inPark)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = _baseLinearDamping + parkExtraLinearDamping;
            rb.angularDamping = _baseAngularDamping + parkExtraAngularDamping;
#else
    rb.drag = _baseLinearDamping + parkExtraLinearDamping;
    rb.angularDrag = _baseAngularDamping + parkExtraAngularDamping;
#endif
        }
        else
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = _baseLinearDamping;
            rb.angularDamping = _baseAngularDamping;
#else
    rb.drag = _baseLinearDamping;
    rb.angularDrag = _baseAngularDamping;
#endif
        }

        // 5) Speed limiter (เดินหน้า)
        if (speedKmhNow > maxSpeedKmh && speedMS > 0.05f)
        {
            Vector3 v = GetVelocity();
            rb.AddForce(-v.normalized * speedLimiterStrength, ForceMode.Acceleration);
        }

        // 6) ABS
        if (enableABS && braking)
        {
            // ถ้าอยู่ P ให้ ABS ใช้ brakeTorque (ซึ่งรวม parkBrakeTorque แล้ว)
            float absRequest = inPark ? brakeTorque : maxBrakeTorque;
            ApplyABS(frontAxle, absRequest);
            ApplyABS(rearAxle, absRequest);
        }

        // 7) กันโคลง + sync mesh
        ApplyAntiRoll(frontAxle);
        ApplyAntiRoll(rearAxle);

        UpdateWheelVisual(frontAxle);
        UpdateWheelVisual(rearAxle);
    }

    // ===================== NEW: จำกัดแรงบิดตอนถอย =====================
    float ComputeMotorTorque(float throttle)
    {
        if (throttle == 0f) return 0f;

        // ความเร็วตามแกนรถ (บวก=ไปหน้า, ลบ=ถอย)
        float forwardMS = Vector3.Dot(GetVelocity(), transform.forward);
        float forwardKmh = forwardMS * 3.6f;

        // เดินหน้า: ทำเหมือนเดิม
        if (throttle > 0f)
            return throttle * maxMotorTorque;

        // ถอยหลัง: จำกัดความเร็ว + จำกัดแรงบิด
        float reverseKmh = Mathf.Max(0f, -forwardKmh); // เป็นบวกเมื่อถอย

        // ratio: 0 = เพิ่งเริ่มถอย, 1 = ถึงเพดาน
        float ratio = reverseMaxSpeedKmh <= 0.1f ? 1f : Mathf.Clamp01(reverseKmh / reverseMaxSpeedKmh);

        // falloff แบบนุ่ม: ใกล้เพดานแล้วแรงบิดลดลงเยอะขึ้น
        // torqueScale จะค่อยๆ ลดจาก 1 -> 0
        float torqueScale = Mathf.Pow(1f - ratio, reverseTorqueFalloff);

        // เพดานแรงบิดถอย (โดยรวมให้ถอยอืดกว่าขับหน้า)
        float limitedTorque = throttle * maxMotorTorque * reverseMaxTorqueScale * torqueScale;

        // ถ้าถึงเพดานแล้ว (ratio==1) torqueScale==0 → ไม่เร่งต่อ
        return limitedTorque;
    }

    // ===================== เดิม (คงไว้) =====================
    void ApplyAxle(Axle axle, float motorTorque, float steerAngle, float brakeTorque)
    {
        if (axle == null) return;

        if (axle.steering)
        {
            axle.leftCollider.steerAngle = steerAngle;
            axle.rightCollider.steerAngle = steerAngle;
        }

        if (axle.motor)
        {
            axle.leftCollider.motorTorque = motorTorque;
            axle.rightCollider.motorTorque = motorTorque;
        }
        else
        {
            axle.leftCollider.motorTorque = 0f;
            axle.rightCollider.motorTorque = 0f;
        }

        axle.leftCollider.brakeTorque = brakeTorque;
        axle.rightCollider.brakeTorque = brakeTorque;
    }

    void ApplyABS(Axle axle, float requestedBrake)
    {
        if (axle == null) return;
        ABSOneWheel(axle.leftCollider, requestedBrake);
        ABSOneWheel(axle.rightCollider, requestedBrake);
    }

    void ABSOneWheel(WheelCollider wc, float requestedBrake)
    {
        if (wc == null) return;
        if (!wc.GetGroundHit(out WheelHit hit)) return;

        float slip = Mathf.Abs(hit.forwardSlip);
        float scale = 1f;

        if (slip > absSlipThreshold)
        {
            float over = Mathf.Clamp01((slip - absSlipThreshold) / Mathf.Max(0.0001f, (1f - absSlipThreshold)));
            scale = Mathf.Lerp(1f, absMinBrakeScale, over);
        }

        wc.brakeTorque = requestedBrake * scale;
    }

    void UpdateWheelVisual(Axle axle)
    {
        if (axle == null) return;
        SyncWheel(axle.leftCollider, axle.leftMesh);
        SyncWheel(axle.rightCollider, axle.rightMesh);
    }

    void SyncWheel(WheelCollider col, Transform mesh)
    {
        if (col == null || mesh == null) return;
        col.GetWorldPose(out Vector3 pos, out Quaternion rot);
        mesh.position = pos;
        mesh.rotation = rot;
    }

    void ApplyAntiRoll(Axle axle)
    {
        if (axle == null) return;

        WheelHit hitL, hitR;
        bool groundedL = axle.leftCollider.GetGroundHit(out hitL);
        bool groundedR = axle.rightCollider.GetGroundHit(out hitR);

        float travelL = 1f;
        float travelR = 1f;

        if (groundedL)
            travelL = (-axle.leftCollider.transform.InverseTransformPoint(hitL.point).y - axle.leftCollider.radius) / axle.leftCollider.suspensionDistance;

        if (groundedR)
            travelR = (-axle.rightCollider.transform.InverseTransformPoint(hitR.point).y - axle.rightCollider.radius) / axle.rightCollider.suspensionDistance;

        float antiRollForce = (travelL - travelR) * antiRoll;

        if (groundedL)
            rb.AddForceAtPosition(axle.leftCollider.transform.up * -antiRollForce, axle.leftCollider.transform.position);

        if (groundedR)
            rb.AddForceAtPosition(axle.rightCollider.transform.up * antiRollForce, axle.rightCollider.transform.position);
    }

    float GetSpeedMS()
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity.magnitude;
#else
        return rb.velocity.magnitude;
#endif
    }

    Vector3 GetVelocity()
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }
}
