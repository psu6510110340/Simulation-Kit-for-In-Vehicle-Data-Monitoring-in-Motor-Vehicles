using UnityEngine;
using System;
using System.Text.RegularExpressions;

#if ENABLE_SERIALPORT
using System.IO.Ports;
using System.Text;
#endif

public class Headlight2LevelController : MonoBehaviour
{
    public enum BeamMode { Off, Low, High }
    public enum CanIdType { Standard, Extended }
    public enum CanFrameType { Data, Remote }

    [Header("References")]
    public Light leftLight;
    public Light rightLight;
    public Renderer[] leftGlows;
    public Renderer[] rightGlows;

    [Header("Input")]
    public KeyCode toggleOnOffKey = KeyCode.L;
    public KeyCode toggleBeamKey = KeyCode.K;

    [Header("Low Beam Settings")]
    public float lowIntensity = 1500f;
    public float lowRange = 35f;
    public float lowSpotAngle = 45f;
    public float lowGlowHDR = 3f;

    [Header("High Beam Settings")]
    public float highIntensity = 4500f;
    public float highRange = 60f;
    public float highSpotAngle = 55f;
    public float highGlowHDR = 8f;

    [Header("State")]
    public BeamMode mode = BeamMode.Off;

    [Header("Serial Source")]
    public bool useAutoPortReader = true;

    [Tooltip("Fallback fixed port (used when useAutoPortReader=false).")]
    public string portName = "COM3";
    public int baudRate = 115200;

    [Header("Frame Type Options")]
    public CanIdType idType = CanIdType.Standard;
    public CanFrameType frameType = CanFrameType.Data;

    [Header("CAN (Inspector Fields)")]
    public string canIdHex = "7B8";
    public string offDataHex = "03 61 42 00 00 00 00 00";
    public string lowDataHex = "03 61 42 01 00 00 00 00";
    public string highDataHex = "03 61 42 10 00 00 00 00";

    [Header("Send options")]
    public int ackTimeoutMs = 500;

    [Header("Gate (must select menu before UART)")]
    public bool requireMenuSelectedToSendUart = true;
    public string requiredMenuName = "Headlight";

#if ENABLE_SERIALPORT
    SerialPort _sp;
#endif

    Material[] _leftGlowMats;
    Material[] _rightGlowMats;

    void Awake()
    {
        Application.runInBackground = true;

        _leftGlowMats = CacheMaterials(leftGlows);
        _rightGlowMats = CacheMaterials(rightGlows);

#if ENABLE_SERIALPORT
        if (!useAutoPortReader)
            OpenFixedPort();
#else
        Debug.LogWarning("[Headlight] SerialPort not enabled. Enable System.IO.Ports support then add define ENABLE_SERIALPORT.");
#endif

        ApplyMode(mode, sendCan: false);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleOnOffKey))
        {
            mode = (mode == BeamMode.Off) ? BeamMode.Low : BeamMode.Off;
            ApplyMode(mode, sendCan: true);
        }

        if (Input.GetKeyDown(toggleBeamKey))
        {
            if (mode == BeamMode.Low) mode = BeamMode.High;
            else if (mode == BeamMode.High) mode = BeamMode.Low;

            ApplyMode(mode, sendCan: true);
        }
    }

    void ApplyMode(BeamMode m, bool sendCan)
    {
        bool on = (m != BeamMode.Off);

        if (leftLight) leftLight.enabled = on;
        if (rightLight) rightLight.enabled = on;

        SetRenderersEnabled(leftGlows, on);
        SetRenderersEnabled(rightGlows, on);

        if (!on)
        {
            if (sendCan) SendHeadlightFrame(offDataHex);
            return;
        }

        if (m == BeamMode.Low)
        {
            ApplyLightSettings(lowIntensity, lowRange, lowSpotAngle);
            ApplyGlowSettings(_leftGlowMats, lowGlowHDR);
            ApplyGlowSettings(_rightGlowMats, lowGlowHDR);
            if (sendCan) SendHeadlightFrame(lowDataHex);
        }
        else
        {
            ApplyLightSettings(highIntensity, highRange, highSpotAngle);
            ApplyGlowSettings(_leftGlowMats, highGlowHDR);
            ApplyGlowSettings(_rightGlowMats, highGlowHDR);
            if (sendCan) SendHeadlightFrame(highDataHex);
        }
    }

    void ApplyLightSettings(float intensity, float range, float spotAngle)
    {
        if (leftLight)
        {
            leftLight.intensity = intensity;
            leftLight.range = range;
            leftLight.spotAngle = spotAngle;
        }
        if (rightLight)
        {
            rightLight.intensity = intensity;
            rightLight.range = range;
            rightLight.spotAngle = spotAngle;
        }
    }

    void SendHeadlightFrame(string dataHex)
    {
#if !ENABLE_SERIALPORT
        return;
#else
        // ✅ Gate: ต้องเลือกเมนู Headlight ก่อน ถึงจะส่ง UART ได้
        if (requireMenuSelectedToSendUart)
        {
            var gate = PanelSelectionGate.Instance;
            if (gate == null || !gate.IsSelected(requiredMenuName))
            {
                Debug.LogWarning($"[Headlight] Block UART: ต้องคลิกเลือก '{requiredMenuName}' ใน SidePanel ก่อน");
                return; // ❌ ไม่ส่ง UART
            }
        }

        if (!TryBuildTxMessage(idType, frameType, canIdHex, dataHex, 8, out string msg, out string err))
        {
            Debug.LogWarning($"[Headlight] Invalid CAN input: {err}");
            return;
        }

        if (useAutoPortReader)
        {
            var mgr = SerialAutoPortReader.Instance;
            if (mgr == null || !mgr.IsOpen)
            {
                Debug.LogWarning("[Headlight] SerialAutoPortReader not ready/open.");
                return;
            }
            mgr.FlushBuffers();
            mgr.SendLine(msg);
            return;
        }

        if (_sp == null || !_sp.IsOpen)
        {
            Debug.LogWarning("[Headlight] Fixed serial port not open.");
            return;
        }

        try
        {
            _sp.DiscardInBuffer();
            _sp.DiscardOutBuffer();
            _sp.WriteLine(msg);

            try
            {
                string line1 = _sp.ReadLine().Trim();
                if (line1.Equals("Pass", StringComparison.OrdinalIgnoreCase))
                {
                    try { _sp.ReadLine(); } catch (TimeoutException) { }
                }
            }
            catch (TimeoutException) { }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Headlight] Serial write failed: {e.Message}");
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
            Debug.Log($"[Headlight] Fixed serial opened: {portName} @ {baudRate}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Headlight] Open fixed serial failed: {e.Message}");
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

        if (string.IsNullOrEmpty(raw))
        {
            msg = $"TX {idTypeToken} {frameTypeToken} {id}";
            return true;
        }

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

    static void SetRenderersEnabled(Renderer[] renderers, bool enabled)
    {
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i]) renderers[i].enabled = enabled;
    }

    static Material[] CacheMaterials(Renderer[] renderers)
    {
        if (renderers == null || renderers.Length == 0) return null;
        var mats = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i]) mats[i] = renderers[i].material;
        return mats;
    }

    static void ApplyGlowSettings(Material[] mats, float hdr)
    {
        if (mats == null) return;
        Color baseColor = Color.white;
        for (int i = 0; i < mats.Length; i++)
        {
            var mat = mats[i];
            if (!mat) continue;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor * hdr);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor * hdr);
        }
    }
}