using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using System.Collections;

public class AcceleratorPanelSpeedUI : MonoBehaviour
{
    [Header("Menu Gate (must match SidePanelSimpleList item)")]
    public string acceleratorMenuName = "Accelerator";

    [Header("Car + Speed Text")]
    public WheelColliderCarController car;
    public TMP_Text speedText;

    [Header("Input + Send Button (TMP)")]
    public TMP_InputField speedInput;
    public Button sendButton;

    [Header("Format")]
    public string prefix = "Speed";
    public string unit = "km/h";
    public bool roundToInt = true;

    [Header("Update Rate")]
    [Range(0.02f, 0.5f)]
    public float refreshInterval = 0.05f;

    [Header("UART (optional)")]
    public bool enableUart = true;

    float _timer;
    bool _shown;

    private Color _normalGraphicColor;
    private Color _normalTextColor;
    private Coroutine _flashRoutine;

    // ===== NEW: pending UART send =====
    bool _uartPending = false;
    byte _pendingByte = 0;
    float _lastTargetKmh = 0f;

    IEnumerator FlashInvalidInput()
    {
        if (!speedInput) yield break;

        var g = speedInput.targetGraphic;
        var t = speedInput.textComponent;

        Color red = new Color(1f, 0.25f, 0.25f, 1f);

        for (int i = 0; i < 2; i++)
        {
            if (g) g.color = red;
            if (t) t.color = red;

            yield return new WaitForSecondsRealtime(0.08f);

            if (g) g.color = _normalGraphicColor;
            if (t) t.color = _normalTextColor;

            yield return new WaitForSecondsRealtime(0.08f);
        }

        // ✅ กันค้าง: บังคับคืนสีปกติอีกรอบตอนจบ
        if (g) g.color = _normalGraphicColor;
        if (t) t.color = _normalTextColor;

        _flashRoutine = null;
    }

    SerialAutoPortReader _readerCached;

    void Awake()
    {
        if (!car) car = FindFirstObjectByType<WheelColliderCarController>();

        if (sendButton)
        {
            sendButton.onClick.RemoveListener(OnClickSend);
            sendButton.onClick.AddListener(OnClickSend);
        }
        if (speedInput)
        {
            speedInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            speedInput.characterLimit = 3; // 0-255
        }
        if (speedInput)
        {
            var g = speedInput.targetGraphic;
            if (g) _normalGraphicColor = g.color;

            if (speedInput.textComponent)
                _normalTextColor = speedInput.textComponent.color;
        }

        SetVisible(false);
    }

    void Update()
    {
        bool shouldShow = false;
        if (PanelSelectionGate.Instance != null)
            shouldShow = PanelSelectionGate.Instance.IsSelected(acceleratorMenuName);

        if (shouldShow != _shown)
            SetVisible(shouldShow);

        if (!_shown) return;

        UpdateSpeedText();

        // ✅ NEW: ถ้ามีค่าค้างไว้ ให้ลองส่งเมื่อ "serial พร้อม" + "อยู่เมนู Accelerator"
        TrySendPendingUartIfReady();
    }

    void SetVisible(bool on)
    {
        _shown = on;

        if (speedText) speedText.gameObject.SetActive(on);
        if (speedInput) speedInput.gameObject.SetActive(on);
        if (sendButton) sendButton.gameObject.SetActive(on);

        // ถ้าอยากให้ออกจากเมนูแล้วไม่คุมความเร็วต่อ ให้เปิดบรรทัดนี้
        // if (!on && car) car.ClearExternalTargetSpeed();
    }

    void UpdateSpeedText()
    {
        if (!speedText) return;

        _timer += Time.unscaledDeltaTime;
        if (_timer < refreshInterval) return;
        _timer = 0f;

        float kmh = car ? car.SpeedKmh : 0f;
        if (kmh < 0f) kmh = 0f;

        string value = roundToInt ? Mathf.RoundToInt(kmh).ToString() : kmh.ToString("0.0");
        speedText.text = $"{prefix} {value} {unit}";
    }

    void FlashInvalid()
    {
        if (_flashRoutine != null) StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(FlashInvalidInput());
    }

    public void OnClickSend()
    {
        if (!car)
        {
            Debug.LogWarning("[Accelerator] Car reference not set");
            return;
        }

        if (!speedInput)
        {
            Debug.LogWarning("[Accelerator] speedInput not set");
            return;
        }

        string raw = speedInput.text?.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            FlashInvalid();
            return;
        }

        // ❌ ไม่รับทศนิยมเด็ดขาด
        if (raw.Contains(".") || raw.Contains(","))
        {
            FlashInvalid();
            return;
        }

        // ต้องเป็นจำนวนเต็มเท่านั้น
        if (!int.TryParse(raw, out int v))
        {
            FlashInvalid();
            return;
        }

        if (v < 0 || v > 255)
        {
            FlashInvalid();
            return;
        }

        float targetKmh = v;
        _lastTargetKmh = targetKmh;

        // คุมความเร็วรถทันที (ไม่สน serial)
        car.SetExternalTargetSpeed(targetKmh);

        // เตรียม UART (ค่อยส่งเมื่อ serial พร้อม + เลือก Accelerator)
        _pendingByte = (byte)v;
        _uartPending = true;
        TrySendPendingUartIfReady();


        // ✅ Force to D so external speed can work
        if (car.gearModeEnabled)
        {
            if (car.CurrentGearChar == 'P' || car.CurrentGearChar == 'N')
                car.CurrentGearChar = 'D';
        }

        // ✅ 1) คุมความเร็วรถใน Unity: ทำงานทันที แม้ไม่ต่อ serial
        car.SetExternalTargetSpeed(targetKmh);

        // ✅ 2) จัดเตรียม UART แต่จะไม่ส่งจนกว่า serial พร้อม + เลือก Accelerator
        if (!enableUart) return;

        _pendingByte = (byte)Mathf.RoundToInt(targetKmh);
        _uartPending = true;

        // ลองส่งทันทีถ้าพร้อมอยู่แล้ว
        TrySendPendingUartIfReady();
    }

    void TrySendPendingUartIfReady()
    {
        if (!enableUart) return;
        if (!_uartPending) return;

        // ต้องเลือกเมนู Accelerator อยู่เท่านั้น
        bool isAcceleratorSelected =
            PanelSelectionGate.Instance != null &&
            PanelSelectionGate.Instance.IsSelected(acceleratorMenuName);

        if (!isAcceleratorSelected) return;

        // หา/แคช serial reader
        if (_readerCached == null)
            _readerCached = FindFirstObjectByType<SerialAutoPortReader>();

        // serial ยังไม่พร้อม => ยังไม่ส่ง แต่ยังค้าง pending ไว้
        if (_readerCached == null || !_readerCached.IsOpen) return;

        // พร้อมแล้ว => ส่งครั้งเดียว และเคลียร์ pending
        string msg = $"7B8 03 61 03 {_pendingByte:X2} 00 00 00 00";
        _readerCached.SendLine(msg);

        _uartPending = false;

        Debug.Log($"[Accelerator] UART sent (Auto): target={_lastTargetKmh:0.#} km/h byte={_pendingByte:X2}");
    }
}
