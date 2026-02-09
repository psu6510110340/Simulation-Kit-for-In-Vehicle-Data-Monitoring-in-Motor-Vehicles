using UnityEngine;

public class CarGearboxController : MonoBehaviour
{
    public enum Gear { P, R, N, D, B }

    [Header("Refs")]
    public WheelColliderCarController car; // ลากตัวรถที่มีสคริปต์นี้
    public GearPanelSlide gearSlide;       // ลาก GearPanelSlide (บน GearPanel)
    public GearUI gearUI;                  // ลาก GearUI (บน GearPanel)

    [Header("Menu Name (must match SidePanel list)")]
    public string gearMenuName = "Gear";

    [Header("Shift Rule")]
    public KeyCode brakeKey = KeyCode.Space;
    public float stillSpeedKmh = 0.6f;

    [Header("Creep (D/B)")]
    [Range(0f, 1f)] public float creepThrottleD = 0.18f;
    [Range(0f, 1f)] public float creepThrottleB = 0.10f;
    [Range(0.1f, 1f)] public float bTorqueScale = 0.55f;

    [Header("Blink on invalid drive input")]
    public bool blinkOnInvalidDrive = true;

    Gear _gear = Gear.P;
    bool _gearModeActive;

    void Start()
    {
        if (!car) car = FindFirstObjectByType<WheelColliderCarController>();

        // เริ่มต้นให้ UI อยู่ที่ P (แต่ซ่อน panel ไว้ก่อน)
        _gear = Gear.P;
        ApplyGearUI();
        PushToCar();

        if (gearSlide) gearSlide.HideImmediate();
    }

    void Update()
    {
        // เปิด/ปิดตามเมนูที่เลือก
        string selected = PanelSelectionGate.Instance ? PanelSelectionGate.Instance.SelectedName : "";
        bool shouldActive = selected == gearMenuName;

        if (shouldActive != _gearModeActive)
        {
            _gearModeActive = shouldActive;

            if (_gearModeActive)
            {
                // เลือก Gear -> เริ่มที่ P เสมอ
                _gear = Gear.P;
                ApplyGearUI();
                PushToCar();
                if (gearSlide) gearSlide.Show();
            }
            else
            {
                if (gearSlide) gearSlide.Hide();
            }
        }

        if (!_gearModeActive) return;

        // เปลี่ยนเกียร์ด้วยลูกศรขึ้น/ลง
        if (Input.GetKeyDown(KeyCode.UpArrow)) TryShift(-1);    // ขึ้นไปหา P
        if (Input.GetKeyDown(KeyCode.DownArrow)) TryShift(+1);  // ลงไปหา B

        // กระพริบแดงเมื่อ "ฝืนเกียร์" (ข้อ 3-7)
        if (blinkOnInvalidDrive)
            CheckInvalidDriveAndBlink();
    }

    void FixedUpdate()
    {
        if (!car) return;

        // ส่ง “กติกาเกียร์” ให้ตัวควบคุมรถทุกเฟรมฟิสิกส์
        float creep = 0f;
        float torqueScale = 1f;

        if (_gear == Gear.D) creep = creepThrottleD;
        if (_gear == Gear.B) { creep = creepThrottleB; torqueScale = bTorqueScale; }

        car.SetGearMode(
            enabled: _gearModeActive,
            gearChar: GearToChar(_gear),
            creepThrottle: creep,
            torqueScale: torqueScale
        );
    }

    void TryShift(int dir)
    {
        if (!car) return;

        bool braking = Input.GetKey(brakeKey);
        bool still = car.SpeedKmh <= stillSpeedKmh;

        // ข้อ 8: ต้องเหยียบเบรก + รถนิ่งเท่านั้น
        if (!braking || !still)
        {
            Blink(); // กระพริบเกียร์ปัจจุบันสีแดง
            return;
        }

        int idx = (int)_gear + dir;
        idx = Mathf.Clamp(idx, 0, 4);
        _gear = (Gear)idx;

        ApplyGearUI();
        PushToCar();
    }

    void CheckInvalidDriveAndBlink()
    {
        // ใช้ GetKeyDown เพื่อไม่ให้กระพริบถี่เกิน (กดเมื่อ “พยายาม” เท่านั้น)
        bool wDown = Input.GetKeyDown(KeyCode.W);
        bool sDown = Input.GetKeyDown(KeyCode.S);

        if (!wDown && !sDown) return;

        // P/N: ห้ามขยับทั้งไปหน้าและถอย (ข้อ 3,5)
        if (_gear == Gear.P || _gear == Gear.N)
        {
            if (wDown || sDown) Blink();
            return;
        }

        // R: ถอยได้อย่างเดียว ถ้าพยายามเดินหน้า -> กระพริบ R (ข้อ 4)
        if (_gear == Gear.R)
        {
            if (wDown) Blink();
            return;
        }

        // D/B: เดินหน้าได้อย่างเดียว (ข้อ 6,7) ถ้าพยายามถอย -> กระพริบ D/B
        if (_gear == Gear.D || _gear == Gear.B)
        {
            if (sDown) Blink();
            return;
        }
    }

    void ApplyGearUI()
    {
        if (!gearUI) return;
        gearUI.SetActive(GearToChar(_gear));
    }

    void Blink()
    {
        if (!gearUI) return;
        gearUI.BlinkCurrent(GearToChar(_gear));
    }

    void PushToCar()
    {
        if (!car) return;
        car.CurrentGearChar = GearToChar(_gear);
    }

    char GearToChar(Gear g) => g switch
    {
        Gear.P => 'P',
        Gear.R => 'R',
        Gear.N => 'N',
        Gear.D => 'D',
        Gear.B => 'B',
        _ => 'P'
    };
}
