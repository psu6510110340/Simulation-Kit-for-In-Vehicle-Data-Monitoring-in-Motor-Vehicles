using UnityEngine;
using System;
using System.Text.RegularExpressions;

public class GearUartAutoByInspector : MonoBehaviour
{
    [Header("Refs")]
    public WheelColliderCarController car;

    [Header("Gate (must select menu 'Gear' to allow UART)")]
    public bool requireMenuSelectedToSendUart = true;
    public string requiredMenuName = "Gear";
    public bool logBlocked = false;

    [Header("CAN Frame")]
    public string canIdHex = "7B8";  // ตามตาราง
    public bool useStdId = true;     // STD/EXT

    [Header("Messages (set in Inspector)")]
    [Tooltip("ตัวอย่าง: 03 61 1C 08 00 00 00 00")]
    public string msgP = "03 61 1C 08 00 00 00 00";
    public string msgR = "03 61 1C 0E 00 00 00 00";
    public string msgN = "03 61 1C 08 00 00 00 00";
    public string msgD = "03 61 1C 07 00 00 00 00";
    public string msgB = "03 61 1C 06 00 00 00 00";

    [Header("Behavior")]
    public bool sendOnSelectGear = true; // ตอน “เพิ่งเลือก Gear” ให้ส่งเกียร์ปัจจุบันทันทีเพื่อ sync
    public bool logTx = true;

    char _lastObservedGear = '\0';
    char _lastSentGear = '\0';
    bool _wasSelected = false;

    void Awake()
    {
        if (!car) car = FindFirstObjectByType<WheelColliderCarController>();
    }

    void Update()
    {
        if (!car) return;

        // เกียร์ปัจจุบัน
        char g = car.CurrentGearChar;

        // เช็คเลือกเมนู Gear อยู่ไหม
        bool selected = true;
        if (requireMenuSelectedToSendUart)
        {
            var gate = PanelSelectionGate.Instance;
            selected = (gate != null && gate.IsSelected(requiredMenuName));
        }

        // 1) ถ้า "เพิ่งถูกเลือก Gear" -> ส่งเกียร์ปัจจุบันเพื่อ sync (ถ้าเปิด sendOnSelectGear)
        if (sendOnSelectGear && selected && !_wasSelected)
        {
            TrySendIfAllowed(g, selected);
        }

        // 2) ส่งเมื่อ "เปลี่ยนเกียร์" แต่ต้อง selected เท่านั้น
        if (g != _lastObservedGear)
        {
            _lastObservedGear = g;

            if (selected)
            {
                TrySendIfAllowed(g, selected);
            }
            else
            {
                if (logBlocked)
                    Debug.LogWarning($"[GearUART] Blocked (not selected '{requiredMenuName}') gear changed to {g}");
            }
        }

        _wasSelected = selected;
    }

    void TrySendIfAllowed(char gear, bool selected)
    {
        // กัน spam ซ้ำเกียร์เดิม
        if (gear == _lastSentGear) return;

        // เกียร์ที่รองรับ
        if (!(gear == 'P' || gear == 'R' || gear == 'N' || gear == 'D' || gear == 'B'))
            return;

        // ถ้าไม่ selected ห้ามส่ง
        if (!selected) return;

        // เลือก payload ตามเกียร์
        string dataHex = gear switch
        {
            'P' => msgP,
            'R' => msgR,
            'N' => msgN,
            'D' => msgD,
            'B' => msgB,
            _ => msgP
        };

        // Build TX string
        if (!TryBuildTx(canIdHex, dataHex, useStdId, out string tx, out string err))
        {
            Debug.LogWarning($"[GearUART] Invalid config: {err}");
            return;
        }

        // ส่งผ่านตัวเดียวกับ Headlight
        var serial = SerialAutoPortReader.Instance;
        if (serial == null || !serial.IsOpen)
        {
            Debug.LogWarning("[GearUART] SerialAutoPortReader not ready/open.");
            return;
        }

        serial.FlushBuffers();
        serial.SendLine(tx);

        _lastSentGear = gear;

        if (logTx) Debug.Log($"[GearUART] Gear={gear} TX => {tx}");
    }

    bool TryBuildTx(string idHex, string dataHex, bool isStd, out string tx, out string err)
    {
        tx = "";
        err = "";

        string id = (idHex ?? "").Trim().ToUpperInvariant();
        if (id.StartsWith("0X")) id = id.Substring(2);

        if (isStd)
        {
            if (!Regex.IsMatch(id, "^[0-9A-F]{1,3}$")) { err = "STD ID must be 1-3 hex digits"; return false; }
            uint v = Convert.ToUInt32(id, 16);
            if (v > 0x7FF) { err = "STD ID out of range (>7FF)"; return false; }
        }
        else
        {
            if (!Regex.IsMatch(id, "^[0-9A-F]{1,8}$")) { err = "EXT ID must be 1-8 hex digits"; return false; }
            uint v = Convert.ToUInt32(id, 16);
            if (v > 0x1FFFFFFF) { err = "EXT ID out of range (>1FFFFFFF)"; return false; }
        }

        string raw = (dataHex ?? "").Trim().ToUpperInvariant().Replace(",", " ");
        raw = Regex.Replace(raw, @"\s+", " ").Trim();
        if (string.IsNullOrEmpty(raw)) { err = "Data is empty"; return false; }

        string[] bytes = raw.Split(' ');
        if (bytes.Length > 8) { err = "Data must be 0-8 bytes"; return false; }

        for (int i = 0; i < bytes.Length; i++)
        {
            string b = bytes[i];
            if (b.StartsWith("0X")) b = b.Substring(2);
            if (!Regex.IsMatch(b, "^[0-9A-F]{2}$")) { err = $"Invalid byte #{i + 1}: {bytes[i]}"; return false; }
            bytes[i] = b;
        }

        string typeToken = isStd ? "STD" : "EXT";
        tx = $"TX {typeToken} DATA {id} {string.Join(" ", bytes)}";
        return true;
    }
}
