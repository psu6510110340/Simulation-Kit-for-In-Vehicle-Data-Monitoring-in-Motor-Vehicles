using UnityEngine;
using System;
using System.Text.RegularExpressions;

#if ENABLE_SERIALPORT
using System.IO.Ports;
using System.Text;
#endif

public class WiperUartController : MonoBehaviour
{
    [System.Serializable]
    public class Wiper
    {
        public Transform pivot;

        [Header("Axis (Local) - choose ONE")]
        public bool useX = false;
        public bool useY = true;
        public bool useZ = false;

        public bool invert = false;
        public float angleOffset = 0f;

        [HideInInspector] public Quaternion baseLocalRotation;

        public Vector3 Axis()
        {
            if (useX) return Vector3.right;
            if (useY) return Vector3.up;
            return Vector3.forward;
        }
    }

    public enum CanIdType { Standard, Extended }
    public enum CanFrameType { Data, Remote }

    public enum WiperMode
    {
        Off = 0,
        Low_Single = 1,
        High_Single = 2,
        Low_Auto = 3,
        Mid_Auto = 4,
        High_Auto = 5
    }

    [Header("Wipers (PIVOT objects, not mesh)")]
    public Wiper left;
    public Wiper right;

    [Header("Mode (runtime)")]
    public WiperMode mode = WiperMode.Off;

    [Header("Motion (feel like real car)")]
    public float maxAngle = 45f;
    public float cyclesPerSecond_Low = 0.8f;
    public float cyclesPerSecond_Mid = 1.2f;
    public float cyclesPerSecond_High = 1.8f;

    [Range(0.5f, 3f)]
    public float easeStrength = 1.5f;

    [Header("Return Home")]
    public bool returnHomeWhenOff = true;
    public float returnDegPerSec = 220f;
    public float homeAngle = 0f;

    [Header("Control (keys)")]
    public bool autoStart = false;                 // ✅ จะถูกบังคับ OFF ตอนเริ่มอยู่ดี
    public KeyCode lowSingleKey = KeyCode.T;       // ✅ ปัดครั้งเดียว (low)
    public KeyCode highSingleKey = KeyCode.Y;      // ✅ ปัดครั้งเดียว (high)
    public KeyCode autoCycleKey = KeyCode.U;       // ✅ วน auto: low->mid->high->off

    [Header("Serial Source")]
    public bool useAutoPortReader = true;
    public string portName = "COM3";
    public int baudRate = 115200;

    [Header("Frame Type Options")]
    public CanIdType idType = CanIdType.Standard;
    public CanFrameType frameType = CanFrameType.Data;

    [Header("CAN (Inspector Fields) - Editable")]
    public string canIdHex = "7B8";

    public string data_Off = "06 61 A2 00 00 00 00 00";
    public string data_LowSingle = "06 61 A2 00 00 00 01 00";
    public string data_HighSingle = "06 61 A2 00 00 00 10 00";
    public string data_LowAuto = "06 61 A2 00 01 00 00 00";
    public string data_MidAuto = "06 61 A2 00 01 01 10 00";
    public string data_HighAuto = "06 61 A2 00 01 10 00 00";

    [Header("Send options")]
    public int ackTimeoutMs = 500;

    [Header("Gate (must select menu before UART)")]
    public bool requireMenuSelectedToSendUart = true;
    public string requiredMenuName = "Windshield Wiper";

#if ENABLE_SERIALPORT
    SerialPort _sp;
#endif

    bool isRunning;          // auto
    bool returningHome;
    float phase;
    float currentAngle;

    bool singleWipeActive;
    float singleTimer;
    float singleDuration;

    void Awake()
    {
        Application.runInBackground = true;

#if ENABLE_SERIALPORT
        if (!useAutoPortReader) OpenFixedPort();
#endif

        CacheBaseFromCurrentPose();

        // ✅ บังคับเริ่ม OFF เสมอ (ไม่สน autoStart)
        currentAngle = homeAngle;
        ApplyAll(currentAngle);

        mode = WiperMode.Off;
        isRunning = false;
        singleWipeActive = false;
        returningHome = false;

        // ถ้าจะให้ตอนเริ่ม Play ส่ง OFF ไปด้วย ให้เปิดบรรทัดนี้
        // SendWiperFrame(data_Off);
    }

    void CacheBaseFromCurrentPose()
    {
        if (left.pivot) left.baseLocalRotation = left.pivot.localRotation;
        if (right.pivot) right.baseLocalRotation = right.pivot.localRotation;
    }

    void Update()
    {
        // ✅ T = ปัดครั้งเดียว low แล้วกลับ OFF
        if (Input.GetKeyDown(lowSingleKey))
        {
            SetMode(WiperMode.Low_Single, sendUart: true);
        }

        // ✅ Y = ปัดครั้งเดียว high แล้วกลับ OFF
        if (Input.GetKeyDown(highSingleKey))
        {
            SetMode(WiperMode.High_Single, sendUart: true);
        }

        // ✅ U = วน auto low->mid->high->off
        if (Input.GetKeyDown(autoCycleKey))
        {
            CycleAutoMode(sendUart: true);
        }

        // ----- Motion -----
        if (isRunning)
        {
            float cps = GetCyclesPerSecond(mode);
            float omega = Mathf.PI * 2f * cps;
            phase += omega * Time.deltaTime;

            float s = Mathf.Sin(phase);
            s = EaseSine(s, easeStrength);

            currentAngle = s * maxAngle;
            ApplyAll(currentAngle);
        }
        else if (singleWipeActive)
        {
            singleTimer += Time.deltaTime;

            float t = Mathf.Clamp01(singleTimer / Mathf.Max(0.0001f, singleDuration));
            float eased = Smooth01(t);

            float s = Mathf.Sin(eased * Mathf.PI * 2f);
            s = EaseSine(s, easeStrength);

            currentAngle = s * maxAngle;
            ApplyAll(currentAngle);

            if (t >= 1f)
            {
                singleWipeActive = false;
                returningHome = true;  // กลับบ้านนุ่มๆ
                // ✅ พอปัดครั้งเดียวเสร็จ “ตั้งสถานะเป็น OFF ทันที”
                mode = WiperMode.Off;
                // ✅ ส่ง OFF หลังจากจบ single wipe ตาม requirement
                SendWiperFrame(data_Off);
            }
        }
        else if (returningHome)
        {
            float step = returnDegPerSec * Time.deltaTime;
            currentAngle = Mathf.MoveTowards(currentAngle, homeAngle, step);
            ApplyAll(currentAngle);

            if (Mathf.Abs(currentAngle - homeAngle) < 0.01f)
            {
                currentAngle = homeAngle;
                returningHome = false;
                ApplyAll(currentAngle);

                // ถ้าอยู่ OFF แล้วอยากย้ำ OFF ซ้ำเมื่อกลับบ้านเสร็จ ให้เปิด
                // SendWiperFrame(data_Off);
            }
        }
    }

    void CycleAutoMode(bool sendUart)
    {
        // วนโหมดตามที่ต้องการ:
        // OFF -> Low_Auto -> Mid_Auto -> High_Auto -> OFF
        switch (mode)
        {
            case WiperMode.Off: SetMode(WiperMode.Low_Auto, sendUart); break;
            case WiperMode.Low_Auto: SetMode(WiperMode.Mid_Auto, sendUart); break;
            case WiperMode.Mid_Auto: SetMode(WiperMode.High_Auto, sendUart); break;
            case WiperMode.High_Auto: SetMode(WiperMode.Off, sendUart); break;

            // ถ้าเผลออยู่ single แล้วกด U ให้เริ่ม auto low
            case WiperMode.Low_Single:
            case WiperMode.High_Single:
            default:
                SetMode(WiperMode.Low_Auto, sendUart);
                break;
        }
    }

    public void SetMode(WiperMode newMode, bool sendUart)
    {
        mode = newMode;

        isRunning = false;
        singleWipeActive = false;
        returningHome = false;

        if (mode == WiperMode.Off)
        {
            if (sendUart) SendWiperFrame(data_Off);

            if (returnHomeWhenOff) returningHome = true;
            else
            {
                currentAngle = homeAngle;
                ApplyAll(currentAngle);
            }
            return;
        }

        if (mode == WiperMode.Low_Single || mode == WiperMode.High_Single)
        {
            if (sendUart)
            {
                SendWiperFrame(mode == WiperMode.Low_Single ? data_LowSingle : data_HighSingle);
            }

            float cps = (mode == WiperMode.Low_Single) ? cyclesPerSecond_Low : cyclesPerSecond_High;
            singleDuration = 1f / Mathf.Max(0.05f, cps);
            singleTimer = 0f;
            phase = 0f;
            singleWipeActive = true;
            return;
        }

        // Auto
        if (sendUart)
        {
            if (mode == WiperMode.Low_Auto) SendWiperFrame(data_LowAuto);
            else if (mode == WiperMode.Mid_Auto) SendWiperFrame(data_MidAuto);
            else if (mode == WiperMode.High_Auto) SendWiperFrame(data_HighAuto);
        }

        phase = 0f;
        isRunning = true;
    }

    float GetCyclesPerSecond(WiperMode m)
    {
        switch (m)
        {
            case WiperMode.Low_Auto: return cyclesPerSecond_Low;
            case WiperMode.Mid_Auto: return cyclesPerSecond_Mid;
            case WiperMode.High_Auto: return cyclesPerSecond_High;
            default: return cyclesPerSecond_Low;
        }
    }

    void ApplyAll(float angle)
    {
        Apply(left, angle);
        Apply(right, angle);
    }

    void Apply(Wiper w, float a)
    {
        if (!w.pivot) return;

        float finalAngle = a + w.angleOffset;
        if (w.invert) finalAngle = -finalAngle;

        Quaternion delta = Quaternion.AngleAxis(finalAngle, w.Axis());
        w.pivot.localRotation = w.baseLocalRotation * delta;
    }

    float EaseSine(float s, float strength)
    {
        float x = (s + 1f) * 0.5f;
        float e = Smooth01(x);
        e = Mathf.Pow(e, strength);
        return (e * 2f) - 1f;
    }

    float Smooth01(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    // ----------------- UART Send -----------------
    void SendWiperFrame(string dataHex)
    {
#if !ENABLE_SERIALPORT
        return;
#else
        if (requireMenuSelectedToSendUart)
        {
            var gate = PanelSelectionGate.Instance;
            if (gate == null || !gate.IsSelected(requiredMenuName))
            {
                Debug.LogWarning($"[Wiper] Block UART: ต้องคลิกเลือก '{requiredMenuName}' ใน SidePanel ก่อน");
                return;
            }
        }

        if (!TryBuildTxMessage(idType, frameType, canIdHex, dataHex, 8, out string msg, out string err))
        {
            Debug.LogWarning($"[Wiper] Invalid CAN input: {err}");
            return;
        }

        if (useAutoPortReader)
        {
            var mgr = SerialAutoPortReader.Instance;
            if (mgr == null || !mgr.IsOpen)
            {
                Debug.LogWarning("[Wiper] SerialAutoPortReader not ready/open.");
                return;
            }
            mgr.FlushBuffers();
            mgr.SendLine(msg);
            return;
        }

        if (_sp == null || !_sp.IsOpen)
        {
            Debug.LogWarning("[Wiper] Fixed serial port not open.");
            return;
        }

        try
        {
            _sp.DiscardInBuffer();
            _sp.DiscardOutBuffer();
            _sp.WriteLine(msg);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Wiper] Serial write failed: {e.Message}");
            CloseFixedPort();
        }
#endif
    }

#if ENABLE_SERIALPORT
    void OpenFixedPort()
    {
        try
        {
            _sp = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = ackTimeoutMs,
                WriteTimeout = 500,
                NewLine = "\n",
                DtrEnable = true,
                RtsEnable = true,
                Handshake = Handshake.None,
                Encoding = Encoding.ASCII
            };

            _sp.Open();
            _sp.DiscardInBuffer();
            _sp.DiscardOutBuffer();
            Debug.Log($"[Wiper] Fixed serial opened: {portName} @ {baudRate}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Wiper] Open fixed serial failed: {e.Message}");
            CloseFixedPort();
        }
    }

    void CloseFixedPort()
    {
        try
        {
            if (_sp != null)
            {
                if (_sp.IsOpen) _sp.Close();
                _sp.Dispose();
            }
        }
        catch { }
        _sp = null;
    }

    void OnApplicationQuit()
    {
        if (!useAutoPortReader)
            CloseFixedPort();
    }
#endif

    bool TryBuildTxMessage(CanIdType idTypeSel, CanFrameType frameTypeSel, string idHex, string dataHex, int rtrDlc, out string msg, out string err)
    {
        msg = "";
        err = "";

        string id = (idHex ?? "").Trim().ToUpperInvariant();
        if (id.StartsWith("0X")) id = id.Substring(2);

        if (idTypeSel == CanIdType.Standard)
        {
            if (!Regex.IsMatch(id, "^[0-9A-F]{1,3}$")) { err = "STD ID must be 1-3 hex digits"; return false; }
            uint val = Convert.ToUInt32(id, 16);
            if (val > 0x7FF) { err = "STD ID out of range (>7FF)"; return false; }
        }
        else
        {
            if (!Regex.IsMatch(id, "^[0-9A-F]{1,8}$")) { err = "EXT ID must be 1-8 hex digits"; return false; }
            uint val = Convert.ToUInt32(id, 16);
            if (val > 0x1FFFFFFF) { err = "EXT ID out of range (>1FFFFFFF)"; return false; }
        }

        string idTypeToken = (idTypeSel == CanIdType.Standard) ? "STD" : "EXT";
        string frameTypeToken = (frameTypeSel == CanFrameType.Data) ? "DATA" : "RTR";

        if (frameTypeSel == CanFrameType.Remote)
        {
            if (rtrDlc < 0 || rtrDlc > 8) { err = "RTR DLC must be 0-8"; return false; }
            msg = $"TX {idTypeToken} {frameTypeToken} {id} {rtrDlc}";
            return true;
        }

        string raw = (dataHex ?? "").Trim().ToUpperInvariant();
        raw = raw.Replace(",", " ");
        raw = Regex.Replace(raw, @"\s+", " ").Trim();

        string[] tokens = raw.Split(' ');
        if (tokens.Length > 8) { err = "Data must be 0-8 bytes"; return false; }

        for (int i = 0; i < tokens.Length; i++)
        {
            string t = tokens[i];
            if (t.StartsWith("0X")) t = t.Substring(2);
            if (!Regex.IsMatch(t, "^[0-9A-F]{2}$")) { err = $"Invalid byte #{i + 1}: {tokens[i]}"; return false; }
            tokens[i] = t;
        }

        msg = $"TX {idTypeToken} {frameTypeToken} {id} {string.Join(" ", tokens)}";
        return true;
    }
}
