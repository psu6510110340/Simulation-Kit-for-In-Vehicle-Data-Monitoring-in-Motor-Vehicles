using System;
using System.Collections.Generic;
using UnityEngine;

public class TurnSignalController : MonoBehaviour
{
    public enum SignalMode { Off, Left, Right, Hazard }

    [System.Serializable]
    public class SignalGroup
    {
        [Tooltip("ชื่อไว้ดูใน Inspector เช่น Front, Rear, Mirror, Door")]
        public string name = "Front";

        [Header("Lights (optional)")]
        public List<Light> lights = new List<Light>();

        [Header("Glow renderers (mesh/quads)")]
        public List<Renderer> glows = new List<Renderer>();

        [Header("Light Settings (optional)")]
        public float lightIntensityOn = 1.5f;
        public float lightRangeOn = 2.0f;
    }

    [Header("Groups - LEFT side")]
    public List<SignalGroup> leftGroups = new List<SignalGroup>();

    [Header("Groups - RIGHT side")]
    public List<SignalGroup> rightGroups = new List<SignalGroup>();

    [Header("Input")]
    public KeyCode leftKey = KeyCode.Z;
    public KeyCode rightKey = KeyCode.X;
    public KeyCode hazardKey = KeyCode.C;

    [Header("Blink Settings")]
    public float onTime = 0.45f;
    public float offTime = 0.45f;
    public bool startOn = true;

    [Header("Behavior")]
    public SignalMode mode = SignalMode.Off;
    public float autoCancelSeconds = 0f;

    [Header("Glow Behavior")]
    public bool keepGlowVisibleWhenOff = true;

    [Header("Glow Boost")]
    public float glowOnMultiplier = 6f;
    [Range(0f, 0.2f)]
    public float offDimFactor = 0f;

    // ---------------- UART / CAN ----------------
    [Header("UART CAN Send")]
    [Tooltip("ต้องเลือกเมนูใน SidePanel ก่อน ถึงจะส่ง UART ได้")]
    public bool requireMenuSelectedToSendUart = true;

    [Tooltip("ชื่อเมนูใน SidePanel ที่ต้องถูกเลือก (ให้ตรงกับ list ของคุณ)")]
    public string requiredMenuName = "Turnlight";

    [Header("CAN (Inspector Fields)")]
    public string canIdHex = "7B8";

    [Tooltip("OFF payload (8 bytes)")]
    public string offDataHex = "03 61 41 00 00 00 00 00";

    [Tooltip("LEFT payload (8 bytes)")]
    public string leftDataHex = "03 61 41 01 00 00 00 00";

    [Tooltip("RIGHT payload (8 bytes)")]
    public string rightDataHex = "03 61 41 10 00 00 00 00";

    [Tooltip("HAZARD payload (8 bytes)")]
    public string hazardDataHex = "03 61 41 11 00 00 00 00";

    float _timer;
    bool _blinkOn;
    float _autoCancelTimer;

    // เก็บ material instance + สีเดิมของมัน
    struct GlowCache
    {
        public Material mat;
        public int propId;
        public Color original;
        public bool valid;
    }

    readonly Dictionary<Renderer, GlowCache> _glowCache = new Dictionary<Renderer, GlowCache>();

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    void Awake()
    {
        CacheGlow(leftGroups);
        CacheGlow(rightGroups);

        _blinkOn = startOn;
        _timer = 0f;

        ApplyAll(false, false);
        ApplyModeImmediate(mode);

        // ไม่ส่งตอนเริ่ม (ถ้าต้องการให้ส่งสถานะเริ่มต้น ให้เปิดเองใน Start)
    }

    void Update()
    {
        HandleInput();

        if (mode == SignalMode.Off) return;

        if (autoCancelSeconds > 0f)
        {
            _autoCancelTimer += Time.deltaTime;
            if (_autoCancelTimer >= autoCancelSeconds)
            {
                SetMode(SignalMode.Off);
                return;
            }
        }

        _timer += Time.deltaTime;
        float target = _blinkOn ? onTime : offTime;

        if (_timer >= target)
        {
            _timer = 0f;
            _blinkOn = !_blinkOn;
            ApplyBlinkState();
        }
    }

    void HandleInput()
    {
        if (Input.GetKeyDown(leftKey))
            SetMode(mode == SignalMode.Left ? SignalMode.Off : SignalMode.Left);

        if (Input.GetKeyDown(rightKey))
            SetMode(mode == SignalMode.Right ? SignalMode.Off : SignalMode.Right);

        if (Input.GetKeyDown(hazardKey))
            SetMode(mode == SignalMode.Hazard ? SignalMode.Off : SignalMode.Hazard);
    }

    public void SetMode(SignalMode newMode)
    {
        // ✅ ถ้าโหมดเดิมเท่าเดิม ไม่ทำซ้ำ
        if (mode == newMode) return;

        mode = newMode;
        _timer = 0f;
        _blinkOn = startOn;
        _autoCancelTimer = 0f;

        ApplyModeImmediate(mode);

        // ✅ ส่ง UART เฉพาะตอนเปลี่ยนโหมด
        TrySendUartForMode(mode);
    }

    void ApplyModeImmediate(SignalMode m)
    {
        ApplyAll(false, false);
        if (m == SignalMode.Off) return;
        ApplyBlinkState();
    }

    void ApplyBlinkState()
    {
        bool leftOn = false;
        bool rightOn = false;

        if (mode == SignalMode.Left) leftOn = _blinkOn;
        else if (mode == SignalMode.Right) rightOn = _blinkOn;
        else if (mode == SignalMode.Hazard) { leftOn = _blinkOn; rightOn = _blinkOn; }

        ApplyAll(leftOn, rightOn);
    }

    void ApplyAll(bool leftOn, bool rightOn)
    {
        ApplySide(leftGroups, leftOn);
        ApplySide(rightGroups, rightOn);
    }

    void ApplySide(List<SignalGroup> groups, bool on)
    {
        foreach (var g in groups)
        {
            foreach (var l in g.lights)
            {
                if (!l) continue;
                l.enabled = on;
                if (on)
                {
                    l.intensity = g.lightIntensityOn;
                    l.range = g.lightRangeOn;
                }
            }

            foreach (var r in g.glows)
            {
                if (!r) continue;

                if (!keepGlowVisibleWhenOff)
                {
                    r.enabled = on;
                    continue;
                }

                r.enabled = true;

                if (_glowCache.TryGetValue(r, out var cache) && cache.valid && cache.mat)
                {
                    var c = on
                        ? (cache.original * glowOnMultiplier)
                        : (cache.original * offDimFactor);

                    cache.mat.SetColor(cache.propId, c);
                }
            }
        }
    }

    void CacheGlow(List<SignalGroup> groups)
    {
        foreach (var g in groups)
        {
            foreach (var r in g.glows)
            {
                if (!r) continue;
                var mat = r.material;
                if (!mat) continue;

                int propId = -1;
                Color original = Color.white;

                if (mat.HasProperty(BaseColorId))
                {
                    propId = BaseColorId;
                    original = mat.GetColor(BaseColorId);
                }
                else if (mat.HasProperty(ColorId))
                {
                    propId = ColorId;
                    original = mat.GetColor(ColorId);
                }
                else
                {
                    _glowCache[r] = new GlowCache { mat = mat, valid = false };
                    continue;
                }

                _glowCache[r] = new GlowCache
                {
                    mat = mat,
                    propId = propId,
                    original = original,
                    valid = true
                };
            }
        }
    }

    // ---------------- UART SEND HELPERS ----------------

    void TrySendUartForMode(SignalMode m)
    {
        // ✅ Gate: ต้องเลือกเมนู Turnlight ก่อน
        if (requireMenuSelectedToSendUart)
        {
            var gate = PanelSelectionGate.Instance;
            if (gate == null || !gate.IsSelected(requiredMenuName))
            {
                Debug.LogWarning($"[Turnlight] Block UART: ต้องคลิกเลือก '{requiredMenuName}' ใน SidePanel ก่อน");
                return;
            }
        }

        // ✅ เลือก payload จาก Inspector
        string dataHex =
            (m == SignalMode.Off) ? offDataHex :
            (m == SignalMode.Left) ? leftDataHex :
            (m == SignalMode.Right) ? rightDataHex :
            hazardDataHex;

        // ส่งผ่าน SerialAutoPortReader (รูปแบบเดียวกับ Headlight)
        var serial = SerialAutoPortReader.Instance;
        if (serial == null || !serial.IsOpen)
        {
            Debug.LogWarning("[Turnlight] Serial not ready/open (SerialAutoPortReader)");
            return;
        }

        string msg = $"TX STD DATA {canIdHex} {dataHex}";
        serial.FlushBuffers();
        serial.SendLine(msg);

        //Debug.Log($"[Turnlight] UART Sent: {msg}");
    }
}
