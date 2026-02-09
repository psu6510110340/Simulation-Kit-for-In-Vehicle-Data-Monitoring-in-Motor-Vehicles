using System;
using System.Collections.Generic;
using UnityEngine;

public class CarDoorMultiController : MonoBehaviour
{
    [Flags]
    public enum DoorMask
    {
        None = 0,
        FrontLeft = 1 << 0, // F1
        RearLeft = 1 << 1, // F2
        FrontRight = 1 << 2, // F3
        RearRight = 1 << 3, // F4
        Tailgate = 1 << 4, // F5

        Everything = FrontLeft | RearLeft | FrontRight | RearRight | Tailgate
    }

    [System.Serializable]
    public class Door
    {
        public string name;

        [Header("What rotates")]
        public Transform hinge;

        [Header("Input")]
        public KeyCode key = KeyCode.E;

        [Header("Rotation")]
        public Vector3 localAxis = Vector3.up;
        public float openAngle = 70f;
        public bool invertDirection = false;

        [Header("Motion")]
        public float speed = 6f;

        [Header("State bit (ตามปุ่ม/บาน)")]
        public DoorMask maskBit = DoorMask.FrontLeft;

        [HideInInspector] public bool isOpen;
        [HideInInspector] public Quaternion closedRot;
        [HideInInspector] public Quaternion openRot;

        bool _prevIsOpen;

        public void Init()
        {
            if (!hinge) return;

            localAxis = localAxis.sqrMagnitude < 0.0001f ? Vector3.up : localAxis.normalized;
            closedRot = hinge.localRotation;

            float dir = invertDirection ? -1f : 1f;
            Quaternion delta = Quaternion.AngleAxis(openAngle * dir, localAxis);
            openRot = delta * closedRot;

            _prevIsOpen = isOpen;
        }

        public bool TickAndCheckChanged()
        {
            if (!hinge) return false;

            if (Input.GetKeyDown(key))
                isOpen = !isOpen;

            Quaternion target = isOpen ? openRot : closedRot;
            hinge.localRotation = Quaternion.Slerp(hinge.localRotation, target, Time.deltaTime * speed);

            bool changed = (isOpen != _prevIsOpen);
            _prevIsOpen = isOpen;
            return changed;
        }
    }

    [System.Serializable]
    public class DoorStateRow
    {
        [Tooltip("สถานะรวมของประตู (เลือกหลายค่าได้)")]
        public DoorMask state;

        [Tooltip("Payload 8 bytes ตามตาราง เช่น 06 61 46 00 00 00 10 00")]
        public string dataHex;
    }

    [Header("Doors")]
    public Door[] doors;

    // -------- State Table (กรอกตามตาราง) --------
    [Header("Door State Table (ตามตารางของคุณ)")]
    public List<DoorStateRow> stateTable = new List<DoorStateRow>();

    // -------- UART gating --------
    [Header("UART Gate (must select menu before sending)")]
    public bool requireMenuSelectedToSendUart = true;
    public string requiredMenuName = "Door"; // ต้องตรงกับ SidePanel

    [Header("CAN")]
    public string canIdHex = "7B8";

    [Header("Behavior")]
    public bool sendOnEveryChange = true;

    [Header("Debug")]
    public bool logUart = false;
    public bool logBlocked = false;
    public bool logMissingState = true;

    void Start()
    {
        if (doors == null) return;
        for (int i = 0; i < doors.Length; i++)
            doors[i].Init();

        // ถ้าต้องการให้เริ่มต้นส่งสถานะเริ่มต้นทันที ให้เปิดบรรทัดนี้
        // TrySendFromCurrentState();
    }

    void Update()
    {
        if (doors == null) return;

        bool anyChanged = false;
        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i] != null && doors[i].TickAndCheckChanged())
                anyChanged = true;
        }

        if (sendOnEveryChange && anyChanged)
            TrySendFromCurrentState();
    }

    DoorMask GetCurrentMask()
    {
        DoorMask mask = DoorMask.None;
        for (int i = 0; i < doors.Length; i++)
        {
            var d = doors[i];
            if (d != null && d.isOpen)
                mask |= d.maskBit;
        }
        return mask;
    }

    void TrySendFromCurrentState()
    {
        // Gate: ต้องเลือกเมนู Door ก่อน
        if (requireMenuSelectedToSendUart)
        {
            var gate = PanelSelectionGate.Instance;
            if (gate == null || !gate.IsSelected(requiredMenuName))
            {
                if (logBlocked)
                    Debug.LogWarning($"[Door] Block UART: ต้องคลิกเลือก '{requiredMenuName}' ใน SidePanel ก่อน");
                return;
            }
        }

        var serial = SerialAutoPortReader.Instance;
        if (serial == null || !serial.IsOpen)
        {
            if (logBlocked)
                Debug.LogWarning("[Door] Serial not ready/open (SerialAutoPortReader)");
            return;
        }

        DoorMask mask = GetCurrentMask();

        // ✅ เหลือเฉพาะบิตที่เรารู้จัก (31)
        int validBits = (int)DoorMask.Everything;
        int maskNorm = ((int)mask) & validBits;

        string dataHex = null;
        for (int i = 0; i < stateTable.Count; i++)
        {
            // บางที stateTable อาจเป็น -1 (Everything ของ Unity) → & 31 จะกลายเป็น 31
            int stateNorm = ((int)stateTable[i].state) & validBits;

            if (stateNorm == maskNorm)
            {
                dataHex = stateTable[i].dataHex;
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(dataHex))
        {
            if (logMissingState)
                Debug.LogWarning($"[Door] No state mapping for mask={mask} ({(int)mask}). maskNorm={maskNorm} ไม่ส่ง UART");
            return;
        }

        string msg = $"TX STD DATA {canIdHex} {dataHex}";
        serial.FlushBuffers();
        serial.SendLine(msg);

        if (logUart)
            Debug.Log($"[Door] UART Sent: {msg}");
    }

    // เผื่ออยากกดปุ่มเรียกส่งเองจาก UI
    public void ForceSendNow() => TrySendFromCurrentState();
}
