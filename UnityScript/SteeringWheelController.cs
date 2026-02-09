using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using System.Text.RegularExpressions;

#if ENABLE_SERIALPORT
using System.IO.Ports;
using System.Text;
#endif

public class SteeringWheelController : MonoBehaviour
{
    public enum RotationAxis { X, Y, Z }
    public enum CanIdType { Standard, Extended }
    public enum CanFrameType { Data, Remote }

    [Header("Steering Wheel (3D)")]
    public Transform steeringWheel;
    public RotationAxis axis = RotationAxis.Z;
    public float minAngle = -510f;
    public float maxAngle = 510f;

    [Header("Keyboard Control")]
    public KeyCode turnLeftKey = KeyCode.A;
    public KeyCode turnRightKey = KeyCode.D;
    public float keyboardDegPerSecond = 260f;

    [Header("Smooth Move To Target (Send)")]
    public float moveToTargetSpeedDegPerSec = 420f;

    [Header("UI (Bottom Panel)")]
    public TMP_Text angleText;
    public TMP_InputField angleInput;
    public Button sendButton;

    [Header("Input Error Feedback")]
    public Color normalInputColor = Color.white;
    public Color errorInputColor = new Color(1f, 0.3f, 0.3f);
    public float errorBlinkDuration = 0.1f;
    public int errorBlinkCount = 2;

    [Header("Runtime Gate")]
    [Tooltip("ถ้า false จะไม่รับ A/D และไม่ขยับไป target (เหมาะใช้ตอนซ่อน Steering)")]
    public bool inputEnabled = true;

    // ========================= NEW: UART / Mapping per degree =========================
    [Header("UART Gate (must select menu before UART)")]
    public bool requireMenuSelectedToSendUart = true;
    public string requiredMenuName = "Steering";

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

    [Header("Per-Degree UART Data (index = angle - minAngle)")]
    [Tooltip("ต้องมีจำนวน = (maxAngle-minAngle+1) เช่น -510..510 = 1021 ช่อง\nช่องที่ 0 = -510, ช่องสุดท้าย = 510")]

    [Header("Send options")]
    public int ackTimeoutMs = 500;

    [Header("Debug")]
    public bool logBlocked = false;
    public bool logSent = false;
    // =================================================================================

    public float CurrentAngle => _currentAngle;

    Coroutine _blinkRoutine;

    float _currentAngle;
    float _targetAngle;
    bool _moveToTarget;

    bool _manualEditing;        // ผู้ใช้กำลังแก้ใน input
    bool _manualDirty;          // มีค่าที่ผู้ใช้พิมพ์ค้างอยู่ (ยังไม่กด Send)

    Quaternion _initialLocalRotation;

#if ENABLE_SERIALPORT
    SerialPort _sp;
#endif

    void Awake()
    {
        Application.runInBackground = true;

        if (!steeringWheel) steeringWheel = transform;
        _initialLocalRotation = steeringWheel.localRotation;

        _currentAngle = 0f;
        ApplyAngle(_currentAngle);

        if (angleInput)
            angleInput.text = Mathf.RoundToInt(_currentAngle).ToString(CultureInfo.InvariantCulture);

        if (sendButton)
            sendButton.onClick.AddListener(OnSendClicked);

        if (angleInput)
        {
            angleInput.onSelect.AddListener(_ => { _manualEditing = true; });
            angleInput.onEndEdit.AddListener(_ => { _manualEditing = false; });

            angleInput.onValueChanged.AddListener(_ =>
            {
                // ถ้าผู้ใช้กำลังแก้ค่า ให้ถือว่าค่ามีการพิมพ์ค้างอยู่
                if (_manualEditing) _manualDirty = true;
            });
        }

        if (angleInput && angleInput.targetGraphic)
            normalInputColor = angleInput.targetGraphic.color;

#if ENABLE_SERIALPORT
        if (!useAutoPortReader)
            OpenFixedPort();
#endif
        RefreshAngleText();
    }

    void Update()
    {
        if (inputEnabled)
        {
            HandleKeyboard();
            HandleMoveToTarget();
        }

        RefreshAngleText();
        SyncAngleInput();
    }

    // เรียกจาก PreviewManager ตอน Show/Hide
    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;

        // ถ้าปิด ให้หยุดการวิ่งไป target ทันที (กันแอบหมุนตอนซ่อน)
        if (!enabled)
            _moveToTarget = false;
    }

    public void SetSteeringWheel(Transform t)
    {
        steeringWheel = t;
        _initialLocalRotation = steeringWheel.localRotation;
        ApplyAngle(_currentAngle);

        if (angleInput)
            angleInput.text = Mathf.RoundToInt(_currentAngle).ToString(CultureInfo.InvariantCulture);
    }

    void HandleKeyboard()
    {
        float dir = 0f;
        if (Input.GetKey(turnLeftKey)) dir -= 1f;
        if (Input.GetKey(turnRightKey)) dir += 1f;

        if (Mathf.Abs(dir) > 0.001f)
        {
            _moveToTarget = false;
            _currentAngle += dir * keyboardDegPerSecond * Time.deltaTime;
            _currentAngle = Mathf.Clamp(_currentAngle, minAngle, maxAngle);
            ApplyAngle(_currentAngle);
        }
    }

    void HandleMoveToTarget()
    {
        if (!_moveToTarget) return;

        _currentAngle = Mathf.MoveTowards(
            _currentAngle,
            _targetAngle,
            moveToTargetSpeedDegPerSec * Time.deltaTime
        );

        ApplyAngle(_currentAngle);

        if (Mathf.Abs(_currentAngle - _targetAngle) < 0.01f)
        {
            _currentAngle = _targetAngle;
            ApplyAngle(_currentAngle);
            _moveToTarget = false;

            if (angleInput)
                angleInput.text = Mathf.RoundToInt(_currentAngle).ToString(CultureInfo.InvariantCulture);
        }
    }

    void OnSendClicked()
    {
        if (!inputEnabled) return;
        if (!angleInput) return;

        // ✅ ล็อกค่าที่ผู้ใช้พิมพ์ไว้ ไม่ให้ Sync ทับ
        _manualDirty = false;
        _manualEditing = false;

        if (!TryParseAngle(angleInput.text, out float val))
        {
            BlinkInputError();
            return;
        }

        if (val < minAngle || val > maxAngle)
        {
            BlinkInputError();
            return;
        }

        // 1) หมุนพวงมาลัยไปยังองศาที่กรอก
        _targetAngle = val;
        _moveToTarget = true;

        if (angleInput)
        {
            angleInput.DeactivateInputField();
            angleInput.ReleaseSelection();
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        // 2) ✅ ส่ง UART เฉพาะตอนกด Send เท่านั้น (และต้องเลือกเมนู Steering ก่อน)
        SendSteeringFrameForAngle(val);
    }

    string BuildSpreadsheetDataHex(int angleDeg)
    {
        // raw = angle*10 + 32768
        int raw = angleDeg * 10 + 32768;

        int c = (raw >> 8) & 0xFF;
        int d = raw & 0xFF;

        // ✅ ตาม spreadsheet: 06 61 06 80 80 C D 00
        return $"06 61 06 80 80 {c:X2} {d:X2} 00";
    }

    // ========================= NEW: Send UART by degree =========================
    void SendSteeringFrameForAngle(float angle)
    {
#if !ENABLE_SERIALPORT
    return;
#else
        // ✅ Gate: ต้องเลือกเมนู Steering ก่อน
        if (requireMenuSelectedToSendUart)
        {
            var gate = PanelSelectionGate.Instance;
            if (gate == null || !gate.IsSelected(requiredMenuName))
            {
                if (logBlocked)
                    Debug.LogWarning($"[Steering] Block UART: ต้องคลิกเลือก '{requiredMenuName}' ใน SidePanel ก่อน");
                return;
            }
        }

        int minInt = Mathf.RoundToInt(minAngle);
        int maxInt = Mathf.RoundToInt(maxAngle);
        int aInt = Mathf.RoundToInt(angle);

        if (aInt < minInt || aInt > maxInt)
        {
            if (logBlocked)
                Debug.LogWarning($"[Steering] Block UART: angle out of range {aInt} (allowed {minInt}..{maxInt})");
            return;
        }

        // ✅ Build data hex exactly like spreadsheet: 06 61 06 80 80 C D 00
        string dataHex = BuildSpreadsheetDataHex(aInt);

        if (!TryBuildTxMessage(idType, frameType, canIdHex, dataHex, 8, out string msg, out string err))
        {
            Debug.LogWarning($"[Steering] Invalid CAN input: {err}");
            return;
        }

        // --- send via AutoPortReader or fixed SerialPort (เหมือนเดิม) ---
        if (useAutoPortReader)
        {
            var mgr = SerialAutoPortReader.Instance;
            if (mgr == null || !mgr.IsOpen)
            {
                Debug.LogWarning("[Steering] SerialAutoPortReader not ready/open.");
                return;
            }
            mgr.FlushBuffers();
            mgr.SendLine(msg);

            if (logSent) Debug.Log($"[Steering] Sent({aInt}°): {msg}");
            return;
        }

        if (_sp == null || !_sp.IsOpen)
        {
            Debug.LogWarning("[Steering] Fixed serial port not open.");
            return;
        }

        try
        {
            _sp.DiscardInBuffer();
            _sp.DiscardOutBuffer();
            _sp.WriteLine(msg);

            if (logSent) Debug.Log($"[Steering] Sent({aInt}°): {msg}");

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
            Debug.LogWarning($"[Steering] Serial write failed: {e.Message}");
            CloseFixedPort();
        }
#endif
    }

    // ============================================================================

    bool TryParseAngle(string s, out float val)
    {
        s = s.Replace("°", "").Trim();
        return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out val)
            || float.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out val);
    }

    void ApplyAngle(float angle)
    {
        if (!steeringWheel) return;

        Vector3 axisVec = axis switch
        {
            RotationAxis.X => Vector3.right,
            RotationAxis.Y => Vector3.up,
            _ => Vector3.forward
        };

        steeringWheel.localRotation = _initialLocalRotation * Quaternion.AngleAxis(angle, axisVec);
    }

    void RefreshAngleText()
    {
        if (angleText)
            angleText.text = $"{Mathf.RoundToInt(_currentAngle)}°";
    }

    void SyncAngleInput()
    {
        if (!angleInput) return;

        // ✅ ถ้ากำลังแก้ หรือมีค่าที่พิมพ์ค้างอยู่ อย่าทับ
        if (_manualEditing || _manualDirty) return;

        string v = Mathf.RoundToInt(_currentAngle).ToString();
        if (angleInput.text != v)
            angleInput.text = v;
    }

    void BlinkInputError()
    {
        if (!angleInput || angleInput.targetGraphic == null) return;

        if (_blinkRoutine != null)
            StopCoroutine(_blinkRoutine);

        _blinkRoutine = StartCoroutine(BlinkRoutine());
    }

    IEnumerator BlinkRoutine()
    {
        var graphic = angleInput.targetGraphic;

        for (int i = 0; i < errorBlinkCount; i++)
        {
            graphic.color = errorInputColor;
            yield return new WaitForSeconds(errorBlinkDuration);

            graphic.color = normalInputColor;
            yield return new WaitForSeconds(errorBlinkDuration);
        }
    }

    // ========================= Copy from Headlight: build TX msg =========================
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
    // ============================================================================

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
            Debug.Log($"[Steering] Fixed serial opened: {portName} @ {baudRate}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Steering] Open fixed serial failed: {e.Message}");
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
}
