using UnityEngine;
using System;

#if ENABLE_SERIALPORT
using System.IO.Ports;
using System.Text;
#endif

/// <summary>
/// Auto-detect and open the first available COM port (or one that matches PreferredPortContains).
/// Put this on any GameObject (e.g., Canvas) – no UI needed.
/// 
/// IMPORTANT (Unity 6 / modern Unity):
/// - System.IO.Ports is not included by default in some Unity setups.
/// - This script will compile without SerialPort if ENABLE_SERIALPORT is not defined.
///   Once you enable SerialPort support, add Scripting Define Symbol: ENABLE_SERIALPORT.
/// </summary>
public class SerialAutoPortReader : MonoBehaviour
{
    public static SerialAutoPortReader Instance { get; private set; }

    [Header("Serial")]
    [Tooltip("If not empty, will try ports that contain this text first (e.g., COM3 or 'USB').")]
    public string preferredPortContains = "";
    public int baudRate = 115200;

    [Tooltip("Enable DTR/RTS like the example code 2_2.")]
    public bool dtrEnable = true;
    public bool rtsEnable = true;

    [Header("Reconnect")]
    public bool autoReconnect = true;
    public float reconnectIntervalSec = 2f;

    [Header("Read (optional)")]
    public bool logIncomingLines = false;
    public int readTimeoutMs = 200;
    public int writeTimeoutMs = 500;

    public string CurrentPortName => _portName;
    public bool IsOpen => _isOpen;

    string _portName = "";
    float _nextReconnectTime = 0f;
    bool _isOpen = false;

#if ENABLE_SERIALPORT
    SerialPort _sp;
#endif

    void Awake()
    {
        Application.runInBackground = true;

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

#if ENABLE_SERIALPORT
        TryOpenAnyPort();
#else
        Debug.LogWarning("[SerialAutoPortReader] SerialPort not enabled. See fix steps: set Api Compatibility Level to .NET Framework and/or install System.IO.Ports, then add define ENABLE_SERIALPORT.");
#endif
    }

    void Update()
    {
#if ENABLE_SERIALPORT
        if (IsOpen)
        {
            if (logIncomingLines)
            {
                try
                {
                    if (_sp.BytesToRead > 0)
                    {
                        string line = _sp.ReadLine();
                        if (!string.IsNullOrEmpty(line))
                            Debug.Log($"[Serial:{_portName}] {line.Trim()}");
                    }
                }
                catch (TimeoutException) { }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Serial] Read error: {e.Message}");
                    SafeClose();
                }
            }
            return;
        }

        if (autoReconnect && Time.unscaledTime >= _nextReconnectTime)
        {
            _nextReconnectTime = Time.unscaledTime + reconnectIntervalSec;
            TryOpenAnyPort();
        }
#endif
    }

    public bool SendLine(string line)
    {
#if ENABLE_SERIALPORT
        if (!IsOpen) return false;

        try
        {
            _sp.WriteLine(line);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Serial] Write error: {e.Message}");
            SafeClose();
            return false;
        }
#else
        return false;
#endif
    }

    public void FlushBuffers()
    {
#if ENABLE_SERIALPORT
        if (!IsOpen) return;
        try
        {
            _sp.DiscardInBuffer();
            _sp.DiscardOutBuffer();
        }
        catch { }
#endif
    }

    public string[] GetAvailablePorts()
    {
#if ENABLE_SERIALPORT
        return SerialPort.GetPortNames();
#else
        return Array.Empty<string>();
#endif
    }

    public bool TryOpenAnyPort()
    {
#if ENABLE_SERIALPORT
        string[] ports = SerialPort.GetPortNames();
        if (ports == null || ports.Length == 0) return false;

        if (!string.IsNullOrWhiteSpace(preferredPortContains))
        {
            foreach (var p in ports)
            {
                if (p.IndexOf(preferredPortContains, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (TryOpenPort(p)) return true;
                }
            }
        }

        foreach (var p in ports)
        {
            if (TryOpenPort(p)) return true;
        }

        return false;
#else
        return false;
#endif
    }

#if ENABLE_SERIALPORT
    bool TryOpenPort(string port)
    {
        try
        {
            SafeClose();

            _sp = new SerialPort(port, baudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = readTimeoutMs,
                WriteTimeout = writeTimeoutMs,
                NewLine = "\n",
                DtrEnable = dtrEnable,
                RtsEnable = rtsEnable,
                Handshake = Handshake.None,
                Encoding = Encoding.ASCII
            };

            _sp.Open();
            _sp.DiscardInBuffer();
            _sp.DiscardOutBuffer();

            _portName = port;
            _isOpen = true;

            Debug.Log($"[Serial] Opened {port} @ {baudRate}. AllPorts={string.Join(",", SerialPort.GetPortNames())}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Serial] Open failed on {port}: {e.Message}");
            SafeClose();
            return false;
        }
    }

    void SafeClose()
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
        _portName = "";
        _isOpen = false;
    }

    void OnApplicationQuit() => SafeClose();
#endif
}