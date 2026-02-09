using UnityEngine;

public class BrakeUartController : MonoBehaviour
{
    [Header("Input")]
    public KeyCode brakeKey = KeyCode.Space;

    [Header("UART Gate (must select menu before sending)")]
    public bool requireMenuSelectedToSendUart = true;

    // ⚠️ ต้อง "ตรงกับชื่อใน SidePanel" ของคุณ
    // ในภาพของคุณเหมือนจะเขียนว่า "Break" (สะกดแบบนั้น)
    public string requiredMenuName = "Break";

    [Header("CAN")]
    public string canIdHex = "7B8";

    [Tooltip("ไม่เหยียบ / ปล่อยเบรก")]
    public string releasedDataHex = "10 0A 61 04 32 CC 00 00";

    [Tooltip("เหยียบเบรก")]
    public string pressedDataHex = "10 0A 61 04 51 AE 3E 5E";

    [Header("Behavior")]
    public bool sendOnChangeOnly = true;

    [Header("Debug")]
    public bool logUart = false;
    public bool logBlocked = false;

    bool _prevPressed;
    bool _hasSentOnce;
    bool _prevGateAllowed;

    void Start()
    {
        _prevPressed = Input.GetKey(brakeKey);
        _prevGateAllowed = IsGateAllowed();
        _hasSentOnce = false;
    }

    void Update()
    {
        bool pressedNow = Input.GetKey(brakeKey);
        bool gateAllowed = IsGateAllowed();

        // ถ้าเพิ่ง "อนุญาต" (ผู้ใช้เพิ่งเลือกเมนู Brake/Break)
        // ให้ส่งสถานะปัจจุบันทันที 1 ครั้งเพื่อ sync
        if (!_prevGateAllowed && gateAllowed)
        {
            TrySend(pressedNow);
            _hasSentOnce = true;
        }

        if (sendOnChangeOnly)
        {
            if (pressedNow != _prevPressed)
            {
                // เปลี่ยนสถานะ (กด/ปล่อย) -> ส่ง
                TrySend(pressedNow);
                _hasSentOnce = true;
            }
        }
        else
        {
            // ส่งตลอด (ไม่แนะนำ) แต่ให้มีไว้เผื่อ
            TrySend(pressedNow);
            _hasSentOnce = true;
        }

        _prevPressed = pressedNow;
        _prevGateAllowed = gateAllowed;
    }

    bool IsGateAllowed()
    {
        if (!requireMenuSelectedToSendUart) return true;

        var gate = PanelSelectionGate.Instance;
        if (gate == null) return false;

        return gate.IsSelected(requiredMenuName);
    }

    void TrySend(bool pressed)
    {
        // Gate: ต้องเลือกเมนู Brake/Break ก่อน
        if (!IsGateAllowed())
        {
            if (logBlocked)
                Debug.LogWarning($"[Brake] Block UART: ต้องคลิกเลือก '{requiredMenuName}' ใน SidePanel ก่อน");
            return;
        }

        var serial = SerialAutoPortReader.Instance;
        if (serial == null || !serial.IsOpen)
        {
            if (logBlocked)
                Debug.LogWarning("[Brake] Serial not ready/open (SerialAutoPortReader)");
            return;
        }

        string dataHex = pressed ? pressedDataHex : releasedDataHex;
        if (string.IsNullOrWhiteSpace(dataHex)) return;

        string msg = $"TX STD DATA {canIdHex} {dataHex}";
        serial.FlushBuffers();
        serial.SendLine(msg);

        if (logUart)
            Debug.Log($"[Brake] UART Sent: {msg}");
    }
}
