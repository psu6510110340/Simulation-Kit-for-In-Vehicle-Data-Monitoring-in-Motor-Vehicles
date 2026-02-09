using UnityEngine;

public class ReverseLightController : MonoBehaviour
{
    [Header("Detect Reverse From")]
    [Tooltip("ลากตัวที่มี WheelColliderCarController มาวาง")]
    public WheelColliderCarController carController;

    [Tooltip("ถ้าไม่ใส่ carController จะใช้ Rigidbody + Transform ตรวจเอง")]
    public Rigidbody carRigidbody;

    [Header("Rear Lights (turn on while reversing)")]
    public Light[] rearLights;
    public Renderer[] rearGlows;

    [Header("Light Settings")]
    public float lightIntensityOn = 4f;
    public float lightRangeOn = 6f;

    [Header("Glow Settings")]
    public float glowHDR = 2.5f;
    [Range(0f, 0.2f)] public float offDimFactor = 0f;

    [Header("Thresholds")]
    [Tooltip("ความเร็วแกนรถ (m/s) ที่ถือว่าเริ่มถอยจริง")]
    public float reverseVelocityThreshold = 0.15f;

    // ===================== UART Gate (ต้องเลือกเมนู Reverselight ก่อน) =====================
    [Header("UART Gate (must select menu before sending)")]
    public bool requireMenuSelectedToSendUart = true;

    // ต้องตรงกับชื่อใน SidePanel เป๊ะ ๆ (จากภาพของคุณคือ "Reverselight")
    public string requiredMenuName = "Reverselight";

    [Header("CAN")]
    public string canIdHex = "7B8";

    [Tooltip("ไฟถอยไม่ทำงาน (OFF) เช่น 03 61 48 00 00 00 00 00")]
    public string offDataHex = "03 61 48 00 00 00 00 00";

    [Tooltip("ไฟถอยทำงาน (ON) เช่น 03 61 48 01 00 00 00 00")]
    public string onDataHex = "03 61 48 01 00 00 00 00";

    [Header("Debug")]
    public bool logUart = false;
    public bool logBlocked = false;

    // ===================== internal =====================
    Material[] _mats;
    Color[] _base;
    int _propId = -1;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    bool _isOn;

    void Awake()
    {
        CacheGlow();
        Apply(false);
        _isOn = false;
    }

    void Update()
    {
        bool reversing = IsReversingNow();

        if (reversing != _isOn)
        {
            _isOn = reversing;
            Apply(_isOn);

            // ✅ ส่ง UART เฉพาะตอนสถานะเปลี่ยน (ON/OFF)
            TrySendUart(_isOn);
        }
    }

    bool IsReversingNow()
    {
        // 1) ถ้ามี carController ใช้สถานะจากมัน (แม่นสุด)
        if (carController != null)
            return carController.IsReversing;

        // 2) fallback: ใช้ Rigidbody ตรวจความเร็วแกนรถ
        if (carRigidbody != null)
        {
            Vector3 v =
#if UNITY_6000_0_OR_NEWER
                carRigidbody.linearVelocity;
#else
                carRigidbody.velocity;
#endif
            float forward = Vector3.Dot(v, transform.forward);
            return forward < -reverseVelocityThreshold;
        }

        // 3) fallback สุดท้าย: ใช้ input + เกณฑ์เบาๆ
        return Input.GetAxis("Vertical") < -0.1f;
    }

    void Apply(bool on)
    {
        // Lights
        if (rearLights != null)
        {
            foreach (var l in rearLights)
            {
                if (!l) continue;
                l.enabled = on;
                if (on)
                {
                    l.intensity = lightIntensityOn;
                    l.range = lightRangeOn;
                }
            }
        }

        // Glows (เปิด renderer ค้าง แล้วคุมด้วยสี จะดูเนียนกว่า)
        if (rearGlows != null)
        {
            foreach (var r in rearGlows)
            {
                if (!r) continue;
                r.enabled = true;
            }
        }

        ApplyGlow(on ? glowHDR : offDimFactor);
    }

    // ===================== UART helpers =====================
    bool IsGateAllowed()
    {
        if (!requireMenuSelectedToSendUart) return true;

        var gate = PanelSelectionGate.Instance;
        if (gate == null) return false;

        return gate.IsSelected(requiredMenuName);
    }

    void TrySendUart(bool isOn)
    {
        // Gate: ต้องเลือกเมนู Reverselight ก่อน
        if (!IsGateAllowed())
        {
            if (logBlocked)
                Debug.LogWarning($"[Reverselight] Block UART: ต้องคลิกเลือก '{requiredMenuName}' ใน SidePanel ก่อน");
            return;
        }

        var serial = SerialAutoPortReader.Instance;
        if (serial == null || !serial.IsOpen)
        {
            if (logBlocked)
                Debug.LogWarning("[Reverselight] Serial not ready/open (SerialAutoPortReader)");
            return;
        }

        string dataHex = isOn ? onDataHex : offDataHex;
        if (string.IsNullOrWhiteSpace(dataHex)) return;

        string msg = $"TX STD DATA {canIdHex} {dataHex}";
        serial.FlushBuffers();
        serial.SendLine(msg);

        if (logUart)
            Debug.Log($"[Reverselight] UART Sent: {msg}");
    }

    // ===================== Glow =====================
    void CacheGlow()
    {
        if (rearGlows == null || rearGlows.Length == 0) return;

        _mats = new Material[rearGlows.Length];
        _base = new Color[rearGlows.Length];

        for (int i = 0; i < rearGlows.Length; i++)
        {
            var r = rearGlows[i];
            if (!r) continue;

            var mat = r.material; // instance
            _mats[i] = mat;

            if (_propId == -1)
            {
                if (mat.HasProperty(BaseColorId)) _propId = BaseColorId;
                else if (mat.HasProperty(ColorId)) _propId = ColorId;
            }

            if (_propId != -1)
                _base[i] = mat.GetColor(_propId);
            else
                _base[i] = Color.white;
        }
    }

    void ApplyGlow(float mult)
    {
        if (_mats == null || _base == null || _propId == -1) return;

        for (int i = 0; i < _mats.Length; i++)
        {
            var m = _mats[i];
            if (!m) continue;
            m.SetColor(_propId, _base[i] * mult);
        }
    }
}
