using UnityEngine;

public class BrakeLightController : MonoBehaviour
{
    [Header("Brake Input")]
    [Tooltip("ปุ่มเบรกของรถ")]
    public KeyCode brakeKey = KeyCode.Space;

    [Header("Brake Lights")]
    public Light[] brakeLights;
    public Renderer[] brakeGlows;

    [Header("Light Settings")]
    public float brakeLightIntensity = 5f;
    public float brakeLightRange = 6f;

    [Header("Glow Settings")]
    public float glowHDR = 3.5f;
    [Range(0f, 0.2f)] public float offDimFactor = 0f;

    // ===================== UART Gate (ต้องเลือกเมนู Brakelight ก่อน) =====================
    [Header("UART Gate (must select menu before sending)")]
    public bool requireMenuSelectedToSendUart = true;
    public string requiredMenuName = "Brakelight"; // ต้องตรงกับชื่อใน SidePanel

    [Header("CAN")]
    public string canIdHex = "7B8";

    [Tooltip("ไฟเบรกไม่ทำงาน (OFF) เช่น 03 61 43 00 00 00 00 00")]
    public string offDataHex = "03 61 43 00 00 00 00 00";

    [Tooltip("ไฟเบรกทำงาน (ON) เช่น 03 61 43 01 00 00 00 00")]
    public string onDataHex = "03 61 43 01 00 00 00 00";

    [Header("Debug")]
    public bool logUart = false;
    public bool logBlocked = false;

    Material[] _mats;
    Color[] _baseColors;
    int _colorProp = -1;

    bool _isBrakeOn;

    static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    static readonly int ColorID = Shader.PropertyToID("_Color");

    void Awake()
    {
        CacheGlow();
        ApplyBrake(false); // เริ่ม OFF
        _isBrakeOn = false;
    }

    void Update()
    {
        bool braking = Input.GetKey(brakeKey);

        if (braking != _isBrakeOn)
        {
            _isBrakeOn = braking;
            ApplyBrake(_isBrakeOn);

            // ✅ ส่ง UART เฉพาะตอนสถานะไฟเบรกเปลี่ยน (ON/OFF)
            TrySendUart(_isBrakeOn);
        }
    }

    // ===================== เปิด / ปิดไฟเบรก =====================
    void ApplyBrake(bool on)
    {
        foreach (var l in brakeLights)
        {
            if (!l) continue;
            l.enabled = on;
            if (on)
            {
                l.intensity = brakeLightIntensity;
                l.range = brakeLightRange;
            }
        }

        ApplyGlow(on ? glowHDR : offDimFactor);
    }

    // ===================== UART =====================
    bool IsGateAllowed()
    {
        if (!requireMenuSelectedToSendUart) return true;

        var gate = PanelSelectionGate.Instance;
        if (gate == null) return false;

        return gate.IsSelected(requiredMenuName);
    }

    void TrySendUart(bool isOn)
    {
        // Gate: ต้องเลือกเมนู Brakelight ก่อน
        if (!IsGateAllowed())
        {
            if (logBlocked)
                Debug.LogWarning($"[Brakelight] Block UART: ต้องคลิกเลือก '{requiredMenuName}' ใน SidePanel ก่อน");
            return;
        }

        var serial = SerialAutoPortReader.Instance;
        if (serial == null || !serial.IsOpen)
        {
            if (logBlocked)
                Debug.LogWarning("[Brakelight] Serial not ready/open (SerialAutoPortReader)");
            return;
        }

        string dataHex = isOn ? onDataHex : offDataHex;
        if (string.IsNullOrWhiteSpace(dataHex)) return;

        string msg = $"TX STD DATA {canIdHex} {dataHex}";
        serial.FlushBuffers();
        serial.SendLine(msg);

        if (logUart)
            Debug.Log($"[Brakelight] UART Sent: {msg}");
    }

    // ===================== Glow =====================
    void CacheGlow()
    {
        if (brakeGlows == null || brakeGlows.Length == 0) return;

        _mats = new Material[brakeGlows.Length];
        _baseColors = new Color[brakeGlows.Length];

        for (int i = 0; i < brakeGlows.Length; i++)
        {
            var r = brakeGlows[i];
            if (!r) continue;

            var mat = r.material; // instance
            _mats[i] = mat;

            if (_colorProp == -1)
            {
                if (mat.HasProperty(BaseColorID)) _colorProp = BaseColorID;
                else if (mat.HasProperty(ColorID)) _colorProp = ColorID;
            }

            if (_colorProp != -1)
                _baseColors[i] = mat.GetColor(_colorProp);
            else
                _baseColors[i] = Color.red;
        }
    }

    void ApplyGlow(float mult)
    {
        if (_mats == null || _colorProp == -1) return;

        for (int i = 0; i < _mats.Length; i++)
        {
            if (_mats[i] == null) continue;
            _mats[i].SetColor(_colorProp, _baseColors[i] * mult);
        }
    }
}
